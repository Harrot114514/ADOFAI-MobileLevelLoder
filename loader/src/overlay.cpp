#include "overlay.hpp"
#include "input.hpp"
#include "util.hpp"
#include "game.hpp"
#include "zipimport.hpp"

#include "imgui.h"
#include "imgui_impl_opengl3.h"
#include "font_embedded.h"

#include <dirent.h>
#include <sys/stat.h>
#include <unistd.h>
#include <string.h>
#include <stdio.h>
#include <stdlib.h>
#include <string>
#include <vector>
#include <algorithm>
#include <atomic>

// ------------------------------------------------------------------ i18n
// Chinese is the default. English strings are provided (interface reserved).

enum Lang { LANG_ZH, LANG_EN };
static Lang g_lang = LANG_ZH;

#define S(zh, en) (g_lang == LANG_ZH ? (zh) : (en))

// ------------------------------------------------------------------ theme
// Two themes (dark default / light) sharing the same accent: the violet of
// the biuret reaction (双缩脲试剂 + 豆浆 → 紫色).

enum Theme { THEME_DARK, THEME_LIGHT };
static Theme g_theme = THEME_DARK;
static bool g_nofail_setting = false;   // 不败模式
static int  g_diff_setting = 1;         // 难度: 0=宽松 1=普通 2=严格

static const ImVec4 kViolet       = ImVec4(0.55f, 0.36f, 0.71f, 1.0f);
static const ImVec4 kVioletHover  = ImVec4(0.65f, 0.47f, 0.80f, 1.0f);
static const ImVec4 kVioletActive = ImVec4(0.45f, 0.29f, 0.60f, 1.0f);

// destructive-action red (delete button / confirm dialog)
static const ImVec4 kDel       = ImVec4(0.72f, 0.22f, 0.25f, 1.0f);
static const ImVec4 kDelHover  = ImVec4(0.82f, 0.30f, 0.32f, 1.0f);
static const ImVec4 kDelActive = ImVec4(0.62f, 0.18f, 0.22f, 1.0f);

static void apply_theme(float scale) {
    ImGuiStyle& st = ImGui::GetStyle();
    st = ImGuiStyle(); // reset defaults
    st.WindowRounding    = 10.0f * scale;
    st.ChildRounding     = 8.0f * scale;
    st.FrameRounding     = 7.0f * scale;
    st.PopupRounding     = 7.0f * scale;
    st.ScrollbarRounding = 8.0f * scale;
    st.GrabRounding      = 7.0f * scale;
    st.TabRounding       = 6.0f * scale;
    st.WindowBorderSize  = 0.0f;
    st.FramePadding      = ImVec2(10 * scale, 6 * scale);
    st.ItemSpacing       = ImVec2(8 * scale, 8 * scale);
    st.ItemInnerSpacing  = ImVec2(6 * scale, 4 * scale);
    st.WindowPadding     = ImVec2(14 * scale, 12 * scale);
    st.ScrollbarSize     = 14 * scale;

    ImVec4* c = st.Colors;
    if (g_theme == THEME_DARK) {
        // warm dark palette, biuret-violet tint throughout
        c[ImGuiCol_WindowBg]         = ImVec4(0.10f, 0.10f, 0.12f, 0.96f);
        c[ImGuiCol_ChildBg]          = ImVec4(0.07f, 0.07f, 0.09f, 1.00f);
        c[ImGuiCol_PopupBg]          = ImVec4(0.13f, 0.12f, 0.16f, 0.98f);
        c[ImGuiCol_Border]           = ImVec4(0.34f, 0.30f, 0.42f, 0.60f);
        c[ImGuiCol_Text]             = ImVec4(0.93f, 0.92f, 0.95f, 1.00f);
        c[ImGuiCol_TextDisabled]     = ImVec4(0.58f, 0.55f, 0.64f, 1.00f);
        c[ImGuiCol_TextSelectedBg]   = ImVec4(0.55f, 0.36f, 0.71f, 0.55f);

        c[ImGuiCol_FrameBg]          = ImVec4(0.18f, 0.16f, 0.22f, 1.00f);
        c[ImGuiCol_FrameBgHovered]   = ImVec4(0.24f, 0.21f, 0.28f, 1.00f);
        c[ImGuiCol_FrameBgActive]    = ImVec4(0.28f, 0.24f, 0.32f, 1.00f);

        c[ImGuiCol_TitleBg]          = ImVec4(0.16f, 0.14f, 0.20f, 1.00f);
        c[ImGuiCol_TitleBgActive]    = ImVec4(0.22f, 0.18f, 0.28f, 1.00f);
        c[ImGuiCol_TitleBgCollapsed] = ImVec4(0.16f, 0.14f, 0.20f, 1.00f);

        c[ImGuiCol_Header]           = ImVec4(0.24f, 0.20f, 0.30f, 1.00f);
        c[ImGuiCol_HeaderHovered]    = ImVec4(0.32f, 0.26f, 0.38f, 1.00f);
        c[ImGuiCol_HeaderActive]     = ImVec4(0.38f, 0.30f, 0.44f, 1.00f);

        c[ImGuiCol_Button]           = ImVec4(0.26f, 0.22f, 0.32f, 1.00f);
        c[ImGuiCol_ButtonHovered]    = ImVec4(0.34f, 0.28f, 0.40f, 1.00f);
        c[ImGuiCol_ButtonActive]     = ImVec4(0.40f, 0.32f, 0.46f, 1.00f);

        c[ImGuiCol_ScrollbarBg]      = ImVec4(0.10f, 0.10f, 0.12f, 1.00f);
        c[ImGuiCol_ScrollbarGrab]    = ImVec4(0.34f, 0.28f, 0.42f, 1.00f);
        c[ImGuiCol_ScrollbarGrabHovered] = ImVec4(0.46f, 0.38f, 0.54f, 1.00f);
        c[ImGuiCol_ScrollbarGrabActive]  = ImVec4(0.58f, 0.46f, 0.66f, 1.00f);

        c[ImGuiCol_Separator]        = ImVec4(0.36f, 0.30f, 0.44f, 0.80f);
        c[ImGuiCol_TableHeaderBg]    = ImVec4(0.18f, 0.15f, 0.22f, 1.00f);
        c[ImGuiCol_TableBorderStrong]= ImVec4(0.34f, 0.28f, 0.42f, 0.6f);
        c[ImGuiCol_TableBorderLight] = ImVec4(0.26f, 0.22f, 0.32f, 0.6f);
    } else {
        // light palette, biuret-violet accent
        c[ImGuiCol_WindowBg]         = ImVec4(0.94f, 0.93f, 0.95f, 0.97f);
        c[ImGuiCol_ChildBg]          = ImVec4(0.98f, 0.97f, 0.99f, 1.00f);
        c[ImGuiCol_PopupBg]          = ImVec4(0.95f, 0.94f, 0.96f, 0.99f);
        c[ImGuiCol_Border]           = ImVec4(0.55f, 0.50f, 0.65f, 0.60f);
        c[ImGuiCol_Text]             = ImVec4(0.18f, 0.16f, 0.25f, 1.00f);
        c[ImGuiCol_TextDisabled]     = ImVec4(0.45f, 0.43f, 0.52f, 1.00f);
        c[ImGuiCol_TextSelectedBg]   = ImVec4(0.55f, 0.36f, 0.71f, 0.45f);

        c[ImGuiCol_FrameBg]          = ImVec4(0.86f, 0.85f, 0.90f, 1.00f);
        c[ImGuiCol_FrameBgHovered]   = ImVec4(0.81f, 0.80f, 0.87f, 1.00f);
        c[ImGuiCol_FrameBgActive]    = ImVec4(0.78f, 0.76f, 0.85f, 1.00f);

        c[ImGuiCol_TitleBg]          = ImVec4(0.87f, 0.84f, 0.92f, 1.00f);
        c[ImGuiCol_TitleBgActive]    = ImVec4(0.80f, 0.76f, 0.88f, 1.00f);
        c[ImGuiCol_TitleBgCollapsed] = ImVec4(0.87f, 0.84f, 0.92f, 1.00f);

        c[ImGuiCol_Header]           = ImVec4(0.85f, 0.81f, 0.90f, 1.00f);
        c[ImGuiCol_HeaderHovered]    = ImVec4(0.80f, 0.75f, 0.87f, 1.00f);
        c[ImGuiCol_HeaderActive]     = ImVec4(0.76f, 0.70f, 0.84f, 1.00f);

        c[ImGuiCol_Button]           = ImVec4(0.83f, 0.81f, 0.88f, 1.00f);
        c[ImGuiCol_ButtonHovered]    = ImVec4(0.88f, 0.85f, 0.93f, 1.00f);
        c[ImGuiCol_ButtonActive]     = ImVec4(0.76f, 0.73f, 0.84f, 1.00f);

        c[ImGuiCol_ScrollbarBg]      = ImVec4(0.93f, 0.92f, 0.95f, 1.00f);
        c[ImGuiCol_ScrollbarGrab]    = ImVec4(0.68f, 0.66f, 0.75f, 1.00f);
        c[ImGuiCol_ScrollbarGrabHovered] = ImVec4(0.60f, 0.56f, 0.68f, 1.00f);
        c[ImGuiCol_ScrollbarGrabActive]  = ImVec4(0.50f, 0.46f, 0.60f, 1.00f);

        c[ImGuiCol_Separator]        = ImVec4(0.62f, 0.58f, 0.70f, 0.80f);
        c[ImGuiCol_TableHeaderBg]    = ImVec4(0.84f, 0.81f, 0.90f, 1.00f);
        c[ImGuiCol_TableBorderStrong]= ImVec4(0.60f, 0.56f, 0.70f, 0.6f);
        c[ImGuiCol_TableBorderLight] = ImVec4(0.72f, 0.69f, 0.80f, 0.6f);
    }

    // shared accent (biuret violet)
    c[ImGuiCol_CheckMark]            = kViolet;
    c[ImGuiCol_SliderGrab]           = kViolet;
    c[ImGuiCol_SliderGrabActive]     = kVioletHover;
    c[ImGuiCol_SeparatorHovered]     = kViolet;
    c[ImGuiCol_SeparatorActive]      = kViolet;
    c[ImGuiCol_TableRowBg]           = ImVec4(0.0f, 0.0f, 0.0f, 0.0f);
    c[ImGuiCol_TableRowBgAlt]        = ImVec4(1.0f, 1.0f, 1.0f, 0.03f);
}

