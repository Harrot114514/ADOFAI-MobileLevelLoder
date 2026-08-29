#pragma once
#include <stdint.h>
#include <stdbool.h>

typedef void* (*pfn_concat2)(void* a, void* b);
typedef void* (*pfn_concat3)(void* a, void* b, void* c);
typedef void* (*pfn_concat4)(void* a, void* b, void* c, void* d);
typedef void* (*pfn_split)(void* str, int32_t sep, int32_t options);

struct MethodInfo;
struct FieldInfo;
struct Il2CppString;

bool game_init();
bool game_ready();
bool game_load_level(const char* level_path);
bool game_pause_for_overlay();
void game_resume_overlay_pause();
bool game_in_gameplay_scene();
bool game_install_load_level_hooks();
bool game_set_no_fail(bool on);
bool game_set_difficulty(int level);
bool game_get_no_fail();
int game_get_difficulty();
void game_apply_queued_options();
void game_queue_quit_to_mobile_menu();
void* game_fix_level_path(void* path);

void game_cleanup_gc_handles();
uint32_t game_protect_object(void* obj);
void game_unprotect_object(uint32_t handle);
bool game_ensure_strings_initialized();
bool game_safe_invoke(const MethodInfo* method, void* obj, void** params, const char* name);

#ifdef __cplusplus
extern "C" {
#endif

bool game_custom_pending();
void* resolve_string_arg(void* p);
bool string_looks_bad(void* s);

extern pfn_concat2 orig_concat2;
extern pfn_concat3 orig_concat3;
extern pfn_concat4 orig_concat4;
extern pfn_split orig_split;
extern Il2CppString* g_empty_string;
extern bool g_strings_ready;

#ifdef __cplusplus
}
#endif

extern uint64_t g_il2cpp_base;