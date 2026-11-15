#!/bin/bash
# Static regression guard for the modal dialogs.
#
# ImGui sizes an ImGuiWindowFlags_AlwaysAutoResize window from its content, so a
# TextWrapped text inside it wraps at the (still tiny) current width and freezes
# the dialog at "one character per line" - exactly the narrow vertical strip the
# delete dialog showed once. Every BeginPopupModal must therefore be preceded by
# a SetNextWindowSizeConstraints() with a real width.
set -u
SRC="$(cd "$(dirname "$0")/../src" && pwd)/overlay.cpp"
fail=0
while IFS=: read -r line _rest; do
    start=$((line - 8)); [ "$start" -lt 1 ] && start=1
    if sed -n "${start},${line}p" "$SRC" | grep -q "SetNextWindowSizeConstraints"; then
        echo "  ok   BeginPopupModal at line $line constrains its width"
    else
        echo "  FAIL BeginPopupModal at line $line: no SetNextWindowSizeConstraints() nearby"
        fail=$((fail + 1))
    fi
done < <(grep -n "ImGui::BeginPopupModal" "$SRC")

if [ "$fail" -eq 0 ]; then echo "POPUP WIDTH CHECK PASSED"; else echo "$fail DIALOG(S) UNCONSTRAINED"; fi
exit $((fail > 0))
