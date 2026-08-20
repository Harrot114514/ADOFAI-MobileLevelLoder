// Simulates the full input pipeline: game Input functions -> our hooks -> overlay
#include <stdio.h>
#include <string.h>
#include <stdint.h>
#include <assert.h>

// ---- stubs for game.cpp
static char g_last_load[512] = "";
static int g_paused = 0; static int g_pause_count = 0;
bool game_load_level(const char* p) { snprintf(g_last_load, sizeof(g_last_load), "%s", p); return true; }
bool game_ready() { return true; }
bool game_init() { return true; }
bool game_in_gameplay_scene() { return false; }
bool game_pause_for_overlay() { if (g_paused) return false; g_paused = 1; g_pause_count++; return true; }
void game_resume_overlay_pause() { if (g_paused) { g_paused = 0; } }

uint64_t g_il2cpp_base = 0x100000;
void* hook_install(void* target, void* handler);
extern "C" void hk_GetTouch_asm(void);
extern "C" void call_get_touch_orig(void* fn, void* retbuf, int index);
#include "input.cpp"

// ---- fake Unity Input layer (simulates the real one)
static TouchEvent g_fake[8]; static int g_fake_n = 0;

// asm stubs so the first instruction is non-PC-relative (short-patch friendly)
extern "C" void real_input_get_touchCount();
extern "C" void real_input_get_touch();
extern "C" int32_t c_real_get_touchCount();
extern "C" void c_real_get_touch(int32_t i, void* retbuf);
asm(
".text\n"
".global real_input_get_touchCount\n"
".type real_input_get_touchCount, %function\n"
"real_input_get_touchCount:\n"
"    sub sp, sp, #0x10\n"
"    str x30, [sp]\n"
"    bl c_real_get_touchCount\n"
"    ldr x30, [sp]\n"
"    add sp, sp, #0x10\n"
"    ret\n"
".global real_input_get_touch\n"
".type real_input_get_touch, %function\n"
"real_input_get_touch:\n"
"    sub sp, sp, #0x10\n"
"    str x30, [sp]\n"
"    str x8, [sp, #8]\n"
"    mov x1, x8\n"
"    bl c_real_get_touch\n"
"    ldr x30, [sp]\n"
"    add sp, sp, #0x10\n"
"    ret\n"
);
extern "C" int32_t c_real_get_touchCount() { return g_fake_n; }
extern "C" void c_real_get_touch(int32_t i, void* retbuf) {
    TouchEvent& t = g_fake[i];
    memset(retbuf, 0, 68);
    memcpy(retbuf, &t.finger, 4);
    memcpy((uint8_t*)retbuf + 4, &t.x, 4);
    memcpy((uint8_t*)retbuf + 8, &t.y, 4);
    memcpy((uint8_t*)retbuf + 36, &t.phase, 4);
}

