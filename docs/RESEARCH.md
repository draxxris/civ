# Research and Calendar Progression

This document describes the mechanics for research, calendar progression, technology
trading, and research options.

______________________________________________________________________

## 1. Calendar Progression

The calendar converts elapsed turns into display dates. It reads turn increment
schedules from the active game speed definition.

### Mod Schedules

The mod layer defines the authoritative calendar schedules. Game speed definitions
specify the number of turns and month increments per stage:

- **Normal Speed**: 1200 scheduled turns. The schedule starts with 18 turns of 900
  months per turn.
- **Marathon Speed**: 2400 scheduled turns. The schedule starts with 38 turns of 480
  months per turn.

The start year is 6000 BCE (`START_YEAR=-6000` in `rule-constants.json`). Turn 0
corresponds to the start year. Each subsequent turn advances the calendar date by the
months specified in the current schedule stage.

### Date Display

The Godot client displays the calendar date in the top bar beside the turn number. The
calendar displays years as BCE or CE. Month names appear when increments drop below
twelve months per turn. The simulation engine and save files store integer turn numbers.
The calendar date is a pure display projection.

______________________________________________________________________

## 2. Research Cost Scaling and Output Modifiers

The research system separates technology costs from player research output.

### Sequential Integer Costs

Base technology costs scale through sequential integer steps. Each step applies a
multiplier and truncates the result:

1. Base technology cost from content definition.
2. Handicap modifier.
3. World size modifier.
4. Game speed modifier.
5. Starting era modifier.
6. Team size modifier.
7. Realistic timescale modifier (when enabled).

Every intermediate calculation truncates downward (`floor`). This deterministic integer
sequence ensures cross-platform consistency.

### Output Rate Modifiers

Diffusion and welfare do not change the listed technology cost. Instead, they adjust the
team research rate modifier (`RateModifier`). This rate modifier increases the science
points applied to the current technology each turn:

- **Tech Diffusion**: Contacted teams that already know the technology grant a research
  rate bonus. The calculation divides by all eligible living non-barbarian teams.
  Contacted technology holders form the weighted numerator.
- **Welfare Scaling**: Teams with lower total scores receive a catch-up research rate
  bonus. Welfare compares the team leader's personal score against the highest score of
  every other living non-barbarian player, so a teammate counts as a rival. It uses the
  existing score contract rather than a separate normalized curve. The team's
  era-weighted technology score gates the bonus before the score ratio applies.

### Realistic Timescale (Timeline Research)

The realistic timescale option applies dynamic cost scaling based on game progress:

- It compares elapsed game turns against expected era transition turn counts.
- It compares this expected era against the team leader era and known technology counts.
- It uses turn schedules and era definitions. It does not use historical calendar dates.
- When a team advances ahead of the expected era schedule, technology costs increase.

______________________________________________________________________

## 3. Technology Trading and Espionage Theft

The game enforces distinct rules for peaceful technology trade and covert technology
theft.

### Diplomatic Trading Gates

Diplomatic technology trade requires explicit prerequisites:

- Both teams must have established diplomatic contact.
- The teams must not be at war.
- The offering team must own an enabling technology with the `TechTrading` flag (such as
  Alphabet).
- The game option `NoTechTrading` must be disabled.
- The receiving team must satisfy all technology prerequisites for the traded
  technology.

### Trade versus Theft Eligibility

Espionage technology theft uses independent eligibility rules:

- A spy unit inside a rival city can execute the `StealTech` mission.
- Theft requires that the rival team knows the technology and the stealing team has
  prerequisites.
- Espionage theft does not require the `TechTrading` diplomatic flag.
- Disabling diplomatic technology trading does not block espionage theft.

### Game Option Defaults and Limitations

The game configuration provides standard option defaults:

- **Tech Diffusion**: Enabled by default.
- **Tech Trading**: Enabled by default.
- **No Tech Brokering**: Disabled by default.
- **Realistic Timescale**: Disabled by default.

**No Tech Brokering Limitation**: The reference game option prevents trading
technologies acquired from third parties. Full enforcement requires tracking technology
acquisition origins (self-researched, traded, or stolen). Because team state does not
yet record acquisition provenance, this option currently has no effect on trade
validation.

______________________________________________________________________

