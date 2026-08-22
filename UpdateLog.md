# 更新日志（CHANGELOG）

冰与火之舞 移动版铺面加载器（adofai-mobilelevelloder）
适用游戏：com.fizzd.connectedworlds v3.3.1（Unity 6000.3.10f1, arm64 il2cpp）

---

## v1.1.5（2026-08-22）

### 问题修复
- **暂停菜单退出崩溃**：游戏 PC 遗留退出路径把 `&局部字符串`（栈指针）传给
  引擎字符串函数导致崩溃（字符串比较/场景名污染同族问题）。最终方案：
  **放弃修补游戏退出路径**，改为工具内自建「返回主页面」按钮（设置页，
  红色按钮）——走我们自己的干净退出：清空 customLevelPaths/internalLevelName
  后直接 `SceneManager.LoadScene("scnMobileMenu")`，完全绕开游戏的 bug 路径。
  同时移除之前尝试的 scnCLS 重定向 hook 与字符串比较 hook。
- **开关微调**：缩小至 36×18×scale，标签与开关垂直居中对齐。
- **不败模式启动默认关闭**：设置记忆但不再自动应用，每次启动强制 false。

### 更改
- **移除工具内难度选择器**：难度已 ShowAll（右下角指示器全显示），
  工具里的难度选择失去意义（底层 GCS.difficulty 接口保留备用）。

### 版本
- 工具内版本号同步至 **v1.1.5**（JNI_OnLoad 日志 + 设置页）。

---

## v1.1.4（2026-08-22）

### 问题修复
- **开关大小修改**：手机设置风格开关缩小（42×22×scale，原 56×30）。

### 新增
- **显示自定义关卡难度指示器**：hook `DetermineDifficultyUIMode(highestBPM)`，
  自定义关卡挂起时强制返回 **ShowAll** → 游戏右下角出现难度指示器
  （移动版默认跳过的 PC 行为）。
- **暂停菜单设置按钮修复**：hook `SceneManager.LoadScene(string)` 与
  `LoadScene(string, LoadSceneMode)`——任何尝试加载 `scnCLS`（PC 选关场景，
  移动版不含）的调用自动改道 `scnMobileMenu`（移动端主页），
  修复点击设置按钮报错并卡黑屏加载的问题。

---

## v1.1.3（2026-08-22）

### 修改
- **手机设置风格开关**：自绘滑动胶囊开关控件（ToggleSwitch），替换原来的
  Selectable 开关：
  - 开启：双缩脲紫轨道（#8C5CB5）+ 白色滑块；关闭：深色主题=深灰 /
    亮色主题=浅灰轨道；悬停时轨道提亮
  - 应用于「不败模式 (No Fail)」与「仅显示关卡文件」两个开关
  - 尺寸随屏幕缩放（56×30×scale），触摸友好；状态持久化与游戏侧应用逻辑不变
- **libTool 共存**：明确搁置。GL 上下文隔离已实现并保留（只在 Unity 主上下文
  渲染，检测到 libTool 输出 coexistence 日志），但实测双装仍会闪退，
  现版本不再追求共存（待后续提供崩溃日志再排查）。

---

## v1.1.2（2026-08-22）

### 问题修复
- **官方关卡歌曲**：移除 ReloadSongCo 内三个无条件补丁（强制自定义分支/异常出口/回边）——
  它们会把官方关卡的内置歌曲路径也改坏。自定义关卡改由条件性
  get_isInternalLevel hook 处理，官方关卡路径完全不动。
- **谷歌校验**：配合 RemoveVerification.md（PairIP 许可证 SDK 两处 smali 修改）。
- **APKVISION 站打包版**：其注入的 libAPKVISION.so（反篡改库）与本工具不兼容，
  安装时删除该 lib（install.txt 已注明）。
- **尝试与原 libTool 共存闪退**：eglSwapBuffers hook 增加 **GL 上下文隔离**—— 但是失败
  只在 Unity 主上下文渲染，libTool 的 SurfaceView 上下文直接放行；
  检测到 libTool 时日志标记 coexistence mode。
- **adofai 过滤框选修复**：从文件页移到「▣ 设置」页，改为 Selectable 开关样式。

### 新增
- **悬浮球贴边隐藏**：拖动靠近屏幕左/右边缘松手 → 自动像悬浮球一样隐藏
  （露出 1/4），点击露出部分可再拖出（宿主机测试已覆盖）。
- **难度选择**：GCS.difficulty（宽 Lenient / 普通 Normal / 严 Strict），
  设置页选择，主线程应用，持久化。（注：右下角不会显示）
