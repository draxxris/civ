# Espionage Subsystem

The espionage subsystem models intelligence gathering, passive information advantages,
and covert operations against rival civilizations.

Rather than being self-contained in a single isolated mod, espionage follows the
project's decoupled architecture: the dedicated espionage mod defines the core
intelligence service and mission logic, while buildings, commerce sliders, units, and
user interfaces are contributed by their respective domain mods.

______________________________________________________________________

## Architecture Overview

Espionage responsibilities are distributed across four main layers:

```
┌────────────────────────────────────────────────────────┐
│                   Presentation Layer                   │
│   Ariel.Godot (GameUi.Panels.cs) / Ariel.Client.Core       │
│   - Espionage modal screen                             │
│   - Top-bar espionage slider & city yield breakdown    │
│   - Contextual unit actions for spies in rival cities  │
└───────────────────────────┬────────────────────────────┘
                            │
┌───────────────────────────▼────────────────────────────┐
│                  Core Espionage Mod                    │
│   Ariel.Mods.Espionage (src/Mods/Espionage)              │
│   - IEspionageService implementation                   │
│   - Point allocation & bilateral spending ratio        │
│   - Passive threshold checks                           │
│   - Active missions (theft, sabotage, revolt, tech)    │
│   - Counter-espionage & success chance calculations    │
└───────────────┬────────────────────────┬───────────────┘
                │                        │
┌───────────────▼───────────┐ ┌──────────▼───────────────┐
│     Economy & Cities      │ │         Military         │
│  src/Mods/Economy         │ │  src/Mods/Military       │
│  src/Mods/Cities          │ │  - Spy & CounterSpy      │
│  - Commerce vector (4th)  │ │    unit definitions      │
│  - Rate sliders & process │ │  - Movement & defense    │
│  - Buildings & specialists│ │                          │
└───────────────────────────┘ └──────────────────────────┘
```

______________________________________________________________________

## 1. Core Espionage Mod (`src/Mods/Espionage`)

The mod entry point is `EspionageMod` (`EspionageMod.cs`). It registers:

- **Service**: `IEspionageService` registered under `"espionage"`.
- **Modifier Source**: `SpyUnhappiness` under `"unhappiness"` for mission-induced
  misery.
- **State Components**:
  - `EspionageTeamState` (`"team_state"`): stores accumulated `Points` and lifetime
    `Spent` per rival team.
  - `EspionagePlayerState` (`"player_state"`): stores target `Weights` and fractional
    carryover `RemainderHundredths`.
  - `EspionageCityState` (`"city_state"`): tracks per-city timers `UnhappyUntilTurn`,
    `RevoltUntilTurn`, and `OwnsDisorder`.
- **Commands**:
  - `SetEspionageWeightCommand` (`"set_weight"`): updates player espionage weight for a
    target rival team.
  - `ExecuteEspionageMissionCommand` (`"execute_mission"`): validates and executes
    active spy operations.
- **Turn Systems**:
  - `SpyAllocation` (`Order = TurnPhase.Espionage + 1`): distributes produced espionage
    commerce into rival balances each turn.
  - `SpyTimers` (`Order = TurnPhase.PlayerEnd + 1`): cleans up expired disorder and
    unrest flags at turn end.
  - `SpyExpiry` (`Order = TurnPhase.GameBegin + 1`): deletes counter-espionage records
    at the first turn boundary after they expire, so dead protection cannot accumulate
    in saved state.

______________________________________________________________________

## 2. Point Generation, Allocation & Bilateral Ratio

### Generation & Sliders (`src/Mods/Economy`)

Espionage is the fourth commerce channel in `CommerceVector` (Gold, Research, Culture,
Espionage):

- **Slider Allocation**: `PlayerEconomy` allows allocating a percentage of empire-wide
  raw commerce toward espionage.
- **Direct Yields**: Buildings and specialists contribute flat espionage hundredths and
  percentage modifiers directly to city output.
- **Production Conversion**: Cities can convert industrial production into espionage via
  `economy:process_espionage`.
- **Turn Turnout**: Total player espionage hundredths are calculated and consumed by
  `EspionageService.Allocate()`.

### Weight Distribution

Players assign relative weights (from 0 to 10,000) against met rival teams. During turn
allocation:

1. Eliminated teams and barbarians are excluded. Contact through `IDiplomacyService` is
   required.
