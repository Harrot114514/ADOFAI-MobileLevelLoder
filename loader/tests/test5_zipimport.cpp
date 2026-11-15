// Host test for the zip import state machine (src/zipimport.cpp).
//
//   ./t5 <archive.zip> <work_root>
//
// Runs a real import (worker thread + extraction + .adofai discovery) and
// prints the outcome. The shell driver (zip_test.sh) asserts on the output.
#include <stdio.h>
#include <unistd.h>
#include <string>
#include <vector>

#include "zipimport.hpp"

int main(int argc, char** argv) {
    if (argc < 3) {
        fprintf(stderr, "usage: %s <archive.zip> <work_root>\n", argv[0]);
        return 2;
    }
    if (!zip_import_begin(argv[1], argv[2])) {
        printf("BEGIN_FAIL\n");
        return 2;
    }
    for (int i = 0; i < 3000; i++) {  // up to ~60 s
        int st = zip_import_state();
        if (st == ZIPIMP_READY) {
            std::vector<std::string> abs_paths, rel_names;
            zip_import_levels(&abs_paths, &rel_names);
            printf("READY levels=%d\n", (int)rel_names.size());
            for (size_t k = 0; k < rel_names.size(); k++) {
                printf("LEVEL %s\n", rel_names[k].c_str());
                printf("ABS %s\n", abs_paths[k].c_str());
            }
            return rel_names.empty() ? 1 : 0;
        }
        if (st == ZIPIMP_FAILED) {
            std::string detail;
            zip_import_error_detail(&detail);
            printf("FAILED code=%d detail=%s\n", zip_import_error_code(), detail.c_str());
            return 1;
        }
        usleep(20 * 1000);
    }
    printf("TIMEOUT state=%d\n", zip_import_state());
    return 3;
}
