"""Farms, pastures, orchards, plantations, wineries, wells and other food and plantation improvements."""
import random

from .kit import *  # noqa: F401,F403
from .kit import FOOT, Part, at

GRAIN_GOLD = srgb(0xD4B04C)
GRAIN_GOLD_DARK = srgb(0xBA963C)
GRAIN_RIPE = srgb(0xB4B04A)
GRAIN_RIPE_DARK = srgb(0x9A9A3E)
GRAIN_YOUNG = srgb(0x86B046)
GRAIN_YOUNG_DARK = srgb(0x6E9A3A)
SOIL_DARK = srgb(0x4E3824)
BANK = srgb(0x7E6646)
LEAF = srgb(0x4F8030)
LEAF_DARK = srgb(0x3C6626)
LEAF_LIGHT = srgb(0x679A3A)
APPLE = srgb(0xC8382A)
MOWN = srgb(0x7BA04E)
BASKET = srgb(0xB08850)
BUSH = srgb(0x3E6C2A)
BUSH_LIGHT = srgb(0x5A8C34)
COTTON = srgb(0xF4F0E6)
BERRY = srgb(0xA8352B)
BANANA = srgb(0x93B83E)
BANANA_DARK = srgb(0x6F9530)
WHITEWASH = srgb(0xF0EBDF)
SHUTTER = srgb(0x3E5B3A)
TOBACCO = srgb(0x9A7438)
VINE = srgb(0x5E8C34)
VINE_DARK = srgb(0x4A7429)
GRAPE = srgb(0x55305A)
LIMESTONE = srgb(0xD3C29E)
LIMESTONE_DARK = srgb(0xB3A07C)
BARREL = srgb(0x8E5C32)
BARREL_END = srgb(0xB07A46)
DOME = srgb(0xC4ECF0)
DOME_SHADE = srgb(0xA6DAE0)
DOME_GREEN = srgb(0x8CCBA0)
DOME_GREEN_DARK = srgb(0x6FB585)
COMPOSITE = srgb(0xEEF0F1)
COMPOSITE_GREY = srgb(0xC6CBD0)
TEAL = srgb(0x2BA9AC)
CONCRETE = srgb(0xBAB6AD)
CONCRETE_DARK = srgb(0x8F8B83)
STEEL = srgb(0x8D9399)
STEEL_DARK = srgb(0x5F656B)
TANK_BLUE = srgb(0x86AFC6)
IRRIGATED = srgb(0x5FA238)
IRRIGATED_DARK = srgb(0x4C8A2C)
OIL = srgb(0x1C1815)
OIL_SHEEN = srgb(0x2C2622)
DERRICK = srgb(0x6B4A34)
DERRICK_LIGHT = srgb(0x8A6244)
TANK = srgb(0xCBC6BA)
TANK_BAND = srgb(0x8E3B2C)
PUMP = srgb(0xC99A2E)
GRAVEL = srgb(0x8A8378)


def tube(part, a, b, r, colour, n=6, end=None):
    """A round bar from point a to point b."""
    a, b = np.asarray(a, dtype=float), np.asarray(b, dtype=float)
    axis = (b - a) / np.linalg.norm(b - a)
    side = np.cross(axis, (0, 1, 0) if abs(axis[1]) < 0.9 else (1, 0, 0))
    side /= np.linalg.norm(side)
    up = np.cross(side, axis)
    ring = [(side * math.cos(k * math.tau / n) + up * math.sin(k * math.tau / n)) * r for k in range(n)]
    pts = [a + c for c in ring] + [b + c for c in ring]
    faces = [list(range(n)), list(range(n, 2 * n))] + [[k, (k + 1) % n, n + (k + 1) % n, n + k] for k in range(n)]
    part.hull(pts, faces, [end or colour, end or colour] + [colour] * n)


def ball(part, x, y, z, r, h, colours, n=6, phase=0.0):
    """A faceted crown or bush: a point below, two staggered rings and a point on top; r wide and h tall."""
    pts = [(x, y, z)]
    for ring, (t, s) in enumerate(((0.32, 1.0), (0.7, 0.85))):
        for i in range(n):
            a = phase + (i + 0.5 * ring) * math.tau / n
            pts.append((x + math.cos(a) * r * s, y + h * t, z + math.sin(a) * r * s))
    pts.append((x, y + h, z))
    centre = (x, y + h * 0.5, z)
    top = len(pts) - 1
    k = 0
    for i in range(n):
        i1 = (i + 1) % n
        part.tri(pts[0], pts[1 + i], pts[1 + i1], colours[k % len(colours)], centre)
        part.tri(pts[1 + i], pts[1 + n + i], pts[1 + i1], colours[(k + 1) % len(colours)], centre)
        part.tri(pts[1 + i1], pts[1 + n + i], pts[1 + n + i1], colours[(k + 2) % len(colours)], centre)
        part.tri(pts[1 + n + i], pts[top], pts[1 + n + i1], colours[(k + 3) % len(colours)], centre)
        k += 1


def blob(part, rx, rz, colour, rng, at=(0, 0, 0), y=0.003, n=10):
    """A flat irregular stain or puddle, fanned from its centre."""
    cx, _, cz = at
    pts = []
    for i in range(n):
        a = (i + 0.3 * rng.random()) * math.tau / n
        j = 0.55 + 0.45 * rng.random()
        pts.append((cx + math.cos(a) * rx * j, cz + math.sin(a) * rz * j))
    centre_top = (cx, y, cz)
    below = (cx, FOOT, cz)
    for i in range(n):
        (x0, z0), (x1, z1) = pts[i], pts[(i + 1) % n]
        part.tri(centre_top, (x0, y, z0), (x1, y, z1), colour, below)
        part.quad((x0, FOOT / 2, z0), (x1, FOOT / 2, z1), (x1, y, z1), (x0, y, z0), colour, (cx, y / 2, cz))


