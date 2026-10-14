# Content schema

The feature definitions are in `Civ.Content.Schema`. The older `Civ.Content` definitions remain for legacy contracts.
Feature mods load immutable definitions from their `data/` directories. Foundation registers setup and feature mods register gameplay services and systems.
The mechanics status is in [mechanics-status.md](mechanics-status.md).

## Definitions

- Foundation: `YieldDef`, `CommerceDef`, `EraDef`, `GameSpeedDef`, `HandicapDef`, `WorldSizeDef`, `CultureLevelDef`, `SpecialistDef`, and `RuleConstantsDef`.
- World: `TerrainDef`, `FeatureDef`, `BonusClassDef`, `BonusDef`, `ImprovementDef`, `RouteDef`, `BuildDef`, and `GoodyDef`.
- Research: `TechDef`.
- Economy: `ProcessDef`.
- Cities: `BuildingDef`, `BuildingCategoryDef`, and `HurryDef`.
- Military: `UnitDef`, `UnitCombatDef`, `UnitAiDef`, `UnitCategoryDef`, `DomainDef`, `InvisibilityDef`, `PromotionDef`, `MissionDef`, and `OutcomeDef`.
- Government: `CivicDef`, `CivicOptionDef`, and `UpkeepDef`.
- Peoples: `CivilizationDef`, `LeaderDef`, and `TraitDef`.
- Religion: `ReligionDef` with founding technology, missionary, spread factor, commerce vectors, and related building ids.
- ProjectsVictory: `ProjectDef` and `VictoryDef` for production projects and executable victory conditions.
- Corporations: `CorporationDef` for founding, required resources, branch output, headquarters output, spread cost, and maintenance.
- Events: `EventTriggerDef` and `EventOptionDef` for eligibility, chance, repeat behavior, outcomes, and timed effects.
- ScriptedWonders: `WonderScriptDef` for building-linked completion, timed, combat, and modifier effects.
- Revolution: `RevolutionRulesDef` for instability thresholds, contributing factors, suppression, unrest, and rebellion.
Foundation loads hand-authored `HexOverridesDef` separately from imported definition families.
Its noise, climate, sight, lake, source-grid scale and start-balance integers enter the content hash.
The importer never writes `hex-overrides.json`.

Foundation reserves `GameOptionDef` and `ConditionDef`, plus corporation and victory identity records.
Corporation and victory gameplay now uses `CorporationDef`, `ProjectDef`, and `VictoryDef` in their feature mods.
Religion owns `religion:*` identities and their prerequisites. Existing content references use those ids.
`ReligionDef` retains state, holy-city, and per-city shrine commerce in whole points.
The importer derives temple, monastery, cathedral, and shrine relations from building categories and religion fields.
Building happiness, civic religion effects, missionary chances, trait commerce policy, and leader favorites retain their existing typed properties.

Every definition has an id, English name, help, pedia, strategy, era, icon, requirements, and unsupported source paths.
An unknown era is null. An empty text field remains empty. A missing English key uses readable words from its key.
The localization report lists missing keys. Unsupported source paths use ordinal order.

### Project and victory fields

`ProjectDef.Cost` uses integer production points. Negative instance limits mean unlimited.
`TechPrereq`, project prerequisites, and the optional victory link use typed content references.
`PrereqProjects` maps project ids to required team counts. `AnyonePrereqProject` requires at least one world completion.
`VictoryThresholds` and `VictoryMinThresholds` map victory ids to team project counts.
`Spaceship` marks a project for project victory. `AllowsNukes` retains the project rule flag.

`VictoryDef.Rule` selects conquest, domination, cultural, time, or project logic.
Lower `Priority` values run first. `Enabled` disables a condition without removing its definition.
Population and land thresholds use percentages. Culture requires a count of cities at a culture level.
Time rules define a speed-scaled turn limit and score weights for population, land, technologies, and wonders.

### Corporation fields

`CorporationDef` links an identity, optional founding technology, required Great Person class, and prerequisite resources.
Yield and commerce vectors use whole points. Headquarters commerce is separate from branch commerce.
`SpreadCost` uses whole gold. `MaintenanceHundredths` uses hundredths of gold per consumed resource copy.

### Event fields

`EventTriggerDef` uses integer eligibility fields for technology, building, nearby improvement or feature, scope, population, treasury, and turn.
`ChancePercent` is from 0 to 100. `Repeatable` and `CooldownTurns` control later event rolls.
`EventOptionDef` links to one trigger. Gold, research, food, production, culture, happiness, health, and plot-yield changes use integer units.
City culture uses hundredths of a point. Negative gold is a cost. `DurationTurns` makes happiness, health, and plot effects temporary; zero means permanent.

## Numeric and collection contracts
Authoritative JSON numbers are integers. Collections use `ImmutableArray` and `ImmutableDictionary`.
Requirement composites deserialize into immutable arrays. Definitions have init-only properties.

`YieldVector` uses food, production, and commerce, in that order.
`CommerceVector` uses gold, research, culture, and espionage, in that order.
Short source arrays receive zeroes in their remaining native slots. Excess entries fail the import.
Vector fields retain their own units: flat amounts, percentage modifiers, doubling times, thresholds, or integer flags.

