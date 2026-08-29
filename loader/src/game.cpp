#include "game.hpp"
#include "hooks.hpp"
#include "il2cpp.hpp"
#include "util.hpp"
#include <string.h>
#include <time.h>
#include <mutex>
#include <vector>
#include <algorithm>

extern "C" {
    void* hk_concat2_c(void* a, void* b);
    void* hk_concat3_c(void* a, void* b, void* c);
    void* hk_concat4_c(void* a, void* b, void* c, void* d);
    void* hk_split_c(void* str, int32_t sep, int32_t options);
    void* hk_LoadTexture_c(void* filePath, void* status, int32_t maxSide);
    void* hk_LoadNewSprite_c(void* filePath, void* status, float ppu, int32_t type);
    bool hk_RDFile_Exists_c(void* path);
    void* hk_RDFile_ReadAllBytes_c(void* path, void* status);
    int32_t hk_DetermineDifficultyUIMode_c(float bpm);
    bool hk_LoadAndPlayLevel_c(void* self, void* path);
    bool hk_LoadLevel_c(void* self, void* path, void* status);
    bool hk_LevelData_LoadLevel_c(void* self, void* path, void* status);
    bool hk_get_isInternalLevel_c();
    bool hk_get_isDLCLevel_c();
    bool hk_get_isBossLevel_c();
    bool hk_get_isCLSBossLevel_c();
    bool hk_ReloadSongCo_MoveNext_c(void* self);
    void hk_ReloadSong_c(void* self, bool force);
    void hk_ReloadCustomSounds_c(void* self, bool force);
}

static uint64_t monotonic_ms() {
    struct timespec ts;
    clock_gettime(CLOCK_MONOTONIC, &ts);
    return (uint64_t)ts.tv_sec * 1000 + (uint64_t)ts.tv_nsec / 1000000;
}

static const uint64_t RVA_scrController_get_instance = 0x25351AC;
static const uint64_t RVA_scrController_LoadCustomLevel = 0x2547B90;
static const uint64_t RVA_scrController_get_paused = 0x253CEEC;
static const uint64_t RVA_scrController_TogglePauseGame = 0x2543B1C;
static const uint64_t RVA_ADOBase_get_isScnGame = 0x241FCAC;

uint64_t g_il2cpp_base = 0;

typedef void* (*pfn_get_controller)(void);
typedef bool (*pfn_get_paused)(void* self);
typedef bool (*pfn_is_scn_game)(void);
typedef void (*pfn_load_custom)(void* self, Il2CppString* path, Il2CppString* id, bool fromBundle);

static pfn_get_controller fn_get_controller;
static pfn_get_paused fn_get_paused;
static pfn_is_scn_game fn_is_scn_game;
static pfn_load_custom fn_load_custom;

static Il2CppClass* cls_gcs = nullptr;
static FieldInfo* fld_customLevelIndex = nullptr;
static FieldInfo* fld_customLevelPaths = nullptr;
static FieldInfo* fld_internalLevelName = nullptr;
static FieldInfo* fld_customLevelId = nullptr;
static FieldInfo* fld_sceneToLoad = nullptr;
static FieldInfo* fld_useNoFail = nullptr;
static FieldInfo* fld_difficulty = nullptr;

static const MethodInfo* m_TogglePauseGame = nullptr;

pfn_concat2 orig_concat2 = nullptr;
pfn_concat3 orig_concat3 = nullptr;
pfn_concat4 orig_concat4 = nullptr;
pfn_split orig_split = nullptr;

static bool g_ready = false;
static bool g_paused_by_us = false;

static std::mutex g_gc_handle_mutex;
static std::vector<uint32_t> g_gc_handles;

Il2CppString* g_empty_string = nullptr;
bool g_strings_ready = false;

uint32_t game_protect_object(void* obj) {
    if (!obj || !il2cpp.gchandle_new) return 0;
    std::lock_guard<std::mutex> lock(g_gc_handle_mutex);
    uint32_t handle = il2cpp.gchandle_new((Il2CppObject*)obj, false);
    if (handle) {
        g_gc_handles.push_back(handle);
        LOGI("game: protected object %p with handle %u", obj, handle);
    }
    return handle;
}

