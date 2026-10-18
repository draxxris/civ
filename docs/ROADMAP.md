# Roadmap & Mechanics Status

This document is the single canonical source of truth for project status: tracking what is done (implemented systems and mechanics) and what needs to be done (partial mechanics, deferred systems, and future milestones) across all game phases and modular subsystems.

For architectural design and core principles ("everything is a mod"), see [docs/DESIGN.md](DESIGN.md).  
For the hex adaptation specification and original porting plan, see [PLAN.md](../PLAN.md).  
For build instructions and test commands, see [README.md](../README.md).

---

## 1. Roadmap & Phase Overview

The project follows a phased roadmap bringing the full content of the source Civ4 extraction onto a deterministic hex grid.

| Phase | Focus | Status | Deliverables |
| :--- | :--- | :--- | :--- |
| **P0** | Toolchain & Housekeeping | **Done** | .NET 10 SDK, Godot 4.7.2 .NET (`godot-mono`), Git LFS art assets, clean headless test loop. |
| **P1** | Content Importer | **Done** | `tools/Civ.ContentImport` tree parser, effective registry merge, branding scrub, data sidecars. |
| **P2** | Core Contract & Foundations | **Done** | `HexGrid`, `Plot[]`, teams, generic registries, modifiers, ledgers, events, save/load, lockstep. |
| **P3** | Ancient Rules Slice | **Done** | Core feature mods: World, Cities, Research, Economy, Military, Government, Peoples. |
| **P4** | Godot Client & Presentation | **Done** | Engine-free client models (`Civ.Client.Core`), wrapped hex map, city/tech/civic UI, sound, icons. |
| **P5** | Deterministic AI | **Done** | `ai:standard` utility AI for research, civics, sliders, production, settlement, workers, and war. |
| **P6** | Full Content & Advanced Systems | **Done** | Multi-era closure, Religion, Great People, Trade, Diplomacy, Projects/Victory, Corporations, Espionage. |
| **P7** | Scripted Features & Revolution | **Done** | Events engine, Scripted Wonders, and Revolution/city instability simulation. |
| **P8** | Unit 3D Art & Animation | **Done** | 357 animated GLB models (`tools/art/convert_units.py`) with combat attack/death playback in engine. |
| **P9** | Hex Balance & Continuous 3D Terrain | **Done** | Civ 6-style continuous 3D blended terrain, seamless 1024×1024 textures, 3D nature scatter, balance tuning. |
| **P10+**| Polish, Multiplayer & Expansion | **Planned**| Networking lobby, historical scenarios, full naval/air AI, expanded diplomatic negotiations. |

---

## 2. Verification & Testing Standards

Per [README.md](../README.md), the repository establishes three separate test suites:

- **`mise run test`**: Default test suite. Fast execution; covers unit, contract, ruleset, client model, determinism, and save/reload tests. **Excludes E2E and Extractor tests.** Always run this by default during development.
- **`mise run test:e2e`**: Long-running end-to-end whole-game simulations and multi-turn soak runs verifying AI stability, desync-free lockstep, and victory resolution.
- **`mise run test:extractor`**: Source extraction and generated JSON content tests (`Civ.ContentImport.Tests`). Run when source assets or importer code change.
- **`dotnet test Civ.slnx`**: Runs all tests across all categories.
- **Headless smoke test**: `xvfb-run -a godot-mono --headless --path src/Civ.Godot --quit-after 200` verifies engine startup, shader compilation, and 200 frames of gameplay.

---

## 3. What Is Done (Implemented Systems & Mechanics)

Status reflects executable simulation rules, loaded definitions, and engine-free models.

### Foundation & Core Contracts
- **Phase contract & turn order**: Deterministic phase lifecycle execution (`docs/02-turn-lifecycle.md`) with lockstep handshake and state hash verification.
- **Setup & configuration**: Game setup selection, world sizes (`WorldSizeDef`), speeds (`GameSpeedDef`), handicaps (`HandicapDef`), eras (`EraDef`), player teams, and initial gold.
- **Hex tuning overrides**: `src/Mods/Foundation/data/hex-overrides.json` controls hex-specific balance, including `barbarianUnownedTilesMultiplier` and `aiMinimumCityGarrison`.
- **Dogfood mod isolation**: All features are standalone mods (`src/Mods/*`). `Civ.Core` and `Civ.Client.Core` hold no hard-coded mod IDs or content kinds. Custom test rulesets run without shipped mods.

