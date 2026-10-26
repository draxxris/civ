# Performance and Optimization Log

This document provides instructions for executing operational benchmarks and profiling workloads, followed by a chronological record of performance optimizations implemented across the codebase.

---

## Benchmarking and Profiling Instructions

### Operational Benchmark Workloads

Benchmarks are executed via `mise run benchmark` with flags selecting specific scenarios:

```sh
# 1. Quick startup check (standard world, seed 42, 4 players)
mise run benchmark -- --quick

# 2. Expanded startup check (standard and gigantic worlds, seeds 1, 42, 987654, 4 players)
mise run benchmark -- --startup

# 3. World generation and session startup (isolates generation and session start, skips catalog rebuilds)
mise run benchmark -- --world

# 4. Operational turn and AI workloads (8-player game, 99-turn preparation, turn-100 decisions)
mise run benchmark
```

#### Workload Descriptions

- **`--quick`**: Loads the catalog once, performs paired serial/preparation catalog rebuilds, generates a standard world (seed 42), starts the session, founds a city, and measures cold/warm production queries.
- **`--startup`**: Performs paired serial/preparation catalog rebuilds, then measures world generation, session startup, city founding, and production choices across standard ($84 \times 52$) and gigantic ($200 \times 120$) worlds for seeds 1, 42, and 987654.
- **`--world`**: Loads the catalog once and focuses exclusively on world generation (plot allocation, terrain, rivers, features, bonuses, goodies) and `GameSession.Start` (including initial opponent AI setup) across standard and gigantic maps for seeds 1, 42, and 987654. It skips catalog rebuilds, founding, and turn simulation.
- **Default (no flag)**: Runs multi-turn simulation benchmarks, including the 50-city turn workload and the 8-player scenario with turn-100 AI decision evaluation (`decisions.RunAi()`).

### Profiling and Diagnostic Tracing

#### Startup Phase Tracing
To inspect elapsed timings for mod discovery, assembly loading, sidecar parsing, validation, canonical hashing, world generation stages, and starting positions:

```sh
CIV_TRACE_STARTUP=1 mise run start:game
# Or with the benchmark harness:
CIV_TRACE_STARTUP=1 mise run benchmark -- --quick
```

#### Renderer Rebuild Tracing
To trace Godot renderer stage timings (snapshot capture, worker mesh generation, native array packing, surface uploads, and scatter planning/rebuilds):

```sh
mise run build:game
mise exec -- godot-mono --path src/Civ.Godot -- --stage map --world tiny --seed 1 --opponents 0 --trace-performance
```

The `--` separates Godot arguments from game runtime arguments.

### Controlled Measurement Methodology

To ensure repeatable and reliable performance comparisons:

1. **Release Builds**: Always measure Release configurations (`dotnet build --configuration Release`).
2. **Disable Tiered JIT Transition Effects**: Set `DOTNET_TieredCompilation=0` so short microbenchmarks avoid dynamic tier-0 to tier-1 recompilation jitter:
   ```sh
   env -u CIV_TRACE_STARTUP DOTNET_TieredCompilation=0 mise run benchmark -- --world
   ```
3. **Disable Active Tracing**: Opt-in traces (`CIV_TRACE_STARTUP=1` or verbose loggers) introduce I/O overhead. Disable them during timing comparisons.
4. **Warmup and Sampling**: Use at least one warmup iteration followed by multiple sequential sample runs (typically 3 to 5 runs), reporting the median of medians rather than isolated runs.
5. **Exact Hash Verification**: Every benchmark must assert bit-for-bit determinism. Content SHA-256 hash, generated plot/RNG hash, session hash, and founded state hash must match the baseline across all samples.
6. **Allocation Tracking**: Track managed bytes using `GC.GetTotalAllocatedBytes(true)`, which queries precise runtime allocation counters without triggering a forced garbage collection.

---

## Chronological Optimization Timeline

```
[Phase 1: Oct 17, 2026] Engine Invalidation, Content Hashing, & Off-Thread Terrain
                        (Commit: 821d3a1)
       │
       ▼
[Phase 2: Oct 17, 2026] AI Multithreading & Speculative Parallel Waves
                        (Commits: d6d73ab, 0685342, b81909f, 4282ff6, f064004, 482ecc4)
       │
       ▼
[Phase 3: Oct 17, 2026] AI Serial Work Reduction & Spatial Indexing
                        (Commits: c339ee2, e6239f8, da5fa32, 9ff9998, 193567e, 3dc5495)
       │
       ▼
[Phase 4: Oct 17, 2026] World Generation Geometry & River Traversal
                        (Commit: 7b12754)
       │
       ▼
[Phase 5: Oct 18, 2026] Idle CPU: Terrain Ray Pick Caching & Unit Height Sampling
                        (Commit: 47b932f)
       │
       ▼
[Phase 6: Oct 18, 2026] Startup Latency: Theme Overlap & Lazy Content Hash
                        (Uncommitted worktree changes)
```