// ------------------------------------------------------------------ fonts

static const char* kSystemFonts[] = {
    "/system/fonts/NotoSansCJK-Regular.ttc",
    "/system/fonts/NotoSansSC-Regular.otf",
    "/system/fonts/NotoSansCJKsc-Regular.otf",
    "/system/fonts/MiSans-Regular.ttf",
    "/system/fonts/MiSans-Demibold.ttf",
    "/system/fonts/DroidSansFallback.ttf",
    "/system/fonts/DroidSansFallbackFull.ttf",
    "/system/fonts/OPPOSans-Regular.ttf",
    "/system/fonts/OPPOSansOS2-Regular.ttf",
    "/system/fonts/HarmonyOS_Sans_SC_Regular.ttf",
    "/system/fonts/Roboto-Regular.ttf",
};

static bool file_exists(const char* p) {
    struct stat st;
    return stat(p, &st) == 0;
}

// glyph ranges: ASCII + latin + CJK punctuation/radicals + CJK unified + fullwidth
static const ImWchar kRanges[] = {
    0x0020, 0x00FF,
    0x2000, 0x206F,
    0x2190, 0x21FF,
    0x2500, 0x26FF,
    0x3000, 0x30FF,
    0x3400, 0x4DBF,
    0x4E00, 0x9FFF,
    0xFF00, 0xFFEF,
    0,
};

// Validate that a font file is stb_truetype-compatible (TrueType outlines).
// Some OEM devices ship NotoSansCJK as CFF/OTTO fonts which stb cannot parse
// and ImGui's font loader asserts -> crash. Only accept 0x00010000 (TTF) or
// ttcf collections whose first font is TrueType.
static bool font_file_supported(const char* path) {
    FILE* f = fopen(path, "rb");
    if (!f) return false;
    unsigned char hdr[12];
    size_t n = fread(hdr, 1, 12, f);
    fclose(f);
    if (n < 12) return false;
    uint32_t tag = ((uint32_t)hdr[0] << 24) | ((uint32_t)hdr[1] << 16) |
                   ((uint32_t)hdr[2] << 8) | (uint32_t)hdr[3];
    if (tag == 0x00010000) return true; // plain TTF
    if (tag != 0x74746366 /*'ttcf'*/) return false;
    uint32_t off = ((uint32_t)hdr[12] << 24) | ((uint32_t)hdr[13] << 16) |
                   ((uint32_t)hdr[14] << 8) | (uint32_t)hdr[15];
    f = fopen(path, "rb");
    if (!f) return false;
    if (fseek(f, (long)off, SEEK_SET) != 0) { fclose(f); return false; }
    unsigned char h2[4];
    n = fread(h2, 1, 4, f);
    fclose(f);
    if (n < 4) return false;
    uint32_t t2 = ((uint32_t)h2[0] << 24) | ((uint32_t)h2[1] << 16) |
                  ((uint32_t)h2[2] << 8) | (uint32_t)h2[3];
    return t2 == 0x00010000;
}

#include <dirent.h>

// Try to load a system CJK fallback: first the known paths, then scan the
// common font directories for candidates (some OEMs put their CJK font at
// non-standard paths, which left dynamic strings like file names as "?").
static bool try_load_system_font(float px, const ImFontConfig& base) {
    ImGuiIO& io = ImGui::GetIO();
    // 1) known paths
    for (const char* p : kSystemFonts) {
        if (file_exists(p) && font_file_supported(p)) {
            ImFontConfig cfg2 = base;
            cfg2.MergeMode = true;
            cfg2.FontDataOwnedByAtlas = false;
            io.Fonts->AddFontFromFileTTF(p, px, &cfg2, kRanges);
            LOGI("overlay: using system font %s", p);
            return true;
        }
    }
    // 2) scan font dirs for CJK-ish candidates
    static const char* kDirs[] = {
        "/system/fonts", "/system/fonts/vendor", "/product/fonts",
        "/my_product/fonts", "/my_stock/fonts", "/system/fonts/zh",
    };
    static const char* kKws[] = {
        "cjk", "CJK", "NotoSans", "MiSans", "OPPO", "Harmony", "SourceHan",
        "DroidSansFallback", "Vivo", "HONOR", "OnePlus", "Meizu",
    };
    for (const char* dir : kDirs) {
        DIR* d = opendir(dir);
        if (!d) continue;
        struct dirent* e;
        while ((e = readdir(d)) != nullptr) {
            const char* name = e->d_name;
            size_t n = strlen(name);
            if (n < 5) continue;
            const char* ext = name + n - 4;
            if (strcmp(ext, ".ttf") != 0 && strcmp(ext, ".ttc") != 0 &&
                strcmp(ext, ".TTF") != 0 && strcmp(ext, ".TTC") != 0) continue;
            bool match = false;
            for (const char* kw : kKws)
                if (strstr(name, kw)) { match = true; break; }
            if (!match) continue;
            char full[600];
            snprintf(full, sizeof(full), "%s/%s", dir, name);
            if (!font_file_supported(full)) continue;
            ImFontConfig cfg2 = base;
            cfg2.MergeMode = true;
            cfg2.FontDataOwnedByAtlas = false;
            io.Fonts->AddFontFromFileTTF(full, px, &cfg2, kRanges);
            LOGI("overlay: using scanned font %s", full);
            closedir(d);
            return true;
        }
        closedir(d);
    }
    LOGE("overlay: no usable system CJK font found (dynamic text may show '?')");
    return false;
}

