# Roadmap & Mechanics Status

This document is the single canonical source of truth for project status: tracking what
is done (implemented systems and mechanics) and what needs to be done (partial
mechanics, deferred systems, and future milestones) across all modular subsystems.

For architectural design and core principles ("everything is a mod"), see
[docs/DESIGN.md](DESIGN.md).\
For the hex adaptation specification and original porting plan, see
[PLAN.md](../PLAN.md).\
For build instructions and test commands, see [README.md](../README.md).

## 1. Roadmap & Milestone Overview

The project brings the full content of the source extraction onto a deterministic hex
grid. Core simulation rules, modular systems, 3D presentation, and unit art (Phases
P0–P9) are fully implemented.

| Milestone | Focus                           | Status      | Deliverables                                                                                 |
| :-------- | :------------------------------ | :---------- | :------------------------------------------------------------------------------------------- |
| **P0–P9** | Core Port, Rules & Engine       | **Done**    | Complete rules simulation, deterministic AI, 3D terrain/art, and modular feature services.   |
| **P10+**  | Polish, Multiplayer & Expansion | **Planned** | Networking lobby, historical scenarios, full naval/air AI, expanded diplomatic negotiations. |

______________________________________________________________________

## 2. What Is Done (Implemented Systems & Mechanics)

Status reflects executable simulation rules, loaded definitions, and engine-free models.

### Foundation & Core Contracts

- **Phase contract & turn order**: Deterministic phase lifecycle execution
  (`docs/02-turn-lifecycle.md`) with lockstep handshake and state hash verification.
- **Calendar progression**: Deterministic calendar date calculation (`GameCalendar`)
  driven by mod speed schedules (Normal 1200 turns, Marathon 2400 turns) and start year
  6000 BCE (`START_YEAR=-6000`). Top bar displays the calendar date alongside the turn
  counter.
- **Setup & configuration**: Game setup selection, game options checklist
  (`SimulationSettings.GameOptions`, `Simulation.IsOptionEnabled`, Godot `SetupScreen`),
  world sizes (`WorldSizeDef`), speeds (`GameSpeedDef`), handicaps (`HandicapDef`), eras
  (`EraDef`), player teams, and initial gold.
- **Hex tuning overrides**: `src/Mods/Foundation/data/hex-overrides.json` controls
  hex-specific balance, including `barbarianUnownedTilesMultiplier` and
  `aiMinimumCityGarrison`.
- **Dogfood mod isolation**: All features are standalone mods (`src/Mods/*`). `Civ.Core`
  and `Civ.Client.Core` hold no hard-coded mod IDs or content kinds. Custom test
  rulesets run without shipped mods.

### World & Map Generation

- **Map topology**: Rectangular odd-row hex map (`HexGrid`) with wrap-safe coordinate
  conversions, shortest-distance calculations, and optional east-west wrapping.
- **Procedural generation**: Continents, relief (peaks, hills, flatlands), climate
  zones, areas, lakes, and shared 6-direction river masks.
- **Features & Goody huts**: Climate-driven feature placement (forest, jungle, ice,
  floodplains, oasis) and goody huts with configurable densities.
- **Resource placement**: Physical resource placement respecting latitude, area size,
  terrain/feature requirements, and cluster rules. The flatlands/hills/peaks test
  applies to land plots only, so sea resources (fish, clams, crabs, pearls, shrimp,
  whales, methane, offshore oil) spawn in the water.
- **Plot yield ledgers**: Natural and improved plot yields with cross-mod modifiers,
  river changes, hills/peak adjustments, and route yield additions.
- **Fresh water & irrigation**: Fresh water propagation from rivers, lakes, and oases;
  irrigation transit through farms and city plots.
- **Worker builds & improvements**: Worker assignments, shared build progress, feature
  removal production credits, improvement construction, route networks, automated
  upgrades, peak build modifiers, and route upgrade discounts.
- **Pillage commands & tile repairs**: Unit pillaging of improvements and routes with
  cargo and domain checks. Enemy pillage awards gold and steps improvements down, while
  workers prioritize repairing ruins and severed routes.
