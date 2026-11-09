#!/usr/bin/env bash
# Two-process multiplayer smoke check on one machine: a host and a guest game client over localhost TCP, under Xvfb,
# each ending turns until the target turn. Fails unless both reach it with the same state hash. Screenshots of both
# land in the output directory.
#   mise run check:multiplayer -- [--turns N] [--world duel] [--opponents N] [--out-dir DIR]
set -u
turns=3; world=duel; opponents=1; out=/tmp/ariel/mp-smoke; port=$((47000 + RANDOM % 900))
while [ $# -gt 0 ]; do
  case "$1" in
    --turns) turns=$2; shift 2 ;;
    --world) world=$2; shift 2 ;;
    --opponents) opponents=$2; shift 2 ;;
    --out-dir) out=$2; shift 2 ;;
    *) echo "unknown option $1"; exit 2 ;;
  esac
done
mkdir -p "$out"
dotnet build src/Ariel.Godot -v quiet -nologo || exit 1
display=${DISPLAY:-}
if command -v Xvfb >/dev/null; then
  display=:97
  Xvfb $display -screen 0 1600x900x24 -nolisten tcp & xvfb=$!
  trap 'kill $xvfb 2>/dev/null' EXIT
  sleep 1
else
  echo "warning: Xvfb is not installed; using the current display, where unfocused windows may be throttled"
fi
common=(--world "$world" --turns "$turns" --frames 3 --mp-port "$port" --out-dir "$out")
DISPLAY=$display godot-mono --path src/Ariel.Godot -- --mp host --opponents "$opponents" --screenshot mp-host "${common[@]}" > "$out/host.log" 2>&1 & host=$!
sleep 2
DISPLAY=$display godot-mono --path src/Ariel.Godot -- --mp join --screenshot mp-guest "${common[@]}" > "$out/guest.log" 2>&1 & guest=$!
rc=0
wait $host || rc=1
wait $guest || rc=1
h=$(grep -o 'multiplayer smoke: .*' "$out/host.log" | tail -1)
g=$(grep -o 'multiplayer smoke: .*' "$out/guest.log" | tail -1)
echo "host:  ${h:-no result (see $out/host.log)}"
echo "guest: ${g:-no result (see $out/guest.log)}"
if [ $rc -ne 0 ] || [ -z "$h" ] || [ "$h" != "$g" ]; then echo "FAILED"; exit 1; fi
echo "in sync: both peers reached the same state; screenshots in $out"
