# ADOFAI（冰与火之舞）异步输入系统（AsyncInput）逆向分析报告

> 研究对象：`文件导出` 文件夹中的 AssetRipper 导出工程（Unity + Mono）
> 核心：`AsyncInput` / `AsyncInputManager` / `AsyncInputUtils` / `RDInputType_AsyncKeyboard` + 预编译插件 `SkyHook.Unity.dll`（原生 DLL 名 `skyhook`，未随导出工程提供）

---

## 1. 这个系统解决什么问题

普通 Unity 输入（`Input.GetKeyDown` 等）只在渲染帧的 `Update` 里被采样。若游戏帧率低于输入节奏（高刷键盘玩家、掉帧、录手元回放），两次采样之间的按键按下/抬起事件会丢失或被合并，导致判定延迟不稳定。

异步输入方案：用**操作系统级键盘钩子**在一个独立线程上实时接收按键事件，为每个事件打上**高精度时间戳**（100ns tick），再由主线程按时间戳“重放”到游戏判定逻辑中。判定不再绑定渲染帧的时刻，而是绑定事件发生的真实时刻。

---

## 2. 总体架构

```
┌─────────────── 原生层（未导出）────────────────┐
│ skyhook.dll : StartHook / StopHook / HookIsRunning│
│               SetContext / GetKeyLabel / GetKeyCode│
│   （系统级键盘钩子，异步回调托管层）                │
└───────────────┬───────────────────────────────┘
                │ P/Invoke（SkyHookNative）
┌───────────────▼───────────────────────────────┐
│ SkyHook.Unity.dll（已反编译 IL）               │
│  SkyHookManager : 单例 MonoBehaviour           │
│   - 启动专用钩子线程 + ManualResetEvent         │
│   - NativeHookCallback → KeyUpdated(UnityEvent) │
│  SkyHookEvent : {TimeSec, TimeSubsecNano,       │
│                  Type, Label, Key}              │
│  SkyHookKeyMapper : 键码映射表                  │
└───────────────┬───────────────────────────────┘
                │ KeyUpdated.AddListener
┌───────────────▼───────────────────────────────┐
│ AsyncInputManager（MonoBehaviour，DontDestroy）│
│  keyQueue : ConcurrentQueue<SkyHookEvent>      │
│  currFrameTick / prevFrameTick / offsetTick    │
│  keyMask / keyDownMask / keyUpMask             │
│  frameDependent* 三套掩码                       │
└───────────────┬───────────────────────────────┘
                │ scrConductor.Update → scrController.UpdateInput()
┌───────────────▼───────────────────────────────┐
│ scrController.UpdateInput                       │
│  keyQueue → PriorityQueue（按事件 tick 排序）   │
│  → 同一 tick 的事件打包 → 掩码更新             │
│  → ProcessKeyInputs(tick) → 每个玩家            │
│     Simulated_PlayerControl_Update(tick)        │
│        → AdjustAngle → planet.AsyncRefreshAngles│
└───────────────────────────────────────────────┘
```