- **Dynamic feature growth & decay**: Per-turn feature disappearance rolls and adjacent
  feature growth and spread rolls scaled by game speed, feature growth modifiers, and
  route penalties.
- **Resource discovery & default-off depletion**: Per-turn resource discovery rolls on
  worked improved bonus-free tiles. Option-gated mine exhaustion creates depleted mines,
  and deposit depletion uses the reference zero-consumption path.
- **Global warming environmental changes**: Global warming defense from map features and
  health civics balances city pollution. Unchecked pollution drives stepped feature and
  terrain degradation.
- **Line-of-sight occlusion & hidden-state fog snapshots**: Elevation and feature
  line-of-sight occlusion with high-ground vantage range and aerial bypass. Per-team
  fog-of-war plot snapshots preserve last-observed terrain, features, improvements, and
  ownership.
- **Foreign trade network transit & vicinity resources**: Physical resource and city
  networks transit revealed peaceful foreign territory without Open Borders treaties.
  Naval blockades sever water networks, and vicinity bonus checks enforce local
  connectivity.

### Research

- **Prerequisite graph**: Strict checking of all/any technology prerequisites, obsoleted
  branches, and era gating.
- **Sequential integer cost scaling**: Deterministic integer truncation stages scaling
  base technology cost sequentially by handicap, world size, game speed, starting era,
  team size, and timeline multipliers.
- **Diffusion & welfare rate modifiers**: Contacted teams knowing a technology grant
  diffusion bonuses using an eligible living non-barbarian team denominator; welfare
  bonuses evaluate the team leader's player total-score contract. These adjust research
  output rate (`RateModifier`) rather than base technology cost.
- **Realistic timescale (timeline scaling)**: Option-gated dynamic cost scaling
  comparing elapsed turns against expected era transition points and team leader
  era/technologies, without referencing historical calendar dates.
- **Tech trading gates & theft eligibility**: Technology trading in diplomacy requires
  enabling technologies (`TechTrading` unlock flag) and option checks. Espionage tech
  theft uses separate, independent eligibility rules.
- **Option defaults & brokering limitation**: Diffusion and tech trading enabled by
  default; realistic timescale and no-tech-brokering disabled by default. The selected
  no-tech-brokering option remains unsupported because technology acquisition provenance
  is not yet tracked.
- **Queues & overflow**: Team research targets, queues, progress tracking, and
  normalized carry-over overflow.
- **Tech discoveries**: `TechDiscovered` domain events, first-discovery free tech
  credits, free unit grants, and current-era milestone progression.

### Economy

- **Commerce sliders**: Gold, research, culture, and espionage allocation sliders with
  mod-configurable step increments and maximum percentages.
- **City commerce ledgers**: Full breakdown of raw commerce, modifier percentages, and
  direct commerce bonuses.
- **Maintenance & costs**: City distance, number-of-cities, and corporate maintenance,
  civic upkeep, military unit upkeep, outside-border supply, and inflation lines.
- **Second-order inflation**: Momentum-based inflation accumulator with turn offsets,
  handicap scaling, and civic modifier support.
- **Specialized production & hurry**: Infinite `economy:process` queue items (converting
  production directly to commerce/gold/research/culture) and `economy:hurry` payments
  (gold hurry with new-item surcharges and population whip with dynamic building anger
  modifiers, retroactive timer rescaling upon building completion, and hurry inflation
  decay).
- **Corporate maintenance integration**: Full branch maintenance equation factoring
  headquarter commerce, resources, world size, population, handicap, civic modifiers
  (`RealCorporationMaintenanceModifier`), pre-inflation division, and realistic
  corporation scaling.
- **Bilateral deal gold & trade transfers**: Per-turn trade deal gold integrated into
  `PlayerEconomy.Calculate` (`dealIncome`, `dealExpenses`, `NetGold`) with automated
  slider adjustments to prevent bankruptcy and net-payment settlement checks.
