#pragma once
#include <stdint.h>
#include <stddef.h>

// Inline hook engine for AArch64.
// Patches the first 4 instructions (16 bytes) of the target with:
//   LDR X16, #8 ; BR X16 ; <absolute address of veneer>
// The veneer jumps to the handler. A trampoline (relocated original
// instructions + jump back) is returned as `orig`.

// Returns trampoline (callable original) or nullptr on failure.
// On failure, reason is logged.
void* hook_install(void* target, void* handler);
bool  hook_uninstall(void* target, void* trampoline);

// Rewrite a single 32-bit instruction at an arbitrary address (used to
// patch unconditional branches inside game functions).
void patch_write_u32(void* addr, uint32_t insn);