def slab(part, w, d, colour, y=0.005, x=0.0, z=0.0, top=None):
    """A ground plate centred at (x, z) that reaches a little below the surface."""
    box(part, x - w / 2, FOOT / 2, z - d / 2, x + w / 2, y, z + d / 2, colour, top=top)


# --- Pasture ---------------------------------------------------------------------------------------------------


def stable(name, w=0.22, d=0.12, wall=0.07):
    """A long, low timber stable: board walls, wide dark stall doors on the front, a shingled roof and hay loft."""
    p = Part(name)
    hw, hd = w / 2, d / 2
    box(p, -hw - 0.006, FOOT, -hd - 0.006, hw + 0.006, 0.01, hd + 0.006, STONE_DARK, top=STONE)
    box(p, -hw, 0.01, -hd, hw, wall, hd, BOARDS)
    n = 4
    for i in range(n):  # stall doors, split stable-door style
        x = -hw + (i + 0.5) * w / n
        box(p, x - 0.018, 0.01, hd - 0.002, x + 0.018, 0.058, hd + 0.003, BOARDS_DARK)
        box(p, x - 0.014, 0.036, hd + 0.002, x + 0.014, 0.054, hd + 0.004, DARK)
    for x in np.linspace(-hw, hw, n + 1):  # posts between stalls
        box(p, x - 0.004, 0.01, hd - 0.002, x + 0.004, wall, hd + 0.005, TIMBER)
    top = prism_roof(p, w, d, wall, 0.06, 0.018, SHINGLE, BOARDS)
    box(p, -hw - 0.018, top - 0.008, -0.008, hw + 0.018, top + 0.004, 0.008, SHINGLE_DARK)
    # hay loft door in the gable end
    box(p, hw - 0.002, wall + 0.004, -0.014, hw + 0.004, wall + 0.032, 0.014, HAY)
    return p


def horse(name, coat=HORSE_BAY, mane=HORSE_DARK, grazing=False):
    """A small low-poly horse facing +X."""
    p = Part(name)
    body = [(-0.034, 0.042, -0.012), (0.03, 0.042, -0.012), (0.03, 0.042, 0.012), (-0.034, 0.042, 0.012),
            (-0.036, 0.068, -0.014), (0.032, 0.07, -0.014), (0.032, 0.07, 0.014), (-0.036, 0.068, 0.014)]
    p.hull(body, BOX_FACES, coat)
    for sx in (-0.026, 0.022):
        for sz in (-0.008, 0.008):
            beam(p, (sx, 0.046, sz), (sx + 0.002, 0.0, sz), 0.007, coat)
            box(p, sx - 0.003, 0.0, sz - 0.004, sx + 0.005, 0.008, sz + 0.004, HORSE_DARK)
    if grazing:
        neck_top, head_tip = (0.05, 0.03, 0.0), (0.058, 0.006, 0.0)
    else:
        neck_top, head_tip = (0.048, 0.098, 0.0), (0.07, 0.082, 0.0)
    beam(p, (0.024, 0.062, 0.0), neck_top, 0.016, coat)
    beam(p, neck_top, head_tip, 0.013, coat)
    beam(p, (0.022, 0.074, 0.0), (neck_top[0] - 0.004, neck_top[1] + 0.006, 0.0), 0.006, mane)
    beam(p, (-0.036, 0.066, 0.0), (-0.046, 0.03, 0.0), 0.006, mane)
    return p


def paddock(name, w=0.26, d=0.17, posts=(7, 5)):
    """Worn ground inside a post-and-rail fence; tilts with the slope."""
    p = Part(name)
    hw, hd = w / 2, d / 2
    box(p, -hw, FOOT / 2, -hd, hw, 0.003, hd, PADDOCK)
    for xs, zs in ((np.linspace(-hw, hw, posts[0]), (-hd, hd)), ((-hw, hw), np.linspace(-hd, hd, posts[1]))):
        for x in xs:
            for z in zs:
                box(p, x - 0.004, 0.0, z - 0.004, x + 0.004, 0.036, z + 0.004, TIMBER_LIGHT)
    for y in (0.016, 0.03):
        for z in (-hd, hd):
            box(p, -hw, y - 0.003, z - 0.002, hw, y + 0.003, z + 0.002, TIMBER_LIGHT)
        for x in (-hw, hw):
            box(p, x - 0.002, y - 0.003, -hd, x + 0.002, y + 0.003, hd, TIMBER_LIGHT)
    return p


def trough(name):
    p = Part(name)
    box(p, -0.03, FOOT / 2, -0.012, 0.03, 0.022, 0.012, BOARDS_DARK)
    box(p, -0.026, 0.022, -0.008, 0.026, 0.023, 0.008, WATER)
    return p


def pasture():
    rng = random.Random(61)
    return [
        at(stable("stable"), -0.12, -0.42, 0.1),
        at(paddock("ground_paddock"), 0.08, 0.16, -0.12),
        at(horse("horse"), -0.06, 0.08, 0.5),
        at(horse("horse_b", coat=HORSE_GREY, mane=HORSE_DARK, grazing=True), 0.2, 0.26, 2.6),
        at(horse("horse_c", coat=HORSE_DARK, mane=HORSE_DARK, grazing=True), 0.2, 0.08, -0.4),
        at(trough("trough"), -0.26, 0.24, 1.4),
        at(haystack("haystack"), 0.42, -0.34),
        at(haystack("haystack_b", r=0.03), 0.54, -0.24),
    ]


