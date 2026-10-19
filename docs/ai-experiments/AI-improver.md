# AI economy improver handoff

## Objective and user instructions

Improve economic decisions in the game AI without increasing its bonuses.
The user authorized autonomous implementation, evaluation, rejection, and further experiments.
Do not stop after each rejected candidate to request another direction.
Deliver measured behavior improvements, not only instrumentation or parameter changes.

The user explicitly prohibited full unit-test runs for this work.
Do not run `mise run test`, solution-wide `dotnet test`, or equivalent broad commands.
Use focused owner tests, relevant sibling tests, builds, and bounded evaluation runs.
This instruction takes precedence over the repository's default test command for this task.

The user requested a checkpoint commit before autonomous improvement work.
That commit was created before the experiments continued.
Do not create more commits without further authorization.

## Current checkout and interruption

At handoff, `HEAD` is `eea7190`, titled `Add repeatable AI economic evaluation and worker experiment records`.
The tracked working tree was clean before this handoff file was created.
That commit currently includes the harness, experiment history, replay fixture repair, and the economy-backed expansion change described below.

The checkpoint originally returned hash `ded1472`.
The checkout history changed during the delegated work.
Current commits and files are authoritative; do not assume that historical hashes still identify the current tree.
Do not reset the checkout to recover an old experiment.

The latest Luna session was interrupted before its final assessment and documentation were complete.
A continuation attempt was aborted.
No evaluation or test process was visible at handoff.
Completed result directories remain available.
The interrupted status does not mean that the recorded simulations failed.

## Actual behavior change currently present

`src/Mods/Ai/SitePlanner.cs` now gates additional expansion on economic readiness after turn 30:

- Treasury must meet `EconomyPlanner.Reserve(cityCount)`, currently `40 + 10 * cityCount`.
- Projected net gold from `IPlayerEconomyService.Calculate` must be non-negative.
- Before or at turn 30, this added economic gate does not apply.

Previously, the economic restriction only blocked expansion after turn 30 when treasury was less than five gold.
The candidate reuses the existing reserve policy instead of adding a new tuning constant.

The existing capital gate remains: one worker, two soldiers, and turn 55.
Site legality, spacing, population requirements, defensive checks, and settler caps remain unchanged.
`WantsMoreCities` also influences retained settler demand, so review effects beyond initial queue selection.

`tests/Civ.Mods.Tests/AiCalculationTests.cs` includes `Settlers_require_the_empire_treasury_reserve`.
It checks a one-city case at turn 60 with treasury 49 versus 50.
It does not independently cover the negative-income branch, every queued-production interaction, or all defensive consequences.

`CityPlanner.cs` has no retained production behavior change from these experiments.
Its current diff from the preceding commit contains only extra blank lines.
Worker selection and worker work-rate APIs are unchanged.

The economic gate has promising insolvency evidence, but its final retain/reject review was interrupted.
Do not describe it as a proven general-strength improvement.

## Harness and experiment ownership

- Evaluation code: `tools/Civ.Benchmarks/AiEvaluation/`.
- Usage and metric definitions: `tools/Civ.Benchmarks/AiEvaluation/README.md`.
- CLI entry: `EvaluationCli.Run`, dispatched by `tools/Civ.Benchmarks/Program.cs`.
- Task: `mise run ai:eval`.
- Harness boundary tests: `tests/Civ.Mods.Tests/AiEvaluationTests.cs`.
- Earlier experiment narrative: `docs/ai-experiments/worker-selection-baseline.md`.

The harness evaluates changes. It does not edit AI code or choose candidates.
Luna investigates, implements, and runs bounded experiments.
The parent assistant reviews evidence, checks regressions, and removes rejected candidate-only plumbing.
There is no persistent autonomous optimization service.

The harness writes manifests, economic snapshots, domain events, progress, summaries, reports, and paired metric deltas.
Failures have durable records when the output path remains writable.
Only completed summaries qualify as baselines.
Existing output directories cannot be reused.

## Evaluation contract and limitations

Current schema: **3**. Scenario: `continents-noble-v1`.
All experiments below use normal speed, the ancient era, four fixed AI leaders, and a small continents world.

Shipped Noble subsidies remain active, including AI research cost 80%, training cost 90%, and growth threshold 45%.
No bonus was increased for an experiment.
These runs compare versions under the same rules, not equal human and AI rules.
The benchmark payload does not include every optional game mod.
Combat, barbarians, exploration rewards, and city capture can affect the results.

