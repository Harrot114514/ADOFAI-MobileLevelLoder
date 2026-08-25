#include "game.hpp"
#include "hooks.hpp"
#include "il2cpp.hpp"
#include "util.hpp"
#include <string.h>
#include <time.h>
#include <atomic>
#include <mutex>

static std::atomic<bool> g_ready{false};
static std::atomic<bool> g_paused_by_us{false};
static std::mutex g_game_mutex;

static uint64_t monotonic_ms() {
    struct timespec ts;
    clock_gettime(CLOCK_MONOTONIC, &ts);
    return (uint64_t)ts.tv_sec * 1000 + (uint64_t)ts.tv_nsec / 1000000;
}

// RVAs (from dump: com.fizzd.connectedworlds_3.3.1.cs)
static const uint64_t RVA_scrController_get_instance = 0x25351AC;
static const uint64_t RVA_scrController_LoadCustomLevel = 0x2547B90;
static const uint64_t RVA_scrController_get_paused     = 0x253CEEC;
static const uint64_t RVA_scrController_TogglePauseGame= 0x2543B1C;
static const uint64_t RVA_ADOBase_get_isScnGame        = 0x241FCAC;

uint64_t g_il2cpp_base = 0;

typedef void* (*pfn_get_controller)(void);
typedef bool  (*pfn_get_paused)(void* self);
typedef bool  (*pfn_is_scn_game)(void);
typedef void  (*pfn_load_custom)(void* self, Il2CppString* path, Il2CppString* id, bool fromBundle);

static pfn_get_controller  fn_get_controller;
static pfn_get_paused      fn_get_paused;
static pfn_is_scn_game     fn_is_scn_game;
static pfn_load_custom     fn_load_custom;

// GCS class statics
static Il2CppClass*  cls_gcs = nullptr;
static FieldInfo*    fld_customLevelIndex = nullptr;
static FieldInfo*    fld_customLevelPaths = nullptr;
static FieldInfo*    fld_internalLevelName = nullptr;
static FieldInfo*    fld_customLevelId = nullptr;
static FieldInfo*    fld_sceneToLoad = nullptr;
static FieldInfo*    fld_useNoFail = nullptr;
static FieldInfo*    fld_difficulty = nullptr;

static const MethodInfo* m_TogglePauseGame = nullptr;

static std::atomic<bool> g_queued_no_fail{false};
static std::atomic<bool> g_queued_no_fail_val{false};
static std::atomic<bool> g_queued_diff{false};
static std::atomic<int>  g_queued_diff_val{0};
static std::atomic<bool> g_queued_quit{false};

static bool resolve_bindings() {
    if (g_ready.load()) return true;
    if (!il2cpp_resolve()) return false;

    Il2CppClass* ctrl = il2cpp_find_class("Assembly-CSharp", "", "scrController");
    if (!ctrl) return false;
    cls_gcs = il2cpp_find_class("Assembly-CSharp", "", "GCS");
    if (!cls_gcs) return false;

    fld_customLevelIndex   = il2cpp.class_get_field_from_name(cls_gcs, "customLevelIndex");
    fld_customLevelPaths   = il2cpp.class_get_field_from_name(cls_gcs, "customLevelPaths");
    fld_internalLevelName  = il2cpp.class_get_field_from_name(cls_gcs, "internalLevelName");
    fld_customLevelId      = il2cpp.class_get_field_from_name(cls_gcs, "customLevelId");
    fld_sceneToLoad        = il2cpp.class_get_field_from_name(cls_gcs, "sceneToLoad");
    fld_useNoFail          = il2cpp.class_get_field_from_name(cls_gcs, "useNoFail");
    fld_difficulty         = il2cpp.class_get_field_from_name(cls_gcs, "difficulty");
    if (!fld_customLevelIndex || !fld_customLevelPaths || !fld_internalLevelName ||
        !fld_customLevelId || !fld_sceneToLoad)
        return false;

    m_TogglePauseGame = il2cpp.class_get_method_from_name(ctrl, "TogglePauseGame", 0);
    if (!m_TogglePauseGame) return false;

    fn_get_controller = (pfn_get_controller)(g_il2cpp_base + RVA_scrController_get_instance);
    fn_get_paused     = (pfn_get_paused)(g_il2cpp_base + RVA_scrController_get_paused);
    fn_is_scn_game    = (pfn_is_scn_game)(g_il2cpp_base + RVA_ADOBase_get_isScnGame);
    fn_load_custom    = (pfn_load_custom)(g_il2cpp_base + RVA_scrController_LoadCustomLevel);

    g_ready.store(true);
    LOGI("game: bindings resolved (GCS=%p scrController=%p)", (void*)cls_gcs, (void*)ctrl);
    return true;
}

