"""Camps, woodcutters, lumbermills and other improvements that stand among trees.

Every model here keeps the tile's trees: only the middle (0.35 world units) is cleared, so the pieces stay compact
and use light or warm colours that read against the dark canopy.
"""
import random

from .kit import *  # noqa: F401,F403
from .kit import FOOT, Part, at

HIDE = srgb(0xDCC59C)
HIDE_DARK = srgb(0x9A714A)
FUR = srgb(0x6B4A30)
FUR_LIGHT = srgb(0x9C7752)
BONE = srgb(0xEDE4CF)
FLAME = srgb(0xF0862A)
FLAME_HOT = srgb(0xFFD54F)
ASH = srgb(0x4A4440)
BAMBOO = srgb(0xC2AE62)
BAMBOO_DARK = srgb(0x8F7C3C)
WEAVE = srgb(0xD6C287)
PALM = srgb(0xC9A84E)
PALM_DARK = srgb(0x9C8236)
WICKER = srgb(0xA9783F)
BANANA = srgb(0xE8CF3A)
BANANA_GREEN = srgb(0xA7B83A)
COFFEE = srgb(0xB0352B)
DYE = srgb(0x7B3C8F)
PLANK = srgb(0xD3A86E)
PLANK_SHADE = srgb(0xB78C55)
SAWDUST = srgb(0xE2C690)
SAPLING = srgb(0x86C24E)
SAPLING_DARK = srgb(0x5F9A36)
WHITE = srgb(0xEEF1F0)
PALE_GREY = srgb(0xC5CBCC)
GREY = srgb(0x8E979A)
TEAL = srgb(0x3FE3C0)
TEAL_MID = srgb(0x2CC2A4)
TEAL_DARK = srgb(0x1C8F82)
GLASS = srgb(0x2B4F5E)


def front_phase(n):
    """Cylinder phase that puts one facet squarely facing +Z."""
    return math.pi / 2 - math.pi / n


def orb(part, r, centre, colours, n=7, rings=4, squash=1.0):
    """A low-poly sphere; colours alternate across the facets."""
    cx, cy, cz = centre
    pts = [(cx, cy + r * squash, cz)]
    for k in range(1, rings):
        a = math.pi * k / rings
        for i in range(n):
            b = (i + 0.5 * k) * math.tau / n
            pts.append((cx + math.cos(b) * math.sin(a) * r, cy + math.cos(a) * r * squash,
                        cz + math.sin(b) * math.sin(a) * r))
    pts.append((cx, cy - r * squash, cz))
    bottom = len(pts) - 1
    faces = [[0, 1 + i, 1 + (i + 1) % n] for i in range(n)]
    for k in range(rings - 2):
        a0, b0 = 1 + k * n, 1 + (k + 1) * n
        for i in range(n):
            i1 = (i + 1) % n
            faces += [[a0 + i, b0 + i, a0 + i1], [a0 + i1, b0 + i, b0 + i1]]
    last = 1 + (rings - 2) * n
    faces += [[last + i, bottom, last + (i + 1) % n] for i in range(n)]
    part.hull(pts, faces, [colours[i % len(colours)] for i in range(len(faces))])


def wheel(part, x0, x1, y, z, r, colour=TIMBER, n=8):
    """A wheel lying in the YZ plane, between x0 and x1."""
    pts = []
    for i in range(n):
        a = i * math.tau / n
        for x in (x0, x1):
            pts.append((x, y + math.sin(a) * r, z + math.cos(a) * r))
    faces = [[2 * i for i in range(n)], [2 * i + 1 for i in range(n)]]
    faces += [[2 * i, 2 * ((i + 1) % n), 2 * ((i + 1) % n) + 1, 2 * i + 1] for i in range(n)]
    part.hull(pts, faces, colour)


# --- Camp -----------------------------------------------------------------------------------------------------------

