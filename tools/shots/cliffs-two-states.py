#!/usr/bin/env python3
"""Capture the cliffs-two-states scene in one launch and assemble a contact sheet.

    python3 tools/shots/cliffs-two-states.py
    python3 tools/shots/cliffs-two-states.py --out /tmp/ariel/sheet.png --shots-dir /tmp/ariel/cliff-shots
    python3 tools/shots/cliffs-two-states.py --skip-capture
    python3 tools/shots/cliffs-two-states.py --only end,west --tuning CliffRamp=2

The scene is tools/shots/scenarios/cliffs-two-states.json. Every pose is taken from one
`mise run shot --shots` launch, which captures on a private Xvfb display (no Wayland
throttling) and builds only when the code changed. The sheet goes to docs/screenshots/cliffs-two-states.png
unless --out says otherwise; the individual captures go to /tmp/ariel/cliffs-two-states.
"""
import argparse
import json
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps

REPO = Path(__file__).resolve().parents[2]
SCENE = REPO / "tools/shots/scenarios/cliffs-two-states.json"
DEFAULT_OUT = REPO / "docs/screenshots/cliffs-two-states.png"
DEFAULT_SHOTS = Path("/tmp/ariel/cliffs-two-states")

# Yaw turns counter-clockwise from north: 0 looks north, 90 west, 180 south, 270 east.
# Poses for one --shots launch. A missing field keeps the previous pose's value, so every
# entry names its full camera, and a debug view is reset with "terrainDebug": "normal".
# Coordinates are the scene's authored ones: the land run is 10,7-10,10 (wall on its east
# side, bare slope to the west and at both ends), the coastal run 10,14-10,17.
SHOTS = [
    ({"name": "end", "focusTile": [10, 7], "yaw": 135, "pitch": 20, "distance": 3.2},
     "Land run: north end (10,7) from the north-west"),
    ({"name": "end-low", "focusTile": [10, 7], "yaw": 135, "pitch": 6, "distance": 2.8},
     "Same end, camera at ground level"),
    ({"name": "corner", "focusTile": [10, 7], "yaw": 135, "pitch": 18, "distance": 1.4},
     "North end corner close-up"),
    ({"name": "corner-b", "focusTile": [10, 7], "yaw": 200, "pitch": 18, "distance": 1.4},
     "North end corner close-up, turned"),
    ({"name": "corner-c", "focusTile": [10, 7], "yaw": 60, "pitch": 16, "distance": 1.2},
     "North end of the wall close-up, from the north-east"),
    ({"name": "corner-plan", "focusTile": [10, 7], "yaw": 0, "pitch": 89, "distance": 2.5,
      "terrainDebug": "bands"},
     "North end plan: height"),
    ({"name": "corner-slope", "focusTile": [10, 7], "yaw": 0, "pitch": 89, "distance": 2.5,
      "terrainDebug": "slope"},
     "North end plan: slope"),
    ({"name": "face", "focusTile": [11, 9], "yaw": 90, "pitch": 12, "distance": 2.2},
     "Land wall and hill (10,9) from the low east side"),
    ({"name": "west", "focusTile": [10, 9], "yaw": 270, "pitch": 15, "distance": 3.5},
     "Bare west side from the west: should slope, no wall"),
    ({"name": "north-close", "focusTile": [11, 7], "yaw": 90, "pitch": 14, "distance": 2.0},
     "North end (10,7) close, from the low east side"),
    ({"name": "south-close", "focusTile": [11, 11], "yaw": 45, "pitch": 14, "distance": 2.0},
     "South end (10,10) close, from the south-east"),
    ({"name": "south", "focusTile": [10, 12], "yaw": 0, "pitch": 15, "distance": 3.0},
     "South end (10,10) from the south"),
    ({"name": "peak", "focusTile": [10, 16], "yaw": 135, "pitch": 22, "distance": 4.5},
     "Land-to-ocean: peak at 10,16 and the bay"),
    ({"name": "height", "focusTile": [10, 9], "yaw": 0, "pitch": 89, "distance": 5,
      "terrainDebug": "bands"},
     "Land run height map (purple raised, blue low)"),
    ({"name": "slope", "focusTile": [10, 9], "yaw": 0, "pitch": 89, "distance": 5,
      "terrainDebug": "slope"},
     "Land run slope (green flat, red steep)"),
]

