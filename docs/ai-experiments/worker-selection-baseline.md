# Worker selection baseline

## Scope

This run adds worker activity measurements to the economic harness. It does not change worker behaviour.

The harness reports worker counts, valid active build orders, worker position changes, assigned worked land without improvements, and current worked-land yields. Position changes do not prove useful travel. Workers without active build orders can be travelling, waiting, retreating, or blocked. Active build orders sampled at checkpoints approximate worker-turns. They do not measure exact work time. Yield values are current plot previews, not gains caused by a completed build.

The metrics use schema version 2. The harness queries read-only world services and records unit positions. It does not issue commands or consume random values.

## Commands and artifacts

The 20-turn pilot ran first:

```sh
mise run ai:eval -- --output .ai-eval/worker-baseline-pilot --seeds 1 --turns 20 --players 4 --world small
```

The full baseline used the requested paired settings:

```sh
mise run ai:eval -- --output .ai-eval/worker-baseline --seeds 1,42 --turns 100 --players 4 --world small
```

Both runs completed. Their recorded durations were about 17 seconds and 2 minutes 16 seconds. The full run contains two seeds, four measured players, and 101 checkpoints per player. This sample is too small for a general strength claim.

Both manifests record revision `f8ffcf7ee528481b65fa59fecb77df23ecf487cc`, dirty state `true`, and content hash `0E76F9D01E85D7A8E0C56AD64F5E92F8B3AFB3CD4089BD2FBF89AA4E3447946D`. The checkout already had the evaluation harness edits before this task. The recorded revision identifies `HEAD`, not a clean source tree. The metrics and harness changes were also uncommitted during both runs. The rules use shipped Noble AI bonuses, not equal human and AI rules.

## Baseline observations

The full run wrote 808 snapshots. It recorded 539 worker observations, including 343 observations with a valid active build order. The final checkpoint had 12 workers, nine with active build orders. It had 70 worked-land plots without improvements out of 88 worked-land plots across the eight seed-player samples. The cumulative position-change count was 94.

These values show active building and a large amount of worked land without improvements at the final checkpoint. They do not identify which jobs workers selected, whether a build completed during an interval, or whether movement advanced a useful mission. Combat, city growth, technology, and other game changes also affect these snapshots.

## Selection hypothesis

`WorkerPlanner` ranks tiles by static priority minus straight-line distance, then takes the first reachable job. `BestBuildAt` ranks builds on one tile by yield gain divided by required work. The tile ranking does not include the selected build's construction time or actual path cost.

Falsifiable hypothesis: on the same seeds and settings, ranking reachable jobs by estimated yield gain per combined path and build time will reduce worked land without improvements by checkpoint 101 and increase worked-land yields. A comparison that shows no repeatable improvement, or a regression in settled economic output, will reject this hypothesis.

The baseline does not prove that this selection rule caused the 70 unimproved worked plots. The current travel metric is displacement, not useful travel, and no build-completion event is available at the harness boundary. I did not implement a selection change. That change needs stronger attribution or a narrowly scoped paired experiment with explicit path and work-time estimates. I kept the measurement work and baseline evidence without treating an unproven candidate as an improvement.

## Continuation hypothesis, stated before behavior edits

The 70 of 88 final worked plots without improvements justify one bounded selection test, but do not prove the cause. The code ranks candidate tiles by static priority minus straight-line distance, then takes the first reachable job. `BestBuildAt` already uses yield gain per required work on one tile.

Candidate: within each existing eight-tile batch, rank reachable jobs by raw yield benefit divided by estimated travel turns plus actual construction turns. Use the existing tile priority and tile id for stable tie breaks. Travel uses pathfinder sub-move cost and the worker's current full-turn movement allowance. Construction uses the build service's required work and effective worker rate. Keep the current batch walk, so one ranking step examines at most eight candidate paths. Preserve mission reuse, legality, safety, reachability, worker budgets, stable tie breaks, and cached build previews.

The candidate passes only if paired runs reduce worked land without improvements by checkpoint 101 without a settled research or net-gold regression, and holdout seeds do not show a broad economic regression. Reject it if these measures are mixed or gains do not repeat. Worked yields and improvement transitions remain proxies, not overall strength.

## Continuation: production reassessment hypothesis