# --- Farm (ancient) --------------------------------------------------------------------------------------------


def field(name, w, d, rows, light, dark, height=0.018):
    """Crop rows along X on bare soil; tilts with the slope."""
    p = Part(name)
    hw, hd = w / 2, d / 2
    slab(p, w, d, SOIL)
    pitch = d / rows
    for i in range(rows):
        z = -hd + (i + 0.5) * pitch
        box(p, -hw + 0.006, 0.005, z - pitch * 0.36, hw - 0.006, height - 0.003 * (i % 2), z + pitch * 0.36,
            dark if i % 2 else light)
    return p


def ditch(name, length, w=0.016):
    """An irrigation channel: water between two raised earth banks; tilts with the slope."""
    p = Part(name)
    hl = length / 2
    box(p, -hl, FOOT / 2, -w / 2, hl, 0.004, w / 2, WATER)
    for sz in (-1, 1):
        box(p, -hl, FOOT / 2, sz * w / 2 - (0.009 if sz < 0 else 0), hl, 0.009,
            sz * w / 2 + (0.009 if sz > 0 else 0), BANK)
    return p


def barn(name, w=0.13, d=0.09, wall=0.06):
    """A small timber barn with a thatched roof and big double doors."""
    p = Part(name)
    hw, hd = w / 2, d / 2
    box(p, -hw - 0.006, FOOT, -hd - 0.006, hw + 0.006, 0.01, hd + 0.006, STONE_DARK, top=STONE)
    box(p, -hw, 0.01, -hd, hw, wall, hd, BOARDS)
    for sx in (-1, 1):
        box(p, sx * hw - 0.004, 0.01, hd - 0.002, sx * hw + 0.004, wall, hd + 0.004, TIMBER)
    box(p, -0.026, 0.01, hd - 0.002, 0.026, 0.054, hd + 0.003, BOARDS_DARK)
    box(p, -0.002, 0.01, hd + 0.002, 0.002, 0.054, hd + 0.005, TIMBER)
    top = prism_roof(p, w, d, wall, 0.055, 0.014, THATCH, BOARDS)
    box(p, -hw - 0.012, top - 0.01, -0.008, hw + 0.012, top + 0.004, 0.008, THATCH_DARK)
    return p


def granary(name):
    """A round wattle granary on a stone base under a conical thatch cap."""
    p = Part(name)
    cylinder(p, 0.034, FOOT, 0.016, STONE_DARK, n=8, top=STONE)
    cylinder(p, 0.028, 0.016, 0.07, PLASTER_WARM, n=8)
    box(p, -0.009, 0.03, 0.022, 0.009, 0.056, 0.031, DOOR)
    cylinder(p, 0.04, 0.066, 0.12, THATCH, n=8, r_top=0.0, phase=0.2)
    return p


def farm():
    return [
        at(field("ground_field_wheat", 0.19, 0.15, 6, GRAIN_GOLD, GRAIN_GOLD_DARK, 0.022), -0.38, -0.3),
        at(field("ground_field_young", 0.19, 0.15, 6, GRAIN_YOUNG, GRAIN_YOUNG_DARK, 0.014), 0.02, -0.36, math.pi / 2),
        at(field("ground_field_ripe", 0.19, 0.16, 6, GRAIN_RIPE, GRAIN_RIPE_DARK, 0.02), -0.38, 0.3, math.pi / 2),
        at(field("ground_field_gold", 0.19, 0.16, 6, GRAIN_GOLD, GRAIN_GOLD_DARK, 0.022), 0.02, 0.36),
        at(field("ground_field_green", 0.17, 0.16, 6, CROP, CROP_DARK, 0.016), 0.42, 0.28, math.pi / 2),
        at(ditch("ground_ditch_w", 0.27), -0.32, -0.02),
        at(ditch("ground_ditch_e", 0.27), 0.3, -0.02),
        at(barn("barn"), 0.38, -0.36, -0.15),
        at(granary("granary"), 0.62, -0.12),
    ]


# --- Orchard ---------------------------------------------------------------------------------------------------


def grove(name, spots, rng, fruit=APPLE, w=0.2, d=0.1):
    """Fruit trees on a mown strip; spots are tree positions in authored units. Tilts with the slope, a little."""
    p = Part(name)
    slab(p, w, d, MOWN, y=0.003)
    for x, z in spots:
        s = 0.9 + 0.2 * rng.random()
        box(p, x - 0.005, 0.0, z - 0.005, x + 0.005, 0.04 * s, z + 0.005, TIMBER)
        ball(p, x, 0.026 * s, z, 0.036 * s, 0.062 * s, [LEAF, LEAF_DARK, LEAF_LIGHT, LEAF_DARK], n=6,
             phase=rng.random())
        for a in (0.6 + rng.random(), 2.4 + rng.random()):  # two fruits on the camera side
            fx, fz = x + math.cos(a) * 0.032 * s, z + math.sin(a) * 0.032 * s
            box(p, fx - 0.005, 0.044 * s, fz - 0.005, fx + 0.005, 0.054 * s, fz + 0.005, fruit)
    return p


