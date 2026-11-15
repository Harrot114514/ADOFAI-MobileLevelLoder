// Host test for the ZIP reader (src/zipfile.cpp) - no Android needed.
//
//   ./t4 <archive.zip> <out_dir>
//
// Lists the central directory, extracts every file entry under <out_dir> and
// reports failures. The shell driver (zip_test.sh) asserts on the output.
#include <stdio.h>
#include <string.h>
#include <sys/stat.h>
#include <unistd.h>
#include <string>
#include <vector>

#include "zipfile.hpp"

static void mkdirs(const char* p) {
    std::string s = p;
    for (size_t i = 1; i <= s.size(); i++) {
        if (i == s.size() || s[i] == '/') {
            std::string d = s.substr(0, i);
            mkdir(d.c_str(), 0777);
        }
    }
}

int main(int argc, char** argv) {
    if (argc < 3) {
        fprintf(stderr, "usage: %s <archive.zip> <out_dir>\n", argv[0]);
        return 2;
    }
    ZipReader zr;
    std::string err;
    if (!zr.open(argv[1], &err)) {
        printf("OPEN_FAIL %s\n", err.c_str());
        return 1;
    }
    printf("OPEN_OK entries=%d\n", (int)zr.entries().size());
    for (const ZipEntry& e : zr.entries()) {
        printf("ENTRY %s method=%u size=%u crc=%08x%s\n", e.name.c_str(), e.method,
               e.uncomp_size, e.crc, ZipReader::is_dir_entry(e) ? " dir" : "");
    }

    mkdirs(argv[2]);
    int i = 0, ok = 0, fail = 0;
    for (const ZipEntry& e : zr.entries()) {
        if (ZipReader::is_dir_entry(e)) continue;
        char out[512];
        snprintf(out, sizeof(out), "%s/f%03d", argv[2], i++);
        if (zr.extract(e, out, &err)) {
            struct stat st;
            long sz = (stat(out, &st) == 0) ? (long)st.st_size : -1;
            printf("EXTRACTED %s -> %ld bytes\n", e.name.c_str(), sz);
            ok++;
        } else {
            printf("EXTRACT_FAIL %s : %s\n", e.name.c_str(), err.c_str());
            fail++;
        }
    }
    printf("DONE ok=%d fail=%d\n", ok, fail);
    return fail == 0 ? 0 : 1;
}
