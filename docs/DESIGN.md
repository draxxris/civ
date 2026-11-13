# Design

A deterministic Civilization-4-style game on a hex grid. A small kernel runs the
simulation, a statically linked base game supplies the rules every game needs, and mods
extend both through one registration API.

## Principles

1. **The base game is linked in; features are mods.** `Ariel.Rules` holds the rules
   modules: Foundation, World, Research, Economy, Cities, Military, Government, Peoples,
   Religion and Relations. `Ariel.Ai` holds the computer player, the base module `ai`. Their
   content references each other, so none of them can be removed, and no mod can replace
   one. Everything a game can run without is a mod: Great People, Trade and the optional
   feature mods. A test builds and plays the base modules with no mod installed.
2. **One registration API.** A base module registers terrain, techs, buildings, units,
   civics, map generators, commands, turn systems and state through the same
   `IModContext` a mod uses, and keeps its own content namespace (`cities:`,
   `military:`). Ids, load order and content identity do not depend on which assembly
   holds the code. When a mod needs a seam the base game lacks, the base game grows it
   for every mod.
3. **No content knowledge in the kernel or the client.** `Ariel.Core`,
   `Ariel.Client.Core` and `Ariel.Godot` never hard-code a content id or kind. Defaults
   and choices come from loaded content or registered services.
4. **Headless deterministic core.** `Ariel.Core` has no engine dependency, no floats in
   game state, no wall clock and no unordered iteration affecting state. The renderer is
   a view.
5. **Commands over the wire.** Multiplayer is lockstep: only player commands are
   exchanged, and every peer runs the same simulation.

## Projects

- `Ariel.Core`: the kernel. Geometry, RNG, plain game state (players, teams, plots,
  cities, units), content and service registries, the mod loader, requirements and
  modifiers, the simulation, turn order, commands, saves and hashes. It defines no
  content schema and no rule contract. It asks the loaded game seven questions through
  one required service, `IRuleset` (map size, player setup, content validation, option
  defaults, scouting).
- `Ariel.Rules`: the rules modules, one folder each with its code, `mod.json` manifest,
  `data/` sidecars and `art/`. `BaseRules` lists them. The build stages manifests and
  data to `rules/<Module>/` beside every host and symlinks `art/` there. `Contracts/` holds the
  service interfaces, state components, commands and domain events of the base game
  and of the seams mods fill; `Schema/` holds the content definitions
  (`Ariel.Content.Schema`). `GameRules` implements `IRuleset` and is the typed root
  of the base services. It references no mod assembly and not `Ariel.Ai`.
- `Ariel.Ai`: the computer player (see Computer players below), one base module with id
  `ai` whose manifest is staged to `rules/Ai/`. It references `Ariel.Core` and
  `Ariel.Rules` only, never a mod or a mod's API. It owns the seams through which mods
  teach the AI their features: planners (`IAiPlanner`) and production wishes
  (`IProductionWishes`).
- `Ariel.Composition`: the composition root. `BaseGame` lists the base modules (the rules
  modules and `ai`) and hands mods the host's copies of `Ariel.Rules` and `Ariel.Ai`
  (`ModLoader.Share`); `GameContent` is the one place a host composes the base game
  with mods. Every host (the client, tools, tests) composes through it, so none has to
  remember a base module. No mod references it.
- `src/Mods/*`: mods, each its own DLL referencing `Ariel.Core` and `Ariel.Rules`, and
  `Ariel.Ai` when it contributes an AI planner, all with `Private="false"` because the
  host supplies them. The loader hands a mod the host's copies, so types are shared. A mod may also
  publish an API assembly in its `Api/` folder (see Mod APIs below); Corporations,
  Diplomacy, Espionage, Events, GreatPeople, ProjectsVictory, Revolution, ScriptedWonders and
  Trade do.
- `Ariel.Net`: lockstep sessions and reliable ordered transport. `InProcessHub` connects
  local peers.
- `tests/Ariel.Tests/TestRuleset`: a small test-only game with its own content
  definitions, city state and `IRuleset`. `Ariel.Tests` references the kernel only,
  which proves the kernel runs a complete game without the base game's domain.
- `Ariel.Client.Core`: engine-free selection, picking, commands and city screen data.
- `Ariel.Godot`: Godot view. Build separately with `dotnet build src/Ariel.Godot`.
- `Ariel.Tests`: contract, rule, client, save/replay and lockstep tests.

The client uses the query contracts in `Ariel.Rules` and the published APIs of the
feature mods it presents (`Ariel.Mods.<Mod>.Api` for Corporations, Diplomacy, Espionage,
Events, GreatPeople, ProjectsVictory, Revolution, ScriptedWonders and Trade). It
has no reference to a mod's implementation or to a test-ruleset assembly.

## Determinism

Authoritative state uses integer math. Rule code uses only simulation state and
`Simulation.Rng`, not clocks or engine APIs. String identities use ordinal comparisons.
Content follows ordinal id order. Ordered sequences retain their order in saves and
hashes.

Mods increase their manifest version whenever rule semantics change. Content hashes do
not hash DLL bytes.

## Map and plots

`MapSpec(int Width, int Height, bool Wrap)` describes a rectangular, pointy-top odd-row
map. `HexGrid` converts axial coordinates, offset coordinates and row-major `TileId`
values. Columns normalize across east–west wrap. Rows never wrap.

- `HexGrid.Neighbors` returns distinct neighbors in direction order, without
  self-neighbors.
- Directions are east, northeast, northwest, west, southwest and southeast.
- `HexGrid.Distance` measures the shortest distance across wrap images.
- `HexGrid.Range` and `Ring` clip to the map and enumerate distinct tiles in ascending
  tile-index order.
- `Hex.DistanceTo`, `Hex.Range` and `Hex.Ring` describe the unbounded plane, not map
  topology.

`GameState.Plots[tile.Value]` stores terrain, height, feature, bonus, improvement,
route, river and cliff edge bits, owner, culture, visibility and area id. Culture uses
sorted player ids. Revealed and visible teams use compact `TeamMask` bitsets, without a
fixed team-count limit. Bitsets enumerate team ids in ascending order. Dense team ids
minimize their storage.

`Plot.Height` stores the tile's tier: deep water `-2`, coast or lake `-1`, flat land
`0`, hills or a raised flat `1`, peak `2`. `RiverEdges` and `CliffEdges` store six bits
in direction order. `GameState.SetRiver` and `SetCliff` mirror shared edges on both
plots, and off-map edges never carry either bit. A cliff bit is only the presence mask;
the raised side is derived by `GameState.CliffLifts` from the two heights, so no
separate lift mask is stored in live state. `SetCliff` only raises the named side, to
one tier above the other and never above a peak. The map rules live in `CliffRules` and
are reported by `mise run validate`; the game neither repairs nor refuses a map at load.

`IMapGenerator.Generate(MapSpec, Plot[], DeterministicRng, ContentDb)` fills every plot.
`IMapGenerator.PreviewLand(MapSpec, seed, ContentDb)` is the land step of that
generation and must return the same land/water mask for the same seed. It reads a fresh
stream, so a setup screen can draw a world's coastline without advancing the game's RNG;
the new-game screen does exactly that, on a worker thread, before the player commits to
a map. Cities and units retain canonical axial positions. The client renders one map
image, without duplicate wrap plots.

### Cliffs

A cliff is a step in the ground on the edge between two plots, at least one of them
land. `World` owns the rules; movement and the client read the plots. See
[cliff-rendering.md](cliff-rendering.md) for the capture workflow and the visual
acceptance checks.

- State. `Plot.Height` is authoritative and is part of the save, the state hash, the
  clone and the fog memory. `Plot.CliffEdges` is the six-bit presence mask in direction
  order, mirrored on the neighbour. `GameState.CliffLifts(tile)` derives the raised side
  by comparing the two heights; it is never stored in live state.
  `GameState.SetCliff( high, direction, present)` writes both presence bits and raises
  `high` to `min(low.Height + 1, Levels.Peak)`. It never lowers a height, so clearing a
  cliff leaves the height to be justified by another edge. Off-map edges never carry a
  cliff, and the rules make water never the high side. `RevealedPlotSnapshot` carries
  `Height`, the cliff bits and a derived `CliffLiftEdges` captured by
  `VisibilityService`, so fog remembers which way a step drops without revealing the far
  tile.
- Generation. `ContinentsGenerator` calls `CliffGenerator.Generate` after rivers and
  before features. It draws its noise from a restored copy of the RNG state, so cliff
  generation leaves every other random stream unchanged. It fills 20% of land/sea edges
  through ranked smooth noise (scale 6, two octaves, range 0–999), with a 400-point
  bonus for Hills and Peak. River mouths and sea-edge river endpoints stay beaches. Each
  coastal area keeps one beach, and islands of at most four tiles keep half their sea
  edges open. It then ranks land/land edges (scale 9, two octaves) with a relief bonus
  and a river bonus, lifts the higher side (Peak above Hills above Flat, then noise,
  then tile id) and fills 8% of the candidates. A candidate terrace is accepted only if
  the tile's existing cliffs still step (`StepsHold`) and the new mass has a legal slope
  out (`SlopesNear`). After terraces, the generator reconnects severed areas, thins
  terraces that stand too close, and clears the walls named by the slope report in
  bounded passes.
- Edge rules. Both plots carry the bit and the edge has a neighbour. Land/land with no
  peak: exactly one tier of difference; a flat raised to `1` counts as a hill, and two
  hills, two raised flats, or a hill and a raised flat have no step. Land/land with a
  peak: one or two tiers, the peak is always the high side and never the low side.
  Land/water: any land height against the water, with an explicit bit, lakes included;
  no step rule. Water/water: never. A plot's height must match its ground, except that a
  flat may stand at `1` while a land cliff drops from it.
- Spacing. Separate terraces need room to read as separate features. The raised side of
  one must be at least three hex steps from the raised side of another (two intermediate
  tiles along the shortest path); two raised tiles with one low tile between them are
  distance two and are illegal. Touching raised tiles are one mass, so a ridge, plateau
  or long bluff is allowed however long its rim runs. Coastal land high sides are not
  compared this way. `CliffRules.CliffSpacing` is the implementation.
- Slope and enclosure. A raised mass is the connected group of cliff-lifted land tiles.
  Every mass must have at least one uncliffed edge to ramp out of. Every uncliffed
  land-to-land step of the mass is a slope, one edge wide (0.5 world units either side
  of the midpoint) and `Lattice.CliffRamp` (1.6) long; its sweep must reach the ground
  below before it meets another face or another mass's slope. A mass walled in on every
  edge, and two slopes that meet in the gap between two raised tiles, are illegal. The
  control applies to any land tile that is the high side of a cliff, including a hill or
  peak that already stands high. `CliffRules.SlopeProblems` is the report, `SlopesHold`
  and `SlopesNear` let the generator ask locally, and `SlopeWalls` names the wall to
  clear.
- Walkability and access. A cliff may not cut a land area in two. The generator
  reconnects a severed area by opening the first cut edge, so `Plot.AreaId` still means
  "reachable on foot"; pathfinding, cities, barbarians and naval blockades need no
  special case. A start or city tile keeps one open sea edge (the beach rule) and, when
  every edge is walled, one land edge; `EnsureAccess` runs when starts and cities are
  placed. `mise run validate` reports a hand-made map that breaks any of these rules.
- Movement and sight. Cliffs block land units from boarding, landing, attacking, or
  walking across the edge. Ships cannot enter or leave a land tile across a cliff,
  including friendly cities. The Amphibious promotion does not bypass cliffs.
  `CanMoveAllTerrain` units and aircraft ignore them. Ranged attacks, aircraft, and
  coastal building/yield eligibility do not change. Pathfinding uses the same rules, and
  naval AI selects ports and landing edges with beach access. `VisibilityService` reads
  `Height` for sight: water and flat add 0, hills and raised flats add +1, peaks add +2.
  `GlobalWarmingSystem` clears the cliffs on a tile it floods and settles the tile and
  its neighbours back to the height their ground implies.
- Look. `TerrainSnapshot` carries `Heights`, `Cliffs` and `CliffLifts`. `TerrainField`
  raises the lifted side by up to `CliffRise` (0.22) over its whole tile and eases bare
  neighbouring ground up to it over `CliffRamp` (1.6), but a ramp never crosses a cliff:
  the ground below a land/land cliff takes none of the rise, so the edge is a step
  rather than a shoulder. The blend is sharpened across the edge (`CliffSharpness` 45)
  and overlapping lifts combine with a maximum, so the ground never rises more than one
  rise above its normal height.
- Run ends. `BuildLiftScale` measures each run of lifted tiles from its caps. A
  multi-tile run tapers from `CliffEndScale` (0.64, about 0.14 world units at the
  default `CliffRise`) at an end tile to full height over up to `CliffEndTaper` (2.5)
  tiles along the run, shortened to the run's own half-length; a run of three or more
  reaches full height in its middle, while a two-tile run has no interior and both tiles
  sit at about 0.14. Beyond the end, the adjacent tile's ground eases from about 0.14 to
  0 (the current `CliffEndRamp` is 2.4 world units, a little over one tile), assuming
  flat land with no hill, peak or water. A single isolated lifted tile keeps full
  `CliffRise` (0.22); its bare adjacent ground ramps from 0.22 to 0 over `CliffRamp`
  (1.6). If the tile has no bare edge at all (every side is a cliff or the map edge),
  there is no ramp and the face drops the full 0.22.
- Face and rock wall. The field drops a cliff within one `PeakResolution` lattice step,
  and the mesh keeps its vertices on the lattice, so the face is a steep slope about
  0.064 world units wide; pulling vertices onto the cliff line would flip triangles that
  cross it. `DrawPoint` finds the cliff line, and the rock wall stands against it. At a
  corner where two cliff edges meet, the intended look is a slight smoothing, not a
  razor arris and not a broad rounded cove; `FilletAt` adds that fillet. A cliff with a bit but no
  height step draws no wall, and `mise run validate` reports it.
- Rock scatter. Every known cliff whose lifted side is known carries a wall from the
  lifted side. A coastal cliff (`EdgeRocks`) draws 22 columns per edge on the waterline,
  with sea stacks and surf only where the lower side is water. A land/land cliff
  (`LandEdgeRocks`) draws 48 pillar positions per edge; each pillar finds the drawn face
  with `DrawPoint`, stands `FaceOut` (0.03) outside it, probes the ground on several
  rings for its top and foot, and yields a wide, short plinth at its foot, so the wall
  is thickest at the ground and thins to pillars at the crest. Cliff rocks are exact
  scatter: they are never thinned. A land/land cliff whose lift is unknown stays bare,
  because a wall with nothing behind it reads worse than no wall.
- Colour. The mesh carries the sharp cliff share in `UV.x`; inside that band the palette
  transition drops its wander and narrows to the step's own width, so the raised top
  keeps its own ground instead of taking the colour of the tile below it. Ordinary props
  are kept off a cliff face when the cliff share is above 0.2 and the normal is steeper
  than 0.8.
- Rivers. Rivers run along hex edges in this ruleset, so a river edge may also carry a
  cliff: `RiverMesher` turns the channel downhill over the face, and the water shader
  foams where it does. The generator keeps river mouths and sea-edge river endpoints
  open and scores river edges higher (`RiverFallBonus`) so some rivers pour over a
  cliff.

## Teams and entity data

Players have `TeamId`, civics, culture and treasury. Each player defaults to a separate
team. Teams hold members, known techs, a research target, progress per tech and war
relations. `Simulation.TeamOf(player)` and `KnowsTech(player, tech)` provide rule
access. Changing a research target retains its progress. `GameState.SetWar` keeps the
war matrix symmetric; only the Relations module calls it during play (see Relations
below).

Cities include a generic `ProductionQueue` of
`ProductionOrder(Kind, ContentId, Progress)` entries. Cities owns feature queues, paused
item progress, completion snapshots, and normalized overflow. Test rules live in
`tests/Ariel.Tests/TestRuleset`. `GameState` contains plain data and rebuildable lookup
caches. Caches do not enter saves or hashes. `UnitById`, `UnitsAt`, `CityById` and
`CityAt` use indexes. Runtime edits use `AddCity`, `RemoveCity`, `AddUnit`, `RemoveUnit`
and `MoveUnit`. After bulk data edits, `RebuildIndexes` restores the caches.

## Mod registration

Each mod folder contains `mod.json`, the mod's DLL, the API assemblies it publishes
or compiles against (see Mod APIs) and optional JSON sidecars. Dependencies
determine load order. Ordinal mod ids break ties. Ids use `modid:name`. Registration
requires the owning namespace. Overrides can replace earlier content.

- `ctx.Content<T>()` returns `ModRegistry<T>` for any `ContentDef` subtype.
- `ContentDb.Get<T>()`, `Get<T>(id)` and `TryGet<T>(id, out item)` resolve generic
  content.
- `ctx.RegisterService<T>(name, service)` registers a non-content implementation under
  its shared interface.
- `ContentDb.Services.Get<T>()` returns implementations in mod-load and registration
  order. Use it for seams with many implementations (`IModifierSource`,
  `IRequirementEvaluator`, `IProductionKind`) and for a mod's own service. The two
  many-implementation seams that clients and planners read, unit orders and support
  fire, are also bound as lists on `GameRules` (below).
