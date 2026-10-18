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
- Repeated catalog builds with loaded assemblies and fresh content.
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
The tool reports the median, minimum, maximum, and process-wide allocated bytes.
Hash calculations run outside the timed samples.
Each repeated catalog, generation, session, and founding sample must match its warmup hash.
The output includes the content hash, generated plot/RNG hash, session hash, and founded state hash for comparisons between builds.

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
