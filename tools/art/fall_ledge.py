#!/usr/bin/env python3
# /// script
# requires-python = ">=3.11"
# dependencies = ["numpy==2.4.6", "Pillow==12.3.0"]
# ///
"""Build the waterfall ledge rock into src/Ariel.Rules/World/art/scatter/fall_ledges/Ledge_1.glb.

Run: mise run art:fall-ledge

A waterfall pours over this rock. It is a rounded, water-worn slab that sits on the brink of a cliff and juts out past the
cliff's rock columns, on a blocky body that fills the face down to the foot, so the water slides over the slab and drops
clear of the wall. Its frame matches FallLedge in src/Ariel.Client.Core/Terrain/FallLedge.cs: origin on the brink at the
top of the slab, +X out over the drop, Z along the lip, Y up. The dimensions below are FallLedge's constants in world
units at scale 1; keep the two in step, because the river mesher lays the water over the slab's top by those numbers.
Colours follow the cliff rocks (Rock_1 to Rock_5), whose texture averages a dark brown-grey; the top is darker, worn
and wet.
"""
from __future__ import annotations

import math
import random
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from improvements.kit import REPO_ROOT, SCALE, Part, srgb, write  # noqa: E402

OUT = REPO_ROOT / "src/Ariel.Rules/World/art/scatter/fall_ledges"

# FallLedge's constants, world units at scale 1.
WIDTH = 0.36
BACK = 0.10
NOSE = 0.14
NOSE_DROP = 0.03
HEIGHT = 0.24

WET = [srgb(0x34363A), srgb(0x3A3C3E)]
ROCK = [srgb(0x4D483F), srgb(0x57514A), srgb(0x45413A)]
UNDER = srgb(0x2E2B28)


def top_y(x: float) -> float:
    """FallLedge.TopAt at scale 1: level over the plateau, rounding down to the nose."""
    t = min(max(x, -BACK), NOSE) / NOSE
    return -NOSE_DROP * t * t if t > 0 else 0.0


def profile() -> list[tuple[float, float]]:
    """The cross-section in the X/Y plane, counter-clockwise seen from +Z: the slab's top from back to nose, the rounded
    nose and its underside, then the body down the face to the foot and back up the buried back."""
    top = [(x, top_y(x)) for x in (-BACK, -0.05, 0.0, 0.05, 0.09, 0.12, NOSE)]
    nose = [(NOSE + 0.008, -0.042), (NOSE - 0.006, -0.054), (0.11, -0.062)]
    body = [(0.075, -0.07), (0.06, -0.11), (0.068, -0.16), (0.055, -0.205), (0.06, -HEIGHT)]
    back = [(-BACK, -HEIGHT), (-BACK, -0.12)]
    # Written clockwise (along the top, down the front, back along the foot, up the back); reversed to run counter-clockwise.
    return list(reversed(top + nose + body + back))


def ledge(seed: int) -> Part:
    rng = random.Random(seed)
    part = Part("fall_ledge")
    base = profile()
    n = len(base)
    centre = (sum(p[0] for p in base) / n, sum(p[1] for p in base) / n)
    half = WIDTH / 2
    # Slices along the lip. The two outer ones draw in, so the slab's sides are rounded rather than cut square.
    zs = [-half, -half + 0.03, -0.09, -0.03, 0.03, 0.09, half - 0.03, half]
    shrink = [0.72, 0.93, 1, 1, 1, 1, 0.93, 0.72]
    slices = []
    for z, k in zip(zs, shrink):
        ring = []
        for x, y in base:
            on_top = y > -0.035 and x < NOSE + 0.001
            jx = 0 if on_top else rng.uniform(-0.006, 0.006)
            jy = rng.uniform(-0.002, 0.002) if on_top else rng.uniform(-0.006, 0.006)
            # Drawing in pulls towards the profile's centre, but keeps the top level so the water still lies on it.
            px = centre[0] + (x + jx - centre[0]) * k
            py = (y + jy) if on_top else centre[1] + (y + jy - centre[1]) * k
            ring.append(np.array([px, py, z]) / SCALE)
        slices.append(ring)
    for s in range(len(slices) - 1):
        a, b = slices[s], slices[s + 1]
        for i in range(n):
            j = (i + 1) % n
            (x0, y0), (x1, y1) = base[i], base[j]
            # Outward normal of this profile edge for a counter-clockwise loop.
            out = np.array([y1 - y0, -(x1 - x0), 0.0])
            colour = colour_for(out, rng)
            quad(part, a[i], a[j], b[j], b[i], out, colour)
    for ring, sign in ((slices[0], -1.0), (slices[-1], 1.0)):
        mid = sum(ring) / n
        for i in range(n):
            tri(part, mid, ring[i], ring[(i + 1) % n], np.array([0, 0, sign]), rng.choice(ROCK))
    return part


def colour_for(out: np.ndarray, rng: random.Random):
    ny = out[1] / (np.linalg.norm(out) or 1)
    if ny > 0.6:
        return rng.choice(WET)
    if ny < -0.6:
        return UNDER
    return rng.choice(ROCK)


def tri(part: Part, a, b, c, out, colour):
    """A triangle wound so its normal agrees with the outward direction."""
    if np.dot(np.cross(b - a, c - a), out) < 0:
        b, c = c, b
    part.tri(a, b, c, colour)


def quad(part: Part, a, b, c, d, out, colour):
    tri(part, a, b, c, out, colour)
    tri(part, a, c, d, out, colour)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    path = OUT / "Ledge_1.glb"
    size, triangles = write(path, [ledge(7)], "centre")
    print(f"{path.relative_to(REPO_ROOT)}: {triangles} triangles, {size} bytes")


if __name__ == "__main__":
    main()
