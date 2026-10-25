# Test audit — consolidated report

Repo: `/home/gordon/workspace/civ` (C# /.NET 10, xUnit, Godot client, 4 shipped mods).
Skill: `~/.agents/skills/test-audit` (AUDIT / discovery mode).
Date of sweep: this session. Four parallel read-only subagent lanes; no repo file was
modified, nothing was built or executed.

---

## 1. Method and scope

Inventory came from `find tests -name '*.cs'` plus the `mise.toml` task definitions:

- `mise run test` → `Category!=Extractor&Category!=E2E` (the default surface audited here)
- `mise run test:e2e` → `Category=E2E`
- `mise run test:extractor` → `Category=Extractor`

**Total in-tree test surface: 119 `.cs` files, 728 test methods (537 `[Fact]` + 191
`[Theory]`), 772 `[InlineData]` rows → ≈1 309 executed cases.**

Excluded from this audit:

| Excluded | Where | Why |
|---|---|---|
| 6 `[Trait("Category","E2E")]` tests | `AiTests.cs` (4), `ResearchEconomyTests.cs:456`, `GovernmentPeoplesTests.cs:186` | Not part of `mise run test`; user asked for non-E2E |
| 4 extractor tests / 2 files | `tests/Civ.ContentImport.Tests/{ExtractionSmokeTests,EraProgressionTests}.cs` | `Category=Extractor`; needs an external extraction location, excluded by repo policy from the default run |

Everything else — 90 test files, 20 995 lines, 728 methods — was split into four coherent
lanes and audited in parallel.

| Group | Theme | Files | Lines | Methods | E2E skipped | Candidates |
|---|---|---|---|---|---|---|
| G1 | Core engine, contracts, mod loading, **test infrastructure** (`TestRuleset/`) | 31 | 3 784 | 115 | 0 | 13 |
| G2 | City, society, economy, research rules | 16 | 5 443 | 216 | 2 | 10 |
| G3 | World, terrain, military, diplomacy, espionage, events | 26 | 6 389 | 216 | 0 | 12 |
| G4 | AI, client/session, network, content import | 17 | 5 379 | 181 | 4 | 11 |

Full per-candidate evidence tables, retained-false-positive lists and follow-ups live in
`/tmp/test-audit/report-G{1..4}.md`. Group manifests: `/tmp/test-audit/G{1..4}-*.md`.

---

## 2. Headline verdict

The suite is **larger and healthier than its file count suggests**. Two lanes (G2, G4)
reported "unusually well owned": ownership comments naming the owning service, hand-computed
vectors, hash/replay as the determinism boundary, and client tests that drive the real
`GameSession` API Godot calls rather than mocks. The junk that does exist clusters into five
recurring patterns, not dozens of one-offs.

The bigger finding is not deletable tests — it is **contracts that are nominally covered but
cannot fail** (section 5). Four of those are latent product-bug surfaces.

---

## 3. Root-cause categories (cross-group)

**R1 — Content inventories / declared-value restatements.** A test re-types a table that
already exists in mod JSON or in a production switch. It fails when a designer edits data and
cannot detect a wrong value. Worst offender: `TerrainPaletteTests` (three tests restating
`TileAppearanceService.cs:12-25`, written in the same commit `1681a39` that added the data).
Others: `CalendarTests.cs:106` (`rule-constants.json` snapshot), `GovernmentPeoplesTests.cs:177`
(`leader.json` traits), `CityLayoutTests.cs:114` (`CityAppearanceService.cs:9-20`),
`BonusModelTests.cs:24/81`, `ModContentTests.cs:36` (`emitted-counts.json` + hard-coded `12`),
`RoutePlanTests.cs:16`, `WorldRulesTests.cs:43` (`MapSpec(84,52)`), `AiEvaluationTests.cs:26`.

**R2 — Second-layer replay of a contract owned elsewhere.** The same behaviour re-asserted
through a thinner projection or a second consumer. `CityRulesTests` is the main source
(great-person pools → `GreatPeopleRulesTests`, hurry anger → `ResearchEconomyTests:363`,
power health → `WorldRulesTests.Warming:104`), plus `WorldRulesTests.Warming:122` (area-clean
duplicates `CityRulesTests:918`) and `CliffRulesTests:38-39` (repeatability duplicates
`WorldRulesTests:39` full-state hash for identical settings/seeds).

**R3 — Assertion hygiene: expected values produced by the code under test.** `TurnsToSpawn ==
SpawnInterval - ElapsedTurns` recomputes the client's own one-liner from the same two DTO
fields; `Assert.All(builds, b => Assert.Null(b.Command.Validate(sim)))` re-runs the exact
validator the production filter already ran; `Status == "preserved"` asserts a record's
*default parameter* that `FieldCensus.Build` never sets; `AiCalculationTests.ScanSiteScore`
re-implements `SitePlanner.Score`'s eight weights.

**R4 — Negative controls that pass for an unrelated reason (and names that promise more
than the input exercises).** `Naval_unit_on_water_upgrades_…` asserts only
`NotEqual("No eligible nearby training city", error)` — five other rejection strings satisfy
it. `HazeVolumeTests.Storm_out_of_sight_is_toned_down` asserts inequality, not darkening.
`CitiesCannotWorkEachOthersTiles` puts the second city 3 tiles away with a radius-2 work area,
so the exclusion it names can never fire. Also `Tech_discovery_notifies_only_the_discovering_team`
(no rival team), `Worker_repairs_ruins_with_a_farm` (accepts merely *starting* the build),
`Capital_relocation_…_uses_lowest_id_ties` (the two cities differ, so `ThenBy(Id)` never decides).

**R5 — Test-only production surface.** Six client/core members whose only callers are tests:
`GameSession.{MarkOf, DescribeStability, PendingBattles}`, `TerrainChunks.TilesOf`,
`ContentDb.CultureLevelFor`, `PromotionDef.HealBonus`. Plus three already-dead members
(`TerrainPalette.TerrainColorOf`, `CityLayout.TileEdge`, `Sites.PlaceStartingUnits`) and a
structural question about `tests/Civ.Tests/TestRuleset/` (a second ruleset that only tests use).

---

## 4. Consolidated candidate list, by action

### Tier A — delete (or delete the listed assertions); low risk

| # | Test | File:line | Why | Unlocks |
|---|---|---|---|---|
| A1 | `UnitDataLoadsFromTheBaseMod` | `Civ.Tests/UnitTests.cs:19` | Fixture inventory (`Count == 5`, `base:shock`); every other test in the file loads the same fixture | — |
| A2 | `BaseGameRegistersTerrainTechsBuildingsAndCulture` | `Civ.Tests/ModTests.cs:10` | Same, plus **sole caller** of `ContentDb.CultureLevelFor` | Delete `ContentDb.CultureLevelFor` (8 LOC, zero non-test callers) |
| A3 | `RangeSize`, `RingHasSixRHexesAllAtDistanceR`, `NeighborsAreAtDistanceOne` | `Civ.Tests/HexTests.cs:20,24,8` | Count formulas; `HexGridTests:65/116/99` prove exact membership vs a BFS oracle (strictly stronger). Keep `PixelRoundTrip` | — |
| A4 | `RngIsDeterministic` | `Civ.Tests/HexTests.cs:43` | Twin-instance self-comparison; seed reproducibility proven by `CivAndAiTests:39`, `ContractTests:322` | — |
| A5 | `BaseCivilizationsLoadAndGrantStartingTechs` | `Civ.Tests/CivAndAiTests.cs:12` | Duplicate of `ContractTests:113-122` on the same fixture; only extra datum is a fixture name string | — |
| A6 | "options must not be registered" `foreach` | `Civ.Mods.Tests/ModContentTests.cs:70-72` | Negative inventory: fails when content improves, cannot fail on a regression | — |
| A7 | `emitted-counts.json` loop + `Assert.Equal(12, …)` | `Civ.Mods.Tests/ModContentTests.cs:36` | Generated-inventory copy; `emitted-counts.json` has no production reader. Keep the reference-closure + content-hash half | — |
| A8 | `Great_person_pools_survive_saves_and_birth_advances_player_threshold` | `Civ.Mods.Tests/CityRulesTests.cs:449` | `GreatPeopleRulesTests:30` is a strict superset (progress, pools, save hash, RNG, threshold×2, pools cleared, event) | `probe:academy` in `CityRulesTests:1694` |
| A9 | `Era_picks_style_and_ground` | `Civ.Mods.Tests/CityLayoutTests.cs:114` | Verbatim copy of `CityAppearanceService.cs:9-20`; `Every_shipped_era_has_a_city_look:121` keeps the real guard. Reduce to 2-3 rows if you want a smoke | — |
| A10 | `Water_layers_fade_into_each_other…`, `Sand_and_grass_keep_their_direction…`, `Lush_ground_spreads…` | `Civ.Mods.Tests/TerrainPaletteTests.cs:68,50,58` | Restate `terrain.json isWater` / `TurnDegrees` / `Spread` through one-line palette copies; names claim blending behaviour that is never exercised | Replace with one real `TileDataTexture` row-layout + `MaxLayers` truncation test (G3 follow-up 3) |
| A11 | `TurnsToSpawn == SpawnInterval - ElapsedTurns` | `Civ.Mods.Tests/ClientRevolutionWondersTests.cs:92` | Self-comparison against `GameSession.Revolution.cs:34` | — |
| A12 | `Assert.All(builds, b => Assert.Null(b.Command!.Validate(sim)))` | `Civ.Mods.Tests/ClientSessionTests.cs:241` | Re-runs the validator `GameSession.Units.cs:190-201` already ran. Keep the dispatch half | — |
| A13 | `Assert.All(census.Fields, x => Assert.Equal("preserved", x.Status))` | `Civ.ContentImport.Tests/MergeTests.cs:107` | Asserts a record default parameter that `FieldCensus.Build` never assigns | — |
| A14 | permission-threshold arrays `{25,50,75,100}` / `{50,100,150,200}` | `Civ.Mods.Tests/ClientIntelligenceEventsTests.cs:182,282` | Thresholds owned by `EspionageRulesTests:66-93`. Keep the 8-row gating theory | — |
| A15 | cliff repeatability pair | `Civ.Mods.Tests/CliffRulesTests.cs:38-39` | `WorldRulesTests:39` hashes the same six generators/seeds; keep the beach/river-mouth assertions | one world build per generator-case |
| A16 | cliff budget `InRange(.15,.25)` | `Civ.Mods.Tests/CliffRulesTests.cs:44` | Restates `CliffGenerator.TargetPercent = 20`; a tuning knob, not a contract | — |
| A17 | `Assert.Equal(new MapSpec(84,52,true), …)` | `Civ.Mods.Tests/WorldRulesTests.cs:43` | Restates `foundation:worldsize_standard`; `WorldLayoutTests:29-51` already exercises three sizes | — |
| A18 | `Enum.GetValues<RouteLook>().Length == routes.Length` | `Civ.Mods.Tests/RoutePlanTests.cs:16` | Ties content count to enum cardinality; the `Distinct()` line is the real contract | — |
| A19 | `Assert.Equal(51…/15…)` counts | `Civ.Mods.Tests/BonusModelTests.cs:24-27` | The `Assert.All(... ModelPath(id) is not null)` and file-existence loop already fail on a missing/extra GLB | — |
| A20 | `Sea_resources_spawn_in_the_water` | `Civ.Mods.Tests/BonusModelTests.cs:81` | Literal 7-id list vs the test's own copy of the `SeaOnly` rule; seed-luck. Fold `SeaOnly ⇒ wet` into `Generated_resources_respect…:52` first | one seed-luck dependency |

### Tier B — tighten, rewrite, or move (these are latent holes, do not blind-delete)

Status: B1/B2/B3/B5 landed in PR-3; B4 and B6-B18 landed in PR-6. Nothing in this tier is open.

| # | Test | File:line | Problem | Fix |
|---|---|---|---|---|
| B1 | `CitiesCannotWorkEachOthersTiles` | `Civ.Tests/CityTests.cs:134` | Vacuous: city `b` at distance 3 vs `Radius == 2`, plus a silent `return` guard. The rule it names is unproven **everywhere**, including the shipped `CityWork.CanWork` (`src/Mods/Cities/CityWork.cs:44`) | Rebuild at distance 2 against `CityWork.CanWork`; then delete the fixture version |
| B2 | `Naval_unit_on_water_upgrades_from_an_adjacent_coastal_city` | `Civ.Mods.Tests/MilitaryRulesTests.cs:890` | Only guard of `UnitUpgrades.SameArea`'s water clause, and it passes for five other rejection reasons | `Assert.Null(Validate(...))`, then apply and assert type / treasury / `MovesLeft == 0` |
| B3 | `Storm_out_of_sight_is_toned_down` | `Civ.Mods.Tests/HazeVolumeTests.cs:53` | Asserts inequality; a brightening tint passes | Assert mean luminance drops and bytes are ≤ visible |
| B4 | `Draft_selection_uses_the_same_plot_context_as_production_eligibility` | `Civ.Mods.Tests/CityQueryContractsTests.cs:42` | Pins call shape (`context.Plot == city tile`); no shipped evaluator distinguishes explicit tile from `CityRequirements.Location`'s default, so it breaks on a behaviour-preserving change | Re-anchor on a real plot-dependent leaf (`condition:foundation:river`) |
| B5 | `Capital_relocation_…_uses_lowest_id_ties` | `Civ.Mods.Tests/CityRulesTests.cs:1493` | Tie-break half unattributable; `CityCapital.cs:11 ThenBy(Id)` has no test | Build a genuine tie, or drop `_uses_lowest_id_ties` from the name |
| B6 | `Settlement_sites_match_independent_spacing_and_score_scans` | `Civ.Mods.Tests/AiCalculationTests.cs:572` (helper `:705-720`) | `ScanSiteScore` copies `SitePlanner.Score`'s eight weights; only the enumeration is independent | Keep tile/area set equality, drop `Score` equality, rename |
| B7 | four golden whole-state hashes | `AiCalculationTests.cs:50,89,352,389` | `StateHasher` FNV over *all* state; the same tests already compare queue/command arrays across variants structurally | One variant-equality test (plain/wave/reload hashes equal each other), drop the constants |
| B8 | `Research_rule_inputs_retain_reference_values…` | `Civ.Mods.Tests/CalendarTests.cs:106` | `rule-constants.json` snapshot; also misfiled (research constants in the calendar file). Every value has a behavioural owner except `new RuleConstants().RevolutionResearchCostPercent` | Keep the two `new RuleConstants()` default lines, drop the JSON snapshot, move file |
| B9 | `Players_start_with_one_civic_per_option_from_civilization_or_option_default` | `Civ.Mods.Tests/GovernmentPeoplesTests.cs:30` | `Assert.Equal(8, …)` is the JSON row count, and **every shipped civ's `StartingCivics` equals the option defaults**, so the "from civilization" half is unobservable | Anchor on `CivicOptionDef` registry count; add one test civ whose `StartingCivics` differs |
| B10 | `Cultural_victory_requires_eligible_legendary_tiers…` | `Civ.Mods.Tests/CultureRulesTests.cs:101` | Real proof of a `ProjectsVictory` contract, living in the Culture file | Move into `ProjectsVictoryRulesTests` as a row |
| B11 | `Hurry_anger_uses_free_buildings_but_not_replaced_buildings` | `CityRulesTests.cs:673` | Replays `ResearchEconomyTests:363`; only free→Active→anger composition is unique | Add one row to the existing whip-anger table |
| B12 | `Power_health_changes_the_actual_health_ledger…` | `CityRulesTests.cs:962` | Replays `WorldRulesTests.Warming:104`; unique part is the ledger lines | Fold those two ledger assertions into the warming weights theory |
| B13 | `Clean_power_in_the_city_or_team_area_removes_dirty_power_pollution` | `WorldRulesTests.Warming.cs:122` | Duplicates `CityRulesTests:918` plus the `"clean"` row of its own theory | One more `InlineData` row, drop the method |
| B14 | `Tech_discovery_notifies_only_the_discovering_team` | `ClientFollowupTests.cs:230` | "only" half unproven: no rival-team discovery | Add the negative, or rename |
| B15 | `Worker_repairs_ruins_with_a_farm_on_a_worked_freshwater_tile` | `AiTests.cs:318` (assert `:335`) | Satisfied by merely *starting* the build | Assert `BuildProgress` + queued `StartBuildCommand`, or rename to `..._starts_a_farm_repair` |
| B16 | `Leader_personality_is_exposed_through_core_query` | `GovernmentPeoplesTests.cs:177` | `leader.json` traits snapshot | Rely on `:143` (traits reach ledgers) |
| B17 | `Cli_records_repeatable_games_settlements_events…` | `AiEvaluationTests.cs:26,34` | Sample-grid literals + `RulesPolicy` round-trip | Derive from args/`EvaluationConfig` |
| B18 | profile-path exception | `Civ.Mods.Tests/ModManagerTests.cs:112` | Pins `UnauthorizedAccessException` (OS detail) | `ThrowsAny<IOException>`, fold into the existing profile theory |

### Tier C — consolidate / deduplicate (net-negative test LOC)

- **C1** Three byte-identical `CommandCodec` round-trips (`SimulationTests.cs:56`, `UnitTests.cs:140`, `CityTests.cs:149`) → one `[Theory]` in `ContractTests` with payload-shape rows.
- **C2** `Equivalent_roots_are_scanned_once…` (`MergedModDiscoveryTests.cs:66`) merges into the symlink sibling `:84` as extra rows.
- **C3** `Fortify_changes_the_unit_activity` (`ClientSessionTests.cs:279`) covers 1 of the 5 rows `GameSession.Units.cs:95-97` emits → one 5-row table test (fortify/sleep/awake/intercept/blockade).
- **C4** `Session_annexes_an_existing_puppet_city` (`ClientSessionTests.cs:567`) is a one-line delegate to the row already in the conquest theory at `:545` → add a "no pending choice" row, delete.
- **C5** `UnitTests.TwoAdjacentLandHexes()` re-inlines `TestGame.FindLand` → extend the helper.
- **C6** Byte-identical brute-force triangle-height helpers: `TerrainMeshTests.MeshHeight:110-126` and `RiverNetworkTests.GroundHeight:242-258` → one `TerrainTestMaps` helper.
- **C7** `AiPromotesItsVeterans` (`UnitTests.cs:149`) is an 80-turn soak asserting a **test-only AI policy** — move to E2E with the other soaks (`602d027` set the precedent) rather than delete.

---

## 5. Contracts that are nominally covered but cannot fail (highest-value findings)

These are the reason not to treat this as a deletion exercise.

1. **Inter-city tile contention has no real proof.** `CitiesCannotWorkEachOthersTiles` is
   vacuous (B1) and no other test puts two cities close enough to contend. `CityWork.cs:44`
   (`CanWork` → `!Cities.Any(c => c.Worked…)`) is unguarded in shipped code.
2. **AI water-upgrade area rule** is asserted only by absence of one error string (B2) —
   `UnitUpgrades.SameArea:72-79` can regress silently.
3. **`PromotionDef.HealBonus` was dead surface.** The only reader was the test ruleset
   (`TestRuleset/TurnSystems.cs:105`); no shipped promotion set it; and the test that looked like
   coverage (`UnitTests.RestingHealsAndRefillsMoves:109`) added `base:medic1` *after*
   `MovesLeft = 0`, so the branch was never reached. **Closed** — deleted with the fixture term and
   the two fixture promotions that only fed it (§10 follow-ups). The shipped heal model is the
   schema promotion's six-field family, so there was nothing to wire it into.
4. **Capital tie-break** (`CityCapital.cs:11 ThenBy(Id)`) has no test (B5).
5. **The terrain palette's real renderer contract is untested while three data restatements
   stand in for it.** `MaxLayers`/`ColorOnlyLayer` truncation (`TerrainPalette.cs:21-24,56`) and
   the `TileDataTexture` `TurnRow`/`EdgeRow` byte layout (`TileDataTexture.cs:95-103`) have zero
   tests — and this is the only C#-half of a documented shader-parity formula.
6. **`NewGameSetup.cs:51-59`'s `civilization.StartingCivics` override is unobservable** with
   shipped content (all 38 civs equal the six option defaults). Closed in PR-6 (B9) by a probe
   civilization that names its own starting civic.
7. **AI planner tests bound by string.** `AiCalculationTests` / `AiIntentTests` used
   `Assembly.GetType("Civ.Mods.Ai.*")` + `GetMethod`. Because `Civ.Mods.Ai` is
   `ReferenceOutputAssembly=false`, any planner rename failed at **runtime** with a
   `NullReferenceException`, not at compile time. **Closed** — one `AiBindings` helper now does every
   lookup and names the missing type or member. Compile-time binding was tried and rejected: enabling
   the reference loads a second `Civ.Mods.Ai` beside the payload copy, so the tests would exercise code
   the game never runs (§10 follow-ups).
8. **`ScriptedWonderTests.Art_of_war_grants_no_experience_when_the_loser_survives`** published
   `CombatResolved` without performing an attack. **Closed** — it now runs a real siege attack that
   ends in limited damage (§10 follow-ups).
9. **Fixture-ownership question for `tests/Civ.Tests/TestRuleset/`.** It is a second ruleset
   used only by tests. ~10 tests assert *its* rules, so their only credible failure is "someone
   edited the fixture". **Decided (PR-7, owner):** engine harness. Its `CityRules`/`UnitRules`/
   `BasicAi` behaviour gets no tests of its own, and the 18 tests that asserted it are deleted —
   see §10 PR-7.

---

## 6. Test-only production surface (delete after rewiring callers)

| Seam | Location | Non-test callers | Action |
|---|---|---|---|
| `ContentDb.CultureLevelFor` | `src/Civ.Core/Content/ContentDb.cs:163-170` | **0** (only `ModTests.cs:17`) | Delete with A2 |
| `TerrainPalette.TerrainColorOf` | `src/Civ.Client.Core/Terrain/TerrainPalette.cs:103` | **0** — already dead | Delete |
| `CityLayout.TileEdge` | `src/Civ.Client.Core/Terrain/CityLayout.cs:82` | **0** — already dead | Delete |
| `PromotionDef.HealBonus` | `src/Civ.Core/Content/Defs.cs:141` | test ruleset only | **Deleted** (§5.3) |
| `GameSession.MarkOf` | `GameSession.Cities.cs:38-42` | only `ClientSessionTests.cs:647` | Rewire to `TileMarks()[hex]`, delete 5 LOC |
| `GameSession.DescribeStability` + `CityStabilitySummary` | `GameSession.Revolution.cs:59-64`, `:7` | only `ClientRevolutionWondersTests.cs:22,34` | Delete DTO+method (7 LOC); no Godot stability panel exists |
| `GameSession.PendingBattles` | `GameSession.Combat.cs:19` | only `ClientFollowupTests.cs:275,293,352,382` | Rewire on `DequeueBattle()`, delete 2 LOC |
| `TerrainChunks.TilesOf` | `src/Civ.Client.Core/Terrain/TerrainChunks.cs:96` | only `RiverNetworkTests.cs:181` (premise check) | Use a distance test like `TerrainMeshTests.AssertDirtyChunks:315` |
| `Sites.PlaceStartingUnits` | `tests/Civ.Tests/TestRuleset/BasicAi.cs:83-90` | **0** — dead test support | Delete |
| `TestGame.FindLand(skip)` | `tests/Civ.Tests/TestGame.cs:24` | `skip` never passed | Drop the parameter |
| `Simulation.{AiWaveLimit, SpeculationObserver, CommandObserver, _recordedCommands, IsParallelAiTurn}` | `src/Civ.Core/Game/Simulation.cs:188-199` | tests only (`InternalsVisibleTo`) | Keep — documented sequential-vs-wave oracle — but name them as test seams in the SDK docs, or promote `AiWaveLimit` to a ruleset option |
| `Simulation.CreateScenario` | `Simulation.Scenarios.cs:20` | no `src/` caller; 11 test call sites | Keep; the scenario feature is unshipped (`docs/ROADMAP.md:245` unchecked) |

Net: roughly **−35 production LOC, −1 DTO, −2 dead public members** in one cleanup batch, plus
`ContentDb.CultureLevelFor` from the test deletion.

---

## 7. Retained false positives (look like junk, are the real contract)

Worth recording so a future sweep does not delete them.

- **`ContractTests.HashIncludesPlotTeamTreasuryQueueAndIdentityFields`** — an 18-`Action`
  field list, but it is the only guard that each authoritative field enters `ComputeHash`, and
  the hash *is* the desync detector.
- **`ContractTests.Clone_preserves_exact_hash_and_isolates_mutable_state…`** — hand-mirrored
  field list; `Clone` is the isolation primitive behind `ForkSpeculative`; a shallow copy would
  be invisible to hash equality.
- **`CanonicalJsonTests`, `PercentModifiersClampAtZero`** — pin the wire bytes (ordinal keys,
  integer-only, `-0`, escapes) and clamping; survive identifier refactors.
- **`DogfoodTests.CoreAndClientsDoNotKnowShippedModIdentities`** — exact source regex, but the
  cheapest independent architectural guard, and it scans `src/`, not tests.
- **`ModContentTests` branding/integer scan and golden records** — the AGENTS.md product rule
  and the only guard that emitted sidecars carry the source XML semantics.
- **`AiSpeculationTests` conflict-reason strings** (`:158,187,203,223`) — hash equality cannot
  tell a correct conflict from a lucky one; the reason string is the guard.
- **`TerrainMathTests.Blend_weights_sum_to_one…:76`** — *not* a tautology: the exact-values test
  at `:93` only probes points where one λ is 0, where a dropped denominator term is invisible;
  random interior points catch it. Sole C#/shader blend-parity guard.
- **`TerrainMathTests.Height_is_continuous_across_tile_edges_and_corners:126`** — the `Region()`
  helper duplicates axial maths, but it is only a locator; the assertion is the anti-crack
  contract the whole renderer depends on.
- **`FeatureIntegrationReplayTests` (all 4)** — no E2E overlap (the E2E suite is 3 unrelated
  100/150-turn smokes) and no cheaper owner; each fact owns a distinct lifecycle (calendar
  overflow, protection expiry, diplomacy↔espionage gate split, overlay privacy).
- **`EspionageRulesTests:295-296` enum `4`/`5`** — a genuine **wire freeze**:
  `CommandCodec`/`ModLoader.Json` has no string-enum converter, so enum order is wire-visible.
  Keep, but relocate to a codec test that asserts the serialised `Mission` number.
- **`CityQueryContractsTests.City_wellbeing_includes_resources_supplied_without_bonus_plots`** —
  the only guard that network-supplied resources become `cities:happiness_good`/`health_good`
  (46 shipped bonuses declare happiness/health). Follow-up: replace the mock with a real
  `ResourceNetwork` path.
- **The four `*_never_names_an_eliminated_teammate` tests** — one helper, but the risk is
  per-call-site.
- **`PillageModifierTests.Negative_pillage_modifiers_preserve_signed_truncation…`** — replays
  the RNG, but the expected value is an independent arithmetic equation and it is asserted at
  the command boundary (event payload + treasury + RNG state + save replay).
- **`AiTests.{War_commands_roundtrip…, Peace_treaty_blocks_redeclaring_war…}`** — not
  `DiplomacyRulesTests` duplicates: `mods.Content` has no Diplomacy mod, so these own the
  `ai:declare_war`/`ai:make_peace` fallback commands.
- **`NetworkTransitTests`' `Probe`** — registers *content*, not behaviour; the real
  `IResourceNetwork`/`ICityConnectionService`/`ITradeRouteService` run.
- **`Work.*Assigned_upgrade_work_stops_in_disorder…`**, **`ScatterPlannerTests` strips**,
  **`HazeVolumeTests.Unexplored_storm_bakes_no_haze:44`**, **`FoliageGradeTests`** — statistically
  framed or fixture-looking, but each asserts a property that a real regression breaks and a
  retune does not.
- All four `Civ.ContentImport.Tests` converter files — real converter contracts (era
  inheritance, overwrite flags, NONE-disables-a-class, last-wins aliases, brand scrubbing with
  negative controls, module traversal order).

---

## 8. Suggested landing batches

Ordered so each PR is one coherent owner-boundary change and stays green on its own.

1. **PR-1 — assertion-only deletions (Tier A, minus A10).** Zero production change, ~19 test
   methods / ~40 assertions. Expected: default suite drops ~1-2 s and no coverage claim is lost.
   Validation: `mise run test`, plus per-file filters from the group reports.
2. **PR-2 — test-only surface cleanup.** Rewire `MarkOf`/`PendingBattles`/`DescribeStability`
   callers, delete the three dead members plus `ContentDb.CultureLevelFor` (with A2),
   `TerrainColorOf`, `CityLayout.TileEdge`, `Sites.PlaceStartingUnits`, `FindLand(skip)`,
   `TerrainChunks.TilesOf`. Net ≈ −35 production LOC.
3. **PR-3 — tighten the four latent holes.** B1 (inter-city tiles at `CityWork.CanWork`),
   B2 (water upgrade), B3 (haze direction), B5 (capital tie). These are the "make the test able
   to fail" batch; expect at least one to expose a real defect.
4. **PR-4 — consolidate.** C1-C6 (codec table, merged-root rows, activity theory, conquest row,
   shared terrain helper) — net-negative test LOC, no coverage change.
5. **PR-5 — palette renderer test.** One test for `TileDataTexture` row layout +
   `MaxLayers`/`ColorOnlyLayer` truncation, replacing A10, plus the cliff-budget hole
   (assert `TargetPercent` is consumed, not ±5 points of it).
6. **PR-6 — the Tier B leftovers plus the E2E soak move.** B4, B6-B18 (every B item not already
   landed by PR-3) and C7. Reviewed as one batch because each item is a small, independent
   rewrite of one test, and the reviewer needs to see them side by side against §4's rationale.
7. **PR-7 — `TestRuleset` is a harness, so its rules get no tests** (§5.9). Done: `CityTests.cs`,
   `UnitTests.cs` and three individual fixture-rule assertions deleted; see §10 PR-7.
8. **Product fixes the audit's own tests found** (§10 PR-7): AI promotion of `UnitRole.Explorer`
   and the `MoveStep` attack guard. Done.
9. **Follow-ups outside this audit.** Done as one batch (§10): `PromotionDef.HealBonus` deleted;
   planner binding made to fail loudly; a real `ResourceNetwork → wellbeing` integration test;
   `Art_of_war` fixture repair.

---

## 9. Limits of this audit

- Static reading only. Nothing was built, run, or executed; every "validation" command in the
  group reports is proposed, not executed. Tier A deletions should be applied with
  `mise run test` after each batch, not all at once.
- Extractor tests (2 files, 4 tests) and the 6 E2E tests were **not** audited — they are outside
  the requested scope and outside `mise run test`.
- Repo policy (`AGENTS.md`) forbids the product names AND / A New Dawn / Rise of Mankind / RoM
  in shipped code, ids, text or docs. Two retained tests enforce that rule
  (`ModContentTests` branding scan, `DogfoodTests`); do not delete them as "string greps".
- No edits were made. `git status` is clean.

---

## 10. Execution log

Each PR below was implemented, verified with `mise run test`, then reviewed by
Google Antigravity (`agy`, `flash` = Gemini 3.8 Flash Medium) in review-only mode.
Complaints and resolutions are recorded verbatim in summary form.

### PR-1 — assertion-only deletions (Tier A)

Landed deletions: A1-A9, A11-A20 (A10 deferred to PR-5). Net `-145/+3` lines across
15 files, no production code touched. Two extra tidy-ups: `emitted-counts.json` is no
longer copied into the test output (nothing read it after A7), and two test names that
over-claimed after their assertion was removed were renamed
(`Workers_offer_a_build_that_dispatches`,
`Generated_cliffs_have_beaches_and_open_river_mouths`).

Verification: `mise run test` → 37 + 139 + 1172 passed, 0 failed.

**Antigravity review 1 — CHANGES NEEDED (4 complaints):**

| File:line | Complaint | Resolution |
|---|---|---|
| `BonusModelTests.cs:77` | Deleting `Sea_resources_spawn_in_the_water` removed the only guard that sea resources spawn in water; the surviving test only inspects already-placed tiles and passes if none spawn | Restored the test without the seed-dependent 7-id list: sea set non-empty, at least one sea-only resource placed, oil can spawn offshore. Added `atSea > 0` to `Generated_resources_respect_their_terrain_and_shore` |
| `CliffRulesTests.cs:32` | After removing the repeat-simulation comparison the name still claimed `are_repeatable` | Renamed to `Generated_cliffs_have_beaches_and_open_river_mouths` |
| `ClientRevolutionWondersTests.cs:84` | Removing the `TurnsToSpawn` self-comparison left `show_countdowns` unasserted | Replaced with an independent claim: active script has a spawn unit and `TurnsToSpawn` is within `[0, SpawnInterval]`. (A "decreases per turn" assertion was tried first and abandoned: this fixture does not run the scripted-wonder turn system, so the count never advances) |
| `ModContentTests.cs:1` | Stale `using System.Reflection;` after the reflection count loop was removed | Removed |

**Antigravity review 2 — APPROVED** (no further findings).

### PR-2 — test-only production surface

Landed: deleted `ContentDb.CultureLevelFor`, `TerrainPalette.TerrainColorOf` (and its
`_terrainColors` dictionary), `CityLayout.TileEdge`, `TerrainChunks.TilesOf`,
`GameSession.MarkOf`, `GameSession.DescribeStability` + `CityStabilitySummary`,
`GameSession.PendingBattles`, `Sites.PlaceStartingUnits`, and `TestGame.FindLand`'s unused
`skip` parameter. Tests rewired onto `TileMarks()`, `DequeueBattle()` and a local
`InInfluenceRing` premise helper. Net `-63/+18` lines. Verified with `mise run test`
(37 + 139 + 1172 passed) and `dotnet build src/Civ.Godot/Civ.Godot.csproj` (0 warnings).

**Antigravity review — APPROVED** (no complaints on the first pass).

Notes for the record:
- `DescribeStability` had no Godot caller; `DescribeCity(city).Stability` is what the
  revolution UI reads, and that path stays asserted.
- `TerrainChunks.TilesOf` was a premise check only; the replacement recomputes the
  rectangle-plus-`RingRadius` membership across the map wrap, which is the same rule
  `InfluenceTiles` uses to build the ring.

### PR-3 — make the four latent holes able to fail

Landed: `+50/-19` lines plus a new `tests/Civ.Mods.Tests/CityWorkTests.cs`. Verified with
`mise run test` (37 + 138 + 1174 passed).

- **B1 inter-city tiles.** Deleted the vacuous `CityTests.CitiesCannotWorkEachOthersTiles`
  (second city 3 tiles away, radius 2) and added
  `CityWorkTests.Cities_two_tiles_apart_cannot_work_each_others_tiles`: a real generated map,
  two towns at hex distance 2, both may claim the shared tile while nobody works it, the
  second claim returns `Work plot is unavailable`, the first still validates, and the rule is
  symmetric. `CityWork.CanWork`'s inter-city clause now has a real guard.
- **B2 naval upgrade.** Now asserts `Validate` is null, applies `UpgradeUnitCommand`, and
  checks the cog's type, position, zero moves and the treasury drop. It passed, so the old
  assertion was under-asserting rather than masking a defect.
- **B3 haze tone.** Replaced "bytes differ" with "fogged luminance is at least 5% below
  visible", which a brightening tint cannot satisfy.
- **B5 capital tie.** Renamed the theory to `Capital_relocation_scores_yields_culture_and_specialists`
  (dropping the unproven `_uses_lowest_id_ties` claim) and added
  `Capital_relocation_breaks_a_score_tie_by_lowest_city_id`, which asserts equal yields and
  equal plot culture for two identical towns before capturing the capital, so `ThenBy(Id)` is
  what decides.

**Antigravity review 1 — CHANGES NEEDED (1 complaint):**

| File:line | Complaint | Resolution |
|---|---|---|
| `CityWorkTests.cs:51` | `Land()` only checked `TerrainDef.Impassable`, so a peak or an impassable feature (e.g. ice) could be chosen as a city site or the contested tile, making the test seed-dependent; `CityWork.BlockedPlot` rejects both | `Land()` now also rejects `PlotKind.Peak` and impassable features. (`GameFactory.Scenario` was not used instead: its plots use the unregistered `world:grass`, so `CanWork`'s terrain lookup throws) |

**Antigravity review 2 — APPROVED.**

### PR-4 — consolidate (C1–C6)

Landed as `eb6bd33`, 10 files, `+124/-121`. Net LOC is flat; assertions went up (6 codec
rows, 4 discovery rows, 3 activity rows, 4 conquest rows). `mise run test`: 37 + 141 + 1180
passed.

- **C1 codec.** Three `*RoundTripThroughCodec` facts became
  `ContractTests.Command_envelopes_round_trip_without_losing_a_field`, one row per payload
  shape, asserting the envelope's `Type` id and a byte-identical re-encode. Strictly stronger
  than the `IsType` checks it replaced. Note the ids are namespaced (`base:found_city`,
  `core:end_turn`), not bare.
- **C2 merged roots.** The path-spelling and symlink tests merged into
  `Equivalent_roots_do_not_repeat_an_installation`. Rows assert the label *and* the root of
  the highest-priority root that named the installation, which the two originals did only
  partially.
- **C3 unit activity.** `Fortify_changes_the_unit_activity` became a 3-row theory over the
  activities a land combat unit can actually take. Intercept and blockade need aircraft and
  ships and are already covered for real by
  `Aircraft_panel_dispatches_patrol_and_recon_actions` and
  `NetworkTransitTests.Client_blockade_action_executes_a_real_order_and_disappears_when_unavailable`,
  so a 5-row table would have needed three unit types and duplicated both.
- **C4 conquest.** The one-line puppet-annex fact is now the `(Annex, existingPuppet: true)`
  row of the conquest theory, and it calls `AnnexCity` so the city screen's entry point stays
  covered.
- **C5/C6 helpers.** `TestGame.IsLand`/`FindLand(where)` and `TerrainTestMaps.HeightAt`.

**Antigravity review — CHANGES NEEDED (4 complaints), then APPROVED:**

| File:line | Complaint | Resolution |
|---|---|---|
| `ClientSessionTests.cs:566` | Calling `ResolveCityConquest` directly for the existing-puppet row dropped coverage of the public `AnnexCity` API | That row now calls `s.AnnexCity(city)` |
| `MergedModDiscoveryTests.cs:88` | Merging lost the relative-symlink-target case, so `ResolveFirstRootLink`'s relative resolution was unguarded | Added a `linked user root through a relative target` row |
| `MergedModDiscoveryTests.cs:105` | `Assert.Equal(mod, winner.Root)` was weakened to a filename compare, redundant with the manifest-id assertion | Full-path assertion restored per row (link spelling + `/shared`) |
| `CliffRulesTests.cs:42` | Summing `PopCount(CliffEdges)` over all plots double-counts, because cliffs are mirrored onto the sea plot, so the bound was 25% rather than the intended "not most" | Counted from the land side: `coast.Sum(c => c.Edges.Count(d => Cliff(...)))` |

### PR-5 — palette renderer test + cliff budget (A10, §5.5)

Landed as `d8deb0d`. `mise run test`: 37 + 141 + 1180 passed; `dotnet build src/Civ.Godot`
succeeds (the Godot project is outside the solution and outside the test projects, which is
why the layout had to move).

- **New `src/Civ.Client.Core/Terrain/PaletteTable.cs`.** The 256×8 RGBA8 colour table's row
  layout moved out of `src/Civ.Godot/TileDataTexture.cs`, which no test can reference.
  `TileDataTexture.PaletteTexture` now wraps `PaletteTable.Bytes` in a Godot image. This is
  the only production change in the audit that was made for testability, and it keeps the
  layout engine-free next to `TerrainPalette`, as the client core's own docs describe.
- **Three tests replace the deleted A10 data restatements** (`Water_layers_fade_into_each_other…`,
  `Sand_and_grass_keep_their_direction…`, `Lush_ground_spreads…` stay deleted; the two that
  were *not* data restatements, `Layers_keep_ordinal_terrain_order…` and
  `Grassland_shows_patches…`, remain):
  - `Colour_table_packs_layer_colour_spread_turn_and_ragged_edge_rows` — layer colour + spread
    alpha, turn row, edge row (ocean / patch layer + 1 / patch share, alpha 0 for water), the
    water-tint row including its 0.6 alpha, and the untinted index-zero feature/owner texels.
  - `Colour_table_packs_feature_tint_look_and_haze_rows` — the three per-feature rows the
    shaders fetch, per ground index.
  - `Terrains_beyond_the_layer_budget_fall_back_to_colour_only_and_surfaces_keep_their_layers`
    — an over-budget probe ruleset (28 terrains vs a 27-terrain budget) asserts truncation to
    `ColorOnlyLayer` in ordinal order while all five surface layers survive.
- **Cliff budget.** The deleted `InRange(cliffs/edges, .15, .25)` is *not* replaced by a
  re-derivation of `TargetPercent = 20`: `CliffGenerator` is `internal` in the World mod, which
  the test project loads as a dll (`ReferenceOutputAssembly=false`), so the constant is
  unreachable without a new `InternalsVisibleTo` and a real project reference that would undo
  the mod-isolation harness. The replacement asserts the observable contract: a coastal map
  gets some cliffs and never most of its sea edges, counted from the land side.

### Remaining, not in these PRs

Nothing planned. PR-7 and the four §8.9 follow-ups landed (§10); the retained false positives in §7
stay deliberately.

### PR-6 — Tier B leftovers and the E2E soak move

13 test files, no production change. Every item below is one test rewritten against the
behaviour it names, not against a copy of the implementation.

**C7 — `AiPromotesItsVeterans` moved to E2E.** The 80-turn soak asserted the *test ruleset's*
AI policy, so it could not fail for a shipped-AI regression. It is now
`AiTests.Ai_promotes_its_veterans` (`[Trait("Category", "E2E")]`, shipped `ai:standard`), and it
asserts more than the original: at least two units promoted, and no AI-owned land soldier left
holding an available promotion. Getting that right needed three exclusions, each of which is a
real planner boundary rather than a test convenience:

| Exclusion | Why it is legitimate |
|---|---|
| units declaring `military:unitai_explore` | `MilitaryPlanner.Promote` walks only Melee/Siege `Soldiers()`, so scouts keep experience **by design of that loop** |
| loaded cargo (`TransportId`) | the same `Soldiers()` filter skips transported units |
| units whose `CreatedTurn` is the current turn | built during the last `AdvanceTurn`, so no planning pass has run |

Barbarian-owned units are also excluded (owner `Ai` is null: player 4 has no planner).

**Finding (product, not test):** the AI never promotes `UnitRole.Explorer` units. `military:scout`
accumulated three legal promotions over 80 turns. This is a genuine AI gap, not a test artefact:
`AiCatalog.Classify` assigns Explorer to land units that declare the explore AI with strength
2 or less, and `MilitaryPlanner.Promote` is only called from the Melee/Siege soldier loop, so
`Scout()` never promotes. Combat-capable explorers (strength above 2) are classified Melee and
*are* promoted, so the test keeps them in scope. Left unfixed because changing planner scope is
a gameplay change; docs do not state the Melee/Siege-only scope anywhere.

**Tier B items.**

| Item | Change |
|---|---|
| B4 | `CityQueryContractsTests` now anchors on a shipped plot-dependent leaf (`condition:foundation:river`): the draft unit is ineligible until a river is set on the city's tile. This also **deleted the test-only `PlotTechnology` requirement evaluator** that existed only to observe `context.Plot == city tile` |
| B6 | `Settlement_sites_match_an_independent_spacing_and_eligibility_scan`: comparison is now the `(tile, area)` set in tile order. `ScanSiteScore` survives only as the eligibility threshold (`>= 100`); the ranking is no longer compared, because that helper copies `SitePlanner.Score`'s weights |
| B7 | four golden whole-state hashes deleted. Each theory became one fact that runs the sequential, in-wave and reloaded variants and asserts **the variants agree with each other**. The structural assertions (queue, command log, RNG untouched) were already there and are unchanged |
| B8 | `Research_rule_inputs_retain_reference_values…` deleted from `CalendarTests`; the surviving claim moved to `ResearchRulesTests.Research_rule_defaults_stay_neutral_and_realistic_timescale_stays_on` (the `new RuleConstants().RevolutionResearchCostPercent == 100` default, plus the realistic-timescale option default) |
| B9 | `Assert.Equal(8, …Civics.Count)` → `CivicOptionDef` registry count. The "from civilization" half is now observable: a `CivicProbe` mod registers one playable civilization with `StartingCivics = [government:coinage]` and the test asserts the civilization wins over the option default |
| B10 | `Legendary_tier_requirements_gate_the_cultural_victory_count`: re-anchored on `ICityCultureService.Level` (the mechanism) before the victory flip. **Deviation:** kept in the Culture file. The ProjectsVictory counting rule is already proven by `ProjectsVictoryRulesTests.Cultural_victory_needs_three_legendary_cities_in_one_team`; what only this test proves is that a tier whose *requirement* is unmet cannot count, which is the Culture mod's level filter |
| B11 | renamed to `Hurry_anger_counts_the_free_instance_and_the_replacement_once_it_takes_over` and documented that the whip-anger mechanics live in `ResearchEconomyTests`. **Deviation:** the fold was not done — the two tests use different probe mods, so one table cannot hold both rows |
| B12 | trimmed to the ledger-only claims (`cities:power`/`health_bad`, `cities:dirty_power`, the food penalty built on them, and the dirty line disappearing once clean power arrives). `EnvironmentalUnhealth` replays were dropped. **Deviation:** not folded into the warming theory — the two tests use different probe mods, so the ledger mapping is not transferable |
| B13 | `Clean_power_in_the_city_or_team_area_removes_dirty_power_pollution` deleted; two rows added to `Warming_weights_authoritative_health_sources…` using a `+` composition (`dirty+clean`, `dirty+area:area_clean`) |
| B14 | the "only" half is now tested: a rival team's `TechDiscovered` must produce no tech notification, asserted against the rival tech's own content name so the negative cannot pass vacuously |
| B15 | `Worker_repairs_ruins…` now asserts the ruins are still ruins **and** `world:build_build_farm` progress is above zero, so merely accepting the order no longer satisfies it |
| B16 | `Leader_query_exposes_the_content_leader_and_their_own_traits`: the trait list and flavors are read from `LeaderDef` instead of being restated as literals |
| B17 | `AiEvaluationTests` sample-grid literals derived from the CLI arguments (`seeds × players × (turns + 1)`), `RunManifest.CurrentSchema` and `EvaluationConfig.RulesPolicy` used instead of their literals, and the delta count taken from `summary.SnapshotCount` |
| B18 | the directory-at-profile-path fact folded into `Broken_profile_never_falls_back_to_the_all_mod_default` as a third failure kind. **Deviation:** the report's suggested `ThrowsAny<IOException>` is wrong — a directory is `UnauthorizedAccessException`, which does **not** derive from `IOException`. The row now asserts the exception is in the read-failure family (`IOException`/`UnauthorizedAccessException`/`SecurityException`) |

Verification: `mise run test` → 37 + 140 + 1173 passed, 0 failed. The counts move because
theories became facts and one soak left the default suite.

**Pre-existing failure found (not caused by this PR):** `mise run test:e2e` fails on two tests —
`Four_computer_players_develop_and_fight_over_200_turns` and
`Identical_games_and_a_midgame_save_have_equal_hashes_at_every_turn` — both with
`InvalidOperationException: Unit cannot attack` from `CombatService.ValidateCombat`. Confirmed on
`d8deb0d` with this PR's changes stashed, and no production file was touched here. The new E2E
soak passes. This is a live product bug in AI-driven combat validation and deserves its own
fix.

**Antigravity review — CHANGES NEEDED (2 complaints), both fixed:**

1. The explorer exclusion was overbroad: `!def.UnitAiTypes.ContainsKey("military:unitai_explore")`
also excluded combat-capable explorers, which `AiCatalog` classifies as Melee and the planner
does promote. Narrowed to `!(explore AI && Strength <= 2)`, which is exactly `UnitRole.Explorer`,
and the comment now states the gap instead of excusing it.
2. B6 dropped the site *ordering* guard along with the copied weights: both sides sorted by tile,
so a `SitePlanner.Score` ranking regression would have been invisible. Added `AssertRanked`,
which asserts the planner's own sites are in descending score order with tile as tie-break.
Ordering is a real contract; only the weights are not compared.

Re-review: **APPROVED**.

### PR-7 — the `TestRuleset` policy: harness, not subject (three commits)

**Decision (owner):** no test may assert the fixture's own rules. `tests/Civ.Tests/TestRuleset/`
is a harness that lets the engine project drive command dispatch, turn systems, events,
save/load, hashing and determinism; it is not a product with contracts to pin.

Deleted outright, because nothing in either file asserted anything else:

- `CityTests.cs` — 8 tests over fixture `CityRules`, `Yields`, `GrowthSystem` and the
  `SetCityWorkCommand`/`SetCivicCommand` fixture commands.
- `UnitTests.cs` — 7 tests over fixture `UnitRules`, `ProductionSystem`, and the
  `StartTrainingCommand`/`MoveUnitCommand`/`AttackCommand`/`PromoteUnitCommand` fixture commands.

Three assertions of the same kind lived in files that also hold engine tests, so the individual
test was removed rather than the file:

| Removed | Why it is a fixture test |
|---|---|
| `SimulationTests.CannotFoundCityOnWaterOrNearAnotherCity` | asserts the fixture `FoundCityCommand`'s placement rules |
| `SimulationTests.ResearchBuildAndCultureProgressAcrossTurns` | asserts the fixture research/production/culture turn systems |
| `CivAndAiTests.CivilizationBonusSpeedsUpCulture` | asserts the fixture `CultureSystem` plus fixture yields |

Nothing in the engine loses coverage, because each deleted claim is pinned elsewhere by an engine
test that does not depend on fixture rules: command rejection without mutation and index
consistency (`ContractTests.RejectedCityAndUnitInsertsAndMovesLeaveAllIndexesUnchanged`),
modifier ledgers (`ContractTests.ModifiersAreSummedInRegistrationOrder…`), turn ordering
(`CivAndAiTests.HumanAndAiShareATurn`, `ContractTests.EndTurnRejectsSecondEndForSamePlayer…`),
determinism and save round-trips (`LockstepTests`, `ContractTests.AiSaveReloadResumes…`,
`SimulationTests.SameSeedSameMapAndHash`). The same rules tested for real against shipped
content stay in `tests/Civ.Mods.Tests` (`CityRulesTests`, `CityWorkTests`, `MilitaryRulesTests`,
`CultureRulesTests`, …).

Two pieces of harness code lost their last reader and are removed with the tests:
`CityRules.SpecialistCount`, and `FoundCityCommand`'s optional settler (`UnitId`,
`ValidateSettler`, and the consumption branch) — no remaining test ever passed a `UnitId`, and the
harness founds cities for free from setup and `BasicAi`. Everything else is still registered and
still dispatched by the turn systems, `BasicAi` and the command-envelope theory, so the harness
itself is otherwise unchanged — which is the point of the decision. Its behaviour is deliberately
untested from here on.

`mise run test` → 37 + 122 + 1174 passed, 0 failed. `Civ.Tests` drops from 140 to 122 (15 deleted
tests plus the 3 above, 2 of them theories-become-facts already counted).

### Product fixes found by the audit's own tests (two commits)

Both were found by the E2E soaks that PR-6 moved onto shipped content. They are product changes,
not test changes, and were reviewed together with PR-7.

**The AI never promoted explorers.** `MilitaryPlanner.Promote` ran over `Soldiers()`
(`UnitRole.Melee`/`UnitRole.Siege`), and `AiCatalog` classifies a land unit with the explore AI
and strength 2 or less as `UnitRole.Explorer`, so `Scout()` never promoted and a scout's
experience was never spent. `Promotable()` now selects every alive, non-cargo unit and
`Soldiers()` narrows it, so the promotion pass asks every unit while the remaining soldier loops
keep their order. Non-combat units are unaffected: `PromotionEligibility` rejects a promotion for
a unit with no combat type. The E2E soak dropped its explorer exclusion and now asserts that at
least one explorer-role unit spent a promotion.

**A path step that revealed a defender threw out of the command pipeline.**
`MovementService.MoveStep` called `CombatService.Resolve` for any hostile tile it reached, and
`ValidateCombat` threw `InvalidOperationException("Unit cannot attack")` when the unit could not
fight. A route is planned against the units visible when it is built, so walking a step can reveal
a defender the planner could not see: in the soak, an only-defensive `military:scout` walked into
a fogged javelineer. `MoveStep` now re-asks `IMovementService.CanEnter` with `attack` before it
fights and stops the path when the rules forbid the attack — the same check `ValidatePath` already
applies to a direct attack, so a mid-path step can no longer be less legal than the step the
command validated. The re-check runs only on a hostile step with an eligible defender, so a scout
still captures an undefended enemy civilian, and a hostile plot whose defenders are all ineligible
still stops the move as before.

The regression test builds the fog case without the soak: the defender is invisible when the route
is validated, the first step reveals it, and the move must end one tile short with the defender
alive. It throws without the guard.

Verification: `mise run test` → 37 + 122 + 1174 passed; `mise run test:e2e` → the two previously
failing soaks and the new promotion soak all pass (16 m 13 s).

**Antigravity review — CHANGES NEEDED (4 complaints), 3 fixed, 1 rejected:**

1. The regression test stopped short of proving *what* stopped the move. Now asserts the exact
   `MovesLeft` after the one legal step (the forbidden step costs nothing), the defender's full
   health, that no `CombatResolved` was published, and that the target plot became visible to
   team 0.
2. *Rejected.* `SetCityWorkCommand` and `SetCivicCommand` are rows of
   `ContractTests.CommandEnvelopes`, the engine test that proves every command payload shape
   survives encode/decode byte for byte; they are used as payload shapes, not as rules.
   `CityRules.Slots` is reached through `EnforceSlots`, which `GrowthSystem` and
   `SetCivicCommand.Apply` both call, and `Assign`/`ValidateAssignment` through
   `SetCityWorkCommand`. The only true part is that no remaining test *applies* them, which is the
   decision. The reviewer accepted this on re-review.
3. *Accepted.* `FoundCityCommand`'s settler path removed (see above).
4. *Accepted.* The closing attack/move pair in
   `ContractTests.SharedTeamCombinesStartingTechsAndResearchAndRetainsSwitchedTargetProgress`
   asserted `UnitRules.IsOpponent` — fixture, not the shared-team research claim the test names.

Re-review: **APPROVED** ("Nothing is left").

Final counts: `mise run test` → 37 + 122 + 1174 passed, 0 failed. `mise run test:e2e` → all six
soaks pass, including the two that were failing before this batch.

### Follow-ups: the four items §8.9 left open (four commits)

All four were verified still open against the tree before starting, then landed one commit each.

**`PromotionDef.HealBonus` deleted.** The legacy flat `PromotionDef` in `Civ.Core/Content/Defs.cs` is
harness-only; every shipped mod uses `Civ.Content.Schema.PromotionDef`, whose heal model is the
six-field family (`FriendlyHealChange`, `NeutralHealChange`, `EnemyHealChange`, `SameTileHealChange`,
`AdjacentTileHealChange`, `AlwaysHeal`) that `MilitaryData` maps into the modifier ledger and
`UnitHealing`/`UnitService` consume. `HealBonus` had one reader, the fixture's rest-heal system, and
no shipped promotion ever set it — sidecar JSON is parsed with `UnmappedMemberHandling.Disallow`, so
the field was reachable only from C# that registers the legacy def. Wiring it would have put a second,
legacy heal knob beside the schema family with no consumer.

Removed with it: the fixture term that read it, and `base:medic1`/`base:medic2`, whose only effect it
was. **Deviation:** that changes the fixture AI's trajectory — `BasicAi` takes the first legal
promotion in registry order, so a mounted unit used to pick Flanking then Medic I and now stops after
Flanking. No test asserts which promotion the fixture AI picks and the soaks only compare runs against
each other, so nothing went red, but it is a real behaviour change in the harness and is recorded
here rather than left implicit.

**Planner binding fails loudly.** `AiCalculationTests` and `AiIntentTests` reached the internal
planners of `Civ.Mods.Ai` through `Assembly.GetType("Civ.Mods.Ai.X")` and `GetMethod(...)!`, with
`NewAi` duplicated between them. A rename surfaced as a `NullReferenceException` inside an unrelated
assertion; worse, the optional `Unit`/`Areas` assignments in `AiIntentTests.Apply` silently skipped,
so a renamed command member made a test exercise a default target and still pass.

One `AiBindings` helper now owns every lookup and throws an `InvalidOperationException` naming the
missing member and listing what the type does have, including the constructor case that otherwise fell
through to the BCL's "no parameterless constructor defined" text. `AiIntentTests.Apply` names the
optional target member at the call site, so a rename fails there instead of quietly changing the
target.

**Rejected the report's own suggested fix.** §5.7 proposed `InternalsVisibleTo` so renames fail at
compile time. That cannot work: the mod projects are referenced with `ReferenceOutputAssembly=false`
so the tests load them from `<OutDir>/mods/<Name>/` in their own load context. Enabling the reference
was tried — `typeof(AiContext).Assembly` and the assembly of the registered `ai:standard` came back as
**different instances**, so the test would have exercised a copy the game never loads. The payload copy
is the correct subject, so binding stays by name and the names now fail loudly.

**A real `ResourceNetwork → wellbeing` test.** The only test that tied a resource to the wellbeing
ledger replaced `IResourceNetwork` with a mock, so the shipped `ResourceNetwork` was never part of the
claim; the bonus-to-ledger mapping was covered, the network that decides whether the bonus is supplied
at all was not. The new test keeps every real service and walks the path on one fixture: owned,
revealed and improved but unconnected supplies nothing, connecting the route adds exactly one ledger
line with the amount taken from the bonus definition, a route without the connecting improvement still
supplies nothing, and losing the route takes the line out. Each step also asserts the network's own
count, so a failure says whether the network or the ledger mapping is wrong.

**`Art_of_war` fixture repair.** The no-victory test published a synthetic `CombatResolved` and then
asserted no experience was granted — a proof about the listener, not about combat. It now runs a real
siege attack against a defender placed one hit short of the attacker's combat limit, so the attack ends
in limited damage on the first landed hit, and compares against a baseline saved before the wonder
completed. **Known limit:** the outcome is not *provably* RNG-independent — a long consecutive run of
defender hits could kill the attacker first — so the test asserts the outcome rather than claiming
certainty; forcing it would mean seeding or manipulating the RNG inside the test.

**Review record.** Four parallel subagent reviews (one per commit) plus an Antigravity review.
Antigravity: CHANGES NEEDED (5) → all five addressed or answered → **APPROVED** ("no remaining
issues"). The subagent reviews produced the changes above plus: `Method<T>` binding flags made
consistent with `Property<T>`, `TypeNames` guarded against `ReflectionTypeLoadException` and filtered
to non-nested non-generated types, the `TrySet` helper removed in favour of the loud `Set`, and the
final route-removal step given a network-count assertion.

Final counts: `mise run test` → 37 + 122 + 1175 passed, 0 failed.