- **Targeted espionage spending**: `IPlayerEspionageSource` integration routing commerce
  to rival team allocations with read-only ledger forecasts and remainder point
  distribution.
- **Deficit strikes & forced disbanding**: Sustained deficits track `StrikeTurns`;
  multi-turn strikes trigger forced disbanding of lowest-value units via valuation
  scoring and role multipliers while protecting military happiness garrisons, halted
  once net gold reaches equilibrium.

### Cities

- **Founding & territory**: Settler founding checks, minimum hex spacing, terrain
  validity, starting buildings, and initial plot culture.
- **Work area**: Radius-2 ring (19 hexes) and culture-unlocked radius-3 ring (37 hexes),
  with deterministic governor tile weights and manual assignments.
- **Yield & commerce aggregation**: City-center minimums, worked plot yields, specialist
  yields, building changes and percentage modifiers.
- **Growth & food**: Sequential food threshold calculations, consumption, health
  penalties, fixed-point food waste, starvation prevention, and avoid-growth toggles.
- **Happiness & health**: Ledgers combining population, handicap base, connected
  resources, buildings, specialists, features, and military presence.
- **Production queues**: Append, prepend, remove, and pause commands; normalized capped
  production overflow; class-limited units, buildings, and world wonders.
- **Culture borders & assimilation**: Per-plot culture ledgers, path-based cultural
  distance falloff, tie-breaking ownership assignment, periodic culture decay halving on
  speed-scaled golden age intervals, and original civilization retention
  (`OriginalCivilization`) for unit/building override assimilation upon capture.
- **Conscription (Draft)**: `ICityConscriptService` and `ConscriptCityCommand`
  (`cities:conscript`) drafting highest-priority eligible units with population
  sacrifice, halved city production experience, zero initial movement, world-size-scaled
  player draft limits, and stacking draft anger.
- **Nationality anger**: Plot culture anger (`cities:culture_anger`) distinguishing
  foreign teams and wartime escalation (+50%) with same-team suppression, plus civic
  foreigner unhappiness (`government:foreigner_anger`).
- **Building power grid**: `CityPower` tracks local and area sources (`ProducesPower`,
  `PowerBonus`, `AreaCleanPower`, `DirtyPower`). Power deactivates `RequiresPower`
  buildings when missing. Clean power blocks `NoPower` construction. Powered cities
  receive `PowerYieldModifiers` and apply health modifiers.
- **Dynamic free building propagation**: Dynamic calculation supplies free buildings
  from team buildings (`FreeBuilding`) and completed wonders (`EmpireFreeBuilding`).
  Grants map through civilization overrides without mutating constructed city buildings.
- **Non-destructive building replacement**: Replacement preserves replaced structures in
  city storage. Active replacers (`ReplaceBuildings`) dynamically suppress lower-tier
  buildings. Suppressed buildings reactivate if the replacer becomes obsolete or is
  removed. Construction enforces `ExtendsBuildingClass` prerequisites.
- **City conquest mechanics**: Capture rolls building survival, awards capture gold, and
  triggers size-1 auto-raze. Capturing or razing capitals relocates victim capitals
  automatically. Occupation disorder timers delay productivity. Interactive conquest
  commands support `Annex`, `Puppet`, and `Raze` choices. Puppet cities reduce
  maintenance and citizen unhappiness.
- **Excess production overflow gold conversion**: Production above the overflow cap
  converts into treasury gold upon order completion. Conversion scales item modifiers
  back to city base yield modifiers.
- **Path-based culture spread obstruction**: Shortest-path culture spread blocks
  propagation across impassable barriers and hostile borders. Optional realistic culture
  adds movement penalties for rough terrain, features, and river distances.

### Military & Combat

- **Hex movement & navigation**: Deterministic A\* pathfinding, fractional move points,
  terrain/feature costs, hill penalties, road/rail bonuses, and river crossing bridges.
- **Combat resolution**: Melee strength ledgers, firepower damage, first strikes and
  first-strike immunity, attacker withdrawal checks, collateral damage caps, and
  best-defender selection.