def hide_tent(name, r=0.042, h=0.1, n=7):
    """A conical hide tent with a dark band, a door flap on +Z and poles crossing at the top."""
    p = Part(name)
    ph = front_phase(n)
    cylinder(p, r, FOOT, h, HIDE, n=n, r_top=0.0, phase=ph)

    def radius(y):  # cone radius at height y
        return r * (h - y) / (h - FOOT)

    y0, y1 = 0.052, 0.064
    cylinder(p, radius(y0) + 0.002, y0, y1, HIDE_DARK, n=n, r_top=radius(y1) + 0.002, phase=ph)
    # door flap: a thin dark triangle laid on the front facet
    face = math.cos(math.pi / n)

    def front(y, push):
        return radius(y) * face + push
    tip = 0.045
    pts = [(-0.014, 0.0, front(0.0, 0.002)), (0.014, 0.0, front(0.0, 0.002)), (0.0, tip, front(tip, 0.002)),
           (-0.014, 0.0, front(0.0, -0.006)), (0.014, 0.0, front(0.0, -0.006)), (0.0, tip, front(tip, -0.006))]
    p.hull(pts, [[0, 1, 2], [3, 4, 5], [0, 1, 4, 3], [1, 2, 5, 4], [2, 0, 3, 5]], DARK)
    for i in range(3):
        a = i * math.tau / 3 + 0.4
        beam(p, (math.cos(a) * 0.006, h - 0.02, math.sin(a) * 0.006),
             (-math.cos(a) * 0.014, h + 0.025, -math.sin(a) * 0.014), 0.005, TIMBER)
    return p


def ridge_tent(name, w=0.075, d=0.06, h=0.055):
    """A low A-frame hide tent; ridge along X, open dark end on +X."""
    p = Part(name)
    hw, hd = w / 2, d / 2
    pts = [(-hw, FOOT / 2, -hd), (hw, FOOT / 2, -hd), (hw, FOOT / 2, hd), (-hw, FOOT / 2, hd), (-hw, h, 0), (hw, h, 0)]
    faces = [[0, 1, 2, 3], [3, 2, 5, 4], [1, 0, 4, 5], [0, 3, 4], [2, 1, 5]]
    p.hull(pts, faces, [HIDE, HIDE, HIDE_DARK, HIDE_DARK, HIDE_DARK])
    # dark opening on the +X gable
    k = 0.6
    pts = [(hw + 0.001, 0.0, -hd * k), (hw + 0.001, 0.0, hd * k), (hw + 0.001, h * 0.62, 0),
           (hw - 0.004, 0.0, -hd * k), (hw - 0.004, 0.0, hd * k), (hw - 0.004, h * 0.62, 0)]
    p.hull(pts, [[0, 1, 2], [3, 4, 5], [0, 1, 4, 3], [1, 2, 5, 4], [2, 0, 3, 5]], DARK)
    for sx in (-1, 1):  # ridge pole ends
        beam(p, (sx * (hw - 0.004), h - 0.006, 0), (sx * (hw + 0.012), h + 0.008, 0), 0.005, TIMBER)
    return p


def campfire(name):
    p = Part(name)
    cylinder(p, 0.026, FOOT / 2, 0.003, ASH, n=7)
    for i in range(7):
        a = i * math.tau / 7
        x, z = math.cos(a) * 0.026, math.sin(a) * 0.026
        box(p, -0.007, FOOT / 2, -0.006, 0.007, 0.012, 0.006, STONE if i % 2 else ROCK_DARK, at=(x, 0, z), yaw=-a)
    for a in (0.3, 0.3 + math.pi * 2 / 3, 0.3 + math.pi * 4 / 3):
        c, s = math.cos(a), math.sin(a)
        beam(p, (c * 0.02, 0.004, s * 0.02), (-c * 0.004, 0.026, -s * 0.004), 0.006, TIMBER)
    cylinder(p, 0.012, 0.004, 0.05, FLAME, n=5, r_top=0.0)
    cylinder(p, 0.007, 0.004, 0.034, FLAME_HOT, n=5, r_top=0.0, phase=0.6, at=(0.004, 0, 0.006))
    return p


def drying_rack(name):
    """Two forked posts, a crossbar with hides hung over it and a pair of antlers on top."""
    p = Part(name)
    hw = 0.045
    for sx in (-1, 1):
        beam(p, (sx * hw, FOOT, 0), (sx * hw, 0.085, 0), 0.007, TIMBER)
    beam(p, (-hw - 0.008, 0.08, 0), (hw + 0.008, 0.08, 0), 0.006, TIMBER_LIGHT)
    for x0, x1, drop, colour in ((-0.038, -0.006, 0.05, FUR_LIGHT), (0.0, 0.034, 0.042, HIDE)):
        box(p, x0, 0.08 - drop, 0.003, x1, 0.083, 0.007, colour)
        box(p, x0 + 0.002, 0.08 - drop * 0.7, -0.007, x1 - 0.002, 0.083, -0.003, colour)
    # antlers on the crossbar centre, branching up and out
    for sx in (-1, 1):
        base = (sx * 0.004, 0.084, 0.0)
        mid = (sx * 0.02, 0.105, 0.0)
        tip = (sx * 0.026, 0.13, 0.002)
        beam(p, base, mid, 0.005, BONE)
        beam(p, mid, tip, 0.004, BONE)
        beam(p, mid, (sx * 0.034, 0.112, 0.004), 0.004, BONE)
        beam(p, (sx * 0.012, 0.095, 0.0), (sx * 0.01, 0.118, 0.004), 0.004, BONE)
    return p


