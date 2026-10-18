# ScnCLS 报错修复（ScnCLSFix.h）— 安装与说明

## 一、病因（已在本包 dump 里逐条对上）

报错框里的场景名是 `ScnCLS`，代码里实际写的是 `scnCLS`：

| 位置 | 代码 | 说明 |
|---|---|---|
| `scrController.QuitToMainMenu()` | `else GCS.sceneToLoad = "scnCLS";` | **打完关回菜单就走这条路**：`GCS.customLevelPaths != null` 时（玩 Xtra / 自制关 / 从列表进的关） |
| `scrController` 的 `Portal.CustomLevelsScene` 分支 | `GCS.sceneToLoad = "scnCLS";` | 进自制关卡列表 |
| `scnGame.LoadAndPlayLevel()` | `SceneManager.LoadScene("scnCLS");` | 读关失败兜底，紧跟着弹错误面板 |

`GCS.sceneToLoad` 最终交给 `SceneManager.LoadScene(...)`；这个 APK 的 build settings 里
**没有** `scnCLS` 场景，Unity 就在 `SceneManager.LoadScene` 内部抛
`ArgumentException: Scene 'ScnCLS' couldn't be loaded ...`。
截图里那段调用栈正好是 `DOTween.Tween.OnTweenCallback → SceneManager.LoadScene`，
也就是「黑屏过场补间的 OnComplete 里调 LoadScene」——和 `QuitToMainMenu → WipeToBlack → 加载` 对得上。

> 顺带说明：安卓版主菜单场景 **不是** `scnLevelSelect`，而是 `scnMobileMenu`。
> `ADOStartup` 里有一句 `if (ADOBase.isMobile) GCNS.sceneLevelSelect = "scnMobileMenu";`。
> 所以「回主菜单」不能用 `scnLevelSelect`。

## 二、修复原理

不去改那几条 C# 分支（编译后是 const 字符串 + 条件跳转，改起来脆），
而是在**唯一的总闸** `UnityEngine.SceneManagement.SceneManager` 的按名字加载重载上装 hook：

```
SceneManager.LoadScene(string)                        0x45D8190
SceneManager.LoadScene(string, LoadSceneMode)         0x45D8090
SceneManager.LoadScene(string, LoadSceneParameters)   0x45D80FC   (返回 Scene，只有 4 字节)
SceneManager.LoadSceneAsync(string)                   0x45D82D4
SceneManager.LoadSceneAsync(string, LoadSceneParams)  0x45D832C
ADOBase.GoToLevelSelect()  static void                0x2420BBC   (游戏自己的回主菜单)
```

名字等于 `scnCLS`（大小写不敏感，`ScnCLS`/`scnCLS` 都算）时改道：

* **模式 0（默认）回主菜单**：直接调用 `ADOBase.GoToLevelSelect()`，
  它内部是 `DOTween.KillAll(); LoadScene(GCNS.sceneLevelSelect)`，
  安卓上就是 `scnMobileMenu` —— 用游戏自己的字符串，不需要我们造 managed string，最稳。
* **模式 1 改跳指定场景**：把参数换成你填的场景名（走 `il2cpp_string_new`）。
* **模式 2 只拦截**：丢掉这次加载（画面会停在黑屏，一般不用）。

所有路径（QuitToMainMenu / Portal / LoadAndPlayLevel 兜底 / 以后任何新加的）都从
`SceneManager` 走，所以一个卡点全管住。

## 三、安装（3 处改动）

### 1) 放文件
把 `ScnCLSFix.h` 放到 `jni/Menu/` 目录（和 `Setup.h` 同级）。

### 2) `Menu/Setup.h` 第一行加 include

```cpp
#include "ScnCLSFix.h"      // ← 加这一行（放在 Setup.h 最前面）
```

`Main.cpp` 里 `#include "Menu/Setup.h"` 在 `hack_thread` 之前，所以 Main.cpp 也能看到这个命名空间。

### 3) `Main.cpp` 的 `hack_thread` 里装钩子

找到这段（你现有代码）：

```cpp
    // ============ 去除星球环（第14号功能）============
    HOOK("0x228A4E4", scrRing_Update, old_scrRing_Update);   // scrRing.Update
```

