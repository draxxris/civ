"""Shared pieces for the resource (bonus) models in bonus_plants, bonus_animals, bonus_minerals and bonus_sea.

A resource model stands on a spot off the tile centre (BonusSpot in src/Ariel.Client.Core/Terrain/BonusSpot.cs), so a
unit token, a city or an improvement model can share the tile. Keep every model compact: all geometry within about
0.25 world units of the origin (at() offsets up to ~0.16, piece radius up to ~0.045 authored). Each top-level part is
seated on the ground under its own origin, so split a cluster into several small parts and it follows hills and
mountain flanks. Name animals and other things that should not leave a worn earth patch "nodecal...". Sea resources
sit at sea level (y = 0): parts reach a little below the translucent surface and stand above it.
"""
from __future__ import annotations

import math

import numpy as np

from .kit import FOOT, Part, beam, cylinder, lumpy_mound, srgb  # noqa: F401
from .water import solid

RIPPLE = srgb(0xCFE3E6)
RIPPLE_DARK = srgb(0xA9C8CF)
SPRAY = srgb(0xDDEFF2)


def scaled(part: Part, f: float) -> Part:
    """Scale a finished part's geometry about its origin."""
    part.tris = [(a * f, b * f, c * f, col) for a, b, c, col in part.tris]
    return part


def ellipsoid(cx, cy, cz, rx, ry, rz, n=8, rings=3, phase=0.0):
    """Points on a faceted ellipsoid, for solid()."""
    pts = [(cx, cy - ry, cz), (cx, cy + ry, cz)]
    for k in range(rings):
        lat = -math.pi / 2 + (k + 1) * math.pi / (rings + 1)
        for i in range(n):
            a = phase + (i + 0.5 * k) * math.tau / n
            pts.append((cx + math.cos(lat) * math.cos(a) * rx, cy + math.sin(lat) * ry,
                        cz + math.cos(lat) * math.sin(a) * rz))
    return pts


def blobby(part, cx, cy, cz, rx, ry, rz, colour, top=None, n=8, rings=3, phase=0.0, top_cut=0.8):
    """A faceted ellipsoid solid."""
    solid(part, ellipsoid(cx, cy, cz, rx, ry, rz, n, rings, phase), colour, top, top_cut=top_cut)


def spindle(part, a, b, r, colour, top=None, n=6, bulge=0.4):
    """A body tapering to both ends from point a to point b, thickest at fraction bulge along it (fish, pods)."""
    a, b = np.asarray(a, dtype=float), np.asarray(b, dtype=float)
    axis = b - a
    unit = axis / np.linalg.norm(axis)
    side = np.cross(unit, (0, 1, 0) if abs(unit[1]) < 0.9 else (1, 0, 0))
    side /= np.linalg.norm(side)
    up = np.cross(side, unit)
    pts = [tuple(a), tuple(b)]
    for t, s in ((bulge * 0.5, 0.7), (bulge, 1.0), ((1 + bulge) / 2, 0.75)):
        centre = a + axis * t
        for i in range(n):
            ang = i * math.tau / n
            pts.append(tuple(centre + (side * math.cos(ang) + up * math.sin(ang)) * r * s))
    solid(part, pts, colour, top)


def crystal(part, base, direction, r, length, colour, tip=None, n=6):
    """A pointed crystal prism growing from base along direction."""
    base = np.asarray(base, dtype=float)
    d = np.asarray(direction, dtype=float)
    d /= np.linalg.norm(d)
    side = np.cross(d, (0, 1, 0) if abs(d[1]) < 0.9 else (1, 0, 0))
    side /= np.linalg.norm(side)
    up = np.cross(side, d)
    ring = [side * math.cos(i * math.tau / n) + up * math.sin(i * math.tau / n) for i in range(n)]
    pts = [tuple(base - d * 0.01 + c * r) for c in ring]
    pts += [tuple(base + d * length * 0.75 + c * r) for c in ring]
    pts.append(tuple(base + d * length))
    solid(part, pts, colour, tip, top_cut=0.9 if tip else 0.8)


def leg(part, x, y, z, colour, t=0.008, hoof=None):
    """A straight leg from the body (x, y, z) down a little below the seat, with an optional hoof."""
    beam(part, (x, y, z), (x, -0.006, z), t, colour)
    if hoof:
        beam(part, (x, 0.008, z), (x, -0.006, z), t * 1.15, hoof)


def ripples(name, rings, rng=None):
    """Flat pale rings on the water surface; rings is a list of (x, z, radius). Tilts with the (flat) sea."""
    p = Part(name)
    for k, (x, z, r) in enumerate(rings):
        n = 12
        w = 0.0022 + 0.001 * (k % 2)
        colour = RIPPLE if k % 2 == 0 else RIPPLE_DARK
        for i in range(n):
            a0, a1 = i * math.tau / n, (i + 1) * math.tau / n
            if rng is not None and rng.random() < 0.18:
                continue  # broken rings read as moving water
            pts = [(x + math.cos(a) * rr, y, z + math.sin(a) * rr)
                   for a in (a0, a1) for rr in (r - w, r + w) for y in (-0.002, 0.003)]
            solid(p, pts, colour)
    return p


def puffs(part, spots, colour, rng, n=5):
    """Faceted round puffs (steam, smoke, spray, cotton bolls); spots are (x, y, z, r)."""
    for x, y, z, r in spots:
        blobby(part, x, y, z, r, r * 0.85, r, colour, n=n, rings=2, phase=rng.random())