- **Combat odds preview**: Exact integer dynamic programming probability engine without
  RNG usage.
- **Promotions & experience**: Experience gain based on strength ratios, promotion
  prerequisite trees, healing bonuses, and great general point counters.
- **Unit actions**: Fortify (defense accumulation), sleep, sentry, pillage, upgrade in
  friendly territory, and disband.
- **Barbarians & naval spawns**: Fog-of-war unowned-land spawning scaled by hex density
  tuning; coastal water area barbarian naval spawns governed by handicap divisors,
  non-barbarian water unit limits, lake exclusions, and pirate pillaging AI.
- **Cargo & amphibious warfare**: Unit loading into transports, capacity limits with
  promotion modifiers, shared ocean transit, cargo loss on ship destruction, and
  amphibious assaults from sea with combat penalties mitigated by amphibious training.
- **Air combat missions & interception**: Rebase, recon, air strike (stack damage capped
  at combat limit), air bomb (city defense reduction and improvement destruction), and
  combat air patrol (CAP interception with evasion checks and dogfight rounds).
- **Artillery bombardment & ranged attacks**: Siege bombardment degrading city defenses,
  and ranged support strikes without mutual melee counterattack damage.
- **City defense damage & recovery**: City defense damage accumulator reducing defense
  modifiers, with turn-by-turn recovery for undamaged cities.
- **Stealth & invisibility**: Submarine/spy invisibility masking presence from enemy
  vision, avoiding sentry triggers and path blocking until revealed by dedicated
  detector units.
- **Flanking strikes & defender withdrawal**: Mounted unit flanking attacks damaging or
  destroying defending siege stacks; tactical defender withdrawal saving units upon
  defeat.
- **Strategic military campaigns**: Multi-theater stack staging partitioned by landmass
  (`AreaId`), combined arms support fire sequencing before melee assault, air theater
  management, and threat-differential defensive reinforcement lines.

### Government & Civics

- **Civic categories**: Multi-category civic options with one active civic per category.
- **Revolution & anarchy**: Anarchy duration calculated from base turns, city count,
  civic weights, trait exemptions (Spiritual), and minimum cooldown timers.
- **Civic modifier source**: Dynamic modifiers applying to yields, commerce, capital
  city bonuses, improvement yields, happiness, health, maintenance, and military
  training.
- **Draft civics & quotas**: Civic-enabled drafting permissions and per-turn quotas
  (`cities:max_conscript`) scaled by world size modifier.

### Peoples, Leaders & Traits

- **Identities**: Full catalog of leaders, civilizations, unique units, and unique
  buildings.
- **Trait modifier source**: City yields, commerce, happiness, health, wonder production
  bonuses, free promotions by combat type, and maintenance discounts.
- **Extra culture levels**: Additive culture level modifiers
  (`cities:extra_culture_level`) from leader traits (`TraitDef.ExtraCultureLevel`),
  civics, and civilizations boosting initial city founding culture and border range.
- **AI personality profiles**: Complete personality matrices (flavors, aggression,
  wonder preferences, civic weights) driving the AI mod.

### Advanced Modular Systems

- **Religion**: 13 imported religions, holy city scoring, natural trade spread,
  international trade route spread across open borders, religion decay and attrition for
  unprotected minorities, civic-gated inquisitions (purging non-state faiths/temples,
  hurry anger, holy city relocation/extinction, and diplomatic memories), missionaries,
  shrine construction, state religion adoption, and religious happiness/commerce.
- **Great People**: City progress points, class pools, deterministic births, combat
  Great General emergence from combat experience, warlord attachment (`military:leader`
  promotions and stack experience sharing), Military Academy/Academy construction, Great
  Merchant trade missions to foreign cities for gold, settlement (free specialists),
  flavor-driven tech bulbing, building hurry, and Golden Ages.
- **Trade**: Domestic and foreign trade routes, slot capacity, connectivity
  verification, overseas profit bonuses, and shared civic bonuses. Player naval
  blockades sever water trade networks and coastal trade, while autonomous AI blockade
  planning remains absent.