void game_unprotect_object(uint32_t handle) {
    if (!handle || !il2cpp.gchandle_free) return;
    std::lock_guard<std::mutex> lock(g_gc_handle_mutex);
    il2cpp.gchandle_free(handle);
    auto it = std::find(g_gc_handles.begin(), g_gc_handles.end(), handle);
    if (it != g_gc_handles.end()) {
        g_gc_handles.erase(it);
    }
}

void game_cleanup_gc_handles() {
    std::lock_guard<std::mutex> lock(g_gc_handle_mutex);
    for (uint32_t h : g_gc_handles) {
        if (h && il2cpp.gchandle_free) {
            il2cpp.gchandle_free(h);
        }
    }
    g_gc_handles.clear();
    LOGI("game: cleaned up %zu GC handles", g_gc_handles.size());
}

bool game_ensure_strings_initialized() {
    if (g_strings_ready) return true;
    if (!g_ready) return false;
    g_empty_string = il2cpp.string_new("");
    if (g_empty_string) {
        game_protect_object(g_empty_string);
    }
    g_strings_ready = (g_empty_string != nullptr);
    LOGI("game: protected strings initialized (empty=%p)", g_empty_string);
    return g_strings_ready;
}

bool game_safe_invoke(const MethodInfo* method, void* obj, void** params, const char* name) {
    if (!method) {
        LOGE("game: safe_invoke: method is null for %s", name ? name : "unknown");
        return false;
    }
    Il2CppException* exc = nullptr;
    il2cpp.runtime_invoke(method, obj, params, &exc);
    if (exc) {
        const char* msg = "unknown";
        if (il2cpp.exception_get_message) {
            msg = il2cpp.exception_get_message(exc);
        }
        LOGE("game: %s threw exception: %s", name ? name : "invoke", msg);
        if (il2cpp.exception_clear) {
            il2cpp.exception_clear();
        }
        return false;
    }
    return true;
}

static bool resolve_bindings() {
    if (g_ready) return true;
    if (!il2cpp_resolve()) return false;

    Il2CppClass* ctrl = il2cpp_find_class("Assembly-CSharp", "", "scrController");
    if (!ctrl) return false;
    cls_gcs = il2cpp_find_class("Assembly-CSharp", "", "GCS");
    if (!cls_gcs) return false;

    fld_customLevelIndex = il2cpp.class_get_field_from_name(cls_gcs, "customLevelIndex");
    fld_customLevelPaths = il2cpp.class_get_field_from_name(cls_gcs, "customLevelPaths");
    fld_internalLevelName = il2cpp.class_get_field_from_name(cls_gcs, "internalLevelName");
    fld_customLevelId = il2cpp.class_get_field_from_name(cls_gcs, "customLevelId");
    fld_sceneToLoad = il2cpp.class_get_field_from_name(cls_gcs, "sceneToLoad");
    fld_useNoFail = il2cpp.class_get_field_from_name(cls_gcs, "useNoFail");
    fld_difficulty = il2cpp.class_get_field_from_name(cls_gcs, "difficulty");
    if (!fld_customLevelIndex || !fld_customLevelPaths || !fld_internalLevelName ||
        !fld_customLevelId || !fld_sceneToLoad)
        return false;

    const MethodInfo* m = il2cpp.class_get_method_from_name(ctrl, "get_instance", 0);
    (void)m;
    m_TogglePauseGame = il2cpp.class_get_method_from_name(ctrl, "TogglePauseGame", 0);
    if (!m_TogglePauseGame) return false;

    fn_get_controller = (pfn_get_controller)(g_il2cpp_base + RVA_scrController_get_instance);
    fn_get_paused = (pfn_get_paused)(g_il2cpp_base + RVA_scrController_get_paused);
    fn_is_scn_game = (pfn_is_scn_game)(g_il2cpp_base + RVA_ADOBase_get_isScnGame);
    fn_load_custom = (pfn_load_custom)(g_il2cpp_base + RVA_scrController_LoadCustomLevel);

    g_ready = true;
    LOGI("game: bindings resolved (GCS=%p scrController=%p)", (void*)cls_gcs, (void*)ctrl);
    return true;
}

