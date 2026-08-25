#pragma once
#include <stdint.h>

// Input layer: hooks UnityEngine.Input (legacy input) which is the single
// funnel for touches in this game (game -> Rewired -> UnityEngine.Input,
// and Unity UI EventSystem -> UnityEngine.Input as well).
//
// Touches are captured on the game thread and either forwarded to the game
// (overlay closed) or suppressed (overlay open). A small floating button
// toggles the overlay; taps on it are consumed before the game sees them.

struct TouchEvent {
    float x, y;
    int32_t phase;   // 0 Began, 1 Moved, 2 Stationary, 3 Ended, 4 Canceled
    int32_t finger;
};

// Install the input hooks. Call once after libil2cpp.so is loaded.
bool input_hooks_install();

// Called by the overlay (render thread) each frame:
//  - set the floating button rect (ImGui coords, y-down). Stored flipped to
//    Unity touch convention (y-up) for main-thread detection. Ignored while
//    the user is dragging the button (main thread owns the rect then).
//  - check if the button was tapped (toggles overlay internally)
void input_set_button_rect(float x0, float y0, float x1, float y1, float display_w, float display_h);
void input_get_button_rect(float* x0, float* y0, float* x1, float* y1); // Unity touch coords (y-up)
bool input_button_tapped();

// UI open state (set by overlay).
void input_set_ui_open(bool open);
bool input_ui_open();

// Pop captured touches for this frame (render thread).
// Returns number of touches copied into out (max n).
int input_pop_touches(TouchEvent* out, int n);

// Main-thread action queue (executed from the touch hook).
void input_queue_load_level(const char* path);
void input_queue_pause_toggle_for_overlay();
void input_queue_resume_overlay_pause();

#ifdef INPUT_TEST
// test-only: inject the "original" function pointers (host simulation)
void input_test_set_orig(void* touchcount, void* gettouch, void* mb, void* mbd,
                         void* mbu, void* mousepos, void* gettouches);
#endif