static void load_fonts(float px) {
    ImGuiIO& io = ImGui::GetIO();
    ImFontConfig cfg;
    cfg.FontDataOwnedByAtlas = false;
    cfg.OversampleH = 2; cfg.OversampleV = 1;
    // primary: embedded subset (guaranteed to render all UI text)
    io.Fonts->AddFontFromMemoryTTF((void*)g_font_subset_ttf, (int)g_font_subset_ttf_len,
                                   px, &cfg, kRanges);
    // fallback: system CJK font for dynamic text (file names etc.)
    try_load_system_font(px, cfg);
}

// ------------------------------------------------------------------ state

static bool  g_inited = false;
static bool  g_open = false;
static float g_scale = 1.0f;

static char g_cwd[1024] = "";
static char g_default_dir[512] = "";

// Runtime-resolved default dir (game data dir root, derived from
// /proc/self/cmdline) - no hard-coded package name.
static void init_default_dir() {
    if (g_default_dir[0]) return;
    char base[512];
    snprintf(base, sizeof(base), "%s", get_data_dir()); // .../Android/data/<pkg>/files
    size_t n = strlen(base);
    if (n > 6 && strcmp(base + n - 6, "/files") == 0) base[n - 6] = 0;
    snprintf(g_default_dir, sizeof(g_default_dir), "%s/", base);
    if (!g_cwd[0]) snprintf(g_cwd, sizeof(g_cwd), "%s", g_default_dir);
}

struct FsEntry {
    std::string name;
    bool is_dir = false;
    bool is_level = false;
    bool is_zip = false;
};
static std::vector<FsEntry> g_entries;
static int g_selected = -1;
static bool g_show_only_levels = true;

// delete confirmation state
static std::string g_del_path;   // full path awaiting confirmation
static bool g_del_is_dir = false;

static int g_status_kind = 0;   // 0 none, 1 info, 2 warn, 3 ok
static char g_status[256] = "";

static void set_status(int kind, const char* zh, const char* en) {
    g_status_kind = kind;
    snprintf(g_status, sizeof(g_status), "%s", S(zh, en));
}

// -------------------------------------------------------------- settings

static void settings_path(char* out, size_t n) {
    init_default_dir();
    snprintf(out, n, "%sfiles/adofai_loader.ini", g_default_dir);
}

static void load_settings() {
    char p[1024];
    settings_path(p, sizeof(p));
    FILE* f = fopen(p, "r");
    if (!f) return;
    char line[1200];
    if (fgets(line, sizeof(line), f)) {
        char* nl = strchr(line, '\n'); if (nl) *nl = 0;
        // format: lang|theme|cwd|nofail|diff|filter
        // (old 2/3-field files: theme defaults dark, options default)
        char* fields[8];
        int nf = 0;
        char* tok = line;
        while (tok && nf < 8) {
            fields[nf++] = tok;
            char* nxt = strchr(tok, '|');
            if (nxt) { *nxt = 0; tok = nxt + 1; }
            else tok = nullptr;
        }
        g_lang = (nf > 0 && strncmp(fields[0], "en", 2) == 0) ? LANG_EN : LANG_ZH;
        g_theme = (nf > 1 && strncmp(fields[1], "light", 5) == 0) ? THEME_LIGHT : THEME_DARK;
        if (nf > 2 && fields[2][0] && strlen(fields[2]) < sizeof(g_cwd)) {
            snprintf(g_cwd, sizeof(g_cwd), "%s", fields[2]);
        }
        if (nf > 3) g_nofail_setting = (fields[3][0] == '1');
        if (nf > 4) g_diff_setting = atoi(fields[4]);
        if (nf > 5) g_show_only_levels = (fields[5][0] == '1');
        if (g_diff_setting < 0 || g_diff_setting > 2) g_diff_setting = 1;
    }
    fclose(f);
    // 不败模式每次启动默认关闭（设置仅记忆但不自动应用）
    g_nofail_setting = false;
    game_set_no_fail(false);
    game_set_difficulty(g_diff_setting);
}

static void save_settings() {
    char p[1024];
    settings_path(p, sizeof(p));
    char dir[1024];
    snprintf(dir, sizeof(dir), "%s", p);
    char* slash = strrchr(dir, '/');
    if (slash) { *slash = 0; mkdir(dir, 0777); }
    FILE* f = fopen(p, "w");
    if (!f) return;
    fprintf(f, "%s|%s|%s|%d|%d|%d\n",
            g_lang == LANG_EN ? "en" : "zh",
            g_theme == THEME_LIGHT ? "light" : "dark",
            g_cwd,
            g_nofail_setting ? 1 : 0,
            g_diff_setting,
            g_show_only_levels ? 1 : 0);
    fclose(f);
}

// -------------------------------------------------------------- fs browse

static bool ends_with(const char* s, const char* suf) {
    size_t ls = strlen(s), lf = strlen(suf);
    if (lf > ls) return false;
    for (size_t i = 0; i < lf; i++) {
        char a = s[ls - lf + i], b = suf[i];
        if (a >= 'A' && a <= 'Z') a += 32;
        if (b >= 'A' && b <= 'Z') b += 32;
        if (a != b) return false;
    }
    return true;
}

static bool scan_dir() {
    g_entries.clear();
    g_selected = -1;
    DIR* d = opendir(g_cwd);
    if (!d) {
        set_status(2, "无法打开目录（可能没有权限）", "Cannot open directory (no permission?)");
        return false;
    }
    struct dirent* de;
    while ((de = readdir(d)) != nullptr) {
        if (strcmp(de->d_name, ".") == 0) continue;
        if (de->d_name[0] == '.' && strcmp(de->d_name, "..") != 0) continue; // hide dotfiles
        FsEntry e;
        e.name = de->d_name;
        std::string full = g_cwd;
        if (!full.empty() && full.back() != '/') full += '/';
        full += de->d_name;
        struct stat st;
        if (stat(full.c_str(), &st) != 0) continue;
        e.is_dir = S_ISDIR(st.st_mode);
        e.is_level = !e.is_dir && ends_with(de->d_name, ".adofai");
        e.is_zip   = !e.is_dir && !e.is_level && ends_with(de->d_name, ".zip");
        if (!e.is_dir && !e.is_level && !e.is_zip && g_show_only_levels) continue;
        g_entries.push_back(std::move(e));
    }
    closedir(d);
    std::sort(g_entries.begin(), g_entries.end(), [](const FsEntry& a, const FsEntry& b) {
        if (a.is_dir != b.is_dir) return a.is_dir;
        return a.name < b.name;
    });
    set_status(1, "", "");
    return true;
}

static void set_cwd(const char* p) {
    if (strlen(p) >= sizeof(g_cwd)) return;
    snprintf(g_cwd, sizeof(g_cwd), "%s", p);
    scan_dir();
    save_settings();
}

static void go_up() {
    std::string cur = g_cwd;
    while (cur.size() > 1 && cur.back() == '/') cur.pop_back();
    size_t pos = cur.rfind('/');
    if (pos == std::string::npos) { set_cwd("/"); return; }
    if (pos == 0) cur = "/";
    else cur = cur.substr(0, pos + 1);
    set_cwd(cur.c_str());
}

// Level picked in the archive's level list, waiting for the confirmation step
// (-1 = list is showing).
static int g_zip_chosen = -1;

// Set when a zip import was started from this overlay session: a single level
// found inside is then loaded without asking. Cleared as soon as the overlay
// closes, so a late-finishing import never changes the level behind the
// player's back (it shows the picker instead).
static bool g_zip_auto_load = false;
// set while a cancel request is in flight (the worker owns the state until it
// notices and cleans up)
static bool g_zip_cancelling = false;

// Load request waiting for the user's confirmation (tapping a level or an
// archive used to start loading immediately - one mis-tap and the current
// level was gone).
static std::string g_pending_path;   // absolute path of the level / archive
static std::string g_pending_name;   // what to show in the dialog
static bool        g_pending_is_zip = false;