bool game_init() {
    if (g_ready) return true;
    if (!g_il2cpp_base) {
        g_il2cpp_base = find_module_base("libil2cpp.so");
        if (!g_il2cpp_base) return false;
    }
    return resolve_bindings();
}

bool game_ready() {
    return g_ready;
}

static void set_int_field(FieldInfo* f, int32_t v) {
    il2cpp.field_static_set_value(f, &v);
}

static void set_string_field(FieldInfo* f, const char* v) {
    if (v) {
        Il2CppString* s = il2cpp.string_new(v);
        il2cpp.field_static_set_value(f, &s);
    } else {
        void* nullp = nullptr;
        il2cpp.field_static_set_value(f, &nullp);
    }
}

bool game_load_level(const char* level_path) {
    if (!game_init()) { LOGE("game: not ready"); return false; }
    if (!level_path || !level_path[0]) return false;

    set_int_field(fld_customLevelIndex, 0);
    set_string_field(fld_internalLevelName, level_path);
    set_string_field(fld_customLevelId, nullptr);
    set_string_field(fld_sceneToLoad, "scnGame");

    void* controller = fn_get_controller();
    if (!controller) { LOGE("game: no scrController instance"); return false; }
    Il2CppString* p = il2cpp.string_new(level_path);

    fn_load_custom(controller, p, nullptr, false);
    LOGI("game: LoadCustomLevel('%s') issued", level_path);
    return true;
}

bool game_in_gameplay_scene() {
    if (!game_init()) return false;
    return fn_is_scn_game();
}

static bool g_queued_no_fail = false;
static bool g_queued_no_fail_val = false;
static bool g_queued_diff = false;
static int g_queued_diff_val = 0;
static bool g_queued_quit = false;

bool game_set_no_fail(bool on) {
    g_queued_no_fail = true;
    g_queued_no_fail_val = on;
    return true;
}

bool game_set_difficulty(int level) {
    if (level < 0 || level > 2) return false;
    g_queued_diff = true;
    g_queued_diff_val = level;
    return true;
}

void game_apply_queued_options() {
    if (g_queued_no_fail) {
        g_queued_no_fail = false;
        if (fld_useNoFail) {
            bool b = g_queued_no_fail_val;
            il2cpp.field_static_set_value(fld_useNoFail, &b);
            void* controller = fn_get_controller();
            if (controller) *(uint8_t*)((uint8_t*)controller + 0x102) = (uint8_t)(b ? 1 : 0);
            LOGI("game: noFail set to %d", b ? 1 : 0);
        }
    }
    if (g_queued_diff) {
        g_queued_diff = false;
        if (fld_difficulty) {
            int32_t d = g_queued_diff_val;
            il2cpp.field_static_set_value(fld_difficulty, &d);
            LOGI("game: difficulty set to %d", d);
        }
    }
    if (g_queued_quit) {
        g_queued_quit = false;
        if (cls_gcs) {
            void* statics = *(void**)((uint8_t*)cls_gcs + 0xB8);
            if (statics) {
                *(void**)((uint8_t*)statics + 0x70) = nullptr;
                *(void**)((uint8_t*)statics + 0x80) = nullptr;
            }
        }
        typedef void (*pfn_ls)(void*);
        ((pfn_ls)(g_il2cpp_base + 0x45D8190))(il2cpp.string_new("scnMobileMenu"));
        LOGI("game: quit to mobile menu issued");
    }
}

void game_queue_quit_to_mobile_menu() {
    g_queued_quit = true;
}

bool game_get_no_fail() {
    if (!g_ready || !fld_useNoFail) return false;
    bool b = false;
    il2cpp.field_static_get_value(fld_useNoFail, &b);
    return b;
}

int game_get_difficulty() {
    if (!g_ready || !fld_difficulty) return 1;
    int32_t d = 1;
    il2cpp.field_static_get_value(fld_difficulty, &d);
    return d;
}

bool game_pause_for_overlay() {
    if (!game_init()) return false;
    if (g_paused_by_us) return true;
    void* controller = fn_get_controller();
    if (!controller) return false;
    if (fn_get_paused(controller)) return false;
    if (!game_safe_invoke(m_TogglePauseGame, controller, nullptr, "TogglePauseGame")) {
        return false;
    }
    g_paused_by_us = true;
    LOGI("game: paused for overlay");
    return true;
}

