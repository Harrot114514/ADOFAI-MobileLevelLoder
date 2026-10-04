#!/bin/bash
# ============================================================================
# CI build script for libadofailoader.so (GitHub Actions, x86_64 Linux).
# Uses the Android NDK's own clang++ (the prebuilt toolchain binaries run
# natively on x86_64 runners). Device builds use build.sh instead.
#
# Requires: NDK_ROOT pointing at an extracted NDK (r27d recommended).
# ============================================================================
set -e

NDK="${NDK_ROOT:-$HOME/android-ndk-r27d}"
CXX=$NDK/toolchains/llvm/prebuilt/linux-x86_64/bin/aarch64-linux-android25-clang++
STRIP=$NDK/toolchains/llvm/prebuilt/linux-x86_64/bin/llvm-strip
SYSROOT=$NDK/toolchains/llvm/prebuilt/linux-x86_64/sysroot
SYSLIB=$SYSROOT/usr/lib/aarch64-linux-android/25
CLANG_LIB=$NDK/toolchains/llvm/prebuilt/linux-x86_64/lib/clang/18/lib

if [ ! -x "$CXX" ]; then
    echo "ERROR: NDK clang++ not found at $CXX (set NDK_ROOT)" >&2
    exit 1
fi

ROOT="$(cd "$(dirname "$0")" && pwd)"
SRC=$ROOT/src
IMGUI=$ROOT/../imgui-master
OUT=$ROOT/out
mkdir -p $OUT

TARGET=aarch64-linux-android25

COMMON="-target $TARGET -std=c++17 -O2 -fPIC -fvisibility=hidden -fno-rtti \
 -fno-strict-aliasing -ffunction-sections -fdata-sections \
 -DANDROID -DIMGUI_IMPL_OPENGL_ES3 \
 -I$SRC -I$SRC/font -I$IMGUI -I$IMGUI/backends \
 --sysroot=$SYSROOT"

echo "== compiling =="
$CXX $COMMON -c -o $OUT/main.o     $SRC/main.cpp
$CXX $COMMON -c -o $OUT/util.o     $SRC/util.cpp
$CXX $COMMON -c -o $OUT/hooks.o    $SRC/hooks.cpp
$CXX $COMMON -c -o $OUT/il2cpp.o   $SRC/il2cpp.cpp
$CXX $COMMON -c -o $OUT/game.o     $SRC/game.cpp
$CXX $COMMON -c -o $OUT/input.o    $SRC/input.cpp
$CXX $COMMON -c -o $OUT/render.o   $SRC/render.cpp
$CXX $COMMON -c -o $OUT/overlay.o  $SRC/overlay.cpp
$CXX $COMMON -c -o $OUT/shims.o    $SRC/shims.cpp
$CXX $COMMON -c -o $OUT/asm.o      $SRC/hooks_asm.S

$CXX $COMMON -c -o $OUT/imgui.o          $IMGUI/imgui.cpp
$CXX $COMMON -c -o $OUT/imgui_draw.o     $IMGUI/imgui_draw.cpp
$CXX $COMMON -c -o $OUT/imgui_tables.o   $IMGUI/imgui_tables.cpp
$CXX $COMMON -c -o $OUT/imgui_widgets.o  $IMGUI/imgui_widgets.cpp
$CXX $COMMON -c -o $OUT/imgui_gl3.o      $IMGUI/backends/imgui_impl_opengl3.cpp

cat > $OUT/version.script <<'EOF'
{
  global: JNI_OnLoad;
  local: *;
};
EOF

echo "== linking =="
$CXX $COMMON -shared -o $OUT/libadofailoader.so \
  -nostdlib++ -fuse-ld=lld \
  -Wl,--exclude-libs,ALL \
  -Wl,--version-script=$OUT/version.script \
  -Wl,--soname=libadofailoader.so \
  -Wl,-z,max-page-size=16384 \
  -Wl,--build-id=sha1 \
  -Wl,--gc-sections \
  $OUT/main.o $OUT/util.o $OUT/hooks.o $OUT/il2cpp.o $OUT/game.o \
  $OUT/input.o $OUT/render.o $OUT/overlay.o $OUT/shims.o $OUT/asm.o \
  $OUT/imgui.o $OUT/imgui_draw.o $OUT/imgui_tables.o $OUT/imgui_widgets.o $OUT/imgui_gl3.o \
  $SYSLIB/libc++.a $CLANG_LIB/aarch64-unknown-linux-musl/libc++abi.a \
  $SYSLIB/libc.so $SYSLIB/libdl.so $SYSLIB/libm.so $SYSLIB/liblog.so \
  $SYSLIB/libandroid.so $SYSLIB/libEGL.so $SYSLIB/libGLESv3.so

if [ -x "$STRIP" ]; then
    $STRIP --strip-unneeded $OUT/libadofailoader.so || true
fi

echo "=== built: $OUT/libadofailoader.so ==="
ls -la $OUT/libadofailoader.so
