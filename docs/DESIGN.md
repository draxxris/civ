# Design

A deterministic Civilization-4-style game on a hex grid. Core defines the contract. Mods implement the rules.

## Projects

- `Civ.Core`: geometry, shared data, content registries, extension contracts, simulation, commands, saves and hashes.
- `Civ.Net`: lockstep sessions and reliable ordered transport. `InProcessHub` connects local peers.
- `Civ.BaseGame`: small test ruleset loaded through the same mod API as other rulesets.
- `Civ.Client.Core`: engine-free selection, picking, commands and city screen data.
- `Civ.Godot`: Godot view. Build separately with `dotnet build src/Civ.Godot`.
- `Civ.Tests`: contract, rule, client, save/replay and lockstep tests.

The current client still references BaseGame rules. Future feature mods need Core query contracts before that dependency can disappear.

## Determinism

Authoritative state uses integer math. Rule code uses only simulation state and `Simulation.Rng`, not clocks or engine APIs.
String identities use ordinal comparisons. Content follows ordinal id order. Ordered sequences retain their order in saves and hashes.

Mods increase their manifest version whenever rule semantics change. Content hashes do not hash DLL bytes.

## Map and plots

`MapSpec(int Width, int Height, bool Wrap)` describes a rectangular, pointy-top odd-row map.
`HexGrid` converts axial coordinates, offset coordinates and row-major `TileId` values.
Columns normalize across east–west wrap. Rows never wrap.

- `HexGrid.Neighbors` returns distinct neighbors in direction order, without self-neighbors.
- Directions are east, northeast, northwest, west, southwest and southeast.
- `HexGrid.Distance` measures the shortest distance across wrap images.
- `HexGrid.Range` and `Ring` clip to the map and enumerate distinct tiles in ascending tile-index order.
- `Hex.DistanceTo`, `Hex.Range` and `Hex.Ring` describe the unbounded plane, not map topology.

`GameState.Plots[tile.Value]` stores terrain, feature, bonus, improvement, route, owner, culture, visibility and area id.
Culture uses sorted player ids. Revealed and visible teams use compact `TeamMask` bitsets, without a fixed team-count limit.
Bitsets enumerate team ids in ascending order. Dense team ids minimize their storage.

River bits follow the six directions. `GameState.SetRiver` mirrors each shared edge on both plots.
At a bounded edge, the single plot owns the edge. Map generators must preserve this convention.

`IMapGenerator.Generate(MapSpec, Plot[], DeterministicRng, ContentDb)` fills every plot.
Cities and units retain canonical axial positions. The client renders one map image, without duplicate wrap plots.

## Teams and entity data

Players have `TeamId`, civics, culture and treasury. Each player defaults to a separate team.
Teams hold members, known techs, a research target, progress per tech and war relations.
`Simulation.TeamOf(player)` and `KnowsTech(player, tech)` provide rule access.
Changing a research target retains its progress. `GameState.SetWar` keeps the war matrix symmetric.

Cities include a generic `ProductionQueue` of `ProductionOrder(Kind, ContentId, Progress)` entries.
BaseGame still uses its existing building/training fields. The future cities mod will own queue processing and overflow rules.

`GameState` contains plain data and rebuildable lookup caches. Caches do not enter saves or hashes.
`UnitById`, `UnitsAt`, `CityById` and `CityAt` use indexes.
Runtime edits use `AddCity`, `RemoveCity`, `AddUnit`, `RemoveUnit` and `MoveUnit`.
After bulk data edits, `RebuildIndexes` restores the caches.

## Mod registration

Each mod folder contains `mod.json`, one DLL and optional JSON sidecars.
Dependencies determine load order. Ordinal mod ids break ties.
Ids use `modid:name`. Registration requires the owning namespace. Overrides can replace earlier content.

- `ctx.Content<T>()` returns `ModRegistry<T>` for any `ContentDef` subtype.
- `ContentDb.Get<T>()`, `Get<T>(id)` and `TryGet<T>(id, out item)` resolve generic content.
- Existing typed registries remain convenience accessors.
- `ctx.RegisterService<T>(name, service)` registers a non-content implementation under its shared interface.
- `ContentDb.Services.Get<T>()` returns implementations in mod-load and registration order.
- `ITurnSystem` retains explicit `(Order, Id)` execution order.

Registries freeze after load. Definitions and their nested collections must not change after that point.
The content hash includes canonical definitions, loaded mod ids/versions, command identities, service order and state-component identities.

