#!/usr/bin/env bash
# Play one headless AI game and write the final save. Relative paths (the save, the --profile directory) resolve
# against the directory you ran mise from.
set -u
here="${MISE_ORIGINAL_CWD:-$PWD}"
# --bin DIR runs a build copied aside earlier (DIR holds Ariel.SyncCheck) instead of building this tree, to time two
# trees one after the other under the same cooling gate.
bin=""
if [[ "${1:-}" == --bin ]]; then bin="$(cd "$here" && realpath -m -- "$2")"; shift 2; fi
args=()
path_next=0
for arg in "$@"; do
  if [[ "$arg" == *.json || $path_next == 1 ]]; then arg="$(cd "$here" && realpath -m -- "$arg")"; fi
  path_next=0
  [[ "$arg" == --profile ]] && path_next=1
  args+=("$arg")
done
if [[ -z "$bin" ]]; then
  dotnet build tools/Ariel.SyncCheck --configuration Release -v quiet -nologo > /dev/null || exit 1
  bin=tools/Ariel.SyncCheck/bin/Release/net10.0
fi
exec "$bin/Ariel.SyncCheck" hash-trace "${args[@]}" > /dev/null
