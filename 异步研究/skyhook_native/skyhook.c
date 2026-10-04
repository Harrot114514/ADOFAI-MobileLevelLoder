/*
 * skyhook.c — SkyHook 原生层 (skyhook.dll) 的 C 语言重新实现
 *
 * 对应反编译得到的 SkyHookNative P/Invoke 接口（SkyHook.Unity.dll）：
 *   string  StartHook(Callback cb)     // 失败返回错误串(CoTaskMem)，成功返回 NULL
 *   string  StopHook()                 // 同上
 *   bool    HookIsRunning()
 *   void    SetContext(IntPtr ctx)     // 托管侧写入 GCHandle 指针，回调时原样传回
 *   KeyLabel GetKeyLabel(ushort key)   // 原生键码 -> 语义标签
 *   ushort  GetKeyCode(KeyLabel label) // 反向映射
 *
 * 回调签名（由托管层 SkyHookManager.NativeHookCallback 反推）：
 *   void Callback(void* ctx, SkyHookEvent e)   // e 按值传递，布局必须与托管 struct 一致
 *
 * 技术要点：
 *   1. WH_KEYBOARD_LL / WH_MOUSE_LL 低级钩子：全局钩子但回调在“安装钩子的线程”
 *      上下文执行，通过该线程的消息队列投递 —— 所以必须有一个常驻消息循环线程。
 *   2. 时间戳：QueryPerformanceCounter + GetSystemTimePreciseAsFileTime 校准，
 *      输出 Unix 秒 + 纳秒（托管层 GetTimeInTicks 再换算成 DateTime 风格 100ns tick）。
 *   3. 回调在钩子线程上直接调用，必须快速返回（托管层只 Enqueue，很快）。
 *   4. 仅用于合法的游戏输入系统研究/复刻。全局钩子会看到系统所有按键，
 *      请勿用于任何恶意用途。
 */
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdint.h>
#include <string.h>
#include <objbase.h>

#pragma comment(lib, "ole32.lib")
#pragma comment(lib, "user32.lib")

/* ================= 与托管层对齐的类型定义 ================= */

typedef enum EventType {
    KeyPressed  = 0,
    KeyReleased = 1
} EventType;

/* 与 SkyHook.Unity.dll 中的 KeyLabel 枚举完全一致（0..120） */
typedef enum KeyLabel {
    Label_Escape = 0, Label_F1 = 1, Label_F2 = 2, Label_F3 = 3, Label_F4 = 4,
    Label_F5 = 5, Label_F6 = 6, Label_F7 = 7, Label_F8 = 8, Label_F9 = 9,
    Label_F10 = 10, Label_F11 = 11, Label_F12 = 12, Label_F13 = 13, Label_F14 = 14,
    Label_F15 = 15, Label_F16 = 16, Label_F17 = 17, Label_F18 = 18, Label_F19 = 19,
    Label_F20 = 20, Label_F21 = 21, Label_F22 = 22, Label_F23 = 23, Label_F24 = 24,
    Label_Grave = 25,
    Label_Alpha1 = 26, Label_Alpha2 = 27, Label_Alpha3 = 28, Label_Alpha4 = 29,
    Label_Alpha5 = 30, Label_Alpha6 = 31, Label_Alpha7 = 32, Label_Alpha8 = 33,
    Label_Alpha9 = 34, Label_Alpha0 = 35,
    Label_Minus = 36, Label_Equal = 37, Label_Backspace = 38, Label_Tab = 39,
    Label_Q = 40, Label_W = 41, Label_E = 42, Label_R = 43, Label_T = 44,
    Label_Y = 45, Label_U = 46, Label_I = 47, Label_O = 48, Label_P = 49,
    Label_LeftBrace = 50, Label_RightBrace = 51, Label_BackSlash = 52,
    Label_CapsLock = 53,
    Label_A = 54, Label_S = 55, Label_D = 56, Label_F = 57, Label_G = 58,
    Label_H = 59, Label_J = 60, Label_K = 61, Label_L = 62,
    Label_Semicolon = 63, Label_Apostrophe = 64,
    Label_Enter = 65, Label_LShift = 66,
    Label_Z = 67, Label_X = 68, Label_C = 69, Label_V = 70, Label_B = 71,
    Label_N = 72, Label_M = 73, Label_Comma = 74, Label_Dot = 75, Label_Slash = 76,
    Label_RShift = 77, Label_LControl = 78, Label_Super = 79, Label_LAlt = 80,
    Label_Space = 81, Label_RAlt = 82, Label_RControl = 83,
    Label_PrintScreen = 84, Label_ScrollLock = 85, Label_PauseBreak = 86,
    Label_Insert = 87, Label_Home = 88, Label_PageUp = 89, Label_Delete = 90,
    Label_End = 91, Label_PageDown = 92,
    Label_ArrowUp = 93, Label_ArrowLeft = 94, Label_ArrowDown = 95, Label_ArrowRight = 96,
    Label_NumLock = 97, Label_KeypadSlash = 98, Label_KeypadAsterisk = 99,
    Label_KeypadMinus = 100,
    Label_Keypad1 = 101, Label_Keypad2 = 102, Label_Keypad3 = 103, Label_Keypad4 = 104,
    Label_Keypad5 = 105, Label_Keypad6 = 106, Label_Keypad7 = 107, Label_Keypad8 = 108,
    Label_Keypad9 = 109, Label_Keypad0 = 110, Label_KeypadDot = 111,
    Label_KeypadPlus = 112, Label_KeypadEnter = 113,
    Label_MouseLeft = 114, Label_MouseRight = 115, Label_MouseMiddle = 116,
    Label_MouseX1 = 117, Label_MouseX2 = 118,
    Label_IgnoredInternal = 119, Label_Unknown = 120
} KeyLabel;