def fruit_shed(name, w=0.1, d=0.07):
    """An open-fronted timber shed with crates of fruit inside."""
    p = Part(name)
    hw, hd = w / 2, d / 2
    box(p, -hw - 0.004, FOOT, -hd - 0.004, hw + 0.004, 0.008, hd + 0.004, STONE_DARK, top=STONE)
    box(p, -hw, 0.008, -hd, hw, 0.062, -hd + 0.008, BOARDS)
    for sx in (-1, 1):
        box(p, sx * hw - 0.004, 0.008, -hd, sx * hw + 0.004, 0.062, hd, BOARDS)
    pts = [(-hw - 0.01, 0.07, -hd - 0.012), (hw + 0.01, 0.07, -hd - 0.012), (hw + 0.01, 0.054, hd + 0.016),
           (-hw - 0.01, 0.054, hd + 0.016)]
    p.hull(pts + [(x, y + 0.008, z) for x, y, z in pts], BOX_FACES, [THATCH_DARK, THATCH] + [THATCH_DARK] * 4)
    for x in (-0.024, 0.0, 0.024):
        box(p, x - 0.01, 0.008, -0.006, x + 0.01, 0.026, 0.014, BOARDS_DARK, top=APPLE)
    box(p, -0.012, 0.026, -0.006, 0.008, 0.044, 0.014, BOARDS_DARK, top=APPLE)
    return p


def fruit_crates(name):
    """Stacked crates and round baskets of picked fruit."""
    p = Part(name)
    for x, z, y in ((-0.02, 0.0, 0.0), (0.004, 0.0, 0.0), (-0.008, 0.002, 0.02)):
        box(p, x - 0.011, y if y else FOOT / 2, z - 0.009, x + 0.011, y + 0.02, z + 0.009, BOARDS, top=APPLE)
    for x, z in ((0.034, 0.012), (0.03, -0.02)):
        cylinder(p, 0.011, FOOT / 2, 0.016, BASKET, at=(x, 0, z), n=7, r_top=0.013, top=APPLE)
    return p


def orchard():
    rng = random.Random(19)
    row4 = [(-0.12, 0.0), (-0.04, 0.0), (0.04, 0.0), (0.12, 0.0)]
    pair = [(-0.045, 0.0), (0.045, 0.0)]
    return [
        at(grove("ground_grove_back", row4, rng, w=0.3, d=0.06), -0.02, -0.5),
        at(grove("ground_grove_w", pair, rng, w=0.15, d=0.06), -0.5, -0.18),
        at(grove("ground_grove_sw", pair, rng, w=0.15, d=0.06), -0.46, 0.2),
        at(grove("ground_grove_s", pair, rng, w=0.15, d=0.06), -0.16, 0.5),
        at(grove("ground_grove_se", pair, rng, w=0.15, d=0.06), 0.32, 0.44),
        at(fruit_shed("shed"), 0.48, -0.2, -0.35),
        at(fruit_crates("crates"), 0.56, 0.12, 0.3),
    ]


# --- Plantation ------------------------------------------------------------------------------------------------


def bush_rows(name, rows, per_row, light, dark, top, w=0.19, d=0.14, r=0.016, h=0.03):
    """Neat rows of bushes on dark soil; tilts with the slope."""
    p = Part(name)
    slab(p, w, d, SOIL_DARK)
    for i in range(rows):
        z = -d / 2 + (i + 0.5) * d / rows
        for k in range(per_row):
            x = -w / 2 + (k + 0.5) * w / per_row + (0.006 if i % 2 else -0.006)
            ball(p, x, 0.0, z, r, h, [light, dark, top, dark], n=5, phase=0.3 * k)
    return p


def plantation_house(name, w=0.17, d=0.1, wall=0.1):
    """A two-storey whitewashed house with a hipped slate roof and a columned veranda on the front."""
    p = Part(name)
    hw, hd = w / 2, d / 2
    box(p, -hw - 0.006, FOOT, -hd - 0.006, hw + 0.006, 0.016, hd + 0.05, STONE_DARK, top=STONE)
    box(p, -hw, 0.016, -hd, hw, wall, hd, WHITEWASH)
    for y0, xs in ((0.03, (-0.056, 0.056)), (0.066, (-0.056, 0.0, 0.056))):
        for x in xs:
            box(p, x - 0.015, y0, hd - 0.002, x + 0.015, y0 + 0.022, hd + 0.002, SHUTTER)
            box(p, x - 0.008, y0, hd + 0.001, x + 0.008, y0 + 0.022, hd + 0.004, WINDOW)
    box(p, -0.011, 0.016, hd - 0.002, 0.011, 0.05, hd + 0.004, DOOR)
    # veranda: board deck, slim white columns and a flat roof at the eaves
    box(p, -hw - 0.004, 0.016, hd, hw + 0.004, 0.02, hd + 0.046, BOARDS)
    for x in np.linspace(-hw + 0.002, hw - 0.002, 6):
        box(p, x - 0.004, 0.02, hd + 0.036, x + 0.004, wall - 0.004, hd + 0.044, WHITEWASH)
    box(p, -hw - 0.008, wall - 0.004, hd - 0.002, hw + 0.008, wall + 0.004, hd + 0.052, SLATE_DARK, top=SLATE)
    top = prism_roof(p, w, d, wall, 0.055, 0.012, SLATE, WHITEWASH, hip=0.045)
    for sx in (-1, 1):
        box(p, sx * 0.05 - 0.01, wall, -0.01, sx * 0.05 + 0.01, top + 0.01, 0.006, TILE_DARK, top=STONE_DARK)
    return p


