// AArch64 assembly glue for il2cpp sret calling conventions.
// IL2CPP on ARM64 returns value types via a hidden pointer in x8.

.text

// void call_get_touch_orig(void* fn, void* retbuf, int index)
// x0=fn, x1=retbuf, w2=index
.global call_get_touch_orig
.type call_get_touch_orig, %function
call_get_touch_orig:
    mov x16, x0
    mov x8,  x1
    mov x0,  x2
    br  x16

// Hook entry for Input.GetTouch(int, Touch* sret):
//  incoming: x0=index, x8=retbuf  (x16 clobbered by veneer)
.global hk_GetTouch_asm
.type hk_GetTouch_asm, %function
hk_GetTouch_asm:
    stp x29, x30, [sp, #-32]!
    mov x29, sp
    str x8, [sp, #16]
    mov x1, x8
    bl  hk_GetTouch_c
    ldp x29, x30, [sp], #32
    ret

.section .note.GNU-stack,"",%progbits
