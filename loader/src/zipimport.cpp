#include "zipimport.hpp"
#include "zipfile.hpp"
#include "util.hpp"

#include <algorithm>
#include <atomic>
#include <ctype.h>
#include <dirent.h>
#include <errno.h>
#include <mutex>
#include <new>
#include <pthread.h>
#include <string.h>
#include <sys/stat.h>
#include <unistd.h>

// Safety budget for the "zip bomb" case: refuse to unpack more than this in
// total (a level pack is a few MB; a few hundred MB is already generous).
static const uint64_t kTotalBudget = 4ull * 1024 * 1024 * 1024;

// ------------------------------------------------------------------ state

static std::mutex g_mu;
static int g_state = ZIPIMP_IDLE;
static int g_error = ZIPERR_NONE;
static std::string g_error_detail;
static int g_done = 0;
static int g_total = 0;
static std::string g_current;
static std::vector<std::string> g_abs;    // absolute .adofai paths
static std::vector<std::string> g_rel;    // same, relative to the root
static std::atomic<bool> g_cancel{false};

static void set_state(int st) {
    std::lock_guard<std::mutex> lk(g_mu);
    g_state = st;
}

static void set_failed(int code, const std::string& detail) {
    std::lock_guard<std::mutex> lk(g_mu);
    g_state = ZIPIMP_FAILED;
    g_error = code;
    g_error_detail = detail;
    g_current.clear();
}

// ------------------------------------------------------------------ fs helpers

static bool mkdir_p(const std::string& path) {
    if (path.empty()) return false;
    std::string cur;
    cur.reserve(path.size());
    size_t i = 0;
    if (path[0] == '/') { cur = "/"; i = 1; }
    while (i <= path.size()) {
        size_t slash = path.find('/', i);
        const std::string comp = path.substr(i, slash == std::string::npos
                                                   ? std::string::npos
                                                   : slash - i);
        if (!comp.empty()) {
            if (!cur.empty() && cur.back() != '/') cur += '/';
            cur += comp;
            if (mkdir(cur.c_str(), 0777) != 0 && errno != EEXIST) {
                struct stat st;
                if (stat(cur.c_str(), &st) != 0 || !S_ISDIR(st.st_mode)) return false;
            }
        }
        if (slash == std::string::npos) break;
        i = slash + 1;
    }
    struct stat st;
    return stat(path.c_str(), &st) == 0 && S_ISDIR(st.st_mode);
}

// Recursively deletes a file or directory (symlinks are unlinked, never
// followed, so a hostile archive cannot escape the scratch root through one).
static void remove_recursive(const std::string& path) {
    struct stat st;
    if (lstat(path.c_str(), &st) != 0) return;
    if (S_ISDIR(st.st_mode)) {
        DIR* d = opendir(path.c_str());
        if (d) {
            struct dirent* de;
            while ((de = readdir(d)) != nullptr) {
                if (strcmp(de->d_name, ".") == 0 || strcmp(de->d_name, "..") == 0) continue;
                remove_recursive(path + "/" + de->d_name);
            }
            closedir(d);
        }
        rmdir(path.c_str());
        return;
    }
    unlink(path.c_str());
}

static std::string parent_of(const std::string& path) {
    size_t pos = path.rfind('/');
    if (pos == std::string::npos || pos == 0) return std::string("/");
    return path.substr(0, pos);
}

// ------------------------------------------------------------------ names

// Turns a stored entry name into a safe relative path inside the scratch root:
//  - '\' becomes '/' (archives written on Windows)
//  - a leading '/' is dropped (the entry stays inside the root)
//  - "." components are dropped, ".." makes the whole entry invalid
//  - drive letters ("C:...") and control characters are rejected
static bool sanitize_rel(const std::string& raw, std::string* out) {
    std::string s;
    s.reserve(raw.size());
    for (size_t i = 0; i < raw.size(); i++) s += (raw[i] == '\\') ? '/' : raw[i];

    size_t lead = 0;
    while (lead < s.size() && s[lead] == '/') lead++;
    if (lead) s.erase(0, lead);

    if (s.size() >= 2 && isalpha((unsigned char)s[0]) && s[1] == ':') return false;

    std::string res;
    size_t start = 0;
    while (start <= s.size()) {
        size_t slash = s.find('/', start);
        const std::string comp = s.substr(start, slash == std::string::npos
                                                    ? std::string::npos
                                                    : slash - start);
        start = (slash == std::string::npos) ? s.size() + 1 : slash + 1;
        if (comp.empty() || comp == ".") continue;
        if (comp == "..") return false;
        for (size_t k = 0; k < comp.size(); k++) {
            if ((unsigned char)comp[k] < 0x20) return false;
        }
        if (!res.empty()) res += '/';
        res += comp;
    }
    if (res.empty() || res.size() > 900) return false;
    *out = res;
    return true;
}

