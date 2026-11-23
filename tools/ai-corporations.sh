#!/usr/bin/env bash
# Play one headless AI game (or continue a save) and report which corporations and guilds the computer players founded,
# and when, with the state of each AI player's founding buildings. Relative paths resolve against the directory you ran
# mise from.
set -u
here="${MISE_ORIGINAL_CWD:-$PWD}"
args=()
for arg in "$@"; do
  if [[ "$arg" == *.json ]]; then arg="$(cd "$here" && realpath -m -- "$arg")"; fi
  args+=("$arg")
done
dotnet build tools/Ariel.SyncCheck --configuration Release -v quiet -nologo > /dev/null || exit 1
exec tools/Ariel.SyncCheck/bin/Release/net10.0/Ariel.SyncCheck corporations "${args[@]}"
