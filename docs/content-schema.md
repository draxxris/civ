# Content schema

Feature definitions reside in `Ariel.Content.Schema`. The common base class for imported
content records is `GameContentDef`, which inherits from `ContentDef` in
`Ariel.Content`. Minimal definition types in `Ariel.Content` remain for legacy contracts
and test rulesets. Feature mods load immutable definitions from their `data/`
directories. Foundation registers setup policies, baseline calendars, and tuning, while
feature mods register gameplay services and systems. The mechanics status and
development roadmap are in [ROADMAP.md](ROADMAP.md).

The committed `data/*.json` files are the hand-maintained source of truth. The content
importer first produced them from the source extraction, but they have since been edited
by hand and no longer match a fresh import. "Imported" in this document describes where a
record came from, not a file that is regenerated. The importer is a reference extractor
that writes to the untracked `.import-staging/` directory; `mise run import:diff` compares
that output with the committed data so changes can be brought in piece-meal (see
[the importer README](../tools/Ariel.ContentImport/README.md)).

## Definitions

- Foundation: `YieldDef`, `CommerceDef`, `EraDef`, `GameSpeedDef`, `HandicapDef`,
  `WorldSizeDef`, `CultureLevelDef`, `SpecialistDef`, `RuleConstantsDef`,
  `HexOverridesDef`, `GameOptionDef`, `ConditionDef`, and `VictoryIdentityDef`. Any mod may register `CommerceDef` rows (`key`, `order`,
  `initialPercent`, `takesRemainder`, `floorAtZero`, `deficitCut`, `aiPoints`); each is a
  commerce type (DESIGN.md, Commerce types).
- World: `TerrainDef`, `FeatureDef`, `BonusClassDef`, `BonusDef`, `ImprovementDef`,
  `RouteDef`, `BuildDef`, and `GoodyDef`.
- Research: `TechDef`.
- Economy: `ProcessDef`.
- Cities: `BuildingDef`, `BuildingCategoryDef`, and `HurryDef`.
- Military: `UnitDef`, `UnitCombatDef`, `UnitAiDef`, `UnitCategoryDef`, `DomainDef`,
  `InvisibilityDef`, and `PromotionDef`.
- Government: `CivicDef`, `CivicOptionDef`, and `UpkeepDef`.
- Peoples: `CivilizationDef`, `LeaderDef`, and `TraitDef`.
- Victory: `VictoryDef`, the executable victory conditions (`data/victory.json`, hand-written;
  the importer does not own it). A mod that adds a way to win ships its own `VictoryDef`
  rows beside the condition it registers: ProjectsVictory the space race, Votes the
  diplomatic victory.
- Religion (a mod, `src/Mods/Religion`; definitions in its API `Ariel.Mods.Religion.Api`):
  `ReligionDef` (leaf kind `religion`) with its founding technology, missionary, spread
  factor and commerce vectors, `ReligionConditionDef` (leaf kind `religion-condition`,
  `data/religion-condition.json`), and `CivicReligionDef` attached to civics
  (`data/civic-religion.json`). Its own base-typed rows are the religious buildings, units
  and promotions (`data/building.json`, `unit.json`, `promotion.json`), its game options
  (`data/game-option.json`), the placeholder technology (`data/tech.json`) and the
  religious victory identity (`data/victory-identity.json`). The mod also registers one
  founding wonder per religion: a `BuildingDef` whose `founds` names the religion (kind
  `religion`), from the hand-written `data/founding-wonders.json`. The importer does not
  own that file.
- ProjectsVictory: `ProjectDef` for production projects. It lives in the mod's published
  API (`Ariel.Mods.ProjectsVictory`, assembly `Ariel.Mods.ProjectsVictory.Api`); the mod's
  space race is a base `VictoryDef` row in its own `data/victory.json`.