## 4. Research Options

The core Research mod is required for all research mechanics, with ordinary sequential
research serving as the default. The game build includes two optional mods that provide
alternate selection mechanics and visual navigation:

- **Card Research** (`card_research`): Limits research selection to a saved hand of up
  to four distinct, eligible technologies.
- **Tech Tree** (`tech_tree`): Adds an interactive, scrollable prerequisite graph for
  browsing and selection. It works alongside ordinary research or Card Research.

### Mod Structure and Configuration

Neither optional mod depends on the other. Each mod adds an independent toggle checkbox
under **Game options**.

To omit either mod from the loaded ruleset, disable its stable ID in the startup
**Mods** manager:

- Card Research uses stable ID `card_research`.
- Tech Tree uses stable ID `tech_tree`.

The mod manager merges bundled, user-installed, and `CIV_MODS` roots:

- A separate `CIV_MODS` directory does not hide bundled mods.
- For duplicate IDs, `CIV_MODS` takes priority over user-installed mods, which take
  priority over bundled mods.
- Each winning copy replaces the entire mod (manifest, assembly, data, and assets).
- See [the client guide](../src/Civ.Godot/README.md#mod-manager) for mod profile
  behavior.

### Card Research

Card Research restricts technology choices to a dealt hand rather than the entire
technology tree:

- **Shared Team Hand**: Teams share a single hand of up to four distinct, eligible
  technologies. The initial hand is drawn using the team's starting technologies via
  uniform sampling without replacement.
- **Draw Triggers**: Completing a technology draws a new hand. Changing selection,
  cancelling research, querying state, rejecting invalid commands, and loading a save do
  not draw cards.
- **Queuing and Free Technologies**: Free technology selection follows the same hand
  constraints. Future queues are not permitted under Card Research.
- **Acquisitions and Ineligible Cards**: Acquiring technologies through trade,
  espionage, or rewards removes ineligible cards from the hand without replacing the
  remaining cards. If no cards remain, the game draws a new hand.
- **Empty Pools**: An empty eligible pool stays empty without random draws.
- **Repeatable Technologies**: Repeatable technologies become eligible for new draws
  after completion.
- **Direct Research Grants**: Direct research grants can complete technologies outside
  the active hand, which immediately triggers drawing a new hand. Unspent progress and
  overflow remain subject to standard research rules.

### Tech Tree

The Tech Tree interface provides visual prerequisite navigation and research commands:

- **Access and Navigation**: Players can open the **Tech Tree** from the top bar or the
  research dialog. Players can scroll through the graph and use the search field to
  locate technologies.
- **Prerequisite Inspection**: Clicking a node inspects its cost, accumulated progress,
  status, and prerequisite groups. The detail panel displays nested all-of, any-of, and
  forbidden requirements. Colored lines display technology dependency links.
- **Command Dispatch**: Browsing alone does not select research. Players submit commands
  through **Research** or **Use free technology**. With Card Research enabled, only
  currently offered cards permit selection.

### Persistence and Save Compatibility

Research state participates fully in save files and deterministic state hashes:

- Active hands, research progress, queues, team knowledge, game options, and RNG state
  are preserved in saves and state hashes.
- Loading a save does not draw cards.
- Research and AI mod versions track schema updates. Older saves require the existing
  explicit content-mismatch override. Forced loads without a hand initialize it at the
  next authoritative research or turn boundary.

______________________________________________________________________

## 5. Residual Notes and Verification

The following observations and decisions are recorded:

- **Eligible-Team Denominator**: The research diffusion denominator counts eligible
  living non-barbarian teams only. The reference mod divides by eligible alive teams,
  including barbarians. The exclusion is intentional and no parity alignment is planned.
  (Severity: None. Reachability: Normal play.)
- **Score Contract in Welfare**: Welfare catch-up compares the team leader's personal
  score against every other living non-barbarian player. A teammate therefore counts as
  a rival, and the bonus applies to the whole team's pooled research. The existing score
  contract is used; no normalized curve is implemented. (Severity: None. Reachability:
  Late-game play.)
- **Malformed Content Guards**: Missing technology identifiers in unlock tables throw
  exceptions during content lookup. (Severity: Low. Reachability: Custom content with
  invalid identifiers.)
