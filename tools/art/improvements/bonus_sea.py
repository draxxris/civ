"""Sea resources: fish, shrimp, crabs, clams, pearls and whales.

Conventions for resource models are in bonus_kit.py. Every part sits at sea level (y = 0): creatures leap or break
the surface, shells and rocks rise just above it, and pale ripple rings float on it. Bodies reach a little below
the translucent water, where they show faintly.
"""
import random

from .bonus_kit import blobby, crystal, puffs, ripples, scaled, spindle  # noqa: F401
from .kit import *  # noqa: F401,F403
from .kit import Part, at
from .water import solid, whale_back, whale_tail

FISH = srgb(0x7E9AAE)
FISH_BELLY = srgb(0xD8E2E6)
FISH_FIN = srgb(0x5A7488)
SHRIMP = srgb(0xEE8A72)
SHRIMP_DARK = srgb(0xD06A56)
CRAB = srgb(0xC8442E)
CRAB_DARK = srgb(0x9A3022)
CRAB_CLAW = srgb(0xE0A080)
SEA_ROCK = srgb(0x6A6660)
SEA_ROCK_DARK = srgb(0x55524C)
WEED = srgb(0x4A6A3A)
SAND = srgb(0xD8C8A0)
SHELL = srgb(0xE4D6BC)
SHELL_DARK = srgb(0xB8A486)
SHELL_RIB = srgb(0x9A8670)
NACRE = srgb(0xD8D0DE)
OYSTER = srgb(0x7E7466)
PEARL = srgb(0xFAF6EE)


def fish_body(name, colour=FISH, s=1.0, arc=0.5):
    """A fish leaping in an arc along +X: a spindle body, a forked tail and a dorsal fin. arc tilts it."""
    p = Part(name)
    head = (0.03 * s, 0.0, 0.0)
    tail = (-0.03 * s, 0.0, 0.0)
    spindle(p, tail, head, 0.011 * s, colour, FISH_BELLY, bulge=0.6)
    for sy in (-1, 1):
        solid(p, [(-0.026 * s, 0.0, -0.002), (-0.026 * s, 0.0, 0.002), (-0.044 * s, sy * 0.014 * s, 0.0),
                  (-0.04 * s, sy * 0.004 * s, 0.0)], FISH_FIN)
    solid(p, [(-0.006 * s, 0.008 * s, -0.001), (0.01 * s, 0.008 * s, 0.001), (-0.008 * s, 0.018 * s, 0.0),
              (0.004 * s, 0.009 * s, 0.0)], FISH_FIN)
    c, sn = math.cos(arc), math.sin(arc)
    p.tris = [tuple(np.array((c * v[0] - sn * v[1], sn * v[0] + c * v[1], v[2])) for v in (a, b, cc)) + (col,)
              for a, b, cc, col in p.tris]
    return p


def lifted(part, y):
    part.tris = [(a + (0, y, 0), b + (0, y, 0), c + (0, y, 0), col) for a, b, c, col in part.tris]
    return part


def splash(name, rng, r=0.012):
    p = Part(name)
    puffs(p, [(0.0, 0.004, 0.0, r), (0.01, 0.01, 0.004, r * 0.7), (-0.008, 0.009, -0.004, r * 0.6)],
          srgb(0xE6F2F4), rng)
    return p


def fish():
    rng = random.Random(401)
    return [
        at(ripples("ground_ripples", [(-0.06, -0.03, 0.04), (0.06, 0.04, 0.035), (-0.0, 0.09, 0.03),
                                      (-0.06, -0.03, 0.07)], rng), 0.0, 0.0),
        at(lifted(fish_body("fish_a", s=1.5, arc=0.6), 0.05), -0.06, -0.03, 0.2),
        at(lifted(fish_body("fish_b", s=1.2, arc=-0.5), 0.035), 0.07, 0.04, 2.8),
        at(lifted(fish_body("fish_c", FISH_FIN, s=1.1, arc=0.2), 0.028), 0.0, 0.1, 1.2),
        at(splash("splash", rng), 0.06, 0.04),
    ]


