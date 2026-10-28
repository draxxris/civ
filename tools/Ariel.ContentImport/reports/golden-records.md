# Golden record review

These values came from direct inspection of the local source trees, not from generated
game JSON. Paths use the neutral source labels from the importer reports. The tests do
not need these local trees.

## Core tables

- `XML/Technologies/CIV4TechInfos.xml`: Absolutism costs 2250. Its era is renaissance.
  Social Contract is an OR prerequisite. Free Artistry is an AND prerequisite.
- The same table: Bronze Working costs 90. Woodworking is an OR prerequisite. Mining is
  an AND prerequisite.
- `XML/Buildings/CIV4BuildingInfos.xml`: Abu Simbel costs 150. It produces six
  obsolete-safe culture and has two priest slots.
- `XML/Buildings/CIV4BuildingClassInfos.xml`: Abu Simbel has a world limit of one. Its
  team and player limits are negative one.
- The building table: Granary retains 30 percent food. Forge costs 60, grants 15 percent
  production, and has one engineer slot.
- The building table: Stable costs 60 and requires either horses or ivory. Its remaining
  three resource slots contain `NONE`.
- `XML/Units/CIV4UnitInfos.xml`: Knight has strength 12, movement two, and no first
  strikes.
- The recovered civic tree: Slavery grants 25 population-anger modifier. Farm grants one
  food. Mine grants one production.
- `XML/Units/CIV4PromotionInfos.xml`: Combat II grants ten combat percent and requires
  Combat I as an OR prerequisite.
- `XML/Terrain/CIV4BonusInfos.xml`: Copper grants one production. Bronze Working reveals
  it. Mining permits trade.
- `XML/Terrain/CIV4ImprovementInfos.xml`: Mine has negative-50 health percent. Copper
  connection is enabled, with discovery range 10000.
- `XML/Terrain/CIV4TerrainInfos.xml`: Grassland grants two food and has movement cost
  one.
- `XML/GameInfo/CIV4SpecialistInfos.xml`: Scientist grants three research and three
  great-person rate.
- `XML/GameInfo/CIV4CultureLevelInfo.xml`: Developing has radius two and a normal-speed
  threshold of 480.
- `XML/GameInfo/CIV4GameSpeedInfo.xml`: Normal speed uses 110 growth percent.
- The define registry: food consumption is three per population. The food-wastage growth
  factor is 0.05, emitted as 50 permille.

## Enabled module tables

- The Europe profile's Albania cavalry tree: the unique cavalry replaces Knight, has one
  first strike, and has 30 withdrawal percent.
- The Albania civilization tree: the cavalry overrides Knight. Illogor overrides
  Garrison. The starting grants include Settler and Ritualism.
- The Albania leader tree: Gjergj Kastrioti has Organized and Philosophical traits. His
  build-unit probability is 50. Nationalist is his favorite civic.

## Getter evidence

The XML utility supplies zero or false when a numeric or boolean field omits its
explicit getter default. The building read supplies 90 for maximum latitude. Negative
costs and specialist slots remain integer values, not missing references. Native
positional-pair readers consume the first key and second value. They accept the
production-modifier spelling variants in the source trees. The native city-location
predicate permits coast or river when both flags are set. The native unit-building
predicate permits a technology or era override instead of the required building class.
