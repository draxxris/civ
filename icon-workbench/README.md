# Icon workbench

Generate fresh borderless artwork in masked 8×8 sheets at 816×816 pixels.
The model is `gpt-image-2.5-flare`. Prefer low quality for generation: it produces simpler icons with less visual detail. Use the direct OpenAI image skill.
Frames and badges are separate steps.
No existing artwork enters generation requests.

## Directories

- `reviewer/`: review server, browser UI, crop preparation, tests, and [review instructions](reviewer/README.md).
- `logos/`: badge assets, placement scripts, attribution, mapping, and [badge instructions](logos/README.md).
- `layout/`: the pink-gutter generation baseline and edit mask.
- `batches/`: source sheets, prompts, request records, mappings, and review crops.
- `prompts/`: active JSON subject lists for generation, including original descriptions, revisions, and rejection reasons.
- `audit/`: the original image inventory for catalog coverage. No active audit scripts remain.

Active commands do not read `audit/`.

The accepted workflow sample is `batches/cities-001-8x8/sheet.png`.
It contains the first 64 revised Cities subjects.
Workflow acceptance does not approve each icon or install production assets.

## Generate a batch

Run commands from the repository root.
Python 3, Pillow, and requests are required.
The direct image skill must exist under `~/.agents/skills/openai-image-generator/`.

Prepare a batch without a paid request.
`cities-draft` is an example output directory name, not an input file:

```sh
python3 icon-workbench/generate_batch.py \
  icon-workbench/prompts/cities.json cities-draft --start 64 --count 64 --dry-run
```

For a paid request, use a different new batch name:

```sh
source ~/.zshenv
python3 icon-workbench/generate_batch.py \
  icon-workbench/prompts/cities.json cities-002-8x8 --start 64 --count 64
```

The script accepts `OPENAI_IMAGE_API_KEY` or `OPENAI_API_KEY` from the environment.
Never store credentials in workbench files.
Offsets are zero-based.
Existing output directories are refused.
Partial batches leave unused squares blank and exclude them from review.

The layout has 90×90 editable squares, 10-pixel gutters, and 13-pixel outer margins.
The skill preserves protected pixels when it saves the result.
Inspect subject placement and clipping before review.
A failed request can leave a prepared directory without a sheet.

`prompts/cities.json` contains 576 mapped subjects.
`prompts/cities-tail.json` preserves six remaining subjects in their original list format.
Convert that list to a version-1 object with a `cells` array before generation.

## Checks

```sh
python3 icon-workbench/reviewer/test_review_server.py
node --check icon-workbench/reviewer/review.js
mise run test
```

No workbench command installs production assets.
