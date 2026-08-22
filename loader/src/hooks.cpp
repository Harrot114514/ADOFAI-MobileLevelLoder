#include "hooks.hpp"
#include "util.hpp"
#include <sys/mman.h>
#include <unistd.h>
#include <string.h>
#include <stdio.h>
#include <stdlib.h>

// ---------------------------------------------------------------- helpers

static uint32_t read32(const void* p) { uint32_t v; memcpy(&v, p, 4); return v; }
static void write32(void* p, uint32_t v) { memcpy(p, &v, 4); }
static void write64(void* p, uint64_t v) { memcpy(p, &v, 8); }

static inline int64_t sext(int64_t v, int bits) {
    return (v << (64 - bits)) >> (64 - bits);
}

// Encode B / BL
static uint32_t enc_b(uint32_t base, int64_t target, uint64_t pc) {
    int64_t off = (target - (int64_t)pc) >> 2;
    if (off < -(1 << 25) || off >= (1 << 25)) return 0; // out of range
    return base | ((uint32_t)off & 0x03FFFFFF);
}

// Relocate one instruction from `src_pc` to `dst` (executed at `dst_pc`).
// Returns true on success.
static bool relocate_one(uint32_t insn, uint64_t src_pc, uint8_t* dst, uint64_t dst_pc) {
    // B / BL
    if ((insn & 0x7C000000) == 0x14000000) {
        uint32_t base = insn & 0xFC000000;
        int64_t target = (int64_t)src_pc + (sext(insn & 0x03FFFFFF, 26) << 2);
        uint32_t e = enc_b(base, target, dst_pc);
        if (!e) return false;
        write32(dst, e);
        return true;
    }
    // B.cond
    if ((insn & 0xFF000010) == 0x54000000) {
        int64_t target = (int64_t)src_pc + (sext((insn >> 5) & 0x7FFFF, 19) << 2);
        int64_t off = (target - (int64_t)dst_pc) >> 2;
        if (off < -(1 << 18) || off >= (1 << 18)) return false;
        write32(dst, (insn & 0xFF00001F) | (((uint32_t)off & 0x7FFFF) << 5));
        return true;
    }
    // CBZ / CBNZ
    if ((insn & 0x7E000000) == 0x34000000) {
        int64_t target = (int64_t)src_pc + (sext((insn >> 5) & 0x7FFFF, 19) << 2);
        int64_t off = (target - (int64_t)dst_pc) >> 2;
        if (off < -(1 << 18) || off >= (1 << 18)) return false;
        write32(dst, (insn & 0xFF00001F) | (((uint32_t)off & 0x7FFFF) << 5));
        return true;
    }
    // TBZ / TBNZ
    if ((insn & 0x7E000000) == 0x36000000) {
        int64_t target = (int64_t)src_pc + (sext((insn >> 5) & 0x3FFF, 14) << 2);
        int64_t off = (target - (int64_t)dst_pc) >> 2;
        if (off < -(1 << 13) || off >= (1 << 13)) return false;
        write32(dst, (insn & 0xFFF8001F) | (((uint32_t)off & 0x3FFF) << 5));
        return true;
    }
    // ADR / ADRP
    if ((insn & 0x1F000000) == 0x10000000) {
        int64_t imm = sext(((insn >> 29) & 0x3) | (((insn >> 5) & 0x7FFFF) << 2), 21);
        int64_t target;
        if (insn & 0x80000000) { // ADRP
            target = ((int64_t)src_pc & ~0xFFFLL) + (imm << 12);
            int64_t delta = target - ((int64_t)dst_pc & ~0xFFFLL);
            uint32_t immlo = ((uint32_t)delta >> 12) & 0x3;
            uint32_t immhi = ((uint32_t)delta >> 14) & 0x7FFFF;
            write32(dst, (insn & 0x9F00001F) | (immlo << 29) | (immhi << 5));
            return true;
        } else { // ADR
            target = (int64_t)src_pc + imm;
            int64_t delta = target - (int64_t)dst_pc;
            if (delta < -(1 << 20) || delta >= (1 << 20)) return false;
            uint32_t immlo = ((uint32_t)delta & 0x3);
            uint32_t immhi = ((uint32_t)delta >> 2) & 0x7FFFF;
            write32(dst, (insn & 0x9F00001F) | (immlo << 29) | (immhi << 5));
            return true;
        }
    }
    // LDR literal (w/x), LDRSW literal, PRFM literal
    if ((insn & 0x3B000000) == 0x18000000 ||
        (insn & 0x3B000000) == 0x58000000 ||
        (insn & 0x3B000000) == 0x98000000 ||
        (insn & 0x3B000000) == 0xD8000000) {
        int64_t target = (int64_t)src_pc + (sext((insn >> 5) & 0x7FFFF, 19) << 2);
        int64_t off = (target - (int64_t)dst_pc) >> 2;
        if (off < -(1 << 18) || off >= (1 << 18)) return false;
        write32(dst, (insn & 0xFF00001F) | (((uint32_t)off & 0x7FFFF) << 5));
        return true;
    }
    // everything else: copy as-is
    write32(dst, insn);
    return true;
}

