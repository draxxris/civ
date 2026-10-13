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
BaseGame still uses its existing building/training fields. Cities owns feature queues, paused item progress, completion snapshots, and normalized overflow.

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

## Turn phases

`Civ.Game.TurnPhase` defines shared order slots. Systems use these constants instead of private numeric priorities.
Systems with equal orders run by ordinal `Id`. Additional orders can use the gaps between slots.

Orders from `PlayerBegin` through `Map - 1` require `IPlayerTurnSystem.Run(Simulation, int player)`.
The scheduler completes this interval for one player before the next player, in ascending player-id order.
Other orders use `ITurnSystem.Run(Simulation)` once per boundary. Legacy BaseGame orders remain outside the player interval.

| Constant | Order | Work |
| --- | ---: | --- |
| `GameBegin` | 0 | Global begin hooks |
| `Properties` | 100 | Global properties |
| `Deals` | 200 | Deal settlement |
| `Teams` | 300 | Team maintenance |
| `PlayerBegin` | 1000 | Player timers and caches |
| `AssignWork` | 1100 | City work assignments |
| `Gold` | 2000 | Treasury settlement |
| `Research` | 2100 | Research and discoveries |
| `Espionage` | 2200 | Espionage allocation |
| `Economy` | 2300 | Taxes, consumption, inflation |
| `CityBegin` | 3000 | City preparation |
| `CityGrowth` | 4000 | Food, growth, starvation |
| `CityCulture` | 4100 | City culture |
| `CityPlotCulture` | 4200 | Plot culture and borders |
| `CityAutoBuildings` | 4300 | Automatic buildings |
| `CityProduction` | 5000 | Production eligibility and work |
| `CityCompletion` | 5100 | Completion effects |
| `CityPostProduction` | 6000 | Decay and later city rules |
| `PlayerEnd` | 7000 | Remaining player maintenance |
| `Units` | 8000 | Units and worker progress |
| `Map` | 9000 | Improvements and map updates |
| `Spawns` | 9100 | Barbarians and other spawns |
| `Environment` | 9200 | Environment and global founding |
| `Visibility` | 9500 | Team sight |
| `GameEnd` | 9900 | Pre-end hooks |

These slots follow `CvPlayer::doTurn` and the city pipeline: gold precedes research, and growth and culture precede production.
Cities processes all owned cities at each city slot, in ascending city-id order. Completion commits effects in that same order.
This preserves the source pipeline's cross-city stage boundaries without parallel mutations.
New buildings cannot change already-settled gold or research in that turn.

The simultaneous engine resolves submitted commands before these slots.
It resolves global map work after player work, rather than before player activation as in `CvGame::doTurn`.
This boundary adaptation gives worker completions one defined map-update point.
It does not reproduce sequential activation or shuffled source player order.
Most slots reserve future work and do not yet contain systems.

`TurnEnded` follows `GameEnd`, before the turn counter increases.
`TurnStarted` follows the counter increase and precedes AI commands. Both events retain their existing presentation semantics.

## Feature ruleset setup

A non-null `SimulationSettings.WorldSize` selects Foundation's `INewGameSetup` service.
Null retains the legacy setup. Feature settings include `WorldSize`, `GameSpeed`, `Handicap`, `StartingEra`, and one entry in `Leaders` per player.
`MapGenerator = "world:continents"` selects World generation.
An optional `Civilizations` entry resolves a leader shared by multiple civilizations. An incompatible selection fails before generation.

Foundation resolves dimensions from `WorldSizeDef.GridWidth/GridHeight` times the source landscape-cell scale of four.
Standard therefore contains 84 × 52 = 4368 plots, with east–west wrap.
`State.Map` holds the effective map. `Settings.Map` remains the caller's original legacy map setting.

Foundation creates teams and grants civilization and handicap starting technologies.
Civilization free-unit classes resolve through `UnitOverrides`, including explicit disabled classes.
Era and handicap fields supply multipliers, extra worker/defense/exploration grants, and starting gold.
Role selection uses eligible land units, known requirements, work rate, strength, movement, and exploration suitability.
It approximates `CvPlayer::addFreeUnitAI`, not the full military AI valuation.
All initial units share their player's start plot. Movement rules remain a Military task.