The worker-ranking candidate failed paired economy checks and stays rejected. The baseline also shows that workers enter the measured empires late. Most slots have no worker at checkpoint 30, and almost all slots have one at checkpoint 55. Worker production is available from population two, but `CityPlanner.NeedsChoice` does not reconsider a valid building or non-settler unit queue for worker demand. Such a queue can delay a needed worker until its current item completes.

Falsifiable hypothesis: when the empire's existing worker target is unmet, a city with population two or more and a legal worker choice will replace its current production head with that worker. The normal queue command preserves paused progress. This will increase early worker availability and useful improvement activity without a broad decline in settled research, gold, city growth, or defense. A paired 20-turn pilot will test direction before the full paired and holdout runs. The worker target, expansion gate, and Noble subsidies remain unchanged.

Authoring gate for the focused regression: protect the city planner's observable choice when an unmet worker target competes with a valid building queue. A regression that ignores worker demand after the first production choice would fail it. Existing planner tests only cover empty or process queues for worker production. The test will use the real city production boundary and no production seam.

## Experiment: production reassessment rejected

The pilot, paired run, and holdout completed with the existing harness settings. Exact commands:

```sh
mise run ai:eval -- --output .ai-eval/worker-v3-production-pilot --baseline .ai-eval/worker-v3-pilot-baseline --seeds 1 --turns 50 --players 4 --world small
mise run ai:eval -- --output .ai-eval/worker-v3-production-candidate --baseline .ai-eval/worker-v3-baseline --seeds 1,42 --turns 100 --players 4 --world small
mise run ai:eval -- --output .ai-eval/worker-v3-production-holdout --baseline .ai-eval/worker-v3-holdout-baseline --seeds 7 --turns 100 --players 4 --world small
```

The candidate let cities replace valid production heads when an unmet worker target and a legal worker choice existed. The pilot ended with the same four workers and two active builds as its baseline. Settled research was 1,581 hundredths lower. In the paired run, final worked plots without improvements fell by 12 and improved owned tiles fell by two. Settled research rose by 21,316 hundredths, while settled gold fell by 28,643 hundredths and owned cities fell by eight. Holdout seed 7 had four more worked plots without improvements, one more improved tile, 6,492 fewer settled research hundredths, and one more city. Wars and city losses varied by player. These results do not show a consistent economic benefit.

**Decision: reject.** The focused owner test proved that the planner can revisit a building queue for a needed worker, but the pilot did not show earlier workers. The longer outcomes conflict across metrics and seeds. I removed the production change and its test-fixture adjustment. The original worker-count target remains unchanged. The worker-selection experiment also remains rejected.

## Continuation hypothesis: expansion readiness

The baseline shows first new cities around checkpoint 60. `SitePlanner.EstablishedCapital` requires a worker, two soldiers, and turn 55, even when the other two readiness conditions already hold. This fixed turn gate delays settler demand after the capital has a workforce and defenders.

Falsifiable hypothesis: remove only the turn-55 condition. Keep the worker and two-soldier checks, the city population gate, site quality and spacing, treasury limit, and settler cap. If this produces earlier settlements without a coherent regression in settled research, gold, population, or defense, retain it. Otherwise reject it. Run a short pilot, then the paired seeds and seed-7 holdout.

## Experiment: removing the expansion turn gate rejected

The pilot and both 100-turn comparisons completed:

```sh
mise run ai:eval -- --output .ai-eval/worker-v3-expansion-pilot --baseline .ai-eval/worker-v3-pilot-baseline --seeds 1 --turns 50 --players 4 --world small
mise run ai:eval -- --output .ai-eval/worker-v3-expansion-candidate --baseline .ai-eval/worker-v3-baseline --seeds 1,42 --turns 100 --players 4 --world small
mise run ai:eval -- --output .ai-eval/worker-v3-expansion-holdout --baseline .ai-eval/worker-v3-holdout-baseline --seeds 7 --turns 100 --players 4 --world small
```

