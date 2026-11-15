#!/bin/bash
# Host test driver for the ZIP import feature (no Android device needed).
#
#   ./zip_test.sh
#
# Builds test4_zip / test5_zipimport, creates a handful of archives (deflate,
# stored, multi-level, zip-slip, no-level, corrupted) and asserts on the
# results. Needs: g++ + zlib (set ZLIB_INC if zlib.h is not in /usr/include).
set -u

HERE="$(cd "$(dirname "$0")" && pwd)"
SRC=$HERE/../src
W=${TMPDIR:-/tmp}/adofai_zip_test
T=$W/t
INC=${ZLIB_INC:+-I$ZLIB_INC}

rm -rf "$W"
mkdir -p "$W/stub/android" "$W/src" "$T"

# <android/log.h> stub for util.cpp (same trick as the hook tests)
cp "$HERE/android_log_stub.h" "$W/stub/android/log.h"

# --- zlib on the host: prefer -lz, fall back to the runtime soname ----------
if echo 'int main(){return 0;}' | g++ -x c++ - -lz -o "$W/zl" 2>/dev/null; then
    ZLIB="-lz"
else
    ZD="$(ldconfig -p 2>/dev/null | awk '/libz\.so\.1 /{print $NF; exit}')"
    [ -n "$ZD" ] || { echo "zlib not found (install zlib1g-dev)"; exit 1; }
    ZLIB="-L$(dirname "$ZD") -l:libz.so.1"
fi

echo "== building =="
g++ -O2 -std=c++17 -o "$W/t4" "$HERE/test4_zip.cpp" "$SRC/zipfile.cpp" \
    -I"$SRC" $INC $ZLIB || exit 1
g++ -O2 -std=c++17 -pthread -o "$W/t5" "$HERE/test5_zipimport.cpp" \
    "$SRC/zipimport.cpp" "$SRC/zipfile.cpp" "$SRC/util.cpp" \
    -I"$W/stub" -I"$SRC" $INC $ZLIB || exit 1

fail=0
check() {  # check <description> <haystack-file> <grep-pattern>
    if grep -q "$3" "$2"; then
        echo "  ok   $1"
    else
        echo "  FAIL $1  (pattern: $3)"
        sed 's/^/       | /' "$2" | head -20
        fail=$((fail + 1))
    fi
}
check_not() {  # check_not <description> <file> <pattern>
    if grep -q "$3" "$2"; then
        echo "  FAIL $1  (unexpected: $3)"
        fail=$((fail + 1))
    else
        echo "  ok   $1"
    fi
}

# --- fixtures ---------------------------------------------------------------
echo "== fixtures =="
mkdir -p "$T/single" "$T/multi/pack/a" "$T/multi/pack/b/sub" "$T/nolevel"
python3 - "$T/single/level.adofai" <<'PY'
import sys
open(sys.argv[1], "w").write('{"song":"song.mp3","bpm":120.0,"pathData":"R"}')
PY
head -c 40000 /dev/urandom > "$T/single/song.mp3"
head -c 8000  /dev/urandom > "$T/single/bg.png"

for d in a b; do
    cp "$T/single/level.adofai" "$T/multi/pack/$d/level-$d.adofai"
done
cp "$T/single/level.adofai" "$T/multi/pack/b/sub/level-c.adofai"
echo "readme" > "$T/multi/pack/readme.txt"
mkdir -p "$T/multi/__MACOSX"
echo "junk"   > "$T/multi/__MACOSX/._level.adofai"

cp "$T/single/bg.png" "$T/nolevel/bg.png"

(cd "$T/single" && zip -q -r "$W/single.zip" .)
(cd "$T/multi"  && zip -q -r "$W/multi.zip" .)
(cd "$T/single" && zip -q -0 -r "$W/stored.zip" .)
(cd "$T/nolevel" && zip -q -r "$W/nolevel.zip" .)

# zip-slip attempts: "../evil.txt" and an absolute path
python3 - "$W/slip.zip" <<'PY'
import sys, zipfile
z = zipfile.ZipFile(sys.argv[1], "w", zipfile.ZIP_DEFLATED)
z.writestr("../evil.txt", "pwned")
z.writestr("/abs.txt", "pwned")
z.writestr("ok.adofai", '{"song":"x.mp3"}')
z.close()
PY