// Find a free page near `target` (within +-100MB), preferring the closest.
static void* alloc_near_page(uint64_t target) {
    const uint64_t RANGE = 100ull * 1024 * 1024;
    uint64_t lo = (target > RANGE) ? (target - RANGE) : 0x10000;
    uint64_t hi = target + RANGE;

    FILE* f = fopen("/proc/self/maps", "r");
    if (!f) return nullptr;
    char line[512];
    struct Range { uint64_t s, e; };
    static Range ranges[4096];
    int n = 0;
    while (fgets(line, sizeof(line), f) && n < 4096) {
        uint64_t s, e;
        if (sscanf(line, "%lx-%lx", &s, &e) != 2) continue;
        if (e <= lo || s >= hi) continue;
        ranges[n].s = s; ranges[n].e = e; n++;
    }
    fclose(f);
    // sort by start
    for (int i = 0; i < n; i++)
        for (int j = i + 1; j < n; j++)
            if (ranges[j].s < ranges[i].s) { Range t = ranges[i]; ranges[i] = ranges[j]; ranges[j] = t; }

    // find gap (>= 0x2000) closest to target
    uint64_t best = 0;
    uint64_t best_dist = UINT64_MAX;
    uint64_t cur = (lo + 0xFFF) & ~0xFFFULL;
    for (int i = 0; i < n; i++) {
        if (ranges[i].s > cur && ranges[i].s - cur >= 0x2000) {
            uint64_t mid = cur + (ranges[i].s - cur) / 2;
            uint64_t d = mid > target ? mid - target : target - mid;
            if (d < best_dist) { best_dist = d; best = cur; }
        }
        if (ranges[i].e > cur) cur = (ranges[i].e + 0xFFF) & ~0xFFFULL;
    }
    if (hi - cur >= 0x2000) {
        uint64_t mid = cur + (hi - cur) / 2;
        uint64_t d = mid > target ? mid - target : target - mid;
        if (d < best_dist) { best_dist = d; best = cur; }
    }
    if (!best) return nullptr;

    void* p = mmap((void*)best, 0x2000, PROT_READ | PROT_WRITE | PROT_EXEC,
                   MAP_PRIVATE | MAP_ANONYMOUS | MAP_FIXED_NOREPLACE, -1, 0);
    if (p == MAP_FAILED) return nullptr;
    return p;
}

struct HookCtx {
    void*  target;
    void*  near_page;      // 0x2000 RWX
    void*  trampoline;     // == near_page + 0x100
};

// Classify PC-relative instructions (cannot be moved without fixup).
static bool is_pcrel(uint32_t insn) {
    if ((insn & 0x7C000000) == 0x14000000) return true; // B / BL
    if ((insn & 0xFF000010) == 0x54000000) return true; // B.cond
    if ((insn & 0x7E000000) == 0x34000000) return true; // CBZ / CBNZ
    if ((insn & 0x7E000000) == 0x36000000) return true; // TBZ / TBNZ
    if ((insn & 0x1F000000) == 0x10000000) return true; // ADR / ADRP
    if ((insn & 0x3B000000) == 0x18000000) return true; // LDR literal (w)
    if ((insn & 0x3B000000) == 0x58000000) return true; // LDR literal (x)
    if ((insn & 0x3B000000) == 0x98000000) return true; // LDRSW literal
    if ((insn & 0x3B000000) == 0xD8000000) return true; // PRFM literal
    return false;
}