def pelt(name):
    """A stretched pelt pegged out on the ground (tilts with the slope)."""
    p = Part(name)
    outline = [(-0.04, 0.0), (-0.026, -0.022), (-0.012, -0.024), (0.0, -0.034), (0.012, -0.024), (0.026, -0.022),
               (0.04, 0.0), (0.026, 0.022), (0.012, 0.024), (0.0, 0.034), (-0.012, 0.024), (-0.026, 0.022)]
    # a convex slab can't keep the legs; use two overlapping convex slabs (body and limbs)
    body = [(x * 0.8, z * 0.75) for x, z in outline]
    for pts2d, y, colour in ((outline, 0.003, FUR), (body, 0.0055, FUR_LIGHT)):
        pts = [(x, FOOT / 4, z) for x, z in pts2d] + [(x, y, z) for x, z in pts2d]
        m = len(pts2d)
        faces = [list(range(m)), list(range(m, 2 * m))]
        faces += [[i, (i + 1) % m, m + (i + 1) % m, m + i] for i in range(m)]
        p.hull(pts, faces, colour)
    for x, z in ((-0.04, 0.0), (0.04, 0.0), (0.0, -0.034), (0.0, 0.034)):
        box(p, x - 0.002, 0.0, z - 0.002, x + 0.002, 0.01, z + 0.002, TIMBER)
    return p


def camp():
    return [
        at(hide_tent("tent", r=0.052, h=0.13), -0.2, -0.1),
        at(hide_tent("tent_b", r=0.046, h=0.115), 0.04, -0.24),
        at(ridge_tent("tent_c", w=0.09, d=0.07, h=0.065), 0.24, -0.06, 0.4),
        at(campfire("fire"), -0.02, 0.02),
        at(drying_rack("rack"), -0.2, 0.15, 0.3),
        at(pelt("ground_pelt"), 0.16, 0.16, 0.3),
    ]


# --- Jungle camp ----------------------------------------------------------------------------------------------------

def stilt_hut(name, w=0.08, d=0.07, floor=0.045, wall=0.038, rise=0.06):
    """A woven bamboo hut on stilts under a steep, deep-eaved palm-leaf roof, with a ladder at the front."""
    p = Part(name)
    hw, hd = w / 2, d / 2
    for sx in (-1, 1):
        for sz in (-1, 1):
            beam(p, (sx * (hw - 0.006), FOOT, sz * (hd - 0.006)), (sx * (hw - 0.006), floor, sz * (hd - 0.006)),
                 0.007, BAMBOO_DARK)
    box(p, -hw - 0.008, floor - 0.008, -hd - 0.008, hw + 0.008, floor, hd + 0.008, BAMBOO)
    box(p, -hw, floor, -hd, hw, floor + wall, hd, WEAVE)
    box(p, -0.012, floor, hd - 0.002, 0.012, floor + 0.03, hd + 0.003, DARK)
    for sx in (-1, 1):  # corner canes
        for sz in (-1, 1):
            box(p, sx * hw - 0.003, floor, sz * hd - 0.003, sx * hw + 0.003, floor + wall, sz * hd + 0.003, BAMBOO_DARK)
    top = prism_roof(p, w, d, floor + wall, rise, 0.022, PALM, WEAVE, hip=w * 0.3)
    box(p, -w * 0.2, top - 0.006, -0.005, w * 0.2, top + 0.004, 0.005, PALM_DARK)
    # ladder from the door down to the ground
    z0, z1 = hd + 0.008, hd + 0.04
    for sx in (-1, 1):
        beam(p, (sx * 0.011, floor, z0), (sx * 0.011, FOOT / 2, z1), 0.004, BAMBOO_DARK)
    for t in (0.25, 0.5, 0.75):
        y, z = floor * (1 - t), z0 + (z1 - z0) * t
        box(p, -0.011, y - 0.002, z - 0.002, 0.011, y + 0.002, z + 0.002, BAMBOO_DARK)
    return p


