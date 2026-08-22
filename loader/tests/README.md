# 宿主机单元测试（aarch64 Linux 上直接运行）

验证内联 hook 引擎、sret 调用约定和输入状态机（无需安卓设备）：

```bash
mkdir -p /tmp/tt/android
cp android_log_stub.h /tmp/tt/android/log.h   # 见本目录
g++ -O2 -o /tmp/tt/t1 test.cpp   hooks.cpp hooks_asm.S util.cpp -I/tmp/tt -I. -I../src
g++ -O2 -o /tmp/tt/t2 test2.cpp  hooks.cpp hooks_asm.S util.cpp -I/tmp/tt -I. -I../src
g++ -O2 -o /tmp/tt/t3 test3.cpp  hooks.cpp hooks_asm.S util.cpp -I/tmp/tt -I. -I../src -DINPUT_TEST
/tmp/tt/t1 && /tmp/tt/t2 && /tmp/tt/t3
```

android_log_stub.h 为宿主机提供的 __android_log_vprint 桩。