void game_resume_overlay_pause() {
    if (!g_paused_by_us) return;
    if (!game_init()) {
        g_paused_by_us = false;
        return;
    }
    void* controller = fn_get_controller();
    if (controller && fn_get_paused(controller)) {
        game_safe_invoke(m_TogglePauseGame, controller, nullptr, "TogglePauseGame (resume)");
        LOGI("game: resumed after overlay close");
    }
    g_paused_by_us = false;
}

static const uint64_t RVA_string_new_size = 0x1F87C60;
typedef void* (*pfn_string_new_size)(int len);
static pfn_string_new_size orig_string_new_size = nullptr;

extern "C" void* hk_string_new_size_c(int len) {
    if (len < 0) len = 64;
    return orig_string_new_size(len);
}

static void game_install_diag_hooks() {
    if (!g_il2cpp_base) return;
    orig_string_new_size = (pfn_string_new_size)hook_install(
        (void*)(g_il2cpp_base + RVA_string_new_size), (void*)&hk_string_new_size_c);
}

static const uint64_t RVA_string_Concat4 = 0x3AC5BD8;
static const uint64_t RVA_string_Concat2 = 0x3AB8898;
static const uint64_t RVA_string_Concat3 = 0x3AC5674;
static const uint64_t RVA_string_Split = 0x3AC816C;

static bool stack_like(void* p) {
    uintptr_t u = (uintptr_t)p;
    uintptr_t sp = (uintptr_t)__builtin_frame_address(0);
    return (u > sp - 8u * 1024 * 1024) && (u < sp + 8u * 1024 * 1024);
}

static bool is_valid_heap_pointer(void* p) {
    if (!p) return false;
    uintptr_t u = (uintptr_t)p;
    if (u < 0x1000000000ULL || u > 0x8000000000ULL) return false;
    if ((u & 0xFFF) == 0) return false;
    void* klass = *(void**)p;
    uintptr_t k = (uintptr_t)klass;
    if (k < 0x1000000000ULL || k > 0x8000000000ULL) return false;
    return true;
}

bool string_looks_bad(void* s) {
    if (!s) return true;
    int32_t len = *(int32_t*)((uint8_t*)s + 0x10);
    return (len < 0 || len > 0x400000);
}

void* resolve_string_arg(void* p) {
    if (!p) return nullptr;
    if (stack_like(p)) {
        void* real = *(void**)p;
        if (is_valid_heap_pointer(real) && !string_looks_bad(real)) {
            return real;
        }
        return nullptr;
    }
    if (is_valid_heap_pointer(p) && !string_looks_bad(p)) {
        return p;
    }
    return nullptr;
}

static const uint64_t RVA_TextureManager_LoadTexture = 0x229ED28;
static const uint64_t RVA_TextureManager_LoadNewSprite = 0x229EBA8;
static const uint64_t RVA_RDFile_Exists = 0x240AB70;
static const uint64_t RVA_RDFile_ReadAllBytes = 0x2414A80;
static const uint64_t RVA_LoadTexture_exists_fail = 0x229F130;
static const uint64_t RVA_DetermineDifficultyUIMode = 0x22805A4;

typedef void* (*pfn_loadtexture)(void* filePath, void* status, int32_t maxSide);
typedef void* (*pfn_loadsprite)(void* filePath, void* status, float ppu, int32_t type);
typedef bool (*pfn_exists)(void* path);
typedef void* (*pfn_readallbytes)(void* path, void* status);
typedef int32_t (*pfn_difmode)(float bpm);

static pfn_loadtexture orig_loadtexture = nullptr;
static pfn_loadsprite orig_loadsprite = nullptr;
static pfn_exists orig_rd_exists = nullptr;
static pfn_readallbytes orig_rd_readall = nullptr;
static pfn_difmode orig_difmode = nullptr;

static void* g_texture_path = nullptr;
static void* g_saved_internal_name = nullptr;