在它下面加两行：

```cpp
    // ============ 修复打完关卡后跳 scnCLS 崩 ============
    ScnCLSFix::Init();          // 装 SceneManager 钩子（默认拦截 scnCLS → 回主菜单）
```

（`ScnCLSFix::Tick();` 是可选的，见第五节的测试按钮。）

### 4) ImGui 面板

在 `Setup.h` 里画菜单的地方，`ImGui::Begin(...)` 之后加一行：

```cpp
    ScnCLSFix::DrawMenu();     // 只画内容；想单独开窗就改成 ScnCLSFix::DrawWindow();
```

### 5)（可选）Java 菜单里也放一个开关

`GetFeatureList` 的 `features[]` 末尾加一项：

```cpp
        OBFUSCATE("Toggle_修复打完关卡后跳ScnCLS崩"),
```

`Changes()` 里加：

```cpp
        case 21:                       // ← 用你实际排到的编号（上面 20 项之后就是 21）
            ScnCLSFix::enabled = boolean;
            break;
```

> 开关默认就是**开**的（`enabled = true`），不加这一项也能修好。

## 四、验证

```bash
adb logcat -s ScnCLSFix
```

应该看到：

```
hook SceneManager.LoadScene(str)                 0x.... -> OK
hook SceneManager.LoadScene(str,mode)            0x.... -> OK
hook SceneManager.LoadScene(str,params)          0x.... -> OK
hook SceneManager.LoadSceneAsync(str)            0x.... -> OK
hook SceneManager.LoadSceneAsync(str,params)     0x.... -> OK
ScnCLSFix 已启用（默认：拦截 scnCLS -> 回主菜单）
```

随便打通一关 → 不再弹「出问题了！」，直接回主菜单，logcat 里出现：

```
拦截 scnCLS -> 回主菜单
```

## 五、面板里都有什么

| 控件 | 作用 |
|---|---|
| 启用修复 | 总开关 |
| 回主菜单（推荐） | 模式 0，调 `ADOBase.GoToLevelSelect()` |
| 改跳到指定场景 | 模式 1，下面填名字 |
| 只拦截，不跳转 | 模式 2 |
| 目标场景名 | 模式 1 用；右边小按钮一键填 `scnMobileMenu` |
| 游戏里实际加载过的场景 | 钩子抓到的所有场景名，点一下直接填入 —— **可以拿它确认你这个包到底有哪些场景** |
| 最近日志 / 已拦截 N 次 | 排查用 |
| 立即测试 | 需要 `ScnCLSFix::Tick()`，见下 |

测试按钮要用主线程，所以在你已有的每帧 hook（`scrRing_Update` 那个）末尾加一行：

```cpp
    ScnCLSFix::Tick();
```

不加也能正常拦截，只是「立即测试」按钮点了不生效。

## 六、排错

| 现象 | 原因 / 处理 |
|---|---|
| logcat 里 5 个 hook 全是 `FAIL` | offset 和你的包不匹配（版本不同）。用你这份 dump.cs 重新取偏移：搜 `SceneManager.LoadScene` / `LoadSceneAsync` |
| 日志里出现 `改跳 scnCLS -> XXX` 之后还报错 | 你填的 `XXX` 这个包里也没有。看「实际加载过的场景」列表，挑一个真实存在的 |
| 回主菜单后黑屏但没弹窗 | 说明拦截成功、但主菜单场景没切过去。改用模式 1，目标填 `scnMobileMenu` |
| 想彻底不动 hook | 那就得改 `QuitToMainMenu` 里的分支指令，是版本相关的字节补丁，没有 .so 反汇编我没法给你可靠地址 —— 把 `libil2cpp.so` 放出来可以再算 |
| 编译报 `DobbyHook` 找不到 | 你的项目里 Dobby 的符号名不是 `DobbyHook`（少数模板用 `DobbyHookEx`）。把 `ScnCLSFix.h` 里第 60 行的声明换成你模板 `Includes/Hooks.h` 里用的那个 |

## 七、偏移出处（本包 dump.cs）

