#include "util.hpp"
#include "il2cpp.hpp"
#include "game.hpp"
#include "input.hpp"
#include "render.hpp"
#include <jni.h>
#include <pthread.h>
#include <unistd.h>
#include <string.h>

// Worker: waits for the game's il2cpp runtime, resolves the API and installs
// the render + input hooks. JNI_OnLoad runs very early (dex-injected
// System.loadLibrary), long before Unity finishes booting.
//
// IMPORTANT: this thread must NOT call any il2cpp API function — the il2cpp
// runtime is not initialized yet and calling e.g. il2cpp_thread_attach or
// domain lookups here crashes the process. Only dlsym (symbol resolution)
// and code patching are done here; all il2cpp calls are lazy, executed on
// the game main thread from the input hook (by then the runtime is up).
static void* bootstrap_thread(void*) {
    // 1) wait for libil2cpp.so
    for (int i = 0; i < 2000; i++) {
        g_il2cpp_base = find_module_base("libil2cpp.so");
        if (g_il2cpp_base) break;
        usleep(20 * 1000);
    }
    if (!g_il2cpp_base) { LOGE("bootstrap: libil2cpp.so never loaded"); return nullptr; }
    LOGI("bootstrap: libil2cpp.so @ 0x%llx", (unsigned long long)g_il2cpp_base);

    // 2) resolve il2cpp API symbols (dlsym only, no calls into il2cpp)
    for (int i = 0; i < 1000 && !il2cpp_resolve(); i++) usleep(20 * 1000);
    if (!il2cpp_resolve()) { LOGE("bootstrap: il2cpp API missing"); return nullptr; }

    // 3) install input hooks (touch funnel) and the EGL render hook.
    //    Game bindings (GCS/scrController) init lazily on the main thread.
    input_hooks_install();
    render_install_egl_hook();
    // Load-level pipeline hooks force the custom-level branch and guard the
    // game's internal-level path split from crashing on our file paths.
    game_install_load_level_hooks();

    LOGI("bootstrap: done (game bindings init lazily on first input poll)");
    return nullptr;
}

extern "C" JNIEXPORT jint JNICALL JNI_OnLoad(JavaVM* vm, void* reserved) {
    (void)vm; (void)reserved;
    LOGI("JNI_OnLoad: ADoFai Mobile Level Loader v1.0 (正式版)");
    pthread_t th;
    pthread_attr_t attr;
    pthread_attr_init(&attr);
    pthread_attr_setdetachstate(&attr, PTHREAD_CREATE_DETACHED);
    pthread_create(&th, &attr, bootstrap_thread, nullptr);
    pthread_attr_destroy(&attr);
    return JNI_VERSION_1_6;
}
