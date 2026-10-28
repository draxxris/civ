# Operational benchmarks

For repeatable headless economic measurements, use `mise run ai:eval`. See
[AI economic evaluation](AiEvaluation/README.md) for metrics, baseline comparisons, and
rule limitations. This mode records results without AI edits. It is separate from timing
benchmarks. Run the focused startup workload in Release:

```sh
mise run benchmark -- --quick
```

`--quick` uses a standard world with seed 42 and four players. For standard and gigantic
worlds with seeds 1, 42, and 987654, run the expanded startup workload:

```sh
mise run benchmark -- --startup
```

For world generation and session startup only, run:

```sh
mise run benchmark -- --world
```

`--world` loads the catalog once and skips paired catalog rebuilds. It measures standard
and gigantic worlds with seeds 1, 42, and 987654. Each world uses four players. The
workload measures world generation and `GameSession.Start`. It does not measure
founding, production choices, or 99-turn AI preparation.

Neither `--quick` nor `--startup` runs the old 99-turn AI preparation. `--terrain`
selects the terrain workload. Without a workload flag, the tool runs the original turn
and AI workloads.

The startup workload measures:

- The first catalog load, including discovery, assembly loading, content validation,
  hashing, and setup choices.
- Paired serial and preparation catalog builds with loaded assemblies and fresh content.
- World generation without player setup or AI.
- `GameSession.Start`, including the initial opponent AI.
- City founding with fresh sessions prepared outside each timed sample.
- `DescribeCity` for the founded city.
- Cold `ProductionChoices` for all kinds, with an empty eligibility cache in each fresh
  session.
- Warm `ProductionChoices` for all kinds, with the eligibility cache populated by the
  warmup.

Each cold sample uses a fresh `GameSession` around `SaveGame.Load` of the founded save.
Save creation and session preparation run outside the timed samples. Cold and warm
samples must return identical choices without changes to the authoritative state or RNG.

Repeated measurements use one warmup and five samples. `Measure` results report median,
minimum, and maximum wall time, process CPU time, and process-wide allocated bytes.
Process CPU time includes all process threads, garbage collection, and adjacent
measurement probes. It is an approximate supporting metric, separate from wall time.
Generation, session, and founding hash calculations run outside the timed samples.
Catalog measurements include content hashing as part of each build. Each repeated
generation, session, and founding sample must match its warmup hash. The output includes
the content hash, generated plot/RNG hash, session hash, and founded state hash for
comparisons between builds.

## Serial and preparation comparison

Both variants reuse the mod instances from the first catalog load. The serial baseline
wraps each instance in an `IMod` delegate that hides `IPreparableMod`. The wrapper
forwards `Register` to the original instance. Both variants use the same source order,
roots, manifests, and registration code. Neither variant repeats discovery or assembly
loading. Each catalog build creates fresh content and setup choices.

The tool warms both variants once, then measures five pairs. The serial variant runs
first in pairs 1, 3, and 5. The preparation variant runs first in pairs 2 and 4. Both
warmups and all ten samples must exactly match the first-load content hash. A mismatch
stops the benchmark with an error.

Preparation uses at most two workers, capped by the available processor count. The
workers read and parse only the typed sidecars that each `IPreparableMod` inventory
lists. Registration remains serial in dependency order. Registry writes, overrides, and
service registration retain their serial order. Mods without `IPreparableMod` use serial
reads during registration. Unlisted typed reads also use the serial fallback.

Each build owns its typed snapshot, keyed by mod, path, and result type. Repeated reads
of a listed file/type pair share the parsed object within that build. Later builds read
fresh values, not a process-wide cache. Sidecars must remain unchanged during a build.
`ReadText` and `Exists` still access the live source.

Preparation does not promise a speed gain. Host load, garbage collection, and filesystem
caches can change the results. The paired measurements compare catalog rebuilds, not
cold-process startup.

For comparable results, stop other CPU workloads and disable startup tracing. The first
catalog measurement includes JIT and serializer initialization, but it does not imply a
cold filesystem cache. The results have no timing thresholds.

## Startup tracing

For phase timings during normal game startup, set `ARIEL_TRACE_STARTUP=1` before the
process starts:

```sh
ARIEL_TRACE_STARTUP=1 mise run start:game
```

The trace writes to stderr. It covers mod discovery loads, registration, reference
validation, requirement validation, hashing, catalog choices, world generation phases,
and starting positions.

## Renderer tracing

For renderer rebuild timings, build the game and pass `--trace-performance` as a game
argument:

```sh
mise run build:game
mise exec -- godot-mono --path src/Ariel.Godot -- --trace-performance
```

The `--` after the project path separates game arguments from Godot arguments. Renderer
tracing is separate from `ARIEL_TRACE_STARTUP`. The environment variable measures
startup phases, not renderer rebuilds.

## AI performance and serial optimization evidence

### 99-turn default benchmark status

The repository's default benchmark mode (`tools/Ariel.Benchmarks/Program.cs` invoked
without `--quick`, `--startup`, or `--terrain`) executes an 8-player scenario on a
standard map that advances 99 turns of AI preparation before measuring a single turn of
AI decisions via `decisions.RunAi()`.