World scores radius-two and radius-three food and production, with coast and fresh-water weights.
It selects habitable starts with minimum wrapped spacing, then improves weak starts with grass and forest near the center.
Normalization never changes water, peaks, or resource plots. Impossible spacing produces a setup error, not overlapping starts.
The harness `tests/Civ.Mods.Tests/GameFactory.cs` creates full headless games through this API.

## World services

Clients and other mods resolve these Core contracts through `ContentDb.Services.Get<T>().Single()`:

- `IPlotYieldService.Calculate(Simulation, int player, TileId)` returns `PlotYieldResult(YieldVector, Ledger)`.
- `IPlotYieldService.CalculateNature(Simulation, int player, TileId, bool ignoreFeature = false)` returns natural potential.
- `IWaterService.IsFreshWater(Simulation, TileId)` and `IsIrrigated(Simulation, int player, TileId)` return availability.
- `IResourceNetwork.ConnectedCount(Simulation, int player, int city, string bonus)` returns physical connected copies.
- `IWorkerBuildService.Validate(Simulation, int player, int unit, string build)` returns a rejection reason or null.
- `IWorkerBuildService.Start(Simulation, int player, int unit, string build)` applies a validated assignment.
- `IWorkerBuildService.RequiredWork(Simulation, TileId, string build)` returns the integer work threshold.
- `IVisibilityService.Recompute(Simulation)` updates visible and revealed team masks.
- `IStartingPositionService.Select(Simulation, int count)` selects locations without mutation.
- `IStartingPositionService.Normalize(Simulation, IReadOnlyList<TileId>)` applies setup-only terrain changes.

Read-only queries never consume RNG or change authoritative state. Services hold no per-game caches.
Plot yields include terrain, relief, lakes, features, revealed resources, improvements, routes, irrigation, technology changes, and registered plot modifier sources.
Feature river changes replace terrain river changes. Route synergy suppresses smaller river contributions per yield, as in `CvPlot::calculateNatureYield`.
Nature clamps before improvement effects. Flat sources sum before percentage multiplication, integer division, and the final nonnegative clamp.
Ledger keys `yield.flat.*` and `yield.percent.*` explain inputs. `world:food`, `world:production`, and `world:commerce` hold final outputs.
Cities owns city-center minima and city-specific plot effects. Government and Peoples can contribute through `IModifierSource` with `ModifierScope.Plot`.

The generator adapts `Continents.py` and `FractalWorld`: polar fractal land with 75% water, separate relief, and latitude-based climate.
Integer value noise replaces the native fractal primitive. Priority-flood drainage connects high ground to water across a temporary filled surface.
River masks describe shared hex edges, not square-map river directions. `SetRiver` maintains both sides.
Flooded depressions can produce level drainage steps. The drainage parent order prevents cycles.
Connected water areas with at most the configured lake size supply fresh water.
Features and resources obey typed placement restrictions. Resource density uses appearance, eligible tiles, player count, class spacing, and clustering.
Starting normalization can replace climate-selected terrain near starts, but never invalidates a resource's terrain or feature.
Goody huts use improvement spacing and tiles-per rules. Starting eras can suppress all huts.

Networks use routes, city nodes, shared river edges, and technology-enabled coast/ocean plots.
Unowned plots require exploration. Foreign territory remains inaccessible until diplomacy rules supply permissions.
Queries recompute components so ownership, pillage and technology changes take effect immediately.
The `bonus` evaluator uses connected counts. Vicinity requirements remain unavailable until Cities defines their scope.

`world:start_build` serializes the Core `StartBuildCommand`.
Workers share plot progress by build id. An assignment stores its original tile, so movement cancels the assignment at the next worker phase.
Build times apply terrain, speed and starting-era multipliers in sequence. Known technologies and AI handicap fields modify work rates.
Completion can remove a feature, create an improvement or route, change terrain, or consume a worker.
A feature effect can permit removal without its production technology. Such removal grants no production.
`FeatureProduction` names the nearest owned city and the production grant. Cities must subscribe to apply overflow.
Improvement upgrades require worked turns or an outside-borders target, plus the target's technology.

