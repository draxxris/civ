"""Plant resources: fruit trees, crops, plantation crops and the ancient temple and fossils that share their land.

Conventions for resource models are in bonus_kit.py. Trees are about 0.2 world units tall, a little under the
scatter forest, so a fruit grove reads among the trees of a forest or jungle tile.
"""
import random

from .bonus_kit import blobby, crystal, puffs, scaled, spindle  # noqa: F401
from .farming import (APPLE, BANANA, BANANA_DARK, BASKET, BERRY, BUSH, BUSH_LIGHT, COTTON,
                      GRAIN_GOLD, GRAIN_GOLD_DARK, GRAPE, LEAF, LEAF_DARK, LEAF_LIGHT, SOIL_DARK, TOBACCO, VINE,
                      VINE_DARK, ball, blob, slab)
from .kit import *  # noqa: F401,F403
from .kit import FOOT, Part, at
from .water import solid

LEMON = srgb(0xEAD03A)
LEMON_LEAF = srgb(0x2F6B2A)
LEMON_LEAF_LIGHT = srgb(0x3F8236)
OLIVE_LEAF = srgb(0x8A9A6A)
OLIVE_LEAF_DARK = srgb(0x6E7E56)
OLIVE_FRUIT = srgb(0x3A2E3E)
OLIVE_BARK = srgb(0x6E6458)
CANNABIS = srgb(0x4E9A3A)
CANNABIS_DARK = srgb(0x3A7A2C)
CANNABIS_LIGHT = srgb(0x6AB24A)
COFFEE_LEAF = srgb(0x2E5E2A)
COFFEE_BEAN = srgb(0x5A3A22)
SACK = srgb(0xB89A6A)
SACK_DARK = srgb(0x9A7E52)
CORN_STALK = srgb(0x7FA048)
CORN_LEAF = srgb(0x6A9440)
CORN_COB = srgb(0xE8C04A)
CORN_SILK = srgb(0xB8863A)
INDIGO = srgb(0x4A3A9A)
VIOLET = srgb(0x8A3A9A)
MADDER = srgb(0xB43A3A)
DYE_VAT_TOP = srgb(0x3A2E7A)
INCENSE_BARK = srgb(0x9A8A72)
INCENSE_LEAF = srgb(0x7A8E4A)
INCENSE_LEAF_DARK = srgb(0x5E7238)
RESIN = srgb(0xE0B060)
SMOKE = srgb(0xD8D6D0)
SMOKE_DARK = srgb(0xB8B6B0)
POTATO = srgb(0xA8824E)
POTATO_LEAF = srgb(0x5A8A34)
PADDY_WATER = srgb(0x6FA0A0)
PADDY_BUND = srgb(0x7E6646)
RICE_SHOOT = srgb(0x8CC448)
RICE_SHOOT_DARK = srgb(0x6EA63A)
RUBBER_BARK = srgb(0xA49A86)
RUBBER_LEAF = srgb(0x2E6A30)
RUBBER_LEAF_LIGHT = srgb(0x3E8038)
LATEX = srgb(0xF2EEE2)
SCAR = srgb(0x5A4A3A)
MULBERRY = srgb(0x4A7A34)
MULBERRY_LIGHT = srgb(0x5E9240)
COCOON = srgb(0xF4F0DA)
CHILI = srgb(0xC8302A)
CHILI_ORANGE = srgb(0xE07A2A)
TURMERIC = srgb(0xE0A82A)
PAPRIKA = srgb(0xB8442A)
SUGAR_CANE = srgb(0x9AB84A)
SUGAR_CANE_DARK = srgb(0x7E9A3A)
SUGAR_LEAF = srgb(0x6EA040)
TEA = srgb(0x4E9A3A)
TEA_LIGHT = srgb(0x6AB848)
TEA_DARK = srgb(0x3A7E2E)
TOBACCO_LEAF = srgb(0x6E9A3E)
TOBACCO_LEAF_DARK = srgb(0x587E32)
MOSS = srgb(0x5E7A3A)
MOSS_DARK = srgb(0x4A6230)
TEMPLE = srgb(0xA49A84)
TEMPLE_LIGHT = srgb(0xBEB49C)
TEMPLE_DARK = srgb(0x847A66)
BONE = srgb(0xE8DEC4)
BONE_SHADE = srgb(0xCFC3A4)
DIG = srgb(0xB89A6E)
DIG_DARK = srgb(0x9A7E56)


# --- fruit trees ----------------------------------------------------------------------------------------------

def fruit_tree(name, rng, fruit, leaves, bark=TIMBER, s=1.0, fruits=4, crown=1.0):
    """A small round fruit tree, about 0.2 world units tall, with fruit dotted round the crown."""
    p = Part(name)
    beam(p, (0, FOOT / 2, 0), (0.002, 0.05 * s, 0.001), 0.011 * s, bark)
    beam(p, (0.002, 0.04 * s, 0.0), (0.016 * s, 0.058 * s, 0.006 * s), 0.007 * s, bark)
    r, h = 0.042 * s * crown, 0.07 * s
    ball(p, 0.0, 0.036 * s, 0.0, r, h, leaves, n=7, phase=rng.random())
    for k in range(fruits):
        a = k * math.tau / fruits + rng.random() * 0.8
        y = 0.036 * s + h * (0.32 + 0.3 * rng.random())
        fx, fz = math.cos(a) * r * 0.9, math.sin(a) * r * 0.9
        box(p, fx - 0.006 * s, y - 0.006 * s, fz - 0.006 * s, fx + 0.006 * s, y + 0.006 * s, fz + 0.006 * s, fruit)
    return p


