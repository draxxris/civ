# AI economic evaluation

This tool measures economic development in repeatable headless games. It does not edit the AI, tune rules, or replace baselines.

## Run a sample

From the repository root, run:

```sh
mise run ai:eval -- --output .ai-eval/economy-baseline --seeds 1,42 --turns 100 --players 4 --world small
```

For a short smoke run, use `--turns 2 --players 2 --world duel`.
To show the CLI help, run `mise run ai:eval -- --help`.

The defaults are seeds `1,42`, 100 advances, four players, and a small world.
Without `--output`, the tool creates a unique directory under `.ai-eval/`.
The tool refuses an existing output directory, including an empty directory.

The CLI accepts:

- `--output DIR`: a new directory for this run.
- `--baseline DIR`: a completed output directory from an earlier run.
- `--seeds CSV`: distinct unsigned 64-bit integers.
- `--turns N`: the number of turn advances, from 0 through 10000.
- `--players N`: two through four measured AI slots.
- `--world SIZE`: `duel`, `tiny`, `small`, `standard`, `large`, `huge`, `giant`, or `gigantic`.

The workload uses the existing benchmark mod payload and `GameFactory.CreateAi`.
It does not copy another set of payloads or load optional game mods.
The manifest records the loaded mod IDs and the content hash.

## Scenario and rules

Every measured slot uses `ai:standard`. The fixed leader order is:

1. `peoples:leader_gjergj_kastrioti`
2. `peoples:leader_julius_caesar`
3. `peoples:leader_bismarck`
4. `peoples:leader_gandhi`

Each seed starts a fresh generated continents game.
The scenario uses normal speed, the ancient era, and the shipped Noble handicap.
Slots retain their identities, leaders, and seed across versions. Players can still have different starts, traits, and neighbors.
This design supports paired comparisons across versions, not equal strength between slots.

**The current scenario is not an equal-rules human/AI experiment.**
The shipped Noble AI subsidies remain active.
For example, the current AI research, training, and growth percentages are 80, 90, and 45.
The manifest records the actual loaded AI handicap numbers and resolved simulation settings.
No harness-only difficulty override is applied.

This is not a controlled peaceful scenario.
The tool does not suppress combat, barbarians, exploration rewards, or other active rules.
Optional diplomacy and victory mods are not part of the default benchmark payload.
The tool cannot infer win rate, overall playing strength, or the reasons for an AI decision.

## Checkpoints and metrics

Checkpoint 1 is the state after the constructor runs the initial AI actions.
No economy settlement occurs before that checkpoint.
Each advance settles the previous turn, runs turn systems, then runs the next AI actions.
For `--turns 100`, the output contains checkpoints 1 through 101.

Each economic row has a complete `key`: `seed`, `player`, `scenario`, and `checkpoint`.
The row includes the authoritative state hash, the last settled turn, and integer metrics.
The hash covers the full game, so players in one checkpoint share the same hash.
Only the configured player slots receive economic rows. Other rule-created players can appear in raw events.

The economic metrics include:

- Owned cities, population, constructed buildings, known team technologies, improved owned tiles, and units.
- Treasury, projected income, projected expenses, projected net gold, unpaid gold, and strike turns.
- Current city food, food surplus after consumption, production, queue production, and raw commerce.
- Current commerce research, culture, espionage, and the research slider percentage.
- Accumulated settled commerce gold, research, culture, and espionage, in hundredths.

`manifest.json` defines every metric and its preferred direction.
Positive metrics treat an increase as an improvement. Negative metrics treat a decrease as an improvement.
Neutral metrics report changes without a competence judgment.
Directions describe individual metrics. They do not prove better decisions.

Worker metrics count owned units with a worker build rate and build options.
The harness records active build orders at each checkpoint, worker position changes between checkpoints, and assigned worked-land yields.
Position changes do not prove useful travel. Workers without active builds can be travelling, waiting, retreating, or blocked.
Building-worker checkpoint totals approximate worker-turns. They do not measure exact work time.
Worked-land yields are current plot previews, not completed-build gains.
Improvement and route transitions compare owned plots at adjacent checkpoints. Captures, upgrades, pillaging, or other rules can cause transitions. Intermediate changes can be missed.
Build progress is normalized to each build's required work. It can remain on a plot without an active worker order.
Schema version 3 adds transition and progress metrics. Earlier baselines are not compatible.