static bool looks_file_path(void* s) {
    if (!s) return false;
    int32_t len = *(int32_t*)((uint8_t*)s + 0x10);
    if (len < 2 || len > 0x400000) return false;
    const uint16_t* chars = (const uint16_t*)((uint8_t*)s + 0x14);
    return chars[0] == '/';
}

static void set_internal_level_name_safe(void* value) {
    if (!fld_internalLevelName) {
        LOGE("game: fld_internalLevelName is null");
        return;
    }
    il2cpp.field_static_set_value(fld_internalLevelName, &value);
}

static void begin_texture_load_window(void* filePath) {
    if (!looks_file_path(filePath)) return;
    if (!g_ready || !cls_gcs) return;
    il2cpp.field_static_get_value(fld_internalLevelName, &g_saved_internal_name);
    set_internal_level_name_safe(nullptr);
}

static void end_texture_load_window() {
    if (!g_saved_internal_name) return;
    set_internal_level_name_safe(g_saved_internal_name);
    g_saved_internal_name = nullptr;
}

extern "C" void* hk_LoadTexture_c(void* filePath, void* status, int32_t maxSide) {
    if (game_custom_pending()) {
        filePath = resolve_string_arg(filePath);
        if (!filePath || string_looks_bad(filePath)) {
            if (status) *(int32_t*)status = 3;
            return nullptr;
        }
        g_texture_path = filePath;
        begin_texture_load_window(filePath);
        void* result = orig_loadtexture(filePath, status, maxSide);
        end_texture_load_window();
        g_texture_path = nullptr;
        return result;
    }
    return orig_loadtexture(filePath, status, maxSide);
}

extern "C" void* hk_LoadNewSprite_c(void* filePath, void* status, float ppu, int32_t type) {
    if (game_custom_pending()) {
        filePath = resolve_string_arg(filePath);
        if (!filePath || string_looks_bad(filePath)) {
            if (status) *(int32_t*)status = 3;
            return nullptr;
        }
        begin_texture_load_window(filePath);
        void* r = orig_loadsprite(filePath, status, ppu, type);
        end_texture_load_window();
        return r;
    }
    return orig_loadsprite(filePath, status, ppu, type);
}

extern "C" bool hk_RDFile_Exists_c(void* path) {
    if (g_texture_path && path != g_texture_path) path = g_texture_path;
    return orig_rd_exists(path);
}

extern "C" void* hk_RDFile_ReadAllBytes_c(void* path, void* status) {
    if (g_texture_path && path != g_texture_path) path = g_texture_path;
    return orig_rd_readall(path, status);
}

extern "C" int32_t hk_DetermineDifficultyUIMode_c(float bpm) {
    if (game_custom_pending()) return 3;
    return orig_difmode(bpm);
}

static const uint64_t RVA_scnGame_LoadAndPlayLevel = 0x251DA58;
static const uint64_t RVA_scnGame_LoadLevel = 0x251E060;
static const uint64_t RVA_LevelData_LoadLevel = 0x24BDFA0;
static const uint64_t RVA_ADOBase_get_isInternalLevel = 0x2412EA0;
static const uint64_t RVA_ADOBase_get_isDLCLevel = 0x24200B4;
static const uint64_t RVA_ADOBase_get_isBossLevel = 0x242014C;
static const uint64_t RVA_ADOBase_get_isCLSBossLevel = 0x2420220;
static const uint64_t RVA_LevelData_get_songFilename = 0x24C5040;
static const uint64_t RVA_ReloadSongCo_MoveNext = 0x252CD34;
static const uint64_t RVA_scnGame_ReloadSong = 0x251F9D4;
static const uint64_t RVA_scnGame_ReloadCustomSounds = 0x2520ED4;

typedef bool (*pfn_loadandplay)(void* self, void* path);
typedef bool (*pfn_loadlevel2)(void* self, void* path, void* status);
typedef bool (*pfn_isinternal)(void);
typedef bool (*pfn_boolgetter)(void);
typedef Il2CppString* (*pfn_get_song_filename)(void* self);
typedef bool (*pfn_movenext)(void* self);
typedef void (*pfn_void_bool)(void* self, bool force);

