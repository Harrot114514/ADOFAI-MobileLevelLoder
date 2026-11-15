#pragma once
#include <stdint.h>
#include <stddef.h>
#include <stdio.h>
#include <string>
#include <vector>

// Minimal read-only ZIP reader (stored + deflate) built on bionic zlib.
// Only what the loader needs: open an archive, list its central directory and
// extract single entries. No ZIP64, no encryption, no multi-disk, no zip64
// data descriptors - those are rejected/skipped by the callers.
//
// Names are returned exactly as stored (they may be non-UTF-8). Path policy
// (normalisation, ".." rejection, etc.) is the caller's business: this class
// only knows about the archive format.
struct ZipEntry {
    std::string name;        // stored name, '/'-separated
    uint32_t crc;
    uint32_t comp_size;
    uint32_t uncomp_size;
    uint32_t local_offset;
    uint16_t method;         // 0 = stored, 8 = deflate
    uint16_t flags;          // bit 0 = encrypted, bit 11 = UTF-8 name
    ZipEntry() : crc(0), comp_size(0), uncomp_size(0), local_offset(0),
                 method(0), flags(0) {}
};

class ZipReader {
public:
    ZipReader();
    ~ZipReader();

    // Opens `path` and parses the central directory.
    bool open(const char* path, std::string* err);
    void close();
    bool is_open() const { return f_ != nullptr; }
    const std::vector<ZipEntry>& entries() const { return entries_; }

    // Extracts `e` to `out_path` (parent directory must exist) and verifies
    // the uncompressed size and the CRC-32. On failure the partial output file
    // is removed.
    bool extract(const ZipEntry& e, const char* out_path, std::string* err);

    // Directory entries are stored as "name/" with a zero payload.
    static bool is_dir_entry(const ZipEntry& e);

private:
    bool read_at(uint64_t off, void* dst, size_t n);

    FILE* f_;
    std::vector<ZipEntry> entries_;
};
