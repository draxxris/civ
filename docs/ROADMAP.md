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
- **Calendar progression**: Deterministic calendar date calculation (`GameCalendar`) driven by mod speed schedules (Normal 1200 turns, Marathon 2400 turns) and start year 6000 BCE (`START_YEAR=-6000`). Top bar displays the calendar date alongside the turn counter.
- **Setup & configuration**: Game setup selection, game options checklist (`SimulationSettings.GameOptions`, `Simulation.IsOptionEnabled`, Godot `SetupScreen`), world sizes (`WorldSizeDef`), speeds (`GameSpeedDef`), handicaps (`HandicapDef`), eras (`EraDef`), player teams, and initial gold.
- **Hex tuning overrides**: `src/Mods/Foundation/data/hex-overrides.json` controls hex-specific balance, including `barbarianUnownedTilesMultiplier` and `aiMinimumCityGarrison`.
- **Dogfood mod isolation**: All features are standalone mods (`src/Mods/*`). `Civ.Core` and `Civ.Client.Core` hold no hard-coded mod IDs or content kinds. Custom test rulesets run without shipped mods.

### World & Map Generation
- **Map topology**: Rectangular odd-row hex map (`HexGrid`) with wrap-safe coordinate conversions, shortest-distance calculations, and optional east-west wrapping.
- **Procedural generation**: Continents, relief (peaks, hills, flatlands), climate zones, areas, lakes, and shared 6-direction river masks.
- **Features & Goody huts**: Climate-driven feature placement (forest, jungle, ice, floodplains, oasis) and goody huts with configurable densities.
- **Resource placement**: Physical resource placement respecting latitude, area size, terrain/feature requirements, and cluster rules. The flatlands/hills/peaks test applies to land plots only, so sea resources (fish, clams, crabs, pearls, shrimp, whales, methane, offshore oil) spawn in the water.
- **Plot yield ledgers**: Natural and improved plot yields with cross-mod modifiers, river changes, hills/peak adjustments, and route yield additions.
- **Fresh water & irrigation**: Fresh water propagation from rivers, lakes, and oases; irrigation transit through farms and city plots.
- **Worker builds & improvements**: Worker assignments, shared build progress, feature removal production credits, improvement construction, route networks, automated upgrades, peak build modifiers, and route upgrade discounts.
- **Pillage commands & tile repairs**: Unit pillaging of improvements and routes with cargo and domain checks. Enemy pillage awards gold and steps improvements down, while workers prioritize repairing ruins and severed routes.
- **Dynamic feature growth & decay**: Per-turn feature disappearance rolls and adjacent feature growth and spread rolls scaled by game speed, feature growth modifiers, and route penalties.
- **Resource discovery & default-off depletion**: Per-turn resource discovery rolls on worked improved bonus-free tiles. Option-gated mine exhaustion creates depleted mines, and deposit depletion uses the reference zero-consumption path.
- **Global warming environmental changes**: Global warming defense from map features and health civics balances city pollution. Unchecked pollution drives stepped feature and terrain degradation.
- **Line-of-sight occlusion & hidden-state fog snapshots**: Elevation and feature line-of-sight occlusion with high-ground vantage range and aerial bypass. Per-team fog-of-war plot snapshots preserve last-observed terrain, features, improvements, and ownership.
- **Foreign trade network transit & vicinity resources**: Physical resource and city networks transit revealed peaceful foreign territory without Open Borders treaties. Naval blockades sever water networks, and vicinity bonus checks enforce local connectivity.

