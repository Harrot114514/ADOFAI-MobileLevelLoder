#include "input.hpp"
#include "hooks.hpp"
#include "game.hpp"
#include "util.hpp"
#include <string.h>
#include <mutex>
#include <atomic>
#include <string>

// RVAs (from dump)
static const uint64_t RVA_Input_get_touchCount    = 0x46206DC; // static int Input.get_touchCount()
static const uint64_t RVA_Input_GetTouch          = 0x461FE20; // static Touch Input.GetTouch(int)
static const uint64_t RVA_Input_get_touches       = 0x4620818; // static Touch[] Input.get_touches()
static const uint64_t RVA_Input_GetMouseButton    = 0x461FD44; // static bool Input.GetMouseButton(int)
static const uint64_t RVA_Input_GetMouseButtonDown= 0x461FD80;
static const uint64_t RVA_Input_GetMouseButtonUp  = 0x461FDBC;
static const uint64_t RVA_Input_get_mousePosition = 0x4620204; // static Vector3 (sret)

// Touch struct layout (verified in dump): fingerId @0, position @4, phase @36
#define TOUCH_POS   4
#define TOUCH_PHASE 36
#define TOUCH_SIZE  68

// asm helpers (hooks_asm.S)
extern "C" void call_get_touch_orig(void* fn, void* retbuf, int index);
extern "C" void hk_GetTouch_asm(void);

static std::atomic<bool> g_ui_open{false};
static std::atomic<bool> g_button_tapped{false};
static float g_btn[4] = {0, 0, 0, 0};      // floating button rect (Unity touch coords, y-up)
static std::mutex g_btn_mutex;
static float g_display_w = 1080.0f, g_display_h = 2400.0f;

// floating-button drag state (main thread only)
static int32_t g_btn_finger = -1;
static float g_btn_press_x = 0, g_btn_press_y = 0;
static float g_btn_origin[4] = {0, 0, 0, 0};
static bool  g_btn_moved = false;

static std::mutex g_touch_mutex;
static TouchEvent g_touches[16];
static int  g_touch_count = 0;

static int32_t g_consumed_finger = -1;     // finger being consumed (button tap)
static std::mutex g_consumed_mutex;

// main-thread action queue
static std::mutex g_act_mutex;
static std::string g_pending_load;
static bool g_pending_pause = false;
static bool g_pending_resume = false;

typedef int32_t (*pfn_get_touchcount)(void);
typedef void    (*pfn_get_touch)(int32_t index, void* retbuf);
typedef bool    (*pfn_get_mouse_button)(int32_t button);
typedef void*   (*pfn_get_touches)(void);

// il2cpp returns UnityEngine.Vector3 (12-byte HFA) in s0/s1/s2 (AAPCS64),
// NOT via sret. Model it as a 3-float struct so clang uses the same convention.
struct Vec3 { float x, y, z; };
typedef Vec3    (*pfn_get_mouse_position)(void);

static pfn_get_touchcount   orig_touchcount = nullptr;
static pfn_get_touch        orig_gettouch   = nullptr;
static pfn_get_mouse_button orig_mouse_btn      = nullptr;
static pfn_get_mouse_button orig_mouse_btn_down = nullptr;
static pfn_get_mouse_button orig_mouse_btn_up   = nullptr;
static pfn_get_mouse_position orig_mouse_pos = nullptr;
static pfn_get_touches      orig_get_touches = nullptr;

// ---------------------------------------------------------------- helpers

static void process_main_thread_actions() {
    std::string load;
    bool pause = false, resume = false;
    {
        std::lock_guard<std::mutex> lk(g_act_mutex);
        load = g_pending_load;
        pause = g_pending_pause;
        g_pending_pause = false;
        resume = g_pending_resume;
        g_pending_resume = false;
    }
    if (resume) {
        game_resume_overlay_pause();
    }
    if (pause) {
        game_pause_for_overlay();
    }
    if (!load.empty()) {
        if (!game_load_level(load.c_str())) {
            // keep it pending and retry on a later poll
            std::lock_guard<std::mutex> lk(g_act_mutex);
            if (g_pending_load.empty()) g_pending_load = load;
        } else {
            std::lock_guard<std::mutex> lk(g_act_mutex);
            g_pending_load.clear();
        }
    }
    game_apply_queued_options();
}

