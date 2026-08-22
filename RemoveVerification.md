# 修改方法（MT管理器操作步骤）

用 MT管理器打开你的 APK → 点 `classes.dex` → 选择 **DEX 编辑器++** → 搜索类名修改。改完保存、重打包、重新签名。

## 修改 1（主入口）：`LicenseClient.checkLicense` 直接返回

- 类名：`com.pairip.licensecheck.LicenseClient`
- 方法：`public static void checkLicense(Context)`

**原代码**（内容大致是）：
```smali
.method public static checkLicense(Landroid/content/Context;)V
    # ...判断 isolated 进程，否则 new LicenseClient(context).initializeLicenseCheck()
    return-void
.end method
```

**改成**：
```smali
.method public static checkLicense(Landroid/content/Context;)V
    .locals 0
    return-void
.end method
```

即把方法体全部删掉，只留 `return-void`（注意 `.locals` 改成 `0`）。

## 修改 2（第二入口）：`LicenseContentProvider.onCreate` 去掉校验

- 类名：`com.pairip.licensecheck.LicenseContentProvider`
- 方法：`public boolean onCreate()`

**原代码**（大致是）：
```smali
.method public onCreate()Z
    # new LicenseClient(getContext()).initializeLicenseCheck()
    const/4 v0, 0x1
    return v0
.end method
```

**改成**：
```smali
.method public onCreate()Z
    .locals 1
    const/4 v0, 0x1
    return v0
.end method
```

即删掉 `new LicenseClient(...)...initializeLicenseCheck()` 那两行，只保留返回 `true`。

---

## 原理说明（为什么这样改）

校验链路：

```
Application.attachBaseContext
    └─→ LicenseClient.checkLicense()        ← 改这里 = 整条链路断掉
           └─→ initializeLicenseCheck()
                  └─→ 绑定 Google Play 许可服务 (com.android.vending)
                         └─→ 验证失败 → handleError()
                                └─→ LicenseActivity 弹错误框
                                       └─→ exitAction = System.exit(0)   ← "被顶掉"的真凶

LicenseContentProvider.onCreate()           ← 第二个独立触发点，同样要断
    └─→ initializeLicenseCheck()（同一套流程）
```

- 改 1 后：Application 启动时不再发起校验，**永远不会**走到 System.exit(0)；
- 改 2 后：ContentProvider 这个旁路也被堵死（否则系统实例化 Provider 时会独立触发一次校验）；
- 已验证：il2cpp（native）侧没有任何 pairip 引用，**没有 VM 完整性保护**，纯 Java 校验，改 dex 不会触发自毁；
- `exitAction`（System.exit(0)）、重复校验（repeatedCheck）、签名校验（validateResponse）都在这两个入口之下，入口断了它们全部不会执行。

改完重签名安装即可，谷歌校验不会再弹