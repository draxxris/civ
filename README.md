# Civ

A super-moddable, hex-grid Civilization 4 clone. Everything, including base terrain, is a mod. See [docs/DESIGN.md](docs/DESIGN.md).

```
dotnet test Civ.slnx                 # headless core, base mod, lockstep tests (.NET 8 SDK)
git clone https://github.com/ProwlEngine/Prowl external/Prowl
dotnet run --project src/Civ.Client  # Prowl front end
```

The client has no display requirement in CI: under Linux you can run it headless with `xvfb-run` and Mesa software GL (`LIBGL_ALWAYS_SOFTWARE=1 MESA_GL_VERSION_OVERRIDE=4.5`).

Licensed under the terms in `LICENSE`.