static bool in_button(float x, float y) {
    std::lock_guard<std::mutex> lk(g_btn_mutex);
    return x >= g_btn[0] && x <= g_btn[2] && y >= g_btn[1] && y <= g_btn[3];
}

// ---------------------------------------------------------------- hooks

extern "C" int32_t hk_get_touchCount() {
    int32_t real = orig_touchcount();
    bool ui_open = g_ui_open.load();

    // Lazy game-bindings init: this hook only runs from managed code on the
    // game main thread, i.e. the il2cpp runtime is fully up by now.
    if (!game_ready()) game_init();

    int n = real > 16 ? 16 : real;
    {
        std::lock_guard<std::mutex> lk(g_touch_mutex);
        g_touch_count = n;
        for (int i = 0; i < n; i++) {
            uint8_t buf[TOUCH_SIZE + 8];
            call_get_touch_orig((void*)orig_gettouch, buf, i);
            TouchEvent& t = g_touches[i];
            memcpy(&t.x, buf + TOUCH_POS, 4);
            memcpy(&t.y, buf + TOUCH_POS + 4, 4);
            memcpy(&t.phase, buf + TOUCH_PHASE, 4);
            memcpy(&t.finger, buf, 4);
        }
    }

    // floating button interaction (overlay closed): a tap toggles the window,
    // a drag moves the button. The finger is consumed so the game never sees it.
    if (!ui_open) {
        for (int i = 0; i < n; i++) {
            TouchEvent& t = g_touches[i];
            if (t.phase == 0 && g_btn_finger < 0 && in_button(t.x, t.y)) {
                std::lock_guard<std::mutex> lk(g_consumed_mutex);
                g_consumed_finger = t.finger;
                g_btn_finger = t.finger;
                g_btn_press_x = t.x;
                g_btn_press_y = t.y;
                g_btn_moved = false;
                std::lock_guard<std::mutex> lk2(g_btn_mutex);
                memcpy(g_btn_origin, g_btn, sizeof(g_btn));
                break;
            }
            if (t.finger == g_btn_finger) {
                if (t.phase == 1 || t.phase == 2) { // Moved / Stationary
                    float dx = t.x - g_btn_press_x;
                    float dy = t.y - g_btn_press_y;
                    if (!g_btn_moved && (dx * dx + dy * dy) > 12.0f * 12.0f)
                        g_btn_moved = true;
                    if (g_btn_moved) {
                        std::lock_guard<std::mutex> lk(g_btn_mutex);
                        g_btn[0] = g_btn_origin[0] + dx;
                        g_btn[1] = g_btn_origin[1] + dy;
                        g_btn[2] = g_btn_origin[2] + dx;
                        g_btn[3] = g_btn_origin[3] + dy;
                        // clamp to screen (Unity coords: y1 < y0)
                        float bw = g_btn[2] - g_btn[0], bh = g_btn[3] - g_btn[1];
                        if (bw < g_display_w) {
                            if (g_btn[0] < 0) g_btn[0] = 0;
                            else if (g_btn[0] + bw > g_display_w) g_btn[0] = g_display_w - bw;
                        } else {
                            g_btn[0] = 0;
                        }
                        g_btn[2] = g_btn[0] + bw;
                        if (bh < g_display_h) {
                            if (g_btn[1] < 0) g_btn[1] = 0;
                            else if (g_btn[1] + bh > g_display_h) g_btn[1] = g_display_h - bh;
                        } else {
                            g_btn[1] = 0;
                        }
                        g_btn[3] = g_btn[1] + bh;
                    }
                } else if (t.phase == 3 || t.phase == 4) { // Ended / Canceled
                    if (!g_btn_moved) g_button_tapped.store(true);
                    else {
                        // floating-ball behavior: near a horizontal edge, tuck
                        // the button so only ~25% stays visible
                        std::lock_guard<std::mutex> lk(g_btn_mutex);
                        float bw = g_btn[2] - g_btn[0];
                        float visible = bw * 0.25f;
                        if (g_btn[0] < g_display_w * 0.15f) {
                            g_btn[0] = -(bw - visible);
                            g_btn[2] = visible;
                        } else if (g_btn[2] > g_display_w * 0.85f) {
                            g_btn[0] = g_display_w - visible;
                            g_btn[2] = g_display_w + (bw - visible);
                        }
                    }
                    g_btn_finger = -1;
                }
            }
        }
    }

    process_main_thread_actions();

    // suppression
    if (ui_open) return 0;
    {
        std::lock_guard<std::mutex> lk(g_consumed_mutex);
        if (g_consumed_finger >= 0) {
            // Suppress while the finger is still reported by Unity (including
            // its final Ended frame); clear once it disappears from the list.
            bool present = false;
            for (int i = 0; i < n; i++)
                if (g_touches[i].finger == g_consumed_finger)
                    present = true;
            if (!present) g_consumed_finger = -1;
            else return 0;
        }
    }
    return real;
}