2. If total weight is zero, points are held in `RemainderHundredths` until positive
   weight is set.
3. Points are divided proportionally according to assigned weights. Indivisible
   remainder points are granted to the teams with the highest fractional remainders
   (with ties broken by lower team id).

### Bilateral Spending Ratio

Passive thresholds and active mission costs scale dynamically according to bilateral
lifetime investment between the two teams:

$$\\text{RatioPercent} = \\text{clamp}\\left(200 \\times \\frac{\\text{rival} +
50}{\\text{own} + \\text{rival} + 100},, 50,, 200\\right)$$

Where `own` and `rival` represent total invested espionage (`Spent + Points`) against
each other.

- If you outspend your rival, costs and passive thresholds drop toward a 50% discount
  floor.
- If the rival outspends you, costs and passive thresholds increase up to a 200%
  ceiling.
- Investment against third parties does not alter this bilateral ratio.

______________________________________________________________________

## 3. Passive Espionage and Intelligence View

Accumulating espionage points against a rival team unlocks passive intelligence tiers
without consuming stored points. The required threshold for each tier is modulated by
the bilateral spending ratio:

$$\\text{Threshold} = \\frac{\\text{Baseline} \\times \\text{RatioPercent}}{100}$$

| Passive Effect       | Baseline Points | Description                                                                           |
| :------------------- | :-------------- | :------------------------------------------------------------------------------------ |
| **Demographics**     | 50              | Grants visibility into rival demographics, rankings, and power scores.                |
| **City Visibility**  | 100             | Grants passive visibility of rival cities on the world map.                           |
| **Inspect Research** | 150             | Reveals what technology the rival team is actively researching.                       |
| **Investigate City** | 200             | Allows viewing detailed production queues, buildings, and yields inside rival cities. |

Passive capabilities are checked through
`IEspionageService.HasPassiveEffect(sim, team, targetTeam, kind)`.

### Full Intelligence View

When passive thresholds are unlocked, the espionage screen and city interface display
detailed rival intelligence:

- **Demographics**: Displays rival demographic standing and comparative rankings.
- **Treasury Reserves**: Displays the rival civilization's current treasury balance.
- **City Inspection**: Displays city yields, population health, happiness, and
  constructed buildings.
- **Production Queues**: Displays the active production order, progress, and turn
  estimates.

### Permission Filtering and Immediate Revocation

Intelligence data passes through strict permission filtering before presentation:

- Client data transfer objects (`EspionageScreen`) populate fields only when passive
  thresholds are met.
- Locked fields are cleared or omitted before reaching the user interface.
- If accumulated points drop below a threshold, or if rival spending raises the
  bilateral ratio, access is immediately revoked.
- Rival intelligence views are strictly read-only and provide no modification controls.

______________________________________________________________________

## 4. Active Spy Missions (`SpyMissions.cs`)

Active missions require a spy unit stationed inside a rival city with movement points
remaining. Missions consume accumulated espionage points upon execution.

### Mission Types

- **`CounterEspionage`** (Baseline Cost: 100):
  - Targets the rival team owning the city.
  - Creates a directed protection record from the protecting team against the target
    rival team.
  - Multiplies rival mission costs against the protecting team by
    `200 + 5 * InterceptionPercent`.
  - The baseline multiplier is 200% (doubling costs) and scales higher with supported
    interception bonuses.
  - Counter-espionage is a directed cost penalty, not a new success roll penalty.
  - Lasts for a speed-scaled duration (10 turns on Normal speed).
  - Repeat missions against the same rival are rejected while protection remains active.
- **`StealTreasury`** (Baseline Cost: 50):
  - Steals up to 25% of the target civilization's treasury (minimum 1 gold).
  - Stolen gold is transferred directly to the player's treasury.
- **`SabotageProduction`** (Baseline Cost: 80):
  - Halves current progress on the target city's active production order or train/build
    progress.
- **`InciteRevolt`** (Baseline Cost: 100):
  - Sets `CityRuleState.Disorder = true` and `RevoltUntilTurn = Turn + 1`.
  - Shuts down city yields, production, and commerce for 1 turn. Restored automatically
    by `SpyTimers`.
- **`FomentUnhappiness`** (Baseline Cost: 60):
  - Sets `UnhappyUntilTurn = Turn + 3`.
  - `SpyUnhappiness` contributes `-3` to `cities:happiness` for the duration.
