# Performance work log

## Current status

All three requested optimization phases are complete. The changes are uncommitted. The final default test suite passes: 794 passed, 0 failed, 0 skipped.

## Goal and order
Work on these areas one at a time, measure each change, and preserve deterministic game state and rendering:

1. Reduce map renderer rebuilds after actions.
2. Reduce startup content hashing cost without changing content hashes.
3. Move terrain geometry preparation off the main thread while keeping Godot resource creation and scene updates on the main thread unless the API explicitly supports otherwise.

## Current assessment

### Map renderer invalidation

`MapRenderer.Refresh` has separate world and simulation revision checks. A world revision rebuilds all baked land layers, while a simulation revision rebuilds tokens and overlays. `TerrainRenderer` already compares per-chunk signatures and rebuilds only dirty terrain chunks. This leaves the non-terrain land, improvement, city, token, label, and overlay work as likely sources of avoidable rebuild cost. First establish which actions increment each revision and measure representative actions before changing invalidation.

### Startup hashing

`ContentDb.Freeze` now serializes definitions as UTF-8 fragments inside the aggregate, without separate per-definition JSON trees. Each definition retains its runtime type, serializer options, and root writer. `CanonicalJson.Digest` hashes canonical UTF-8 bytes directly. The canonical writer, ordering, and numeric rejection remain unchanged.

### Terrain geometry threading

`TerrainRenderer.RebuildWorld` now prepares managed surface packets in the existing mesher workers. Godot array packing, resource construction, node assignment, scatter planning, and scatter updates stay on the main thread. The trace separates these stages.

### AI multithreading

#### Benchmark measurements

`mise run benchmark` reports single samples without timing thresholds. The Phase 1 run did not finish the 8-player preparation benchmark. It recorded these values before termination:

- World generation/setup, seed 1: 489.66 ms; 4,368 plots and 2 players.
- World generation/setup, seed 42: 249.24 ms; 4,368 plots and 2 players.
- World generation/setup, seed 987654: 335.52 ms; 4,368 plots and 2 players.
- Fifty-city turn: 83,120.65 ms; 50 cities, turn 2.
- The 8-player preparation (generation, 99 turns, save/load) exceeded 90 minutes of CPU time without finishing. It was terminated as instructed. No turn-100 AI decision result was captured.

After the AI multithreading implementation, one `mise run benchmark` run recorded:

- World generation/setup, seed 1: 482.04 ms; 4,368 plots and 2 players.
- World generation/setup, seed 42: 238.73 ms; 4,368 plots and 2 players.
- World generation/setup, seed 987654: 347.23 ms; 4,368 plots and 2 players.
- Fifty-city turn: 2,139.04 ms; 50 cities, turn 2.
- 8-player preparation (generation, 99 turns, save/load): 231,096.19 ms (3 minutes 51.10 seconds).
- 8-player `RunAi()` decisions at turn 100: 1,802.43 ms; 41 cities and 114 units.

The fifty-city turn decreased by 97.43%, or 38.86× against the measured baseline. A valid speedup ratio is not available for 8-player preparation: the baseline was an incomplete CPU-time observation, while the post-change run completed and reported a benchmark duration. There is no turn-100 `RunAi()` baseline for comparison.

#### End-to-end tests

Phase 1 did not run `mise run test:e2e`, so there is no pre-change E2E duration or speedup comparison. The post-change command was `mise run test:e2e -- --logger 'console;verbosity=detailed'`. The two AI tests reported:

- `Four_computer_players_develop_and_fight_over_200_turns`: failed after 1 minute 41 seconds, at turn 76, with `InvalidOperationException: Unit cannot attack` from the explorer/scout movement path.
- `Identical_games_and_a_midgame_save_have_equal_hashes_at_every_turn`: passed in 10 minutes 6 seconds.

The complete E2E run took 11.8656 minutes (about 11 minutes 52 seconds) and finished with 3 tests passed and 1 failed. Therefore this run did not verify the full AI gameplay test suite.