extern "C" void hk_GetTouch_c(int32_t index, void* retbuf) {
    call_get_touch_orig((void*)orig_gettouch, retbuf, index);
    bool ui_open = g_ui_open.load();
    bool consumed = false;
    {
        std::lock_guard<std::mutex> lk(g_consumed_mutex);
        consumed = g_consumed_finger >= 0;
    }
    if (ui_open || consumed) {
        memset(retbuf, 0, TOUCH_SIZE);
        *(int32_t*)((uint8_t*)retbuf + TOUCH_PHASE) = 4; // Canceled
    }
}

extern "C" bool hk_GetMouseButton(int32_t button) {
    if (!orig_mouse_btn) return false;
    bool r = orig_mouse_btn(button);
    if (g_ui_open.load()) return false;
    return r;
}
extern "C" bool hk_GetMouseButtonDown(int32_t button) {
    if (!orig_mouse_btn_down) return false;
    bool r = orig_mouse_btn_down(button);
    if (g_ui_open.load()) return false;
    return r;
}
extern "C" bool hk_GetMouseButtonUp(int32_t button) {
    if (!orig_mouse_btn_up) return false;
    bool r = orig_mouse_btn_up(button);
    if (g_ui_open.load()) return false;
    return r;
}
extern "C" Vec3 hk_get_mousePosition_c() {
    if (!orig_mouse_pos) return Vec3{0, 0, 0};
    Vec3 v = orig_mouse_pos();
    if (g_ui_open.load()) return Vec3{0, 0, 0};
    return v;
}

extern "C" void* hk_get_touches() {
    if (!orig_get_touches) return nullptr;
    void* r = orig_get_touches();
    if (g_ui_open.load() && r) {
        // il2cpp array: max_length at +0x18 (il2cpp_array_size_t) -> present an empty array
        *(uint64_t*)((uint8_t*)r + 0x18) = 0;
    }
    return r;
}

// ---------------------------------------------------------------- public