// Modal dialogs use a fixed fraction of the screen width. An
// ImGuiWindowFlags_AlwaysAutoResize window is sized from its content, so a
// TextWrapped text inside it would wrap to the (still tiny) current width and
// freeze the window at "one character per line" - exactly the narrow column
// this helper avoids.
static float dialog_width() { return ImGui::GetIO().DisplaySize.x * 0.82f; }

// Title (and therefore popup ID) of the "really load this level?" dialog.
// Shared by every entry point that can start a load - the file browser and the
// level picker of an imported archive.
static const char* load_confirm_title() { return S("确认加载关卡", "Confirm load"); }

// Hands a level file to the game main thread and closes the overlay so the
// player can play. Shared by the file browser and the zip importer.
static void start_level_load(const std::string& path) {
    if (path.empty()) return;
    LOGI("overlay: load '%s'", path.c_str());
    set_status(3, "已发送加载指令，正在进入关卡…", "Load issued, entering level...");
    input_queue_load_level(path.c_str());
    // close overlay so the player can play
    g_open = false;
    input_set_ui_open(false);
    input_queue_resume_overlay_pause();
    save_settings();
}

// Scratch root for imported archives: <game data dir>/files/zipcache/import
// (always writable by the game process, unlike /sdcard on scoped storage).
static std::string zip_work_root() {
    init_default_dir();
    std::string root = g_default_dir;
    root += "files/zipcache/import";
    return root;
}

static void start_zip_import(const std::string& zip_path) {
    if (!zip_import_begin(zip_path.c_str(), zip_work_root().c_str())) {
        set_status(2, "无法开始解压（可能已有任务在进行）", "Cannot start extraction (import already running)");
        return;
    }
    g_zip_auto_load = true;
    g_zip_chosen = -1;
    set_status(1, "正在解压压缩包…", "Extracting archive...");
}

static void activate_entry(int i) {
    if (i < 0 || i >= (int)g_entries.size()) return;
    FsEntry& e = g_entries[i];
    if (e.is_dir) {
        if (e.name == "..") { go_up(); return; }
        std::string full = g_cwd;
        if (!full.empty() && full.back() != '/') full += '/';
        full += e.name + "/";
        set_cwd(full.c_str());
        return;
    }
    if (e.is_level || e.is_zip) {
        std::string full = g_cwd;
        if (!full.empty() && full.back() != '/') full += '/';
        full += e.name;
        g_pending_path = full;
        g_pending_name = e.name;
        g_pending_is_zip = e.is_zip;
        return;
    }
    set_status(2, "只能加载 .adofai 关卡文件或 .zip 谱面包",
               "Only .adofai levels or .zip level packs can be loaded");
}

// -------------------------------------------------------------- delete

static std::string full_path_of(int i) {
    std::string full = g_cwd;
    if (!full.empty() && full.back() != '/') full += '/';
    full += g_entries[i].name;
    return full;
}

// Recursively delete a file or directory (symlinks are unlinked, never
// followed). Returns true when everything was removed.
static bool delete_path_recursive(const char* path) {
    struct stat st;
    if (lstat(path, &st) != 0) return false;
    if (S_ISDIR(st.st_mode)) {
        DIR* d = opendir(path);
        if (!d) return false;
        struct dirent* de;
        bool ok = true;
        while ((de = readdir(d)) != nullptr) {
            if (strcmp(de->d_name, ".") == 0 || strcmp(de->d_name, "..") == 0) continue;
            std::string child = path;
            if (!child.empty() && child.back() != '/') child += '/';
            child += de->d_name;
            if (!delete_path_recursive(child.c_str())) ok = false;
        }
        closedir(d);
        if (rmdir(path) != 0) ok = false;
        return ok;
    }
    return unlink(path) == 0;
}

// Red destructive button; returns true when clicked.
static bool RedButton(const char* label, const ImVec2& size) {
    ImGui::PushStyleColor(ImGuiCol_Button, kDel);
    ImGui::PushStyleColor(ImGuiCol_ButtonHovered, kDelHover);
    ImGui::PushStyleColor(ImGuiCol_ButtonActive, kDelActive);
    ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(1.0f, 1.0f, 1.0f, 1.0f));
    bool clicked = ImGui::Button(label, size);
    ImGui::PopStyleColor(4);
    return clicked;
}

// Draw the delete button for entry i and open the confirmation popup.
static void request_delete(int i, const ImVec2& size) {
    if (i < 0 || i >= (int)g_entries.size()) return;
    if (RedButton(S("删除", "Delete"), size)) {
        g_del_path = full_path_of(i);
        g_del_is_dir = g_entries[i].is_dir;
        ImGui::OpenPopup(S("删除确认", "Confirm delete"));
    }
}

// Modal confirmation dialog for the pending delete (drawn after the list).
static void draw_delete_confirm() {
    bool open = true;
    const float dlg_w = dialog_width();
    ImGui::SetNextWindowSizeConstraints(ImVec2(dlg_w, 0.0f), ImVec2(dlg_w, 100000.0f));
    if (!ImGui::BeginPopupModal(S("删除确认", "Confirm delete"), &open,
                                ImGuiWindowFlags_AlwaysAutoResize))
        return;
    ImGui::TextWrapped("%s", S("确定要删除以下内容吗？此操作无法撤销。",
                               "Delete the following? This cannot be undone."));
    ImGui::Spacing();
    ImGui::TextWrapped("%s", g_del_path.c_str());
    if (g_del_is_dir) {
        ImGui::Spacing();
        ImGui::TextColored(ImVec4(0.95f, 0.45f, 0.35f, 1.0f), "%s",
            S("这是一个目录，里面的全部文件都会被删除！",
              "This is a directory: everything inside it will be deleted!"));
    }
    ImGui::Spacing();
    float w = (ImGui::GetContentRegionAvail().x - ImGui::GetStyle().ItemSpacing.x) / 2.0f;
    if (ImGui::Button(S("取消", "Cancel"), ImVec2(w, 0))) {
        ImGui::CloseCurrentPopup();
        g_del_path.clear();
    }
    ImGui::SameLine();
    ImGui::PushStyleColor(ImGuiCol_Button, kDel);
    ImGui::PushStyleColor(ImGuiCol_ButtonHovered, kDelHover);
    ImGui::PushStyleColor(ImGuiCol_ButtonActive, kDelActive);
    ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(1.0f, 1.0f, 1.0f, 1.0f));
    if (ImGui::Button(S("确认删除", "Delete"), ImVec2(w, 0))) {
        bool ok = !g_del_path.empty() && delete_path_recursive(g_del_path.c_str());
        ImGui::CloseCurrentPopup();
        g_del_path.clear();
        scan_dir(); // refresh listing (resets selection); then report result
        if (ok) set_status(3, "已删除所选内容", "Selected item deleted");
        else set_status(2, "删除失败（可能没有权限）", "Delete failed (no permission?)");
    }
    ImGui::PopStyleColor(4);
    ImGui::EndPopup();
}

