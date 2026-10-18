# Terrain texture generation report

## Generation

Each layer has one candidate from `openai/gpt-image-2.5-sunburst` and one from `meta/muse-image`.
The model capability queries ran before generation.
Sunburst used `--quality medium --background opaque --aspect-ratio 1:1` and returned 1024 × 1024 PNGs.
Muse used no optional image parameters and returned 1600 × 1600 WebP images.
Neither model exposed a seed for these requests. No refinement requests were necessary after comparison and in-game inspection.
Candidate outputs and request manifests are in `/tmp/p5/candidates/<layer>/<model>/`.
The comparison sheets are `/tmp/p5/comparison-0.png` through `/tmp/p5/comparison-2.png`.

## Post-processing P1

`terrain_textures.py` uses Pillow and NumPy.
It resizes RGB to 1024 × 1024 with Lanczos and offsets both axes by half the image size.
A smoothstep cross feather covers 18 percent of each axis on each side of the central seam.
One-axis offsets support the cross arms. The original supports the crossing.
This separable cross avoids exposing non-periodic source edges at the cross endpoints.
A 24-pixel smoothstep feather matches opposite edge samples exactly, including the corners.

The height channel uses luminance coefficients 0.2126, 0.7152, and 0.0722.
The script subtracts a radius-12 Gaussian blur, computed over a 3 × 3 repeat for periodic support.
It normalizes the result to [0, 1] and stores it in 8-bit alpha.
It matches RGBA edges again after height generation.
Every output is a 1024 × 1024 RGBA PNG.
The script asserts equal opposite RGBA edges and saves RGB contact sheets outside the repository.

## Layer selections

### grass

- Selected model: `openai/gpt-image-2.5-sunburst`.
- Selected output: `grass-20261014-174348-001.png`.
- Comparison: Sunburst has soft, continuous meadow strokes. Muse has distinct raised grass clumps and stronger implied lighting.
- Post-processing: P1, with no layer-specific color adjustment.
- Final: `src/Mods/World/art/terrain/grass.png`.
- Contact sheet: `/tmp/p5/contact-sheets/grass.png`, inspected without visible seam lines.

Full prompt (identical for both candidates):

> Fresh meadow ground, muted medium leaf green and moss green, tiny soft painted grass strokes, continuous dense ground cover, subtle patches of olive soil. seamless tileable texture, straight top-down orthographic view, flat even lighting with no cast shadows or highlights, stylised hand-painted look like Civilization VI terrain (soft colour variation, painterly strokes, no photographic detail), no objects, no text, no horizon, 1:1

### plains

- Selected model: `meta/muse-image`.
- Selected output: `plains-20261014-174347-001.webp`.
- Comparison: Muse has softer dry-earth mottling and finer strokes. Sunburst has conspicuous grass tufts with implied shadows.
- Post-processing: P1, with no layer-specific color adjustment.
- Final: `src/Mods/World/art/terrain/plains.png`.
- Contact sheet: `/tmp/p5/contact-sheets/plains.png`, inspected without visible seam lines.

Full prompt (identical for both candidates):

> Dry prairie ground, warm muted golden ochre and straw beige, tiny painted dry grass strokes, subtle dusty earth mottling. seamless tileable texture, straight top-down orthographic view, flat even lighting with no cast shadows or highlights, stylised hand-painted look like Civilization VI terrain (soft colour variation, painterly strokes, no photographic detail), no objects, no text, no horizon, 1:1

### desert

- Selected model: `openai/gpt-image-2.5-sunburst`.
- Selected output: `desert-20261014-174347-001.png`.
- Comparison: Sunburst has low-contrast, broad painted sand ripples. Muse has sharper, densely repeated ridges and rusty spots.
- Post-processing: P1, with no layer-specific color adjustment.
- Final: `src/Mods/World/art/terrain/desert.png`.
- Contact sheet: `/tmp/p5/contact-sheets/desert.png`, inspected without visible seam lines.

Full prompt (identical for both candidates):

> Warm sandy desert ground, pale golden beige sand, subtle soft shallow ripples, low contrast ochre mottling. seamless tileable texture, straight top-down orthographic view, flat even lighting with no cast shadows or highlights, stylised hand-painted look like Civilization VI terrain (soft colour variation, painterly strokes, no photographic detail), no objects, no text, no horizon, 1:1

### tundra

- Selected model: `meta/muse-image`.
- Selected output: `tundra-20261014-174408-001.webp`.
- Comparison: Muse has soft sage lichen mottling. Sunburst has large stone plates that look too close to the cliff layer.
- Post-processing: P1, with no layer-specific color adjustment.
- Final: `src/Mods/World/art/terrain/tundra.png`.
- Contact sheet: `/tmp/p5/contact-sheets/tundra.png`, inspected without visible seam lines.