The latest default-suite evidence is `/tmp/civ-p4-default.log`: 725 passed and 5 previously documented failures out of 730 tests in `Civ.Mods.Tests`; `Civ.Tests` passed 124/124 and `Civ.ContentImport.Tests` passed 37/37. `mise run test` was not rerun during this measurement task.

#### Known wave-independent gameplay failure

Independent reproductions supplied during review establish that the turn-76 failure predates speculative turns. These reproductions were not rerun for this correction.
An `onlyDefensive` scout discovers a hostile unit partway through a multi-step movement command.
`ExplorerPlanner` reaches `MovementService.MoveStep`, then `CombatService.ValidateCombat` rejects the attack with `InvalidOperationException: Unit cannot attack`.
This gameplay defect remains deferred. Movement, combat, and existing E2E expectations are unchanged.

Reproduction configurations:

- Base `23db29c`: about 1 hour 15 minutes to the failure.
- `bee661e`, `AiWaveLimit = 1`: the same failure.
- `c67b88f`, `AiWaveLimit = 1`, `DOTNET_PROCESSOR_COUNT=1`: about 1 minute 41 seconds to the failure.
- Speculative `c67b88f`: the same failure at turn 76.

The states at turns 26, 51, and 76 were bit-for-bit identical across the compared runs.
At turn 76, the four players had cities 5/5/5/1, technologies 28/24/25/26, and units 11/11/12/6. The war count was 1.
The approximate 45× comparison measures time to the same failure. It is not a successful 200-turn E2E speedup or a benchmark ratio.
This failure is separate from the five pre-existing city-capture failures in the default suite.

#### Global aggregate safety correction

The earlier distant-creation optimization was unsafe. Spatial footprints do not protect global entity reads in war and settlement planning.
Wave validation now rejects unit or city counter changes, entity membership or ownership changes, and changes to population or power inputs.
Unit type, health, experience, and promotions participate in this guard. City positions, buildings, and extensions also participate.
Ordinary unit movement and local activity retain spatial validation instead of forcing global conflicts.

Production completion and growth run in `AdvanceTurn` systems before `RunAi`. Ordinary production selection does not create a unit during a wave.
However, founding consumes a settler and creates a city during a command. Disbanding, upgrades, combat, capture, and great-person actions also change entities during commands.
The correction therefore protects global reads regardless of the command that changed an entity.
The existing measurements describe the earlier commit, not the corrected guard. No new performance ratio follows from this correction.

## Running log

### Started

- Confirmed the working tree was clean after commit `be04c49`.
- Inspected `MapRenderer.Refresh`, `TerrainRenderer.RebuildWorld`, `ContentDb.Freeze`, and the renderer mesh builder.
- At the start, no changes to the three optimization targets had been made.
- `GameSession.Submit` increments `WorldRevision` for every successful command, not only map-visible changes; selection increments `Revision` alone. `MapRenderer` therefore treats unrelated successful actions as world changes.
- Terrain mesh signatures already avoid rebuilding unchanged terrain chunks. `TerrainRenderer` also uses `Parallel.For` for terrain/river data generation, so later terrain work must target measured main-thread conversion/upload cost or improve the pure-data phase rather than duplicate existing threading.
### Renderer rebuild reduction

- Status: completed a narrow renderer change. Successful commands still conservatively invalidate land. Startup hashing and terrain threading remain untouched.

#### Change and invalidation

- `TerrainRenderer.RefreshWorld` keys the perceived snapshot on `GameSession.WorldRevision`, not `Revision`.
- Selection, messages, and rejected commands no longer rebuild terrain snapshots, signatures, height fields, or terrain textures.
- World revisions still refresh perceived terrain, dirty chunks, tileA, and scatter visibility before map layers sample heights.
- `MapRenderer` uploads tileB once after `BuildOverlay` writes current marks. Hover still uploads tileB directly.
- Tokens, labels, and selection-dependent city overlays retain their existing revision checks.
- `BuildLand` retains its independent world-revision check. Zero dirty terrain chunks never suppress land rebuilds.
- The performance trace now includes the final overlay upload and reports fractional milliseconds.