// Second tap that actually starts loading. Tapping a level / archive only
// arms this dialog, so a mis-tap costs one more tap instead of the level.
static void draw_load_confirm() {
    const char* title = load_confirm_title();
    if (g_pending_path.empty()) return;
    // Open from this (main window) context: a popup opened inside the level
    // picker would hash its ID against that window and never match here.
    if (!ImGui::IsPopupOpen(title)) ImGui::OpenPopup(title);

    const float dlg_w = dialog_width();
    ImGui::SetNextWindowSizeConstraints(ImVec2(dlg_w, 0.0f), ImVec2(dlg_w, 100000.0f));
    if (!ImGui::BeginPopupModal(title, nullptr, ImGuiWindowFlags_AlwaysAutoResize)) return;

    bool        go = false;
    std::string path;
    bool        is_zip = false;

    ImGui::TextWrapped("%s", g_pending_is_zip
        ? S("确定要解压并加载这个谱面包吗？", "Extract this level pack and play it?")
        : S("确定要加载这个关卡吗？", "Load this level?"));
    ImGui::Spacing();
    ImGui::TextWrapped("%s%s", g_pending_is_zip ? S("[压缩包] ", "[ZIP] ") : "",
                       g_pending_name.c_str());
    ImGui::Spacing();
    ImGui::PushStyleColor(ImGuiCol_Text, ImGui::GetStyle().Colors[ImGuiCol_TextDisabled]);
    ImGui::TextWrapped("%s", g_pending_path.c_str());
    ImGui::PopStyleColor();
    if (g_pending_is_zip) {
        ImGui::Spacing();
        ImGui::TextDisabled("%s", S("压缩包会被解压到游戏数据目录，里面有多个谱面时会再让你选",
                                    "It is extracted into the game data dir; you pick a level if there are several"));
    }
    ImGui::Spacing();

    const float bw = (ImGui::GetContentRegionAvail().x - ImGui::GetStyle().ItemSpacing.x) / 2.0f;
    if (ImGui::Button(S("取消", "Cancel"), ImVec2(bw, 0))) {
        g_pending_path.clear();
        g_pending_name.clear();
        ImGui::CloseCurrentPopup();
    }
    ImGui::SameLine();
    ImGui::PushStyleColor(ImGuiCol_Button, kViolet);
    ImGui::PushStyleColor(ImGuiCol_ButtonHovered, kVioletHover);
    ImGui::PushStyleColor(ImGuiCol_ButtonActive, kVioletActive);
    ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(1.0f, 1.0f, 1.0f, 1.0f));
    if (ImGui::Button(S("▶ 加载", "> Load"), ImVec2(bw, 0))) {
        path = g_pending_path;
        is_zip = g_pending_is_zip;
        g_pending_path.clear();
        g_pending_name.clear();
        // drop a level picker that is still open behind this dialog
        zip_import_reset();
        go = true;
        ImGui::CloseCurrentPopup();
    }
    ImGui::PopStyleColor(4);
    ImGui::EndPopup();

    // start after EndPopup() so the overlay can close safely
    if (go && !path.empty()) {
        if (is_zip) start_zip_import(path);
        else start_level_load(path);
    }
}

// -------------------------------------------------------------- input map

static int  g_primary_finger = -1;
static bool g_primary_down = false;
static bool g_mouse_source_set = false;

static void feed_im_touches() {
    TouchEvent ts[16];
    int n = input_pop_touches(ts, 16);
    ImGuiIO& io = ImGui::GetIO();
    float h = io.DisplaySize.y;
    if (n > 0 && !g_mouse_source_set) {
        io.AddMouseSourceEvent(ImGuiMouseSource_TouchScreen);
        g_mouse_source_set = true;
    }
    for (int i = 0; i < n; i++) {
        TouchEvent& t = ts[i];
        // Unity touch: origin bottom-left, +Y up. ImGui: origin top-left, +Y down.
        float y = h - t.y;
        if (t.phase == 0) { // Began
            if (g_primary_finger < 0) {
                g_primary_finger = t.finger;
                g_primary_down = true;
                io.AddMousePosEvent(t.x, y);
                io.AddMouseButtonEvent(0, true);
            }
        } else if (t.finger == g_primary_finger) {
            if (t.phase == 1 || t.phase == 2) { // Moved / Stationary
                io.AddMousePosEvent(t.x, y);
            } else if (t.phase == 3 || t.phase == 4) { // Ended / Canceled
                io.AddMousePosEvent(t.x, y);
                if (g_primary_down) io.AddMouseButtonEvent(0, false);
                g_primary_down = false;
                g_primary_finger = -1;
            }
        }
    }
}

// -------------------------------------------------------------- settings page

static int g_page = 0; // 0 = file browser, 1 = game, 2 = settings

// Phone-settings-style toggle switch (sliding pill), theme-aware.
// Returns true when the value changed.
static bool ToggleSwitch(const char* label, bool* v) {
    ImGuiStyle& st = ImGui::GetStyle();
    float W = 36.0f * g_scale;
    float H = 18.0f * g_scale;
    ImVec2 p = ImGui::GetCursorScreenPos();
    // align the label with the switch's vertical center
    ImGui::InvisibleButton(label, ImVec2(W, H));
    bool hovered = ImGui::IsItemHovered();
    bool clicked = ImGui::IsItemClicked();
    if (clicked) *v = !*v;

    ImDrawList* dl = ImGui::GetWindowDrawList();
    bool on = *v;
    float r = H * 0.5f;
    // track: biuret violet when on; neutral grey when off (theme aware)
    ImU32 track;
    if (on) {
        track = IM_COL32(140, 92, 181, 255);
        if (hovered) track = IM_COL32(158, 110, 196, 255);
    } else {
        track = (g_theme == THEME_DARK) ? IM_COL32(90, 88, 98, 220)
                                        : IM_COL32(196, 194, 206, 255);
        if (hovered) track = (g_theme == THEME_DARK) ? IM_COL32(106, 104, 114, 230)
                                                     : IM_COL32(186, 184, 198, 255);
    }
    dl->AddRectFilled(p, ImVec2(p.x + W, p.y + H), track, r);

    // knob
    float kr = r - 3.0f * g_scale;
    float kx = on ? (p.x + W - kr - 3.0f * g_scale) : (p.x + kr + 3.0f * g_scale);
    dl->AddCircleFilled(ImVec2(kx, p.y + r), kr, IM_COL32(255, 255, 255, 255));

    // label vertically centered on the switch
    ImVec2 ts = ImGui::CalcTextSize(label);
    float ly = p.y + (H - ts.y) * 0.5f;
    ImGui::SetCursorScreenPos(ImVec2(p.x + W + 10.0f * g_scale, ly));
    ImGui::TextUnformatted(label);
    // advance the row to below the switch
    ImGui::SetCursorScreenPos(ImVec2(p.x, p.y + H + st.ItemSpacing.y));
    return clicked;
}

static void draw_game_page() {
    ImGui::Text("%s", S("游戏选项 / Game options", "Game options"));
    ImGui::Spacing();
    if (ToggleSwitch(S("不败模式 (No Fail)", "No Fail"), &g_nofail_setting)) {
        game_set_no_fail(g_nofail_setting);
        save_settings();
    }

    ImGui::Spacing();
    ImGui::Separator();
    ImGui::Spacing();

    // return to the mobile main menu (clean quit, bypasses the game's own
    // buggy PC-leftover quit path)
    if (ImGui::Button(S("返回主页面", "Back to main menu"), ImVec2(-1, 0))) {
        g_open = false;
        input_set_ui_open(false);
        game_queue_quit_to_mobile_menu();
    }
}

static void draw_settings_page() {
    ImGui::Text("%s", S("语言 / Language", "Language"));
    ImGui::Spacing();
    if (ImGui::Selectable(S("中文", "Chinese"), g_lang == LANG_ZH)) {
        g_lang = LANG_ZH;
        save_settings();
    }
    if (ImGui::Selectable("English", g_lang == LANG_EN)) {
        g_lang = LANG_EN;
        save_settings();
    }

    ImGui::Spacing();
    ImGui::Separator();
    ImGui::Spacing();

    ImGui::Text("%s", S("主题 / Theme", "Theme"));
    ImGui::Spacing();
    if (ImGui::Selectable(S("暗色主题", "Dark theme"), g_theme == THEME_DARK)) {
        g_theme = THEME_DARK;
        apply_theme(g_scale);
        save_settings();
    }
    if (ImGui::Selectable(S("亮色主题", "Light theme"), g_theme == THEME_LIGHT)) {
        g_theme = THEME_LIGHT;
        apply_theme(g_scale);
        save_settings();
    }

    ImGui::Spacing();
    ImGui::Separator();
    ImGui::Spacing();

    ImGui::Text("%s", S("文件选择器 / Browser", "Browser"));
    ImGui::Spacing();
    if (ToggleSwitch(S("仅显示关卡文件", "Only level files"), &g_show_only_levels)) {
        scan_dir();
        save_settings();
    }

    ImGui::Spacing();
    ImGui::Separator();
    ImGui::Spacing();

    if (ImGui::Button(S("清理日志", "Clear log"), ImVec2(-1, 0))) {
        log_clear();
    }

    ImGui::Spacing();
    ImGui::Separator();
    ImGui::Spacing();
    ImGui::TextDisabled("%s v1.1.10.1", S("冰与火之舞 移动版铺面加载器", "ADoFAI Mobile Level Loader"));
    ImGui::TextDisabled("%s", S("设置保存在游戏数据目录 files/adofai_loader.ini",
                               "Settings are saved in the game data dir: files/adofai_loader.ini"));
}