// Rewrite a single 32-bit instruction at an arbitrary address (used to
// patch unconditional branches inside game functions).
void patch_write_u32(void* addr, uint32_t insn) {
    uint64_t page = (uint64_t)addr & ~0xFFFULL;
    if (mprotect((void*)page, 0x1000, PROT_READ | PROT_WRITE | PROT_EXEC) != 0) {
        LOGE("patch: mprotect failed for %p", addr);
        return;
    }
    write32(addr, insn);
    __builtin___clear_cache((char*)addr, (char*)addr + 4);
    mprotect((void*)page, 0x1000, PROT_READ | PROT_EXEC);
    LOGI("patch: wrote %08x @ %p", insn, addr);
}

void* hook_install(void* target, void* handler) {    if (!target || !handler) return nullptr;
    uint64_t t = (uint64_t)target;

    void* np = alloc_near_page(t);
    if (!np) { LOGE("hook: no near page for %p", target); return nullptr; }

    // veneer at np+0: LDR X16,#8 ; BR X16 ; .quad handler
    write32(np, 0x58000050);                 // ldr x16, #8
    write32((uint8_t*)np + 4, 0xD61F0200);   // br x16
    write64((uint8_t*)np + 8, (uint64_t)handler);
    __builtin___clear_cache((char*)np, (char*)np + 16);

    uint8_t* tramp = (uint8_t*)np + 0x100;
    uint32_t insn0 = read32(target);
    uint64_t page = t & ~0xFFFULL;
    bool short_patch = !is_pcrel(insn0);

    if (mprotect((void*)page, 0x1000, PROT_READ | PROT_WRITE | PROT_EXEC) != 0) {
        LOGE("hook: mprotect failed %p", (void*)page);
        munmap(np, 0x2000);
        return nullptr;
    }

    if (short_patch) {
        // Patch only 4 bytes: B veneer. Trampoline = insn0 + B target+4.
        int64_t off = ((int64_t)np - (int64_t)t) >> 2;
        if (off < -(1 << 25) || off >= (1 << 25)) {
            LOGE("hook: veneer out of B range for %p", target);
            mprotect((void*)page, 0x1000, PROT_READ | PROT_EXEC);
            munmap(np, 0x2000);
            return nullptr;
        }
        write32(tramp, insn0);
        uint32_t jb = enc_b(0x14000000, t + 4, (uint64_t)(tramp + 4));
        if (!jb) { mprotect((void*)page, 0x1000, PROT_READ | PROT_EXEC); munmap(np, 0x2000); return nullptr; }
        write32(tramp + 4, jb);
        __builtin___clear_cache((char*)tramp, (char*)tramp + 8);

        write32(target, 0x14000000 | ((uint32_t)off & 0x03FFFFFF));
        __builtin___clear_cache((char*)target, (char*)target + 4);
        mprotect((void*)page, 0x1000, PROT_READ | PROT_EXEC);
    } else {
        // 16-byte patch: relocate the first 4 instructions into the trampoline.
        for (int i = 0; i < 4; i++) {
            uint32_t insn = read32((uint8_t*)target + i * 4);
            if (!relocate_one(insn, t + i * 4, tramp + i * 4, (uint64_t)(tramp + i * 4))) {
                LOGE("hook: relocate failed at %p insn %08x", (uint8_t*)target + i * 4, insn);
                mprotect((void*)page, 0x1000, PROT_READ | PROT_EXEC);
                munmap(np, 0x2000);
                return nullptr;
            }
        }
        uint32_t jmp_back = enc_b(0x14000000, t + 16, (uint64_t)(tramp + 16));
        if (!jmp_back) { mprotect((void*)page, 0x1000, PROT_READ | PROT_EXEC); munmap(np, 0x2000); return nullptr; }
        write32(tramp + 16, jmp_back);
        __builtin___clear_cache((char*)tramp, (char*)tramp + 20);

        write32(target, 0x58000050);
        write32((uint8_t*)target + 4, 0xD61F0200);
        write64((uint8_t*)target + 8, (uint64_t)np);
        __builtin___clear_cache((char*)target, (char*)target + 16);
        mprotect((void*)page, 0x1000, PROT_READ | PROT_EXEC);
    }

    LOGI("hook: installed %p -> %p (tramp %p, %s)", target, handler, tramp,
         short_patch ? "short" : "full");
    return tramp;
}
