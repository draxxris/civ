# Research, Calendar, and Map Overlays

This document describes the mechanics for research, calendar progression, technology trading, and map overlays.

---

## 1. Calendar Progression

The calendar converts elapsed turns into display dates.
It reads turn increment schedules from the active game speed definition.

### Mod Schedules

The mod layer defines the authoritative calendar schedules.
Game speed definitions specify the number of turns and month increments per stage:

* **Normal Speed**: 1200 scheduled turns. The schedule starts with 18 turns of 900 months per turn.
* **Marathon Speed**: 2400 scheduled turns. The schedule starts with 38 turns of 480 months per turn.

The start year is 6000 BCE (`START_YEAR=-6000` in `rule-constants.json`).
Turn 0 corresponds to the start year.
Each subsequent turn advances the calendar date by the months specified in the current schedule stage.

### Date Display

The Godot client displays the calendar date in the top bar beside the turn number.
The calendar displays years as BCE or CE.
Month names appear when increments drop below twelve months per turn.
The simulation engine and save files store integer turn numbers.
The calendar date is a pure display projection.

---

## 2. Research Cost Scaling and Output Modifiers

The research system separates technology costs from player research output.

### Sequential Integer Costs

Base technology costs scale through sequential integer steps.
Each step applies a multiplier and truncates the result:

1. Base technology cost from content definition.
2. Handicap modifier.
3. World size modifier.
4. Game speed modifier.
5. Starting era modifier.
6. Team size modifier.
7. Realistic timescale modifier (when enabled).

Every intermediate calculation truncates downward (`floor`).
This deterministic integer sequence ensures cross-platform consistency.

### Output Rate Modifiers

Diffusion and welfare do not change the listed technology cost.
Instead, they adjust the team research rate modifier (`RateModifier`).
This rate modifier increases the science points applied to the current technology each turn:

* **Tech Diffusion**: Contacted teams that already know the technology grant a research rate bonus.
  The calculation divides by all eligible living non-barbarian teams.
  Contacted technology holders form the weighted numerator.
* **Welfare Scaling**: Teams with lower total scores receive a catch-up research rate bonus.
  Welfare compares the team leader's personal score against the highest score of every other living non-barbarian player, so a teammate counts as a rival.
  It uses the existing score contract rather than a separate normalized curve.
  The team's era-weighted technology score gates the bonus before the score ratio applies.

### Realistic Timescale (Timeline Research)

The realistic timescale option applies dynamic cost scaling based on game progress:

* It compares elapsed game turns against expected era transition turn counts.
* It compares this expected era against the team leader era and known technology counts.
* It uses turn schedules and era definitions. It does not use historical calendar dates.
* When a team advances ahead of the expected era schedule, technology costs increase.

---

## 3. Technology Trading and Espionage Theft

The game enforces distinct rules for peaceful technology trade and covert technology theft.

### Diplomatic Trading Gates

Diplomatic technology trade requires explicit prerequisites:

* Both teams must have established diplomatic contact.
* The teams must not be at war.
* The offering team must own an enabling technology with the `TechTrading` flag (such as Alphabet).
* The game option `NoTechTrading` must be disabled.
* The receiving team must satisfy all technology prerequisites for the traded technology.

### Trade versus Theft Eligibility

Espionage technology theft uses independent eligibility rules:

* A spy unit inside a rival city can execute the `StealTech` mission.
* Theft requires that the rival team knows the technology and the stealing team has prerequisites.
* Espionage theft does not require the `TechTrading` diplomatic flag.
* Disabling diplomatic technology trading does not block espionage theft.

### Game Option Defaults and Limitations

The game configuration provides standard option defaults:

* **Tech Diffusion**: Enabled by default.
* **Tech Trading**: Enabled by default.
* **No Tech Brokering**: Disabled by default.
* **Realistic Timescale**: Disabled by default.

**No Tech Brokering Limitation**:
The reference game option prevents trading technologies acquired from third parties.
Full enforcement requires tracking technology acquisition origins (self-researched, traded, or stolen).
Because team state does not yet record acquisition provenance, this option currently has no effect on trade validation.

---

## 4. Map Overlays

Map overlays present tactical and economic data directly on the world map.

### Layer Controls and Defaults

Players can toggle four independent map overlay layers:

| Layer | Default State | Description |
| :--- | :--- | :--- |
| **Tile Yields** | Off | Displays food, production, and commerce badges on worked or visible plots. |
| **Resource Badges** | On | Displays resource icons on revealed plots containing bonuses. |
| **Hex Grid** | On | Displays terrain-draped hexagonal grid outlines. |
| **Cultural Borders** | On | Displays colored boundary lines representing national territory. |

### Presentation Isolation and Fog Safety

Overlay settings are local presentation preferences managed in `GamePreferences`:

* Toggles update shader uniforms and node visibility without rebuilding terrain geometry.
* Toggles do not mutate simulation state, commands, random number generation, or state hashes.
* Yields and resources appear only on revealed plots according to the team perceived plot snapshot.
* Unexplored tiles show no overlay data.
* Hidden resources remain concealed until the required technology is discovered.

---

## 5. Residual Notes and Verification

The following observations and decisions are recorded:

* **Eligible-Team Denominator**: The research diffusion denominator counts eligible living non-barbarian teams only.
  The reference mod divides by eligible alive teams, including barbarians.
  The exclusion is intentional and no parity alignment is planned.
  (Severity: None. Reachability: Normal play.)
* **Score Contract in Welfare**: Welfare catch-up compares the team leader's personal score against every other living non-barbarian player.
  A teammate therefore counts as a rival, and the bonus applies to the whole team's pooled research.
  The existing score contract is used; no normalized curve is implemented.
  (Severity: None. Reachability: Late-game play.)
* **Malformed Content Guards**: Missing technology identifiers in unlock tables throw exceptions during content lookup.
  (Severity: Low. Reachability: Custom content with invalid identifiers.)