- **不败模式开关**：GCS.useNoFail + scrController.noFail 同步写入（游戏中
  bool），设置页开关，持久化。
- **文件日志**：日志输出到 files/log/adofailoader.log（追加），
  与 logcat 双写；分级 [I]/[W]/[E]（LOGI/LOGW/LOGE）。
- **UI 开关**：设置页新增「游戏选项」与「文件选择器」分组
  （不败模式、难度、仅显示关卡文件）。

---

## v1.0 正式版（2026-08-21）

### 功能
- 悬浮按钮「加载关卡」：点击打开加载器、按住拖动位置；游戏中打开自动暂停、关闭自动恢复
- 文件选择器：默认目录为游戏数据目录；路径跳转（上一级 / 游戏数据目录 / /sdcard / 根目录）；
  触摸滑动 + 滚动条 + ▲▼ 按钮；「仅显示关卡文件」过滤
- 关卡加载：完整加载 .adofai 铺面（轨道 + 音乐 + 背景图）
- 设置页：语言（中文 / English）、主题（暗色 / 亮色，双缩脲紫强调色），持久化到 files/adofai_loader.ini
- 窗口可拖动、字体按屏幕自动缩放

### 相对测试版的清理
- 移除全部诊断日志与纯诊断钩子（异常探针、字典探针、限频计数器等）
- 保留功能性修复与安全保险丝（字符串长度钳制、路径替换、GC 锚点）
- 版本号从 v2.x 重置为 v1.0

---

## 版本历史（逐版本试错记录）

### v1.0（开发初版）
hook 引擎（短补丁 + 就近页）、EGL 钩子 + ImGui GLES3 注入、文件选择器、
关卡加载管线（GCS 静态字段 + LoadCustomLevel）、悬浮按钮、暂停/恢复、主题框架。
- 首测即崩：dlopen 找不到 `__assert_fail`（musl libc++abi.a 引用的 glibc 符号，bionic 无）
  → shims.cpp 本地定义；库更名 libTool.so → **libadofailoader.so**

### v1.1
- il2cpp 导出符号全部"缺失"：System.loadLibrary 以 RTLD_LOCAL 加载 libil2cpp，
  dlsym(RTLD_DEFAULT) 不可见 → dlopen(RTLD_NOLOAD) 句柄解析 + RTLD_DEFAULT + RVA 表三级兜底
- bootstrap 过早调用 il2cpp（thread_attach）崩溃 → 全部 il2cpp 调用改为**懒初始化**
  （主线程首次输入轮询时执行）

### v1.2
- hook 目标地址错误（`0xffffffffe...` 假地址）→ 根因：find_module_base 64 位基址截断进
  32 位 int；修复：uint64_t 全程传递 → 按钮终于可点
- 悬浮按钮支持拖动（12px 阈值区分点击/拖动）、屏幕边缘钳制
- 去掉「♪」符号；主色改柔和暖沙色

### v1.3
- 打开文件选择器即崩：il2cpp 的 Vector3（12 字节 HFA）走 **s0/s1/s2 浮点寄存器返回**，
  不是 sret → hook 改为返回 Vec3 结构体（clang ABI 与 il2cpp 逐位一致）

### v1.4
- value_box 崩溃（GC 写屏障垃圾指针）→ LoadCustomLevel 改为**函数指针直调**
  （签名反汇编验证：this, string, string, bool-in-w3）
- 字体加大（fb_h/36）；主窗口可拖动；新增亮色主题；强调色改**双缩脲紫**

### v1.5
- 自定义路径走进"内置关卡"分支 → Split 崩溃：get_isInternalLevel()==(internalLevelName!=null)，
  游戏管线内部反复回写该字段 → hook 三个加载入口强制清空 + 路径兜底
- 新增设置页（语言 + 主题）

### v1.6
- 存档系统 null 崩溃（GDMiniJSON 序列化 null）→ internalLevelName = 关卡路径（非 null）
- 配套 hook `get_isInternalLevel`：customLevelPaths 非空时强制返回 false（中和所有内置分支）
- ArgumentNullException("key")：WorldData.dict[null]（PC 遗留）→ 见 v2.0

### v1.7
- 限频调用者日志（每秒 1 条 + 调用地址），用于定位循环

### v1.8
- 歌曲协程死循环（误判见 v2.13）→ 当时方案：currentSongKey 补齐（len=32）
  + ReloadSongCo 回边补丁 + MoveNext 节流阀