def baskets(name):
    p = Part(name)
    for (x, z, r, h), fruit in zip(((-0.022, 0.0, 0.016, 0.026), (0.014, -0.012, 0.014, 0.022),
                                    (0.012, 0.02, 0.013, 0.02)), (BANANA, COFFEE, DYE)):
        cylinder(p, r * 0.85, FOOT / 2, h, WICKER, n=7, r_top=r, at=(x, 0, z))
        cylinder(p, r * 0.9, h, h + 0.008, fruit, n=7, r_top=r * 0.4, at=(x, 0, z))
    return p


def canoe(name, length=0.13, w=0.026):
    """A dugout canoe with a paddle across it."""
    p = Part(name)
    hl, hw = length / 2, w / 2
    pts = [(-hl, 0.02, 0), (hl, 0.02, 0)]
    for x in (-hl * 0.6, hl * 0.6):
        pts += [(x, 0.018, -hw), (x, 0.018, hw), (x * 0.9, 0.0, -hw * 0.5), (x * 0.9, 0.0, hw * 0.5)]
    # faces: top deck, bottom, sides, ends
    faces = [[0, 2, 6, 1, 7, 3], [4, 8, 9, 5], [2, 4, 8, 6], [3, 5, 9, 7],
             [0, 2, 4], [0, 3, 5], [0, 4, 5], [1, 6, 8], [1, 7, 9], [1, 8, 9]]
    p.hull(pts, faces, [DARK] + [TIMBER] * 9)
    # hollow: a dark inset strip, and a paddle lying across
    beam(p, (-0.02, 0.024, -0.03), (0.0, 0.024, 0.03), 0.004, TIMBER_LIGHT)
    box(p, -0.006, 0.02, 0.026, 0.006, 0.026, 0.044, TIMBER_LIGHT, yaw=0.32)
    return p


def harvest_pile(name, rng):
    p = Part(name)
    lumpy_mound(p, 0.03, 0.026, 0.032, [BANANA, BANANA_GREEN, BANANA], rng, y0=FOOT / 2, rings=2, n=7)
    lumpy_mound(p, 0.016, 0.014, 0.022, [COFFEE, srgb(0x8E2A22)], rng, at=(0.03, 0, 0.018), y0=FOOT / 2, rings=2, n=6)
    return p


def jungle_camp():
    rng = random.Random(17)
    return [
        at(stilt_hut("hut", w=0.095, d=0.08, floor=0.07, wall=0.042, rise=0.07), -0.14, -0.14),
        at(stilt_hut("hut_b", w=0.08, d=0.07, floor=0.06, wall=0.036, rise=0.06), 0.18, -0.12, -0.4),
        at(baskets("baskets"), 0.22, 0.1),
        at(harvest_pile("harvest", rng), -0.02, 0.12),
        at(canoe("canoe"), -0.22, 0.12, 0.5),
    ]


# --- Woodcutter -----------------------------------------------------------------------------------------------------

def log_cabin(name, w=0.12, d=0.09, wall=0.06, rise=0.055):
    """A small log cabin: board walls with log courses, notched log ends at the corners, shingles, stone chimney."""
    p = Part(name)
    hw, hd = w / 2, d / 2
    box(p, -hw - 0.006, FOOT, -hd - 0.006, hw + 0.006, 0.01, hd + 0.006, STONE_DARK, top=STONE)
    box(p, -hw, 0.01, -hd, hw, wall, hd, TIMBER_LIGHT)
    for y in (0.022, 0.036, 0.05):  # log courses
        box(p, -hw - 0.002, y - 0.002, hd - 0.002, hw + 0.002, y + 0.002, hd + 0.003, TIMBER)
        box(p, hw - 0.002, y - 0.002, -hd - 0.002, hw + 0.003, y + 0.002, hd + 0.002, TIMBER)
    for sx in (-1, 1):  # crossed log ends
        for sz in (-1, 1):
            box(p, sx * hw - 0.007, 0.01, sz * hd - 0.007, sx * hw + 0.007, wall, sz * hd + 0.007, TIMBER)
    box(p, -0.014, 0.01, hd - 0.002, 0.014, 0.05, hd + 0.004, DOOR)
    box(p, 0.024, 0.03, hd - 0.002, 0.044, 0.046, hd + 0.004, WINDOW)
    top = prism_roof(p, w, d, wall, rise, 0.014, SHINGLE, TIMBER_LIGHT)
    box(p, -hw - 0.014, top - 0.008, -0.007, hw + 0.014, top + 0.004, 0.007, SHINGLE_DARK)
    box(p, -hw - 0.016, FOOT, -0.016, -hw + 0.008, top + 0.012, 0.012, STONE, top=STONE_DARK)
    return p