### World & Map Generation
- **Map topology**: Rectangular odd-row hex map (`HexGrid`) with wrap-safe coordinate conversions, shortest-distance calculations, and optional east-west wrapping.
- **Procedural generation**: Continents, relief (peaks, hills, flatlands), climate zones, areas, lakes, and shared 6-direction river masks.
- **Features & Goody huts**: Climate-driven feature placement (forest, jungle, ice, floodplains, oasis) and goody huts with configurable densities.
- **Resource placement**: Physical resource placement respecting latitude, area size, terrain/feature requirements, and cluster rules.
- **Plot yield ledgers**: Natural and improved plot yields with cross-mod modifiers, river changes, hills/peak adjustments, and route yield additions.
- **Fresh water & irrigation**: Fresh water propagation from rivers, lakes, and oases; irrigation transit through farms and city plots.
- **Worker builds & improvements**: Worker assignments, shared build progress, feature removal production credits, improvement construction, route networks, and automated upgrades.

### Research
- **Prerequisite graph**: Strict checking of all/any technology prerequisites, obsoleted branches, and era gating.
- **Cost scaling**: Cost formulas scaled sequentially by handicap, world size, game speed, starting era, and team size.
- **Queues & overflow**: Team research targets, queues, progress tracking, and normalized carry-over overflow.
- **Tech discoveries**: `TechDiscovered` domain events, first-discovery free tech credits, free unit grants, and current-era milestone progression.

### Economy
- **Commerce sliders**: Gold, research, culture, and espionage allocation sliders with mod-configurable step increments and maximum percentages.
- **City commerce ledgers**: Full breakdown of raw commerce, modifier percentages, and direct commerce bonuses.
- **Maintenance & costs**: City distance, number-of-cities, and corporate maintenance, civic upkeep, military unit upkeep, outside-border supply, and inflation lines.
- **Second-order inflation**: Momentum-based inflation accumulator with turn offsets, handicap scaling, and civic modifier support.
- **Specialized production & hurry**: Infinite `economy:process` queue items (converting production directly to commerce/gold/research/culture) and `economy:hurry` payments (gold hurry with new-item surcharges and population whip with dynamic building anger modifiers, retroactive timer rescaling upon building completion, and hurry inflation decay).
- **Corporate maintenance integration**: Full branch maintenance equation factoring headquarter commerce, resources, world size, population, handicap, civic modifiers (`RealCorporationMaintenanceModifier`), pre-inflation division, and realistic corporation scaling.
- **Bilateral deal gold & trade transfers**: Per-turn trade deal gold integrated into `PlayerEconomy.Calculate` (`dealIncome`, `dealExpenses`, `NetGold`) with automated slider adjustments to prevent bankruptcy and net-payment settlement checks.
- **Targeted espionage spending**: `IPlayerEspionageSource` integration routing commerce to rival team allocations with read-only ledger forecasts and remainder point distribution.
- **Deficit strikes & forced disbanding**: Sustained deficits track `StrikeTurns`; multi-turn strikes trigger forced disbanding of lowest-value units via valuation scoring and role multipliers while protecting military happiness garrisons, halted once net gold reaches equilibrium.

### Cities
- **Founding & territory**: Settler founding checks, minimum hex spacing, terrain validity, starting buildings, and initial plot culture.
- **Work area**: Radius-2 ring (19 hexes) and culture-unlocked radius-3 ring (37 hexes), with deterministic governor tile weights and manual assignments.
- **Yield & commerce aggregation**: City-center minimums, worked plot yields, specialist yields, building changes and percentage modifiers.
- **Growth & food**: Sequential food threshold calculations, consumption, health penalties, fixed-point food waste, starvation prevention, and avoid-growth toggles.
- **Happiness & health**: Ledgers combining population, handicap base, connected resources, buildings, specialists, features, and military presence.
- **Production queues**: Append, prepend, remove, and pause commands; normalized capped production overflow; class-limited units, buildings, and world wonders.
- **Culture borders & assimilation**: Per-plot culture ledgers, hex distance falloff, tie-breaking ownership assignment, periodic culture decay halving on speed-scaled golden age intervals, and original civilization retention (`OriginalCivilization`) for unit/building override assimilation upon capture.
- **Conscription (Draft)**: `ICityConscriptService` and `ConscriptCityCommand` (`cities:conscript`) drafting highest-priority eligible units with population sacrifice, halved city production experience, zero initial movement, world-size-scaled player draft limits, and stacking draft anger.
- **Nationality anger**: Plot culture anger (`cities:culture_anger`) distinguishing foreign teams and wartime escalation (+50%) with same-team suppression, plus civic foreigner unhappiness (`government:foreigner_anger`).

