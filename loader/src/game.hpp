#pragma once
#include <stdint.h>
#include <stdbool.h>

// Game (A Dance of Fire and Ice 3.3.1, Unity 6000.3.10f1, il2cpp) bindings.
// All addresses are RVAs inside libil2cpp.so, verified against the libtool dump.

// Initializes bindings. Returns true when everything is resolved.
bool game_init();

// True if bindings are ready.
bool game_ready();

// Load a custom .adofai level. Must be called on the game (main) thread.
// Returns false if the call could not be issued.
bool game_load_level(const char* level_path);

// Pause game if currently in a gameplay scene (used while the overlay is open).
// Returns true if we paused it.
bool game_pause_for_overlay();

// Resume the game if we paused it.
void game_resume_overlay_pause();

// If a gameplay scene is active.
bool game_in_gameplay_scene();

// Install entry hooks on the level-load pipeline that force the custom-level
// branch (get_isInternalLevel reports false) and normalize the level path.
bool game_install_load_level_hooks();

// If a custom level is pending (GCS.customLevelPaths != null), make sure
// GCS.internalLevelName is null. Called from the load-level entry hooks.
void game_force_custom_state();

// NoFail (不败) / difficulty controls. Queued to the game main thread.
bool game_set_no_fail(bool on);     // returns whether it was applied
bool game_set_difficulty(int level); // 0=Lenient 1=Normal 2=Strict
bool game_get_no_fail();             // current GCS.useNoFail
int  game_get_difficulty();          // current GCS.difficulty
// Apply queued option changes. Call on the game main thread.
void game_apply_queued_options();
// Queue a clean quit to the mobile main menu (bypasses the game's own buggy
// PC-leftover quit path).
void game_queue_quit_to_mobile_menu();

// Replace a possibly-wrong level path with customLevelPaths[0] when a custom
// level is pending. Pointer comparison only.
void* game_fix_level_path(void* path);

extern uint64_t g_il2cpp_base;
