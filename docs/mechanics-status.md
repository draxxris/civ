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

- **Implemented:** known-tech requirement evaluator. Field: team `Techs`.
- **Not started:** costs, research commands, progress, overflow, discovery effects and repeatable technology counts.

## Economy

- **Not started:** commerce rates, treasury settlement, maintenance, inflation, taxes and process conversion.
  Setup grants starting treasury, but no economy turn system changes it.

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
  Religion and power predicates fail closed. Automatic buildings, active-civic disabling, decay, hurry, draft, projects, and processes remain absent.
  One queue item completes per city per turn. Excess above the overflow cap does not convert to gold yet.
- **Partial:** city culture keeps hundredths. Plot culture uses hex distance and the source level/rate equation.
  Ownership preserves incumbent ties, then uses player-id order. Capture, terrain-obstructed spread, and isolated-coast eligibility remain absent.
- **Partial:** maintenance provides distance/government-center and exponential city-count costs in hundredths, including early-turn reduction.
  Hex map diameter normalizes distance. A fixed-point root replaces floating-point exponentiation.
  Colony, vassal, corporation, and connectedness costs remain absent. Economy must charge the returned total.
- **Implemented:** specialist/building great-person rates, historical type pools, weighted births, speed/era thresholds, and saved player/team threshold progression.
  Combat great people and specialist settlement remain separate future mechanics.
- **Partial:** current-era growth uses `CityPlayerState.CurrentEra`, with starting-era fallback.
  Research must update that state. AI per-era growth/cost factors and flexible difficulty remain absent.
- **Not started:** building power, free-building propagation, replacement effects, original-owner commerce, calendar doubling, and scripted rewards.

## Military

- **Implemented:** the existing exact-unit and held-promotion predicates. Fields: unit `Type/Promotions`.
- **Not started:** movement, combat, healing, experience, upgrades, capture and military AI.
  Setup creates Core units. World uses unit build/work fields without implementing movement.

## Government

- **Implemented:** the existing active-civic predicate. Field: player `Civics`.
- **Not started:** civic commands, adoption, anarchy, upkeep and modifier sources.

## Peoples

- **Partial:** starting civilizations, technologies, civic declarations and free units through Foundation setup.
  Fields: civilization `Playable/Leaders/StartingTechs/StartingUnits/UnitOverrides/StartingCivics`,
  leader identity, unit `DefaultUnit/Requirements/Domain/Cost/UnitAiTypes/WorkRate/Strength/Moves/NoBadGoodies/CityDefense`.
  Initial units share one tile. Role valuation approximates the source military AI.
- **Implemented in Cities:** civilization city names and construction class replacements.
- **Not started:** trait modifier sources, assimilation, and starting civic defaults.

## Validation

Generated games, active worker orders, exploration and RNG state survive save/load with identical hashes.
Tests cover deterministic maps, physical placement, ledgers, network gates, worker commands, upgrades, setup and phase order.
The Godot client remains on the legacy ruleset. Its feature-ruleset setup screen and rendering remain separate work.
