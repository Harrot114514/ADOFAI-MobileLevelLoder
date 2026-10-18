/* ============================================================================
 *  ScnCLSFix.h  ——  冰与火之舞 (ADOFAI) 安卓版
 *  修复「打完一关后跳 scnCLS，但这个场景不存在 → 抛异常 → 弹『出问题了！』」
 * ----------------------------------------------------------------------------
 *  病因（来自本包 dump.cs / dump 源码，行号可查）：
 *    · scrController.QuitToMainMenu()  里：
 *          if (GCS.customLevelPaths == null) GCS.sceneToLoad = GCNS.sceneLevelSelect;
 *          else                              GCS.sceneToLoad = "scnCLS";      ← 就在这里
 *      GCS.customLevelPaths != null 的情形：玩 Xtra / 自制关 / 从列表进的关，
 *      也就是「打完一关」最常见的场景。
 *    · scrController 的 Portal.CustomLevelsScene 分支同样是 GCS.sceneToLoad = "scnCLS"。
 *    · scnGame.LoadAndPlayLevel() 读关失败时还会直接 SceneManager.LoadScene("scnCLS")。
 *    这个 APK 的 build settings 里没有 scnCLS 场景 → Unity 在 SceneManager.LoadScene
 *    内部抛 ArgumentException（就是截图里那段），DOTween 回调里抛出去 → 弹错误面板。
 *
 *  修法（一个卡点，全部路径通吃）：
 *    在 UnityEngine.SceneManagement.SceneManager 的 LoadScene / LoadSceneAsync
 *    这几个「按名字加载」的重载上装 hook，凡是目标名等于 scnCLS（大小写不敏感，
 *    截图里是 "ScnCLS"）就改道：
 *       模式 0（默认）回主菜单：直接调用游戏自己的 ADOBase.GoToLevelSelect()，
 *                   它内部用 GCNS.sceneLevelSelect —— 安卓上就是 "scnMobileMenu"，
 *                   是游戏自己保证存在的场景名，不需要我们造字符串，最稳。
 *       模式 1 改跳到 ImGui 里填的场景名（需要 il2cpp_string_new）。
 *       模式 2 只拦截、不跳转。
 *
 *  偏移（0x...，来自本包 dump.cs，和 Main.cpp 里其它 PATCH 用的是同一份 dump）：
 *      SceneManager.LoadScene(string)                        0x45D8190
 *      SceneManager.LoadScene(string, LoadSceneMode)         0x45D8090
 *      SceneManager.LoadScene(string, LoadSceneParameters)   0x45D80FC  (返回 Scene，4 字节)
 *      SceneManager.LoadSceneAsync(string)                   0x45D82D4
 *      SceneManager.LoadSceneAsync(string, LoadSceneParams)  0x45D832C
 *      ADOBase.GoToLevelSelect()  static void                0x2420BBC
 *
 *  依赖：只需要 DobbyHook（模板里已有 libdobby）+ imgui.h。
 *        不引用 Includes/Utils.h、不依赖 targetLibName，自带 ELF 基址解析，
 *        放在哪个目录都能编译。
 * ==========================================================================*/
#pragma once

#include <cstddef>
#include <cstdint>
#include <cstdio>
#include <cstdarg>
#include <cstring>
#include <cstdlib>
#include <string>
#include <dlfcn.h>
#include <elf.h>
#include <link.h>
#include <android/log.h>
#include <imgui.h>

#ifndef SCF_LOG_TAG
#define SCF_LOG_TAG "ScnCLSFix"
#endif
#define SCF_LOGI(...) __android_log_print(ANDROID_LOG_INFO,  SCF_LOG_TAG, __VA_ARGS__)
#define SCF_LOGE(...) __android_log_print(ANDROID_LOG_ERROR, SCF_LOG_TAG, __VA_ARGS__)

/* Dobby 的 C 接口（模板里的 Includes/Hooks.h 也是走这个，重复声明 ABI 一致） */
extern "C" int DobbyHook(void *target, void *replace, void **backup);