- `sim.Service<T>()` (or `content.Services.Find<T>()`) returns the one service
  registered under `T`, or null when no loaded mod registered one, and throws when
  several did. It is the lookup for a contract one mod provides, above all an
  interface of a mod's published API. The answer is cached per contract type once
  content freezes (an array slot per type, no registry search), so planners and rule
  code may call it on hot paths and from parallel AI scoring.
- `sim.Rules()` (or `content.Rules()`) returns `GameRules`: one property per base
  service (`Units`, `Combat`, `CityYields`, `Visibility`, `Relations`, ...), a
  nullable property per base-owned seam one mod answers, and a list per seam many may
  answer (see "The base game's seams" under Mod APIs). It has no property typed by a
  mod's own service; a mod's service is found with `sim.Service<T>()`. `GameRules.Bind` fills them
  from the registry once, when content freezes, so rule code reads a field instead of
  searching the registry on every call. A base module still registers each service,
  so service identity stays in the content hash.
- A feature that gives units an order registers an `IUnitActionSource`, which offers
  ready-made `(id, label, command)` candidates for a unit. The client validates and
  lists every source's offers (`GameRules.UnitActions`) without naming the commands, so
  a mod adds an order without a client change. Artillery (the RangedCombat mod) and
  great people (the GreatPeople mod) use it, and so does the base game's mission
  support; air missions, cargo and promotions are still listed by the client itself
  and should move to sources.
- A feature that lets a unit hurt a tile without entering it registers an
  `ISupportFire`. AI assaults ask every kind in registration order
  (`GameRules.SupportFire`) for the command that fires a unit at the target, instead of
  naming bombardment and ranged strikes.
- The RangedCombat mod is the model for a military feature outside the base game. It
  owns the bombard and ranged-strike commands, their rules, its unit orders and its
  support fire. Military keeps what such fire acts on and offers it as entry points:
  `ICombatService.CityDefense`, `BombardResistance`, `DamageCityDefense`, `Collateral`
  and `RecordStrike`, and `IUnitService.Effect` and `SpendAttack`. The unit and
  building fields that describe the weapons (`BombardRate`, `BombRange`,
  `BombardDefense`) stay in the base schema, as unit data carries them. Without the
  mod no fire is offered and siege units fight only in melee.
- A mission is something a unit does to the city it stands in. A feature registers each
  kind as an `IMission` (`Subjects`, `Label`, `Validate`, `Terms`, `Resolve`), and the
  base game's `IMissionService` (`GameRules.Missions`, in Military) does what every
  mission shares: it checks that the unit is the player's, alive, able to act and in a
  city, rolls `MissionTerms.Chance` once, removes the unit as `MissionTerms.Fate` says,
  publishes `MissionResolved`, refreshes sight, and offers every legal mission in the
  unit's panel. One command, `PerformMissionCommand`, carries them all. Religion
  (`religion:spread`, `religion:inquisition`, `religion:shrine`), the Corporations mod
  (`corporations:found`, `corporations:spread`) and the Espionage mod (one
  `espionage:*` kind per spy mission) are built on it. A mission collects its own
  price and publishes its own event for what it changed. Great person actions have the
  same shape and are not ported yet. Planners still choose missions per feature: the
  contract says what is possible and on what terms, not what it is worth.
- `ITurnSystem` retains explicit `(Order, Id)` execution order.

Registries freeze after load. Definitions and their nested collections must not change
after that point. The content hash includes canonical definitions, loaded mod
ids/versions, command identities, service order and state-component identities.

### Mod APIs

A mod publishes its own API so that other mods, the client and tests can build on it
without the base game naming it. The API is a separate assembly, by convention
`src/Mods/<Mod>/Api/Ariel.Mods.<Mod>.Api.csproj` (the mods' `Directory.Build.props`
keeps `Api/` out of the mod project). It holds the mod's service interfaces, commands,
domain events, saved state and content definitions, in the namespace
`Ariel.Mods.<Mod>`, references only `Ariel.Core`, `Ariel.Rules` and other mods' APIs,
and is marked `[assembly: ModApi("<mod id>")]`. The mod's manifest names it:
`"api": "Ariel.Mods.<Mod>.Api.dll"`. The mod implements the API and registers its
services under the API's interfaces.

The loader (`ModApis`) keeps exactly one copy of each API assembly in the process, so a
service registered under an API interface is found under that same type by every mod,
the client and the tests:

1. A copy already shared wins.
2. Otherwise the host's copy: if the context that loaded `Ariel.Core` (the default
   context, or Godot's own) resolves the name, because the host was compiled against
   the API, that copy is shared.
3. Otherwise the copy in a mod folder is loaded once into a shared API context.
   `ModLoader.FromDirectories` publishes the API every provider's manifest names
   before any mod code runs, so the provider's copy wins over its consumers' copies.

A mod's load context sends every reference that is an API (a file marked `ModApi`) to
that one copy; anything else a mod carries stays private to it. A consumer's build
copies the APIs it compiles against into its own folder (a normal project reference),
so it still loads when the provider is absent or disabled: the API types resolve and
`sim.Service<T>()` returns null. The client and tools reference the APIs they present,
so their output has the host copy. Copies in several mod folders are therefore
expected and harmless. An API built against a different version than the shared copy
binds to the shared copy anyway; change the API compatibly or bump the provider's
manifest version.

Manifest fields and API use:

- Compiling against another mod's API needs no manifest entry. Treat the provider as
  optional: check `sim.Service<T>()` for null and degrade. The client reads both
  published APIs this way.
- List the provider in `dependencies` when the consumer cannot work without it, or
  must register after it (to override its content, for example). Loading then
  requires the provider, and it registers first. Services are looked up only after
  content freezes, so using a service alone never needs registration order.
- List the provider in `references` when the consumer's content refers to the
  provider's content ids. That too requires the provider, without ordering.
- Never make the base game reference a mod API. When base rules need an answer from a
  mod, the base game owns a small seam, named after the question it asks, and the mod
  implements it (below). Without the mod the base game applies its default. Base content
  names corporations only by `CorporationIdentityDef`, which therefore stays in
  Foundation, like `VictoryIdentityDef`; `ProjectDef` and `VictoryDef` belong to the
  ProjectsVictory API. The computer player follows the same rule: it knows no feature
  mod, and a feature mod contributes the planner that plays it (`IAiPlanner`) and the
  production it wants (`IProductionWishes`, below).