def drying_shed(name, w=0.13, d=0.07, h=0.055):
    """An open, long-roofed shed with crop hung to dry under it."""
    p = Part(name)
    hw, hd = w / 2, d / 2
    slab(p, w + 0.01, d + 0.01, PADDOCK, y=0.004)
    for x in np.linspace(-hw, hw, 4):
        for sz in (-1, 1):
            box(p, x - 0.004, 0.0, sz * hd - 0.004, x + 0.004, h, sz * hd + 0.004, TIMBER)
    for z in (-0.018, 0.0, 0.018):
        box(p, -hw + 0.008, 0.024, z - 0.005, hw - 0.008, h - 0.004, z + 0.005, TOBACCO)
    prism_roof(p, w, d, h, 0.04, 0.014, THATCH, THATCH_DARK)
    return p


def plantation():
    return [
        at(plantation_house("house"), -0.12, -0.42),
        at(drying_shed("drying_shed"), 0.46, -0.24, -0.4),
        at(bush_rows("ground_coffee", 3, 5, BUSH_LIGHT, BUSH, BERRY), -0.46, 0.04),
        at(bush_rows("ground_cotton", 3, 5, BUSH_LIGHT, BUSH, COTTON), -0.12, 0.42),
        at(bush_rows("ground_banana", 3, 4, BANANA, BANANA_DARK, BANANA, r=0.02, h=0.05), 0.36, 0.3),
    ]


# --- Winery ----------------------------------------------------------------------------------------------------


def vineyard(name, rows=4, w=0.2, d=0.14):
    """Trellised vine rows: stakes and a green leaf hedge with grapes underneath; tilts with the slope."""
    p = Part(name)
    slab(p, w, d, SOIL)
    hw = w / 2 - 0.008
    for i in range(rows):
        z = -d / 2 + (i + 0.5) * d / rows
        for x in (-hw, 0.0, hw):
            box(p, x - 0.003, 0.0, z - 0.003, x + 0.003, 0.04, z + 0.003, TIMBER)
        box(p, -hw + 0.004, 0.012, z - 0.007, hw - 0.004, 0.022, z + 0.007, GRAPE)
        box(p, -hw + 0.002, 0.022, z - 0.009, hw - 0.002, 0.038, z + 0.009, VINE if i % 2 else VINE_DARK,
            top=VINE)
    return p


def winery_house(name, w=0.15, d=0.09, wall=0.075):
    """A pale stone winery with a terracotta roof, a big arched cellar door and a low side wing."""
    p = Part(name)
    hw, hd = w / 2, d / 2
    box(p, -hw - 0.006, FOOT, -hd - 0.006, hw + 0.006, 0.01, hd + 0.006, STONE_DARK, top=STONE)
    box(p, -hw, 0.01, -hd, hw, wall, hd, LIMESTONE)
    box(p, -0.02, 0.01, hd - 0.002, 0.02, 0.05, hd + 0.003, DOOR)
    pts = [(-0.02, 0.05, hd - 0.002), (0.02, 0.05, hd - 0.002), (0.0, 0.062, hd - 0.002)]
    p.hull(pts + [(x, y, z + 0.005) for x, y, z in pts], [[0, 1, 2], [3, 4, 5], [0, 1, 4, 3], [1, 2, 5, 4],
                                                         [2, 0, 3, 5]], DOOR)
    for x in (-0.05, 0.05):
        box(p, x - 0.008, 0.035, hd - 0.002, x + 0.008, 0.055, hd + 0.003, WINDOW)
    for sx in (-1, 1):  # quoins
        box(p, sx * hw - 0.005, 0.01, hd - 0.005, sx * hw + 0.005, wall, hd + 0.003, LIMESTONE_DARK)
    top = prism_roof(p, w, d, wall, 0.05, 0.012, TILE, LIMESTONE)
    box(p, -hw - 0.012, top - 0.008, -0.007, hw + 0.012, top + 0.004, 0.007, TILE_DARK)
    # low wing at the back of the -X end, ridge along Z
    box(p, -hw - 0.05, FOOT, -hd - 0.006, -hw + 0.002, 0.01, hd - 0.02, STONE_DARK, top=STONE)
    box(p, -hw - 0.044, 0.01, -hd, -hw, 0.05, hd - 0.026, LIMESTONE)
    prism_roof(p, 0.064, 0.044, 0.05, 0.03, 0.01, TILE, LIMESTONE, at=(-hw - 0.022, 0, -0.016),
               yaw=math.pi / 2)
    return p


def barrels(name):
    """Two barrels on a rack and two standing on end."""
    p = Part(name)
    for z in (-0.012, 0.012):
        box(p, -0.03, FOOT / 2, z - 0.005, 0.03, 0.006, z + 0.005, TIMBER)
    for x in (-0.017, 0.017):
        tube(p, (x, 0.022, -0.026), (x, 0.022, 0.026), 0.016, BARREL, n=8, end=BARREL_END)
    for x, z in ((0.052, 0.012), (0.05, -0.022)):
        cylinder(p, 0.015, FOOT / 2, 0.02, BARREL, at=(x, 0, z), n=8, r_top=0.017)
        cylinder(p, 0.017, 0.02, 0.04, BARREL, at=(x, 0, z), n=8, r_top=0.015, top=BARREL_END)
    return p


def winery():
    return [
        at(winery_house("winery"), 0.2, -0.4, -0.1),
        at(barrels("barrels"), 0.52, -0.06, -0.3),
        at(vineyard("ground_vines_w", w=0.2, d=0.15), -0.4, -0.24),
        at(vineyard("ground_vines_sw", w=0.2, d=0.15), -0.32, 0.3),
        at(vineyard("ground_vines_se", w=0.2, d=0.15), 0.16, 0.42),
        at(vineyard("ground_vines_e", rows=3, w=0.12, d=0.12), 0.52, 0.2),
    ]


