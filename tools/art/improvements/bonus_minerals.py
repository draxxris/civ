"""Mineral resources: ore outcrops, stone, crystals, salt, oil seeps, the thermal vent and methane.

Conventions for resource models are in bonus_kit.py. Most minerals may also lie on hills and mountains, so every
outcrop is a few small parts, each with a deep foot (it is seated on the ground under its own origin and must not
show a gap on a steep flank). Oil may also lie in the ocean: its slick is one flat ground part, so it drapes on a
slope ashore and floats flat at sea level. Methane lies only in the ocean.
"""
import random

from .bonus_kit import blobby, crystal, puffs, ripples, spindle  # noqa: F401
from .farming import OIL, OIL_SHEEN, blob
from .kit import *  # noqa: F401,F403
from .kit import Part, at
from .water import solid

DEEP = -0.12  # outcrop foot: deep enough for a mountain flank

BAUXITE = srgb(0xB0603A)
BAUXITE_DARK = srgb(0x8E4A2E)
BAUXITE_LIGHT = srgb(0xC87A4E)
COAL = srgb(0x2A2A2E)
COAL_DARK = srgb(0x1A1A1C)
COAL_SHINE = srgb(0x4A4C56)
VERDIGRIS = srgb(0x4E9E86)
COPPER = srgb(0xC0703A)
COPPER_DARK = srgb(0x9A5430)
GOLD = srgb(0xEAB83A)
GOLD_LIGHT = srgb(0xF6D870)
QUARTZ = srgb(0xE6E2D8)
RUST = srgb(0x8A4A30)
RUST_DARK = srgb(0x6E3A28)
HEMATITE = srgb(0x4A4448)
LEAD_ROCK = srgb(0x6A7078)
LEAD_ROCK_DARK = srgb(0x545A62)
GALENA = srgb(0xA4AAB4)
SILVER_ROCK = srgb(0x8C8A88)
SILVER = srgb(0xDCE2EA)
SILVER_DARK = srgb(0xB4BCC6)
EMERALD = srgb(0x2EAA6A)
RUBY = srgb(0xC8304A)
SAPPHIRE = srgb(0x3A5ACF)
AMETHYST = srgb(0x8A4AC0)
MARBLE = srgb(0xEDEAE4)
MARBLE_SHADE = srgb(0xD6D2CC)
MARBLE_VEIN = srgb(0xB8B4B0)
GRANITE = srgb(0x9A958C)
GRANITE_DARK = srgb(0x7A766E)
GRANITE_LIGHT = srgb(0xB0ABA2)
OBSIDIAN = srgb(0x1E1A24)
OBSIDIAN_SHEEN = srgb(0x3E3650)
SALT = srgb(0xF2F0EA)
SALT_SHADE = srgb(0xDCD8CC)
SALT_CRUST = srgb(0xE4DED0)
URANIUM_ROCK = srgb(0x4A4A40)
URANIUM_ROCK_DARK = srgb(0x3A3A32)
URANIUM_GLOW = srgb(0xB8E83A)
URANIUM_YELLOW = srgb(0xD8C83A)
VENT_ROCK = srgb(0x5A504A)
VENT_ROCK_DARK = srgb(0x443C38)
SULPHUR = srgb(0xE0C840)
SULPHUR_ORANGE = srgb(0xD08A3A)
STEAM = srgb(0xEEF0F0)
STEAM_SHADE = srgb(0xD4D8DA)
HYDRATE = srgb(0xE6F0F4)
HYDRATE_SHADE = srgb(0xC4D8E0)
BUBBLE = srgb(0xB8DCE6)


def outcrop(name, rng, rocks, r=0.05, h=0.05, y0=DEEP):
    """An irregular rock outcrop with a deep foot."""
    p = Part(name)
    lumpy_mound(p, r, r * 0.85, h, rocks, rng, y0=y0, rings=3, n=8)
    return p


def nuggets(part, rng, colour, count, r, h, size=0.007, shine=None):
    """Lumps of ore set into the faces of an outcrop of radius r and height h."""
    for k in range(count):
        a = k * math.tau / count + rng.random() * 0.6
        t = 0.25 + 0.5 * rng.random()
        y = h * (1 - t) * 0.9
        rr = r * math.sin(t * math.pi / 2) * 0.95
        x, z = math.cos(a) * rr, math.sin(a) * rr
        s = size * (0.8 + 0.5 * rng.random())
        blobby(part, x, y, z, s, s * 0.8, s, colour if not shine or k % 2 else shine, n=5, rings=2,
               phase=rng.random())


def ore_heap(name, rng, colours, r=0.03, h=0.022):
    """A small heap of broken ore at the foot of the outcrop."""
    p = Part(name)
    lumpy_mound(p, r, r * 0.85, h, colours, rng, y0=-0.04, rings=2, n=7)
    return p