- A mod's API interface may extend the seam it answers (`ICorporationService :
  ICityCorporations`, `IEspionageService : IEspionageSpending`, `ITradeRouteService :
  ICityTradeLinks`, `IRevolutionService : ICityUnrest`), and the mod registers its
  service under both. A seam answered by a helper (a capital weight, a free-building
  grant) is registered on its own.

#### The base game's seams

Each seam lives in `Ariel.Rules/Contracts` beside the rules that ask it. A nullable
`GameRules` property means one mod answers; a list means every registered answer counts.

| Seam | `GameRules` | Asked by | Answered by | Without an answer |
| --- | --- | --- | --- | --- |
| `ICityCorporations` | `CityCorporations` | the Foundation `corporation` requirement leaf | Corporations | no corporations; the leaf fails |
| `ITeamAgreements`, `IDispositions`, `IPlayerExchanges` | `TeamAgreements`, `Dispositions`, `Exchanges` | Relations, Economy (see Relations) | Diplomacy | no agreements, neutral attitude, no deals |
| `ICapitalWeight` | `CapitalWeights` (list) | Cities, when a lost capital moves | Religion (one per faith), Corporations (one per branch), GreatPeople (two per settled great person) | size, yields, level and culture alone |
| `IFreeBuildingGrants` | `FreeBuildingGrants` (list) | Cities, for empire-wide free buildings | ScriptedWonders | only constructed buildings grant |
| `ICityUnrest` | `CityUnrest` | Religion (inquisition compensation calms the city), the AI religion and great people planners | Revolution (the instability index) | no unrest; compensation is paid in gold |
| `ICityTradeLinks` | `TradeLinks` | Religion, for natural spread | Trade | spread follows city connections only |
| `IEspionageSpending` | `EspionageSpending` | Economy, for the ledger forecast and the espionage phase | Espionage | the economy banks espionage output |
| `IPlayerScores` | `Scores` | Research, for diffusion welfare | ProjectsVictory | no welfare factor |
| `ICombatLeaders` | `CombatLeaders` | Military, right after combat earns general points | GreatPeople (Great General births) | general points accumulate |
| `IBarbarianBarrier` | `BarbarianBarriers` (list) | Military movement, for a barbarian that does not attack | ScriptedWonders (border wonders) | nothing bars barbarians |
| `IProductionCompletion` | (registry) | Cities, for a finished order of a kind it does not own | ProjectsVictory (projects) | nothing |
| `IUnitActionSource`, `ISupportFire`, `IMission` | `UnitActions`, `SupportFire`, `MissionKinds` | the client, AI assaults, missions | base modules and mods | see above |

Mods also consume each other's APIs directly, always as optional: Espionage asks the
Revolution API to incite a revolt, Revolution asks the GreatPeople API whether a golden
age calms a city, and GreatPeople prices a trade mission from the Trade API. Each
references the provider's `Api` project and treats a null `sim.Service<T>()` as "absent".

#### Content attachments

A mod that has something to say about another module's definition attaches its own row
to it instead of asking for a field on the other module's schema. The row type derives
from `ContentAttachment<TTarget>` in the mod's API and is an ordinary `ContentDef` in
the attaching mod's namespace, with a `Target` naming the definition it is about. Trade
attaches `CivicTradeDef` to civics (`trade:mercantilism` about
`government:mercantilism`), Corporations `CivicCorporationsDef`, Diplomacy
`CivicDiplomacyDef`, Revolution `CivicRevolutionDef` and ProjectsVictory
`CivicProjectsDef`.

- Loading validates an attachment like any definition: `ModLoader.Json` rejects an
  unknown property, `Target` is a content reference to `TTarget` (a missing id or one
  of another kind fails), and the target's mod must be in the attaching mod's
  `references` or `dependencies`.
- When content freezes, `ContentDb` indexes one row per (attachment type, target). Two
  rows of one type about one definition fail the load, because the type's owner could
  not know which to read; another mod that wants to change an attached row overrides it
  by id, like any definition.
- Attachments are definitions, so they are in the content hash: changing attached data
  changes the hash that saves and multiplayer sessions compare.
- The owner reads its row with `content.Attachment<T>(target)`, which is null when no
  row exists (the row's defaults then apply). Presentation reads every row with
  `content.AttachmentsOf(target)`; a row that implements `IDescribedContent` describes
  itself on the civics screen and in the pedia.

Attachments replace typed fields only where the reading module is a mod. A rule the base
game applies to any calculation is an open effect instead (Modifiers and ledgers).

## Setup and presentation services

Mods register these contracts through `ctx.RegisterService<T>()`:

- `ISetupPolicy.Default(SetupField)` supplies an optional default content id.
  `SortOrder(SetupField, string id)` supplies an optional display order. Each field
  permits at most one default provider. Each choice permits at most one display-order
  provider. The client selects the first loaded choice when no default exists.
  Display-order ties use ordinal ids.
- `ISetupPolicy.GameOptions(ContentDb)` supplies optional setup game options.
  `GameCatalog.GameOptions` aggregates these options for the setup screen.
  `SimulationSettings.GameOptions` records enabled option choices.
  `Simulation.IsOptionEnabled(string id)` checks option state during play. The setup
  screen displays checkboxes for implemented simulation options. Remaining option
  definitions in `src/Ariel.Rules/Foundation/data/game-option.json` remain deferred as a future
  pending item.
- `IProductionKind` supplies a queue-kind id, tab label, continuous-production flag,
  content candidates, and definition lookup. It also supplies base costs, eligibility
  visibility, validation, and production calculations. The client builds tabs and
  choices from all registered kinds in service order. Continuous kinds show no
  completion turns or progress bar. Cities registers building and unit kinds. Economy
  registers the process kind. Commands require an explicit kind or hurry id, except
  queue-removal commands.
- `ILedgerSourceLabel.Label(Simulation, string source)` supplies optional text for
  mod-owned ledger sources. The client uses the first non-null label in service order,
  then content names or generic text.
- `ITileAppearanceService` supplies optional terrain and feature metadata by full
  content id. `TileAppearance` stores integer RGB, tint percent, mark count, optional 3D
  scatter set name and density, and a terrain's ground spread (0..100, how far its
  ground reaches into neighbouring tiles). `TerrainSurface` enumerates procedural and
  system surfaces (`Cliff`, `Seabed`, `DeepSeabed`, `Unexplored`). Default interface
  methods `Surface(TerrainSurface)` and `Scatter(string feature, string terrain)` allow
  mods to supply surface textures and terrain-specific scatter variants (such as snowy
  pine trees on tundra/snow forests). The client uses the first non-null result in
  service order. Unknown terrain is gray, and unknown features have no marks or props.

`ICityWorkService.DefaultSpecialist(Simulation)` identifies the plain citizen role for
assignment controls. All presentation and setup queries must avoid state changes and RNG
use. Service implementations hold no mutable per-game state. Service identities enter
the content hash. Mods must change their manifest version when service behavior changes.

## Turn phases

`Ariel.Game.TurnPhase` defines shared order slots. Systems use these constants instead
of private numeric priorities. Systems with equal orders run by ordinal `Id`. Additional
orders can use the gaps between slots.

Orders from `PlayerBegin` through `Map - 1` require
`IPlayerTurnSystem.Run(Simulation, int player)`. The scheduler completes this interval
for one player before the next player, in ascending player-id order. Other orders use
`ITurnSystem.Run(Simulation)` once per boundary. The test ruleset's orders lie
outside the player interval.

| Constant             | Order | Work                            |
| -------------------- | ----: | ------------------------------- |
| `GameBegin`          |     0 | Global begin hooks              |
| `Properties`         |   100 | Global properties               |
| `Deals`              |   200 | Deal settlement                 |
| `Teams`              |   300 | Team maintenance                |
| `PlayerBegin`        |  1000 | Player timers and caches        |
| `AssignWork`         |  1100 | City work assignments           |
| `Gold`               |  2000 | Treasury settlement             |
| `Research`           |  2100 | Research and discoveries        |
| `Espionage`          |  2200 | Espionage allocation            |
| `Economy`            |  2300 | Taxes, consumption, inflation   |
| `CityBegin`          |  3000 | City preparation                |
| `CityGrowth`         |  4000 | Food, growth, starvation        |
| `CityCulture`        |  4100 | City culture                    |
| `CityPlotCulture`    |  4200 | Plot culture and borders        |
| `CityAutoBuildings`  |  4300 | Automatic buildings             |
| `CityProduction`     |  5000 | Production eligibility and work |
| `CityCompletion`     |  5100 | Completion effects              |
| `CityPostProduction` |  6000 | Decay and later city rules      |
| `PlayerEnd`          |  7000 | Remaining player maintenance    |
| `Units`              |  8000 | Units and worker progress       |
| `Map`                |  9000 | Improvements and map updates    |
| `Spawns`             |  9100 | Barbarians and other spawns     |
| `Environment`        |  9200 | Environment and global founding |
| `Visibility`         |  9500 | Team sight                      |
| `GameEnd`            |  9900 | Pre-end hooks                   |

These slots follow `CvPlayer::doTurn` and the city pipeline: gold precedes research, and
growth and culture precede production. Cities processes all owned cities at each city
slot, in ascending city-id order. Completion commits effects in that same order. This
preserves the source pipeline's cross-city stage boundaries without parallel mutations.
New buildings cannot change already-settled gold or research in that turn.

The simultaneous engine resolves submitted commands before these slots. It resolves
global map work after player work, rather than before player activation as in
`CvGame::doTurn`. This boundary adaptation gives worker completions one defined
map-update point. It does not reproduce sequential activation or shuffled source player
order. Most slots reserve future work and do not yet contain systems.

`TurnEnded` follows `GameEnd`, before the turn counter increases. `TurnStarted` follows
the counter increase and precedes AI commands. Both events retain their existing
presentation semantics.

## Feature ruleset setup

The kernel requires exactly one registered `IRuleset`; `ContentDb.Ruleset` resolves it at
freeze. The base game's is `GameRules`, registered by Foundation. It requires
`SimulationSettings.WorldSize` and fails with a clear error without one. Settings
include `WorldSize`, `GameSpeed`,
`Handicap`, `StartingEra`, and one entry in `Leaders` per player. Empty speed, handicap,
era, and generator ids resolve through mod-registered `ISetupPolicy` services before
generation. Explicit ids remain unchanged. `Simulation.Settings` stores the resolved
ids, and saves retain them. World supplies its generator default. Foundation supplies
the speed, handicap, era, and setup-screen world-size defaults. The test ruleset
supplies its own generator default. Without a generator policy, Core selects the first
loaded generator in ordinal id order. Core never selects a world size implicitly. An optional `Civilizations` entry resolves a
leader shared by multiple civilizations. An incompatible selection fails before
generation.

Foundation resolves dimensions from `WorldSizeDef.GridWidth/GridHeight` times the source
landscape-cell scale of four. Standard therefore contains 84 × 52 = 4368 plots, with
east–west wrap. `State.Map` holds the effective map. `Settings.Map` is the literal map
a ruleset without world sizes uses, as the test ruleset does.

Foundation creates teams and grants civilization and handicap starting technologies.
Civilization free-unit classes resolve through `UnitOverrides`, including explicit
disabled classes. Era and handicap fields supply multipliers, extra
worker/defense/exploration grants, and starting gold. Role selection uses eligible land
units, known requirements, work rate, strength, movement, and exploration suitability.
It approximates `CvPlayer::addFreeUnitAI`, not the full military AI valuation. All
initial units share their player's start plot. Movement rules remain a Military task.

World scores radius-two and radius-three food and production, with coast and fresh-water
weights. It selects habitable starts with minimum wrapped spacing, then improves weak
starts with grass and forest near the center. Normalization never changes water, peaks,
or resource plots. Impossible spacing produces a setup error, not overlapping starts.
The harness `tests/Ariel.Mods.Tests/GameFactory.cs` creates full headless games through
this API.

## World services

Clients and mods reach these contracts through `sim.Rules()`:

- `IPlotYieldService.Calculate(Simulation, int player, TileId)` returns
  `PlotYieldResult(YieldVector, Ledger)`.
- `IPlotYieldService.CalculateNature(Simulation, int player, TileId, bool ignoreFeature = false)`
  returns natural potential.
- `IWaterService.IsFreshWater(Simulation, TileId)` and
  `IsIrrigated(Simulation, int player, TileId)` return availability.
- `IResourceNetwork.ConnectedCount(Simulation, int player, int city, string bonus)`
  returns physical connected copies.
- `IWorkerBuildService.Validate(Simulation, int player, int unit, string build)` returns
  a rejection reason or null.
- `IWorkerBuildService.Start(Simulation, int player, int unit, string build)` applies a
  validated assignment.
- `IWorkerBuildService.RequiredWork(Simulation, TileId, string build)` returns the
  integer work threshold.
- `IWorkerBuildService.IsWorking(Simulation, int player, int unit)` checks active build
  orders.
- `IVisibilityService.Recompute(Simulation)` updates visible and revealed team masks.
- `IVisibilityService.CanSeePlot(Simulation, TileId observer, TileId target, int team, int range, bool aerial = false)`
  tests line of sight.
- `IVisibilityService.SnapshotOf(Simulation, TileId tile, int team)` returns
  last-observed plot state.
- `INavalBlockadeService.IsTileBlockaded(Simulation, TileId tile, int team)` and
  `IsCityBlockaded(Simulation, int city)` query naval blockades.
- `IStartingPositionService.Select(Simulation, int count)` selects locations without
  mutation.
- `IStartingPositionService.Normalize(Simulation, IReadOnlyList<TileId>)` applies
  setup-only terrain changes.

Read-only queries never consume RNG or change authoritative state. Services hold no
per-game caches. Plot yields include terrain, relief, lakes, features, revealed
resources, improvements, routes, irrigation, technology changes, and registered plot
modifier sources. Feature river changes replace terrain river changes. Route synergy
suppresses smaller river contributions per yield, as in `CvPlot::calculateNatureYield`.
Nature clamps before improvement effects. Flat sources sum before percentage
multiplication, integer division, and the final nonnegative clamp. Ledger keys
`yield.flat.*` and `yield.percent.*` explain inputs. `world:food`, `world:production`,
and `world:commerce` hold final outputs. Cities owns city-center minima and
city-specific plot effects. Government and Peoples can contribute through
`IModifierSource` with `ModifierScope.Plot`.

The generator adapts `Continents.py` and `FractalWorld`: polar fractal land with 75%
water, separate relief, and latitude-based climate. Integer value noise replaces the
native fractal primitive. Priority-flood drainage connects high ground to water across a
temporary filled surface. River masks describe shared hex edges, not square-map river
directions. `SetRiver` maintains both sides. Flooded depressions can produce level
drainage steps. The drainage parent order prevents cycles. Connected water areas with at
most the configured lake size supply fresh water. Features and resources obey typed
placement restrictions. Resource density uses appearance, eligible tiles, player count,
class spacing, and clustering. Starting normalization can replace climate-selected
terrain near starts, but never invalidates a resource's terrain or feature. Goody huts
use improvement spacing and tiles-per rules. Starting eras can suppress all huts.

Networks use routes, city nodes, shared river edges, and technology-enabled coast/ocean
plots. Unowned plots require exploration. Physical resource networks and city
connections transit revealed peaceful foreign territory without Open Borders treaties
(`CvPlot::isTradeNetwork`). International city trade routes still require Open Borders
agreements when Diplomacy is active. Naval blockades by enemy warships within
`SHIP_BLOCKADE_RANGE` (default 3) on non-lake water sever water network tiles. Blockades
also prevent coastal cities from working water tiles and cancel trade routes. Autonomous
AI blockade planning is absent. Queries recompute components so ownership, pillage,
blockade, and technology changes take effect immediately. The `bonus` evaluator uses
connected counts. Vicinity requirements check bonus reveal, obsolescence, city
availability, and physical connectivity to local sources. Valid sources include city
centers, improved local plots, or active building grants.

`world:start_build` serializes the Core `StartBuildCommand`. Workers share plot progress
by build id. An assignment stores its original tile, so movement cancels the assignment
at the next worker phase. `IWorkerBuildService.IsWorking` checks active orders so legal
continuing work avoids repeat costs or abandonment. Build times apply terrain, speed,
and starting-era multipliers in sequence. Building on peak tiles applies
`PEAK_BUILD_TIME_MODIFIER` (+100%). Upgrading an existing route to a superior route
subtracts the existing route's base build time under `ROUTES_UPGRADE`, with a minimum
floor of one. Known technologies and AI handicap fields modify work rates. Completion
can remove a feature, create an improvement or route, change terrain, or consume a
worker. A feature effect can permit removal without its production technology. Such
removal grants no production. `FeatureProduction` names the nearest owned city and the
production grant. Cities must subscribe to apply overflow. Improvement upgrades require
worked turns or an outside-borders target, plus the target's technology. City disorder
or empire anarchy suspends discovery, mine exhaustion, and normal upgrades. Genuinely
unworked tiles retain the existing outside-borders upgrade exception.

At `TurnPhase.Map`, `ImprovementTurnSystem` processes feature evolution and resource
discovery. Feature disappearance rolls (`FeatureDef.Disappearance`) can remove temporary
features. Adjacent feature growth and spread rolls (`FeatureDef.Growth`,
`FeatureDef.Spread`, `ImprovementDef.FeatureGrowth` on preserves) scale with game speed,
feature growth modifiers, and route penalties. Worked improved bonus-free tiles roll for
resource discovery, with a 10× boost when `foundation:gameoption_resource_depletion` is
active. When resource depletion is active, worked mines can exhaust to
`world:depleted_mine`, setting `Plot.DepletedMine` and blocking new mines until a bonus
is revealed. Resource deposit exhaustion removes exhausted bonuses. Deposit depletion
uses the reference zero-consumption path until a resource-consumption service exists.

`GlobalWarmingSystem` runs at `TurnPhase.Environment`. Global warming defense sums map
features (`FeatureDef.WarmingDefense`), doubled for players with active health civics
(`TREEHUGGER_DEFENSE_BONUS`), scaled by `GLOBAL_WARMING_FOREST` per land plot. City
pollution sums building unhealthiness (`GLOBAL_WARMING_UNHEALTH_WEIGHT`), bonus
unhealthiness (`GLOBAL_WARMING_BONUS_WEIGHT`), and dirty power
(`GLOBAL_WARMING_POWER_WEIGHT`). City wellbeing queries derive pollution from active
building and resource rules because city power outage state is absent. Warming rolls
consume probabilities and degrade random non-city plots. Degradation steps follow snow
to tundra or marsh, grass, plains, desert, and coast. Tile degradation clears
improvements and emits `GlobalWarmingOccurred`. Deterministic port RNG selection uses an
ordinal non-city candidate array instead of native random map coordinate retries.

Visibility runs at `TurnPhase.Visibility`, after founding, and after unit movement.
Line-of-sight elevation and feature occlusion (`TerrainDef.SeeFrom`,
`TerrainDef.SeeThrough`, `FeatureDef.SeeThrough`) block sight behind hills, peaks, and
dense features. High ground grants outer-ring vantage sight across lower terrain. Aerial
units and recon bypass terrain occlusion. Per-team fog-of-war plot snapshots
(`plot.RevealedSnapshots[team]`) preserve last-observed terrain, relief, height, cliff
bits and derived cliff lifts, features, bonuses, improvements, routes, and ownership.
Missing fog memory hides tiles until real observation or explicit map reveal, preventing
live state leaks. The client reads perceived snapshots for fogged tiles to hide unseen
mutations.

`military:pillage` (`PillageCommand`) validates cargo, domain, and improvement
permanence. During peace, a player can demolish their own non-permanent improvement
without a gold award. Wartime enemy pillaging awards gold from
`ImprovementDef.PillageGold` scaled by `pillage_percent` and steps improvements down
along `ImprovementDef.ImprovementPillage`. Pillaging routes removes the route and emits
`PlotPillaged`. `WorkerPlanner` prioritizes repairing ruins, depleted mines, and severed
routes, and replacing pillaged improvements to reconnect revealed resources.

`Plot.Kind`, `Plot.Height`, `Plot.CliffEdges`, `IsLake`, `DepletedMine`,
`ImprovementTurns`, `BuildProgress`, `RevealedSnapshots`, player leader/start ids, and
worker components enter saves and state hashes. Heights are authoritative and live-state
cliff lifts are derived, so the map never stores a lift that disagrees with its heights;
fog snapshots carry the lift they derived when captured. The save schema is `4`; schema
`3` and older are rejected by the version check. Changed mod versions reject old content
identities unless the caller explicitly forces loading. Hand-authored geometry tuning
lives in `src/Ariel.Rules/Foundation/data/hex-overrides.json` and enters the content hash. The
importer writes only its declared definition families and rejects that reserved
filename.

## City services

Cities registers the city query contracts. All queries are RNG-free and do not change
authoritative state. `Calculate(Simulation, int city)` returns ledgers through
`ICityYieldService`, `ICityWellbeingService`, `ICityGrowthService`,
`ICityProductionService`, `ICityMaintenanceService`, and `ICityGreatPeopleService`.
Production calculation additionally takes `(string kind, string content)`.
`ICityProductionService.Validate` returns structured requirement failures with the same
arguments. `ICityWorkService` supplies `Radius`, `WorkArea`, assignment validation, and
authoritative `Assign`. `ICityBuildingService` provides `EffectiveBuildings`,
`ActiveBuildings`, `IsPowered`, `IsDirtyPower`, and `ProvidesPower`.
`ICityCaptureService` provides `Capture`, `ValidateRaze`, and `Raze`.

`CityYieldResult.Yields.Commerce` is raw whole commerce. `DirectCommerceHundredths`
bypasses sliders. `CommercePercent` contains additive percentage bonuses, not final
rates. Economy allocates raw commerce, adds direct contributions, and applies each
commerce modifier. Economy can register one
`ICityCultureSource.CultureHundredths(Simulation, int city)` provider for the final
culture rate. Without that provider, Cities applies culture modifiers to direct culture
only.

Commands use `cities:found_city`, `cities:set_city_work`, `cities:set_city_production`,
`cities:conscript`, and `cities:resolve_conquest_choice` identities. The Core types are
`FoundCityWithSettlerCommand`, `SetCityAssignmentsCommand`, `SetCityProductionCommand`,
`ConscriptCityCommand`, and `ResolveCityConquestCommand`. Queue kinds are
`cities:building` and `cities:unit`. Cities completes at most one item per city per
turn. Growth and culture precede production. Completion effects follow production work
in city-id order.

`ICityProductionService` handles order completion and surplus conversion. Surplus
production above the overflow cap converts into treasury gold. The overflow cap is the
maximum of item cost and current turn production. Surplus production scales back from
item-specific modifiers to city base yield production modifiers. The system multiplies
scaled surplus by unit, building, or project gold percentages. Invalid head orders in
the queue are removed and refund accumulated progress as gold.

`ICityBuildingService` resolves power states and active building lists.
`CityData.IsPowered` checks local power plants and area clean power sources. Local
plants supply power through `ProducesPower` or `PowerBonus` with connected resources.
Buildings with `AreaCleanPower` grant clean power to all team cities on the landmass
(`IsAreaCleanPower`). `CityData.IsDirtyPower` is true when all active local power
sources have `DirtyPower` and clean power is absent. Buildings with `RequiresPower` fail
construction validation and deactivate when unpowered. Clean area power blocks
construction of buildings with `NoPower`. Powered cities apply `PowerYieldModifiers` in
`BuildingEffects`. City wellbeing adds `PowerHealthChange` for power and
`DirtyPowerHealthChange` for dirty power.

Free buildings calculate dynamically from team `FreeBuilding` sources and completed
`EmpireFreeBuilding` wonders. Categories resolve through owner civilization overrides or
original civilization under assimilation. Grants appear in `CityData.EffectiveBuildings`
and `ActiveBuildings` without mutating `city.Buildings`. Building replacement is
non-destructive. Constructed buildings remain in city storage when replaced. Active
buildings with `ReplaceBuildings` suppress matching lower-tier buildings in
`CityBuildingRules.Active`. Suppressed buildings reactivate automatically if the
replacer becomes obsolete or is removed. `ProductionEligibility` blocks construction of
replaced buildings and checks `ExtendsBuildingClass` prerequisites.

`ICityCaptureService` executes city conquest during wartime. Captures calculate capture
gold from population and random rolls, scaled by elapsed ownership turns. Building
survival strips capitals and non-capturable buildings. Surviving buildings check
location validity, player class limits, and survival percentage rolls. Team culture
majority guarantees building survival on recapture. Capturing or razing a capital
relocates the victim capital automatically via `CityCapital.RelocateIfNeeded`. Size-1
cities that never grew auto-raze upon capture. Captures with less than 75% team culture
suffer civil disorder during `OccupationTurns`.

Human conquests set `PendingConquestChoice` and default to `Puppet` until resolved.
Players resolve conquests with `CityConquestOption.Annex`, `Puppet`, or `Raze`. Puppet
cities manage citizen assignments and production automatically via the governor AI.
Puppet cities reject manual queue, citizen assignment, hurry, and conscription commands.
Puppet governance reduces city-count maintenance by 50% and city-count unhappiness by
50%. Players can annex puppet cities at any time through `ResolveCityConquestCommand`.
AI conquests evaluate city counts, raze probabilities, and raze protections. Capitals,
wonders, holy cities, headquarters, and strategic resource cities are protected from
razing.

`ICityConscriptService` provides `Calculate`, `Validate`, and `Conscript` operations for
drafting. The city selects the highest-priority eligible unit with `Conscription > 0`
(respecting civilization overrides and assimilation). Conscription validates against
minimum nationality team culture on the city plot (`CONSCRIPT_MIN_CULTURE_PERCENT`,
default 50%), absence of civil disorder, adequate population
(`ConscriptMinCityPopulation = baseMin + unit.Cost / perCost`), and remaining player
draft quotas (`cities:max_conscript`). Executing a draft deducts population, spawns the
unit with zero moves and halved accumulated local production experience, marks
`DraftedThisTurn`, and increments `ConscriptAngerTurns` by `flatHurryAngerLength` scaled
by world size draft modifiers.

`CityRuleState` and `CityPlayerState` contain saved feature-rule state. `CityRuleState`
tracks `TurnAcquired`, `HighestPopulation`, `OccupationTurns`, `IsPuppet`, and
`PendingConquestChoice`. Modifier sources use City scope for local effects and Player
scope for player growth/maintenance percentages. City keys include `cities:happiness`,
`cities:health`, `cities:war_weariness`, `cities:food_kept_percent`,
`cities:growth_percent`, and `cities:extra_culture_level`. Specialist keys use
`cities:slots:<specialist-id>`, `cities:free:<specialist-id>`, and
`cities:free_specialists`. Other keys cover maintenance percentages, production
percentages, experience, and great-person percentages. Modifier calculations inspect
these keys during yield, wellbeing, and growth processing.

Food waste uses fixed-point recurrence with scale one billion. City-count maintenance
uses a fixed-point root with scale one trillion. These deterministic adaptations can
differ from native floating-point results near integer boundaries. Maintenance uses the
maximum wrapped hex distance as the map normalization.

Cultural spread uses shortest-path distance search bounded by city cultural radius.
Pathfinding blocks spread across impassable terrain, peaks, and hostile enemy borders.
The realistic culture option adds terrain and feature distance penalties plus river
crossing costs. Founding grants claim unobstructed first-ring plots immediately. Plot
ownership resolves among living cities whose cultural radius reaches the plot. Ownership
preserves an incumbent on ties, then selects the lowest player id. `DecayPlotCulture`
runs at `TurnPhase.Map + 2` on game-speed golden age halving intervals
(`CultureDecayTurns`). Plots with culture for non-barbarian players above
`CityFreeCultureGrowthFactor` and foreign city `CultureHundredths` are halved, allowing
cultural borders and city nationality to assimilate over time. When cities are founded
or captured, `CityRuleState.OriginalCivilization` records the origin civilization. Under
the assimilation game option (`foundation:gameoption_assimilation`), production
eligibility and conscription resolve unit and building class overrides against
`OriginalCivilization` rather than current owner civilization.

`ICityWellbeingService` calculates `cities:culture_anger` based on foreign living
players' plot culture on the city tile, with a +50% multiplier against teams at war and
complete suppression for same-team players. Foreign nationality unhappiness is further
modified by civic `ForeignerUnhappyPercent` under `government:foreigner_anger`.
Modifiers under `cities:extra_culture_level` (from traits, civics, or civilizations) are
additive and expand initial city founding culture and border radius. Governor weights
live in the hand-authored hex sidecar. The governor orders equal plot scores by tile id.
No feature mod references a test ruleset assembly. The client loads feature rules
through Core contracts.

## Research and economy services

Research registers `IResearchService` for cost ledgers, rate modifiers, eligibility,
queues, and team contributions. `research:set_research` uses `SetTeamResearchCommand`.
It accepts a target and optional queue, or one free-tech selection. `ResearchTeamState`
saves normalized overflow, repeat counts, free-tech credits and queues; the diffusion
bonus asks the Relations module which teams have met. Discoveries publish
`TechDiscovered`. Cities updates the current era through this event. Overflow applies on the next contribution. Optional diffusion and timeline
policies remain deferred.

Economy registers `IPlayerEconomyService` for player ledgers, city commerce ledgers,
rates, and rate validation. `economy:set_commerce_rates` uses `SetCommerceRatesCommand`.
City commerce keeps hundredths through allocation, direct contributions, percentage
modifiers, and process conversion. Gold settlement saves a commerce snapshot.
`IPlayerResearchSource` supplies that snapshot to Research. `ICityCultureSource`
supplies final culture to Cities. Queries never mutate state or consume RNG.

`ICityProcessService` supplies eligibility and conversion for infinite `economy:process`
queue items. Processes use current production. Stored overflow remains available for
later finite production. `ICityHurryService` supplies payment previews and mutations
through `economy:hurry` and `HurryCityCommand`. Cities completes hurried items during
its normal completion phase. Whip anger duration dynamically incorporates building
modifiers (`cities:hurry_anger_percent`) with a 1-turn floor. When a building with
`HurryAngerModifier` completes in a city with active `HurryAngerTurns > 0`, the
remaining anger turns are retroactively rescaled by
`(100 + newModifier) / (100 + oldModifier)`. Player-wide building discounts contribute
to `economy:hurry_percent`. Each hurry increments `EconomyPlayerState.HurriedCount`,
multiplying per-turn inflation by `(100 + HurriedCount) / 100`. `HurriedCount` decays on
elapsed turn boundaries based on game speed and civic `HurryInflationModifier`.

`CorporationService.MaintenanceHundredths` calculates city corporate branch expenses:
base headquarter commerce (100 per gold, research, culture, and espionage), resource
consumption scaled by world size, population scaling `(population + 17) / 18`, handicap
scaling, player civic modifier (`corporations:maintenance_percent`, from the civic's
`CivicCorporationsDef`, selecting `RealisticMaintenancePercent` when realistic
corporations is active), inflation
pre-division `* 100 / (100 + inflation)` to balance subsequent economy inflation, and
realistic handicap scaling. `CityMaintenanceResult.CorporateHundredths` exposes the
uninflated base corporate cost, while `RecurringCosts` records
`economy:corporate_maintenance_hundredths` per city and `economy:corporate_maintenance`
in empire expenses.

Gold per turn that players exchange (the Diplomacy mod's `TradeItemKind.GoldPerTurn`
deal items, through `IPlayerExchanges`) integrates into `PlayerEconomy.Calculate`,
contributing to `Income` (`economy:deal_income`), `Expenses` (`economy:deal_expenses`),
and `NetGold`. In `PlayerEconomy.Settle` (`TurnPhase.Gold`),
sliders automatically step down flexible allocations to keep `Treasury + NetGold >= 0`.
Domestic income and expenses apply immediately, while domestic deficit is held in
`EconomyPlayerState.UnpaidGold`. At `TurnPhase.Map + 1`, `DiplomacyDeals` executes
transfers with net-payment participant affordability validation (`CanSettle`).

At `TurnPhase.Map + 2`, `PlayerEconomy.ReconcileGold` reconciles `UnpaidGold` against
actual treasury following deal payments. If treasury remains negative after deal
transfers, `EconomyPlayerState.StrikeTurns` increments; when `StrikeTurns > 1`,
`DisbandForDeficit` forces removal of up to `StrikeTurns / 2` lowest-value units. Unit
valuation in `SelectDisbandUnit` scores units based on random seed, cost, experience,
level, city defense, extra cost, territory ownership (own territory score is divided by
6, prioritizing domestic units unless outweighed by extra cost), and AI role multipliers
(settlers ×20, workers ×10, explorers ×15, city defense ×6, spies ×12). Units with
`GoldenAge = true`, free units (`Cost <= 0`), and the sole garrison unit providing
military happiness are exempt. Disbanding halts early once `NetGold` reaches
non-negative equilibrium.

`PlayerEconomy` implements `IPlayerEspionageSource.EspionageHundredths`. When a feature
answers `IEspionageSpending` (`GameRules.EspionageSpending`, the Espionage mod), the
economy's espionage phase leaves accumulation to it, preventing duplicate aggregate
pooling, and `PlayerEconomy.Calculate` asks it to project per-rival points under
`economy:espionage_target:<team>`.

Player modifier keys include `economy:max_rate:<type>`, `economy:upkeep_percent`,
`economy:inflation_percent`, and `economy:hurry_percent`. Team key
`research:cost_percent` changes research costs after the normal scaling steps.
`EconomyPlayerState` saves sliders, settlement snapshots, unpaid gold, strike turns,
hurried count, aggregate espionage (when nothing answers `IEspionageSpending`), and the inflation
accumulator. `EconomyCityState` saves hurry anger. The imported profile disables
inflation. Enabled profiles use the source binomial formula and momentum update after
research.

## Military services

Military registers `IMovementService`, `IPathfindingService`, `ICombatService`,
`IUnitService`, `IUnitSupportService`, and `IAirMissionService` through Core, and
interacts with World's `IVisibilityService`. Movement paths exclude the starting plot.
Commands traverse until movement expires or combat interrupts the path. A costly
destination can consume the remaining sub-moves. A\* minimizes total edge cost, not the
number of turns. Route movement and combat share the hex river-edge policy. Military
recomputes World visibility after movement and unit removal.

Command identities include:

- `military:move_unit` (`MoveUnitPathCommand`), `military:promote_unit`
  (`PromoteMilitaryUnitCommand`), `military:set_unit_activity`
  (`SetUnitActivityCommand`), `military:pillage` (`PillageCommand`),
  `military:upgrade_unit` (`UpgradeUnitCommand`), `military:disband_unit`
  (`DisbandUnitCommand`), and `military:raze_city` (`RazeCityCommand`).
- `military:bombard_city` (`BombardCityCommand`): Siege weapons expend movement to
  reduce defensive fortification bonuses (`DefenseDamage`) on an adjacent enemy city.
- `military:ranged_strike` (`RangedStrikeCommand`): Support fire up to `BombRange` hexes
  against visible enemy units or cities. Rolls against accuracy (`BombAccuracyPercent`),
  delivering damage to the top defender (capped at `CombatLimit`) plus collateral damage
  to stack companions, without reciprocal melee damage or risking the firing unit. Also
  applies city defense damage if targeting an enemy city.
- `military:load_unit` (`LoadUnitCommand`): Boards an eligible land unit onto a
  co-located transport ship possessing available `CargoCapacity` and matching cargo
  categories/domains.
- `military:unload_unit` (`UnloadUnitCommand`): Disembarks a cargo unit to the current
  tile (if valid) or an adjacent land tile, expending remaining movement.
- `military:air_mission` (`AirMissionCommand`): Executes domain-air operations governed
  by `AirMissionKind` (`Rebase`, `AirStrike`, `AirBomb`, `Recon`).
- `military:set_unit_activity`: Added `UnitActivity.Intercept` (Combat Air Patrol) and
  `UnitActivity.Blockade` (Naval Blockade). Naval combat units on non-lake water can
  blockade within `SHIP_BLOCKADE_RANGE` (default 3), severing enemy water trade networks
  and coastal city sea work. Core exposes `INavalBlockadeService`. Autonomous AI naval
  blockade planning is absent.

Domain events include:

- `PlotPillaged(int Unit, int Player, TileId Plot, string? Improvement, string? ReplacementImprovement, string? Route, int Gold)`:
  Published when an improvement or route is pillaged.
- `CityBombarded(int Unit, int City, int DefenseReduction, int TotalDefenseDamage)`:
  Published upon successful city defense bombardment.
- `AirMissionResolved(AirMissionReport Report)`: Details air mission results including
  interceptor involvement, interception damage, target damage, collateral hits, and
  suicide consumption.
- `GreatGeneralBorn(int Player, int Unit, string UnitType, int City)`: Dispatched when
  combat experience satisfies the general emergence threshold.

Saved extension components:

- `MilitaryUnitState`: `int? TransportId` (tracking vessel hosting this cargo unit),
  `string? AttachedLeaderUnitType` (attached warlord promotion identity), and
  `bool InterceptedThisTurn` (preventing multiple CAP interceptions in the same turn).
- `MilitaryPlayerState`: `long GeneralPoints` (accumulated combat XP),
  `int GreatGeneralsCreated` (lifetime count), `long GeneralThresholdModifier`
  (threshold inflation), and `bool Barbarian` (flag).
- `MilitaryGameState`: `ReconSights` (temporary turn reconnaissance visibility entries
  cleared on turn boundaries).
- `CityRuleState` (Cities): `int DefenseDamage` (accumulated wall/defense reduction) and
  `bool BombardedThisTurn` (suppressing natural defense healing).

Combat and system mechanics:

- **Combat resolution & odds preview**: Combat preview returns strengths, ledgers,
  firepower, round damage, first strikes, withdrawal, and success odds in millionths.
  The defender strength includes relative attack modifiers, including the reciprocal
  formula for negative totals. Combat is atomic. Saves between commands retain health,
  moves, experience fractions, activity, RNG, and current-turn combat reports.
- **Combat Great General emergence**: Military victories award combat experience to
  `playerState.GeneralPoints`. Reaching `GeneralThreshold(sim, player)` (scaled by game
  speed, era, and inflation modifiers) spawns a Great General in the player's best
  domestic city, increments `GreatGeneralsCreated`, and escalates both personal and team
  thresholds.
- **Warlord attachments & leader promotions**: Great Generals attaching via
  `LeadGreatPersonCommand` grant `military:leader`, record `AttachedLeaderUnitType`, and
  distribute bonus experience across the plot stack. Units with an attached leader
  qualify for exclusive `PromotionDef.Leader` promotions (e.g. Morale).
- **City defense damage & recovery**: City fortifications provide defensive percentage
  modifiers that are scaled down by accumulated `DefenseDamage` up to
  `MaximumCityDefenseDamage` (default 100). Cities that avoid enemy bombardment across a
  full turn cycle heal `CityDefenseDamageHealRate` defense damage during
  `TurnPhase.PlayerBegin`.
- **Cargo transports & amphibious warfare**: Transports expose `CargoCapacity` computed
  from unit definitions plus `cargo` promotion modifiers. Moving a transport
  simultaneously moves all carried units. If a transport is sunk or disbanded, all
  loaded cargo units are immediately lost. Land units aboard naval transports may
  assault adjacent hostile coastal land directly; amphibious attacks suffer
  `-AmphibAttackModifier` unless the attacking unit holds the `military:amphibious`
  promotion.
- **Stealth & invisibility detection**: Units with `Invisibility` (e.g. Submarines,
  Spies) remain invisible to enemy teams unless an enemy unit possessing a matching
  `VisibleInvisibilityTypes` category is stationed within detection range. Invisible
  units do not wake sentry units and do not obstruct peaceful unit movement through
  their tile.
- **Flanking strikes & defender withdrawal**: Mounted units with flanking capabilities
  inflict collateral damage on siege weapons defending in the same tile when the
  attacker wins or withdraws, provided combat occurs outside a city. Defending units
  with withdrawal capabilities can trigger `CombatOutcome.DefenderWithdrawn` when
  surviving lethal combat, retreating to an adjacent safe tile.
- **Air missions & interception**: Aircraft operate from friendly cities and carriers
  within `AirUnitCapacity` (default 8 per city). Missions include `Rebase` (up to
  `2 * AirRange`), `Recon` (revealing fog of war across the target area until next
  turn), `AirStrike` (bombarding units up to `AirCombatLimit` with collateral damage),
  and `AirBomb` (reducing city defenses by `BombardRate` or pillaging worked
  improvements). Defending interceptors on CAP or anti-air ground units roll
  interception chances; upon interception, 5 rounds of air-to-air combat occur,
  factoring in attacker evasion. Suicide aircraft are consumed after an uninterrupted
  attack. An intercepted aircraft survives an aborted mission unless interception damage
  kills it. Active city buildings scale direct and collateral air-strike damage through
  their combined `AirModifier`, before damage limits apply.
- **Barbarian spawns & naval piracy**: Land spawns run at `TurnPhase.Spawns`.
  `BarbarianSpawns.SpawnArea` also evaluates water areas, applying handicap setting
  `UnownedWaterTilesPerBarbarianUnit` against unowned coastal water plots. Spawning
  enforces bounds on the ratio of barbarian to civilized naval units, excludes
  freshwater lakes and tiles visible to players, and spawns adjacent to land. Barbarian
  naval raiders hunt visible fishing improvements and vulnerable merchant/transport
  shipping.

Training modifiers use `military:initial_experience` and `military:free_promotion:<id>`.
Promotion modifiers use Unit scope. Government and Peoples must avoid duplicate military
contributions when they supply full modifier sources.

## Government and Peoples services

`IGovernmentService` validates, previews (`CivicSwitchResult`) and adopts civic changes.
`SetCivicsCommand` calls it. Anarchy and cooldown timers live in
`GovernmentPlayerState`. `GovernmentStatus.IsAnarchy` and `IsDisorder` give other mods
one query. `IPeoplesService` exposes leader, civilization and trait data. Both mods
contribute through `IModifierSource`. Civic and trait keys use the owning consumer's
namespace: `cities:*`, `economy:*`, `world:*`, `military:*`, and `government:*`.
`RequirementContext.ProposedCivics` lets civic requirements see a pending cabinet
without mutating state.

A civic's plain modifiers are data. `CivicDef.Effects` lists `ModifierEffect` entries,
and Government's `CivicEffects` adds each one to every calculation of its scope for a
player who runs the civic, with the civic id as the ledger source. A civic therefore
gives any declared key, including one a mod defines and reads, without a schema or
Government change. The typed `CivicDef` fields that remain are rules that read more than
the calculation's subject: the capital, buildings, improvements, unit support, upkeep and
anarchy. The fields marked "Religion" leave with the Religion module when it becomes a
mod. Feature mods say what a civic means to them through attachments (Mod APIs). A mod's
new civic option is a `CivicOptionDef`; new players start on its `DefaultCivic`, or else
on its first civic without requirements.

Civics supply draft permissions and quotas under `cities:max_conscript` at Player scope,
with base quotas scaled by the active world size modifier (`getWorldSizeMaxConscript`)
and integer truncation. Leader traits (`TraitDef.ExtraCultureLevel`), civics, and
civilizations contribute additive modifiers under `cities:extra_culture_level`. Newly
founded cities inspect this modifier to expand initial plot culture and work territory
immediately upon founding.

## Religion

`src/Ariel.Rules/Religion` owns religion definitions, founding, city presence, state choice,
spread, and shrine construction. `IReligionService` exposes read-only presence, holy
cities, adoption choices, missionary definitions, odds, and conversion costs through
Core. AI and client code can use this service without a reference to the Religion
assembly.

Command identities are:

- `religion:set_state_religion`, `religion:spread_religion`, and
  `religion:build_shrine`.
- `religion:perform_inquisition` (`PerformInquisitionCommand`): Dispatches an inquisitor
  unit to purge non-state faiths from an owned or friendly city.

Domain events include:

- `InquisitionAttempt(int City, int Player, bool Success, IReadOnlyList<string> RemovedReligions, long CompensationGold)`:
  Published when an inquisition completes.
- `ReligionRemoved(int City, string Religion, string Reason)`: Published when a minority
  religion is purged or decays.

City presence, last state choice, conversion cooldown, holy cities, and founding turns
use saved extension components. Secular civics suppress the last choice
without deleting it. Conversion adds source-scaled anarchy to `GovernmentPlayerState`.
Natural spread runs immediately before the city post-production phase. All random draws
use the simulation RNG.

World supplies `ICityConnectionService` for physical routes, river trade, and unlocked
water trade. Distance uses hex distance and the hex map diameter. Religion age uses
elapsed turns.

Religion mechanics:

- **Founding**: Each religion has one world wonder, named after it, in the Religion mod's
  hand-written `data/founding-wonders.json`. The wonder requires the religion's founding
  technology. Completing it founds the religion: the completing city becomes the holy
  city, and its owner gets the religion's free missionaries. Discovering the technology
  founds nothing by itself. Once the religion is founded, no one can build its wonder,
  and barbarians never can. `BuildingDef.FoundsReligion` marks a founding wonder.
- **Foreign trade spread**: International trade routes connect cities across open
  borders and revealed road/water networks. Natural spread evaluates connected foreign
  trade partners, spreading state religions to foreign cities when allowed by standard
  spread rules or the `foundation:gameoption_multiple_religion_spread` option.
- **Religion decay & attrition**: When `foundation:gameoption_religion_decay` is
  enabled, cities with non-state minority religions lacking religious buildings roll for
  decay based on elapsed city turns and foreign pressure. Holy cities and the player's
  active state religion are immune. When decay triggers, minority religions and their
  associated buildings are purged, emitting `ReligionRemoved`.
- **Civic-gated Inquisitions**: Inquisitions are enabled when player civics grant
  `AllowInquisitions` (e.g., State Church) and blocked when any civic has
  `DisallowInquisitions` (e.g., Liberalism). Inquisitors validate against cities with
  non-state religions. Success purges minority religions and buildings
  (temples/monasteries), grants gold compensation (or reduces
  `RevolutionCityState.InstabilityIndex`), increases `HurryAngerTurns`, and leaves a
  `religion:inquisition` memory with the leaders who follow the purged faith or own its
  holy city.
- **Holy City relocation & extinction**: When an inquisition purges a holy city, if
  other cities in the world possess the religion, holy city status relocates to the
  highest-scoring candidate city; if no other city in the world has the faith, holy city
  status is extinguished (`-1` / `null`).

Religion contributes happiness, health, direct commerce, shrine commerce, state-building
commerce, production modifiers, and training experience. Religion requirements
distinguish city presence, state choice, and holy-city identity. Queries never consume
RNG or change state.

## Great People and Trade

`src/Mods/GreatPeople` owns city GP progress, historical class weights, births,
settlement, unit actions, Golden Age timers and Great General births. Its API
(`Ariel.Mods.GreatPeople.Api`) holds `IGreatPeopleService`, which supplies
read-only progress, rate ledgers, thresholds, settled specialists, timers, and available
actions, `ICityGreatPeopleService` (the city screen's view of the same calculation), the
commands, state and events. Cities no longer runs a birth system. The mod answers two
base seams: `ICombatLeaders` (combat adds general points to `MilitaryPlayerState`, and the
mod spends them on Great Generals at once, inside the fight) and `ICapitalWeight`
(settled great people weigh toward a new capital). Threshold increases include both the personal
increase and the team increase for the creator, as in the source. Births use one
simulation RNG draw, resolve civilization replacements, and publish `UnitCreated` and
`GreatPersonBorn`.

Commands include:

- `great_people:join_city`, `discover_tech`, `hurry_production`, `culture_bomb`, and
  `golden_age`.
- `great_people:lead_unit` (`LeadGreatPersonCommand`): Attaches a Great General to a
  friendly combat unit on the same tile, granting `military:leader`, setting
  `AttachedLeaderUnitType`, and distributing experience across the stack. Unlocks
  `PromotionDef.Leader` promotions (e.g. Morale).
- `great_people:construct_building` (`ConstructGreatPersonCommand`): Constructs special
  Great Person buildings (Academies by Scientists, Military Academies by Great
  Generals).
- `great_people:trade_mission` (`TradeGreatPersonCommand`): Great Merchants can traverse
  peaceful foreign borders without open borders agreements. Executing a trade mission in
  a foreign city awards instant gold calculated from distance and capital trade route
  profit.

Great People mechanics:

- **Combat Great General emergence**: Combat experience won in battle contributes to
  `playerState.GeneralPoints`. Reaching `GeneralThreshold(sim, player)` (scaled by game
  speed, era, and threshold modifiers) spawns a Great General in the player's best
  domestic city. Spawning increments `playerState.GreatGeneralsCreated` and increases
  both personal and team thresholds (`GREAT_GENERALS_THRESHOLD_INCREASE` and
  `GREAT_GENERALS_THRESHOLD_INCREASE_TEAM`), publishing `GreatGeneralBorn`.
- **Warlord attachments**: Attaching a Great General to a unit distributes bonus
  experience evenly across all eligible friendly combat units on the plot and enables
  elite leadership promotions.
- **Special building construction**: Allows Great Scientists to build Academies
  (`cities:academy`) and Great Generals to build Military Academies
  (`cities:military_academy`), verifying technology prerequisites and existing building
  instances.
- **Foreign trade missions**: Great Merchants can enter peaceful foreign territory
  without open borders treaties. Reaching a foreign city enables `"trade"`, granting
  instant treasury gold calculated from base trade values, speed settings, and capital
  trade route distance profit. Settlement contributes free-specialist keys, so Cities
  applies normal specialist yields, commerce, and modifiers. Discovery selects the
  highest eligible flavor score, with ordinal technology ties.
  `IResearchService.AddProgress` grants unmodified, capped research. Hurry adds capped
  production to the current eligible building. Cities completes it during the normal
  completion phase. Culture works use `ICityCultureService` to update city history,
  surrounding plots, and ownership immediately. Golden Ages consume distinct unit
  classes. Optional command `Units` lists all participants, including the initiating
  unit. An omitted list selects nearest eligible units, then unit ids. Wonder completion
  also starts a Golden Age without increasing unit requirements. Golden Ages add
  threshold-based plot production and commerce through `IPlotYieldFinalizer`, and
  additive GP rate modifiers. Wonder-triggered timers retain their full duration until
  the next player turn. Unit-triggered timers include the current turn.

`src/Mods/Trade` derives active partners through `ICityConnectionService` and exposes
`ITradeRouteService` in its API (`Ariel.Mods.Trade.Api`); the service answers the base
seam `ICityTradeLinks`, along which religion spreads. Capacity includes initial, technology, civic, local-building,
coastal-building, global-building, and external modifier contributions. Routes use
partner population, wrapped hex distance, world-size scaling, capital connection,
overseas, building, civic, and foreign modifiers. Profit and route yields retain
hundredths. City aggregation truncates once per yield before the ordinary city modifiers
and Economy sliders. Routes select highest profit, then same-team partners, then city
ids. Each city uses a partner at most once. Physical resource networks and city
connections transit revealed peaceful foreign territory without Open Borders treaties
(`CvPlot::isTradeNetwork`). International city trade routes still require Open Borders
agreements when Diplomacy is active. Player naval blockades by enemy warships within
`SHIP_BLOCKADE_RANGE` (default 3) on non-lake water sever water trade networks.
Blockades also prevent coastal cities from working water tiles and cancel trade routes.
Autonomous AI naval blockade planning remains absent. Connectedness economics and
forced-trade diplomacy remain deferred. The imported profile sets `INITIAL_TRADE_ROUTES`
and `MAX_TRADE_ROUTES` to zero. Trade remains disabled unless a profile overrides these
values. External keys are `trade:slots`, `trade:profit_percent`,
`trade:foreign_profit_percent`, and `trade:yield_percent:<yield>`. GP accepts both the
existing `cities:great_people_percent` and `cities:great_people_rate_percent` in city
and player scopes. Mod versions change the content identity. Older city GP state needs a
migration before it can continue under the new owner.

## Modular feature services

Modular feature services include Diplomacy, ProjectsVictory, Corporations, Espionage,
Events, ScriptedWonders, and Revolution. Each mod registers its contracts, commands,
and saved extension state, and publishes its contracts and content definitions in its
own API assembly (`src/Mods/<Mod>/Api`). `Ariel.Rules/Contracts` holds only the base
game's contracts and the seams mods answer.

Diplomacy elaborates the base game's relations (see Relations): attitudes, agreements
(open borders, defensive pacts) and bilateral deals. Its API (`Ariel.Mods.Diplomacy.Api`)
holds `IDiplomacyService`, the deal and agreement types and their commands, which
validate treaty changes, deal proposals, execution and cancellation. Deal assets include
resources, gold, gold per turn, and technology. Team ids identify agreements; player ids
identify deal participants. The service also answers the base game's seams: agreements
(`ITeamAgreements`: open borders lets units pass, a defensive pact calls the partner into
a war against it and is never attacked by the AI, and an offensive declaration dissolves
the aggressor's pacts), dispositions (`IDispositions`: the attitude tier) and exchanges
(`IPlayerExchanges`: deal resources and gold per turn). A war ends the two teams'
agreements and deals (`WarDeclared`). Attitude adds personality, religion, favourite
civic, borders, war and peace, war history, agreements, trade and civics, and every
remembered count weighed by the leader's `MemoryAttitudePercents` or else the memory
kind's `AttitudePercent`. Its own memory kinds are a technology received in a deal, a
gift (`give_help`, limit 3) and a stopped active trade. Without the mod there are no
attitudes, agreements or deals, and war, peace, contact and memory work as the base game
defines them.

ProjectsVictory owns project eligibility and completion, team project counts, scores,
and saved victory status, and the project and victory definitions (`ProjectDef`,
`VictoryDef`) in its API. It answers the base seam `IPlayerScores` with a player's total. Project definitions carry costs, technology and project
prerequisites, instance limits, victory links, spaceship flags, and thresholds. Victory
definitions select conquest, domination, cultural, project, or time rules. Enabled
conditions run by priority. Projects enter the city production queue through a
registered production kind and completion handler.

Corporations use founding and spread commands. A Great Person founds a corporation in a
city with a connected prerequisite resource. Active branches apply corporation yield,
commerce, headquarters commerce, and resource-based maintenance. Civics can prohibit
local or foreign corporations. A branch requires an active branch owned by the spreading
player.

Espionage allocates whole points from saved commerce output across met rival teams by
player-set weights. An unset weight is zero, and unspent points stay held and visible.
Passive thresholds unlock demographics, city visibility, research inspection, and city
investigation. The service answers the base seam `IEspionageSpending`, so the economy
leaves banking to it. Spy missions spend points and use a calculated success chance; running a
mission lowers the attacker's own investment, so later missions cost more. Counter-
espionage raises both cost and risk for a fixed number of turns. All numbers come from
`data/espionage-rules.json`. Saved team, player, and city state tracks points, weights,
remainders, and timers.

Events roll eligible triggers per turn with the simulation RNG. Trigger definitions set
eligibility, chance, repeat policy, and cooldown. Pending events hold a player choice.
Options can change treasury, research, city stores, happiness, health, and plot yields.
Unresolved events use a default option. AI players resolve events automatically.

ScriptedWonders adds completion, periodic, combat, and modifier effects to selected
buildings, and answers two base seams: empire free buildings (`IFreeBuildingGrants`) and
wonders that keep barbarians out (`IBarbarianBarrier`). City-local timers survive saves and capture. Inactive wonders pause their
timers, while completed effects do not repeat. Revolution tracks city instability from
wellbeing, distance, religion, civics, anarchy, garrisons, Golden Ages, and natural
recovery. Thresholds can trigger mutiny or rebellion. Mutiny adds unhappiness; Warning
is a display-only alert. Players can suppress instability with gold, priced by the
reduction, or with a military garrison. A city with no legal rebel tile does not rebel.
A spy-incited revolt adds instability through the Revolution service. The service answers
the base seam `ICityUnrest` with the instability index, so inquisition compensation calms
a city, and the AI's religion and great people planners read it.

## Client presentation

`Ariel.Client.Core` supplies screen models and validated commands for diplomacy (base war
and peace, and the Diplomacy mod's attitudes, agreements and deals), score and victory,
corporations, espionage, and pending events. The city screen shows
corporations, religion, commerce, wellbeing, and ledgers. Unit actions expose
corporation and spy commands when valid. `Ariel.Godot` presents diplomacy and victory,
corporation, espionage, event, religion, pedia, save, and load panels. It also shows
espionage rates, pending-event notifications, and corporation details on the city
screen.

## Computer players

`src/Ariel.Ai` is part of the base game: the module `ai` registers `ai:standard` under
`IPlayerAI`, its saved intent (`ai:team_state`, `ai:player_state`) and its commands. It
cannot be disabled. Select it through `SimulationSettings.PlayerAis`. A mod can still
register a different computer player under `IPlayerAI`.

### Planners and the planner seam

A turn of the standard AI is a list of planners. Each one is an `IAiPlanner`
(`Id`, `Order`, `Plan(AiContext)`) registered as a service:
`context.RegisterService<IAiPlanner>("planner", new MyPlanner())`. `StandardAi.Decide`
runs every registered planner in ascending `Order`, then ordinal `Id`; the sorted list is
computed once per content set (`AiCatalog.Planners`) and duplicate ids fail. The AI's own
planners register the same way, at the slots `AiPlannerOrder` publishes, a hundred apart:

| Order | Planner | Order | Planner |
| ---: | --- | ---: | --- |
| 100 | `ai:research` | 1000 | `ai:cities` (renews cached views first) |
| 200 | `ai:war` | 1100 | `ai:workers` |
| 300 | `ai:civics` | 1200 | `ai:air` |
| 400 | (`diplomacy:planner`) | 1300 | `ai:military` |
| 500 | `ai:settlers` | 1400 | (`espionage:planner`) |
| 600 | `ai:citizens` (renews cached views first) | 1500 | (`revolution:planner`) |
| 700 | `ai:religion` | 1600 | `ai:economy` |
| | | 1700 | `ai:hurry` |

Feature mods fill the gaps, and three mods fill slots the AI keeps for them: the Diplomacy
mod the 400 slot (deals and agreements) with `diplomacy:planner`, the Espionage mod the
1400 slot (spending weights, spy moves and missions) with `espionage:planner`, and the
Revolution mod the 1500 slot (suppressing unrest with a garrison or gold) with
`revolution:planner`. The Corporations mod runs `corporations:planner` at
`Religion + 100` (found and spread corporations) and the GreatPeople mod runs
`great_people:planner` at `Religion + 200`, so a great person that can found a
corporation does that before the great people planner spends it, and both act before
cities choose production. That ordering lives in those two numbers; a mod that must run
before or after another contributed planner says so in its own value.

How a mod adds AI behaviour:

1. Reference `Ariel.Ai` (with `Private="false"`, like `Ariel.Rules`).
2. Implement `IAiPlanner` with an id in the mod's namespace and an `Order` from
   `AiPlannerOrder`. One instance serves every player of every game, and players' AI
   turns can run at the same time on forked simulations, so the planner keeps no mutable
   fields: `Plan` builds a per-turn object and works there.
3. In `Plan`, read the game through `AiContext` and act with `ctx.Issue(command)`, which
   validates and applies through the normal command path and renews the context's cached
   views, so later planners see the effect. Take only units nobody claimed
   (`ctx.HasActed`) and claim the ones used (`ctx.MarkActed`). Decide from simulation
   state and `sim.Rng` only, and break ties by ordinal id.
4. Register it in the mod's `Register`. Without the mod the planner is simply absent.

`AiContext` is public on purpose and small: the player and its team
(`Sim`, `PlayerId`, `Self`, `Team`, `Turn`, `Grid`), cached `Cities` and `Units`,
`Def`, `Alive`, `Tile`, `Service<T>`, `Issue`, `MoveToward`, `Refresh` (after a mutation
outside `Issue`), the acted-unit ledger and `GoldReserve` (the treasury the economy
planner keeps in hand). `PlannerPoints` gives the points per yield and commerce the city
planner ranks content in, so a contributed planner values things on the same scale.
Everything else (roles, threat maps, city views, campaign targets, the shared site, army
and production plans) stays internal to the built-in planners.

Planners still choose what a feature's actions are worth themselves. War and peace are the AI's own (`ai:war`): the war planner issues the
base commands and weighs targets by `Relations.DispositionToward` and `Allied`, so it
names no Diplomacy type.

### Production wishes

A planner acts with units it already has; what cities build is the city planner's choice.
A mod that needs its own units or buildings tells the city planner through a second seam,
`IProductionWishes` (`Id`, `Wishes(AiContext, City)`), registered like a planner:
`context.RegisterService<IProductionWishes>("wishes", new MyWishes())`. Each
`ProductionWish(Kind, ContentId, Score)` names a production kind, what to produce and its
rank. The city planner asks every source, by ordinal id (duplicate ids fail), each time it
scores a city's options, so a source sees what earlier choices this turn already queued.
It keeps a wish only when the player could plausibly build it (the technology,
civilization and obsolescence prefilters of its own options) and the city can produce it,
then ranks it with its own scored options, highest score first, ties by ordinal id.
Emergency defenders, settlers, workers and explorers still come first, and processes
last.

The score is on the city planner's ranking scale, not raw `PlannerPoints`: a building
scores its value in planner points times 1000 over its cost plus 60; an attacker for a
short army 80 to about 300; naval, siege and air units in a war 170 to 390; an inquisitor
500 plus 1000 over its cost. The Espionage mod's `espionage:spies` wishes for spies at 150
plus 1000 over their cost, one per six cities once a rival is known. A wish source keeps
no mutable fields, never issues a command and never draws from the RNG.

Wishes are the first of the AI valuers: the seams through which a mod adjusts what the
AI values. Valuers for plots, buildings, units, technologies, civics and trades will sit
beside it in `Ariel.Ai`, each a registered service asked by the planner that owns that
choice.

Until the civic valuer exists, `CivicPlanner` scores a civic from its flavors and the
open-effect keys it knows (`commerce.percent.*`, `yield.percent.*`, `cities:happiness`,
`cities:health`, `cities:free_specialists`, growth, worker speed, and the war keys).
A mod's civic is weighed and can be adopted, but a key only a mod reads adds nothing
to its score, and attached mod data is not read.

### What the built-in planners do

Specialized planners handle empire governance and military campaigns:

- **Civilian & empire planners**: `CitizenPlanner`, `ProductionPlanner`,
  `WorkerPlanner`, `ResearchPlanner`, `CivicsPlanner`, `CommercePlanner`,
  `SettlerPlanner`, and `ExplorerPlanner`.
- **Multi-theater combined arms campaigns (`AttackPlanner`, `MilitaryPlanner`)**:
  Partitions free combat units by landmass (`AreaId`), forming separate coordinated task
  forces per theater. Staging rings form 2 hexes outside target enemy cities. Before
  melee units attack, the AI sequences support fire: siege weapons execute
  `BombardCityCommand` against city defenses, and ranged units execute
  `RangedStrikeCommand` to soften stack defenders. Defensive reinforcement lines score
  candidate defenders by threat differential, moving surplus garrisons to border cities
  along road corridors.
- **Naval warfare & amphibious sealift (`NavalPlanner`)**: Classifies naval vessels into
  `NavalExplorer`, `NavalCombat`, and `NavalTransport`. Transports coordinate amphibious
  invasions: gathering at friendly coastal embarkation cities, loading waiting
  melee/siege troops up to `CargoCapacity`, sailing to enemy beachheads on foreign
  landmasses, and disembarking or attacking from sea. Warships escort transports, patrol
  coastal waters, and bombard coastal cities.
- **Air warfare management (`AirPlanner`)**: Dynamically rebases bombers and fighters
  forward to theater cities within range of target objectives. Flies aerial
  reconnaissance (`AirRecon`) to expose enemy stacks and fogged frontier plots. Conducts
  `AirBomb` missions against city defenses and strategic tile improvements, executes
  `AirStrike` missions against clustered enemy defenders, and maintains Combat Air
  Patrol (`UnitActivity.Intercept`) to intercept incoming enemy airstrikes.
- **Religion AI (`ReligionPlanner`)**: Dispatches missionaries to unconverted domestic
  cities, and trains inquisitors to purge minority religions from unstable cities under
  state church civics.
- **Great people (`GreatPersonPlanner`, contributed by the GreatPeople mod)**:
  Dynamically scores Great Person actions: evaluates empire-wide value of Golden Ages
  (scaled by population and revolution instability) versus immediate technology
  bulbing, special building construction, or trade missions.
- **Corporations (`CorporationPlanner`, contributed by the Corporations mod)**: Founds
  corporations with the fitting great person and spreads founded ones to the empire's
  own cities while income allows.
- **Espionage (`EspionagePlanner`, contributed by the Espionage mod)**: Points the
  espionage weights at an enemy, else the nearest known rival, walks spies to its
  cities and runs the best affordable mission; `SpyWishes` asks cities for spies.
- **Stability (`StabilityPlanner`, contributed by the Revolution mod)**: Every third
  turn suppresses unrest at the warning tier or worse, with a free garrison when one
  qualifies, otherwise with gold.

Decisions use integer scores and ordinal content-id ties. War checks use only
`Simulation.Rng`. Planners apply ordinary commands through `Simulation.TryApply` during
`Decide`, then return an empty command sequence. This explicit sequential policy lets
later decisions see founding, movement and production changes in the same turn. Calling
`Decide` therefore takes the AI turn; it is not a read-only preview.

The AI has no hidden per-game caches. Immutable content indexes can be shared between
games. The war planner applies the base game's `relations:declare_war` and
`relations:make_peace` commands and reads war-start turns from the saved relations
component, so it follows the same validation as any other caller.

World exposes `IWorkerBuildService.Preview` for remote build legality, resulting yields
and required work. Plot modifier queries accept a proposed plot, so civic and trait
improvement effects enter the preview without state mutation. Military path queries
reuse modifier ledgers within one search. Their integer heuristic accounts for
fractional routes. Neither query cache survives a command or enters saves.

### Parallel candidate scoring and intra-planner concurrency

Candidate evaluation within single-player planners parallelizes read-only scoring using
`ParallelScoring.Select` across candidate thresholds:

- `SitePlanner`: candidate city founding plots and full-map tile valuations.
- `CityPlanner`: city view snapshots and building production candidate options.
- `ResearchPlanner`: candidate research technologies.
- `WorkerPlanner`: candidate tile improvement jobs for each worker.
- `MilitaryPlanner`: pathfinding for defensive reinforcements.
- `AttackPlanner`: reachability of candidate assault and staging hexes.

Evaluation callbacks must not issue commands, mutate simulation state, or draw from
`Simulation.Rng`. Results populate preallocated arrays matching input indices,
preserving identical candidate evaluation results regardless of thread scheduling.
Candidates are sorted deterministically using descending integer scores with
ordinal/entity ID tie-breaking (e.g. `StringComparer.Ordinal` on content IDs, or
ascending `Tile.Value` / `Unit.Id`).

To prevent thread-pool oversubscription when multiple players plan concurrently,
`ParallelScoring.Select` checks `Simulation.IsParallelAiTurn`. When true (during
multi-player speculative waves), intra-planner candidate loops execute serially.
Candidate parallelism is reserved for single-player turns.

### Read-only city query optimizations

City and worker planning perform intensive read-only queries against empire state.
Several query paths avoid redundant traversal:

- `CityReadScope`: a `[ThreadStatic]` per-thread memoization scope keyed by simulation
  instance caches building activity, power status, and modifier grants during read-only
  city calculations (such as citizen specialist and tile assignments in
  `CitizenPlanner`). It provides zero-overhead memoization without mutating game state
  or leaking across threads.
- Lazy `ResourceNetwork.Modes`: route and transport mode checks are resolved lazily per
  query on first access rather than traversed upfront.
- `INavalBlockadeService.BlockadedTiles`: computes and caches the complete set of
  blockaded water tiles once per query instead of repeating hex-distance checks per
  candidate plot.

### State cloning and simulation forking

To support concurrent turn planning without full-save canonical JSON serialization
overhead:

- `GameState.Clone()`: performs an in-memory deep copy of the simulation state.
  `Plot.Clone()` copies plot attributes, culture arrays, team visibility masks, and
  shallow-copies immutable `RevealedPlotSnapshot` records while sharing the immutable
  `HexGrid`. Player, unit, team, and city records are cloned directly into pre-allocated
  collections and reindexed.
- `ExtensionState.Clone()`: clones attached extension component instances using UTF-8
  JSON round-trips (`JsonSerializer.SerializeToUtf8Bytes` and
  `JsonSerializer.Deserialize`) with `CanonicalJson.Options`, copying only entities that
  define non-empty extension dictionaries.
- `Simulation.ForkSpeculative()`: creates an isolated speculative `Simulation` with a
  cloned `GameState`, restored `DeterministicRng` matching the source RNG state, and
  top-level command recording enabled.

### Wave-based speculative parallel turns (`AiTurnSpeculator`)

`Simulation.RunAi()` delegates turn execution to `AiTurnSpeculator.Run(this)`:

- **Speculative waves**: uncommitted AI players are partitioned into waves. Players on
  the same team or at war with earlier players in the wave are excluded from the same
  wave to avoid direct interactions. Within each wave, the first remaining player
  ($p_0$) executes directly against the authoritative `Simulation`, while following
  independent players ($p_1, \\dots, p\_{m-1}$) plan concurrently on isolated
  speculative forks of the wave-start state.
- **Deterministic commit order**: plans commit strictly in ascending `Player.Id` order.
  Each committed plan replays its recorded `GameCommand` sequence against the
  authoritative simulation via `TryApply`, verifying that pre-command and post-command
  RNG states match the fork execution and that per-player state digests remain
  identical.
- **Conflict detection**:
  - *Active mutation footprint proximity*: a cumulative active mutation footprint tracks
    tiles where earlier players moved units (including intermediate path steps),
    founded, captured, or razed cities, or modified plot ownership, improvements,
    routes, or features. Stationary units (such as fortified garrisons) and unchanged
    border plots are excluded from the footprint unless their unit state (health,
    experience, promotions, activity) mutates. A spatial conflict occurs if any tile in
    a candidate plan's active footprint is within 5 hexes (`ActiveFootprintRadius = 5`)
    of an earlier player's cumulative active footprint.
  - *Global entity-aggregate conflicts*: a conflict occurs if an earlier player altered
    entity counters (`NextCityId`, `NextUnitId`), entity membership or ownership, unit
    health/experience/promotions/type, city buildings/population/extensions, or power
    inputs. This prevents stale reads for global evaluations (such as `WarPlanner`
    military power calculations or world wonder constraints).
  - *RNG and diplomacy conflicts*: a conflict occurs if an earlier player drew from
    `Simulation.Rng` and the candidate plan also used RNG, or if team relations
    (`Team.Wars`) or global extension components changed.
- **Wave relaunch on conflict**: if a speculative plan encounters any spatial,
  aggregate, RNG, or diplomatic conflict, or if command replay diverges, the
  authoritative simulation restores from a pre-commit checkpoint, re-applies already
  verified plans, and immediately relaunches a new speculative wave for the remaining
  uncommitted players against the updated authoritative state.

### Serial work reduction and calculation reuse

To minimize serial latency during AI player turns and the surrounding simulation, work
is reduced across eight optimization areas:

- **Calculation reuse within planning segments (`AiContext.Segment`)**: Planners
  evaluate decisions in a fixed sequence within an unchanged planning segment. A context
  segment caches role counts, queued production counts, city view snapshots, production
  eligibility dictionaries, nearest-city queries, and campaign targets geography.
  Repeated queries within the segment reuse memoized results instead of repeating linear
  entity scans or service queries.
- **Path query reuse and local search structures (`AiContext.FindPath`)**: Exact
  pathfinding queries (keyed by unit ID, destination tile, and attack mode) are cached
  within the segment. Garrison selection carries its reachability path directly into the
  resulting movement command, eliminating duplicate searches. Within A\*
  (`PathfindingService`), costs and parent pointers are consolidated into a single local
  table (`Dictionary<TileId, (int Cost, TileId Parent)>`), removing a second dictionary
  and its growth allocations per search.
- **Candidate pruning and allocation reduction**: Cheap legality and budget checks
  precede expensive evaluations. In `CityPlanner`, unaffordable wonder candidates are
  rejected before evaluating production eligibility or building values. In
  `WorkerPlanner`, tile candidates skip improvement previews if the tile already has an
  identical improvement, depleted mine, obsolete build, or equal/better route. In
  `AiOptions.Best`, the top legal choice is selected directly via score comparisons and
  ordinal content-ID ties without allocating a sorted ranking buffer. Garrison selection
  sorts city priority once for both defensive passes and materializes only requested
  defenders before commands.
- **Non-AI turn optimization: call-local visibility recomputation
  (`VisibilityService.Recompute`)**: During full visibility recalculation, directional
  terrain levels (`SeeFrom` and `SeeThrough`) are resolved at most once per queried
  tile, and team water-sight capability is resolved once per queried team. Two
  plot-sized lazy arrays and a small team dictionary memoize these queries exclusively
  for the duration of a single `Recompute` call.
- **Spatial threat indexing and settlement geography**: In `AiContext`, repeated
  whole-map enemy scans are replaced by a spatial threat grid (`ThreatStrength`). Enemy
  combat units populate tile-strength buckets once per segment that queries threats, and
  threat queries sum values within a radius over the grid. In
  `SitePlanner.BuildGeography`, city exclusion masks and a nearest-owned-city distance
  field are precomputed, replacing repeated whole-city distance scans during site
  eligibility scoring.
- **Strategic goal retention**: Unchanged civic and primary land campaign evaluations
  are retained rather than re-scored every turn.
- **Continuing civilian missions**: Valid settler and worker destinations are retained
  across turns, bypassing destination enumeration, scoring, and candidate ranking.
- **Sorting and allocation reduction**: Nearest-city queries use distance/ID tuple
  minima, eliminating ordered-iterator allocations.

### Cache lifetimes and invalidation boundaries

All caching mechanisms adhere to strict lifetime and isolation boundaries:

- **Segment-local caches (`AiContext.Segment`)**: Segment caches are strictly local to a
  single planning pass on a specific simulation instance. Caches are invalidated and
  replaced immediately upon any successful command execution via `AiContext.Issue`, as
  well as on explicit `Refresh`. Synchronous command callbacks (movement, visibility,
  promotions, cargo transport, diplomacy, city capture, and tile ownership) finish
  before the next command or query.
- **Bounded path cache**: To prevent unbounded memory growth, `AiContext.FindPath` caps
  cached routes at $\\min(256, \\max(1, \\text{units}) \\times 8)$ per unchanged
  segment. Subsequent queries fall back deterministically to uncached A\* search.
- **Call-local visibility memoization**: The lazy level arrays and team capability
  dictionary in `VisibilityService.Recompute` belong entirely to that single invocation.
  They are discarded before `Recompute` returns and are never stored on the service, in
  state, or across threads.
- **Mid-turn entity refresh and stale-cache bug fixes**:
  - *Zero-health enemy filtering*: `AiContext.Enemies` explicitly filters
    `u.Health > 0`. Because `CombatService.Collateral` can leave zero-health units in
    `State.Units`, this repairs stale enemy state and prevents dead units from exerting
    threat or attracting attacks.
  - *Per-command entity refresh*: Discarding and replacing the segment on successful
    `Issue` updates `Cities` and `Units` mid-turn. Planners observe live state following
    captures, civilian captures, and city foundings rather than stale turn-start
    snapshots.

### Persistent strategic goals and civilian missions

Rather than remaining volatile caches, strategic intent and civilian travel missions are
modelled as authoritative game state:

- **Authoritative state component (`AiPlayerState`)**: Registered under
  `ai:player_state`, this component stores active civic goals, primary land campaign
  city targets (or explicit absence) per requested land area, and travelling civilian
  mission records. It stores no entity references, service results, scores, or path
  routes. It is cloned, serialized to JSON, canonically hashed, and restored via
  standard Core extension state contracts. Older saves without the component start with
  empty intent.
- **Speculative wave safety and command replay**: Intent changes are executed through
  replayable `AiPlanningCommand` records (`ReviewCivicGoalCommand`,
  `ReviewCampaignGoalsCommand`, `PrepareWorkerMissionsCommand`,
  `PrepareSettlerMissionsCommand`). During validation—both on speculative forks and
  during authoritative wave commit—the command recomputes detached decision data from
  current state dependencies and verifies that the canonical `ExpectedIntent` digest
  matches the command's recorded digest. If authoritative dependencies changed (e.g.
  from an earlier player's actions in the wave), validation fails with
  `AI intent changed`, preventing any state write and triggering a speculative wave
  relaunch.
- **Periodic strategic review and immediate invalidation**: Civic and primary land
  campaign goals have a maximum lifetime of 3 turns, staggered by player ID
  (`turn + 3 - (turn + playerId) % 3`) to distribute re-evaluation across turns without
  consuming RNG. Immediate invalidation occurs upon relevant state transitions:
  - War declaration or peace treaty.
  - City loss, acquisition, capture, or destruction.
  - Destruction or acquisition of a targeted campaign city.
  - Technology acquisition.
  - Economic emergencies: treasury falling below reserve, unpaid unit gold, economic
    strike, or negative net income.
  - For civics, anarchy and revolution cooldown are captured as Boolean thresholds
    (`> 0`) in the dependency digest, preventing countdown turns from prematurely
    invalidating stable goals.
- **Civilian mission persistence and live safety checks**:
  - Settler and worker missions store unit ID, unit type, mission kind, target tile,
    optional build ID, creation turn, and a fixed 12-turn expiry. Valid continuation
    does not extend the 12-turn deadline.
  - Both origin and destination tiles are checked for hostile threat (radius 3 for
    settlers, 2 for workers). Any detected threat immediately overrides the mission and
    triggers a retreat step toward a friendly city. Settler destinations must be safe,
    replacing the former wait-for-escort policy.
  - Workers recheck build permissions, technology, obsolescence, budget, domain, and
    reachability each turn. Settlers recheck spacing, founding terrain, ownership,
    domain, and reachability. Paths are recomputed through live movement validation;
    routes are never persisted.
  - Multi-batch worker fallback recovery: `WorkerPlanner.ChooseJob` evaluates candidate
    tiles in ordered batches of 8. If all candidates in the top batch are path-blocked,
    subsequent batches are evaluated until reachable work is found or candidates are
    exhausted, preventing workers from idling or retreating when reachable jobs exist.
  - Mission cleanup immediately clears records for dead, captured, zero-health,
    transported, or role-changed units, as well as completed builds or city foundings.

### Policy changes, transparent optimizations, and bug fixes

To maintain clear behavioral accounting, changes are separated into three distinct
categories:

- **Transparent optimizations**: Calculation reuse in `AiContext.Segment`, exact path
  query caching, local A\* table consolidation, early wonder-budget pruning, worker
  build checks before preview, `AiOptions.Best` selection, once-per-recompute visibility
  level memoization, spatial threat bucketing, and settlement geography masks. These
  preserve exact score arithmetic, candidate rankings, ties, RNG consumption, and state
  hashes.
- **Stale-cache bug fixes**: Filtering zero-health units in `AiContext.Enemies` and
  refreshing `Cities`/`Units` after successful commands. These correct stale-state
  anomalies in collateral damage, capture, and founding turns.
- **Deliberate policy changes**: Strategic inertia (up to 3-turn retention of civic and
  land campaign goals), civilian mission persistence (12-turn fixed missions bypassing
  destination search), safe settler destination requirements (retreating instead of
  waiting for escort), and multi-batch worker candidate search (evaluating batches of 8
  to find reachable jobs when the top 8 are blocked).

### Search compatibility, unmeasured gains, and architecture limits

- **Deferred shared searches and unsuccessful candidates**: Broader path sharing (such
  as reverse Dijkstra distance fields or multi-unit shared routes) was evaluated and
  deferred. Movement costs in the simulation are directional (elevation slope, river
  crossings, bridge technology), depend on unit-specific capabilities (domain,
  double-move promotions, impassable terrain filters, transport capacity), and are
  restricted by dynamic diplomatic borders (open borders, wars, zone of control).
  Furthermore, sharing searches across units or multiple goals alters goal-directed A\*
  tie-breaking and equal-cost parent selection. Priority queue and buffer pooling were
  also deferred pending demonstrable allocation benefit.
- **Unmeasured runtime gains**: All performance gains are documented as removed
  redundant work (scans, sorts, queries, allocations, and re-evaluations). Elapsed
  execution time, memory allocations, and speedup ratios remain unmeasured; no
  percentage speedup is claimed.
- **Operational trade-offs and limits**:
  - *Threat grid allocation*: In `AiContext.ThreatStrength`, allocating an
    `int[Grid.Count]` array per segment that queries threats introduces allocation and
    initialization overhead on large maps with many commands, representing an unmeasured
    trade-off against eliminated linear scans.
  - *Worker fallback search*: Evaluating candidate improvement tiles in batches of 8
    until reachable work is found, combined with full mission re-evaluation upon
    invalidation (`PrepareWorkerMissionsCommand`), adds search work on turns where top
    candidate tiles are blocked.
  - *Speculative replay validation*: Recomputing detached decision data and verifying
    `ExpectedIntent` digests during command validation adds CPU verification overhead
    during speculative wave commits.
  - *Explorer requirement index*: `CityPlanner.NeedsExplorer` indexes `ctx.Cities[0]`;
    safe because production planning runs after puppet annexation, but any future
    command that removes all cities inside the city loop must guard against empty
    `Cities`.
  - *Settler destination spacing*: `SitePlanner.LegalDestination` checks distance
    against cities directly rather than sharing the `BuildGeography` exclusion mask.
  - *Campaign goal scope*: `ReviewCampaignGoalsCommand` persists goals only for land
    area IDs requested by active free-soldier theaters in `AttackPlanner`; areas without
    free soldiers drop saved goals.

## Relations

Contact, war and peace between teams, and what players remember of each other, are the
base game's: the Relations module (`relations`, `src/Ariel.Rules/Relations`) owns them,
because every other module reads them and none of them is their natural owner. Its
contracts are in `Contracts/RelationContracts.cs`; `GameRules.Relations` is
`IRelationsService`.

- Contact. `HasMet`, `EstablishContact` and `DiscoverContacts` (current sight, not
  revealed history: a team meets the teams whose units or borders it sees or touches).
  Barbarians meet no one. Contact is discovered when a unit is created, a city is founded,
  a turn starts, sight is renewed (`MilitaryData.Sight`), and by the `relations:turn`
  system at `TurnPhase.Visibility + 1`, after team sight and before the end-of-turn hooks,
  so a mod's own end-of-turn bookkeeping already sees this turn's contacts. First contact
  creates the pair's `TeamRelation` in `RelationsState` and publishes `TeamsMet`.
- War and peace. `DeclareWarCommand` (`relations:declare_war`) and `MakePeaceCommand`
  (`relations:make_peace`) are the only way a war starts or ends; the client and the AI
  both issue them, and nothing else calls `GameState.SetWar` during play. A war needs
  contact and no peace treaty younger than `RelationRules.PeaceCooldownTurns` (10);
  peace needs a war at least `MinimumWarTurns` (12) old. `TeamRelation` keeps the contact
  turn, the start of the current war or peace, the last treaty and each side's offensive
  wars. A declaration reads the defender's friends first, then asks the agreements seam
  which teams it calls to arms (`ITeamAgreements.CallToArms`, which also settles the
  agreements it breaks), starts the war, records the memories, and starts each obliged
  ally's war (not offensive, no memories). Every war publishes `WarDeclared` and every
  treaty `PeaceSigned`.
- Border passage. `MayEnter(team, owner)` is true for a team's own territory and, at
  peace, when `ITeamAgreements.OpenBorders` says so. Movement and research diffusion
  ask it. Without an agreements mod foreign territory is closed at peace; the Trade mod
  then lets foreign routes run without open borders, as before.
- Dispositions. `DispositionToward(from, toward)` is how a player regards another
  (`Disposition`: Furious to Friendly). A mod answers through `IDispositions`; without
  one every player is Cautious. The war planner and the friends a war declaration
  offends read it. `Allied` asks `ITeamAgreements.Allied` (a defensive pact): the war
  planner never attacks an ally.
- Exchanges. `IPlayerExchanges` (`GameRules.Exchanges`) answers what running
  exchanges between players add: the copies of a resource a city can use
  (`ResourceAccess`, `BuildingEffects`) and the gold per turn the economy books as
  `economy:deal_income` and `economy:deal_expenses`. Without it a city uses its connected
  copies and no gold changes hands.

### Memory

The memory ledger records that one player remembers an act by another, as a count per
kind, in `RelationsState.Memories`. `IRelationsService.Remember(from, toward, kind)`
records one; `Memories(from, toward)` reads them. Teammates and barbarians remember
nothing of each other. A kind is content, `MemoryKindDef`:

- `Decay`: each turn every remembered kind loses one count with a chance of one in this
  many turns, scaled by the game speed's pace, unless the remembering leader's
  `LeaderDef.MemoryDecays` names its own. Memories fade in `relations:turn`, once a turn
  and never on turn 0, rolled in player order and then in ordinal order of the kind's
  leader-table name.
- `Limit`: most counts one player keeps of the kind about another (0 is none).
- `AttitudePercent`: attitude per count for a leader whose
  `LeaderDef.MemoryAttitudePercents` does not name the kind.
- `Key`: the name leader tables use, by default the id without its namespace
  (`relations:declared_war` is `declared_war`).

Each owner registers the kinds it records, from a `data/memory-kind.json` array loaded
with `MemoryKinds.Load`, which checks them: Relations registers `declared_war` (by every
member of the defending team), `declared_war_on_friend` (by third parties at least
Pleased with a defender who have met the aggressor), `razed_city` (by the player it was
captured from) and `razed_holy_city` (by followers of its religion); Religion
registers `inquisition`; Espionage `spy_caught` (by the owner of the city where a spy
was caught); Diplomacy `traded_tech_to_us`, `give_help` (limit 3) and `stopped_trading`.
A mod adds a kind the same way and records it with `sim.Rules().Relations.Remember`; it
needs no reference to the Diplomacy mod. `tests/TestMods/MemoryKind` is a complete
example. Whoever forms an opinion reads the ledger: the Diplomacy mod turns each count
into attitude by the leader's weight for the kind or else the kind's own.

## Requirements

`Requirement` supports `All`, `Any`, `Not` and `Leaf(Kind, Id, Count?, Param?)`. A bare
string denotes a `tech` leaf. Multiple composite clauses in one object form an `All`
expression.

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

Each owning mod registers one `IRequirementEvaluator` per leaf kind. Loading rejects
unknown kinds and duplicate evaluators, including requirements nested in custom
definitions. `RequirementContext` supplies the simulation, player and optional city,
plot or unit. Evaluation returns success and failure explanations. Successful
alternatives discard failed branches. Empty `All` succeeds. Empty `Any` fails.
Evaluators must not change state.

## Modifiers and ledgers

`IModifierSource.Contribute(ModifierContext, ModifierSink)` contributes to a plot, city,
player, team or unit calculation. The calculating mod calls `ModifierSink.Collect`, then
applies its own formula.

`YieldVector` holds food, production and commerce. `CommerceVector` holds gold,
research, culture and espionage. Sources contribute flat vectors, percent vectors and
namespaced integer effects such as `military:combat_percent`. Percentages add before
integer multiplication and division. Division truncates toward zero.

The sink produces a `Ledger` with ordered `(Source, Key, Amount)` lines and totals by
key. Typed keys separate flat and percent units. Each calculating mod owns effect-key
meanings, final formulas and rounding boundaries. Test-ruleset civilization and civic
bonuses are modifier sources, not special cases in the turn systems.

Content can carry modifiers as data. A `ModifierEffect` is `{scope, key, target,
amount}`; `ModifierSink.Apply` routes the fourteen typed vector keys
(`yield.flat.food`, `commerce.percent.research`, ...) to the typed vectors and every
other key to a ledger line, keyed `key:target` when the effect has a target. A
non-typed key must be declared by a `ModifierKeyDef` from the module that reads it
(`data/modifier-key.json`). The declaration gives the unit (`flat`, `percent`, `flag`,
`hundredths`), whether the key needs a target, and the label the client shows, in which
`{target}` stands for the target's name. Reference validation rejects an undeclared key,
a target on an untargeted key or a typed key, a missing target on a targeted key, and a
key whose provider the content's mod does not reference. Effects are part of their
definition, so they are in the content hash. A key nobody reads is legal and inert; the
computer player values only the keys it knows (Computer players).

## Events and extension state

`ctx.Subscribe<TEvent>(name, handler)` registers deterministic event handlers.
`Simulation.Events.Publish(record)` dispatches handlers synchronously in registration
order. Nested publications enter a FIFO queue after the current dispatch.

Core and mod event records cover:

- Core lifecycle: `TurnStarted`, `TurnEnded`, `TechDiscovered`, `CityFounded`,
  `CityGrown`, `BuildingCompleted`, `UnitCreated`, and `CombatResolved`.
- Advanced systems: `GreatPersonBorn`, `GreatGeneralBorn`, `InquisitionAttempt`,
  `ReligionRemoved`, `CityBombarded`, and `AirMissionResolved`.

`Published` holds current-turn presentation events. `PreviousTurn` retains the last
completed turn. These lists are neither saved nor hashed. Loading does not republish
events or rerun AI.

`ctx.RegisterStateComponent<T>(name)` registers a concrete state type under a stable id.
Game, player, team, city and unit objects each expose
`Extensions.Set<T>(content, instance)` and `Get<T>(content)`. Attached instances remain
mutable. Saves and hashes include their canonical JSON data.

Registered state components across mods include:

- Game: `ReligionGameState` (founded holy cities), `EspionageGameState` (global
  espionage points), and `MilitaryGameState` (temporary recon sights, the one barbarian
  player).
- Player: `EconomyPlayerState` (sliders, inflation, settlement), `GovernmentPlayerState`
  (anarchy, cooldowns), `ReligionPlayerState` (state religion, cooldown),
  `GreatPeoplePlayerState` (Golden Age timers), `MilitaryPlayerState` (combat general
  points, thresholds), and `EspionagePlayerState` (point allocations).
- City: `EconomyCityState` (hurry anger), `ReligionCityState` (city religion list),
  `RevolutionCityState` (instability indices), and `CityRuleState` (accumulated defense
  damage, turn bombardment status).
- Unit: `MilitaryUnitState` (activity, transport vessel reference, attached warlord
  leader type, air interception exhaustion).

State components use exact registered types, integer numbers and JSON-serializable
properties. Fields require `JsonInclude`. Runtime state belongs in components, not
shared service instances. Floating-point schemas fail during registration. Sorted string
sets and dictionaries retain ordinal comparers after loading. Unordered arrays, custom
converters, opaque objects and computed properties need care: the mod owns their
deterministic serialization contract.

## Saves and multiplayer

`SaveGame.Save(Simulation)` returns versioned JSON with content identity, settings, full
state and four RNG words. `SaveGame.Load(ContentDb, json, force: false)` checks the
schema and content identity, restores state, and rebuilds indexes. Forced loading
bypasses content mismatch only. It cannot restore unknown component types or unsupported
schemas. Schema changes need explicit migrations. Component authors must version and
migrate their own data contracts. To support pre-rename saves across namespace renames
(`Civ.*` to `Ariel.*`), `ContentDb` computes both canonical and legacy content hashes so
pre-rename saves with identical definitions load successfully without `force: true`.

### User-data migration and compatibility

When the application name changed to `Ariel`, the Godot user-data folder changed from
`Civ` to `Ariel`. `UserDataMigration` (`src/Ariel.Client.Core/UserDataMigration.cs`)
performs an engine-free, restart-safe one-time copy of `saves/`, `mods/`, and
`mod-profile.json` on first launch. If a file exists in both folders with differing
contents, neither is overwritten (both are kept, the new `Ariel` folder wins, and a
conflict is reported once). Upon successful verification of all copied files,
`user-data-migration.json` is written to mark completion.

Third-party mod binaries compiled against the old `Civ.*` assemblies cannot bind to the
renamed runtime; `LegacyModuleCheck` (`src/Ariel.Client.Core/LegacyModuleCheck.cs`)
detects these assemblies and excludes them from loading with an actionable message.

Runtime environment variables use the `ARIEL_` prefix:

- `ARIEL_MODS` (formerly `CIV_MODS`): optional additional mod search root.
- `ARIEL_TRACE_STARTUP` (formerly `CIV_TRACE_STARTUP`): enables startup phase timings.
- `ARIEL_CONTENT_SOURCE` (formerly `CIV_CONTENT_SOURCE`): specifies the root of the
  source extraction for extractor test suites.

`StateHasher` hashes canonical state JSON and RNG words with FNV-1a. Canonical JSON
sorts object properties ordinally, preserves array order and rejects non-integer
numbers. Content identity uses SHA-256 over canonical content data.

Commands travel as registered JSON type/data envelopes. AI runs on every peer and submits
ordinary validated commands locally.

`SimulationSettings.TurnMode` decides how players share a turn. `Simultaneous` (the
default) moves everyone at once. `Sequential` moves one player at a time. `Dynamic` moves
players at peace together and separates teams at war, as a greedy grouping of teams in id
order. At each turn start `TurnOrder.Plan` splits the players who still have to move
into segments. Players already ended, such as barbarians and rebels, are left out. The
plan is saved in `GameState.TurnOrder` and kept until the turn ends, so a war declared
mid-turn changes the next turn's plan. When a segment starts, its AI players move. A run
of segments with only AI players plans as one ordered speculative batch, which gives the
same result as running the segments one after another. The next segment starts once
every player of the active one has ended, and the turn advances after the last.

`Simulation.TrySubmit` applies a command a player issued, from a front end or the
network, and rejects it unless it is that player's move: a human moves only in its own
segment. Players who join after the turn was planned are bound by no segment. Commands
the rules issue themselves (turn systems, AI, effects) use `TryApply` and are never
gated.

The client's `GameSession` controls one seat, `LocalPlayer`, and changes the simulation
only through an `ICommandChannel`. `LocalCommandChannel` applies each command at once. A
networked channel queues commands and applies them, from every peer, when the front end
calls `GameSession.Pump` each frame. The session reacts to whatever was applied, whoever
issued it: it reads new events, drops stale selections, and re-issues standing go-to
orders as ordinary commands when a new turn begins. Only a local game ends the turns of
human players nobody controls.

Lockstep peers exchange content hashes before any tick runs. A mismatch raises an
explicit content error. Peers then submit command batches and exchange state hashes
after each tick. A state mismatch raises `Desync`. The handshake identifies content, not
initial setup. State hashes detect setup divergence.

### Multiplayer sessions

`Ariel.Net` carries lockstep over an `ITransport`. `TcpTransport` is a star for LAN and
direct IP: clients connect to the host, which relays their messages and stamps the true
sender. Each link has its own reader, writer and heartbeat tasks, so a long turn on the
game thread never looks like a lost connection. Received messages queue until `Poll`.

`Ariel.Client.Core/Multiplayer` holds the engine-free session layer. In the `Lobby`, the
host owns the `LobbyState` (setup, seats, peers, chat) and broadcasts it after every
request. On start, the host sends every peer either the resolved `SimulationSettings`,
as canonical JSON, or the save it loaded. `MultiplayerGame` builds the simulation, the
`LockstepSession` and a `GameSession` over a `LockstepChannel`.

Lockstep membership changes at ticks the host chooses, which no peer can have run yet:

- A seated peer that leaves stays a member up to the last batch the host received from
  it. Its player passes to the AI through `SetPlayerControlCommand`, a built-in command
  allowed out of turn, so every peer switches at the same step.
- A peer that connects later takes back a seat the AI holds. The host makes it a member
  from `LockstepSession.NextSubmit`. When its own game reaches that tick
  (`SnapshotBefore`), it sends the joiner the save and every batch it holds from that
  tick on, and the joiner continues with `LockstepSession.Resume`.
- After a desync, every peer writes its save to `user://desync`, and the host sends
  everyone the same kind of snapshot to restart from (`LockstepSession.Restart`).
  `mise run check:desync-diff -- A.json B.json` prints where two such saves differ.

