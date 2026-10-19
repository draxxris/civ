#!/usr/bin/env python3
"""Build the procedural reed clumps of the swamp scatter set into src/Mods/World/art/scatter/swamp/Reeds_<n>.glb.

Run: python3 tools/art/reeds.py

Each clump is a tuft of thin, tapered, leaning blades (narrow three-sided spikes, so they show from every side) in
olive and straw greens, a few with a brown cattail head. Sizes are authored before the kit's SCALE, so a written clump
stands about 0.1 to 0.15 tall, under the swamp's dead trees (0.4). Geometry and colours follow improvements/kit.py.
"""
from __future__ import annotations

import math
import random
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from improvements.kit import REPO_ROOT, Part, srgb, write  # noqa: E402

OUT = REPO_ROOT / "src/Mods/World/art/scatter/swamp"
BLADES = [srgb(0x5F7434), srgb(0x6E8240), srgb(0x7D8A48), srgb(0x8F8E52), srgb(0xA59A5E)]
CATTAIL = srgb(0x5A3A22)
STALK = srgb(0x6B7A3C)


def spike(part: Part, base, tip, width, colour, rng):
    """A thin three-sided spike from a triangle around base to tip."""
    turn = rng.uniform(0, math.tau)
    corners = [(base[0] + width * math.cos(turn + k * math.tau / 3), base[1],
                base[2] + width * math.sin(turn + k * math.tau / 3)) for k in range(3)]
    part.hull(corners + [tip], [[0, 1, 2], [0, 1, 3], [1, 2, 3], [2, 0, 3]], colour)


def cattail(part: Part, base, height, lean, rng):
    """A straight stalk with a brown seed head below its tip."""
    dx, dz = lean
    top = (base[0] + dx, height, base[2] + dz)
    spike(part, base, top, 0.0018, STALK, rng)
    f0, f1 = 0.62, 0.86
    head0 = (base[0] + dx * f0, height * f0, base[2] + dz * f0)
    head1 = (base[0] + dx * f1, height * f1, base[2] + dz * f1)
    n = 5
    ring = lambda c: [(c[0] + 0.0045 * math.cos(k * math.tau / n), c[1], c[2] + 0.0045 * math.sin(k * math.tau / n))
                      for k in range(n)]
    pts = ring(head0) + ring(head1)
    faces = [list(range(n)), list(range(n, 2 * n))] + [[k, (k + 1) % n, n + (k + 1) % n, n + k] for k in range(n)]
    part.hull(pts, faces, CATTAIL)


def clump(seed: int, blades: int, radius: float, height: float, cattails: int) -> Part:
    rng = random.Random(seed)
    part = Part("reeds")
    for _ in range(blades):
        r, a = radius * math.sqrt(rng.random()), rng.uniform(0, math.tau)
        base = (r * math.cos(a), -0.004, r * math.sin(a))
        h = height * rng.uniform(0.55, 1.0)
        # Blades lean outward from the clump's middle, the tall ones most.
        lean = rng.uniform(0.15, 0.45) * h
        tip = (base[0] + lean * math.cos(a) + rng.uniform(-0.006, 0.006), h,
               base[2] + lean * math.sin(a) + rng.uniform(-0.006, 0.006))
        spike(part, base, tip, rng.uniform(0.0025, 0.004), rng.choice(BLADES), rng)
    for _ in range(cattails):
        r, a = radius * 0.6 * math.sqrt(rng.random()), rng.uniform(0, math.tau)
        cattail(part, (r * math.cos(a), -0.004, r * math.sin(a)), height * rng.uniform(1.0, 1.2),
                (0.1 * height * math.cos(a), 0.1 * height * math.sin(a)), rng)
    return part


CLUMPS = {
    "Reeds_1": dict(seed=11, blades=22, radius=0.022, height=0.065, cattails=2),
    "Reeds_2": dict(seed=23, blades=16, radius=0.016, height=0.055, cattails=1),
    "Reeds_3": dict(seed=37, blades=30, radius=0.03, height=0.07, cattails=3),
    "Reeds_4": dict(seed=41, blades=12, radius=0.012, height=0.05, cattails=0),
    "Reeds_5": dict(seed=53, blades=26, radius=0.026, height=0.06, cattails=2),
    "Reeds_6": dict(seed=67, blades=18, radius=0.02, height=0.075, cattails=1),
}


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for name, spec in CLUMPS.items():
        size, triangles = write(OUT / f"{name}.glb", [clump(**spec)], "centre")
        print(f"{(OUT / name).relative_to(REPO_ROOT)}.glb: {triangles} triangles, {size} bytes")


if __name__ == "__main__":
    main()