- **Diplomacy**: Bilateral team contact discovery, attitude ledgers, peace treaties,
  open borders, defensive pacts, and multi-item trade deals (resources, gold, gold/turn,
  techs).
- **Projects & Victory Conditions**: Team/world projects; Conquest, Domination,
  Cultural, Space Race, and Time victories with victory resolution screens.
- **Corporations**: Great Person founding, branch spread (via gold or executives),
  headquarters protection, branch revenue, resource consumption, and maintenance.
- **Espionage & Intelligence**: Rival-team point allocation, bilateral spending ratios,
  passive visibility/investigation unlocks, full rival intelligence screen
  (demographics, city yields/buildings, production queues, treasury reserves) with
  permission filtering and immediate revocation, and active missions (counter-espionage
  with directed cost penalties scaling from a 200% baseline by supported interception,
  treasury theft, production sabotage, city revolt, tech theft).
- **Events**: Dynamic random event rolls, option choices with yield/gold/culture
  impacts, cooldown timers, and automated AI choices.
- **Scripted Wonders**: Special wonder abilities, Great Wall barbarian exclusion,
  periodic unit grants, and combat bonuses.
- **Revolution**: City instability ledgers (wellbeing, distance, civics, garrisons,
  golden age), warning/mutiny/rebellion states, military/gold suppression, and rebel
  unit spawns.

### Computer Players (`ai:standard`)

- **Deterministic AI**: Operates strictly via validated player commands using simulation
  RNG; zero hidden game state.
- **Empire management**: Automated research selection, civic revolutions, commerce
  slider adjustments, production queue management, citizen tile allocation, and Great
  Person valuation (weighing Golden Ages vs. tech bulbing vs. settlement).
- **Expansion & military**: Settler scouting and escorted founding; worker build
  planning and road corridors; city garrison retention, multi-theater campaign staging
  by `AreaId`, combined arms pre-assault bombardment and ranged support fire, naval
  amphibious sealift, air rebasing and bombing missions, threat-differential
  reinforcement corridors, and combat odds gating.
- **Religious management**: Missionary spread, state religion adoption, and inquisition
  purging based on city instability and state church policies.

### Client, Presentation & 3D Art

- **Engine-free client (`Civ.Client.Core`)**: Complete screen models for setup, city
  management, research tree, civics, diplomacy, victory, corporations, espionage,
  events, pedia, and save/load. Setup screen options and perceived fog snapshots prevent
  unseen map mutation leaks.
- **Godot 4.7.2 views (`Civ.Godot`)**: Native 3D viewport, camera controls, hex prism
  highlights, audio playback, and responsive HUD panels.
