# HUD Invalidation: stop rebuilding the whole interface on every click

## Why

Every click on the map bumps `GameSession.Revision`, and `GameUi.Refresh` treats any
revision change as "destroy and rebuild the whole HUD":

- `GameView.Click` -> `GameSession.LeftClick` -> `SelectAt`, which does `Revision++`
  unconditionally (`src/Civ.Client.Core/GameSession.cs:92-104`). The same happens in
  `SelectUnit` (`:106`), `SelectCity` (`:112`), `ClearSelection` (`:118`),
  `SelectNextUnit` (`:121`) and `SetMessage` (`:45`).
- `GameUi.NeedsRefresh` compares that counter (`src/Civ.Godot/GameUi.cs:86`), and
  `GameUi.Refresh` then calls `BuildTop`, `BuildLeft` or `_city.Build`,
  `BuildResearchDialog`, `BuildGovernmentDialog`, `BuildModal` and `BuildFeed`
  (`src/Civ.Godot/GameUi.cs:65-83`). Each of those starts with `Ui.Clear(...)`
  (`src/Civ.Godot/Ui.cs:86`), which frees every child and builds new ones.

Measured on this tree with a throwaway probe (Debug build, small world, headless, warm
skin cache, `--trace-performance`), one rebuild costs:

| Work                    | Cost                                    |
| :---------------------- | :-------------------------------------- |
| Whole HUD rebuild       | 13-21 ms warm (99 ms cold, first paint) |
| of which top bar        | 5-7 ms                                  |
| of which left panel     | 8-14 ms                                 |
| dialogs and feed        | ~0.02 ms                                |
| one `Ui.Button` created | ~45 us (200 in 9.03 ms)                 |
| one `Ui.Label` created  | ~15 us (200 in 2.95 ms)                 |

The same click also re-runs `MapRenderer.BuildDynamic` (about 1.7-2 ms on a small map),
which is out of scope here.

Two secondary defects follow from the rebuild and are fixed as a consequence:

- New controls get new canvas item RIDs, and `BrushedStyleBox._Draw` seeds its scrape
  tile phase from that RID (`src/Civ.Godot/Skin/BrushedStyleBox.cs:41-46`), so every
  plate changes its scratch pattern on every click.
- `BuildLeft` recreates its `ScrollContainer`, so the city list scroll position resets
  on every click anywhere on the map.

The skin texture cache merged in `7f865e4` does not help the click cost: `Frames` and
`ScrapeSets` were already cached in memory, and a rebuild pays for `Control`
construction, stylebox lookup, text sizing and canvas items, not for painting.

## Out of scope

- The map's dynamic layer rebuild (`MapRenderer.BuildDynamic`) keeps keying on
  `Revision`. Recorded as a residual, not changed here.
- The city screen (`CityUi.Build`) keeps rebuilding on a revision change while it is
  open, because its content is a whole screen of numbers that change together.
- The paint phase being derived from a canvas item RID stays as it is. Persistent
  widgets keep their RID, so the symptom disappears for the panels converted here. Do
  not change the seed formula.
- New features from the open list in `docs/ROADMAP.md` (for example a stacked-unit list
  in the left pane) are not implemented. The left panel refactor must leave the panel
  easy to extend, nothing more.

## Phase List

```
[x] 1.   (*pawn:ds4*) Guard the session's invalidation bumps
[x] 2.   (*pawn:ds4*) Give each HUD panel its own refresh gate
[x] R-a. (*pawn:muse*) (`ask-antigravity`) Checkpoint A: Review Phases 1-2
[x] 3.   (*pawn:ds4*) Keep the top bar's widgets and update them in place
[x] 4.   (*pawn:ds4*) Keep the left panel's widgets and update them in place
[x] R-b. (*pawn:muse*) (`ask-antigravity`) Checkpoint B: Review Phases 3-4
[x] 5.   (`ask-antigravity`) Record the work in OPTIMIZE.md and ROADMAP.md (no review)
```

Progress (verified state, with the commits and the agent sessions):

- Phase 1: `877fb7a`, session `01a178d2-1055-7306-826b-ac1e34b77d80`.
- Phase 2: `70ab679`, session `01a178d7-4163-7306-826b-ac20cef7411d`.
- Test consolidation (checkpoint A fix): `003cd30`, session `01a178d2-…` (resumed).
- Checkpoint A: closed. muse session `01a17914-b01b-7306-826b-ac22ecb9ed8c`;
  ask-antigravity conversation `4003d653-b248-468d-8ada-217bf7d0d9c9`.
- Phase 3: `1ad1904`, session `01a1793c-8083-7306-826b-ac261162b4ba`.
- Phase 4: `4b6fd9d`, session `01a179b1-5851-7306-826b-ac2bc2faf01c`.
- Checkpoint B: closed on `4b6fd9d`, no DEMANDs. muse session
  `01a179ec-b02a-7306-826b-ac2c3c55ccb8`; ask-antigravity conversation
  `0c815c92-17f1-4cf9-bf47-17fe9fe6ec37` (report at `/tmp/r-b-agy-report.md`).
- Phase 5: `d39ed44`, ask-antigravity conversation
  `be5ba714-5816-4d88-8895-aa9756e633e2`. OPTIMIZE.md gained Phase 7 (timeline, section,
  summary table) and lost the stale startup markers; ROADMAP items 2 and 7 annotated in
  place.

## Parallelism

None. Phases 1-4 all edit `GameUi.cs` or the session counters that gate it, so they run
serially in the current checkout. Within a checkpoint, the two reviewers run in parallel
against the same frozen commit.

## Commit Boundaries

