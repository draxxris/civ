#!/usr/bin/env bash
# Mutation-test chosen source files with Stryker.NET. Stryker swaps mutated assemblies into the test project's output,
# so it runs on a scratch copy of the tree under /var/tmp/ariel, never on this checkout.
#
#   tools/mutation-test.sh TEST_PROJECT PROJECT GLOB [GLOB ...] [-- stryker options]
#   e.g. tools/mutation-test.sh tests/Ariel.Mods.Tests Ariel.Rules.csproj 'Research/**/*.cs'
#
# Globs are relative to the mutated project's folder. The run keeps going after the first failing test (--disable-bail)
# and covers per test, so the JSON report names every test that kills each mutant. Reports land in
# $ARIEL_MUTATION_DIR/reports/<timestamp>/ (default /var/tmp/ariel/mutation). ARIEL_MUTATION_FILTER replaces the default
# test filter (the default suite's), e.g. to leave out whole-game tests that make every mutant take half a minute, and
# ARIEL_MUTATION_CONCURRENCY sets the parallel test sessions (default 2; each session runs xunit in parallel too).
set -euo pipefail
[[ $# -ge 3 ]] || { sed -n '2,9p' "$0" >&2; exit 2; }
test_project=$1 project=$2; shift 2
mutate=()
while [[ $# -gt 0 && "$1" != -- ]]; do mutate+=(--mutate "$1"); shift; done
[[ "${1:-}" == -- ]] && shift
root=${ARIEL_MUTATION_DIR:-/var/tmp/ariel/mutation}
tool=$root/tool
tree=$root/tree
mkdir -p "$root"
if [[ ! -x $tool/dotnet-stryker ]]; then
  dotnet tool install dotnet-stryker --version 5.1.0 --tool-path "$tool" > /dev/null
fi
# The Godot client, docs and art workbench are not needed to build or run the headless tests.
rsync -a --delete --exclude .git --exclude bin --exclude obj --exclude StrykerOutput --exclude /docs \
  --exclude /icon-workbench --exclude /src/Ariel.Godot ./ "$tree/"
out=$root/reports/$(date +%Y%m%d-%H%M%S)
mkdir -p "$out"
cat > "$tree/$test_project/stryker-config.json" <<'EOF'
{ "stryker-config": { "coverage-analysis": "perTest" } }
EOF
cd "$tree/$test_project"
# An interrupted run can leave a mutated assembly in the test output, which an incremental build would keep.
rm -rf bin
dotnet build -v quiet -nologo > /dev/null
exec "$tool/dotnet-stryker" --project "$project" "${mutate[@]}" --disable-bail --skip-version-check \
  --test-case-filter "${ARIEL_MUTATION_FILTER:-Category!=Extractor&Category!=E2E&Category!=WorldGen&Category!=Verify}" \
  --concurrency "${ARIEL_MUTATION_CONCURRENCY:-2}" --reporter json --reporter html --reporter progress \
  --output "$out" "$@"