// -------------------------------------------------------------- UI

// Immediate drag-to-scroll for a scrollable child window (touch screens have
// no mouse wheel). Call right after BeginChild().
static void child_touch_scroll() {
    static bool  dragging = false;
    static float last_y = 0;
    if (!ImGui::IsWindowHovered()) { dragging = false; return; }
    ImGuiIO& io = ImGui::GetIO();
    if (io.MouseDown[0]) {
        if (!dragging) dragging = true;
        else if (io.MousePos.y != last_y) {
            ImGui::SetScrollY(ImGui::GetScrollY() - (io.MousePos.y - last_y));
        }
        last_y = io.MousePos.y;
    } else {
        dragging = false;
    }
}

// -------------------------------------------------------------- zip import

static const char* zip_error_text(int code) {
    switch (code) {
        case ZIPERR_OPEN:     return S("无法打开压缩包（不是有效的 zip 或文件已损坏）",
                                       "Cannot open the archive (invalid or corrupted zip)");
        case ZIPERR_EXTRACT:  return S("解压失败：压缩包内的数据已损坏",
                                       "Extraction failed: corrupted data inside the archive");
        case ZIPERR_DISK:     return S("无法写入解压目录（空间不足或无权限）",
                                       "Cannot write the extraction directory (no space / permission)");
        case ZIPERR_NO_LEVEL: return S("压缩包内没有 .adofai 谱面文件",
                                       "No .adofai level found in this archive");
        case ZIPERR_TOO_BIG:  return S("压缩包解压后体积超出安全上限（4 GB）",
                                       "The archive expands beyond the 4 GB safety limit");
        default:              return S("导入失败", "Import failed");
    }
}

// Progress / result dialog for the running zip import. Also auto-loads the
// level when a pack contains exactly one .adofai and the user started the
// import from this session (g_zip_auto_load).
static void draw_zip_import() {
    int st = zip_import_state();
    if (st != ZIPIMP_WORKING) g_zip_cancelling = false;
    if (st != ZIPIMP_READY) g_zip_chosen = -1;
    if (st == ZIPIMP_IDLE) return;

    std::vector<std::string> abs_paths, rel_names;
    if (st == ZIPIMP_READY) zip_import_levels(&abs_paths, &rel_names);

    const char* title = S("导入压缩包", "Import archive");

    // Single level, started here: no question needed.
    if (st == ZIPIMP_READY && g_zip_auto_load && abs_paths.size() == 1) {
        LOGI("zip: single level '%s'", rel_names[0].c_str());
        std::string path = abs_paths[0];
        g_zip_auto_load = false;
        zip_import_reset();
        // no BeginPopupModal() this frame -> ImGui closes the dialog itself
        start_level_load(path);
        return;
    }

    if (!ImGui::IsPopupOpen(title)) ImGui::OpenPopup(title);

    const float dlg_w = dialog_width();
    ImGui::SetNextWindowSizeConstraints(ImVec2(dlg_w, 0.0f), ImVec2(dlg_w, 100000.0f));
    if (!ImGui::BeginPopupModal(title, nullptr, ImGuiWindowFlags_AlwaysAutoResize)) return;

    bool        close_popup = false;
    std::string pending_path;

    if (st == ZIPIMP_WORKING) {
        int done = 0, total = 0;
        std::string current;
        zip_import_progress(&done, &total, &current);

        ImGui::Text("%s", S("正在解压谱面包…", "Extracting level pack..."));
        ImGui::Spacing();
        char overlay_text[64];
        if (total > 0) snprintf(overlay_text, sizeof(overlay_text), "%d / %d", done, total);
        else overlay_text[0] = 0;
        const float frac = (total > 0) ? (float)done / (float)total : 0.0f;
        ImGui::ProgressBar(frac, ImVec2(-1, 0), overlay_text);
        if (!current.empty()) {
            ImGui::PushStyleColor(ImGuiCol_Text, ImGui::GetStyle().Colors[ImGuiCol_TextDisabled]);
            ImGui::TextWrapped("%s", current.c_str());
            ImGui::PopStyleColor();
        }
        ImGui::Spacing();
        if (g_zip_cancelling) {
            ImGui::TextDisabled("%s", S("正在取消…", "Cancelling..."));
        } else if (ImGui::Button(S("取消", "Cancel"), ImVec2(-1, 0))) {
            g_zip_cancelling = true;
            g_zip_auto_load = false;
            zip_import_cancel();
        }
    } else if (st == ZIPIMP_READY) {
        if (g_zip_chosen >= (int)abs_paths.size()) g_zip_chosen = -1;

        if (g_zip_chosen < 0) {
            // ---- step 1: pick a level -------------------------------------
            if (rel_names.size() > 1) {
                ImGui::TextWrapped("%s", S("压缩包内有多个谱面，请选择要打开的一个：",
                                           "This archive contains more than one level - pick one to open:"));
            } else {
                ImGui::TextWrapped("%s", S("压缩包内找到 1 个谱面：",
                                           "One level found in this archive:"));
            }
            ImGui::Spacing();
            const float list_h = ImGui::GetTextLineHeightWithSpacing() * 7.0f;
            int chosen = -1;
            if (ImGui::BeginChild("##ziplevels", ImVec2(0, list_h), true)) {
                child_touch_scroll();
                for (int i = 0; i < (int)rel_names.size(); i++) {
                    if (ImGui::Selectable(rel_names[i].c_str(), false)) chosen = i;
                }
            }
            ImGui::EndChild();
            ImGui::TextDisabled(S("共 %d 个谱面", "%d level(s) in this archive"),
                                (int)rel_names.size());
            ImGui::Spacing();
            if (chosen >= 0) {
                // Tapping a level used to start it right away; park the choice
                // and let the user confirm on the next step instead.
                LOGI("zip: picked '%s', asking for confirmation", rel_names[chosen].c_str());
                g_zip_chosen = chosen;
            } else if (ImGui::Button(S("取消", "Cancel"), ImVec2(-1, 0))) {
                g_zip_auto_load = false;
                zip_import_reset();
                close_popup = true;
            }
        } else {
            // ---- step 2: confirm the picked level -------------------------
            ImGui::TextWrapped("%s", S("确认加载关卡？", "Load this level?"));
            ImGui::Spacing();
            ImGui::TextWrapped("%s", rel_names[g_zip_chosen].c_str());
            ImGui::Spacing();
            ImGui::PushStyleColor(ImGuiCol_Text, ImGui::GetStyle().Colors[ImGuiCol_TextDisabled]);
            ImGui::TextWrapped("%s", abs_paths[g_zip_chosen].c_str());
            ImGui::PopStyleColor();
            ImGui::Spacing();

            const float bw = (ImGui::GetContentRegionAvail().x -
                              ImGui::GetStyle().ItemSpacing.x) / 2.0f;
            if (ImGui::Button(S("取消", "Cancel"), ImVec2(bw, 0))) {
                g_zip_chosen = -1;   // back to the list, nothing is lost
            }
            ImGui::SameLine();
            ImGui::PushStyleColor(ImGuiCol_Button, kViolet);
            ImGui::PushStyleColor(ImGuiCol_ButtonHovered, kVioletHover);
            ImGui::PushStyleColor(ImGuiCol_ButtonActive, kVioletActive);
            ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(1.0f, 1.0f, 1.0f, 1.0f));
            if (ImGui::Button(S("▶ 加载关卡", "> Load level"), ImVec2(bw, 0))) {
                pending_path = abs_paths[g_zip_chosen];
                g_zip_chosen = -1;
                g_zip_auto_load = false;
                zip_import_reset();
                close_popup = true;
            }
            ImGui::PopStyleColor(4);
        }
    } else { // ZIPIMP_FAILED
        int code = zip_import_error_code();
        std::string detail;
        zip_import_error_detail(&detail);
        ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.95f, 0.45f, 0.35f, 1.0f));
        ImGui::TextWrapped("%s", zip_error_text(code));
        ImGui::PopStyleColor();
        if (!detail.empty()) {
            ImGui::Spacing();
            ImGui::PushStyleColor(ImGuiCol_Text, ImGui::GetStyle().Colors[ImGuiCol_TextDisabled]);
            ImGui::TextWrapped("%s", detail.c_str());
            ImGui::PopStyleColor();
        }
        ImGui::Spacing();
        if (ImGui::Button(S("知道了", "OK"), ImVec2(-1, 0))) {
            g_zip_auto_load = false;
            zip_import_reset();
            close_popup = true;
        }
    }

    if (close_popup) ImGui::CloseCurrentPopup();
    ImGui::EndPopup();
    if (!pending_path.empty()) start_level_load(pending_path);
}