Only `settled*Total` fields accumulate actual output from `EconomyPlayerState.SettledCommerceHundredths`.
The harness requires exactly one settlement for each measured advance.
Settled gold excludes expenses, deal transfers, and one-time rewards. It is not cumulative net income.
Settled research precedes research modifiers and technology costs. It is not completed technology value.

Other yield and income fields are snapshot previews, not cumulative production.
Queue production includes queue-specific modifiers, overflow, and food conversion where the rules permit them.
The tool does not sum these previews into a claim about completed output.

## Compare versions

Keep the earlier output directory unchanged.
After a deliberate code change, run the same scenario with a new output directory:

```sh
mise run ai:eval -- --output .ai-eval/economy-candidate --baseline .ai-eval/economy-baseline --seeds 1,42 --turns 100 --players 4 --world small
```

The comparison requires matching schemas, metric definitions, scenario configuration, mod IDs, AI handicap numbers, and resolved sample settings.
It requires complete, unique sample keys from both runs.
A failed, incomplete, or incompatible baseline causes a nonzero exit and a durable failure record.
The baseline is never replaced automatically.

Different git revisions and content hashes are allowed.
The report warns about different content hashes because rule changes can confound economic comparisons.
If rules or content change, interpret the deltas as whole-version changes, not isolated AI improvements.

The machine deltas preserve every sample key and metric name.
Each delta includes the baseline value, current value, signed change, preferred direction, and outcome.
The report counts improved, regressed, unchanged, and neutral-changed snapshots for each metric.
Repeated checkpoints are correlated observations, not independent games.
There is no composite score, timing-based intelligence measure, confidence claim, or autonomous optimization loop.

## Output and failure handling

Each run writes:

- `manifest.json`: schema, arguments, configuration, git revision, dirty state, module ID, content hash, handicap numbers, and resolved sample settings.
- `economy.jsonl`: one row for each seed, measured player, and checkpoint.
- `events.jsonl`: available domain events with their runtime type and original payload.
- `progress.jsonl`: timestamped run, sample, checkpoint, and comparison progress.
- `summary.json`: status, completed sample count, snapshot count, and the latest snapshots from each attempted sample.
- `report.md`: a human-readable explanation and comparison, when requested.
- `comparison.json` and `deltas.jsonl`: comparison metadata and per-metric deltas, when a comparison completes.
- `failure.json`: exception details, when a failure occurs after the output directory opens.

Raw events describe actual notifications, not commands or decision-score explanations.
The harness drains new events from both event-bus buffers without repeating previously recorded events.
Event envelopes retain the seed, scenario, observed checkpoint, source turn, and ordinal.
Player and team attribution remains in the original payload where the event supplies it.

Each JSONL append flushes file contents to disk before the next simulation step.
The harness writes JSON summaries and reports through temporary files, then renames them.
A disk or permission failure can prevent a failure record. The process also reports that failure on stderr.
An unusable or existing output path causes a nonzero exit without changes to that path.

Ctrl+C requests cancellation between simulation steps. A synchronous turn must finish before the harness processes cancellation.
An abrupt process kill can leave `summary.json` with status `running`.
A partial run is not a successful baseline. There is no resume mode.
A failed comparison can leave partial delta files. Only a completed summary marks the run as successful.

Git provenance describes the checkout at run start, not proof that a prebuilt binary matches that checkout.
The mise task builds the tool before each run. The module ID identifies the built harness.
Keep durable result directories outside temporary storage when the results must survive machine cleanup.

## Tests

`AiEvaluationTests` calls the same CLI entry point with real mod payloads and temporary output directories.
It covers deterministic rows, settled totals, event rollover, paired metric directions, baseline compatibility, and durable failures.
The tests link the harness source into the existing mod test suite. They do not add payload-copy targets.

Run the focused tests:

```sh
mise exec -- dotnet test tests/Civ.Mods.Tests/Civ.Mods.Tests.csproj --filter FullyQualifiedName~AiEvaluationTests
```

Run the default suite with `mise run test`.