Visibility runs at `TurnPhase.Visibility`, after founding, and after unit creation events.
Military must call `Recompute` after moves. Sight uses hex distance, team sharing, and promotion visibility changes.
It does not yet model terrain occlusion or remembered hidden terrain.

`Plot.Kind`, `IsLake`, `ImprovementTurns`, `BuildProgress`, player leader/start ids, and worker components enter saves and state hashes.
These fields are additive to save schema 1 and have defaults for older JSON.
Changed mod versions reject old content identities unless the caller explicitly forces loading.
Hand-authored geometry tuning lives in `src/Mods/Foundation/data/hex-overrides.json` and enters the content hash.
The importer writes only its declared definition families and rejects that reserved filename.

See [mechanics-status.md](mechanics-status.md) for consumed fields and remaining mechanics.

## City services

Cities registers Core query contracts. All queries are RNG-free and do not change authoritative state.
`Calculate(Simulation, int city)` returns ledgers through `ICityYieldService`, `ICityWellbeingService`, `ICityGrowthService`,
`ICityProductionService`, `ICityMaintenanceService`, and `ICityGreatPeopleService`.
Production calculation additionally takes `(string kind, string content)`.
`ICityProductionService.Validate` returns structured requirement failures with the same arguments.
`ICityWorkService` supplies `Radius`, `WorkArea`, assignment validation, and authoritative `Assign`.

`CityYieldResult.Yields.Commerce` is raw whole commerce. `DirectCommerceHundredths` bypasses sliders.
`CommercePercent` contains additive percentage bonuses, not final rates.
Economy allocates raw commerce, adds direct contributions, and applies each commerce modifier.
Economy can register one `ICityCultureSource.CultureHundredths(Simulation, int city)` provider for the final culture rate.
Without that provider, Cities applies culture modifiers to direct culture only.

Commands use `cities:found_city`, `cities:set_city_work`, and `cities:set_city_production` identities.
The Core types are `FoundCityWithSettlerCommand`, `SetCityAssignmentsCommand`, and `SetCityProductionCommand`.
Queue kinds are `cities:building` and `cities:unit`. Cities completes at most one item per city per turn.
Growth and culture precede production. Completion effects follow production work in city-id order.

`CityRuleState` and `CityPlayerState` contain saved feature-rule state.
Modifier sources use City scope for local effects and Player scope for player growth/maintenance percentages.
City keys include `cities:happiness`, `cities:health`, `cities:war_weariness`, `cities:food_kept_percent`, and `cities:growth_percent`.
Specialist keys use `cities:slots:<specialist-id>`, `cities:free:<specialist-id>`, and `cities:free_specialists`.
Other keys cover maintenance percentages, production percentages, experience, and great-person percentages.
See the Cities implementation and [mechanics-status.md](mechanics-status.md) for supported effects and deferred rules.

Food waste uses fixed-point recurrence with scale one billion. City-count maintenance uses a fixed-point root with scale one trillion.
These deterministic adaptations can differ from native floating-point results near integer boundaries.
Maintenance uses the maximum wrapped hex distance as the map normalization.
Plot culture uses hex distance without terrain obstacles. Ownership keeps an incumbent on ties, then selects the lowest player id.
Governor weights live in the hand-authored hex sidecar. The governor orders equal plot scores by tile id.
No feature mod references the BaseGame assembly. The client still uses the legacy ruleset.

## Research and economy services

Research registers `IResearchService` for cost ledgers, rate modifiers, eligibility, queues, and team contributions.
`research:set_research` uses `SetTeamResearchCommand`. It accepts a target and optional queue, or one free-tech selection.
`ResearchTeamState` saves normalized overflow, repeat counts, free-tech credits, queues, and explicit contacts.
Discoveries publish `TechDiscovered`. Cities updates the current era through this event.
Overflow applies on the next contribution. Optional diffusion and timeline policies remain deferred.

Economy registers `IPlayerEconomyService` for player ledgers, city commerce ledgers, rates, and rate validation.
`economy:set_commerce_rates` uses `SetCommerceRatesCommand`.
City commerce keeps hundredths through allocation, direct contributions, percentage modifiers, and process conversion.
Gold settlement saves a commerce snapshot. `IPlayerResearchSource` supplies that snapshot to Research.
`ICityCultureSource` supplies final culture to Cities. Queries never mutate state or consume RNG.