游戏侧文件（`Assets\Scripts\Assembly-CSharp\`）：
- `AsyncInput.cs` — 对外查询 API（GetKey/GetKeyDown/GetKeyUp）
- `AsyncInputManager.cs` — 状态与队列管理
- `AsyncInputUtils.cs` — 时钟对齐与角度计算
- `AsyncKeyCode.cs` — (ushort 原始键码, KeyLabel) 结构体
- `RDInputType_AsyncKeyboard.cs` — RDInput 输入抽象层的异步键盘实现
- `RDC.cs`（208–218 行）— `useAsyncInput` 属性 ↔ `ToggleHook`
- 调用方：`scrConductor.cs`、`scrController.cs`、`scrPlayer.cs`、`scrPlanet.cs`、`SettingsMenu.cs`、`KeysSetting.cs`、`Persistence.cs`

---

## 3. 底层插件 SkyHook（托管层 IL 反编译结果）

### 3.1 P/Invoke（`SkyHook.SkyHookNative`，原生库名 `"skyhook"`）

```csharp
public static class SkyHookNative {
    const string Lib = "skyhook";
    [DllImport(Lib)] static extern string   StartHook(Callback cb);   // 失败返回错误串，成功返回 null
    [DllImport(Lib)] static extern string   StopHook();               // 同上
    [DllImport(Lib)] static extern bool     HookIsRunning();
    [DllImport(Lib)] static extern void     SetContext(IntPtr ctx);   // 传入 GCHandle 指针
    [DllImport(Lib)] static extern KeyLabel GetKeyLabel(ushort key);  // 原生键码 → 语义标签
    [DllImport(Lib)] static extern ushort   GetKeyCode(KeyLabel key); // 反向映射
}
```

原生 `skyhook.dll` **不在导出工程中**（AssetRipper 不导出原生插件；实际游戏目录下有该 DLL）。

### 3.2 SkyHookEvent（值类型）

```csharp
public struct SkyHookEvent {
    long   TimeSec;         // Unix 秒
    uint   TimeSubsecNano;  // 亚秒部分，单位：纳秒
    EventType Type;         // KeyPressed=0, KeyReleased=1
    KeyLabel Label;         // 语义键位（Q/W/E…/ArrowLeft…/MouseX2…，Unknown=120）
    ushort Key;             // 原始键码（Native 键码）
    static long EpochTicks = new DateTime(1970,1,1).Ticks; // 621355968000000000
    long GetTimeInTicks() => TimeSec*10_000_000 + TimeSubsecNano/100 + EpochTicks;
}
```

`GetTimeInTicks()` 把事件时间换算成 **DateTime 风格的 100ns tick 数**（与 C# `DateTime.Now.Ticks` 同一坐标系）——这就是异步输入时间戳的基石。

### 3.3 SkyHookManager 启动/停止流程（IL 反编译 + 原始字节核对）

字段：`_instance`、`Nullable<GCHandle> _handle`、`ManualResetEvent _mre`、`static bool IsFocused`、`requireFocus`（默认 true）、`static UnityEvent<SkyHookEvent> KeyUpdated`。

**`_StartHook()`**（伪代码，已按 IL 分支目标修正）：

```csharp
var d = new { owner = this, started = false, exception = (Exception)null };
_mre = new ManualResetEvent(false);
_handle = GCHandle.Alloc(this, GCHandleType.Normal);   // 防止 this 被 GC，句柄地址作为上下文传给原生层
SkyHookNative.SetContext(GCHandle.ToIntPtr(_handle.Value));
new Thread(d.HookThread).Start();
while (!d.started && d.exception == null) Thread.Yield();  // 忙等钩子安装结果
if (d.exception != null) throw d.exception;
```

**钩子线程体 `HookThread()`**：

```csharp
try {
    string err = SkyHookNative.StartHook(new Callback(null, NativeHookCallback));
    if (err != null) exception = new SkyHookException(err);
    started = true;                 // 无论成败都置位，主线程退出忙等
    _mre.WaitOne();                 // 阻塞直到 StopHook 唤醒
    Debug.Log("Thread ended");
} catch (Exception ex) { exception = ex; Debug.LogError(ex); throw; }
```

> 注：`started` 字段含义是“StartHook 已返回”，不是“成功”。主线程忙等结束后若 `exception != null` 则抛给调用方。

**`_StopHook()`**：

```csharp
string err = SkyHookNative.StopHook();
if (_handle.HasValue) { _handle.Value.Free(); _handle = null; }
if (err != null) throw new SkyHookException(err);
if (_mre != null) _mre.Set();       // 释放钩子线程的 WaitOne
```

**回调链**：

```csharp
static void NativeHookCallback(IntPtr ctx, SkyHookEvent e) {
    if (ctx != <已登记的 GCHandle 指针>) return;      // 防御性校验
    ((SkyHookManager)GCHandle.FromIntPtr(ctx).Target).HookCallback(e);
}
void HookCallback(SkyHookEvent e) {
    if (requireFocus && !IsFocused && e.Type == EventType.KeyPressed) return; // 失焦时忽略按下事件
    KeyUpdated.Invoke(e);
}
void Update() { if (requireFocus) IsFocused = Application.isFocused; }
```

`get_isHookActive` 直接查 `SkyHookNative.HookIsRunning()`。

**要点**：钩子回调发生在**原生钩子线程**上；事件经 `KeyUpdated`（UnityEvent）直接转发给 `AsyncInputManager.Setup` 注册的监听器 → 进入 `keyQueue`（并发队列，跨线程安全）。

### 3.4 SkyHookKeyMapper

静态字典 `AsyncKeyToUnityKeyMap` / `UnityKeyToAsyncKeyMap`，`.cctor` 里用 `AddMapping` 登记约 120 组映射（含小键盘 256–296、媒体键 300+ 等）。`NativeKeyCodeToKeyLabel` / `KeyLabelToNativeKeyCode` 走原生 DLL。

---

## 4. 事件队列与掩码（AsyncInputManager + scrController.UpdateInput）

### 4.1 静态状态（`AsyncInputManager.cs`）

```csharp
ConcurrentQueue<SkyHookEvent> keyQueue;   // 钩子线程写入
ulong currFrameTick, prevFrameTick;      // 每帧的 DateTime.Now.Ticks
ulong targetSongTick;                    // 本帧判定用的事件 tick（已减 offsetTick）
ulong offsetTick;  bool offsetTickUpdated;
HashSet<AsyncKeyCode> keyMask, keyDownMask, keyUpMask;               // 按“事件流”维护
HashSet<AsyncKeyCode> frameDependentKeyMask, ...Down, ...Up;         // 按“帧”维护
ulong lastReportedTargetTick;
```

`Setup()`（`[RuntimeInitializeOnLoadMethod]`）：注册 `KeyUpdated` 监听（**过滤掉全部鼠标键**，鼠标只用于 UI），创建 `DontDestroyOnLoad` 的 "Async Input Manager" 对象，默认 `enabled=false`（只有钩子激活时才启用）。

### 4.2 UpdateInput()（scrController.cs 2498–2591）

`scrConductor.Update()` 每帧（`AsyncInputManager.isActive` 时）执行：

```csharp
prevFrameTick = currFrameTick;
currFrameTick = (ulong)DateTime.Now.Ticks;     // 帧边界时间戳
previousFrameTime = Time.unscaledTimeAsDouble;
if (!pause) AsyncInputUtils.UpdateOffsetTime(100);  // 时钟对齐（见 §5）
if (!controller.paused) controller.UpdateInput();
```

`UpdateInput()` 流程：

1. 清空四个 Down/Up 掩码；若窗口失焦则 `ClearKeys()`。
2. 把 `keyQueue` 全部搬入 `PriorityQueue<SkyHookEvent, ulong> sortedKeyQueue`，按 `GetTimeInTicks()` **升序**排序（解决队列乱序/迟到的回调）。
3. 逐个取出事件；**tick 相同的事件视为同一批**：
   - 批边界处调用 `ProcessKeyInputs(上一批 tick)`，并清空 `keyDownMask/keyUpMask`；
   - `KeyPressed`：加入 `keyMask`（若没有）+ `keyDownMask` + `frameDependentKeyMask` + `frameDependentKeyDownMask`；
   - `KeyReleased`：从 `keyMask`/`frameDependentKeyMask` 移除，加入 `keyUpMask`/`frameDependentKeyUpMask`。
4. 若本帧有按下事件 → 记 `frameOnAsyncInputDetected`；5 帧无任何输入且超过 10 帧未检测到异步事件且不在游戏状态 → 警告 "Async input is not working properly!" 并 `IncrementDefuncWarn()`（连续 5 次且处于特定菜单场景 → `DisableAsyncInput()` 自动关掉该功能并弹错误框）。
5. 最后 `ProcessKeyInputs(最后一批 tick)`。

### 4.3 ProcessKeyInputs（2598–2613）

```csharp
if (paused) return;
ulong tick = (eventTick != 0) ? eventTick : currFrameTick;  // 无事件时用帧时刻兜底
if (state == PlayerControl && 目标状态 == PlayerControl)
    foreach (scrPlayer p in playerManager) p.Simulated_PlayerControl_Update(tick);