static void draw_file_browser() {
    // header: path + jump buttons
    ImGui::Text("%s", S("当前目录:", "Current directory:"));
    ImGui::TextWrapped("%s", g_cwd);

    ImGui::Spacing();
    float btn_w = (ImGui::GetContentRegionAvail().x - 2 * ImGui::GetStyle().ItemSpacing.x) / 2.0f;
    if (ImGui::Button(S("上一级", "Up"), ImVec2(btn_w, 0))) go_up();
    ImGui::SameLine();
    if (ImGui::Button(S("游戏数据目录", "Game data dir"), ImVec2(btn_w, 0))) {
        init_default_dir();
        set_cwd(g_default_dir);
    }
    if (ImGui::Button(S("/sdcard", "/sdcard"), ImVec2(btn_w, 0))) set_cwd("/sdcard/");
    ImGui::SameLine();
    if (ImGui::Button(S("根目录", "Root /"), ImVec2(btn_w, 0))) set_cwd("/");
    ImGui::SameLine();
    if (ImGui::Button(S("刷新列表", "Refresh list"), ImVec2(-1, 0))) scan_dir();

    ImGui::Separator();

    // file list
    float item_h = ImGui::GetFrameHeightWithSpacing();
    ImVec2 list_size = ImVec2(0, ImGui::GetContentRegionAvail().y - item_h * 2.6f);
    static bool  g_list_req = false;
    static float g_list_req_val = 0;
    static float g_list_scroll_y = 0;
    static float g_list_scroll_max = 0;

    ImGui::BeginChild("##flist", list_size, true);

    if (g_list_req) { ImGui::SetScrollY(g_list_req_val); g_list_req = false; }

    child_touch_scroll();

    // ".." when not at root
    if (strcmp(g_cwd, "/") != 0) {
        if (ImGui::Selectable(S("..  [上级目录]", "..  [up]"), g_selected == -2)) {
            g_selected = -2;
            if (ImGui::IsMouseDoubleClicked(0)) go_up();
        }
    }

    for (int i = 0; i < (int)g_entries.size(); i++) {
        FsEntry& e = g_entries[i];
        char label[1024];
        if (e.is_dir) snprintf(label, sizeof(label), "[%s] %s", S("目录", "DIR"), e.name.c_str());
        else if (e.is_level) snprintf(label, sizeof(label), "[%s] %s", S("关卡", "LVL"), e.name.c_str());
        else if (e.is_zip) snprintf(label, sizeof(label), "[%s] %s", S("压缩包", "ZIP"), e.name.c_str());
        else snprintf(label, sizeof(label), "     %s", e.name.c_str());

        bool sel = (i == g_selected);
        if (ImGui::Selectable(label, sel)) {
            g_selected = i;
            if (ImGui::IsMouseDoubleClicked(0)) activate_entry(i);
        }
    }

    g_list_scroll_y = ImGui::GetScrollY();
    g_list_scroll_max = ImGui::GetScrollMaxY();
    ImGui::EndChild();

    // scroll buttons (▲/▼) for touch users
    if (ImGui::Button(S("▲ 上滚", "> Scroll up"), ImVec2(btn_w, 0))) {
        float t = g_list_scroll_y - item_h * 3.0f;
        if (t < 0) t = 0;
        g_list_req = true; g_list_req_val = t;
    }
    ImGui::SameLine();
    if (ImGui::Button(S("▼ 下滚", "> Scroll down"), ImVec2(btn_w, 0))) {
        float t = g_list_scroll_y + item_h * 3.0f;
        if (t > g_list_scroll_max) t = g_list_scroll_max;
        g_list_req = true; g_list_req_val = t;
    }

    ImGui::Separator();

    // selection summary + actions
    if (g_selected >= 0 && g_selected < (int)g_entries.size()) {
        FsEntry& e = g_entries[g_selected];
        ImGui::Text("%s %s", S("已选择:", "Selected:"), e.name.c_str());

        // main action + delete share one row (delete = ~30% width, red)
        float avail = ImGui::GetContentRegionAvail().x;
        float del_w = avail * 0.30f;
        if (del_w < 64.0f * g_scale) del_w = 64.0f * g_scale;
        float main_w = avail - del_w - ImGui::GetStyle().ItemSpacing.x;

        if (e.is_dir) {
            if (ImGui::Button(S("进入目录", "Enter directory"), ImVec2(main_w, item_h * 1.1f))) activate_entry(g_selected);
            ImGui::SameLine();
            request_delete(g_selected, ImVec2(del_w, item_h * 1.1f));
        } else if (e.is_zip) {
            ImGui::PushStyleColor(ImGuiCol_Button, kViolet);
            ImGui::PushStyleColor(ImGuiCol_ButtonHovered, kVioletHover);
            ImGui::PushStyleColor(ImGuiCol_ButtonActive, kVioletActive);
            ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(1.0f, 1.0f, 1.0f, 1.0f));
            bool extract_clicked = ImGui::Button(S("▶ 解压并加载", "> Extract & play"), ImVec2(main_w, item_h * 1.1f));
            ImGui::PopStyleColor(4);
            if (extract_clicked) activate_entry(g_selected);
            ImGui::SameLine();
            request_delete(g_selected, ImVec2(del_w, item_h * 1.1f));
        } else if (e.is_level) {
            // biuret-violet accent button
            ImGui::PushStyleColor(ImGuiCol_Button, kViolet);
            ImGui::PushStyleColor(ImGuiCol_ButtonHovered, kVioletHover);
            ImGui::PushStyleColor(ImGuiCol_ButtonActive, kVioletActive);
            ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(1.0f, 1.0f, 1.0f, 1.0f));
            bool load_clicked = ImGui::Button(S("▶ 加载所选关卡", "> Load selected level"), ImVec2(main_w, item_h * 1.1f));
            ImGui::PopStyleColor(4);
            if (load_clicked) activate_entry(g_selected);
            ImGui::SameLine();
            request_delete(g_selected, ImVec2(del_w, item_h * 1.1f));
        } else {
            ImGui::TextDisabled("%s", S("该文件不是关卡文件", "Not a level file"));
            request_delete(g_selected, ImVec2(-1, item_h * 1.1f));
        }
    } else {
        ImGui::TextDisabled("%s", S("点击列表中的 .adofai 文件，然后加载", "Tap a .adofai file in the list, then load"));
    }

    if (g_status[0]) {
        ImVec4 col = ImVec4(0.7f, 0.7f, 0.6f, 1.0f);
        if (g_status_kind == 2) col = ImVec4(0.95f, 0.45f, 0.35f, 1.0f);
        else if (g_status_kind == 3) col = ImVec4(0.45f, 0.85f, 0.55f, 1.0f);
        ImGui::TextColored(col, "%s", g_status);
    }
    ImGui::TextDisabled("%s", S("提示：把 .adofai 及同目录歌曲/图片放入游戏数据目录",
                               "Tip: put .adofai (and its song/images in the same folder) into the game data dir"));
    ImGui::TextDisabled("%s", S("也可以直接选择 .zip 谱面包：自动解压后游玩，多个谱面时会让你选",
                               "You can also pick a .zip level pack: it is extracted and played, and you choose when it holds several levels"));

    // delete confirmation popup (renders on top of the browser when open)
    draw_delete_confirm();
    // zip import progress / level picker (may open the load confirmation)
    draw_zip_import();
    // "really load this level?" confirmation; drawn last so it stacks on top
    // of the level picker when the player picked one there
    draw_load_confirm();
}