Full prompt (identical for both candidates):

> Cold tundra ground, muted grey sage and stone beige earth, subtle lichen mottling, sparse tiny grass strokes, no snow cover. seamless tileable texture, straight top-down orthographic view, flat even lighting with no cast shadows or highlights, stylised hand-painted look like Civilization VI terrain (soft colour variation, painterly strokes, no photographic detail), no objects, no text, no horizon, 1:1

### snow

- Selected model: `meta/muse-image`.
- Selected output: `snow-20261014-174414-001.webp`.
- Comparison: Muse has restrained blue-white powder and fine wind streaks. Sunburst has larger, stronger blue patches.
- Post-processing: P1, with no layer-specific color adjustment.
- Final: `src/Mods/World/art/terrain/snow.png`.
- Contact sheet: `/tmp/p5/contact-sheets/snow.png`, inspected without visible seam lines.

Full prompt (identical for both candidates):

> Snow-covered ground, soft white and pale cool blue, delicate low contrast powder snow mottling and shallow wind streaks. seamless tileable texture, straight top-down orthographic view, flat even lighting with no cast shadows or highlights, stylised hand-painted look like Civilization VI terrain (soft colour variation, painterly strokes, no photographic detail), no objects, no text, no horizon, 1:1

### marsh

- Selected model: `meta/muse-image`.
- Selected output: `marsh-20261014-174425-001.webp`.
- Comparison: Muse has soft damp mud mottling without raised plants. Sunburst has conspicuous grass clumps and darker patch boundaries.
- Post-processing: P1, with no layer-specific color adjustment.
- Final: `src/Mods/World/art/terrain/marsh.png`.
- Contact sheet: `/tmp/p5/contact-sheets/marsh.png`, inspected without visible seam lines.

Full prompt (identical for both candidates):

> Wet marsh ground, subdued moss green and grey olive mud, soft mottled damp earth patches, tiny painted grass strokes, no open water. seamless tileable texture, straight top-down orthographic view, flat even lighting with no cast shadows or highlights, stylised hand-painted look like Civilization VI terrain (soft colour variation, painterly strokes, no photographic detail), no objects, no text, no horizon, 1:1

### hill

- Selected model: `openai/gpt-image-2.5-sunburst`.
- Selected output: `hill-20261014-174429-001.png`.
- Comparison: Sunburst has small painted stones mixed with olive grass. Muse has sharper gravel highlights and stronger lighting.
- Post-processing: P1, with no layer-specific color adjustment.
- Final: `src/Mods/World/art/terrain/hill.png`.
- Contact sheet: `/tmp/p5/contact-sheets/hill.png`, inspected without visible seam lines.

Full prompt (identical for both candidates):

> Rocky grass ground for rolling hills, muted olive meadow greens interspersed with flat grey beige stony patches, small ground-level gravel strokes, no boulders. seamless tileable texture, straight top-down orthographic view, flat even lighting with no cast shadows or highlights, stylised hand-painted look like Civilization VI terrain (soft colour variation, painterly strokes, no photographic detail), no objects, no text, no horizon, 1:1

### rock

- Selected model: `openai/gpt-image-2.5-sunburst`.
- Selected output: `rock-20261014-174431-001.png`.
- Comparison: Sunburst has natural painted fractures and muted slate planes. Muse resembles regular paving stones with heavy outlines.
- Post-processing: P1, with no layer-specific color adjustment.
- Final: `src/Mods/World/art/terrain/rock.png`.
- Contact sheet: `/tmp/p5/contact-sheets/rock.png`, inspected without visible seam lines.

Full prompt (identical for both candidates):

> Cliff rock surface, muted slate grey and warm grey, soft painted fractured stone planes and narrow cracks, continuous rock material, not a mountain landscape. seamless tileable texture, straight top-down orthographic view, flat even lighting with no cast shadows or highlights, stylised hand-painted look like Civilization VI terrain (soft colour variation, painterly strokes, no photographic detail), no objects, no text, no horizon, 1:1

### seabed

- Selected model: `openai/gpt-image-2.5-sunburst`.
- Selected output: `seabed-20261014-174446-001.png`.
- Comparison: Sunburst has subtle sand ripples with fine speckles. Muse has broad directional bands that repeat more visibly.
- Post-processing: P1, with no layer-specific color adjustment.
- Final: `src/Mods/World/art/terrain/seabed.png`.
- Contact sheet: `/tmp/p5/contact-sheets/seabed.png`, inspected without visible seam lines.

