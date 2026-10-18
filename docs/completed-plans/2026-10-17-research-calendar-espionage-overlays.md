# Research, calendar, espionage, and map overlays

## Phase List

- [x] 1. (*sol*, session=`ce746921-8fb3-488b-ad2f-bd91e05ccb33`, commit=`e41e5586ab55d37c155ea16984412a5441955331`, fix=`3ccc34f1224b1b12e98e377e317fddf9094aa7a8`) Complete rule data and deterministic calendar progression.
- [x] 2. (*luna*, session=`6384580b-ea95-4aa9-9154-728aa80b29a3`, commit=`fc7e514df97345fdda5d861be7dacac626e011c3`) Show the calendar date beside the turn number.
- [x] R-a. (*ds4* session=`a16f142d-2dc2-434e-8ac6-bac72207f1cc`, `agy(model=flash)` session=`d340e467-2dd4-442f-b0b1-c551041b899f`, verified=`3ccc34f1224b1b12e98e377e317fddf9094aa7a8`) Dual review of phases 1–2.
- [x] 3. (*sol*, session=`63b81bfb-bd96-4031-a4eb-3643b15098e0`, commit=`3693f334213a1e3f0fcdf10f53da56844433bd2f`) Implement diffusion, welfare, and realistic-timescale research costs.
- [x] 4. (*sol*, session=`b19dc547-1700-4cfa-b0f1-5b5f46b9a2b7`, commit=`a6b2389f0137b043ecae6cacf0607cbee5481c28`) Complete technology-trading gates.
- [x] R-b. (*ds4* original session=`da11e68d-5b09-406f-babd-007d90de86b4`, resumed task=`subtask_pawn_1792273592109_8d954d66`, session=`01a14bd4-af41-71bf-8dbd-9764f52c30d2`, `agy(model=flash)` conversation=`8bfd6705-1307-49ce-a8e8-a7869b5e6da4`, verified=`227486cbf24eda3fa20b304fa517a0703a984036`) Dual review of phases 3–4.
- [x] 5. (*sol*, task=`subtask_pawn_1792274107687_0a5875b3`, session=`01a14bdc-8d35-71bf-8dbd-9766b94f4fea`, path=`/home/gordon/.pi/agent/sessions/--home-gordon-workspace-civ--/2026-10-17T21-55-07-702Z_01a14bdc-8d35-71bf-8dbd-9766b94f4fea.jsonl`, commit=`80fa6714c16a623180846823dab9e59d8ce3d305`) Add counter-espionage rules and permission-filtered intelligence models.
- [x] 6. (*sol*, task=`subtask_pawn_1792274805256_c3a35ddd`, path=`/home/gordon/.pi/agent/sessions/--home-gordon-workspace-civ--/2026-10-17T22-06-45-267Z_01a14be7-3212-71bf-8dbd-97683ca42213.jsonl`, commit=`3cb1b509b52a40caa7abe5c11bd6f5201bc3eb13`) Show espionage intelligence and mission status in Godot.
- [x] R-c. (*ds4* task=`subtask_pawn_1792276148904_89a43f4a`, path=`/home/gordon/.pi/agent/sessions/--home-gordon-workspace-civ--/2026-10-17T22-29-08-915Z_01a14bfb-b2b2-71bf-8dbd-976bddc642da.jsonl`, `agy(model=flash)` conversation=`0a7cd144-86be-4e19-9674-3f641593c966`, verified=`3cb1b509b52a40caa7abe5c11bd6f5201bc3eb13`) Dual review of phases 5–6.
- [x] 7. (*sol*, task=`subtask_pawn_1792276625598_32aaf69b`, path=`/home/gordon/.pi/agent/sessions/--home-gordon-workspace-civ--/2026-10-17T22-37-05-603Z_01a14c02-f8c3-71bf-8dbd-976ce28dcb13.jsonl`, worktree commit=`c665713430e81c9ffd3ab8d1ed6293779988d427`, integrated commit=`b545779a9223036b9c60fcad2294401faac70eb3`, isolated worktree=`/tmp/civ-overlays-phase7`) Add safe overlay data, local toggles, and map rendering.
- [x] 8. (*sol*, task=`subtask_pawn_1792277401654_d193e6d7`, path=`/home/gordon/.pi/agent/sessions/--home-gordon-workspace-civ--/2026-10-17T22-50-01-669Z_01a14c0e-d044-71bf-8dbd-976e614ed2a0.jsonl`, commit=`212d8522bf66236c2d9ea670df334b3c7585729a`) Add cross-system integration and replay tests.
- [x] R-e. (*ds4* task=`subtask_pawn_1792278048345_a47f6c82`, path=`/home/gordon/.pi/agent/sessions/--home-gordon-workspace-civ--/2026-10-17T23-00-48-359Z_01a14c18-ae66-71bf-8dbd-97716c01c5e2.jsonl`, `agy(model=flash)` conversation=`5dc42047-0a84-4a92-8a06-d9362247ef19`, verified=`212d8522bf66236c2d9ea670df334b3c7585729a`) Dual review of phases 7–8 and feature integration.
- [x] 9. (`agy(model=flash)`, session=`9fe9a829-7091-4996-bb12-ec8fa7a463f3`, commit=`f76b5f0c523d8ea1ec917195ea1bf5a81884795c`) Run release checks and update documentation, without a review checkpoint.

## Scope and completion bar

This plan implements the pending Research items except player-specific technology queues.
It also implements calendar progression, map overlays, counter-espionage, and the full espionage intelligence view.
Shared teams retain their current research queue. Multiplayer transport and other roadmap items remain outside this scope.

The user selected reference counter-espionage behavior: temporarily increase rival espionage costs, rather than add a new success penalty.
Calendar progression remains in scope at the user's request.
The verified timeline formula uses elapsed turns and speed schedules, not calendar dates.
Calendar display and timeline research therefore share schedule data, but neither requires the other to run.

Completion requires all exit criteria, closed checkpoints, and executable evidence for the user-visible features.
A reachable defect in normal use prevents completion. Cosmetic caveats and unreachable edge cases can remain as recorded NOTE findings.

**Reference decision:** The mod is authoritative. Preserve its effective schedules and -6000 start year.
The original-game chart and XML are explanatory references only, not replacement settings.
Port the verified realistic-timescale formula rather than invent historical technology dates.

## Evidence and existing behavior

- `src/Mods/Research/ResearchCosts.cs`: `Calculate` applies sequential integer cost multipliers and returns a ledger.
  `RateModifier` currently adds a basic contacted-team research bonus.
- `src/Mods/Research/ResearchService.cs`: `Contribute` applies rate modifiers and normalizes overflow.
  `AddProgress`, technology theft, free discoveries, and AI research must remain consistent with effective costs.
- `src/Mods/Research/ResearchMod.cs`: registers team state, commands, and the research phase.
- `src/Civ.Core/Content/Schema/GameSpeedDef.cs`: already exposes `TurnIncrements`.
  `TurnIncrement` in `GameContentDef.cs` retains integer months and turn counts.
- `src/Civ.Core/Content/Schema/EraDef.cs`: already exposes `StartPercent` and `TechCostModifier`.
  Technology era membership comes from `GameContentDef.Era`.
- `src/Mods/Foundation/data/rule-constants.json`: raw constants include `START_YEAR=-6000` and diffusion values `50`, `100`, and `75`.
  These represent the known-team modifier, welfare modifier, and welfare threshold.
- `src/Civ.Core/Content/Schema/RuleConstantsDef.cs`: these calendar and diffusion values lack typed properties.
- `tools/Civ.ContentImport/ContentMapper.cs`: maps legacy month-based turn increments.
  The reference also uses `CvDate` date-based increments, so imported legacy steps alone do not prove calendar parity.
- `src/Mods/Diplomacy/DealRules.cs`: `ValidateProposal` already checks `TechTrading` through `TradeTech`.
  `ValidateExecution` rechecks the proposal. Technology items reject disabled, repeatable, unknown, and already-owned technologies.
- `src/Mods/Diplomacy/DiplomacyService.cs`: `TradeTech` bypasses restrictions when all teams lack technologies or content lacks an enabling flag.
- `src/Mods/Foundation/data/game-option.json`: includes no-diffusion, no-technology-trading, and no-technology-brokering options.
- `src/Civ.Core/Game/EspionageContracts.cs`: mission enum, team extension state, commands, and service contracts already exist.
- `src/Mods/Espionage/SpyMissions.cs`: missions currently require a spy in a rival city and return successful spies to a friendly city.
- `src/Mods/Espionage/EspionageService.cs`: passive thresholds and bilateral cost ratios already exist.
- `src/Civ.Client.Core/GameSession.Intelligence.cs`: `DescribeEspionage` currently returns unlock flags, points, and weights only.
- `src/Civ.Client.Core/GameSession.Map.cs`: `PerceivedPlot`, `BonusVisible`, and fog snapshots are the existing knowledge boundary.
- `src/Civ.Godot/MapRenderer.Land.cs`: resource icons already exist.
  `TerrainRenderer.cs` and `shaders/terrain_common.gdshaderinc` already support cultural border lines.
  Overlays must expose and control these paths rather than duplicate them.
