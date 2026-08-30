#include "il2cpp.hpp"
#include "util.hpp"
#include <dlfcn.h>
#include <string.h>
#include <stdio.h>

Il2CppApi il2cpp;

extern uint64_t g_il2cpp_base; // defined in game.cpp

// Unity loads libil2cpp.so via System.loadLibrary => RTLD_LOCAL, so its
// exports are NOT in the global scope and dlsym(RTLD_DEFAULT) fails.
// Look up through the library's own handle instead (RTLD_NOLOAD reuses the
// already-loaded instance).
static void* g_il2cpp_handle = nullptr;

void* il2cpp_lib_handle() { return g_il2cpp_handle; }

static void* open_il2cpp_handle() {
    void* h = dlopen("libil2cpp.so", RTLD_NOLOAD | RTLD_NOW);
    if (!h) h = dlopen("libil2cpp.so", RTLD_NOW);
    return h;
}

// Hard-coded export table (RVAs into libil2cpp.so, from the game's
// libil2cpp.so symbol table) as a last-resort fallback if the linker refuses
// dlsym lookups on this device.
struct ApiEntry { const char* name; uint64_t rva; };
static const ApiEntry kApiTable[] = {
    { "il2cpp_domain_get",                0x1F55C88 },
    { "il2cpp_domain_get_assemblies",     0x1F55C94 },
    { "il2cpp_assembly_get_image",        0x1F5578C },
    { "il2cpp_image_get_name",            0x1F56360 },
    { "il2cpp_class_from_name",           0x1F557C4 },
    { "il2cpp_class_get_field_from_name", 0x1F557E4 },
    { "il2cpp_class_get_method_from_name",0x1F557EC },
    { "il2cpp_field_static_get_value",    0x1F55E7C },
    { "il2cpp_field_static_set_value",    0x1F55E80 },
    { "il2cpp_string_new",                0x1F560C8 },
    { "il2cpp_string_new_len",            0x1F560D4 },
    { "il2cpp_runtime_invoke",            0x1F560AC },
    { "il2cpp_thread_attach",             0x1F560E4 },
    { "il2cpp_value_box",                 0x1F56088 },
    { "il2cpp_class_get_type",            0x1F5586C },
    { "il2cpp_object_new",                0x1F56064 },
    { "il2cpp_free",                      0x1F55768 },
    { "il2cpp_class_get_name",            0x1F557F0 },
};

static void* resolve_sym(const char* sym) {
    if (g_il2cpp_handle) {
        void* p = dlsym(g_il2cpp_handle, sym);
        if (p) return p;
    }
    // fallback 1: global scope (in case the game loaded it RTLD_GLOBAL)
    void* p = dlsym(RTLD_DEFAULT, sym);
    if (p) return p;
    // fallback 2: hard-coded export RVA + module base
    if (g_il2cpp_base) {
        for (const ApiEntry& e : kApiTable) {
            if (strcmp(e.name, sym) == 0) {
                LOGI("il2cpp: %s resolved via RVA table", sym);
                return (void*)(g_il2cpp_base + e.rva);
            }
        }
    }
    return nullptr;
}

#define RESOLVE(field, sym) do { \
    il2cpp.field = (decltype(il2cpp.field))resolve_sym(sym); \
    if (!il2cpp.field) { LOGE("il2cpp: missing symbol %s", sym); ok = false; } \
} while (0)

bool il2cpp_resolve() {
    static bool done = false, ok = false;
    if (done) return ok;
    if (!g_il2cpp_handle) {
        g_il2cpp_handle = open_il2cpp_handle();
        if (!g_il2cpp_handle) {
            LOGI("il2cpp: libil2cpp.so not yet loadable");
            return false;
        }
    }
    done = true;
    ok = true;
    RESOLVE(domain_get, "il2cpp_domain_get");
    RESOLVE(domain_get_assemblies, "il2cpp_domain_get_assemblies");
    RESOLVE(assembly_get_image, "il2cpp_assembly_get_image");
    RESOLVE(image_get_name, "il2cpp_image_get_name");
    RESOLVE(class_from_name, "il2cpp_class_from_name");
    RESOLVE(class_get_field_from_name, "il2cpp_class_get_field_from_name");
    RESOLVE(class_get_method_from_name, "il2cpp_class_get_method_from_name");
    RESOLVE(field_static_get_value, "il2cpp_field_static_get_value");
    RESOLVE(field_static_set_value, "il2cpp_field_static_set_value");
    RESOLVE(string_new, "il2cpp_string_new");
    RESOLVE(string_new_len, "il2cpp_string_new_len");
    RESOLVE(runtime_invoke, "il2cpp_runtime_invoke");
    RESOLVE(thread_attach, "il2cpp_thread_attach");
    RESOLVE(value_box, "il2cpp_value_box");
    RESOLVE(class_get_type, "il2cpp_class_get_type");
    RESOLVE(object_new, "il2cpp_object_new");
    RESOLVE(free, "il2cpp_free");
    RESOLVE(class_get_name, "il2cpp_class_get_name");
    if (ok) {
        LOGI("il2cpp: API resolved via handle %p", g_il2cpp_handle);
    } else {
        LOGI("il2cpp: some symbols missing, retrying later");
        done = false; // allow retry
    }
    return ok;
}

Il2CppClass* il2cpp_find_class(const char* assembly_name, const char* ns, const char* name) {
    if (!il2cpp_resolve()) return nullptr;
    Il2CppDomain* domain = il2cpp.domain_get();
    if (!domain) return nullptr;
    size_t count = 0;
    const Il2CppAssembly** asms = il2cpp.domain_get_assemblies(domain, &count);
    if (!asms) return nullptr;
    for (size_t i = 0; i < count; i++) {
        const Il2CppImage* img = il2cpp.assembly_get_image(asms[i]);
        if (!img) continue;
        const char* n = il2cpp.image_get_name(img);
        if (n && strcmp(n, assembly_name) == 0) {
            Il2CppClass* k = il2cpp.class_from_name(img, ns, name);
            if (k) return k;
        }
    }
    // fallback: search all images (some Unity versions name assemblies differently)
    for (size_t i = 0; i < count; i++) {
        const Il2CppImage* img = il2cpp.assembly_get_image(asms[i]);
        if (!img) continue;
        Il2CppClass* k = il2cpp.class_from_name(img, ns, name);
        if (k) return k;
    }
    return nullptr;
}