### v1.9
- 字典共享泛型探针（get_Item/Add null-key 检测），定位到 get_isDLCLevel 抛异常

### v2.0
- hook get_isDLCLevel / get_isBossLevel / get_isCLSBossLevel：
  自定义关卡挂起时直接返回 false（自定义关卡非 DLC/Boss，语义正确）

### v2.1
- 循环回边补丁（0x252d41c `b 0x252cf14` → 出口）+ MoveNext 节流阀

### v2.2
- MoveNext 入口速率诊断 → 发现"MoveNext 从未被调用但函数中段在跑"的矛盾

### v2.3
- 状态机外科手术补丁：入口分派→return、get_isInternalLevel 调用点→恒 false、
  异常抛出点→干净返回

### v2.4
- 异常出口补丁 + currentSongKey 手动写入加 GC 锚点

### v2.5
- 诊断探针三件套：raise 助手、string_new_size、GC 分配器
  → 抓到 `GC alloc(0xffffffffced818ae) -> NULL`（垃圾尺寸）

### v2.6
- 字符串长度保险丝：>1MB 钳制 → **误伤**（游戏启动时合法的 105 万字符大字符串被钳坏）

### v2.7
- 保险丝修正：只钳制负长度（真正的垃圾长度是负数）

### v2.8
- 跳过 ReloadSong/ReloadCustomSounds（静音加载过渡版）

### v2.9
- Concat4 探针 → caller 定位到 TextureManager.LoadTexture

### v2.10
- Concat 日志带模块基址（跨会话可换算 caller RVA）

### v2.11
- LoadTexture/LoadNewSprite 路径校验守卫（坏路径跳过）

### v2.12
- **发现 il2cpp codegen bug**：Concat4 收到**栈地址**（&局部字符串变量而非字符串本身）
  → 栈形参数修复（先替换为空串）

### v2.13
- **音乐恢复**：拆除 v2.1-2.8 的过激补丁（x8560"循环"实为 Play 逐地板正常循环；
  "OOM"实为 codegen bug 传栈指针所致）→ 音乐正常

### v2.14
- 栈形参数改为**解引用**取出真字符串（堆范围 + 长度校验，失败换空串）

### v2.15
- 修复覆盖全部 Concat 重载（2/3/4 参数版）

### v2.16
- LoadTexture 加载路径内容日志 + UpdateBackgroundSprites 调用日志

### v2.17
- LoadTexture 返回值/状态日志 → `result=0x0 status=6`（MissingFile）

### v2.18
- Split(char) 同族栈参数 bug 修复

### v2.19
- RDFile.Exists/ReadAllBytes 路径替换窗口（LoadTexture 执行期间换回正确路径）

### v2.20
- 替换触发日志 → 发现 Exists 钩子**从未被调用**（流程根本没走到文件读取）

### v2.21
- NOP "Exists 失败"分支 → 无效（流程仍不经过）

### v2.22
- raise 探针无条件日志 → 无异常抛出（排除异常路径）

### v2.23
- 跳过"精灵定义查找"分支（0x229f0a8 → 文件路径）
  → 背景能加载但地板精灵注册表被跳过 → OverflowException 铺面失败 → **回滚**

### v2.24
- 新方案：LoadTexture/LoadNewSprite 执行期间**临时置空** internalLevelName
  （仅对 '/' 开头的真实文件路径），返回后恢复——游戏走 PC 自定义关卡的纯文件路径

### v2.25
- 窗口验证日志 → `verify=0x72942c1b38`（**置空根本没生效**！）

### v2.26 ✅
- **发现 GC 写屏障 NULL 语义坑**：il2cpp_field_static_set_value 对 NULL 的处理错误，
  把 &局部变量（而非 null）写进字段（也解释了 v1.5 时代"清了却没用"的旧谜团）
- 修复：NULL 写入无屏障需求 → **原生直写静态槽**
  （Il2CppClass.static_fields@0xB8 + 字段偏移 0x80），恢复时原生写回 + GC 锚点
- **结果**：`LoadTexture result=0x... status=0` → 铺面 + 音乐 + 背景全部正常 ✅

### v1.0 正式版
- 清理全部诊断代码；版本重置；文档整理（install.txt / README.md / 更新日志.md）

---
## 轮次

### 第 1 轮：dlopen 符号缺失 → 秒崩
- **现象**：注入即崩，`UnsatisfiedLinkError: cannot locate symbol "__assert_fail"`
- **根因**：静态链接的 musl 版 libc++abi.a 引用 glibc 专有符号 `__assert_fail`，bionic libc 无此符号
- **修复**：shims.cpp 本地定义 `__assert_fail`（日志 + abort）
- **附带**：库更名 libTool.so → **libadofailoader.so**（dex 植入 "adofailoader"）