def grove_floor(name, rng, colour=srgb(0x7BA04E), r=0.07):
    """A mown patch under the trees; tilts with the slope."""
    p = Part(name)
    blob(p, r, r * 0.8, colour, rng, y=0.002, n=9)
    return p


def fallen_fruit(name, fruit, rng, count=5):
    p = Part(name)
    box(p, -0.012, FOOT / 2, -0.01, 0.012, 0.016, 0.01, BASKET, top=fruit)
    for _ in range(count):
        x, z = 0.02 + 0.02 * rng.random(), -0.015 + 0.03 * rng.random()
        box(p, x - 0.004, 0.0, z - 0.004, x + 0.004, 0.007, z + 0.004, fruit)
    return p


def apple():
    rng = random.Random(101)
    leaves = [LEAF, LEAF_DARK, LEAF_LIGHT, LEAF_DARK]
    return [
        at(fruit_tree("tree_a", rng, APPLE, leaves, s=1.15), -0.08, -0.06),
        at(fruit_tree("tree_b", rng, APPLE, leaves, s=1.0), 0.1, -0.03),
        at(fruit_tree("tree_c", rng, APPLE, leaves, s=0.9), 0.0, 0.1),
        at(fallen_fruit("basket", APPLE, rng), 0.14, 0.12, 0.4),
    ]


def lemon():
    rng = random.Random(102)
    leaves = [LEMON_LEAF, LEMON_LEAF_LIGHT, LEMON_LEAF, LEAF]
    return [
        at(fruit_tree("tree_a", rng, LEMON, leaves, s=1.05, fruits=5, crown=0.9), -0.07, -0.07),
        at(fruit_tree("tree_b", rng, LEMON, leaves, s=0.95, fruits=5, crown=0.9), 0.1, -0.02),
        at(fruit_tree("tree_c", rng, LEMON, leaves, s=0.85, fruits=4, crown=0.9), -0.02, 0.1),
        at(fallen_fruit("basket", LEMON, rng), 0.13, 0.13, -0.3),
    ]


def olive_tree(name, rng, s=1.0):
    """A gnarled olive: a twisted, leaning trunk splitting in two under a broad silvery crown."""
    p = Part(name)
    lean = 0.008 * s
    beam(p, (0, FOOT / 2, 0), (lean, 0.03 * s, 0.002), 0.014 * s, OLIVE_BARK)
    beam(p, (lean, 0.028 * s, 0.002), (-0.014 * s, 0.054 * s, -0.004), 0.009 * s, OLIVE_BARK)
    beam(p, (lean, 0.028 * s, 0.002), (0.022 * s, 0.05 * s, 0.006), 0.009 * s, OLIVE_BARK)
    colours = [OLIVE_LEAF, OLIVE_LEAF_DARK, OLIVE_LEAF, OLIVE_LEAF_DARK]
    ball(p, -0.014 * s, 0.044 * s, -0.004, 0.034 * s, 0.04 * s, colours, n=6, phase=rng.random())
    ball(p, 0.022 * s, 0.042 * s, 0.006, 0.032 * s, 0.038 * s, colours, n=6, phase=rng.random())
    for k in range(6):
        a = k * math.tau / 6 + rng.random()
        cx = 0.004 * s + math.cos(a) * 0.036 * s
        cz = math.sin(a) * 0.03 * s
        y = 0.056 * s + 0.01 * rng.random()
        box(p, cx - 0.004, y - 0.004, cz - 0.004, cx + 0.004, y + 0.004, cz + 0.004, OLIVE_FRUIT)
    return p


def olive_jars(name):
    p = Part(name)
    for x, z, r in ((0.0, 0.0, 0.012), (0.024, 0.008, 0.01)):
        cylinder(p, r * 0.7, FOOT / 2, 0.006, TILE_DARK, at=(x, 0, z), n=7)
        cylinder(p, r * 0.7, 0.006, 0.02, TILE, at=(x, 0, z), n=7, r_top=r)
        cylinder(p, r, 0.02, 0.032, TILE, at=(x, 0, z), n=7, r_top=r * 0.45, top=OLIVE_FRUIT)
    return p


def olives():
    rng = random.Random(103)
    return [
        at(olive_tree("tree_a", rng, s=1.2), -0.07, -0.06),
        at(olive_tree("tree_b", rng, s=1.0), 0.11, 0.0),
        at(olive_tree("tree_c", rng, s=0.85), -0.03, 0.11),
        at(olive_jars("jars"), 0.13, 0.13),
    ]


def banana_plant(name, rng, s=1.0, bunch=True):
    """A banana plant: a thick green stem, long drooping leaves and a bunch of yellow fruit under them."""
    p = Part(name)
    top = 0.075 * s
    cylinder(p, 0.01 * s, FOOT / 2, top, BANANA_DARK, n=6, r_top=0.007 * s)
    for k in range(6):
        a = k * math.tau / 6 + rng.random() * 0.4
        dx, dz = math.cos(a), math.sin(a)
        length = (0.06 + 0.015 * rng.random()) * s
        root = (0.0, top - 0.004, 0.0)
        mid = (dx * length * 0.55, top + 0.016 * s, dz * length * 0.55)
        tip = (dx * length, top - 0.03 * s, dz * length)
        side = (-dz * 0.014 * s, 0, dx * 0.014 * s)
        pts = [root, tip, (mid[0] + side[0], mid[1], mid[2] + side[2]), (mid[0] - side[0], mid[1], mid[2] - side[2])]
        pts += [(x, y - 0.003, z) for x, y, z in pts[2:]]
        solid(p, pts, BANANA if k % 2 else LEAF, BANANA)
    if bunch:
        for k in range(4):
            y = top - 0.02 * s - k * 0.007 * s
            box(p, 0.008 * s, y - 0.004, -0.006 * s, 0.022 * s, y + 0.004, 0.008 * s,
                LEMON if k % 2 else srgb(0xD8C040))
    return p