def shrimp_body(name, s=1.0, curl=0.9):
    """A pink shrimp curled in a C along +X: tapering segments, a fan tail and long feelers."""
    p = Part(name)
    segments = 6
    pts = []
    for k in range(segments + 1):
        a = -curl / 2 + curl * k / segments
        pts.append((math.sin(a) * 0.03 * s, (1 - math.cos(a)) * 0.03 * s, 0.0))
    for k in range(segments):
        r = 0.009 * s * (1 - 0.1 * k)
        spindle(p, pts[k], pts[k + 1], r, SHRIMP if k % 2 else SHRIMP_DARK, bulge=0.5, n=5)
    end = pts[0]
    fan = [end, (end[0] - 0.012 * s, end[1] + 0.008 * s, -0.008 * s),
           (end[0] - 0.012 * s, end[1] + 0.008 * s, 0.008 * s), (end[0] - 0.004 * s, end[1] + 0.012 * s, 0.0)]
    solid(p, fan, SHRIMP_DARK)
    head = pts[-1]
    for sz in (-1, 1):
        beam(p, head, (head[0] + 0.03 * s, head[1] + 0.03 * s, sz * 0.012 * s), 0.0016, SHRIMP_DARK)
    return p


def shrimp():
    rng = random.Random(402)
    return [
        at(ripples("ground_ripples", [(-0.05, -0.02, 0.04), (0.06, 0.05, 0.035), (0.0, -0.08, 0.03)], rng), 0.0, 0.0),
        at(lifted(shrimp_body("shrimp_a", 1.5), 0.012), -0.05, -0.02, 0.3),
        at(lifted(shrimp_body("shrimp_b", 1.2, curl=1.3), 0.024), 0.06, 0.05, 2.5),
        at(lifted(shrimp_body("shrimp_c", 1.0, curl=1.1), 0.006), 0.0, -0.08, 1.6),
        at(lifted(shrimp_body("shrimp_d", 0.9), 0.03), -0.08, 0.08, 4.0),
    ]


def sea_rock(name, rng, r=0.045, h=0.03, weed=True):
    """A rock just breaking the surface, with weed at the waterline."""
    p = Part(name)
    lumpy_mound(p, r, r * 0.85, h, [SEA_ROCK, SEA_ROCK_DARK, SEA_ROCK], rng, y0=-0.06, rings=3, n=8)
    if weed:
        for k in range(6):
            a = k * math.tau / 6 + rng.random()
            x, z = math.cos(a) * r * 0.95, math.sin(a) * r * 0.85 * 0.95
            blobby(p, x, 0.0, z, 0.009, 0.004, 0.009, WEED, n=5, rings=2)
    return p


def crab_body(name, s=1.0):
    """A red crab facing +X: a broad shell, four legs a side and two raised claws."""
    p = Part(name)
    blobby(p, 0.0, 0.012 * s, 0.0, 0.018 * s, 0.009 * s, 0.024 * s, CRAB, CRAB_DARK, n=8, rings=2, top_cut=0.95)
    for sz in (-1, 1):
        for k in range(4):
            x = -0.01 * s + k * 0.006 * s
            knee = (x - 0.004 * s, 0.018 * s, sz * 0.036 * s)
            beam(p, (x, 0.01 * s, sz * 0.018 * s), knee, 0.004 * s, CRAB_DARK)
            beam(p, knee, (x - 0.008 * s, 0.0, sz * 0.044 * s), 0.0035 * s, CRAB_DARK)
        elbow = (0.02 * s, 0.024 * s, sz * 0.026 * s)
        beam(p, (0.012 * s, 0.012 * s, sz * 0.014 * s), elbow, 0.005 * s, CRAB)
        blobby(p, 0.032 * s, 0.03 * s, sz * 0.022 * s, 0.011 * s, 0.008 * s, 0.007 * s, CRAB_CLAW, n=6, rings=2)
        beam(p, (0.016 * s, 0.018 * s, sz * 0.006 * s), (0.02 * s, 0.026 * s, sz * 0.007 * s), 0.0025 * s, DARK)
    return p


def crab():
    rng = random.Random(403)
    parts = [
        at(sea_rock("rock", rng, r=0.05, h=0.032), -0.04, -0.03),
        at(lifted(crab_body("nodecal_crab", 1.4), 0.026), -0.04, -0.03, 0.3),
        at(sea_rock("rock_b", rng, r=0.03, h=0.016, weed=False), 0.08, 0.05),
        at(lifted(crab_body("nodecal_crab_b", 1.1), 0.012), 0.08, 0.05, 2.2),
    ]
    pot = Part("pot")
    cylinder(pot, 0.016, -0.03, 0.006, THATCH_DARK, n=8, top=THATCH)
    beam(pot, (0.0, 0.006, 0.0), (0.0, 0.03, 0.0), 0.003, ROPE)
    blobby(pot, 0.0, 0.034, 0.0, 0.007, 0.007, 0.007, CLOTH_RED, n=6, rings=2)
    parts.append(at(pot, 0.02, 0.11))
    return parts