# corrupted payload: flip one byte inside the deflated stream
python3 - "$W/single.zip" "$W/corrupt.zip" <<'PY'
import sys
data = bytearray(open(sys.argv[1], "rb").read())
data[len(data) // 2] ^= 0xFF
open(sys.argv[2], "wb").write(data)
PY

# --- 1. reader on a normal archive -----------------------------------------
echo "== reader: single.zip =="
"$W/t4" "$W/single.zip" "$W/x_single" > "$W/o1" 2>&1
check "opens and lists 3 entries"     "$W/o1" "OPEN_OK entries=3"
check "extracts all entries"          "$W/o1" "DONE ok=3 fail=0"
check "deflate method used"           "$W/o1" "method=8"
check "level.adofai extracted"        "$W/o1" "EXTRACTED level.adofai"

# --- 2. single level -------------------------------------------------------
echo "== import: single.zip =="
"$W/t5" "$W/single.zip" "$W/x_root1" > "$W/o2" 2>&1
check "one level found"               "$W/o2" "READY levels=1"
check "level path reported"           "$W/o2" "LEVEL level.adofai"
check "assets extracted next to it"   "$W/o2" "READY"
cmp -s "$W/x_root1/song.mp3" "$T/single/song.mp3" \
    && echo "  ok   song.mp3 byte-identical" || { echo "  FAIL song.mp3 differs"; fail=$((fail+1)); }

# --- 3. several levels -----------------------------------------------------
echo "== import: multi.zip =="
"$W/t5" "$W/multi.zip" "$W/x_root2" > "$W/o3" 2>&1
check "three levels found"            "$W/o3" "READY levels=3"
check "levels sorted (a)"             "$W/o3" "LEVEL pack/a/level-a.adofai"
check "levels sorted (b/sub)"         "$W/o3" "LEVEL pack/b/sub/level-c.adofai"
check "levels sorted (b)"             "$W/o3" "LEVEL pack/b/level-b.adofai"
check_not "__MACOSX is skipped"       "$W/o3" "__MACOSX"

# --- 4. stored (no compression) -------------------------------------------
echo "== import: stored.zip =="
"$W/t5" "$W/stored.zip" "$W/x_root3" > "$W/o4" 2>&1
check "stored archive imports"        "$W/o4" "READY levels=1"
cmp -s "$W/x_root3/song.mp3" "$T/single/song.mp3" \
    && echo "  ok   stored song.mp3 byte-identical" || { echo "  FAIL stored song differs"; fail=$((fail+1)); }

# --- 5. zip-slip -----------------------------------------------------------
echo "== import: slip.zip =="
"$W/t5" "$W/slip.zip" "$W/x_root4" > "$W/o5" 2>&1
check "safe entry still imported"     "$W/o5" "LEVEL ok.adofai"
[ -e "$W/evil.txt" ] && { echo "  FAIL ../evil.txt escaped the root"; fail=$((fail+1)); } \
                     || echo "  ok   ../evil.txt blocked"
[ -e "$W/x_root4/abs.txt" ] && echo "  ok   /abs.txt re-rooted inside" \
                            || echo "  note /abs.txt not created (rejected)"
[ -e /abs.txt ] && { echo "  FAIL absolute path escaped to /"; fail=$((fail+1)); }

# --- 6. no level inside ----------------------------------------------------
echo "== import: nolevel.zip =="
"$W/t5" "$W/nolevel.zip" "$W/x_root5" > "$W/o6" 2>&1
check "reports ZIPERR_NO_LEVEL (4)"   "$W/o6" "FAILED code=4"

# --- 7. corrupted archive --------------------------------------------------
echo "== import: corrupt.zip =="
"$W/t5" "$W/corrupt.zip" "$W/x_root6" > "$W/o7" 2>&1
if grep -q -E "FAILED code=(1|2)" "$W/o7"; then
    echo "  ok   corruption detected ($(grep -o 'FAILED code=[0-9]*' "$W/o7"))"
else
    echo "  FAIL corruption not detected"; sed 's/^/       | /' "$W/o7" | head -10
    fail=$((fail + 1))
fi
"$W/t4" "$W/corrupt.zip" "$W/x_corrupt" > "$W/o8" 2>&1
check "reader reports CRC/inflate failure" "$W/o8" "EXTRACT_FAIL"

echo
if [ "$fail" -eq 0 ]; then echo "ALL ZIP TESTS PASSED"; else echo "$fail CHECK(S) FAILED"; fi
exit $((fail > 0))