def banana():
    rng = random.Random(104)
    return [
        at(banana_plant("plant_a", rng, s=1.3), -0.07, -0.05),
        at(banana_plant("plant_b", rng, s=1.05), 0.1, -0.02),
        at(banana_plant("plant_c", rng, s=0.9, bunch=False), 0.0, 0.11),
        at(fallen_fruit("basket", LEMON, rng, count=3), 0.13, 0.13),
    ]


# --- bushes and herbs ------------------------------------------------------------------------------------------

def cannabis_plant(name, rng, s=1.0):
    """A tall herb: a thin stem with stacked, serrated, star-shaped leaf tiers narrowing to the top."""
    p = Part(name)
    top = 0.1 * s
    beam(p, (0, FOOT / 2, 0), (0, top, 0), 0.005 * s, CANNABIS_DARK)
    tiers = 5
    for t in range(tiers):
        y = 0.02 * s + t * (top - 0.024 * s) / tiers
        r = 0.034 * s * (1 - t / (tiers + 1))
        for k in range(7):
            a = k * math.tau / 7 + t * 0.45 + rng.random() * 0.2
            tip = (math.cos(a) * r, y + 0.006 * s, math.sin(a) * r)
            side = (-math.sin(a) * 0.005 * s, 0, math.cos(a) * 0.005 * s)
            mid = (tip[0] * 0.5, y + 0.008 * s, tip[2] * 0.5)
            solid(p, [(0, y, 0), tip, (mid[0] + side[0], mid[1], mid[2] + side[2]),
                      (mid[0] - side[0], mid[1], mid[2] - side[2]), (mid[0], mid[1] - 0.003, mid[2])],
                  CANNABIS if (k + t) % 2 else CANNABIS_LIGHT)
    cylinder(p, 0.008 * s, top - 0.012 * s, top + 0.012 * s, CANNABIS_LIGHT, n=5, r_top=0.0)
    return p


def cannabis():
    rng = random.Random(105)
    return [
        at(cannabis_plant("plant_a", rng, 1.2), -0.08, -0.05),
        at(cannabis_plant("plant_b", rng, 1.0), 0.02, -0.09),
        at(cannabis_plant("plant_c", rng, 1.1), 0.1, 0.02),
        at(cannabis_plant("plant_d", rng, 0.85), -0.04, 0.08),
        at(cannabis_plant("plant_e", rng, 0.75), 0.06, 0.12),
    ]


def berry_bush(name, rng, leaves, berry, s=1.0, berries=7, r=0.034, h=0.06):
    """A round bush dotted with berries, flowers or bolls."""
    p = Part(name)
    ball(p, 0.0, FOOT / 2, 0.0, r * s, h * s, leaves, n=7, phase=rng.random())
    for k in range(berries):
        a = k * math.tau / berries + rng.random() * 0.6
        y = FOOT / 2 + h * s * (0.35 + 0.4 * rng.random())
        rr = r * s * (0.95 - 0.25 * (y - FOOT / 2) / (h * s))
        bx, bz = math.cos(a) * rr, math.sin(a) * rr
        e = 0.005 * s
        box(p, bx - e, y - e, bz - e, bx + e, y + e, bz + e, berry)
    return p


def sack(name, top_colour, r=0.016, h=0.03):
    """A slumped burlap sack, open at the top to show what it holds."""
    p = Part(name)
    cylinder(p, r, FOOT / 2, h * 0.7, SACK, n=7, r_top=r * 1.08)
    cylinder(p, r * 1.08, h * 0.7, h, SACK_DARK, n=7, r_top=r * 0.9, top=top_colour)
    return p


def coffee():
    rng = random.Random(106)
    leaves = [COFFEE_LEAF, LEAF_DARK, COFFEE_LEAF, LEAF]
    return [
        at(berry_bush("bush_a", rng, leaves, BERRY, s=1.2, berries=9), -0.08, -0.05),
        at(berry_bush("bush_b", rng, leaves, BERRY, s=1.0, berries=8), 0.08, -0.06),
        at(berry_bush("bush_c", rng, leaves, BERRY, s=0.95, berries=8), -0.01, 0.07),
        at(sack("sack", COFFEE_BEAN), 0.13, 0.1),
        at(sack("sack_b", COFFEE_BEAN, r=0.013, h=0.026), 0.17, 0.05),
    ]


def cotton():
    rng = random.Random(107)
    leaves = [BUSH, BUSH_LIGHT, BUSH, LEAF_DARK]
    parts = []
    for i, (x, z) in enumerate(((-0.1, -0.06), (0.0, -0.08), (0.1, -0.05), (-0.06, 0.04), (0.05, 0.05))):
        parts.append(at(berry_bush(f"bush_{i}", rng, leaves, COTTON, s=0.85 + 0.1 * (i % 2), berries=9, r=0.03,
                                   h=0.05), x, z))
    p = Part("basket")
    cylinder(p, 0.016, FOOT / 2, 0.024, BASKET, n=8, r_top=0.019)
    puffs(p, [(-0.006, 0.028, 0.0, 0.009), (0.006, 0.03, 0.004, 0.009), (0.0, 0.034, -0.005, 0.008)], COTTON, rng)
    parts.append(at(p, 0.13, 0.12))
    return parts