Indexed effects use maps with full target ids. Examples include specialist slots, resource production modifiers, combat-type modifiers, and technology yield changes.
Flavors, contact kinds, memories, attitudes, and adaptation keys are native categories, not content references. Their map keys remove the source prefix.
Anonymous appearance-random arrays retain source order. Calendar steps retain month increments and turn counts.

Missing numeric fields use the getter default in the mapping catalog. Empty numeric values use that default too.
Explicit zeroes and negative integers remain intact. `NONE`, empty strings, zero, and negative-one reference sentinels become null or absent entries.
A missing map entry uses the source getter's default slot value. Most effect maps use zero, not a percentage multiplier.

`RuleConstantsDef.Values` has 62 integer constants for growth, food, culture, combat, healing, research, upkeep, anarchy, and production.
The food-wastage growth factor uses permille: the source value 0.05 becomes 50. Conversion must be exact, without rounding.
`Raw` retains the other define values as exact strings. Raw floating-point strings are not authoritative JSON numbers.

## Prerequisites and classes

`Requirements` retains separate `All` and `Any` groups. Negative prerequisites use `Not`.
Resource, civic, building, promotion, terrain, feature, religion, corporation, and game-option leaves retain their own kinds.

Building-class leaves refer to the default building. Parameter 0 means a local class requirement.
Parameter 1 means an empire-wide count. The cities rules must apply world-size scaling to that count.
Resource parameter 2 means a vicinity resource. Religion parameters 1 and 2 mean state religion and holy city.
Unit building prerequisites can contain technology or era alternatives that bypass the building requirement.

Location and count predicates refer to foundation conditions. A building with both coast and river flags requires either location, not both.
Minimum coastal area size is the coastal leaf count. Other condition counts represent population, city count, team count, or unit level.
Latitude and setup-era bounds remain typed definition fields for the future setup and location queries.

Building and unit classes are folded into their definitions. `DefaultBuilding` and `DefaultUnit` identify the class default.
`ClassLimits` retains world, team, player, and extra-player limits. Negative one means unlimited.
Zero prohibits construction in that scope. Class lookup maps use default-definition ids as their keys.

Civilizations map those default ids to unique replacements. Null explicitly disables a class.
Starting grants resolve through civilization overrides. Initial civics retain declaration order, not category-slot order.
Each civic's `Option` determines its category. Missing category declarations remain unspecified.
The source uses registration-dependent zero-filled slots for undeclared categories. The schema does not invent portable civic identities for these slots.
In this profile, 38 civilizations declare six initial civics. Another 22 declare none. The rules must select defaults for the eight civic options.

## Registration and reference checks

All content mods currently have empty load-order `dependencies`. They have no overrides or registration-order requirements.
Each manifest declares its direct cross-mod content providers in `references`. These presence requirements can be cyclic.
The importer reports the complete provider graph in `emitted-counts.json`.

Before registration, ModLoader rejects a missing dependency or content provider.
After registration, Core checks typed scalar references, map keys and values, nested records, requirement leaves, and text links.
An unresolved reference or undeclared provider fails load before freeze and content hashing. Unknown JSON properties also fail load.

Each mod owns evaluators for its leaf kinds. Research checks known technologies. Government checks active civics.
Cities checks exact local building ids. Military checks exact unit ids and held promotions.
World checks connected resource counts through `IResourceNetwork`. Vicinity resources remain unavailable.
Other evaluators fail closed with an unavailable-state explanation.
Feature services now evaluate diplomacy permissions, project prerequisites, corporation rules, and event choices where their runtime state exists.
Unsupported predicates remain fail-closed. See [mechanics-status.md](mechanics-status.md) for remaining gaps.
## Import decisions and limits

The importer retains source button and atlas strings for later conversion. It replaces branded path segments with neutral segments.
The future asset converter must use the development provenance map to find the original assets.
Text links to emitted records use full ids. Links to external encyclopedia topics use `{topic:target}text{/topic}`.

The XML inventory omits four native unit-AI roles: diplomat, hunter, pillage counter, and subdued animal.
The importer adds these identities from the documented native enum and registration contract.
Four stale optional references become null, as the native missing-info lookup does. No guessed replacement enters the data.
The data-issues report identifies each affected record and field.

The era filter uses native source era order. It removes later content, then removes every transitive reference dependent.
This includes obsolescence, upgrades, effect maps, and text links, not only eligibility prerequisites.
The filter is deliberately conservative. For example, an ancient building can disappear because its replacement belongs to a later era.
The importer writes empty arrays for fully removed families. It does not leave stale sidecars from an earlier full import.

Native hunting, subdue, butchering, and resource-spawn outcomes retain chances, unit grants, resource grants, and yields.
Action maps use mission ids. Outcome lists retain source order and promotion chance modifiers.
One absent delivery-policy boolean define affects 34 units. The native getter returns false. The schema also retains each policy key for review.

Artwork, audio, and Python-scripted effects remain unsupported. Revolution gameplay uses the typed rules in `RevolutionRulesDef`.
The census labels mapped and unsupported paths separately. A mapped field does not mean that gameplay rules exist.