A 100-turn run means 100 advances after the constructor's initial AI actions.
Its final checkpoint is 101.
Each seed produces 404 economic snapshots for four measured player slots.
Checkpoints are correlated, not independent games.

`settled*Total` metrics accumulate actual settled commerce in hundredths.
Settled research precedes technology rate modifiers and costs.
Settled gold is gross commerce, not net income or treasury change.
Production and most other yield metrics are snapshots, not cumulative output.

Worker position changes do not prove useful travel.
Improvement and route transitions do not prove worker completions.
Normalized build progress can include stale entries.
Fewer unimproved worked plots can result from a smaller empire rather than better worker decisions.
Do not judge a candidate from one proxy or a composite score.

Manifests identify the checkout revision, dirty state, settings, content hash, and built harness module.
The historical baselines and candidates have dirty-state provenance.
A revision alone cannot reproduce those exact binaries.
The available comparisons share the recorded content hash, but that does not identify every planner-code difference.

## Completed experiments

### Rejected: worker travel-plus-build-time ranking

The candidate ranked reachable jobs within the existing eight-tile batches by benefit per travel-plus-construction time.
It preserved the existing safety, budget, mission, and reachability rules.

The 50-turn pilot matched the baseline exactly.
At checkpoint 101 across seeds 1 and 42, the candidate had:

- Seven more unimproved worked plots.
- Two fewer improved owned tiles.
- 31,095 fewer settled research hundredths.
- About 165 seconds of simulation time versus 154 seconds for baseline.

Seed 7 also showed more unimproved worked plots and lower research.
The ranking and its public work-rate query were removed.
Do not repeat this approach without a new causal explanation.

An initial test failure was a brittle fixture assumption, not broken mission continuity.
A worker can start a valid local build and no longer need a destination mission.
The repaired speculative test checks sequential equivalence, a real replay conflict, and a mission or active build.
Do not restore its old exact-destination assertion.

### Rejected: revisit production queues for worker demand

This candidate let cities replace valid queue heads when their existing worker target was unmet.
The longer paired run increased research but reduced cities and gold.
Seed 7 showed lower research and more unimproved worked plots.
The pilot did not show earlier worker availability.
The production change and candidate-specific fixture adjustment were removed.

### Rejected: remove the turn-55 expansion gate

Earlier expansion increased cities, population, improvements, and gold.
It also reduced settled research substantially and introduced strike observations.
The main paired run lost 139,341 settled research hundredths.
The fixed capital gate was restored.
Do not equate more cities with a better economy.

### Current candidate: economy-backed expansion

This is the `SitePlanner.ExpansionEconomyReady` change currently in the checkout.
It retains the turn gate and requires the existing reserve plus non-negative projected net gold.

The completed comparisons cover five seeds and 20 final player snapshots.
The following values sum final checkpoint metrics, except the explicitly accumulated commerce totals.

Across seeds 1, 42, 7, 9, and 17:

- Settled research: 1,626,536 to 1,656,113 hundredths, an increase of 29,577 (about 1.8%).
- Settled gold: 372,574 to 348,199 hundredths, a decrease of 24,375 (about 6.5%).
- Final unpaid gold: 40 to zero.
- Final consecutive strike-turn values: total 10 to zero. This is not the count of all strike turns.
- Observed deficit disband events for measured players: 13 to zero across the complete event logs.
- Cities: 58 to 54. Population: 159 to 155.
- Improved owned tiles: 95 to 96.
- Unimproved worked land: 160 to 147, with worked land also lower at 213 to 206.
- Workers: 35 to 31.
- Final projected net gold: total -40 to -13.

Important mixed evidence:

- Seeds 1 and 42 gained 17,499 settled research hundredths, but lost six cities and five population.
- Seeds 9 and 17 gained 13,729 settled research hundredths, with unchanged total cities and two fewer population.
- Seed 7 lost 1,651 settled research hundredths, but gained two cities, three population, and 6,220 settled gold hundredths.
- Per-player outcomes vary. The aggregate research gain is not a uniform gain for every nation.

The strongest evidence is avoidance of deficit-driven disbandment, not faster development everywhere.
The main unresolved question is whether the reserve gate sacrifices too much useful expansion over a longer horizon.
Final evaluation and independent review remain outstanding.

## Artifact map

All paths below are under ignored `.ai-eval/` in this checkout.
These directories are not stored in git. Preserve them before cleanup or machine migration.

Original schema 3 baselines:

- `worker-v3-pilot-baseline`: seed 1, 50 advances.
- `worker-v3-baseline`: seeds 1 and 42, 100 advances.
- `worker-v3-holdout-baseline`: seed 7, 100 advances.