static pfn_loadandplay orig_loadandplay = nullptr;
static pfn_loadlevel2 orig_scngame_loadlevel = nullptr;
static pfn_loadlevel2 orig_leveldata_loadlevel = nullptr;
static pfn_isinternal orig_get_isinternal = nullptr;
static pfn_boolgetter orig_get_isdlclevel = nullptr;
static pfn_boolgetter orig_get_isbosslevel = nullptr;
static pfn_boolgetter orig_get_isclsbosslevel = nullptr;
static pfn_movenext orig_reloadsongco_movenext = nullptr;
static pfn_void_bool orig_reloadsong = nullptr;
static pfn_void_bool orig_reloadcustomsounds = nullptr;

bool game_custom_pending() {
    if (!g_ready || !fld_customLevelPaths) return false;
    void* paths = nullptr;
    il2cpp.field_static_get_value(fld_customLevelPaths, &paths);
    return paths != nullptr;
}

void* game_fix_level_path(void* path) {
    if (!g_ready || !fld_customLevelPaths) return path;
    void* paths = nullptr;
    il2cpp.field_static_get_value(fld_customLevelPaths, &paths);
    if (!paths) return path;
    int32_t len = *(int32_t*)((uint8_t*)paths + 0x18);
    if (len < 1) return path;
    void* first = *(void**)((uint8_t*)paths + 0x20);
    if (first && first != path) {
        LOGI("game: level path swapped (%p -> %p)", path, first);
        return first;
    }
    return path;
}

static void game_set_current_song_key(void* self) {
    if (!g_ready || !self) return;
    if (!game_custom_pending()) return;
    void* levelData = *(void**)((uint8_t*)self + 0x70);
    if (!levelData) {
        LOGW("game: no levelData in scnGame");
        return;
    }
    Il2CppString* fn = ((pfn_get_song_filename)(g_il2cpp_base + RVA_LevelData_get_songFilename))(levelData);
    if (!fn) {
        fn = (Il2CppString*)*(void**)((uint8_t*)self + 0x80);
    }
    if (!fn) {
        LOGW("game: no song filename found");
        return;
    }
    void** current_song_key = (void**)((uint8_t*)self + 0xC0);
    if (*current_song_key == nullptr) {
        uint32_t handle = game_protect_object(fn);
        if (handle) {
            *current_song_key = fn;
            LOGI("game: currentSongKey set with handle %u", handle);
        } else {
            LOGE("game: failed to protect currentSongKey");
        }
    }
}

extern "C" bool hk_LoadAndPlayLevel_c(void* self, void* path) {
    bool r = orig_loadandplay(self, game_fix_level_path(path));
    game_set_current_song_key(self);
    return r;
}

extern "C" bool hk_LoadLevel_c(void* self, void* path, void* status) {
    bool r = orig_scngame_loadlevel(self, game_fix_level_path(path), status);
    game_set_current_song_key(self);
    return r;
}

extern "C" bool hk_LevelData_LoadLevel_c(void* self, void* path, void* status) {
    return orig_leveldata_loadlevel(self, game_fix_level_path(path), status);
}

extern "C" bool hk_get_isInternalLevel_c() {
    bool r = orig_get_isinternal();
    if (r && game_custom_pending()) return false;
    return r;
}

extern "C" bool hk_get_isDLCLevel_c() {
    if (game_custom_pending()) return false;
    return orig_get_isdlclevel();
}

extern "C" bool hk_get_isBossLevel_c() {
    if (game_custom_pending()) return false;
    return orig_get_isbosslevel();
}

extern "C" bool hk_get_isCLSBossLevel_c() {
    if (game_custom_pending()) return false;
    return orig_get_isclsbosslevel();
}

extern "C" bool hk_ReloadSongCo_MoveNext_c(void* self) {
    return orig_reloadsongco_movenext(self);
}

extern "C" void hk_ReloadSong_c(void* self, bool force) {
    orig_reloadsong(self, force);
}

extern "C" void hk_ReloadCustomSounds_c(void* self, bool force) {
    orig_reloadcustomsounds(self, force);
}

