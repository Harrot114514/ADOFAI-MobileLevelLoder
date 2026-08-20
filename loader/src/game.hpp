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

// Replace a possibly-wrong level path with customLevelPaths[0] when a custom
// level is pending. Pointer comparison only.
void* game_fix_level_path(void* path);

extern uint64_t g_il2cpp_base;
