/*

xxc: Here are my simple notes:
 
This is an auto‑update check module for the game. It is a so‑file that reads the current version, connects to the server to check for new versions, and pops an update reminder window when updates are available.
 
It uses the Dobby Hook framework inside libAPKVISION. Most likely it is used for code protection or packing. Its main functions are version checking and the update pop‑up.
 
These are just my guesses. I inferred them from names seen in UN Manager. It may or may not cause compatibility problems. Please refer to comments from repository contributors for real information.

*/


/*

这个只是我想说的简单的几句话：
    这是一个游戏的自动更新检查模块，负责获取当前版本、联网查询服务器是否有新版本，并在有新版本时弹出更新提示窗口的so文件。

    虽然它（ilbAPKVISION）内部用了Dobby Hook框架，但那大概率是为了做代码保护或者专门为了加一个壳，反正主要的功能就是版本检测与更新弹窗。

    当然以上都是我的推测，我是在UN管理器里面分析命名推测的，也不一定影响或不兼容，但有可能占了openGL的上下文，具体请看仓库贡献者怎么说

*/