bool game_init() {
    if (g_ready.load()) return true;
    if (!g_il2cpp_base) {
        g_il2cpp_base = find_module_base("libil2cpp.so");
        if (!g_il2cpp_base) return false;
    }
    return resolve_bindings();
}

bool game_ready() { return g_ready.load(); }

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

// ------------------------------------------------------------------ NoFail / difficulty

bool game_set_no_fail(bool on) {
    g_queued_no_fail.store(true);
    g_queued_no_fail_val.store(on);
    return true;
}

bool game_set_difficulty(int level) {
    if (level < 0 || level > 2) return false;
    g_queued_diff.store(true);
    g_queued_diff_val.store(level);
    return true;
}

void game_apply_queued_options() {
    std::lock_guard<std::mutex> lk(g_game_mutex);
    
    if (g_queued_no_fail.exchange(false)) {
        bool b = g_queued_no_fail_val.load();
        if (fld_useNoFail) {
            il2cpp.field_static_set_value(fld_useNoFail, &b);
            void* controller = fn_get_controller();
            if (controller) {
                FieldInfo* f = il2cpp.class_get_field_from_name(
                    il2cpp_find_class("Assembly-CSharp", "", "scrController"), "noFail");
                if (f) il2cpp.field_static_set_value(f, &b);
            }
            LOGI("game: noFail set to %d", b ? 1 : 0);
        }
    }
    if (g_queued_diff.exchange(false)) {
        int32_t d = g_queued_diff_val.load();
        if (fld_difficulty) {
            il2cpp.field_static_set_value(fld_difficulty, &d);
            LOGI("game: difficulty set to %d", d);
        }
    }
    if (g_queued_quit.exchange(false)) {
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
    g_queued_quit.store(true);
}

bool game_get_no_fail() {
    if (!g_ready.load() || !fld_useNoFail) return false;
    bool b = false;
    il2cpp.field_static_get_value(fld_useNoFail, &b);
    return b;
}

int game_get_difficulty() {
    if (!g_ready.load() || !fld_difficulty) return 1;
    int32_t d = 1;
    il2cpp.field_static_get_value(fld_difficulty, &d);
    return d;
}

bool game_pause_for_overlay() {
    if (!game_init()) return false;
    if (g_paused_by_us.load()) return true;
    void* controller = fn_get_controller();
    if (!controller) return false;
    if (fn_get_paused(controller)) return false;
    Il2CppException* exc = nullptr;
    il2cpp.runtime_invoke(m_TogglePauseGame, controller, nullptr, &exc);
    if (exc) {
        LOGE("game: TogglePauseGame threw");
        return false;
    }
    g_paused_by_us.store(true);
    LOGI("game: paused for overlay");
    return true;
}

void game_resume_overlay_pause() {
    if (!g_paused_by_us.load()) return;
    if (!game_init()) { g_paused_by_us.store(false); return; }
    void* controller = fn_get_controller();
    if (controller && fn_get_paused(controller)) {
        Il2CppException* exc = nullptr;
        il2cpp.runtime_invoke(m_TogglePauseGame, controller, nullptr, &exc);
        LOGI("game: resumed after overlay close");
    }
    g_paused_by_us.store(false);
}

// ------------------------------------------------------------------ diagnostics
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

static const uint64_t RVA_scnGame_ReloadSong         = 0x251F9D4;
static const uint64_t RVA_scnGame_ReloadCustomSounds = 0x2520ED4;

typedef void (*pfn_void_bool)(void* self, bool force);
static pfn_void_bool orig_reloadsong = nullptr;
static pfn_void_bool orig_reloadcustomsounds = nullptr;

extern "C" void hk_ReloadSong_c(void* self, bool force) {
    orig_reloadsong(self, force);
}
extern "C" void hk_ReloadCustomSounds_c(void* self, bool force) {
    orig_reloadcustomsounds(self, force);
}

static bool game_custom_pending() {
    if (!g_ready.load() || !fld_customLevelPaths) return false;
    void* paths = nullptr;
    il2cpp.field_static_get_value(fld_customLevelPaths, &paths);
    return paths != nullptr;
}

static void* resolve_string_arg(void* p) {
    if (!p) return nullptr;
    uintptr_t u = (uintptr_t)p;
    uintptr_t sp = (uintptr_t)__builtin_frame_address(0);
    bool stack_like = (u > sp - 8u * 1024 * 1024) && (u < sp + 8u * 1024 * 1024);
    if (stack_like) {
        void* real = *(void**)p;
        uintptr_t ru = (uintptr_t)real;
        bool heap_like = ru > 0x6000000000ULL && ru < 0x7600000000ULL;
        if (heap_like) return real;
        return nullptr;
    }
    return p;
}

static bool string_looks_bad(void* s) {
    if (!s) return true;
    int32_t len = *(int32_t*)((uint8_t*)s + 0x10);
    return (len < 0 || len > 0x400000);
}

static const uint64_t RVA_string_Concat2 = 0x3AB8898;
static const uint64_t RVA_string_Concat3 = 0x3AC5674;
static const uint64_t RVA_string_Concat4 = 0x3AC5BD8;
static const uint64_t RVA_string_Split = 0x3AC816C;
static const uint64_t RVA_TextureManager_LoadTexture    = 0x229ED28;
static const uint64_t RVA_TextureManager_LoadNewSprite  = 0x229EBA8;
static const uint64_t RVA_RDFile_Exists      = 0x240AB70;
static const uint64_t RVA_RDFile_ReadAllBytes= 0x2414A80;
static const uint64_t RVA_DetermineDifficultyUIMode = 0x22805A4;
static const uint64_t RVA_LoadTexture_exists_fail = 0x229F130;

typedef void* (*pfn_concat2)(void* a, void* b);
typedef void* (*pfn_concat3)(void* a, void* b, void* c);
typedef void* (*pfn_concat4)(void* a, void* b, void* c, void* d);
typedef void* (*pfn_split)(void* str, int32_t sep, int32_t options);
typedef void* (*pfn_loadtexture)(void* filePath, void* status, int32_t maxSide);
typedef void* (*pfn_loadsprite)(void* filePath, void* status, float ppu, int32_t type);
typedef bool (*pfn_exists)(void* path);
typedef void* (*pfn_readallbytes)(void* path, void* status);
typedef int32_t (*pfn_difmode)(float bpm);

static pfn_concat2 orig_concat2 = nullptr;
static pfn_concat3 orig_concat3 = nullptr;
static pfn_concat4 orig_concat4 = nullptr;
static pfn_split orig_split = nullptr;
static pfn_loadtexture orig_loadtexture = nullptr;
static pfn_loadsprite orig_loadsprite = nullptr;
static pfn_exists orig_rd_exists = nullptr;
static pfn_readallbytes orig_rd_readall = nullptr;
static pfn_difmode orig_difmode = nullptr;

static Il2CppString* g_empty_string = nullptr;
static void* g_texture_path = nullptr;
static void* g_saved_internal_name = nullptr;

static void gcs_write_internal_level_name_raw(void* value) {
    if (!cls_gcs) return;
    void* statics = *(void**)((uint8_t*)cls_gcs + 0xB8);
    if (!statics) return;
    *(void**)((uint8_t*)statics + 0x80) = value;
}

static void begin_texture_load_window(void* filePath) {
    if (!g_ready.load() || !cls_gcs) return;
    il2cpp.field_static_get_value(fld_internalLevelName, &g_saved_internal_name);
    gcs_write_internal_level_name_raw(nullptr);
}
static void end_texture_load_window() {
    if (!g_saved_internal_name) return;
    gcs_write_internal_level_name_raw(g_saved_internal_name);
    g_saved_internal_name = nullptr;
}

static bool looks_file_path(void* s) {
    if (!s) return false;
    int32_t len = *(int32_t*)((uint8_t*)s + 0x10);
    if (len < 2 || len > 0x400000) return false;
    const uint16_t* chars = (const uint16_t*)((uint8_t*)s + 0x14);
    return chars[0] == '/';
}

extern "C" void* hk_concat2_c(void* a, void* b) {
    if (game_custom_pending()) {
        if (!g_empty_string) g_empty_string = il2cpp.string_new("");
        a = resolve_string_arg(a); if (!a) a = g_empty_string;
        b = resolve_string_arg(b); if (!b) b = g_empty_string;
    }
    return orig_concat2(a, b);
}

extern "C" void* hk_concat3_c(void* a, void* b, void* c) {
    if (game_custom_pending()) {
        if (!g_empty_string) g_empty_string = il2cpp.string_new("");
        a = resolve_string_arg(a); if (!a) a = g_empty_string;
        b = resolve_string_arg(b); if (!b) b = g_empty_string;
        c = resolve_string_arg(c); if (!c) c = g_empty_string;
    }
    return orig_concat3(a, b, c);
}

extern "C" void* hk_concat4_c(void* a, void* b, void* c, void* d) {
    if (game_custom_pending()) {
        if (!g_empty_string) g_empty_string = il2cpp.string_new("");
        a = resolve_string_arg(a); if (!a) a = g_empty_string;
        b = resolve_string_arg(b); if (!b) b = g_empty_string;
        c = resolve_string_arg(c); if (!c) c = g_empty_string;
        d = resolve_string_arg(d); if (!d) d = g_empty_string;
    }
    return orig_concat4(a, b, c, d);
}

extern "C" void* hk_split_c(void* str, int32_t sep, int32_t options) {
    if (game_custom_pending()) {
        if (!g_empty_string) g_empty_string = il2cpp.string_new("");
        str = resolve_string_arg(str);
        if (!str) str = g_empty_string;
    }
    return orig_split(str, sep, options);
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

// ------------------------------------------------------------------ load hooks
static const uint64_t RVA_scnGame_LoadAndPlayLevel = 0x251DA58;
static const uint64_t RVA_scnGame_LoadLevel        = 0x251E060;
static const uint64_t RVA_LevelData_LoadLevel      = 0x24BDFA0;
static const uint64_t RVA_ADOBase_get_isInternalLevel = 0x2412EA0;
static const uint64_t RVA_ADOBase_get_isDLCLevel     = 0x24200B4;
static const uint64_t RVA_ADOBase_get_isBossLevel    = 0x242014C;
static const uint64_t RVA_ADOBase_get_isCLSBossLevel = 0x2420220;
static const uint64_t RVA_LevelData_get_songFilename = 0x24C5040;
static const uint64_t RVA_ReloadSongCo_MoveNext = 0x252CD34;

typedef bool (*pfn_loadandplay)(void* self, void* path);
typedef bool (*pfn_loadlevel2)(void* self, void* path, void* status);
typedef bool (*pfn_isinternal)(void);
typedef bool (*pfn_boolgetter)(void);
typedef Il2CppString* (*pfn_get_song_filename)(void* self);
typedef bool (*pfn_movenext)(void* self);

static pfn_loadandplay orig_loadandplay = nullptr;
static pfn_loadlevel2  orig_scngame_loadlevel = nullptr;
static pfn_loadlevel2  orig_leveldata_loadlevel = nullptr;
static pfn_isinternal  orig_get_isinternal = nullptr;
static pfn_boolgetter orig_get_isdlclevel = nullptr;
static pfn_boolgetter orig_get_isbosslevel = nullptr;
static pfn_boolgetter orig_get_isclsbosslevel = nullptr;
static pfn_movenext orig_reloadsongco_movenext = nullptr;
static void* g_root_song_key = nullptr;

void* game_fix_level_path(void* path) {
    if (!g_ready.load() || !fld_customLevelPaths) return path;
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
    if (!g_ready.load() || !self) return;
    if (!game_custom_pending()) return;
    void* levelData = *(void**)((uint8_t*)self + 0x70);
    if (!levelData) return;
    Il2CppString* fn = ((pfn_get_song_filename)(g_il2cpp_base + RVA_LevelData_get_songFilename))(levelData);
    if (!fn) {
        fn = (Il2CppString*)*(void**)((uint8_t*)self + 0x80);
    }
    if (fn && *(void**)((uint8_t*)self + 0xC0) == nullptr) {
        *(void**)((uint8_t*)self + 0xC0) = fn;
        g_root_song_key = fn;
        LOGI("game: currentSongKey set (len=%d)", fn->length);
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

bool game_install_load_level_hooks() {
    if (orig_loadandplay && orig_scngame_loadlevel) return true;
    if (!g_il2cpp_base) return false;

    game_install_diag_hooks();

    orig_concat2 = (pfn_concat2)hook_install(
        (void*)(g_il2cpp_base + RVA_string_Concat2), (void*)&hk_concat2_c);
    orig_concat3 = (pfn_concat3)hook_install(
        (void*)(g_il2cpp_base + RVA_string_Concat3), (void*)&hk_concat3_c);
    orig_concat4 = (pfn_concat4)hook_install(
        (void*)(g_il2cpp_base + RVA_string_Concat4), (void*)&hk_concat4_c);
    orig_split = (pfn_split)hook_install(
        (void*)(g_il2cpp_base + RVA_string_Split), (void*)&hk_split_c);
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