### Military & Combat
- **Hex movement & navigation**: Deterministic A* pathfinding, fractional move points, terrain/feature costs, hill penalties, road/rail bonuses, and river crossing bridges.
- **Combat resolution**: Melee strength ledgers, firepower damage, first strikes and first-strike immunity, attacker withdrawal checks, collateral damage caps, and best-defender selection.
- **Combat odds preview**: Exact integer dynamic programming probability engine without RNG usage.
- **Promotions & experience**: Experience gain based on strength ratios, promotion prerequisite trees, healing bonuses, and great general point counters.
- **Unit actions**: Fortify (defense accumulation), sleep, sentry, pillage, upgrade in friendly territory, and disband.
- **Barbarians**: Fog-of-war unowned-land spawning scaled by hex density tuning, Deficit formula, tech parity adoption, and target-hunting AI.

### Government & Civics
- **Civic categories**: Multi-category civic options with one active civic per category.
- **Revolution & anarchy**: Anarchy duration calculated from base turns, city count, civic weights, trait exemptions (Spiritual), and minimum cooldown timers.
- **Civic modifier source**: Dynamic modifiers applying to yields, commerce, capital city bonuses, improvement yields, happiness, health, maintenance, and military training.
- **Draft civics & quotas**: Civic-enabled drafting permissions and per-turn quotas (`cities:max_conscript`) scaled by world size modifier.

### Peoples, Leaders & Traits
- **Identities**: Full catalog of leaders, civilizations, unique units, and unique buildings.
- **Trait modifier source**: City yields, commerce, happiness, health, wonder production bonuses, free promotions by combat type, and maintenance discounts.
- **Extra culture levels**: Additive culture level modifiers (`cities:extra_culture_level`) from leader traits (`TraitDef.ExtraCultureLevel`), civics, and civilizations boosting initial city founding culture and border range.
- **AI personality profiles**: Complete personality matrices (flavors, aggression, wonder preferences, civic weights) driving the AI mod.

### Advanced Systems (P6 / P7)
- **Religion**: 13 imported religions, holy city scoring, natural trade spread, missionaries, shrine construction, state religion adoption, and religious happiness/commerce.
- **Great People**: City progress points, class pools, deterministic births, settlement (free specialists), flavor-driven tech bulbing, building hurry, and Golden Ages.
- **Trade**: Domestic and foreign trade routes, slot capacity, connectivity verification, overseas profit bonuses, and shared civic bonuses.
- **Diplomacy**: Bilateral team contact discovery, attitude ledgers, peace treaties, open borders, defensive pacts, and multi-item trade deals (resources, gold, gold/turn, techs).
- **Projects & Victory Conditions**: Team/world projects; Conquest, Domination, Cultural, Space Race, and Time victories with victory resolution screens.
- **Corporations**: Great Person founding, branch spread (via gold or executives), headquarters protection, branch revenue, resource consumption, and maintenance.
- **Espionage**: Rival-team point allocation, passive visibility/investigation unlocks, and active missions (treasury theft, production sabotage, city revolt, tech theft).
- **Events**: Dynamic random event rolls, option choices with yield/gold/culture impacts, cooldown timers, and automated AI choices.
- **Scripted Wonders**: Special wonder abilities, Great Wall barbarian exclusion, periodic unit grants, and combat bonuses.
- **Revolution**: City instability ledgers (wellbeing, distance, civics, garrisons, golden age), warning/mutiny/rebellion states, military/gold suppression, and rebel unit spawns.