### Research
- **Prerequisite graph**: Strict checking of all/any technology prerequisites, obsoleted branches, and era gating.
- **Sequential integer cost scaling**: Deterministic integer truncation stages scaling base technology cost sequentially by handicap, world size, game speed, starting era, team size, and timeline multipliers.
- **Diffusion & welfare rate modifiers**: Contacted teams knowing a technology grant diffusion bonuses using an eligible living non-barbarian team denominator; welfare bonuses evaluate the team leader's player total-score contract. These adjust research output rate (`RateModifier`) rather than base technology cost.
- **Realistic timescale (timeline scaling)**: Option-gated dynamic cost scaling comparing elapsed turns against expected era transition points and team leader era/technologies, without referencing historical calendar dates.
- **Tech trading gates & theft eligibility**: Technology trading in diplomacy requires enabling technologies (`TechTrading` unlock flag) and option checks. Espionage tech theft uses separate, independent eligibility rules.
- **Option defaults & brokering limitation**: Diffusion and tech trading enabled by default; realistic timescale and no-tech-brokering disabled by default. The selected no-tech-brokering option remains unsupported because technology acquisition provenance is not yet tracked.
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
- **Culture borders & assimilation**: Per-plot culture ledgers, path-based cultural distance falloff, tie-breaking ownership assignment, periodic culture decay halving on speed-scaled golden age intervals, and original civilization retention (`OriginalCivilization`) for unit/building override assimilation upon capture.
- **Conscription (Draft)**: `ICityConscriptService` and `ConscriptCityCommand` (`cities:conscript`) drafting highest-priority eligible units with population sacrifice, halved city production experience, zero initial movement, world-size-scaled player draft limits, and stacking draft anger.
- **Nationality anger**: Plot culture anger (`cities:culture_anger`) distinguishing foreign teams and wartime escalation (+50%) with same-team suppression, plus civic foreigner unhappiness (`government:foreigner_anger`).
- **Building power grid**: `CityPower` tracks local and area sources (`ProducesPower`, `PowerBonus`, `AreaCleanPower`, `DirtyPower`). Power deactivates `RequiresPower` buildings when missing. Clean power blocks `NoPower` construction. Powered cities receive `PowerYieldModifiers` and apply health modifiers.
- **Dynamic free building propagation**: Dynamic calculation supplies free buildings from team buildings (`FreeBuilding`) and completed wonders (`EmpireFreeBuilding`). Grants map through civilization overrides without mutating constructed city buildings.
- **Non-destructive building replacement**: Replacement preserves replaced structures in city storage. Active replacers (`ReplaceBuildings`) dynamically suppress lower-tier buildings. Suppressed buildings reactivate if the replacer becomes obsolete or is removed. Construction enforces `ExtendsBuildingClass` prerequisites.
- **City conquest mechanics**: Capture rolls building survival, awards capture gold, and triggers size-1 auto-raze. Capturing or razing capitals relocates victim capitals automatically. Occupation disorder timers delay productivity. Interactive conquest commands support `Annex`, `Puppet`, and `Raze` choices. Puppet cities reduce maintenance and citizen unhappiness.
- **Excess production overflow gold conversion**: Production above the overflow cap converts into treasury gold upon order completion. Conversion scales item modifiers back to city base yield modifiers.
- **Path-based culture spread obstruction**: Shortest-path culture spread blocks propagation across impassable barriers and hostile borders. Optional realistic culture adds movement penalties for rough terrain, features, and river distances.