bool input_hooks_install() {
    if (orig_touchcount) return true; // already installed
    if (!g_il2cpp_base) return false;

    orig_touchcount = (pfn_get_touchcount)hook_install(
        (void*)(g_il2cpp_base + RVA_Input_get_touchCount), (void*)&hk_get_touchCount);
    orig_gettouch = (pfn_get_touch)hook_install(
        (void*)(g_il2cpp_base + RVA_Input_GetTouch), (void*)&hk_GetTouch_asm);
    orig_mouse_btn = (pfn_get_mouse_button)hook_install(
        (void*)(g_il2cpp_base + RVA_Input_GetMouseButton), (void*)&hk_GetMouseButton);
    orig_mouse_btn_down = (pfn_get_mouse_button)hook_install(
        (void*)(g_il2cpp_base + RVA_Input_GetMouseButtonDown), (void*)&hk_GetMouseButtonDown);
    orig_mouse_btn_up = (pfn_get_mouse_button)hook_install(
        (void*)(g_il2cpp_base + RVA_Input_GetMouseButtonUp), (void*)&hk_GetMouseButtonUp);
    orig_mouse_pos = (pfn_get_mouse_position)hook_install(
        (void*)(g_il2cpp_base + RVA_Input_get_mousePosition), (void*)&hk_get_mousePosition_c);
    orig_get_touches = (pfn_get_touches)hook_install(
        (void*)(g_il2cpp_base + RVA_Input_get_touches), (void*)&hk_get_touches);

    bool ok = orig_touchcount && orig_gettouch;
    LOGI("input: hooks %s (tc=%p gt=%p mb=%p mbD=%p mbU=%p mp=%p)",
         ok ? "ok" : "FAILED",
         (void*)orig_touchcount, (void*)orig_gettouch, (void*)orig_mouse_btn,
         (void*)orig_mouse_btn_down, (void*)orig_mouse_btn_up, (void*)orig_mouse_pos);
    return ok;
}

void input_set_button_rect(float x0, float y0, float x1, float y1, float display_w, float display_h) {
    std::lock_guard<std::mutex> lk(g_btn_mutex);
    if (g_btn_finger >= 0) return; // dragging: main thread owns the rect
    g_display_w = display_w;
    g_display_h = display_h;
    // ImGui y-down -> Unity touch y-up
    g_btn[0] = x0;
    g_btn[1] = display_h - y1;
    g_btn[2] = x1;
    g_btn[3] = display_h - y0;
}

void input_get_button_rect(float* x0, float* y0, float* x1, float* y1) {
    std::lock_guard<std::mutex> lk(g_btn_mutex);
    *x0 = g_btn[0]; *y0 = g_btn[1]; *x1 = g_btn[2]; *y1 = g_btn[3];
}

bool input_button_tapped() {
    return g_button_tapped.exchange(false);
}

void input_set_ui_open(bool open) {
    {
        std::lock_guard<std::mutex> lk(g_consumed_mutex);
        g_consumed_finger = -1;
    }
    g_ui_open.store(open);
}
bool input_ui_open() { return g_ui_open.load(); }

int input_pop_touches(TouchEvent* out, int n) {
    std::lock_guard<std::mutex> lk(g_touch_mutex);
    int c = g_touch_count < n ? g_touch_count : n;
    if (c > 0) memcpy(out, g_touches, sizeof(TouchEvent) * c);
    g_touch_count = 0;
    return c;
}

void input_queue_load_level(const char* path) {
    std::lock_guard<std::mutex> lk(g_act_mutex);
    g_pending_load = path;
}

void input_queue_pause_toggle_for_overlay() {
    std::lock_guard<std::mutex> lk(g_act_mutex);
    g_pending_pause = true;
}

void input_queue_resume_overlay_pause() {
    std::lock_guard<std::mutex> lk(g_act_mutex);
    g_pending_resume = true;
}

#ifdef INPUT_TEST
void input_test_set_orig(void* touchcount, void* gettouch, void* mb, void* mbd,
                         void* mbu, void* mousepos, void* gettouches) {
    orig_touchcount = (pfn_get_touchcount)touchcount;
    orig_gettouch = (pfn_get_touch)gettouch;
    orig_mouse_btn = (pfn_get_mouse_button)mb;
    orig_mouse_btn_down = (pfn_get_mouse_button)mbd;
    orig_mouse_btn_up = (pfn_get_mouse_button)mbu;
    orig_mouse_pos = (pfn_get_mouse_position)mousepos;
    orig_get_touches = (pfn_get_touches)gettouches;
}
#endif
