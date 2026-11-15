#include "zipfile.hpp"

#include <algorithm>
#include <errno.h>
#include <string.h>
#include <unistd.h>
#include <zlib.h>

// ---------------------------------------------------------------------------
// ZIP record layout (all little-endian):
//
//  [local file header] ... payload ...       (+ optional data descriptor)
//  ...                                       one per file
//  [central directory header] * n            (names, sizes, offsets)
//  [end of central directory]                (entry count, cd offset/size)
//
// We only need the central directory: it carries the authoritative sizes,
// CRC and payload offsets, so archives written with data descriptors
// (local sizes = 0) work too.
// ---------------------------------------------------------------------------

namespace {

const uint32_t kSigEocd = 0x06054b50u;  // end of central directory
const uint32_t kSigCen  = 0x02014b50u;  // central directory header
const uint32_t kSigLoc  = 0x04034b50u;  // local file header

// EOCD is 22 bytes + up to 65535 bytes of archive comment.
const size_t kEocdMaxSearch = 22 + 65535;

inline uint16_t rd16(const uint8_t* p) { return (uint16_t)(p[0] | (p[1] << 8)); }
inline uint32_t rd32(const uint8_t* p) {
    return (uint32_t)p[0] | ((uint32_t)p[1] << 8) |
           ((uint32_t)p[2] << 16) | ((uint32_t)p[3] << 24);
}
inline void set_err(std::string* err, const char* msg) {
    if (err) *err = msg;
}
inline void set_err(std::string* err, const std::string& msg) {
    if (err) *err = msg;
}

const size_t kChunk = 64 * 1024;

} // namespace

ZipReader::ZipReader() : f_(nullptr) {}

ZipReader::~ZipReader() { close(); }

void ZipReader::close() {
    if (f_) { fclose(f_); f_ = nullptr; }
    entries_.clear();
}

bool ZipReader::read_at(uint64_t off, void* dst, size_t n) {
    if (!f_) return false;
    if (fseeko(f_, (off_t)off, SEEK_SET) != 0) return false;
    return fread(dst, 1, n, f_) == n;
}

bool ZipReader::is_dir_entry(const ZipEntry& e) {
    return !e.name.empty() && e.name.back() == '/';
}

bool ZipReader::open(const char* path, std::string* err) {
    close();
    if (!path || !path[0]) { set_err(err, "empty path"); return false; }

    f_ = fopen(path, "rb");
    if (!f_) {
        set_err(err, std::string("cannot open archive: ") + strerror(errno));
        return false;
    }
    if (fseeko(f_, 0, SEEK_END) != 0) { set_err(err, "seek end failed"); close(); return false; }
    const off_t file_size = ftello(f_);
    if (file_size < 22) { set_err(err, "not a zip (too small)"); close(); return false; }

    const size_t tail_len = (size_t)std::min<off_t>(file_size, (off_t)kEocdMaxSearch);
    std::vector<uint8_t> tail(tail_len);
    if (!read_at((uint64_t)(file_size - (off_t)tail_len), tail.data(), tail_len)) {
        set_err(err, "cannot read archive tail");
        close();
        return false;
    }

    // Scan backwards for the EOCD signature.
    long eocd = -1;
    for (long i = (long)tail_len - 22; i >= 0; i--) {
        if (rd32(&tail[(size_t)i]) == kSigEocd) { eocd = i; break; }
    }
    if (eocd < 0) { set_err(err, "not a zip (no end record)"); close(); return false; }

    const uint8_t* e = &tail[(size_t)eocd];
    const uint16_t count   = rd16(e + 10);
    const uint32_t cd_size = rd32(e + 12);
    const uint32_t cd_off  = rd32(e + 16);

    if (count == 0xFFFF || cd_off == 0xFFFFFFFFu || cd_size == 0xFFFFFFFFu) {
        set_err(err, "ZIP64 archives are not supported");
        close();
        return false;
    }
    if (count == 0) { set_err(err, "archive is empty"); close(); return false; }
    if (cd_size == 0 || cd_size > 64u * 1024u * 1024u) {
        set_err(err, "central directory size is implausible");
        close();
        return false;
    }
    if ((uint64_t)cd_off + cd_size > (uint64_t)file_size) {
        set_err(err, "central directory out of range");
        close();
        return false;
    }

    std::vector<uint8_t> cd(cd_size);
    if (!read_at(cd_off, cd.data(), cd_size)) {
        set_err(err, "cannot read central directory");
        close();
        return false;
    }

    size_t p = 0;
    entries_.reserve(count);
    for (uint16_t i = 0; i < count; i++) {
        if (p + 46 > cd_size) { set_err(err, "truncated central directory"); break; }
        if (rd32(&cd[p]) != kSigCen) { set_err(err, "bad central directory entry"); break; }

        ZipEntry ent;
        ent.flags        = rd16(&cd[p + 8]);
        ent.method       = rd16(&cd[p + 10]);
        ent.crc          = rd32(&cd[p + 16]);
        ent.comp_size    = rd32(&cd[p + 20]);
        ent.uncomp_size  = rd32(&cd[p + 24]);
        const uint16_t name_len = rd16(&cd[p + 28]);
        const uint16_t extra_len = rd16(&cd[p + 30]);
        const uint16_t comment_len = rd16(&cd[p + 32]);
        ent.local_offset = rd32(&cd[p + 42]);

        const size_t rec_len = (size_t)46 + name_len + extra_len + comment_len;
        if (p + rec_len > cd_size) { set_err(err, "truncated central directory entry"); break; }
        ent.name.assign((const char*)&cd[p + 46], name_len);
        p += rec_len;

        if (ent.comp_size == 0xFFFFFFFFu || ent.uncomp_size == 0xFFFFFFFFu ||
            ent.local_offset == 0xFFFFFFFFu) {
            // ZIP64 extended fields: skip that entry rather than mis-reading it.
            continue;
        }
        if (ent.local_offset >= (uint32_t)file_size) continue;
        entries_.push_back(std::move(ent));
    }

    if (entries_.empty()) {
        set_err(err, "archive contains no usable entries");
        close();
        return false;
    }
    return true;
}