- `src/Civ.Core/Game/SaveGame.cs`: saves include settings, state extensions, RNG, and content identity.

Reference evidence comes from the local extraction reference tree, not web descriptions:

- `CvPlayer.cpp`, `calculateResearchModifier`, around lines 11752–11885: diffusion and welfare formulas.
- `CvTeam.cpp`, `getBestKnownTechScorePercent`, around line 6461: relative technology score.
- `CvInfos.cpp`, `CvTechInfo::getResearchCost`, around line 2116: global and era cost modifiers.
- `CvDate.cpp`: date-based speed increments and season calculation.
- `CvGameCoreUtils.cpp`, around line 4317: calendar modes and legacy turn/date conversion.
- `CvPlayer.cpp`, around lines 20999, 21227, and 21903: counter-espionage eligibility, cost multiplier, and duration.

The user supplied the Normal-speed chart in `docs/references/turns.jpg`.
Original attachment: <https://forums.civfanatics.com/attachments/turns-vs-years-jpg.479103/>.
The image defines calendar progression, not a research-cost multiplier.

The schedule has 500 elapsed turns, from 4000 BCE to 2050 CE:

| Elapsed-turn interval | Turn count | Months per turn |
| --- | ---: | ---: |
| 0–75 | 75 | 480 |
| 75–135 | 60 | 300 |
| 135–160 | 25 | 240 |
| 160–210 | 50 | 120 |
| 210–270 | 60 | 60 |
| 270–320 | 50 | 24 |
| 320–440 | 120 | 12 |
| 440–500 | 60 | 6 |

Each interval describes transitions, not inclusive turn labels. The first transition is turn 0 to turn 1.
Reference anchors are turn 0 = -4000, 75 = -1000, 115 = 0, 135 = 500,
160 = 1000, 210 = 1500, 270 = 1800, 320 = 1900, 440 = 2020, and 500 = 2050.
The chart explicitly includes numeric year zero. It repeats displayed years during six-month turns.
Keep exact internal months, rather than deriving progress from rounded display years.
Do not assume a conversion for other speeds.

The chart's start year differs from the imported raw `START_YEAR=-6000`.
The user selected the mod as the reference, so the imported start year takes precedence.

Further inspection confirms that the user's Normal and Marathon XML snippets exactly match the local expansion-layer
`XML/GameInfo/CIV4GameSpeedInfo.xml.yaml` records. They do not match the effective mod-layer schedules.
The shipped `src/Mods/Foundation/data/game-speed.json` already preserves the effective mod-layer schedules:
Normal has 1200 scheduled turns and starts with 18 turns of 900 months.
Marathon has 2400 scheduled turns and starts with 38 turns of 480 months.
The supplied expansion schedules have 500 and 1500 turns respectively.
The difference is source-layer precedence, not a missing `GameTurnInfos` mapping.
Preserve those effective mod values. Do not replace them with the original-game examples.

A subsequent inspection found the actual timeline rule in `CvTeam.cpp`, `getResearchCost`, around lines 3757–3837.
The earlier research stopped too early in this function and missed its realistic-timescale branch.
It compares expected era progress against the team's leader era and known technologies.
It does not compare calendar years against historical dates.

The user also supplied these original-game formulas:

- Technology cost: `FLOOR(FLOOR(FLOOR(Base Cost * Difficulty Modifier) * Map Modifier) * Speed Modifier)`.
- Known civilization bonus: `1 + FLOOR(0.30 * known civs with tech / starting civs, 0.01)`.

The sequential floors agree with the initial handicap, world, and speed stages in the existing cost service.
The reference adds starting-era, team-size, and other mod-specific stages. Preserve them.
The known-team bonus increases research output rather than directly reducing the listed technology cost.
The effective content uses `ResearchKnownTeamsPercent=50`, not the original-game coefficient 30.
The reference fallback divides by eligible living teams. Do not replace that denominator with starting civilizations.
The enhanced diffusion branch replaces this fallback when diffusion is enabled.

## Resolved rules and implementation constraints

1. **Calendar precedence:** use effective imported mod schedules and the -6000 start. Do not blend source layers.
2. **Timeline formula:** port the reference realistic-timescale branch, including its option gate and truncation order.
   Inspect the reference option default before adding the setting. Do not silently enable an optional mechanic.
3. **Arithmetic:** use deterministic fixed-point powers with independent reference vectors and stated rounding bounds.
   Specify overflow and malformed-content guards without silently introducing new gameplay caps.
4. **Trading scope:** retain reference technology unlock behavior. Only add civic restrictions when actual content defines them.
   No new civic gate is justified by the inspected schema alone.

## Architecture and invariants

- Feature rules stay in mods. Core holds reusable contracts and schemas, not shipped content IDs.
- Calendar dates derive from the turn, speed, starting era, and immutable content.
  Do not store a second mutable clock unless the selected reference schedule requires one.
- Cost previews, AI choices, progress contributions, and theft use the same authoritative research service.
- No floating-point authoritative calculations, machine clock reads, or RNG use in previews.
- Counter-espionage state belongs to a directed team pair, not to an individual city.
- Intelligence permissions apply before construction of client DTOs. Hidden values must not exist in locked DTO fields.
- Overlay preferences are local presentation state. Toggles must not change simulation hashes, commands, or RNG.
- Every phase ends in a commit before its review starts. Reviewers examine a frozen commit hash.
- Every implementor records non-trivial problems in its phase's **Encountered Serious Issues** section.
- Each phase targets at most eight files. Phase 7 records a nine-file exception to keep terrain, water, and river controls consistent.
  The user explicitly consolidated overlays into one implementation phase. Other additional scope requires a separate phase.

## Parallelism

The dependency chains are `1 → 2`, `1 → 3`, and `5 → 6`. Phase 4 can proceed independently of phase 3.
Phase 3 uses phase 1's verified schedule data, not phase 2's calendar display.
Phase 7 can proceed alongside the calendar, research, diplomacy, and espionage work only with disjoint file ownership.
Shared schema, importer, client intelligence, and HUD files require serial changes.
Phase 8 follows phases 1–7 and checkpoints R-a, R-b, and R-c.
Checkpoint R-e covers phases 7–8, including overlay behavior. There is no R-d checkpoint.
Phase 9 follows R-e. `agy(model=flash)` owns both release checks and documentation edits.
There is no documentation reviewer pass.

## Phase 1 — Complete rule data and calendar progression

**Owner:** sol.

### Dumb explanation

The game stores a list that says how much time passes on each turn.
Read that list and calculate the date for any turn.
Use the mod's numbers, not the original game's example numbers.
Expose the result through one shared service so other code does not repeat the calculation.

### Requirements

Potential sites: `GameSpeedDef.cs`, `RuleConstantsDef.cs`, `RuleConstantMapping.json`, Foundation data,
new `CalendarContracts.cs`, new `src/Mods/Foundation/GameCalendar.cs`, `FoundationMod.cs`, and calendar tests.
Reuse the existing `TurnIncrement` mapping unless reference evidence proves that it loses required data.

- Compare effective speed data with `CvDate` increments. Preserve month/day or date endpoints where the reference needs them.
- Map start year and diffusion constants to typed integer fields without embedding ruleset IDs in core.
- Validate positive durations and progress, ordered endpoints, nonempty shipped schedules, and overflow-safe ranges.
- Preserve custom rulesets through explicit neutral defaults. Do not silently infer schedules from unrelated research percentages.
- Map the reference realistic-timescale option and required global cost constants.
- Expose an engine-free calendar service with date, month, season, and formatted-date inputs.
- Apply the reference starting-era offset without changing the turn counter used by existing systems.
- Use the effective imported mod schedule and -6000 start. Keep the supplied chart separate from shipped date expectations.
- Resolve display labels at year zero from the reference date presentation. Do not silently shift date arithmetic.
- Resolve the final schedule tail and subyear display. Six-month turns retain distinct internal dates.
- Follow reference date increments where available. Use legacy turn-count increments only for content that supplies that form.
- Pseudocode: `effectiveTurn = startingEraOffset + elapsedTurns`, then traverse the selected immutable schedule to derive the date.
- Use floor division for negative dates. Separate internal date arithmetic from display labels.

