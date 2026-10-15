from pathlib import Path
import math
import subprocess
from PIL import Image, ImageDraw

work = Path('/home/gordon/workspace/civ-worktrees/goodies/icon-workbench/generation/style-preview')
size, cell = 1536, 128
image = Image.new('RGB', (size, size))
pixels = image.load()
for y in range(size):
    for x in range(size):
        glow = math.exp(-(((x % cell) - 63.5)**2 + ((y % cell) - 63.5)**2) / (2 * 27**2))
        pixels[x, y] = tuple(int(a + (b - a) * glow) for a, b in zip((18, 32, 43), (155, 144, 100)))
draw = ImageDraw.Draw(image)
for row in range(12):
    for col in range(12):
        x, y = col * cell, row * cell
        draw.rectangle((x, y, x + 127, y + 127), outline=(35, 43, 50), width=1)
        draw.line([(x + 2, y + 125), (x + 2, y + 2), (x + 125, y + 2)], fill=(196, 209, 216), width=2)
        draw.line([(x + 125, y + 2), (x + 125, y + 125), (x + 2, y + 125)], fill=(100, 119, 133), width=2)
        draw.rectangle((x + 4, y + 4, x + 123, y + 123), outline=(59, 75, 86))
image.save(work / 'template-12x12.png')
rows = [
'pyramid, classical temple, library, academy, granary, windmill, aqueduct, castle, lighthouse, harbor, market stall, workshop',
'blacksmith, observatory, factory, courthouse, power station, laboratory, hospital, futuristic tower, water wheel, stone bridge, watchtower, city wall',
'spearman, archer, swordsman, mounted scout, knight, musketeer, envoy, explorer, merchant, scholar, engineer, physician',
'cannon, tank, fighter plane, airship, sailing ship, steamship, submarine, shield, helmet, sword, battle axe, wooden bow',
'wheat, corn, bananas, grapes, apple, fish, crab, clam, sheep, cow, horse, pig',
'deer, chicken, camel, elephant, copper ore, coal, iron ore, silver ingot, gold ingot, gemstones, salt, spices',
'silk spool, cotton, timber, stone blocks, pottery, coins, balance scales, open book, scroll, quill and ink, gears, telescope',
'compass, microscope, laboratory flask, leafy plant, DNA helix, atom, circuit chip, crown, laurel wreath, handshake, ballot box, map',
'hourglass, lantern, treasure chest, anvil, wagon, fountain, well, bakery, orchard, vineyard, amphitheater, monastery',
'ancient king, queen, philosopher, diplomat, admiral, general, worker, miner, architect, astronomer, botanist, chemist',
'olive branch, orange, lemon, pear, peach, cherry, pineapple, coconut, potato, carrot, onion, mushroom',
'rope coil, horseshoe, hammer, pickaxe, shovel, saw, plow, sickle, fishing net, loom, spinning wheel, barrel',
]
assert len(rows) == 12 and all(len(row.split(',')) == 12 for row in rows)
prompt = '''Make a precise 12x12 square icon atlas in the approved art style. Image 1 is the NEW MANDATORY 12x12 empty grid. Image 2 is the latest painted icon sheet, provided ONLY for visual style and torso-up character framing; its dense grid has incorrect shrunken bottom rows, which must NOT be copied.
Use image 1 for ALL geometry. Exactly TWELVE columns and TWELVE rows, 144 equal SQUARE cells. Every row must have IDENTICAL HEIGHT from the first through the last. Every column must have identical width. All horizontal and vertical boundaries must stay on the template lines. The bottom four rows must be the SAME HEIGHT as the top four rows; do not squeeze the bottom rows to make content fit. This is a flat orthographic atlas, with no page perspective or foreshortening of the grid. If subjects need to fit, simplify their details inside their own cells rather than changing cell sizes. No merged cells, no additional rows, no omitted cells. The last row ends at the bottom image edge with full-size square cells.
Preserve image 2's approved mature stylized painted look, broad shaded forms, warm rich colors, silver beveled square frames, charcoal-blue backgrounds and clearly visible soft ivory or pale cyan glows behind each subject. Neither photorealistic nor cartoonish; no thick black ink outlines. All HUMAN CHARACTERS are natural-proportioned TORSO-UP portraits with headroom, not squashed full-body figures. Mounted characters are torso-up rider portraits with recognizable riding equipment, not a whole horse and rider squeezed into a square. Animals retain natural proportions. Keep one coherent subject per cell, no hybrid tools, no merged subjects. No labels, text or numbers. Original artwork only.
Below are exactly TWELVE rows, each containing TWELVE subjects, in left-to-right order. Fill these 144 cells without changing the grid:
'''
for index, row in enumerate(rows, 1):
    prompt += f'Row {index}: {row}.\n'
prompt += 'Final geometry requirement: 12 identical-height rows, 12 identical-width columns, 144 identical square cells. Bottom row cells must have exactly the same dimensions as top row cells. Match the grid template, not the style references faulty layout.'
(work / 'flare-12x12-prompt.txt').write_text(prompt)
subprocess.run(['python3', '/home/gordon/.agents/skills/openrouter-image-generator/scripts/openrouter_images.py', '--model', 'openai/gpt-image-2.5-flare', '--aspect-ratio', '1:1', '--quality', 'high', '--count', '1', '--no-chain', '--output-dir', str(work), '--prefix', 'flare-12x12-torso-v4', '--reference', str(work / 'template-12x12.png'), '--reference', str(work / 'flare-16x16-torso-v3-20261015-031507-001.png'), '--prompt', prompt], check=True)