An optional turn timer runs on the host's clock. When it runs out, the host submits
`EndTurnCommand`s for the humans still moving, so the result stays deterministic.
`mise run check:sync` checks determinism across processes, thread counts,
save/reload and background turns. `mise run check:multiplayer` plays a host and a guest
client against each other over localhost.

### Background turns and replayed moves

AI moves and the turn rollover run off the game thread, in single player and
multiplayer alike. With `Simulation.DeferTurnWork` set, the command that ends a turn or
hands a player to the AI only passes finished segments. AI moves and the rollover
remain, and `HasPendingTurnWork` reports them, derived from the turn order and who has
ended. The game thread runs nothing else in the meantime:

- `GameSession` (built with `backgroundTurns`) calls `BeginTurnWork`, which copies the
  state, RNG and this turn's events. It runs `TurnWork.Run` on a pool thread and takes
  the result back with `CompleteTurnWork` in a later `Pump`. The front end reads the
  untouched original the whole time, so nothing races. The local player's commands are
  refused until the work is back, and the end-turn button reads "Others moving...".
- A command that applies while work is pending runs that work in place first, so a
  deferred game plays out exactly like one that runs the work inside the command.
  `CompleteTurnWork` refuses a result if any command applied after the copy was made.
- `LockstepChannel` runs no tick while work is pending; it only keeps receiving.
  Lockstep hashes between ticks, so every peer must defer the same way, and
  `MultiplayerGame` always does.