/*
 * 与托管 SkyHookEvent 顺序布局(Sequential)一致：
 *   long TimeSec; uint TimeSubsecNano; EventType Type; KeyLabel Label; ushort Key;
 * 注意：托管 EventType 底层是 int32，KeyLabel 底层是 uint16 —— 此处必须严格对应，
 * 否则按值传递的回调结构体会错位。
 */
typedef struct SkyHookEvent {
    int64_t  timeSec;         /* Unix 秒 */
    uint32_t timeSubsecNano;  /* 秒内纳秒，< 1e9 */
    int32_t  type;            /* EventType */
    uint16_t label;           /* KeyLabel */
    uint16_t key;             /* 原生键码 = 扫描码 | (扩展键 ? 0x100 : 0) */
} SkyHookEvent;

/* 回调：x86 用 stdcall（托管 delegate 默认），x64 忽略调用约定 */
#if defined(_WIN64)
#  define HOOKCALL
#elif defined(_MSC_VER)
#  define HOOKCALL __stdcall
#else
#  define HOOKCALL __attribute__((stdcall))
#endif
typedef void (HOOKCALL *HookCallbackFn)(void* ctx, SkyHookEvent ev);

/* ================= 扫描码 <-> KeyLabel 映射表 ================= */
/* key 编码：扫描码 | (LLKHF_EXTENDED ? 0x100 : 0)；鼠标用 0x300 区段 */
typedef struct { uint16_t key; uint16_t label; } KeyMapEntry;

