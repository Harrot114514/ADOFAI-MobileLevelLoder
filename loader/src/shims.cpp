// Bionic compatibility shims.
//
// We statically link libc++abi.a built for musl, which references a couple of
// glibc/musl-specific symbols that do not exist in bionic libc:
//   __assert_fail  ->  abort with a log line (bionic has __assert2 instead)
//
// Defining them here lets the dynamic linker resolve them locally.
#include <android/log.h>
#include <stdlib.h>
#include <stdio.h>

extern "C" void __assert_fail(const char* assertion, const char* file,
                              unsigned int line, const char* function) {
    __android_log_print(ANDROID_LOG_FATAL, "ADoFaiLoader",
                        "assertion failed: %s (%s:%u, %s)",
                        assertion ? assertion : "?",
                        file ? file : "?",
                        line,
                        function ? function : "?");
    abort();
}