#### Measurement

- Used a Debug build on the NVIDIA GTX 1050 Ti with Vulkan Forward+.
- Both launches used this command, with `LABEL` replaced by `before` or `after`:

  ```sh
  mise exec -- godot-mono --path src/Civ.Godot -- --stage map --world tiny --seed 1 --opponents 0 --screenshot /tmp/civ-render-LABEL.png --frames 100000 --trace-performance
  ```

- The screenshot option opens the debug map. Both processes stopped before the screenshot frame limit.
- After the initial rebuild, sent ten Tab presses followed by ten Escape presses, spaced 1.2 seconds apart:

  ```sh
  for key in Tab Escape; do
    for i in $(seq 1 10); do
      xdotool key --window 8388614 "$key"
      sleep 1.2
    done
  done
  ```

- The window ID applies only to these local launches. Each sample contains 20 map refreshes on a map with 117 terrain chunks.
- Before: 20 snapshot/signature rebuilds, all with zero dirty chunks. The terrain stage median was 8 ms, with a 2–14 ms range.
- After: zero snapshot/signature rebuilds. The terrain stage reported 0.00 ms for all 20 actions at trace precision.
- The final overlay upload median was 0.11 ms. Its cost was absent from the old trace.
- Token-stage medians were 4.5 ms before and 4.785 ms after. This change does not optimize token or UI rebuilds.
- This demonstrates removal of terrain work, not a general frame-rate or successful-command speedup.
- Local samples: `/tmp/civ-render-before-sample.txt` and `/tmp/civ-render-after-sample.txt`.

#### Safety evidence and rejected approaches

- `GameCommand.Apply` can change arbitrary simulation state. Synchronous event subscribers can also change state. Neither contract supplies complete layer-specific changes.
- Therefore, command-name classification is not safe without a wider invalidation contract. Queue and assignment commands still increment the conservative world counter.
- Rejected terrain dirtiness as a land invalidation signal. Signatures omit routes, resources, improvements, sight, city population/ownership, player era, and layout inputs.
- An explicit land signature needs a separate dependency audit. No speculative land cache or command whitelist was added.
- Live smoke: founding rebuilt 8/117 terrain chunks, then land. A later turn rebuilt land despite zero dirty terrain chunks.
- Live smoke: selection and an unavailable founding action refreshed tokens/overlays without terrain work. No engine errors appeared.
- Movement, fog, turn, and load paths retain world-revision invalidation. Existing visibility and persistence tests passed.
- Test authoring gate: no tests were added or changed. Engine-free tests cannot exercise renderer uploads; a private-counter test needs an unjustified production seam.
- Used the real engine trace for renderer behavior and existing owner-boundary tests for visibility, persistence, scatter, and session behavior.

#### Validation

- Before and after: `mise run build:game` passed with zero warnings and zero errors.
- Before and after: `mise run test` passed all 784 tests: 97 core, 650 mod/client, and 37 content-import tests.
- Focused command passed all 62 tests:

  ```sh
  mise exec -- dotnet test tests/Civ.Mods.Tests/Civ.Mods.Tests.csproj --no-restore --filter 'FullyQualifiedName~ClientSessionTests|FullyQualifiedName~TerrainIntegrationTests|FullyQualifiedName~SightMemoryTests|FullyQualifiedName~ScatterPlannerTests'
  ```

- `git diff --check` passed.
- No configured complexity threshold or local Lizard installation was available. Manual branch counts: map refresh 9 → 8, terrain refresh 6 → 2.
- The new overlay upload method has complexity 1. No terrain meshing or threading code changed.

### Startup hashing

- Status: complete. This phase changed only content hashing, digest output buffers, compatibility tests, and this log.
- No renderer or terrain implementation changes belong to this phase. Terrain threading remains unstarted.
- Supplied baseline trace: hash and registry freeze 1,322.23 ms, total freeze 1,376.60 ms, first catalog load 2,403.38 ms.
- Supplied `--startup` baseline: cold catalog 2,578.43 ms, paired serial/preparation medians 648.09/600.21 ms, about 315.6 MiB per build.
- Exact content hash before and after: `D2A65A5162647FA8CA9D3837EC5666C6283B9682EA1AD8FB17F0DD1204DE948E`.