At pilot checkpoint 51, the candidate had seven cities versus four, 17 population versus 15, 9,952 fewer settled research hundredths, and 9,391 more settled gold hundredths. It had four wars versus none. In the paired 100-turn run, the candidate had ten more cities, 26 more population, 139,341 fewer settled research hundredths, and 204,039 more settled gold hundredths. It also had 21 more improved owned tiles, 28 more worked plots without improvements, and four additional strike-turn observations. The holdout had two more cities, 11,836 fewer settled research hundredths, and 32,564 more settled gold hundredths. Early expansion increased production and gold, but it also created large research and development tradeoffs.

**Decision: reject.** The turn gate provides useful restraint. I restored it. The site-readiness regression remains valuable because it proves that the gate blocks a capital with a worker and two soldiers before turn 55.

## Continuation hypothesis: economy-backed expansion

The rejected change exposed the real failure mode: the existing treasury check allows expansion until the treasury falls below five gold, even when the empire has not met its own reserve. `CityPlanner.EconomyCanAnnex` already uses the reserve and projected net gold for another irreversible city investment.

Falsifiable hypothesis: keep the turn-55 capital gate, but require the existing treasury reserve and non-negative projected net gold before producing additional settlers after turn 30. This reuses the current reserve and economy service rather than adding a new threshold. Keep site quality, spacing, population, garrison, treasury, and settler-count rules. If this reduces excessive expansion without harming research or useful development, retain it. Otherwise reject it.

## Continuation: transitions, progress, and candidate gate
Schema version 3 adds adjacent-checkpoint improvement and route id transitions plus normalized build progress on currently owned plots. A transition can come from an upgrade, pillage, capture, or another rule. The harness can miss intermediate changes. It does not label transitions as worker completions. Progress sums capped fractions of required work for current build-progress entries. Stale entries and entries without active worker orders can contribute.

The rejected candidate briefly added `IWorkerBuildService.WorkRate` so the planner could estimate construction time. The API had no harness consumer. I removed the interface method, implementation wrapper, calculation rename, and test assertion after rejecting the candidate. The production work-rate calculation remains unchanged.

New artifacts preserve the older schema 2 runs:

- Pilot baseline: `.ai-eval/worker-v3-pilot-baseline/`
- Main baseline: `.ai-eval/worker-v3-baseline/`

Exact commands:

```sh
mise run ai:eval -- --output .ai-eval/worker-v3-pilot-baseline --seeds 1 --turns 50 --players 4 --world small
mise run ai:eval -- --output .ai-eval/worker-v3-baseline --seeds 1,42 --turns 100 --players 4 --world small
```

Both runs completed with the original worker-selection code. The pilot took about 28 seconds. The main baseline took about 2 minutes 34 seconds and wrote 808 snapshots. Its final eight seed-player snapshots show 37 improvement transitions, 17 route transitions, nine current build-progress entries totaling 51,196 basis points, and 70 worked plots without improvements out of 88. These counts do not prove worker completion or selection quality.

Before editing behavior, I stated this candidate: rank reachable jobs within each existing eight-tile batch by yield benefit divided by estimated travel turns plus construction turns. Use current tile priority, then tile id, for stable ties. Estimate travel from pathfinder sub-move cost and current full-turn movement allowance. Estimate construction from required work, remaining plot progress, and the effective worker rate. Keep the existing batch walk and all legality, mission, safety, budget, reachability, and deterministic rules.

I implemented this candidate briefly, then ran focused owner tests. The filter `FullyQualifiedName~AiEvaluationTests|FullyQualifiedName~WorkerRepairTests|FullyQualifiedName~AiIntentTests` failed one case: `AiIntentTests.Real_speculative_waves_commit_intent_and_reject_changed_remote_decisions(changeRemoteTarget: True, transient: True)`. The failing assertion found no retained worker mission. A repeat using the focused `AiIntentTests.Real_speculative_waves_commit_intent_and_reject_changed_remote_decisions` method filter failed the same case again. This violates mission continuity, so I removed the candidate without tuning it. The worker-selection source now has no behavior change.

No paired candidate run or holdout run was made. The candidate failed a mission contract before simulation comparison, so its economic deltas would not justify keeping it. The schema 3 baselines are pre-behavior measurements, not candidate results.

## Continuation: replay test audit and bounded candidate result

### Replay-test root cause