### 第 2 轮：il2cpp 导出符号全部"缺失"
- **现象**：`il2cpp: missing symbol il2cpp_domain_get ...` 全部失败
- **根因**：Unity 用 System.loadLibrary（RTLD_LOCAL）加载 libil2cpp，符号不在全局作用域，
  bionic 的 dlsym(RTLD_DEFAULT) 找不到
- **修复**：dlopen(RTLD_NOLOAD) 取库句柄 → dlsym(句柄)；兜底 1：RTLD_DEFAULT；
  兜底 2：硬编码导出表 RVA + 基址

### 第 3 轮：bootstrap 过早调用 il2cpp → 空指针崩溃
- **现象**：`API resolved` 后立即 SIGSEGV（fault addr 0x135，il2cpp Thread::Attach 内部）
- **根因**：il2cpp 运行时尚未初始化就调 thread_attach / domain 查询
- **修复**：引导线程只做 dlsym + 装 hook；所有 il2cpp 调用改为**懒初始化**
  （主线程首次输入轮询时执行——托管代码能跑 = 运行时必然就绪）

### 第 4 轮：hook 目标地址错误 → 按钮点不动
- **现象**：`hook: no near page for 0xffffffffe8c7ad44`（假地址）
- **根因**：find_module_base 把 64 位基址截断存入 32 位 int，符号扩展出假地址
  （该设备 libil2cpp 基址第 31 位恰为 1）
- **修复**：全程 uint64_t 传递

### 第 5 轮：get_mousePosition 返回约定错误 → 开窗即崩
- **现象**：打开文件选择器 2 秒内崩溃（hk_get_mousePosition_c+60，fault 0x8）
- **根因**：il2cpp 的 Vector3（12 字节 HFA）按 AAPCS64 走 **s0/s1/s2 浮点寄存器返回**，
  不是 sret（x8）——GetTouch（68 字节）才走 sret
- **修复**：hook 改为返回 Vec3 结构体，clang 编译产物与 il2cpp 约定逐位一致

### 第 6 轮：il2cpp_value_box 崩溃
- **现象**：game_load_level+276（value_box 调用点）→ GC 写屏障垃圾指针
- **根因**：此 ROM 的 il2cpp 装箱路径不可靠
- **修复**：LoadCustomLevel 改为**函数指针直调**（签名自反汇编验证：this, string, string, bool-in-w3）

### 第 7 轮：关卡路径走进"内置关卡"分支 → Split 崩溃
- **现象**：加载后 2 秒崩于 string.Split（长度 8626 的假字符串扫描越界）
- **根因**：`get_isInternalLevel() == (GCS.internalLevelName != null)`；
  游戏加载管线内部反复回写该字段 → 自定义路径进入 PC 内置分支
- **修复**：hook 三个加载入口（LoadAndPlayLevel / scnGame.LoadLevel / LevelData.LoadLevel）
  强制清空 + 路径兜底替换

### 第 8 轮：ArgumentNullException("key")
- **现象**：`WorldData.dict[scrController.currentWorldString]`，移动端该字段为 null
- **根因**：get_isDLCLevel / get_isBossLevel / get_isCLSBossLevel 的 PC 遗留字典查询
- **修复**：hook 三个 getter，自定义关卡挂起时直接返回 false（语义正确：自定义关卡非 DLC/Boss）

### 第 9 轮："x8560 循环"谜团（含误判与纠正）
- **现象**：get_isInternalLevel 每秒被调 8560 次，随后 OOM 式崩溃
- **误判**：曾认为是 ReloadSongCo 协程死循环（加了循环回边补丁、MoveNext 节流阀、
  状态机"外科手术"补丁、甚至跳过整个歌曲加载）
- **真相**：① x8560 = `scnGame.Play` 逐地板应用事件的**正常循环**（850 地板 × ~10 次）；
  ② "OOM" = 垃圾尺寸其实是 **il2cpp codegen bug**（见第 11 轮）传栈指针导致的
- **纠正**：v2.13 拆除了所有过激补丁，恢复歌曲加载（音乐正常）

### 第 10 轮：存档系统 null 值崩溃
- **现象**：PlayerPrefsJson.Save → GDMiniJSON 序列化 null → GC 屏障崩溃
- **根因**：internalLevelName = null 被游戏存进存档
- **修复**：internalLevelName = 关卡路径（非 null），配套 get_isInternalLevel 强制 false
  中和所有内置分支

