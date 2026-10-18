"""Build docs/screenshots/city-stages.png from a cities.json capture (see tools/shots/README.md, "City buildings").

    python3 tools/shots/city_stages.py <shot dir> docs/screenshots/city-stages.png
"""
import sys

from PIL import Image, ImageDraw, ImageFont

ERAS = [("ancient", "Ancient-Medieval:\nhuts, dirt"), ("renaissance", "Renaissance:\nVictorian, dirt"),
        ("industrial", "Industrial:\nVictorian,\ncobblestone"), ("modern", "Modern-Future:\ntowers, concrete")]
SIZES = [3, 8, 13, 20]
TILE_W, TILE_H, LABEL_W, HEAD_H = 420, 276, 200, 40


def main(shots, out):
    try:
        font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 18)
    except OSError:
        font = ImageFont.load_default()
    sheet = Image.new("RGB", (LABEL_W + TILE_W * len(SIZES), HEAD_H + TILE_H * len(ERAS)), (30, 30, 34))
    draw = ImageDraw.Draw(sheet)
    for c, size in enumerate(SIZES):
        draw.text((LABEL_W + c * TILE_W + 10, 10), f"Size {size} (walls)", fill="white", font=font)
    for r, (era, label) in enumerate(ERAS):
        draw.multiline_text((10, HEAD_H + r * TILE_H + TILE_H // 2 - 30), label, fill="white", font=font)
        for c, size in enumerate(SIZES):
            shot = Image.open(f"{shots}/{era}_{size}_walls.png")
            w, h = shot.size
            crop = shot.crop((w // 2 - 300, h // 2 - 197, w // 2 + 300, h // 2 + 197)).resize((TILE_W - 4, TILE_H - 4))
            sheet.paste(crop, (LABEL_W + c * TILE_W + 2, HEAD_H + r * TILE_H + 2))
    sheet.save(out)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
