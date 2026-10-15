import json
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parent
out = root / 'logos/simple-v1'
out.mkdir(parents=True, exist_ok=True)
symbols = json.loads((root / 'logos/symbols.json').read_text())
symbols[38][1] = 'traditional Valknut of three interlocking triangles, matching reference 2 geometry'
prompt = '''Redesign the supplied badge sheet for TINY 22-PIXEL DISPLAY. These are overlay glyphs, not illustrations. Reference 1 supplies the subject identities and exact layout ONLY. Remove its excessive detail completely. Reference 2 supplies accurate Valknut geometry, replacing the hammer symbol.
EXACTLY SIX COLUMNS and EIGHT ROWS, 48 equal cells on a square canvas. Preserve cell order. Each glyph must fit within the central 60 percent of its cell, with wide empty margins so no part can enter a neighboring cell. PURE SOLID BLACK background. No grid lines, tile borders, badge rings, discs, shadows, labels, captions or extra marks.
Draw each symbol as a bold FLAT PICTOGRAM with ONE warm ivory or gold color, at most ONE additional flat accent color where essential. Thick simple strokes and broad negative spaces. NO gradients, shading, highlights, outlines around outlines, bevels, materials, textures, tiny decoration, facial details, fur, feather barbs, bread scoring, glass reflections, crown jewels, stitching, individual fish scales or nested ornament. Prefer silhouette recognition over realistic drawing. Readable at 22 pixels. No fine line thinner than about one twelfth of the glyph height.
Company letters are plain bold monograms, NOT decorative lettering or interwoven tiny letters. The W candy company uses a simple W only. Guild objects are simple recognizable silhouettes: music as two connected notes, quill as a plain feather shape, bull and cow as simple head silhouettes, soldier as a helmeted bust without facial detail, cloth and textile as distinct simple folded/rolled material symbols. Religious symbols must retain their established geometry: Om, Valknut, eight-spoke Dharma wheel, Latin cross, crescent and star, Star of David, torii, taijitu with two opposite dots. No invented religious motifs. The Valknut is three linked TRIANGLES, never a hammer or triquetra. All other company designs are original, not copies of existing commercial marks.
Strict row-major subjects:\n'''
for row in range(8):
    prompt += f'Row {row+1}: ' + '; '.join(item[1] for item in symbols[row*6:(row+1)*6]) + '.\n'
prompt += 'Only the glyph in each cell, centered with generous margins. Final result must look like a tiny UI symbol font, not a painted object inventory.'
(out / 'prompt.txt').write_text(prompt)
subprocess.run(['python3','/home/gordon/.agents/skills/openrouter-image-generator/scripts/openrouter_images.py','--model','openai/gpt-image-2.5-flare','--quality','high','--aspect-ratio','1:1','--count','1','--no-chain','--output-dir',str(out),'--prefix','simple-badges','--reference',str(root/'logos/badge-sheet-20261015-051321-001.png'),'--reference',str(root/'logos/valknut.png'),'--prompt',prompt],check=True)