### 第 11 轮：il2cpp codegen bug——"栈指针当字符串传参"
- **现象**：Concat4 收到栈地址（0x7292... 与传入路径相同的栈指针）→ 读长度垃圾 → 崩溃
- **根因**：TextureManager.LoadTexture 内部 `Concat(&局部字符串变量, ...)`——
  传了"字符串变量的地址"而非字符串本身（此 il2cpp 构建的代码生成缺陷）
- **修复**：hook Concat 2/3/4 参数版 + Split，栈形参数**解引用**取出真字符串
  （堆范围校验 + 长度合法性校验，失败则替换为空串）

### 第 12 轮：背景图 MissingFile 之谜
- **现象**：LoadTexture 收到正确路径，返回 status=6（MissingFile），且从不调用文件读取
- **根因**：LoadTexture 内部有 PC 专用"精灵定义反序列化"路径：
  `Concat(静态串, internalLevelName.Split('-')[0], ...)` → JSON 反序列化 → 查找失败即返回
- **试错**：① 路径替换窗口（Exists/ReadAllBytes 替换成正确路径）→ 无效（流程根本没走到）；
  ② NOP "Exists 失败"分支 → 无效；③ **跳过查找分支** → 背景能加载但地板精灵注册表被跳
  → OverflowException 铺面加载失败（**此路不通，回滚**）

### 第 13 轮：GC 写屏障的 NULL 语义坑（最终钥匙）
- **现象**：临时置空 internalLevelName 后回读 verify≠null，字段里是栈地址
- **根因**：`il2cpp_field_static_set_value` 的写屏障对 NULL 处理错误——
  把 **&局部变量**（而非 null）写进字段（也解释了第 7 轮"清了却没用"的旧谜团）
- **修复**：NULL 写入不需要 GC 屏障 → **原生直写静态槽**
  （Il2CppClass.static_fields@0xB8 + 字段偏移 0x80），恢复时原生写回 + GC 锚点

### 第 14 轮：背景图加载成功 ✅
- **方案**：LoadTexture/LoadNewSprite 执行期间**临时置空** internalLevelName
  （仅对 '/' 开头的真实文件路径；地板等内部资源路径不受影响），
  返回后恢复——游戏走 PC 自定义关卡的纯文件路径
- **结果**：`LoadTexture result=0x... status=0`，铺面 + 音乐 + 背景全部正常

---

## 附带 UI 迭代记录
- 字体：fb_h/60 → fb_h/42 → fb_h/36（最终 1080p 约 30px，可随屏缩放）
- 悬浮按钮：点击打开 → 增加拖拽（12px 阈值区分点击/拖动）、屏幕边缘钳制
- 文件列表：拖动滚动 → 恢复滚动条 + 无阈值即时拖动 + ▲▼ 按钮
- 主题：暖沙色 → **双缩脲紫**（暗色全面紫色化）；新增亮色主题；修复主题持久化时机
- 新增设置页（文件/设置双页签）：语言 + 主题
- 主窗口支持标题栏拖动

## 关键知识沉淀（试错中最有价值的发现）

1. **bionic 无 `__assert_fail`**：musl 静态库交叉链接 Android 的经典坑
2. **RTLD_LOCAL 符号隔离**：System.loadLibrary 的库必须用句柄 dlsym
3. **il2cpp 小结构体返回约定**：HFA（≤16 字节同质浮点聚合）走 s0-s3，大结构体走 sret(x8)
4. **il2cpp 运行时初始化时序**：托管代码能执行 = 运行时就绪（懒初始化的安全性依据）
5. **il2cpp 泛型共享**：字典等共享代码被 Il2CppDumper 误标注（如"WeightedEase"）
6. **GC 写屏障 NULL 语义**：field_static_set_value(NULL) 会写入 &局部变量——置空必须原生直写
7. **codegen bug（栈指针传参）**：Concat/Split 收到栈地址——检测方法：参数值落在
   SP±8MB 内 + 解引用后堆范围校验
8. **误判教训**：性能型刷屏未必是死循环（逐地板循环）；垃圾值未必是 OOM（可能是栈指针）
9. **PC 遗留代码三处**：内置关卡分支、WorldData 字典空键、LoadTexture 精灵定义反序列化

## 遗留说明
- 背景图依赖关卡目录内同名文件（.adofai 与歌曲/图片必须同目录）
- 全部 RVA 基于游戏 v3.3.1；游戏更新需重新核对地址
