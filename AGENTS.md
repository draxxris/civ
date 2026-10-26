Run `mise run test` by default. It excludes E2E and extractor tests. Use
`mise run test:e2e` to run E2E tests and `mise run test:extractor` to run extractor
tests.

`mise run test` also excludes world generation tests (`Category=WorldGen`). Run
`mise run test:worldgen` when the work set touches world generation: `src/Mods/World/`,
map generator registrations, `MapSpec`/`HexGrid` layout, terrain or relief generation,
river and cliff generation. `mise run test:nobuild` reruns the default tests on the
current build.

Always prefer background mode for sub-agents.

Product rule: never write "AND", "A New Dawn", "Rise of Mankind" or "RoM" in shipped
code, ids, text or docs.

# Allocations

Cut allocation on the paths a player waits for, not to keep GC pauses short. .NET's
generational collector makes a gen0 collection cost scale with survivors rather than
garbage, so a small pause disappears next to a two-second turn or a 1.8 s map rebuild.
The allocation work is the cost: catalog freeze fell from 365 ms to 266 ms, and standard
world generation from about 65 ms to 50 ms, once the intermediate trees and strings went
away. See docs/OPTIMIZE.md for the measurements.

- Keep the 60 Hz frame path allocation-free. `GameView._Process` runs `_map.Refresh` and
  `UpdateHover` every frame, and both must early-out on an unchanged revision or pointer.
- Avoid arrays at or above 85 KB in per-turn or per-frame paths. They land on the large
  object heap, which is the one place a .NET pause is worth designing against.
- Reuse a field buffer instead of allocating per call, the way `TerrainRenderer` holds
  `_copies` and `_marks`. Do not reach for `ArrayPool` or convert hot data to structs
  without a benchmark that shows wall-clock gain; buffer pooling was deferred pending
  that evidence.
- Measure with `GC.GetTotalAllocatedBytes(true)`, already in `StartupBenchmarks`, and
  `mise run benchmark` or `mise run benchmark -- --terrain`. Record the result in
  docs/OPTIMIZE.md.