I read the full `AiIntentTests` fixture and assertions, `WorkerPlanner`/`AiIntent` mission ownership, planning-command validation, speculative wave conflict checks and replay, plus neighboring mission tests. The previous failure was at `Assert.True(HasMission(...))`, before the exact replay-conflict assertion. A temporary diagnostic run with the candidate showed `IsWorking == true`, an empty mission map, and events `0:Direct;1:Committed`. The worker had started a legal local build on tile 7,2. The transient fixture changed progress on distant tile 17,2 by only one point; that did not change the candidate's ranked decision, so speculative replay correctly committed. This was not a mission-continuity regression. The old fixture assumed the previous static priority would react to the remote plot change and that every valid worker task remains a destination mission.

I revised the test stimulus to make the remote progress change decisive: it now uses a nearby-but-outside-the-five-tile speculative footprint task, and raises its build progress to near completion. I removed the exact destination assertion and retained the actual contract: speculative and sequential states match, a changed intent produces `Conflict` with `replay rejected: AI intent changed`, and the final worker owns either a valid destination mission or a valid active build. Existing tests continue to own mission persistence, safety, legality, reachability, reservation, load and replay contracts. The fixture does not require a production test seam.

### Candidate and results

Candidate hypothesis, recorded before behavior edits: within each existing eight-tile batch, rank reachable legal jobs by useful yield benefit divided by estimated remaining travel turns plus construction turns, accounting for current build progress, pathfinder cost, current movement, maximum movement and effective worker rate. Preserve existing candidate generation, legality, safety, budgets, mission reuse, claimed tiles, stable tile-priority/id ties and batch bounds. No other AI changes.

Focused tests passed before the candidate (68/68), and the revised replay case passed against it. Schema 3 config remained compatible, so the existing pre-behavior runs were reused. Candidate outputs are preserved:

- Pilot command: `mise run ai:eval -- --output .ai-eval/worker-v3-candidate-pilot --baseline .ai-eval/worker-v3-pilot-baseline --seeds 1 --turns 50 --players 4 --world small`
- Main command: `mise run ai:eval -- --output .ai-eval/worker-v3-candidate --baseline .ai-eval/worker-v3-baseline --seeds 1,42 --turns 100 --players 4 --world small`
- Holdout commands:
  ```sh
  mise run ai:eval -- --output .ai-eval/worker-v3-holdout-baseline --seeds 7 --turns 100 --players 4 --world small
  mise run ai:eval -- --output .ai-eval/worker-v3-holdout-candidate --baseline .ai-eval/worker-v3-holdout-baseline --seeds 7 --turns 100 --players 4 --world small
  ```
  The baseline used the original worker code, restored after the run. Candidate output compares against it.
Pilot: all 204 paired snapshots were identical. Main: at checkpoint 101, the eight paired player samples had seven more worked plots without improvements in total, two fewer improved owned tiles, and 31,095 fewer settled research hundredths. Six players had lower settled research; one had more and one was unchanged. Settled gold was 51,928 hundredths higher in total, concentrated unevenly and not a substitute for research or worker improvement. Holdout seed 7 had nine more worked plots without improvements and three more improved owned tiles at checkpoint 101, while settled research was 11,436 hundredths lower in total. These samples are mixed and do not establish broad strength.

The main candidate simulation took about 165 seconds through sample completion versus 154 seconds for the existing baseline. The pilot took about 36 seconds versus 28 seconds. These timings are noisy and comparison/report generation is excluded; candidate path queries add measurable cost. The main candidate's 808 snapshots and holdout's 404 snapshots are preserved for inspection.

**Decision: reject the candidate.** It did not improve the target worker measures, had lower research in the main and holdout samples, and cost more processing. I removed the candidate ranking and its candidate-only `IWorkerBuildService.WorkRate` query. The original private work-rate calculation and worker behavior remain unchanged. The revised speculative test fixture remains because it tests the replay contract without requiring one exact AI destination or mission representation.

### Validation

- Before candidate: `mise exec -- dotnet test tests/Civ.Mods.Tests/Civ.Mods.Tests.csproj --no-restore --filter 'FullyQualifiedName~AiIntentTests|FullyQualifiedName~WorkerRepairTests|FullyQualifiedName~AiEvaluationTests'` — 68/68.
- Candidate replay filter passed 3/3 after correcting the stimulus.
- Final focused filter with original worker behavior and revised test — 68/68.
- No full unit suite or solution-wide test command ran. No commit was made.