# --- Dome farm (transhuman) ------------------------------------------------------------------------------------


def dome(name, r, h=None, n=10):
    """A faceted glass dome on a white ring: pale cyan panels high up, green-tinted ones low down where the crops
    show through."""
    p = Part(name)
    h = h or r * 0.85
    cylinder(p, r * 1.08, FOOT, 0.012, COMPOSITE_GREY, n=n, top=COMPOSITE)
    cylinder(p, r * 1.085, 0.004, 0.009, TEAL, n=n)
    rings = 3
    pts = [(0.0, 0.012 + h, 0.0)]
    for ring in range(rings):
        t = (ring + 1) / rings
        y = 0.012 + h * math.cos(t * math.pi / 2)
        s = math.sin(t * math.pi / 2)
        for i in range(n):
            a = (i + 0.5 * ring) * math.tau / n
            pts.append((math.cos(a) * r * s, y, math.sin(a) * r * s))
    centre = (0.0, 0.012 + h * 0.3, 0.0)
    for i in range(n):
        p.tri(pts[0], pts[1 + i], pts[1 + (i + 1) % n], DOME if i % 2 else DOME_SHADE, centre)
    for ring in range(rings - 1):
        a0, b0 = 1 + ring * n, 1 + (ring + 1) * n
        low = ring == rings - 2
        for i in range(n):
            i1 = (i + 1) % n
            c1 = (DOME_GREEN if i % 2 else DOME_GREEN_DARK) if low else DOME_SHADE
            c2 = DOME_GREEN_DARK if low else DOME
            p.tri(pts[a0 + i], pts[b0 + i], pts[a0 + i1], c1, centre)
            p.tri(pts[a0 + i1], pts[b0 + i], pts[b0 + i1], c2, centre)
    return p


def dome_tube(name, length, r=0.012):
    """A white connecting tube along X with a teal band; tilts with the slope."""
    p = Part(name)
    hl = length / 2
    box(p, -hl * 0.6, FOOT / 2, -0.008, hl * 0.6, 0.006, 0.008, COMPOSITE_GREY)
    tube(p, (-hl, r + 0.004, 0), (hl, r + 0.004, 0), r, COMPOSITE, n=6)
    tube(p, (-0.012, r + 0.004, 0), (0.012, r + 0.004, 0), r * 1.15, TEAL, n=6)
    return p


def service_block(name, w=0.1, d=0.07, h=0.055):
    """A white composite service building with a window band, a teal strip and a slim mast."""
    p = Part(name)
    hw, hd = w / 2, d / 2
    box(p, -hw - 0.004, FOOT, -hd - 0.004, hw + 0.004, 0.008, hd + 0.004, COMPOSITE_GREY)
    box(p, -hw, 0.008, -hd, hw, h, hd, COMPOSITE)
    box(p, -hw - 0.002, h - 0.004, -hd - 0.002, hw + 0.002, h + 0.006, hd + 0.002, COMPOSITE_GREY)
    box(p, -hw + 0.008, 0.03, hd - 0.002, hw - 0.008, 0.042, hd + 0.003, WINDOW)
    box(p, -hw - 0.001, 0.016, hd - 0.002, hw + 0.001, 0.021, hd + 0.004, TEAL)
    box(p, -0.012, 0.008, hd - 0.002, 0.012, 0.026, hd + 0.003, STEEL_DARK)
    cylinder(p, 0.003, h, h + 0.09, COMPOSITE_GREY, at=(hw - 0.016, 0, -hd + 0.016), n=4)
    cylinder(p, 0.006, h + 0.09, h + 0.1, TEAL, at=(hw - 0.016, 0, -hd + 0.016), n=6)
    return p


def hydro_beds(name, w=0.2, d=0.1, rows=4):
    """Raised hydroponic beds: green strips in white composite troughs with a teal feed line; tilts with the slope."""
    p = Part(name)
    slab(p, w, d, COMPOSITE_GREY, y=0.004)
    for i in range(rows):
        z = -d / 2 + (i + 0.5) * d / rows
        box(p, -w / 2 + 0.008, 0.004, z - 0.009, w / 2 - 0.008, 0.012, z + 0.009, COMPOSITE)
        box(p, -w / 2 + 0.012, 0.012, z - 0.006, w / 2 - 0.012, 0.02, z + 0.006, GRAIN_YOUNG if i % 2 else CROP)
    box(p, -w / 2 - 0.002, 0.004, -d / 2 - 0.002, -w / 2 + 0.006, 0.016, d / 2 + 0.002, TEAL)
    return p


def dome_farm():
    return [
        at(dome("dome_big", 0.1), -0.28, -0.32),
        at(dome("dome_east", 0.075), 0.38, -0.36),
        at(dome("dome_west", 0.07), -0.46, 0.3),
        at(dome_tube("ground_tube_n", 0.2), 0.06, -0.34),
        at(dome_tube("ground_tube_w", 0.16), -0.4, 0.0, math.pi / 2 - 0.3),
        at(dome_tube("ground_tube_e", 0.2), 0.42, -0.02, math.pi / 2),
        at(service_block("service"), 0.42, 0.3, -0.2),
        at(hydro_beds("ground_beds"), -0.02, 0.48),
    ]


# --- Groundwater well (modern) ---------------------------------------------------------------------------------


