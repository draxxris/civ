# Mechanics status

Status describes executable rules, not imported data. Feature mods load definition sidecars and executable systems. AI, GreatPeople, and Trade supply additional rules.
## Foundation

- **Implemented:** phase contract, setup selection, source plot counts, speed/handicap/era validation, team creation, starting gold.
  Fields: `WorldSizeDef.GridWidth/GridHeight`, `EraDef.StartingGold`, `HandicapDef.Gold`, and `HexOverridesDef`.
- **Partial:** era and handicap grants. Fields: `StartingUnitMultiplier`, `StartingDefenseUnits`, `StartingWorkerUnits`,
  `StartingExploreUnits`, their AI counterparts, `StartingTechs`, and `AiStartingTechs`.
  Earlier-era technology grants, advanced start, calendar progression, and per-player difficulty remain absent.
- **Not started:** default civics for civilizations with incomplete civic declarations and global properties.
## World

- **Implemented:** continents, relief, climate, shared river masks, areas, lakes, feature placement and goody huts.
  Fields: terrain `IsWater/Impassable`, feature `Terrains/Appearance/RequiresFlatlands/RequiresRiver/NoRiver/NoCoast/NoAdjacent`,
  improvement `Goody/TilesPerGoody/GoodyRange`, era `NoGoodies`, and the hex tuning fields.
  Forest, jungle and ice use climate rules from the hand-authored sidecar where imported appearance is zero.
- **Implemented:** physical resource placement.
  Fields: `Terrains/Features/FeatureTerrains`, `Flatlands/Hills/Peaks`, latitude bounds, `NoRiverSide/MinAreaSize`,
  `ConstAppearance/AppearanceRandomRanges/TilesPer/Player/PlacementOrder`, `Area/MinLandPercent`,
  `MinimumPerWorld/MaximumPerWorld`, `Unique/GroupRange/GroupRand`, and `BonusClassDef.Unique`.
  Clusters intentionally bypass unique spacing, as in the source generator.
- **Implemented:** natural and improved plot yield ledgers, with cross-mod plot modifiers.
  Fields: terrain/feature yields and river changes, `YieldDef.HillsChange/PeakChange/LakeChange`,
  bonus `YieldChanges/RevealTech/ObsoleteTech`, improvement `YieldChanges/BonusEffects.Yields`,
  `RiverSideYieldChange/IrrigatedYieldChange/RouteYieldChanges/TechYieldChanges`, and route `Yields`.
- **Implemented:** fresh water and irrigation carriers.
  Fields: feature `AddsFreshWater`, improvement `CarriesIrrigation/ActsAsCity`, technology `Irrigation`.
- **Partial:** resource networks and the `bonus` evaluator.
  Fields: bonus reveal/trade/obsolescence technologies, improvement `BonusEffects.Connects/ActsAsCity`,
  technology `RiverTrade/TradeTerrains`, plot ownership, exploration, routes, and shared river edges.
  Foreign permissions, blockades, corporation-aware resource accounting beyond diplomatic allocations, and vicinity predicates remain absent.
- **Implemented:** worker assignment, shared progress, feature removal events, route/improvement creation and direct upgrades.
  Fields: build requirements, `Cost/Time/ObsoleteTech/FeatureEffects/ImprovementType/RouteType/TerrainChange/FeatureChange/Kill`,
  terrain `BuildModifier`, unit `Builds/WorkRate`, technology `WorkerSpeedModifier/IgnoreIrrigation/CanPassPeaks`,
  handicap `AiWorkRateModifier`, speed/era `BuildPercent/ImprovementPercent`, speed `FeatureProductionPercent`,
  technology `FeatureProductionModifier`, improvement validity flags, `PrereqTech/PrereqNatureYields/UpgradeTime/ImprovementUpgrade`,
  and route `SeaTunnel/Value`.
- **Partial:** start evaluation and normalization. Fields: terrain yields/founding flags, feature yields/`NoCity`, relief yields, and hex start weights.
  Spacing is strict. Terrain normalization improves weak starts without moving resources. Fertility balance needs more seed sweeps.
- **Partial:** sight. Fields: unit `NoRevealMap`, promotion `VisibilityChange`, team ids, and hex sight radii.
  Military must refresh sight after movement. Terrain occlusion and hidden-state snapshots remain absent.
- **Not started:** feature growth/decay, resource discovery/depletion, pillage commands, route-upgrade time discounts,
  peak build-time modifiers, city-center yield minima, advanced climate scripts, and environmental changes.

## Research