### Computer Players (`ai:standard`)
- **Deterministic AI**: Operates strictly via validated player commands using simulation RNG; zero hidden game state.
- **Empire management**: Automated research selection, civic revolutions, commerce slider adjustments, production queue management, and citizen tile allocation.
- **Expansion & military**: Settler scouting and escorted founding; worker build planning and road corridors; city garrison retention, threat-aware stack movement, and combat odds gating.

### Client, Presentation & 3D Art
- **Engine-free client (`Civ.Client.Core`)**: Complete screen models for setup, city management, research tree, civics, diplomacy, victory, corporations, espionage, events, pedia, and save/load.
- **Godot 4.7.2 views (`Civ.Godot`)**: Native 3D viewport, camera controls, hex prism highlights, audio playback, and responsive HUD panels.
- **Continuous 3D blended terrain**:
  - Seamless 1024×1024 terrain textures with height in alpha.
  - Procedural terrain mesher generating continuous height and relief transitions.
  - `terrain.gdshader` and `water.gdshader` with shore foam, depth transparency, and slope cliff rock.
  - Slope ray-marching bisection cursor picking.
- **3D nature scatter**: Quaternius CC0 models converted to compact GLBs, jittered Poisson-like lattice planning, and GPU MultiMesh instancing.
- **3D unit art & combat animations**: 357 converted unit models with `idle`, `run`, `fortify`, `attack`, `hurt`, and `die` animations rendered on the world map.

---

## 4. What Needs to Be Done (Pending & Deferred Work)

### Foundation & Setup
- [ ] **Advanced start mode**: Allow purchasing cities, units, buildings, and technologies before turn 1.
- [ ] **Era & handicap start scaling**: Implement `StartingUnitMultiplier`, `StartingDefenseUnits`, `StartingWorkerUnits`, and `StartingExploreUnits` for advanced era starts.
- [ ] **Per-player handicap options**: Allow human and individual AI players to have distinct difficulty levels in the same session.
- [ ] **Calendar progression**: Add non-turn timeline display (months, seasons, variable years per turn by era/speed).
- [ ] **Default civics fallback**: Auto-fill missing civic selections for incomplete custom civilization declarations.

### World & Map Generation
- [ ] **Dynamic feature growth & decay**: Implement forest expansion and jungle spread onto adjacent plots.
- [ ] **Resource discovery & depletion**: Random chance for new resources to appear on worked mines or existing deposits to exhaust.
- [ ] **Pillage command**: Implement unit pillaging of improvements and routes with gold rewards and tile repair builds.
- [ ] **Route upgrade discounts & peak build modifiers**: Time discounts when replacing roads with railroads; terrain modifiers for peak tunneling.
- [ ] **Environmental changes & advanced climate scripts**: Implement global warming tile degradation and custom map generator scripts.
- [ ] **Terrain sight occlusion**: Block unit sight lines behind mountains, peaks, and dense forests; implement hidden-state fog snapshots.
- [ ] **Foreign trade network transit**: Resource transit through foreign territory without explicit open borders treaties, blockades, and vicinity resource predicates.

### Research
- [ ] **Tech diffusion / welfare**: Research discounts on technologies already known by multiple contacted civilizations.
- [ ] **Timeline research cost scaling**: Era-based dynamic cost scaling based on game turn versus historical date.
- [ ] **Tech trading gates**: Civic or technology restrictions controlling when technology items can be exchanged in diplomacy.
- [ ] **Player-specific tech queues**: Separate tech choices within shared multiplayer teams.

### Economy
- [x] **Forced military disbanding**: Automatically disband lowest-value military units when treasury drops to zero with sustained deficit across turn boundaries.
- [x] **Targeted espionage spending**: Direct espionage commerce allocations against specific rival civilizations rather than aggregate pooling.
- [x] **Corporate cost integration**: Full unification of corporate branch expenses into overall empire city maintenance ledgers.
- [x] **Dynamic hurry anger**: Dynamic building hurry anger modifiers, retroactive timer rescaling, and hurry-driven inflation acceleration and decay.
- [x] **Trade deal income in treasury**: Real-time per-turn gold flows directly reflecting bilateral trade deals.

