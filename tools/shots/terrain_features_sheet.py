#!/usr/bin/env python3
"""Join the terrain feature captures into one labelled contact sheet.

    python3 tools/shots/terrain_features_sheet.py /tmp/tf docs/screenshots/terrain-features.png
"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

PANELS = [
    ("forest", "Forest (grass)"), ("forest_tundra", "Forest (tundra)"), ("jungle", "Jungle (grass)"),
    ("jungle_marsh", "Jungle (marsh)"), ("swamp", "Swamp (marsh)"), ("flood_plains", "Flood Plains (desert, river)"),
    ("oasis", "Oasis (desert)"), ("fallout", "Fallout (plains)"), ("infectious_smog", "Infectious Smog"),
    ("plagued_smog", "Plagued Smog"), ("volcano", "Volcano (peak)"), ("ice", "Ice (coast)"),
    ("storm", "Storm (ocean)"), ("overview", "Overview"),
]
COLUMNS, WIDTH = 4, 560


def font(size):
    for path in ("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", "/usr/share/fonts/TTF/DejaVuSans-Bold.ttf"):
        if Path(path).exists():
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


def main(source, target):
    images = [(Image.open(Path(source) / f"tf_{name}.png").convert("RGB"), label) for name, label in PANELS]
    height = round(WIDTH * images[0][0].height / images[0][0].width)
    rows = -(-len(images) // COLUMNS)
    sheet = Image.new("RGB", (COLUMNS * WIDTH, rows * height), (20, 20, 20))
    draw, label_font = ImageDraw.Draw(sheet), font(20)
    for i, (image, label) in enumerate(images):
        x, y = i % COLUMNS * WIDTH, i // COLUMNS * height
        sheet.paste(image.resize((WIDTH, height), Image.LANCZOS), (x, y))
        draw.rectangle((x, y, x + WIDTH - 1, y + height - 1), outline=(20, 20, 20), width=2)
        draw.text((x + 10, y + 8), label, font=label_font, fill=(255, 255, 255), stroke_width=3, stroke_fill=(0, 0, 0))
    Path(target).parent.mkdir(parents=True, exist_ok=True)
    sheet.save(target, optimize=True)


if __name__ == "__main__":
    main(*sys.argv[1:3])