## Requirements

`Requirement` supports `All`, `Any`, `Not` and `Leaf(Kind, Id, Count?, Param?)`.
A bare string denotes a `tech` leaf. Multiple composite clauses in one object form an `All` expression.

```json
{
  "all": ["research:bronze_working"],
  "any": [
    { "kind": "bonus", "id": "world:copper", "count": 1 },
    { "kind": "bonus", "id": "world:iron", "count": 1 }
  ],
  "not": { "kind": "civic", "id": "government:pacifism" }
}
```

Each owning mod registers one `IRequirementEvaluator` per leaf kind.
Loading rejects unknown kinds and duplicate evaluators, including requirements nested in custom definitions.
`RequirementContext` supplies the simulation, player and optional city, plot or unit.
Evaluation returns success and failure explanations. Successful alternatives discard failed branches.
Empty `All` succeeds. Empty `Any` fails. Evaluators must not change state.

## Modifiers and ledgers

`IModifierSource.Contribute(ModifierContext, ModifierSink)` contributes to a plot, city, player, team or unit calculation.
The calculating mod calls `ModifierSink.Collect`, then applies its own formula.

`YieldVector` holds food, production and commerce. `CommerceVector` holds gold, research, culture and espionage.
Sources contribute flat vectors, percent vectors and namespaced integer effects such as `military:combat_percent`.
Percentages add before integer multiplication and division. Division truncates toward zero.

The sink produces a `Ledger` with ordered `(Source, Key, Amount)` lines and totals by key.
Typed keys separate flat and percent units. Each calculating mod owns effect-key meanings, final formulas and rounding boundaries.
BaseGame civilization and civic bonuses are modifier sources, not special cases in the turn systems.

## Events and extension state

`ctx.Subscribe<TEvent>(name, handler)` registers deterministic event handlers.
`Simulation.Events.Publish(record)` dispatches handlers synchronously in registration order.
Nested publications enter a FIFO queue after the current dispatch.

Core event records cover tech discovery, founding, growth, building completion, unit creation, combat and turn boundaries.
Mods can define additional `DomainEvent` records.
`Published` holds current-turn presentation events. `PreviousTurn` retains the last completed turn.
These lists are neither saved nor hashed. Loading does not republish events or rerun AI.

`ctx.RegisterStateComponent<T>(name)` registers a concrete state type under a stable id.
Game, player, team, city and unit objects each expose `Extensions.Set<T>(content, instance)` and `Get<T>(content)`.
Attached instances remain mutable. Saves and hashes include their canonical JSON data.

State components use exact registered types, integer numbers and JSON-serializable properties.
Fields require `JsonInclude`. Runtime state belongs in components, not shared service instances.
Floating-point schemas fail during registration. Sorted string sets and dictionaries retain ordinal comparers after loading.
Unordered arrays, custom converters, opaque objects and computed properties need care: the mod owns their deterministic serialization contract.

## Saves and multiplayer

`SaveGame.Save(Simulation)` returns versioned JSON with content identity, settings, full state and four RNG words.
`SaveGame.Load(ContentDb, json, force: false)` checks the schema and content identity, restores state, and rebuilds indexes.
Forced loading bypasses content mismatch only. It cannot restore unknown component types or unsupported schemas.
Schema changes need explicit migrations. Component authors must version and migrate their own data contracts.

`StateHasher` hashes canonical state JSON and RNG words with FNV-1a.
Canonical JSON sorts object properties ordinally, preserves array order and rejects non-integer numbers.
Content identity uses SHA-256 over canonical content data.

Commands travel as registered JSON type/data envelopes. Every player ends a simultaneous turn before the simulation advances.
AI runs on every peer and submits ordinary validated commands locally.

Lockstep peers exchange content hashes before any tick runs. A mismatch raises an explicit content error.
Peers then submit command batches and exchange state hashes after each tick. A state mismatch raises `Desync`.
The handshake identifies content, not initial setup. State hashes detect setup divergence.

## Remaining feature work

The richer building, unit and tech schemas follow separately.
Feature mods must define effect keys, query interfaces, turn priorities, queue rules, diplomacy enforcement and save migrations.
BaseGame remains a test scaffold: combat does not enforce war relations, and its economy sends all commerce to research.
Fog rules, advanced AI, border rules, city capture, art conversion and network transports remain separate tasks.