### Cities
- [ ] **Building power grid**: Implement power plant requirements, clean energy distribution, and building power deactivation when unpowered.
- [ ] **Free building propagation**: Mechanics for national wonders or civics that grant free buildings in all cities (e.g., Monument in every city).
- [ ] **Building replacement effects**: Automatic obsolescence/replacement of lower-tier buildings by higher-tier counterparts.
- [ ] **City conquest options**: Interactive conquest choice dialog (annex, puppet, raze); capture gold calculations; building survival rolls; capital relocation logic.
- [ ] **Excess production gold conversion**: Automatically convert surplus production above the overflow cap into treasury gold.
- [x] **Conscription (Draft)**: Draft military units from city population under authorized civics with temporary draft anger.
- [ ] **Hostile culture spread obstruction**: Restrict cultural spread across enemy borders or impassable natural barriers.

### Military & Combat
- [ ] **Naval unit spawns & naval AI**: Barbarian galleys/pirates; AI naval pathfinding, exploration, and coastal patrol.
- [ ] **Cargo transports & amphibious warfare**: Unit loading into naval transports, ocean transit, and amphibious landings.
- [ ] **Air combat missions**: Aircraft rebasing, aerial reconnaissance, combat air patrol (interception), and air bombardment.
- [ ] **Artillery bombardment & ranged attacks**: Siege weapons reducing city defense bonuses; ranged fire without mutual melee damage.
- [ ] **Stealth & invisibility**: Submarine/spy invisibility with anti-submarine detection radius.
- [ ] **Flanking & defender withdrawal**: Cavalry flanking attacks destroying enemy siege units; defender tactical withdrawal chances.
- [ ] **Strategic military AI campaigns**: Multi-stack army coordination, targeted theater staging, and defensive reinforcement lines.

### Government & Peoples
- [x] **Cultural assimilation**: Gradual conversion of foreign nationality in captured cities over time.
- [x] **Draft civics**: Full support for civic-enabled conscription.
- [x] **Extra culture levels**: Support for civilization-specific or civic-specific maximum cultural tier expansion.

### Religion & Great People
- [ ] **Foreign trade religion spread**: Spread religion along international trade routes to contacted foreign cities.
- [ ] **Religion decay & attrition**: Gradual loss of minority religions in cities with low religious pressure.
- [ ] **Inquisitions**: Religious units capable of purging non-state religions from friendly cities.
- [ ] **Combat Great Generals**: Great General emergence driven by earned military combat experience points.
- [ ] **Trade missions**: Great Merchant mission granting instant treasury gold upon reaching distant foreign capitals.
- [ ] **AI Great Person valuation**: Enhanced AI weighting between immediate tech bulbing, city settlement, or Golden Age triggers.

### Trade & Diplomacy
- [ ] **Naval blockades**: Military ships blockading coastal cities and severing sea trade routes.
- [ ] **AI diplomatic negotiations**: AI-initiated treaty proposals, counter-offers, peace demands, and gift requests.
- [ ] **Diplomatic vote resolutions**: United Nations and Apostolic Palace voting sessions (world peace, global civics, diplomatic victory).
- [ ] **Embargoes & sanctions**: Multilateral agreements to prohibit trade with targeted civilizations.

### Espionage & UI
- [ ] **Full espionage intelligence view**: In-game screens showing rival city demographics, production queues, and treasury reserves.
- [ ] **Counter-espionage missions**: Active missions reducing foreign spy success chances within domestic borders.
- [ ] **Dedicated status panels**: Global HUD panels for empire-wide Revolution unrest and Scripted Wonder active timers.
- [ ] **Map overlays**: Toggleable visual layers for tile yields, resource badges, hex grid lines, and cultural borders.

### Future Roadmap Milestones (P10+)
- [ ] **P10 – Multiplayer Lobby & Wire Transport**: Peer-to-peer and dedicated server transport over `Civ.Net` with match setup, lobby chat, and reconnect recovery.
- [ ] **P11 – Historical Scenarios & Earth Maps**: Pre-scripted historical earth maps, civilization start locations, and custom scenario loaders.
- [ ] **P12 – Gigantic Map Optimization**: Sparse 64-bit spatial indexing and multi-threaded turn phase processing for smooth play on gigantic (160×100+) maps.
