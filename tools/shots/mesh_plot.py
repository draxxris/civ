#!/usr/bin/env python3
"""Draw the meshes the game builds for one region of a capture scenario, seen from straight above, as a PNG.

    mise run shot:mesh -- tools/shots/scenarios/waterfalls.json --focus 9,9
    mise run shot:mesh -- tools/shots/scenarios/waterfalls.json --focus 9,9 --fall --radius 0.4 --layers river,falls,courses

Use it when a capture shows a seam, a kink or a piece of water in the wrong place: the textured render hides the shape
of the triangles, and this shows it. tools/Ariel.MeshPlot builds the world exactly as `mise run shot -- --scenario`
does (a scene-sized world, the same seed) and runs the same meshers, so the triangles are the ones on screen; this
script draws them. North (smaller z) is up, as the capture camera sees it at yaw 0.

Layers, bottom to top (--layers picks some, in any order):

- terrain: the ground lattice, shaded by height (dark low, light high), with its triangle edges.
- tiles: hex outlines, each labelled with its column,row in the scenario's own coordinates and its height tier.
- river: the river strips. Blue is water (|across| <= 1), tan is the wet bank; the fainter a triangle, the more it
  fades out (fade, Info.x).
- falls: the waterfall water. Red is the lip (the tongue over the ledge), white the falling sheet, magenta the veil,
  orange the plunge pool; fainter is more faded.
- courses: each river's path, a yellow line with a dot per sample. A course that leaves a fall (along the cliff foot,
  or out to sea) is orange; one that feeds a fall is cyan.
- lips: each fall's brink as a green line, with an arrow the way it pours.
"""
from __future__ import annotations

import argparse
import json
import math
import os
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
# Width of the key panel beside the plot, in pixels.
PANEL = 300
LAYERS = ["terrain", "tiles", "river", "falls", "courses", "lips"]
FALL_PARTS = {0: ((235, 60, 60), "lip"), 1: ((250, 250, 250), "sheet"), 2: ((255, 150, 30), "pool"), 3: ((220, 60, 220), "veil")}


