# Content schema

Feature definitions reside in `Ariel.Content.Schema`. The common base class for imported
content records is `GameContentDef`, which inherits from `ContentDef` in
`Ariel.Content`. Minimal definition types in `Ariel.Content` remain for legacy contracts
and test rulesets. Feature mods load immutable definitions from their `data/`
directories. Foundation registers setup policies, baseline calendars, and tuning, while
feature mods register gameplay services and systems. The mechanics status and
development roadmap are in [ROADMAP.md](ROADMAP.md).

## Definitions

- Foundation: `YieldDef`, `CommerceDef`, `EraDef`, `GameSpeedDef`, `HandicapDef`,
  `WorldSizeDef`, `CultureLevelDef`, `SpecialistDef`, `RuleConstantsDef`,
  `HexOverridesDef`, `GameOptionDef`, `ConditionDef`, `CorporationIdentityDef`, and
  `VictoryIdentityDef`.
- World: `TerrainDef`, `FeatureDef`, `BonusClassDef`, `BonusDef`, `ImprovementDef`,
  `RouteDef`, `BuildDef`, and `GoodyDef`.
- Research: `TechDef`.
- Economy: `ProcessDef`.
- Cities: `BuildingDef`, `BuildingCategoryDef`, and `HurryDef`.
- Military: `UnitDef`, `UnitCombatDef`, `UnitAiDef`, `UnitCategoryDef`, `DomainDef`,
  `InvisibilityDef`, `PromotionDef`, `MissionDef`, and `OutcomeDef`.
- Government: `CivicDef`, `CivicOptionDef`, and `UpkeepDef`.
- Peoples: `CivilizationDef`, `LeaderDef`, and `TraitDef`.
- Religion: `ReligionDef` with founding technology, missionary, spread factor, commerce
  vectors, and related building ids.
- ProjectsVictory: `ProjectDef` and `VictoryDef` for production projects and executable
  victory conditions.
- Corporations: `CorporationDef` for founding, required resources, branch output,
  headquarters output, spread cost, and maintenance.
- Events: `EventTriggerDef` and `EventOptionDef` for eligibility, chance, repeat
  behavior, outcomes, and timed effects.
- ScriptedWonders: `WonderScriptDef` for building-linked completion, timed, combat, and
  modifier effects.
- Revolution: `RevolutionRulesDef` for instability thresholds, contributing factors,
  suppression, unrest, and rebellion.

Foundation loads hand-authored `HexOverridesDef` separately from imported definition
families. Its noise, climate, sight, lake, source-grid scale, and start-balance integers
enter the content hash. `HexOverridesDef` also specifies barbarian and garrison tuning
(`BarbarianUnownedTilesMultiplier`, `AiMinimumGarrison`, `AiThreatStrengthPerDefender`,
`AiMaximumGarrison`, `GovernorFoodWeight`, `GovernorProductionWeight`, and
`GovernorCommerceWeight`), along with `FeatureClimate` latitude bounds. When loading
handicaps into `ContentDb`, Foundation scales `HandicapDef.UnownedTilesPerBarbarianUnit`
by `BarbarianUnownedTilesMultiplier`. The importer never writes `hex-overrides.json`.

Foundation reserves `GameOptionDef` and `ConditionDef`, plus identity records
`CorporationIdentityDef` and `VictoryIdentityDef`. Corporation and victory gameplay
executes through `CorporationDef`, `ProjectDef`, and `VictoryDef` in their respective
feature mods. Religion owns `religion:*` identities and their prerequisites; existing
content references use those ids. `ReligionDef` retains `StateReligionCommerce`,
`HolyCityCommerce`, and `GlobalCityCommerce` vectors in whole points. The importer
derives temple, monastery, cathedral, and shrine relations (`Temples`, `Monasteries`,
`Cathedrals`, `Shrines`) from building categories and religion fields. Building
happiness, civic religion effects, missionary chances, trait commerce policy, and leader
favorites retain their existing typed properties.