// note: the real hooks patch Input functions; here we call hook functions directly
int main() {
    // install hooks on the fake Input functions (like the real bootstrap)
    void* tc_tramp = hook_install((void*)&real_input_get_touchCount, (void*)&hk_get_touchCount);
    void* gt_tramp = hook_install((void*)&real_input_get_touch, (void*)&hk_GetTouch_asm);
    assert(tc_tramp && gt_tramp);
    input_test_set_orig(tc_tramp, gt_tramp, nullptr, nullptr, nullptr, nullptr, nullptr);
    input_set_button_rect(10, 10, 160, 70, 100.0f, 100.0f);
    // --- scenario 1: overlay closed, tap on button (fires on release)
    g_fake[0] = {50, 40, 0 /*Began*/, 1}; g_fake_n = 1;
    int32_t seen = hk_get_touchCount();               // game's view
    printf("s1: game sees %d touches (expect 0, tap consumed)\n", seen);
    assert(seen == 0);
    assert(!input_button_tapped());                   // not yet (tap = release)
    g_fake[0].phase = 1; seen = hk_get_touchCount();  // tiny move (below threshold)
    assert(seen == 0);
    assert(!input_button_tapped());
    g_fake[0].phase = 3; seen = hk_get_touchCount();  // Ended frame (still reported)
    assert(seen == 0);
    assert(input_button_tapped());                    // released on button -> tap
    // render thread reacts
    g_ui_open.store(true);
    // --- scenario 1b: drag the button (moves rect, does NOT open)
    g_ui_open.store(false);
    input_set_button_rect(10, 10, 160, 70, 300.0f, 200.0f); // Unity rect: x[10,160] y[130,190]
    g_fake[0] = {50, 150, 0 /*Began*/, 9}; g_fake_n = 1;
    hk_get_touchCount();
    g_fake[0].x = 50; g_fake[0].y = 170; g_fake[0].phase = 1; // moved 20px -> drag
    hk_get_touchCount();
    g_fake[0].phase = 3;   // Ended frame
    hk_get_touchCount();
    g_fake_n = 0;
    hk_get_touchCount();
    assert(!input_button_tapped()); // drag, not a tap
    float x0, y0, x1, y1;
    input_get_button_rect(&x0, &y0, &x1, &y1);
    printf("s1b: button rect after drag: (%.0f,%.0f)-(%.0f,%.0f) (expect y0=140, clamped)\n", x0, y0, x1, y1);
    assert(y0 == 140.0f && x0 == 10.0f && x1 == 160.0f); // moved vertically, clamped at bottom
    // consume any queued tapped flag
    input_button_tapped();

    // --- scenario 2: overlay open, game touch suppressed, captured touches available
    g_ui_open.store(true); // render thread opened the window
    g_fake[0] = {200, 300, 0, 2}; g_fake[1] = {201, 301, 2, 3}; g_fake_n = 2;
    seen = hk_get_touchCount();
    assert(seen == 0);
    TouchEvent out[8];
    int n = input_pop_touches(out, 8);
    printf("s2: captured %d touches (expect 2)\n", n);
    assert(n == 2 && out[0].finger == 2 && out[0].x == 200);
    // GetTouch returns zeroed while open
    uint8_t buf[68]; memset(buf, 0xAA, 68);
    call_get_touch_orig((void*)&real_input_get_touch, buf, 0);
    hk_GetTouch_c(0, buf);
    int32_t phase; memcpy(&phase, buf + 36, 4);
    printf("s2: game GetTouch phase=%d (expect 4 canceled)\n", phase);
    assert(phase == 4);
    // --- scenario 3: load queue executes on main thread
    input_queue_load_level("/data/x/level.adofai");
    hk_get_touchCount();
    printf("s3: loaded '%s'\n", g_last_load);
    assert(strcmp(g_last_load, "/data/x/level.adofai") == 0);
    // --- scenario 4: close overlay, touches pass through again
    input_set_ui_open(false);
    g_fake[0] = {100, 100, 0, 4}; g_fake_n = 1;
    seen = hk_get_touchCount();
    printf("s4: game sees %d touches (expect 1)\n", seen);
    assert(seen == 1);
    // GetTouch passthrough
    uint8_t tmp[68];
    call_get_touch_orig((void*)&real_input_get_touch, tmp, 0);
    hk_GetTouch_c(0, tmp);
    memcpy(&phase, tmp + 36, 4);
    assert(phase == 0);
    // --- scenario 5: pause/resume queue
    input_queue_pause_toggle_for_overlay();
    hk_get_touchCount();
    assert(g_paused == 1 && g_pause_count == 1);
    input_queue_resume_overlay_pause();
    hk_get_touchCount();
    assert(g_paused == 0);
    // double pause guard: two queued toggles collapse into one
    input_queue_pause_toggle_for_overlay();
    input_queue_pause_toggle_for_overlay();
    hk_get_touchCount();
    assert(g_pause_count == 2);   // doubled queue -> exactly one extra pause
    input_queue_resume_overlay_pause();
    hk_get_touchCount();
    assert(g_paused == 0);
    printf("ALL INPUT PIPELINE TESTS PASSED\n");
    return 0;
}