def vein_outcrop(name, rng, rocks, ore, shine=None, count=7, r=0.05, h=0.055):
    p = outcrop(name, rng, rocks, r, h)
    nuggets(p, rng, ore, count, r, h, shine=shine)
    return p


def ore_resource(seed, rocks, ore, heap, shine=None, big=(0.058, 0.064), small=(0.036, 0.04)):
    """Two veined outcrops and a heap of broken ore: copper, iron, gold, lead, silver, bauxite and coal."""
    rng = random.Random(seed)
    return [
        at(vein_outcrop("outcrop", rng, rocks, ore, shine, count=8, r=big[0], h=big[1]), -0.05, -0.04),
        at(vein_outcrop("outcrop_b", rng, rocks, ore, shine, count=5, r=small[0], h=small[1]), 0.1, -0.02),
        at(ore_heap("heap", rng, heap), 0.02, 0.1),
    ]


def bauxite():
    return ore_resource(301, [BAUXITE_DARK, BAUXITE, BAUXITE_DARK], BAUXITE_LIGHT,
                        [BAUXITE, BAUXITE_LIGHT, BAUXITE_DARK])


def coal():
    return ore_resource(302, [COAL, COAL_DARK, COAL], COAL_SHINE, [COAL_DARK, COAL_SHINE, COAL], shine=COAL_DARK)


def copper():
    return ore_resource(303, [ROCK, ROCK_DARK, ROCK], COPPER, [COPPER, COPPER_DARK, VERDIGRIS], shine=VERDIGRIS)


def gold():
    return ore_resource(304, [ROCK, ROCK_DARK, QUARTZ], GOLD, [GOLD, GOLD_LIGHT, QUARTZ], shine=GOLD_LIGHT)


def iron():
    return ore_resource(305, [RUST, RUST_DARK, RUST], HEMATITE, [RUST_DARK, HEMATITE, RUST])


def lead():
    rng = random.Random(306)
    parts = ore_resource(306, [LEAD_ROCK, LEAD_ROCK_DARK, LEAD_ROCK], GALENA, [LEAD_ROCK_DARK, GALENA, LEAD_ROCK])
    cubes = Part("galena")
    for k, (x, z, s) in enumerate(((0.0, 0.0, 0.012), (0.016, 0.006, 0.009), (0.006, -0.014, 0.008))):
        box(cubes, x - s, -0.03, z - s, x + s, 2 * s - 0.004, z + s, GALENA, top=SILVER_DARK, yaw=rng.random())
    parts.append(at(cubes, -0.1, 0.1))
    return parts


def silver():
    rng = random.Random(307)
    parts = ore_resource(307, [SILVER_ROCK, ROCK_DARK, SILVER_ROCK], SILVER, [SILVER_ROCK, SILVER, SILVER_DARK],
                         shine=SILVER_DARK)
    shards = Part("shards")
    for k in range(4):
        a = k * math.tau / 4 + rng.random()
        crystal(shards, (math.cos(a) * 0.008, 0.0, math.sin(a) * 0.008), (math.cos(a) * 0.4, 1.0, math.sin(a) * 0.4),
                0.006, 0.03 + 0.012 * rng.random(), SILVER, SILVER_DARK, n=4)
    parts.append(at(shards, -0.1, 0.08))
    return parts


def crystal_cluster(name, rng, colours, count=7, size=1.0):
    """A cluster of coloured crystals bursting from a rock."""
    p = outcrop(name, rng, [ROCK, ROCK_DARK, ROCK], r=0.036 * size, h=0.03 * size)
    for k in range(count):
        a = k * math.tau / count + rng.random() * 0.5
        tilt = 0.35 + 0.35 * rng.random()
        base = (math.cos(a) * 0.012 * size, 0.02 * size, math.sin(a) * 0.012 * size)
        crystal(p, base, (math.cos(a) * tilt, 1.0, math.sin(a) * tilt), 0.008 * size,
                (0.04 + 0.025 * rng.random()) * size, colours[k % len(colours)], n=6)
    return p


def gems():
    rng = random.Random(308)
    return [
        at(crystal_cluster("cluster", rng, [EMERALD, RUBY, SAPPHIRE, EMERALD, AMETHYST], count=7, size=1.3),
           -0.04, -0.04),
        at(crystal_cluster("cluster_b", rng, [RUBY, SAPPHIRE, EMERALD], count=5, size=0.9), 0.1, 0.03),
        at(outcrop("rock", rng, [ROCK, ROCK_DARK, ROCK], r=0.026, h=0.022), -0.04, 0.1),
    ]