Every definition inheriting from `GameContentDef` has an id, localized display name,
requirements expression, era, help text, pedia text, strategy text, icon path, and
unsupported source paths. An unknown era is null. An empty text field remains empty. A
missing English localization key derives readable words from its key identifier. The
localization report lists missing keys. Unsupported source paths use ordinal order and
preserve XML paths containing non-default data not mapped into schema properties.

### Project and victory fields

`ProjectDef.Cost` uses integer production points; negative instance limits
(`MaxGlobalInstances`, `MaxTeamInstances`) mean unlimited. `TechPrereq`, project
prerequisites, and the optional `Victory` link use typed content references.
`PrereqProjects` maps project ids to required team counts. `AnyonePrereqProject`
requires at least one world completion across all teams. `VictoryThresholds` and
`VictoryMinThresholds` map victory ids to team project counts. `Spaceship` marks a
project for project victory. `AllowsNukes` retains the project rule flag.

`VictoryDef.Rule` selects conquest, domination, cultural, time, or project logic. Lower
`Priority` values run first. `Enabled` disables a condition without removing its
definition. Population and land thresholds (`PopulationPercent`, `LandPercent`) use
percentages. Cultural victory requires `CultureCities` count of cities at
`CultureLevel`. Time rules define a speed-scaled turn limit (`MaxTurns`) and score
weights (`ScorePerPopulation`, `ScorePerLand`, `ScorePerTech`, `ScorePerWonder`).

### Corporation fields

`CorporationDef` links an `Identity` reference, optional founding technology
(`FoundingTech`), required Great Person unit class (`RequiredGreatPersonClass`), and
prerequisite resources (`PrereqBonuses`). Yield and commerce vectors (`YieldProduced`,
`CommerceProduced`) use whole points. Headquarters commerce (`HeadquartersCommerce`) is
evaluated separately from branch commerce. `SpreadCost` uses whole gold.
`MaintenanceHundredths` uses hundredths of gold per consumed resource copy.

### Event fields

`EventTriggerDef` uses integer eligibility fields for technology (`RequiredTech`),
building (`RequiredBuilding`), nearby improvement or feature (`RequiredImprovement`,
`RequiredFeature`), city scope (`CityScope`), minimum population (`MinPopulation`),
minimum treasury (`MinTreasury`), and turn (`MinTurn`). `ChancePercent` is from 0 to
100\. `Repeatable` and `CooldownTurns` control subsequent event rolls. `EventOptionDef`
links to one trigger via `Trigger`. Gold, research, food, production, culture,
happiness, health, and plot-yield changes use integer units (`GoldDelta`,
`ResearchBonus`, `CityFoodDelta`, `CityProductionDelta`, `HappinessDelta`,
`HealthDelta`, `PlotFoodDelta`, `PlotProductionDelta`, `PlotCommerceDelta`). City
culture uses hundredths of a point (`CityCultureHundredthsDelta`). Negative gold
indicates a cost. `DurationTurns` makes happiness, health, and plot effects temporary;
zero means permanent. `Outcome` provides post-choice feedback text.

## Numeric and collection contracts

Authoritative JSON numbers are integers. Collections use `ImmutableArray` and
`ImmutableDictionary`. Requirement composites deserialize into immutable arrays.
Definitions have init-only properties.

`YieldVector` stores food, production, and commerce, in that order:
`(Food, Production, Commerce)`. `CommerceVector` stores gold, research, culture, and
espionage, in that order: `(Gold, Research, Culture, Espionage)`. Short source arrays
receive zeroes in their remaining native slots. Excess entries fail the import. Vector
fields retain their own units: flat amounts, percentage modifiers, doubling times,
thresholds, or integer flags.

Indexed effects use maps with full target ids. Examples include specialist slots,
resource production modifiers, combat-type modifiers, and technology yield changes.
Flavors, contact kinds, memories, attitudes, and adaptation keys are native categories,
not content references. Their map keys remove the source prefix. Anonymous
appearance-random arrays retain source order. Calendar steps retain month increments and
turn counts (`TurnIncrement`).

