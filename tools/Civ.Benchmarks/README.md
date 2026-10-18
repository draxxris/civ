# Operational benchmarks

Run the focused startup workload in Release:

```sh
mise run benchmark -- --quick
```

`--quick` uses a standard world with seed 42 and four players.
For standard and gigantic worlds with seeds 1, 42, and 987654, run the expanded startup workload:

```sh
mise run benchmark -- --startup
```

Neither workload runs the old 99-turn AI preparation.
Without these flags, the tool runs the original turn and AI workloads.
`--terrain` selects the terrain workload.

The startup workload measures:

- The first catalog load, including discovery, assembly loading, content validation, hashing, and setup choices.
- Paired serial and preparation catalog builds with loaded assemblies and fresh content.
- World generation without player setup or AI.
- `GameSession.Start`, including the initial opponent AI.
- City founding with fresh sessions prepared outside each timed sample.
- `DescribeCity` for the founded city.
- Cold `ProductionChoices` for all kinds, with an empty eligibility cache in each fresh session.
- Warm `ProductionChoices` for all kinds, with the eligibility cache populated by the warmup.

Each cold sample uses a fresh `GameSession` around `SaveGame.Load` of the founded save.
Save creation and session preparation run outside the timed samples.
Cold and warm samples must return identical choices without changes to the authoritative state or RNG.

Repeated measurements use one warmup and five samples.
The tool reports the median, minimum, and maximum for elapsed time and process-wide allocated bytes.
Generation, session, and founding hash calculations run outside the timed samples.
Catalog measurements include content hashing as part of each build.
Each repeated generation, session, and founding sample must match its warmup hash.
The output includes the content hash, generated plot/RNG hash, session hash, and founded state hash for comparisons between builds.

## Serial and preparation comparison

Both variants reuse the mod instances from the first catalog load.
The serial baseline wraps each instance in an `IMod` delegate that hides `IPreparableMod`.
The wrapper forwards `Register` to the original instance.
Both variants use the same source order, roots, manifests, and registration code.
Neither variant repeats discovery or assembly loading.
Each catalog build creates fresh content and setup choices.

The tool warms both variants once, then measures five pairs.
The serial variant runs first in pairs 1, 3, and 5.
The preparation variant runs first in pairs 2 and 4.
Both warmups and all ten samples must exactly match the first-load content hash.
A mismatch stops the benchmark with an error.

Preparation uses at most two workers, capped by the available processor count.
The workers read and parse only the typed sidecars that each `IPreparableMod` inventory lists.
Registration remains serial in dependency order.
Registry writes, overrides, and service registration retain their serial order.
Mods without `IPreparableMod` use serial reads during registration.
Unlisted typed reads also use the serial fallback.

Each build owns its typed snapshot, keyed by mod, path, and result type.
Repeated reads of a listed file/type pair share the parsed object within that build.
Later builds read fresh values, not a process-wide cache.
Sidecars must remain unchanged during a build.
`ReadText` and `Exists` still access the live source.

Preparation does not promise a speed gain.
Host load, garbage collection, and filesystem caches can change the results.
The paired measurements compare catalog rebuilds, not cold-process startup.

For comparable results, stop other CPU workloads and disable startup tracing.
The first catalog measurement includes JIT and serializer initialization, but it does not imply a cold filesystem cache.
The results have no timing thresholds.

## Startup tracing

For phase timings during normal game startup, set `CIV_TRACE_STARTUP=1` before the process starts:

```sh
CIV_TRACE_STARTUP=1 mise run start:game
```

The trace writes to stderr.
It covers mod discovery loads, registration, reference validation, requirement validation, hashing, catalog choices, world generation phases, and starting positions.
## Renderer tracing

For renderer rebuild timings, build the game and pass `--trace-performance` as a game argument:

```sh
mise run build:game
mise exec -- godot-mono --path src/Civ.Godot -- --trace-performance
```

The `--` after the project path separates game arguments from Godot arguments.
Renderer tracing is separate from `CIV_TRACE_STARTUP`.
The environment variable measures startup phases, not renderer rebuilds.