def shell(part, x, z, s, yaw, gape, outer=SHELL, inner=SHELL_DARK, ribs=SHELL_RIB, y=0.0):
    """A ribbed bivalve: two fan-shaped valves hinged at the back (-X), opened by gape radians."""
    def valve(sign):
        pts = [(0.0, 0.0, 0.0)]
        for k in range(7):
            a = -1.1 + 2.2 * k / 6
            pts.append((math.cos(a) * 0.024 * s, 0.0, math.sin(a) * 0.024 * s))
        pts += [(px * 0.8, sign * 0.006 * s, pz * 0.8) for px, _, pz in pts[1:]]
        ang = sign * gape / 2
        c, sn = math.cos(ang), math.sin(ang)
        pts = [(c * px - sn * py, sn * px + c * py, pz) for px, py, pz in pts]
        return [tuple(np.add(rot_y(p, yaw), (x, y + 0.006 * s, z))) for p in pts]
    solid(part, valve(-1), inner, outer)
    solid(part, valve(1), outer, ribs)


def clam_bed(name, rng):
    """Clams on a sandbar just under and breaking the surface."""
    p = Part(name)
    pts = []
    for k in range(9):
        a = k * math.tau / 9 + rng.random() * 0.3
        rr = 0.075 * (0.8 + 0.3 * rng.random())
        for y in (-0.03, -0.004 + 0.004 * rng.random()):
            pts.append((math.cos(a) * rr, y, math.sin(a) * rr * 0.75))
    solid(p, pts, SAND, SAND)
    for k in range(7):
        a = rng.random() * math.tau
        rr = 0.05 * rng.random()
        shell(p, math.cos(a) * rr, math.sin(a) * rr * 0.75, 0.9 + 0.4 * rng.random(), rng.random() * math.tau,
              0.25 + 0.4 * rng.random(), y=0.0)
    return p


def clam():
    rng = random.Random(404)
    big = Part("clam_big")
    shell(big, 0.0, 0.0, 2.2, 0.4, 0.55, y=-0.004)
    return [
        at(clam_bed("bed", rng), -0.02, -0.01),
        at(big, 0.1, 0.06),
        at(ripples("ground_ripples", [(-0.02, -0.01, 0.11), (0.1, 0.06, 0.06)], rng), 0.0, 0.0),
    ]


def pearl_oyster(name, s=1.0):
    """A dark oyster opened wide on a rock, its nacre lining and a pearl catching the light."""
    p = Part(name)
    shell(p, 0.0, 0.0, 1.6 * s, 0.0, 1.1, outer=OYSTER, inner=NACRE, ribs=OYSTER)
    blobby(p, 0.012 * s, 0.014 * s, 0.0, 0.007 * s, 0.007 * s, 0.007 * s, PEARL, n=7, rings=3)
    return p


def pearls():
    rng = random.Random(405)
    return [
        at(sea_rock("rock", rng, r=0.055, h=0.026), -0.03, -0.02),
        at(lifted(pearl_oyster("oyster", 1.3), 0.02), -0.03, -0.02, 0.5),
        at(sea_rock("rock_b", rng, r=0.03, h=0.014, weed=False), 0.09, 0.05),
        at(lifted(pearl_oyster("oyster_b", 0.9), 0.008), 0.09, 0.05, 2.0),
        at(ripples("ground_ripples", [(-0.03, -0.02, 0.09)], rng), 0.0, 0.0),
    ]


def whale():
    rng = random.Random(406)
    return [
        at(ripples("ground_ripples", [(0.0, 0.0, 0.1), (0.0, 0.0, 0.14), (0.12, 0.08, 0.04)], rng), -0.02, -0.02),
        at(scaled(whale_back("whale", rng), 0.95), -0.02, -0.02, 0.4),
        at(scaled(whale_tail("tail"), 0.8), 0.12, 0.08, 1.2),
    ]


MODELS = {
    "fish": fish,
    "shrimp": shrimp,
    "crab": crab,
    "clam": clam,
    "pearls": pearls,
    "whale": whale,
}
