#include "render.hpp"
#include "hooks.hpp"
#include "overlay.hpp"
#include "util.hpp"
#include <dlfcn.h>
#include <EGL/egl.h>
#include <GLES3/gl3.h>
#include <time.h>
#include <mutex>
#include <atomic>

// ImGui sources are compiled into this project (see build.sh), so include them.
//#include "imgui.h" // pulled in via overlay.hpp

// eglSwapBuffers original
typedef EGLBoolean (*pfn_eglSwapBuffers)(EGLDisplay dpy, EGLSurface surface);
static pfn_eglSwapBuffers orig_eglSwapBuffers = nullptr;

static bool  g_gl_ready = false;
static void* g_last_context = nullptr;
static int   g_init_retry_frames = 0;

static void ensure_gl_ready(EGLDisplay dpy, EGLSurface surf) {
    void* ctx = (void*)eglGetCurrentContext();
    if (g_gl_ready && ctx == g_last_context) return;

    if (g_gl_ready) {
        overlay_gl_shutdown();
        g_gl_ready = false;
    }
    if (!ctx) return;
    if (g_init_retry_frames > 0) { g_init_retry_frames--; return; }

    EGLint w = 0, h = 0;
    eglQuerySurface(dpy, surf, EGL_WIDTH, &w);
    eglQuerySurface(dpy, surf, EGL_HEIGHT, &h);
    if (w <= 0 || h <= 0) return;

    if (!overlay_gl_init((int)w, (int)h)) {
        g_init_retry_frames = 120; // don't hammer if init keeps failing
        return;
    }
    g_gl_ready = true;
    g_last_context = ctx;
    LOGI("render: ImGui GLES3 ready (%dx%d, ctx=%p)", w, h, ctx);
}

extern "C" EGLBoolean hk_eglSwapBuffers(EGLDisplay dpy, EGLSurface surface) {
    // draw the overlay on top of Unity's frame before presenting
    if (orig_eglSwapBuffers) {
        ensure_gl_ready(dpy, surface);
        if (g_gl_ready) {
            EGLint w = 0, h = 0;
            eglQuerySurface(dpy, surface, EGL_WIDTH, &w);
            eglQuerySurface(dpy, surface, EGL_HEIGHT, &h);
            if (w > 0 && h > 0) {
                GLint vp[4];
                glGetIntegerv(GL_VIEWPORT, vp);
                overlay_render_frame((int)w, (int)h);
                glViewport(vp[0], vp[1], vp[2], vp[3]);
            }
        }
    }
    return orig_eglSwapBuffers(dpy, surface);
}

bool render_install_egl_hook() {
    if (orig_eglSwapBuffers) return true;
    void* egl = dlopen("libEGL.so", RTLD_NOW | RTLD_LOCAL);
    if (!egl) egl = dlopen("libEGL.so", RTLD_LAZY);
    if (!egl) { LOGE("render: cannot dlopen libEGL.so"); return false; }
    void* fn = dlsym(RTLD_DEFAULT, "eglSwapBuffers");
    if (!fn) fn = dlsym(egl, "eglSwapBuffers");
    if (!fn) { LOGE("render: eglSwapBuffers not found"); return false; }

    orig_eglSwapBuffers = (pfn_eglSwapBuffers)hook_install(fn, (void*)&hk_eglSwapBuffers);
    if (!orig_eglSwapBuffers) { LOGE("render: eglSwapBuffers hook failed"); return false; }
    LOGI("render: eglSwapBuffers hooked @%p (orig %p)", fn, (void*)orig_eglSwapBuffers);
    return true;
}
