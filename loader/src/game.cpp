#include "game.hpp"
#include "hooks.hpp"
#include "il2cpp.hpp"
#include "util.hpp"
#include <string.h>
#include <time.h>

static uint64_t monotonic_ms() {
    struct timespec ts;
    clock_gettime(CLOCK_MONOTONIC, &ts);
    return (uint64_t)ts.tv_sec * 1000 + (uint64_t)ts.tv_nsec / 1000000;
}

// RVAs (from dump: com.fizzd.connectedworlds_3.3.1.cs)
static const uint64_t RVA_scrController_get_instance = 0x25351AC;   // public static scrController get_instance()
static const uint64_t RVA_scrController_LoadCustomLevel = 0x2547B90; // void LoadCustomLevel(string levelPath, string levelId, bool fromBundle)
static const uint64_t RVA_scrController_get_paused     = 0x253CEEC;  // public bool get_paused()
static const uint64_t RVA_scrController_TogglePauseGame= 0x2543B1C;  // public bool TogglePauseGame()
static const uint64_t RVA_ADOBase_get_isScnGame        = 0x241FCAC;  // public static bool get_isScnGame()

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
static FieldInfo*    fld_customLevelIndex = nullptr;   // int32
static FieldInfo*    fld_customLevelPaths = nullptr;   // string[]
static FieldInfo*    fld_internalLevelName = nullptr;  // string
static FieldInfo*    fld_customLevelId = nullptr;      // string
static FieldInfo*    fld_sceneToLoad = nullptr;        // string

// methods invoked via il2cpp_runtime_invoke (safe: managed exceptions are
// returned instead of unwinding through our statically-linked libc++abi)
static const MethodInfo* m_TogglePauseGame = nullptr;   // ()

static bool g_ready = false;
static bool g_paused_by_us = false;