- A save made while work is pending holds everything needed to finish it. The loading
  session simply carries on.

Moves are replayed on the map in the order they happened. `MilitaryData.MoveUnit`
publishes a `UnitMoved` presentation event for every plot a unit (or its cargo) enters.
The session turns these into `StepPlayback`s, queued with `BattlePlayback`s as
`MapPlayback`. The map renderer gives each moving unit a walker that leaves its stack
and plays its steps one after another, with every unit walking at once. A battle waits
for the steps queued before it, and steps queued after a battle wait for that battle.
Steps the local player cannot see, and units without a 3D model, skip to where they
ended up. Long walks speed up so a walker never takes much longer than six steps.
`ARIEL_FIXED_FPS=60 mise run shot -- ...` captures such animations at fixed simulated
time, even when the desktop throttles the window.

## Feature content mods

`src/Ariel.Rules/` contains the rules modules Foundation, World, Research, Economy,
Cities, Military, Government, Peoples, Religion and Relations, and `src/Ariel.Ai/` the base module
`ai`. `src/Mods/` contains GreatPeople,
Trade, RangedCombat, Diplomacy, ProjectsVictory, Corporations, Espionage, Events, ScriptedWonders,
Revolution, CardResearch and TechTree. Each manifest declares content providers in
`references` and load-order needs in `dependencies`; a base module's manifest names no
assembly. Base module ids always satisfy a mod's requirement and never appear in the
mod manager. ModLoader checks
provider presence before registration and typed references after all mods register. The
post-registration pass runs before freeze and content hashing. Unknown sidecar
properties fail load.