#### Change and compatibility

- A private definition converter replaces the retained per-definition `JsonElement` trees with temporary UTF-8 fragments.
- Each fragment uses `JsonSerializer.SerializeToUtf8Bytes` with the original explicit runtime type and the same serializer options.
- Each definition still receives a separate root writer. Custom converters retain their original writer depth and serializer scope.
- The aggregate serializer inserts the fragments without another validation pass. `Digest` parses the aggregate into a JSON tree before canonicalization.
- The aggregate schema, registry-type order, definition-id order, array order, and extension identities stay unchanged.
- `Digest` uses `ArrayBufferWriter<byte>` and hashes its written span after the canonical writer flushes.
- This removes the output copy, UTF-8 decode, and UTF-8 re-encode from the old digest path.
- `Serialize`, `Normalize`, the recursive canonical writer, shared options, and all public signatures stay unchanged.
- Integer normalization still uses the same signed/unsigned 64-bit checks. Fractional, exponent-form, and out-of-range numbers still fail.
- The aggregate JSON tree remains. This change does not introduce a hash cache or parallel hashing.

#### Repeatable measurement

The local comparison used two fresh processes before and two after, with this exact command:

```sh
CIV_TRACE_STARTUP=1 mise run benchmark -- --quick
```

The benchmark uses Release builds, one initial catalog load, two variant warmups, and five alternating serial/preparation pairs per process.
Both warmups and all measured catalog builds must match the first content hash. World workloads do not enter the catalog measurements.

- First-load hash and registry freeze: 1,245.60/1,335.51 ms before, 876.41/952.69 ms after.
- First-load total freeze: 1,295.88/1,387.86 ms before, 934.04/1,006.43 ms after.
- First catalog load: 2,577.14/2,559.58 ms before, 2,085.44/2,112.62 ms after.
- Across 20 measured rebuilds per version, the hash/freeze median fell from 365.31 to 266.43 ms: 27.1% less time.
- The corresponding total-freeze median fell from 397.75 to 284.36 ms: 28.5% less time.
- Per-build allocation fell from about 315.61 to 257.35 MiB: 58.26 MiB, or 18.5%, less allocation.
- Paired serial catalog medians: 608.16/651.13 ms before, 533.52/535.16 ms after.
- Paired preparation catalog medians: 613.00/658.73 ms before, 482.90/620.13 ms after.
- These are local samples, not timing thresholds. JIT, garbage collection, host load, and filesystem caches affect catalog timings.
- All four processes produced the exact baseline content hash for the initial load, both warmups, and every measured rebuild.

Local trace files:

- `/tmp/civ-hash-before.log` and `/tmp/civ-hash-before-repeat.log`.
- `/tmp/civ-hash-final.log` and `/tmp/civ-hash-final-repeat.log`.

#### Approaches that did not work

- Replacing each element with an `object` cast preserved the shipped hash, but changed explicit runtime-type dispatch to object polymorphism.
- Rejected that shortcut because custom serializer options can change object conversion.
- A direct-to-parent-writer converter reduced allocation further, but changed the writer depth visible to definition converters.
- The compatibility fixture failed both registration-order cases with that prototype and passed with the original code and final implementation.
- Retained separate root serialization despite about 10.3 MiB more allocation than the direct-writer prototype.
- Direct hashing from a `MemoryStream` removed text conversions, but retained stream staging and copying.
- In otherwise identical intermediate prototypes, `ArrayBufferWriter<byte>` reduced allocation from about 256.76 to 247.01 MiB per build.
- No isolated timing claim applies to that buffer comparison. Final allocation includes the separate root serialization cost.

#### Tests and validation