def uranium():
    rng = random.Random(309)
    rocks = [URANIUM_ROCK, URANIUM_ROCK_DARK, URANIUM_ROCK]
    return [
        at(crystal_cluster("cluster", rng, [URANIUM_GLOW, URANIUM_YELLOW, URANIUM_GLOW], count=6, size=1.2),
           -0.05, -0.03),
        at(vein_outcrop("outcrop", rng, rocks, URANIUM_YELLOW, URANIUM_GLOW, count=6, r=0.04, h=0.04), 0.09, -0.04),
        at(ore_heap("heap", rng, [URANIUM_YELLOW, URANIUM_ROCK, URANIUM_GLOW]), 0.03, 0.1),
    ]


def obsidian():
    rng = random.Random(310)
    parts = []
    for i, (x, z, s) in enumerate(((-0.05, -0.04, 1.3), (0.08, -0.03, 1.0), (0.0, 0.09, 0.8))):
        p = outcrop(f"outcrop_{i}", rng, [OBSIDIAN, OBSIDIAN_SHEEN, OBSIDIAN], r=0.03 * s, h=0.025 * s)
        for k in range(5):
            a = k * math.tau / 5 + rng.random()
            crystal(p, (math.cos(a) * 0.01 * s, 0.01 * s, math.sin(a) * 0.01 * s),
                    (math.cos(a) * 0.5, 1.0, math.sin(a) * 0.5), 0.01 * s, (0.035 + 0.02 * rng.random()) * s,
                    OBSIDIAN if k % 2 else OBSIDIAN_SHEEN, OBSIDIAN_SHEEN, n=4)
        parts.append(at(p, x, z))
    return parts


def cut_blocks(name, rng, colour, shade, vein=None, count=4):
    """Rough-cut blocks stacked two high."""
    p = Part(name)
    spots = [(-0.02, 0.0, 0.0), (0.02, 0.0, 0.0), (0.0, 0.026, 0.0), (0.0, 0.0, 0.03)][:count]
    for x, y, z in spots:
        yaw = (rng.random() - 0.5) * 0.3
        box(p, -0.019, y - (0.04 if y == 0 else 0), -0.013, 0.019, y + 0.026, 0.013, shade, at=(x, 0, z), yaw=yaw,
            top=colour)
        if vein:
            box(p, -0.0195, y + 0.008, -0.0135, 0.0195, y + 0.011, 0.0135, vein, at=(x, 0, z), yaw=yaw)
    return p


def marble():
    rng = random.Random(311)
    return [
        at(outcrop("outcrop", rng, [MARBLE, MARBLE_SHADE, MARBLE_VEIN], r=0.058, h=0.058), -0.05, -0.04),
        at(cut_blocks("blocks", rng, MARBLE, MARBLE_SHADE, MARBLE_VEIN), 0.09, 0.03, 0.3),
        at(outcrop("rock", rng, [MARBLE_SHADE, MARBLE, MARBLE_SHADE], r=0.026, h=0.024), -0.02, 0.1),
    ]


def stone():
    rng = random.Random(312)
    return [
        at(outcrop("boulder", rng, [GRANITE, GRANITE_DARK, GRANITE_LIGHT], r=0.06, h=0.062), -0.05, -0.04),
        at(outcrop("boulder_b", rng, [GRANITE_LIGHT, GRANITE, GRANITE_DARK], r=0.034, h=0.034), 0.1, -0.05),
        at(cut_blocks("blocks", rng, GRANITE_LIGHT, GRANITE), 0.06, 0.09, -0.3),
    ]


def salt_flat(name, rng):
    """A white crust of evaporated salt with a cracked, polygonal pattern; tilts with the slope."""
    p = Part(name)
    blob(p, 0.11, 0.085, SALT_CRUST, rng, y=0.003, n=12)
    for k in range(9):
        x, z = -0.07 + 0.14 * rng.random(), -0.05 + 0.1 * rng.random()
        blob(p, 0.022, 0.018, SALT if k % 2 else SALT_SHADE, rng, at=(x, 0, z), y=0.004, n=6)
    return p


def salt_cone(name, r, h):
    p = Part(name)
    cylinder(p, r, -0.03, 0.0, SALT_SHADE, n=8)
    cylinder(p, r, 0.0, h, SALT, n=8, r_top=r * 0.15, top=SALT, phase=0.3)
    return p


def salt():
    rng = random.Random(313)
    return [
        at(salt_flat("ground_flat", rng), 0.0, 0.0),
        at(salt_cone("cone", 0.03, 0.05), -0.05, -0.03),
        at(salt_cone("cone_b", 0.024, 0.038), 0.05, -0.05),
        at(salt_cone("cone_c", 0.02, 0.03), 0.02, 0.06),
    ]


