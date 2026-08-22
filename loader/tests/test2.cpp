#include <stdio.h>
#include <string.h>
#include <stdint.h>
#include <assert.h>
void* hook_install(void* target, void* handler);
extern "C" void hk_GetTouch_c(int32_t index, void* retbuf) {}
extern "C" void hk_get_mousePosition_c(void* retbuf) {}

// Mimic Input.get_touchCount's real prologue: stp, adrp, ldr, cbnz
static int32_t g_cache = -1;
static int32_t g_val = 7;

extern "C" int32_t real_touchCount() __attribute__((noinline));
extern "C" int32_t real_touchCount() {
    if (g_cache < 0) { g_cache = g_val; }   // expect compiler to emit cbnz
    return g_cache;
}

extern "C" int32_t mimic_GetMouseButton(int32_t b) __attribute__((noinline));
extern "C" int32_t mimic_GetMouseButton(int32_t b) { return b * 3; }

typedef int32_t (*pfn_tc)(void);
typedef int32_t (*pfn_mb)(int32_t);
static pfn_tc orig_tc = nullptr;
static pfn_mb orig_mb = nullptr;
static int calls = 0;

extern "C" int32_t hk_tc2() { calls++; return orig_tc() + 5; }
extern "C" int32_t hk_mb2(int32_t b) { calls++; return orig_mb(b) + 1; }

int main() {
    orig_tc = (pfn_tc)hook_install((void*)&real_touchCount, (void*)&hk_tc2);
    assert(orig_tc);
    volatile pfn_tc p = &real_touchCount;
    int32_t r = p();
    printf("tc: hooked=%d (expect 12), orig=%d (expect 7), calls=%d\n", r, orig_tc(), calls);
    assert(r == 12 && orig_tc() == 7);

    orig_mb = (pfn_mb)hook_install((void*)&mimic_GetMouseButton, (void*)&hk_mb2);
    assert(orig_mb);
    volatile pfn_mb q = &mimic_GetMouseButton;
    int32_t r2 = q(4);
    printf("mb: hooked=%d (expect 13), orig=%d (expect 12)\n", r2, orig_mb(4));
    assert(r2 == 13 && orig_mb(4) == 12);
    printf("REALISTIC PROLOGUE TESTS PASSED\n");
    return 0;
}