`Ariel.Content.Schema` holds immutable definitions. The schema includes costs, grouped
requirements, vectors, indexed effects, class limits, civilization overrides, and AI
weights. Definitions retain English text, eras, icon references, and unsupported-field
paths. Corporation and victory identities now have executable definitions. Religion owns
playable religion definitions and prerequisites. The schema lives in `src/Ariel.Rules/Schema`. See
[content-schema.md](content-schema.md) for definition types, units, scopes, and importer
decisions.

## 3D terrain rendering

The client renders continuous 3D blended terrain instead of flat per-hex meshes.
Neighbouring tiles blend smoothly in height and material across hex boundaries, mountain
peaks merge into continuous ranges, and 3D nature props scatter organically with edge
thinning and species mixing across borders.

```mermaid
flowchart TB
    subgraph Core["Ariel.Client.Core/Terrain (engine-free math)"]
        Noise["PeriodicNoise / WarpGrid<br/>wrap-safe periodic noise & baked 2D domain warp"]
        Blend["HexBlend<br/>dual-triangle barycentric weights"]
        Snap["TerrainSnapshot<br/>layers, relief, heights, cliffs, rivers, sight, features"]
        Field["TerrainField<br/>HeightAt, SurfaceHeightAt, NormalAt"]
        Chunks["TerrainChunks<br/>4x4 tile partition & 64-bit dirty signatures"]
        Mesher["TerrainMesher<br/>global triangle lattice (k = 9, or 27 near peaks/cliffs)"]
        Ray["TerrainRay<br/>slope ray marching & bisection picking"]
        Palette["TerrainPalette<br/>layers, surfaces, feature styles, scatter sets"]
        Planner["ScatterPlanner<br/>global jittered lattice, thinning, mixing, clearings"]
    end
    subgraph View["Ariel.Godot (presentation)"]
        TR["TerrainRenderer<br/>chunk MeshInstance3D x3 wrap copies, parallel meshing"]
        TDT["TileDataTexture<br/>tileA, tileB, packed 16-bit warp grid, palette strip"]
        TT["TerrainTextures<br/>Texture2DArray with height in alpha"]
        TS["terrain.gdshader<br/>height blend, slope cliff rock, drifting clouds"]
        WS["water.gdshader<br/>vertex-depth, ripples, shore foam, drifting clouds"]
        SR["ScatterRenderer<br/>MultiMesh x3 copies, SHA-256 deduplication, hash thinning"]
    end
    Palette --> Snap
    Noise --> Field
    Blend --> Field
    Snap --> Field
    Field --> Mesher
    Chunks --> Mesher
    Field --> Ray
    Field --> Planner
    Blend --> Planner
    Snap --> TDT
    Mesher --> TR
    TT --> TS
    TDT --> TS
    TDT --> WS
    TS --> TR
    WS --> TR
    Planner --> SR
```