def dye():
    rng = random.Random(108)
    leaves = [BUSH, LEAF_DARK, BUSH_LIGHT, LEAF_DARK]
    vat = Part("vat")
    cylinder(vat, 0.026, FOOT, 0.03, BOARDS, n=9, r_top=0.028, top=DYE_VAT_TOP)
    for y in (0.008, 0.024):
        cylinder(vat, 0.0285, y - 0.003, y + 0.003, IRON, n=9)
    cloth = Part("cloth")
    for x in (-0.046, 0.046):
        box(cloth, x - 0.004, FOOT / 2, -0.004, x + 0.004, 0.07, 0.004, TIMBER)
    for colour, x in ((INDIGO, -0.028), (VIOLET, 0.0), (MADDER, 0.028)):
        box(cloth, x - 0.012, 0.02, -0.002, x + 0.012, 0.064, 0.002, colour)
    box(cloth, -0.05, 0.064, -0.003, 0.05, 0.07, 0.003, TIMBER_LIGHT)
    return [
        at(berry_bush("bush_a", rng, leaves, INDIGO, s=1.1, berries=10), -0.09, -0.04),
        at(berry_bush("bush_b", rng, leaves, VIOLET, s=0.95, berries=9), 0.04, -0.1),
        at(berry_bush("bush_c", rng, leaves, MADDER, s=0.9, berries=9), -0.06, 0.09),
        at(vat, 0.09, 0.07),
        at(cloth, 0.04, -0.0, 0.3),
    ]


def incense_tree(name, rng, s=1.0):
    """A frankincense tree: a pale, twisting trunk with thin branches and a flat, sparse crown; resin beads."""
    p = Part(name)
    beam(p, (0, FOOT / 2, 0), (0.006 * s, 0.04 * s, 0.0), 0.012 * s, INCENSE_BARK)
    tips = []
    for k in range(3):
        a = k * math.tau / 3 + rng.random() * 0.5
        tip = (math.cos(a) * 0.03 * s, 0.075 * s + 0.01 * rng.random(), math.sin(a) * 0.03 * s)
        beam(p, (0.006 * s, 0.038 * s, 0.0), tip, 0.007 * s, INCENSE_BARK)
        tips.append(tip)
    for x, y, z in tips:
        ball(p, x, y - 0.008 * s, z, 0.026 * s, 0.022 * s, [INCENSE_LEAF, INCENSE_LEAF_DARK], n=6, phase=rng.random())
    for y in (0.018, 0.03):
        box(p, 0.008 * s, y * s, 0.006 * s, 0.014 * s, y * s + 0.006, 0.012 * s, RESIN)
    return p


def censer(name, rng):
    """A bronze bowl on a stone plinth with a curl of smoke."""
    p = Part(name)
    box(p, -0.014, FOOT, -0.014, 0.014, 0.02, 0.014, STONE_DARK, top=STONE)
    cylinder(p, 0.01, 0.02, 0.034, srgb(0x9A6A3A), n=8, r_top=0.016, top=DARK)
    puffs(p, [(0.0, 0.05, 0.0, 0.009), (0.004, 0.068, 0.002, 0.011), (-0.002, 0.088, -0.003, 0.012),
              (0.006, 0.108, 0.0, 0.01)], SMOKE, rng)
    return p


def incense():
    rng = random.Random(109)
    return [
        at(incense_tree("tree_a", rng, 1.3), -0.07, -0.06),
        at(incense_tree("tree_b", rng, 1.0), 0.1, -0.02),
        at(censer("censer", rng), 0.0, 0.1),
    ]


def chili_bush(name, rng, s=1.0):
    p = berry_bush(name, rng, [BUSH, LEAF_DARK, BUSH_LIGHT, LEAF_DARK], CHILI, s=s, berries=6, r=0.03, h=0.05)
    for k in range(4):
        a = k * math.tau / 4 + rng.random()
        x, z = math.cos(a) * 0.026 * s, math.sin(a) * 0.026 * s
        spindle(p, (x, 0.04 * s, z), (x * 1.2, 0.018 * s, z * 1.2), 0.005 * s, CHILI_ORANGE if k % 2 else CHILI)
    return p


def spice_bowls(name):
    """Three shallow baskets heaped with ground spice."""
    p = Part(name)
    for x, z, colour in ((-0.022, 0.0, CHILI), (0.0, 0.02, TURMERIC), (0.022, 0.0, PAPRIKA)):
        cylinder(p, 0.012, FOOT / 2, 0.01, BASKET, at=(x, 0, z), n=8, r_top=0.015)
        cylinder(p, 0.014, 0.01, 0.022, colour, at=(x, 0, z), n=8, r_top=0.0)
    return p


def spices():
    rng = random.Random(110)
    return [
        at(chili_bush("bush_a", rng, 1.2), -0.08, -0.06),
        at(chili_bush("bush_b", rng, 1.0), 0.08, -0.07),
        at(chili_bush("bush_c", rng, 0.9), -0.07, 0.07),
        at(spice_bowls("bowls"), 0.07, 0.08, 0.2),
    ]


def mulberry(name, rng, s=1.0):
    """A mulberry bush with white silk cocoons hanging among the leaves."""
    p = Part(name)
    beam(p, (0, FOOT / 2, 0), (0, 0.03 * s, 0), 0.01 * s, TIMBER)
    ball(p, 0.0, 0.016 * s, 0.0, 0.04 * s, 0.07 * s, [MULBERRY, MULBERRY_LIGHT, MULBERRY, LEAF_DARK], n=7,
         phase=rng.random())
    for k in range(7):
        a = k * math.tau / 7 + rng.random() * 0.5
        y = 0.03 * s + 0.03 * s * rng.random()
        x, z = math.cos(a) * 0.038 * s, math.sin(a) * 0.038 * s
        blobby(p, x, y, z, 0.006 * s, 0.009 * s, 0.006 * s, COCOON, n=5, rings=2)
    return p