- **Implemented:** known-tech requirements, all/any prerequisites, disabled/known gates, team targets, queues, progress, and normalized overflow.
  Commands: `research:set_research`, including a single free-tech selection. Repeatable technologies retain completion counts.
- **Implemented:** sequential cost scaling by handicap, world, speed, starting era, and team size, including the large-cost guard.
  Known contacted teams increase the research rate, not the cost. The contact set is empty until another rule supplies contacts.
- **Implemented:** `TechDiscovered`, first-discovery free-tech credits and unit grants, and current-era updates through the Cities event handler.
  Other mods use the discovery event for obsolescence. Starting technologies remain setup grants.
- **Partial:** queues belong to teams, not individual players. Overflow carries to the next contribution instead of completing multiple technologies immediately.
  Era order uses `EraDef.StartPercent`. Flexible difficulty, timeline scaling, diffusion welfare, and traded-tech permissions remain absent. Religion supplies founding rewards through discovery events.

## Economy

- **Implemented:** `IPlayerEconomyService`, city commerce ledgers, and `economy:set_commerce_rates` with imported step size and percentage limits.
  Gold receives allocation residuals. Direct commerce bypasses sliders. Positive and negative commerce use the source modifier order.
  Research and culture cannot become negative. Disorder suppresses all city commerce.
- **Implemented:** final culture through `ICityCultureSource`, research through `IPlayerResearchSource`, and saved aggregate espionage output.
  Gold settlement stores a commerce snapshot before research discoveries and city production.
- **Implemented:** city maintenance, civic upkeep, unit upkeep, outside-unit supply, and separate inflation ledger lines.
  Costs retain sequential handicap and speed scaling. Civic upkeep includes its own inflation multiplication, as in the source.
- **Implemented:** second-order inflation with a saved momentum accumulator, turn offset, effect window, handicap, speed, and civic modifiers.
  The imported profile disables inflation through `DISABLE_INFLATION`. Formula tests use a mod override to enable it.
- **Implemented:** infinite `economy:process` city queue items convert current production after commerce modifiers without consuming stored overflow.
  `economy:hurry` supports civic-gated gold and population payments, new-item surcharge, scaled population production, and temporary anger.
- **Partial:** bankruptcy lowers science, then culture and espionage, before settlement. Treasury clamps to zero and records unpaid costs.
  Forced disbanding remains absent. Commerce maxima use modifier keys. Civic slider permissions and technology flexibility unlocks remain deferred.
  Espionage lacks target allocation. Hurry anger uses fixed unhappy citizens per active timer interval.
- **Not started:** taxes, advanced economy, trade/deal income, corporate costs, anarchy exemptions, optional inflation sources, and flexible difficulty.

## Cities

- **Implemented:** settler founding, hex spacing, terrain checks, civilization names, first-city buildings and palace, and initial plot culture.
- **Implemented:** radius-two work, culture-based third-ring access, deterministic governor weights, manual assignments, specialist slots and free specialists.
  Building and technology slot changes use modifier keys. Generic free specialists currently use the citizen type.
- **Implemented:** city-center minima, worked plot yields, specialist output, local/global/area building effects, and direct commerce in hundredths.
  Common fields include yield/commerce changes and modifiers, technology changes, connected bonus changes, and specialist changes.
  Cities exposes raw commerce, direct commerce, and commerce percentages separately. It does not operate sliders or the treasury.
- **Implemented:** food consumption, health penalties, fixed-point food waste, sequential growth thresholds, retained food history, starvation, and avoid-growth.
  Growth occurs once per turn, before culture and production. Third-ring access applies the imported growth surcharge.
- **Partial:** happiness and health ledgers cover population, handicap, city count, buildings, connected bonuses, specialists, terrain, and features.
  War weariness is an external modifier hook. Military presence, distance anger, occupation, taxation, and temporary anger remain absent. Religion supplies religious happiness and health.
- **Implemented:** set/append/remove production commands, paused item progress, item modifiers, normalized capped overflow, feature grants, buildings, and Core unit creation.
  Completion rechecks world/team/player class limits in city-id order. Unit creation supplies experience and a domain event.
- **Partial:** eligibility covers requirements, civilization replacements, class limits, culture wonder capacity, obsolescence, latitude, population, coast, and river.
  Religion supplies its predicates. Power predicates fail closed. Economy supplies process eligibility and hurry commands.
  Automatic buildings, active-civic disabling, decay, and draft remain absent. ProjectsVictory owns project production and completion.
  One queue item completes per city per turn. Excess above the overflow cap does not convert to gold yet.
- **Partial:** city culture keeps hundredths. Plot culture uses hex distance and the source level/rate equation.
  Ownership preserves incumbent ties, then uses player-id order. Capture, terrain-obstructed spread, and isolated-coast eligibility remain absent.