def stump(part, x, z, r=0.018, h=0.026):
    cylinder(part, r * 1.15, FOOT / 2, 0.006, TIMBER, n=7, at=(x, 0, z))
    cylinder(part, r, 0.006, h, TIMBER, n=7, at=(x, 0, z), top=PLANK)


def chopping_block(name):
    """A wide stump with an axe sunk in it, a split log beside it and a smaller stump."""
    p = Part(name)
    stump(p, 0.0, 0.0, r=0.02, h=0.03)
    beam(p, (0.002, 0.03, 0.0), (0.03, 0.062, 0.004), 0.005, TIMBER_LIGHT)
    pts = [(-0.008, 0.028, -0.002), (0.01, 0.028, -0.002), (-0.008, 0.042, -0.002), (0.004, 0.044, -0.002)]
    pts += [(x, y, 0.004) for x, y, _ in pts]
    p.hull(pts, [[0, 1, 3, 2], [4, 5, 7, 6], [0, 1, 5, 4], [1, 3, 7, 5], [3, 2, 6, 7], [2, 0, 4, 6]], IRON)
    stump(p, -0.05, 0.03, r=0.014, h=0.02)
    for a in (0.3, 1.9):  # split halves
        c, s = math.cos(a), math.sin(a)
        box(p, -0.004, 0.0, -0.016, 0.004, 0.012, 0.016, PLANK, at=(0.032 + c * 0.008, 0, 0.026 + s * 0.008),
            yaw=a)
    return p


def log_pile(name, length=0.11, r=0.012, rows=(3, 2, 1)):
    """Felled logs stacked along X, wedged by short stakes."""
    p = Part(name)
    for row, count in enumerate(rows):
        for k in range(count):
            z = (k - (count - 1) / 2) * 2 * r
            log(p, -length / 2, length / 2, r + row * 1.7 * r - 0.002, z, r, end=PLANK)
    w = rows[0] * r
    for sx in (-1, 1):
        for sz in (-1, 1):
            beam(p, (sx * length * 0.36, FOOT / 2, sz * (w + 0.004)), (sx * length * 0.36, 0.04, sz * (w + 0.004)),
                 0.005, TIMBER)
    return p


def sawhorse(name):
    """An X-legged trestle with a log across it and a bow saw leaning on it."""
    p = Part(name)
    for x in (-0.026, 0.026):
        for sz in (-1, 1):
            beam(p, (x, FOOT / 2, sz * 0.018), (x, 0.042, -sz * 0.006), 0.005, TIMBER_LIGHT)
    log(p, -0.05, 0.05, 0.046, 0.0, 0.01, end=PLANK)
    beam(p, (0.006, 0.0, 0.026), (0.006, 0.04, 0.014), 0.003, TIMBER)
    beam(p, (0.006, 0.0, 0.026), (-0.022, 0.0, 0.03), 0.003, IRON)
    return p


def woodcutter():
    return [
        at(log_cabin("cabin"), -0.06, -0.17),
        at(log_pile("logs"), 0.22, -0.04, -0.4),
        at(chopping_block("block"), -0.22, 0.08),
        at(sawhorse("sawhorse"), 0.08, 0.14, 0.3),
    ]


# --- Lumbermill -----------------------------------------------------------------------------------------------------