def cocoon_tray(name, rng):
    p = Part(name)
    box(p, -0.03, FOOT / 2, -0.02, 0.03, 0.008, 0.02, BASKET)
    for i in range(6):
        x, z = -0.02 + 0.008 * i, -0.008 + 0.016 * (i % 2)
        blobby(p, x, 0.012, z, 0.007, 0.005, 0.005, COCOON, n=5, rings=2, phase=rng.random())
    return p


def silk():
    rng = random.Random(111)
    return [
        at(mulberry("bush_a", rng, 1.2), -0.07, -0.06),
        at(mulberry("bush_b", rng, 1.0), 0.1, -0.03),
        at(mulberry("bush_c", rng, 0.85), -0.04, 0.09),
        at(cocoon_tray("tray", rng), 0.1, 0.11, 0.3),
    ]


def tea():
    """Two clipped rows of tea bushes, the back row raised on a terrace, and a picker's basket."""
    rng = random.Random(112)
    parts = []
    for row, (z, lift) in enumerate(((-0.06, 0.016), (0.04, 0.0))):
        p = Part(f"ground_row_{row}")
        slab(p, 0.26, 0.07, SOIL_DARK, y=lift + 0.004)
        if lift:
            box(p, -0.13, FOOT, 0.026, 0.13, lift + 0.004, 0.036, STONE_DARK, top=STONE)
        for k in range(5):
            x = -0.1 + k * 0.05
            ball(p, x, lift + 0.002, 0.0, 0.026, 0.034, [TEA, TEA_LIGHT, TEA, TEA_DARK], n=6, phase=rng.random())
        parts.append(at(p, 0.0, z))
    b = Part("basket")
    cylinder(b, 0.014, FOOT / 2, 0.03, BASKET, n=8, r_top=0.02, top=TEA_LIGHT)
    parts.append(at(b, 0.15, 0.12))
    return parts


def tobacco_plant(name, rng, s=1.0):
    """Broad, pale-green leaves in a rosette round a stem with a pink flower head."""
    p = Part(name)
    top = 0.08 * s
    beam(p, (0, FOOT / 2, 0), (0, top, 0), 0.005 * s, TOBACCO_LEAF_DARK)
    for t in range(3):
        y = 0.012 * s + t * 0.02 * s
        length = 0.045 * s * (1 - 0.22 * t)
        for k in range(4):
            a = k * math.tau / 4 + t * 0.8 + rng.random() * 0.2
            dx, dz = math.cos(a), math.sin(a)
            side = (-dz * 0.014 * s, 0, dx * 0.014 * s)
            mid = (dx * length * 0.5, y + 0.01 * s, dz * length * 0.5)
            tip = (dx * length, y - 0.004 * s, dz * length)
            pts = [(0, y, 0), tip, (mid[0] + side[0], mid[1], mid[2] + side[2]),
                   (mid[0] - side[0], mid[1], mid[2] - side[2]), (mid[0], mid[1] - 0.003, mid[2])]
            solid(p, pts, TOBACCO_LEAF if (k + t) % 2 else TOBACCO_LEAF_DARK)
    for k in range(3):
        a = k * math.tau / 3
        flower = (math.cos(a) * 0.006 * s, 0, math.sin(a) * 0.006 * s)
        cylinder(p, 0.004 * s, top - 0.002, top + 0.008 * s, srgb(0xD88AA0), at=flower, n=5)
    return p


def drying_rack(name):
    """A pole rack hung with curing leaves."""
    p = Part(name)
    for x in (-0.032, 0.032):
        beam(p, (x, FOOT / 2, 0.0), (x, 0.07, 0.0), 0.006, TIMBER)
    beam(p, (-0.036, 0.068, 0.0), (0.036, 0.068, 0.0), 0.005, TIMBER_LIGHT)
    for k in range(5):
        x = -0.024 + k * 0.012
        box(p, x - 0.004, 0.03, -0.004, x + 0.004, 0.066, 0.004, TOBACCO if k % 2 else srgb(0x7A5A2E))
    return p


def tobacco():
    rng = random.Random(113)
    return [
        at(tobacco_plant("plant_a", rng, 1.25), -0.08, -0.06),
        at(tobacco_plant("plant_b", rng, 1.1), 0.05, -0.08),
        at(tobacco_plant("plant_c", rng, 1.0), -0.05, 0.07),
        at(drying_rack("rack"), 0.1, 0.06, -0.4),
    ]


# --- crops ----------------------------------------------------------------------------------------------------

def corn_patch(name, rng):
    """Tall corn stalks in three rows with leaves and ripe cobs; tilts with the slope."""
    p = Part(name)
    slab(p, 0.15, 0.12, SOIL_DARK, y=0.003)
    for row in range(3):
        for k in range(4):
            x = -0.055 + k * 0.037 + 0.006 * rng.random()
            z = -0.04 + row * 0.04 + 0.006 * rng.random()
            h = 0.085 + 0.02 * rng.random()
            beam(p, (x, 0.0, z), (x, h, z), 0.005, CORN_STALK)
            for t, side in ((0.35, 1), (0.6, -1)):
                a = rng.random() * math.tau
                lx, lz = math.cos(a) * 0.022, math.sin(a) * 0.022
                beam(p, (x, h * t, z), (x + lx, h * t + 0.01 * side + 0.006, z + lz), 0.004, CORN_LEAF)
            box(p, x + 0.003, h * 0.45, z - 0.004, x + 0.01, h * 0.62, z + 0.004, CORN_COB)
            beam(p, (x, h, z), (x + 0.006, h + 0.012, z), 0.003, CORN_SILK)
    return p


