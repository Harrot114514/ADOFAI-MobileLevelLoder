#include "util.hpp"
#include "il2cpp.hpp"
#include "game.hpp"
#include "input.hpp"
#include "render.hpp"
#include <pthread.h>
#include <unistd.h>
#include <dlfcn.h>
#include <string.h>
#include <stdlib.h>

#define JNIEXPORT __attribute__((visibility("default")))
#define JNICALL
#define JNI_VERSION_1_6 0x00010006

typedef struct _JavaVM JavaVM;
typedef struct _JNIEnv JNIEnv;

static void* bootstrap_thread(void*) {
    for (int i = 0; i < 2000; i++) {
        g_il2cpp_base = find_module_base("libil2cpp.so");
        if (g_il2cpp_base) break;
        usleep(20 * 1000);
    }
    if (!g_il2cpp_base) { LOGE("bootstrap: libil2cpp.so never loaded"); return nullptr; }
    LOGI("bootstrap: libil2cpp.so @ 0x%llx", (unsigned long long)g_il2cpp_base);

    for (int i = 0; i < 1000 && !il2cpp_resolve(); i++) usleep(20 * 1000);
    if (!il2cpp_resolve()) { LOGE("bootstrap: il2cpp API missing"); return nullptr; }

    input_hooks_install();
    render_install_egl_hook();
    game_install_load_level_hooks();

    atexit([]() {
        LOGI("bootstrap: cleaning up...");
        game_cleanup_gc_handles();
    });

    LOGI("bootstrap: done");
    return nullptr;
}

extern "C" JNIEXPORT jint JNICALL JNI_OnLoad(JavaVM* vm, void* reserved) {
    (void)vm;
    (void)reserved;
    LOGI("JNI_OnLoad: ADoFai Mobile Level Loader v1.1.7");
    if (dlopen("libTool.so", RTLD_NOLOAD)) {
        LOGI("JNI_OnLoad: libTool detected - coexistence mode");
    }
    pthread_t th;
    pthread_attr_t attr;
    pthread_attr_init(&attr);
    pthread_attr_setdetachstate(&attr, PTHREAD_CREATE_DETACHED);
    pthread_create(&th, &attr, bootstrap_thread, nullptr);
    pthread_attr_destroy(&attr);
    return JNI_VERSION_1_6;
}