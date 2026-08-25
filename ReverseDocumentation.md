# ADoFAI Mobile Level Loader（冰与火之舞 移动版铺面加载器）
# 逆向分析文档（关卡加载逻辑梳理）

目标版本：游戏 3.3.1（`libil2cpp.so` BuildID 47b7ab40，libtool dump：
`dump/com.fizzd.connectedworlds_3.3.1.cs`，全部地址为 libil2cpp.so 内 RVA）。

## 1. 场景构成（来自 globalgamemanagers / catalog.bin）

移动版包含场景：`scnSplash`、`scnMobileMenu`、`scnCalibration`、`scnLoading`、
**`scnGame`（自定义关卡游玩场景，PC 遗留但完整保留！）**、`scnMinesweeper`、
`Levels/1-X`…`XN-X`（官方关卡）。注意：**没有** `scnCLS`（自定义关卡选关）、
`scnEditor`、`scnLevelSelect`——PC 的选关/编辑器场景未随包，但 `scnGame` 的完整
加载管线都在。

## 2. 关键静态状态：`GCS`（全局静态类，PC 遗留）

| 字段 | 偏移 | 含义 |
|---|---|---|
| customLevelPaths | 0x70 | `string[]`，自定义关卡路径列表 |
| loadCustomFromBundle | 0x78 | 是否从 AssetBundle 读 |
| customLevelIndex | 0x7c | 使用列表中的第几个 |
| internalLevelName | 0x80 | 内置关卡名（如 `"1-X"`）；**非空时 scnGame 忽略 customLevelPaths** |
| customLevelId | 0x88 | 创意工坊 ID（移动端恒 null） |
| sceneToLoad | 0x100 | 转场后要加载的场景名 |

## 3. 官方加载链路（PC 残留，移动端代码一致）

1. `scrController.LoadCustomLevel(path, id, fromBundle)` @ `0x2547B90`
   （实例方法，`(this,x1=path,x2=id,w3=fromBundle)`）：
   - 组装 `string[1]{path}` 写入 `GCS.customLevelPaths`；
   - `GCS.loadCustomFromBundle = fromBundle`；`id != null` 时写 `customLevelId`；
   - 尾调 `StartLoadingScene(WipeDirection=1)` @ `0x25477F4`。
2. `StartLoadingScene`：
   - 若非 `lofiVersion`：`scrLoader.LoadSceneWithTransition(wipe, scene=null)`。
   - `LoadSceneWithTransition` @ `0x227B848`：`scene != null` 才会覆盖
     `GCS.sceneToLoad`；然后黑屏转场，完成后 `LoadTargetScene`。
3. `LoadTargetScene` @ `0x227BDB0`：读 `GCS.sceneToLoad`，非空则
   `SceneManager.LoadScene(name)`。
4. `scnGame.Awake` @ `0x251D398` / `Update` @ `0x251D858`：
   - Update 中：`GCS.customLevelPaths != null` 且非编辑器场景时，Awake 后第 3 帧
     （`Time.frameCount - startFrame == 3`）且 `GCS.internalLevelName == null` 时，
     取 `customLevelPaths[customLevelIndex]` 调 `LoadAndPlayLevel(path)`。
   - `LoadAndPlayLevel` @ `0x251DA58` → `scnGame.LoadLevel(path, out status)`
     @ `0x251E060` → `LevelData.LoadLevel` @ `0x24BDFA0`：
     非 bundle 分支直接 `RDFile.ReadAllText(path)`（System.IO，可读任意有权限的
     绝对路径）→ JSON 反序列化 → `Decode` @ `0x24BE4F4`，随后 `RemakePath`、
     `ReloadAssets`、`UpdateDecorationObjects` 等完成铺面/音乐/装饰加载。
5. 相对资源（歌曲/背景图/装饰）相对铺面文件所在目录解析——所以
   `.adofai` 与其资源放同一目录即可。

**结论**：要加载自定义关卡，只需：

