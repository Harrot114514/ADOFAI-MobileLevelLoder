#include "util.hpp"
#include <android/log.h>
#include <link.h>
#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>
#include <sys/stat.h>
#include <errno.h>
#include <mutex>

// ------------------------------------------------------------------ logging
static std::mutex g_log_mutex;
static FILE* g_log_file = nullptr;
static bool g_log_tried = false;

static char g_data_dir[512] = "";

static void resolve_data_dir() {
    if (g_data_dir[0]) return;
    char cmdline[256] = "";
    FILE* f = fopen("/proc/self/cmdline", "rb");
    if (f) {
        size_t n = fread(cmdline, 1, sizeof(cmdline) - 1, f);
        fclose(f);
        cmdline[n] = 0;
    }
    const char* pkg = cmdline[0] ? cmdline : "com.fizzd.connectedworlds";
    snprintf(g_data_dir, sizeof(g_data_dir),
             "/storage/emulated/0/Android/data/%s/files", pkg);
}

const char* get_data_dir() {
    resolve_data_dir();
    return g_data_dir;
}

static void log_file_open() {
    if (g_log_file || g_log_tried) return;
    g_log_tried = true;
    resolve_data_dir();
    char dir[700];
    snprintf(dir, sizeof(dir), "%s/log", g_data_dir);
    char base[600];
    snprintf(base, sizeof(base), "%s", g_data_dir);
    
    // 创建目录，并检查错误
    if (mkdir(base, 0777) != 0 && errno != EEXIST) {
        __android_log_print(ANDROID_LOG_WARN, TAG, "mkdir %s failed: %s", base, strerror(errno));
    }
    if (mkdir(dir, 0777) != 0 && errno != EEXIST) {
        __android_log_print(ANDROID_LOG_WARN, TAG, "mkdir %s failed: %s", dir, strerror(errno));
    }
    
    char path[800];
    snprintf(path, sizeof(path), "%s/adofailoader.log", dir);
    g_log_file = fopen(path, "a");
    if (!g_log_file) {
        __android_log_print(ANDROID_LOG_WARN, TAG, "cannot open log file %s: %s", path, strerror(errno));
    }
}

void log_line(int level, const char* fmt, ...) {
    char msg[1024];
    va_list ap;
    va_start(ap, fmt);
    vsnprintf(msg, sizeof(msg), fmt, ap);
    va_end(ap);

    __android_log_print(level == LOG_ERROR ? ANDROID_LOG_ERROR :
                        level == LOG_WARN ? ANDROID_LOG_WARN : ANDROID_LOG_INFO,
                        TAG, "%s", msg);

    std::lock_guard<std::mutex> lk(g_log_mutex);
    if (!g_log_file) log_file_open();
    if (g_log_file) {
        struct timespec ts;
        clock_gettime(CLOCK_REALTIME, &ts);
        struct tm tmv;
        localtime_r(&ts.tv_sec, &tmv);
        char stamp[32];
        strftime(stamp, sizeof(stamp), "%Y-%m-%d %H:%M:%S", &tmv);
        fprintf(g_log_file, "[%s.%03ld] [%s] %s\n", stamp, ts.tv_nsec / 1000000,
                level == LOG_ERROR ? "E" : level == LOG_WARN ? "W" : "I", msg);
        fflush(g_log_file);
    }
}

void log_info(const char* fmt, ...) {
    char msg[1024];
    va_list ap; va_start(ap, fmt);
    vsnprintf(msg, sizeof(msg), fmt, ap);
    va_end(ap);
    log_line(LOG_INFO, "%s", msg);
}
void log_error(const char* fmt, ...) {
    char msg[1024];
    va_list ap; va_start(ap, fmt);
    vsnprintf(msg, sizeof(msg), fmt, ap);
    va_end(ap);
    log_line(LOG_ERROR, "%s", msg);
}

void log_clear() {
    {
        std::lock_guard<std::mutex> lk(g_log_mutex);
        if (g_log_file) { fclose(g_log_file); g_log_file = nullptr; }
    }
    char path[700];
    snprintf(path, sizeof(path), "%s/log/adofailoader.log", g_data_dir);
    remove(path);
    log_line(LOG_INFO, "log: cleared");
}

static uint64_t g_found_base = 0;
static const char* g_find_name = nullptr;

static int dl_callback(struct dl_phdr_info* info, size_t size, void* data) {
    (void)size; (void)data;
    const char* name = info->dlpi_name;
    if (!name || !name[0]) return 0;
    const char* base = strrchr(name, '/');
    base = base ? base + 1 : name;
    if (strcmp(base, g_find_name) == 0) {
        g_found_base = (uint64_t)info->dlpi_addr;
        return 1;
    }
    return 0;
}

uint64_t find_module_base(const char* name) {
    g_found_base = 0;
    g_find_name = name;
    dl_iterate_phdr(dl_callback, nullptr);
    return g_found_base;
}

uint8_t* read_file(const char* path, size_t* out_size) {
    FILE* f = fopen(path, "rb");
    if (!f) return nullptr;
    fseek(f, 0, SEEK_END);
    long sz = ftell(f);
    fseek(f, 0, SEEK_SET);
    if (sz <= 0) { fclose(f); return nullptr; }
    uint8_t* buf = (uint8_t*)malloc((size_t)sz);
    if (!buf) {
        LOGE("read_file: malloc %ld bytes failed", sz);
        fclose(f);
        return nullptr;
    }
    size_t rd = fread(buf, 1, (size_t)sz, f);
    fclose(f);
    if (rd != (size_t)sz) {
        LOGE("read_file: read %zu/%ld bytes", rd, sz);
        free(buf);
        return nullptr;
    }
    if (out_size) *out_size = (size_t)sz;
    return buf;
}