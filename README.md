# adofai-mobilelevelloder — 冰与火之舞 移动版铺面加载器

向《冰与火之舞》(A Dance of Fire and Ice) 安卓移动版（`com.fizzd.connectedworlds`，
v3.3.1，Unity 6000.3.10f1 + il2cpp）注入一个 ImGui 文件选择器，
用于加载自定义铺面（`.adofai`）——官方移动版未提供该入口，但游戏的
自定义关卡游玩场景 `scnGame` 及其完整加载管线（PC 遗留代码）都随包保留，
本工具把这条链路完整接了起来。

## 目录结构

```
adofai-mobilelevelloder/
├── gameassest/                 游戏包全部资源文件，现只保留重要文件
├── gamelibs/                   游戏全部 lib（libil2cpp.so 等，arm64）
├── imgui-master/               Dear ImGui 源码
├── levelexample/               自定义关卡示例（lightItUp.adofai + 歌曲/背景）
├── install方法以及示例/        安装说明（dex 植入 loadLibrary 方式）
│   ├── install.txt               <--这个就是安装方式
│   └── libTool.so              （旧示例工具，仅参考）
├── dump/                       libtool dump 出的 il2cpp 脚本
│   └── com.fizzd.connectedworlds_3.3.1.cs
├── loader/                     ★ 本工具源码与产物
│   ├── build.sh                构建脚本
│   ├── src/                    源码
│   ├── tests/                  宿主机单元测试（hook 引擎/输入状态机）
│   ├── ReverseDocumentation.md               完整逆向分析文档（加载链路/输入链路/hook 设计）
│   ├──RemoveVerification.md         谷歌校验去除方式
│   └── out/libadofailoader.so ★ 最终产物（放入 APK lib/arm64-v8a/）
└── UpdateLog.md                 ★ 完整开发与调试试错记录（14 轮+35个测试版本）
```

## 快速开始

1. `cd loader && ./build.sh` → `out/libadofailoader.so`（正式版 v1.0）
2. 按 `install方法以及示例/install.txt` 安装：
   - APK 的 `lib/arm64-v8a/` 放入 `libadofailoader.so`
   - dex 植入 `System.loadLibrary("adofailoader")`
   - 重打包签名安装
3. 游戏内左上角「加载关卡」按钮（可拖动）→ 文件选择器 → 选择 `.adofai` → 加载游玩（铺面+音乐+背景齐全）。

## 功能一览（正式版 v1.0）

- 文件选择器：路径跳转（上级/游戏数据目录//sdcard/根目录）、触摸滑动、
  滚动条、▲▼按钮、仅显示关卡文件过滤
- 悬浮按钮：点击打开、按住拖动、位置记忆
- 关卡加载：LoadCustomLevel 管线（铺面/音乐/背景图完整）
- 游戏中打开加载器自动暂停，关闭自动恢复
- 双主题（暗色/亮色，双缩脲紫强调色）+ 设置页（语言 中文/English）
- 修复了移动版多个 PC 遗留崩溃（内置关卡分支、字典空键、
  il2cpp 栈指针传参 codegen bug、字符串分配异常等）

## 核心结论（详见 ReverseDocumentation.md 逆向文档）

- 移动版场景包含 **scnGame**（自定义关卡游玩场景），但不含 scnCLS/scnEditor。
- 加载一条链路：设置 `GCS.customLevelIndex=0 / internalLevelName=null /
  customLevelId=null / sceneToLoad="scnGame"` → 调用
  `scrController.LoadCustomLevel(path, null, false)` → 游戏黑屏转场加载
  scnGame → Awake 后第 3 帧自动 `LoadAndPlayLevel(path)` →
  `LevelData.LoadLevel` 直接 System.IO 读文件 + JSON 解析完成加载。
- 输入全部经过 `UnityEngine.Input`（游戏→Rewired→Input），hook
  `get_touchCount/GetTouch/get_touches/GetMouseButton*/get_mousePosition`
  即可同时完成触摸捕获与屏蔽。
- 渲染 hook `libEGL.eglSwapBuffers`，在 Unity 提交前绘制 ImGui（GLES3）。

