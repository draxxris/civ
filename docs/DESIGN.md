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
| `Civ.Client.Core` | Engine-free front-end logic (`GameSession`: selection, clicks, city/unit actions) and mod discovery; unit-tested. |
| `Civ.Godot` | Godot 4 .NET front end (outside `Civ.slnx`; build with `dotnet build src/Civ.Godot`). |
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
- Extension points today: terrain, tech, building, culture level, civilization, unit, promotion, experience level, player AI, map generator, turn system (ordered rule steps), command type. Next: units, improvements/resources, civics, promotions, events, AI, UI panels (client side API).

## Civilizations and AI

- `CivilizationDef` (sidecar JSON or code): name, leader, color, starting techs and percentage research/production/culture bonuses that the base turn systems apply. Players pick one via `SimulationSettings.Civilizations` (default: cycle through the registry).
- `IPlayerAI` is a registry like any other. `SimulationSettings.PlayerAis[i]` makes player `i` AI-controlled. The simulation itself calls the AI at game start and after every turn advance, on every peer, and applies the commands it returns through the normal validate/apply path. AI therefore never goes over the network and lockstep stays intact; it must be deterministic (state and `sim.Rng` only, no floats or unordered iteration).
- The base game ships `base:basic` (found a city, research the cheapest tech, build the cheapest building), registered through the same API a mod would use.

## Units, experience and promotions

- `UnitDef` (sidecar JSON): cost, required tech, strength, moves, `canFoundCity`, and class tags (`melee`, `ranged`, `mounted`, ...). Cities train units (`start_training`), and `FoundCity` can consume a settler.
- Experience works like Civ4: `ExperienceLevelDef` thresholds (2, 5, 10, 17, 26 in the base game) each earn one promotion. Winning a fight gives XP (2 attacking, 1 defending), and buildings can give new units starting XP (`unitExperience`, e.g. Barracks +3).
- `PromotionDef` is the selectable specialization. It can be limited to unit classes, can require any of a list of earlier promotions (so Combat I -> II -> III, Shock/Cover/Flanking branches), and grants percent bonuses to strength, attack or defense, extra moves, or extra resting heal. Players choose with `promote_unit`; the AI picks the first legal one by id.
- Combat is in the base mod, not the core: rounds of 20 damage with odds proportional to strength (promotions, terrain defense bonus and health all feed in), drawn from the simulation RNG so every peer agrees.

## Simulation and multiplayer

- `GameState` is plain data. `Simulation.TryApply(command)` validates then applies; invalid commands are rejected identically everywhere. Turns are simultaneous (Civ4 MP style): `core:end_turn` from all players runs the registered `ITurnSystem`s in `(Order, Id)` order.
- Commands are JSON envelopes `{type, data}` where `type` is a registered command id, so mod commands travel like built-in ones. Every peer must load the same mods, in the same order, with the same versions (to be enforced in a handshake).
- `LockstepSession`: each peer submits a batch (possibly empty) for tick `t + inputDelay`; a tick runs when all batches are present, applied in peer order. After each tick peers exchange `StateHasher` hashes; mismatch raises `Desync`.
- Transport is `ITransport` (reliable, ordered broadcast). `InProcessHub` exists; TCP/relay transport is next.

## Rendering: Godot 4 (C#)

The client is a Godot 4.4 .NET project. Logic and AI mods stay plain .NET DLLs loaded by `ModLoader`; the base game and every mod reference only `Civ.Core`, so no Godot types appear in mod DLLs. Visual content from mods is data: `.pck` resource packs and plain assets, loaded at start from the `mods/` folders. A client-side UI plugin interface (a separate `Civ.ClientApi`) is a possible later addition; it is not built yet. `WorldView` builds hex prisms colored from `TerrainDef.Color`; `GameUi` is a Control-based panel driven by `GameSession`.

## City management and specialists

- Cities have population, a food box (`FoodToGrow`), and work tiles within radius 2 (the centre is free). Every citizen either works a tile or is a specialist. Growth adds a citizen on the best free tile; starvation removes one.
- `SpecialistDef` (sidecar JSON): food/production/commerce/culture per specialist (priest, scientist, merchant, engineer, artist, plus the unlimited plain citizen). Slots come from `BuildingDef.specialistSlots` and `CivicDef.specialistSlots` (-1 = unlimited): temple = priests, library = scientists, workshop = engineers, market = merchants, Caste System = unlimited scientists/merchants/artists.
- `CivicDef` (government, labor, ...): one civic per category per player, gated by tech, with percent research/production/culture bonuses and specialist slots. `set_civic` swaps one; cities that lose slots turn surplus specialists back into citizens at once.
- `set_city_work` replaces a city's whole assignment (tiles + specialist counts) and is validated server-side like every command (distinct tiles in range, not used by another city, slot limits, every citizen assigned).

## Godot client

`Civ.Godot` is a playable hot-seat-vs-AI front end: hex prisms colored from `TerrainDef.Color`, cities and units tinted by civilization, and a Control-based UI with Selection / Research / Civics tabs. Selecting a city shows food, yields and specialist +/- rows with slot counts, highlights its workable tiles (click one to put a citizen to work or unassign it), and lets you pick buildings or units to produce. Selecting a unit shows health, experience level and available promotions, and offers "Found city". Controls: WASD pan, wheel zoom, left click select (click again to cycle a stack), right click move/attack, Enter ends the turn. The client only reads state and submits commands, so the same code can route through `LockstepSession`.

## Not yet done

City capture, ranged/sea/air combat, unit stacking rules, fog of war, a real AI strategy (the basic AI only trains, promotes and attacks adjacent targets, and never changes civics or citizen assignments), happiness/health, anarchy on civic change, unit pathfinding in the client (right-click walks greedily), floating city/unit labels, unique buildings/units per civilization, improvements, borders driven by culture, AI, save/load, real network transport, handshake, mod folder discovery in the client, client input (picking, commands).