Missing numeric fields use the getter default in the mapping catalog. Empty numeric
values use that default too. Explicit zeroes and negative integers remain intact.
`NONE`, empty strings, zero, and negative-one reference sentinels become null or absent
entries. A missing map entry uses the source getter's default slot value. Most effect
maps use zero, not a percentage multiplier.

`RuleConstantsDef.Values` contains 68 integer constants for growth, food, culture,
combat, healing, research (including research diffusion), anarchy, upkeep, golden ages,
hurried production, conscription, anger, city defense damage, and trade profit. The
food-wastage growth factor uses permille: the source value 0.05 becomes 50 in
`FoodWastageGrowthPermille`. Conversion is exact without rounding. `Raw` retains all
other XML define entries as exact string key-value pairs. Raw floating-point strings are
preserved for reference but are not authoritative JSON numbers.

## Prerequisites and classes

`Requirements` retains separate `All` and `Any` composite groups. Negative prerequisites
use `Not`. The 21 registered leaf kinds (`ContentLeafKinds`) retain their own typed
evaluators: `tech`, `bonus`, `feature`, `terrain`, `improvement`, `route`, `build`,
`building`, `building-class`, `unit`, `unit-class`, `unit-combat`, `promotion`, `civic`,
`era`, `culture-level`, `condition`, `game-option`, `religion`, `corporation`, and
`victory`.

