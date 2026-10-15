# Icon review page

The page compares the current icon with its mapped candidate. It reads only the four catalog-mapped batches. It does not install or change game assets.

## Start the page

From the repository root, prepare the 64×64 candidate crops. This step needs Python 3 and Pillow:

```sh
python3 icon-workbench/prepare_review.py
```

Start the server on an available port:

```sh
python3 icon-workbench/review_server.py --port 0
```

Open the `http://127.0.0.1:<port>/` URL printed by the server. The server binds to `0.0.0.0` so the browser can reach it through the local network. Stop it with `Ctrl+C` when review ends.

The current review instance is at `http://127.0.0.1:40559/`. This URL works only while its server is running.

Anyone who can reach the port can save decisions. Use a trusted network.

## Approval meaning

The user approved the generated art for style and content. This approval does not approve centering, cropping, cell geometry, or final placement. No generated art is installed in production assets.

## Save decisions

New items start checked. Saving approves checked items. Uncheck an item to reject it, then enter a note. The server requires a note for every rejected item.

Items stay unreviewed until you save. A saved rejection note stays with its item. If you check a rejected item and save, the server changes it to approved and removes its rejection note.

The server saves approvals and rejection notes in `icon-workbench/review-selections.json`. Each submit updates only the current batch. It keeps decisions in the other batches. The page loads saved choices after a server restart.

Select **Hide approved icons** to hide items that were saved as approved when the batch loaded. Saved rejections and unreviewed items stay visible. An unreviewed item remains visible even though it starts checked. The filter does not change any decision. It keeps unsaved approval choices and rejection notes in place, and saving still submits every item in the current batch, including hidden approvals.

## Revised candidates

Revised candidate images are optional. The server reads `icon-workbench/revisions/logo-overlays/manifest.json` on each review or candidate-image request. The manifest format is:

```json
{
  "version": 1,
  "candidates": {
    "cities:example": {
      "file": "revisions/logo-overlays/example.png",
      "badge": "badge-key",
      "revision": "logo-overlay-v1"
    }
  }
}
```

Each entry must use a known catalog ID. Its file must be the matching `<catalog suffix>.png` file under `icon-workbench/revisions/logo-overlays/` and contain a 64×64 PNG image. The server uses this image only for the candidate preview. It does not change the original image or the generated base crop under `batches/`. The separate `revisions/` directory also keeps these files safe from `prepare_review.py` reruns.

A revised item is labeled **Revised candidate · review required**. The overlay does not change saved approvals, rejections, or notes. A revised item that was rejected stays rejected, and its prior rejection note remains visible. It changes to approved only after a user checks it and submits the normal review form.

## Image limits
The page serves only its HTML, CSS, JavaScript, mapped original icons, and generated crops. It does not serve directory listings or other repository files.

Each 1024×1024 sheet uses equal 12×12 divisions. The script resizes each cell to 64×64. A frame can touch a crop edge. Leave the item unchecked if the frame or subject looks cut off, then explain the problem in its note.

The source images stay under `src/Mods/*/art/icons/`. The review tool never writes to those paths.