- **Partial:** maintenance provides distance/government-center and exponential city-count costs in hundredths, including early-turn reduction.
  Hex map diameter normalizes distance. A fixed-point root replaces floating-point exponentiation.
  Colony, vassal, and connectedness costs remain absent. Corporations owns branch maintenance. Economy charges the returned city total.
- **Delegated to GreatPeople:** specialist/building GP rates, historical pools, weighted births, and threshold progression.
- **Partial:** current-era growth uses `CityPlayerState.CurrentEra`, with starting-era fallback.
  Technology discovery events update that state. AI per-era growth/cost factors and flexible difficulty remain absent.
- **Not started:** building power, free-building propagation, replacement effects, original-owner commerce, calendar doubling, and scripted rewards.

## Military

- **Implemented:** path movement, deterministic A*, integer sub-moves, terrain/feature costs, hills, routes, river bridge gates, visibility refresh, and unlimited stacking.
  Land and sea domains enforce impassability, peak capabilities, ocean technology gates, and closed borders at peace.
  River crossings disable routes without bridges. They do not add a separate movement penalty.
- **Implemented:** melee strength ledgers, firepower damage, first strikes and immunity, attacker withdrawal, limited damage, collateral caps/immunity, defender selection, and combat events.
  Preview odds use integer dynamic probability calculations without RNG. Combat reports survive saves between commands.
  Experience uses strength ratios, promotion percentages, hundredths, barbarian caps, and a great-general point counter.
- **Implemented:** earned promotion levels, prerequisite evaluation, half-damage promotion healing, free unit/building/trait grants, and civic training experience.
  Location healing includes city buildings and the best same-plot or adjacent medic. Fortify, sleep, sentry, pillage, upgrades, disband, and support counts have commands or services.
- **Partial:** city capture transfers ownership, preserves historical culture, removes the palace, reduces population, and clears queues and work assignments.
  Razing removes the city. Occupation, capture gold, building survival rolls, conquest decisions, and capital relocation remain absent.
- **Partial:** goody outcomes include gold, experience, healing, maps, technologies, units, and hostile villagers.
  Eligibility and map selection omit some multiplayer, civilization, and source-specific restrictions.
- **Partial:** barbarians create a player/team at the first spawn, enforce war, and spawn on unowned fogged land after handicap and city-count gates.
  Area density follows the source deficit formula. Technology adoption uses majority knowledge instead of gradual research.
  Behavior attacks adjacent favorable targets or follows paths toward the nearest city. Animals, naval spawns, and full military AI remain absent.
- **Deferred:** cargo, air missions/rebasing, defender withdrawal, flanking, fort/city zones of control, bombardment, ranged attacks, invisibility, and optional combat variants.
  Promotion zones of control work. Upgrade city search uses a nearby same-area training city, not the full native search policy.
  Conditional free effects remain permanent grants. Worker orders still cancel at the worker phase, not immediately on each move.

## Government

- **Implemented:** one civic per option per player, initial civics from the civilization or `CivicOptionDef.DefaultCivic`,
  and the `government:set_civics` command (one or several options in one revolution).
  Requirements, including negative civic gates, are checked against the final cabinet.
- **Implemented:** anarchy length from the source formula: base, city-count percent, civic lengths, building and trait modifiers,
  speed and era scaling, golden-age exemption, trait cap (spiritual-like), minimum revolution timer.
  Anarchy disables city yields, commerce, culture, growth, production, research and civic/unit costs through `GovernmentStatus`.
  Timers are saved player state, decrement at `PlayerEnd + 1`.
- **Implemented:** `CivicEffects` modifier source: yield/commerce modifiers, capital modifiers, improvement yields, building-class commerce,
  happiness and health (per building class, per military unit, largest cities, features, improvements), population anger,
  war weariness, maintenance percentages, growth, great people, free and unlimited specialists, specialist yield and commerce changes,
  production modifiers (military, building class, unit combat), worker speed, improvement upgrade rate, general rates,
  free experience, military food production. Cities, World and Military consume these keys.
- **Partial:** `RequiresActiveCivics` buildings deactivate when the civic requirement fails. Max sliders, hurry, unit support and upkeep use
  Religion consumes state religion effects. Corporations consumes corporation prohibitions and maintenance modifiers.
  Revolution consumes civic and religious inputs for city instability. Draft, trade routes, connectedness, city limit, and can't-build specials remain unconsumed.
## Peoples