static const KeyMapEntry KEYMAP[] = {
    {0x001, Label_Escape},
    {0x03B, Label_F1}, {0x03C, Label_F2}, {0x03D, Label_F3}, {0x03E, Label_F4},
    {0x03F, Label_F5}, {0x040, Label_F6}, {0x041, Label_F7}, {0x042, Label_F8},
    {0x043, Label_F9}, {0x044, Label_F10}, {0x057, Label_F11}, {0x058, Label_F12},
    {0x029, Label_Grave},
    {0x002, Label_Alpha1}, {0x003, Label_Alpha2}, {0x004, Label_Alpha3},
    {0x005, Label_Alpha4}, {0x006, Label_Alpha5}, {0x007, Label_Alpha6},
    {0x008, Label_Alpha7}, {0x009, Label_Alpha8}, {0x00A, Label_Alpha9},
    {0x00B, Label_Alpha0},
    {0x00C, Label_Minus}, {0x00D, Label_Equal}, {0x00E, Label_Backspace},
    {0x00F, Label_Tab},
    {0x010, Label_Q}, {0x011, Label_W}, {0x012, Label_E}, {0x013, Label_R},
    {0x014, Label_T}, {0x015, Label_Y}, {0x016, Label_U}, {0x017, Label_I},
    {0x018, Label_O}, {0x019, Label_P},
    {0x01A, Label_LeftBrace}, {0x01B, Label_RightBrace}, {0x02B, Label_BackSlash},
    {0x03A, Label_CapsLock},
    {0x01E, Label_A}, {0x01F, Label_S}, {0x020, Label_D}, {0x021, Label_F},
    {0x022, Label_G}, {0x023, Label_H}, {0x024, Label_J}, {0x025, Label_K},
    {0x026, Label_L}, {0x027, Label_Semicolon}, {0x028, Label_Apostrophe},
    {0x01C, Label_Enter},
    {0x02A, Label_LShift},
    {0x02C, Label_Z}, {0x02D, Label_X}, {0x02E, Label_C}, {0x02F, Label_V},
    {0x030, Label_B}, {0x031, Label_N}, {0x032, Label_M}, {0x033, Label_Comma},
    {0x034, Label_Dot}, {0x035, Label_Slash},
    {0x036, Label_RShift},
    {0x01D, Label_LControl},
    {0x15B, Label_Super}, {0x15C, Label_Super},          /* E0 5B / E0 5C */
    {0x038, Label_LAlt}, {0x039, Label_Space},
    {0x138, Label_RAlt},                                  /* E0 38 */
    {0x11D, Label_RControl},                              /* E0 1D */
    {0x137, Label_PrintScreen},                           /* E0 37（简化） */
    {0x046, Label_ScrollLock},
    {0x152, Label_Insert}, {0x147, Label_Home}, {0x149, Label_PageUp},
    {0x153, Label_Delete}, {0x14F, Label_End}, {0x151, Label_PageDown},
    {0x148, Label_ArrowUp}, {0x14B, Label_ArrowLeft},
    {0x150, Label_ArrowDown}, {0x14D, Label_ArrowRight},
    {0x145, Label_NumLock},
    {0x135, Label_KeypadSlash}, {0x037, Label_KeypadAsterisk}, {0x04A, Label_KeypadMinus},
    {0x04F, Label_Keypad1}, {0x050, Label_Keypad2}, {0x051, Label_Keypad3},
    {0x04B, Label_Keypad4}, {0x04C, Label_Keypad5}, {0x04D, Label_Keypad6},
    {0x047, Label_Keypad7}, {0x048, Label_Keypad8}, {0x049, Label_Keypad9},
    {0x052, Label_Keypad0}, {0x053, Label_KeypadDot},
    {0x04E, Label_KeypadPlus}, {0x11C, Label_KeypadEnter}, /* E0 1C */
    {0x301, Label_MouseLeft}, {0x302, Label_MouseRight}, {0x303, Label_MouseMiddle},
    {0x304, Label_MouseX1}, {0x305, Label_MouseX2},
};
#define KEYMAP_COUNT (sizeof(KEYMAP) / sizeof(KEYMAP[0]))

/* ================= 内部状态 ================= */

static HookCallbackFn g_cb;
static void*          g_ctx;
static volatile LONG  g_running;        /* 钩子安装成功且线程在跑 */
static HANDLE         g_thread;
static DWORD          g_threadId;
static HANDLE         g_readyEvent;     /* 钩子线程就绪信号 */
static char           g_err[512];

/* 时间基：StartHook 时校准一次 */
static LARGE_INTEGER g_qpcFreq, g_qpcBase;
static int64_t       g_baseFt100ns;     /* 对应时刻的 FILETIME(100ns since 1601) */

