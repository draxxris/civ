# Agent Guidelines

## Testing

Run tests using `mise`:

- `mise run test`: Runs the default test suite (skips E2E, extractor, and world
  generation tests).
- `mise run test:nobuild`: Re-runs the default test suite without rebuilding first.
- `mise run test:worldgen`: Runs world generation tests (`Category=WorldGen`). Run this
  whenever modifying world generation code (e.g., `src/Mods/World/`, map generator
  registrations, `MapSpec`/`HexGrid` layout, terrain/relief generation, or river and
  cliff generation).
- `mise run test:e2e`: Runs end-to-end simulation tests.
- `mise run test:extractor`: Runs extractor tests.

### Running and Testing the Game

Run and test the game using a virtual framebuffer (`Xvfb`) instead of directly on a
Wayland desktop, unless the developer specifically requests otherwise. Wayland throttles
unfocused or background windows to ~1 fps, making startup benchmarks, frame rates, and
automated screenshots inaccurate. `Xvfb` runs without window throttling while still
utilizing the real GPU.

Launch example:

```sh
Xvfb :99 -screen 0 1600x900x24 -nolisten tcp &
DISPLAY=:99 CIV_TRACE_STARTUP=1 godot-mono --path src/Civ.Godot -- --stage setup \
    --screenshot /tmp/shots/s.png --frames 3
pkill -f "Xvfb :99"
```

Notes:

- The `--frames 3` flag skips the standard 60-frame delay before taking a screenshot,
  letting capture runs complete in a few seconds. Omit `--frames 3` only when you need
  to measure sustained frame timing over time.
- You can safely ignore the Mesa notice
  `No DRI3 support detected - required for presentation` under `Xvfb`. Presentation
  falls back to an internal copy path, which is completely fine for taking screenshots
  and measuring startup speed.

### Test Metrics and Runtime Tracking

- `.test-metrics/test-runtimes.jsonl`: Stores rolling execution times for test runs.
- `tools/test-metrics/check.py`: Inspects `.test-metrics/test-runtimes.jsonl` to detect
  runtime regressions and flag slow tests (e.g., single runs exceeding 30 seconds or
  times increasing by 50% or more). This script runs automatically via
  `.githooks/pre-commit` to produce informational warnings.

## Sub-Agents

Always prefer background mode when launching sub-agents.

## Product Rules

Never write "AND", "A New Dawn", "Rise of Mankind", or "RoM" in shipped code,
identifiers, text, or documentation.

## Git Commits

Follow the commit standards described in the `verbose-commits` skill for all git
commits.

## Performance and Allocations

Focus on reducing memory allocations where players actively wait (such as turn
processing, map generation, and catalog loading), rather than optimizing purely to
reduce GC pauses. In .NET, small temporary objects that die quickly are cheap for the
garbage collector; the CPU overhead of allocating and constructing them in the first
place is the real bottleneck.

Key rules:

- **Keep 60 FPS frame loops allocation-free**: In `GameView._Process`, `_map.Refresh`
  and `UpdateHover` run every frame. Both must immediately early-out if the state
  revision or pointer position hasn't changed.
- **Avoid Large Object Heap (LOH) allocations**: Do not allocate arrays of 85 KB or
  larger during per-turn or per-frame code paths, as LOH collections cause noticeable GC
  pauses.
- **Reuse buffers instead of allocating each call**: Store reusable field buffers on
  classes (like `TerrainRenderer` does with `_copies` and `_marks`). Do not introduce
  `ArrayPool` or refactor classes into structs without benchmark evidence proving
  wall-clock speed improvements.
- **Measure allocations**: Use `GC.GetTotalAllocatedBytes(true)` (integrated in
  `StartupBenchmarks`) and run benchmarks with `mise run benchmark` or
  `mise run benchmark -- --terrain`. Record benchmark results in `docs/OPTIMIZE.md`.

## UI Skin Cache

`BrassSkin.Apply` renders 109 UI textures. Generating these takes ~2.9 seconds on cold
launch, so `SkinCache` saves them as PNG files in
`user://skin-cache/v<Version>/x<scale>/`. Subsequent launches load these pre-rendered
textures in ~0.13 seconds.

Important rules:

- **Version bumps**: If you change painting or rendering logic in `BrassSkin`,
  `BrassPainter`, `ScrapedPaint`, `BackdropStyleBox`, or `BrushedStyleBox`, you **must**
  bump `SkinCache.Version` in `src/Civ.Godot/Skin/SkinCache.cs`. The cache does not
  invalidate automatically; without a version bump, the game will load old cached
  textures.
- **Deterministic cache keys**: Texture cache keys are FNV-1a hashes based on the paint
  inputs rather than counters or run-specific hashes, ensuring files never conflict or
  load incorrectly.
- **Clearing the cache**: You can manually clear the cache by deleting the `skin-cache`
  folder in the Godot user data directory.