### Engine-free terrain math (`Ariel.Client.Core/Terrain`)

Terrain math is fully decoupled from Godot and deterministically unit-tested in
`Ariel.Client.Core`:

- `PeriodicNoise` & `WarpGrid`: Periodic value and ridged noise lattices wrap modulo the
  cylindrical map period ($W \\sqrt{3}$). `WarpGrid` precomputes a 2D domain-warp
  displacement field that is sampled bilinearly on the CPU and as an RGBA8 texture in
  the shaders, giving tile boundaries an organic appearance without cylindrical wrap
  seams.
- `HexBlend`: Dual-triangle barycentric blending. Tile centres form a triangular
  lattice. Every world point lies within exactly one "corner triangle" formed by three
  mutually adjacent tile centres. Its barycentric coordinates $\\lambda$ are continuous
  across triangle borders ($\\lambda = 0$ on the opposite vertex). Weights are sharpened
  via $w_k = \\lambda_k^s / \\sum \\lambda_j^s$ ($s = 4$ for materials, $s = 6$ for peak
  heights).
- `TerrainSnapshot`: Immutable snapshot of local-team terrain perception containing tile
  layers, feature ground indices, relief classes (`Hidden`, `DeepWater`, `ShallowWater`,
  `Flat`, `Hills`, `Peak`), stored heights, mirrored 6-bit river and cliff masks,
  derived cliff lifts, crater flags, sight (`Hidden`, `Fogged`, `Visible`), owner,
  terrain ids and features.
