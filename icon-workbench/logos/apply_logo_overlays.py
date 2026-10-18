"""Place retained badges on a generated 8x8 sheet without changing base artwork."""

import argparse
import json
from pathlib import Path
import sys

from PIL import Image, ImageDraw

from logo_geometry import BADGE_OFFSETS, fit_badge
ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))
from reviewer import prepare_review


def badge_layer(key):
    path = ROOT / 'logos' / f'{key}.png'
    if Path(key).name != key or not path.resolve().is_relative_to((ROOT / 'logos').resolve()):
        raise ValueError('Invalid badge name')
    with Image.open(path) as source:
        badge = fit_badge(source.convert('RGBA'), key)
    layer = Image.new('RGBA', (64, 64))
    backing = Image.new('RGBA', (112, 112))
    draw = ImageDraw.Draw(backing)
    draw.ellipse((1, 1, 110, 110), fill=(11, 23, 39, 255), outline=(163, 183, 199, 255), width=4)
    layer.alpha_composite(backing.resize((28, 28), Image.Resampling.LANCZOS), (3, 3))
    dx, dy = BADGE_OFFSETS.get(key, (0, 0))
    layer.alpha_composite(badge, (3 + (28 - badge.width) // 2 + dx,
                                  3 + (28 - badge.height) // 2 + dy))
    return layer


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('batch', help='Completed batch directory name')
    args = parser.parse_args()
    batch, sheet_path, cells = prepare_review.load_batch(args.batch, set(), set())
    mapping = json.loads((ROOT / 'logos/badges.json').read_text())['badges']
    output = batch / 'badged'
    output.mkdir(exist_ok=False)
    with Image.open(sheet_path) as source:
        sheet = source.convert('RGBA')
    placed = []
    for cell in cells:
        key = mapping.get(cell['catalog_id'])
        if key:
            left, top, right, bottom = cell['edit_bounds']
            layer = badge_layer(key).resize((right - left, bottom - top), Image.Resampling.LANCZOS)
            sheet.alpha_composite(layer, (left, top))
            placed.append({'catalog_id': cell['catalog_id'], 'badge': key})
        crop = sheet.crop(prepare_review.crop_bounds(cell['edit_bounds'])).resize((64, 64), Image.Resampling.LANCZOS)
        crop.save(output / f'r{cell["row"]:02}-c{cell["column"]:02}.png')
    sheet.save(output / 'sheet.png')
    (output / 'manifest.json').write_text(json.dumps({'source': sheet_path.name, 'badges': placed}, indent=2) + '\n')
    print(f'Placed {len(placed)} badges in {output}. Base sheet and review decisions are unchanged.')


if __name__ == '__main__':
    main()