// Archive metadata that is never a level asset: macOS resource forks, the
// __MACOSX/ side tree and the usual OS junk files.
static bool is_junk_entry(const std::string& name) {
    if (name.compare(0, 9, "__MACOSX/") == 0) return true;
    const size_t pos = name.rfind('/');
    const std::string base = (pos == std::string::npos) ? name : name.substr(pos + 1);
    if (base.size() >= 2 && base[0] == '.' && base[1] == '_') return true;
    if (base == ".DS_Store" || base == "Thumbs.db" || base == "desktop.ini") return true;
    return false;
}

// Archives written by Windows tools on a CJK locale store names as GBK
// bytes, which then never match the UTF-8 names inside the .adofai JSON.
// We cannot convert them without a code page table, so at least say so.
static bool is_valid_utf8(const std::string& s) {
    size_t i = 0;
    while (i < s.size()) {
        const unsigned char c = (unsigned char)s[i];
        size_t extra;
        uint32_t cp;
        if (c < 0x80) { i++; continue; }
        if ((c & 0xE0) == 0xC0) { extra = 1; cp = c & 0x1F; }
        else if ((c & 0xF0) == 0xE0) { extra = 2; cp = c & 0x0F; }
        else if ((c & 0xF8) == 0xF0) { extra = 3; cp = c & 0x07; }
        else return false;
        if (i + extra >= s.size()) return false;
        for (size_t k = 1; k <= extra; k++) {
            const unsigned char cc = (unsigned char)s[i + k];
            if ((cc & 0xC0) != 0x80) return false;
            cp = (cp << 6) | (cc & 0x3F);
        }
        if ((extra == 1 && cp < 0x80) || (extra == 2 && cp < 0x800) ||
            (extra == 3 && cp < 0x10000) || cp > 0x10FFFF) return false;
        i += extra + 1;
    }
    return true;
}

static bool ends_with_ci(const std::string& s, const char* suffix) {
    const size_t n = strlen(suffix);
    if (s.size() < n) return false;
    for (size_t i = 0; i < n; i++) {
        char a = s[s.size() - n + i], b = suffix[i];
        if (a >= 'A' && a <= 'Z') a += 32;
        if (b >= 'A' && b <= 'Z') b += 32;
        if (a != b) return false;
    }
    return true;
}

// ------------------------------------------------------------------ worker

static void import_main(std::string zip_path, std::string root) {
    LOGI("zip: importing %s -> %s", zip_path.c_str(), root.c_str());

    remove_recursive(root);
    if (!mkdir_p(root)) {
        set_failed(ZIPERR_DISK, root);
        return;
    }

    ZipReader zr;
    std::string err;
    if (!zr.open(zip_path.c_str(), &err)) {
        set_failed(ZIPERR_OPEN, err);
        return;
    }

    // Everything below only touches plain files: directories, junk metadata,
    // encrypted entries and unsupported compression methods are skipped.
    struct Job {
        const ZipEntry* ent;
        std::string rel;
        std::string abs;
    };
    std::vector<Job> jobs;
    int skipped = 0;
    int non_utf8 = 0;
    for (const ZipEntry& e : zr.entries()) {
        if (ZipReader::is_dir_entry(e) || is_junk_entry(e.name)) continue;
        std::string rel;
        if (!sanitize_rel(e.name, &rel)) {
            LOGW("zip: skipping unsafe entry '%s'", e.name.c_str());
            skipped++;
            continue;
        }
        if (e.flags & 0x0001) {
            LOGW("zip: skipping encrypted entry '%s'", rel.c_str());
            skipped++;
            continue;
        }
        if (e.method != 0 && e.method != 8) {
            LOGW("zip: skipping entry with compression method %u ('%s')",
                 (unsigned)e.method, rel.c_str());
            skipped++;
            continue;
        }
        if (!is_valid_utf8(rel)) non_utf8++;
        jobs.push_back(Job{&e, rel, root + "/" + rel});
    }
    if (skipped) LOGW("zip: %d entries skipped", skipped);
    if (non_utf8) {
        LOGW("zip: %d entry names are not UTF-8 (archives zipped by a CJK-locale "
             "Windows tool may not find their song/background files)", non_utf8);
    }

    {
        std::lock_guard<std::mutex> lk(g_mu);
        g_total = (int)jobs.size();
        g_done = 0;
    }

    uint64_t budget = kTotalBudget;
    for (const Job& j : jobs) {
        if (g_cancel.load()) {
            LOGI("zip: cancelled, cleaning up");
            remove_recursive(root);
            set_state(ZIPIMP_IDLE);
            return;
        }
        if (j.ent->uncomp_size > budget) {
            set_failed(ZIPERR_TOO_BIG, j.rel);
            return;
        }
        budget -= j.ent->uncomp_size;

        {
            std::lock_guard<std::mutex> lk(g_mu);
            g_current = j.rel;
        }
        if (!mkdir_p(parent_of(j.abs))) {
            set_failed(ZIPERR_DISK, j.rel);
            return;
        }
        if (!zr.extract(*j.ent, j.abs.c_str(), &err)) {
            set_failed(ZIPERR_EXTRACT, j.rel + ": " + err);
            return;
        }
        if (ends_with_ci(j.rel, ".adofai")) {
            std::lock_guard<std::mutex> lk(g_mu);
            g_abs.push_back(j.abs);
            g_rel.push_back(j.rel);
        }
        std::lock_guard<std::mutex> lk(g_mu);
        g_done++;
    }

    {
        std::lock_guard<std::mutex> lk(g_mu);
        // Sort the levels (and their paths along with them) by relative name.
        std::vector<size_t> order(g_rel.size());
        for (size_t i = 0; i < order.size(); i++) order[i] = i;
        std::sort(order.begin(), order.end(),
                  [](size_t a, size_t b) { return g_rel[a] < g_rel[b]; });
        std::vector<std::string> abs_sorted, rel_sorted;
        abs_sorted.reserve(order.size());
        rel_sorted.reserve(order.size());
        for (size_t idx : order) {
            abs_sorted.push_back(g_abs[idx]);
            rel_sorted.push_back(g_rel[idx]);
        }
        g_abs.swap(abs_sorted);
        g_rel.swap(rel_sorted);

        g_current.clear();
        if (g_rel.empty()) {
            g_state = ZIPIMP_FAILED;
            g_error = ZIPERR_NO_LEVEL;
        } else {
            g_state = ZIPIMP_READY;
            LOGI("zip: %d level(s) found", (int)g_rel.size());
        }
    }
}

