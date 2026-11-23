# Performance and Optimization Log

This document provides instructions for executing operational benchmarks and profiling
workloads, followed by a chronological record of performance optimizations implemented
across the codebase.

______________________________________________________________________

## Benchmarking and Profiling Instructions

### Operational Benchmark Workloads

Benchmarks are executed via `mise run benchmark` with flags selecting specific
scenarios:

```sh
# 1. Quick startup check (standard world, seed 42, 4 players)
mise run benchmark -- --quick

# 2. Expanded startup check (standard and gigantic worlds, seeds 1, 42, 987654, 4 players)
mise run benchmark -- --startup

# 3. World generation and session startup (isolates generation and session start, skips catalog rebuilds)
mise run benchmark -- --world

# 4. Operational turn and AI workloads (8-player game, 99-turn preparation, turn-100 decisions)
mise run benchmark

# 5. Lockstep state hash (fresh gigantic world, then standard 8-player game at turn 100)
mise run benchmark -- --hash

# 5b. State copy for AI forks (standard 8-player game at turn 100)
mise run benchmark -- --clone

# 5c. What a turn allocates, by type (same game, turns 100-104)
mise run benchmark -- --allocations

# 6. Whole AI turns on a gigantic world (8 players, turns 21-30 timed, final hash printed)
mise run benchmark -- --gigantic

# 7. Borders phase on the standard 8-player game at turn 100 (hashes before and after printed)
mise run benchmark -- --borders
```

#### Workload Descriptions

- **`--quick`**: Loads the catalog once, performs paired serial/preparation catalog
  rebuilds, generates a standard world (seed 42), starts the session, founds a city, and
  measures cold/warm production queries.
- **`--startup`**: Performs paired serial/preparation catalog rebuilds, then measures
  world generation, session startup, city founding, and production choices across
  standard ($84 \\times 52$) and gigantic ($200 \\times 120$) worlds for seeds 1, 42,
  and 987654.
- **`--world`**: Loads the catalog once and focuses exclusively on world generation
  (plot allocation, terrain, rivers, features, bonuses, goodies) and `GameSession.Start`
  (including initial opponent AI setup) across standard and gigantic maps for seeds 1,
  42, and 987654. It skips catalog rebuilds, founding, and turn simulation.
- **Default (no flag)**: Runs multi-turn simulation benchmarks, including the 50-city
  turn workload and the 8-player scenario with turn-100 AI decision evaluation
  (`decisions.RunAi()`).
- **`--hash`**: Times 100 `Simulation.ComputeHash` calls on a fresh eight-player gigantic
  world, then on an eight-player standard world played to turn 100, and reports time,
  allocated bytes and gen-2 collections per hash.
- **`--clone`**: Times 50 `GameState.Clone` calls on an eight-player standard world played
  to turn 100, the part of each spent copying extension components through their JSON
  contract, and five whole turns to set both against.
- **`--allocations`**: Plays the same game through turns 100-104 and lists the 40 types
  that allocate the most per turn, from the runtime's allocation samples. It names types,
  not call sites.
- **`--gigantic`**: Plays an eight-player AI game on a gigantic world, times turns 21-30,
  and reports time and allocated bytes per turn, gen-2 collections, and the final state
  hash, which must not change under a pure optimization.
- **`--borders`**: Plays the standard eight-player game to turn 100, then times the
  `cities:borders` phase (culture spread, plot ownership and work assignment) on fresh
  copies of that state, printing the state hash before and after.

### Profiling and Diagnostic Tracing

#### Startup Phase Tracing

To inspect elapsed timings for mod discovery, assembly loading, sidecar parsing,
validation, canonical hashing, world generation stages, and starting positions:

```sh
ARIEL_TRACE_STARTUP=1 mise run start:game
# Or with the benchmark harness:
ARIEL_TRACE_STARTUP=1 mise run benchmark -- --quick
```

#### Renderer Rebuild Tracing

To trace Godot renderer stage timings (snapshot capture, worker mesh generation, native
array packing, surface uploads, and scatter planning/rebuilds):

```sh
mise run build:game
mise exec -- godot-mono --path src/Ariel.Godot -- --stage map --world tiny --seed 1 --opponents 0 --trace-performance
```

The `--` separates Godot arguments from game runtime arguments.

#### Turn Profiler

To see where a long AI game's time goes, by turn system, AI planner, AI seam, command and
kernel step, with a per-turn curve:

```sh
# One game, profiled: report on stderr, files in the directory.
mise run ai:play -- --seed 3 --players 4 --turns 100 --profile /tmp/ariel/profile
# The same switch as an environment variable; ai:eval writes one profile per sample.
ARIEL_PROFILE=/tmp/ariel/profile mise run ai:play -- --seed 3 --turns 100
ARIEL_PROFILE=/tmp/ariel/eval-profile mise run ai:eval -- --seeds 1 --turns 100
# A later start, to reach late-game rules within 100 turns.
mise run ai:play -- --seed 3 --players 4 --turns 100 --era foundation:era_industrial --profile DIR
# Continue a saved late game for 40 more turns, profiled, and write where it ended.
mise run ai:play -- --load /tmp/ariel/ai330.json --turns 40 --profile DIR --save /tmp/ariel/out.json
```

The run prints `final turn T hash H` on stderr, so two builds continuing the same save can
be compared by hash as well as by their final saves (`mise run check:desync-diff`).

A host attaches a `SimulationProfiler` to `Simulation.Profiler`; nothing is measured, read
or allocated without one, and a profiled game plays exactly as an unprofiled one (tests in
`SimulationProfilerTests` and `AiPlannerTests`). The directory receives:

- `report.txt`: the run's wall time, allocation and collector pauses, the per-turn curve
  (first recorded turn, turn 10, every 25th, the last), then every entry sorted by cost:
  kind, total ms, calls, mean µs, MiB allocated by the measuring thread, id.
- `totals.csv`: the same entries, unrounded.
- `turns.csv`: one row per advance (`turn,wall_ms,systems_ms,ai_ms,allocated_bytes,
  gc_pause_ms,gen2,cities,units`). A row is the rollover into that turn plus that turn's
  AI moves; the first row includes JIT compilation.
- `turn-entries.csv`: every entry's calls, ms and bytes per turn, for finding what grows.

How to read the kinds: `TurnSystem` is each registered system by id (a player-interval
system counts one call per player); `AiPlanner` is each planner by id, per player;
`AiSeam` is each valuer and production wish source inside a planner; `Command` is each
top-level command type; `Rule` is a shared rules step nested inside its callers
(`modifiers:collect`, `world:sight_recompute`, `relations:discover_contacts`); `Kernel`
is the AI waves as a whole (`kernel:ai_turns`, wall time), their parallel planning
(`kernel:wave_plan`), direct turns, forks, commits and speculation bookkeeping, and
`kernel:state_hash`, which is the harness hashing each turn (lockstep peers pay it too).
Entries are inclusive and summed over every thread they ran on: AI planners, seams and
the commands AI players issue also run on parallel forks, and a wave's commit replays
those commands, so those sums are processor time and can exceed the wall time. A `Rule`
entry can also nest inside itself: a city's `modifiers:collect` that gathers a player
ledger counts the inner collection twice under the same id, so a `Rule` total is an upper
bound on that step's own cost.

`mise run benchmark` has no profiler switch. Profile with `ai:play` or `ai:eval`.

### Controlled Measurement Methodology

To ensure repeatable and reliable performance comparisons:

1. **Release Builds**: Always measure Release configurations
   (`dotnet build --configuration Release`).
2. **Disable Tiered JIT Transition Effects**: Set `DOTNET_TieredCompilation=0` so short
   microbenchmarks avoid dynamic tier-0 to tier-1 recompilation jitter:
   ```sh
   env -u ARIEL_TRACE_STARTUP DOTNET_TieredCompilation=0 mise run benchmark -- --world
   ```
3. **Disable Active Tracing**: Opt-in traces (`ARIEL_TRACE_STARTUP=1` or verbose
   loggers) introduce I/O overhead. Disable them during timing comparisons.
4. **Warmup and Sampling**: Use at least one warmup iteration followed by multiple
   sequential sample runs (typically 3 to 5 runs), reporting the median of medians
   rather than isolated runs.
5. **Exact Hash Verification**: Every benchmark must assert bit-for-bit determinism.
   Content SHA-256 hash, generated plot/RNG hash, session hash, and founded state hash
   must match the baseline across all samples.
6. **Allocation Tracking**: Track managed bytes using `GC.GetTotalAllocatedBytes(true)`,
   which queries precise runtime allocation counters without triggering a forced garbage
   collection.

______________________________________________________________________

## Chronological Optimization Timeline

```
[Pass 1: Oct 17, 2026] Engine Invalidation, Content Hashing, & Off-Thread Terrain
                       (Commit: 821d3a1)
       │
       ▼
[Pass 2: Oct 17, 2026] AI Multithreading & Speculative Parallel Waves
                       (Commits: d6d73ab, 0685342, b81909f, 4282ff6, f064004, 482ecc4)
       │
       ▼
[Pass 3: Oct 17, 2026] AI Serial Work Reduction & Spatial Indexing
                       (Commits: c339ee2, e6239f8, da5fa32, 9ff9998, 193567e, 3dc5495)
       │
       ▼
[Pass 4: Oct 17, 2026] World Generation Geometry & River Traversal
                       (Commit: 7b12754)
       │
       ▼
[Pass 5: Oct 18, 2026] Idle CPU: Terrain Ray Pick Caching & Unit Height Sampling
                       (Commit: 47b932f)
       │
       ▼
[Pass 6: Oct 18, 2026] Startup Latency: Theme Overlap & Lazy Content Hash
                       (Commit: 7f865e4)
       │
       ▼
[Pass 7: Oct 26, 2026] HUD Invalidation on Clicks & In-Place Panel Updates
                       (Commits: 877fb7a, 70ab679, 003cd30, 1ad1904, 4b6fd9d)
       │
       ▼
[Pass 8: Nov 11, 2026] Lockstep State Hash Without Copies
                       (Commits: a17020e, 4c63893)
       │
       ▼
[Pass 9: Nov 11, 2026] AI Threat Grid Reuse
                       (Commit: 4cb9780)
       │
       ▼
[Pass 10: Nov 11, 2026] Culture Reach Cached Across a Batch of Plots
                        (Commit: 4eb56de)
       │
       ▼
[Pass 11: Nov 12, 2026] Work Reassigned Only Where Borders Move
                        (Commit: reassign work only where borders move)
       │
       ▼
[Pass 13: Nov 22, 2026] Turn Profiler and Long AI Game Costs
                        (Commits: 5016ba5 .. 8c23637)
```

______________________________________________________________________

### Pass 1: Engine Invalidation, Content Hashing, and Off-Thread Terrain Preparation

- **Date**: 2026-10-17
- **Primary Commit**: `821d3a1` (*Optimize terrain geometry preparation, content
  hashing, and map invalidation*)

#### 1. Map Renderer Rebuild Reduction

- **Motivation**: `GameSession.Submit` increments `WorldRevision` on every successful
  command, while selection and UI events increment `Revision`. `MapRenderer` was
  rebuilding terrain height fields, signatures, snapshots, and textures even for
  non-world actions (e.g. selection, menu toggles, rejected actions).
- **Changes**:
  - In `src/Ariel.Godot/TerrainRenderer.cs` and `src/Ariel.Godot/MapRenderer.cs`,
    decoupled world terrain refreshes from non-world simulation revisions
    (`RefreshWorld` vs. `RefreshOverlay`).
  - Keyed terrain snapshot and signature rebuilds strictly on
    `GameSession.WorldRevision`.
  - Non-world actions (Tab, Escape, selection changes) no longer trigger terrain
    rebuilds.
  - Uploaded overlay marks (`tileB`) once after overlay generation without touching land
    geometry.
- **Measurements**:
  - Tested on tiny world with 117 terrain chunks under Vulkan Forward+ via 20 UI actions
    (10 Tab, 10 Escape):
    - **Terrain stage rebuild time**: dropped from **8.00 ms median** (2–14 ms range) to
      **0.00 ms** (zero snapshot/signature rebuilds).
    - **Overlay upload**: median 0.11 ms.
    - Token stage medians remained 4.5 ms before vs. 4.785 ms after (confirming terrain
      isolation).

#### 2. Startup Content Hashing Optimization

- **Motivation**: Startup profiling revealed that `ContentDb.Freeze` spent >1.3s
  creating intermediate `JsonElement` syntax trees for each definition and serializing
  full canonical JSON strings into `Digest` buffers.
- **Changes**:
  - In `src/Ariel.Core/Content/ContentDb.cs`, serialized content definitions as UTF-8
    byte fragments using a custom `HashDefinition` converter, eliminating intermediate
    `JsonElement` allocations while preserving runtime type dispatch and root writer
    depth.
  - In `src/Ariel.Core/Game/CanonicalJson.cs`, modified `Digest` to stream canonical
    UTF-8 directly into `ArrayBufferWriter<byte>` and hash the span directly, avoiding
    intermediate string allocations, UTF-8 decodes, and UTF-8 re-encodings.
- **Measurements**:
  - Measured across 20 catalog rebuilds
    (`ARIEL_TRACE_STARTUP=1 mise run benchmark -- --quick`):
    - **Catalog hash/freeze median**: fell from 365.31 ms to 266.43 ms (**27.1%
      reduction**).
    - **Total freeze median**: fell from 397.75 ms to 284.36 ms (**28.5% reduction**).
    - **Per-build memory allocations**: fell from 315.61 MiB to 257.35 MiB (**58.26 MiB
      / 18.5% reduction**).
    - **First catalog load**: fell from 2,568 ms to 2,099 ms median (**18.3%
      reduction**).
    - Content hash remained exactly bit-for-bit identical
      (`D2A65A5162647FA8CA9D3837EC5666C6283B9682EA1AD8FB17F0DD1204DE948E`).

#### 3. Off-Thread Terrain Geometry Preparation

- **Motivation**: `TerrainRenderer.RebuildWorld` spent significant main-thread time
  converting raw mesher vertex, normal, UV, and index data into Godot surface arrays.
- **Changes**:
  - In `src/Ariel.Godot/TerrainRenderer.cs`, parallelized ground, water, and river
    surface preparation across worker threads inside the existing `Parallel.For` loop
    using `SurfaceData` and `PreparedChunk` records.
  - Workers compute vertex positions, normals, UVs, colors, wet masks, and front-face
    indices concurrently.
  - The main thread exclusively handles native `Godot.Collections.Array` packing,
    `ArrayMesh` construction, material assignments, and scene tree node updates.
  - Gated diagnostic stopwatches and per-chunk logging behind `MapRenderer.LogTimings`.
- **Measurements**:
  - Measured across 3 runs on huge world (672 chunks, seed 1, zero opponents):
    - **Main-thread managed preparation**: dropped from **205.81 ms to 0.00 ms** (moved
      to workers, totaling 468.95 ms summed worker intervals).
    - **Terrain total time**: decreased from 1,932.71 ms to 1,792.75 ms (**139.96 ms /
      7.2% reduction**).
    - **Map refresh total**: decreased from 2,147.20 ms to 1,944.26 ms (**9.5%
      reduction**).
    - Native array packing and surface upload remained 124.28 ms before vs. 147.55 ms
      after.
    - Record-sequence SHA-256 matched identically
      (`B53018458D3645BBAFB800D73968D4401FF687B15CCE56A087403C1766F9E05B`).

______________________________________________________________________

### Pass 2: AI Multithreading and Speculative Parallel Turns

- **Date**: 2026-10-17
- **Primary Commits**:
  - `d6d73ab`: *Parallelize read-only candidate scoring in the standard AI planners*
  - `0685342`: *Run independent AI turns in speculative parallel waves*
  - `b81909f`: *Track complete unit mutations in speculative turns and bound
    concurrency*
  - `4282ff6`: *Document AI multithreading performance results*
  - `f064004`: *Invalidate speculative plans after global entity changes*
  - `482ecc4`: *Document AI multithreading architecture and safety invariants*

#### Architecture and Safety Invariants