def vent_crust(name, rng):
    """Mineral crust round the vent: orange and sulphur-yellow rings on dark rock; tilts with the slope."""
    p = Part(name)
    blob(p, 0.1, 0.085, VENT_ROCK_DARK, rng, y=0.002, n=11)
    blob(p, 0.075, 0.062, SULPHUR_ORANGE, rng, y=0.003, n=10)
    blob(p, 0.05, 0.042, SULPHUR, rng, y=0.004, n=9)
    return p


def vent_cone(name, rng, r=0.04, h=0.05):
    """A squat mineral chimney with a steaming mouth."""
    p = Part(name)
    lumpy_mound(p, r, r * 0.9, h, [VENT_ROCK, VENT_ROCK_DARK, SULPHUR_ORANGE], rng, y0=DEEP, rings=3, n=8)
    cylinder(p, r * 0.32, h - 0.01, h + 0.002, SULPHUR, n=7, top=DARK)
    puffs(p, [(0.0, h + 0.014, 0.0, 0.012), (0.004, h + 0.034, 0.003, 0.016), (-0.003, h + 0.058, -0.002, 0.019),
              (0.006, h + 0.084, 0.002, 0.017)], STEAM, rng)
    return p


def thermal_vent():
    rng = random.Random(314)
    small = Part("vent_b")
    small.tris = vent_cone("tmp", rng, r=0.022, h=0.026).tris
    return [
        at(vent_crust("ground_crust", rng), 0.0, 0.0),
        at(vent_cone("vent", rng), -0.03, -0.03),
        at(small, 0.07, 0.04),
    ]


def slick(name, rng):
    """A black, glossy oil seep with a rainbow sheen and bubbles. On land it drapes on the slope; at sea it floats."""
    p = Part(name)
    blob(p, 0.09, 0.07, OIL, rng, y=0.003, n=12)
    blob(p, 0.055, 0.04, OIL_SHEEN, rng, at=(0.012, 0, -0.006), y=0.004, n=9)
    for k, colour in enumerate((srgb(0x5A3E6E), srgb(0x2E5A6A), srgb(0x6E6A2E))):
        blob(p, 0.018, 0.012, colour, rng, at=(-0.03 + 0.03 * k, 0, 0.02 - 0.012 * k), y=0.005, n=7)
    for k in range(4):
        x, z = -0.04 + 0.08 * rng.random(), -0.03 + 0.06 * rng.random()
        blobby(p, x, 0.006, z, 0.006, 0.005, 0.006, OIL_SHEEN, n=5, rings=2, phase=rng.random())
    return p


def oil():
    rng = random.Random(315)
    return [at(slick("ground_slick", rng), 0.0, 0.0)]


def hydrate_floe(name, rng, r=0.04):
    """A flat, pale chunk of methane ice floating at the surface."""
    p = Part(name)
    pts = []
    for k in range(7):
        a = k * math.tau / 7 + rng.random() * 0.4
        rr = r * (0.75 + 0.35 * rng.random())
        for y in (-0.012, 0.01 + 0.004 * rng.random()):
            pts.append((math.cos(a) * rr, y, math.sin(a) * rr * 0.8))
    solid(p, pts, HYDRATE_SHADE, HYDRATE)
    return p


def bubbles(name, rng):
    """Methane welling up: a dome of bubbles breaking the surface and a few rising above it."""
    p = Part(name)
    for k in range(12):
        a = rng.random() * math.tau
        rr = 0.03 * rng.random()
        r = 0.005 + 0.007 * rng.random()
        blobby(p, math.cos(a) * rr, -0.002 + 0.006 * rng.random(), math.sin(a) * rr, r, r * 0.8, r, BUBBLE, n=5,
               rings=2, phase=rng.random())
    for k in range(5):
        r = 0.004 + 0.003 * rng.random()
        blobby(p, 0.01 * (rng.random() - 0.5), 0.02 + 0.016 * k, 0.01 * (rng.random() - 0.5), r, r, r, HYDRATE,
               n=5, rings=2)
    return p


def methane():
    rng = random.Random(316)
    return [
        at(ripples("ground_ripples", [(0.0, 0.0, 0.05), (0.0, 0.0, 0.08), (0.0, 0.0, 0.11)], rng), -0.02, -0.02),
        at(bubbles("bubbles", rng), -0.02, -0.02),
        at(hydrate_floe("floe", rng), 0.1, 0.06),
        at(hydrate_floe("floe_b", rng, r=0.026), -0.1, 0.08),
    ]


MODELS = {
    "bauxite": bauxite,
    "coal": coal,
    "copper": copper,
    "gold": gold,
    "iron": iron,
    "lead": lead,
    "silver": silver,
    "gems": gems,
    "uranium": uranium,
    "obsidian": obsidian,
    "marble": marble,
    "stone": stone,
    "salt": salt,
    "thermal_vent": thermal_vent,
    "oil": oil,
    "methane": methane,
}