- `TerrainField`: Pure deterministic function of world coordinates $(x, z)$ and the
  `TerrainSnapshot`:
  - `HeightAt(x, z)`: Blends relief profiles using warped corner-triangle weights.
    Unexplored tiles stay at flat 0.0 to prevent leaking unseen terrain relief. Deep
    water is $-0.45 \\pm 0.02$ noise, shallow water/lakes are $-0.15 \\pm 0.02$ noise,
    flat land is $0.03 + 0.03 \\cdot \\text{fbm}$, hills stand on a $0.05$ saddle and
    rise up to $0.33$ over their mounds with $0.04$ fbm, and peaks rise from a $0.38$
    saddle by up to $1.15$ with up to $0.22$ ridged noise and $0.09$ fine rock relief,
    merging adjacent peaks into continuous ridges. Rivers carve down by up to $0.075$
    within a band along mirrored river edges. Cliff-raised land adds up to `CliffRise`
    (0.22): a sharp step along a cliff edge and a 1.6 ramp on a bare edge, combined with
    a maximum so lifts never stack.
  - `SurfaceHeightAt(x, z)`: Visible ground or water elevation. Submerged terrain ($\<
    0$) is clamped to 0 near water for seating surface units, tokens, and path previews,
    while the seabed stays at `HeightAt`.
  - `NormalAt(x, z)`: Unit surface normal evaluated by central differences of
    `HeightAt`.
- `TerrainChunks`: Partitions the map into world-space rectangular chunks of $4 \\times
  4$ tiles ($4\\sqrt{3} \\times 6$ world units). Computes a 64-bit dirty signature over
  each chunk's tiles and an influence ring (accounting for domain warp, blend radius,
  normal step, river carving, heights, cliff bits and lifts, and the cliff ramp). A
  chunk whose influence ring contains a peak or a cliff resolves at 27; every other
  chunk at 9. When world state changes, only chunks with modified signatures are
  rebuilt.
- `TerrainMesher`: Generates chunk meshes on a single global equilateral-ish triangle
  lattice at resolution $k = 9$ by default (step $\\sqrt{3}/k$ in $x$, $1.5/k$ in $z$,
  alternating row offsets, about 81 vertices per hex), and at $k = 27$ around peaks and
  cliffs. Because lattice vertices are globally indexed and divide chunk bounds exactly,
  chunk borders and the cylindrical wrap seam share identical vertex positions and
  normals without cracks. Vertices never leave the lattice; a cliff drop falls within one
  $k = 27$ step, so the face is a steep slope that wide.
- `TerrainRay`: Slope ray marching (step 0.05) with 8 rounds of bisection against
  `HeightAt` or `SurfaceHeightAt`. Accurately picks the visible 3D slope under the
  cursor rather than intersecting a flat $y = 0$ plane.
- `TerrainPalette`: Content-driven layer lookup mapping terrain and surface IDs to
  texture array indices and fallback RGB colours, plus feature ground tint styles and
  scatter model configurations.
- `ScatterPlanner`: Generates 3D prop candidates on a global jittered lattice (spacing
  ~0.2) dividing the map period. Evaluates the warped corner-triangle weights to sample
  cumulative density $\\sum w_k \\cdot \\text{density}\_k$; features thin smoothly at
  borders (forest fading into plains) and mix species across boundaries (pines and palms
  between forest and jungle). Avoids water, river troughs, cloud-covered tiles,
  designated clearings (city centres, improvements, unit tokens), and cliff faces (cliff
  share above 0.2 with a normal below 0.8). Cliff rocks and sea stacks are added as
  exact props after the ordinary lattice, so they are never thinned.

### Godot presentation (`Ariel.Godot`)

The Godot view translates the engine-free math into GPU-rendered visuals compliant with
`gl_compatibility`:

- `TerrainRenderer`: Manages one lit ground `MeshInstance3D`, one water `MeshInstance3D`
  and one river surface per chunk across 3 horizontal wrap copies ($[-P, 0, P]$) for
  seamless east-west cylindrical wrap. When chunks are invalidated, CPU meshing runs in
  parallel via `Parallel.For`.
- `TileDataTexture`: Packs tile state into two RGBA8 textures sampled via `texelFetch`:
  - `tileA`: R = terrain layer, G = feature ground, B = owner + 1, A = river edge mask
    (refreshed on world changes).
  - `tileB`: R = sight, G = overlay mark (workable/worked/locked), B = hover flag, A =
    255 on a peak tile (relief) (refreshed on selection, sight, or hover changes without
    rebuilding meshes).
  - Also generates a 16-bit packed domain-warp texture and a $256 \\times 4$ palette
    strip (layer colours, feature ground tints, owner colours, water tints).
- `TerrainTextures`: A mipmapped `Texture2DArray` (1024×1024 RGBA8 per slice) storing
  albedo in RGB and high-frequency surface height in alpha.
- Shaders (`terrain.gdshader` and `water.gdshader`):
  - `terrain.gdshader`: Soft corner-triangle ground blends whose centre line wanders.
    Each tile claims a point with $\\ln\\lambda$ plus its own seeded noise (up to 0.4
    world units of shift), its terrain's spread (lush ground such as grass grows into
    dry ground such as desert) and its texture height; a softmax of the claims
    ($\\lambda^s e^{s \\cdot \\text{shift}}$) gives the colour weights over a 0.35-unit
    band. Water layers get no noise or texture-height term, so seabeds only fade.
    Forest-floor shade and fog stay soft; transitions to cliff rock on steep slopes, and
    above the hills on peak tiles only, so a hill on lifted ground keeps its own
    texture; the palette transition narrows inside the cliff share in `UV.x`, so a
    raised top keeps its own ground; renders owner borders, territory glow, river
    channels, work-tile overlay marks, and hover outline; draws unexplored tiles as
    stylised, slowly drifting dual-layer clouds.
  - `water.gdshader`: Translucent water surface at $y = 0$. Reads terrain depth from
    vertex `UV2.x = -HeightAt` and the cliff share from `UV2.y = CliffAt` without
    depth-texture fetches; renders multi-frequency ripples, shore foam, surf and foam
    streaks at the foot of a cliff, shallow/deep water gradients, seabed texturing, fog
    darkening, and drifting cloud cover.
- Ground marker shaders, all drawn on terrain-draped meshes and animated with `TIME`:
  - `selection.gdshader`: The marker under the selected unit: a bevelled gold ring, a
    dashed inner ring, turning chevrons, glow and a drop shadow.
  - `path.gdshader`: The go-to route. Ribbon vertices carry UV (across, distance along),
    which drives a dark-edged core and chevrons that flow toward the destination; black
    vertices mark waypoint pips with a radial UV.
  - `destination.gdshader`: The route's end: a ring in the route colour, four inward
    chevrons, an outward ripple, and a centre dot (a cross for an attack).
- `ScatterRenderer`: Batches chunk props into `MultiMesh` instances shared across the
  three wrap copies:
  - Deduplicates GPU textures across GLB models via SHA-256 image hashes.
  - Applies hash-stable per-instance thinning (`KeepProp` using deterministic 32-bit
    integer mixing of tile, model index, and float coordinates) to sustain high
    framerates on large maps without prop flickering during chunk updates. Exact props
    (cliff rocks and sea stacks) bypass thinning so a wall keeps no gaps.
  - Distance culling with `VisibilityRangeEnd = 34f`.
  - Modulates instance vertex colours for fog darkening (1.0 for visible, 0.55 for
    fogged).

### Mod appearance contract & asset discovery

The presentation contract in `Ariel.Rules` and client catalog in `Ariel.Client.Core`
provide extension points for 3D art:

- `TileAppearance`: Extended record
  `TileAppearance(int Rgb, int TintPercent = 0, int Marks = 0, string? Scatter = null, int ScatterDensity = 0, int Spread = 50)`.
- `TerrainSurface`: Enum defining system and procedural surfaces (`Cliff`, `Seabed`,
  `DeepSeabed`, `Unexplored`).
- `ITileAppearanceService`: Interface offering default methods
  `string? Surface(TerrainSurface surface) => null;` and
  `TileAppearance? Scatter(string feature, string terrain) => null;`. Mods override
  these to specify procedural surface textures and terrain-specific scatter variants
  (e.g. snowy pine trees on tundra/snow forests).
- `GameCatalog.TexturePath(string? id)`: Resolves texture PNGs from
  `<mod>/art/terrain/<name>.png`.
- `GameCatalog.ScatterModels(string? set)`: Discovers sorted `.glb` models from
  `<mod>/art/scatter/<suffix>/*.glb`.
- `GameCatalog.ModelPath(string? id)`: Resolves `<mod>/art/models/<name>.glb`. Units and
  improvements share it. `ImprovementRenderer` treats each top-level node of an
  improvement model as one part. It seats each part upright on the ground at the part's
  offset from the tile centre, or tilts it to the surface when its name starts with
  `ground`. The scene extras `"clear"` value (`tile` by default, or `centre`) decides
  whether scatter props are removed from the whole tile or only from its centre. It
  batches parts into MultiMeshes per model and part, and uses instance colour for fog.
  For each part that is not `ground*` or `nodecal*`, it takes the convex hull of the
  vertices at or below y = 0.06 as the part's footprint. On every rebuild it drapes all
  footprints into one transparent mesh (`shaders/ground_decal.gdshader`): a fan over the
  hull grown by a margin, then two feathered rings that fade to zero alpha. Every vertex
  is seated on the ground height, with a lift that grows with slope, and is tinted dark
  earth times the fog shade. Footprints whose centre is on water are skipped. The map
  stats report its size as `improvement_ground`.