- Test-audit gate: existing tests cover hash sensitivity, registry freezing, and save compatibility, but not exact digest bytes or converter root scope.
- Added ten cases at the public digest and mod-loading boundaries. They need no production test hooks.
- Digest cases cover nested ordinal keys, duplicate-key stability, array order, escaping, signed/unsigned limits, and numeric rejection.
- A fixed content-hash fixture covers runtime converters, root writer depth, mod identity, core command identity, and reversed registration order.
- All new cases passed with the original freeze/digest path and the final implementation.
- The focused final command passed 40 tests:

  ```sh
  mise exec -- dotnet test tests/Civ.Tests/Civ.Tests.csproj --no-restore --filter 'FullyQualifiedName~CanonicalJsonTests|FullyQualifiedName~ContentHashTests|FullyQualifiedName~ContractTests|FullyQualifiedName~ModPreparationTests'
  ```

- `mise run test` passed all 794 default tests: 107 core, 650 mod/client, and 37 content-import tests.
- Both builds passed with zero warnings and zero errors:

  ```sh
  mise run build:game
  mise exec -- dotnet build tools/Civ.Benchmarks/Civ.Benchmarks.csproj --configuration Release --no-restore
  ```

- `git diff --check` passed. No commit was made.

### Terrain geometry off-thread

- Status: implementation complete. This phase changes only terrain preparation, terrain timing logs, and this document.
- The completed renderer invalidation and content hashing implementations remain unchanged.
- Supplied baseline: 672/672 chunks, mesh stage 556–571 ms, conversion/upload/scatter stage 1,262–1,898 ms.
- Supplied terrain and map-refresh medians: about 2,119 ms and 2,327 ms.

#### Change and thread boundary

- The existing `Parallel.For` now produces one managed surface packet per dirty chunk.
- Workers convert ground, water, and river attributes into managed `Vector3[]`, `Vector2[]`, and `Color[]` arrays.
- Workers also compute wet masks and clockwise front-face indices. All numeric expressions and iteration orders remain unchanged.
- Water preparation reads the captured field, snapshot, and warp grid. `NearWater` does not write caches or access engine objects.
- Each worker writes a separate result slot. The loop joins before the main thread consumes packets in dirty-chunk order.
- Raw mesher arrays remain local to each worker. The consumer clears packet slots after retrieval to limit retained memory.
- Timing-only arrays and stopwatch calls are gated by `MapRenderer.LogTimings`; normal refreshes do not pay for per-chunk diagnostic timing.
- Godot `Variant` conversion and `Godot.Collections.Array` construction remain on the main thread.
- `ArrayMesh` construction, `AddSurfaceFromArrays`, materials, triangle statistics, and mesh assignments remain on the main thread.
- Scatter planning, model access, `MultiMesh` updates, wrap nodes, textures, and visibility updates also remain on the main thread.
- No rendering thread settings, global caches, new background tasks, public APIs, or test hooks were added.

Safety sources:

- [Godot thread-safe APIs](https://docs.godotengine.org/en/latest/tutorials/performance/thread_safe_apis.html): active scene-tree access is not thread-safe.
- The same guide warns about resource sharing and GPU synchronization. No resource construction moved to workers.
- Inspected Godot source at the installed engine revision `ed1daf0bf`, under `modules/mono/glue/GodotSharp/GodotSharp/Core/`.
- Numeric constructors in `Vector2.cs`, `Vector3.cs`, and `Color.cs` only assign value-type fields. `Vector3.Up` returns a readonly managed value.
- `ScatterPlanner` writes appearance and model-count dictionaries. `TerrainPalette.ScatterFor` calls mod appearance services.
- `GameCatalog.ScatterModels` also writes a dictionary and enumerates model files. These paths have no concurrent-call contract.
- A separate planner per worker would not protect the shared catalog or mod services. Scatter planning therefore remains serial.

#### Measurement method

The local comparison used Debug builds, Godot 4.7.2, Vulkan Forward+, and the NVIDIA GTX 1050 Ti.
Each version ran in three fresh processes with a huge world, seed 1, zero opponents, and 672 dirty chunks.
The serial baseline uses the same packet split and timing probes, but prepares packets on the main thread.
An unchanged-source launch also completed with a valid screenshot: mesh 589 ms, combined upload stage 1,228 ms.

The desktop locked during measurement, and direct display runs stalled after the initial refresh.
An isolated X11 display avoided that presentation stall without changing the GPU or renderer.
All compared runs used this command, with a distinct `LABEL`:

```sh
xvfb-run -a -s "-screen 0 1280x720x24" mise exec -- godot-mono \
  --display-driver x11 --path src/Civ.Godot -- \
  --stage map --world huge --seed 1 --opponents 0 \
  --screenshot /tmp/civ-terrain-thread/LABEL.png --frames 30 --stats --trace-performance
```

The initial map view contains mostly hidden terrain. It matches the supplied workload, not a fully explored world.
The trace separates input construction, worker wall time, managed preparation, native packing, surface creation, assignment, and scatter work.
`mesh sum` and `prepare sum` add elapsed intervals across workers. They include scheduling and GC pauses, not CPU time.
Those sums must not be added to worker wall time.

Three-run medians, in milliseconds:

| Stage | Serial preparation | Worker preparation |
| --- | ---: | ---: |
| Snapshot, field, signatures, resolutions | 71.01 | 68.33 |
| Worker wall time | 526.97 | 614.45 |
| Managed preparation on main thread | 205.81 | 0 |
| Managed preparation, summed worker intervals | not applicable | 468.95 |
| Native array packing | 19.64 | 30.64 |
| Resource construction, surface upload, material | 104.64 | 116.91 |
| Statistics and node assignment | 9.20 | 9.30 |
| Scatter planning | 283.14 | 286.50 |
| Scatter rebuilding | 676.80 | 657.91 |
| tileA | 0.98 | 1.12 |
| Terrain total | 1,932.71 | 1,792.75 |
| Map refresh, sum of traced stages | 2,147.20 | 1,944.26 |

- Terrain total decreased by 139.96 ms, or 7.2%, at the median.
- Terrain ranges: 1,849.21–1,994.39 ms before, 1,709.64–1,873.52 ms after.
- Serial preparation took 197.33–213.42 ms. Worker wall time increased because it now includes preparation.
- Preparation does not become free. Concurrent allocation, GC, and scheduling increase the summed elapsed preparation intervals.
- Token-stage medians also changed from 149.58 to 102.26 ms. No token code changed; the full map delta is not an isolated terrain speedup.
- Native surface calls do not dominate this workload. Scatter rebuilding remains the largest serial stage.
- These samples are local observations, not performance thresholds or a frame-rate claim.
- A later final-build smoke took 1,935.10 ms for terrain and 665.04 ms for workers. This overlaps the serial range.
- That smoke confirms runtime correctness, not the three-run median benefit. Host load, JIT, GC, and model import costs remain variable.

A separate fully visible huge-world pair used `--stage rivers --clean` instead of `--stage map`, without diagnostic hashing.
Both runs used the same seed, opponents, GPU, display, and 30-frame capture settings.
This pair is a smoke measurement, not a median:

- Terrain total: 14,718.92 ms serial, 14,015.57 ms with worker preparation.
- Managed preparation: 920.04 ms serial, 1,736.46 ms summed worker intervals.
- Worker wall time: 5,533.85 ms serial, 5,786.07 ms with preparation.
- Native packing: 56.88/70.70 ms. Surface construction: 421.11/419.99 ms.
- Scatter planning: 5,529.20/5,676.42 ms. Scatter rebuilding: 1,881.91/1,686.82 ms.
- Map refresh, sum of traced stages: 15,644.78/14,951.48 ms.
- The large serial scatter cost limits the total benefit. No general fully explored-world speedup follows from one pair.
- These traces are `revealed-serial.log` and `revealed-cleanparallel.log`, with adjacent screenshots and statistics.

#### Approaches rejected or limited
- Rejected concurrent `ScatterPlanner.Plan` calls because its caches, shared catalog, and mod callbacks are not thread-safe.
- Rejected worker-side Godot arrays, meshes, materials, nodes, and model loading. No measured result justifies expanding that safety boundary.
- No chunk scheduling, geometry resolution, density, thinning, or candidate-order changes were made.
- All prepared geometry packets are built before serial resource uploads. This keeps the worker join simple and ordered, but peak temporary memory was not measured; bounded batches remain a possible follow-up if profiling shows memory pressure.
- Luna's review found no worker-thread race or geometry/index semantic change. It flagged the always-on timing overhead, which was fixed by gating diagnostics behind `MapRenderer.LogTimings`.
- Did not add another parallel mesher pass. Preparation follows each chunk's mesher work in the existing pass.
- The first temporary comparison build reused an old assembly because its external source override ran after MSBuild's dependency cache.
- Discarded that comparison. Moved the temporary override earlier and forced recompilation before the byte comparison.
- All diagnostic sources and build overrides live under `/tmp/civ-terrain-thread`, not in the shipped project.

#### Validation

- `mise run build:game` passed before and after with zero warnings and zero errors.
- `mise exec -- dotnet build src/Civ.Godot --configuration Release` passed with zero errors.
- Release reported two existing CS1573 warnings for missing constructor parameter comments. This phase does not change that constructor.
- `git diff --check` passed. No commit was made.
- No tests or source contracts changed. Per the task boundary, default tests were not rerun for this phase.
- Temporary runtime diagnostics hashed every uploaded surface attribute and ordered scatter candidate list in the real engine.
- The reference used the original terrain implementation, not a second copy of the new preparation helpers.
- Fully visible huge-world runs matched all 2,665 ordered records: 672 chunk markers, 1,321 surface hashes, and 672 scatter hashes.
- The record-sequence SHA-256 was `B53018458D3645BBAFB800D73968D4401FF687B15CCE56A087403C1766F9E05B` for both versions.
- Geometry hashes include array lengths and raw bytes for positions, normals, UV, UV2, colors, and indices.
- Scatter hashes include each candidate's set, model index, position, yaw, scale, and tile in original order.
- Both runs reported 3,992,256 terrain triangles. Planned/kept scatter counts, height statistics, and other triangle counts matched for all four poses.
- The normal-view statistics also matched across all six timed runs, including 1,744,992 terrain triangles.
- Inspected paired river, wrap, mountain, and forest screenshots. Terrain boundaries, river banks, prop placement, and the wrap seam remained consistent.
- Screenshots are not pixel-identical because water, foliage, and temporal rendering depend on frame time. Geometry hashes provide the exact-byte check.
- No terrain errors or exceptions appeared. Xvfb reported an input-method warning and a DRI3 presentation notice in both versions.
- Temporary byte-hash diagnostics and external build overrides are absent from shipped source. The existing performance trace remains opt-in.
- After all three phases, `mise run test` passed 794 tests with 0 failures and 0 skipped in 122 seconds.
- Lizard reported `RebuildWorld` complexity 4 → 4, water preparation 7 → 7, and river preparation 3 → 3.
- The shared resource builder changed from 3 to 5 because it now handles optional river attributes. No touched method exceeds complexity 10.

Local evidence lives under `/tmp/civ-terrain-thread/`:

- `serial-{1,2,3}.log` and `parallel-{1,2,3}.log`: uninstrumented timing runs and adjacent PNG/JSON captures.
- `validation-original.log` and `validation-parallel.log`: original-versus-worker byte comparisons. These timings include diagnostic hashing and are not performance samples.
- `validation-original/` and `validation-parallel/`: four fully visible huge-world poses and their statistics.
- `validation-compare.png`: paired poses, with the reference on the left and the worker version on the right.
- `complexity.log`: same-analyzer before/after complexity results.
- `final-debug-build.log` and `final-release-build.log`: normal builds without external source overrides.
- `final-smoke.log`, PNG, and JSON: final normal-build capture without diagnostic hash code.