bool ZipReader::extract(const ZipEntry& e, const char* out_path, std::string* err) {
    if (!f_ || !out_path) { set_err(err, "reader not open"); return false; }

    // The payload offset must be derived from the LOCAL header: its name/extra
    // field lengths may differ from the central directory's.
    uint8_t lh[30];
    if (!read_at(e.local_offset, lh, sizeof(lh))) { set_err(err, "read local header"); return false; }
    if (rd32(lh) != kSigLoc) { set_err(err, "bad local header"); return false; }
    const uint64_t data_off = (uint64_t)e.local_offset + 30 + rd16(lh + 26) + rd16(lh + 28);

    FILE* out = fopen(out_path, "wb");
    if (!out) { set_err(err, std::string("create file: ") + strerror(errno)); return false; }

    bool ok = false;
    uint32_t crc = (uint32_t)crc32(0L, Z_NULL, 0);
    std::vector<uint8_t> in(kChunk), dst(kChunk);

    if (e.method == 0) {
        // Stored: payload is the file verbatim.
        ok = (e.comp_size == e.uncomp_size);
        if (!ok) set_err(err, "stored entry has mismatched sizes");
        uint64_t left = e.comp_size;
        while (ok && left > 0) {
            const size_t n = (size_t)std::min<uint64_t>(left, in.size());
            if (!read_at(data_off + (e.comp_size - left), in.data(), n)) {
                set_err(err, "read data");
                ok = false;
                break;
            }
            crc = (uint32_t)crc32(crc, in.data(), (uInt)n);
            if (fwrite(in.data(), 1, n, out) != n) {
                set_err(err, "write data");
                ok = false;
                break;
            }
            left -= n;
        }
    } else if (e.method == 8) {
        z_stream zs;
        memset(&zs, 0, sizeof(zs));
        if (inflateInit2(&zs, -15) != Z_OK) {  // raw deflate, no zlib header
            set_err(err, "inflateInit2 failed");
        } else {
            ok = true;
            uint64_t left = e.comp_size;
            uint64_t produced = 0;
            int zret = Z_OK;
            while (ok && zret != Z_STREAM_END) {
                if (left == 0) { set_err(err, "truncated deflate stream"); ok = false; break; }
                const size_t n = (size_t)std::min<uint64_t>(left, in.size());
                if (!read_at(data_off + (e.comp_size - left), in.data(), n)) {
                    set_err(err, "read data");
                    ok = false;
                    break;
                }
                left -= n;
                zs.next_in = in.data();
                zs.avail_in = (uInt)n;
                for (;;) {
                    zs.next_out = dst.data();
                    zs.avail_out = (uInt)dst.size();
                    zret = inflate(&zs, Z_NO_FLUSH);
                    if (zret != Z_OK && zret != Z_STREAM_END) {
                        set_err(err, "inflate failed");
                        ok = false;
                        break;
                    }
                    const size_t got = dst.size() - zs.avail_out;
                    if (got) {
                        produced += got;
                        if (produced > e.uncomp_size) {
                            set_err(err, "inflated data exceeds declared size");
                            ok = false;
                            break;
                        }
                        crc = (uint32_t)crc32(crc, dst.data(), (uInt)got);
                        if (fwrite(dst.data(), 1, got, out) != got) {
                            set_err(err, "write data");
                            ok = false;
                            break;
                        }
                    }
                    if (zret == Z_STREAM_END || zs.avail_in == 0) break;
                }
            }
            inflateEnd(&zs);
            if (ok && produced != e.uncomp_size) {
                set_err(err, "inflated size mismatch");
                ok = false;
            }
        }
    } else {
        set_err(err, "unsupported compression method");
    }

    if (fclose(out) != 0) {
        if (ok) set_err(err, std::string("close file: ") + strerror(errno));
        ok = false;
    }
    if (ok && crc != e.crc) {
        set_err(err, "CRC mismatch (corrupted archive)");
        ok = false;
    }
    if (!ok) unlink(out_path);  // never leave half-written files behind
    return ok;
}
