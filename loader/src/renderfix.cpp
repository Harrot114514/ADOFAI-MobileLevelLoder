#include "renderfix.hpp"
#include "il2cpp.hpp"
#include "hooks.hpp"
#include "util.hpp"

#include <stdint.h>
#include <string.h>
#include <time.h>

// ---------------------------------------------------------------------------
// UnityEngine.RenderTexture.GetTemporary(width, height, ...)
//
// Every overload is a static method that only takes integer / enum arguments,
// so one fixed 12-slot integer trampoline reproduces the ABI of all of them
// (arguments 9..12 travel on the stack, exactly as the callee expects).
// ---------------------------------------------------------------------------

typedef uintptr_t (*pfn_generic)(uintptr_t, uintptr_t, uintptr_t, uintptr_t,
                                 uintptr_t, uintptr_t, uintptr_t, uintptr_t,
                                 uintptr_t, uintptr_t, uintptr_t, uintptr_t);

static pfn_generic g_orig[8];
static int g_clamped = 0;
static int g_guarded = 0;

// A value is a plausible texture dimension when it is small or a small
// negative number. A managed pointer (0x0000007xxxxxxxxx) never matches, which
// keeps us from ever clamping a 'this' pointer if the ABI assumption is wrong.
static inline bool dim_like(uintptr_t v) {
    return v < 0x1000000ull || v >= 0xFFFFFFFFFFFFFF00ull;  // 0..16M, or -256..-1
}

static inline uintptr_t fix_dim(uintptr_t v) {
    if (v == 0) return 1;
    if (v >= 0xFFFFFFFFFFFFFF00ull) return 1;  // negative sizes are invalid too
    return v;
}

static uintptr_t pass_through(int slot, uintptr_t a0, uintptr_t a1, uintptr_t a2,
                              uintptr_t a3, uintptr_t a4, uintptr_t a5,
                              uintptr_t a6, uintptr_t a7, uintptr_t a8,
                              uintptr_t a9, uintptr_t a10, uintptr_t a11) {
    uintptr_t w = a0, h = a1;
    if (dim_like(w) && dim_like(h)) {
        w = fix_dim(w);
        h = fix_dim(h);
    }
    if (w != a0 || h != a1) {
        if (g_clamped < 8) {
            LOGW("renderfix: RenderTexture.GetTemporary(%ld, %ld) -> (%ld, %ld)",
                 (long)(int32_t)a0, (long)(int32_t)a1, (long)(int32_t)w, (long)(int32_t)h);
        }
        g_clamped++;
    }
    return g_orig[slot](w, h, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11);
}

template <int SLOT>
static uintptr_t thunk(uintptr_t a0, uintptr_t a1, uintptr_t a2, uintptr_t a3,
                       uintptr_t a4, uintptr_t a5, uintptr_t a6, uintptr_t a7,
                       uintptr_t a8, uintptr_t a9, uintptr_t a10, uintptr_t a11) {
    return pass_through(SLOT, a0, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11);
}

// ------------------------------------------------------------------ lookup

static Il2CppClass* find_rendertexture() {
    Il2CppClass* k = il2cpp_find_class("UnityEngine.CoreModule", "UnityEngine", "RenderTexture");
    if (k) return k;
    // Fallback: the engine assembly is named differently across Unity
    // versions, so scan every loaded image before giving up.
    Il2CppDomain* domain = il2cpp.domain_get();
    if (!domain) return nullptr;
    size_t count = 0;
    const Il2CppAssembly** asms = il2cpp.domain_get_assemblies(domain, &count);
    if (!asms) return nullptr;
    for (size_t i = 0; i < count; i++) {
        const Il2CppImage* img = il2cpp.assembly_get_image(asms[i]);
        if (!img) continue;
        Il2CppClass* c = il2cpp.class_from_name(img, "UnityEngine", "RenderTexture");
        if (c) return c;
    }
    return nullptr;
}

// il2cpp's MethodInfo starts with the native entry point of the method.
static void* method_entry(const MethodInfo* m) {
    if (!m) return nullptr;
    void* code = nullptr;
    memcpy(&code, m, sizeof(code));
    return code;
}

template <int SLOT>
static bool guard_overload(Il2CppClass* klass, int argc) {
    const MethodInfo* m = il2cpp.class_get_method_from_name(klass, "GetTemporary", argc);
    void* code = method_entry(m);
    if (!code) return false;
    void* trampoline = hook_install(code, (void*)&thunk<SLOT>);
    if (!trampoline) {
        LOGW("renderfix: cannot hook GetTemporary/%d", argc);
        return false;
    }
    g_orig[SLOT] = (pfn_generic)trampoline;
    LOGI("renderfix: guarded RenderTexture.GetTemporary/%d @ %p", argc, code);
    return true;
}

void renderfix_install() {
    if (g_guarded > 0) return;          // already done
    // Retries are throttled: this is called from lazy-init paths, and a failed
    // class lookup walks every loaded assembly.
    static uint64_t last_try_ms = 0;
    struct timespec ts;
    clock_gettime(CLOCK_MONOTONIC, &ts);
    const uint64_t now_ms = (uint64_t)ts.tv_sec * 1000ull + (uint64_t)ts.tv_nsec / 1000000ull;
    if (last_try_ms && now_ms - last_try_ms < 2000) return;
    last_try_ms = now_ms;

    if (!il2cpp_resolve()) return;      // retry later

    Il2CppClass* klass = find_rendertexture();
    if (!klass) {
        LOGW("renderfix: UnityEngine.RenderTexture not found yet");
        return;                          // retry on the next lazy-init call
    }

    int n = 0;
    n += guard_overload<0>(klass, 2) ? 1 : 0;
    n += guard_overload<1>(klass, 3) ? 1 : 0;
    n += guard_overload<2>(klass, 4) ? 1 : 0;
    n += guard_overload<3>(klass, 5) ? 1 : 0;
    n += guard_overload<4>(klass, 6) ? 1 : 0;
    n += guard_overload<5>(klass, 7) ? 1 : 0;
    n += guard_overload<6>(klass, 8) ? 1 : 0;
    n += guard_overload<7>(klass, 9) ? 1 : 0;
    g_guarded = n;
    LOGI("renderfix: %d RenderTexture.GetTemporary overload(s) guarded", n);
}
