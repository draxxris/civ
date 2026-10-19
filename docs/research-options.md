# Research options

The Research mod remains required for research. Ordinary research remains the default.

The game build includes two optional mods. Each mod adds an independent checkbox under **Game options**:

- **Card Research** limits research selection to a saved hand of up to four distinct, eligible technologies.
- **Tech Tree** adds a scrollable prerequisite graph. It works with ordinary research or Card Research.

To omit either mod from the loaded ruleset, disable its stable ID in the startup **Mods** manager.
Card Research uses `card_research`. Tech Tree uses `tech_tree`. Neither mod depends on the other.
The manager merges bundled, user-installed, and `CIV_MODS` roots. A separate `CIV_MODS` directory does not hide bundled mods.
For duplicate IDs, `CIV_MODS` takes priority over user-installed mods, which take priority over bundled mods.
Each winning copy replaces the entire mod. See [the client guide](../src/Civ.Godot/README.md#mod-manager) for profile behavior.

## Card Research

Teams share one hand. The first hand uses the team's starting technologies. The draw uses uniform sampling without replacement.

Selection changes, cancellation, queries, rejected commands, and save loading do not draw cards. Research completion draws a new hand. Free technology selection follows the same restrictions. Future queues are not permitted.

Trade and reward acquisitions remove ineligible cards without replacing the remaining cards. If no cards remain, the game draws a new hand. An empty eligible pool stays empty without random draws. Repeatable technologies can appear again after completion.

Direct research grants can complete technologies outside the hand. Such completion draws a new hand. Unspent progress and overflow remain subject to ordinary research rules.

## Tech Tree

Open **Tech Tree** from the top bar or research dialog. Scroll to browse the graph. Use the search field to locate a technology.

Click a node to inspect its cost, progress, status, and prerequisite groups. The detail panel preserves nested all-of, any-of, and forbidden requirements. Colored lines show technology links.

Browsing does not select research. Use **Research** or **Use free technology** to submit a command. With Card Research enabled, only offered cards permit selection.

## Persistence

Hands, research progress, queues, team knowledge, game options, and RNG state participate in saves and state hashes. Loading a save does not draw cards.

Research and AI mod versions changed. Older saves require the existing explicit content-mismatch override. Forced loads without a hand initialize it at the next authoritative research or turn boundary.