- **Implemented:** `IPeoplesService` (leader, civilization, traits, and the complete leader AI data for the AI mod),
  `TraitEffects` modifier source: city yield/commerce changes and modifiers, happiness/health, building and bonus happiness,
  great people, wonder production percents, building/unit trait production modifiers, improvement trait yields, extra-yield thresholds,
  upkeep, general rates, anarchy cap, level experience, free promotions by combat type.
- **Implemented:** starting civilizations, technologies, free units, civics, civilization unit/building replacements in Cities.
- **Not consumed yet:** extra culture level, upgrade anywhere, assimilation. Religion consumes non-state-religion commerce.

## Religion

- **Implemented:** 13 imported religions replace deferred identities. Definitions retain founding technologies, missionaries, commerce, text, icons, and related buildings.
- **Implemented:** first-discovery founding, source holy-city scoring, free missionaries, and saved reservations when the discovering team has no city.
- **Implemented:** deterministic natural spread through same-team physical trade connections, with influence, distance, civic, speed, and age factors.
  Religion age uses elapsed turns instead of the source's reversed subtraction. Natural spread stops after the first religion.
- **Implemented:** missionary commands, foreign civic restrictions, source success chances, and unit consumption on success or failure.
  Prophets construct shrines through `religion:build_shrine`, subject to holy-city requirements and class limits.
- **Implemented:** state religion commands, civic permission, conversion cooldown, scaled anarchy, and saved last choice under secular civics.
- **Implemented:** religious happiness and health, state-building happiness and commerce, holy-city commerce, worldwide shrine commerce, and trait commerce policy.
  State religion civics also affect production, great-person rates, and unit training experience.
- **Implemented:** presence, state religion, and holy-city requirements. `IReligionService` supplies Core-only AI and client queries.
- **Deferred:** foreign trade connections, religion decay, optional multiple natural spread, inquisitions, religion-picking settings, diplomacy, and religious votes.
  Vote-source religion yields and commerce remain unsupported. The client has query data but no religion screen.
- **Save compatibility:** the religion id change alters the content hash. Older saves need a migration; forced loading does not rewrite old ids.

## Great People

- **Implemented:** Core queries, saved city progress and class pools, specialist/building sources, additive city/player modifiers, and deterministic weighted births.
  Thresholds use speed and starting-era scaling. Civilization overrides resolve the selected class.
  Personal and team threshold increases both apply to the creator.
- **Implemented:** settlement through free-specialist effects, flavor-selected research, capped building production, and immediate culture works.
  Research grants use team population and speed scaling. Hurry grants use city population and speed scaling.
- **Implemented:** distinct-class Golden Age commands, increasing unit requirements, wonder triggers, scaled duration, plot yield bonuses, and GP bonuses.
  Timers, unit-use counts, settled specialists, and threshold progression survive saves.
- **Deferred:** combat births, resource-sensitive bulb valuation, trade missions, and AI mission selection.
  Legacy Cities GP components need migration. Forced loading does not transfer their progress.

## Trade

- **Implemented:** read-only route queries, slot limits, physical connectivity, deterministic partner selection, and domestic/foreign routes.
  Capacity consumes technology, civic, local, coastal, global-building, and external modifier fields.
- **Implemented:** source population/distance profit, capital connection, overseas, building/civic foreign bonuses, shared civics, and trade-yield modifiers.
  Route hundredths aggregate before whole city yields enter Economy sliders.
- **Adapted:** peaceful foreign connections do not require diplomatic agreements yet. Foreign routes receive full peace credit.
  Partners are unique per city, not across the player's entire empire.
- **Profile:** imported initial and maximum route counts are zero. Tests override these values to exercise enabled trade.
- **Deferred:** blockades, open borders, peace-credit recovery, forced trade, and connectedness economics.

## P6 features

### Diplomacy

- **Implemented:** contact discovery between teams, attitude scores and ledgers, open borders, defensive pacts, war, peace, and saved relation history.
- **Implemented:** validated deal proposals, recipient acceptance, cancellation, and settlement of resource, gold, gold-per-turn, and technology items.
  Settlements revalidate assets. War cancels agreements and deals between the affected teams. Defensive pacts can involve partners.
- **Partial:** trade deals do not cover every source deal type. AI diplomacy does not negotiate deals or treaties.

### ProjectsVictory

- **Implemented:** project eligibility, city production integration, team and world counts, prerequisites, limits, and saved completion state.
- **Implemented:** conquest, domination, cultural, project, and time victories. Priority order selects the first eligible condition.
  Saved outcome records winner, victory id, and turn. Score uses population, land, technologies, and world wonders.
- **Partial:** game rules depend on loaded victory definitions. Diplomatic, religious, and other vote-based victories are not defined by this service.