### Military & Combat
- **Hex movement & navigation**: Deterministic A* pathfinding, fractional move points, terrain/feature costs, hill penalties, road/rail bonuses, and river crossing bridges.
- **Combat resolution**: Melee strength ledgers, firepower damage, first strikes and first-strike immunity, attacker withdrawal checks, collateral damage caps, and best-defender selection.
- **Combat odds preview**: Exact integer dynamic programming probability engine without RNG usage.
- **Promotions & experience**: Experience gain based on strength ratios, promotion prerequisite trees, healing bonuses, and great general point counters.
- **Unit actions**: Fortify (defense accumulation), sleep, sentry, pillage, upgrade in friendly territory, and disband.
- **Barbarians & naval spawns**: Fog-of-war unowned-land spawning scaled by hex density tuning; coastal water area barbarian naval spawns governed by handicap divisors, non-barbarian water unit limits, lake exclusions, and pirate pillaging AI.
- **Cargo & amphibious warfare**: Unit loading into transports, capacity limits with promotion modifiers, shared ocean transit, cargo loss on ship destruction, and amphibious assaults from sea with combat penalties mitigated by amphibious training.
- **Air combat missions & interception**: Rebase, recon, air strike (stack damage capped at combat limit), air bomb (city defense reduction and improvement destruction), and combat air patrol (CAP interception with evasion checks and dogfight rounds).
- **Artillery bombardment & ranged attacks**: Siege bombardment degrading city defenses, and ranged support strikes without mutual melee counterattack damage.
- **City defense damage & recovery**: City defense damage accumulator reducing defense modifiers, with turn-by-turn recovery for undamaged cities.
- **Stealth & invisibility**: Submarine/spy invisibility masking presence from enemy vision, avoiding sentry triggers and path blocking until revealed by dedicated detector units.
- **Flanking strikes & defender withdrawal**: Mounted unit flanking attacks damaging or destroying defending siege stacks; tactical defender withdrawal saving units upon defeat.
- **Strategic military campaigns**: Multi-theater stack staging partitioned by landmass (`AreaId`), combined arms support fire sequencing before melee assault, air theater management, and threat-differential defensive reinforcement lines.

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
- **Religion**: 13 imported religions, holy city scoring, natural trade spread, international trade route spread across open borders, religion decay and attrition for unprotected minorities, civic-gated inquisitions (purging non-state faiths/temples, hurry anger, holy city relocation/extinction, and diplomatic memories), missionaries, shrine construction, state religion adoption, and religious happiness/commerce.
- **Great People**: City progress points, class pools, deterministic births, combat Great General emergence from combat experience, warlord attachment (`military:leader` promotions and stack experience sharing), Military Academy/Academy construction, Great Merchant trade missions to foreign cities for gold, settlement (free specialists), flavor-driven tech bulbing, building hurry, and Golden Ages.
- **Trade**: Domestic and foreign trade routes, slot capacity, connectivity verification, overseas profit bonuses, and shared civic bonuses. Player naval blockades sever water trade networks and coastal trade, while autonomous AI blockade planning remains absent.
- **Diplomacy**: Bilateral team contact discovery, attitude ledgers, peace treaties, open borders, defensive pacts, and multi-item trade deals (resources, gold, gold/turn, techs).
- **Projects & Victory Conditions**: Team/world projects; Conquest, Domination, Cultural, Space Race, and Time victories with victory resolution screens.
- **Corporations**: Great Person founding, branch spread (via gold or executives), headquarters protection, branch revenue, resource consumption, and maintenance.
- **Espionage & Intelligence**: Rival-team point allocation, bilateral spending ratios, passive visibility/investigation unlocks, full rival intelligence screen (demographics, city yields/buildings, production queues, treasury reserves) with permission filtering and immediate revocation, and active missions (counter-espionage with directed cost penalties scaling from a 200% baseline by supported interception, treasury theft, production sabotage, city revolt, tech theft).
- **Events**: Dynamic random event rolls, option choices with yield/gold/culture impacts, cooldown timers, and automated AI choices.
- **Scripted Wonders**: Special wonder abilities, Great Wall barbarian exclusion, periodic unit grants, and combat bonuses.
- **Revolution**: City instability ledgers (wellbeing, distance, civics, garrisons, golden age), warning/mutiny/rebellion states, military/gold suppression, and rebel unit spawns.

### Computer Players (`ai:standard`)
- **Deterministic AI**: Operates strictly via validated player commands using simulation RNG; zero hidden game state.
- **Empire management**: Automated research selection, civic revolutions, commerce slider adjustments, production queue management, citizen tile allocation, and Great Person valuation (weighing Golden Ages vs. tech bulbing vs. settlement).
- **Expansion & military**: Settler scouting and escorted founding; worker build planning and road corridors; city garrison retention, multi-theater campaign staging by `AreaId`, combined arms pre-assault bombardment and ranged support fire, naval amphibious sealift, air rebasing and bombing missions, threat-differential reinforcement corridors, and combat odds gating.
- **Religious management**: Missionary spread, state religion adoption, and inquisition purging based on city instability and state church policies.