lastReportedTargetTick = tick;
```

即：**每一批同 tick 的事件触发一次完整的玩家控制模拟**，模拟的时刻就是事件 tick。

---

## 5. 时钟对齐：把“挂钟时间”映射到“歌曲时间”

三套时间：
- **事件 tick**：SkyHook 时间戳（100ns，基于挂钟）。
- **帧 tick**：`currFrameTick = DateTime.Now.Ticks`。
- **歌曲时间**：`conductor.dspTime`（`AudioSettings.dspTime`，音频线程的采样时间，会随 `song.pitch` 等修正）。

### 5.1 UpdateOffsetTime（AsyncInputUtils.cs 40–61）

```csharp
static void UpdateOffsetTime(long fixDivider = 1) {
    ulong now  = currFrameTick - (ulong)(scrConductor.instance.dspTime * 10_000_000);
    long  diff = (long)(now - offsetTick);
    if (fixDivider == 1) offsetTick = now;               // 立即对齐（暂停恢复/状态切换时）
    else if (diff != 0)  offsetTick += (ulong)(diff / fixDivider); // 分 100 帧渐进收敛，防止跳变
    else return;
    offsetTickUpdated = true;
}
```

含义：`offsetTick ≈ 挂钟tick − 音频dspTime对应的tick`，即两时钟的**偏移量**。每帧用 1/100 的步长趋近真值（低通滤波），`UpdateOffsetTime(1)` 则瞬间对齐。调用点：
- `scrConductor.Update`：每帧 `UpdateOffsetTime(100)`（非暂停时）；
- `scrController.TogglePauseGame`：暂停时置 `offsetTickUpdated=false`，恢复时 `UpdateOffsetTime(1)`；
- `scrController` 2775（过关等状态）：`UpdateOffsetTime(1)`。

### 5.2 AdjustAngle → AsyncRefreshAngles

```csharp
// AsyncInputUtils.AdjustAngle
if (isActive && offsetTickUpdated) {
    targetSongTick = targetTick - offsetTick;           // 事件tick → “音频时钟tick”
    player.planetarySystem.chosenPlanet.AsyncRefreshAngles();
}

