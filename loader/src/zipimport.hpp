#pragma once
#include <string>
#include <vector>

// ZIP import: extract a .zip archive into a scratch directory and collect the
// .adofai levels found inside, so a level pack can be played without
// unpacking it by hand.
//
// Extraction runs on a worker thread (archives can be hundreds of MB, and the
// overlay renders on the GL thread); the UI polls the state below and draws a
// progress bar. The caller passes the scratch root explicitly so this module
// stays free of game/data-dir knowledge (and is host-testable).

enum {
    ZIPIMP_IDLE = 0,
    ZIPIMP_WORKING = 1,   // worker running (or cancelling)
    ZIPIMP_READY = 2,     // done; levels() lists what was found
    ZIPIMP_FAILED = 3,    // see error_code() / error_detail()
};

enum {
    ZIPERR_NONE = 0,
    ZIPERR_OPEN,          // archive cannot be opened / is not a valid zip
    ZIPERR_EXTRACT,       // one entry failed to extract (corrupt data)
    ZIPERR_DISK,          // cannot create the directory / write the files
    ZIPERR_NO_LEVEL,      // the archive contains no .adofai
    ZIPERR_TOO_BIG,       // archive expands beyond the safety budget
    ZIPERR_BUSY,          // an import is already running
};

// Start importing `zip_path`. `work_root` is wiped and re-created, then the
// archive is extracted into it. Returns false when the request is rejected
// (already busy, or bad arguments).
bool zip_import_begin(const char* zip_path, const char* work_root);

// Ask the running worker to stop. It cleans the partial extraction up and
// returns to ZIPIMP_IDLE between entries.
void zip_import_cancel();

int zip_import_state();

// Progress snapshot (safe from any thread). `total` is the number of entries
// that will be extracted; `current` is the entry being written (may be empty).
void zip_import_progress(int* done, int* total, std::string* current);

// Results, valid while ZIPIMP_READY: absolute paths of the extracted .adofai
// files and the same entries relative to the extraction root, sorted by name.
void zip_import_levels(std::vector<std::string>* abs_paths,
                       std::vector<std::string>* rel_names);

int zip_import_error_code();
void zip_import_error_detail(std::string* out);

// Back to ZIPIMP_IDLE (extracted files are left on disk).
void zip_import_reset();