static bool resolve_bindings() {
    if (g_ready) return true;
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
    if (!fld_customLevelIndex || !fld_customLevelPaths || !fld_internalLevelName ||
        !fld_customLevelId || !fld_sceneToLoad)
        return false;

    // MethodInfo lookup (verify presence), but we call by address.
    const MethodInfo* m = il2cpp.class_get_method_from_name(ctrl, "get_instance", 0);
    (void)m;
    m_TogglePauseGame = il2cpp.class_get_method_from_name(ctrl, "TogglePauseGame", 0);
    if (!m_TogglePauseGame) return false;

    fn_get_controller = (pfn_get_controller)(g_il2cpp_base + RVA_scrController_get_instance);
    fn_get_paused     = (pfn_get_paused)(g_il2cpp_base + RVA_scrController_get_paused);
    fn_is_scn_game    = (pfn_is_scn_game)(g_il2cpp_base + RVA_ADOBase_get_isScnGame);
    fn_load_custom    = (pfn_load_custom)(g_il2cpp_base + RVA_scrController_LoadCustomLevel);

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

bool game_ready() { return g_ready; }

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

    // PC leftover state that must be sane before entering scnGame:
    //  - GCS.customLevelIndex = 0
    //  - GCS.internalLevelName = levelPath  (non-null so the game's save system
    //    records a valid string instead of null; the internal-level branches
    //    are neutralized by our get_isInternalLevel hook instead)
    //  - GCS.customLevelId = null
    //  - GCS.sceneToLoad = "scnGame"    (LoadTargetScene loads it after the wipe)
    set_int_field(fld_customLevelIndex, 0);
    set_string_field(fld_internalLevelName, level_path);
    set_string_field(fld_customLevelId, nullptr);
    set_string_field(fld_sceneToLoad, "scnGame");

    // scrController.LoadCustomLevel sets GCS.customLevelPaths=[path] and
    // triggers StartLoadingScene -> wipe -> scnGame -> LoadAndPlayLevel.
    // Called DIRECTLY via its native address (verified signature:
    // void LoadCustomLevel(this, string path, string id, bool fromBundle)):
    // the bool goes in w3, no boxing needed. (il2cpp_value_box proved
    // unreliable on this device/ROM.)
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

bool game_pause_for_overlay() {
    if (!game_init()) return false;
    if (g_paused_by_us) return true;
    void* controller = fn_get_controller();
    if (!controller) return false; // no gameplay scene (menu)
    if (fn_get_paused(controller)) return false;
    Il2CppException* exc = nullptr;
    il2cpp.runtime_invoke(m_TogglePauseGame, controller, nullptr, &exc);
    if (exc) {
        LOGE("game: TogglePauseGame threw");
        return false;
    }
    g_paused_by_us = true;
    LOGI("game: paused for overlay");
    return true;
}

void game_resume_overlay_pause() {
    if (!g_paused_by_us) return;
    if (!game_init()) { g_paused_by_us = false; return; }
    void* controller = fn_get_controller();
    if (controller && fn_get_paused(controller)) {
        Il2CppException* exc = nullptr;
        il2cpp.runtime_invoke(m_TogglePauseGame, controller, nullptr, &exc);
        LOGI("game: resumed after overlay close");
    }
    g_paused_by_us = false;
}

// ------------------------------------------------------------------ diagnostics
// Safety fuses (silent in release builds).

static const uint64_t RVA_string_new_size = 0x1F87C60; // String::NewSize(len)

typedef void* (*pfn_string_new_size)(int len);
static pfn_string_new_size orig_string_new_size = nullptr;

extern "C" void* hk_string_new_size_c(int len) {
    // Fuse: a corrupted (negative) string length must not reach the
    // allocator (this il2cpp build crashes on it). Clamp negative lengths;
    // large positive lengths are legitimate (multi-MB JSON strings).
    if (len < 0) len = 64;
    return orig_string_new_size(len);
}

static void game_install_diag_hooks() {
    if (!g_il2cpp_base) return;
    orig_string_new_size = (pfn_string_new_size)hook_install(
        (void*)(g_il2cpp_base + RVA_string_new_size), (void*)&hk_string_new_size_c);
}

// The song-loading machinery is the source of the dangling-string corruption
// on the custom path. Neutralize the entry points: skip them entirely so the
// level loads silently and the corrupted-string code never runs.
static const uint64_t RVA_scnGame_ReloadSong         = 0x251F9D4; // void ReloadSong(bool force)
static const uint64_t RVA_scnGame_ReloadCustomSounds = 0x2520ED4; // void ReloadCustomSounds(bool force)

static bool game_custom_pending(); // defined below

typedef void (*pfn_void_bool)(void* self, bool force);
static pfn_void_bool orig_reloadsong = nullptr;
static pfn_void_bool orig_reloadcustomsounds = nullptr;

extern "C" void hk_ReloadSong_c(void* self, bool force) {
    orig_reloadsong(self, force);
}
extern "C" void hk_ReloadCustomSounds_c(void* self, bool force) {
    orig_reloadcustomsounds(self, force);
}

// string.Concat(str0,str1,str2,str3) - one of its args is a dangling string
// during custom level loading. Log the caller + args to find the source.
static const uint64_t RVA_string_Concat4 = 0x3AC5BD8;

typedef void* (*pfn_concat4)(void* s0, void* s1, void* s2, void* s3);
static pfn_concat4 orig_concat4 = nullptr;
static Il2CppString* g_empty_string = nullptr;

// A stack-like pointer passed as a managed string: the il2cpp codegen bug in
// TextureManager.LoadTexture passes &localString instead of the string.
static bool stack_like(void* p) {
    uintptr_t u = (uintptr_t)p;
    uintptr_t sp = (uintptr_t)__builtin_frame_address(0);
    return (u > sp - 8u * 1024 * 1024) && (u < sp + 8u * 1024 * 1024);
}

// GC heap-ish pointer range (observed heap: 0x7000000000-0x7400000000)
static bool heap_like(void* p) {
    uintptr_t u = (uintptr_t)p;
    return u > 0x6000000000ULL && u < 0x7600000000ULL;
}

static bool string_looks_bad(void* s); // defined below

// Resolve a possibly-mispassed string argument: if it is a stack address
// (a pointer to a local string VARIABLE), dereference it to the real string.
// Returns nullptr if it cannot be resolved safely.
static void* resolve_string_arg(void* p) {
    if (!p) return nullptr;
    if (stack_like(p)) {
        void* real = *(void**)p;
        if (heap_like(real) && !string_looks_bad(real)) return real;
        return nullptr;
    }
    return p;
}

// string.Split(char separator, options) - the same codegen bug hits it: the
// string argument can be a stack address. Fix it the same way.
static const uint64_t RVA_string_Split = 0x3AC816C;

typedef void* (*pfn_split)(void* str, int32_t sep, int32_t options);
static pfn_split orig_split = nullptr;

extern "C" void* hk_split_c(void* str, int32_t sep, int32_t options) {
    if (game_custom_pending()) {
        if (!g_empty_string) g_empty_string = il2cpp.string_new("");
        str = resolve_string_arg(str);
        if (!str) str = g_empty_string;
    }
    return orig_split(str, sep, options);
}

// string.Concat overloads: all of them can receive the mis-passed stack
// pointer (il2cpp codegen bug) - fix every overload.
static const uint64_t RVA_string_Concat2 = 0x3AB8898;
static const uint64_t RVA_string_Concat3 = 0x3AC5674;

typedef void* (*pfn_concat2)(void* a, void* b);
typedef void* (*pfn_concat3)(void* a, void* b, void* c);
static pfn_concat2 orig_concat2 = nullptr;
static pfn_concat3 orig_concat3 = nullptr;

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

extern "C" void* hk_concat4_c(void* s0, void* s1, void* s2, void* s3) {
    if (game_custom_pending()) {
        s0 = resolve_string_arg(s0); if (!s0) s0 = g_empty_string;
        s1 = resolve_string_arg(s1); if (!s1) s1 = g_empty_string;
        s2 = resolve_string_arg(s2); if (!s2) s2 = g_empty_string;
        s3 = resolve_string_arg(s3); if (!s3) s3 = g_empty_string;
        // lazy init of the empty string (game thread)
        if (!g_empty_string) g_empty_string = il2cpp.string_new("");
    }
    return orig_concat4(s0, s1, s2, s3);
}

// TextureManager.LoadTexture / LoadNewSprite receive a dangling filePath
// (async UniTask path captures a span over a collected temporary string -
// a PC-leftover bug exposed by mobile GC timing). Validate the string and
// skip the load when it is corrupted.
static const uint64_t RVA_TextureManager_LoadTexture    = 0x229ED28;
static const uint64_t RVA_TextureManager_LoadNewSprite  = 0x229EBA8;

// LoadTexture(string filePath, out LoadResult status, int maxSideSize) -> Texture2D
typedef void* (*pfn_loadtexture)(void* filePath, void* status, int32_t maxSide);
// LoadNewSprite(string filePath, out LoadResult status, float ppu, SpriteMeshType type) -> Sprite
typedef void* (*pfn_loadsprite)(void* filePath, void* status, float ppu, int32_t type);
static pfn_loadtexture orig_loadtexture = nullptr;
static pfn_loadsprite  orig_loadsprite = nullptr;

static void* g_texture_path = nullptr; // set only while LoadTexture executes

// Absolute file path detection: real filesystem paths start with '/'.
static bool looks_file_path(void* s) {
    if (!s) return false;
    int32_t len = *(int32_t*)((uint8_t*)s + 0x10);
    if (len < 2 || len > 0x400000) return false;
    const uint16_t* chars = (const uint16_t*)((uint8_t*)s + 0x14);
    return chars[0] == '/';
}

// Temporarily null GCS.internalLevelName so LoadTexture takes the PC
// custom-level path (plain file loading, no sprite-metadata rebuild).
// Restores the value afterwards.
//
// NOTE: il2cpp_field_static_set_value's write barrier mishandles a NULL
// value (it stores the ADDRESS of our local instead of null - observed on
// device). Null needs no GC barrier, so we write the static slot directly:
// Il2CppClass.static_fields (offset 0xB8) + field offset 0x80.
static void* g_saved_internal_name = nullptr;
static void* g_root_internal_name = nullptr; // GC anchor for the raw restore

static void gcs_write_internal_level_name_raw(void* value) {
    if (!cls_gcs) return;
    void* statics = *(void**)((uint8_t*)cls_gcs + 0xB8);
    if (!statics) return;
    *(void**)((uint8_t*)statics + 0x80) = value;
}

static void begin_texture_load_window(void* filePath) {
    if (!looks_file_path(filePath)) return; // internal/resource sprites unaffected
    if (!g_ready || !cls_gcs) return;
    il2cpp.field_static_get_value(fld_internalLevelName, &g_saved_internal_name);
    gcs_write_internal_level_name_raw(nullptr);
}
static void end_texture_load_window() {
    if (!g_saved_internal_name) return;
    gcs_write_internal_level_name_raw(g_saved_internal_name);
    g_root_internal_name = g_saved_internal_name; // keep alive (no barrier)
    g_saved_internal_name = nullptr;
}

static bool string_looks_bad(void* s) {
    if (!s) return true;
    int32_t len = *(int32_t*)((uint8_t*)s + 0x10);
    return (len < 0 || len > 0x400000);
}

extern "C" void* hk_LoadTexture_c(void* filePath, void* status, int32_t maxSide) {
    if (game_custom_pending()) {
        filePath = resolve_string_arg(filePath);
        if (!filePath || string_looks_bad(filePath)) {
            // signal an error result (LoadResult.Error = 3) and return null
            if (status) *(int32_t*)status = 3;
            return nullptr;
        }
        // log what is being loaded (content of the resolved path)
        g_texture_path = filePath; // guard window for the path substitution
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

// LoadTexture rebuilds the texture path from GCS.internalLevelName (PC logic:
// internal levels live in world folders). We set internalLevelName to the
// level path, so the rebuilt path is garbage -> RDFile.Exists fails ->
// MissingFile. Fix: while LoadTexture runs, substitute the ORIGINAL path in
// RDFile.Exists / RDFile.ReadAllBytes. Additionally force the Exists-failure
// branch to fall through (the ReadAllBytes hook substitutes the path anyway).
static const uint64_t RVA_RDFile_Exists      = 0x240AB70; // static bool Exists(string)
static const uint64_t RVA_RDFile_ReadAllBytes= 0x2414A80; // static byte[] ReadAllBytes(string, out LoadResult)
static const uint64_t RVA_LoadTexture_exists_fail = 0x229F130; // tbz -> NOP
static const uint64_t RVA_LoadTexture_skip_lookup = 0x229F0A8; // lookup-fail return -> jump to file path

typedef bool (*pfn_exists)(void* path);
typedef void* (*pfn_readallbytes)(void* path, void* status);
static pfn_exists orig_rd_exists = nullptr;
static pfn_readallbytes orig_rd_readall = nullptr;

extern "C" bool hk_RDFile_Exists_c(void* path) {
    if (g_texture_path && path != g_texture_path) path = g_texture_path;
    return orig_rd_exists(path);
}

extern "C" void* hk_RDFile_ReadAllBytes_c(void* path, void* status) {
    if (g_texture_path && path != g_texture_path) path = g_texture_path;
    return orig_rd_readall(path, status);
}

// ---------------------------------------------------------------- load hooks
// ---------------------------------------------------------------- load hooks
// The game's LevelData.LoadLevel takes the INTERNAL-level branch (WorldData
// dictionary lookups keyed by the level path, path.Split('-'), ...) whenever
// ADOBase.get_isInternalLevel() == (GCS.internalLevelName != null). The game's
// own load pipeline keeps writing internalLevelName, so instead of fighting
// it we leave internalLevelName = levelPath (keeps the save system happy) and
// force get_isInternalLevel() to report FALSE whenever a custom level is
// pending. Entry hooks additionally swap in customLevelPaths[0] if a bogus
// path was passed.

static const uint64_t RVA_scnGame_LoadAndPlayLevel = 0x251DA58;
static const uint64_t RVA_scnGame_LoadLevel        = 0x251E060;
static const uint64_t RVA_LevelData_LoadLevel      = 0x24BDFA0;
static const uint64_t RVA_ADOBase_get_isInternalLevel = 0x2412EA0;

// True if a custom level is pending (GCS.customLevelPaths != null).
static bool game_custom_pending() {
    if (!g_ready || !fld_customLevelPaths) return false;
    void* paths = nullptr;
    il2cpp.field_static_get_value(fld_customLevelPaths, &paths);
    return paths != nullptr;
}

// If a custom level is pending but the pipeline was handed a different path
// (e.g. a stale internal-level name), swap in customLevelPaths[0]. Pointer-only
// comparison: never dereferences the (possibly bogus) incoming string.
void* game_fix_level_path(void* path) {
    if (!g_ready || !fld_customLevelPaths) return path;
    void* paths = nullptr;
    il2cpp.field_static_get_value(fld_customLevelPaths, &paths);
    if (!paths) return path;
    int32_t len = *(int32_t*)((uint8_t*)paths + 0x18); // il2cpp array max_length
    if (len < 1) return path;
    void* first = *(void**)((uint8_t*)paths + 0x20);   // element 0 (ref array data)
    if (first && first != path) {
        LOGI("game: level path swapped (%p -> %p)", path, first);
        return first;
    }
    return path;
}

// scnGame.LoadAndPlayLevel(string levelPath) -> bool
typedef bool (*pfn_loadandplay)(void* self, void* path);
// scnGame.LoadLevel / LevelData.LoadLevel(string levelPath, out LoadResult) -> bool
typedef bool (*pfn_loadlevel2)(void* self, void* path, void* status);
// ADOBase.get_isInternalLevel() -> bool
typedef bool (*pfn_isinternal)(void);
// ADOFAI.LevelData.get_songFilename() -> string (instance)
typedef Il2CppString* (*pfn_get_song_filename)(void* self);

// RVA: public System.String ADOFAI.LevelData.get_songFilename(); // 0x24c5040
static const uint64_t RVA_LevelData_get_songFilename = 0x24C5040;

static pfn_loadandplay orig_loadandplay = nullptr;
static pfn_loadlevel2  orig_scngame_loadlevel = nullptr;
static pfn_loadlevel2  orig_leveldata_loadlevel = nullptr;
static pfn_isinternal  orig_get_isinternal = nullptr;

// On the custom-level path nothing ever sets scnGame.currentSongKey (field
// @0xC0); ReloadSongCo loops on IsNullOrEmpty(currentSongKey) forever,
// reloading the song each iteration (memory explosion -> crash). Set it to
// the level's song filename after scnGame.LoadLevel finishes.
static void* g_root_song_key = nullptr; // GC anchor for the manual field write
static void game_set_current_song_key(void* self) {
    if (!g_ready || !self) return;
    if (!game_custom_pending()) return;
    void* levelData = *(void**)((uint8_t*)self + 0x70); // scnGame.levelData
    if (!levelData) return;
    Il2CppString* fn = ((pfn_get_song_filename)(g_il2cpp_base + RVA_LevelData_get_songFilename))(levelData);
    if (!fn) {
        // no song setting: fall back to the level path (still non-empty)
        fn = (Il2CppString*)*(void**)((uint8_t*)self + 0x80); // scnGame.levelPath
    }
    if (fn && *(void**)((uint8_t*)self + 0xC0) == nullptr) {
        *(void**)((uint8_t*)self + 0xC0) = fn; // scnGame.currentSongKey
        g_root_song_key = fn;                  // keep it alive for the GC
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

// ADOBase.get_isDLCLevel / get_isBossLevel / get_isCLSBossLevel do
// WorldData.dict[scrController.currentWorldString] lookups; for custom levels
// currentWorldString is null on mobile -> ArgumentNullException("key").
// Custom levels are never DLC/boss levels, so short-circuit them to false.
static const uint64_t RVA_ADOBase_get_isDLCLevel     = 0x24200B4;
static const uint64_t RVA_ADOBase_get_isBossLevel    = 0x242014C;
static const uint64_t RVA_ADOBase_get_isCLSBossLevel = 0x2420220;

// ReloadSongCo (scnGame's song-loading coroutine) contains an unconditional
// retry branch at 0x252d41c: "b 0x252cf14". On the custom-level path it loops
// and reloads the song thousands of times per second -> OOM. Redirect the
// branch to the bailout path (0x252d420, "return false") instead.
static const uint64_t RVA_ReloadSongCo_loopback = 0x252D41C;
// also throttle MoveNext: end the coroutine if driven > 240 times/second
static const uint64_t RVA_ReloadSongCo_MoveNext = 0x252CD34;

// Surgical patches to neutralize the runaway state machine completely:
//  0x252CD58: "tbnz w8,#0, 0x252ce6c" (state dispatch) -> "b 0x252d424"
//             (return false - coroutine finishes immediately)
//  0x252CEFC: "bl get_isInternalLevel" -> "mov w0, wzr"
//             (the check always sees false; the loop never even calls out)
//  0x252D4F0: "bl il2cpp_raise_exception" -> "b 0x252d424"
//             (all error paths become a clean "coroutine finished" instead
//              of throwing - this il2cpp build's exception machinery crashes)
static const uint64_t RVA_RSC_patch_dispatch = 0x252CD58;
static const uint64_t RVA_RSC_patch_isint     = 0x252CEFC;
static const uint64_t RVA_RSC_patch_raise     = 0x252D4F0;

typedef bool (*pfn_boolgetter)(void);
static pfn_boolgetter orig_get_isdlclevel = nullptr;
static pfn_boolgetter orig_get_isbosslevel = nullptr;
static pfn_boolgetter orig_get_isclsbosslevel = nullptr;

// ReloadSongCo.MoveNext -> bool (kept as a passthrough hook slot)
typedef bool (*pfn_movenext)(void* self);
static pfn_movenext orig_reloadsongco_movenext = nullptr;

extern "C" bool hk_ReloadSongCo_MoveNext_c(void* self) {
    return orig_reloadsongco_movenext(self);
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
    // NOP the "Exists failed -> return" branch inside LoadTexture
    patch_write_u32((void*)(g_il2cpp_base + RVA_LoadTexture_exists_fail), 0xD503201F);
    // NOTE: no skip-lookup patch - the temporary-null window (in
    // hk_LoadTexture_c) makes LoadTexture take the proper custom-level path
    // for filesystem paths, which does not touch the sprite registry.

    orig_loadandplay = (pfn_loadandplay)hook_install(
        (void*)(g_il2cpp_base + RVA_scnGame_LoadAndPlayLevel), (void*)&hk_LoadAndPlayLevel_c);
    orig_scngame_loadlevel = (pfn_loadlevel2)hook_install(
        (void*)(g_il2cpp_base + RVA_scnGame_LoadLevel), (void*)&hk_LoadLevel_c);
    orig_leveldata_loadlevel = (pfn_loadlevel2)hook_install(
        (void*)(g_il2cpp_base + RVA_LevelData_LoadLevel), (void*)&hk_LevelData_LoadLevel_c);
    orig_get_isinternal = (pfn_isinternal)hook_install(
        (void*)(g_il2cpp_base + RVA_ADOBase_get_isInternalLevel), (void*)&hk_get_isInternalLevel_c);

    // DLC/boss getters: null-key crash guards
    orig_get_isdlclevel = (pfn_boolgetter)hook_install(
        (void*)(g_il2cpp_base + RVA_ADOBase_get_isDLCLevel), (void*)&hk_get_isDLCLevel_c);
    orig_get_isbosslevel = (pfn_boolgetter)hook_install(
        (void*)(g_il2cpp_base + RVA_ADOBase_get_isBossLevel), (void*)&hk_get_isBossLevel_c);
    orig_get_isclsbosslevel = (pfn_boolgetter)hook_install(
        (void*)(g_il2cpp_base + RVA_ADOBase_get_isCLSBossLevel), (void*)&hk_get_isCLSBossLevel_c);

    // ReloadSongCo: throttle runaway MoveNext driving
    orig_reloadsongco_movenext = (pfn_movenext)hook_install(
        (void*)(g_il2cpp_base + RVA_ReloadSongCo_MoveNext), (void*)&hk_ReloadSongCo_MoveNext_c);

    // Rewrite the coroutine's unconditional retry branch into the bailout path.
    // Original: b 0x252cf14  (0x17fffebe). New: b 0x252d420 (0x14000001).
    patch_write_u32((void*)(g_il2cpp_base + RVA_ReloadSongCo_loopback), 0x14000001);
    // Replace the get_isInternalLevel call with "mov w0, wzr" (always false,
    // so the coroutine takes the CUSTOM-song branch).
    patch_write_u32((void*)(g_il2cpp_base + RVA_RSC_patch_isint), 0x2A1F03E0);
    // Replace the coroutine's raise_exception with a clean return (0x252d424).
    patch_write_u32((void*)(g_il2cpp_base + RVA_RSC_patch_raise), 0x17FFFFCD);

    // Skip the song machinery on custom levels (level plays silently).
    orig_reloadsong = (pfn_void_bool)hook_install(
        (void*)(g_il2cpp_base + RVA_scnGame_ReloadSong), (void*)&hk_ReloadSong_c);
    orig_reloadcustomsounds = (pfn_void_bool)hook_install(
        (void*)(g_il2cpp_base + RVA_scnGame_ReloadCustomSounds), (void*)&hk_ReloadCustomSounds_c);

    // diagnostic probes

    bool ok = orig_loadandplay && orig_scngame_loadlevel && orig_leveldata_loadlevel && orig_get_isinternal;
    LOGI("game: load-level hooks %s", ok ? "ok" : "FAILED");
    return ok;
}
