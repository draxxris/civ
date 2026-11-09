#!/usr/bin/env bash
# Plays one AI game in three processes and fails unless every turn hash matches. See mise task check:sync.
set -u
dotnet build tools/Ariel.SyncCheck --configuration Release -v quiet -nologo || exit 1
exe=tools/Ariel.SyncCheck/bin/Release/net10.0/Ariel.SyncCheck
out=/tmp/ariel/sync-check
mkdir -p "$out"
DOTNET_PROCESSOR_COUNT=1 "$exe" hash-trace "$@" > "$out/serial.txt" & a=$!
"$exe" hash-trace "$@" > "$out/parallel.txt" & b=$!
"$exe" hash-trace --reload-every 10 "$@" > "$out/reload.txt" & c=$!
rc=0
wait $a || rc=1; wait $b || rc=1; wait $c || rc=1
[ $rc -eq 0 ] || { echo "a hash-trace run failed"; exit 1; }
for run in parallel reload; do
  if ! cmp -s "$out/serial.txt" "$out/$run.txt"; then
    first=$(diff "$out/serial.txt" "$out/$run.txt" | grep -m1 '^<' | cut -d' ' -f2)
    echo "DESYNC: serial and $run differ from turn $first (traces in $out)"
    exit 1
  fi
done
echo "in sync: $(wc -l < "$out/serial.txt") turn hashes match across serial, parallel and save/reload runs"