- Votes: `VoteSourceDef` (a council and its interval and civic), `VoteDef` (a
  resolution: the councils that hold it, its threshold, and what it forces), and
  `BuildingVotesDef` attached to the buildings that seat a council (its `voteSource`,
  and `forceTeamVoteEligible` when the owner's team always stands). They live in the
  mod's published API (`Ariel.Mods.Votes`, assembly `Ariel.Mods.Votes.Api`).
- Corporations: `CorporationDef` for corporations and guilds: their seat (the founding building's name), required
  resources, branch output, headquarters output, spread cost, and maintenance. It lives in the mod's published
  API (`Ariel.Mods.Corporations`, assembly `Ariel.Mods.Corporations.Api`).
- Events: `EventTriggerDef` and `EventOptionDef` for eligibility, chance, repeat
  behavior, outcomes, and timed effects. They live in the mod's published API
  (`Ariel.Mods.Events`, assembly `Ariel.Mods.Events.Api`).
- ScriptedWonders: `WonderScriptDef` for building-linked completion, timed, combat, and
  modifier effects.
- Revolution: `RevolutionRulesDef` for instability thresholds, contributing factors,
  suppression, unrest, rebellion, and rebel faction ids.
- Espionage: `EspionageRulesDef` for passive thresholds, mission costs, mission
  effects, counter-espionage protection, and success chance.
- Relations (base) and any mod: `MemoryKindDef` in `data/memory-kind.json` for each kind
  of memory, with its decay, an optional limit per pair, and the attitude weight used when
  the remembering leader's own table leaves the kind out.

Foundation loads hand-authored `HexOverridesDef` separately from imported definition
families. Its noise, climate, sight, lake, source-grid scale, and start-balance integers
enter the content hash. `HexOverridesDef` also specifies garrison and governor tuning
(`AiMinimumGarrison`, `AiThreatStrengthPerDefender`, `AiMaximumGarrison`,
`GovernorFoodWeight`, `GovernorProductionWeight`, and `GovernorCommerceWeight`), along
with `FeatureClimate` latitude bounds. The importer never writes `hex-overrides.json`.
The Barbarians mod keeps its own hand-authored hex tuning in `data/barbarian-rules.json`
(`BarbarianRulesDef.UnownedTilesMultiplier`, at least 1): when it loads its handicap rows
into `ContentDb`, it scales `HandicapBarbariansDef.UnownedTilesPerBarbarianUnit` by that
multiplier. The importer never writes `barbarian-rules.json` either.

Foundation reserves `GameOptionDef` and `ConditionDef`, plus the identity record
`VictoryIdentityDef`; victory gameplay executes through the Victory module's `VictoryDef`.
A religion or a corporation has no base identity: no base row names one, and the Religion
mod's `ReligionDef` (`religion:buddhism`, kept in saves) and the Corporations mod's
`CorporationDef` (`corporations:1`) are their only records. The importer assigns
`ReligionInfo` to the Religion mod and gives it every building, unit and promotion that
names a faith or needs a religion, the religion and inquisition game options, the
religion technology and the religious victory (`IdMapping.RecordOwners`); a family a mod
emits for another mod's records gets the id `mod:name`. `ReligionDef` retains
`StateReligionCommerce`, `HolyCityCommerce`, and `GlobalCityCommerce` vectors in whole
points.

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
project for the space race (`projects_victory:spaceship`). `AllowsNukes` retains the project rule flag.

`VictoryDef.Rule` names the registered victory condition that decides it: the Victory
module's `victory:conquest`, `victory:domination`, `victory:cultural` or `victory:time`,
ProjectsVictory's `projects_victory:spaceship` or Votes' `votes:council` (won only by a
council's vote). A rule no loaded module registers fails the load. Lower
`Priority` values run first. `Enabled` disables a condition without removing its
definition. Population and land thresholds (`PopulationPercent`, `LandPercent`) use
percentages. Cultural victory requires `CultureCities` count of cities at
`CultureLevel`. Time rules define a speed-scaled turn limit (`MaxTurns`) and score
weights (`ScorePerPopulation`, `ScorePerLand`, `ScorePerTech`, `ScorePerWonder`); the
first time victory by id weighs every player's score, enabled or not.

### Corporation fields

`CorporationDef` (imported, `data/corporation.json`, 36 rows: 21 corporations and 15
guilds) has its `Seat` (what the founding building is called:
`headquarters` or `Master Guild`, from the special building type of the source
headquarters) and prerequisite resources
(`PrereqBonuses`, empty for the four guilds that consume none). Each corporation is
founded by exactly one building whose `Founds` names it (`{"kind": "corporation", "id":
"corporations:1"}`), the
imported headquarters or, for a guild, the Master Guild (`Seat` names it in the
corporations screen, the city panel, notices and the encyclopedia; the Guild Hall is a
different, common building). That building's own requirements gate founding
(its technology; for a guild also buildings of its trade across the empire) and its
world limit is one. Once content freezes, the mod refuses a corporation without exactly
one founding building, a founding building with a negative cost or no technology, any
building or unit row that names a corporation no `CorporationDef` defines (frozen
reference validation reports it), and an executive
that spreads one of the mod's corporations but that no city could train (a negative
cost, or every way through its requirements needs a building nothing can build or a
corporation the mod does not define).

The founding buildings, the stores a branch opens, their special building types and
the executives are the mod's own `BuildingDef`, `BuildingCategoryDef` and `UnitDef` rows
(`data/building.json`, `data/building-category.json`, `data/unit.json`, ids
`corporations:corporation_*`, `corporations:specialbuilding_*` and
`corporations:*executive*`), imported like the base rows of their class. The importer
gives a source record to a feature mod outright when a rule in `IdMapping.RecordOwners`
claims it: a building that founds or needs a corporation (`FoundsCorporation` or
`PrereqCorporation`), a special building type only such buildings use, or a unit with a
positive `CorporationSpreads`. A base building that replaces a claimed building (the
theatre and the Red Curtains Theater) cannot name it in `ReplaceBuildings`; the claimed
row lists the base building in `ReplacedBy` instead, which the rules read the same way. The claiming mod then emits every row about the record, including
other mods' attachments (`data/building-great-people.json`). A row of any other mod
leaves out a map entry that names the record, such as a great person's
`ConstructibleBuildings` or a civilization's `UnitOverrides`. A mapping field marked
`owned` keeps only such entries, which is how `CivilizationCorporationsDef`
(`data/civilization-corporations.json`) holds the barbarians' executive overrides. Any
other reference to a claimed record from another mod's row fails the import.

Per-copy output (`YieldProducedHundredths`,
`CommerceProducedHundredths`) uses hundredths of a point per consumed resource copy,
summed and truncated per city. Flat branch output (`YieldChanges`, `CommerceChanges`)
and headquarters commerce per branch (`HeadquartersCommerce`) use whole points.
`SpreadCost` uses whole gold. `MaintenanceHundredths` uses hundredths of gold per
consumed resource copy. `CompetingCorporations` lists identities whose branches a branch
of this one evicts (either side listing the other, or a shared prerequisite resource,
makes two compete; listing itself is refused). `Happiness` and `Health` are whole points
and `MilitaryProductionPercent` a percent on military unit production in each city with
an active branch. `BonusProduced` names a resource each active branch supplies to its
own city, one copy per consumed copy (one when it consumes nothing), while the owner's
team can trade the resource; supplied copies count for city use but not for trade.
`ObsoleteTech` names the technology that closes the team's branches and dissolves a
headquarters in the team. The source's `PrereqBuildingClasses` (a guild's group Guild
Hall) is left unread: a guild branch needs what a corporation's does. `SpreadFactorPercent` raises the spread cost by that percent for each
active branch of a competitor in the target city.

Two fields serve the Realistic Guilds/Corporations mode. `NaturalSpreadPercent` (the
source's `iSpread`, at least 1) scales how far a branch reaches on its own; Mapster, which
the source leaves without one, has 50. `FoundingBuildingClasses` maps a building to the
count the nation must hold across its empire (counted as the Master Guild's own
`building-class` requirement counts it) to found a guild that consumes no resource on its
own; it is required, with counts of at least 1, exactly when `PrereqBonuses` is empty.
The four such guilds name what their Master Guilds ask for: four siege workshops,
theatres, apothecaries or libraries.

`CorporationRulesDef` (`data/corporation-rules.json`, one object, hand-written) holds the
realistic mode's numbers the source does not define, each checked when the mod loads:
`foundingMeanTurns` (30, at least 1: mean turns before a nation that can found a
corporation does), `foundingCooldownTurns` (20, at least 0: turns before a nation that
lost a corporation may found it again), `branchDecayTurns` (5) and
`headquartersDecayTurns` (10), at least 1 (turns without the resource before a branch
closes or a headquarters dissolves), `supplyPercentPerCopy` (50) and `supplyCopiesCap`
(10), at least 0 (the spread bonus per usable copy and the copies it counts), `spreadDistanceFloor` (5, at least 0: subtracted from the distance divisor, as
the source does), `competitorInfluenceDivisor` (10, at least 1) and `crowdingDivisor` (2,
at least 1). All turn counts scale with the game pace. The spread also reads the source's
`CORPORATION_SPREAD_RAND`, `CORPORATION_SPREAD_DISTANCE_DIVISOR`,
`CORPORATION_RESOURCE_BASE_INFLUENCE` and `FREE_TRADE_CORPORATION_SPREAD_MOD` rule
constants, which a load check requires.

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
indicates a cost. `DurationTurns` makes happiness, health, and plot effects temporary,
counted from the turn the event is answered and still applying on the turn that count
reaches; zero means permanent. The lasting effects are kept as timers in the base
game's store (see "Timers" in DESIGN.md). `Outcome` is what the choice did, in words: the events screen shows it as the
result once the option is chosen, and the resolution notice ends with it.

### Civic fields, effects and attachments

`CivicDef.Effects` is the civic's list of plain modifiers. Each entry is
`{ "scope": "city", "key": "cities:happiness", "target": null, "amount": 2 }`, applied
to every calculation of that scope for a player who runs the civic, with the civic id as
the ledger source. Scopes are `plot`, `city`, `player`, `team` and `unit`. The key is a
typed vector key (`yield.flat.food`, `yield.percent.production`,
`commerce.flat.gold`, `commerce.percent.research`, ..., for every loaded commerce type) or a key some loaded module
declares in its `data/modifier-key.json`:

```json
{ "id": "cities:combat_production_percent", "name": "production of {target} units",
  "requirements": null, "targeted": true, "unit": "percent" }
```

`unit` is `flat`, `percent`, `flag` (any non-zero amount turns the rule on) or
`hundredths`. A targeted key takes a content id as `target`, and the ledger line is
keyed `key:target`. The base keys are declared by Cities (happiness, health, free
specialists, maintenance, growth, great people, production and specialist keys), World
(worker speed, improvement upgrade rate), Military (initial experience, city defense,
great general rates) and Corporations (`corporations:maintenance_percent`). Effects are
written in key, then target order with zero amounts left out.

The typed fields that remain on `CivicDef` read more than the calculation's subject
(capital, buildings, improvements, features, units, upkeep, anarchy). What a civic means
to religion is the Religion mod's attachment below.

A feature mod keeps its civic data in its own attachment file, one row for each civic it
has something to say about:

- Corporations, `data/civic-corporations.json` (`CivicCorporationsDef`):
  `noCorporations`, `noForeignCorporations`, `maintenancePercent`,
  `realisticMaintenancePercent`. Corporations turns the maintenance percents into the
  Player-scope `corporations:maintenance_percent` modifier.
- Trade, `data/civic-trade.json` (`CivicTradeDef`): `noForeignTrade`, `routes`,
  `foreignProfitPercent`, `sharedCivicProfitPercent`, `yieldPercent`,
  `forcesOpenTradeRoutesWithWeakerPlayers` and the three connectedness percents.
- Diplomacy (a base module), `data/civic-diplomacy.json` (`CivicDiplomacyDef`): `attitudeChanges`,
  `globalRelationsChange`, `attitudeShareMod`, the three relative-power relations
  changes, `canNotCeaseRelations`, `tributePercent`.
- Religion, `data/civic-religion.json` (`CivicReligionDef`): `requiresStateReligion`,
  `allowInquisitions`, `disallowInquisitions`, `freeReligion`, `noNonStateReligionSpread`,
  `religionSpreadRate`, `nonStateReligionHappiness`, and the six `stateReligion*` fields.
- Revolution, `data/civic-revolution.json` (`CivicRevolutionDef`): `canDoElection`,
  `freeSpeech`, `communism`.
- ProjectsVictory, `data/civic-projects.json` (`CivicProjectsDef`): `enablesMadNukes`.

An attachment row has its own id in the attaching mod's namespace and names the civic in
`target`:

```json
{ "id": "trade:mercantilism", "name": "", "requirements": null,
  "target": "government:mercantilism", "noForeignTrade": true, ... }
```

The same holds for every base definition: a field only a mod reads is in that mod's
attachment file, with a row only for the definitions where it is not at its default, and a
definition without a row reads as the defaults:

- Trade: `data/building-trade.json` (`BuildingTradeDef`: `tradeRoutes`, `globalTradeRoutes`,
  `coastalTradeRoutes`, `tradeRouteModifier`, `foreignTradeRouteModifier`),
  `data/tech-trade.json` (`TechTradeDef`: `tradeRoutes`), `data/yield-trade.json`
  (`YieldTradeDef`: `tradeModifier`), `data/world-size-trade.json` (`WorldSizeTradeDef`:
  `tradeProfitPercent`) and `data/rule-constants-trade.json` (`RuleConstantsTradeDef`:
  `tradeProfitPercent`, about `foundation:rule_constants`).
- GreatPeople: `data/building-great-people.json` (`BuildingGreatPeopleDef`:
  `greatPersonUnit`, `goldenAgeModifier`, `tradeMissionModifier`),
  `data/unit-great-people.json` (`UnitGreatPeopleDef`: `greatPeoples`, `forceBuildings`,
  `greatGeneral`, `leaderPromotion`, `leaderExperience`, `baseDiscover`,
  `discoverMultiplier`, `baseHurry`, `hurryMultiplier`, `greatWorkCulture`),
  `data/specialist-great-people.json` (`SpecialistGreatPeopleDef`: `greatPersonUnit`),
  `data/yield-great-people.json` (`YieldGreatPeopleDef`: `goldenAgeYield`,
  `goldenAgeYieldThreshold`, `aiWeightPercent`), `data/game-speed-great-people.json`
  (`GameSpeedGreatPeopleDef`: `greatPeoplePercent`, `unitDiscoverPercent`,
  `unitGreatWorkPercent`, `unitHurryPercent`, `unitTradePercent`),
  `data/era-great-people.json` (`EraGreatPeopleDef`: `greatPeoplePercent`) and
  `data/rule-constants-great-people.json` (`RuleConstantsGreatPeopleDef`:
  `goldenAgeTurns`, `goldenAgeGreatPeoplePercent`). `TechGreatPeopleDef`
  (`tradeMissionModifier`) ships no rows; no source field sets it.
- Religion: `data/building-religion.json` (`BuildingReligionDef`: `religion`,
  `requiresReligion`, `requiresStateReligion`, `religionChanges`,
  `stateReligionCommerces`, `freeFromStateReligion`, `stateReligionHappiness`,
  `globalReligionCommerce`), `data/unit-religion.json` (`UnitReligionDef`:
  `religionSpreads`, `inquisitor`), `data/trait-religion.json` (`TraitReligionDef`:
  `nonStateReligionCommerce`), `data/leader-religion.json` (`LeaderReligionDef`: the six
  same and different religion attitude fields, and `memoryAttitudePercents` and
  `memoryDecays` for `accepted_religion` and `denied_religion`, which amend the leader)
  and `data/rule-constants-religion.json` (`RuleConstantsReligionDef`:
  `baseReligionAnarchyTurns` and the spread, decay, inquisition and conversion
  constants). Its amending rows are `data/building-grants-religion.json`
  (`BuildingGrantsReligionDef`: `freePromotion`, and `requirementAlternatives`, a map from
  an alternative of one of the target's `any` groups to the requirements that join it),
  `data/unit-grants-religion.json`
  (`UnitGrantsReligionDef`: `constructibleBuildings`, `freePromotions`) and
  `data/civilization-religion.json` (`CivilizationReligionDef`: `buildingOverrides`,
  `unitOverrides`).
- Corporations: `data/unit-corporations.json` (`UnitCorporationsDef`:
  `corporationSpreads`), `data/handicap-corporations.json` (`HandicapCorporationsDef`:
  `corporationMaintenancePercent`) and `data/world-size-corporations.json`
  (`WorldSizeCorporationsDef`: `corporationMaintenancePercent`).
- Espionage: `data/unit-espionage.json` (`UnitEspionageDef`: `spy`, `counterSpy`), and
  rows that amend their targets with espionage amounts: `data/building-espionage.json`
  (`BuildingEspionageDef`), `data/civic-espionage.json` (`CivicEspionageDef`: `effects`),
  `data/trait-espionage.json` (`TraitEspionageDef`), `data/specialist-espionage.json`
  (`SpecialistEspionageDef`: `commerces`), `data/religion/building-religion-espionage.json`
  (`BuildingReligionEspionageDef`, with the Religion mod) and
  `data/corporations/building-espionage.json` (with the Corporations mod).

An attachment property marked `[AmendsTarget]` is not read by its mod: it adds into the
target's property of the same name when content freezes (vectors are summed, arrays
appended, dictionaries merged; see "Content amendments" in DESIGN.md). A row
`{ "target": "cities:library", "commerceChanges": { "espionage": 1 } }` leaves the base
row's gold, research and culture as they are and gives the library one espionage while the
Espionage mod is loaded.
- AirCombat: `data/unit-air-combat.json` (`UnitAirCombatDef`: `airCombatLimit`, `airRange`,
  `bombRate`, `evasionPercent`), `data/improvement-air-combat.json`
  (`ImprovementAirCombatDef`: `airBombDefense`) and `data/rule-constants-air-combat.json`
  (`RuleConstantsAirCombatDef`: `airCombatDamage`).
- RangedCombat: `data/unit-ranged-combat.json` (`UnitRangedCombatDef`:
  `bombAccuracyPercent`).
- Barbarians: `data/handicap-barbarians.json` (`HandicapBarbariansDef`:
  `barbarianCreationTurnsElapsed`, `unownedTilesPerBarbarianUnit`,
  `unownedWaterTilesPerBarbarianUnit`), `data/game-speed-barbarians.json`
  (`GameSpeedBarbariansDef`: `barbPercent`) and `data/era-barbarians.json`
  (`EraBarbariansDef`: `noBarbUnits`).
- GlobalWarming: `data/feature-global-warming.json` (`FeatureGlobalWarmingDef`:
  `warmingDefense`).

To add a civic, register a `CivicDef` whose `option` is an existing `CivicOptionDef` or
one of the mod's own. New players start a new option on its `defaultCivic`, or else on
its first civic without requirements. To give it a new kind of
effect, declare the key in the mod's `data/modifier-key.json`, list it in the civic's
`effects`, and read it in the mod's own rule from the ledger of that scope
(`ModifierSink.Collect(...).Ledger.Total(key)`). To say something about another module's
civic, register a `ContentAttachment<CivicDef>` row from the mod's API.
`tests/TestMods/CivicExtension` does all three.

## Numeric and collection contracts

Authoritative JSON numbers are integers. Collections use `ImmutableArray` and
`ImmutableDictionary`. Requirement composites deserialize into immutable arrays.
Definitions have init-only properties.

`YieldVector` stores food, production, and commerce, in that order:
`(Food, Production, Commerce)`. `CommerceVector` is an object of amounts by commerce type
key, `{ "culture": 0, "gold": 2, "research": 1 }`: a key is a loaded
`CommerceDef`'s `key`, a missing key is zero, and a key no loaded mod registers (or one from
a mod the definition's mod does not reference) fails loading, even with a zero amount, so
base rows never name a mod's type. The importer reads the
source's native commerce arrays in its enum order (gold, research, culture, espionage).
Short source arrays receive zeroes in their remaining native slots. Excess entries fail the import. Vector
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

`RuleConstantsDef.Values` contains 58 integer constants for growth, food, culture,
combat, healing, research (including research diffusion), anarchy, upkeep,
hurried production, conscription, anger and city defense damage. The
food-wastage growth factor uses permille: the source value 0.05 becomes 50 in
`FoodWastageGrowthPermille`. Conversion is exact without rounding. A constant only a mod
reads is that mod's row about `foundation:rule_constants` instead (Trade's
`tradeProfitPercent`, GreatPeople's golden age length and great people percent, Religion's conversion anarchy, spread, decay and inquisition numbers, AirCombat's air strike damage). A mod's define it does not read typed (`IdMapping.ConstantOwner`: espionage's, and religion's other ones) is dropped. `Raw` retains all
other XML define entries as exact string key-value pairs. Raw floating-point strings are
preserved for reference but are not authoritative JSON numbers.

## Prerequisites and classes

`Requirements` retains separate `All` and `Any` composite groups. Negative prerequisites
use `Not`. Each definition type names the leaf kinds that refer to it with
`[ContentLeafKind]`; the importer and reference validation collect them from the loaded
schema assemblies, so a mod's API declares its own (the Corporations API's
`CorporationDef` declares `corporation`). The registered kinds retain their own typed
evaluators: `tech`, `bonus`, `feature`, `terrain`, `improvement`, `route`, `build`,
`building`, `building-class`, `unit`, `unit-class`, `unit-combat`, `promotion`, `civic`,
`era`, `culture-level`, `condition`, `game-option`, `victory` and, with their mods,
`religion` and `religion-condition` (Religion) and `corporation` (Corporations).

Building-class leaves refer to the default building definition. Parameter 0 (or omitted)
means a local city class requirement. Parameter 1 means an empire-wide count. The cities
rules scale this target count by `WorldSizeDef.BuildingClassPrereqModifier`. Resource
(`bonus`) parameter 0 (or omitted) checks connected resources via the trade network;
parameter 2 means a vicinity resource (within the city's workable radius). Religion
parameter 0 (or omitted) means city presence; parameters 1 and 2 mean state religion and
holy city. Corporation parameter 0 (or omitted) means a branch in the city; parameter 2
means the city is the corporation's headquarters. The importer writes a requirement for an
imported headquarters building (one whose `Founds` names a corporation) as that headquarters leaf,
since the imported headquarters cannot be built: executives are trained in the city that
founded their corporation. Unit building prerequisites can contain technology or era alternatives
(`TechOverride`, `EraOverride`) that bypass the building requirement.

Location and count predicates refer to foundation conditions. A building with both coast
and river flags requires either location, not both. Minimum coastal area size is the
coastal leaf count (`foundation:coastal`). Other condition predicates represent river
access (`foundation:river`), fresh water (`foundation:fresh_water`), population
(`foundation:population`), city count (`foundation:city_count`), team count
(`foundation:team_count`). Religion state is the Religion mod's `religion-condition` leaf
(`religion:any_religion`, `religion:state_religion`), which only its own rows name. Latitude and setup-era bounds remain typed definition
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
Military, Government, Peoples, Religion, ProjectsVictory, Corporations, and Events)
declare their direct cross-mod content providers in `references` (which may be cyclic).
Their load-order `dependencies` are empty, except that every mod whose rules ask which
buildings a city runs (Economy, Government, Religion, Trade, and GreatPeople) depends on
Cities. Those rules read `CityBuildingRules.Running`, the city rules' answer, and never
filter `City.Buildings` themselves. Feature mods with explicit load-order service
requirements (such as `Revolution`, `ScriptedWonders`, and `CardResearch`)
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
- Foundation evaluates `game-option` and `victory` identity.
- Religion evaluates `religion` (presence, state religion, and holy city) and
  `religion-condition` (a state religion, any religion); without the mod no row names
  either kind.
- Corporations evaluates `corporation` (a branch in the city, or with parameter 2 its
  headquarters); without the mod no row names the kind.

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
families into its staging output, so a staged run has a file for every family.

Native hunting, subdue, butchering, and resource-spawn outcomes (`OutcomeInfo`, a unit's
`KillOutcomes` and `Actions`) are not imported: no rule runs them. Their source paths are
listed in each unit's `unsupported`.

Artwork, audio, and Python-scripted effects remain unmodeled in schema definitions.
Revolution gameplay uses the typed rules in `RevolutionRulesDef`. The census labels
mapped and unsupported paths separately. A mapped field indicates schema representation,
not that gameplay rules have been implemented.
