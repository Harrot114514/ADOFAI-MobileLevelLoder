// Host-test stub for <android/log.h>
#ifndef ANDROID_LOG_STUB_H
#define ANDROID_LOG_STUB_H
#include <stdio.h>
#include <stdarg.h>
enum {
    ANDROID_LOG_INFO = 4,
    ANDROID_LOG_WARN = 5,
    ANDROID_LOG_ERROR = 6,
};
static inline int __android_log_print(int prio, const char* tag, const char* fmt, ...) {
    (void)prio; (void)tag;
    va_list ap; va_start(ap, fmt);
    vfprintf(stderr, fmt, ap);
    va_end(ap);
    return 0;
}
static inline int __android_log_vprint(int prio, const char* tag, const char* fmt, va_list ap) {
    (void)prio; (void)tag;
    return vfprintf(stderr, fmt, ap);
}
#endif