static void InitTimeBase(void)
{
    QueryPerformanceFrequency(&g_qpcFreq);
    QueryPerformanceCounter(&g_qpcBase);
    FILETIME ft;
    GetSystemTimePreciseAsFileTime(&ft);
    g_baseFt100ns = ((int64_t)ft.dwHighDateTime << 32) | ft.dwLowDateTime;
}

/* 用 QPC 计算当前事件时刻（秒 + 纳秒），比每次调 GetSystemTime* 更快 */
static void FillTime(SkyHookEvent* e)
{
    LARGE_INTEGER now;
    QueryPerformanceCounter(&now);
    double elapsedSec = (double)(now.QuadPart - g_qpcBase.QuadPart) /
                        (double)g_qpcFreq.QuadPart;
    int64_t ft100ns = g_baseFt100ns + (int64_t)(elapsedSec * 1e7);
    /* FILETIME -> Unix 100ns：减去 1601-01-01 到 1970-01-01 的差值 */
    int64_t unix100ns = ft100ns - 116444736000000000LL;
    e->timeSec        = unix100ns / 10000000LL;
    e->timeSubsecNano = (uint32_t)((unix100ns % 10000000LL) * 100);
}

/* ================= 导出 API ================= */

__declspec(dllexport) KeyLabel GetKeyLabel(uint16_t key)
{
    size_t i;
    for (i = 0; i < KEYMAP_COUNT; ++i)
        if (KEYMAP[i].key == key) return (KeyLabel)KEYMAP[i].label;
    return Label_Unknown;
}

__declspec(dllexport) uint16_t GetKeyCode(KeyLabel label)
{
    size_t i;
    for (i = 0; i < KEYMAP_COUNT; ++i)
        if (KEYMAP[i].label == (uint16_t)label) return KEYMAP[i].key;
    return 0xFFFF; /* 托管侧 AsyncKeyCode(KeyLabel) 用 ushort.MaxValue 表示“无键码” */
}

__declspec(dllexport) void SetContext(void* ctx)
{
    g_ctx = ctx;
}

__declspec(dllexport) int HookIsRunning(void)
{
    return (int)InterlockedCompareExchange(&g_running, 0, 0);
}

/* ================= 钩子过程（在钩子线程执行，务必快速） ================= */

static HHOOK g_kbdHook, g_mouseHook;

static LRESULT CALLBACK KbdProc(int nCode, WPARAM wParam, LPARAM lParam)
{
    if (nCode >= 0 && HookIsRunning()) {
        KBDLLHOOKSTRUCT* k = (KBDLLHOOKSTRUCT*)lParam;
        SkyHookEvent e;
        FillTime(&e);
        e.type  = (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN)
                      ? KeyPressed : KeyReleased;
        /* 扫描码是布局无关的物理键位，这正是 ADOFAI 异步输入想要的 */
        e.key   = (uint16_t)(((k->flags & LLKHF_EXTENDED) ? 0x100 : 0) |
                             (k->scanCode & 0xFF));
        e.label = GetKeyLabel(e.key);
        if (g_cb) g_cb(g_ctx, e);   /* 托管层只做 Enqueue，很快 */
    }
    return CallNextHookEx(g_kbdHook, nCode, wParam, lParam);
}

static LRESULT CALLBACK MouseProc(int nCode, WPARAM wParam, LPARAM lParam)
{
    if (nCode >= 0 && HookIsRunning()) {
        MSLLHOOKSTRUCT* m = (MSLLHOOKSTRUCT*)lParam;
        uint16_t label = Label_Unknown, key = 0;
        switch (wParam) {
        case WM_LBUTTONDOWN:  case WM_LBUTTONUP:  label = Label_MouseLeft;   key = 0x301; break;
        case WM_RBUTTONDOWN:  case WM_RBUTTONUP:  label = Label_MouseRight;  key = 0x302; break;
        case WM_MBUTTONDOWN:  case WM_MBUTTONUP:  label = Label_MouseMiddle; key = 0x303; break;
        case WM_XBUTTONDOWN:  case WM_XBUTTONUP:
            if (HIWORD(m->mouseData) == XBUTTON1) { label = Label_MouseX1; key = 0x304; }
            else                                  { label = Label_MouseX2; key = 0x305; }
            break;
        default: break;
        }
        if (label != Label_Unknown) {
            SkyHookEvent e;
            FillTime(&e);
            e.type  = (wParam == WM_LBUTTONDOWN || wParam == WM_RBUTTONDOWN ||
                       wParam == WM_MBUTTONDOWN || wParam == WM_XBUTTONDOWN)
                          ? KeyPressed : KeyReleased;
            e.key   = key;
            e.label = label;
            if (g_cb) g_cb(g_ctx, e);
        }
    }
    return CallNextHookEx(g_mouseHook, nCode, wParam, lParam);
}