static void draw_overlay(float w, float h) {
    feed_im_touches();

    // ---- floating toggle button (when window closed)
    // Interaction (tap=drag detection) is handled on the game thread in
    // input.cpp; this window is purely visual (NoInputs) and follows the rect.
    if (!g_open) {
        g_zip_auto_load = false; // a later-finishing import must ask, not jump
        g_pending_path.clear();  // an unconfirmed load dies with the panel
        g_pending_name.clear();
        float bx0, by0, bx1, by1;
        input_get_button_rect(&bx0, &by0, &bx1, &by1); // Unity coords (y-up)
        ImVec2 pos(bx0, h - by1);
        ImVec2 size(bx1 - bx0, by1 - by0);
        if (size.x < 20.0f || size.y < 20.0f ||
            bx1 < 0 || bx0 > w || by1 < 0 || by0 > h) {
            // first frame, or the rect ended up fully off-screen: seed default
            pos = ImVec2(10, 10);
            size = ImVec2(200.0f * g_scale, 80.0f * g_scale);
        }

        ImGui::SetNextWindowPos(pos);
        ImGui::SetNextWindowSize(size);
        ImGui::PushStyleVar(ImGuiStyleVar_WindowRounding, 14.0f * g_scale);
        ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(0, 0));
        ImGui::PushStyleVar(ImGuiStyleVar_WindowBorderSize, 1.0f);
        if (g_theme == THEME_DARK) {
            ImGui::PushStyleColor(ImGuiCol_WindowBg, ImVec4(0.19f, 0.17f, 0.26f, 0.88f));
        } else {
            ImGui::PushStyleColor(ImGuiCol_WindowBg, ImVec4(0.91f, 0.89f, 0.97f, 0.92f));
        }
        ImGui::PushStyleColor(ImGuiCol_Border, ImVec4(0.55f, 0.36f, 0.71f, 0.60f));
        ImGui::Begin("##floatbtn",
                     nullptr,
                     ImGuiWindowFlags_NoDecoration | ImGuiWindowFlags_NoMove |
                     ImGuiWindowFlags_NoResize | ImGuiWindowFlags_NoScrollbar |
                     ImGuiWindowFlags_NoCollapse | ImGuiWindowFlags_NoSavedSettings |
                     ImGuiWindowFlags_NoFocusOnAppearing | ImGuiWindowFlags_NoInputs |
                     ImGuiWindowFlags_NoNav);
        ImDrawList* dl = ImGui::GetWindowDrawList();
        const char* label = S("加载关卡", "Load Level");
        ImVec2 ts = ImGui::CalcTextSize(label);
        ImVec2 tp(pos.x + (size.x - ts.x) * 0.5f, pos.y + (size.y - ts.y) * 0.5f);
        unsigned int col = (g_theme == THEME_DARK)
            ? IM_COL32(224, 205, 255, 255)
            : IM_COL32(122, 82, 186, 255);
        dl->AddText(tp, col, label);
        ImGui::End();
        ImGui::PopStyleColor(2);
        ImGui::PopStyleVar(3);

        // report the rect each frame (flipped to Unity coords); ignored while dragging
        input_set_button_rect(pos.x, pos.y, pos.x + size.x, pos.y + size.y, w, h);
        return;
    }

    // ---- main window
    float W = w * 0.94f, H = h * 0.86f;
    ImGui::SetNextWindowPos(ImVec2((w - W) * 0.5f, (h - H) * 0.45f));
    ImGui::SetNextWindowSize(ImVec2(W, H));
    bool open_state = g_open;
    ImGui::Begin(S("冰与火之舞 · 铺面加载器", "ADoFAI · Level Loader"), &open_state,
                 ImGuiWindowFlags_NoCollapse | ImGuiWindowFlags_NoResize);
    if (!open_state) {
        g_open = false;
        input_set_ui_open(false);
        input_queue_resume_overlay_pause();
        save_settings();
    }
    if (g_open) {
        // page tabs
        float tab_w = (ImGui::GetContentRegionAvail().x - 2 * ImGui::GetStyle().ItemSpacing.x) / 3.0f;
        if (ImGui::Button(g_page == 0 ? S("▣ 文件选择", "> Files") : S("□ 文件选择", "  Files"), ImVec2(tab_w, 0)))
            g_page = 0;
        ImGui::SameLine();
        if (ImGui::Button(g_page == 1 ? S("▣ 游戏", "> Game") : S("□ 游戏", "  Game"), ImVec2(tab_w, 0)))
            g_page = 1;
        ImGui::SameLine();
        if (ImGui::Button(g_page == 2 ? S("▣ 设置", "> Settings") : S("□ 设置", "  Settings"), ImVec2(tab_w, 0)))
            g_page = 2;
        ImGui::Separator();

        if (g_page == 0) draw_file_browser();
        else if (g_page == 1) draw_game_page();
        else draw_settings_page();
    }
    ImGui::End();
}

// -------------------------------------------------------------- public

bool overlay_gl_init(int fb_w, int fb_h) {
    (void)fb_w; (void)fb_h;
    IMGUI_CHECKVERSION();
    if (ImGui::GetCurrentContext()) ImGui::DestroyContext();
    ImGui::CreateContext();
    ImGuiIO& io = ImGui::GetIO();
    io.IniFilename = nullptr; // no imgui.ini on disk
    io.LogFilename = nullptr;
    io.ConfigFlags |= ImGuiConfigFlags_NoMouseCursorChange;

    float base = (float)fb_h / 36.0f;
    if (base < 28.0f) base = 28.0f;
    if (base > 72.0f) base = 72.0f;
    g_scale = base / 16.0f;

    load_fonts(base);
    init_default_dir();
    load_settings();      // BEFORE apply_theme so the saved theme takes effect
    apply_theme(g_scale);

    if (!ImGui_ImplOpenGL3_Init("#version 300 es")) {
        LOGE("overlay: ImGui_ImplOpenGL3_Init failed");
        return false;
    }
    scan_dir();
    g_inited = true;
    LOGI("overlay: init ok (scale %.2f, font px %.0f)", g_scale, base);
    return true;
}

void overlay_gl_shutdown() {
    if (!g_inited) return;
    ImGui_ImplOpenGL3_Shutdown();
    if (ImGui::GetCurrentContext()) ImGui::DestroyContext();
    g_inited = false;
    LOGI("overlay: shutdown");
}

void overlay_render_frame(int fb_w, int fb_h) {
    if (!g_inited) return;

    ImGuiIO& io = ImGui::GetIO();
    io.DisplaySize = ImVec2((float)fb_w, (float)fb_h);
    io.DisplayFramebufferScale = ImVec2(1.0f, 1.0f);

    // open via floating button tap (detected on the game thread)
    if (input_button_tapped() && !g_open) {
        g_open = true;
        input_set_ui_open(true);
        input_queue_pause_toggle_for_overlay();
        scan_dir(); // refresh listing (also clears stale status)
    }

    ImGui_ImplOpenGL3_NewFrame();
    ImGui::NewFrame();
    draw_overlay((float)fb_w, (float)fb_h);
    ImGui::Render();
    ImGui_ImplOpenGL3_RenderDrawData(ImGui::GetDrawData());
}