def saw_shed(name, w=0.18, d=0.1, wall=0.075, rise=0.06):
    """A timber sawmill shed, open on the +Z front: a saw bench with a log and an upright frame saw inside."""
    p = Part(name)
    hw, hd = w / 2, d / 2
    box(p, -hw - 0.006, FOOT, -hd - 0.006, hw + 0.006, 0.01, hd + 0.006, STONE_DARK, top=STONE)
    box(p, -hw + 0.004, 0.01, -hd + 0.002, hw - 0.004, 0.0125, hd - 0.004, SAWDUST)  # floor
    box(p, -hw, 0.01, -hd, hw, wall, -hd + 0.008, BOARDS)  # back wall
    box(p, -hw, 0.01, -hd, -hw + 0.008, wall, hd, BOARDS)  # end walls
    box(p, hw - 0.008, 0.01, -hd, hw, wall, hd, BOARDS)
    for x in np.linspace(-hw, hw, 4):  # front posts
        box(p, x - 0.005, 0.01, hd - 0.008, x + 0.005, wall, hd + 0.002, TIMBER)
    box(p, -hw, wall - 0.012, hd - 0.008, hw, wall, hd + 0.002, TIMBER)  # lintel
    top = prism_roof(p, w, d, wall, rise, 0.016, SHINGLE, BOARDS_DARK)
    box(p, -hw - 0.016, top - 0.008, -0.008, hw + 0.016, top + 0.004, 0.008, SHINGLE_DARK)
    # saw bench along X with a log on it
    box(p, -0.06, 0.0125, -0.012, 0.06, 0.03, 0.012, TIMBER_LIGHT, top=PLANK)
    log(p, -0.055, 0.02, 0.04, 0.0, 0.01, end=PLANK)
    box(p, 0.02, 0.03, -0.009, 0.055, 0.036, 0.009, PLANK)  # sawn board
    # frame saw: two uprights, crossbars and a blade
    for sz in (-1, 1):
        box(p, 0.016, 0.03, sz * 0.02 - 0.003, 0.022, 0.07, sz * 0.02 + 0.003, TIMBER)
    box(p, 0.016, 0.064, -0.023, 0.022, 0.07, 0.023, TIMBER)
    box(p, 0.0175, 0.03, -0.0015, 0.0205, 0.064, 0.0015, IRON)
    return p


def plank_stack(name, length=0.1, layers=4):
    """Sawn planks stacked in layers with dark spacer sticks between them."""
    p = Part(name)
    w = 0.05
    for i in range(layers):
        y = 0.004 + i * 0.011
        box(p, -length / 2, y if i else FOOT / 2, -w / 2, length / 2, y + 0.008, w / 2,
            PLANK_SHADE if i % 2 else PLANK, top=PLANK)
        if i < layers - 1:
            for x in (-length * 0.35, 0.0, length * 0.35):
                box(p, x - 0.003, y + 0.008, -w / 2 - 0.002, x + 0.003, y + 0.011, w / 2 + 0.002, BOARDS_DARK)
    return p


def log_cart(name):
    """A two-wheeled cart loaded with logs, shafts resting on the ground."""
    p = Part(name)
    for sx in (-1, 1):
        wheel(p, sx * 0.028, sx * 0.036, 0.022, 0.0, 0.022, TIMBER)
        beam(p, (sx * 0.018, 0.026, -0.04), (sx * 0.014, 0.003, 0.085), 0.006, TIMBER_LIGHT)
    box(p, -0.026, 0.024, -0.045, 0.026, 0.03, 0.04, BOARDS)
    for x, y in ((-0.012, 0.04), (0.012, 0.04), (0.0, 0.058)):
        pts = []
        for k in range(6):
            a = k * math.tau / 6
            for z in (-0.06, 0.05):
                pts.append((x + math.cos(a) * 0.01, y + math.sin(a) * 0.01, z))
        faces = [[2 * k for k in range(6)], [2 * k + 1 for k in range(6)]]
        faces += [[2 * k, 2 * ((k + 1) % 6), 2 * ((k + 1) % 6) + 1, 2 * k + 1] for k in range(6)]
        p.hull(pts, faces, [PLANK, PLANK] + [TIMBER] * 6)
    return p


def lumbermill():
    return [
        at(saw_shed("mill"), -0.02, -0.16),
        at(log_pile("logs", length=0.13, r=0.014, rows=(3, 2, 1)), 0.24, 0.04, 1.2),
        at(plank_stack("planks"), -0.24, 0.08, 0.4),
        at(log_cart("cart"), 0.0, 0.26, 1.57),
    ]


# --- Forest preserve ------------------------------------------------------------------------------------------------

