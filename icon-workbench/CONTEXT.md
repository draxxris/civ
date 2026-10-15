# Icon replacement checkpoint

## Stop condition and approval scope

Generation is paused at the user request.
All existing generated artwork has approval for style and content.
**No icon has approval for centering, cropping, border trimming, or square framing.**
The saved review checkboxes do not represent geometry approval.
No production assets changed, and no generated icons are installed.

## Goal and coverage

Replace all 1,153 inherited catalog icons with original artwork, including unused icons.
Terrain textures and documentation screenshots are outside this icon task.
Four Cities batches cover 576 mapped candidates.
Six Cities icons remain in `audit/cities-remaining-tail.json`.
The other categories also remain unfinished: Military, Peoples, World, Foundation, Research, Government, and Economy.
A total of 577 catalog icons still lack mapped replacement candidates.
The mixed-subject style sample is not an installed or fully mapped batch.

## Current files

All paths here are relative to `icon-workbench/`.

- `status.json`: concise checkpoint and counts.
- `geometry-review.json`: 576 current candidate paths, image hashes, and pending geometry status.
- `review-selections.json`: user review decisions, preserved without modification.
- `batches/cities-001/` through `cities-004/`: source sheets, cell mappings, prompts, API records, and provisional crops.
- `revisions/logo-overlays/manifest.json`: current candidate overrides, including non-logo corrections.
- `revisions/artwork-fixes-001/`: 31-subject correction sheet and mapping.
- `revisions/final-two/`: final burning-effigy and rectangular-railgun sources.
- `logos/simple-v1/`: simplified badge source sheet and prompt.
- `logos/`: extracted badges, current alignment audit, symbol list, and Valknut source attribution.
- `extraction/`: adapted splitter, vendored source, tests, and instructions.
- `extraction-audit/artwork-fixes-001/`: staged crop comparison and debug evidence.
- `audit/inventory.json`: complete source inventory and catalog associations.
- `SCRIPTS.md`: Python entry points, dependencies, outputs, and overwrite hazards.
- `REVIEW-README.md`: review page instructions.
- `cleanup-record.json`: deleted file paths, sizes, and hashes.

## Approved art direction

The model is `openai/gpt-image-2.5-flare`, with high quality and square output.
The preferred batch layout is 12×12.
The style uses mature painted shapes, warm colors, charcoal-blue backgrounds, silver beveled frames, and visible soft glows.
Human figures use natural-proportioned torso-up portraits.
Companies and guilds use original badges.
Religions use established symbols, including a Valknut for Asatru.

The retained style reference is:

`generation/style-preview/flare-12x12-torso-v4-20261015-031843-001.png`

Its prompt, template, generator, and upstream style reference remain in the same directory.
Earlier models failed style or grid requirements.
Do not switch to a model without image-reference support.
Do not resume paid generation during the geometry checkpoint.

## Unresolved geometry issue

The base review crops use equal divisions of each generated sheet.
The correction sheet also used equal divisions before resizing.
A generated frame can drift across those divisions or have unequal width and height.
A 64×64 output file therefore does not prove correct geometry.
Resizing an incorrect rectangle can distort the subject and preserve unwanted neighboring borders.
The existing crops remain provisional, even where the user approved their artwork.

The supplied splitter is `/home/gordon/workspace/felix/tools/split_icons.py`.
Its complete vendored copy is `extraction/split_icons_base.py`.
The wrapper supports square source crops, center search, edge trimming, manual offsets, debug images, and 64×64 outputs.

The first audit found an important limitation.
Silver frames enclose blue backgrounds, which prevents useful edge-connected mask detection.
All 36 automatic balance scores were zero on the tested correction sheet.
Thus automatic centering did not improve those equal-cell positions.
The staged audit is evidence, not an approved extraction solution.

Badge extraction already uses checked row boundaries because the generated badge sheet has uneven rows.
The geometry helper removes detached neighboring fragments and applies badge-specific optical offsets.
Those local corrections do not establish whole-icon crop approval.

## Resume procedure

1. Read this checkpoint and `SCRIPTS.md`.
2. Keep saved artwork approvals separate from geometry decisions.
3. Develop extraction changes in a new staging directory.
4. Compare source boundaries, current crops, and proposed square crops.
5. Inspect debug boxes and native-size contact sheets before publication.
6. Use explicit per-cell bounds or manual offsets where automatic detection fails.
7. Trim overflow borders without clipping subjects or distorting their proportions.
8. Recompose badges from source art after base crop changes, rather than resizing an existing composite.
9. Preserve source sheets, mappings, badge assets, and attribution.
10. Record geometry approval separately before any production installation.

Do not rerun publication scripts casually.
Several scripts overwrite approved candidate files or require IDs to remain rejected.
The saved form now has 576 approvals and no rejection notes.
These are artwork decisions only.

## Review server

The last checked URL is `http://127.0.0.1:40559/`, bound to `0.0.0.0:40559`.
The page supports rejection notes, revised candidates, and a hide-approved filter.
The filter uses saved artwork decisions, not geometry status.
The server does not yet provide separate geometry review.
It reads current overrides from `revisions/logo-overlays/manifest.json`.
The legacy directory name also contains general artwork corrections.

## Cleanup and checks

The cleanup removed superseded calibration images, obsolete generators, old overlay snapshots, and disposable caches.
It retained current sources, prompts, API records, mappings, review data, and active images.
Historical API records can refer to deleted calibration outputs.
Those records are provenance, not a complete replay bundle.
The cleanup preserved all 576 current candidate image hashes and the saved decisions.

The prior repository test run had 429 passes, three skips, and one unrelated dogfood failure.
That failure concerns mod identifiers in comments in `src/Civ.Godot/Debug/DebugScenario.cs`.
The prior log remains at `test-results.log`.
Run `mise run test` after production changes.

## Repository constraints

Keep task files inside this Git worktree.
Do not commit without a user request.
Obey the root product terminology restrictions.
Mise activation already occurs for shell commands.
Never write API credentials into task files.