def corn():
    rng = random.Random(114)
    pile = Part("cobs")
    for i, (x, z, y) in enumerate(((-0.012, 0.0, 0.0), (0.0, 0.006, 0.0), (0.012, 0.0, 0.0), (-0.006, 0.003, 0.009),
                                   (0.006, 0.003, 0.009))):
        spindle(pile, (x, y + 0.004, z - 0.016), (x, y + 0.004, z + 0.016), 0.006, CORN_COB, bulge=0.5)
    return [at(corn_patch("ground_corn", rng), -0.02, -0.03), at(pile, 0.14, 0.1, 0.3)]


def stook(name, rng):
    """A stook of wheat sheaves leaning together."""
    p = Part(name)
    for k in range(5):
        a = k * math.tau / 5 + rng.random() * 0.3
        foot = (math.cos(a) * 0.016, FOOT / 2, math.sin(a) * 0.016)
        top = (math.cos(a) * 0.004, 0.06, math.sin(a) * 0.004)
        beam(p, foot, top, 0.011, GRAIN_GOLD if k % 2 else GRAIN_GOLD_DARK)
    cylinder(p, 0.012, 0.056, 0.074, GRAIN_GOLD, n=6, r_top=0.0)
    box(p, -0.013, 0.032, -0.013, 0.013, 0.038, 0.013, TIMBER_LIGHT)
    return p


def wheat_patch(name, rng):
    """A square of ripe wheat: dense golden tufts in rows; tilts with the slope."""
    p = Part(name)
    slab(p, 0.17, 0.13, SOIL, y=0.003)
    for row in range(5):
        for k in range(7):
            x = -0.072 + k * 0.024 + 0.004 * rng.random()
            z = -0.052 + row * 0.026 + 0.004 * rng.random()
            h = 0.032 + 0.008 * rng.random()
            cylinder(p, 0.008, 0.0, h, GRAIN_GOLD if (k + row) % 2 else GRAIN_GOLD_DARK, at=(x, 0, z), n=4,
                     r_top=0.011, top=srgb(0xE0C060), phase=rng.random())
    return p


def wheat():
    rng = random.Random(115)
    return [
        at(wheat_patch("ground_wheat", rng), -0.03, -0.03),
        at(stook("stook_a", rng), 0.13, 0.08),
        at(stook("stook_b", rng), 0.06, 0.14),
    ]


def potato_patch(name, rng):
    """Earthed-up potato ridges with leafy tops; tilts with the slope."""
    p = Part(name)
    slab(p, 0.16, 0.12, SOIL_DARK, y=0.003)
    for row in range(4):
        z = -0.045 + row * 0.03
        pts = [(-0.07, 0.0, z - 0.011), (0.07, 0.0, z - 0.011), (0.07, 0.0, z + 0.011), (-0.07, 0.0, z + 0.011),
               (-0.07, 0.012, z), (0.07, 0.012, z)]
        p.hull(pts, [[0, 1, 2, 3], [3, 2, 5, 4], [1, 0, 4, 5], [0, 3, 4], [2, 1, 5]], SOIL)
        for k in range(5):
            x = -0.056 + k * 0.028 + 0.004 * rng.random()
            ball(p, x, 0.008, z, 0.013, 0.022, [POTATO_LEAF, LEAF_DARK, LEAF_LIGHT], n=5, phase=rng.random())
    return p


