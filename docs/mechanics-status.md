# Mechanics status

Status describes executable rules, not imported data. All eight mods load their definition sidecars.

## Foundation

- **Implemented:** phase contract, setup selection, source plot counts, speed/handicap/era validation, team creation, starting gold.
  Fields: `WorldSizeDef.GridWidth/GridHeight`, `EraDef.StartingGold`, `HandicapDef.Gold`, and `HexOverridesDef`.
- **Partial:** era and handicap grants. Fields: `StartingUnitMultiplier`, `StartingDefenseUnits`, `StartingWorkerUnits`,
  `StartingExploreUnits`, their AI counterparts, `StartingTechs`, and `AiStartingTechs`.
  Earlier-era technology grants, advanced start, calendar progression, and per-player difficulty remain absent.
- **Not started:** default civics for civilizations with incomplete civic declarations, global properties, deals, victory rules.

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
  Foreign permissions, blockades, traded allocations, corporation consumption and vicinity predicates remain absent.
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
  Era order uses `EraDef.StartPercent`. Flexible difficulty, timeline scaling, diffusion welfare, traded-tech permissions, and religion rewards remain absent.

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
  War weariness is an external modifier hook. Military presence, distance anger, occupation, religion, taxation, and temporary anger remain absent.
- **Implemented:** set/append/remove production commands, paused item progress, item modifiers, normalized capped overflow, feature grants, buildings, and Core unit creation.
  Completion rechecks world/team/player class limits in city-id order. Unit creation supplies experience and a domain event.
- **Partial:** eligibility covers requirements, civilization replacements, class limits, culture wonder capacity, obsolescence, latitude, population, coast, and river.
  Religion and power predicates fail closed. Economy supplies process eligibility and hurry commands.
  Automatic buildings, active-civic disabling, decay, draft, and projects remain absent.
  One queue item completes per city per turn. Excess above the overflow cap does not convert to gold yet.
- **Partial:** city culture keeps hundredths. Plot culture uses hex distance and the source level/rate equation.
  Ownership preserves incumbent ties, then uses player-id order. Capture, terrain-obstructed spread, and isolated-coast eligibility remain absent.
- **Partial:** maintenance provides distance/government-center and exponential city-count costs in hundredths, including early-turn reduction.
  Hex map diameter normalizes distance. A fixed-point root replaces floating-point exponentiation.
  Colony, vassal, corporation, and connectedness costs remain absent. Economy must charge the returned total.
- **Implemented:** specialist/building great-person rates, historical type pools, weighted births, speed/era thresholds, and saved player/team threshold progression.
  Combat great people and specialist settlement remain separate future mechanics.
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
  civic definitions in Economy. Not consumed yet: state religion effects (stub keys `religion:*`), corporation maintenance, draft,
  trade routes, connectedness, diplomacy attitudes, revolution-index fields, city limit, can't-build specials.

## Peoples

- **Implemented:** `IPeoplesService` (leader, civilization, traits, and the complete leader AI data for the AI mod),
  `TraitEffects` modifier source: city yield/commerce changes and modifiers, happiness/health, building and bonus happiness,
  great people, wonder production percents, building/unit trait production modifiers, improvement trait yields, extra-yield thresholds,
  upkeep, general rates, anarchy cap, level experience, free promotions by combat type.
- **Implemented:** starting civilizations, technologies, free units, civics, civilization unit/building replacements in Cities.
- **Not consumed yet:** extra culture level, non-state-religion commerce, upgrade anywhere, assimilation.

## Validation

Generated games, active worker orders, exploration and RNG state survive save/load with identical hashes.
Tests cover deterministic maps, physical placement, ledgers, network gates, worker commands, upgrades, setup and phase order.
The Godot client remains on the legacy ruleset. Its feature-ruleset setup screen and rendering remain separate work.