def pump_house(name, w=0.09, d=0.07, h=0.06):
    """A flat-roofed concrete pump house with a steel door and a pipe stub."""
    p = Part(name)
    hw, hd = w / 2, d / 2
    box(p, -hw - 0.004, FOOT, -hd - 0.004, hw + 0.004, 0.008, hd + 0.004, CONCRETE_DARK)
    box(p, -hw, 0.008, -hd, hw, h, hd, CONCRETE)
    box(p, -hw - 0.006, h, -hd - 0.006, hw + 0.006, h + 0.008, hd + 0.006, CONCRETE_DARK)
    box(p, -0.03, 0.008, hd - 0.002, -0.008, 0.048, hd + 0.003, STEEL_DARK)
    box(p, 0.006, 0.032, hd - 0.002, 0.034, 0.046, hd + 0.003, WINDOW)
    cylinder(p, 0.008, h + 0.008, h + 0.026, STEEL, at=(0.02, 0, -0.012), n=6, top=STEEL_DARK)
    tube(p, (hw - 0.004, 0.016, 0.014), (hw + 0.03, 0.016, 0.014), 0.007, STEEL, n=6)
    return p


def water_tower(name, r=0.042, legs=0.15):
    """A steel water tower: four legs, cross braces, a blue tank and a conical cap."""
    p = Part(name)
    base, top = 0.032, 0.022
    feet = [(sx * base, FOOT, sz * base) for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    heads = [(sx * top, legs, sz * top) for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    for f in feet:
        box(p, f[0] - 0.008, FOOT, f[2] - 0.008, f[0] + 0.008, 0.008, f[2] + 0.008, CONCRETE)
    for f, t in zip(feet, heads):
        beam(p, f, t, 0.007, STEEL)
    mid = [(x * 0.85, legs * 0.5, z * 0.85) for x, _, z in feet]
    for i in range(4):
        beam(p, mid[i], mid[(i + 1) % 4], 0.004, STEEL_DARK)
        beam(p, (feet[i][0] * 0.95, 0.01, feet[i][2] * 0.95), mid[(i + 1) % 4], 0.003, STEEL_DARK)
    tube(p, (0, 0.0, 0), (0, legs, 0), 0.006, STEEL_DARK, n=5)
    cylinder(p, r, legs, legs + 0.06, TANK_BLUE, n=10)
    cylinder(p, r * 1.02, legs + 0.045, legs + 0.052, STEEL_DARK, n=10)
    cylinder(p, r * 1.04, legs + 0.06, legs + 0.085, COMPOSITE_GREY, n=10, r_top=0.0)
    return p


def pivot_field(name, r=0.11, n=12):
    """A round centre-pivot field: green crop rings round a pivot with a long sprinkler arm; tilts with the slope."""
    p = Part(name)
    for radius, y, colour in ((r, 0.004, IRRIGATED), (r * 0.66, 0.008, IRRIGATED_DARK), (r * 0.33, 0.012,
                                                                                         IRRIGATED)):
        cylinder(p, radius, FOOT / 2, y, colour, n=n, phase=math.pi / n)
    cylinder(p, 0.008, 0.0, 0.04, STEEL_DARK, n=6)
    beam(p, (0.0, 0.036, 0.0), (r * 0.95, 0.03, 0.0), 0.004, STEEL)
    for x in (r * 0.35, r * 0.7):
        for sz in (-1, 1):
            beam(p, (x, 0.032, 0.0), (x, 0.0, sz * 0.01), 0.003, STEEL_DARK)
    return p


def pipe(name, length, r=0.006):
    """A steel pipe on small concrete saddles; tilts with the slope."""
    p = Part(name)
    hl = length / 2
    for x in np.linspace(-hl + 0.01, hl - 0.01, 4):
        box(p, x - 0.006, FOOT, -0.008, x + 0.006, 0.006, 0.008, CONCRETE)
    tube(p, (-hl, 0.01, 0), (hl, 0.01, 0), r, STEEL, n=6)
    return p


def sprinkler_patch(name, w=0.15, d=0.11, rows=5):
    """A small irrigated plot: bright crop rows on wet dark soil with a pipe and sprinkler risers along the back."""
    p = Part(name)
    slab(p, w, d, SOIL_DARK)
    for i in range(rows):
        z = -d / 2 + (i + 0.5) * d / rows
        box(p, -w / 2 + 0.006, 0.005, z - 0.007, w / 2 - 0.006, 0.016, z + 0.007,
            IRRIGATED if i % 2 else IRRIGATED_DARK)
    tube(p, (-w / 2, 0.01, -d / 2 - 0.004), (w / 2, 0.01, -d / 2 - 0.004), 0.004, STEEL, n=5)
    for x in (-w / 3, 0.0, w / 3):
        box(p, x - 0.002, 0.01, -d / 2 - 0.006, x + 0.002, 0.03, -d / 2 - 0.002, STEEL_DARK)
    return p


def groundwater_well():
    return [
        at(pump_house("pump_house"), -0.4, -0.3),
        at(water_tower("water_tower"), -0.04, -0.5),
        at(pivot_field("ground_pivot_east", r=0.115), 0.36, -0.22),
        at(pivot_field("ground_pivot_south", r=0.105), 0.18, 0.42),
        at(sprinkler_patch("ground_patch"), -0.4, 0.3, 0.1),
        at(pipe("ground_pipe_e", 0.13), -0.24, -0.29, 0.06),
        at(pipe("ground_pipe_s", 0.12), -0.42, -0.14, math.pi / 2),
    ]


# --- Oil well --------------------------------------------------------------------------------------------------


def oil_derrick(name, h=0.27, base=0.045, top=0.01):
    """A tapering lattice derrick on a concrete pad, with a crown block and a small doghouse."""
    p = Part(name)
    box(p, -base - 0.014, FOOT, -base - 0.014, base + 0.014, 0.012, base + 0.014, CONCRETE_DARK, top=CONCRETE)

    def ring(t):
        r = base + (top - base) * t
        return [np.array((sx * r, 0.012 + h * t, sz * r)) for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1))]

    levels = [0.0, 0.26, 0.5, 0.72, 1.0]
    bottom, apex = ring(0.0), ring(1.0)
    for i in range(4):
        beam(p, bottom[i], apex[i], 0.009, DERRICK)
    for k in range(1, len(levels)):
        lo, hi = ring(levels[k - 1]), ring(levels[k])
        for i in range(4):
            beam(p, hi[i], hi[(i + 1) % 4], 0.005, DERRICK_LIGHT)
            a, b = (lo[i], hi[(i + 1) % 4]) if k % 2 else (lo[(i + 1) % 4], hi[i])
            beam(p, a, b, 0.004, DERRICK_LIGHT)
    y = 0.012 + h
    box(p, -0.016, y, -0.016, 0.016, y + 0.016, 0.016, IRON)
    tube(p, (0, y, 0), (0, 0.06, 0), 0.002, ROPE, n=4)
    box(p, -0.007, 0.05, -0.007, 0.007, 0.07, 0.007, IRON)
    # doghouse beside the rig floor
    box(p, base + 0.006, 0.012, -0.02, base + 0.05, 0.05, 0.026, BOARDS, top=SLATE_DARK)
    box(p, base + 0.02, 0.012, 0.024, base + 0.036, 0.04, 0.028, DOOR)
    return p


