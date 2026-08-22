#pragma once
#include <stdint.h>
#include <stddef.h>

#define TAG "ADoFaiLoader"

#ifdef __cplusplus
extern "C" {
#endif
enum { LOG_INFO = 1, LOG_WARN = 2, LOG_ERROR = 3 };
// Log a line to logcat AND the on-disk log file
// (/sdcard/Android/data/com.fizzd.connectedworlds/files/log/adofailoader.log).
void log_line(int level, const char* fmt, ...) __attribute__((format(printf, 2, 3)));
void log_info(const char* fmt, ...) __attribute__((format(printf, 1, 2)));
void log_error(const char* fmt, ...) __attribute__((format(printf, 1, 2)));
#ifdef __cplusplus
}
#endif

#define LOGI(...) log_line(LOG_INFO, __VA_ARGS__)
#define LOGW(...) log_line(LOG_WARN, __VA_ARGS__)
#define LOGE(...) log_line(LOG_ERROR, __VA_ARGS__)

// Find the base address of a loaded module by name (returns 0 if not loaded).
uint64_t find_module_base(const char* name);

// Read a file fully into memory (caller frees).
uint8_t* read_file(const char* path, size_t* out_size);
