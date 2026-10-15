# Icon workbench scripts

This guide covers the retained Python scripts in `icon-workbench/`. Run commands from the repository root unless a command says otherwise.

## Current state and safety

The generated art has user approval for style and content. That approval does not approve centering, cropping, cell geometry, or final placement. No generated icon is installed in production assets.

Generation scripts call the image API when Python loads the file. Do not import them. Do not run them unless you intend to make a paid generation request. Some scripts also overwrite prompts, images, manifests, and review outputs.

Make a safety copy of `icon-workbench/review-selections.json` and `icon-workbench/revisions/logo-overlays/manifest.json` before any publication script. No script in this workbench installs production assets.

Python scripts use Python 3. Pillow is required for image processing. The vendored YAML splitter also requires PyYAML for its legacy CLI. API generation requires the local OpenRouter image CLI and API credentials in the environment. Generation scripts use this CLI path:

```text
/home/gordon/.agents/skills/openrouter-image-generator/scripts/openrouter_images.py
```

## Review page

### `prepare_review.py`

Run:

```sh
python3 icon-workbench/prepare_review.py
```

The script reads the four mapped batches, validates their 144-cell manifests and source icons, then writes 64×64 crops and `geometry.json` under each batch's `review-crops/` directory. It requires Pillow and all original images under `src/Mods/`.

The script overwrites matching crop files and each `geometry.json`. It does not write review decisions. Its crops use equal divisions of each source sheet; the output does not prove that a subject is centered or safely framed.

### `review_server.py`

Run:

```sh
python3 icon-workbench/review_server.py --port 40559
```

The server reads the four batch manifests and prepared crops. It serves the review page, mapped originals, candidates, and optional revisions from `revisions/logo-overlays/manifest.json`. It saves approvals and rejection notes to `review-selections.json`. It does not install or edit source icons.

The server binds to `0.0.0.0`, so other devices on the reachable network can submit decisions. Use a trusted network. Stop the server with `Ctrl+C`. The current review URL is `http://127.0.0.1:40559/`; the URL is unavailable when its server is stopped.

The JSON state has version `1`, a `saved_at` timestamp, an `approved_catalog_ids` list, and a `rejected_catalog_notes` map. Each submission replaces decisions only for the submitted batch and retains decisions for other batches. Every item in the submitted batch must be approved or rejected. Every rejection needs a non-empty note of at most 1,000 characters. An item cannot be both approved and rejected.

A revised image changes only the candidate preview. It does not change a saved decision or rejection note. A user must approve the revised item through the review form.

## Batch generation

### `generate_batch.py`

This command accepts a JSON file and a new batch directory name:

```sh
python3 icon-workbench/generate_batch.py path/to/subjects.json cities-005
```

The JSON file must contain 144 objects. Each object needs a unique `path`, `catalog_id`, `label`, and `subject`. Each `path` must point to an existing file under `src/Mods/`. The command writes `cells.json` and `prompt.txt` into `icon-workbench/batches/<name>/`, then calls the image API to save one sheet there.

The batch name must be one directory name. The output directory must not already exist. The script creates it before the API call, so a failed request can leave partial output. Do not reuse or delete a batch directory without a safety copy. The script never installs assets.

### `generation/style-preview/generate_flare_12x12.py`

Run only for an intentional paid regeneration:

```sh
python3 icon-workbench/generation/style-preview/generate_flare_12x12.py
```

This script creates the 12×12 template and prompt, then calls the image API with the template and an existing style-reference image. It writes generated files under `generation/style-preview/`. It overwrites the template and prompt and can add another generated image. The retained source art is approved for style and content, not crop geometry.

## Logo generation and previews

### `make_logo_sheet.py`

Run only for an intentional paid regeneration:

```sh
python3 icon-workbench/make_logo_sheet.py
```

The script writes `logos/symbols.json` and `logos/prompt.txt`, then requests a badge sheet from the image API. It can overwrite both JSON and prompt files and add generated images under `logos/`.

### `generate_simple_logos.py`

Run only for an intentional paid regeneration:

```sh
python3 icon-workbench/generate_simple_logos.py
```

The script reads `logos/symbols.json`, writes a prompt under `logos/simple-v1/`, then requests a new badge sheet. It can overwrite that prompt and add a generated image.

### `apply_logo_overlays.py`

Run:

```sh
python3 icon-workbench/apply_logo_overlays.py
```

This local image-processing script reads the generated badge sheet, symbol list, current review state, and current overlay manifest. It extracts badge PNGs, writes badge assets under `logos/`, regenerates selected rejected logo candidates under `revisions/logo-overlays/`, and rewrites the manifest and contact sheet. It retains existing artwork-correction candidates, including approved ones. It can overwrite badge files and overlay files, even for approved items. Back up the manifest and affected images first. It does not change `review-selections.json` or base crops.