---

### Phase 1: Engine Invalidation, Content Hashing, and Off-Thread Terrain Preparation

- **Date**: 2026-10-17
- **Primary Commit**: [`821d3a1`](file:///home/gordon/workspace/civ/docs/OPTIMIZE.md) (*Optimize terrain geometry preparation, content hashing, and map invalidation*)

#### 1. Map Renderer Rebuild Reduction
- **Motivation**: `GameSession.Submit` increments `WorldRevision` on every successful command, while selection and UI events increment `Revision`. `MapRenderer` was rebuilding terrain height fields, signatures, snapshots, and textures even for non-world actions (e.g. selection, menu toggles, rejected actions).
- **Changes**:
  - In `src/Civ.Godot/TerrainRenderer.cs` and `src/Civ.Godot/MapRenderer.cs`, decoupled world terrain refreshes from non-world simulation revisions (`RefreshWorld` vs. `RefreshOverlay`).
  - Keyed terrain snapshot and signature rebuilds strictly on `GameSession.WorldRevision`.
  - Non-world actions (Tab, Escape, selection changes) no longer trigger terrain rebuilds.
  - Uploaded overlay marks (`tileB`) once after overlay generation without touching land geometry.
- **Measurements**:
  - Tested on tiny world with 117 terrain chunks under Vulkan Forward+ via 20 UI actions (10 Tab, 10 Escape):
    - **Terrain stage rebuild time**: dropped from **8.00 ms median** (2–14 ms range) to **0.00 ms** (zero snapshot/signature rebuilds).
    - **Overlay upload**: median 0.11 ms.
    - Token stage medians remained 4.5 ms before vs. 4.785 ms after (confirming terrain isolation).

#### 2. Startup Content Hashing Optimization
- **Motivation**: Startup profiling revealed that `ContentDb.Freeze` spent >1.3s creating intermediate `JsonElement` syntax trees for each definition and serializing full canonical JSON strings into `Digest` buffers.
- **Changes**:
  - In `src/Civ.Core/Content/ContentDb.cs`, serialized content definitions as UTF-8 byte fragments using a custom `HashDefinition` converter, eliminating intermediate `JsonElement` allocations while preserving runtime type dispatch and root writer depth.
  - In `src/Civ.Core/Game/CanonicalJson.cs`, modified `Digest` to stream canonical UTF-8 directly into `ArrayBufferWriter<byte>` and hash the span directly, avoiding intermediate string allocations, UTF-8 decodes, and UTF-8 re-encodings.
- **Measurements**:
  - Measured across 20 catalog rebuilds (`CIV_TRACE_STARTUP=1 mise run benchmark -- --quick`):
    - **Catalog hash/freeze median**: fell from 365.31 ms to 266.43 ms (**27.1% reduction**).
    - **Total freeze median**: fell from 397.75 ms to 284.36 ms (**28.5% reduction**).
    - **Per-build memory allocations**: fell from 315.61 MiB to 257.35 MiB (**58.26 MiB / 18.5% reduction**).
    - **First catalog load**: fell from 2,568 ms to 2,099 ms median (**18.3% reduction**).
    - Content hash remained exactly bit-for-bit identical (`D2A65A5162647FA8CA9D3837EC5666C6283B9682EA1AD8FB17F0DD1204DE948E`).

#### 3. Off-Thread Terrain Geometry Preparation
- **Motivation**: `TerrainRenderer.RebuildWorld` spent significant main-thread time converting raw mesher vertex, normal, UV, and index data into Godot surface arrays.
- **Changes**:
  - In `src/Civ.Godot/TerrainRenderer.cs`, parallelized ground, water, and river surface preparation across worker threads inside the existing `Parallel.For` loop using `SurfaceData` and `PreparedChunk` records.
  - Workers compute vertex positions, normals, UVs, colors, wet masks, and front-face indices concurrently.
  - The main thread exclusively handles native `Godot.Collections.Array` packing, `ArrayMesh` construction, material assignments, and scene tree node updates.
  - Gated diagnostic stopwatches and per-chunk logging behind `MapRenderer.LogTimings`.
- **Measurements**:
  - Measured across 3 runs on huge world (672 chunks, seed 1, zero opponents):
    - **Main-thread managed preparation**: dropped from **205.81 ms to 0.00 ms** (moved to workers, totaling 468.95 ms summed worker intervals).
    - **Terrain total time**: decreased from 1,932.71 ms to 1,792.75 ms (**139.96 ms / 7.2% reduction**).
    - **Map refresh total**: decreased from 2,147.20 ms to 1,944.26 ms (**9.5% reduction**).
    - Native array packing and surface upload remained 124.28 ms before vs. 147.55 ms after.
    - Record-sequence SHA-256 matched identically (`B53018458D3645BBAFB800D73968D4401FF687B15CCE56A087403C1766F9E05B`).

---

### Phase 2: AI Multithreading and Speculative Parallel Turns

- **Date**: 2026-10-17
- **Primary Commits**:
  - [`d6d73ab`](file:///home/gordon/workspace/civ/docs/OPTIMIZE.md): *Parallelize read-only candidate scoring in the standard AI planners*
  - [`0685342`](file:///home/gordon/workspace/civ/docs/OPTIMIZE.md): *Run independent AI turns in speculative parallel waves*
  - [`b81909f`](file:///home/gordon/workspace/civ/docs/OPTIMIZE.md): *Track complete unit mutations in speculative turns and bound concurrency*
  - [`4282ff6`](file:///home/gordon/workspace/civ/docs/OPTIMIZE.md): *Document AI multithreading performance results*
  - [`f064004`](file:///home/gordon/workspace/civ/docs/OPTIMIZE.md): *Invalidate speculative plans after global entity changes*
  - [`482ecc4`](file:///home/gordon/workspace/civ/docs/OPTIMIZE.md): *Document AI multithreading architecture and safety invariants*

#### Architecture and Safety Invariants
1. **Intra-Planner Parallel Candidate Scoring**:
   - `ParallelScoring.Select` in `SitePlanner`, `CityPlanner`, `ResearchPlanner`, `WorkerPlanner`, `MilitaryPlanner`, and `AttackPlanner` evaluates independent read-only candidate items concurrently across worker threads above candidate thresholds.
   - Candidates are scored without mutating simulation state or drawing from `Simulation.Rng`. Results sort deterministically by score with ordinal/entity ID tie-breaking.
   - Scoring runs serially during speculative turns (`Simulation.IsParallelAiTurn`) to prevent threadpool oversubscription.
2. **Read-Only Query Memoization**:
   - `[ThreadStatic] CityReadScope` caches building activity, power status, and modifiers during citizen allocation.
   - `ResourceNetwork.Modes` evaluates transport connections lazily per query.
   - `INavalBlockadeService.BlockadedTiles` resolves blockaded water plots once per query rather than per plot.
3. **Fast In-Memory State Cloning**:
   - `GameState.Clone()`, `Plot.Clone()`, and `ExtensionState.Clone()` produce independent simulation copies for `Simulation.ForkSpeculative()` without full-save canonical normalization.
4. **Wave-Based Speculative Parallel Turns**:
   - `Simulation.RunAi()` delegates to `AiTurnSpeculator`, partitioning uncommitted AI players into speculative waves.
   - Player $p_0$ plans directly against the authoritative simulation; players $p_1 \dots p_{m-1}$ plan concurrently on isolated state forks.
   - Plans commit sequentially in ascending `Player.Id` order by replaying recorded `GameCommand` sequences against authoritative state.
5. **Safety and Conflict Guards**:
   - **Active Mutation Footprint**: Plans conflict if any tile in an earlier player's active mutation footprint (unit moves, city founding/capture/razing, border changes, improvements, routes) lies within 5 hexes (`ActiveFootprintRadius = 5`) of a candidate plan's footprint. Moving settlers and explorers expand footprints by radius 3.
   - **Global Entity-Aggregate Protection**: Changes to entity counters (`NextCityId`, `NextUnitId`), entity membership/ownership, unit health/XP/promotions, city buildings/population/extensions, or power inputs trigger global entity-aggregate conflicts for subsequent plans in the wave.
   - **RNG and Diplomacy Consistency**: Rejects plans if RNG draws or team war relations diverge.
   - **Fingerprint Verification**: Plan commit verifies that pre/post-command RNG streams and state digests match the fork execution.

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

---

### Phase 3: AI Serial Work Reduction and Spatial Indexing

- **Date**: 2026-10-17
- **Primary Commits**:
  - [`c339ee2`](file:///home/gordon/workspace/civ/docs/DESIGN.md): *Reuse AI planning calculations and index spatial queries*
  - [`e6239f8`](file:///home/gordon/workspace/civ/docs/DESIGN.md): *Reuse exact AI paths within unchanged planning segments*
  - [`da5fa32`](file:///home/gordon/workspace/civ/docs/DESIGN.md): *Prune rejected AI choices and reuse candidate eligibility*
  - [`9ff9998`](file:///home/gordon/workspace/civ/docs/DESIGN.md): *Cache sight levels within each visibility recomputation*
  - [`193567e`](file:///home/gordon/workspace/civ/docs/DESIGN.md): *Retain AI strategic goals and safe civilian destinations*
  - [`a295bf0`](file:///home/gordon/workspace/civ/docs/DESIGN.md): *Validate AI isolation and intent replay across independent games*
  - [`3dc5495`](file:///home/gordon/workspace/civ/docs/DESIGN.md): *Document AI serial optimization architecture, cache boundaries, and benchmark limits*

#### Architectural Work Reductions
1. **Calculation Reuse**: `AiContext.Segment` memoizes unit role counts, queued production counts, city views, production eligibility, nearest-city queries, and campaign targets across unchanged planning segments.
2. **Path Query Reuse**: `AiContext.FindPath` caches exact queries up to $\min(256, \max(1, \text{units}) \times 8)$ entries per segment. `PathfindingService` combines cost and parent maps into a single local table, eliminating dictionary growth allocations.
3. **Candidate Pruning**: Wonder budgets and worker prerequisite filters skip candidate evaluations and yield previews; `AiOptions.Best` selects top legal candidates without allocating sorted ranking lists.
4. **Visibility Memoization**: `VisibilityService.Recompute` caches directional terrain sight levels per plot and team water sight in call-local arrays and dictionaries.
5. **Spatial Indexing**: Tile-bucket threat strength grids (`ThreatStrength`) replace repeated whole-map enemy unit scans; settlement exclusion masks and nearest-city distance fields replace per-candidate city distance scans.
6. **Strategic Goal Retention**: Unchanged civics and primary land campaigns are retained for up to 3 turns; valid settler and worker travel destinations are retained for up to 12 turns.

#### Measurement Status
- Operational gains were verified through algorithmic inspection of removed scans, sorts, allocations, and re-evaluations. Dedicated before-and-after wall clock benchmark tables were not recorded for these serial commits (documented as unmeasured runtime gains in `tools/Civ.Benchmarks/README.md`).

---

### Phase 4: World Generation Geometry and River Traversal

- **Date**: 2026-10-17
- **Primary Commit**: [`7b12754`](file:///home/gordon/workspace/civ/docs/OPTIMIZE.md) (*Optimize world generation geometry and river traversal*)
- **Baseline Revision**: `fc7e514df97345fdda5d861be7dacac626e011c3`

#### Implementation Changes
1. **Hexagonal Grid Intervals (`src/Civ.Core/HexGrid.cs`)**:
   - Replaced sorted range tree construction with direct enumeration of row intervals (`RangeRows`, `RangeColumns`).
   - Each row produces clipped column intervals in ascending tile order.
   - Wrapped intervals split at the seam; full-width intervals emit each column once; intermediate arithmetic uses `long` to prevent radius overflow.
   - `HexGrid.Neighbors`: Skips the per-query `HashSet` deduplication when geometry ensures directions cannot produce duplicate tiles (retained for wrapped maps of width 1 or 2).
2. **River Traversal Deduplication (`src/Mods/World/ContinentsGenerator.cs`)**:
   - River drainage generation terminates early upon reaching an already drawn drainage suffix, as each tile has a single parent.
   - A generation-local bitmap records completed paths.
   - Bounded direction loops replace captured LINQ closures for each river edge.
3. **Harness & Verification (`tools/Civ.Benchmarks`, `tests/Civ.Tests/HexGridTests.cs`)**:
   - Added `--world` benchmark option for isolated world generation and session startup timings.
   - Added independent breadth-first range verification tests covering bounded, wrapped, narrow, and single-row maps.

#### Benchmark Measurements

Controlled comparison settings: Fedora 44 x64, Intel Core i7-7700HQ, .NET SDK 10.0.401, `DOTNET_TieredCompilation=0`, 4 players, east–west wrapped map. Values report medians of three process medians.

##### 1. World Generation Workload
*Includes plot allocation, terrain, rivers, features, bonuses, and goodies. Excludes catalog rebuilds, player setup, and AI.*

| World | Seed | Before ms | After ms | Time Delta | Before MiB | After MiB | Allocation Delta |
| :--- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| **Standard 84×52** | 1 | 69.97 | 54.98 | **-21.4%** | 11.99 | 6.34 | **-47.1%** |
| **Standard 84×52** | 42 | 63.46 | 54.03 | **-14.9%** | 11.85 | 6.33 | **-46.6%** |
| **Standard 84×52** | 987654 | 61.72 | 46.60 | **-24.5%** | 12.69 | 6.35 | **-50.0%** |
| **Gigantic 200×120** | 1 | 311.04 | 309.79 | **-0.4%** | 58.84 | 35.15 | **-40.3%** |
| **Gigantic 200×120** | 42 | 320.29 | 377.74 | +17.9% | 59.77 | 35.18 | **-41.1%** |
| **Gigantic 200×120** | 987654 | 402.11 | 364.39 | **-9.4%** | 60.00 | 35.33 | **-41.1%** |

*Note: Gigantic seed 42 wall-clock increase reflected host scheduling and CPU contention; allocation reduction remained uniformly ~41%.*

##### 2. Full Session Startup Workload
*Includes generation, player setup, starting site selection, and initial opponent AI.*

| World | Seed | Before ms | After ms | Time Delta | Before MiB | After MiB | Allocation Delta |
| :--- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| **Standard 84×52** | 1 | 244.93 | 322.63 | +31.7% | 100.97 | 91.38 | **-9.5%** |
| **Standard 84×52** | 42 | 250.68 | 242.86 | **-3.1%** | 83.04 | 74.10 | **-10.8%** |
| **Standard 84×52** | 987654 | 247.86 | 244.10 | **-1.5%** | 95.50 | 85.24 | **-10.7%** |
| **Gigantic 200×120** | 1 | 1467.85 | 1339.56 | **-8.7%** | 279.27 | 241.28 | **-13.6%** |
| **Gigantic 200×120** | 42 | 1253.19 | 1091.04 | **-12.9%** | 288.90 | 250.10 | **-13.4%** |
| **Gigantic 200×120** | 987654 | 1215.94 | 1127.57 | **-7.3%** | 294.62 | 255.57 | **-13.3%** |

##### Correctness and Determinism
- Content hash matched baseline: `58BF65EEC063C432AF274B66DC6400087BFFED318F955AF7121F89A1010F6008`.
- Generated plot/RNG hashes (standard: seeds 1, 42, 987654; gigantic: seeds 1, 42, 987654):
  - `F6436E2BE45A3B35585BA2A6535BF37EEB421AA27533A3CF2C1C19DED6575E68`
  - `2B182D6951E35D73E6AA2731FCB16608EAABA3E8E252E1E8516D74A801AAAD31`
  - `3B64D6B8A97CED32F2C7B34C668C4DE4D4061EB6AE384E0A34039F6B3428F526`
  - `0282F40B80F873E914539A92F63CC3BDAFDB8885C68914DF6650120F66BD599C`
  - `08EA1DAA7B56715204DAFF29AA462308EEFCF9CF0318A8B2F58946422D0B5FBB`
  - `0A63FED13BBADAF6F0818E15DFA17FEE1AB9B78E0E7C44D6BDFC5B5BD43DE5E1`
- Session hashes matched baseline in identical order:
  - `46F651468826615C`, `D90B5CF7C47CD23D`, `46DFA66041914FFE`, `CDFD92CD292B0C64`, `1AD5F7CBE2AF0F16`, `BD3C3D8FE93FB0FB`.

---

### Phase 5: Idle CPU Reduction, Terrain Ray Pick Caching, and Unit Height Sampling

- **Date**: 2026-10-18
- **Primary Commit**: [`47b932f`](file:///home/gordon/workspace/civ/docs/OPTIMIZE.md) (*Reduce idle CPU by caching terrain picks and skipping stationary ground samples*)

#### Root Causes
- Profiling identified two sources of excessive CPU work during stationary idle gameplay.
- First, `GameView.HoveredHex` executed terrain ray picking every frame with a stationary pointer.
- Second, `UnitActor.UpdateGroundPose` evaluated two identical terrain height queries every frame for each stationary unit.
- A 10-second .NET sampled-thread-time trace before the change attributed about 23% of sampled thread time to the call `TerrainRay.Pick` Below.
- This trace value measures sampled thread time, not the percentage of total process CPU.

#### Implementation Changes
1. **Terrain Pick Caching (`src/Civ.Godot/GameView.cs`)**:
   - The engine now caches the terrain pick result.
   - The cache key stores pointer coordinates, camera transform, camera FOV, viewport size, world revision, UI interception state, and forced hover.
   - When one of these input properties changes, the view recomputes the ray pick.
   - When the session revision changes, hover descriptions and move previews still update.
2. **Stationary Ground Pose Sampling (`src/Civ.Godot/UnitActor.cs`)**:
   - In `src/Civ.Godot/UnitActor.cs`, `UpdateGroundPose` inspects procedural motion (`Shake` and `Lunge`).
   - When procedural motion is zero, the method avoids height difference computation.
   - Unit animation and the 60 FPS target frame rate remain unchanged.

#### Measurements and Evidence
- The team collected measurements on Godot Vulkan Forward+ with an NVIDIA GeForce GTX 1050 Ti.
- The test environment ran a standard map with default seed 1 in a stationary 1280x720 window.
- Each sample recorded process `utime+stime` from `/proc/PID/stat` over 10 seconds after startup.
- The values are normalized to one CPU core.
- Process CPU utilization decreased from 38.8% before the change to 18.8% after the change.
- The engine maintained approximately 60 FPS in both runs.
- These values represent single local samples.
- They do not constitute a controlled benchmark or a guaranteed reduction on other hardware.

#### Validation
- The command `dotnet build src/Civ.Godot` completed with zero warnings and zero errors.
- The command `mise run test` passed 1,315 tests (37 Core, 147 Mod, and 1,131 Engine tests).
- The default test run excludes end-to-end tests and extractor tests.
- The change added no new automated tests.
- The team performed no interactive regression tests and no GPU profiling.

---

### Phase 6: Startup Latency through Theme Overlap and a Lazy Content Hash

- **Date**: 2026-10-18
- **Primary Commit**: uncommitted worktree changes (not yet committed)

#### Implementation Changes
1. **Theme Painted While the Catalog Loads (`src/Civ.Godot/Main.cs`)**:
   - The automatic startup paths (headless, `--screenshot`, `--fps-probe`, `--skip-mod-manager`) call `BeginModStartup()` before `Ui.InstallTheme(GetWindow())`.
   - The 2.3-2.7 s procedural brass-theme painting therefore runs while the catalog loads on the thread pool, instead of strictly after it.
   - The interactive path keeps the original order because `BeginModStartup()` opens the mod manager, which builds `Control` nodes immediately and needs the theme installed first.
   - A startup that fails before the load begins (no usable mods, a damaged mod profile, an invalid selection) skips the theme: it has already queued its own quit, and painting would only delay the exit by about 2.7 s.
2. **Lazy, Prefetched Content Digest (`src/Civ.Core/Content/ContentDb.cs`, `src/Civ.Client.Core/GameCatalog.cs`)**:
   - `Freeze()` no longer computes the SHA-256 digest inline. It captures the definition array for the digest input under the `content hash input` trace phase, assigns a fresh `Lazy<string>` (`LazyThreadSafetyMode.ExecutionAndPublication`), then freezes the registries under `registry freeze`.
   - `ContentDb.ContentHash` is computed on first read under its own `content hash` trace phase. Registry enumeration order is ordinal before and after the freeze, so the digest is byte-identical; the content-hash test literals in `tests/Civ.Tests/ContentHashTests.cs` pass unchanged.
   - `ContentDb.PrefetchContentHash()` starts the digest on the thread pool, and `GameCatalog.Load` calls it once content is frozen. Freezing itself still never spawns background work, so tests and benchmarks that build catalogs directly stay single-threaded.

#### Measurements
- Machine: Fedora 44 x64, 8 cores, NVIDIA GeForce GTX 1050 Ti, .NET SDK 10.0.402, Debug build.
- Command: `CIV_TRACE_STARTUP=1 godot-mono --path src/Civ.Godot -- --stage setup --screenshot /tmp/shots/a-after.png`, timed from process start to the `screenshot ...: Ok` line, one launch at a time with `sleep 20` between launches.
- **Before**: theme 2,272.73 ms; `catalog load` 2,372.03 ms (content freeze 1,218.14 ms, of which 1,156.39 ms was the inline digest); total **8.60 s**, **8.69 s** (median 8.65 s, traced run 7.50 s).
- **After**: theme 2,703.18 ms, now overlapping the catalog; `catalog load` 1,454.04-1,557.93 ms (registry freeze 83.86-114.02 ms, digest 1,379.27-1,409.88 ms on the thread pool); total **6.19 s**, **6.38 s**, **6.50 s** (median 6.38 s, traced run 6.23 s).
- Cold launch is about 2.3 s shorter (-26%). The theme painting is roughly 0.4 s slower while the catalog's worker threads run, which the overlap absorbs.
- The interactive mod-manager path gains nothing: it still paints the theme serially before the mod manager appears.

#### Validation
- `dotnet build Civ.slnx -c Debug --no-restore` completed with zero warnings and zero errors.
- `mise run test` passed 1,297 tests (37 content-import, 122 core, and 1,138 mod tests).
- Setup-screen screenshots are byte-identical across runs of one build. Between the old and new order, 0.55% of pixels differ, confined to thin plate strokes and glyph edges (maximum channel delta 55); layout, colours and text are unchanged.
- The changes added no new automated tests, and no E2E, extractor, world-generation, or benchmark runs were performed.

#### Painted Skin Texture Cache
- All 109 skin textures (27 plate frames, 34 icons, 3 backdrop layers, 45 scrape-overlay layers) are written to `user://skin-cache/v1/x1/` as PNGs by the launch that paints them, and loaded from there by every later launch. Keys are FNV-1a hashes of the paint inputs, and `SkinCache.Version` has to be bumped whenever the painting changes.
- Measured with `CIV_TRACE_STARTUP=1 godot-mono --path src/Civ.Godot -- --stage setup --screenshot /tmp/shots/s.png --frames 3`, one launch at a time with `sleep 30` between launches:
  - **Theme**: cold **2,845.28 ms**, **2,892.56 ms**, **2,907.31 ms** (2,829.6-2,883.9 ms of it inside `SkinCache`); warm **130.69 ms**, **136.67 ms**, **151.64 ms** (115.2-131.9 ms inside `SkinCache`), and **343.01 ms** when the cache files were not in the page cache (immediately after a build).
  - **Cache on disk**: 524,889 bytes in 109 files.
- End-to-end launch numbers are only worth reading from an unthrottled display. On this Wayland session a game window that is not in front renders at about 1 fps, so launch-to-screenshot totals from it are frame-quantised and useless: the same build measured 5.5-5.9 s both cold and warm. Measure on a virtual X server instead, which still reaches the real GPU:
  `Xvfb :99 -screen 0 1600x900x24 -nolisten tcp &` then `DISPLAY=:99 CIV_TRACE_STARTUP=1 godot-mono --path src/Civ.Godot -- --stage setup --screenshot /tmp/shots/s.png --frames 3`
  - **Launch to screenshot**, same command on `DISPLAY=:99`: original code **6,390 ms**; this build with an empty `skin-cache` **5,180 ms** (theme 2,757.22 ms painted, `catalog load` 1,299.25 ms); this build with a warm `skin-cache` **3,560 ms** (theme 135.45 ms loaded, `catalog load` 1,158.49 ms). So the whole startup work landed **6.39 s -> 3.56 s (-44%)**, of which the texture cache is the last 1.6 s.
  - The cache saves less than the 2.62 s it removes from the theme because painting already overlaps the catalog load (see the overlap change above), so part of the 2.9 s was being hidden.
- Setup-screen screenshots: the painting launch and the pre-cache build are byte-identical. A cached launch is byte-identical to the painting one once the scrape overlays' tile phase is pinned, which shows the textures themselves round-trip exactly: `BrushedStyleBox` seeds that phase from a canvas-item RID, and the cached launch renders loading-screen frames the painting launch hides behind its 2.9 s block (a measured 38 RIDs earlier), so its overlays land at other offsets.
- A cache that is missing, unreadable, truncated or corrupt is repainted and overwritten: a launch with one corrupt frame, one truncated overlay layer and one unreadable backdrop layer painted 9 textures, loaded 100, and exited 0.

---

## Comprehensive Performance Summary

The following table aggregates all recorded performance measurements across optimization phases:

| Optimization Phase & Focus Area | Primary Commit | Workload / Operation | Metric | Before | After | Improvement |
| :--- | :--- | :--- | :--- | ---: | ---: | ---: |
| **Phase 1: Renderer Invalidation** | `821d3a1` | Non-world UI actions (20 actions, tiny map) | Terrain rebuild time | 8.00 ms | 0.00 ms | **-100% (eliminated)** |
| **Phase 1: Startup Content Hashing** | `821d3a1` | Catalog freeze (20-rebuild median) | Wall time | 365.31 ms | 266.43 ms | **-27.1%** |
| | | Catalog total freeze | Wall time | 397.75 ms | 284.36 ms | **-28.5%** |
| | | Catalog build allocation | Managed bytes | 315.61 MiB | 257.35 MiB | **-18.5% (-58.3 MiB)** |
| | | First catalog load | Wall time | 2,568 ms | 2,099 ms | **-18.3%** |
| **Phase 1: Terrain Geometry Off-Thread** | `821d3a1` | Huge world (672 chunks, seed 1) | Main-thread managed prep | 205.81 ms | 0.00 ms | **-100% (offloaded)** |
| | | Huge world terrain total | Wall time | 1,932.71 ms | 1,792.75 ms | **-7.2%** |
| | | Huge world map refresh | Traced wall time | 2,147.20 ms | 1,944.26 ms | **-9.5%** |
| **Phase 2: AI Multithreading** | `d6d73ab`, `0685342` | Fifty-city turn (turn 2, 50 cities) | Turn execution time | 83,120.65 ms | 2,139.04 ms | **-97.43% (38.86× speedup)** |
| | | 8-player 99-turn preparation | Elapsed time | >90 min (timeout) | 231,096.19 ms | **Finished (3m 51s)** |
| | | Turn-100 8-player AI decisions | `RunAi()` duration | *Unrecorded* | 1,802.43 ms | *Baseline timed out* |
| **Phase 3: AI Serial Work Reduction** | `c339ee2`, `3dc5495` | A* pathfinding, spatial threat grid, goal reuse | Operational work | Linear scans / sorts | Cached / indexed | *Unmeasured runtime gains* |
| **Phase 4: World Generation** | `7b12754` | Standard world generation (84×52, 3 seeds) | Managed bytes | 11.85–12.69 MiB | 6.33–6.35 MiB | **-46.6% to -50.0%** |
| | | Standard world generation (84×52, 3 seeds) | Wall time | 61.72–69.97 ms | 46.60–54.98 ms | **-14.9% to -24.5%** |
| | | Gigantic world generation (200×120, 3 seeds) | Managed bytes | 58.84–60.00 MiB | 35.15–35.33 MiB | **-40.3% to -41.1%** |
| | | Gigantic world generation (200×120, 3 seeds) | Wall time | 311.04–402.11 ms | 309.79–377.74 ms | **-0.4% to -9.4%** *(seed 42 Contention)* |
| | | Standard session startup (84×52, 3 seeds) | Managed bytes | 83.04–100.97 MiB | 74.10–91.38 MiB | **-9.5% to -10.8%** |
| | | Gigantic session startup (200×120, 3 seeds) | Managed bytes | 279.27–294.62 MiB | 241.28–255.57 MiB | **-13.3% to -13.6%** |
| | | Gigantic session startup (200×120, 3 seeds) | Wall time | 1,215–1,468 ms | 1,091–1,340 ms | **-7.3% to -12.9%** |
| **Phase 5: Idle CPU Reduction** | `47b932f` | Stationary idle (Vulkan Forward+, 1050 Ti) | Process CPU (1 core) | 38.8% | 18.8% | **-51.5% (-20.0 pp)** |
| **Phase 6: Startup Latency** | *uncommitted* | Cold launch to setup screen (8 cores) | Wall time | 8.65 s | 6.38 s | **-26.2%** |
| | | Theme painting (overlapped with catalog) | Wall time | 2,272.73 ms | 2,703.18 ms | +18.9% *(absorbed by overlap)* |
| | | Catalog load | Wall time | 2,372.03 ms | 1,454.04 ms | **-38.7%** |
| | | Content digest (critical path) | Wall time | 1,156.39 ms | 0 ms | **-100% (prefetched off path)** |
| | | Launch to setup screen (Xvfb, warm skin cache) | Wall time | 6.39 s | 3.56 s | **-44.3%** |
| | | Launch to setup screen (Xvfb, empty skin cache, first run) | Wall time | 6.39 s | 5.18 s | **-18.9%** |