- **`StealTech`** (Baseline Cost: $2 \\times \\text{CalculateCost}(\\text{tech})$):
  - Validates that the rival knows the tech, the player does not, and the player
    satisfies all tech prerequisites.
  - Awards the technology immediately to the player's team via
    `IResearchService.AddProgress()` and fires `TechDiscovered`.
  - Operates under independent theft eligibility: does not require diplomatic peace or
    the `TechTrading` diplomatic flag.

### Mission Resolution and Counter-Spies

- **Success Rate**: $$\\text{SuccessChance} = \\text{clamp}\\left(85 - 15 \\times
  \\text{Defenders} + \\frac{100 - \\text{RatioPercent}}{5},, 10,, 95\\right)$$ Where
  `Defenders` is the count of units with `UnitDef.CounterSpy == true` owned by the rival
  team at the city plot.
- **Failure**: The spy unit is caught and eliminated (`sim.State.RemoveUnit()`). Spent
  points are not refunded.
- **Success**: The mission effect is applied, and the spy escapes back to the owner's
  capital city with moves depleted.

______________________________________________________________________

## 5. Domain Mod Integration

### Buildings & Specialists (`src/Mods/Cities`)

Espionage infrastructure is configured via standard building and specialist definitions:

- **Buildings**: Defined in `src/Mods/Cities/data/building.json`.
  - Flat bonuses: Security Bureau, Intelligence Agency, Safehouse, TV Station, Cryogenic
    Prison.
  - Multipliers: Scotland Yard (+100% espionage output).
  - Processed by `BuildingEffects.cs` into `sink.AddCommerce()`
    (`direct_espionage_hundredths` and `PercentCommerce.Espionage`).
- **Specialists**: Spy specialists defined in `src/Mods/Cities/data/specialist.json`
  provide espionage yields per specialist assigned, evaluated in `Specialists.cs`.

### Spy Units (`src/Mods/Military`)

Units with espionage roles are defined in `src/Mods/Military/data/unit.json`:

- `military:spy` (Spy)
- `military:agent` (Agent)
- `military:special_agent` (Special Agent)
- `military:007` (Top Secret Agent)
- `military:hacker` (Hacker)
- `military:nanite_spy` (Nanite Spy)

Each unit is designated with:

- `"spy": true`: Permits entry into rival cities and execution of espionage missions.
- `"counterSpy": true`: Provides defensive counter-surveillance penalties (-15% per
  defender) against enemy spies attempting missions in the city.

______________________________________________________________________

## 6. Client & Presentation Layer

- **Client Session Projection** (`src/Ariel.Client.Core/GameSession.Intelligence.cs`):
  - `DescribeEspionage()`: Produces `EspionageScreen` with lists of known rivals,
    current points, weights, and active passive statuses.
  - `SetEspionageWeight()`: Submits weight commands to the simulation.
  - `SpyActions()`: Generates contextual `UnitActionInfo` entries when selecting a spy
    unit on a rival city plot.
  - *Note on Tech Stealing UI*: `SpyActions()` currently omits `StealTech` from default
    single-click unit action buttons because tech theft requires a target selection
    dialog; the simulation and tests fully support the mission.
- **Godot UI** (`src/Ariel.Godot`):
  - **Espionage Panel** (`GameUi.Panels.cs`): Dedicated modal screen listing rival
    teams, accumulated points, weight increment/decrement controls, and unlocked passive
    capabilities.
  - **Top Bar** (`GameUi.cs`): Displays and controls the national espionage commerce
    slider rate.
  - **City Interface** (`CityUi.cs`): Displays per-turn city espionage commerce
    production.

______________________________________________________________________

## 7. Residual Notes and Verification

The following observations are recorded for future maintenance:

- **Commander and Ground-Health Interception**: Counter-espionage protection uses the
  performing spy's interception percent without an attached commander unit's bonus and
  without health scaling for ground units. Neither input can change a result here: the
  engine has no commander unit pool (a leader bonus is copied onto the led unit as an
  ordinary promotion, which the modifier already counts), and spies have no combat
  strength, so nothing can wound one. (Severity: None. Reachability: Unreachable with
  shipped play.)
- **Malformed Research Identifiers**: Content definitions referencing non-existent
  technology identifiers can fail when espionage unlocks evaluate permissions.
  (Severity: Low. Reachability: Malformed custom content declarations.)