Every phase ends in a commit before its review starts. Reviewers work on a frozen commit
hash and must name the hash they reviewed. `plans/` is gitignored, so each commit
message has to stand alone: state the defect, the change, the tests added, the
measurement and the screenshot result.

## Review Checkpoints

Two checkpoints, each a dual adversarial review: a `pawn:muse` sub-agent and an
`ask-antigravity` review of the same frozen commit, run independently.

- **Checkpoint A** after Phase 2 reviews the invalidation contract: are the guards in
  the session correct and complete, does any caller depend on an unconditional bump, are
  the guards covered by the tests added in Phase 1, and does the per-panel gating in
  `GameUi.Refresh` rebuild everything it must and nothing it need not. Both reviewers
  also apply the `test-audit` skill to
  `tests/Civ.Mods.Tests/SessionInvalidationTests.cs`: the authoring gate's four
  questions and the junk-pattern checklist, with a per-test keep / consolidate / delete
  verdict. A dozen near-identical single-assertion tests for one guard family is exactly
  what a table-driven theory should replace, so a DEMAND to collapse them is in scope
  for this checkpoint.
- **Checkpoint B** after Phase 4 reviews the persistent panels end to end: every widget
  that `BuildTop` and `BuildLeft` used to create must still exist, still be reachable
  and still show the same text, colour, tooltip, disabled state and order; plus the
  probes, screenshots and commit messages.

Documentation-only Phase 5 has no review.

## Checkpoint A Outcome (closed)

Both reviewers, independently and on the same frozen commit, accepted the production
change and reached the same verdict on the tests. Round 1 reviewed `70ab679`; the only
finding that blocked closure was on the tests, from both reviewers: twelve methods for
one invariant. The fix (`003cd30`) collapsed nine of them into two `[Theory]`s and left
three facts. Round 2 reviewed `003cd30` and both closed it.

- Individually verified by both reviewers in a `/tmp` worktree at `8c483aa` that the
  consolidated file still fails there: six cases, each by exactly one extra bump.