/* ================= 钩子线程 ================= */

static DWORD WINAPI HookThread(LPVOID unused)
{
    (void)unused;
    /*
     * 低级钩子（LL）例外：全局钩子允许 lpfn 位于本进程且 hMod = NULL，
     * 事件经由本线程消息队列投递 —— 本线程必须有消息循环。
     */
    g_kbdHook   = SetWindowsHookExW(WH_KEYBOARD_LL, KbdProc,   NULL, 0);
    g_mouseHook = SetWindowsHookExW(WH_MOUSE_LL, MouseProc, NULL, 0);
    if (!g_kbdHook || !g_mouseHook) {
        strcpy_s(g_err, sizeof(g_err), "SetWindowsHookEx failed");
        SetEvent(g_readyEvent);
        return 1;
    }
    InterlockedExchange(&g_running, 1);
    SetEvent(g_readyEvent);   /* 通知 StartHook：安装完成 */

    MSG msg;
    while (GetMessageW(&msg, NULL, 0, 0) > 0) {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }
    /* 收到 WM_QUIT 后在同线程卸载钩子 */
    UnhookWindowsHookEx(g_kbdHook);
    UnhookWindowsHookEx(g_mouseHook);
    g_kbdHook = g_mouseHook = NULL;
    InterlockedExchange(&g_running, 0);
    return 0;
}

/* 错误串：托管侧 (Mono) 按 CoTaskMem 释放返回的字符串 */
static char* DupError(const char* msg)
{
    size_t n = strlen(msg) + 1;
    char* p  = (char*)CoTaskMemAlloc((ULONG)n);
    if (p) memcpy(p, msg, n);
    return p;
}

__declspec(dllexport) const char* StartHook(HookCallbackFn cb)
{
    if (HookIsRunning()) return DupError("hook already running");
    g_cb = cb;
    g_err[0] = '\0';
    InitTimeBase();
    if (!g_readyEvent) g_readyEvent = CreateEventW(NULL, FALSE, FALSE, NULL);
    ResetEvent(g_readyEvent);
    g_thread = CreateThread(NULL, 0, HookThread, NULL, 0, &g_threadId);
    if (!g_thread) return DupError("CreateThread failed");
    if (WaitForSingleObject(g_readyEvent, 5000) != WAIT_OBJECT_0)
        return DupError("hook thread did not become ready");
    if (!HookIsRunning())
        return DupError(g_err[0] ? g_err : "hook start failed");
    return NULL; /* 成功 */
}

__declspec(dllexport) const char* StopHook(void)
{
    if (!g_thread) return NULL;
    if (HookIsRunning())
        PostThreadMessageW(g_threadId, WM_QUIT, 0, 0); /* 让钩子线程自己退出并卸载 */
    WaitForSingleObject(g_thread, 5000);
    CloseHandle(g_thread);
    g_thread = NULL;
    g_cb     = NULL;
    return NULL;
}

/* x64 上调用约定统一，DllMain 非必需 */
BOOL WINAPI DllMain(HINSTANCE h, DWORD reason, LPVOID reserved)
{
    (void)h; (void)reserved;
    if (reason == DLL_PROCESS_DETACH) {
        if (g_thread) {
            PostThreadMessageW(g_threadId, WM_QUIT, 0, 0);
            WaitForSingleObject(g_thread, 1000);
        }
        if (g_readyEvent) CloseHandle(g_readyEvent);
    }
    return TRUE;
}
