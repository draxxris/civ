"""Create native-size and enlarged inspection sheets for every active badge."""
import json
from pathlib import Path
from PIL import Image, ImageDraw
from logo_geometry import BADGE_OFFSETS, fit_badge

root = Path(__file__).resolve().parent
symbols = json.loads((root / 'logos/symbols.json').read_text())
canvas = Image.new('RGB', (6 * 160, 8 * 164), (28, 35, 44))
draw = ImageDraw.Draw(canvas)
results = []
for index, (key, _) in enumerate(symbols):
    if key.startswith('blank'):
        continue
    badge = fit_badge(Image.open(root / 'logos' / f'{key}.png').convert('RGBA'), key)
    panel = Image.new('RGBA', (28, 28))
    pen = ImageDraw.Draw(panel)
    pen.ellipse((0, 0, 27, 27), fill=(11, 23, 39, 255), outline=(163, 183, 199, 255))
    dx, dy = BADGE_OFFSETS.get(key, (0, 0))
    x = (28 - badge.width) // 2 + dx
    y = (28 - badge.height) // 2 + dy
    panel.alpha_composite(badge, (x, y))
    error_x = x + badge.width / 2 - 14
    error_y = y + badge.height / 2 - 14
    assert abs(error_x - dx) <= 0.5 and abs(error_y - dy) <= 0.5
    results.append({'badge': key, 'visible_size': list(badge.size), 'optical_offset_px': [dx, dy], 'center_error_px': [error_x, error_y]})
    left = index % 6 * 160
    top = index // 6 * 164
    canvas.paste(panel, (left + 12, top), panel)
    enlarged = panel.resize((112, 112), Image.Resampling.NEAREST)
    canvas.paste(enlarged, (left + 12, top + 32), enlarged)
    draw.text((left + 12, top + 146), key, fill='white')
canvas.save(root / 'logos/alignment-audit-current.png')
(root / 'logos/alignment-metrics.json').write_text(json.dumps(results, indent=2) + '\n')
print(f'Prepared actual-size and enlarged views for {len(results)} badges.')
