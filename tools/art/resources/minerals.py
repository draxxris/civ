"""Mineral resources: ore outcrops, stone, crystals, salt, oil seeps, the thermal vent and methane.

Conventions are in kit.py. Most minerals may also lie on hills and mountains, so an outcrop is a few separate rocks,
each its own part with a deep foot (DEEP): each is seated on the ground under its own origin and must show no gap on
a steep flank. Every ore says what it is by colour: the rock it sits in and the ore showing in veins and lumps.
"""
import math
import random

from mathutils import Vector

from .kit import DEEP, FOOT, Paint, Part, Strata, at, hexc, nugget, rock

RUST = hexc(0xA4462A)
RUST_DARK = hexc(0x86361F)
RUST_LIGHT = hexc(0xC0623A)
HEMATITE = hexc(0x46454C)
HEMATITE_LIGHT = hexc(0x6C6B75)


def layers(rng, depth, widths, jitter=0.4):
    """Uneven layer boundaries from -depth to depth, the layers taking widths in turn."""
    out, s, k = [], -depth, 0
    while s < depth:
        s += widths[k % len(widths)] * rng.uniform(1 - jitter, 1 + jitter)
        out.append(s)
        k += 1
    return out


def banded(name, rng, radii, colours, widths, tilt=0.3, points=14, merge=12.0, chamfer=0.05):
    """A boulder of layered rock, the layers dipping at tilt (radians) toward a random side."""
    p = Part(name)
    a = rng.random() * math.tau
    normal = Vector((math.sin(tilt) * math.cos(a), math.sin(tilt) * math.sin(a), math.cos(tilt)))
    centre = (0.0, 0.0, radii[2] * 0.6)
    offsets = layers(rng, max(radii) * 1.2, widths)
    surface = rock(p, centre, radii, Strata(colours, centre, normal, offsets), rng, points=points, flat=0.35,
                   bottom=DEEP, merge=merge, chamfer=chamfer, strata=(normal, offsets))
    return p, surface


def chunks(name, rng, colours, count=5, spread=0.045, size=0.018):
    """Broken lumps of ore at the foot of an outcrop."""
    p = Part(name)
    for i in range(count):
        a, r = rng.random() * math.tau, spread * math.sqrt(rng.random())
        s = size * rng.uniform(0.7, 1.25)
        rock(p, (math.cos(a) * r, math.sin(a) * r, s * 0.35), (s, s * 0.9, s * 0.75), colours[i % len(colours)],
             rng, points=9, flat=0.2, bottom=FOOT / 2)
    return p


def iron():
    """Banded iron: boulders striped rust red and dark metal grey, with lumps of ore at their foot."""
    rng = random.Random(305)
    red = Paint([RUST, RUST_DARK, RUST, RUST_LIGHT], top=RUST_LIGHT, top_cut=0.8, rng=rng)
    grey = Paint([HEMATITE, HEMATITE, HEMATITE_LIGHT], top=HEMATITE_LIGHT, top_cut=0.8, rng=rng)
    bands, widths = [red, grey, red, grey], [0.026, 0.009, 0.018, 0.014]
    big, _ = banded("rock", rng, (0.125, 0.105, 0.13), bands, widths, tilt=0.35)
    mid, _ = banded("rock_b", rng, (0.085, 0.07, 0.08), bands, widths, tilt=0.5)
    small, _ = banded("rock_c", rng, (0.06, 0.05, 0.05), bands, widths, tilt=0.25)
    return [
        at(big, -0.05, 0.06),
        at(mid, 0.12, 0.05),
        at(small, -0.14, -0.1),
        at(chunks("lumps", rng, [grey, red, grey]), 0.06, -0.1),
    ]


MODELS = {
    "iron": iron,
}
