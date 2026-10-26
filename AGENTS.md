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

# Skin cache

`BrassSkin.Apply` paints 109 textures in about 2.9 s. `SkinCache` writes them as PNGs, so every launch after the
first loads them in about 0.13 s instead. The cache lives in `user://skin-cache/v<Version>/x<scale>/`, which is
`~/.local/share/godot/app_userdata/Civ/skin-cache/` on Linux; deleting that directory clears it.

- Any change to the painting in `BrassSkin`, `BrassPainter`, `ScrapedPaint`, `BackdropStyleBox` or
  `BrushedStyleBox` must bump `SkinCache.Version` in `src/Civ.Godot/Skin/SkinCache.cs`. Nothing else invalidates the
  cache, so without a bump the game silently keeps showing yesterday's skin.
- Cache keys are FNV-1a hashes of the paint inputs, never of a counter, so inserting a plate or an icon cannot hand
  out another one's file.

# Running the game

Run and test the application under Xvfb, not on the developer's Wayland desktop, unless the developer asks
otherwise. Wayland throttles a game window that is not in front to about 1 fps, so launch times, frames and
screenshots taken from it are frame-quantised and meaningless. Xvfb does not throttle and still reaches the real
GPU.

```sh
Xvfb :99 -screen 0 1600x900x24 -nolisten tcp &
DISPLAY=:99 CIV_TRACE_STARTUP=1 godot-mono --path src/Civ.Godot -- --stage setup \
    --screenshot /tmp/shots/s.png --frames 3
pkill -f "Xvfb :99"
```

`--frames 3` cuts the 60-frame wait before a screenshot, so a capture run takes seconds; drop it only when the frame
count itself is what is being measured. Mesa prints `No DRI3 support detected - required for presentation` there:
presentation goes through a copy path, which is fine for startup and screenshot work, not for judging frame timing.
Looking at the UI by hand is the one thing the desktop does better.