1. **Intra-Planner Parallel Candidate Scoring**:
   - `ParallelScoring.Select` in `SitePlanner`, `CityPlanner`, `ResearchPlanner`,
     `WorkerPlanner`, `MilitaryPlanner`, and `AttackPlanner` evaluates independent
     read-only candidate items concurrently across worker threads above candidate
     thresholds.
   - Candidates are scored without mutating simulation state or drawing from
     `Simulation.Rng`. Results sort deterministically by score with ordinal/entity ID
     tie-breaking.
   - Scoring runs serially during speculative turns (`Simulation.IsParallelAiTurn`) to
     prevent threadpool oversubscription.
2. **Read-Only Query Memoization**:
   - `[ThreadStatic] CityReadScope` caches building activity, power status, and
     modifiers during citizen allocation.
   - `ResourceNetwork.Modes` evaluates transport connections lazily per query.
   - `INavalBlockadeService.BlockadedTiles` resolves blockaded water plots once per
     query rather than per plot.
3. **Fast In-Memory State Cloning**:
   - `GameState.Clone()`, `Plot.Clone()`, and `ExtensionState.Clone()` produce
     independent simulation copies for `Simulation.ForkSpeculative()` without full-save
     canonical normalization.
4. **Wave-Based Speculative Parallel Turns**:
   - `Simulation.RunAi()` delegates to `AiTurnSpeculator`, partitioning uncommitted AI
     players into speculative waves.
   - Player $p_0$ plans directly against the authoritative simulation; players $p_1
     \\dots p\_{m-1}$ plan concurrently on isolated state forks.
   - Plans commit sequentially in ascending `Player.Id` order by replaying recorded
     `GameCommand` sequences against authoritative state.
5. **Safety and Conflict Guards**:
   - **Active Mutation Footprint**: Plans conflict if any tile in an earlier player's
     active mutation footprint (unit moves, city founding/capture/razing, border
     changes, improvements, routes) lies within 5 hexes (`ActiveFootprintRadius = 5`) of
     a candidate plan's footprint. Moving settlers and explorers expand footprints by
     radius 3.
   - **Global Entity-Aggregate Protection**: Changes to entity counters (`NextCityId`,
     `NextUnitId`), entity membership/ownership, unit health/XP/promotions, city
     buildings/population/extensions, or power inputs trigger global entity-aggregate
     conflicts for subsequent plans in the wave.
   - **RNG and Diplomacy Consistency**: Rejects plans if RNG draws or team war relations
     diverge.
   - **Fingerprint Verification**: Plan commit verifies that pre/post-command RNG
     streams and state digests match the fork execution.

#### Measurements and Results

- **Fifty-City Turn (turn 2, 50 cities)**:
  - Baseline execution: **83,120.65 ms** (~83.1 s).
  - Multithreaded execution: **2,139.04 ms** (~2.14 s).
  - Improvement: **97.43% reduction (38.86× speedup)**.
- **8-Player Preparation Benchmark (99 turns setup)**:
  - Baseline execution: exceeded 90 minutes of CPU time without completing (terminated).
  - Multithreaded execution: completed in **231,096.19 ms** (3 minutes 51.10 seconds).
- **Turn-100 `RunAi()` Decisions (8 players, 41 cities, 114 units)**:
  - Execution time: **1,802.43 ms** (no baseline available due to baseline timeout).

______________________________________________________________________

### Pass 3: AI Serial Work Reduction and Spatial Indexing

- **Date**: 2026-10-17
- **Primary Commits**:
  - `c339ee2`: *Reuse AI planning calculations and index spatial queries*
  - `e6239f8`: *Reuse exact AI paths within unchanged planning segments*
  - `da5fa32`: *Prune rejected AI choices and reuse candidate eligibility*
  - `9ff9998`: *Cache sight levels within each visibility recomputation*
  - `193567e`: *Retain AI strategic goals and safe civilian destinations*
  - `a295bf0`: *Validate AI isolation and intent replay across independent games*
  - `3dc5495`: *Document AI serial optimization architecture, cache boundaries, and
    benchmark limits*

#### Architectural Work Reductions

1. **Calculation Reuse**: `AiContext.Segment` memoizes unit role counts, queued
   production counts, city views, production eligibility, nearest-city queries, and
   campaign targets across unchanged planning segments.
2. **Path Query Reuse**: `AiContext.FindPath` caches exact queries up to $\\min(256,
   \\max(1, \\text{units}) \\times 8)$ entries per segment. `PathfindingService`
   combines cost and parent maps into a single local table, eliminating dictionary
   growth allocations.
3. **Candidate Pruning**: Wonder budgets and worker prerequisite filters skip candidate
   evaluations and yield previews; `AiOptions.Best` selects top legal candidates without
   allocating sorted ranking lists.
4. **Visibility Memoization**: `VisibilityService.Recompute` caches directional terrain
   sight levels per plot and team water sight in call-local arrays and dictionaries.
5. **Spatial Indexing**: Tile-bucket threat strength grids (`ThreatStrength`) replace
   repeated whole-map enemy unit scans; settlement exclusion masks and nearest-city
   distance fields replace per-candidate city distance scans.
6. **Strategic Goal Retention**: Unchanged civics and primary land campaigns are
   retained for up to 3 turns; valid settler and worker travel destinations are retained
   for up to 12 turns.

#### Measurement Status

- Operational gains were verified through algorithmic inspection of removed scans,
  sorts, allocations, and re-evaluations. Dedicated before-and-after wall clock
  benchmark tables were not recorded for these serial commits (documented as unmeasured
  runtime gains in `tools/Ariel.Benchmarks/README.md`).

______________________________________________________________________

### Pass 4: World Generation Geometry and River Traversal

- **Date**: 2026-10-17
- **Primary Commit**: `7b12754` (*Optimize world generation geometry and river
  traversal*)
- **Baseline Revision**: `fc7e514df97345fdda5d861be7dacac626e011c3`

#### Implementation Changes

1. **Hexagonal Grid Intervals (`src/Ariel.Core/HexGrid.cs`)**:
   - Replaced sorted range tree construction with direct enumeration of row intervals
     (`RangeRows`, `RangeColumns`).
   - Each row produces clipped column intervals in ascending tile order.
   - Wrapped intervals split at the seam; full-width intervals emit each column once;
     intermediate arithmetic uses `long` to prevent radius overflow.
   - `HexGrid.Neighbors`: Skips the per-query `HashSet` deduplication when geometry
     ensures directions cannot produce duplicate tiles (retained for wrapped maps of
     width 1 or 2).
2. **River Traversal Deduplication (`src/Ariel.Rules/World/ContinentsGenerator.cs`)**:
   - River drainage generation terminates early upon reaching an already drawn drainage
     suffix, as each tile has a single parent.
   - A generation-local bitmap records completed paths.
   - Bounded direction loops replace captured LINQ closures for each river edge.
3. **Harness & Verification (`tools/Ariel.Benchmarks`,
   `tests/Ariel.Tests/HexGridTests.cs`)**:
   - Added `--world` benchmark option for isolated world generation and session startup
     timings.
   - Added independent breadth-first range verification tests covering bounded, wrapped,
     narrow, and single-row maps.

#### Benchmark Measurements

Controlled comparison settings: Fedora 44 x64, Intel Core i7-7700HQ, .NET SDK 10.0.401,
`DOTNET_TieredCompilation=0`, 4 players, east–west wrapped map. Values report medians of
three process medians.

##### 1. World Generation Workload

*Includes plot allocation, terrain, rivers, features, bonuses, and goodies. Excludes
catalog rebuilds, player setup, and AI.*

| World                |   Seed | Before ms | After ms | Time Delta | Before MiB | After MiB | Allocation Delta |
| :------------------- | -----: | --------: | -------: | ---------: | ---------: | --------: | ---------------: |
| **Standard 84×52**   |      1 |     69.97 |    54.98 | **-21.4%** |      11.99 |      6.34 |       **-47.1%** |
| **Standard 84×52**   |     42 |     63.46 |    54.03 | **-14.9%** |      11.85 |      6.33 |       **-46.6%** |
| **Standard 84×52**   | 987654 |     61.72 |    46.60 | **-24.5%** |      12.69 |      6.35 |       **-50.0%** |
| **Gigantic 200×120** |      1 |    311.04 |   309.79 |  **-0.4%** |      58.84 |     35.15 |       **-40.3%** |
| **Gigantic 200×120** |     42 |    320.29 |   377.74 |     +17.9% |      59.77 |     35.18 |       **-41.1%** |
| **Gigantic 200×120** | 987654 |    402.11 |   364.39 |  **-9.4%** |      60.00 |     35.33 |       **-41.1%** |

*Note: Gigantic seed 42 wall-clock increase reflected host scheduling and CPU
contention; allocation reduction remained uniformly ~41%.*

##### 2. Full Session Startup Workload

*Includes generation, player setup, starting site selection, and initial opponent AI.*

| World                |   Seed | Before ms | After ms | Time Delta | Before MiB | After MiB | Allocation Delta |
| :------------------- | -----: | --------: | -------: | ---------: | ---------: | --------: | ---------------: |
| **Standard 84×52**   |      1 |    244.93 |   322.63 |     +31.7% |     100.97 |     91.38 |        **-9.5%** |
| **Standard 84×52**   |     42 |    250.68 |   242.86 |  **-3.1%** |      83.04 |     74.10 |       **-10.8%** |
| **Standard 84×52**   | 987654 |    247.86 |   244.10 |  **-1.5%** |      95.50 |     85.24 |       **-10.7%** |
| **Gigantic 200×120** |      1 |   1467.85 |  1339.56 |  **-8.7%** |     279.27 |    241.28 |       **-13.6%** |
| **Gigantic 200×120** |     42 |   1253.19 |  1091.04 | **-12.9%** |     288.90 |    250.10 |       **-13.4%** |
| **Gigantic 200×120** | 987654 |   1215.94 |  1127.57 |  **-7.3%** |     294.62 |    255.57 |       **-13.3%** |

##### Correctness and Determinism

- Content hash matched baseline:
  `58BF65EEC063C432AF274B66DC6400087BFFED318F955AF7121F89A1010F6008`.
- Generated plot/RNG hashes (standard: seeds 1, 42, 987654; gigantic: seeds 1, 42,
  987654):
  - `F6436E2BE45A3B35585BA2A6535BF37EEB421AA27533A3CF2C1C19DED6575E68`
  - `2B182D6951E35D73E6AA2731FCB16608EAABA3E8E252E1E8516D74A801AAAD31`
  - `3B64D6B8A97CED32F2C7B34C668C4DE4D4061EB6AE384E0A34039F6B3428F526`
  - `0282F40B80F873E914539A92F63CC3BDAFDB8885C68914DF6650120F66BD599C`
  - `08EA1DAA7B56715204DAFF29AA462308EEFCF9CF0318A8B2F58946422D0B5FBB`
  - `0A63FED13BBADAF6F0818E15DFA17FEE1AB9B78E0E7C44D6BDFC5B5BD43DE5E1`
- Session hashes matched baseline in identical order:
  - `46F651468826615C`, `D90B5CF7C47CD23D`, `46DFA66041914FFE`, `CDFD92CD292B0C64`,
    `1AD5F7CBE2AF0F16`, `BD3C3D8FE93FB0FB`.

______________________________________________________________________

### Pass 5: Idle CPU Reduction, Terrain Ray Pick Caching, and Unit Height Sampling

- **Date**: 2026-10-18
- **Primary Commit**: `47b932f` (*Reduce idle CPU by caching terrain picks and skipping
  stationary ground samples*)

#### Root Causes

- Profiling identified two sources of excessive CPU work during stationary idle
  gameplay.
- First, `GameView.HoveredHex` executed terrain ray picking every frame with a
  stationary pointer.
- Second, `UnitActor.UpdateGroundPose` evaluated two identical terrain height queries
  every frame for each stationary unit.
- A 10-second .NET sampled-thread-time trace before the change attributed about 23% of
  sampled thread time to the call `TerrainRay.Pick` Below.
- This trace value measures sampled thread time, not the percentage of total process
  CPU.

#### Implementation Changes

1. **Terrain Pick Caching (`src/Ariel.Godot/GameView.cs`)**:
   - The engine now caches the terrain pick result.
   - The cache key stores pointer coordinates, camera transform, camera FOV, viewport
     size, world revision, UI interception state, and forced hover.
   - When one of these input properties changes, the view recomputes the ray pick.
   - When the session revision changes, hover descriptions and move previews still
     update.
2. **Stationary Ground Pose Sampling (`src/Ariel.Godot/UnitActor.cs`)**:
   - In `src/Ariel.Godot/UnitActor.cs`, `UpdateGroundPose` inspects procedural motion
     (`Shake` and `Lunge`).
   - When procedural motion is zero, the method avoids height difference computation.
   - Unit animation and the 60 FPS target frame rate remain unchanged.

#### Measurements and Evidence

- The team collected measurements on Godot Vulkan Forward+ with an NVIDIA GeForce GTX
  1050 Ti.
- The test environment ran a standard map with default seed 1 in a stationary 1280x720
  window.
- Each sample recorded process `utime+stime` from `/proc/PID/stat` over 10 seconds after
  startup.
- The values are normalized to one CPU core.
- Process CPU utilization decreased from 38.8% before the change to 18.8% after the
  change.
- The engine maintained approximately 60 FPS in both runs.
- These values represent single local samples.
- They do not constitute a controlled benchmark or a guaranteed reduction on other
  hardware.

#### Validation

- The command `dotnet build src/Ariel.Godot` completed with zero warnings and zero
  errors.
- The command `mise run test` passed 1,315 tests (37 Core, 147 Mod, and 1,131 Engine
  tests).
- The default test run excludes end-to-end tests and extractor tests.
- The change added no new automated tests.
- The team performed no interactive regression tests and no GPU profiling.

______________________________________________________________________

### Pass 6: Startup Latency through Theme Overlap and a Lazy Content Hash

- **Date**: 2026-10-18
- **Primary Commit**: `7f865e4` (*Cut cold startup to 3.6 s by overlapping the catalog
  and caching the skin*)

#### Implementation Changes

1. **Theme Painted While the Catalog Loads (`src/Ariel.Godot/Main.cs`)**:
   - The automatic startup paths (headless, `--screenshot`, `--fps-probe`,
     `--skip-mod-manager`) call `BeginModStartup()` before
     `Ui.InstallTheme(GetWindow())`.
   - The 2.3-2.7 s procedural brass-theme painting therefore runs while the catalog
     loads on the thread pool, instead of strictly after it.
   - The interactive path keeps the original order because `BeginModStartup()` opens the
     mod manager, which builds `Control` nodes immediately and needs the theme installed
     first.
   - A startup that fails before the load begins (no usable mods, a damaged mod profile,
     an invalid selection) skips the theme: it has already queued its own quit, and
     painting would only delay the exit by about 2.7 s.
2. **Lazy, Prefetched Content Digest (`src/Ariel.Core/Content/ContentDb.cs`,
   `src/Ariel.Client.Core/GameCatalog.cs`)**:
   - `Freeze()` no longer computes the SHA-256 digest inline. It captures the definition
     array for the digest input under the `content hash input` trace phase, assigns a
     fresh `Lazy<string>` (`LazyThreadSafetyMode.ExecutionAndPublication`), then freezes
     the registries under `registry freeze`.
   - `ContentDb.ContentHash` is computed on first read under its own `content hash`
     trace phase. Registry enumeration order is ordinal before and after the freeze, so
     the digest is byte-identical; the content-hash test literals in
     `tests/Ariel.Tests/ContentHashTests.cs` pass unchanged.
   - `ContentDb.PrefetchContentHash()` starts the digest on the thread pool, and
     `GameCatalog.Load` calls it once content is frozen. Freezing itself still never
     spawns background work, so tests and benchmarks that build catalogs directly stay
     single-threaded.

#### Measurements

- Machine: Fedora 44 x64, 8 cores, NVIDIA GeForce GTX 1050 Ti, .NET SDK 10.0.402, Debug
  build.