- Production code was verified by inspection, not only by the tests: every `Revision`
  consumer (`MapRenderer`, `GameView`'s hover cache, `CityScreen`, `LockstepSession`)
  was checked against the new guards, and a mutation probe was used to prove that
  guarding `Submit` on message equality is wrong (it breaks `EndTurn`'s bump and skips
  `CollectNotifications` and dead-selection pruning).

Carried NOTEs, none requiring action:

- `ContinueOrders` still bumps `Revision` and `WorldRevision` unconditionally on every
  `EndTurn`, so a turn still rebuilds the map's dynamic layer even when nothing moved.
  Pre-existing and consistent with the recorded residual.
- The government dialog and the modal rebuild on every reopen, unlike the research
  dialog, which now reuses its children. Only the research dialog has a scroll-retention
  criterion.
- The modal and the feed rebuild on any `Revision` change while open, including a
  selection click. Unchanged from before this work; a residual, not a regression.
- The research and government gates do not test the city-open flag; unreachable today
  because opening or closing a city always bumps `Revision`, which fires their
  `(revision && open)` clause. The modal gate tests it defensively.
- `_message.Text` and `_endTurn.Text` are still assigned on every refresh pass. No nodes
  are created, and Phases 3-4 own in-place updates.
- The `"select the current unit"` theory row primes the selection before capturing
  `Revision`, so its pre-fix numbers are 2 vs 3 rather than the 1 vs 2 of the original
  method. Same call repeated, same assertion, no lost proof.
- X11 wheel events do not reach a `ScrollContainer` under Xvfb, so dialog scroll
  retention was proved by child-instance identity instead of by scrolling.

## Review Protocol

**Verdicts.** Every finding gets exactly one:

- **ACCEPT**: the reviewer reproduced the claimed behavior (or the claimed absence of a
  defect) and confirms it. Nothing to record and no action needed.
- **NOTE**: the reviewer has something to record: a caveat, a nit, or an unreachable
  edge case. It does not require a change, and it is carried into the plan and the
  closing summary so it is not lost.
- **DEMAND**: a change is required before the checkpoint closes. A DEMAND must carry a
  minimal reproduction and an exact implementable scope.

**Review scope.** Every review states which areas and behaviors it examined, how it
examined them (for example, code inspection, tests, or probes), and what it did not
examine or run. Reviewers should assess whether the phase's relevant tests support its
claims, but they do not need to prove that a defect category is exhausted or enumerate
hypothetical untested cases. Report a coverage gap as a finding only when it creates a
specific concern; explain the scenario and its reachability. A DEMAND requires a minimal
reproduction and an exact implementable scope.

## Stopping Rule

Complete when Phases 1-5 are committed, both checkpoints are closed, and:

- a click that changes nothing performs no UI work at all (proved by a session test);
- a click that changes the selection updates text in place: no `Ui.Clear` on the top bar
  or the left panel, and the paint pattern and the left panel's scroll position survive
  the click;
- the HUD still shows every value, colour, tooltip and button it showed before, proved
  by the screenshot comparison and by Checkpoint B's widget-by-widget walk;
- `mise run test` passes and every phase's measurement is in its commit message and is
  carried into `docs/OPTIMIZE.md`.

Acceptable residual, recorded in `docs/OPTIMIZE.md`: the map's dynamic layer still
rebuilds per revision, the city screen still rebuilds while open, `Summary()` still runs
per revision change, and recomputing a panel's identity key allocates strings on the
revision-change path (not on the per-frame path).

Not acceptable: any lost widget, label, colour or tooltip; a per-frame allocation added
to `GameUi.Refresh` or `MapRenderer.Refresh`; or a `Revision` bump removed where another
consumer needs it (`MapRenderer`, `GameView`'s hover cache, the lockstep and audit
paths).

## Test Strategy

Two layers, and each phase adds its tests in the phase that changes the baseline. No
test work is deferred to a later phase.

- **Automated.** Anything in `Civ.Client.Core` is plain .NET and is tested with xunit in
  `tests/Civ.Mods.Tests`, which is part of `mise run test`. `GameSession` is already
  exercised there: `ClientSessionTests` builds sessions with
  `new GameSession(GameFactory.Create(mods.Content, seed))` against the full eight-mod
  ruleset, and it has `Found` / `CaptureForHuman` helpers for states that need a city.
- **No engine harness.** `src/Civ.Godot` types derive from `Godot.Control` and need the
  engine; `dotnet test` cannot construct them. So a phase that only edits `GameUi*.cs`
  cannot add an xunit test, and instead must provide three things in its commit message:
  the probe measurement from the Measurement Procedure, the screenshot comparison
  result, and the list of states it exercised by hand. A reviewer may DEMAND a test if a
  phase changed `Civ.Client.Core` without covering it.
- **Gates.** `mise run test` after every phase (it builds and runs 1,297 tests).
  `dotnet build Civ.slnx` stays at zero warnings. The focused command for the session
  tests is
  `dotnet test tests/Civ.Mods.Tests --filter FullyQualifiedName~SessionInvalidation`.

## Measurement Procedure

Run on a virtual X server, never on the interactive desktop: a window that is not in
front is throttled to about 1 fps there and frame-quantises every number (see
`docs/OPTIMIZE.md`, Phase 6).

```sh
dotnet build src/Civ.Godot
Xvfb :99 -screen 0 1600x900x24 -nolisten tcp &
# `--stage map` alone stops on the setup screen: the game only enters the map when a
# screenshot or probe is requested, so name one and give it a frame budget.
DISPLAY=:99 godot-mono --path src/Civ.Godot -- \
  --trace-performance --stage map --world small \
  --screenshot /tmp/late-map.png --frames 100000 &
sleep 45
DISPLAY=:99 xdotool search --name 'Civ' | head -1          # window id; the real title
                                                          # is "Civ (DEBUG)", so no ^Civ$
# Click empty map tiles below the top bar and right of the left panel, ten times,
# far enough apart that the selection really changes, then twice on the same tile.
DISPLAY=:99 xdotool mousemove 900 620 click 1; sleep 1
DISPLAY=:99 xdotool mousemove 960 660 click 1; sleep 1
DISPLAY=:99 xdotool mousemove 960 660 click 1
```

Read the printed `ui update:` lines and the per-input `input InputEventMouseButton:`
lines. On the base commit every click prints a `ui rebuild:` of 6-48 ms (median 16.5 ms
over ten clicks); after Phase 2 the same click prints
`ui update: top …, left …, city …, dialogs …, feed …`, and a click on a tile that
changes nothing prints no update line at all.

Note that synthetic X11 wheel events do not reach a `ScrollContainer` under Xvfb when
the pointer is over a row `Button` (Godot's hovered-control state only updates on
pointer motion), so scroll retention cannot be proved by scrolling the dialog. Prove it
by checking that the dialog keeps its single child instance across close, a click
elsewhere, and reopen. Screenshots are the portable evidence for everything else.
Screenshot regression, for layout, text and the HUD stages:

```sh
mise run shot -- --screenshots --out-dir /tmp/hud-before      # on the base commit
mise run shot -- --screenshots --out-dir /tmp/hud-after       # on the reviewed commit
mise run shot:compare -- /tmp/hud-before/map.png /tmp/hud-after/map.png /tmp/hud-map.png
```

The scrape overlays may land at different offsets between two builds (the phase comes
from a canvas item RID). Everything else, layout included, must match.

## Checkpoint B Outcome (closed)

Both reviewers reviewed `4b6fd9d` and closed it with no DEMAND: no lost, leaked or
permanently toggled widget; no stale-rendering gate; no per-frame allocation; no false
commit claim. Both reproduced the build and the session tests, both confirmed by
inspection of `UnitActions`/`ValidateActivity` and `DebugScenario.AddUnits` that the two
declared-unreachable states really are unreachable, and both confirmed checkpoint A's
closures still hold (no `GameSession` diff since `003cd30`).

Carried NOTEs, none requiring action:

- `Visible` and the conquest button's `TooltipText` are assigned on every revision pass
  without a compare-before-write. Values are always correct and the path is
  revision-only, so there is no 60 Hz cost; the comparisons that matter (text, tint) are
  all present.
- `OrderKey` excludes availability state on purpose: `Disabled` and `TooltipText` are
  updated in place when the key matches, so a validity flip cannot go stale. It rebuilds
  conservatively when the unit changes even with the same action set.
- The city key is exact for the rendered text (`Id`, `Name`, `Population`,
  `ReligionBadge` in `OrderBy Id` order); a single row change rebuilds the whole list,
  which is rare and revision-path only.
- Phase 3 could not reach golden age, anarchy, pending events, or a ruleset without
  corporations and espionage: there is no debug switch for them and the turn-1
  government dialog accepts no civic change. The phases said so instead of inventing
  numbers; the uncovered paths are `Visible` toggles and one format string reusing
  exercised factories.
- X11 wheel input does reach a `ScrollContainer` under Xvfb when the pointer is over a
  plain `Label` rather than a row `Button`, which is how Phase 4 proved scroll retention
  (`scroll 115` kept, `cityrebuild 0`) after Phase 2 recorded it as unreachable.

______________________________________________________________________

## Phase 1: Guard the session's invalidation bumps

**Simple explanation.** Clicking the same thing twice, or closing a selection that is
already closed, currently tells the whole game "something changed" even when nothing
did. This phase makes the session only say that when it is true.

**Files.** `src/Civ.Client.Core/GameSession.cs`, plus a new
`tests/Civ.Mods.Tests/SessionInvalidationTests.cs`.

**Change.**

1. `SetMessage` (`:45`): return before `Revision++` when the text is unchanged.
   ```csharp
   public void SetMessage(string message) { if (Message == message) return; Message = message; Revision++; }
   ```
2. `SelectAt` (`:92-104`): after choosing `pick`, compare with the current selection and
   return without bumping when the same unit or city is already selected. The stack
   cycling stays: with two units on a tile the pick still differs, so it still bumps.
3. `SelectUnit` / `SelectCity`: return when the requested unit or city is already the
   selection and the other half is already null.
4. `ClearSelection` (`:118`): return when nothing is selected.
5. `SelectNextUnit` (`:121-128`): compute `next` first, then return when it equals the
   current selection.
6. Leave `Submit` (`:48-59`) alone. It always bumps, because a rejected command still
   writes `Message` and can clear a dead selection; do not add a guard there.

**Watch out.** `EndTurn` calls `SelectNextUnit` after the turn has advanced
(`GameSession.cs:61-69`). If the same unit stays selected across a turn, `Submit` inside
`EndTurn` has already bumped `Revision`, so the HUD still refreshes; the guard only
suppresses a *second*, redundant bump. Verify this by reading the call order, not by
assumption.

**Test Strategy.** New `SessionInvalidationTests` in `tests/Civ.Mods.Tests`, using the
`LoadedMods` fixture and `new GameSession(GameFactory.Create(mods.Content, seed))`.
Prefer one table-driven theory per guard family (`[Theory]` with `[InlineData]` naming
the case) over a separate method per case: the guards are one invariant, "a call that
does not change the selection or the message must not move `Revision`", so each
assertion is a row, not a test. Add:

- no-bump rows for: `SetMessage` with the same text, `ClearSelection` with nothing
  selected, `SelectUnit` of the current unit, `SelectCity` of the current city, a second
  click on a tile holding one human unit;
- bump rows for: a different unit, a different city, clearing an active selection, a new
  message, `Submit` of a rejected command;
- one stack-cycling test (two human units on a tile cycle and bump on each click);
- one `EndTurn` test (still leaves `Revision` changed when the selection ends
  unchanged).

Gate:
`dotnet test tests/Civ.Mods.Tests --filter FullyQualifiedName~SessionInvalidation`, then
`mise run test`. Quote the new test count in the commit message.

**Exit criteria.**

- The new tests pass and cover every guard added here.
- `mise run test` passes, `dotnet build Civ.slnx` has zero warnings.
- No known regression from an earlier phase.

**Encountered Serious Issues.**

_(Implementor: append entries here. Format: one line per issue, what was tried, what
worked. Leave "None" when there are none.)_

None.

______________________________________________________________________

## Phase 2: Give each HUD panel its own refresh gate

**Simple explanation.** Today one switch says "rebuild everything". This phase replaces
it with one switch per panel, so a panel that cannot have changed is left alone. It also
stops the hidden dialogs from being emptied and rebuilt on every click.

**Files.** `src/Civ.Godot/GameUi.cs`, `src/Civ.Godot/GameUi.Panels.cs`,
`src/Civ.Godot/GameUi.Research.cs`, `src/Civ.Godot/GameUi.Government.cs`.

**Change.**

1. Keep the per-frame early-out exactly as it is: `Refresh` must stay allocation-free
   and do nothing when `NeedsRefresh()` is false (`GameUi.cs:61-88`). Add the dialog
   flags and the selection identity to `NeedsRefresh` only if they are not already
   covered by the revision.

2. Split the body into named refreshers, and gate each one on the cheap key it needs:

   | Panel             | Gate                                            |
   | :---------------- | :---------------------------------------------- |
   | top bar           | `Revision` changed                              |
   | left panel        | `Revision` changed                              |
   | city screen       | city open changed, `Revision`, or `_city.State` |
   | research dialog   | research open changed, or (`Revision` and open) |
   | government dialog | `_governmentRevision`, or (`Revision` and open) |
   | modal             | `_modalRevision`, or (`Revision` and open)      |
   | feed              | `Revision` changed (content compared inside)    |

3. For the three dialogs, only `Ui.Clear` them when they are visible and their gate
   fired. When a dialog is closed, hide it and keep its children, so reopening does not
   rebuild it a second time:

   ```csharp
   private void RefreshResearchDialog()
   {
       bool open = _researchOpen && _session.City == null;
       if (!open) { _researchDialog.Visible = false; return; }
       if (_researchDialog.Visible && _shownResearchRevision == _session.Revision) return;
       _shownResearchRevision = _session.Revision;
       _researchDialog.Visible = true;
       Ui.Clear(_researchDialog);
       ...
   }
   ```

   Reset the shown revision to -1 when the dialog opens, so it rebuilds on first show.
   Remove `Ui.Clear` from the closed path in `BuildResearchDialog`
   (`GameUi.cs:210-214`), `BuildGovernmentDialog` (`GameUi.Government.cs:22-25`) and
   `BuildModal` (`GameUi.Panels.cs:112-116`); keep their visibility rules, including
   `_session.City == null`.

4. Extend the existing timing line (`GameUi.cs:83`) to report the panels separately so
   the measurement procedure has a surface to read:
   `ui update: top 0.4 ms, left 0.2 ms, city 0 ms, dialogs 0 ms, feed 0 ms`. Keep it on
   `MapRenderer.LogTimings`, matching the existing style.

5. Leave `BuildFeed`'s content rule alone; only stop it from being called when
   `Revision` did not change.

**Watch out.** `AddPanelButtons` (`GameUi.Panels.cs:49`) calls `UpdateConquestModal`,
which mutates `_pendingConquests` and can close the modal (`GameUi.Panels.cs:47-75`).
That side effect must keep running whenever the top bar refreshes; it is not pure
rendering.

**Test Strategy.** This phase changes no `Civ.Client.Core` code, so there is no xunit
test to add. The gate is the probe plus the screenshot comparison, both recorded in the
commit message:

- with no dialog open, clicking a map tile prints no dialog `Ui.Clear` work in the
  per-panel timing line (dialogs 0.00 ms) and no research-dialog rebuild;
- opening and closing the research dialog three times keeps its scroll position, because
  the closed path no longer clears it;
- the HUD stages' screenshots are unchanged apart from scrape phase.

`mise run test` still runs as the phase gate.

**Exit criteria.**

- A click that changes nothing does no panel work at all (Phase 1 makes `NeedsRefresh`
  false).
- A click that changes only the selection updates the same panels as before.
- The timing line prints one entry per refresh with per-panel times.
- The probe and screenshot results are in the commit message; `mise run test` passes.
- No known regression from an earlier phase.

**Encountered Serious Issues.**

None.

______________________________________________________________________

## Phase 3: Keep the top bar's widgets and update them in place

**Simple explanation.** The top bar is about a dozen widgets whose text changes and
whose buttons stay the same. Build them once when the game screen is created, then only
change their text, colour, tooltip and visibility.

**Files.** `src/Civ.Godot/GameUi.cs`, `src/Civ.Godot/GameUi.Panels.cs`,
`src/Civ.Godot/Ui.cs`.

**Change.**

1. Add tiny "set only when different" helpers to `Ui` so a refresh neither reshapes text
   nor allocates a theme override on every revision:
   ```csharp
   public static void Set(Label label, string text) { if (label.Text != text) label.Text = text; }
   public static void Set(Button button, string text) { if (button.Text != text) button.Text = text; }
   public static void Tint(Control c, Color colour, string name = "font_color")
   { if (c.GetThemeColor(name) != colour) c.AddThemeColorOverride(name, colour); }
   ```
   Confirm `Control.GetThemeColor` returns the overridden colour once one is set; if it
   does not, hold the last colour in a field instead.
2. Add `InitTop()` called once from the constructor, before the first `Refresh`, holding
   the `HFlowContainer` and every widget as a field:
   - `_turnText`, `_goldenAgeText`, `_goldText`, `_researchButton`, `_civicsButton`,
     `_citiesText`;
   - three rate groups, each `(Label Label, Button Minus, Button Plus)`, built by a
     `RateBox(kind)` that takes the kind once and wires the two stable callbacks
     `() => _session.ShiftRate(kind, -10)` and `+10`;
   - the panel buttons in their canonical order: Conquest, Religion, Corporations,
     Espionage, Events, Diplomacy, Pedia, Tech Tree, Save, Load. Create **all** of them
     and toggle `Visible` in `RefreshTop`, so the flow layout keeps a stable child order
     for the conditional ones (`GameUi.Panels.cs:49-75`).
3. `RefreshTop()` replaces `BuildTop` + `AddPanelButtons` and sets, comparing before
   writing:
   - turn/date/era text; golden age text and visibility; gold text and tint
     (`s.Net < 0 ? Ui.Bad : Ui.Accent`);
   - research text (`{Name} {Progress}/{Cost} ({Turns}t)`, or `Choose research!`) and
     its font colour override when there is no research;
   - each rate label `{name} {percent}%`;
   - civics text (`Civics` or `Civics · {n}t anarchy`);
   - `UpdateConquestModal(...)` with its side effects, then the conquest button's text,
     visibility and tooltip; corporations, espionage, events and tech-tree visibility
     from their availability calls, each called exactly once per refresh;
   - the cities/units text.
4. Remove `Ui.Clear(_top)`; `_top` keeps its one child row forever. `BuildTop`,
   `ResearchButton`, `RateBox` and `AddPanelButtons` are either folded into the two new
   methods or deleted. If `AddPanelButtons` survives, it takes the container and the
   already-described availability facts, and is called only from `InitTop`.

**Watch out.** `Ui.Text` sets `SizeFlagsHorizontal = ExpandFill`. Keep it, it is what
today's bar does. Keep `ClipText` and `CustomMinimumSize = new Vector2(170, 0)` on the
research button. The `_endTurn` button and `_message` label are already persistent and
must not be touched.

**Test Strategy.** This phase changes no `Civ.Client.Core` code, so there is no xunit
test to add. Gates, all recorded in the commit message:

- the probe: the top bar's per-panel time and a child-count check, printed temporarily
  from the timing line and removed before the commit, showing the row's child count is
  constant across ten clicks;
- the screenshot comparison for the top bar in these seven states: no research, research
  in progress, golden age active, anarchy active, pending conquests, pending events,
  corporations and espionage unavailable;
- a visual check that the top bar's scrape pattern no longer changes across a click.

`mise run test` still runs as the phase gate.

**Exit criteria.**

- Clicking a map tile several times creates no new top-bar children.
- The top bar shows the same values, colours, tooltips, button order and conditional
  buttons as before in all seven states, checked against the base commit's screenshots
  and by the reviewer's widget walk.
- The probe and screenshot results are in the commit message; `mise run test` passes.
- No known regression from an earlier phase.

**Encountered Serious Issues.**

None.

______________________________________________________________________

## Phase 4: Keep the left panel's widgets and update them in place

**Simple explanation.** The left panel shows one unit or nothing, plus a list of cities.
Build that skeleton once and then fill in the numbers, rebuilding only the order buttons
and the city list when the set of them changes.

**Files.** `src/Civ.Godot/GameUi.Left.cs`, `src/Civ.Godot/GameUi.cs`,
`src/Civ.Godot/Ui.cs` (only if a helper is missing).

**Change.**

1. `InitLeft()` called once from the constructor builds and keeps:
   - `_leftContent` (the `VBox`), inside `Ui.Scroll(...)` added to `_left` once;
   - `_unitEmpty` label ("Nothing selected. Click a unit or city.");
   - `_unitSection` `VBox` holding `_unitIcon` (`TextureRect`), `_unitName`,
     `_unitStats`, `_unitMovesHealth`, `_unitHealthBar` (`ProgressBar`), `_unitXp`,
     `_unitPromotions`, `_unitHint`, and `_ordersGrid` (`GridContainer`) with its action
     buttons;
   - `_separator`, the "Cities" heading, and `_cityList` `VBox`.
2. `RefreshLeft()` sets `Visible` on `_unitEmpty` / `_unitSection`, then:
   - icon texture, unit name, stats, "Moves .. Health ..%", the bar's `Value`, the xp
     text, promotions text (hide the label when empty, matching today's
     `if (Promotions.Count > 0)` at `GameUi.Left.cs:34`), and the constant hint;
   - the bar's fill tint only when the tint changes: keep the last tint in a field and
     call `AddThemeStyleboxOverride("fill", BrassSkin.BarFill(tint))` only on a change
     (`Ui.Bar`, `Ui.cs:144`);
   - the order buttons: rebuild `_ordersGrid` only when the identity key changes
     (`unit.Id` plus the joined `Actions[].Id` and `Label`), otherwise update `Disabled`
     and `TooltipText` on the existing buttons, comparing first;
   - the city buttons: rebuild `_cityList` only when the identity key changes: the
     ordered list of `(Id, Name, Population, ReligionBadge)` for
     `Sim.State.Cities.Where(c => c.Owner == GameSession.Human)`, joined into a string.
3. Keys are recomputed only on the revision-change path, never per frame, so the string
   allocation is acceptable there. State that in the commit message; it is what keeps
   `AGENTS.md`'s 60 Hz rule intact.
4. Delete `BuildLeft`, `UnitSection`, `ActionGrid` and `CityList` once their content is
   fully carried by the persistent widgets.

**Watch out.** `Ui.Bar` creates its text label only when `text.Length > 0`; the unit
health bar passes no text, so a persistent `ProgressBar` needs only `Value` and the fill
tint. Do not lose the `Ui.Icon(...)` size of 48. Keep the `Ui.Text(...)`
`SizeFlagsHorizontal` semantics inside the unit `VBox`. Keep the unit row's widgets in
one helper that fills the already-built labels, so a future stacked-unit list
(`docs/ROADMAP.md`, "More TODO Tasks" item 2) can repeat the row per unit. Do not
implement that list here.

**Test Strategy.** This phase changes no `Civ.Client.Core` code, so there is no xunit
test to add. Gates, all recorded in the commit message:

- the probe: the final before/after pair for ten clicks that change the selection and
  two that do not, using the Measurement Procedure. Before: a 13-21 ms rebuild per
  click. After: per-panel update under 1 ms, and no update at all for the two repeat
  clicks;
- the screenshot comparison for: unit selected, no selection, city selected, a unit with
  no actions, a unit with promotions, a city list that grew after founding a city;
- a scroll-retention check: scroll the city list, click the map, confirm the scroll
  position and the paint pattern survive.

`mise run test` still runs as the phase gate.

**Exit criteria.**

- Clicking a map tile repeatedly creates no new left-panel children when the selection
  identity is unchanged.
- The left panel shows the same content as the base commit in all six states above.
- The probe, screenshot and scroll-retention results are in the commit message;
  `mise run test` passes.
- No known regression from an earlier phase.

**Encountered Serious Issues.**

None.

______________________________________________________________________

## Phase 5: Record the work in OPTIMIZE.md and ROADMAP.md (`ask-antigravity`, no review)

**Simple explanation.** Write down what was measured and what is still open, in the two
documents that already track it.

**Files.** `docs/OPTIMIZE.md`, `docs/ROADMAP.md`.

**Change.**

1. `docs/OPTIMIZE.md`:
   - Fix the stale "uncommitted" markers for the startup work, which landed in `7f865e4`
     (merged by `8c483aa`): the mermaid timeline entry
     `[Phase 6 ...] (Uncommitted worktree changes)` (around line 95), the
     `**Primary Commit**: uncommitted worktree changes` line (around line 327) and the
     summary table row `**Phase 6: Startup Latency** | *uncommitted*` (around line 394).
     Use `7f865e4` as the primary commit.
   - Add `Phase 7: HUD Invalidation on Clicks` to the mermaid timeline, the summary
     table and its own section, following the shape of the existing Phase sections
     (Date, Primary Commit, Implementation Changes, Measurements, Validation). Sources:
     the measurements recorded in the commit messages of Phases 1-4, the Measurement
     Procedure in this plan, and the guard list from Phase 1. Record the residual items
     from the Stopping Rule.
   - Keep the rule that numbers are only worth reading from an unthrottled display; the
     Xvfb recipe is already in the Phase 6 section.
2. `docs/ROADMAP.md`:
   - Keep the "More TODO Tasks" list at the top of the file as it stands: same wording,
     numbering and the "-- needs to be reformatted later" heading, no reordering and no
     entries dropped. The one exception is item 2 (the stacked-unit list in the left
     pane), which Phase 4 is groundwork for: annotate that entry in place to say the
     left pane is now built once and refreshed in place, so listing every stacked unit
     is an extension of `RefreshLeft` rather than a rewrite. Do not mark it done, and do
     not implement the feature in the docs phase.
   - Note where else the completed work touches that list: persistent panels make item
     7's scroll-margin work easier. Do not mark that item done either.
   - If the roadmap has a section for engine or UI work, add one line recording that the
     HUD no longer rebuilds on every click, pointing at the OPTIMIZE.md section. Do not
     invent a new roadmap section.
3. Do not touch `AGENTS.md`, source files, or the plan.

**Exit criteria.**

- `docs/OPTIMIZE.md` has no remaining "uncommitted" reference to the startup work
  (`rg -n "uncommit" -i docs/OPTIMIZE.md` shows only the unrelated AI-speculation
  sentence at line 171).
- The top of `docs/ROADMAP.md` is unchanged apart from the item 2 annotation.
- `mise run test` still passes (docs-only change, but the gate is cheap and the plan
  requires it).

**Encountered Serious Issues.**

None.

______________________________________________________________________

# Plan Addendum: every panel, not just the top bar and the left pane

## Why this addendum exists

The plan closed with a residual it judged acceptable: "the city screen (`CityUi.Build`)
keeps rebuilding on a revision change while it is open, because its content is a whole
screen of numbers that change together", and a carried NOTE that "the modal and the feed
rebuild on any `Revision` change while open, including a selection click". For a player,
that residual is not a performance detail. It is the bug the plan claimed to fix, still
visible in every panel the plan did not convert:

- With the Pedia open, changing the Science budget re-randomised the scratch pattern on
  every Pedia button.
- Reordering a city production queue re-randomised every plate in both city panels and
  stalled the frame.

Both follow from the same two facts the plan already recorded, but only applied to the
two panels it converted. `Revision` moves on every command, selection change and
message, so any `Revision`-keyed panel rebuilds on unrelated actions (`Ui.Clear` frees
every child and builds new ones), and `BrushedStyleBox._Draw` seeds each plate's scrape
phase from its canvas item RID, so a rebuilt control is a new pattern. The plan's own
"Why" section promised that consequence away ("every plate changes its scratch pattern
on every click"), and it did not hold outside the top bar and the left pane. The
stopping rule was met as written and the player-visible defect was not, so the invariant
needed to be completed rather than deferred again.

## What was reproduced before the change

Xvfb `:99`, 1600x900 with the game at 1280x720 and +160,+90, Debug build, warm skin
cache, `--trace-performance`, `--world small`, baseline `7ae3717`. Pixel counts are
text-free crops of a single plate.

- Pedia open, Science `+` once: `ui update: … dialogs 87.34 ms`; one plate changed 702
  px (idle to idle: 0 px). Repeated: 80.25 ms / 701 px, 85.79 ms / 704 px.
- Pedia row click: `dialogs 123.79 ms`, plate 703 px.
- Research dialog open, Science `+`: `dialogs 12.66 ms`, plate crop 2.9 differing px.
- City screen open: `city screen: describe 87 ms, controls 400 ms` (`city 487.88 ms`).
- Queue reorder: `city screen: describe 20-64 ms, controls 219-267 ms`
  (`city 284-302 ms`), and the left panel's `Close` plate changed though nothing about
  it changed.
- `CityScreen.CheckEligibility` (from the city rebuild's `ProductionChoices`): 246 ms
  warm for 605 building candidates, 0.37 ms per `Validate`, repeated on every
  `WorldRevision` bump - which a queue reorder causes.

## Changes

Commits `60c8419` (the fix) and `922cbcd` (what the two reviews found).

### 1. Each panel is gated on the inputs it renders

`GameUi.Refresh` now tracks `WorldRevision` (simulation changes only: `Submit` and
`ContinueOrders`) and the open city's identity next to `Revision`. A panel is rebuilt
only when something it reads moved.

| Panel               | Gate                                                    |
| :------------------ | :------------------------------------------------------ |
| Top bar, left panel | `Revision` (they render the selection and message)      |
| City screen         | open, city identity, `WorldRevision`, or its tab state  |
| Research dialog     | open, or `WorldRevision`/city change while open         |
| Government dialog   | own revision, or `WorldRevision` while open             |
| Espionage           | own revision, or `Revision` (renders the selected spy)  |
| Data modals         | own revision, or `WorldRevision`/city change while open |
| Pedia, Load         | own revision only (catalog and saves folder)            |
| Feed                | `WorldRevision` or city change                          |

The data modals are religion, corporations, events, conquest, diplomacy and the tech
tree.

The city identity clause is not optional: `SelectCity` and `SelectAt` bump only
`Revision`, so switching between two open cities would otherwise go stale once the city
gate stops following `Revision`. `NeedsRefresh` still compares stored primitives and a
dictionary lookup, so the 60 Hz path stays allocation-free and early-outs.

### 2. Pedia updates in place

The search field, category tabs, list scroll and article scroll are built once per open.
Tabs update their disabled state, the list rebuilds only when the category, the query or
the ordered result ids change, and the article refills only when the selection changes
(returning to the top). A row click no longer touches the 300 list buttons or their
plates.

### 3. Both city panels update in place

`CityUi` builds one skeleton per panel and refreshes it. The production queue keeps one
row per position, rewritten in place (icon, caption, bar value, button states), and its
callbacks read the row's current index, so a reorder recreates nothing. Hurry,
conscript, choices, citizens, trade routes, settled specialists, the status block and
each ledger rebuild only when the text they render changed; the kind tabs and "Show
unavailable" stay and only their pressed state moves.

### 4. The research dialog updates in place

Its skeleton is built once and each choice's captions are rewritten when costs or turn
counts move; only a change in the choice set, layout or free-credit state recreates
widgets.

### 5. One eligibility pass per candidate list

`IProductionKind.ValidateBatch` validates a list in one call. The Cities mod runs it
inside the existing `CityReadScope` memo, so the whole-empire building, power and
resource work is shared instead of repeated 605 times; the project and process kinds
keep a trivial override and the interface member has a default implementation, so
out-of-tree kinds stay source compatible. `cities:building` eligibility for 605
candidates falls from 246 ms to 101 ms.

### 6. The two wrong-city defects the reviewers found

Persistence introduced a defect class the rebuild never had, and both reviews caught a
different half of it:

- Section keys were built with `new StringBuilder(city.Id)`, the capacity constructor,
  so the city id never entered the key. Two cities with the same visible choices or the
  same citizen layout shared a key, the section was not rebuilt, and its buttons still
  acted on the previous city.
- Click callbacks captured the `City` object they were built with. A game load replaces
  `Sim`, so after a load the reused rows held a discarded city: a reorder read the old
  queue and re-enqueued it into the new game. Callbacks now capture the city id and
  resolve the live city when they run.

The same capacity-constructor mistake in the left panel's order key was fixed too.

## Measurements after the change

Same setup, after `922cbcd`.

| Gesture                                       | Before                             | After                                                |
| :-------------------------------------------- | :--------------------------------- | :--------------------------------------------------- |
| Science `+` with the Pedia open               | 80-99 ms, plate 701-704 px changed | 0.00 ms, 0 px                                        |
| Pedia row click                               | 123.79 ms                          | 3.4-44.5 ms (article text shaping), list plates 0 px |
| Science `+` with the Research dialog open     | 12.66 ms, plate 2.9 px             | 2.5-3.8 ms, plate 0 px                               |
| Opening the city screen                       | 488 ms                             | 270-313 ms                                           |
| Queue reorder, steady state                   | 284-302 ms                         | 65-69 ms                                             |
| Queue reorder, first after opening            | 302 ms                             | 138-150 ms (cold)                                    |
| `cities:building` eligibility, 605 candidates | 246 ms                             | 101 ms                                               |
| Selection click with the city screen open     | screens closes, no city work       | unchanged                                            |

Plate crops that must not move after a queue reorder (left `Close` plate, a
production-choice plate, an untouched queue row's arrow plate, its progress-bar plate,
the conscript plate) all measure 0 differing px, against an idle-to-idle control of 0.
Screenshot regression across the `city`, `research`, `civics`, `map` and `world` stages
leaves only map animation and scrape phases, which differ between any two builds.
`mise run test` passes 1,299 tests with zero-warning builds.

## Review record

Two independent reviewers on the frozen commit, then a second round on the follow-up.

- In-family reviewer (`opencode-go/muse-spark-1.3-contributor`) reviewed `60c8419`:
  gate-by-gate input enumeration, key coverage, old-versus-new rendering walk, and live
  probes. Verdict: blocked by one DEMAND (callbacks captured `City`, so a load left them
  acting on a discarded city), with NOTEs on the missing default interface
  implementation and the Load modal going stale after a quicksave.
- `ask-antigravity` reviewer reviewed the same commit: blocked by one DEMAND (the
  capacity-constructor keys), and NOTEs on the trade/specialist keys, the pedia article
  keeping its scroll offset, and the wording of the "selection click does no city work"
  claim.
- Round two, on `922cbcd`: the in-family reviewer accepted both blocking causes as fixed
  and returned "shippable with NOTEs"; the `ask-antigravity` reviewer returned
  "shippable as-is". No DEMAND remained.

The new test,
`CityRulesTests.Batched_eligibility_equals_per_id_validation_for_buildings_and_units`,
holds the batch to the per-id results for every building and unit candidate, asserts
that the three blocker families it relies on are actually present, and that the batch
leaves the simulation hash unchanged.

## Residuals carried forward

- `DescribeCity` still runs on every city refresh (17-90 ms) and is now the largest part
  of a reorder; a queue add or remove also recomputes hurry costs and choice turn
  counts, so those sections legitimately rebuild (73-113 ms).
- Eligibility still costs 101 ms warm; inside the batch, `CityData.Level` and
  `EffectiveBuildings` are recomputed per candidate.
- The first refresh after opening the city screen, and the research dialog's first
  build, are cold paths (138-150 ms and 19-21 ms).
- The city screen still rebuilds wholesale on any `WorldRevision` while open, and every
  section key is recomputed there; the keys allocate strings on the revision path only,
  never per frame.
- A pedia row click still refills the article body (3-45 ms, Godot's wrapped-text
  shaping), and a category or query change still recreates up to 300 list buttons.
- Dither on translucent panel edges means whole-region captures differ by a few pixels
  between any two captures; the plate interiors are the stable measurement.
- `MapRenderer`'s dynamic layer still rebuilds per `Revision`, and the feed still
  rebuilds per `WorldRevision` change (both unchanged residuals).
- `docs/OPTIMIZE.md` Phase 7 named the top bar and left panel files as `GameUiTop.cs`
  and `GameUiLeft.cs`; neither file exists. The section now names `GameUi.cs` and
  `GameUi.Left.cs`.
