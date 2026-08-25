# adofai-mobilelevelloader

## 冰与火之舞 · 移动版谱面加载器

对《冰与火之舞》(A Dance of Fire and Ice) Android 移动版（`com.fizzd.connectedworlds`，v3.3.1，Unity 6000.3.10f1 + il2cpp）注入一个 ImGui 文件选择器，用于加载自定义谱面（`.adofai`）。虽然官方移动版未提供该入口，但游戏的自定义关卡游玩场景 `scnGame` 及其完整加载管线（PC 遗留代码）都随包保留，本工具把这条链路完整接了起来。


## 目录结构

```
adofai-mobilelevelloder/
├── gameassest/                 游戏包全部资源文件，只保留重要文件
├── gamelibs/                   游戏全部 arm64 的 lib（libil2cpp.so 等）
├── imgui-master/               Dear ImGui 源码
├── levelexample/               自定义关卡示例（lightItUp.adofai + 歌曲/背景）
├── install方法以及示例/        安装说明（dex 植入 loadLibrary 方式）
│   ├── install.txt             安装说明
│   └── libTool.so              （旧示例工具，仅参考）
├── dump/                       libtool dump 出的 il2cpp 脚本
│   └── com.fizzd.connectedworlds_3.3.1.cs
├── loader/                     本工具源码与产物
│   ├── build.sh                构建脚本
│   ├── src/                    源码
│   ├── tests/                  宿主机单元测试（hook 引擎/输入状态机）
│   ├── ReverseDocumentation.md 完整逆向分析文档（加载链路/输入链路/hook 设计）
│   ├── RemoveVerification.md   谷歌校验去除方式
│   └── out/libadofailoader.so  最终产物（放入 APK lib/arm64-v8a/）
└── UpdateLog.md                完整开发与调试试错记录（14 轮 + 35 个测试版本）

```


## 快速开始

1. 进入 `loader` 目录执行 `./build.sh`，生成 `out/libadofailoader.so`（正式版 v1.0）
2. 按 `install方法以及示例/install.txt` 中的步骤安装：
   - 将 `libadofailoader.so` 放入 APK 的 `lib/arm64-v8a/` 目录
   - 在 dex 中植入 `System.loadLibrary("adofailoader")`
   - 重打包、签名、安装
3. 启动游戏，左上角可见「加载关卡」按钮（可拖动），点击后弹出文件选择器，选择 `.adofai` 谱面文件即可加载游玩（谱面 + 音乐 + 背景图齐全）。


## 功能一览（正式版 v1.0）

- **文件选择器**：支持路径跳转（上级目录 / 游戏数据目录 / sdcard 根目录）、触摸滑动、滚动条、▲▼按钮，并仅显示关卡文件（过滤非 `.adofai` 后缀）
- **悬浮按钮**：点击打开文件选择器，按住可拖动，位置自动记忆
- **关卡加载**：完整调用 `LoadCustomLevel` 管线，谱面、音乐、背景图全部正常加载
- **游戏暂停控制**：在游戏中打开加载器自动暂停，关闭后自动恢复
- **双主题**：暗色 / 亮色（搭配双缩脲紫强调色）
- **设置页**：支持语言切换（中文 / English）
- **稳定性修复**：修复了移动版多个 PC 遗留崩溃问题（内置关卡分支、字典空键、il2cpp 栈指针传参 codegen bug、字符串分配异常等）


## 核心结论

经逆向分析确认（详见 `ReverseDocumentation.md`）：

- 移动版场景包含 `scnGame`（自定义关卡游玩场景），但不包含 `scnCLS` 和 `scnEditor`
- 加载链路：设置 `GCS.customLevelIndex = 0`、`internalLevelName = null`、`customLevelId = null`、`sceneToLoad = "scnGame"` → 调用 `scrController.LoadCustomLevel(path, null, false)` → 游戏黑屏转场加载 `scnGame` → `Awake` 后第 3 帧自动执行 `LoadAndPlayLevel(path)` → `LevelData.LoadLevel` 直接通过 `System.IO` 读取文件并解析 JSON，完成加载
- 输入系统：全部经过 `UnityEngine.Input`（游戏 → Rewired → Input），通过 hook `get_touchCount`、`GetTouch`、`get_touches`、`GetMouseButton*`、`get_mousePosition` 即可同时完成触摸捕获与屏蔽
- 渲染注入：hook `libEGL.eglSwapBuffers`，在 Unity 提交画面前绘制 ImGui（GLES3 环境）



## 浅见补充说明

### 场景加载控制

游戏默认包含三个主要场景：`scnGame`（关卡游玩）、`scnCLS`（关卡选择）和 `scnEditor`（关卡编辑器）。本项目仅针对 `scnGame` 场景进行操作。

加载时强制设定关卡索引为 0，并清空关卡名称与 ID 等参数，直接触发场景切换。进入 `scnGame` 后，在场景启动的第 3 帧自动执行关卡加载逻辑，该过程不走 Unity 内置的 AssetBundle 或 Resources 加载机制，而是直接通过文件流读取 ADOFAI 的关卡数据并完成解析，从而实现进入关卡。

### 输入事件拦截

游戏内所有触摸、键盘及鼠标操作均通过 Unity 引擎的 `Input` 类进行统一管理。这里进行了拦截。

### 渲染层界面注入

游戏画面渲染提交至屏幕前，最终会调用 `eglSwapBuffers` 函数。本项目在该函数执行前插入 ImGui 的渲染代码，在游戏画面之上叠加绘制自定义界面，可用于显示调试信息、操作菜单或状态面板。


## 技术依赖

- 目标游戏基于 Unity 引擎开发
- 渲染管线使用 OpenGL ES 3.0
- 注入技术依赖 Hook 库（如 Detours、MinHook 或 MonoMod）
- 界面绘制依赖 ImGui 框架


## 大概流程

1. 将 Hook 库注入目标游戏进程
2. 触发加载逻辑，自动跳转至 `scnGame` 场景
3. 场景启动后自动加载指定关卡文件
4. 通过叠加的 ImGui 界面进行操作与控制


## 注意事项

- 加载的关卡文件需符合游戏自身的 ADOFAI 数据格式，后缀需为 `.adofai`