namespace ScnCLSFix {

/* ========================================================================== */
/*  1. 偏移                                                                   */
/* ========================================================================== */
#define SCF_OFF_LS_STRING        0x45D8190u  /* SceneManager.LoadScene(string)                  */
#define SCF_OFF_LS_STRING_MODE   0x45D8090u  /* SceneManager.LoadScene(string, LoadSceneMode)   */
#define SCF_OFF_LS_STRING_PARAM  0x45D80FCu  /* SceneManager.LoadScene(string, LoadSceneParams) */
#define SCF_OFF_LSA_STRING       0x45D82D4u  /* SceneManager.LoadSceneAsync(string)             */
#define SCF_OFF_LSA_STRING_PARAM 0x45D832Cu  /* SceneManager.LoadSceneAsync(string, LoadSceneParams) */
#define SCF_OFF_GOTO_MENU        0x2420BBCu  /* ADOBase.GoToLevelSelect()                       */

/* ========================================================================== */
/*  2. 配置（ImGui 写、钩子线程读）                                            */
/* ========================================================================== */
enum Mode {
    MODE_GO_MENU  = 0,   /* 回主菜单：调 ADOBase.GoToLevelSelect()（推荐） */
    MODE_REDIRECT = 1,   /* 改跳 target 指定的场景 */
    MODE_BLOCK    = 2    /* 只拦截，不跳转（一般不用） */
};

static bool     enabled    = true;                /* 默认开着，装上即修好 */
static int      mode       = MODE_GO_MENU;
static char     target[64] = "scnMobileMenu";     /* 安卓版主菜单场景名 */
static volatile int hits   = 0;                   /* 命中次数 */

/* 观察到的场景名（钩子里抓，用来在 ImGui 里挑目标） */
static char         seenNames[32][64];
static volatile int seenCount = 0;

/* 最近 12 条日志（环形） */
static char         logs[12][96];
static volatile int logCount = 0;

static inline void PushLog(const char *fmt, ...) {
    char line[96];
    va_list ap;
    va_start(ap, fmt);
    vsnprintf(line, sizeof(line), fmt, ap);
    va_end(ap);

    int idx = logCount % 12;
    strncpy(logs[idx], line, sizeof(logs[idx]) - 1);
    logs[idx][sizeof(logs[idx]) - 1] = '\0';
    logCount = logCount + 1;
    SCF_LOGI("%s", line);
}

static inline void PushSeen(const char *name) {
    if (!name || !name[0]) return;
    for (int i = 0; i < seenCount; i++) {
        if (strcmp(seenNames[i], name) == 0) return;
    }
    if (seenCount < 32) {
        strncpy(seenNames[seenCount], name, 63);
        seenNames[seenCount][63] = '\0';
        seenCount = seenCount + 1;
    }
}

/* ========================================================================== */
/*  3. Il2CppString（布局：klass, monitor, length, utf16 chars[]）             */
/* ========================================================================== */
struct Il2CppString {
    void    *klass;
    void    *monitor;
    int32_t  length;
    uint16_t chars[1];
};

static inline uint16_t *StrChars(Il2CppString *s) {
    return (uint16_t *) ((char *) s + offsetof(Il2CppString, chars));
}
static inline const uint16_t *StrCharsC(const Il2CppString *s) {
    return (const uint16_t *) ((const char *) s + offsetof(Il2CppString, chars));
}

static void *g_strKlass = nullptr;   /* 从游戏传进来的字符串对象上「借」到的 System.String klass */

static inline std::string StrToStd(Il2CppString *s) {
    if (!s) return std::string();
    g_strKlass = s->klass;
    const int32_t n = s->length;
    if (n <= 0 || n > 128) return std::string();   /* 场景名不可能更长，超了当非法 */
    const uint16_t *c = StrCharsC(s);
    std::string out;
    out.resize((size_t) n);
    for (int32_t i = 0; i < n; i++) {
        out[(size_t) i] = (c[i] < 0x80) ? (char) c[i] : '?';   /* 场景名都是 ASCII */
    }
    return out;
}

typedef Il2CppString *(*Il2CppStringNewFn)(const char *);
static Il2CppStringNewFn g_stringNew = nullptr;

static inline Il2CppString *MakeStr(const char *utf8) {
    if (!utf8) return nullptr;

    if (!g_stringNew) {
        g_stringNew = (Il2CppStringNewFn) dlsym(RTLD_DEFAULT, "il2cpp_string_new");
        if (!g_stringNew) {
            void *h = dlopen("libil2cpp.so", RTLD_LAZY);
            if (h) g_stringNew = (Il2CppStringNewFn) dlsym(h, "il2cpp_string_new");
        }
        SCF_LOGI("il2cpp_string_new = %p", (void *) g_stringNew);
    }
    if (g_stringNew) return g_stringNew(utf8);

    /* 兜底：libil2cpp 没导出 il2cpp_string_new 时手工拼一个 managed string。
       只有「改跳指定场景」模式才会走到这里；klass 借用游戏自己的字符串对象。
       该对象只作为参数往下传、不会被存进托管堆，所以没有 GC 追踪问题。 */
    if (!g_strKlass) return nullptr;
    const size_t n = strlen(utf8);
    Il2CppString *s = (Il2CppString *) calloc(1, sizeof(Il2CppString) + n * sizeof(uint16_t));
    if (!s) return nullptr;
    s->klass   = g_strKlass;
    s->monitor = nullptr;
    s->length  = (int32_t) n;
    uint16_t *c = StrChars(s);
    for (size_t i = 0; i < n; i++) c[i] = (uint16_t) (unsigned char) utf8[i];
    c[n] = 0;
    return s;
}

/* ========================================================================== */
/*  4. 模块基址 / RVA → 运行时地址（自带，不依赖 Includes/Utils.h）            */
/* ========================================================================== */
struct ModFind {
    const char *name;
    uintptr_t   base;
};

static inline int FindModCb(struct dl_phdr_info *info, size_t size, void *data) {
    (void) size;
    ModFind *f = (ModFind *) data;
    const char *path = info->dlpi_name;
    if (!path || !*path) return 0;                     /* 主可执行文件，跳过 */
    const char *slash = strrchr(path, '/');
    const char *baseName = slash ? slash + 1 : path;
    if (strcmp(baseName, f->name) != 0) return 0;
    f->base = (uintptr_t) info->dlpi_addr;
    return 1;                                          /* 找到就停 */
}

static inline uintptr_t ModuleBase(const char *name) {
    ModFind f;
    f.name = name;
    f.base = 0;
    dl_iterate_phdr(FindModCb, &f);
    return f.base;
}

/* dump.cs 里的 0x... 是相对模块基址的 RVA；按 PT_LOAD 段做一次校验式换算 */
static inline uintptr_t Rva2Addr(const char *libName, uintptr_t rva) {
    const uintptr_t base = ModuleBase(libName);
    if (!base) return 0;

    const Elf64_Ehdr *eh = (const Elf64_Ehdr *) base;
    if (memcmp(eh->e_ident, ELFMAG, SELFMAG) != 0) return base + rva;

    const Elf64_Phdr *ph = (const Elf64_Phdr *) (base + eh->e_phoff);
    for (int i = 0; i < eh->e_phnum; i++) {
        if (ph[i].p_type != PT_LOAD) continue;
        if (rva >= ph[i].p_vaddr && rva < ph[i].p_vaddr + ph[i].p_memsz) {
            return base + ph[i].p_vaddr + (rva - ph[i].p_vaddr);
        }
    }
    return base + rva;
}

/* ========================================================================== */
/*  5. 判定 & 拦截                                                            */
/* ========================================================================== */
static inline bool IsBadScene(const std::string &n) {
    /* 大小写不敏感比 "scncls"：代码里写 scnCLS，报错框里是 ScnCLS */
    if (n.size() != 6) return false;
    static const char *kBad = "scncls";
    for (size_t i = 0; i < 6; i++) {
        char c = n[i];
        if (c >= 'A' && c <= 'Z') c = (char) (c - 'A' + 'a');
        if (c != kBad[i]) return false;
    }
    return true;
}

static void  *g_gotoMenu      = nullptr;                    /* ADOBase.GoToLevelSelect() */
static void (*g_origLoadScene1)(Il2CppString *) = nullptr;  /* 给 Tick 的测试按钮用 */

/* 返回 true：照常调原函数（参数可能已被换成新名字）
   返回 false：把这次加载整个丢掉                                             */
static inline bool Intercept(Il2CppString **ppName) {
    std::string name = StrToStd(*ppName);
    PushSeen(name.c_str());

    if (!enabled || !IsBadScene(name)) return true;

    hits = hits + 1;

    if (mode == MODE_BLOCK) {
        PushLog("拦截 %s（不跳转）", name.c_str());
        return false;
    }

    if (mode == MODE_REDIRECT) {
        if (!target[0]) {
            PushLog("拦截 %s（目标场景为空）", name.c_str());
            return false;
        }
        Il2CppString *ns = MakeStr(target);
        if (!ns) {
            PushLog("拦截 %s（造字符串失败）", name.c_str());
            return false;
        }
        *ppName = ns;
        PushLog("改跳 %s -> %s", name.c_str(), target);
        return true;
    }

    /* MODE_GO_MENU：调游戏自己的回主菜单函数（内部用 GCNS.sceneLevelSelect，
       安卓上是 "scnMobileMenu"，是游戏自己保证存在的名字）。 */
    static __thread bool inRedirect = false;          /* 防递归 */
    if (inRedirect) {
        PushLog("递归保护：再拦一次 %s", name.c_str());
        return false;
    }
    if (!g_gotoMenu) {
        PushLog("拦截 %s（GoToLevelSelect 地址没解析到）", name.c_str());
        return false;
    }
    PushLog("拦截 %s -> 回主菜单", name.c_str());
    inRedirect = true;
    ((void (*)(void)) g_gotoMenu)();
    inRedirect = false;
    return false;
}

/* ========================================================================== */
/*  6. 钩子                                                                   */
/* ========================================================================== */
static void    (*old_LS1)(Il2CppString *)               = nullptr;
static void    (*old_LS2)(Il2CppString *, int32_t)      = nullptr;
static int32_t (*old_LS3)(Il2CppString *, int32_t)      = nullptr;  /* 返回 Scene（4 字节） */
static void   *(*old_LSA1)(Il2CppString *)              = nullptr;
static void   *(*old_LSA2)(Il2CppString *, int32_t)     = nullptr;

static inline void h_LS1(Il2CppString *name) {
    if (!Intercept(&name)) return;
    if (old_LS1) old_LS1(name);
}

static inline void h_LS2(Il2CppString *name, int32_t m) {
    if (!Intercept(&name)) return;
    if (old_LS2) old_LS2(name, m);
}

static inline int32_t h_LS3(Il2CppString *name, int32_t p) {
    /* Scene 结构体只有一个 int，x0/w0 就是整个返回值，所以返回 0 = 无效 Scene */
    if (!Intercept(&name)) return 0;
    return old_LS3 ? old_LS3(name, p) : 0;
}

static inline void *h_LSA1(Il2CppString *name) {
    if (!Intercept(&name)) return nullptr;
    return old_LSA1 ? old_LSA1(name) : nullptr;
}

static inline void *h_LSA2(Il2CppString *name, int32_t p) {
    if (!Intercept(&name)) return nullptr;
    return old_LSA2 ? old_LSA2(name, p) : nullptr;
}

static inline void InstallHook(const char *what, uintptr_t rva, void *fn, void **orig) {
    uintptr_t addr = Rva2Addr("libil2cpp.so", rva);
    if (!addr) {
        SCF_LOGE("定位失败 %s", what);
        return;
    }
    int r = DobbyHook((void *) addr, fn, orig);
    SCF_LOGI("hook %-36s %p -> %s", what, (void *) addr, r == 0 ? "OK" : "FAIL");
}

/* ========================================================================== */
/*  7. 入口：hack_thread 里、libil2cpp 加载完成后调一次                        */
/* ========================================================================== */
static inline void Init(const char *libName = nullptr) {
#if defined(__aarch64__)
    (void) libName;
    g_gotoMenu = (void *) Rva2Addr("libil2cpp.so", SCF_OFF_GOTO_MENU);
    SCF_LOGI("ADOBase.GoToLevelSelect = %p", g_gotoMenu);

    InstallHook("SceneManager.LoadScene(str)",             SCF_OFF_LS_STRING,        (void *) h_LS1,  (void **) &old_LS1);
    InstallHook("SceneManager.LoadScene(str,mode)",        SCF_OFF_LS_STRING_MODE,   (void *) h_LS2,  (void **) &old_LS2);
    InstallHook("SceneManager.LoadScene(str,params)",      SCF_OFF_LS_STRING_PARAM,  (void *) h_LS3,  (void **) &old_LS3);
    InstallHook("SceneManager.LoadSceneAsync(str)",        SCF_OFF_LSA_STRING,       (void *) h_LSA1, (void **) &old_LSA1);
    InstallHook("SceneManager.LoadSceneAsync(str,params)", SCF_OFF_LSA_STRING_PARAM, (void *) h_LSA2, (void **) &old_LSA2);

    g_origLoadScene1 = old_LS1;
    PushLog("ScnCLSFix 已启用（默认：拦截 scnCLS -> 回主菜单）");
#else
    (void) libName;
    SCF_LOGE("ScnCLSFix 只支持 arm64-v8a（偏移来自 64 位 libil2cpp.so）");
#endif
}

/* 可选：把「测试按钮」挂到主线程 —— 在你已有的每帧 hook（例如 scrRing_Update）
   末尾调一次 ScnCLSFix::Tick(); 即可。不挂也不影响正常拦截。 */
enum { PEND_NONE = 0, PEND_MENU = 1, PEND_REDIRECT = 2 };
static volatile int g_pending = PEND_NONE;

static inline void Tick() {
    const int p = g_pending;
    if (!p) return;
    g_pending = PEND_NONE;

    if (p == PEND_MENU && g_gotoMenu) {
        PushLog("手动测试：回主菜单");
        ((void (*)(void)) g_gotoMenu)();
    } else if (p == PEND_REDIRECT && g_origLoadScene1 && target[0]) {
        Il2CppString *s = MakeStr(target);
        if (s) {
            PushLog("手动测试：跳到 %s", target);
            g_origLoadScene1(s);
        } else {
            PushLog("手动测试失败：造字符串失败（先让游戏自己触发一次拦截即可）");
        }
    }
}

/* 给 Java 菜单（Changes 里的 case）用 */
static inline void SetEnabled(bool v) { enabled = v; }
static inline bool IsEnabled()        { return enabled; }
static inline int  HitCount()         { return hits; }

/* ========================================================================== */
/*  8. ImGui                                                                  */
/* ========================================================================== */
static inline void DrawMenu() {
    ImGui::TextWrapped("打完一关后游戏会执行 LoadScene(\"scnCLS\")"
                       "（scrController.QuitToMainMenu 的 customLevelPaths 分支），"
                       "但这个 APK 里没有 scnCLS 场景，于是抛异常、弹「出问题了！」。"
                       "开启后会在 SceneManager 这一层截住它并改道。");

    ImGui::Spacing();
    ImGui::Checkbox("启用修复##scncls", &enabled);
    ImGui::SameLine();
    ImGui::TextDisabled("已拦截 %d 次", (int) hits);

    ImGui::Separator();
    ImGui::TextUnformatted("命中 scnCLS 时：");
    if (ImGui::RadioButton("回主菜单（推荐）",  mode == MODE_GO_MENU))  mode = MODE_GO_MENU;
    if (ImGui::RadioButton("改跳到指定场景",    mode == MODE_REDIRECT)) mode = MODE_REDIRECT;
    if (ImGui::RadioButton("只拦截，不跳转",    mode == MODE_BLOCK))    mode = MODE_BLOCK;

    if (mode == MODE_REDIRECT) {
        ImGui::Spacing();
        ImGui::InputText("目标场景名", target, sizeof(target));
        ImGui::SameLine();
        if (ImGui::SmallButton("用 scnMobileMenu##scf")) {
            strncpy(target, "scnMobileMenu", sizeof(target) - 1);
            target[sizeof(target) - 1] = '\0';
        }
        if (seenCount > 0 && ImGui::TreeNode("游戏里实际加载过的场景（点一下填入）")) {
            for (int i = 0; i < seenCount; i++) {
                if (ImGui::Selectable(seenNames[i], false, ImGuiSelectableFlags_DontClosePopups)) {
                    strncpy(target, seenNames[i], sizeof(target) - 1);
                    target[sizeof(target) - 1] = '\0';
                }
            }
            ImGui::TreePop();
        }
        ImGui::TextDisabled("提示：安卓版主菜单场景是 scnMobileMenu（ADOStartup 里写死的）");
    }

    if (mode == MODE_GO_MENU) {
        ImGui::TextDisabled("调用 ADOBase.GoToLevelSelect()，用游戏自己的场景名，最不容易出错");
    }

    ImGui::Separator();
    if (ImGui::Button("立即测试：回主菜单##scf"))   g_pending = PEND_MENU;
    ImGui::SameLine();
    if (ImGui::Button("立即测试：跳目标场景##scf")) g_pending = PEND_REDIRECT;
    ImGui::TextDisabled("测试按钮需要主线程每帧 hook 里调用 ScnCLSFix::Tick()（可选）");

    ImGui::Separator();
    ImGui::TextUnformatted("最近日志：");
    const int n = (logCount < 12) ? (int) logCount : 12;
    for (int k = 0; k < n; k++) {
        const int idx = (logCount - 1 - k) % 12;
        ImGui::TextUnformatted(logs[idx]);
    }
    if (ImGui::Button("清空日志##scf")) logCount = 0;
}

/* 独立窗口版；想塞进你自己的菜单里就只调 DrawMenu() */
static inline void DrawWindow(bool *pOpen = nullptr) {
    if (ImGui::Begin("ScnCLS 修复##scnclsfix", pOpen)) {
        DrawMenu();
    }
    ImGui::End();
}

} /* namespace ScnCLSFix */