Building-class leaves refer to the default building definition. Parameter 0 (or omitted)
means a local city class requirement. Parameter 1 means an empire-wide count. The cities
rules scale this target count by `WorldSizeDef.BuildingClassPrereqModifier`. Resource
(`bonus`) parameter 0 (or omitted) checks connected resources via the trade network;
parameter 2 means a vicinity resource (within the city's workable radius). Religion
parameter 0 (or omitted) means city presence; parameters 1 and 2 mean state religion and
holy city. Unit building prerequisites can contain technology or era alternatives
(`TechOverride`, `EraOverride`) that bypass the building requirement.

Location and count predicates refer to foundation conditions. A building with both coast
and river flags requires either location, not both. Minimum coastal area size is the
coastal leaf count (`foundation:coastal`). Other condition predicates represent river
access (`foundation:river`), fresh water (`foundation:fresh_water`), population
(`foundation:population`), city count (`foundation:city_count`), team count
(`foundation:team_count`), or religion state (`foundation:any_religion`,
`foundation:state_religion`). Latitude and setup-era bounds remain typed definition
fields for setup and location queries.

Building and unit classes are folded into their definitions. `DefaultBuilding` and
`DefaultUnit` identify the class default. `ClassLimits` retains world, team, player, and
extra-player limits (`World`, `Team`, `Player`, `ExtraPlayer`). Negative one means
unlimited; zero prohibits construction in that scope. Class lookup maps use
default-definition ids as their keys.

Civilizations map those default ids to unique replacements via `BuildingOverrides` and
`UnitOverrides`. Null explicitly disables a class for that civilization. Starting grants
resolve through civilization definitions: `StartingBuildings`, `StartingTechs`,
`StartingUnits`, and `StartingCivics`. Initial civic choices retain declaration order,
not category-slot order. Each civic's `Option` determines its category. Missing category
declarations remain unspecified. The source profile uses registration-dependent
zero-filled slots for undeclared categories; the schema does not invent artificial civic
identities for these slots. In this profile, 38 civilizations declare six starting
civics in `StartingCivics`, while 22 declare none. Game setup selects defaults for
undeclared civic options among the eight categories.

## Registration and reference checks

The 12 primary imported content mods (Foundation, World, Research, Economy, Cities,
Military, Government, Peoples, Religion, ProjectsVictory, Corporations, and Events) have
empty load-order `dependencies`. They declare their direct cross-mod content providers
in `references` (which may be cyclic). Feature mods with explicit load-order service
requirements (such as `Revolution`, `ScriptedWonders`, `TechTree`, and `CardResearch`)
declare explicit `dependencies` in their manifests. The importer reports the complete
content provider graph in `emitted-counts.json`.

Before registration, `ModLoader` verifies that all declared dependencies and referenced
content providers are present. After all mods register, `ContentDb` checks typed scalar
references, map keys and values, nested records, requirement leaves, and text links.
Every referenced identity must resolve to an existing registered definition of the
expected type, and cross-mod references require the provider mod to be declared in
`references` or `dependencies`. An unresolved reference or undeclared provider fails
load before freeze and content hashing. Unknown JSON properties also fail load.

Each mod owns evaluators for its leaf kinds:

- Research evaluates `tech` (known technologies).
- Government evaluates `civic` (active player civics).
- Cities evaluates `building`, `building-class`, `condition` predicates (coastal, river,
  fresh water, population, city count, team count), `culture-level`, and `era`.
- Military evaluates `unit`, `unit-class`, `unit-combat`, and `promotion`.
- World evaluates `bonus` connected resource counts through `IResourceNetwork`,
  delegating vicinity resource checks (`Param == 2`) to `ICityRequirementService` in
  Cities.
- Religion evaluates `religion` (presence, state religion, and holy city).
- Foundation evaluates `game-option`, `corporation` identity, and `victory` identity.

Feature services evaluate diplomacy permissions, project prerequisites, corporation
rules, and event choices where runtime state exists. Unsupported predicates fail closed
with an explanatory reason. See [ROADMAP.md](ROADMAP.md) for remaining gaps.

## Import decisions and limits

The importer retains source button and atlas strings in `Icon` for subsequent asset
conversion, replacing branded path segments with neutral paths. The asset converter uses
the development provenance map to locate source assets. Text links to emitted records
use `{link:full_id}Display Text{/link}`. Links to external encyclopedia topics use
`{topic:target}topic text{/topic}`.

The XML inventory omits four native unit-AI roles: diplomat, hunter, pillage counter,
and subdued animal. The importer adds these identities from the documented native enum
and DLL registration contract, bringing Military to 45 unit-AI definitions. Four stale
optional references in source data become null, matching native missing-info fallback
(Charles Martel's favorite civic Nationhood, Genseric's and Thrasamund's favorite civic
Vassalage, and Venezia's derivative civilization reference to a missing civilization
entry). No guessed replacements enter shipped data. The data-issues report
(`data-issues.json` and `data-issues.md`) records each affected record and field.

The era filter uses native source era order (ancient, classical, medieval, renaissance,
industrial, modern, transhuman, future). It removes content beyond the maximum era and
strips every transitive dependent. This pruning includes obsolescence, upgrades, effect
maps, and text links, in addition to eligibility prerequisites. The filter is
deliberately conservative: an earlier-era building is pruned if its replacement or
prerequisite belongs to a later era. The importer writes empty arrays for fully removed
families, preventing stale sidecars from previous imports.

Native hunting, subdue, butchering, and resource-spawn outcomes retain chances, unit
grants, resource grants, and yields in `OutcomeEffect`. Action maps use mission ids.
Outcome lists retain source order and promotion chance modifiers. One absent
delivery-policy boolean define (`DONT_ESCORT_SUBDUED_LAND_ANIMAL`) affects 34 units. The
native getter returns false when the define is absent. The schema sets
`DeliverUnitToCity = false` and preserves
`DeliveryPolicyKey = "DONT_ESCORT_SUBDUED_LAND_ANIMAL"` for review.

Artwork, audio, and Python-scripted effects remain unmodeled in schema definitions.
Revolution gameplay uses the typed rules in `RevolutionRulesDef`. The census labels
mapped and unsupported paths separately. A mapped field indicates schema representation,
not that gameplay rules have been implemented.
