# Badge assets and placement

This directory contains the badge PNGs, source sheets, prompts, alignment data, attribution, and placement scripts.
`badges.json` maps catalog IDs to badge names.
`logo_geometry.py` contains optical offsets and badge sizing helpers.
Valknut attribution remains in `VALKNUT-LICENSE.md` and the accompanying source records.

## Place badges

Run this command from the repository root:

```sh
python3 icon-workbench/logos/apply_logo_overlays.py cities-001-8x8
```

The script uses the completed batch manifest and its generated source sheet.
It places badges only on mapped subjects.
It writes a composite sheet, 64×64 crops, and a placement manifest under the batch's `badged/` directory. Each crop has a 1-pixel inset on every side to remove edge bleed.
It refuses an existing output directory.

The script does not change the base sheet, review crops, or decisions.
The reviewer continues to show base artwork.
No command installs production assets.
Frames are a separate step with no supplied frame asset.
