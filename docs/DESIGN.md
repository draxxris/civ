# Design

A Civilization 4-style game on a hex grid where **everything is a mod**, including the base game.

## Principles

1. **Dogfood.** The base game (`Civ.BaseGame`) is an ordinary mod DLL. It has no private hooks: if the base game needs something, the Mod API grows to offer it to everyone. Base terrain, techs, buildings, culture levels, the map generator, commands and rules (research, production, culture) are all registered through `IModContext`.
2. **Headless deterministic core.** `Civ.Core` has no engine dependency, no floats in game state, no wall clock, no unordered iteration affecting state. The renderer is a view.
3. **Commands over the wire.** Multiplayer is lockstep: only player commands are exchanged, every peer runs the same simulation.

## Layout

| Project | Role |
|---|---|
| `Civ.Core` | Hex math, deterministic RNG, content definitions + registries, the mod API (`IMod`, `IModContext`), mod loader, `Simulation`, command codec, state hash. Mods reference only this. |
| `Civ.Net` | Lockstep session, transport abstraction, in-process transport. |
| `Civ.BaseGame` | The base game as a mod: `mod.json`, DLL, `data/*.json` sidecars. |
| `Civ.Client` | Prowl front end (outside `Civ.slnx`; needs a Prowl checkout). |
| `Civ.Tests` | xunit tests incl. loading the base mod from its folder and two-peer lockstep. |

## Mods

A mod folder is `mod.json` + a DLL + optional sidecar data:

```
mods/mymod/
  mod.json            { "id": "mymod", "assembly": "MyMod.dll", "dependencies": ["base"] }
  MyMod.dll           exactly one public IMod
  data/*.json         read by the DLL through ctx.Data
```

- Ids are namespaced `modid:name`; a mod can only *register* inside its own namespace, but can `Override` anything registered earlier (load order follows `dependencies`, ties broken by id).
- Each mod loads in its own `AssemblyLoadContext`; `Civ.Core` always resolves to the host's copy so types are shared.
- Registries are frozen after load.
- Extension points today: terrain, tech, building, culture level, map generator, turn system (ordered rule steps), command type. Next: units, improvements/resources, civics, promotions, events, AI, UI panels (client side API).

## Simulation and multiplayer

- `GameState` is plain data. `Simulation.TryApply(command)` validates then applies; invalid commands are rejected identically everywhere. Turns are simultaneous (Civ4 MP style): `core:end_turn` from all players runs the registered `ITurnSystem`s in `(Order, Id)` order.
- Commands are JSON envelopes `{type, data}` where `type` is a registered command id, so mod commands travel like built-in ones. Every peer must load the same mods, in the same order, with the same versions (to be enforced in a handshake).
- `LockstepSession`: each peer submits a batch (possibly empty) for tick `t + inputDelay`; a tick runs when all batches are present, applied in peer order. After each tick peers exchange `StateHasher` hashes; mismatch raises `Desync`.
- Transport is `ITransport` (reliable, ordered broadcast). `InProcessHub` exists; TCP/relay transport is next.

## Rendering: Prowl

Prowl is a Unity-like .NET 10 engine; `Prowl.Runtime` works standalone without the editor, so `Civ.Client` is a plain console-style executable that references the runtime project and subclasses `Game` (the same shape as Prowl's `Samples/`). The renderer reads `Simulation.State` and builds hex prisms (`Mesh.CreateCylinder(r, h, 6)`) colored from `TerrainDef.Color`. Client-side mod API (custom meshes/UI per content def) comes later via a separate `Civ.ClientApi` so headless servers never load rendering code.

## Not yet done

Units/movement/combat, improvements, borders driven by culture, AI, save/load, real network transport, handshake, mod folder discovery in the client, client input (picking, commands).