- Command:
  `ARIEL_TRACE_STARTUP=1 godot-mono --path src/Ariel.Godot -- --stage setup --screenshot /tmp/shots/a-after.png`,
  timed from process start to the `screenshot ...: Ok` line, one launch at a time with
  `sleep 20` between launches.
- **Before**: theme 2,272.73 ms; `catalog load` 2,372.03 ms (content freeze 1,218.14 ms,
  of which 1,156.39 ms was the inline digest); total **8.60 s**, **8.69 s** (median 8.65
  s, traced run 7.50 s).
- **After**: theme 2,703.18 ms, now overlapping the catalog; `catalog load`
  1,454.04-1,557.93 ms (registry freeze 83.86-114.02 ms, digest 1,379.27-1,409.88 ms on
  the thread pool); total **6.19 s**, **6.38 s**, **6.50 s** (median 6.38 s, traced run
  6.23 s).
- Cold launch is about 2.3 s shorter (-26%). The theme painting is roughly 0.4 s slower
  while the catalog's worker threads run, which the overlap absorbs.
- The interactive mod-manager path gains nothing: it still paints the theme serially
  before the mod manager appears.

#### Validation

- `dotnet build Ariel.slnx -c Debug --no-restore` completed with zero warnings and zero
  errors.
- `mise run test` passed 1,297 tests (37 content-import, 122 core, and 1,138 mod tests).
- Setup-screen screenshots are byte-identical across runs of one build. Between the old
  and new order, 0.55% of pixels differ, confined to thin plate strokes and glyph edges
  (maximum channel delta 55); layout, colours and text are unchanged.
- The changes added no new automated tests, and no E2E, extractor, world-generation, or
  benchmark runs were performed.

#### Painted Skin Texture Cache

- All 109 skin textures (27 plate frames, 34 icons, 3 backdrop layers, 45 scrape-overlay
  layers) are written to `user://skin-cache/v1/x1/` as PNGs by the launch that paints
  them, and loaded from there by every later launch. Keys are FNV-1a hashes of the paint
  inputs, and `SkinCache.Version` has to be bumped whenever the painting changes.
- Measured with
  `ARIEL_TRACE_STARTUP=1 godot-mono --path src/Ariel.Godot -- --stage setup --screenshot /tmp/shots/s.png --frames 3`,
  one launch at a time with `sleep 30` between launches:
  - **Theme**: cold **2,845.28 ms**, **2,892.56 ms**, **2,907.31 ms** (2,829.6-2,883.9
    ms of it inside `SkinCache`); warm **130.69 ms**, **136.67 ms**, **151.64 ms**
    (115.2-131.9 ms inside `SkinCache`), and **343.01 ms** when the cache files were not
    in the page cache (immediately after a build).
  - **Cache on disk**: 524,889 bytes in 109 files.
- End-to-end launch numbers are only worth reading from an unthrottled display. On this
  Wayland session a game window that is not in front renders at about 1 fps, so
  launch-to-screenshot totals from it are frame-quantised and useless: the same build
  measured 5.5-5.9 s both cold and warm. Measure on a virtual X server instead, which
  still reaches the real GPU: `Xvfb :99 -screen 0 1600x900x24 -nolisten tcp &` then
  `DISPLAY=:99 ARIEL_TRACE_STARTUP=1 godot-mono --path src/Ariel.Godot -- --stage setup --screenshot /tmp/shots/s.png --frames 3`
  - **Launch to screenshot**, same command on `DISPLAY=:99`: original code **6,390 ms**;
    this build with an empty `skin-cache` **5,180 ms** (theme 2,757.22 ms painted,
    `catalog load` 1,299.25 ms); this build with a warm `skin-cache` **3,560 ms** (theme
    135.45 ms loaded, `catalog load` 1,158.49 ms). So the whole startup work landed
    **6.39 s -> 3.56 s (-44%)**, of which the texture cache is the last 1.6 s.
  - The cache saves less than the 2.62 s it removes from the theme because painting
    already overlaps the catalog load (see the overlap change above), so part of the 2.9
    s was being hidden.
- Setup-screen screenshots: the painting launch and the pre-cache build are
  byte-identical. A cached launch is byte-identical to the painting one once the scrape
  overlays' tile phase is pinned, which shows the textures themselves round-trip
  exactly: `BrushedStyleBox` seeds that phase from a canvas-item RID, and the cached
  launch renders loading-screen frames the painting launch hides behind its 2.9 s block
  (a measured 38 RIDs earlier), so its overlays land at other offsets.
- A cache that is missing, unreadable, truncated or corrupt is repainted and
  overwritten: a launch with one corrupt frame, one truncated overlay layer and one
  unreadable backdrop layer painted 9 textures, loaded 100, and exited 0.

______________________________________________________________________

### Pass 7: HUD Invalidation on Clicks

- **Date**: 2026-10-26
- **Primary Commits**:
  - `877fb7a`: *Stop bumping the session revision when a click changes nothing*
  - `70ab679`: *Give each HUD panel its own refresh gate instead of one rebuild switch*
  - `003cd30`: *Make the session invalidation tests table-driven*
  - `1ad1904`: *Keep the top bar's widgets and update them in place*
  - `4b6fd9d`: *Keep the left panel's widgets and update them in place*

#### Implementation Changes

1. **Guarded Session Revision Bumps (`src/Ariel.Client.Core/GameSession.cs`)**:

   - `GameSession.Revision` was previously incremented unconditionally on every
     selection and message call, causing whole-HUD rebuilds even when clicking an
     already-selected unit or empty tile.
   - Guarded every mutation point so `Revision` only increments when session state
     actually changes:
     - `SetMessage`: requires different text.
     - `SelectAt`: compares the picked unit and city against current selection; still
       bumps when cycling multi-unit stacks.
     - `SelectUnit` and `SelectCity`: compare requested ID and require the other half of
       the selection to be empty.
     - `ClearSelection`: returns immediately when nothing is selected.
     - `SelectNextUnit`: computes the next candidate unit before comparing.
     - `Submit`: left deliberately unguarded so rejected commands still record error
       messages, prune dead selections, and collect notifications; `EndTurn` continues
       to bump unconditionally through `Submit` and `ContinueOrders`.

2. **Per-Panel Refresh Gates (`src/Ariel.Godot/GameUi.cs`)**:

   - `GameUi.Refresh` previously treated any `Revision` change as an instruction to
     clear and rebuild the entire HUD tree (top bar, left panel or city screen, feed,
     and all three dialogs).
   - Dialogs were emptied on every revision change even while closed, destroying scroll
     offsets, widget instances, and canvas item RIDs.
   - Replaced the single monolithic rebuild pass with independent refresh gates per
     panel. The gates below describe the version this pass shipped; the follow-up pass
     that removed the last two residuals gated every simulation-reading panel on
     `WorldRevision` instead of `Revision`, which moves on a mere selection or message.
     That pass is recorded in `docs/completed-plans/2026-10-26-hud-invalidation.md`, and
     the gates it left in place are:
     - **Top bar**: `WorldRevision` changed. It renders nothing a selection touches.
     - **Left panel**: `Revision` changed (while city screen is closed).
     - **City screen**: city open state or identity changed, or `WorldRevision` changed;
       a local input (tab, filter, unavailable toggle) redraws only the choice list from
       the cached description.
     - **Research dialog**: research dialog open state changed, or (`WorldRevision`
       changed and dialog is open).
     - **Government dialog**: `_governmentRevision` changed, or (`WorldRevision` changed
       and dialog is open).
     - **Modal**: `_modalRevision` changed, or (the modal renders what moved and the
       city screen is closed). The espionage modal follows the selection, so it still
       reads `Revision`; the pedia and load modals read no session state at all.
     - **Feed**: `WorldRevision` changed.
   - The conquest modal is settled before the dialog gates are read, because
     `UpdateConquestModal` manages pending conquest states and may toggle the modal; the
     top bar then renders the conquest list the same call worked out.
   - Closed dialogs hide rather than clearing their children. Reopening the research
     dialog with an unchanged session reuses existing child controls, preserving scroll
     position and geometry.
   - `NeedsRefresh` only compares stored keys, keeping the 60 Hz frame path
     allocation-free.

3. **Persistent Top Bar Updated In Place (`src/Ariel.Godot/GameUi.cs`)**:

   - The top bar flow container and all 19 constituent widgets are constructed once in
     `InitTop` and retained as persistent fields: turn/date/era, golden age, gold,
     research button, three commerce rate groups (label and callback buttons), civics
     button, and ten panel buttons (Conquest, Religion, Corporations, Espionage, Events,
     Diplomacy, Pedia, Tech Tree, Save, Load).
   - `RefreshTop` updates properties in place: `Ui.Set` writes label/button text only
     when differing; `Ui.Tint` overrides theme colour only when differing; research
     button removes font colour override once a technology is chosen.
   - The ten panel buttons are created once in canonical order; `RefreshTop` toggles
     `Visible`, maintaining flow row child order and layout stability without recreating
     nodes.
   - Conquest bookkeeping runs once per world change before the dialog gates are
     evaluated, and the same call's conquest list drives the button's visibility and
     tooltip. Availability checks for corporations, espionage, events, and tech tree run
     once per world change to drive button visibility, because none of them renders
     anything a selection changes.

4. **Persistent Left Panel Skeleton Updated In Place
   (`src/Ariel.Godot/GameUi.Left.cs`)**:

   - The left panel skeleton is constructed once in `InitLeft` and retained as fields:
     scroll container, VBox, empty-selection label, unit section (48 px icon,
     name/stats, moves/health, health bar, XP line, promotions label, Orders heading,
     order grid, and hint), separator, Cities heading, and city list container.
   - `RefreshLeft` writes properties in place: `Ui.Set` updates text, icon texture
     updates only when differing, health bar value updates only when differing, and fill
     tint stylebox override updates only when tint changes (cached in a field to avoid
     per-click allocations).
   - `FillUnitRow` encapsulates the unit row update in a single helper to support future
     stacked unit lists.
   - Dynamic sub-panels rebuild only when their structural identity keys change:
     - **Order grid**: keyed on selected unit ID plus action IDs and labels. When key
       matches, existing buttons update `Disabled` and `TooltipText` in place without
       recreating nodes.
     - **City list**: keyed on ordered ID, name, population, and religion badge of human
       cities. When key matches, existing buttons and canvas item RIDs remain untouched.
   - Key strings are computed only inside `RefreshLeft` on revision changes, never on
     the 60 Hz frame path.

#### Measurements

- End-to-end and per-click HUD numbers are only meaningful from an unthrottled display.
  A Wayland session throttles unfocused game windows to ~1 fps, quantising frame
  timings. Measure on a virtual X server with GPU access instead:
  `Xvfb :99 -screen 0 1600x900x24 -nolisten tcp &`
  `DISPLAY=:99 godot-mono --path src/Ariel.Godot -- --trace-performance --stage map --world small`
- Test workload: Xvfb :99, 1600x900 (1280x720 window), Debug build, warm skin cache,
  `--trace-performance --stage map --world small`. Ten map clicks alternating between
  the initial unit stack and empty tiles (so every click alters selection), followed by
  two repeat clicks on the final tile.
- **HUD Update per Selection Click (top + left)**:
  - Base commit (`70ab679` / pre-refactor baseline): **6.38–41.08 ms** (median **16.06
    ms**, n=10; earlier full-HUD rebuilds measured 6–48 ms, median 16.5 ms).
  - After (`4b6fd9d`): **0.34–18.60 ms** (median **2.40 ms**, **-85.1%** median
    reduction).
- **Top Bar Refresh Alone**:
  - Base commit: **4.95–9.88 ms** (median **6.08 ms** across 20 refreshes; up to 27.72
    ms during selection toggles).
  - After (`1ad1904` / `4b6fd9d`): **0.27–0.95 ms** (median **0.42 ms**, **-93.1%**
    median reduction across 20 refreshes).
- **Left Panel Component Split**:
  - Total left panel time dropped from **1.40–26.30 ms** to **0.03–17.59 ms**.
  - A timing probe isolated the cost: `GameSession.DescribeUnit` (untouched by this
    work) takes **0.13–6.16 ms** when a unit is selected and 0 ms with empty selection.
  - Excluding `DescribeUnit`, the left panel's own UI work:
    - Empty selection: dropped from **1.23–1.69 ms** to **0.11–0.38 ms** (**-77.5%**).
    - Unit selected: dropped from **3.30–3.94 ms** to **0.08–0.18 ms** (**-95.4%**);
      rebuilt order buttons on stack selection changes took 1.87–1.90 ms.
- **Repeat Clicks & Dialogs**:
  - Repeat clicks on the same tile or empty ground produce no `ui update:` line at all,
    as `Revision` does not bump.
  - Closed dialogs are never cleared or rebuilt on selection clicks (`dialogs 0.00 ms`).
  - Opening the research dialog costs 18.74 ms (top and left 0.00 ms), closing costs
    0.05 ms, and reopening with an unchanged session costs **0.07 ms** (reusing existing
    child nodes, down from 14–23 ms full rebuilds).
- **Scrape Texture & Scroll Retention**:
  - The top bar retains 19 persistent children across clicks; the left panel retains its
    scroll container and 23 descendants.
  - Preserving canvas item RIDs prevents `BrushedStyleBox` scrape overlay re-seeding:
    - Religion button plate: differing pixels across selection clicks dropped from **594
      px** on base to **0 px**.
    - Top bar total: differing pixels dropped from **10,246 px** (of 104,960) to **15
      px**.
    - Left panel: differing pixels across clear and re-selection of the same unit
      dropped from **123 px** to **0 px**.
  - Scroll retention: in a 20-city scenario scrolled to offset 115, a map click on the
    base rebuild recreated `ScrollContainer` and reset scroll to 0.0; the persistent
    panel preserves the `ScrollContainer` instance, keeps scroll at **115**, and skips
    city list rebuilds (`cityrebuild 0`).

#### Residuals (Stopping Rule)

The following items were deliberately kept out of scope per the plan's stopping rule.
Items 2 and 3 were removed by the follow-up pass recorded in
`docs/completed-plans/2026-10-26-hud-invalidation.md`; the list below is what is still
open:

1. **Map dynamic layer**: Still rebuilds per `Revision` bump.
2. **Panel identity keys**: String formatting for `OrderKey` and city list identity keys
   allocates on revision-change paths (never on the 60 Hz frame path).
3. **End turn invalidation**: `ContinueOrders` unconditionally bumps `Revision` and
   `WorldRevision` on every end turn, even if no units moved.
4. **Government and modal dialogs**: The government dialog still rebuilds on every open
   and on every world change while open; the pedia builds once per open and then updates
   its tabs, list and article in place.

#### Validation

- `dotnet build Ariel.slnx` completed with zero warnings and zero errors.
- `mise run test` passed 1,309 tests (37 content-import, 122 core, 1,150 mod tests), 0
  failed.
- Session invalidation test suite
  (`tests/Ariel.Mods.Tests/SessionInvalidationTests.cs`): 12 table-driven cases (silent
  theory with 5 rows, bumping theory with 4 rows, and 3 dedicated facts for rejected
  submit, stack cycling, and end turn). Verified on pre-fix baseline (`8c483aa`) that
  all 6 silent/end-turn cases fail by exactly one extra revision bump. The later review
  pass replaced the two theories with shared-session facts, so the file now holds six
  facts that assert the same cases without a string switch.
- Visual regressions verified via `mise run shot:compare` across tour stages; pixel
  differences between base and refactored builds match the magnitude of same-build runs
  and are confined to procedural scrape phases seeded by canvas item RIDs. Text,
  colours, tooltips, and layouts remain identical.
- States verified unreachable with current capture tooling: golden age, anarchy, pending
  events, ruleset without corporations/espionage, unit with no actions (Sleep/Wake
  always validate), and unit with promotions (requires combat experience or
  `IUnitService.Create` free promotions).

### Pass 8: Lockstep State Hash Without Copies

- **Date**: 2026-11-11
- **Primary Commits**:
  - `a17020e`: *Hash the canonical state bytes where they are written*
  - `4c63893`: *Keep the state hash buffers for gigantic worlds*

#### Implementation Changes