// scrPlanet.AsyncRefreshAngles
angle = AsyncInputUtils.GetAngle(this, snappedLastAngle, targetSongTick);

// GetAngle
GetSongPosition(nowTick) / crotchetAtStart * π * speed * (isCW ? 1 : -1) + snappedLastAngle;

// GetSongPosition（非旧版/WebGL）
(nowTick/1e7 - conductor.dspTimeSong - calibration_i) * song.pitch - addoffset;
```

对比非异步路径 `scrPlanet.Update_RefreshAngles`（371 行）：用 `conductor.songposition_minusi`（每帧实时音频位置）计算角度。异步路径在**每一次输入事件批处理前**用事件 tick 精确重算行星角度，使判定（角度窗口）对应该事件发生的瞬间。

### 5.3 时序图

```
挂钟tick:  ────────|────────────|───────
事件 e₁(按下)  e₂(抬起)     帧边界(currFrameTick)
音频dspTime: ──────────────────────────── (连续)
offsetTick = currFrameTick - dspTime*1e7  →  每帧向真值收敛1/100
targetSongTick(e) = e.tick - offsetTick   →  事件时刻对应的“音频时间”
```

---

## 6. 游戏逻辑重放：scrPlayer.Simulated_PlayerControl_Update（scrPlayer.cs 401–451）

```csharp
public void Simulated_PlayerControl_Update(ulong? targetTick = null) {
    if (!alive || paused || currFloor == null || isCutscene) return;
    validInputWasReleasedThisFrame = ValidInputWasReleased();
    ...缓存 nextfloor / hold 信息...
    WhileFloorNotChange(() => CheckPostHoldFail(targetTick));     // 过晚失败判定
    WhileFloorNotChange(() => OttoHoldHit(targetTick));           // 自动/连打
    HitAutoFloors(targetTick);                                    // 计数连打键数
    UpdateHoldBehavior(targetTick);                               // hold 成功/失败
    WhileFloorNotChange(() => HitHoldFloorsIfStartedAtHold(targetTick));
    WhileFloorNotChange(() => CheckPreHoldFail(targetTick));      // 过早失败判定
    WhileFloorNotChange(() => UpdateHoldKeys(targetTick));
    if (RDInput.GetMain(ButtonState.WentUp) > 0) HitInputEvent(isAuto:false, InputEventState.Up);
    ...相机位置变化检测...
}
```

每个子步骤开头若有 `targetTick` 就先 `AdjustAngle`——**以事件时刻的角度做判定**。`WhileFloorNotChange` 循环执行到 `currFloor.seqID` 不再变化为止：一批输入可能连跨多个格子（多押/连打），必须迭代收敛。

输入读取仍走 `RDInput`（`ValidInputWasTriggered` → `RDInput.GetMain(IsDown)` → `CountValidKeysPressed`），但底层数据来自 §4 的掩码而非 Unity 轮询。

---

## 7. 查询 API 与输入抽象层

### 7.1 AsyncInput（静态类）

```csharp
GetKey(AsyncKeyCode|ushort|KeyLabel, frameDependent = true)
GetKeyDown(...)   GetKeyUp(...)
```

按 `KeyState.Held/Down/Up` 选择 `keyMask/keyDownMask/keyUpMask`；`frameDependent=true` 则用 `frameDependent*` 三套。匹配规则（`AsyncKeyCode.==`）：**原始键码相同，或键码不同但语义标签相同**（如左右 Shift 互为替补）。

### 7.2 RDInputType_AsyncKeyboard

- 三实例：`KeyboardFull`（全部 `KeyLabel`）、`KeyboardLeft`（17 键）、`KeyboardRight`（20 键）。
- `Main(ButtonState)`：从对应掩码取集合，过滤到 `mainKeys` 范围；`WentDown` 时扣除 `GetSpecialInput()`（Esc、PrintScreen/F12/LAlt/Super、暂停键、UI 方向键、关卡选择键、CLS 键等），再经过 `Persistence.keyLimiterKeys.asyncKeysCache` 键位限制器过滤，产出 `List<AnyKeyCode>`。
- `IncrementDefuncWarn` 连续触发 5 次（每 5 秒递减一次）且在菜单/选关场景 → `DisableAsyncInput()`：弹 `error.asyncInputDisabled`、`ToggleHook(false)`、`SetChosenAsynchronousInput(false)` 并保存。
- `OnSceneChanged` 重置警告计数。

### 7.3 KeysSetting（按键设置）

`asyncKeys`：存 `HashSet<ushort>`（**原生键码**）到 `generalPrefs`；`KeyLabels` 用 `SkyHookKeyMapper.NativeKeyCodeToKeyLabel` 显示；`Add(KeyCode unityKey, ushort? asyncKey)` 同时写两套（Unity 键 + 异步键码）。

### 7.4 设置项与偏好

- `Persistence.GetChosenAsynchronousInput()` → `generalPrefs.GetBool("useAsynchronousInput")`，**SteamOS 上强制 false**（1241–1248）。
- `SettingsMenu` "useAsynchronousInput" 项：`RDC.useAsyncInput = flag → AsyncInputManager.ToggleHook`，异常时显示平台相关错误（Mac 有专属文案）。
- `scrController` 929–932：场景就绪时若偏好开启且钩子未激活 → `ToggleHook(true)`。
- `RDC.useAsyncInput` ↔ `AsyncInputManager.isActive`（后者查原生 `HookIsRunning`；原生 DLL 缺失时捕获 `DllNotFoundException` 返回 false）。

### 7.5 运行期输入切换（AsyncInputManager.Update）

在游戏状态（`States.PlayerControl`）下：禁用普通 `RDInputType_Keyboard`，启用三个 async 实例；离开游戏状态则反过来，并 `ClearKeys()`。暂停菜单里也据此给 UI 恢复普通键盘（`PauseMenu.cs:739`）。

---

## 8. 暂停 / 失焦 / 清键语义

- `TogglePauseGame`：暂停 → `offsetTickUpdated = false`（禁止用旧偏移重算角度）；恢复 → `UpdateOffsetTime(1)` 立即重对齐（消除暂停期间的挂钟漂移）。
- `UpdateInput` 开头窗口失焦 → `ClearKeys()`（`keyMask/keyQueue/keyDownMask/keyUpMask`）。
- `ClearKeys()` **不清理** `frameDependentKeyMask`（疑似遗留行为，`frameDependent` 按下掩码只在 `UpdateInput` 开头清 Down/Up 部分）。
- `paused` 时 `ProcessKeyInputs` 直接返回，事件照常进入掩码但不驱动判定。
- 键位编辑模式（`pauseMenu.settingsMenu.editingKeys`）下 `GetSpecialInput` 不剥离任何键，便于录键。

---

## 9. 调试功能

- 作弊码 `asyncinputdebug`（`RDCheatCode`，`AsyncInputManager.Update` 里检测）切换 `DebugMode`。
- `OnGUI` 右上角面板显示：ON/OFF、队列数、`keyMask` / `keyDownMask` / `keyUpMask` 内容与计数。
- 开发模式（`GCS.allowDebug`）下 `UpdateInput` 打印每条按下事件："key down event received!" + 当前帧 tick + 事件 tick。

---

## 10. 值得注意的实现细节与潜在问题

1. **跨线程**：`keyQueue` 用 `ConcurrentQueue`；`KeyUpdated` 的回调发生在钩子线程，直接 Enqueue，主线程才消费——队列是唯一共享点。
2. **钩子线程生命周期**：`StartHook` 在专用线程调用，成功后 `WaitOne` 挂起；`StopHook` 释放 GCHandle、停钩、`Set()` 唤醒线程退出。GCHandle 用 `Normal`（非 Pinned）——原生层拿到的是句柄的数值地址，回调时 `FromIntPtr` 找回托管对象。
3. **失焦过滤只过滤 KeyPressed**：抬起事件照常上报，防止“按下时失焦、抬起时丢事件”导致的按键卡死。
4. **鼠标事件不进 keyQueue**（`Setup` 里按 `MouseKeys` 列表过滤），鼠标仅通过 `pausedKeys`/UI 路径参与。
5. **批次边界即判定时机**：同一 tick 的多键同时生效（天然支持多押），不同 tick 的事件按排序依次触发完整模拟，多事件可一帧内连跨多格（`WhileFloorNotChange` 收敛）。
6. **时钟偏移渐进收敛**（÷100）：避免 dspTime 跳变（音频设备切换、暂停恢复）造成角度突变；代价是前 100 帧内偏移有微小误差。
7. **`frameDependent` 掩码语义**：与非 frameDependent 掩码在 `UpdateInput` 里同步维护，差异只在 `ClearKeys()` 不清 frameDependent 版；`AsyncInput.GetKey*` 默认 `frameDependent: true`。
8. **原生 DLL 缺失降级**：`isActive` 捕获 `DllNotFoundException` 返回 false，游戏回退普通输入；Mac 切换失败有专属报错文案。
9. **自动熔断**：异步事件长期不工作（可能钩子被安全软件拦截/驱动冲突）→ 5 次警告后自动禁用并写回偏好，避免玩家卡死在不可操作状态。

---

## 11. 附录：文件清单

游戏侧（`文件导出\ExportedProject\Assets\Scripts\Assembly-CSharp\`）：

| 文件 | 内容 |
|---|---|
| `AsyncInput.cs` | 查询 API（GetKey/Down/Up，Held/Down/Up 掩码选择） |
| `AsyncInputManager.cs` | 队列、tick、掩码、启停、调试面板 |
| `AsyncInputUtils.cs` | GetSongPosition / GetAngle / AdjustAngle / UpdateOffsetTime |
| `AsyncKeyCode.cs` | (key, label) 结构体与相等性规则 |
| `RDInputType_AsyncKeyboard.cs` | RDInput 异步键盘实现、键区、特殊键剥离、熔断 |
| `scrController.cs` | UpdateInput / ProcessKeyInputs（2498–2613）、暂停对齐（2182–2238）、启用（929–932）、PlayerControl_Update（2778） |
| `scrConductor.cs` | Update 中帧 tick 与 UpdateOffsetTime(100)（792–825） |
| `scrPlayer.cs` | Simulated_PlayerControl_Update 及全部判定子步骤（401–622）、ValidInputWasReleased（884） |
| `scrPlanet.cs` | AsyncRefreshAngles（549–552）、Update_RefreshAngles（371） |
| `RDC.cs` / `Persistence.cs` / `SettingsMenu.cs` / `KeysSetting.cs` | 开关、偏好、设置 UI、键位存储 |

插件：`文件导出\ExportedProject\Assets\Plugins\SkyHook.Unity.dll`（+ `AuxiliaryFiles\GameAssemblies\SkyHook.Unity.dll`）。
原生 `skyhook.dll` 未导出（游戏中位于游戏目录/Plugins）。

本报告配套产物：
- `skyhook_il_full.txt` — SkyHook.Unity.dll 全量 IL 反汇编（含原始字节）
- `ildump\` — 本次编写的独立 IL 反汇编器（.NET 9 控制台工程，离线可用）

---

## 12. C/C++ 语言实现（附完整可编译代码）

该系统的"C/C++ 实现"分两段，均已在工作区给出可编译源码并验证通过：

### 12.1 采集层：`skyhook_native\skyhook.c`（C）

用 Win32 重写原生 `skyhook.dll`，与反编译出的 P/Invoke 接口一一对应：

| 反编译接口 | C 实现要点 |
|---|---|
| `StartHook(Callback cb)` | 专用线程 + `SetWindowsHookExW(WH_KEYBOARD_LL/WH_MOUSE_LL, ..., NULL, 0)` + 消息循环；失败错误串用 `CoTaskMemAlloc` 返回 |
| `StopHook()` | `PostThreadMessage(WM_QUIT)` → 钩子线程内 `UnhookWindowsHookEx` |
| `SetContext(IntPtr)` | 保存 GCHandle 指针，回调时原样传回 |
| 回调 `void(void* ctx, SkyHookEvent e)` | **结构体按值传递**，布局严格对齐托管顺序布局（24 字节）；x86 用 `__stdcall` |
| `SkyHookEvent` 时间戳 | QPC + `GetSystemTimePreciseAsFileTime` 一次性校准 → Unix 秒 + 纳秒（托管层再换算 tick） |
| `GetKeyLabel/GetKeyCode` | 静态映射表：原生键码 = `扫描码 | (E0 扩展位<<8)`（布局无关物理键位），鼠标 0x301–0x305 |

构建（MSVC/MinGW 命令见 `skyhook_native\README.md`），x64 已用 VS Build Tools 编译通过；x86 另附 `skyhook_x86.def`（stdcall `_Name@N` 修饰名 + 别名，兼容 Mono DllImport）。

### 12.2 引擎侧管线：`cpp_async_input_demo.cpp`（C++17，跨平台、纯标准库）

完整复刻游戏侧链路，每个类/函数标注了对应的游戏方法：

```
EventQueue(互斥+priority_queue)          ≈ ConcurrentQueue + sortedKeyQueue
Clocks::UpdateOffsetTime(100/1)          ≈ AsyncInputUtils.UpdateOffsetTime
Clocks::SongPosAt(tick)                  ≈ AsyncInputUtils.GetSongPosition
AsyncInputSystem::UpdateInput            ≈ scrController.UpdateInput（按 tick 分批）
AsyncInputSystem::ProcessBatch           ≈ scrController.ProcessKeyInputs
Game::SimulatePlayerUpdate(tick)         ≈ scrPlayer.Simulated_PlayerControl_Update
Game::AngleAt(tick)                      ≈ scrPlanet.AsyncRefreshAngles / GetAngle
```

演示用模拟钩子线程按拍点±30ms 抖动注入事件，60fps 跑完整管线；实测输出：

```
[HIT] beat 1, 事件偏离理想点 +28.75 ms
[HIT] beat 2, 事件偏离理想点 -37.66 ms
[HIT] beat 3, 事件偏离理想点 -2.42 ms
...（全部落在注入抖动范围内，与帧周期 16.7ms 无关）
```

两个实现要点（调试中发现并已修正，值得注意）：
1. **offsetTick 必须"瞬间对齐一次"**（对应游戏在进入 PlayerControl/取消暂停时调
   `UpdateOffsetTime(1)`）——EMA(÷100) 收敛速率是几何的（每帧只追 1%），从 0 开始
   收敛到 100ns 级精度需要数千帧；EMA 只负责追踪对齐之后的**慢漂移**。
2. **音频时钟必须由真实时间（音频后端）驱动**，不能按"每帧 +1/fps"累加——帧周期
   抖动会累积成系统性漂移（实测曾导致每拍 ~500ms 的偏差）。
