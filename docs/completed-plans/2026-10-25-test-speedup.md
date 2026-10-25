# Test suite speed-up

Journal for the `mise run test` speed-up work set. Measured facts live under [Baseline](#baseline).

## Baseline

Measured 2026-10-25 on the dev box: 8 logical cores, unrelated background load
~4.0, so wall times swing about ±15 %. Numbers are reproducible with the commands
in [How to reproduce](#how-to-reproduce).

| Measure | Value |
|:--|:--|
| Default suite size | 1334 tests (1175 + 122 + 37) |
| Serial test work | 240 s |
| `mise run test`, warm build | 178.6 s |
| `mise run test` with `DOTNET_gcServer=1` | 135.3 s |
| No-op build | 6.5 s |
| Build after touching one `Civ.Core` file | 13.0 s |
| `Civ.Mods.Tests` parallel wall | 134 - 145 s |
| `Civ.Mods.Tests` sequential wall | 227 s |
| `Civ.Mods.Tests` in-test total (sequential) | 221.0 s |
| `Civ.Tests` | 9 s wall, 18.6 s serial |
| `Civ.ContentImport.Tests` | 0.8 s wall, 37 tests |
| CPU used by a parallel run | 4.9 of 8 cores |
| CPU consumed vs serial work | 668 CPU-s for 221 s, parallelism buys ~1.5x |
| `ModLoader.Build` (steady state) | 500 - 650 ms |

Cost distribution inside `Civ.Mods.Tests`:

| Bucket | Tests | Time |
|:--|--:|--:|
| under 100 ms | 858 | 13.6 s (6 %) |
| over 250 ms | 245 | 196.3 s |
| over 500 ms | 216 | 186.7 s (85 %) |
| slowest 20 cases | 20 | 47.9 s (22 %) |
| slowest 50 cases | 50 | 78.0 s (35 %) |

### Slowest 20 test cases (sequential, in-test time)

| # | Test | s | Cause |
|--:|:--|--:|:--|
| 1 | AiEvaluationTests.Cli_records_repeatable_games_settlements_events… | 7.53 | CLI x3 |
| 2 | RiverNetworkTests.Changing_a_river_edge… (col 0, row 3) | 3.40 | Mesh all chunks |
| 3 | TerrainMeshTests.Mesh_anchors_at_every_tile_centre_match_the_field… | 3.24 | Full-map mesh |
| 4 | AiEvaluationTests.Cli_rejects_baseline… (duplicate-row) | 3.10 | CLI x2 |
| 5 | AiEvaluationTests.Cli_rejects_baseline… (failed) | 2.84 | CLI x2 |
| 6 | TerrainMeshTests.Neighbouring_chunks_share_identical_border_vertices | 2.76 | Full-map mesh |
| 7 | AiEvaluationTests.Cli_rejects_baseline… (schema) | 2.74 | CLI x2 |
| 8 | AiEvaluationTests.Cli_rejects_baseline… (settings) | 2.65 | CLI x2 |
| 9 | AiEvaluationTests.Cli_rejects_baseline… (missing-row) | 2.45 | CLI x2 |
| 10 | ClientDiplomacyVictoryTests.Deal_screen… (option) | 1.99 | Content rebuild |
| 11 | RiverNetworkTests.Changing_a_river_edge… (col 2, row 3) | 1.76 | Mesh all chunks |
| 12 | RiverNetworkTests.Changing_a_river_edge… (col 10, row 0) | 1.75 | Mesh all chunks |
| 13 | AiTests.Strong_army_declares_war_and_captures_a_weak_neighbour | 1.72 | Real sim (keep) |
| 14 | WorldLayoutTests.Layouts_keep_their_landmarks… (new_world, 42) | 1.62 | Worldgen x3 sizes |
| 15 | ClientDiplomacyVictoryTests.Deal_screen… (unlock) | 1.55 | Content rebuild |
| 16 | WorldLayoutTests.Layouts_keep_their_landmarks… (great_plains, 42) | 1.40 | Worldgen x3 sizes |
| 17 | WorldLayoutTests.Layouts_keep_their_landmarks… (great_plains, 1) | 1.40 | Worldgen x3 sizes |
| 18 | ClientDiplomacyVictoryTests.Active_deals_can_be_cancelled | 1.39 | Content rebuild |
| 19 | TerrainIntegrationTests.Removing_a_fogged_forest… | 1.33 | Mesh + scatter plan |
| 20 | ResearchRulesTests.Missing_technology_era_is_not_inferred… | 1.31 | 2 probe content builds |

### Slowest methods and classes (sequential)

| Method | Total | Cases |
|:--|--:|--:|
| WorldRulesTests.Generators_are_deterministic_habitable_and_have_valid_layers | 15.6 s | 18 |
| AiEvaluationTests.Cli_rejects_incompatible_or_incomplete_baselines… | 13.8 s | 5 |
| WorldLayoutTests.Layouts_keep_their_landmarks_and_supply_separated_starts | 13.4 s | 11 |
| AiEvaluationTests.Cli_records_repeatable_games… | 7.5 s | 1 |
| RiverNetworkTests.Changing_a_river_edge_dirties_every_chunk… | 6.9 s | 3 |
| NetworkTransitTests (whole class) | 25.5 s | 41 |
| ClientIntelligenceEventsTests (whole class) | 12.5 s | 19 |
| ClientDiplomacyVictoryTests (whole class) | 9.4 s | 8 |

### Root causes found

1. Per-test `ModLoader.Build`. `xunit` creates a new test class instance per
   test, so a content cache held in an instance field is rebuilt for every test.
   `NetworkTransitTests` (41 tests), `ClientIntelligenceEventsTests` (19),
   `ClientDiplomacyVictoryTests` (8), `TradeRulesTests` (5),
   `BarbarianRulesTests` (4), `PillageModifierTests` (3), `ClientFollowupTests`
   (2), plus inline probes in `AiTests`, `AiIntentTests`, `WorldRulesTests`.
   Estimated 35 - 45 s of the 221 s.
2. `AiEvaluationTests` shells into `EvaluationCli.Run`, which reloads and
   rebuilds all mods on every invocation, and each theory case runs the CLI
   twice (baseline, then comparison).
3. World generation repeated per case: `WorldLayoutTests` generates duel,
   standard and gigantic per case; `WorldRulesTests.Generators_are_deterministic…`
   generates two sims per case and re-generates the same worlds as
   `WorldLayoutTests`.
4. Full-map meshing per case in `TerrainMeshTests`, `RiverNetworkTests`,
   `TerrainIntegrationTests`.
5. Workstation GC. The suite is allocation heavy, so a single background GC
   thread is the bottleneck; server GC measured at −24 % on the full task.
6. Parallelism buys only ~1.5x because tests are memory-bandwidth bound.
   Adding threads does not help; cutting work does.

## Decisions

| Item | Decision |
|:--|:--|
| G server GC | Add to `mise run test` and `test:worldgen`, keep off `test:e2e`. |
| I fast/slow split | Rejected. Not worth it. |
| C world generator tests | Move to `Category=WorldGen`, excluded by default, own task. |
| A B D E | Fix in the test code. |
| J build and orchestration | Build once, run the three test projects in parallel, add `test:nobuild`. |
| K metrics log | Raise the logger cap from 5 MB to 10 MB. |

`mise run test:worldgen` is run on a work set when the change touches world
generation: `src/Mods/World/`, map generator registrations, `MapSpec`/`HexGrid`
layout, terrain or relief generation, river and cliff generation, and the
generator-facing setup in `src/Civ.Core`.

## How to reproduce

Sequential per-test timings (ground truth):

```sh
printf '{"parallelizeTestCollections": false, "maxParallelThreads": 1}\n' \
  > tests/Civ.Mods.Tests/bin/Debug/net10.0/xunit.runner.json
dotnet test tests/Civ.Mods.Tests/Civ.Mods.Tests.csproj --no-build \
  --filter 'Category!=Extractor&Category!=E2E' \
  --logger "trx;LogFileName=s.trx" --results-directory /tmp/trxS
rm tests/Civ.Mods.Tests/bin/Debug/net10.0/xunit.runner.json
```

Full task: `mise run test`. Server GC comparison: `DOTNET_gcServer=1 mise run test`.

## Test code optimizations (items A, B, D, E)

Only files under `tests/` changed. Measurements use the ground-truth sequential mode from [How to reproduce](#how-to-reproduce) (temporary `xunit.runner.json` in `tests/Civ.Mods.Tests/bin/Debug/net10.0`, deleted afterwards), `dotnet test --no-build` after an explicit `dotnet build Civ.slnx`. In-test totals come from the `.trx` files.

| Measure | Before | After |
|:--|--:|--:|
| `Civ.Mods.Tests` default filter, sequential in-test total | 220.0 s | 168.3 s |
| `Civ.Mods.Tests` default filter, sequential wall | 225 s | 180 s |
| `Civ.Mods.Tests` default filter, parallel wall | - | 109 s (1 m 46 s) |
| Tests | 1175 | 1175 (all passing) |

The eight classes this work set touched went from 89.7 s to 35.9 s in-test (-53.8 s). The rest of the suite is unchanged; the remaining ~2 s of drift is the documented ±15 % background noise (for example `AiTests` 4.3 s -> 8.0 s, `WorldRulesTests` 27.8 s -> 25.3 s, neither touched).

| Class | Change | Before | After |
|:--|:--|--:|--:|
| NetworkTransitTests | instance dict -> two static `Lazy<ContentDb>` | 25.3 s | 1.6 s |
| ClientIntelligenceEventsTests | `ConcurrentDictionary<string, Lazy<ContentDb>>` | 12.6 s | 3.2 s |
| ClientDiplomacyVictoryTests | `ConcurrentDictionary<(bool,bool), Lazy<ContentDb>>` | 6.4 s | 3.1 s |
| TradeRulesTests | one static `Lazy<ContentDb>` | 3.0 s | 0.6 s |
| BarbarianRulesTests | one static `Lazy<ContentDb>` | 2.7 s | 0.8 s |
| AiEvaluationTests | one shared baseline, copied per case | 20.6 s | 15.3 s |
| TerrainMeshTests | one shared `RenderedMap`, peaks in one chunk row/col | 11.6 s | 7.2 s |
| RiverNetworkTests | re-mesh only dirty plus widest influence ring | 7.5 s | 4.1 s |

### A - stop rebuilding `ContentDb` per test

Root cause confirmed: `xunit` creates a new test class instance per test, so an instance cache is a per-test cache. Every cache is now static and thread-safe (`Lazy<ContentDb>` or a `ConcurrentDictionary` of `Lazy<ContentDb>`); each probe's registrations, source order and captured `RuleConstantsDef` are byte-for-byte what they were. `ContentDb` is frozen, so sharing is safe; the simulations (`GameSession`, `Simulation`) remain per test.

| Class | Change | In-test before | In-test after |
|:--|:--|--:|--:|
| `NetworkTransitTests` | instance `Dictionary<bool, ContentDb>` -> two static `Lazy<ContentDb>`; the `LoadedMods` fixture moved into the static builder | 25.3 s (41) | 1.6 s (41) |
| `ClientIntelligenceEventsTests` | static `ConcurrentDictionary<string, Lazy<ContentDb>>` keyed by the optional-mod set (5 distinct sets) | 12.6 s (19) | 3.2 s (19) |
| `ClientDiplomacyVictoryTests` | static `ConcurrentDictionary<(bool, bool), Lazy<ContentDb>>` keyed by the diplomacy/victory set (2 distinct sets) | 6.4 s (8) | 3.1 s (8) |
| `TradeRulesTests` | `Scenario()`'s inline probe build -> one static `Lazy<ContentDb>` | 3.0 s (5) | 0.6 s (5) |
| `BarbarianRulesTests` | `Coast()`'s inline probe build -> one static `Lazy<ContentDb>` | 2.7 s (4) | 0.8 s (4) |

`ClientIntelligenceEventsTests`, `ClientDiplomacyVictoryTests` and `NetworkTransitTests` no longer need the `LoadedMods` class fixture (their static builders create their own, and `LoadedMods` already shares the sources and content statically).

Skipped, with reasons:

- `PillageModifierTests`: three theory cases each pass a different `percent`, and each key is used by exactly one case, so a cache keyed by `percent` builds the same three contents. Zero saving; nothing gained for a new static cache.
- `ClientFollowupTests`: its two inline builds belong to two single `[Fact]`s with different probes. One build per test either way. Zero saving.
- `AiTests`: same shape (two `[Fact]`s, two one-shot `SettlementProbe`/`ScoringProbe` builds). Worse, both probes carry mutable run state that the assertions read (`probe.Decisions`, `probe.Checked`), so a static cache would also freeze probe state across tests. Not done.
- `AiIntentTests`: the theory's three cases each pass different arguments, so there are three distinct contents and each is used once. Zero saving.
- `WorldRulesTests.Warming.cs`: the two remaining inline `WarmingContent(...)` calls each feed one `[Fact]` (`extremeDefense: true`, `flood: false`), so they build exactly once per run. Zero saving; the shared default and slow contents were already `Lazy` static.
- `CityRulesTests`: already hoisted. The three probe helpers at ~118, ~677 and ~1034 are only reached from static `Lazy<ContentDb>` fields (`ProbeContent`/`RefundProbeContent`/`ExtraCultureProbeContent`, `PowerContent`/`FreePowerContent`, `ConquestContent`/`UnscaledConquestContent`/`AlwaysRazeConquestContent`), so nothing rebuilds per test.

### B - `AiEvaluationTests`

`Cli_rejects_incompatible_or_incomplete_baselines_without_overwriting_them` used to run the CLI twice per case: once to produce a baseline and once to compare against the damaged baseline. `EvaluationCli.Run` reloads and rebuilds every mod on each call, so the five cases paid for ten CLI runs.

Now a class fixture (`ValidBaseline`) runs the CLI once for the baseline every case shares, and each case `File.Copy`s it into its own workspace directory before damaging its copy. The copy is byte-identical, so the assertions are untouched, including "the baseline file is unchanged" and "no comparison.json was written" (both now read the case's own copy). The shared directory is deleted when the fixture is disposed after the class. No overload or hook was added under `tools/`.

| Measure | Before | After |
|:--|--:|--:|
| `Cli_rejects_...` theory | 13.6 s (5 cases) | 8.5 s (5 cases) |
| `AiEvaluationTests` class | 20.6 s (8) | 15.3 s (8) |

The initial estimate's "about 3 s" target is not reachable test-side: the remaining cost is five comparison invocations at ~1.2 s each, and one CLI run per case is the behaviour under test (a baseline cannot be compared without a comparison run). Reducing further would need content caching inside `tools/Civ.Benchmarks/AiEvaluation`, which is out of scope for this work set.

### D - terrain mesh tests

#### `TerrainMeshTests`

`MeshBaseline` built two complete `RenderedMap`s (`Mixed` and `MixedResolution`), each meshing every chunk at peak resolution (measured: 3.2 s + 2.6 s). Both fixture options in the initial investigation were checked first:

1. Derive `MixedResolution` by re-meshing only the chunks whose inputs differ: measured, 14 of the 16 chunks differ (the fixture's peaks are spread over the whole random map and a chunk's ring is 2.6 world units wide, so nearly every chunk is a peak chunk). A signature-based "inputs differ" set is all 16 chunks. Re-meshing 14 instead of 16 saves ~0.2 s, so this option buys nothing here.
2. Lower the "peak field resolution argument": `new TerrainField(snapshot, 8)` is the field's **seed**, not a resolution. The mesher resolution comes from `TerrainChunks.Resolutions` and is fixed by `ReliefClass.Peak`; there is no detail knob in the fixture.

What changed instead, keeping every assertion:

- The two fixtures now share one `RenderedMap` (both properties return the same read-only instance). The border test needs a resolution boundary; the anchor test needs a vertex at every tile centre. One map provides both, so the second full-map mesh is gone.
- The shared map's peaks are kept inside one chunk column and one chunk row, so two chunks mesh at the peak lattice and the other fourteen at the default lattice. `Assert.Equal(2, rendered.Resolutions.Distinct().Count())` still holds, the fine/coarse stitching case still runs over fine chunks with coarse neighbours on every side, the wrap check still compares both coarse edge columns, and the anchor check still walks every tile of the map. Peak chunks are ~9x the meshing cost of default chunks, which is where the saving comes from.
- One tolerance change in `A_fine_chunk_edge_lies_on_its_coarse_neighbour_without_cracks`: the fine edge height is compared to the coarse mesh height with a 1e-5 epsilon instead of "4 decimal places". The mesher guarantees exact lattice agreement and float storage leaves ~1.5e-7, but rounding both values to four decimals admits up to 5e-5 and flips when a height sits on a rounding boundary (the concentrated map put one at 0.01125). The epsilon is strictly tighter than the old rounding window, so the guard is not weakened; it is less brittle.

| Test | Before | After |
|:--|--:|--:|
| `Mesh_anchors_at_every_tile_centre_match_the_field_including_the_seabed` | 3.26 s | 0.02 s |
| `Neighbouring_chunks_share_identical_border_vertices` | 2.70 s | 1.38 s |
| `TerrainMeshTests` class | 11.6 s (16) | 7.2 s (16) |

The two slow rows above pay for the single shared build in whichever test touches the fixture first (`Neighbouring...` here), so read the class total, not the rows.

#### `TerrainIntegrationTests`

Declined after measuring. The catalog is already shared: `LoadedMods` is a class fixture and its `Catalog` is created once per class, so `mods.Catalog` is class-scoped today. The remaining parts are cheap: `new TerrainPalette(catalog)` costs ~5 ms and `new ScatterPlanner(catalog)` is free, so sharing them saves ~40 ms over the class. A class fixture cannot take another class fixture as a constructor argument (xunit reports an unresolved constructor argument), so the only way to build them in a fixture is a second `GameCatalog`, measured at ~1.0 s - more than the whole saving. Snapshots, fields and simulations must stay per test in any case. Class total stays 3.7 s (4); no change was kept.

#### `ResearchRulesTests`

Declined after trying it. Merging `MissingEra` and `CustomRules` into one probe mod is not content-compatible: with `probe:missing` (a technology without an era) in the excessive-power scenario, the research power stays below the guard and `Assert.Throws<OverflowException>` stops firing, so the merged content silently guards less than the two separate contents. The two `Lazy<ContentDb>` builds stay; the test keeps its two contents and its 1.31 s.

### E - `RiverNetworkTests`

`Changing_a_river_edge_dirties_every_chunk_whose_terrain_or_river_surface_changes` re-meshed all 30 chunks (two meshers each) to prove that every chunk whose mesh changed is in `DirtyChunks`. The rebuild now covers only the chunks that can legally change: the `dirty` set plus every chunk within the widest of `RingRadius`, `RiverRingRadius` and `CliffRingRadius` of the edited tile (`InInfluenceRing`, wrap-aware). The comment in the test explains the bound: `TerrainChunks` collects exactly those rings as the tiles that can change a chunk's ground, river surface or cliffs, so a chunk outside all three provably cannot change, while the dirty set is still compared in full. All four assertions are unchanged: `changed` is non-empty, every changed chunk is dirty, `dirty.Count < chunks.Count`, and the wrap-edge / north-boundary / long-connector specifics. No theory case was removed.

| Measure | Before | After |
|:--|--:|--:|
| theory (col 0, row 3, dir 2) | 3.41 s | 2.54 s |
| theory (col 10, row 0, dir 0) | 1.89 s | 0.43 s |
| theory (col 2, row 3, dir 2) | 1.76 s | 0.69 s |
| `RiverNetworkTests` class | 7.5 s (7) | 4.1 s (7) |

The west-edge case stays the slowest because the wrap puts the east chunk column inside the river ring as well, so its candidate set is the largest of the three.

### Verification of test code changes

```
dotnet build Civ.slnx                                                   # 0 warnings, 0 errors
dotnet test tests/Civ.Mods.Tests ... --filter 'FullyQualifiedName~<class>'  # each touched class green
dotnet test tests/Civ.Mods.Tests ... --filter 'Category!=Extractor&Category!=E2E'
    # Passed: 1175, Failed: 0 (1 m 46 s wall, parallel)
```

No new trait appeared in `WorldRulesTests`, `WorldLayoutTests`, `CliffRulesTests` or `BonusModelTests`, so the filter needs no extra category. The temporary `xunit.runner.json` used for the sequential measurements was deleted.

## Harness and runner optimizations (items C, G, J, K)

Work started from a tree that already contained the test-code fixes from items A/B/D/E (content caching in `Civ.Mods.Tests`, per-class mesh fixtures). Measured 2026-10-25 on the dev box (8 logical cores, load average 1.3 - 3.3 during the runs, background load varies run to run, so ranges are given).

| Measure | Before | After |
|:--|--:|--:|
| `mise run test`, wall | 127.3 - 139.4 s | 61.9 - 64.4 s |
| Default test count | 1334 (1175 + 122 + 37) | 1296 (1137 + 122 + 37) |
| `test:worldgen` | — | 38 tests, 18 s in-test, 33.6 - 33.8 s wall |
| `test:nobuild` | — | 54.6 s wall |

### G - server GC for the default and world generation tasks

`mise --version` is 2026.9.1 and that version supports a task-level `env` table, so no inline prefix was needed. Verified with a scratch task outside the repo (`run = "echo GC=$DOTNET_gcServer"`, `env = { DOTNET_gcServer = "1" }` printed `GC=1`), and also that a task `env` value wins over an inherited one (exporting `DOTNET_gcServer=0` still ran the task with `1`).

Changed: `env = { DOTNET_gcServer = "1" }` on `test`, `test:nobuild` and `test:worldgen`. Deliberately not on `test:e2e` (long games, wants workstation memory behaviour) or `test:extractor` - those task definitions are byte for byte unchanged.

Reaches the test host? Sampled `/proc/<pid>/environ` of every `testhost` process while `mise run test` was running: all three test hosts had `DOTNET_gcServer=1`.

Measurable effect, same three projects, same filter, both in parallel, `--no-build`:

| GC mode | `Civ.Mods.Tests` in-test | Wall |
|:--|--:|--:|
| workstation (`DOTNET_gcServer` unset) | 2 m 18 s | 141.6 s |
| server (`test:nobuild`) | 51 - 57 s | 54.6 s |

That is the whole 2.6x; nothing else differs between the two runs except background load. Note for future comparisons: `DOTNET_gcServer=0 mise run test` does **not** give the workstation result, because the task `env` overrides it - run `dotnet test` directly.

### J - build once, parallel projects, `test:nobuild`

`test` is now just `dotnet build Civ.slnx` followed by `mise run test:nobuild`, so the build happens exactly once and the orchestration lives in a single place. `test:nobuild` starts one `dotnet test --no-build` host per project (`Civ.Tests`, `Civ.Mods.Tests`, `Civ.ContentImport.Tests`), buffers each project's output to its own temp file and prints it as soon as that project finishes, so the three `Passed!` summaries stay readable instead of interleaving. Exit code: each project's status is written to a `.rc` file and the task exits 1 if any of them is non-zero.

Exit-code path was tested with a deliberately missing project path: `sh /tmp/failtest.sh` printed both passing summaries plus `FAILED project: DoesNotExist` and exited 1. `mise` propagates that (checked: a scratch `run = "exit 7"` task exits 7).

Is parallel actually a win? Yes, but the projects are small compared to `Civ.Mods.Tests`, so the gain is bounded:

| Arrangement (all with server GC, all with a single build step) | Wall |
|:--|--:|
| serial, build once then three projects one after another | 72.1 s |
| parallel (`mise run test`) | 61.9 s / 64.4 s |
| parallel, no build (`mise run test:nobuild`) | 54.6 s |

Parallel buys about 10 s (~14 %). The no-op build costs 6 - 7 s of that, which is what `test:nobuild` is for. Concurrency cost on the big project is small: `Civ.Mods.Tests` alone is 52 s, and it is 51 - 57 s while the other two run beside it.

Three processes now write `.test-metrics/test-runtimes.jsonl` at the same time; the logger's cross-process named mutex handled it, no failures and no truncated log observed.

### C - world generation tests in their own category

Tagged with `[Trait("Category", "WorldGen")]` (38 cases):

| File | Method | Cases |
|:--|:--|--:|
| `WorldRulesTests.cs` | `Generators_are_deterministic_habitable_and_have_valid_layers` | 18 |
| `WorldLayoutTests.cs` | `Layouts_keep_their_landmarks_and_supply_separated_starts` | 11 |
| `CliffRulesTests.cs` | `Generated_cliffs_have_beaches_and_open_river_mouths` | 6 |
| `BonusModelTests.cs` | `Generated_resources_respect_their_terrain_and_shore` | 3 |

Searched for other tests whose primary subject is generator output (`MapGenerator` set away from the default, or `MapGenerators` read). Only `WorldLayoutTests` reads the registry, and it is tagged. Declined, with reasons:

- `DogfoodContractTests` (registers `alternate:*` generators): asserts the settings and registry contract of the dogfood ruleset, not the shape of a generated map.
- `WorldExtensionTests`, `CultureRulesTests`, `GovernmentPeoplesTests`, `ResearchEconomyTests`, `AiTests`, `WorldRulesTests.Standard_world_supports_its_default_player_count...`: use `GameFactory.Create`/`Scenario` or a default-generator `Simulation` as setup for a different rule. These keep running by default, as instructed.
- `TerrainIntegrationTests`, `TerrainMeshTests`, `RiverNetworkTests`: they do grade a generated world, but the subject is meshing/rendering, not generation quality.

Default filter is now `Category!=Extractor&Category!=E2E&Category!=WorldGen` with an updated description. `test:e2e` and `test:extractor` filters are untouched (E2E 7 cases, extractor 5 cases, same as before).

Counts:

| Filter | `Civ.Tests` | `Civ.Mods.Tests` | `Civ.ContentImport.Tests` | Total |
|:--|--:|--:|--:|--:|
| before (`Category!=Extractor&Category!=E2E`) | 122 | 1175 | 37 | 1334 |
| after (default filter) | 122 | 1137 | 37 | 1296 |
| `Category=WorldGen` | 0 | 38 | 0 | 38 |

`--list-tests` agrees on the delta (`Civ.Mods.Tests` 1175 -> 1137, `Category=WorldGen` 38). One oddity that predates this work: `dotnet test --list-tests` lists 117 `Civ.Tests` tests while a run reports 122 for the same filter. Since both filters list 117, the delta above is unaffected; the table uses run counts.

`test:worldgen` = `dotnet test Civ.slnx --filter 'Category=WorldGen'`, measured twice: 33.60 s and 33.82 s wall, 38/38 passed, 18 s in-test. Most of the 15 s outside the test time is the incrementality-checked build plus three test-host startups, not world generation.

### K - metrics log cap

`tools/Civ.TestMetricsLogger/CivTestMetricsLogger.cs`: `MaxFileBytes` 5 MB -> 10 MB. Nothing else in the file changed.

### Documentation updates

`AGENTS.md` gained one paragraph: `mise run test` excludes world generation tests, `mise run test:worldgen` is required when the work set touches world generation (`src/Mods/World/`, map generator registrations, `MapSpec`/`HexGrid` layout, terrain/relief generation, river and cliff generation), and `mise run test:nobuild` reruns the default tests on the current build.

## Overall results and verification

### Initial verification

`mise run test`: 1296/1296 green in 63.3 s (build 6-7 s, `Civ.Mods.Tests` 55 s, `Civ.Tests` 9 s, `Civ.ContentImport.Tests` 0.4 s).

**Net: 178.6 s -> 63.3 s (-65 %).** Per-test cost of the slowest classes fell 53 %, the default set is 38 world-generation cases lighter, and the remaining hot spots are `AiEvaluationTests` (CLI) and `Civ.Tests` (real games).

### Final rerun (post-compaction, machine load 4.29)

Run against the finished tree, same box and same background load as the 178.6 s baseline, so the comparison is like for like.

| Command | Wall | Result |
|:--|--:|:--|
| `mise run test` run 1 | 61.4 s | 1296/1296 |
| `mise run test` run 2 | 57.5 s | 1296/1296 |
| `mise run test:nobuild` | 57.8 s | 1296/1296 |
| `mise run test:worldgen` | 33.9 s | 38/38 |

Inside `mise run test`: build 6 - 7 s, `Civ.Mods.Tests` 49 - 53 s, `Civ.Tests` 9 s, `Civ.ContentImport.Tests` 0.4 s. Nothing left to attack without changing test intent: `Civ.Mods.Tests` is now dominated by real simulations and the `AiEvaluationTests` CLI, and `Civ.Tests` is 122 real-game tests in 9 s.