PANEL_W, PANEL_H = 640, 360
GAP = 8
HEADER = 44
COLUMNS = 2


def font(size):
    for path in ("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
                 "/usr/share/fonts/TTF/DejaVuSans-Bold.ttf"):
        if Path(path).exists():
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


def poses_with_resets(shots):
    """Every pose resets the debug view unless it names one, so a view never leaks forward."""
    poses = []
    for pose, _label in shots:
        pose = dict(pose)
        pose.setdefault("terrainDebug", "normal")
        poses.append(pose)
    return poses


def capture(shots, shots_dir, tuning):
    shots_dir.mkdir(parents=True, exist_ok=True)
    pose_file = shots_dir / "poses.json"
    pose_file.write_text(json.dumps(poses_with_resets(shots), indent=1))
    cmd = ["mise", "run", "shot", "--", "--scenario", str(SCENE), "--clean", "--frames", "3",
           "--out-dir", str(shots_dir), "--shots", str(pose_file)]
    if tuning:
        cmd += ["--tuning", tuning]
    print(f"[cliffs] capturing {len(shots)} poses")
    result = subprocess.run(cmd, cwd=REPO, capture_output=True, text=True)
    if result.returncode != 0:
        print(result.stdout[-4000:])
        print(result.stderr[-4000:], file=sys.stderr)
        raise SystemExit(result.returncode)


def assemble(shots, out, shots_dir, title):
    title_font, label_font = font(22), font(18)
    rows = (len(shots) + COLUMNS - 1) // COLUMNS
    width = GAP * (COLUMNS + 1) + PANEL_W * COLUMNS
    height = HEADER + GAP * (rows + 1) + PANEL_H * rows
    sheet = Image.new("RGB", (width, height), (24, 24, 28))
    draw = ImageDraw.Draw(sheet)
    draw.text((GAP, 10), title, font=title_font, fill=(240, 240, 240))
    for index, (pose, label) in enumerate(shots):
        image = ImageOps.fit(Image.open(shots_dir / f"{pose['name']}.png").convert("RGB"),
                             (PANEL_W, PANEL_H), method=Image.Resampling.LANCZOS)
        x = GAP + (index % COLUMNS) * (PANEL_W + GAP)
        y = HEADER + GAP + (index // COLUMNS) * (PANEL_H + GAP)
        sheet.paste(image, (x, y))
        draw.rectangle((x, y, x + PANEL_W - 1, y + PANEL_H - 1), outline=(10, 10, 12), width=2)
        draw.text((x + 10, y + 9), label, font=label_font, fill=(255, 255, 255),
                  stroke_width=3, stroke_fill=(0, 0, 0))
    out.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out, optimize=True)
    print(f"[cliffs] wrote {out}")


def main():
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT,
                        help=f"assembled sheet (default: {DEFAULT_OUT})")
    parser.add_argument("--shots-dir", type=Path, default=DEFAULT_SHOTS,
                        help=f"individual captures (default: {DEFAULT_SHOTS})")
    parser.add_argument("--skip-capture", action="store_true",
                        help="assemble existing captures without launching the game")
    parser.add_argument("--only", help="comma-separated pose names to capture and assemble")
    parser.add_argument("--tuning", help="TerrainTuning overrides, e.g. CliffRamp=2,CliffRise=0.25")
    parser.add_argument("--title", default="Two cliff states", help="sheet title")
    args = parser.parse_args()

    shots = SHOTS
    if args.only:
        names = set(args.only.split(","))
        shots = [s for s in SHOTS if s[0]["name"] in names]
    if not args.skip_capture:
        capture(shots, args.shots_dir, args.tuning)
    assemble(shots, args.out, args.shots_dir, args.title)


if __name__ == "__main__":
    main()
