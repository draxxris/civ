# Revolution Subsystem

The Revolution mod tracks city instability and turns it into rebellion. It is optional
and depends on Cities, Foundation, Government and Military.

```mermaid
flowchart TB
    subgraph Sources["Per-turn inputs"]
        Well[Wellbeing]
        Dist[Distance to capital]
        Rel[Religion]
        Civic[Civics and anarchy]
        Gar[Garrison]
        GA[Golden age]
    end
    Ledger[Instability ledger]
    State[RevolutionCityState]
    Tiers[Stable / Warning / Mutiny / Rebellion]
    Spawn[Rebellion opens rebels]
    Sources --> Ledger --> State --> Tiers
    Tiers -->|threshold crossed| Spawn
    Espionage[Spy InciteRevolt] --> State
    Suppress[Gold or garrison suppression] --> State
```

## Rules content

`src/Mods/Revolution/data/revolution-rules.json` is typed by `RevolutionRulesDef`.
`RevolutionMod` implements `IPreparableMod` and validates it at load:

- Threshold ordering and positivity.
- Response values: release, cooldown, unrest, rebel count, suppression price and size,
  and the turns in revolt before a city breaks away.
- Factor signs: stabilizers are non-positive, irritants are non-negative.
- Content ids: the rebel unit and every civic factor key.
  A rename fails at load instead of during a rebellion.

## Instability ledger

Each city has one integer `InstabilityIndex`. Every turn, `InstabilityLedger` rebuilds
the contributing lines and `Update` applies the total once per city per owner turn.

Line sources:

- `revolution:wellbeing`: unhappy minus happy, times `WellbeingFactor`.
- `revolution:distance`: hex distance to the capital, times `DistanceFactor`.
- `revolution:non_state_religion` and `revolution:state_religion`, with an intolerance
  multiplier from the player's civics.
- One line per civic, valued by `CivicFactors`.
- `revolution:anarchy` while the player is in anarchy.
- `revolution:garrison`: `UndefendedFactor` with no garrison, otherwise the garrison
  count capped at `MaxGarrison` times `GarrisonFactor`. A garrison is a land military
  unit with positive strength, alive and not an animal: air and naval units add
  nothing, a wounded unit calms as much as a healthy one, and the fifth garrison adds
  nothing. Undefended is worse than one garrison by more than one garrison's worth, on
  purpose.
- `revolution:golden_age` during a Golden Age.
- `revolution:recovery`, always applied.

Each line is scaled by the game speed's pace and rounded once, to the nearest point
away from zero, so the displayed lines add up to the displayed total. Turn counts use
`GamePace.Scale`, never the research rate.

## Tiers

`CityStabilityStatus` is computed from the index:

| Tier | Start | Effect |
| :--- | :--- | :--- |
| Stable | 0 | None |
| Warning | `WarningThreshold` | Display only: an early alert |
| Mutiny | `MutinyThreshold` | `MutinyUnhappiness` added to city happiness |
| Rebellion | `RebellionThreshold` | Rebellion opens at turn end |

Warning is deliberately display-only. The city screen colours every tier differently.
The top bar shows how many own cities are at or above Warning: `CitiesAtRisk` reads the
saved index only, so counting them costs no wellbeing ledger per city.

## Rebellion

`revolution:rebellions` runs at `TurnPhase.GameEnd`. A city rebels when its index is at
or above `RebellionThreshold` and its cooldown has passed.

- Rebels spawn on legal adjacent tiles: owned by the city owner or unowned, passable,
  outside a third party's borders, and empty. The tile order is shuffled with the
  seeded simulation RNG, so the spots are random per game but identical on replay.
- If no legal tile exists, or no faction takes uprisings (the Barbarians mod is off),
  no units spawn: the rebellion only releases `RebellionRelease` pressure and starts the
  cooldown.
- Rebels join the faction whose data row sets `Uprisings` (the barbarians), creating its
  player on first use. A `CityRebellionTriggered` event carries the rebel player id, or
  -1 when no units appeared.
- Pressure above the threshold is discarded: the index is clamped to the threshold and
  reduced by `RebellionRelease`, so a rebellion always leaves the city below the
  threshold.
- The rebellion cooldown (`revolution:rebellion_cooldown`) and the unrest
  (`revolution:unrest`) are timers on the city (Timers in DESIGN.md) that start the turn after
  and last `GamePace.Scale` of their configured turns; both end if the city changes hands.
  Unrest adds `UnrestUnhappiness` to city happiness while it lasts, and a later rebellion or
  incited revolt prolongs it, never shortens it.

## Secession

A nation's city that stays in revolt breaks away as a new nation. The same
`revolution:rebellions` step counts it, once per turn, after the rebellion check:

- A city is in revolt while its index is at or above `MutinyThreshold`, or while the
  unrest a rebellion or an incited revolt leaves lasts. Each such turn end adds one to
  `RevoltTurns`.
- The count holds while the city is only at the warning tier, so the lull after a
  rebellion does not start it over. It clears once the index is below
  `WarningThreshold` and no unrest remains. Suppressing a city below the warning tier
  is therefore the way to keep it.
- At `SecessionTurns` (12, scaled by `GamePace.Scale`; zero turns the rule off) the city
  secedes through the base game's `ICityCaptureService.Secede` (see "Cities that break
  away" in `docs/DESIGN.md`). The new nation gets `SecessionGarrison` (3) free defenders: the base
  secession rule gives it the best city defender it can train with the technologies it
  starts with (the unit new-game setup grants, not the fixed `RebelUnit`, which only fills
  rebellions that spawn), the former owner's units are put out of the city, and with `SecessionWar`
  (the default) the two start at war.
- A capital, a nation's last city, and a city in a game with no free civilization never
  break away, and their count stays at zero, so a city never secedes without the
  countdown having been on its screen.
- The city screen shows the turns left ("In revolt: breaks away in N turns"), from
  `CityStabilityDescription.SecessionTurns`. The base client reports the secession.

## Suppression

`SuppressRebellionCommand` removes `SuppressionReduction` instability, or all of it when
the city has less. The player chooses the method:

- Gold costs `SuppressionGold * reduction / SuppressionReduction`, so suppressing a
  nearly calm city is cheap. The city screen shows the price.
- A garrison spends the remaining movement of one owned land military unit in the city.

There is no suppression cooldown. Validation is atomic: a rejected command changes no
state and draws no random numbers.

## Spy-incited revolt

The Espionage mod may call `IRevolutionService.Incite` on a rival city. It raises the
index by `InciteInstability` and starts unrest. It does not spawn units directly: a
real rebellion follows the normal rules if the index crosses the threshold. A city
already in disorder cannot be incited.

## State

`RevolutionCityState` holds `InstabilityIndex`, `LastUpdatedTurn` and `RevoltTurns`; the
rebellion cooldown and unrest are timers in the base game's store. A conquest or a secession resets the component, so the loser's instability
never reaches the winner and a new nation's capital starts calm.

## AI

`StabilityPlanner` suppresses in AI cities every three turns: a qualifying garrison
first, then gold, in city id order. It stops when the treasury can no longer pay.