def potato_heap(name, rng):
    p = Part(name)
    for i in range(9):
        x, z = -0.016 + 0.032 * rng.random(), -0.012 + 0.024 * rng.random()
        y = 0.004 + 0.006 * (i // 4)
        blobby(p, x, y, z, 0.007, 0.005, 0.006, POTATO, n=5, rings=2, phase=rng.random())
    return p


def potato():
    rng = random.Random(116)
    return [
        at(potato_patch("ground_potato", rng), -0.03, -0.03),
        at(potato_heap("heap", rng), 0.12, 0.1),
        at(sack("sack", POTATO), 0.17, 0.05),
    ]


def paddy(name, rng):
    """A flooded paddy: water inside low earth bunds, planted with rows of bright rice shoots; tilts with the
    slope (a terrace on a hill)."""
    p = Part(name)
    w, d = 0.18, 0.14
    box(p, -w / 2, FOOT / 2, -d / 2, w / 2, 0.004, d / 2, PADDY_WATER)
    for sz in (-1, 1):
        box(p, -w / 2 - 0.008, FOOT / 2, sz * d / 2 - 0.006, w / 2 + 0.008, 0.012, sz * d / 2 + 0.006, PADDY_BUND)
    for sx in (-1, 1):
        box(p, sx * w / 2 - 0.008, FOOT / 2, -d / 2, sx * w / 2 + 0.008, 0.012, d / 2, PADDY_BUND)
    box(p, -0.006, FOOT / 2, -d / 2, 0.006, 0.01, d / 2, PADDY_BUND)
    for row in range(5):
        for k in range(8):
            x = -0.078 + k * 0.0225
            if abs(x) < 0.01:
                continue
            z = -0.05 + row * 0.025
            cylinder(p, 0.005, 0.0, 0.024 + 0.006 * rng.random(), RICE_SHOOT if (k + row) % 2 else RICE_SHOOT_DARK,
                     at=(x, 0, z), n=4, r_top=0.0, phase=rng.random())
    return p


def rice():
    rng = random.Random(117)
    return [at(paddy("ground_paddy", rng), -0.02, -0.02), at(sack("sack", COTTON, r=0.014, h=0.026), 0.15, 0.11)]


def sugar_cane(name, rng, s=1.0):
    """A clump of tall, jointed canes with leaves flaring from the tops."""
    p = Part(name)
    for k in range(5):
        a = k * math.tau / 5 + rng.random()
        x, z = math.cos(a) * 0.012 * s, math.sin(a) * 0.012 * s
        h = (0.1 + 0.03 * rng.random()) * s
        joints = 2
        for j in range(joints):
            y0, y1 = h * j / joints, h * (j + 1) / joints
            lean = 0.004 * s
            beam(p, (x + lean * j, y0 if j else FOOT / 2, z), (x + lean * (j + 1), y1, z), 0.0055 * s,
                 SUGAR_CANE if j % 2 else SUGAR_CANE_DARK)
        for t in range(2):
            b = a + t * 3.1
            beam(p, (x + 0.016 * s, h, z), (x + 0.016 * s + math.cos(b) * 0.03 * s, h - 0.012 * s,
                                            z + math.sin(b) * 0.03 * s), 0.004 * s, SUGAR_LEAF)
    return p


def sugar():
    rng = random.Random(118)
    bundle = Part("bundle")
    for i in range(5):
        z = -0.012 + i * 0.006
        y = 0.005 + 0.006 * (i % 2)
        beam(bundle, (-0.04, y, z), (0.04, y, z), 0.006, SUGAR_CANE if i % 2 else SUGAR_CANE_DARK)
    return [
        at(sugar_cane("cane_a", rng, 1.15), -0.08, -0.05),
        at(sugar_cane("cane_b", rng, 1.0), 0.03, -0.09),
        at(sugar_cane("cane_c", rng, 1.05), 0.1, 0.0),
        at(sugar_cane("cane_d", rng, 0.9), -0.05, 0.07),
        at(bundle, 0.08, 0.13, 0.3),
    ]


def wine():
    """Two trellised vine rows heavy with grapes and a barrel."""
    rng = random.Random(119)
    parts = []
    for row, z in enumerate((-0.07, 0.01)):
        p = Part(f"ground_vines_{row}")
        slab(p, 0.24, 0.05, SOIL, y=0.003)
        for x in (-0.11, 0.0, 0.11):
            box(p, x - 0.004, 0.0, -0.004, x + 0.004, 0.06, 0.004, TIMBER)
        box(p, -0.11, 0.054, -0.002, 0.11, 0.058, 0.002, TIMBER_LIGHT)
        box(p, -0.104, 0.026, -0.014, 0.104, 0.058, 0.014, VINE_DARK if row else VINE, top=VINE)
        for k in range(8):
            x = -0.09 + k * 0.026 + 0.004 * rng.random()
            for sz in (-1, 1):
                cylinder(p, 0.008, 0.012, 0.03, GRAPE, at=(x, 0, sz * 0.014), n=5, r_top=0.004, phase=rng.random())
        parts.append(at(p, 0.0, z))
    barrel = Part("barrel")
    cylinder(barrel, 0.016, FOOT / 2, 0.02, srgb(0x8E5C32), n=8, r_top=0.019)
    cylinder(barrel, 0.019, 0.02, 0.04, srgb(0x8E5C32), n=8, r_top=0.016, top=srgb(0xB07A46))
    for y in (0.008, 0.032):
        cylinder(barrel, 0.0185, y - 0.002, y + 0.002, IRON, n=8)
    parts.append(at(barrel, 0.14, 0.12))
    return parts


def rubber_tree(name, rng, s=1.0, cup=True):
    """A tall rubber tree with pale bark, a spiral tapping scar and a cup catching white latex."""
    p = Part(name)
    top = 0.11 * s
    cylinder(p, 0.011 * s, FOOT / 2, top, RUBBER_BARK, n=7, r_top=0.008 * s)
    for k in range(3):
        y = 0.022 * s + k * 0.008 * s
        box(p, -0.004 * s, y, 0.009 * s, 0.006 * s, y + 0.003 * s, 0.0115 * s, SCAR)
    if cup:
        cylinder(p, 0.006 * s, 0.014 * s, 0.022 * s, srgb(0x8A6A4A), at=(0.0, 0, 0.014 * s), n=6, top=LATEX)
    leaves = [RUBBER_LEAF, RUBBER_LEAF_LIGHT, RUBBER_LEAF, LEAF_DARK]
    ball(p, 0.0, top - 0.02 * s, 0.0, 0.04 * s, 0.06 * s, leaves, n=7, phase=rng.random())
    ball(p, 0.018 * s, top - 0.034 * s, 0.01 * s, 0.026 * s, 0.036 * s, leaves, n=6, phase=rng.random())
    return p


def latex_sheets(name):
    p = Part(name)
    for i in range(4):
        y = 0.003 * i
        box(p, -0.02 + 0.002 * i, FOOT / 2 if i == 0 else y, -0.014, 0.02 + 0.002 * i, y + 0.003, 0.014,
            LATEX if i % 2 else srgb(0xE2DAC6))
    return p


def rubber():
    rng = random.Random(120)
    return [
        at(rubber_tree("tree_a", rng, 1.15), -0.07, -0.05),
        at(rubber_tree("tree_b", rng, 1.0, cup=False), 0.1, -0.04),
        at(latex_sheets("sheets"), 0.07, 0.1, 0.2),
    ]


# --- ruins and digs -------------------------------------------------------------------------------------------

def ziggurat(name, rng):
    """A stepped, overgrown stone shrine: three tiers, a small sanctuary on top and moss on the ledges."""
    p = Part(name)
    for k, (r, y0, y1) in enumerate(((0.07, FOOT, 0.022), (0.052, 0.022, 0.044), (0.036, 0.044, 0.066))):
        box(p, -r, y0, -r, r, y1, r, TEMPLE if k % 2 else TEMPLE_DARK, top=MOSS if k < 2 else TEMPLE_LIGHT)
    box(p, -0.022, 0.066, -0.022, 0.022, 0.094, 0.022, TEMPLE_LIGHT)
    box(p, -0.009, 0.066, 0.02, 0.009, 0.086, 0.024, DARK)
    prism_roof(p, 0.044, 0.044, 0.094, 0.02, 0.006, TEMPLE_DARK, TEMPLE_LIGHT)
    for k in range(5):  # stair down the front
        y = FOOT + k * 0.016
        z = 0.07 + 0.012 - k * 0.006
        box(p, -0.014, y, z - 0.016, 0.014, y + 0.016, z, TEMPLE_LIGHT)
    for sx, sz in ((-1, -1), (1, -1), (-1, 1)):  # moss tufts and creepers
        ball(p, sx * 0.06, 0.018, sz * 0.06, 0.014, 0.02, [MOSS, MOSS_DARK], n=5, phase=rng.random())
    return p


def broken_column(name, h):
    p = Part(name)
    box(p, -0.012, FOOT, -0.012, 0.012, 0.006, 0.012, TEMPLE_DARK)
    cylinder(p, 0.0085, 0.006, h, TEMPLE_LIGHT, n=7, top=TEMPLE)
    return p


def ancienttemple():
    rng = random.Random(121)
    lying = Part("column_fallen")
    log(lying, -0.03, 0.03, 0.008, 0.0, 0.0085, end=TEMPLE, bark=TEMPLE_LIGHT, n=7)
    return [
        at(ziggurat("temple", rng), -0.03, -0.04),
        at(broken_column("column_a", 0.06), 0.13, 0.04),
        at(broken_column("column_b", 0.034), 0.1, 0.12),
        at(lying, -0.07, 0.14, 0.5),
    ]


def dig_site(name, rng):
    """A shallow, roped dig: pale sandy earth with a skeleton half exposed; tilts with the slope."""
    p = Part(name)
    blob(p, 0.09, 0.07, DIG_DARK, rng, y=0.002, n=10)
    blob(p, 0.075, 0.055, DIG, rng, y=0.003, n=10)
    # spine and ribs
    for k in range(7):
        x = -0.04 + k * 0.011
        box(p, x - 0.004, 0.003, -0.003, x + 0.004, 0.009, 0.003, BONE)
        if 1 <= k <= 5:
            h = 0.018 + 0.006 * (k in (2, 3, 4))
            for sz in (-1, 1):
                beam(p, (x, 0.008, sz * 0.003), (x + 0.002, 0.004 + h * 0.4, sz * (0.018 + h * 0.4)), 0.003, BONE_SHADE)
    # skull at the +X end
    solid(p, [(0.04, 0.003, -0.01), (0.04, 0.003, 0.01), (0.07, 0.003, -0.006), (0.07, 0.003, 0.006),
              (0.044, 0.02, 0.0), (0.06, 0.014, 0.0)], BONE, BONE)
    box(p, 0.05, 0.012, 0.004, 0.056, 0.016, 0.009, DARK)
    # tail
    for k in range(4):
        x = -0.05 - k * 0.009
        box(p, x - 0.003, 0.003, -0.002 + 0.002 * k, x + 0.003, 0.007, 0.002 + 0.002 * k, BONE_SHADE)
    # ropes and pegs round the dig
    for x, z in ((-0.08, -0.06), (0.08, -0.06), (0.08, 0.06), (-0.08, 0.06)):
        box(p, x - 0.003, 0.0, z - 0.003, x + 0.003, 0.026, z + 0.003, TIMBER_LIGHT)
    for (x0, z0), (x1, z1) in (((-0.08, -0.06), (0.08, -0.06)), ((0.08, -0.06), (0.08, 0.06)),
                               ((0.08, 0.06), (-0.08, 0.06)), ((-0.08, 0.06), (-0.08, -0.06))):
        beam(p, (x0, 0.022, z0), (x1, 0.022, z1), 0.0025, CLOTH_RED)
    return p


def fossils():
    rng = random.Random(122)
    tools = Part("tools")
    box(tools, -0.012, FOOT / 2, -0.008, 0.012, 0.014, 0.008, BOARDS, top=BOARDS_DARK)
    beam(tools, (0.016, 0.0, 0.0), (0.02, 0.05, -0.004), 0.004, TIMBER_LIGHT)
    box(tools, 0.012, 0.0, -0.006, 0.026, 0.004, 0.004, IRON)
    return [at(dig_site("ground_dig", rng), -0.02, -0.02), at(tools, 0.15, 0.1, 0.3)]


MODELS = {
    "apple": apple,
    "banana": banana,
    "lemon": lemon,
    "olives": olives,
    "cannabis": cannabis,
    "coffee": coffee,
    "cotton": cotton,
    "dye": dye,
    "incense": incense,
    "spices": spices,
    "silk": silk,
    "tea": tea,
    "tobacco": tobacco,
    "corn": corn,
    "wheat": wheat,
    "potato": potato,
    "rice": rice,
    "sugar": sugar,
    "wine": wine,
    "rubber": rubber,
    "ancienttemple": ancienttemple,
    "fossils": fossils,
}
