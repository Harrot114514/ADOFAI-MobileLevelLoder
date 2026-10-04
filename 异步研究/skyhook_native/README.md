# skyhook 原生层 C 实现 — 构建与接入说明

对应反编译得到的 `SkyHook.Unity.dll` 中 `SkyHookNative` 的 P/Invoke 接口，
用 C 语言重写的 Windows 实现（`skyhook.dll`）。

## 文件

| 文件 | 说明 |
|---|---|
| `skyhook.c` | 完整实现：WH_KEYBOARD_LL + WH_MOUSE_LL 低级钩子、QPC 时间戳、键码映射、导出 API |
| `skyhook.def` | x64 导出名（未修饰） |
| `skyhook_x86.def` | x86 导出名（stdcall `_Name@N` 修饰名 + 未修饰别名，兼容 Mono DllImport 探测顺序） |

## 构建

### MSVC x64（推荐，游戏本体是 64 位）

```bat
call "%VSINSTALLDIR%\VC\Auxiliary\Build\vcvars64.bat"
cl /nologo /LD /O2 /W3 /utf-8 skyhook.c /link /DEF:skyhook.def /OUT:skyhook.dll
```

### MSVC x86（仅当托管侧是 32 位 Mono 时）

```bat
call "%VSINSTALLDIR%\VC\Auxiliary\Build\vcvars32.bat"
cl /nologo /LD /O2 /W3 /utf-8 skyhook.c /link /DEF:skyhook_x86.def /OUT:skyhook.dll
```

### MinGW-w64 x64

```bat
x86_64-w64-mingw32-gcc -shared -O2 -o skyhook.dll skyhook.c skyhook.def -lole32 -luser32
```

本仓库内已用 MSVC 14.51 (VS Build Tools) x64 编译验证通过，产物 `skyhook.dll`。

## 接口（与反编译结果一一对应）

```c
const char* StartHook(void (__stdcall *cb)(void* ctx, SkyHookEvent e)); // 失败返回错误串(CoTaskMem), 成功 NULL
const char* StopHook(void);            // 同上
int         HookIsRunning(void);
void        SetContext(void* ctx);     // 托管层写入 GCHandle 指针
KeyLabel    GetKeyLabel(uint16_t key);
uint16_t    GetKeyCode(KeyLabel label);
```

## ABI 关键点（必须与托管层对齐，否则回调结构体会错位）

1. **SkyHookEvent 按值传递**，布局 = 托管 `struct SkyHookEvent` 的顺序布局：
   `int64 timeSec; uint32 timeSubsecNano; int32 type; uint16 label; uint16 key;`
   （共 24 字节，8 对齐）。托管侧 `EventType` 底层是 `int32`、`KeyLabel` 是 `uint16`。
2. **回调调用约定**：x86 用 `__stdcall`（托管 delegate 默认），x64 忽略。
3. **字符串**：Mono 按 CoTaskMem 分配/释放返回字符串，失败信息用 `CoTaskMemAlloc`。
4. **x86 导出名**：Mono DllImport(Winapi=stdcall) 先探测未修饰名再探测 `_Name@N`，
   `skyhook_x86.def` 两种名字都导出。
5. **时间戳**：`timeSec` 是 Unix 秒、`timeSubsecNano` 是秒内纳秒；
   托管层 `GetTimeInTicks() = timeSec*1e7 + timeSubsecNano/100 + 1970-01-01 的 ticks`。
6. **原生键码**：本实现用 `扫描码 | (扩展键 ? 0x100 : 0)`（布局无关的物理键位，
   与 ADOFAI 左右手键区分码的用法一致）；鼠标用 0x301~0x305。`GetKeyCode` 未命中返回
   `0xFFFF`（对应托管 `AsyncKeyCode(KeyLabel)` 的 `ushort.MaxValue`）。

## 实现要点

- **WH_KEYBOARD_LL / WH_MOUSE_LL 是低级钩子**：全局捕获，但回调在安装线程的消息
  队列上投递 —— 所以必须有一个**常驻消息循环的专用线程**；且这类钩子允许 `lpfn`
  位于本进程、`hMod = NULL`，**不需要注入到其他进程**。
- `StartHook`：先 `SetContext`（写入 GCHandle 指针），再开线程装钩子，
  用事件对象等装好；失败路径把错误串经 CoTaskMem 返回，托管层会抛 `SkyHookException`。
- `StopHook`：`PostThreadMessage(WM_QUIT)` 让钩子线程退出消息循环后**在同线程**
  `UnhookWindowsHookEx`，再 `SetEvent` 语义上等价地唤醒等待方（托管层用
  `ManualResetEvent` 挂起钩子线程，本实现对应 `WaitForSingleObject(g_thread)`）。
- **回调必须极快**：它在钩子线程上执行，托管层只做 `ConcurrentQueue.Enqueue`。
- 时间基用 `QueryPerformanceCounter` + `GetSystemTimePreciseAsFileTime` 一次性校准，
  每条事件只算一次 QPC（比每次调 `GetSystemTime*` 快得多）。
- 按键自动重复：LL 钩子会持续收到 WM_KEYDOWN 重复消息，直接照传 ——
  托管层用 `keyMask.Contains` 去重，行为与原版一致。
- 失焦过滤（`requireFocus && !IsFocused && KeyPressed`）在托管层完成，原生层照发。

## 接入游戏/Unity 工程

1. 把编译出的 `skyhook.dll` 放到游戏目录（或 Unity 工程 `Assets/Plugins/` 对应平台子目录）。
2. 托管侧无需改动：`SkyHook.Unity.dll` 的 `SkyHookNative` 会自动找到本 DLL。
3. 替换游戏原版 `skyhook.dll` 属于对游戏运行环境的修改，仅用于研究与兼容性
   （如 Linux/Wine 下的替代实现、崩溃问题排查）。

## 注意事项

- 全局键盘钩子能看到**系统上所有按键**，本代码仅用于游戏输入系统研究/复刻，
  请勿用于任何恶意用途。
- 部分杀毒软件会误报低级键盘钩子；分发时需做签名或说明。
- 若钩子安装失败（如被杀软拦截），`StartHook` 会返回错误串，托管层会弹错误框，
  与游戏原版行为一致。