Under current runtime restrictions and testing discipline:

- The default 99-turn benchmark is not run as a verification prerequisite.
- No baseline-versus-optimized timing comparison is recorded for the 99-turn workload.
- No replacement benchmark or multi-turn simulation benchmark was created as an
  implementation prerequisite.

### Unmeasured runtime gains across optimization areas

Serial work reductions eliminate redundant operations across eight areas in AI
decision-making and surrounding simulation systems:

1. **Calculation reuse**: `AiContext.Segment` memoizes role counts, queued production
   counts, city views, production eligibility, nearest-city answers, and campaign
   targets geography across unchanged planning segments.
2. **Path query reuse and local search tables**: `AiContext.FindPath` caches exact
   (unit, destination, attack mode) queries up to a segment-bound cap; garrison
   reachability paths are carried directly into commands; `PathfindingService` merges
   A\* cost and parent maps into a single local table, eliminating a second dictionary
   and its growth allocations.
3. **Candidate pruning and allocation reduction**: Early wonder-budget checks and worker
   build prerequisites skip expensive candidate evaluations and yield previews;
   `AiOptions.Best` selects top legal candidates without allocating sorted ranking
   lists; garrison priority is sorted once for both defensive passes.
4. **Non-AI turn optimization**: `VisibilityService.Recompute` caches directional
   terrain sight levels once per queried plot and team water-sight capability once per
   queried team in call-local arrays and dictionaries.
5. **Spatial indexing**: Tile-bucket threat strength grids (`ThreatStrength`) replace
   repeated whole-map enemy unit scans; settlement exclusion masks and nearest-city
   distance fields in `SitePlanner.BuildGeography` replace per-candidate city distance
   scans.
6. **Strategic goal retention**: Unchanged civic and primary land campaign evaluations
   are retained for up to 3 turns, skipping full candidate evaluation and ranking on
   unchanged turns.
7. **Continuing civilian missions**: Valid settler and worker travel destinations are
   retained for up to 12 turns, skipping full destination searches and ranking while the
   mission remains valid.
8. **Sorting and allocation reduction**: Nearest-city queries use distance/ID tuple
   minima, eliminating ordered-iterator allocations.

All performance gains are documented based on code inspection of removed operations
(scans, sorts, queries, allocations, and re-evaluations). Elapsed execution times,
memory allocation reductions, and wall-clock speedup ratios remain unmeasured; no
percentage speedup is claimed.

### Operational trade-offs and performance characteristics

- **Cache invalidation overhead**: Segment caches in `AiContext` are invalidated and
  discarded after every successful command (`Issue`) and explicit `Refresh`. On very
  large maps with frequent commands, allocating and populating the spatial threat grid
  (`int[Grid.Count]`) per querying segment introduces allocation and initialization
  overhead, representing an unmeasured trade-off against eliminated linear enemy scans.
- **Worker fallback search overhead**: When candidate improvement tiles in the top batch
  of 8 are path-blocked, `WorkerPlanner.ChooseJob` evaluates subsequent batches of 8
  tiles until reachable work is found or candidates are exhausted. In turns with blocked
  tiles or high mission invalidation rates, this adds search work relative to the prior
  static 8-candidate shortlist.
- **Speculative replay validation overhead**: During speculative parallel waves,
  replayable `AiPlanningCommand` records recompute detached intent and verify canonical
  `ExpectedIntent` digests on authoritative state before committing, adding CPU
  validation overhead to prevent transient intent drift.
- **Bounded path caching**: Path reuse in `AiContext.FindPath` is capped at $\\min(256,
  \\max(1, \\text{units}) \\times 8)$ entries per segment. Excess queries fall back to
  uncached deterministic A\* search to bound memory consumption.
- **Deferred shared searches**: Broader path sharing (multi-unit routes, reverse
  distance fields, multi-goal search modifications) was evaluated and deferred due to
  directional costs, unit-specific domain/promotion rules, dynamic diplomatic borders,
  attack combat legality, and A\* tie-break preservation. Buffer pooling was deferred
  pending evidence of allocation benefit.

### Unrun validation checks

To maintain clear verification accounting, the following test workloads were not run
during validation:

- **Broad default test suite**: `mise run test` was not run because its category filters
  select untagged 20-turn and 100-turn simulation tests. Focused tests used exact-name
  filters.
- **E2E test suite**: The full E2E suite (~8 minutes) was not run; pre-existing
  `Unit cannot attack` issues and five known city-capture test failures remain
  unassessed.
- **Extractor test suite**: Not run under repository test conventions.
- **Long simulation scenarios**: Long-running simulations
  (`Four_computer_players_develop_and_fight_over_200_turns`,
  `Identical_games_and_a_midgame_save_have_equal_hashes_at_every_turn`, 20-turn
  speculative tests, and `Strong_army_declares_war_and_captures_a_weak_neighbour`)
  remain unrun.
- **99-turn default benchmark preparation**: Unrun as described above.
