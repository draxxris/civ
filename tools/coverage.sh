#!/usr/bin/env bash
# Line and branch coverage of the default tests with Microsoft.CodeCoverage (bundled with the test SDK, no network),
# as Cobertura XML: one file per test project under $ARIEL_COVERAGE_DIR (default /var/tmp/ariel/coverage).
#
#   tools/coverage.sh [--filter EXPR]
#
# --filter narrows the tests as dotnet test does (it is added to the default suite's filter), e.g.
# --filter 'FullyQualifiedName~Ariel.Mods.Tests.ResearchRulesTests.' to see what one class covers. Mod assemblies loaded
# from the payload folders are covered too; test assemblies and test mods are left out (tools/coverage.runsettings).
set -euo pipefail
filter='Category!=Extractor&Category!=E2E&Category!=WorldGen&Category!=Verify'
if [[ "${1:-}" == --filter ]]; then filter="$filter&($2)"; shift 2; fi
out=${ARIEL_COVERAGE_DIR:-/var/tmp/ariel/coverage}/$(date +%Y%m%d-%H%M%S)
mkdir -p "$out"
dotnet build Ariel.slnx -v quiet -nologo > /dev/null
for project in Ariel.Tests Ariel.Mods.Tests; do
  dotnet test "tests/$project/$project.csproj" --no-build --filter "$filter" --settings tools/coverage.runsettings \
    --collect 'Code Coverage' --results-directory "$out/$project.raw" -v quiet -nologo | grep -E 'Passed!|Failed!|No test' || true
  file=$(find "$out/$project.raw" -name '*.cobertura.xml' | head -1)
  [[ -n "$file" ]] && mv "$file" "$out/$project.cobertura.xml"
  rm -rf "$out/$project.raw"
done
ls "$out"/*.cobertura.xml