**Exit criteria:** Importer and content checks pass. Effective values match reference records.
Shipped dates match independent calculations from effective mod schedules for every speed.
The original-game chart can serve as a separate synthetic fixture, not the expected shipped Normal calendar.
Boundary, speed, late-start, and save/reload date tests pass. No known regression from earlier phases.

### Encountered Serious Issues

- Verified the native date class with a temporary compiled C++ harness. It starts in February and retains the previous increment at inclusive endpoints. All seven effective schedules reach 2200 CE only with these semantics. Legacy multiplication gives incorrect terminal years. No day increments or explicit date endpoints occur in the effective records, so the existing month/turn schema remains sufficient.
- Calendar registration captured the raw start year and ignored later content overrides. Calendar queries now read all rule inputs from effective `sim.Content`. Load-time schedule validation uses the maximum integer start year to check overflow bounds. The override regression covers -4000, zero, and both integer limits. All four cases failed at the date assertion on `fc7e514df97345fdda5d861be7dacac626e011c3` in `/tmp/pre-fix-ra`. The temporary worktree was removed. After the fix, the focused `CalendarTests` and `ClientSessionTests` run passed all 61 tests.
- Native random-access dates beyond the final scheduled turn use an uninitialized local. The calendar instead continues the final live increment, consistent with normal sequential advancement and the reference tail labels.
- Realistic timescale is a configuration option, not an imported game-option record. Its reference default is true. It is exposed through the existing setup-policy option contract; the research implementation must consume that policy default when no explicit override exists, rather than rely on Simulation.IsOptionEnabled, which only checks imported definitions.
- Verification: focused CalendarTests passed 16 tests; mise run test passed 1140 tests; mise run test:extractor passed 5 tests. The first focused run exposed an invalid test setup for an absent speed, rejected by new-game setup before a calendar query; that unreachable fixture was removed. The first combined verification command exhausted its timeout during extractor execution; the standalone extractor rerun passed.

## Phase 2 — Show calendar dates

**Owner:** luna. Depends on phase 1.

### Dumb explanation

Ask the calendar service for the current date and show it beside the turn number.
Do not calculate the date again inside the screen code.
If a custom ruleset has no calendar, keep the existing turn-only display.

### Requirements

Potential sites: `src/Civ.Client.Core/PlayerSummary.cs`, `GameSession.cs`, `src/Civ.Godot/GameUi.cs`, and client tests.

- Show the date beside the existing turn number. Retain the turn number for debugging and player reference.
- Use the calendar service rather than a second formula. Show months or seasons when the calendar requires them.
- Support rulesets without the calendar service through the existing turn-only display.

**Exit criteria:** Client display checks and Godot build pass. No known regression from earlier phases.

### Encountered Serious Issues

- None. Verified by `ClientSessionTests` (41 passed) and `dotnet build src/Civ.Godot/Civ.Godot.csproj` (0 warnings, 0 errors).

## Phase 3 — Implement diffusion, welfare, and timeline research

**Owner:** sol. Depends on phase 1.

### Dumb explanation

Research can get faster when other civilizations already know the technology.
A civilization that falls far behind can get extra help.
A separate rule raises or lowers costs based on how far its technology progress differs from the expected progress.
Copy the mod's formulas and round numbers in the same order.
Use the same result for research, AI decisions, and technology theft.

### Requirements

Potential sites: `ResearchCosts.cs`, new `ResearchDiffusion.cs`, `ResearchService.cs`, new `ResearchTimeline.cs`,
`ResearchMod.cs`, and research tests.

- Replace the basic known-team shortcut with the reference diffusion branch. Do not stack both bonuses.
- Count eligible living teams and contacted holders. Apply reference open-border and war weights.
  Apply vassal weights only if a vassal contract exists. Do not add a vassal system in this phase.
- Use low-prevalence suppression, high-prevalence acceleration, and contacted-team scaling.
- Port the exponential bonus with deterministic fixed-point arithmetic and explicit rounding.
- Calculate the technology-score ratio and welfare score factor through existing score contracts where available.
  Missing score semantics require an explicit decision, not a guessed count of technologies.
- The no-diffusion option restores the reference basic known-team bonus, rather than disabling all knowledge effects.
- Keep overflow normalization correct across a rate change. Read-only previews must not mutate team state.

- Preserve effective mod cost data and the reference realistic-timescale option gate.
- Estimate total turns by summing the selected speed's turn counts, then multiply by `(100 - startingEra.StartPercent) / 100`.
- Resolve ordered era indices and the reference terminal-era selection. Compute `eraCount = max(1, endEra - startEra)`.
- Count technologies in the inclusive start-to-end era range.
- Compute `expectedEra = elapsedTurns / (estimatedTurns / eraCount) + startEra` with deterministic fractional arithmetic.
- Start `playerEra` from the reference team leader's current era.
  Subtract one technology fraction for each unknown technology below that era.
  Add one technology fraction for each known technology in or above that era.
- Preserve the reference fraction denominator: integer `techCount / eraCount`, followed by fractional reciprocal arithmetic.
  Guard a zero denominator in malformed or sparse custom content with an explicit neutral result and diagnostic.
- Pseudocode: `raw = truncate(100 * pow(4, playerEra - expectedEra))`,
  then `percent = truncate((raw + 10) * 10 / 11)`, then scale the cost by that percentage.
- Implement deterministic powers with checked bounds and independent numeric vectors around truncation thresholds.
- Place this stage after the reference revolution ratio and before barbarian multipliers.
  Audit global and technology-era cost modifiers before the existing handicap/world/speed stages.
  Keep related cost stages in this phase and reuse the existing cost service.
- Add ledger lines for expected era, adjusted player era, timeline percentage, and intermediate cost.
- Do not rewrite accumulated progress when the turn changes. Completion remains at the existing contribution boundary.
- Verify that theft, AI estimates, normal research, and direct progress grants use effective costs consistently.

**Exit criteria:** Independent formula examples, contact/war cases, opt-out, zero-score, speed changes, late starts,
sparse content, and overflow checks pass. No known regression from earlier phases.

### Encountered Serious Issues

