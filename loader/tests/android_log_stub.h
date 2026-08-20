#pragma once
#include <stdarg.h>
#include <stdio.h>
#define ANDROID_LOG_INFO 4
#define ANDROID_LOG_ERROR 6
static inline int __android_log_vprint(int prio, const char* tag, const char* fmt, va_list ap) {
    (void)prio; (void)tag; return vprintf(fmt, ap);
}