def lookout_tower(name, h=0.24, base=0.032, top_w=0.022):
    """A tall timber fire-lookout: splayed legs with cross-bracing, a railed cabin platform and a pyramid roof."""
    p = Part(name)
    corners = [(sx, sz) for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    for sx, sz in corners:
        cylinder(p, 0.008, FOOT, 0.008, STONE, n=5, at=(sx * base, 0, sz * base))
        beam(p, (sx * base, 0.0, sz * base), (sx * top_w, h, sz * top_w), 0.007, TIMBER)
    for (ax, az), (bx, bz) in zip(corners, corners[1:] + corners[:1]):
        for y0, y1 in ((0.02, h * 0.5), (h * 0.5, h * 0.95)):
            t0, t1 = y0 / h, y1 / h
            w0, w1 = base + (top_w - base) * t0, base + (top_w - base) * t1
            beam(p, (ax * w0, y0, az * w0), (bx * w1, y1, bz * w1), 0.004, TIMBER_LIGHT)
    pw = top_w + 0.014
    box(p, -pw, h, -pw, pw, h + 0.008, pw, BOARDS)
    box(p, -top_w, h + 0.008, -top_w, top_w, h + 0.032, top_w, TIMBER_LIGHT)  # cabin
    box(p, -top_w + 0.004, h + 0.016, top_w - 0.001, top_w - 0.004, h + 0.028, top_w + 0.002, WINDOW)
    for sx, sz in corners:
        box(p, sx * pw - 0.003, h + 0.008, sz * pw - 0.003, sx * pw + 0.003, h + 0.044, sz * pw + 0.003, TIMBER)
    box(p, -pw, h + 0.02, pw - 0.003, pw, h + 0.025, pw + 0.001, TIMBER)  # front rail
    cylinder(p, pw * 1.5, h + 0.044, h + 0.075, SLATE, n=4, r_top=0.0, phase=math.pi / 4)
    return p


def nursery(name, w=0.12, d=0.08, rng=None):
    """A fenced bed of young saplings in rows (tilts with the slope)."""
    p = Part(name)
    hw, hd = w / 2, d / 2
    box(p, -hw, FOOT / 2, -hd, hw, 0.004, hd, SOIL)
    for i, z in enumerate((-hd * 0.5, hd * 0.5)):
        for k, x in enumerate((-hw * 0.6, -hw * 0.2, hw * 0.2, hw * 0.6)):
            s = 0.8 + 0.4 * rng.random()
            box(p, x - 0.002, 0.004, z - 0.002, x + 0.002, 0.012 * s, z + 0.002, TIMBER)
            cylinder(p, 0.01 * s, 0.01 * s, 0.04 * s, SAPLING if (i + k) % 2 else SAPLING_DARK, n=5, r_top=0.0,
                     at=(x, 0, z), phase=rng.random())
    for x in (-hw, 0.0, hw):
        for z in (-hd, hd):
            box(p, x - 0.003, 0.0, z - 0.003, x + 0.003, 0.024, z + 0.003, TIMBER_LIGHT)
    for z in (-hd, hd):
        box(p, -hw, 0.015, z - 0.002, hw, 0.02, z + 0.002, TIMBER_LIGHT)
    for x in (-hw, hw):
        box(p, x - 0.002, 0.015, -hd, x + 0.002, 0.02, hd, TIMBER_LIGHT)
    return p


def sign_post(name):
    p = Part(name)
    for sx in (-1, 1):
        box(p, sx * 0.022 - 0.003, FOOT / 2, -0.003, sx * 0.022 + 0.003, 0.05, 0.003, TIMBER)
    box(p, -0.03, 0.026, -0.002, 0.03, 0.048, 0.003, TIMBER_LIGHT)
    box(p, -0.022, 0.033, 0.003, 0.022, 0.041, 0.005, SAPLING_DARK)
    prism_roof(p, 0.064, 0.012, 0.05, 0.008, 0.004, SHINGLE_DARK, TIMBER)
    return p


def forest_preserve():
    rng = random.Random(23)
    return [
        at(house("lodge", w=0.12, d=0.085, wall=0.068, rise=0.06, walls=PLASTER_WARM, roof=SLATE,
                 roof_end=SLATE_DARK, upper=False, seed=1), -0.12, -0.17),
        at(lookout_tower("tower"), 0.2, -0.1),
        at(nursery("ground_nursery", rng=rng), -0.12, 0.17),
        at(sign_post("sign"), 0.16, 0.2, -0.3),
    ]


# --- Hybrid forest --------------------------------------------------------------------------------------------------

def gene_tree(name, h=0.13, r=0.035, kind="orb"):
    """An engineered tree: a slim white trunk with a teal ring and a glowing teal canopy."""
    p = Part(name)
    cylinder(p, 0.014, FOOT, 0.006, PALE_GREY, n=6)
    cylinder(p, 0.0055, 0.006, h, WHITE, n=6, r_top=0.004)
    cylinder(p, 0.008, h * 0.45, h * 0.45 + 0.006, TEAL, n=6)
    if kind == "orb":
        orb(p, r, (0, h + r * 0.55, 0), [TEAL, TEAL_MID, TEAL, TEAL_DARK], n=7, rings=4, squash=0.85)
    else:  # stacked cones
        cylinder(p, r, h - 0.02, h + 0.04, TEAL_MID, n=6, r_top=0.0)
        cylinder(p, r * 0.75, h + 0.02, h + 0.075, TEAL, n=6, r_top=0.0, phase=0.5)
    return p


def research_pod(name, r=0.045):
    """A white domed lab pod on a pale pad, with a teal light band, a dark window strip and a door."""
    p = Part(name)
    n = 10
    ph = front_phase(n)
    cylinder(p, r + 0.01, FOOT, 0.006, PALE_GREY, n=n, phase=ph)
    cylinder(p, r, 0.006, 0.03, WHITE, n=n, phase=ph)
    cylinder(p, r + 0.002, 0.022, 0.027, TEAL, n=n, phase=ph)
    cylinder(p, r + 0.0015, 0.011, 0.019, GLASS, n=n, phase=ph + math.pi / n)  # window band, offset facets
    orb_pts = []
    rings = 3
    for k in range(rings):  # dome: rings of a hemisphere
        a = math.pi / 2 * k / rings
        for i in range(n):
            b = ph + i * math.tau / n
            orb_pts.append((math.cos(b) * math.cos(a) * r, 0.03 + math.sin(a) * r * 0.8, math.sin(b) * math.cos(a) * r))
    orb_pts.append((0, 0.03 + r * 0.8, 0))
    faces = [list(range(n))]
    for k in range(rings - 1):
        faces += [[k * n + i, k * n + (i + 1) % n, (k + 1) * n + (i + 1) % n, (k + 1) * n + i] for i in range(n)]
    faces += [[(rings - 1) * n + i, (rings - 1) * n + (i + 1) % n, rings * n] for i in range(n)]
    p.hull(orb_pts, faces, [WHITE] + [WHITE if j % 2 else PALE_GREY for j in range(len(faces) - 1)])
    face = r * math.cos(math.pi / n)
    box(p, -0.011, 0.006, face - 0.004, 0.011, 0.032, face + 0.006, GREY)
    box(p, -0.008, 0.006, face + 0.006, 0.008, 0.028, face + 0.008, GLASS)
    cylinder(p, 0.004, 0.03 + r * 0.78, 0.03 + r * 0.8 + 0.02, WHITE, n=5)
    cylinder(p, 0.006, 0.03 + r * 0.8 + 0.02, 0.03 + r * 0.8 + 0.028, TEAL, n=5)
    return p


def sensor_mast(name, h=0.16):
    p = Part(name)
    cylinder(p, 0.01, FOOT, 0.006, PALE_GREY, n=5)
    cylinder(p, 0.0035, 0.006, h, WHITE, n=5, r_top=0.0025)
    box(p, -0.016, h * 0.7, -0.002, 0.016, h * 0.7 + 0.004, 0.002, PALE_GREY)
    for sx in (-1, 1):
        box(p, sx * 0.016 - 0.003, h * 0.7 - 0.006, -0.003, sx * 0.016 + 0.003, h * 0.7 + 0.006, 0.003, TEAL)
    orb(p, 0.007, (0, h + 0.004, 0), [TEAL, TEAL_MID], n=5, rings=3)
    return p


def hybrid_forest():
    return [
        at(research_pod("pod"), 0.02, -0.18),
        at(gene_tree("tree", h=0.16, r=0.04), -0.2, -0.06),
        at(gene_tree("tree_b", h=0.13, r=0.035), 0.22, 0.04),
        at(gene_tree("tree_c", h=0.13, r=0.036, kind="cone"), -0.14, 0.16),
        at(gene_tree("tree_d", h=0.11, r=0.03), 0.1, 0.18),
        at(sensor_mast("mast"), 0.24, -0.16),
    ]


MODELS = {
    "improvement_improvement_camp": (camp, "centre"),
    "improvement_improvement_jungle_camp": (jungle_camp, "centre"),
    "improvement_improvement_woodcutter": (woodcutter, "centre"),
    "improvement_improvement_lumbermill": (lumbermill, "centre"),
    "improvement_improvement_forest_preserve": (forest_preserve, "centre"),
    "improvement_improvement_hybrid_forest": (hybrid_forest, "centre"),
}
