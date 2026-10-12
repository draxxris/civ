# Civ

A super-moddable, hex-grid Civilization 4 clone. Everything, including base terrain, is a mod. See [docs/DESIGN.md](docs/DESIGN.md).

```
dotnet test Civ.slnx                 # headless core, base mod, lockstep tests (.NET 10 SDK)
git clone https://github.com/ProwlEngine/Prowl external/Prowl
dotnet run --project src/Civ.Client  # Prowl front end
```

Licensed under the terms in `LICENSE`.