Full prompt (identical for both candidates):

> Shallow seabed sand material without water above it, muted pale tan sand with soft painted ripples and tiny sandy speckles. seamless tileable texture, straight top-down orthographic view, flat even lighting with no cast shadows or highlights, stylised hand-painted look like Civilization VI terrain (soft colour variation, painterly strokes, no photographic detail), no objects, no text, no horizon, 1:1

### deep_seabed

- Selected model: `openai/gpt-image-2.5-sunburst`.
- Selected output: `deep_seabed-20261014-174454-001.png`.
- Comparison: Sunburst has subdued blue-grey silt and sand. Muse has conspicuous rocky plates and pale spots.
- Post-processing: P1, with no layer-specific color adjustment.
- Final: `src/Mods/World/art/terrain/deep_seabed.png`.
- Contact sheet: `/tmp/p5/contact-sheets/deep_seabed.png`, inspected without visible seam lines.

Full prompt (identical for both candidates):

> Deep seabed material without water above it, muted dark blue grey silty sand, low contrast soft sandy mottling. seamless tileable texture, straight top-down orthographic view, flat even lighting with no cast shadows or highlights, stylised hand-painted look like Civilization VI terrain (soft colour variation, painterly strokes, no photographic detail), no objects, no text, no horizon, 1:1

### unexplored

- Selected model: `openai/gpt-image-2.5-sunburst`.
- Selected output: `unexplored-20261014-174457-001.png`.
- Comparison: Sunburst has soft painterly cloud wisps without sky gaps. Muse has strongly lit, raised billows that repeat more visibly.
- Post-processing: P1, with no layer-specific color adjustment.
- Final: `src/Mods/World/art/terrain/unexplored.png`.
- Contact sheet: `/tmp/p5/contact-sheets/unexplored.png`, inspected without visible seam lines.

Full prompt (identical for both candidates):

> Stylised blanket of soft rounded cloud wisps seen from above, muted blue grey with creamy white billows, painterly cloud material, low contrast, no sky gaps. seamless tileable texture, straight top-down orthographic view, flat even lighting with no cast shadows or highlights, stylised hand-painted look like Civilization VI terrain (soft colour variation, painterly strokes, no photographic detail), no objects, no text, no horizon, 1:1

## Content aliases

The script also writes five byte-identical aliases for the content-id asset lookup:

- `peak.png` uses `rock.png`.
- `coast.png` and `slimy_coast.png` use `seabed.png`.
- `ocean.png` and `slimy_ocean.png` use `deep_seabed.png`.

The shader retains the palette green tint for slimy water and its bed.
Only the invisible `world:none` placeholder uses a solid fallback.
No visible World terrain lacks a texture. Surface lookups still use the original four surface ids.
The 16 PNGs total approximately 30 MiB. Alias content deduplicates in Git LFS.
The 17-layer RGBA8 array with mipmaps needs approximately 91 MiB before driver overhead.

## Shader and runtime checks

The array uses uniform RGBA8 images, including the scaled 4 × 4 fallback with neutral height alpha.
Godot generates mipmaps before `Texture2DArray.CreateFromImages`.
Mesa llvmpipe accepted the array and shaders on the Compatibility renderer without shader errors.

Both texture scales use an integer number of repeats across the map period.
The second sample swaps its axes and adds a fixed offset.
Periodic low-frequency noise mixes the samples. Unwrapped sample coordinates avoid mip-derivative jumps at the wrap line.
The height-blend depth is 0.2. Alpha contributes a 0.18 height range to preserve zero-weight support at triangle edges.
A world-space normal controls slope rock. Feature tint and fog apply after the slope blend.
Two periodic scrolling cloud samples cover hidden terrain without its actual material.

Inspected screenshots:

- `/tmp/p5/world.png`: textured biome blends and seabeds.
- `/tmp/p5/peak.png`: textured mountains and rock on steep slopes.
- `/tmp/p5/wrap.png`: camera centered at x = 0, with no visible wrap seam.
- `/tmp/p5/close.png`: grass, plains, shallow sand, and painted cloud coverage.
- `/tmp/p5/map.png`: tokens above textured ground and unexplored clouds.

The build passed with zero errors and zero warnings. The headless smoke exited 0.
All five software-GL screenshot runs exited 0. Only the existing XIM and V-Sync warnings occurred.
`mise run test` passed: 288 mod tests, 80 core tests, 37 import tests, and 3 existing skips.
The fallback test now uses the untextured placeholder because grass supplies art.
Contact sheets and generation intermediates remain in `/tmp/p5/`. No documentation screenshots changed.