### Corporations

- **Implemented:** Great Person founding, local and foreign branch spread, resource requirements, civic restrictions, competing branch replacement, and headquarters protection.
  Spread can use gold or a valid executive. Founding consumes the Great Person.
- **Implemented:** branch yields, commerce, headquarters commerce, resource-based maintenance, and civic maintenance modifiers.
  Branch presence, activity, headquarters, and consumed resource counts are queryable.
- **Partial:** resource queries use physical connections plus diplomatic resource allocations. Trade-network corporation costs remain separate from city maintenance totals.

### Espionage

- **Implemented:** saved rival-team point pools, player weights, fractional output, deterministic point allocation, passive thresholds, mission costs, and success chances.
  Missions cover treasury theft, production sabotage, revolt, unhappiness, and technology theft.
- **Implemented:** passive effects for demographics, city visibility, research inspection, and city investigation.
  Spy missions validate units, target cities, point balances, and mission-specific requirements before execution.
- **Partial:** the UI exposes allocation and passive unlocks. It does not yet show every passive intelligence result or mission detail.

## P7 features

### Events

- **Implemented:** integer eligibility checks, per-turn random rolls, city and plot subjects, repeat cooldowns, pending choices, automatic fallback, and saved effects.
  Options change gold, research, city food, queued production, culture, happiness, health, and plot yields.
- **Implemented:** AI resolves events automatically. Unresolved human events use the default option at the next event phase.
- **Partial:** option effects cover the schema fields only. Event triggers allow one pending event per player at a time.

### ScriptedWonders

- **Implemented:** wonder completion rewards, periodic unit spawns, combat experience, upgrade discounts, and barbarian combat penalties.
  City timers pause while inactive and survive capture and saves. Completion rewards apply once.
- **Partial:** scripted effects are data-driven and limited to the registered effect fields. Inactive wonders do not advance timers.

### Revolution

- **Implemented:** saved city instability, per-turn change ledgers, stable/warning/mutiny/rebellion states, unrest, and rebellion cooldowns.
  Inputs include wellbeing, capital distance, religion, civics, anarchy, garrisons, Golden Ages, and natural recovery.
- **Implemented:** suppression spends gold or the remaining movement of an owned military garrison.
  Rebellion can spawn rebel units and applies temporary unrest.
- **Partial:** revolution rules and thresholds come from mod content. Rebellion scope does not implement every source-era revolution feature.

## Client and Godot UI

- **Implemented:** Core-backed screen models for diplomacy, agreements, deals, attitudes, victory status, score, corporations, espionage allocation, and pending events.
- **Implemented:** Godot panels for diplomacy and victory, corporations, espionage, events, religion, pedia, save, and load.
  City screens show corporations and espionage commerce. Unit actions expose legal corporation and spy commands.
- **Partial:** spy passive effects are visible, while full city intelligence detail remains unavailable. Scripted-wonder and revolution status have no dedicated global panels.

## Computer players
- **Implemented:** `ai:standard` selects research, civics, commerce rates, production, citizen assignments and gold hurry purchases.
  Scores use integer AI weights, content flavors, unit AI types and leader personality from `IPeoplesService`.
  Civic changes account for anarchy and the revolution cooldown. Research includes free-tech selections.
- **Implemented:** settlers score revealed radius-two/three sites, use escorts under nearby threat, and found cities through normal commands.
  Cities retain garrisons. Spare soldiers explore, collect goody huts, fight nearby enemies, heal, promote and upgrade.
  Strong armies can declare war and move siege-first stacks toward weak cities. Combat previews supply the attack thresholds.
- **Implemented:** workers use read-only World build previews to compare legal yield gains and resource connections.
  Workers finish active tasks before new tasks and construct road corridors between cities and resources.
- **Implemented:** all mutations use validated commands. Personality war checks use the simulation RNG; ties use ordinal ids or entity ids.
  The AI keeps no hidden per-game memory. War-start turns use a saved team component.
- **Partial:** war and peace commands change symmetric Core team relations. Peace is unilateral; agreements and diplomatic attitudes remain absent.
  Road corridors do not plan detours around terrain barriers. Naval expansion, transport operations and great-person missions remain absent.
  Dense barbarian pressure and the imported one-production settler cost still require balance work.

## Validation
Generated games, active worker orders, exploration, feature state, and RNG state survive save/load with identical hashes.
Tests cover deterministic maps, feature services, diplomacy, projects, corporations, espionage, events, scripted wonders, revolution, client models, and phase order.
The Godot client loads feature rules through Core contracts. The test ruleset resides in `tests/Civ.Tests/TestRuleset`.