bool game_install_load_level_hooks() {
    if (orig_loadandplay && orig_scngame_loadlevel && orig_leveldata_loadlevel && orig_get_isinternal)
        return true;
    if (!g_il2cpp_base) return false;

    game_install_diag_hooks();

    orig_concat2 = (pfn_concat2)hook_install(
        (void*)(g_il2cpp_base + RVA_string_Concat2), (void*)&hk_concat2_c);
    orig_split = (pfn_split)hook_install(
        (void*)(g_il2cpp_base + RVA_string_Split), (void*)&hk_split_c);
    orig_concat3 = (pfn_concat3)hook_install(
        (void*)(g_il2cpp_base + RVA_string_Concat3), (void*)&hk_concat3_c);
    orig_concat4 = (pfn_concat4)hook_install(
        (void*)(g_il2cpp_base + RVA_string_Concat4), (void*)&hk_concat4_c);

    orig_loadtexture = (pfn_loadtexture)hook_install(
        (void*)(g_il2cpp_base + RVA_TextureManager_LoadTexture), (void*)&hk_LoadTexture_c);
    orig_loadsprite = (pfn_loadsprite)hook_install(
        (void*)(g_il2cpp_base + RVA_TextureManager_LoadNewSprite), (void*)&hk_LoadNewSprite_c);
    orig_rd_exists = (pfn_exists)hook_install(
        (void*)(g_il2cpp_base + RVA_RDFile_Exists), (void*)&hk_RDFile_Exists_c);
    orig_rd_readall = (pfn_readallbytes)hook_install(
        (void*)(g_il2cpp_base + RVA_RDFile_ReadAllBytes), (void*)&hk_RDFile_ReadAllBytes_c);
    orig_difmode = (pfn_difmode)hook_install(
        (void*)(g_il2cpp_base + RVA_DetermineDifficultyUIMode), (void*)&hk_DetermineDifficultyUIMode_c);

    patch_write_u32((void*)(g_il2cpp_base + RVA_LoadTexture_exists_fail), 0xD503201F);

    orig_loadandplay = (pfn_loadandplay)hook_install(
        (void*)(g_il2cpp_base + RVA_scnGame_LoadAndPlayLevel), (void*)&hk_LoadAndPlayLevel_c);
    orig_scngame_loadlevel = (pfn_loadlevel2)hook_install(
        (void*)(g_il2cpp_base + RVA_scnGame_LoadLevel), (void*)&hk_LoadLevel_c);
    orig_leveldata_loadlevel = (pfn_loadlevel2)hook_install(
        (void*)(g_il2cpp_base + RVA_LevelData_LoadLevel), (void*)&hk_LevelData_LoadLevel_c);
    orig_get_isinternal = (pfn_isinternal)hook_install(
        (void*)(g_il2cpp_base + RVA_ADOBase_get_isInternalLevel), (void*)&hk_get_isInternalLevel_c);

    orig_get_isdlclevel = (pfn_boolgetter)hook_install(
        (void*)(g_il2cpp_base + RVA_ADOBase_get_isDLCLevel), (void*)&hk_get_isDLCLevel_c);
    orig_get_isbosslevel = (pfn_boolgetter)hook_install(
        (void*)(g_il2cpp_base + RVA_ADOBase_get_isBossLevel), (void*)&hk_get_isBossLevel_c);
    orig_get_isclsbosslevel = (pfn_boolgetter)hook_install(
        (void*)(g_il2cpp_base + RVA_ADOBase_get_isCLSBossLevel), (void*)&hk_get_isCLSBossLevel_c);

    orig_reloadsongco_movenext = (pfn_movenext)hook_install(
        (void*)(g_il2cpp_base + RVA_ReloadSongCo_MoveNext), (void*)&hk_ReloadSongCo_MoveNext_c);

    orig_reloadsong = (pfn_void_bool)hook_install(
        (void*)(g_il2cpp_base + RVA_scnGame_ReloadSong), (void*)&hk_ReloadSong_c);
    orig_reloadcustomsounds = (pfn_void_bool)hook_install(
        (void*)(g_il2cpp_base + RVA_scnGame_ReloadCustomSounds), (void*)&hk_ReloadCustomSounds_c);

    bool ok = orig_loadandplay && orig_scngame_loadlevel && orig_leveldata_loadlevel && orig_get_isinternal;
    LOGI("game: load-level hooks %s", ok ? "ok" : "FAILED");
    return ok;
}