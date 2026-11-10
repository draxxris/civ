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
# Captures run on a display of their own: a desktop throttles a window it does not focus to about one frame a second,
# so a capture there takes many times longer. ARIEL_SHOT_DESKTOP=1 uses the current display instead, to watch one;
# --inspect, which stays open for the camera, always does.
if [[ -z "${ARIEL_SHOT_DESKTOP:-}" && " ${args[*]} " != *" --inspect "* ]]; then
  if command -v Xvfb >/dev/null; then
    number=$(mktemp)
    # Xvfb picks a free display and writes its number to fd 3 once it accepts clients.
    Xvfb -displayfd 3 -screen 0 1600x900x24 -nolisten tcp 3>"$number" 2>/dev/null & xvfb=$!
    trap 'kill $xvfb 2>/dev/null; rm -f "$number"' EXIT
    for _ in $(seq 100); do [[ -s "$number" ]] && break; sleep 0.05; done
    [[ -s "$number" ]] || { echo "shot: Xvfb did not start" >&2; exit 1; }
    export DISPLAY=":$(head -n1 "$number")"
    unset WAYLAND_DISPLAY
  else
    echo "shot: Xvfb is not installed; capturing on the current display, where an unfocused window may run at 1 fps" >&2
  fi
fi
godot-mono --path src/Ariel.Godot "${engine[@]}" -- "${args[@]}"