Lockstep hashes the whole state on every tick that applies commands. `StateHasher`
serialized the state into a fresh array, canonicalized it, decoded the canonical bytes
into a string, then encoded the string again to run FNV-1a over it: three state-sized
copies per hash, all on the large object heap.

- `CanonicalJson` serializes into a reused per-thread buffer and passes the canonical
  bytes straight to a finishing function (string decode, SHA-256 or FNV-1a).
- `CanonicalJson.Fnv1a64` replaces the string round trip in `StateHasher`.
- The reused serializer and canonical buffers are dropped after use only above 64 MB,
  well past a gigantic world's state (about 7 MB at turn 1), so every shipped size keeps
  reusing them.

#### Measurements

`mise run benchmark -- --hash`, 100 samples after one warm-up hash, with before and
after measured back to back:

| World                                       | Metric                         |    Before |    After |
| :------------------------------------------ | :----------------------------- | --------: | -------: |
| Standard, 8 players, turn 100 (2.0 MB JSON) | Time per hash                  |  14.90 ms | 13.03 ms |
|                                             | Allocated per hash             |  8,923 KB |   827 KB |
|                                             | Gen-2 collections (100 hashes) |        14 |        0 |
| Gigantic, 8 players, turn 1 (7.0 MB JSON)   | Time per hash                  |  61.66 ms | 56.02 ms |
|                                             | Allocated per hash             | 31,002 KB | 2,327 KB |
|                                             | Gen-2 collections (100 hashes) |        29 |        0 |

The hash values are unchanged (`11110498118ABED7` and `F36B7E720365C85E`), and
`mise run test` passes. A first cut capped the reused buffers at 8 MB, which a fresh
gigantic world fills exactly and a later one would overflow, regrowing them on every hash.

### Pass 9: AI Threat Grid Reuse

- **Date**: 2026-11-11
- **Primary Commit**: `4cb9780`: *Refill one threat grid per turn instead of allocating one per command*

`AiContext` rebuilt its whole-map threat grid (`int[Grid.Count]`, 94 KB on a gigantic map,
so a large-object-heap allocation) after every command an AI issued. The context now owns
one array that each segment clears and refills. Segments never overlap: parallel
scoring joins before `Issue` or `Refresh` replaces one.

`mise run benchmark -- --gigantic`, back to back:

| Metric (turns 21-30)            |   Before |    After |
| :------------------------------ | -------: | -------: |
| Allocated per turn              | 483.4 MB | 481.4 MB |
| Gen-2 collections (10 turns)    |       15 |       13 |
| Time per turn                   |   486 ms |   501 ms |

The final hash is unchanged (`8DE653BF50D0B641`). At turn 30 each AI has only a few
workers and settlers, so about 20 grid allocations a turn go away. The saving grows with
the number of workers and settlers issuing commands; the turn-time difference is noise.

### Pass 10: Culture Reach Cached Across a Batch of Plots

- **Date**: 2026-11-11
- **Primary Commit**: `4eb56de`: *Reuse each city's culture reach across a batch of plot resolutions*

Settling a plot's owner asks, for every player with culture on it, whether one of that
player's cities reaches the plot. Each answer evaluated the city's culture levels and ran
a fresh culture-distance search, for every plot of every spreading city. `CultureRanges`
now keeps each city's levels, radius and distances for one batch (one city's spread, one
decay pass), and drops a city's distances whenever a plot changes owner to or from a
team at war with that city's owner, since only that changes what blocks its culture.

`mise run benchmark -- --borders` (36 cities, turn 100). The state hashes before and after
the phase (`11110498118ABED7`, `121AA603D84F93B6`) are unchanged.

| Measured part of the borders phase |            Before |             After |
| :--------------------------------- | ----------------: | ----------------: |
| Culture spread and plot ownership  |   1.44 ms, 1.4 MB |   0.37 ms, 0.4 MB |
| Whole phase, with work assignment  | 11.21 ms, 15.3 MB | 11.28 ms, 14.3 MB |

The spread-only figures come from a diagnostic run with work assignment disabled. About
97% of the phase is `ICityWorkService.Assign`, which runs again for every city after it
spreads, and at this state changes no assignment. That is the next target here.

### Pass 11: Work Reassigned Only Where Borders Move

- **Date**: 2026-11-12
- **Primary Commit**: *Reassign worked plots only in cities a spread's new borders reach*

Every city that spread culture then chose its worked plots again from scratch: its
wellbeing, and the full plot yield of every plot in its radius. Spreading culture changes
one input of that choice, which plots the player owns, and it rarely changes any. A
150-turn, six-player AI game (seed 1) spread culture 2,096 times; 6 of the reassignments
changed an assignment, and every one of those followed a plot changing owner in that
player's spread. Two were a sibling city taking up a plot a lower-id city had just
claimed. A spread now reassigns only the spreading player's cities within work radius of
a plot whose owner changed, which also reaches siblings that spread earlier in the phase
or not at all.

`mise run benchmark -- --borders` (35 cities, turn 100). The state hashes before and after
the phase (`9ABF26A5B877B926`, `63CD73EB061A7F48`) are unchanged.

| Measured part of the borders phase |            Before |           After |
| :--------------------------------- | ----------------: | --------------: |
| Whole phase, with work assignment  | 16.42 ms, 19.0 MB | 2.80 ms, 0.5 MB |

### Measurement: Extension State Copies in AI Forks (No Change Made)

- **Date**: 2026-11-12

Each speculative AI fork copies the game state, and `ExtensionState.Clone` copies every
mod state component by serializing it to JSON and reading it back. The proposal was a
typed copy hook per component, with the JSON round trip as the fallback. It was measured
first and rejected: the copies are too small a share of a turn to pay for a second copy
path that must stay equal to the save contract.

`mise run benchmark -- --clone` (standard world, turn 100: 4,368 plots, 9 players, 35
cities, 104 units; 50 samples after one warm-up).

| Measured part                          |       Time | Managed bytes |
| :------------------------------------- | ---------: | ------------: |
| `GameState.Clone`                      |    1.81 ms |      5,360 KB |
| Extension components within that clone |    0.21 ms |        300 KB |
| One whole turn (turns 100-104)         | 1,263.4 ms |   2,439.6 MiB |

Extension copies are 11% of a clone's time and 6% of its bytes. A turn of this game makes
about 26 clones (counted with a temporary counter over turns 100-104: the per-player
forks and the speculator's own snapshots). Together they are 3.7% of the turn, and the
extension copies in them 0.4%. Removing the JSON round trip entirely could not be seen
in a turn time. The clones also account for about 140 MiB of the 2.4 GiB a turn allocates,
so the rest of the turn is where further work on turn processing should look.

### Pass 12: Sight Lines and Network Searches Without Per-Call Buffers

- **Date**: 2026-11-12

A turn-100 turn of a standard eight-player game allocated 2.4 GiB. The new
`mise run benchmark -- --allocations` profile named two sources for over half of it.

- **Resource network searches** (`ResourceNetwork`): every connectivity query allocated a
  map-sized mode array and two map-sized flag arrays, and a turn makes about 22,000
  queries: 382 MiB of `NetworkMode[]` and most of 306 MiB of `bool[]`. A search now
  borrows one set of buffers kept per thread (`Search`, cleared per query, with a fresh
  set for a search opened inside another), stores a mode in a byte, and reads team
  techs and blockades without LINQ.
- **Sight lines** (`SightLine`, `VisibilityService`): every step of every ray built a
  list of four candidates and three LINQ iterators, and each sight test a closure:
  about 430 MiB together. The ray now walks the four candidates on the stack and takes
  its tile rule as a struct (`ISightStep`). The elevation lookups also built their
  constant's name by concatenation on every call and now use literals.

Results are unchanged: the 60-turn seed-7 AI game saves an identical state, and
`mise run check:sync` matches on all 101 turn hashes.

| Workload                                     |             Before |              After |
| :------------------------------------------- | -----------------: | -----------------: |
| Standard turn 100-104, allocated per turn    |        2,439.6 MiB |        1,194.6 MiB |
| Standard turn 100-104, time per turn         | 1,233-1,263 ms     |         907-921 ms |
| Gigantic turns 21-30, allocated per turn     |           481.4 MB |           431.0 MB |

The gigantic "before" is the Pass 9 figure, so that row also spans later changes.

What the profile shows next, per turn: `System.String` 201 MiB (likely the
`"cities:" + key` and `"military:" + key` modifier names built on every lookup),
`RevealedPlotSnapshot` 75 MiB (every visible plot is captured again on every sight
recompute), and about 80 MiB of LINQ closures over `TechDef` and `TerrainDef` lookups.
None was changed here; each needs its call sites found and its own measurement.

### Measurement: Base Victory Module and Timed Effects Store (No Regression)

- **Date**: 2026-11-21

Score and victory moved into the base game (`victory`), and Events, Espionage and
Revolution now keep their timed modifiers in one Cities store
(`cities:timed_effect_modifiers`, which returns at once for any scope other than plot,
city and player). `mise run benchmark`, single samples, run back to back on the same
machine before (`513df09`) and after. The games are not identical: main-mod games now
score, check victory each turn and apply research diffusion welfare, so the turn-100
position differs.

| Workload                                            |    Before |     After |
| :-------------------------------------------------- | --------: | --------: |
| Fifty-city turn (turn 2)                            |  1,204 ms |  1,244 ms |
| Eight-player preparation (99 turns, save/load)      | 69,150 ms | 70,247 ms |
| Eight-player AI decisions at turn 100               |    254 ms | 330-488 ms |
| World generation, seeds 1 / 42 / 987654             | 393 / 235 / 436 ms | 398 / 297 / 266 ms |

The turn and preparation figures are within 3% and inside the run-to-run spread. The AI
decision sample moved between 330 and 488 ms over two runs of the same build, so that row
reflects the different turn-100 position and noise more than either change.

### Measurement: Mod-Only Content Fields as Attachments (No Regression)

- **Date**: 2026-11-21

Base-schema fields that only a mod reads moved into that mod's attachment rows, read with
`ContentDb.Attachment<T>(id)` (two dictionary lookups) where the rules used to read a
property. The games are identical: a 200-turn, six-player AI game (seed 11) differs from
the one before only in its content hash and module versions. `mise run benchmark`,
single samples, run back to back on the same idle machine, twice for each build: before
(`993c22a`, in a separate worktree) and after. A first "before" run, taken while the
machine was busy, read 1,935 ms and 128,923 ms for the first two rows and is not used.

| Workload                                            |               Before |                After |
| :-------------------------------------------------- | -------------------: | -------------------: |
| Fifty-city turn (turn 2)                            |         940 / 921 ms |       1,105 / 935 ms |
| Eight-player preparation (99 turns, save/load)      | 49,679 / 64,012 ms   | 50,677 / 49,977 ms   |
| Eight-player AI decisions at turn 100               |         204 / 177 ms |         172 / 182 ms |
| World generation, seed 1 (42, 987654 alike)         |         309 / 285 ms |         419 / 286 ms |

Every row falls inside the spread between two runs of one build; the attachment lookups
cost nothing measurable.

### Measurement: General Timers (No Regression)

The timed-effects store became the Foundation's general timer store, and the rebellion
cooldown, counter-espionage and spy revolts moved onto it. Seeds 1 (four players, 120
turns) of `mise run ai:play` before and after differ only in module versions and in the
moved state components; the three timers that were live in the compared state carry the
same holder, expiry turn and modifiers. `mise run benchmark`, single samples, on a busy
machine (load average about 8). "Before" is the tree at `e317abf`, taken first, while the
machine was hotter; "after" is the final tree.

| Workload                                            |       Before |        After |
| :-------------------------------------------------- | -----------: | -----------: |
| Fifty-city turn (turn 2)                            |     2,031 ms |       939 ms |
| Eight-player preparation (99 turns, save/load)      |    56,007 ms |    50,653 ms |
| Eight-player AI decisions at turn 100               |       253 ms |       275 ms |
| World generation, seed 1                            |          n/a |       311 ms |

The latest recorded idle figure for the fifty-city turn is 921 to 1,105 ms (previous
section). The machine's load, not the change, set the "before" column; timer queries
are a pass over a handful of timers and a turn with nothing to expire allocates nothing.

### Pass 13: Turn Profiler and Long AI Game Costs

- **Date**: 2026-11-22
- **Commits**: `5016ba5`, `8f182fe`, `7d25936`, `8d4aafb`, `f35cf8c`, `202e187` (profiler and
  tooling); `6eb72f2`, `de952e8`, `6d8feda`, `9d88c13`, `604af14`, `708d2bc`, `8c23637`
  (optimizations)

Long AI games were reported to take hours over 350 to 500 turns. Nothing measured where a
turn's time went, so this pass first added the turn profiler (see "Turn Profiler" above),
then measured and removed the largest costs it showed. Every optimization leaves the
game byte-identical: each commit's 100-turn final save matches the one before it
(`mise run check:desync-diff`), the final tree's saves match `b424234`'s for all three
subjects below, and `mise run check:sync -- --seed 3 --players 4 --turns 100` matches all
101 turn hashes on the final tree, the same 101 hashes that `b424234` prints.

#### Method and Machine

- AMD Ryzen AI 5 PRO 340 (12 threads), Release builds through `mise run ai:play`.
- Main subject: seed 3, four AI players, small world, ancient start, default mod set (the
  main mods and every optional feature mod), 100 turns. No 300-turn run was made. The
  300-turn figures below are extrapolated from the 100-turn curves.
- Supplementary subjects, same seed and 100 turns: an industrial-era start
  (`--era foundation:era_industrial`), which reaches 30 cities and late-game rules, and
  eight players on a standard world.
- "Before" is `b424234` (main) with only the profiler patches applied, in a separate
  worktree. "After" is the final tree. Both play the identical game.
- Other agents shared the machine (load average 0.5 to 5 during the runs). Wall times of
  a 20-second game vary by 1 to 2 seconds between back-to-back runs. The commit tables
  therefore lead with calls and bytes, which are deterministic. The ancient-start wall
  time is the mean of three alternating runs of each build.

#### Baseline: What a Turn Costs

The mandated game does not take hours. 100 turns took 20.4 s (three runs: 20.4, 19.9 and
21.3 s) and allocated 19.6 GiB. The AIs founded few cities: 4 until turn 50, then 19 cities
and 71 units at turn 100. A turn at turn 100 costs about 0.3 s.

| Advance | Wall ms | Turn systems ms | AI waves ms | Allocated MiB | GC pause ms | Cities | Units |
| ------: | ------: | --------------: | ----------: | ------------: | ----------: | -----: | ----: |
|       2 |   988.7 |            61.0 |        81.1 |         339.3 |        51.1 |      4 |     4 |
|      10 |   159.5 |            36.0 |        84.5 |          69.8 |        16.3 |      4 |     4 |
|      25 |   216.9 |            66.7 |       121.7 |          69.8 |        14.6 |      4 |     9 |
|      50 |    88.9 |            17.0 |        58.1 |          80.7 |        11.8 |      4 |    20 |
|      75 |   252.8 |            77.1 |       167.2 |         352.7 |        53.0 |     13 |    43 |
|     100 |   368.1 |            84.4 |       273.6 |         527.7 |       102.3 |     19 |    71 |

Row 2 is the first advance and includes compiling. Turn time follows the game's size,
not the turn number. The curves alone say little about the shape of that growth. A fit
of turn time (advances 10 to 101) to cities and units goes from R² 0.68 with linear
terms to 0.93 with squared terms in the industrial start. It goes only from 0.83 to 0.85
for eight players, and from 0.65 to 0.69 for the ancient start, where single turns are
mostly noise. The four costs below grow faster than the game. That conclusion comes
from the code's structure, read from the code and confirmed by call counts. It does
not come from the curve:

1. **Sight and contacts per move step.** Every step of every move recomputed every
   team's sight from every unit and city (units × observers). It then scanned every plot
   of the map for contacts and built two sorted sets per plot (steps × map size).
2. **Per-city modifier ledgers.** Each city query rebuilds the city's ledger from every
   modifier source: about 130,000 city ledgers in the 100 industrial turns. Building
   effects add every building of the owner's other cities to each ledger (cities
   squared). Each ledger also gathered a whole player ledger for corporate maintenance.
