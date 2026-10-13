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

- **Implemented:** the existing exact-building requirement predicate. Field: city `Buildings`.
- **Not started:** founding, food, growth, culture, borders, work assignments, queues, buildings, happiness and health.
  Cities must consume `IPlotYieldService` and apply `FeatureProduction` grants to saved overflow state.

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
- **Not started:** trait modifiers, naming services, assimilation and civilization-specific building selection.

## Validation

Generated games, active worker orders, exploration and RNG state survive save/load with identical hashes.
Tests cover deterministic maps, physical placement, ledgers, network gates, worker commands, upgrades, setup and phase order.
The Godot client remains on the legacy ruleset. Its feature-ruleset setup screen and rendering remain separate work.
