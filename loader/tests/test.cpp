#include <stdio.h>
#include <string.h>
#include <stdint.h>
#include <assert.h>
void* hook_install(void* target, void* handler);
extern "C" void call_get_touch_orig(void* fn, void* retbuf, int index);
extern "C" void hk_GetTouch_c(int32_t index, void* retbuf) {}
extern "C" void hk_get_mousePosition_c(void* retbuf) {}

// ---- emulate Input.get_touchCount: stp, adrp, ldr, cbnz prologue ----
struct Touch { float x, y; int phase; };
static Touch g_touches[4] = {{10,20,0},{30,40,1},{50,60,2},{70,80,3}};

extern "C" int32_t test_get_touchCount() __attribute__((noinline));
extern "C" int32_t test_get_touchCount() {
    volatile int32_t n = 2;
    return n;
}

extern "C" void test_GetTouch(int32_t index, void* retbuf) __attribute__((noinline));
extern "C" void test_GetTouch(int32_t index, void* retbuf) {
    // mimics sret: uses x8! must write via asm-like behavior
    // Here we emulate: compiler sees retbuf in x1 (normal C), which is NOT x8.
    // So we test via the asm wrapper path instead (see below).
    memcpy(retbuf, &g_touches[index], sizeof(Touch));
}

// wrapper mimicking our hook flow: hook test_get_touchCount
typedef int32_t (*pfn_tc)(void);
typedef void (*pfn_gt)(int32_t, void*);
static pfn_tc orig_tc = nullptr;
static pfn_gt orig_gt = nullptr;
static int hk_called = 0;

extern "C" int32_t hk_tc() {
    int32_t real = orig_tc();
    hk_called++;
    return real + 100;   // observable behavior change
}

extern "C" void hk_gt_c(int32_t index, void* retbuf) {
    call_get_touch_orig((void*)orig_gt, retbuf, index);
    ((Touch*)retbuf)->phase += 10;
}

// asm entry for gt hook: x8->x1
extern "C" void hk_gt_asm();
asm(
".text\n"
".global hk_gt_asm\n"
".type hk_gt_asm, %function\n"
"hk_gt_asm:\n"
"    stp x29, x30, [sp, #-32]!\n"
"    mov x29, sp\n"
"    str x8, [sp, #16]\n"
"    mov x1, x8\n"
"    bl  hk_gt_c\n"
"    ldp x29, x30, [sp], #32\n"
"    ret\n"
);

// caller that mimics il2cpp: GetTouch with sret in x8
extern "C" void call_gt_sret(void* fn, int32_t index, void* retbuf);
asm(
".text\n"
".global call_gt_sret\n"
".type call_gt_sret, %function\n"
"call_gt_sret:\n"
"    mov x16, x0\n"
"    mov x8,  x2\n"
"    mov x0,  x1\n"
"    br  x16\n"
);

int main() {
    // hook touchCount
    orig_tc = (pfn_tc)hook_install((void*)&test_get_touchCount, (void*)&hk_tc);
    printf("orig_tc trampoline = %p\n", (void*)orig_tc);
    assert(orig_tc != nullptr);
    // hooked call: test_get_touchCount now returns real+100 and increments hk_called
    volatile pfn_tc call_tc = &test_get_touchCount;
    int32_t r = call_tc();
    printf("hooked tc -> %d (expect 102), hk_called=%d\n", r, hk_called);
    assert(r == 102 && hk_called == 1);
    // trampoline: original behavior
    int32_t r2 = orig_tc();
    printf("trampoline tc -> %d (expect 2)\n", r2);
    assert(r2 == 2);

    // hook GetTouch
    orig_gt = (pfn_gt)hook_install((void*)&test_GetTouch, (void*)&hk_gt_asm);
    printf("orig_gt trampoline = %p\n", (void*)orig_gt);
    assert(orig_gt != nullptr);
    Touch t;
    volatile void* call_gt = (void*)&test_GetTouch;
    call_gt_sret((void*)call_gt, 1, &t);
    printf("hooked gt: phase=%d (expect 11), x=%g y=%g\n", t.phase, t.x, t.y);
    assert(t.phase == 11 && t.x == 30 && t.y == 40);

    printf("ALL HOOK TESTS PASSED\n");
    return 0;
}
