#!/usr/bin/env bash
# Play one headless AI game and write the final save. Relative paths resolve against the directory you ran mise from.
set -u
here="${MISE_ORIGINAL_CWD:-$PWD}"
args=()
for arg in "$@"; do
  if [[ "$arg" == *.json ]]; then arg="$(cd "$here" && realpath -m -- "$arg")"; fi
  args+=("$arg")
done
dotnet build tools/Ariel.SyncCheck --configuration Release -v quiet -nologo > /dev/null || exit 1
exec tools/Ariel.SyncCheck/bin/Release/net10.0/Ariel.SyncCheck hash-trace "${args[@]}" > /dev/null