- Self-held repeatable research exposed an incorrect diffusion weight. Own technology knowledge must always have neutral weight 1 half-unit, never the open-borders weight 4. Self remains in the contacted-team and holder counts. The fix adds the self-team guard before the open-borders query; DiplomacyService.HasOpenBorders self semantics are unchanged. Also corrected the indentation of the technology definition lookup in ResearchCosts.cs.
- Regression proof used detached worktree `/tmp/civ-research-self-weight-pre-fix` at `a6b2389f0137b043ecae6cacf0607cbee5481c28`. Only the new table-driven test change was copied there. Command: `mise exec -- dotnet test /tmp/civ-research-self-weight-pre-fix/tests/Civ.Mods.Tests/Civ.Mods.Tests.csproj --filter 'FullyQualifiedName~Self_held_repeatable_technology_uses_neutral_diffusion_weight'`. Both cases failed at the public rate-query assertion: two teams with only self holding `research:future_tech` expected 104, actual 114; four teams with self plus two peaceful holders expected 126, actual 138. Independent exponents are 0.5 and 4.5. Existing vectors had no self holders, so they could not catch this defect. No production test seams were added. The temporary worktree was removed with `git worktree remove --force /tmp/civ-research-self-weight-pre-fix`; absence was verified. The live tree was never reverted.
- Fixed verification: `mise exec -- dotnet test tests/Civ.Mods.Tests/Civ.Mods.Tests.csproj --filter 'FullyQualifiedName~Self_held_repeatable_technology_uses_neutral_diffusion_weight' --no-restore` passed 2/2. `mise exec -- dotnet test tests/Civ.Mods.Tests/Civ.Mods.Tests.csproj --filter 'FullyQualifiedName~Research&Category!=E2E' --no-restore` passed 77/77. `mise run test` passed 1190/1190 (37 importer, 135 core, 1018 mod tests). No E2E tests or broader checkpoint review ran. `git diff --check` passed before the standalone fix commit. Only ResearchDiffusion.cs, ResearchCosts.cs and ResearchRulesTests.cs are included in that commit; this plan remains excluded.
- Ported reference research modifier, best-known technology score, technology-info cost stages, team cost stages, leader selection, and terminal-era selection from the local C++ reference. Existing PlayerScore.Techs counts technologies; native technology score instead sums era index plus one. Research uses the native weighted score and consumes IProjectVictoryService.GetScore().Total for the welfare ratio. This deliberately preserves existing total-score semantics rather than replacing the victory subsystem. Missing score service or technology-era semantics disables welfare. Zero or negative own total score gives neutral welfare because native division is undefined.
- No vassal contract exists. Vassal weights remain unsupported; contact, open borders and war weights are implemented. Eligible living civilizations follow the existing city-or-unit ownership convention and exclude barbarian players. Era order follows the existing StartPercent then ordinal-id convention. Era cutoff options affect timeline estimation only; broader era eligibility remains outside this implementation.
- Timeline reads the setup-policy default when no explicit setting exists. Empty schedules, sparse technologies, missing technology/current/start eras, invalid duration, and absent leaders return a neutral timeline with diagnostic ledger lines. Existing custom probe technologies without eras therefore remain supported without inferred era membership.
- Q36 integer powers use truncated logarithm constants, range reduction and a positive Taylor series. Relative error is below 1e-30 within the supported range. Integer decay powers use exact rational half-up rounding: focused vectors exposed 42.5 rounding down in the initial approximation (three failures); the exact path fixes these ties. Fractional values within the stated error of a threshold may differ from native floating-point results. Powers above the supported range throw; sufficiently small powers underflow to zero without changing integer outputs. Cost and contribution intermediates use checked widened arithmetic, not gameplay caps.
- Added observable rate, welfare, timeline, cost-preview, theft-price and contribution lifecycle tests. Expected vectors were calculated independently with Python Decimal at 70-digit precision. Existing economy tests now explicitly isolate optional timeline and revolution rules, retain the mandatory era cost stage, and provide a living contacted rival for fallback overflow normalization. AI cost consumers were inspected in ResearchPlanner; theft and direct grants already use the authoritative cost service. No new production test seams were added.
- Complexity: uvx lizard -l csharp -C 10 src/Mods/Research/*.cs reported Calculate at 11 before extraction. Extracted BarbarianPercent; the same command now reports no functions above 10. No broader checkpoint was performed.
- Exact final verification: mise exec -- dotnet test tests/Civ.Mods.Tests/Civ.Mods.Tests.csproj --filter 'FullyQualifiedName~Research&Category!=E2E' --no-restore passed 75/75. mise run test passed 1175/1175 (37 importer, 135 core, 1003 mod tests). Initial compile caught an ambiguous TechDef import, then fixed. The initial existing-suite run exposed seven obsolete formula/fixture expectations, updated with independent expected values. No E2E or extractor-category tests were run. git diff --check passed before commit.

## Phase 4 — Complete technology-trading gates

**Owner:** sol.

### Dumb explanation

Technology trades must obey the rules, even when a player sends a command directly instead of using the screen.
Check the rules when a trade starts and again when the other player accepts it.
Show the player why a trade is blocked.
Do not let simplified test setups bypass the real game rules.

### Requirements

Potential sites: `DiplomacyService.cs`, `DealRules.cs`, `GameSession.Diplomacy.cs`, and `DiplomacyRulesTests.cs`.

- Preserve the existing reference either-team technology unlock rule.
- Remove the all-teams-techless bypass for content that defines technology-trading unlocks.
  Update synthetic fixtures to grant real prerequisites rather than weakening production checks.
- Enforce the no-technology-trading option at proposal and acceptance.
- Inspect reference no-brokering provenance requirements before adding that option.
  Record any missing provenance dependency rather than adding an unrelated trade-history subsystem without approval.
- Expose the same rejection reason in the diplomacy model. Recheck deals after changes to eligibility.
- Add civic gates only for an evidence-backed content rule.

**Exit criteria:** Allowed and denied proposals, acceptance rechecks, and custom-content behavior pass.
No known regression from earlier phases.

### Encountered Serious Issues

- Active implementation session: `01a14b4c-2cb5-77b5-8c62-6ed1be636e32`. The prior session entry was stale.
- Reference inspection: `CvPlayer.cpp:7764–7794` enforces the no-technology-trading setting and a team technology eligibility flag. This local source requires both teams to unlock technology trades. The authorized rule explicitly retains the existing either-team behavior, so this implementation preserves it rather than claiming exact local-source parity.
- No-brokering needs persistent per-team, per-technology acquisition provenance. `CvDeal.cpp:841–843` marks received technologies as non-tradable. `CvTeam.cpp:1489–1498` clears those flags each turn only when no-brokering is disabled. Research completion also sets temporary restrictions (`CvTeam.cpp:6411–6414`). Existing team state stores ownership, not provenance. The imported no-brokering setting defaults to true, but enforcement remains unsupported. No acquisition-history subsystem is authorized or added.
- Civic inspection: `CivicDef`, effective civic data, and the reference technology-trade branch contain no civic technology-trading gate. `NoForeignTrade` concerns city trade routes, not technology exchange. No civic restriction is added.
- Test authoring gate: command-boundary tests own unlocks, proposal rejection, acceptance rechecks, custom rulesets, and atomic rejection. Credible regressions include the techless bypass and an ignored setting. Existing tests covered neither. Client tests own fresh rejection reasons and stale-action submission after eligibility changes. Expected decisions and messages are literal, independent contracts. No test-only production seams or source assertions are added.
- Implementation removes the techless bypass for all shared trading gates. Fixtures now grant real prerequisites. Proposal validation enforces the no-technology-trading setting; acceptance already reuses that validation. `GameSession.Diplomacy.cs` already exposes its exact rejection through `DealOf` and submits stale actions through command validation, so no client production change was needed. Client lifecycle tests verify both paths.
- Initial temporary-worktree compilation exposed a missing required `ModSource.Root` in the custom fixture. The fixture now supplies it. No production workaround was added.
- Pre-fix proof used detached worktree `/tmp/civ-trading-pre-fix` at `3693f334213a1e3f0fcdf10f53da56844433bd2f`, with only updated tests copied into it. Command: `mise exec -- dotnet test /tmp/civ-trading-pre-fix/tests/Civ.Mods.Tests/Civ.Mods.Tests.csproj --filter 'FullyQualifiedName~Techless_teams_cannot_bypass_content_unlocks|FullyQualifiedName~No_technology_trading_rejects_either_givers_mixed_proposal_but_allows_gold|FullyQualifiedName~Deal_screen_refreshes_rejection_reasons_and_stale_actions_cannot_bypass_them|FullyQualifiedName~Custom_content_without_unlock_flags_stays_open_but_obeys_the_option_default|FullyQualifiedName~Technology_deals_recheck_eligibility_before_acceptance' --no-restore`. Six cases failed for the intended bugs: techless admission, both proposal directions, option-default admission, acceptance admission, and missing client rejection reason. Five control cases passed. An earlier seven-case run gave five expected failures and two passes. The worktree was removed; live files were never reverted.
- Final focused verification: `mise exec -- dotnet test tests/Civ.Mods.Tests/Civ.Mods.Tests.csproj --filter 'FullyQualifiedName~DiplomacyRulesTests|FullyQualifiedName~ClientDiplomacyVictoryTests|FullyQualifiedName~NetworkTransitTests|FullyQualifiedName~ResearchEconomyTests|FullyQualifiedName~CorporationRulesTests' --no-restore` passed 124/124.
- `mise run build:game` passed with zero warnings and errors. `mise run test` passed 1188/1188: 37 importer, 135 core, 1016 mod tests. No E2E, race, or extractor-category suites ran. No startup or visual checks ran because no rendering code changed.
- Complexity command: `uvx lizard -l csharp -V src/Mods/Diplomacy/DealRules.cs src/Mods/Diplomacy/DiplomacyService.cs`. `ValidateProposal` decreased from 16 to 7; extracted `ValidateTradeUnlocks` and `ValidateItems` measure 6 and 8. `TradeTech` decreased from 5 to 4. Other functions were not refactored.
- Six implementation files changed: two production files (17 additions, six deletions) and four test files (154 additions, 13 deletions). No core content identifiers were added. Added lines passed the prohibited-token scan. `git diff --check` passed before commit. This plan remains excluded from staging and commit. No blocker remains for the authorized scope; no-brokering remains the recorded unsupported dependency.
- Baseline: `mise exec -- dotnet test tests/Civ.Mods.Tests/Civ.Mods.Tests.csproj --filter 'FullyQualifiedName~DiplomacyRulesTests|FullyQualifiedName~ClientDiplomacyVictoryTests|FullyQualifiedName~NetworkTransitTests|FullyQualifiedName~ResearchEconomyTests' --no-restore` passed 102/102 before changes.

## Phase 5 — Add counter-espionage and safe intelligence models

**Owner:** sol.

### Dumb explanation

Add a spy mission that temporarily makes one rival's spy missions more expensive against your team.
Store which team it affects and when it ends, so saves preserve the effect.
Also prepare the rival information that the espionage screen can show.
Check permission before reading private values, and leave locked information out of the screen data.

### Requirements

Potential sites: `EspionageContracts.cs`, `SpyMissions.cs`, `EspionageService.cs`, `EspionageMod.cs`,
`GameSession.Intelligence.cs`, a focused intelligence DTO file, and espionage rules and client tests.

- Append the mission enum value to preserve existing values.
- Verify effective reference mission data. Inspected records specify baseline cost 100, duration 10, and cost multiplier 200 percent.
- Require a spy in the rival city, as the reference mission data specifies.
- Save the directed target-team effect in registered team extension state. Scale duration by game-speed research percentage.
- Reject repeat missions while the effect remains active, as reference eligibility does.
- Apply the defender's effect against the attacker when calculating mission costs and applicable passive thresholds.
- Do not add the roadmap's proposed success penalty. Preserve existing mission failure and spy-return behavior outside this feature.
- Inspect reference promotion-dependent multiplier behavior. Port only supported interception inputs and record any unsupported dependency explicitly.
- Use a single expiry boundary across all team members. Ensure old saves deserialize with no active effect.

- Add selected-rival details, passive thresholds, current spending, research details, treasury, city demographics, and production queues.
- Define a field-permission matrix before implementation. Use Demographics for treasury and aggregate statistics,
  InspectResearch for research, CityVisibility for known city presence, and InvestigateCity for detailed city statistics and queues.
- Preserve per-player treasury within shared teams. Do not invent a pooled treasury.
- Return locked or absent fields before reading protected live data. Never use zero as a disguised locked value.
- Rival city inspection is read-only. Do not route it through own-city controls without a real permission guard.
- Threshold loss, point spending, elimination, and turn changes revoke access immediately.
- Intelligence query methods must not submit commands, alter fog memory, or consume RNG.

**Exit criteria:** Direction, cost rounding, duration, expiry, repeat rejection, save/hash, threshold boundary,
and permission-revocation checks pass. Hidden mutations do not change locked DTOs.
No known regression from earlier phases.

### Encountered Serious Issues

- Active session: `/home/gordon/.pi/agent/sessions/--home-gordon-workspace-civ--/2026-10-17T21-55-07-702Z_01a14bdc-8d35-71bf-8dbd-9766b94f4fea.jsonl`.
- Permission matrix, defined before implementation: Demographics permits aggregate city, population and unit counts plus per-player treasury records. InspectResearch permits current research, progress, cost and queue. CityVisibility permits city identity, owner and position. InvestigateCity permits city statistics and production queues within visible city entries. Locked sections are null. No rival controls enter the DTO.
- Reference inspection: `CvPlayer.cpp:20999–21004` scales the counter mission base cost by research speed and rejects an active directed effect. Lines 21227–21233 multiply the cost modifier by the defender's effect. Lines 21903–21915 scale duration and add five times the spy's current interception probability to the 200-percent multiplier. Effective mission data specifies cost 100, ten turns and a city target.
- Native `CvUnit.cpp:13017–13033` uses full interception for land units by default. The existing air service scales all units by health. Counter-espionage therefore reads supported base interception and promotion modifiers directly, with the native cap and default land behavior. Commander bonuses and the optional ground-health interception setting have no supported contracts. They remain unsupported dependencies, not new success penalties.
- Baseline focused verification passed 21/21 espionage and client intelligence tests. Initial compilation found missing modifier imports, then ambiguous schema names. Explicit aliases resolved them. No existing expectation was changed.
- Found a live permission defect: `HasPassiveEffect` retained access after target-team elimination. The new owner-boundary regression failed on frozen baseline `227486cbf24eda3fa20b304fa517a0703a984036`: expected false, actual true. Proof command: `mise exec -- dotnet test /tmp/civ-p5-passive-pre-fix/tests/Civ.Mods.Tests/Civ.Mods.Tests.csproj --filter 'FullyQualifiedName~Passive_permissions_are_revoked_when_the_target_team_is_eliminated'`. Only that new test was copied into the detached worktree. The worktree was removed. Live files were never reverted.
- Test authoring gate: mission commands own direction, rounding, speed, expiry, repeat rejection, save/hash and failure behavior. The client query owns section boundaries, hidden-state invariance, detached snapshots, shared-team treasuries and access revocation. Credible regressions include reversed team pairs, per-player timers, reading locked research or queues, pooled gold, and stale permissions. Previous coverage lacked these contracts. Expected numeric values use literal independent formulas. No test-only production seam was added.
- Initial new-fixture failures used incorrect JSON property casing and measured success chance in the city where the spy returned. The returned spy legitimately adds the existing defensive bonus. The corrected fixture measures another friendly city. Existing expectations remain unchanged.
- Expiry is exclusive: active while `State.Turn < ExpiresTurn`, where expiry is the mission turn plus `floor(10 * ResearchPercent / 100)`. No per-player decrement occurs. Expired records remain inert until replaced. Normal, Quick and Marathon durations are 8, 6 and 16 turns. Base costs also use research-speed scaling. Cost calculation floors the combined ratio/multiplier before the final price. No new success penalty exists.
- Phase 6 contract: `DescribeEspionage()` still supplies rival rows. `DescribeRivalIntelligence(teamId)` returns null for unavailable, unknown, self or eliminated rivals. Its snapshot includes points, weight, spending forecast, threshold/unlock records, both directed protection records, and nullable demographics/research/city sections. Treasury records retain player IDs. City details contain copied queue rows and no controls. Requery after commands and turn changes. Protection duration is `ExpiresTurn - Sim.State.Turn` while its record is present.
- Final focused command: `mise exec -- dotnet test tests/Civ.Mods.Tests/Civ.Mods.Tests.csproj --filter 'FullyQualifiedName~EspionageRulesTests|FullyQualifiedName~ClientIntelligenceEventsTests' --no-restore` passed 40/40. `mise run test` passed 1209/1209: 37 importer, 135 core, 1037 mod tests. Output was redirected to `/tmp/civ-p5-default.log`. `mise run build:game` passed with zero warnings and errors. No E2E, extractor-category, race, release or visual suites ran.
- Complexity: `uvx lizard -l csharp -V` on the two changed espionage implementation files and the new client file reports MissionAvailability 8 to 6, ApplyEffect 9 to 9, and the new rival query 6 after budget extraction from its initial 10. SpyInterception is 4. No touched production function exceeds 10. `git diff --check` and the added-line prohibited-token scan passed. Six implementation files changed. The ignored plan remains outside the commit.

## Phase 6 — Show espionage intelligence and status

**Owner:** sol. Depends on phase 5.

### Dumb explanation

Build the screen from the information prepared in phase 5.
Let the player choose a rival and inspect the details they have unlocked.
Explain locked fields and show how long counter-espionage lasts.
Update the screen when permissions change, and never let the player control a rival's cities.

### Requirements

Potential sites: `GameUi.Panels.cs`, a focused `GameUi.Espionage.cs`, `GameSession.Intelligence.cs`, and UI model tests.

- Add rival selection, scrollable city details, locked-state explanations, and production queue display.
- Show treasury reserves only through the authorized DTO fields.
- Show directed counter-espionage duration and cost effects. Connect the validated spy action to the existing command.
- Refresh an open panel after commands and turn changes. Remove stale selection when the rival or city disappears.
- Do not expose production, slider, or citizen controls for a rival city.

**Exit criteria:** Client checks, Godot build, and manual unlock/revoke/mission UI checks pass.
No known regression from earlier phases.

### Encountered Serious Issues

- Session: `/home/gordon/.pi/agent/sessions/--home-gordon-workspace-civ--/2026-10-17T22-06-45-267Z_01a14be7-3212-71bf-8dbd-97683ca42213.jsonl`.
- Rendered checks found a modal height of 2006 pixels at 1280×720. The regression failed in detached worktree `/tmp/civ-p6-layout-before`, built from the baseline plus the frozen implementation. Evidence: `/tmp/civ-p6/layout-before.log`, `Panel exceeds viewport: (280, 80), (720, 2006)`. The live tree was never reverted. The worktree was removed. A non-wrapping modal heading and bounded mission scroll area fix transient minimum-size growth. All ten final captures now report a 720×566 panel inside the viewport.
- Test authoring gate: the rendered tour owns UI delivery and lifecycle risks that client DTO tests cannot reach. It presses real rival and mission buttons, changes the city picker, submits validated commands, and advances a turn. It checks separate player reserves, queue display, protection direction, revocation, expiry, stale city/rival selection, and viewport bounds. No private UI exports or test-only production seams were added. ToggleEspionage follows the existing debug-panel workflow.
- Initial fixture failures selected an existing generated city, retained extension state across a turn, and included notification text in modal checks. The corrected fixture clears generated cities, requeries live extension state, and limits checks to the modal. The first default suite caught shipped content IDs in the fixture through DogfoodTests. Content queries replace those IDs. No existing test or expectation changed.
- Final focused tests: espionage rules and client intelligence passed 40/40. `mise run test` passed 1209/1209 (37 importer, 135 core, 1037 mod tests), log `/tmp/civ-p6/default-final.log`. `mise run build:game` passed with zero warnings/errors, log `/tmp/civ-p6/build-final.log`. No E2E, extractor-category, race, release suite or review checkpoint ran.
- Rendered Xvfb/Mesa compatibility captures passed all ten lifecycle checks at 1280×720: `/tmp/civ-p6/final*.png`, log `/tmp/civ-p6/capture-final.log`. Earlier 1600×900 captures established the mission and queue views. Inspected city and directed-protection screenshots. These use control signals rather than physical pointer clicks. Hardware rendering, multiple-rival switching, and arbitrary small windows remain unverified. Startup alone is not interaction evidence. Existing top-bar overflow at 1280 pixels remains outside this phase.
- Complexity: `uvx lizard -l csharp -V` reports BuildEspionage 3 before / 5 after. New city rendering peaks at 7. Capture Advance decreased 12 to 3, Check 20 to 1; extracted checks peak at 7. TakeScreenshot decreased 16 to 10, PrepareDebugStage 12 to 7. Untouched PollStarting remains 13. No touched function exceeds 10. Five tracked implementation files change; this ignored plan remains outside the commit.

## Phase 7 — Add safe map overlay data, toggles, and rendering

**Owner:** sol.

### Dumb explanation

Add four switches for yields, resource icons, hex lines, and cultural borders.
Draw only information the player is allowed to know.
Reuse the existing icons and border drawing instead of drawing them twice.
These switches change the picture, not the game state or its random numbers.

### Requirements

Potential sites: `GameSession.Map.cs`, `MapStyle.cs`, `MapRenderer.cs`, `MapRenderer.Land.cs`, `TerrainRenderer.cs`,
`GameUi.cs`, `shaders/terrain_common.gdshaderinc`, and client fog tests.
Keep preferences and overlay data in the existing client files where that avoids extra layers.

- Define four independent toggles: tile yields, resource badges, hex grid, and cultural borders.
- Preserve existing default resource and border visibility. Default new yield and grid layers to off.
- Use perceived snapshots and `BonusVisible` for resources and borders.
- Use live yields only on visible tiles. Hide yields in fog unless an existing remembered-yield contract can supply safe values.
- Hidden terrain receives no overlay data. Hex geometry must not reveal terrain information.
- Session-local preferences survive turns and modal changes. No new persisted simulation settings are required.

- Control existing resource icons and border rendering instead of adding duplicate layers.
- Add batched yield badges and terrain-draped hex grid lines.
- Apply border controls consistently to terrain, water, and river surfaces. Preserve hover and city-work highlights.
- Match wrap copies, surface height, fog, camera zoom, and refresh behavior.
- Toggle presentation without regenerating terrain geometry or mutating the world revision.
- Reuse shared shader controls for surfaces that already share border logic.

**Exit criteria:** Independent toggle and hidden-state checks pass. Simulation hash and RNG remain unchanged.
Godot build and visual checks pass for all toggles, wrap seams, fog, water, hills, and city mode.
No known regression from earlier phases.

### Encountered Serious Issues

- Implemented in isolated worktree `/tmp/civ-overlays-phase7` while agy's R-c review kept the main checkout frozen. Commit `c665713430e81c9ffd3ab8d1ed6293779988d427` was cherry-picked as `b545779a9223036b9c60fcad2294401faac70eb3` after both R-c reviewers closed. Main integration will be exercised by phase 8.
- Nine-file exception: client overlay/preferences, renderer integration, resource/yield batching, terrain uniforms, UI controls, three surface shaders, and tests. Shared terrain/water/river behavior required the extra shader file. This preserves the user's requested phase consolidation instead of combining unrelated owners.
- City mode retains work-tile labels and suppresses duplicate map yield badges. Toggles change visibility/uniforms, not terrain geometry.
- Initial new fixtures encountered a Sight name collision and assumed turn zero. Both were corrected without changing existing expectations. No existing regression was identified.
- Implementor evidence: `mise run test` 1211/1211 passed, `mise run build:game` zero warnings/errors. Logs `/tmp/civ-p7/{default-final,build-final,capture}.log`. Nine rendered captures cover defaults, all-off, individual layers, all-on, wrap/zoom, and city mode. The tour confirmed stable terrain mesh identities and unchanged simulation hash, RNG, and revisions during toggles. Physical clicks, multi-rival interaction, and arbitrary small windows remain unverified.
- Touched/new production functions have CCN at most 8. Untouched `RebuildWorld` remains 24. Diff and added-token checks passed. Main tracked files and shared plan were not edited by the implementor.

## Phase 8 — Cross-system integration and replay tests

**Owner:** sol. Depends on phases 1–7 and checkpoints R-a, R-b, and R-c.

### Dumb explanation

Check that the features still work when used together, not just on their own.
Run the same game with and without a save and compare the results.
Make sure hidden information stays hidden after turns, missions, and screen updates.
Use expected results calculated separately from the code under test.

### Requirements

Potential sites: existing research, diplomacy, espionage, client intelligence, fog, and save/determinism test owners.

- Add focused tests alongside each feature during implementation. This phase covers distinct integration risks, not duplicated unit cases.
- Replay turn advancement across calendar schedule boundaries with realistic-timescale costs and normalized research overflow.
  Prove that the date display does not become an extra input to the research formula.
- Save/reload active counter-espionage, then compare expiry, balances, RNG, and state hashes against uninterrupted execution.
- Prove that spying cannot bypass technology-trading restrictions accidentally, while theft retains its own intended eligibility.
- Exercise unlocked intelligence, point expenditure, and immediate revocation.
- Mutate hidden tiles and rival queues. Locked DTOs and fogged overlay data must remain unchanged.
- Apply the test-audit authoring gate. Each new test names its observable contract and a credible regression.
  Use independent expected values. Add no test-only production seams or source-text assertions.

**Exit criteria:** Focused suites and `mise run test` pass. Replay and hidden-state invariants hold.
No known regression from earlier phases.

### Encountered Serious Issues

- No production defect or behavior change found. One tracked test file added; no production seams or existing expectations changed. The ignored plan records this session separately.
- Authoring gate: inspected the research/economy overflow, espionage mission/expiry, client permissions, and sight/overlay owners first. Existing phase 5 tests already own numeric thresholds, directed timers, basic mission revocation and static hidden mutations. New tests own distinct turn/save lifecycle and cross-owner interactions: queued technology rate changes from 100 to 125 after saving, calendar boundary replay with a separately shifted start year, mid-protection reload plus last-active-turn spending and expiry, denied trades versus independently eligible theft, and hidden queue/tile persistence with presentation activity on only one replay peer. Expected calendar years, cost percentages, overflow, balances and permission thresholds are independent literal vectors.
- Initial new-test failures were fixture errors: disabling revolution gives a base cost of 53, not the 43 vector that retains its cost stage. Independent arithmetic is 53*88/100=46, excess 62-46=16, normalized overflow 12; later 12 overflow at the changed 125-percent rate contributes 15 plus one base beaker. The new rejection-message literal was corrected to the owner's existing message. No existing test was altered.
- Focused `mise exec -- dotnet test tests/Civ.Mods.Tests/Civ.Mods.Tests.csproj --filter 'FullyQualifiedName~FeatureIntegrationReplayTests'` passed 5/5. `mise run test` passed 1216/1216 (37 importer, 135 core, 1044 mod). `mise run build:game` passed with zero warnings/errors both with the temporary rendered probe and after its removal. Logs: `/tmp/civ-p8/{focused-final,default,build-final,input-build}.log`. No release, E2E, extractor-category or race suites ran.
- Closed the concrete input gap using existing Xvfb, xdotool and Godot compatibility tooling, without installing dependencies. Eight OS pointer clicks toggled all four overlay controls, opened espionage, and selected rival 1 → rival 2 → rival 1. Checks verified control states, selected-rival disabled buttons, distinct 2222/3333 gold reserves, and unchanged simulation hash/RNG. Eight captures and the passing log are `/tmp/civ-p8/input-{1..8}.png` and `/tmp/civ-p8/input.log`; harness source is `/tmp/civ-p8/P8Input.cs`. Temporary live probe files were removed, not committed. Inspected the second-rival rendered capture.
- Input evidence uses genuine X11 mouse events delivered by xdotool, not control-signal emission. Rendering uses Mesa llvmpipe at 1600×900, not a physical GPU or human-operated mouse. Arbitrary small-window behavior remains unverified; this input probe does not repeat phase 7's surface/wrap rendering tour. Existing top-bar overflow and nonfatal XIM/V-sync warnings remain outside scope.
- Lizard reports test functions at CCN at most 5. `git diff --check` passed. No regression fix needed, so no pre-fix worktree was required. R-e and documentation work were not started.

## Phase 9 — Run release checks and update documentation

**Owner:** `agy(model=flash)`. Depends on checkpoint R-e. No review checkpoint.

### Dumb explanation

Run the final checks and record what actually passed.
Then update the instructions and roadmap to match what the game now does.
Do not mark unfinished work as complete or claim that a skipped check passed.
Record any remaining limits so the next person can find them.

### Requirements

Potential sites: `docs/ROADMAP.md`, `README.md`, and the relevant mechanics documents under `docs/`.

- Run the required checks and inspect the resulting feature behavior.
- Record completed roadmap items, final formulas, defaults, controls, data contracts, and NOTE findings.
- Include the corrected counter-espionage description and explicit exclusion of player-specific queues.
- Mark only completed items as done. Leave player-specific queues unchecked.
- Describe calendar progression, final research formulas, espionage permissions, cost effects, and overlay controls.
- Record unsupported reference dependencies and residual NOTE findings with severity and reachability.
- Keep the roadmap canonical. Do not create a second status inventory.

**Exit criteria:** Required release checks, documentation checks, and `git diff --check` pass.
Statements match the release evidence.
No known regression from earlier phases.

### Encountered Serious Issues

- Release checks executed directly from clean HEAD `212d8522bf66236c2d9ea670df334b3c7585729a`:
  - `mise run test`: passed 1216/1216 (37 importer, 135 core, 1044 mod). Duration 2m 19s. Exit code 0. Log `/tmp/civ-p9/test-default.log`.
  - `mise run test:e2e`: passed 4/4 in `Civ.Mods.Tests`. Duration 13m 24s. Exit code 0. Log `/tmp/civ-p9/test-e2e.log`.
  - `mise run test:extractor`: passed 5/5 in `Civ.ContentImport.Tests`. Duration 45s. Exit code 0. Log `/tmp/civ-p9/test-extractor.log`.
  - `mise run build:game`: Build succeeded, 0 Warning(s), 0 Error(s). Duration 8.09s. Exit code 0. Log `/tmp/civ-p9/build-game.log`.
  - `xvfb-run -a godot-mono --headless --path src/Civ.Godot --quit-after 200`: "Loaded 19 mods; content frozen.", 0 errors, 200 frames. Exit code 0. Log `/tmp/civ-p9/godot-headless.log`.
  - `git diff --check`: passed with exit code 0.
- Existing visual and input evidence under `/tmp/civ-p6`, `/tmp/civ-p7`, and `/tmp/civ-p8` reviewed; phase 8 verified actual-pointer X11 clicks and multi-rival switching.
- Note decisions: preserved residuals (eligible-team denominator, score contract, commander/ground-health interception, malformed content, inert expired records, hardware GPU/small-window); did not fix code or weaken tests.
- Documentation updated in canonical `docs/ROADMAP.md`, `README.md`, `docs/ESPIONAGE.md`, and new focused mechanics guide `docs/RESEARCH.md`. Standalone documentation commit: `dcecac4c57fb2a9ad7e28e01e3fd6b1c98fdcf47`.
- Factual reconciliation: addressed wording conflicts in docs with verified reference facts via standalone corrective commit `f76b5f0c523d8ea1ec917195ea1bf5a81884795c`. Reconciled diffusion denominator (eligible living non-barbarian teams with contacted holders as weighted numerator; parity fix aligns alive-team/barbarian eligibility), welfare evaluation (team leader player total score contract), counter-espionage scaling (`200 + 5 * InterceptionPercent` baseline 200%), interception input scope (counter mission calculation, not air/CAP), small-window scope (Phase 6 verified 1280x720; arbitrary sweeps unverified), explicit unsupported no-tech-brokering option, and expired record cleanup (deterministic turn boundary/hash contract, not serialization).

## Test Strategy

- Run focused tests for each changed owner during each phase.
- Run `mise run test` by default. It excludes E2E and extractor tests.
- Run `mise run test:extractor` after importer or generated content changes.
- Run `mise run test:e2e` after integration to detect AI, turn-loop, and replay regressions.
- Run `mise run build:game` for client changes.
- Run `xvfb-run -a godot-mono --headless --path src/Civ.Godot --quit-after 200` for startup and shader checks.
- Capture visual evidence through the existing `mise run shot` workflow. Headless startup alone does not prove overlay correctness.
- Run `git diff --check` before phase commits.
- Compare representative research rates, dates, and mission costs with independently calculated reference examples.
- Record commands actually run, failures, and environmental blockers. A skipped check is not a passing check.

## Review Checkpoints

R-a examines phases 1–2: calendar data, arithmetic, display, and starting-era offsets.
R-b examines phases 3–4: research formulas, overflow, gates, and custom-content fallbacks.
R-c examines phases 5–6: espionage state direction, expiry, permission boundaries, and read-only UI.
R-e examines phases 7–8: fog safety, independent toggles, rendering, simulation isolation, and cross-system integration evidence.
There is no R-d checkpoint. Phase 9 has no additional review checkpoint.

### R-a — Dumb explanation

Check that the game converts turns into the right dates and shows those dates correctly.

- **Round 1 (`fc7e514df97345fdda5d861be7dacac626e011c3`):**
  - `agy(model=flash)` (`d340e467-2dd4-442f-b0b1-c551041b899f`): 0 DEMANDs, 3 NOTEs (February start month `iMonth=1` invisible at 480-month increments; random-access tail continuation vs reference uninitialized local; realistic-timescale setup-policy default handoff to Phase 3).
  - `ds4` (`a16f142d-2dc2-434e-8ac6-bac72207f1cc`): 1 DEMAND (`D1`: `GameCalendar` captured `startYear` from foundation's file at registration instead of resolving effective `RuleConstantsDef` from `sim.Content` at query time), 10 NOTEs (`N1` Phase 2 evidence recorded; `N2` 0-based `elapsedTurns` vs 1-based `State.Turn` naming; `N3` `iDayIncrement` not present in effective content so `CalendarPrecision.Day` unreachable; `N4` custom final increment != 12 months tail divergence; `N5` BCE/CE vs BC/AD wording; `N6` dependent-mod invalid schedule fails at query time; `N7` minor whitespace in `rule-constants.json`; `N8` only turn bar displays calendar date; `N9` headless `GameFactory` doesn't populate `SetupPolicies` defaults so Phase 3 must read `SetupPolicy` default; `N10` `RuleConstantMapping.json` key order cosmetic).
- **Round 2 (`3ccc34f1224b1b12e98e377e317fddf9094aa7a8`, fixed in `ce746921-8fb3-488b-ad2f-bd91e05ccb33`):**
  - Pre-fix failure verified in `/tmp/pre-fix-ra`.
  - Both `ds4` (`a16f142d-2dc2-434e-8ac6-bac72207f1cc`) and `agy(model=flash)` (`d340e467-2dd4-442f-b0b1-c551041b899f`) confirmed `D1` resolved (`ACCEPT`, 0 DEMANDs, 15,307-point schedule parity intact, 61/61 targeted tests passed). Additional minor NOTEs `N11` (`ValidateShipped` uses `int.MaxValue` bound) and `N12` (`SingleOrDefault() ?? 0` fallback) recorded. Checkpoint R-a closed.

### R-b — Dumb explanation

Check the research math and make sure technology trades obey the same rules through every entry point.

- Round 1 at `a6b2389f0137b043ecae6cacf0607cbee5481c28`: agy reported 0 DEMANDs and 4 NOTEs. ds4 reported one DEMAND: self-held repeatable technology incorrectly received the open-border diffusion weight.
- Fix `227486cbf24eda3fa20b304fa517a0703a984036` gives self-held technology the neutral weight without changing diplomacy semantics. The original implementor session was `63b81bfb-bd96-4031-a4eb-3643b15098e0`.
- Round 2 at that frozen fix commit: both independent reviewers confirmed the fix, reproduced pre-fix failures in temporary worktrees, and reported 0 DEMANDs. The two cases changed from 114 to 104 and from 138 to 126. Both confirmed prior R-a items still hold.
- ds4 task `subtask_pawn_1792273592109_8d954d66`, session path `/home/gordon/.pi/agent/sessions/--home-gordon-workspace-civ--/2026-10-17T21-46-32-129Z_01a14bd4-af41-71bf-8dbd-9764f52c30d2.jsonl`: 71 research/economy, 20 calendar, and 68 client/diplomacy tests passed. Both new regression cases passed and failed before the fix.
- agy conversation `8bfd6705-1307-49ce-a8e8-a7869b5e6da4`: 77 research, 84 diplomacy/network/corporation, 20 calendar, and 41 client tests passed. Both regression cases failed without the fix. No live tracked edits.
- Preserved NOTEs: low, normally reachable eligible-team denominator excludes barbarians and can shift diffusion thresholds. Low-to-moderate, late-game welfare uses the existing score contract rather than the native normalized curve. Low, shared-team welfare/era uses the leader. Low, custom content has an extra diffusion-constant gate and some untested neutral guards. Negligible, absent era-cutoff options and millionths ledger units. Low, no-tech-brokering remains unsupported without acquisition provenance, reachable when selected. Low, empty-member teams are unreachable in normal play. The either-team unlock rule is reference behavior, not a defect.
- Integration handoff: include a rate-changing overflow test in phase 8. The cosmetic indentation NOTE is closed. Checkpoint R-b closed.

### R-c — Dumb explanation

Check that counter-espionage affects the right rival for the right time, and that the screen does not reveal private information.

- Round 1 at `3cb1b509b52a40caa7abe5c11bd6f5201bc3eb13`: ds4 independently confirmed directed pairs, speed/expiry/rounding, repeat rejection, save/hash, permission filtering/revocation, read-only UI, refresh and mission action. No DEMANDs. Passed 40 espionage/client and 78 research tests, plus Godot build with zero warnings/errors. Prior R-a/R-b items hold.
- agy independently approved the same frozen commit with 0 DEMANDs and 4 NOTEs, conversation `0a7cd144-86be-4e19-9674-3f641593c966`. Passed 40 espionage/client tests, 135 calendar/research/diplomacy tests, the 1209-test default suite, and Godot build. Reviewed DTO/permission/expiry/UI owners and rendered artifacts. Prior R-a/R-b items hold. NOTEs preserve unsupported interception inputs, intended passive-threshold scaling, signal-driven capture limits, and unverified multi-rival physical interaction. No tracked edits. Checkpoint R-c closed.
- ds4 NOTEs: low, real pointer input and multi-rival switching remain unverified by the signal-driven ten-step capture. Low, commander and optional ground-health interception inputs remain unsupported. Low, malformed content with unknown research IDs can fail when that section unlocks. Negligible, expired protection records remain inert in saves. Negligible, malformed zero-health-denominator and extreme theft-price overflow guards remain outside this change. Carry these to final verification/documentation.

### R-e — Dumb explanation

Check the map switches and the combined game behavior.
Look for bugs that appear only after several features interact or after a save is loaded.

- Round 1 at `212d8522bf66236c2d9ea670df334b3c7585729a`: ds4 reported 0 DEMANDs and 3 NOTEs. Independently checked perceived fog data, bonus concealment, visible-only yields, all toggle combinations, defaults, shared shader behavior, wrap/city rendering, no simulation or mesh mutations, and independent integration expectations. Confirmed eight actual X11 clicks and rival switching in the captured artifacts. Prior R-a/R-b/R-c items hold. agy independently approved this same commit with 0 DEMANDs and 3 NOTEs, conversation `5dc42047-0a84-4a92-8a06-d9362247ef19`.
- agy verification: default suite 1216/1216, build zero warnings/errors, 5 replay tests, 20 sight-memory tests, 124 earlier-feature tests. Independently inspected all nine render captures and eight actual X11 pointer/multi-rival interactions, and confirmed previous checkpoints remain intact. Deferred E2E/extractor to phase 9. NOTEs preserve harmless headless driver warnings, supported-interception scope limits, and arbitrary small-window coverage. No tracked edits. Checkpoint R-e closed.
- ds4 verification: 20 sight-memory tests, 5 integration tests, 183 earlier-feature owner tests, 68 client tests, default suite 1216/1216, and Godot build with zero warnings/errors.
- Preserved NOTEs: low cosmetic, derivative behavior at the exact wrap seam was not swept across every zoom level, with no visible defect reproduced. Low scope, arbitrary small-window control overlap remains unverified. Negligible custom-content limit, remembered bonus IDs removed from content can throw on lookup. No normal-use defect was reproduced. Physical-GPU rendering remains unverified.

## Review Protocol

**Verdicts.** Every finding gets exactly one:

- **ACCEPT** — The reviewer reproduced the claimed behavior or absence of a defect and confirms it. No action or record is necessary.
- **NOTE** — A caveat, nit, or unreachable edge case needs a record, but not a change. Carry it into this plan and the closing summary.
- **DEMAND** — A change is required before the checkpoint closes. Include a minimal reproduction and an exact implementable scope.

**Review scope.** Every review states which areas and behaviors it examined, how it examined them, and what it did not examine or run.
Reviewers assess whether relevant tests support phase claims. They need not prove an entire defect category exhausted.
A coverage gap requires a specific concern with a reachable scenario. Every DEMAND requires a minimal reproduction and an exact implementable scope.

## Stopping Rule

Complete means all requested mechanics meet their exit criteria, required checks pass, and every named checkpoint closes.
Remaining NOTE findings require severity, reachability, and a clear description in the closing summary.
Unverified reference formulas, permission leaks, or a reachable normal-use regression prevents completion.
Multiplayer queues, new vassal mechanics, unrelated espionage missions, and general AI espionage strategy remain out of scope.

## User-supplied XML references

These snippets match the local expansion-layer records. The user identified them as original-game examples.
They do not override the effective mod content.

### Normal

```xml
<GameTurnInfos>
<GameTurnInfo>
<iMonthIncrement>480</iMonthIncrement>
<iTurnsPerIncrement>75</iTurnsPerIncrement>
</GameTurnInfo>
<GameTurnInfo>
<iMonthIncrement>300</iMonthIncrement>
<iTurnsPerIncrement>60</iTurnsPerIncrement>
</GameTurnInfo>
<GameTurnInfo>
<iMonthIncrement>240</iMonthIncrement>
<iTurnsPerIncrement>25</iTurnsPerIncrement>
</GameTurnInfo>
<GameTurnInfo>
<iMonthIncrement>120</iMonthIncrement>
<iTurnsPerIncrement>50</iTurnsPerIncrement>
</GameTurnInfo>
<GameTurnInfo>
<iMonthIncrement>60</iMonthIncrement>
<iTurnsPerIncrement>60</iTurnsPerIncrement>
</GameTurnInfo>
<GameTurnInfo>
<iMonthIncrement>24</iMonthIncrement>
<iTurnsPerIncrement>50</iTurnsPerIncrement>
</GameTurnInfo>
<GameTurnInfo>
<iMonthIncrement>12</iMonthIncrement>
<iTurnsPerIncrement>120</iTurnsPerIncrement>
</GameTurnInfo>
<GameTurnInfo>
<iMonthIncrement>6</iMonthIncrement>
<iTurnsPerIncrement>60</iTurnsPerIncrement>
</GameTurnInfo>
</GameTurnInfos>
```

### Marathon

```xml
<GameTurnInfos>
<GameTurnInfo>
<iMonthIncrement>180</iMonthIncrement>
<iTurnsPerIncrement>100</iTurnsPerIncrement>
</GameTurnInfo>
<GameTurnInfo>
<iMonthIncrement>120</iMonthIncrement>
<iTurnsPerIncrement>300</iTurnsPerIncrement>
</GameTurnInfo>
<GameTurnInfo>
<iMonthIncrement>60</iMonthIncrement>
<iTurnsPerIncrement>170</iTurnsPerIncrement>
</GameTurnInfo>
<GameTurnInfo>
<iMonthIncrement>24</iMonthIncrement>
<iTurnsPerIncrement>201</iTurnsPerIncrement>
</GameTurnInfo>
<GameTurnInfo>
<iMonthIncrement>12</iMonthIncrement>
<iTurnsPerIncrement>129</iTurnsPerIncrement>
</GameTurnInfo>
<GameTurnInfo>
<iMonthIncrement>6</iMonthIncrement>
<iTurnsPerIncrement>180</iTurnsPerIncrement>
</GameTurnInfo>
<GameTurnInfo>
<iMonthIncrement>3</iMonthIncrement>
<iTurnsPerIncrement>264</iTurnsPerIncrement>
</GameTurnInfo>
<GameTurnInfo>
<iMonthIncrement>1</iMonthIncrement>
<iTurnsPerIncrement>156</iTurnsPerIncrement>
</GameTurnInfo>
</GameTurnInfos>
```