3. **Great people.** Each idle great person scores every city on the map every turn.
   Each foreign city costs three ledgers for the profit of its trade mission.
4. **Allocation.** The industrial game allocated 1.5 GiB per turn at 30 cities. The AI
   plans its players on parallel threads, and each collection pauses all of them: 13.6 s
   of pauses in 82 s.

Top entries of the baseline, ancient start (processor time summed over threads):

| Entry                              | Calls | Total ms | Allocated MiB |
| :--------------------------------- | ----: | -------: | ------------: |
| `kernel:ai_turns` (AI waves, wall) |   100 |   12,875 |         6,497 |
| `MoveUnitPathCommand`              | 1,796 |    8,932 |         4,950 |
| `ai:military`                      |   709 |    8,254 |         4,337 |
| `relations:discover_contacts`      | 4,895 |    7,865 |         4,787 |
| `ai:settlers`                      |   709 |    3,598 |         1,765 |
| `ai:citizens`                      |   709 |    3,147 |         1,723 |
| `world:sight_recompute`            | 5,053 |    2,511 |         1,514 |
| `ai:civics`                        |   709 |    2,328 |           571 |
| `ai:economy`                       |   709 |    2,280 |         1,952 |
| `ai:cities`                        |   709 |    1,809 |         1,114 |

Top entries of the industrial start (82.4 s, 145 GiB):

| Entry                              | Calls | Total ms | Allocated MiB |
| :--------------------------------- | ----: | -------: | ------------: |
| `kernel:ai_turns` (AI waves, wall) |   100 |   59,081 |        43,339 |
| `great_people:planner`             |   890 |   35,084 |        41,290 |
| `ai:military`                      |   890 |   19,177 |        20,282 |
| `ai:citizens`                      |   890 |   16,325 |        16,105 |
| `ai:cities`                        |   890 |   12,130 |        11,934 |
| `MoveUnitPathCommand`              | 2,535 |   11,112 |         8,112 |
| `ai:economy`                       |   890 |   10,546 |        11,135 |
| `SetCityAssignmentsCommand`        | 7,484 |    8,477 |         8,892 |
| `relations:discover_contacts`      | 6,531 |    7,977 |         6,600 |
| `world:sight_recompute`            | 6,986 |    4,387 |         2,683 |

