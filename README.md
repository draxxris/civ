# Civ

A super-moddable, hex-grid Civilization 4 clone. Everything, including base terrain, is a mod. See [docs/DESIGN.md](docs/DESIGN.md).

```
dotnet test Civ.slnx                 # headless core, mods, lockstep tests (.NET 8; .slnx needs SDK 9.0.200+, otherwise: dotnet test tests/Civ.Tests)
dotnet build src/Civ.Godot           # Godot client (not in Civ.slnx)
godot-mono --path src/Civ.Godot      # Godot 4.4 .NET editor/runtime
```

Headless on Linux: `xvfb-run` plus software GL (`LIBGL_ALWAYS_SOFTWARE=1 godot-mono --rendering-driver opengl3 --path src/Civ.Godot`).

Religion (Faith), guilds (Renown) and corporations (Capital) ship as separate mods built only on the public resource and institution API; the Godot build bundles them. Mods are DLLs (plus `data/*.json`) in a `mods/` folder next to the client or in the user data folder. Mod DLLs reference only `Civ.Core`, never Godot types; visuals ship as data (`.pck` packs, plain assets). AI players implement `IPlayerAI`.

Licensed under the terms in `LICENSE`.