```
GCS.customLevelIndex   = 0
GCS.internalLevelName  = null        // 关键！否则 Update 走内置关卡分支
GCS.customLevelId      = null
GCS.sceneToLoad        = "scnGame"   // LoadTargetScene 用
scrController.LoadCustomLevel(path, null, false)
```

`LoadCustomLevel` 内部会设置 `customLevelPaths=[path]` 并触发黑屏转场 →
`scnGame` → 3 帧后自动 `LoadAndPlayLevel(path)`。退出关卡走游戏自身的
`PortalTravelAction`/`QuitToMainMenu`（移动端加载 `scnMobileMenu`，均已适配）。

## 4. 输入链路（触摸拦截点）

游戏触摸输入全部汇聚到 UnityEngine 旧输入系统：
`游戏(Rewired.ReInput.UnityTouch / EventSystem) → UnityEngine.Input`。
因此只需 hook `UnityEngine.Input` 的方法即可同时完成**捕获**与**屏蔽**：

| 方法 | RVA |
|---|---|
| `Input.get_touchCount()` | `0x46206DC` |
| `Input.GetTouch(int)`（Touch 结构体经 x8 sret 返回） | `0x461FE20` |
| `Input.get_touches()`（返回 Touch[]，屏蔽时清 max_length=0x18） | `0x4620818` |
| `Input.GetMouseButton/Down/Up(int)` | `0x461FD44/0x461FD80/0x461FDBC` |
| `Input.get_mousePosition()`（Vector3 经 x8 sret） | `0x4620204` |

Touch 结构体（68 字节）：fingerId@0、position@4(Vector2)、phase@36
（0=Began,1=Moved,2=Stationary,3=Ended,4=Canceled）。

## 5. 注入与渲染

- **注入**：dex 中 `const-string v0, "Tool"` + `System.loadLibrary(v0)`（与
  libTool 示例相同）。`JNI_OnLoad` 启动后台线程等待 `libil2cpp.so` 加载 →
  dlsym il2cpp 导出 API → 解析 `GCS`/`scrController`（Assembly-CSharp）。
- **渲染**：hook `libEGL.so` 的 `eglSwapBuffers`，在 Unity 提交前用
  ImGui + GLES3 后端（`IMGUI_IMPL_OPENGL_ES3`，`#version 300 es`）绘制覆盖层；
  EGL 上下文变化（切后台恢复）时自动重建 ImGui。
- **Hook 引擎**（`hooks.cpp`）：AArch64 内联 hook。优先 4 字节 `B veneer`
  （首条指令非 PC-relative 时），否则 16 字节 `LDR X16;BR X16;.quad` 并搬移
  前 4 条指令到近程 trampoline（完整重定位 B/BL/B.cond/CBZ/TBZ/ADR/ADRP/字面
  量加载）。veneer/trampoline 分配在目标 ±100MB 内的最近空闲页。已在宿主机
  aarch64 上通过单元测试（含 adrp/tbnz 前奏的真实函数）。
- **线程模型**：游戏函数调用（LoadCustomLevel / TogglePauseGame）一律排队到
  **游戏主线程**（在 `get_touchCount` hook 内执行）；`LoadCustomLevel`、
  `TogglePauseGame` 经 `il2cpp_runtime_invoke` 调用（受控异常不会穿过我们的
  静态 libc++abi 边界）。GCS 静态字段用 `il2cpp_field_static_set_value`
  （已从反汇编确认其语义：引用类型取 `*(void**)value`）。
- **字体**：内嵌 NotoSansCJK 子集（UI 全部字符，`src/font/font_embedded.h`），
  另尝试合并系统 CJK 字体用于文件名显示。

## 已知限制

- 仅 arm64（游戏本身即 arm64-only 的 libil2cpp）。
- 无存储权限时只能浏览游戏自有数据目录（系统限制，非工具限制）。
- 悬浮按钮依赖游戏每帧轮询 `Input.touchCount`（Unity 旧输入在任何场景都会
  轮询；个别 Loading 画面可能不响应）。
