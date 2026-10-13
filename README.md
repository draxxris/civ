# Civ

A super-moddable, hex-grid Civilization 4 clone. Everything, including base terrain, is a mod. See [docs/DESIGN.md](docs/DESIGN.md).

```
mise install                         # once per clone: .NET 10 SDK and Godot 4.7.2-stable .NET (godot-mono)
dotnet test Civ.slnx                 # headless core, base mod, lockstep tests (.NET 10 SDK)
dotnet build src/Civ.Godot           # Godot client (not in Civ.slnx)
godot-mono --path src/Civ.Godot      # Godot 4.7.2 .NET editor/runtime
```

`godot-mono` is a mise shim for `github:godotengine/godot@4.7.2-stable` (`Godot_v4.7.2-stable_mono_linux_x86_64.zip`, renamed). Mise resolves it only from inside this repo, and mise sets `DOTNET_ROOT` for it. Without `DOTNET_ROOT`, Godot cannot find the .NET host and does not exit.

Smoke test (after `dotnet build src/Civ.Godot`; exits 0 after 200 frames): `xvfb-run -a godot-mono --headless --path src/Civ.Godot --quit-after 200`

Headless on Linux: `xvfb-run` plus software GL (`LIBGL_ALWAYS_SOFTWARE=1 godot-mono --rendering-driver opengl3 --path src/Civ.Godot`).

Mods are DLLs (plus `data/*.json`) in a `mods/` folder next to the client or in the user data folder. Mod DLLs reference only `Civ.Core`, never Godot types; visuals ship as data (`.pck` packs, plain assets). AI players implement `IPlayerAI`.

Private project; no license.

## Content

Rules content is compiled into the mods' `data/` folders by `tools/Civ.ContentImport` from a local extraction. See [PLAN.md](PLAN.md).
