#!/bin/bash
# Build libadofailoader.so for ADoFAI mobile (冰与火之舞 移动版铺面加载器)
# Host: aarch64 Linux with apt clang + NDK r27d sysroot (the NDK prebuilt
# toolchain binaries are x86_64, so we drive the host clang with the NDK
# sysroot instead — clang is a cross-compiler by nature).

#!/bin/bash
#!/bin/bash
set -e

NDK=/workspace/ndk/android-ndk-r27d
CLANG_PREBUILT=$NDK/toolchains/llvm/prebuilt/linux-x86_64
SYSROOT=$CLANG_PREBUILT/sysroot
SYSLIB=$SYSROOT/usr/lib/aarch64-linux-android/21
CLANG_LIB=$CLANG_PREBUILT/lib/clang/18/lib

ROOT="$(cd "$(dirname "$0")" && pwd)"
SRC=$ROOT/src
IMGUI=$ROOT/../imgui-master
OUT=$ROOT/out
mkdir -p $OUT

TARGET=aarch64-linux-android21

mkdir -p $ROOT/link
ln -sf $CLANG_LIB/linux/libclang_rt.builtins-aarch64-android.a $ROOT/link/libgcc.a
ln -sf $CLANG_LIB/linux/aarch64/libunwind.a $ROOT/link/libunwind.a

COMMON="-target $TARGET -std=c++17 -O2 -fPIC -fvisibility=hidden -fno-rtti \
 -fno-strict-aliasing -ffunction-sections -fdata-sections \
 -DANDROID -DIMGUI_IMPL_OPENGL_ES3 \
 -I$SRC -I$SRC/font -I$IMGUI -I$IMGUI/backends \
 -I$CLANG_PREBUILT/sysroot/usr/include \
 -I$CLANG_PREBUILT/sysroot/usr/include/aarch64-linux-android \
 -I$NDK/toolchains/llvm/prebuilt/linux-x86_64/sysroot/usr/include \
 -B$CLANG_PREBUILT/bin \
 --sysroot=$CLANG_PREBUILT/sysroot"

echo "=== Build Debug Info ==="
echo "NDK: $NDK"
echo "CLANG_PREBUILT: $CLANG_PREBUILT"
echo "Checking sysroot:"
ls -la $CLANG_PREBUILT/sysroot/usr/include/ 2>/dev/null | head -5 || echo "Not found"
echo "=== End Debug Info ==="

clang++ $COMMON -c -o $OUT/main.o $SRC/main.cpp
clang++ $COMMON -c -o $OUT/util.o $SRC/util.cpp
clang++ $COMMON -c -o $OUT/hooks.o $SRC/hooks.cpp
clang++ $COMMON -c -o $OUT/il2cpp.o $SRC/il2cpp.cpp
clang++ $COMMON -c -o $OUT/game.o $SRC/game.cpp
clang++ $COMMON -c -o $OUT/input.o $SRC/input.cpp
clang++ $COMMON -c -o $OUT/render.o $SRC/render.cpp
clang++ $COMMON -c -o $OUT/overlay.o $SRC/overlay.cpp
clang++ $COMMON -c -o $OUT/shims.o $SRC/shims.cpp
clang++ $COMMON -c -o $OUT/asm.o $SRC/hooks_asm.S

clang++ $COMMON -c -o $OUT/imgui.o $IMGUI/imgui.cpp
clang++ $COMMON -c -o $OUT/imgui_draw.o $IMGUI/imgui_draw.cpp
clang++ $COMMON -c -o $OUT/imgui_tables.o $IMGUI/imgui_tables.cpp
clang++ $COMMON -c -o $OUT/imgui_widgets.o $IMGUI/imgui_widgets.cpp
clang++ $COMMON -c -o $OUT/imgui_gl3.o $IMGUI/backends/imgui_impl_opengl3.cpp

cat > $OUT/version.script <<'EOF'
{
  global: JNI_OnLoad;
  local: *;
};
EOF

clang++ $COMMON -shared -o $OUT/libadofailoader.so \
  -nostdlib++ -fuse-ld=lld \
  -L$ROOT/link \
  -Wl,--exclude-libs,ALL \
  -Wl,--version-script=$OUT/version.script \
  -Wl,--soname=libadofailoader.so \
  -Wl,-z,max-page-size=16384 \
  -Wl,--build-id=sha1 \
  -Wl,--gc-sections \
  $OUT/main.o $OUT/util.o $OUT/hooks.o $OUT/il2cpp.o $OUT/game.o \
  $OUT/input.o $OUT/render.o $OUT/overlay.o $OUT/shims.o $OUT/asm.o \
  $OUT/imgui.o $OUT/imgui_draw.o $OUT/imgui_tables.o $OUT/imgui_widgets.o $OUT/imgui_gl3.o \
  $SYSLIB/libc++.a $SYSLIB/libc++abi.a \
  $SYSLIB/libc.so $SYSLIB/libdl.so $SYSLIB/libm.so $SYSLIB/liblog.so \
  $SYSLIB/libandroid.so $SYSLIB/libEGL.so $SYSLIB/libGLESv3.so

echo "=== built: $OUT/libadofailoader.so ==="
ls -la $OUT/libadofailoader.so