// ------------------------------------------------------------------ public

// The worker is started with pthread_create (like the JNI bootstrap in
// main.cpp) instead of std::thread: the static libc++abi in this build is a
// musl one, so we stay clear of the C++ exception machinery std::thread pulls
// in.
struct ImportJob {
    std::string zip_path;
    std::string root;
};

static void* import_thread(void* p) {
    ImportJob* job = (ImportJob*)p;
    import_main(job->zip_path, job->root);
    delete job;
    return nullptr;
}

bool zip_import_begin(const char* zip_path, const char* work_root) {
    if (!zip_path || !zip_path[0] || !work_root || !work_root[0]) return false;
    {
        std::lock_guard<std::mutex> lk(g_mu);
        if (g_state == ZIPIMP_WORKING) return false;
        g_state = ZIPIMP_WORKING;
        g_error = ZIPERR_NONE;
        g_error_detail.clear();
        g_done = 0;
        g_total = 0;
        g_current.clear();
        g_abs.clear();
        g_rel.clear();
    }
    g_cancel.store(false);

    ImportJob* job = new (std::nothrow) ImportJob;
    if (!job) {
        set_failed(ZIPERR_BUSY, "out of memory");
        return false;
    }
    job->zip_path = zip_path;
    job->root = work_root;

    pthread_t th;
    pthread_attr_t attr;
    pthread_attr_init(&attr);
    pthread_attr_setdetachstate(&attr, PTHREAD_CREATE_DETACHED);
    const int rc = pthread_create(&th, &attr, import_thread, job);
    pthread_attr_destroy(&attr);
    if (rc != 0) {
        delete job;
        set_failed(ZIPERR_BUSY, "cannot spawn worker thread");
        return false;
    }
    return true;
}

void zip_import_cancel() { g_cancel.store(true); }

int zip_import_state() {
    std::lock_guard<std::mutex> lk(g_mu);
    return g_state;
}

void zip_import_progress(int* done, int* total, std::string* current) {
    std::lock_guard<std::mutex> lk(g_mu);
    if (done) *done = g_done;
    if (total) *total = g_total;
    if (current) *current = g_current;
}

void zip_import_levels(std::vector<std::string>* abs_paths,
                       std::vector<std::string>* rel_names) {
    std::lock_guard<std::mutex> lk(g_mu);
    if (abs_paths) *abs_paths = g_abs;
    if (rel_names) *rel_names = g_rel;
}

int zip_import_error_code() {
    std::lock_guard<std::mutex> lk(g_mu);
    return g_error;
}

void zip_import_error_detail(std::string* out) {
    std::lock_guard<std::mutex> lk(g_mu);
    if (out) *out = g_error_detail;
}

void zip_import_reset() {
    std::lock_guard<std::mutex> lk(g_mu);
    if (g_state == ZIPIMP_WORKING) return;  // the worker owns the state
    g_state = ZIPIMP_IDLE;
    g_error = ZIPERR_NONE;
    g_error_detail.clear();
    g_done = 0;
    g_total = 0;
    g_current.clear();
    g_abs.clear();
    g_rel.clear();
}
