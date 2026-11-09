#!/usr/bin/env bash
# Run the Godot client for a capture. Relative file arguments are resolved against the directory you ran from,
# because Godot runs with its own working directory.
#   mise run shot -- --focus hills --clean --out-dir /tmp/shots --screenshot hills
set -euo pipefail
here="${MISE_ORIGINAL_CWD:-$PWD}"
args=()
prev=""
for arg in "$@"; do
  case "$prev" in
    --out-dir|--shots|--scenario|--tuning-file) arg="$(cd "$here" && realpath -m -- "$arg")" ;;
    --screenshot) [[ "$arg" == */* ]] && arg="$(cd "$here" && realpath -m -- "$arg")" ;;
  esac
  args+=("$arg"); prev="$arg"
done
# ARIEL_FIXED_FPS=N steps the engine N frames per simulated second however slowly frames really render, so captures
# of animations (walks, battles) land at the same moment on a throttled desktop as under Xvfb.
engine=()
[[ -n "${ARIEL_FIXED_FPS:-}" ]] && engine=(--fixed-fps "$ARIEL_FIXED_FPS")
exec godot-mono --path src/Ariel.Godot "${engine[@]}" -- "${args[@]}"