Rejected candidates:

- `worker-v3-candidate-pilot`, `worker-v3-candidate`, `worker-v3-holdout-candidate`: worker ranking.
- `worker-v3-production-pilot`, `worker-v3-production-candidate`, `worker-v3-production-holdout`: production reassessment.
- `worker-v3-expansion-pilot`, `worker-v3-expansion-candidate`, `worker-v3-expansion-holdout`: removal of the turn gate.

Current economy-backed expansion:

- `worker-v3-economy-expansion-pilot`: seed 1, 50 advances.
- `worker-v3-economy-expansion-candidate`: seeds 1 and 42, 100 advances.
- `worker-v3-economy-expansion-holdout`: seed 7, 100 advances.
- `worker-v3-economy-seeds9-17-baseline`: original behavior on seeds 9 and 17, 100 advances.
- `worker-v3-economy-seeds9-17-candidate`: economic gate on seeds 9 and 17, 100 advances.

Every current-candidate directory listed above has a completed summary.
Older schema 2 directories also remain, but they are not compatible with schema 3 comparisons.

## Commands for continuation

For a fresh paired candidate run against the original seeds, use a new output path:

```sh
mise run ai:eval -- --output .ai-eval/economy-next-main --baseline .ai-eval/worker-v3-baseline --seeds 1,42 --turns 100 --players 4 --world small
```

For the existing holdout configuration:

```sh
mise run ai:eval -- --output .ai-eval/economy-next-holdout --baseline .ai-eval/worker-v3-holdout-baseline --seeds 7 --turns 100 --players 4 --world small
```

For seeds 9 and 17:

```sh
mise run ai:eval -- --output .ai-eval/economy-next-extra --baseline .ai-eval/worker-v3-economy-seeds9-17-baseline --seeds 9,17 --turns 100 --players 4 --world small
```

These commands compare against the original behavior, not the current economic gate.
For an incremental experiment, first record a fresh baseline with the current code.
If the schema or settings change, create a compatible baseline before editing behavior.
Do not relabel an old candidate as a new baseline without recording that decision.

## Validation status

Earlier worker instrumentation and replay-fixture work passed a focused 68-test filter.
The last autonomous session recorded **36 passing `AiCalculationTests`** after the economic gate change.
It did not complete a final combined owner-and-sibling validation pass before interruption.
No full suite ran during the autonomous session.
The earlier 1,323-test full-suite result predates the current behavior change and does not validate it.

For focused follow-up validation, run:

```sh
mise exec -- dotnet test tests/Civ.Mods.Tests/Civ.Mods.Tests.csproj --no-restore --filter 'FullyQualifiedName~AiCalculationTests|FullyQualifiedName~AiIntentTests|FullyQualifiedName~WorkerRepairTests|FullyQualifiedName~AiEvaluationTests'
```

Then run `git diff --check`.
Do not run the full suite.

## Immediate next work

1. Inspect current source and manifests before modifying anything. Checkout history changed during the experiment.
2. Complete focused validation of the economic gate, especially negative income and production cancellation interactions.
3. Review per-player trajectories, deficit events, defense losses, and runtime rather than only final sums.
4. Establish clean-source baselines for longer-horizon checks. The existing five seeds are no longer untouched holdouts.
5. Check new seeds and a longer horizon before a broad-strength claim. Do not compare incompatible run lengths.
6. Finalize the economic-gate retain/reject assessment with explicit development and insolvency tradeoffs.
7. Continue with a separately testable economic bottleneck instead of repeatedly changing constants on these same seeds.

Useful next investigations include worker capacity versus useful available jobs, building returns, and research choices tied to immediate development needs.
The available evidence does not yet prove which of those is the next highest-value change.
Do not restore the rejected worker ranking or remove the turn gate without new evidence.

## Luna recovery

The latest delegated task is `subtask_pawn_1792372514744_22010a41`.
Its title is `Improve AI economic competence autonomously`, using `pawn` with model alias `luna`.
Its persisted session is:

```text
/home/gordon/.pi/agent/sessions/--home-gordon-workspace-civ--/2026-10-19T01-15-14-756Z_01a151ba-1fc1-71bf-8dbd-97c932a34335.jsonl
```

That session has no final assistant response for the interrupted experiment.
Recover tool results if more detail is needed. Do not infer completion from the task status alone.
The earlier worker-measurement task is `subtask_pawn_1792368306343_79a935c4`.
Use the session reader skill for recovery instead of dumping full session files into the conversation.