### `audit_badge_layout.py`

Run:

```sh
python3 icon-workbench/audit_badge_layout.py
```

The script reads the badge PNGs and `logos/symbols.json`. It writes `logos/alignment-audit-current.png` and `logos/alignment-metrics.json`. It overwrites those two audit outputs. It does not change candidates or review state.

### `logo_geometry.py`

This module provides `clean_badge()` and `fit_badge()` to the logo scripts. It has no command-line entry point and does not write files by itself. Do not import generation scripts to use these helpers.

## Artwork correction publication

### `generate_artwork_fixes.py`

This script has no arguments. It reads the saved review state and batch manifests, writes correction cells and a prompt under `revisions/artwork-fixes-001/`, then requests one 6×6 sheet from the image API.

Run only for a paid regeneration while all 31 listed catalog IDs remain rejected:

```sh
python3 icon-workbench/generate_artwork_fixes.py
```

The script asserts that all 31 IDs are currently rejected. After any of those items is approved, the assertion stops the script. It can overwrite `cells.json` and `prompt.txt` and add a generated sheet.

### `prepare_artwork_fixes.py`

Run:

```sh
python3 icon-workbench/prepare_artwork_fixes.py
```

The script reads the generated 6×6 sheet, correction cells, saved review state, and current overlay manifest. It crops the 31 listed subjects, applies the existing badge to the headquarters correction, writes base crops and revised candidates, then adds them to the overlay manifest. It also writes `review.json` and a one-time backup at `previous-review-manifest.json`.

It accepts only currently rejected IDs. It rewrites the manifest and candidate images, including paths that can already be approved. It is not a general rerun command after approvals. Back up the manifest and candidates first. The script checks that saved decisions and previously approved candidate image bytes remain unchanged during its run.

### `revisions/final-two/generate.py`

Run only for an intentional paid regeneration:

```sh
python3 icon-workbench/revisions/final-two/generate.py
```

The script requests two individual images: one effigy correction and one rectangular railgun correction. It reads the current overlay manifest for references and writes prompts and generated images under `revisions/final-two/`. It can overwrite prompts and add generated images.

### `revisions/final-two/publish.py`

Run:

```sh
python3 icon-workbench/revisions/final-two/publish.py
```

The script accepts only the two IDs when they remain rejected. It extracts the two square images to 64×64, copies them into `revisions/logo-overlays/`, updates those entries in the overlay manifest, and writes `review.json`. It asserts that saved decisions and all other candidate image hashes remain unchanged. The script rewrites the manifest and the two candidate files. It is not a general rerun command after approval. Back up the manifest and candidate files first.

## Extraction helpers

### `extraction/extract_icons.py`

The command-line tool extracts a known grid into square 64×64 RGBA PNGs. It accepts an input image, output directory, required row and column counts, and required source crop size. Optional flags control edge trimming, manual offsets, center search, and mask settings.

Example:

```sh
python3 icon-workbench/extraction/extract_icons.py input.png output-dir \
  --rows 6 --cols 6 --crop-size 164 --cut-edges 4
```

The output includes `icons/`, `geometry.json`, `_debug_crop_boxes.png`, and `_debug_background_mask.png`. Existing output files with matching names are overwritten; other files stay in place. Choose a new output directory or back up existing results first. Automatic center scores can be unhelpful when a frame encloses the cell background. Inspect the debug images and geometry; do not treat them as proof of correct framing.

For Python imports, add the extraction directory to `PYTHONPATH`. The module imports `split_icons_base` by its top-level name:

```sh
PYTHONPATH=icon-workbench/extraction python3 -c \
  'from extract_icons import extract_sheet; print(extract_sheet.__name__)'
```

### `extraction/split_icons_base.py`

This is the vendored splitter implementation used by `extract_icons.py`. It provides background-mask, center-search, crop, trim, resize, and debug-image functions. It has a separate legacy YAML command-line interface, but this workbench uses `extract_icons.py` as its entry point. The vendored source matches the external source named in `extraction/README.md`.

### Tests

`test_review_server.py` tests optional revised-candidate serving and rejection-state preservation. Run it with:

```sh
python3 icon-workbench/test_review_server.py
```

`extraction/tests/test_extract_icons.py` tests grid offsets, trimming, output dimensions, and debug files. Run it with:

```sh
PYTHONPATH=icon-workbench/extraction python3 -m unittest discover \
  -s icon-workbench/extraction/tests
```

The repository test commands are:

```sh
mise run test
mise run test:extractor
```

The default command excludes extractor and end-to-end tests. These commands are documented here, not run as part of this documentation update.