Turn systems are not in these lists, because each system is smaller than the largest AI
entries. Together they are the largest share of a late industrial turn: 490 of 850 ms at
turn 100, against 351 ms of AI waves (253 of 532 ms after this pass). Over the whole
industrial game they took 20.9 s and allocated 29.5 GiB, and over the ancient game 5.0 s
and 4.7 GiB. The top systems, before
and after this pass (time on the turn's own thread):

| Industrial start          | Calls | Before ms | Before MiB | After ms | After MiB |
| :------------------------ | ----: | --------: | ---------: | -------: | --------: |
| `cities:production`       |   425 |     3,797 |      5,620 |    1,839 |     2,262 |
| `cities:growth`           |   425 |     3,213 |      5,496 |    1,998 |     2,390 |
| `global_warming:warming`  |   100 |     2,180 |      3,218 |      760 |       975 |
| `cities:assign_work`      |   425 |     2,089 |      3,058 |    1,175 |     1,329 |
| `great_people:births`     |   425 |     1,955 |      2,827 |      666 |       810 |
| `economy:gold`            |   425 |     1,316 |      1,964 |    1,131 |     1,221 |
| `military:units`          |   425 |     1,161 |      1,789 |    1,169 |     1,780 |
| `economy:reconcile_gold`  |   100 |     1,064 |      1,641 |      777 |       857 |
| All turn systems          |       |    20,875 |     30,165 |   13,005 |    14,791 |

| Ancient start            | Calls | Before ms | Before MiB | After ms | After MiB |
| :----------------------- | ----: | --------: | ---------: | -------: | --------: |
| `barbarians:behavior`    |   100 |       919 |      1,054 |      424 |       363 |
| `cities:production`      |   472 |       656 |        753 |      460 |       353 |
| `world:improvements`     |   100 |       570 |        147 |      608 |       110 |
| `cities:growth`          |   472 |       509 |        611 |      486 |       355 |
| `cities:assign_work`     |   472 |       314 |        338 |      266 |       182 |
| `military:units`         |   472 |       249 |        317 |      312 |       313 |
| `economy:gold`           |   472 |       270 |        249 |      291 |       191 |
| `global_warming:warming` |   100 |       283 |        344 |      148 |       124 |
| All turn systems         |       |     4,995 |      4,820 |    3,868 |     2,455 |

The city systems read city ledgers throughout, so the modifier commits below cut the
industrial ones by 40 to 50%. `barbarians:behavior` moves units, so it most likely gains from the sight
and contact commits. Its time was not broken down further. Single ancient-start
systems below 400 ms are within run-to-run noise.

A diagnostic run timed each modifier source; that timing was not committed. Its
per-source times sum to 70 s of the game's processor time in modifier collection, an
upper bound, because a city collection that gathers a player ledger counts the player
sources inside it as well. The largest sources were
`BuildingEffects` (30.5 s, 40 GiB), `CivicEffects` (17.5 s), `CorporationEffects` (8.7 s,
in a game with no corporation) and `BuildingMilitaryEffects` (5.8 s). Only 23 of 658
great-person evaluations led to an action. The rest scored every city and stayed idle.

#### Optimizations

Each commit message records its own before and after. Figures are processor time summed
over threads, from the subject named.

| Commit    | Change                                                       | Subject    | Before                  | After                   |
| :-------- | :----------------------------------------------------------- | :--------- | :---------------------- | :---------------------- |
| `6eb72f2` | Contact discovery skips plots with no unit and no owner      | ancient    | 8,089 ms, 4.5 GiB       | 728 ms, 0.2 GiB         |
| `de952e8` | Sight tests and captures each plot once per team             | ancient    | 2,945 ms, 1.4 GiB       | 2,034 ms, 0.8 GiB       |
| `6d8feda` | Sight keeps a plot memory that already holds what is seen    | ancient    | 2,034 ms, 779 MiB       | 1,867 ms, 499 MiB       |
| `9d88c13` | A path renews sight once when its last step just did         | ancient    | 5,053 recomputes        | 3,007 recomputes        |
| `604af14` | Building effects name only non-zero lines                    | industrial | 30.5 s, 40.0 GiB        | 25.6 s, 21.2 GiB        |
| `708d2bc` | Civic effects resolve running buildings once per calculation | industrial | 17.5 s, 23.0 GiB        | 12.5 s, 15.9 GiB        |
| `8c23637` | No player ledger for a city without corporations             | industrial | 730,615 ledgers, 60.5 s | 589,093 ledgers, 39.4 s |

The first three rows come from a temporary scope that timed only the calls made inside
moves (4,656 per game). That scope was not committed, so its call counts differ from the
committed `Rule` entries in the baseline tables (4,895 contact scans and 5,053 sight
recomputes). Those also count callers outside movement: combat, cargo, upgrades,
missions and city capture. The `8c23637` times are
`modifiers:collect` totals, which nest (see "Turn Profiler"). Before the commit, each city
collection held a player collection that counted twice, so the 60.5 s overstates the real
cost. The drop of 141,522 nested player collections is exact.

One change was measured and dropped. It asked each city only for the great-person actions
that depend on going there, which skips the technology choice. It saved nothing
measurable (32.3 s before, 31.8 s after): the cost was in trade profits, not discovery.

#### Results

| Subject (100 turns, seed 3)              | Wall before | Wall after | Allocated before | Allocated after | GC pauses before | GC pauses after |
| :--------------------------------------- | ----------: | ---------: | ---------------: | --------------: | ---------------: | --------------: |
| Four players, small, ancient (mean of 3) |      20.5 s |     16.9 s |         19.6 GiB |        11.3 GiB |            3.4 s |           2.0 s |
| Four players, small, industrial          |      82.4 s |     57.4 s |        145.0 GiB |        88.9 GiB |           13.6 s |           6.8 s |
| Eight players, standard, ancient         |      53.2 s |     34.0 s |         58.9 GiB |        31.4 GiB |           19.3 s |          10.3 s |

Per-turn wall time before and after:

| Advance | Ancient before | Ancient after | Industrial before | Industrial after | 8 players before | 8 players after |
| ------: | -------------: | ------------: | ----------------: | ---------------: | ---------------: | --------------: |
|      10 |         160 ms |        110 ms |            398 ms |           273 ms |           433 ms |          230 ms |
|      25 |         217 ms |        157 ms |            324 ms |           267 ms |           234 ms |          266 ms |
|      50 |          89 ms |        168 ms |            689 ms |           488 ms |           297 ms |          215 ms |
|      75 |         253 ms |        165 ms |            887 ms |           633 ms |           543 ms |          334 ms |
|     100 |         368 ms |        206 ms |            850 ms |           532 ms |           959 ms |          494 ms |

Single turns vary by tens of milliseconds between runs, so compare the totals above. At
turn 100 the eight-player game allocated 1,252 MiB before and 666 MiB after. It paused
for collection for 402 ms before and 188 ms after.

Top entries after, industrial start:

| Entry                              |   Calls | Total ms | Allocated MiB |
| :--------------------------------- | ------: | -------: | ------------: |
| `kernel:ai_turns` (AI waves, wall) |     100 |   42,513 |        27,633 |
| `modifiers:collect`                | 589,093 |   37,679 |        39,547 |
| `great_people:planner`             |     890 |   20,227 |        22,983 |
| `ai:military`                      |     890 |   14,407 |        16,529 |
| `ai:citizens`                      |     890 |   11,476 |         8,419 |
| `ai:cities`                        |     890 |   10,705 |        10,290 |
| `ai:economy`                       |     890 |    8,616 |         7,277 |
| `SetCityAssignmentsCommand`        |   7,484 |    5,099 |         4,097 |
| `MoveUnitPathCommand`              |   2,535 |    2,209 |           772 |
| `world:sight_recompute`            |   4,451 |    2,071 |           591 |

Top entries after, ancient start (one run, 16.7 s):

| Entry                              |   Calls | Total ms | Allocated MiB |
| :--------------------------------- | ------: | -------: | ------------: |
| `kernel:ai_turns` (AI waves, wall) |     100 |    9,718 |         3,842 |
| `modifiers:collect`                | 114,049 |    6,178 |         3,886 |
| `ai:military`                      |     709 |    4,031 |         1,679 |
| `ai:citizens`                      |     709 |    2,860 |         1,141 |
| `ai:economy`                       |     709 |    2,323 |         1,534 |
| `ai:cities`                        |     709 |    2,021 |           978 |
| `ai:civics`                        |     709 |    1,972 |           459 |
| `ai:settlers`                      |     709 |    1,771 |           626 |
| `SetCityAssignmentsCommand`        |   2,626 |    1,372 |           570 |
| `MoveUnitPathCommand`              |   1,796 |    1,359 |           350 |
| `world:sight_recompute`            |   3,007 |    1,181 |           320 |
| `relations:discover_contacts`      |   2,849 |      447 |           138 |

The baseline ancient table has no `modifiers:collect` row because that scope (`202e187`)
came after it. Fewer contact scans run after `9d88c13`, because a path that just renewed
sight no longer renews it again.

`mise run benchmark` (single samples), at the start of the pass (`b424234`) and on the
final tree:

| Workload                                       |             Before |              After |
| :--------------------------------------------- | -----------------: | -----------------: |
| Fifty-city turn (turn 2)                       |             913 ms |             877 ms |
| Eight-player preparation (99 turns, save/load) |          50,076 ms |          31,712 ms |
| Eight-player AI decisions at turn 100          |             262 ms |             255 ms |
| World generation, seeds 1 / 42 / 987654        | 467 / 308 / 253 ms | 245 / 184 / 234 ms |

World generation is untouched by this pass. Its difference is machine noise.

#### Extrapolation to 300 Turns

These are extrapolations, not measurements. Each per-turn cost is anchored at the mean
of advances 92 to 101. Cities are assumed to keep growing at their turn 51 to 101 rate.
The first table caps them at 7.5 cities per player. That is where the small-world
industrial game stopped growing (30 cities from turn 75 on), so the cap is 30 cities on
the small world and 60 on the standard world. The linear bound scales turn cost with
cities, and the squared bound with cities squared. Units are left out. The industrial
game ended its wars and fell from 79 to 30 units, so no unit trend can be read from these
curves.

| Subject                         | 100 turns, measured | 300 turns, linear | 300 turns, squared | Turn 300, squared |
| :------------------------------ | ------------------: | ----------------: | -----------------: | ----------------: |
| Ancient, 4 players, before      |              20.4 s |             115 s |              165 s |            770 ms |
| Ancient, 4 players, after       |              16.7 s |              76 s |              108 s |            482 ms |
| Industrial, 4 players, before   |              82.4 s |             247 s |              247 s |            820 ms |
| Industrial, 4 players, after    |              57.4 s |             159 s |              159 s |            506 ms |
| Standard, 8 players, before     |              53.2 s |             380 s |              622 s |          3,163 ms |
| Standard, 8 players, after      |              34.0 s |             209 s |              337 s |          1,688 ms |

The industrial game has already reached its cap, so both of its bounds equal its flat
rate.

The cap is the assumption that decides the answer. The eight-player standard game was
still gaining 0.5 cities per turn at turn 100, at 33 cities, so nothing measured shows
where it stops. Without a cap, at that rate (132 cities at turn 300, 232 at turn 500):

| Subject, no cap             | 300 turns, linear | 300 turns, squared | 500 turns, linear | 500 turns, squared |
| :-------------------------- | ----------------: | -----------------: | ----------------: | -----------------: |
| Standard, 8 players, before |             9 min |             24 min |            27 min |              2.1 h |
| Standard, 8 players, after  |             5 min |             13 min |            14 min |              1.1 h |
| Ancient, 4 players, before  |             3 min |              8 min |             9 min |             43 min |
| Ancient, 4 players, after   |             2 min |              5 min |             6 min |             27 min |

The uncapped ancient rows assume 79 cities on a small world at turn 300, more than the
industrial game ever held there, so they are a loose ceiling. With the cap, the worst
case is about 10 minutes for 300 turns before this pass and under 6
after. No run reproduced games that take hours. They do fit squared growth wherever
cities keep growing past turn 100: an uncapped eight-player game reaches about 2 hours
over 500 turns before this pass and 1 hour after. Long wars would add unit-driven sight
costs on top. Active corporations are a cost these games never triggered.

#### Corporation Branch (Unmerged) Compared

No corporation was founded in any of these games, so this compares only the paths that
run while no corporation exists. The cost of active corporations is not measured here.

The corporation rules on `worktree-agent-a1512cf58901b57f1` (tip `449a89e`) were measured
with the same profiler patches, in a separate worktree. Each game ran with Corporations
loaded (the default set) and without it (`--optional` naming the other six feature
mods). The branch's per-branch-city rules ran only on their empty paths.

| Game (100 turns, seed 3)    | Main, with Corporations | Branch, with | Branch, without |
| :-------------------------- | ----------------------: | -----------: | --------------: |
| Ancient start: wall         |                  20.4 s |       20.9 s |          20.8 s |
| Ancient start: allocated    |                19.6 GiB |     19.6 GiB |        18.8 GiB |
| Industrial start: wall      |                  82.4 s |       82.0 s |          74.8 s |
| Industrial start: allocated |               145.0 GiB |    144.9 GiB |       129.3 GiB |

The branch costs nothing measurable over main. Every entry is within run-to-run noise,
and the games are identical. The corporation entries themselves (planner, valuers and
maintenance) total under 70 ms per game. Loading Corporations at all cost about 8 s and
16 GiB per industrial game, on main and on the branch alike. That cost was
`CorporationEffects` gathering a player ledger for every city calculation. Commit
`8c23637` removes it here.

The branch and this pass both change `BuildingEffects.cs`, `CorporationService.cs` and
`AiContext.cs`, but their hunks do not overlap. `git merge-tree` merges the two tips
without conflict. The branch's `BuildingEffects` change adds resources a city's own rules
supply (`ResourceAccess.Supplied`) to `Available`, which `604af14` does not touch. Its
`MaintenanceHundredths` still sums only the corporations in
`CorporationCityState.Corporations` (through `Present`). The early return of `8c23637`
for a city with none therefore stays correct after a merge. The cost of active
corporations, per branch city per turn, is still unmeasured. It needs a game that founds
some.

#### Still Slow, Ranked

1. **Per-city modifier ledgers.** `modifiers:collect` takes 38 s of processor time and
   40 GiB in the industrial game, over about 590,000 collections at 64 µs each. Every
   query rebuilds a city's ledger from all sources, and building effects walk every
   building of the owner's other cities, so the cost grows with cities squared. The
   structural fix is a ledger cached per city and invalidated by a state revision
   (buildings, civics, techs, ownership, population). A smaller step is a read scope
   spanning one turn system or one AI segment. Either needs every source's inputs named
   for invalidation, which is more than one clean commit. The 38 s is the ceiling of the
   gain. `8c23637` removed the one known nested collection for cities without
   corporations. A source that reaches a player calculation some other way would still
   be counted twice.
2. **Turn systems on the turn's own thread.** They take 13 s of the industrial game and
   253 of 532 ms of a turn at turn 100, the largest part of a late turn. Unlike AI
   planning, they run on one thread and nothing overlaps them. `cities:growth`,
   `cities:production`, `cities:assign_work`, `military:units` and `economy:gold` lead,
   at 1.1 to 2.0 s each. The city systems spend their time mostly in city ledgers, so
   item 1 would shrink them. Running independent players' city systems in parallel
   would need the rules to show that those players' cities never read one another.
3. **Great-person planning** (20 s). An idle great person scores every city on the map
   every turn, and each foreign city's trade profit gathers three ledgers. Caching the
   trade profit per partner within one evaluation would remove most of it. Scoring
   trade missions only for the few best partners would remove more, but it changes AI
   choices.
4. **Sight per move step.** `world:sight_recompute` takes 2 s and grows with units
   squared. An incremental recompute for the moving team alone would make it linear.
   Every team's plot memories must still refresh at each recompute, though, so it needs
   per-team observer counts per plot. Not attempted.
5. **AI planners' fixed cost.** `ai:military`, `ai:citizens`, `ai:cities` and
   `ai:economy` each take 3 to 15 ms per player per turn. The economy planner tries
   slider settings by issuing commands and recalculating, and each issued command renews
   the AI context's cached views.
6. **Allocation in general.** The industrial game still allocates 0.7 GiB per turn at 30
   cities, and collector pauses are still 10 to 20% of a turn.

Pass 14 below measured the late game these 100-turn runs could not reach. Item 1 is no
longer the largest cost there, and items 2 and 3 shrank by an order of magnitude. Pass 14
gives the new ranking.

### Pass 14: The Late Game, Corporations and Save Continuation

- **Date**: 2026-11-23
- **Commits**: `f0931c9` (tooling); `6476e52`, `a1f4ba8`, `1420193`, `833dacb`, `8fb21e0`,
  `927a9f6`, `013cf71`, `e853955`, `35c72a6`, `cb1626d`, `ef252c2`, `5085f9d`, `29cea16`
  (optimizations)

The owner's hours-long games were six-player AI games with the optional feature mods
loaded. Pass 13 measured only the first 100 turns. This pass continued the real slow game
from its saves, measured where late turns go, and removed the largest costs. Each
optimization leaves the game byte-identical, as described under "Verification".

#### Method and Saves

- Same machine as Pass 13, Release builds, other agents sharing it (load average 1 to 4).
  The tables lead with processor time, calls and bytes. Wall times of single turns vary
  by about 10%.
- `mise run ai:play -- --load SAVE --turns N --profile DIR --save OUT` continues a save
  with the profiler attached (`f0931c9`). It prints `final turn T hash H` on stderr. The
  option has no automated test. Every measurement below exercised it, and so did a
  determinism check that reloads every 2 turns.
- Content: the default optional set (Votes, ProjectsVictory, Corporations, Espionage,
  Events, ScriptedWonders, Revolution) on `main` merged with the corporation source-rule
  branch (`449a89e`). The saves were made on that branch and loaded without forcing: their
  content hash matches this tree.
- "Before" is `f0931c9`, the merged tree with only the `--load` tooling, run from a
  separate worktree. "After" is the final tree.

| Save                    | Turn | Cities | Units | Players | Buildings per city | Deals (all, active) |
| :---------------------- | ---: | -----: | ----: | ------: | -----------------: | ------------------: |
| `/tmp/ariel/ai.json`    |  251 |     32 |   147 |      14 |               10.7 |                1, 0 |
| `/tmp/ariel/ai330.json` |  331 |     35 |   178 |      14 |               19.3 |               23, 6 |
| `/tmp/ariel/ai450.json` |  451 |     38 |   234 |      14 |               35.6 |              76, 13 |

The game is seed 3, six AI players, small continents world, ancient start. Its 14
players include barbarians and minor players. The turn-100 reference is a new game with
the same settings (`--seed 3 --players 6 --turns 100`), not a load of the same game.
The saves are local files from that game and are not in the repository, so the numbers
below are reproducible only with copies of them.

**No corporation or guild exists in any of these saves.** The same holds after continuing
`ai450.json` for 100 more turns to turn 551 on the optimized tree. By then the AIs know 33
to 108 techs each and have built 26 Artist Guilds, which are ordinary buildings, but no
Guild Hall, Master Guild or headquarters. A 120-turn modern-era game
(`--era foundation:era_modern`, 107 to 115 techs) founded none either. The slowdown
below therefore follows buildings per city (10.7, then 19.3, then 35.6), deals and units.
Corporations do not cause it. To measure active corporations anyway, a scratch script
(`/tmp/ariel/p14/inject.py`, not committed) wrote a synthetic save, `ai450corp.json`,
from `ai450.json`. It founds eight corporations and guilds (Cereal Mills, Creative Constructions, Mining Inc, Civilized
Jewelers, East West Traders, Mobby Meats, the Bakers and Masons guilds) in the capitals
of the six major empires and opens 101 branches in their 32 cities, three per city. The
injection bypasses the site rules (resources, guild halls, headquarters conflicts, two
headquarters in one city), so its numbers show what such branches cost. They do not show
what an AI game would found.

#### Baseline: The Late Game Is Not Linear

Per-turn wall time, mean of the continued advances (the first advance, which compiles,
excluded):

| Turns continued                | Mean wall before | Mean wall after | Systems before / after | AI before / after | Allocated per turn before / after | Collector pauses per turn before / after |
| :----------------------------- | ---------------: | --------------: | ---------------------: | ----------------: | --------------------------------: | ---------------------------------------: |
| New game, 2-101 (same settings) |           225 ms |          207 ms |               44 / 41 ms |      159 / 156 ms |                  204 / 161 MiB |                              47 / 44 ms |
| `ai.json`, 252-271              |         2,172 ms |          902 ms |             560 / 168 ms |      1,600 / 721 ms |              3,170 / 1,105 MiB |                            347 / 210 ms |
| `ai330.json`, 332-371           |        10,552 ms |        1,537 ms |           2,761 / 318 ms |    7,779 / 1,206 ms |             13,484 / 2,107 MiB |                            982 / 239 ms |
| `ai450.json`, 452-471           |        46,678 ms |        3,842 ms |          13,689 / 819 ms |   32,976 / 3,010 ms |             48,883 / 4,709 MiB |                          3,599 / 597 ms |
| `ai450corp.json`, 452-455       |        61,058 ms |        5,023 ms |          11,826 / 792 ms |   49,218 / 4,217 ms |             64,554 / 6,038 MiB |                          4,899 / 803 ms |

Whole runs, as the profiler reports them: 40 turns from turn 331 took 428 s before and 67
s after, and 20 turns from turn 451 took 957 s before and 82 s after. A turn at turn 330
cost 32 times a turn at turn 100 before (10.6 s against 0.33 s for advances 92 to 101),
and a turn at turn 450 cost 144 times as much. City counts barely move (23, then 32, 36
and 38), so late-game size explains little of it. Two per-call costs grew with buildings
per city:

1. **Modifier collections.** `modifiers:collect` averaged 64 µs per call in Pass 13's
   industrial game. Here it averaged 876 µs at turn 331 and 2,462 µs at turn 451. It took
   1,128 s of processor time in the 20 turns from turn 451. Nearly all of it was rebuilding
   one city's running buildings (`CityData.Active`), about 154,000 times every four turns
   at turn 331, because each collection opens and closes its own city read scope.
   Rebuilding a city's running set cost about 400 µs, nearly all of it the power check.
   That check reads the connected resources of every city in the area, and with a
   resource deal running it asked the deal rules once per resource.
2. **Per-pair scans.** A building set was resolved with a scan of every pair of buildings
   for replacement. Building effects looked up every building's extra commerce for every
   building of every other city. Both scans grew with the square of 36 buildings.

Before, the largest entries over 20 turns from turn 451 (processor time over all threads):

| Entry                              | Calls before | Before ms | Before MiB | After ms | After MiB |
| :--------------------------------- | -----------: | --------: | ---------: | -------: | --------: |
| `modifiers:collect`                |      457,940 | 1,127,532 |    677,720 |   78,565 |    39,457 |
| `kernel:ai_turns` (AI waves, wall) |           20 |   683,831 |    281,634 |   62,589 |    27,909 |
| `ai:cities`                        |          992 |   450,081 |    203,480 |   49,222 |    22,190 |
| `ai:military`                      |          992 |   400,263 |    213,406 |   23,417 |    12,999 |
| `great_people:planner`             |          992 |   244,379 |    110,904 |   12,495 |     4,547 |
| `ai:economy`                       |          992 |   210,445 |    152,454 |   25,691 |    16,951 |
| `ai:citizens`                      |          992 |   145,717 |     74,060 |   26,531 |     9,594 |
| `military:units`                   |          280 |   117,087 |     69,487 |    3,201 |     2,750 |
| `ai:civics`                        |          992 |    74,696 |     25,931 |   19,955 |     3,541 |
| `SetCityAssignmentsCommand`        |        3,372 |    71,193 |     36,171 |   10,162 |     4,079 |

On the final tree `modifiers:collect` makes 258,437 collections in those 20 turns, about
half as many as before, mostly because of `5085f9d`.

Turn systems on the turn's own thread, the same 20 turns:

| System                   | Calls | Before ms | Before MiB | After ms | After MiB |
| :----------------------- | ----: | --------: | ---------: | -------: | --------: |
| `military:units`         |   280 |   117,087 |     69,487 |    3,201 |     2,750 |
| `cities:production`      |   280 |    28,632 |     21,151 |    2,555 |     1,787 |
| `great_people:births`    |   280 |    22,031 |     15,111 |      953 |       638 |
| `cities:growth`          |   280 |    20,010 |     20,714 |    3,369 |     2,612 |
| `economy:gold`           |   280 |    14,568 |     12,200 |    1,848 |     1,377 |
| `cities:assign_work`     |   280 |    12,463 |     10,354 |    1,652 |     1,107 |
| `global_warming:warming` |    20 |    12,451 |      9,079 |    1,343 |       863 |
| `religion:city_turns`    |   280 |    11,416 |      6,461 |      921 |       508 |
| `barbarians:behavior`    |    20 |     8,522 |      3,883 |      306 |       176 |
| `revolution:instability` |   280 |     6,157 |      5,397 |      998 |       598 |

#### Active Corporations and the Optional Mods

From `ai450corp.json`, 4 turns, before and after (processor time):

| Entry                       |   Calls | Before ms | After ms |
| :-------------------------- | ------: | --------: | -------: |
| `modifiers:collect`         | 138,031 |   431,977 |   27,697 |
| `ai:civics`                 |     235 |   141,526 |    6,140 |
| `corporations:civic_valuer` |  10,854 |   122,096 |      398 |
| `corporations:planner`      |     235 |     8,881 |    2,131 |
| `corporations:executives`   |      74 |     1,611 |      390 |

The final tree makes 79,092 collections there. The before count, 138,031, is from before
`5085f9d`.

Active branches made a turn about 30% dearer than the same save without them, both before
(61.1 s against 46.7 s) and after (5.0 s against 3.8 s). Ranked by cost before this pass:

1. **The corporations civic valuer** (122 s in 4 turns). It weighed every city's branches
   and maintenance again for each civic the planner scored. Each city's maintenance
   gathered a whole player calculation, and each player calculation rebuilt every city's
   running buildings for anarchy modifiers.
2. **Branch effects in city calculations.** `CorporationEffects` took 18.4 s of 48.4 s of
   collection time in an instrumented run after the first eight optimizations. Per branch city it
   asked the resource counts once for each active branch (`ConsumedBonusCount`), then again
   in maintenance, and it gathered the player's maintenance modifier as well
   (`x:corp_maintenance` 12.4 s, `x:corp_consumed` 6.9 s over 51,102 calls).
   `IsActive` (51,102 calls, 88 ms) and the headquarters branch count (4,464 calls, 155 ms)
   cost little.
3. **`corporations:planner`** (8.9 s before, 2.1 s after). `HeadquartersValuer` (4,159
   calls, 12 ms in 20 turns) and `ExecutiveWishes` (`corporations:executives`, 1.0 s in
   20 turns after) are minor.

Over 20 turns of the corporation save on the final tree, every optional mod's entries
together took 15.0 s of 104 s: `corporations:planner` 9.5 s, `corporations:civic_valuer`
2.1 s, `revolution:instability` 1.3 s, `corporations:executives` 1.0 s, `espionage:planner`
0.9 s, `revolution:planner` 0.2 s. `votes:planner`, `espionage:spies`,
`events:turn`, `scripted_wonders:city_turns`, `espionage:allocation`,
`revolution:rebellions`, `votes:sessions` and `corporations:maintenance` were each 20 ms
or less. In the plain save before this pass, `revolution:instability` was the largest
(6.2 s in 20 turns), and its city ledgers made it so.

#### Optimizations

Each row is measured on the same save and turn count before and after its commit, with a
byte-identical final save. "330" continues `ai330.json` for 4 turns, "450" continues
`ai450.json` for 4 turns, "corp" continues `ai450corp.json` for 4 turns, and "550"
continues the turn-551 save for 4 turns. Times are processor time for the entry named.

| Commit    | Change                                                         | Subject | Measure                       | Before             | After              |
| :-------- | :------------------------------------------------------------- | :------ | :---------------------------- | -----------------: | -----------------: |
| `6476e52` | One batched resource query for a city's traded resources       | 330     | `modifiers:collect`; run      | 45.3 s; 47.6 s     | 36.4 s; 39.5 s     |
| `a1f4ba8` | Power is asked only of a city holding a building that needs it | 330     | `modifiers:collect`; run      | 36.4 s; 39.5 s     | 20.8 s; 22.5 s     |
| `1420193` | Grants resolve only cities that built a granting building      | 330     | `modifiers:collect`; run      | 20.8 s; 22.5 s     | 18.2 s; 19.2 s     |
| `833dacb` | A scripted wonder is not resolved in a city without it         | 330     | `modifiers:collect`; run      | 18.2 s; 19.2 s     | 14.6 s; 14.2 s     |
| `8fb21e0` | The replacement scan visits only buildings that replace        | 330     | set resolution; run           | 6.5 s; 14.2 s      | 5.0 s; 13.8 s      |
| `927a9f6` | Empty per-building maps are passed over in building effects    | 330     | `modifiers:collect`; MiB      | 13.9 s; 16.9 GiB   | 10.3 s; 8.9 GiB    |
| `013cf71` | One city-group layout per resource count under exports         | 450     | `modifiers:collect`; run      | 24.3 s; 24.0 s     | 22.5 s; 22.6 s     |
| `e853955` | Only cities whose buildings reach others are resolved          | 450     | `modifiers:collect`           | 22.5 s             | 20.3 s             |
| `35c72a6` | Only cities that could run an anarchy building are resolved    | corp    | `modifiers:collect`           | 46.8 s             | 38.0 s             |
| `cb1626d` | Branch values gathered once per AI segment for all civics      | corp    | `corporations:civic_valuer`   | 4.3 s              | 0.5 s              |
| `ef252c2` | One resource query per branch city for outputs and maintenance | corp    | `modifiers:collect`; run      | 37.7 s; 31.9 s     | 28.7 s; 26.4 s     |
| `5085f9d` | Promotions validated only for the unit's combat type           | 450     | collections; `ai:military`    | 105,135; 7.2 s     | 54,028; 5.7 s      |
| `29cea16` | Hunting odds reckoned once per enemy tile                      | 550     | `ai:military`; run            | 114.1 s; 85.5 s    | 24.5 s; 38.5 s     |

The temporary instrumentation that found these is not committed. It covered time per
modifier source and scope, the parts of `CityData.Active`, the deal arithmetic, and
sampled call stacks of collections. One change was measured and dropped: memoizing
`ConnectedCounts` and city connections in the city read scope saved 1.5 s of 23 s.
Each city calculation asks every owned city's network only once, so the memo had nothing
to share, and it would have made World depend on the Cities read scope.

Every change keeps the result by construction. Each one either skips work that adds only
zero lines, or tests a necessary condition first: the running set only narrows the
constructed and granted buildings. The rest reuse a value that does not depend on the
loop variable, within a read that issues nothing. The ScriptedWonders pre-check changes
only which exception a save with an unknown building id raises.

#### Verification

- Final saves of the full continuations are byte-identical to the `f0931c9` baseline:
  `ai330.json` + 40 turns (turn 371, hash `2eea532a9f68e438`), `ai450.json` + 20 turns
  (turn 471, `6eedba278a9d2381`), `ai.json` + 20 turns (turn 271, `e23a15c0e47ebab3`), the
  new 100-turn game (turn 101, `9bf38cf7bc3e5245`), and `ai450corp.json` + 4 turns (turn
  455, `24a7278a6d3bddb2`).
- On the final tree, `ai450corp.json` + 4 turns prints the same hash with
  `--reload-every 2`, with `--background` and with `DOTNET_PROCESSOR_COUNT=1`. This covers
  the paths `mise run check:sync` never reaches: active branches, `AiContext.Derived`,
  deal groups.
- Repeating a 4-turn continuation reproduces its hash, with or without temporary
  instrumentation.

#### Results and the 450-Turn Estimate

| Run (profiled, headless)       | Before  | After  |
| :----------------------------- | ------: | -----: |
| New game, 100 turns            | 24.4 s  | 21.5 s |
| `ai.json`, 20 turns from 251   | 50.8 s  | 21.6 s |
| `ai330.json`, 40 turns from 331 | 428.3 s | 67.1 s |
| `ai450.json`, 20 turns from 451 | 956.5 s | 82.4 s |
| `ai450corp.json`, 4 turns       | 287.9 s | 26.2 s |

A turn at turn 450 now costs 14 times a turn at turn 100 (3.8 s against 0.27 s), against
144 times before. The estimate integrates per-turn wall time between four anchors:
advances 92 to 101 of the new game, and the mean turns from 251, 331 and 451. It
interpolates log-linearly, because the cost grew about fivefold per 100 turns before.

| 450-turn six-player game, headless | Turns 1-100 | 100-250 | 250-350 | 350-450 | Total     |
| :--------------------------------- | ----------: | ------: | ------: | ------: | --------: |
| Before                             |        24 s |   133 s |   481 s | 2,176 s | about 48 min |
| After                              |        22 s |    75 s |   116 s |   232 s | about 7.5 min |

Past turn 450 the armies grow: the continued game holds 614 units by turn 551. The before
curve, extrapolated at fivefold per 100 turns, would cost about 200 s per turn by turn
550 and several hours for turns 450 to 550. That fits the reported hours. Turns with active
corporations cost about 30% more than this estimate, and the Godot client adds its own work.
On the final tree, turns 552 to 555 take 8 to 9 s each (13 s for the first, which
compiles). Before the last commit they took 17 to 23 s.

#### Still Slow, Ranked

From the final tree, 4 turns from turn 551 (38 cities, 619 units, 38.5 s) and 20 turns
from turn 451:

1. **AI military planning with large armies.** `ai:military` took 24.5 s of processor time
   in 4 turns at turn 551. After `29cea16` it still previews combat for every hunter and
   tile in reach, and against every defender on that tile (`CombatService.BestDefender`).
   That cost grows with units squared.
2. **City calculations under resource exports.** `modifiers:collect` still averages 170 to
   290 µs per call. Its largest part is a city's available resources while its owner
   exports a resource: the export arithmetic traverses the network of every owned city
   (`ConnectedCounts`) for each city calculation, about 600 µs per city with deals. One
   traversal per network per read would need a cache that outlives one calculation.
3. **The per-city ledger cache (not built).** A ledger cached per city across calculations
   would need a state revision that every mutation bumps. Core has none: turn systems and
   commands write state directly, and about 20 modifier sources read inputs beyond the
   city (units on the tile, deals, other players' civics, timers), while AI forks plan in
   parallel. Its gain is bounded by `modifiers:collect`: 79 s of processor time, summed over
   threads, in the 20 turns from 451, against 82 s of wall time. Realistically it would save 30 to 50% of that,
   and it would mean touching every writer of city, player and team state.
4. **AI planners' fixed cost.** `ai:cities` (49 s in 20 turns from 451), `ai:citizens`
   (27 s), `ai:economy` (26 s), `ai:civics` (20 s) and `corporations:planner` (9.5 s with
   branches) lead. Each is now within a factor of two of the collections it makes.
5. **Collector pauses.** These are 16% of a late turn after this pass (597 of 3,842 ms at
   turn 451), against 8% before. Allocation fell tenfold, from 48.9 to 4.7 GiB per turn,
   but the AI threads still pause together.

### Pass 15: Turn Processing from Two Analyses

- **Date**: 2026-11-23
- **Commits**: `2e12f467`, `c0fc4b74`, `e2628324`, `08bd2df9`, `bb8398f1`, `61a2c023`,
  `274bd64c`, `0e6e067e`, `1ee3383f`, `db6640c6`, `312b2397`

The owner's priority was faster turns. Two independent analyses of the Pass 14 tree
proposed 32 candidates: fifteen from one (F1 to F15) and seventeen from the other (N1 to
N17). This pass checked each claim in the code, built the ones that held, and measured
each one on its own. Every accepted change leaves every final state byte-identical.

#### Method, Saves and Hashes

- Same machine as Pass 14, Release builds, other agents sharing it (load average 0 to 2).
  One heavy run at a time.
- Each step was measured with `mise run ai:play -- --load SAVE --turns 4 --profile DIR`.
  The figure is the mean wall time of the later turns, leaving out the first, which
  compiles. The same binary varies by about 5% on this machine, and by up to 20% when one
  turn is an outlier: same-binary runs from turn 451 spanned 1,498 to 1,868 ms. Small
  gains were therefore judged on the profiler entry they target, and on repeat runs.
- "451" continues `ai450.json` (Pass 14's save) for 4 turns. "552" continues the turn-551
  save from Pass 14 for 4 turns. "New game" is `--seed 3 --players 6 --turns 100`.
- Each `mod.json` bump changes the content hash, which a save must match to load. The
  measurement saves were therefore copied with the current tree's `ContentHash` and
  `Mods` header after every bump (a scratch script, not committed). Their game state was
  not touched.
- Reference hashes, unchanged by every commit: 451 + 4 turns `975963b591c0c71e`; 451 + 20
  turns `6eedba278a9d2381`; 552 + 4 turns `11d283f0b52d0e80`; new game `9bf38cf7bc3e5245`.
  On the final tree, `--verify-read-epochs` printed the same hashes for 451, 552 (with
  `--background`) and the new game. Pass 14's synthetic save with corporation branches
  (`ai450corp.json` + 4 turns, reheaded) printed Pass 14's `24a7278a6d3bddb2` with it,
  which covers active branches under the kernel memos.

#### Accepted

Turn walls are per later turn. Entries are processor time summed over threads, per 4
turns. Each "before" is the previous row's tree.

| Commit     | Candidate | Change                                                                      | 451 turn (ms)          | 552 turn (ms)   | Entry evidence                                                    |
| :--------- | :-------- | :-------------------------------------------------------------------------- | ---------------------: | --------------: | :---------------------------------------------------------------- |
| (baseline) |           | Pass 14 tree, `main` at `43c616cf`                                          |                  4,148 |           7,837 | 5,030 / 9,626 MiB per turn; collector 676 / 1,027 ms               |
| `2e12f467` | F1        | AI players run in turn by default (`AiWaveLimit = 1`)                       |                  2,506 |           5,011 | new game 22.2 → 14.7 s                                            |
| `c0fc4b74` | F2 (N8 step 2) | No heal rate for a unit already at full health                              |                  2,411 |           4,169 | `military:units` 755 → 172 (451), 5,182 → 610 (552)                 |
| `e2628324` | F4 (N2)   | Cached `Ledger`, lazy totals, `ModifierSink.Total`                          |                  2,277 |           3,956 |                                                                   |
| `08bd2df9` | F5        | A movement query derives per-unit step inputs once                          |                  2,254 |           3,388 | `ai:military` 5,987 → 3,613 (552)                                  |
| `bb8398f1` | F8 (N4 steps 1-2) | Best defender from defence strength alone                                   |                  2,258 |           3,319 | `ai:military` 3,613 → 3,294; collections 46,623 → 42,537 (552)      |
| `61a2c023` | F3        | Kernel read epochs; one city read scope per AI segment                      |                  1,804 |           2,621 | `ai:cities` 2,709 → 1,113 (451)                                    |
| `274bd64c` | N3        | Modifier sums kept per read epoch (generalised to every scope)              |          1,655 to 1,818 |           2,484 | collections 18,867 → 12,128 (451), 42,537 → 23,171 (552)             |
| `0e6e067e` | F7 (N5)   | Commerce sliders priced on an economy forecast                              |                  1,661 |           2,273 | `ai:economy` 991 → 254 (451), 1,078 → 255 (552)                      |
| `1ee3383f` | N6        | City groups with one flood per group; deals scanned once                    |  1,763 → 1,518 to 1,584 | 2,302 → 2,099 | 1,197 → 1,069 MiB (451)                                            |
| `db6640c6` | (new)     | Connected resource counts kept per read epoch (`ReadEpochMemo`)             |         1,498 to 1,553 |           1,990 | exporter path inside epochs 1,226 → 711 (451)                      |
| `312b2397` | N11 (half)| The citizen planner asks garrison and threat without a full city view       |         1,502 to 1,616 |           1,976 | `ai:citizens` 946 to 1,108 → 794 to 847 (451), 880 to 958 → 707 (552) |

The `1ee3383f` row is measured against a fresh run of `0e6e067e`, taken back to back,
which explains why its "before" differs from the row above. Those notes:

- **F1.** The speculative waves cost more than they saved at every size measured
  (seed 3, 100 turns, total wall, equal hashes in every pair):

  | Game                      | Max waves | Width 1 | Width 2 |
  | :------------------------ | --------: | ------: | ------: |
  | 2 players                 |    11.9 s |  11.7 s |         |
  | 4 players                 |    15.6 s |  13.2 s |         |
  | 6 players                 |    22.2 s |  14.7 s |  17.6 s |
  | 8 players                 |    29.7 s |  16.8 s |         |
  | 8 players, standard world |    31.1 s |  16.9 s |  22.2 s |

  On the final tree the gap remains: 6 players 14.0 to 14.5 s at width 1 against 20.0 s
  at max, 8 players 15.8 s against 27.9 s, 451 1,541 to 1,564 ms per turn against
  2,339 ms. Waves stay available as an experimental, off-by-default preference,
  "Parallel AI planning (experimental)" on the setup screen under "This computer"
  (`"ParallelAiPlanning": true` in `user://preferences.json`; it asks for a quarter of
  the processors, two to four, so not the unlimited width measured above). The
  preference is per machine and never enters saves, hashes or lobbies. The lockstep
  tools still exercise waves. `hash-trace --waves N|max` selects the width, and `mise run check:sync` runs a
  fifth process with `--waves max`. Tests that are about waves pin
  `AiWaveLimit = int.MaxValue`.
- **F3 and N3.** The first analysis wanted one read scope per AI segment, held by the
  kernel. The second wanted a city sink cached in the Cities read scope. Both are built on
  one kernel seam, the read epoch (see DESIGN.md), which knows no rules. A debug guard,
  `Simulation.VerifyReadEpochs` (`hash-trace --verify-read-epochs`), hashes the state at
  each end of an epoch and throws on a difference. Its first run found that AI planners
  draw RNG outside commands, so an epoch promises the state and not the RNG stream.
- **F7.** The risks the first analysis left unchecked were checked: no subscriber reacts
  to a commerce-rate command, the recorded command list is compared nowhere, and
  `ResearchEconomyTests` checks that the forecast equals `Calculate` for 60 random rate
  vectors per player, with a process city, a city in disorder and an occupied city.
- **N6 and the epoch memo.** A temporary probe at 451 put `AvailableResourceCounts` at
  5.1 s of processor time per 4 turns over 7,508 calls, 2.2 s of it in group building.
  N6 made group membership one flood per group, through a default method on
  `ICityConnectionService` that a replacement service still answers pair by pair, and
  scanned the deals once. The remaining cost was each group member's own flood, which
  the epoch memo now shares within an AI segment. A memo of the whole group layout was
  tried on top and dropped: it reused 756 of 1,357 builds at about 0.1 ms each.

#### Rejected and Deferred

Every candidate that was not accepted, with its numbers. "Deferred" means measured only
as an upper bound, or not attempted.

| Candidate                      | Verdict  | Evidence                                                                                                                                                                                                                                             |
| :----------------------------- | :------- | :---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| N1 (trade routes)              | Rejected | Premise false. `MAX_TRADE_ROUTES` is `"0"` in the Foundation rule constants, so `TradeRouteService.Calculate` returns on its first line. City `TradeEffects` took 13 to 16 ms per 4 turns at 552 over 4,306 to 6,545 calls.                    |
| N7 (`MayRun` for free promotions) | Rejected | 552: 2,302 → 2,280 ms; 451: 1,763 → 1,870 ms (noise). This matches the first analysis's prototype. Unit `BuildingMilitaryEffects` is now 625 ms per 4 turns at 552, and the running set it needs is already in the read scope.          |
| F6 (batched eligibility)       | Rejected | F3 absorbed it. `ValidateBatch` only shares the city read scope, which epochs already share. A prototype that primed each city's buildings through it before scoring took `ai:cities` from 900 to 950 up to 1,195 ms at 451, because it serialised validation that `ParallelScoring` spreads. |
| N9 (`ProvidesPower` early exit) | Rejected | `ai:cities` 945 → 900 ms (451) and 612 → 657 ms (552); walls within noise. The resource answer it skips is already kept in the read scope.                                                                                                       |
| N13 (maintenance)              | Rejected | 451 wall 1,657 ms (one 2,340 ms outlier), 552 wall 2,009 against 1,990 ms. `economy:gold` 472 to 495 → 501 (451) and 638 → 612 (552); `ai:civics` 530 to 574 → 531 and 503 → 444; `ai:economy` 199 to 212 → 227 and 176 → 189. Mixed, within noise. |
| N17a (`Array.IndexOf` in naval entry) | Rejected | `ai:military` 1,225 to 1,356 → 1,267 ms (451), 2,419 to 2,674 → 2,317 ms (552); walls 1,677 and 2,005 ms. Within noise: naval units rarely test a land tile.                                                                                |
| N11, second half               | Not taken | Skipping the automatic assignment when production is emphasised changes which commands are issued.                                                                                                                                                |
| F9, N2 steps 3-4 (zero-value keys) | Deferred | Not measured. It spans building lines, `CityYields.AddPlot` and `PlotYieldService.AddThresholds`; the building lines alone took 416 ms per 4 turns at 451 in the probe.                                                                 |
| N4 step 3 (one preview for the chosen defender) | Deferred | Not measured. F8 took defender choice off the full calculation; `UnitOrders.Odds` still previews the chosen defender once more.                                                                                         |
| F10 (promotion keys)           | Deferred | Unit `PromotionEffects`: 53 ms per 4 turns at 552.                                                                                                                                                                                                   |
| F11 (scripted wonders per unit) | Deferred | Unit `ScriptedWonderService`: 427 ms per 4 turns at 552 over 16,230 calls.                                                                                                                                                                        |
| F12 (yes/no eligibility)       | Deferred | Not measured. Part of `ai:cities`: 587 ms per 4 turns at 552.                                                                                                                                                                                        |
| F13 (combat preview lookups)   | Deferred | Not measured. Inside `ai:military`.                                                                                                                                                                                                                  |
| F14 (wave traces)              | Moot     | Waves are off by default (F1).                                                                                                                                                                                                                       |
| F15 (kernel linear scans)      | Deferred | Not measured.                                                                                                                                                                                                                                        |
| N8 step 1 (unit effects once)  | Deferred | F2 took `military:units` to 172 ms (451) and 610 ms (552). This step is bounded by that.                                                                                                                                                        |
| N10 (civic effects)            | Deferred | City `CivicEffects`: 308 ms per 4 turns at 552.                                                                                                                                                                                                      |
| N12 (great person planner)     | Deferred | `great_people:planner`: 177 ms (451) and 194 ms (552) per 4 turns, from 2,370 and 6,331 ms before this pass.                                                                                                                                         |
| N14 (`UInt128` combat odds)    | Deferred | Not measured. F8 removed odds from defender choice.                                                                                                                                                                                                  |
| N15 (sea yields, state religion) | Deferred | City `ReligionEffects`: 277 ms per 4 turns at 552.                                                                                                                                                                                                 |
| N16 (corporation branches)     | Deferred | No corporation exists in the measured saves (Pass 14), so nothing could show a gain.                                                                                                                                                                 |
| N17b (culture anger players)   | Deferred | Not measured.                                                                                                                                                                                                                                        |

#### Where the Analyses Disagreed

- **Waves.** Only the first analysis measured the speculative waves, and it found that
  they cost more than they save (F1). The second analysis measured with waves on
  throughout. The first was right at every size tried.
- **Trade routes (N1).** The second analysis ranked this "very high". The first
  analysis's per-source timing showed it at 16 ms. The content switches trade routes off,
  so the first was right.
- **Free promotions (N7).** The second analysis expected a medium-high gain, and the first
  had prototyped it with none. Measured here with none.
- **Where the reuse lives (F3 against N3).** The first analysis wanted a kernel span and
  the second a Cities-scope cache. Both were built on the kernel span, so that World
  (connected resource counts) and Core (modifier sums) reuse it without a dependency on
  Cities.

#### Results

Final tree, three runs each, per later turn:

| Run                  | Before (Pass 14 tree) | After               | Systems before / after | AI before / after   | Allocated per turn before / after | Collector per turn before / after |
| :------------------- | --------------------: | ------------------: | ---------------------: | ------------------: | --------------------------------: | --------------------------------: |
| 451, 4 turns         |              4,148 ms | 1,541 to 1,564 ms   |         740 / 722-762 ms | 3,393 / 740-758 ms |                 5,030 / 1,008 MiB |                      676 / 76-88 ms |
| 552, 4 turns         |              7,837 ms | 1,981 to 2,066 ms   |       2,042 / 886-915 ms | 5,779 / 1,042-1,095 ms |               9,626 / 1,536 MiB |                   1,027 / 120-125 ms |
| 451, 20 turns        |              3,765 ms | 1,154 to 1,168 ms   |           810 / 565-584 ms |   2,943 / 560-564 ms |           4,710 / 1,018-1,049 MiB |                      604 / 75-78 ms |
| New game, 100 turns  |        22.2 s (total) | 13.4 to 14.5 s (total) |                      |                     |                       161 / 41 MiB |                          45 / 4 ms |

A turn at 451 is 2.7 times faster and a turn at 552 is 3.9 times faster. Turn systems
are now half of a late turn.

`mise run benchmark`, Pass 14 tree against the final tree:

| Benchmark                                         | Before      | After       |
| :------------------------------------------------ | ----------: | ----------: |
| World generation, seeds 1, 42, 987654             | 283.6, 190.8, 258.2 ms | 258.6, 182.0, 227.9 ms |
| Fifty-city turn                                   | 324 ms      | 262 ms      |
| Eight-player preparation (99 turns, save/load)    | 31,366 ms   | 13,192 ms   |
| Eight-player AI decisions at turn 100             | 192 ms      | 52 ms       |

#### Still Slow, Ranked

From the final tree, 4 turns at 451 and at 552 (processor time per 4 turns):

1. **City building effects outside read epochs.** `modifiers:collect` is 4.4 s (451) and
   5.9 s (552). City `BuildingEffects` made up 84% of it at 451 in the probe after N6, and
   68% at 552 on the final tree (4,006 of 5,912 ms). Most of that is a city's
   available resources while its owner exports one. Turn systems collect outside any epoch
   (2,600 of 3,971 calls at 451, 1.2 to 1.4 s), and every one floods each member of each
   group ahead of the city. One flood per group would need proof that the network's edges
   are symmetric. `RiverEdge` is not written symmetrically, which is why the group copies
   take the largest count over members. A read epoch around each turn system's read phase
   would need those systems split into a read pass and a write pass.
2. **AI military with large armies.** `ai:military` is 1.3 s (451) and 2.5 s (552), the
   largest planner. It still previews combat per hunter, tile and defender (F13, N14).
3. **City turn systems.** `cities:growth` (0.8 to 1.1 s), `cities:production` (0.6 to
   0.9 s), `economy:gold` (0.5 to 0.7 s) and `cities:assign_work` (0.5 to 0.6 s) run
   outside epochs, so each recalculates its city's ledgers.
4. **AI city and citizen planners.** `ai:cities` (1.0 and 0.6 s) and `ai:citizens` (0.9
   and 0.7 s). F12 and the second half of N11 would trim them.
5. **Unit-scope sources.** `BuildingMilitaryEffects` (625 ms) and `ScriptedWonderService`
   (427 ms) at 552, over 16,230 unit collections (F11).

______________________________________________________________________

## Comprehensive Performance Summary

The following table aggregates all recorded performance measurements across optimization
passes:

| Optimization Pass & Focus Area          | Primary Commit       | Workload / Operation                                       | Metric                   |               Before |             After |                               Improvement |
| :-------------------------------------- | :------------------- | :--------------------------------------------------------- | :----------------------- | -------------------: | ----------------: | ----------------------------------------: |
| **Pass 1: Renderer Invalidation**       | `821d3a1`            | Non-world UI actions (20 actions, tiny map)                | Terrain rebuild time     |              8.00 ms |           0.00 ms |                    **-100% (eliminated)** |
| **Pass 1: Startup Content Hashing**     | `821d3a1`            | Catalog freeze (20-rebuild median)                         | Wall time                |            365.31 ms |         266.43 ms |                                **-27.1%** |
|                                         |                      | Catalog total freeze                                       | Wall time                |            397.75 ms |         284.36 ms |                                **-28.5%** |
|                                         |                      | Catalog build allocation                                   | Managed bytes            |           315.61 MiB |        257.35 MiB |                    **-18.5% (-58.3 MiB)** |
|                                         |                      | First catalog load                                         | Wall time                |             2,568 ms |          2,099 ms |                                **-18.3%** |
| **Pass 1: Terrain Geometry Off-Thread** | `821d3a1`            | Huge world (672 chunks, seed 1)                            | Main-thread managed prep |            205.81 ms |           0.00 ms |                     **-100% (offloaded)** |
|                                         |                      | Huge world terrain total                                   | Wall time                |          1,932.71 ms |       1,792.75 ms |                                 **-7.2%** |
|                                         |                      | Huge world map refresh                                     | Traced wall time         |          2,147.20 ms |       1,944.26 ms |                                 **-9.5%** |
| **Pass 2: AI Multithreading**           | `d6d73ab`, `0685342` | Fifty-city turn (turn 2, 50 cities)                        | Turn execution time      |         83,120.65 ms |       2,139.04 ms |              **-97.43% (38.86× speedup)** |
|                                         |                      | 8-player 99-turn preparation                               | Elapsed time             |    >90 min (timeout) |     231,096.19 ms |                     **Finished (3m 51s)** |
|                                         |                      | Turn-100 8-player AI decisions                             | `RunAi()` duration       |         *Unrecorded* |       1,802.43 ms |                      *Baseline timed out* |
| **Pass 3: AI Serial Work Reduction**    | `c339ee2`, `3dc5495` | A\* pathfinding, spatial threat grid, goal reuse           | Operational work         | Linear scans / sorts |  Cached / indexed |                *Unmeasured runtime gains* |
| **Pass 4: World Generation**            | `7b12754`            | Standard world generation (84×52, 3 seeds)                 | Managed bytes            |      11.85–12.69 MiB |     6.33–6.35 MiB |                      **-46.6% to -50.0%** |
|                                         |                      | Standard world generation (84×52, 3 seeds)                 | Wall time                |       61.72–69.97 ms |    46.60–54.98 ms |                      **-14.9% to -24.5%** |
|                                         |                      | Gigantic world generation (200×120, 3 seeds)               | Managed bytes            |      58.84–60.00 MiB |   35.15–35.33 MiB |                      **-40.3% to -41.1%** |
|                                         |                      | Gigantic world generation (200×120, 3 seeds)               | Wall time                |     311.04–402.11 ms |  309.79–377.74 ms | **-0.4% to -9.4%** *(seed 42 Contention)* |
|                                         |                      | Standard session startup (84×52, 3 seeds)                  | Managed bytes            |     83.04–100.97 MiB |   74.10–91.38 MiB |                       **-9.5% to -10.8%** |
|                                         |                      | Gigantic session startup (200×120, 3 seeds)                | Managed bytes            |    279.27–294.62 MiB | 241.28–255.57 MiB |                      **-13.3% to -13.6%** |
|                                         |                      | Gigantic session startup (200×120, 3 seeds)                | Wall time                |       1,215–1,468 ms |    1,091–1,340 ms |                       **-7.3% to -12.9%** |
| **Pass 5: Idle CPU Reduction**          | `47b932f`            | Stationary idle (Vulkan Forward+, 1050 Ti)                 | Process CPU (1 core)     |                38.8% |             18.8% |                     **-51.5% (-20.0 pp)** |
| **Pass 6: Startup Latency**             | `7f865e4`            | Cold launch to setup screen (8 cores)                      | Wall time                |               8.65 s |            6.38 s |                                **-26.2%** |
|                                         |                      | Theme painting (overlapped with catalog)                   | Wall time                |          2,272.73 ms |       2,703.18 ms |            +18.9% *(absorbed by overlap)* |
|                                         |                      | Catalog load                                               | Wall time                |          2,372.03 ms |       1,454.04 ms |                                **-38.7%** |
|                                         |                      | Content digest (critical path)                             | Wall time                |          1,156.39 ms |              0 ms |           **-100% (prefetched off path)** |
|                                         |                      | Launch to setup screen (Xvfb, warm skin cache)             | Wall time                |               6.39 s |            3.56 s |                                **-44.3%** |
|                                         |                      | Launch to setup screen (Xvfb, empty skin cache, first run) | Wall time                |               6.39 s |            5.18 s |                                **-18.9%** |
| **Pass 7: HUD Invalidation**            | `4b6fd9d`            | Selection click (HUD)                                      | Median time              |             16.06 ms |           2.40 ms |                                **-85.1%** |
|                                         |                      | Top bar refresh alone                                      | Median time              |              6.08 ms |           0.42 ms |                                **-93.1%** |
|                                         |                      | Left panel (unit selected, excl. DescribeUnit)             | Wall time                |         3.30–3.94 ms |      0.08–0.18 ms |                                **-95.4%** |
|                                         |                      | Left panel (no selection, excl. DescribeUnit)              | Wall time                |         1.23–1.69 ms |      0.11–0.38 ms |                                **-77.5%** |
|                                         |                      | Unchanged selection click (repeat click)                   | UI update lines          |               1 line |           0 lines |                    **-100% (eliminated)** |
|                                         |                      | Research dialog reopen (unchanged session)                 | Wall time                |             14.00 ms |           0.07 ms |                                **-99.5%** |
|                                         |                      | Religion plate scrape pattern delta on click               | Differing pixels         |               594 px |              0 px |                        **-100% (stable)** |
| **Pass 8: Lockstep State Hash**         | `a17020e`, `4c63893` | Standard turn-100 8-player `ComputeHash`                   | Managed bytes per hash   |             8,923 KB |            827 KB |                                **-90.7%** |
|                                         |                      | Standard turn-100 8-player `ComputeHash`                   | Wall time per hash       |             14.90 ms |          13.03 ms |                                **-12.6%** |
|                                         |                      | Gigantic turn-1 8-player `ComputeHash`                     | Managed bytes per hash   |            31,002 KB |          2,327 KB |                                **-92.5%** |
| **Pass 9: AI Threat Grid Reuse**        | `4cb9780`            | Gigantic 8-player turns 21-30                              | Managed bytes per turn   |             483.4 MB |          481.4 MB |                                  **-0.4%** |
| **Pass 10: Culture Reach Cache**        | `4eb56de`            | Turn-100 culture spread and plot ownership (36 cities)     | Wall time                |              1.44 ms |           0.37 ms |                                **-74.3%** |
| **Pass 13: Long AI Games**              | `6eb72f2`..`8c23637` | 100-turn 4-player AI game, ancient start (seed 3)          | Wall time                |               20.5 s |            16.9 s |                                **-17.6%** |
|                                         |                      | 100-turn 4-player AI game, industrial start (seed 3)       | Wall time                |               82.4 s |            57.4 s |                                **-30.3%** |
|                                         |                      | 100-turn 4-player AI game, industrial start (seed 3)       | Managed bytes            |            145.0 GiB |          88.9 GiB |                                **-38.7%** |
|                                         |                      | 100-turn 8-player AI game, standard world (seed 3)         | Wall time                |               53.2 s |            34.0 s |                                **-36.1%** |
|                                         |                      | Eight-player preparation (`mise run benchmark`)            | Wall time                |            50,076 ms |         31,712 ms |                                **-36.7%** |
| **Pass 14: The Late Game**              | `6476e52`..`29cea16` | Six-player AI game, 40 turns from turn 331 (saved game)    | Wall time (profiled)     |              428.3 s |            67.1 s |                                **-84.3%** |
|                                         |                      | Six-player AI game, 20 turns from turn 451 (saved game)    | Wall time (profiled)     |              956.5 s |            82.4 s |                                **-91.4%** |
|                                         |                      | Same, per turn at turns 452-471                            | Managed bytes per turn   |           48,883 MiB |         4,709 MiB |                                **-90.4%** |
|                                         |                      | Turn-451 save with 101 corporation branches, 4 turns       | Wall time (profiled)     |              287.9 s |            26.2 s |                                **-90.9%** |
|                                         |                      | 450-turn six-player game (estimated from four anchors)     | Wall time                |         about 48 min |     about 7.5 min |                                  **-84%** |
| **Pass 15: Turn Processing**            | `2e12f467`..`312b2397` | Six-player AI game, per turn at turns 452-455 (saved game) | Wall time (profiled)     |             4,148 ms |    1,541–1,564 ms |                                **-62.7%** |
|                                         |                      | Same, per turn at turns 553-556 (saved game)               | Wall time (profiled)     |             7,837 ms |    1,981–2,066 ms |                                **-74.1%** |
|                                         |                      | Same, per turn at turns 452-471 (saved game)               | Managed bytes per turn   |            4,710 MiB |         1,049 MiB |                                **-77.7%** |
|                                         |                      | 100-turn six-player AI game, ancient start (seed 3)        | Wall time (profiled)     |               22.2 s |      13.4–14.5 s |                       **-35% to -40%** |
|                                         |                      | Eight-player preparation (`mise run benchmark`)            | Wall time                |            31,366 ms |         13,192 ms |                                **-57.9%** |