`ICityProcessService` supplies eligibility and conversion for infinite `economy:process` queue items.
Processes use current production. Stored overflow remains available for later finite production.
`ICityHurryService` supplies payment previews and mutations through `economy:hurry` and `HurryCityCommand`.
Cities completes hurried items during its normal completion phase.

Player modifier keys include `economy:max_rate:<type>`, `economy:upkeep_percent`, `economy:inflation_percent`, and `economy:hurry_percent`.
Team key `research:cost_percent` changes research costs after the normal scaling steps.
`EconomyPlayerState` saves sliders, settlement snapshots, unpaid gold, aggregate espionage, and the inflation accumulator.
`EconomyCityState` saves hurry anger. Bankruptcy adjusts sliders before research, but does not disband units.
The imported profile disables inflation. Enabled profiles use the source binomial formula and momentum update after research.

## Military services

Military registers `IMovementService`, `IPathfindingService`, `ICombatService`, `IUnitService`, and `IUnitSupportService` through Core.
Movement paths exclude the starting plot. Commands traverse until movement expires or combat interrupts the path.
A costly destination can consume the remaining sub-moves. A* minimizes total edge cost, not the number of turns.
Route movement and combat share the hex river-edge policy. Military recomputes World visibility after movement and unit removal.

Command identities are `military:move_unit`, `promote_unit`, `set_unit_activity`, `pillage`, `upgrade_unit`, `disband_unit`, and `raze_city`.
The promotion command type is `PromoteMilitaryUnitCommand`, distinct from the legacy command.
Cities supplies the additive `ICityCaptureService`. Other mods need no dependency on the Military assembly.

Combat preview returns strengths, ledgers, firepower, round damage, first strikes, withdrawal, and success odds in millionths.
The defender strength includes relative attack modifiers, including the reciprocal formula for negative totals.
Combat is atomic. Saves between commands retain health, moves, experience fractions, activity, RNG, and current-turn combat reports.
General points are a counter only. No general births occur yet.

`UnitCreated` initializes training and granted units. `TurnStarted` initializes setup units without rerunning initialization after saves.
The unit phase follows worker work, then applies healing and refreshes moves. Newly created units do not heal or fortify that boundary.
Barbarian behavior runs at `GameBegin`. Land spawns run at `Spawns`. The barbarian player automatically ends each simultaneous turn.
Training modifiers use `military:initial_experience` and `military:free_promotion:<id>`.
Promotion modifiers use Unit scope. Government and Peoples must avoid duplicate military contributions when they supply full modifier sources.

See [mechanics-status.md](mechanics-status.md) for partial rules and deferred combat options.

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

## V1 content mods

`src/Mods/` contains Foundation, World, Research, Economy, Cities, Military, Government, and Peoples.
These mods have no load-order dependencies yet. Foundation, World, Cities, Research, and Economy register setup or gameplay services. Each manifest declares its cross-mod content providers in `references`.
References can be cyclic. ModLoader checks provider presence before registration and typed reference closure after all mods register.
The post-registration pass runs before freeze and content hashing. Unknown sidecar properties fail load.

`Civ.Content.Schema` holds immutable definitions alongside the legacy BaseGame types.
The schema includes costs, grouped requirements, vectors, indexed effects, folded class limits, civilization overrides, and AI weights.
Definitions retain English text, eras, source icon references, and unsupported-field paths.
Foundation reserves deferred mechanic identities so later religion, corporation, and victory rules do not lose their prerequisites.
See [content-schema.md](content-schema.md) for definition types, units, scopes, and importer decisions.

## Remaining feature work

The v1 definition schema, Foundation setup, and the first World rules are available. Gameplay rules continue one feature mod at a time.
Feature mods must define effect keys, query interfaces, turn priorities, queue rules, diplomacy enforcement and save migrations.
BaseGame remains a test scaffold: combat does not enforce war relations, and its economy sends all commerce to research.
Fog rules, advanced AI, border rules, city capture, art conversion and network transports remain separate tasks.