```
4147:  public static System.Void GoToLevelSelect(); // 0x2420BBC     ← ADOBase
208861: SceneManagerAPIInternal.LoadSceneAsyncNameIndexInternal(...) // 0x45D70C0
208905: public static System.Void LoadScene(System.String sceneName, LoadSceneMode mode);   // 0x45D8090
208906: public static System.Void LoadScene(System.String sceneName);                       // 0x45D8190
208907: public static Scene  LoadScene(System.String sceneName, LoadSceneParameters p);     // 0x45D80FC
208910: public static AsyncOperation LoadSceneAsync(System.String sceneName);               // 0x45D82D4
208911: public static AsyncOperation LoadSceneAsync(System.String sceneName, LoadSceneParameters p); // 0x45D832C
```

`Scene` 结构体 = `{ SceneHandle m_Handle; }`，`SceneHandle` = `{ int m_Handle; }` → 4 字节，
所以 `LoadScene(string, LoadSceneParameters)` 的返回值就在 `w0`，hook 里声明成 `int32_t` 即可。
`LoadSceneParameters` 也只有一个 `LoadSceneMode` 字段（4 字节），从 `w1` 进。

## 八、几个技术细节（怕你踩坑）

1. **这套包是 Unity 6**。它的 metadata 里已经没有
   `SceneManager.LoadSceneAsync(string, LoadSceneMode)` 这个重载了
   （只有 `LoadSceneAsync(string)` 和 `LoadSceneAsync(string, LoadSceneParameters)`），
   所以本文这 5 个钩子就是「按名字加载场景」的全部入口，不会漏。
   `LoadScene(string, LoadSceneMode)` 还在（0x45D8090），所以也钩了它做保险。

2. **`LoadScene(string, LoadSceneParameters)` 的返回值**：`Scene` = `{ SceneHandle }`，
   `SceneHandle` = `{ int }` → 4 字节，走 `w0` 返回，hook 里当 `int32_t` 没问题。
   被拦掉时返回 0 就是「无效 Scene」，调用方本来也只用 `void` 那个重载，不影响。

3. **为什么模式 0 最稳**：`ADOBase.GoToLevelSelect()` 是游戏自己的函数，
   里面 `LoadScene(GCNS.sceneLevelSelect)` 用的是**游戏自己持有的字符串对象**
   （安卓上 `ADOStartup` 把 `GCNS.sceneLevelSelect` 改成了 `"scnMobileMenu"`），
   我们不需要造任何 managed string，也就没有 GC / klass 的风险。
   模式 1 才需要 `il2cpp_string_new`（拿不到就退化成手动拼一个 Il2CppString）。

4. **还会不会重复触发**：模式 0 里我们调 `GoToLevelSelect()` → 它会再走一次
   `SceneManager.LoadScene("scnMobileMenu")` → 又进我们的 hook → 名字正常 → 放行。
   用 `__thread inRedirect` 挡了极端情况下的自我递归。

5. **还想再深一层兜底**（比如走 Addressables 的 DLC 场景路径），可以再加一个钩子：

   ```cpp
   static void *(*old_ByNameOrIndex)(void *, Il2CppString *, int32_t, int32_t, int32_t);
   static void *h_ByNameOrIndex(void *self, Il2CppString *name, int32_t idx, int32_t param, int32_t must) {
       if (!Intercept(&name)) return nullptr;
       return old_ByNameOrIndex ? old_ByNameOrIndex(self, name, idx, param, must) : nullptr;
   }
   // Init() 里：
   InstallHook("SceneManagerAPI.LoadSceneAsyncByNameOrIndex", 0x45D7428, (void *) h_ByNameOrIndex, (void **) &old_ByNameOrIndex);
   ```
   （`this` 在 x0，string 在 x1，int 在 x2，`LoadSceneParameters` 4 字节在 x3，bool 在 w4。）

6. 交付前做过静态验证：
   - `g++ -std=c++11/14/17/20 -Wall -Wextra -Werror`（含 `-D__aarch64__` 分支）全部零告警通过；
   - 用 8 个用例跑了逻辑测试（大小写命中、正常场景放行且指针原样传递、
     模式 0/1/2、总开关、观察列表、日志），全部通过。