def main() -> int:
    here = Path(os.environ.get("MISE_ORIGINAL_CWD", os.getcwd()))
    p = argparse.ArgumentParser(description=__doc__.split("\n\n")[0], formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("scenario", help="capture scenario JSON (tools/shots/scenarios)")
    p.add_argument("--focus", help="COL,ROW at the centre, in the scenario's coordinates (default: the scenario's focus)")
    p.add_argument("--fall", action="store_true", help="centre on the brink of the waterfall nearest the focus tile")
    p.add_argument("--radius", type=float, default=1.5, help="half the region's width in tiles (default 1.5)")
    p.add_argument("--seed", type=int, default=1, help="game seed, as mise run shot -- --seed (default 1)")
    p.add_argument("--layers", default=",".join(LAYERS), help=f"comma list of {', '.join(LAYERS)} (default all)")
    p.add_argument("--size", type=int, default=1000, help="plot width and height in pixels, beside a key panel (default 1000)")
    p.add_argument("--out", help="PNG to write (default /tmp/ariel/mesh/<scenario>-<focus>.png)")
    a = p.parse_args()

    layers = [l.strip() for l in a.layers.split(",") if l.strip()]
    unknown = [l for l in layers if l not in LAYERS]
    if unknown:
        p.error(f"unknown layer(s): {', '.join(unknown)}")
    scenario = (here / a.scenario).resolve()
    stem = scenario.stem + (f"-{a.focus.replace(',', '-')}" if a.focus else "") + ("-fall" if a.fall else "")
    out = (here / a.out).resolve() if a.out else Path("/tmp/ariel/mesh") / f"{stem}.png"
    out.parent.mkdir(parents=True, exist_ok=True)
    dump = out.with_suffix(".json")

    command = ["dotnet", "run", "--project", str(ROOT / "tools/Ariel.MeshPlot"), "--", str(scenario), "--out", str(dump),
               "--radius", str(a.radius), "--seed", str(a.seed)]
    if a.focus:
        command += ["--focus", a.focus]
    if a.fall:
        command.append("--fall")
    if subprocess.run(command, cwd=ROOT).returncode != 0:
        return 1
    draw(json.loads(dump.read_text()), layers, a.size, out)
    print(out)
    return 0


def draw(doc: dict, layers: list[str], size: int, out: Path) -> None:
    x0, z0, x1, z1 = doc["region"]
    scale = size / max(x1 - x0, z1 - z0)

    def at(x: float, z: float) -> tuple[float, float]:
        return ((x - x0) * scale, (z - z0) * scale)

    image = Image.new("RGBA", (size, size), (34, 36, 40, 255))
    font = load_font(max(11, size // 70))

    def triangles(mesh: dict):
        pos, attr, stride, idx = mesh["positions"], mesh["attributes"], mesh["stride"], mesh["indices"]
        for t in range(0, len(idx), 3):
            corners = idx[t:t + 3]
            yield ([at(pos[3 * v], pos[3 * v + 2]) for v in corners], [pos[3 * v + 1] for v in corners],
                   [attr[stride * v:stride * v + stride] for v in corners])

    def layer() -> tuple[Image.Image, ImageDraw.ImageDraw]:
        sheet = Image.new("RGBA", image.size, (0, 0, 0, 0))
        return sheet, ImageDraw.Draw(sheet, "RGBA")

    for name in [l for l in LAYERS if l in layers]:
        sheet, d = layer()
        if name == "terrain":
            ys = doc["terrain"]["positions"][1::3] or [0.0]
            lo, hi = min(ys), max(ys)
            for corners, heights, _ in triangles(doc["terrain"]):
                share = (sum(heights) / 3 - lo) / (hi - lo) if hi > lo else 0.5
                grey = int(60 + 130 * share)
                d.polygon(corners, fill=(grey, int(grey * 1.05), grey - 10, 255), outline=(20, 20, 20, 120))
        elif name == "tiles":
            for tile in doc["tiles"]:
                cx, cz = tile["x"], tile["z"]
                hexagon = [at(cx + math.cos(math.radians(60 * k - 90)), cz + math.sin(math.radians(60 * k - 90))) for k in range(7)]
                d.line(hexagon, fill=(255, 255, 255, 90), width=1)
                d.text(at(cx, cz), f"{tile['col']},{tile['row']}  h{tile['height']}", fill=(255, 255, 255, 200), font=font, anchor="mm")
        elif name == "river":
            for corners, _, attrs in triangles(doc["river"]):
                bank = max(abs(at_[0]) for at_ in attrs) > 1.01
                fade = sum(at_[1] for at_ in attrs) / 3
                colour = (215, 190, 140) if bank else (50, 90, 230)
                d.polygon(corners, fill=colour + (int(50 + 205 * fade),), outline=(0, 0, 0, 110))
        elif name == "falls":
            # Pool under the sheet and veil, as the game draws them over it, but the lip first.
            for part in (0, 2, 1, 3):
                colour = FALL_PARTS[part][0]
                for corners, _, attrs in triangles(doc["falls"]):
                    if round(attrs[0][0]) != part:
                        continue
                    fade = sum(at_[2] for at_ in attrs) / 3
                    d.polygon(corners, fill=colour + (int(40 + 120 * fade),), outline=colour + (220,))
        elif name == "courses":
            for course in doc["courses"]:
                colour = (255, 150, 0) if course["falls"] else (0, 230, 255) if course["feeds"] else (255, 230, 0)
                points = [at(x, z) for x, z in course["points"]]
                d.line(points, fill=colour + (255,), width=2)
                for px, pz in points:
                    d.ellipse([px - 3, pz - 3, px + 3, pz + 3], fill=colour + (255,))
        elif name == "lips":
            for lip in doc["lips"]:
                d.line([at(*lip["brink"][0]), at(*lip["brink"][1])], fill=(0, 255, 90, 255), width=4)
                bx = (lip["brink"][0][0] + lip["brink"][1][0]) / 2
                bz = (lip["brink"][0][1] + lip["brink"][1][1]) / 2
                tip = (bx + lip["dx"] * 0.15, bz + lip["dz"] * 0.15)
                d.line([at(bx, bz), at(*tip)], fill=(0, 255, 90, 255), width=2)
                ax, az = -lip["dz"] * 0.03, lip["dx"] * 0.03
                back = (tip[0] - lip["dx"] * 0.04, tip[1] - lip["dz"] * 0.04)
                d.polygon([at(*tip), at(back[0] + ax, back[1] + az), at(back[0] - ax, back[1] - az)], fill=(0, 255, 90, 255))
        image = Image.alpha_composite(image, sheet)

    framed = Image.new("RGBA", (size + PANEL, size), (20, 21, 24, 255))
    framed.paste(image, (0, 0))
    legend(framed, size, doc, layers, font, scale)
    framed.convert("RGB").save(out)


def legend(image: Image.Image, left: int, doc: dict, layers: list[str], font, scale: float) -> None:
    """The key, in a panel to the right of the plot so it hides nothing."""
    lines = [(None, doc["scenario"]), (None, f"seed {doc['seed']}, north up"), (None, f"{scale:.0f} px per unit"),
             (None, "(a hex is 1.73 units wide)"), (None, "")]
    if "river" in layers:
        lines += [((50, 90, 230), "river water"), ((215, 190, 140), "river bank (fainter = faded)")]
    if "falls" in layers:
        lines += [(c, f"fall {n}") for c, n in FALL_PARTS.values()]
    if "courses" in layers:
        lines += [((255, 230, 0), "river course"), ((0, 230, 255), "course feeding a fall"), ((255, 150, 0), "course leaving a fall")]
    if "lips" in layers:
        lines += [((0, 255, 90), "brink, arrow the way it pours")]
    d = ImageDraw.Draw(image, "RGBA")
    step = font.size + 6 if hasattr(font, "size") else 18
    for i, (colour, text) in enumerate(lines):
        y = 12 + step * i
        x = left + 14
        if colour:
            d.rectangle([x, y + 2, x + 14, y + step - 6], fill=colour + (255,))
            x += 22
        d.text((x, y), text, fill=(255, 255, 255, 255), font=font)


def load_font(size: int):
    for path in ("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/TTF/DejaVuSans.ttf"):
        if Path(path).exists():
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


if __name__ == "__main__":
    sys.exit(main())