def pumpjack(name):
    """A nodding-donkey pump: skid, A-frame, walking beam with a horsehead, crank and counterweights; faces +X."""
    p = Part(name)
    box(p, -0.075, FOOT / 2, -0.018, 0.07, 0.008, 0.018, CONCRETE_DARK, top=CONCRETE)
    pivot = (0.0, 0.085, 0.0)
    for sx in (-1, 1):
        for sz in (-1, 1):
            beam(p, (sx * 0.022, 0.008, sz * 0.014), (pivot[0], pivot[1], sz * 0.004), 0.006, IRON)
    beam(p, (-0.07, 0.096, 0.0), (0.05, 0.078, 0.0), 0.011, PUMP)
    p.hull([(-0.068, 0.11, -0.007), (-0.068, 0.07, -0.007), (-0.084, 0.074, -0.007), (-0.088, 0.1, -0.007),
            (-0.068, 0.11, 0.007), (-0.068, 0.07, 0.007), (-0.084, 0.074, 0.007), (-0.088, 0.1, 0.007)],
           [[0, 1, 2, 3], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]], PUMP)
    beam(p, (-0.086, 0.074, 0.0), (-0.086, 0.014, 0.0), 0.003, STEEL)
    cylinder(p, 0.01, 0.0, 0.016, IRON, at=(-0.086, 0, 0), n=6)
    box(p, 0.03, 0.008, -0.012, 0.062, 0.034, 0.012, IRON)
    for sz in (-1, 1):
        box(p, 0.034, 0.016, sz * 0.016 - 0.004, 0.068, 0.05, sz * 0.016 + 0.004, TANK_BAND)
        beam(p, (0.05, 0.04, sz * 0.012), (0.05, 0.078, sz * 0.007), 0.004, IRON)
    return p


def oil_tank(name, r=0.045, h=0.07):
    """A squat storage tank with a red band and a shallow conical roof."""
    p = Part(name)
    cylinder(p, r * 1.08, FOOT, 0.008, CONCRETE_DARK, n=10, top=CONCRETE)
    cylinder(p, r, 0.008, h, TANK, n=10)
    cylinder(p, r * 1.01, h * 0.62, h * 0.78, TANK_BAND, n=10)
    cylinder(p, r * 1.02, h, h + 0.02, TANK, n=10, r_top=0.0)
    box(p, -0.003, 0.008, r - 0.001, 0.003, h, r + 0.004, STEEL_DARK)  # ladder
    return p


def stains(name, spots, rng):
    """Dark oil-soaked ground and gravel; tilts with the slope."""
    p = Part(name)
    for i, (x, z, rx, rz) in enumerate(spots):
        blob(p, rx, rz, OIL if i % 2 == 0 else OIL_SHEEN, rng, at=(x, 0, z), y=0.003 + 0.001 * i)
    return p


def oil_well():
    rng = random.Random(83)
    return [
        at(stains("ground_stain_rig", [(0.0, 0.0, 0.11, 0.09), (0.05, 0.05, 0.05, 0.04)], rng), -0.26, -0.3),
        at(stains("ground_stain_pump", [(0.0, 0.0, 0.1, 0.06), (-0.04, 0.02, 0.04, 0.03)], rng), 0.24, 0.32),
        at(oil_derrick("derrick"), -0.26, -0.32),
        at(pumpjack("pumpjack"), 0.24, 0.34),
        at(oil_tank("tank"), 0.42, -0.32),
        at(oil_tank("tank_b", r=0.036, h=0.056), 0.62, -0.06),
        at(pipe("ground_pipe", 0.16), 0.52, 0.06, math.pi / 2 - 0.2),
    ]


MODELS = {
    "improvement_improvement_pasture": pasture,
    "improvement_improvement_farm": farm,
    "improvement_improvement_apple_farm": orchard,
    "improvement_improvement_plantation": plantation,
    "improvement_improvement_winery": winery,
    "improvement_improvement_dome_farm": dome_farm,
    "improvement_improvement_groundwater_well": groundwater_well,
    "improvement_improvement_well": oil_well,
}