- **HUD in-place invalidation (`Civ.Godot`)**: The top bar and left panel are built once
  and updated in place instead of rebuilding the widget tree on every map click; see
  [docs/OPTIMIZE.md](OPTIMIZE.md#pass-7-hud-invalidation-on-clicks).
- **3D resource models**: Every map resource has a procedural low-poly model
  (`art/models/<id>.glb`, built by `tools/art/improvement_models.py`) standing on a spot
  off the tile centre that avoids rivers and the wrong side of the shore. Scatter props
  keep the spot clear. Manufactured goods have no model.
- **Map overlays & local toggles**: Independent visual toggles for tile yield badges,
  resource badges, hex grid lines, and cultural borders. Managed locally in
  `GamePreferences` without affecting simulation state or hashes, strictly respecting
  fog-of-war perceived snapshots.
- **Continuous 3D blended terrain**:
  - Seamless 1024×1024 terrain textures with height in alpha.
  - Procedural terrain mesher generating continuous height and relief transitions,
    tapering coastal cliff mountains down to the coastal bluff before the sea drop-off
    so cliff rock columns cover the face without a sheared vertical mountain wall.
  - `terrain.gdshader` and `water.gdshader` with shore foam, depth transparency, and
    slope cliff rock.
  - Slope ray-marching bisection cursor picking.
- **3D nature scatter**: Quaternius CC0 models converted to compact GLBs, jittered
  Poisson-like lattice planning, and GPU MultiMesh instancing.
- **3D unit art & combat animations**: 357 converted unit models with `idle`, `run`,
  `fortify`, `attack`, `hurt`, and `die` animations rendered on the world map.
- **Production queue categories**: The city building production queue has Wonders and
  All categories (like Projects and Units), plus a filter text box. A kind declares its
  own extra categories, so the wonder split is content knowledge rather than a client
  special case.
- **Scroll bar margins**: Every scroll view widget has a margin to the left of the
  scroll bar so lists starting or stopping scrolling do not reflow rows. The tech tree
  canvas keeps its own clearance.
- **Stacked unit list**: When the selected unit shares its tile with another of the
  player's units, the left panel lists every one of them above the unit detail, with
  unit art, name, health and a small icon or badge per earned promotion whose name is
  its tooltip. Clicking a row selects that unit, and the rows follow the same id order
  that repeated map clicks cycle through, so the list and the cycling always agree.
- **Uncapped production list**: The city screen's production list draws every row its
  tab and filter match, available first and unavailable after them, and the panel's
  scroll container reaches all of them. The `Show unavailable` checkbox still decides
  whether the blocked rows are drawn at all.

______________________________________________________________________

## 3. What Needs to Be Done (Pending & Deferred Work)

### Foundation & Setup

- [ ] **Advanced start mode**: Allow purchasing cities, units, buildings, and
  technologies before turn 1.
- [ ] **Era & handicap start scaling**: Implement `StartingUnitMultiplier`,
  `StartingDefenseUnits`, `StartingWorkerUnits`, and `StartingExploreUnits` for advanced
  era starts.
- [ ] **Per-player handicap options**: Allow human and individual AI players to have
  distinct difficulty levels in the same session.
- [ ] **Default civics fallback**: Auto-fill missing civic selections for incomplete
  custom civilization declarations.
- [ ] **Remaining game option flags**: Implement simulation rules for the unimplemented
  game option definitions in `src/Mods/Foundation/data/game-option.json`.
- [ ] **Malformed-content guards**: Add fallback validation and default placeholders
  during content loading for missing technology identifiers in unlock tables or missing
  bonus identifiers in sight memory.

### World & Map Generation

- [ ] **Custom map generator scripts**: Support external or custom procedural map script
  definitions.

### Research

- [ ] **Player-specific tech queues**: Separate tech choices within shared multiplayer
  teams.

### Trade & Diplomacy

- [ ] **AI naval blockade planning**: Autonomous AI orders to deploy and maintain naval
  blockades (player blockade commands, water network severing, coastal city work
  blocking, and trade route cancellation are implemented).
- [ ] **AI diplomatic negotiations**: AI-initiated treaty proposals, counter-offers,
  peace demands, and gift requests.
- [ ] **Diplomatic vote resolutions**: United Nations and Apostolic Palace voting
  sessions (world peace, global civics, diplomatic victory).
- [ ] **Embargoes & sanctions**: Multilateral agreements to prohibit trade with targeted
  civilizations.

### UI & Client

- [ ] **Terrain bonus icons**: Square icons still appear above terrain bonuses (like
  Stone) even though we now have 3D meshes; provide cleaner icons and disable the
  Resources overlay by default in map view.
- [ ] **Research selector tooltips**: The research selector GUI needs tooltips showing
  what each technology will give you, including rich content support (icons, colored
  text, etc.).
- [ ] **Active unit designator**: The active unit designator has spinning chevrons
  around it that spin very fast briefly when the game window is restored after being
  minimized.
- [ ] **City view panes**: Clicking most GUI panes (Religion, Corporations, Espionage,
  etc.) currently does nothing when you are in city view; handle transitions or modal
  displays cleanly.
- [ ] **City view yields checkbox**: Wire up the city view GUI yields checkbox at the
  bottom of the screen (the yields checkbox currently works in map view only).
- [ ] **City view units**: Provide the ability to view and select units located inside
  the city within the city view GUI.