### Client, Presentation & 3D Art
- **Engine-free client (`Civ.Client.Core`)**: Complete screen models for setup, city management, research tree, civics, diplomacy, victory, corporations, espionage, events, pedia, and save/load. Setup screen options and perceived fog snapshots prevent unseen map mutation leaks.
- **Godot 4.7.2 views (`Civ.Godot`)**: Native 3D viewport, camera controls, hex prism highlights, audio playback, and responsive HUD panels.
- **HUD in-place invalidation (`Civ.Godot`)**: The top bar and left panel are built once and updated in place instead of rebuilding the widget tree on every map click; see [docs/OPTIMIZE.md](OPTIMIZE.md#phase-7-hud-invalidation-on-clicks).
- **3D resource models**: Every map resource has a procedural low-poly model (`art/models/<id>.glb`, built by `tools/art/improvement_models.py`) standing on a spot off the tile centre that avoids rivers and the wrong side of the shore. Scatter props keep the spot clear. Manufactured goods have no model.
- **Map overlays & local toggles**: Independent visual toggles for tile yield badges, resource badges, hex grid lines, and cultural borders. Managed locally in `GamePreferences` without affecting simulation state or hashes, strictly respecting fog-of-war perceived snapshots.
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
- [x] **Calendar progression**: Non-turn timeline display with mod speed schedules (Normal 1200, Marathon 2400) starting at 6000 BCE.
- [ ] **Default civics fallback**: Auto-fill missing civic selections for incomplete custom civilization declarations.
- [ ] **Remaining game option flags**: Implement simulation rules for the unimplemented game option definitions in src/Mods/Foundation/data/game-option.json.

### World & Map Generation
- [x] **Dynamic feature growth & decay**: Implement forest expansion and jungle spread onto adjacent plots.
- [x] **Resource discovery & depletion**: Random chance for new resources to appear on worked mines or existing deposits to exhaust.
- [x] **Pillage command**: Implement unit pillaging of improvements and routes with gold rewards and tile repair builds.
- [x] **Route upgrade discounts & peak build modifiers**: Time discounts when replacing roads with railroads; terrain modifiers for peak tunneling.
- [x] **Environmental changes**: Implement global warming tile degradation.
- [x] **Terrain sight occlusion**: Block unit sight lines behind mountains, peaks, and dense forests; implement hidden-state fog snapshots.
- [x] **Foreign trade network transit**: Resource transit through foreign territory without explicit open borders treaties, blockades, and vicinity resource predicates.
- [ ] **Custom map generator scripts**: Support external or custom procedural map script definitions.

### Research
- [x] **Tech diffusion / welfare**: Contacted-team diffusion and score welfare rate bonuses adjusting science output.
- [x] **Timeline research cost scaling**: Era-based dynamic cost scaling comparing elapsed turns against expected era progression.
- [x] **Tech trading gates**: Diplomatic technology trade gates gated by enabling technologies and game options; independent espionage theft.
- [ ] **Player-specific tech queues**: Separate tech choices within shared multiplayer teams.

### Economy
- [x] **Forced military disbanding**: Automatically disband lowest-value military units when treasury drops to zero with sustained deficit across turn boundaries.
- [x] **Targeted espionage spending**: Direct espionage commerce allocations against specific rival civilizations rather than aggregate pooling.
- [x] **Corporate cost integration**: Full unification of corporate branch expenses into overall empire city maintenance ledgers.
- [x] **Dynamic hurry anger**: Dynamic building hurry anger modifiers, retroactive timer rescaling, and hurry-driven inflation acceleration and decay.
- [x] **Trade deal income in treasury**: Real-time per-turn gold flows directly reflecting bilateral trade deals.

### Cities
- [x] **Building power grid**: Implement power plant requirements, clean energy distribution, and building power deactivation when unpowered.
- [x] **Free building propagation**: Mechanics for national wonders or civics that grant free buildings in all cities (e.g., Monument in every city).
- [x] **Building replacement effects**: Automatic obsolescence/replacement of lower-tier buildings by higher-tier counterparts.
- [x] **City conquest options**: Interactive conquest choice dialog (annex, puppet, raze); capture gold calculations; building survival rolls; capital relocation logic.
- [x] **Excess production gold conversion**: Automatically convert surplus production above the overflow cap into treasury gold.
- [x] **Conscription (Draft)**: Draft military units from city population under authorized civics with temporary draft anger.
- [x] **Hostile culture spread obstruction**: Restrict cultural spread across enemy borders or impassable natural barriers.

### Military & Combat
- [x] **Naval unit spawns & naval AI**: Barbarian galleys/pirates; AI naval pathfinding, exploration, and coastal patrol.
- [x] **Cargo transports & amphibious warfare**: Unit loading into naval transports, ocean transit, and amphibious landings.
- [x] **Air combat missions**: Aircraft rebasing, aerial reconnaissance, combat air patrol (interception), and air bombardment.
- [x] **Artillery bombardment & ranged attacks**: Siege weapons reducing city defense bonuses; ranged fire without mutual melee damage.
- [x] **Stealth & invisibility**: Submarine/spy invisibility with anti-submarine detection radius.
- [x] **Flanking & defender withdrawal**: Cavalry flanking attacks destroying enemy siege units; defender tactical withdrawal chances.
- [x] **Strategic military AI campaigns**: Multi-stack army coordination, targeted theater staging, and defensive reinforcement lines.

### Government & Peoples
- [x] **Cultural assimilation**: Gradual conversion of foreign nationality in captured cities over time.
- [x] **Draft civics**: Full support for civic-enabled conscription.
- [x] **Extra culture levels**: Support for civilization-specific or civic-specific maximum cultural tier expansion.

### Religion & Great People
- [x] **Foreign trade religion spread**: Spread religion along international trade routes to contacted foreign cities.
- [x] **Religion decay & attrition**: Gradual loss of minority religions in cities with low religious pressure.
- [x] **Inquisitions**: Religious units capable of purging non-state religions from friendly cities.
- [x] **Combat Great Generals**: Great General emergence driven by earned military combat experience points.
- [x] **Trade missions**: Great Merchant mission granting instant treasury gold upon reaching distant foreign capitals.
- [x] **AI Great Person valuation**: Enhanced AI weighting between immediate tech bulbing, city settlement, or Golden Age triggers.

### Trade & Diplomacy
- [ ] **AI naval blockade planning**: Autonomous AI orders to deploy and maintain naval blockades (player blockade commands, water network severing, coastal city work blocking, and trade route cancellation are implemented).
- [ ] **AI diplomatic negotiations**: AI-initiated treaty proposals, counter-offers, peace demands, and gift requests.
- [ ] **Diplomatic vote resolutions**: United Nations and Apostolic Palace voting sessions (world peace, global civics, diplomatic victory).
- [ ] **Embargoes & sanctions**: Multilateral agreements to prohibit trade with targeted civilizations.

### Espionage & UI
- [x] **Full espionage intelligence view**: In-game screens showing permission-filtered rival city demographics, production queues, and treasury reserves.
- [x] **Counter-espionage missions**: Active missions increasing rival mission costs via a directed cost penalty (200% baseline plus 5x interception percent).
- [ ] **Dedicated status panels**: Global HUD panels for empire-wide Revolution unrest and Scripted Wonder active timers.
- [x] **Map overlays**: Toggleable visual layers for tile yields, resource badges, hex grid lines, and cultural borders with fog-safe snapshots.

### Future Roadmap Milestones (P10+)
- [ ] **P10 – Multiplayer Lobby & Wire Transport**: Peer-to-peer and dedicated server transport over `Civ.Net` with match setup, lobby chat, and reconnect recovery.
- [ ] **P11 – Historical Scenarios & Earth Maps**: Pre-scripted historical earth maps, civilization start locations, and custom scenario loaders.
- [ ] **P12 – Gigantic Map Optimization**: Sparse 64-bit spatial indexing and multi-threaded turn phase processing for smooth play on gigantic (160×100+) maps.

---

## 5. Residual Notes & Verification Status

The following observations, decisions, and verification records are preserved:

* **Eligible-Team Denominator (Barbarians Excluded)**:
  * Severity: None. Reachability: Normal play.
  * The research diffusion denominator counts eligible living non-barbarian teams only. Barbarian players and their cities join neither the denominator nor the contacted set, the weighted holder numerator, or the welfare baseline. The exclusion holds on the diffusion path and on the no-tech-diffusion fallback, which shares the same team set.
  * Decision: the reference mod divides by eligible alive teams including barbarians. Exclusion is intentional, because shared barbarian technologies would otherwise dilute every civilization's diffusion rate. No parity reconciliation is planned, and the `Diffusion_excludes_dead_teams_...` and `Diffusion_excludes_barbarian_teams_...` tests lock the behavior.
* **Score Contract in Welfare**:
  * Severity: None. Reachability: Late-game play when score disparities widen.
  * Welfare catch-up compares the team leader's personal score against the highest score of every other living non-barbarian player. A teammate counts as a rival, so a lagging member is helped even inside a strong alliance. The existing score contract is used; no separate normalized curve exists.
  * Decision: the existing contract is intentional. A normalized curve would make an invisible research modifier disagree with the score the game displays and uses for time victory. Research is pooled per team, so the bonus applies to the whole team, and the team-level technology gate runs before the score ratio, so an alliance cannot farm welfare unless the team is genuinely behind in technology.
  * Revisit trigger: team play becoming player-facing (P10 lobby), which makes the choice between the lowest-id leader and the weakest living member observable.
* **Commander and Ground-Health Interception Inputs**:
  * Severity: None. Reachability: Unreachable with shipped play.
  * Counter-espionage protection is `200 + 5 x the performing spy's interception percent`. The reference also adds an attached commander unit's interception bonus and, because its "Better Air Interception" mod setting defaults to on, scales every non-air unit's interception by remaining health.
  * Decision: neither input can change a result here. This engine has no commander unit pool; a great-general style bonus is copied onto the led unit as an ordinary promotion, which the interception modifier already counts. Health scaling applies to aircraft only, and spies have no combat strength, so nothing can wound one and the reference default would produce the same number for every reachable state.
  * Revisit trigger: a commander unit system, a health-scaling rule for non-air interception, or any damage source that can reach a spy.
* **Malformed-Content Guards**:
  * Severity: Low. Reachability: Unreachable with shipped content; reachable with invalid custom content declarations.
  * Missing technology identifiers in unlock tables or missing bonus identifiers in sight memory throw on lookup.
  * Future fix need: add fallback validation and default placeholders during content loading.
