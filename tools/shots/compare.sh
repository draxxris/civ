#!/usr/bin/env bash
# Side-by-side and difference image of two captures: compare.sh before.png after.png out.png
# Writes out.png (before | after | diff) and prints how many pixels differ.
set -euo pipefail
[[ $# -eq 3 ]] || { echo "usage: $0 before.png after.png out.png" >&2; exit 2; }
diff_png="$(mktemp --suffix=.png)"
trap 'rm -f "$diff_png"' EXIT
changed="$(compare -metric AE -fuzz 2% "$1" "$2" "$diff_png" 2>&1 || true)"
magick "$1" "$2" "$diff_png" +append -resize 3000x "$3"
echo "differing pixels: ${changed%% *} (wrote $3)"
