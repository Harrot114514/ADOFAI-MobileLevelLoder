#pragma once
#include <stdint.h>
#include <stddef.h>

#define TAG "ADoFaiLoader"

#ifdef __cplusplus
extern "C" {
#endif
void log_info(const char* fmt, ...) __attribute__((format(printf, 1, 2)));
void log_error(const char* fmt, ...) __attribute__((format(printf, 1, 2)));
#ifdef __cplusplus
}
#endif

#define LOGI(...) log_info(__VA_ARGS__)
#define LOGE(...) log_error(__VA_ARGS__)

// Find the base address of a loaded module by name (returns 0 if not loaded).
uint64_t find_module_base(const char* name);

// Read a file fully into memory (caller frees).
uint8_t* read_file(const char* path, size_t* out_size);
