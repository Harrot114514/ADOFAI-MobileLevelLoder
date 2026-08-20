#include "util.hpp"
#include <android/log.h>
#include <link.h>
#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

void log_info(const char* fmt, ...) {
    va_list ap; va_start(ap, fmt);
    __android_log_vprint(ANDROID_LOG_INFO, TAG, fmt, ap);
    va_end(ap);
}
void log_error(const char* fmt, ...) {
    va_list ap; va_start(ap, fmt);
    __android_log_vprint(ANDROID_LOG_ERROR, TAG, fmt, ap);
    va_end(ap);
}

static uint64_t g_found_base = 0;
static const char* g_find_name = nullptr;

static int dl_callback(struct dl_phdr_info* info, size_t size, void* data) {
    (void)size; (void)data;
    const char* name = info->dlpi_name;
    if (!name || !name[0]) return 0;
    // basename
    const char* base = strrchr(name, '/');
    base = base ? base + 1 : name;
    if (strcmp(base, g_find_name) == 0) {
        g_found_base = (uint64_t)info->dlpi_addr;
        return 1;
    }
    return 0;
}

uint64_t find_module_base(const char* name) {
    g_found_base = 0;
    g_find_name = name;
    dl_iterate_phdr(dl_callback, nullptr);
    return g_found_base;
}

uint8_t* read_file(const char* path, size_t* out_size) {
    FILE* f = fopen(path, "rb");
    if (!f) return nullptr;
    fseek(f, 0, SEEK_END);
    long sz = ftell(f);
    fseek(f, 0, SEEK_SET);
    if (sz <= 0) { fclose(f); return nullptr; }
    uint8_t* buf = (uint8_t*)malloc((size_t)sz);
    if (!buf) { fclose(f); return nullptr; }
    size_t rd = fread(buf, 1, (size_t)sz, f);
    fclose(f);
    if (rd != (size_t)sz) { free(buf); return nullptr; }
    if (out_size) *out_size = (size_t)sz;
    return buf;
}
