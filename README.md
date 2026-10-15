# Civ

A super-moddable, hex-grid Civilization 4 clone. Everything, including base terrain, is a mod. See [docs/DESIGN.md](docs/DESIGN.md).

```
mise install                         # once per clone: .NET 10 SDK and Godot 4.7.2-stable .NET (godot-mono)
# Art under src/Mods/World/art/ uses Git LFS; run `git lfs pull` after cloning.
mise run test                        # default suite; excludes E2E and extractor tests
mise run test:e2e                    # E2E simulation tests
mise run test:extractor              # source extraction and emitted-content tests
dotnet test Civ.slnx                 # all tests, including E2E and extractor tests
dotnet build src/Civ.Godot
godot-mono --path src/Civ.Godot      # Godot 4.7.2 .NET editor/runtime
```

`godot-mono` is a mise shim for `github:godotengine/godot@4.7.2-stable` (`Godot_v4.7.2-stable_mono_linux_x86_64.zip`, renamed). Mise resolves it only from inside this repo, and mise sets `DOTNET_ROOT` for it. Without `DOTNET_ROOT`, Godot cannot find the .NET host and does not exit.

Smoke test (after `dotnet build src/Civ.Godot`; exits 0 after 200 frames): `xvfb-run -a godot-mono --headless --path src/Civ.Godot --quit-after 200`

Headless on Linux: `xvfb-run` plus software GL (`LIBGL_ALWAYS_SOFTWARE=1 godot-mono --rendering-driver opengl3 --path src/Civ.Godot`).

Mods are DLLs (plus `data/*.json`) in a `mods/` folder next to the client or in the user data folder. Mod DLLs reference only `Civ.Core`, never Godot types; visuals ship as data (`.pck` packs, plain assets). AI players implement `IPlayerAI`.

## Documentation

- **[docs/DESIGN.md](docs/DESIGN.md)**: Architectural design, core principles ("everything is a mod"), and subsystem contracts.
- **[docs/ROADMAP.md](docs/ROADMAP.md)**: Single source of truth for project roadmap: what is done (implemented systems and mechanics) and what needs to be done.
- **[PLAN.md](PLAN.md)**: Source extraction porting plan, product rules, and hex adaptation specifications.

## Operational benchmarks

Run `mise run benchmark` to measure world generation/setup, a fifty-city turn, and eight-player AI decisions at turn 100.
Run `dotnet run --project tools/Civ.Benchmarks/Civ.Benchmarks.csproj --configuration Release -- --terrain` to benchmark CPU terrain meshing and scatter planning across standard, huge, and gigantic maps.
The console suite prints single-sample timings and result counts without pass/fail thresholds.
It reports AI preparation separately from decision time. Content loading is outside the measurements.
The first sample includes JIT costs. Compare results on the same machine and build configuration.

The benchmark project is outside `Civ.slnx` and has no test-framework dependency.
Regular `dotnet test` runs retain world correctness and AI simulation assertions, without timing budgets.

## E2E tests

Long-running whole-game simulations use the `E2E` category. Run them with `mise run test:e2e`.
They are excluded from the default suite.

## Extractor tests

Tests that validate source extraction and generated content use the `Extractor` category.
Run them with `mise run test:extractor`; they are excluded from the default suite.
Run extractor tests when the source assets or importer change.

## Content

Rules content is compiled into the mods' `data/` folders by `tools/Civ.ContentImport` from a local extraction. See [PLAN.md](PLAN.md).

## 3D terrain and nature art

The game features continuous 3D blended terrain with height blending, seamless textures, and scattered 3D nature props:

- **Terrain textures:** Prepared using `tools/art/terrain_textures.py --selection <selection.json>`. The script creates seamless 1024×1024 RGBA textures with Lanczos resizing, smoothstep cross-feathers, pinned edge matching, and periodic Gaussian-subtracted height in the 8-bit alpha channel. Texture prompts, candidate selections, and post-processing details are documented in [tools/art/terrain_report.md](tools/art/terrain_report.md).
- **Scatter models:** Converted from the nature pack using `blender --background --python tools/art/convert_nature.py -- --source <dir>` based on the set mappings in [tools/art/nature_sets.json](tools/art/nature_sets.json). The converter simplifies geometries to strict triangle budgets (1,500 for trees, 300 for small props), embeds optimized albedo textures, sets alpha-cutoff masks, and writes snow-tinted variants as documented in [tools/art/nature_report.md](tools/art/nature_report.md).
- **Attribution:** 3D nature scatter models (trees, palms, rocks, bushes) are based on the *Ultimate Stylized Nature* pack by Quaternius, dedicated to the public domain under the CC0 1.0 Universal Public Domain Dedication.
- **Screenshots:** A visual showcase of the 3D blended terrain, mountain ridges, and feature scatter is captured in [docs/screenshots/terrain.png](docs/screenshots/terrain.png).
