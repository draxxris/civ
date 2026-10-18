"""Tribal villages, ruins, dig sites and wonder improvements."""
import random

from .kit import *  # noqa: F401,F403
from .kit import FOOT, Part, at

MUD = srgb(0x9E7B55)
MUD_DARK = srgb(0x7D5F41)
HIDE = srgb(0xCDA97C)
HIDE_DARK = srgb(0xA9845A)
PAINT_YELLOW = srgb(0xD9A43A)
PAINT_BLUE = srgb(0x2F6482)
FIRE = srgb(0xE8842A)
FLAME = srgb(0xF6C850)
RUIN = srgb(0xA39D90)
RUIN_LIGHT = srgb(0xBDB7A9)
RUIN_DARK = srgb(0x7C776C)
MOSS = srgb(0x6C8A3C)
MOSS_DARK = srgb(0x587330)
SLAB = srgb(0xDEE1E2)
SLAB_GREY = srgb(0xA9AEB2)
SLAB_DARK = srgb(0x6E757B)
STEEL = srgb(0x5A5E62)
TEAL_DIM = srgb(0x3C8F89)
SAND = srgb(0xCDAE74)
SAND_DARK = srgb(0xA88A55)
BONE = srgb(0xE8DEC4)
STRING = srgb(0xF2EEE2)
CANVAS = srgb(0xE3D7B6)
CANVAS_DARK = srgb(0xC2B48F)
GRANITE = srgb(0xB0A898)
GRANITE_DARK = srgb(0x8E877A)
TERRACE_GRASS = srgb(0x6D963F)
DEEP = -0.15  # peak foundations


def ngon(r, n, phase=0.0):
    return [(math.cos(phase + i * math.tau / n) * r, math.sin(phase + i * math.tau / n) * r) for i in range(n)]


def prism(part, outline, y0, y1, colour, top=None, at=(0, 0, 0), yaw=0.0, shrink=1.0):
    """Extrude a convex XZ outline from y0 to y1; shrink < 1 tapers the top."""
    n = len(outline)
    pts = [(x, y0, z) for x, z in outline] + [(x * shrink, y1, z * shrink) for x, z in outline]
    faces = [list(range(n)), list(range(n, 2 * n))] + [[i, (i + 1) % n, n + (i + 1) % n, n + i] for i in range(n)]
    part.hull(place(pts, at, yaw), faces, [colour, top or colour] + [colour] * n)


def tufts(part, spots, rng, colours=(MOSS, MOSS_DARK)):
    """Small grass tufts: low three-sided cones."""
    for i, (x, z) in enumerate(spots):
        h = 0.012 + 0.012 * rng.random()
        cylinder(part, 0.01 + 0.006 * rng.random(), -0.004, h, colours[i % len(colours)], at=(x, 0, z), n=3,
                 r_top=0.0, phase=rng.random())


# --- tribal village ---------------------------------------------------------------------------------------------

def hut(name, r=0.05, wall=0.038, rise=0.07, lean=0.0):
    """A round mud hut under a conical thatch roof; doorway faces +Z."""
    p = Part(name)
    ph = math.pi / 8
    cylinder(p, r + 0.004, FOOT, 0.006, MUD_DARK, n=8, phase=ph)
    cylinder(p, r, 0.006, wall, MUD, n=8, phase=ph, r_top=r * 0.96)
    apothem = r * math.cos(math.pi / 8)
    box(p, -0.011, 0.006, apothem - 0.004, 0.011, 0.032, apothem + 0.003, DARK)
    box(p, -0.014, 0.03, apothem - 0.004, 0.014, 0.036, apothem + 0.005, TIMBER)
    pts = [(math.cos(i * math.tau / 9) * r * 1.35, wall - 0.012, math.sin(i * math.tau / 9) * r * 1.35)
           for i in range(9)] + [(lean, wall + rise, 0.0)]
    faces = [list(range(9))] + [[i, (i + 1) % 9, 9] for i in range(9)]
    p.hull(pts, faces, [THATCH_DARK] + [THATCH if i % 2 else THATCH_DARK for i in range(9)])
    box(p, lean - 0.004, wall + rise - 0.01, -0.004, lean + 0.004, wall + rise + 0.012, 0.004, TIMBER)
    return p


def fire_pit(name):
    p = Part(name)
    for i, (x, z) in enumerate(ngon(0.03, 7)):
        box(p, x - 0.009, FOOT / 2, z - 0.008, x + 0.009, 0.012, z + 0.008, STONE if i % 2 else STONE_DARK)
    box(p, -0.024, FOOT / 2, -0.024, 0.024, 0.002, 0.024, DARK)
    for a in (0.3, 2.4, 4.4):
        c, s = math.cos(a), math.sin(a)
        beam(p, (c * 0.026, 0.004, s * 0.026), (-c * 0.004, 0.026, -s * 0.004), 0.008, TIMBER)
    cylinder(p, 0.016, 0.006, 0.05, FIRE, n=5, r_top=0.0)
    cylinder(p, 0.009, 0.006, 0.065, FLAME, n=4, r_top=0.0, phase=0.4)
    return p


def totem(name):
    """A carved pole of painted blocks with outspread wings and a beak."""
    p = Part(name)
    box(p, -0.018, FOOT, -0.018, 0.018, 0.006, 0.018, STONE_DARK)
    shades = [TIMBER, CLOTH_RED, TIMBER_LIGHT, PAINT_BLUE, TIMBER]
    y = 0.006
    for i, colour in enumerate(shades):
        h = 0.03
        w = 0.014 if i % 2 else 0.012
        box(p, -w, y, -w, w, y + h, w, colour)
        box(p, -0.008, y + 0.014, w - 0.002, -0.003, y + 0.021, w + 0.003, PAINT_YELLOW)  # painted eyes
        box(p, 0.003, y + 0.014, w - 0.002, 0.008, y + 0.021, w + 0.003, PAINT_YELLOW)
        y += h
    box(p, -0.06, y - 0.03, -0.005, 0.06, y - 0.016, 0.005, PAINT_BLUE)  # wings
    box(p, -0.06, y - 0.03, 0.005, -0.03, y - 0.02, 0.007, PAINT_YELLOW)
    box(p, 0.03, y - 0.03, 0.005, 0.06, y - 0.02, 0.007, PAINT_YELLOW)
    pts = [(-0.007, y - 0.025, 0.012), (0.007, y - 0.025, 0.012), (0.0, y - 0.012, 0.012), (0.0, y - 0.022, 0.035)]
    p.hull(pts, [[0, 1, 2], [0, 1, 3], [1, 2, 3], [2, 0, 3]], PAINT_YELLOW)
    return p


def hide_rack(name):
    """Two posts and a crossbar with stretched hides."""
    p = Part(name)
    for sx in (-1, 1):
        beam(p, (sx * 0.05, FOOT / 2, 0.0), (sx * 0.046, 0.075, 0.0), 0.008, TIMBER)
    beam(p, (-0.058, 0.07, 0.0), (0.058, 0.07, 0.0), 0.007, TIMBER_LIGHT)
    for x, w, h, colour in ((-0.022, 0.022, 0.05, HIDE), (0.024, 0.02, 0.042, HIDE_DARK)):
        pts = [(x - w, 0.066, 0.004), (x + w, 0.066, 0.004), (x + w * 0.8, 0.066 - h, 0.008),
               (x - w * 0.9, 0.066 - h * 0.9, 0.008), (x, 0.066 - h * 1.1, 0.008)]
        pts += [(px, py, pz + 0.003) for px, py, pz in pts]
        p.hull(pts, [[0, 1, 2, 4, 3], [5, 6, 7, 9, 8], [0, 1, 6, 5], [1, 2, 7, 6], [2, 4, 9, 7], [4, 3, 8, 9],
                     [3, 0, 5, 8]], colour)
    return p


def goody_hut():
    return [
        at(hut("hut", r=0.058, rise=0.08), -0.4, -0.28, 0.4),
        at(hut("hut_b", lean=0.004), 0.22, -0.48, -0.15),
        at(hut("hut_c", r=0.054, rise=0.075, lean=-0.004), 0.5, 0.0, -0.9),
        at(hut("hut_d", r=0.045, wall=0.034, rise=0.06), -0.5, 0.3, 1.3),
        at(fire_pit("fire"), 0.12, 0.44),
        at(totem("totem"), -0.12, -0.5),
        at(hide_rack("rack"), 0.42, 0.42, -0.5),
        at(hide_rack("rack_b"), -0.22, 0.56, 0.3),
    ]


# --- city ruins -------------------------------------------------------------------------------------------------

def broken_wall(name, rng, length=0.2, h=0.08, corner=0.0):
    """A ruined stretch of wall along X with a ragged top; corner > 0 adds a return wall running to -Z."""
    p = Part(name)
    t = 0.026
    runs = [(-length / 2, 0.0, 0.0, length)]
    if corner:
        runs.append((-length / 2, 0.0, math.pi / 2, corner))
    for x0, z0, yaw, run in runs:
        n = max(3, int(run / 0.035))
        for i in range(n):
            a = i * run / n
            b = (i + 1) * run / n + 0.002
            top = h * (0.35 + 0.65 * rng.random()) * (1.0 - 0.5 * i / n)
            colour = RUIN if i % 2 else RUIN_LIGHT
            box(p, a, FOOT, -t / 2, b, top, t / 2, colour, at=(x0, 0, z0), yaw=yaw, top=RUIN_DARK)
            if rng.random() < 0.4:
                box(p, a + 0.003, top, -t / 2 + 0.002, b - 0.004, top + 0.004, t / 2 - 0.002, MOSS, at=(x0, 0, z0),
                    yaw=yaw)
    return p


def fallen_column(name, rng, drums=3):
    """Drums of a toppled column lying along X, slightly out of line, with its capital nearby."""
    p = Part(name)
    r = 0.018
    x = -0.06
    for i in range(drums):
        dz = 0.004 * (rng.random() - 0.5)
        log(p, x, x + 0.036, r - 0.003, dz, r, end=RUIN_LIGHT, bark=RUIN if i % 2 else RUIN_LIGHT, n=8)
        x += 0.04
    box(p, x + 0.006, FOOT / 2, -0.024, x + 0.05, 0.014, 0.024, RUIN, top=RUIN_LIGHT)  # capital block
    box(p, x + 0.012, 0.014, -0.016, x + 0.044, 0.024, 0.016, RUIN_LIGHT)
    return p


def column_row(name, rng, heights=(0.13, 0.06, 0.09)):
    """A short colonnade of standing column stumps on a plinth."""
    p = Part(name)
    n = len(heights)
    span = 0.05 * (n - 1)
    box(p, -span / 2 - 0.03, FOOT, -0.03, span / 2 + 0.03, 0.012, 0.03, RUIN_DARK, top=RUIN)
    for i, h in enumerate(heights):
        x = -span / 2 + i * 0.05
        box(p, x - 0.019, 0.012, -0.019, x + 0.019, 0.02, 0.019, RUIN)
        cylinder(p, 0.014, 0.02, h, RUIN_LIGHT, at=(x, 0, 0), n=8, r_top=0.013, top=RUIN_DARK)
        if h > 0.12:
            box(p, x - 0.021, h, -0.021, x + 0.021, h + 0.012, 0.021, RUIN, top=RUIN_LIGHT)
            box(p, x - 0.004, h + 0.012, -0.022, x + 0.04, h + 0.024, 0.022, RUIN_LIGHT)  # broken lintel
    return p


def ruined_arch(name, rng):
    """A gateway: two piers, the arch still springing from the left one and broken off past the crown."""
    p = Part(name)
    w, t, spring = 0.05, 0.032, 0.1  # half span, pier width, springing height
    d = 0.024
    box(p, -w - t - 0.006, FOOT, -d - 0.006, w + t + 0.006, 0.01, d + 0.006, RUIN_DARK, top=RUIN)
    box(p, -w - t, 0.01, -d, -w, spring, d, RUIN_LIGHT, top=RUIN_DARK)
    box(p, w, 0.01, -d, w + t, 0.075, d, RUIN, top=RUIN_DARK)
    for k in range(5):  # voussoirs from the left springing round past the crown
        a0, a1 = math.pi - k * math.pi / 8, math.pi - (k + 1) * math.pi / 8
        pts = []
        for a in (a0, a1):
            for r in (w, w + t):
                pts += [(math.cos(a) * r, spring + math.sin(a) * r, -d), (math.cos(a) * r, spring + math.sin(a) * r, d)]
        p.hull(pts, [[0, 1, 3, 2], [4, 5, 7, 6], [0, 2, 6, 4], [1, 3, 7, 5], [0, 1, 5, 4], [2, 3, 7, 6]],
               RUIN_LIGHT if k % 2 else RUIN)
    box(p, -w - t, spring, -d, -w - t + 0.02, spring + 0.05, d, RUIN, top=MOSS)  # haunch masonry
    lumpy_mound(p, 0.045, 0.035, 0.028, [RUIN_DARK, RUIN, RUIN_LIGHT], rng, at=(w + 0.01, 0, 0.035), rings=2, n=7)
    return p


def tower_stump(name, rng, r=0.045, h=0.06):
    """A round tower broken off with a ragged rim, higher at the back, and a dark doorway on +Z."""
    p = Part(name)
    n = 8
    cylinder(p, r, FOOT, h, RUIN, n=n, top=RUIN_DARK, phase=math.pi / 8)
    inner = r * 0.7
    for i in range(n):
        a0, a1 = math.pi / 8 + i * math.tau / n, math.pi / 8 + (i + 1) * math.tau / n
        back = -math.sin((a0 + a1) / 2)  # 1 at the back (-Z), -1 at the front
        top = h + max(0.0, 0.05 * (back + 0.4) * (0.6 + 0.6 * rng.random()))
        if top - h < 0.006:
            continue
        pts = [(math.cos(a) * rr, y, math.sin(a) * rr) for a in (a0, a1) for rr in (inner, r) for y in (h - 0.002, top)]
        p.hull(pts, [[0, 1, 3, 2], [4, 5, 7, 6], [0, 2, 6, 4], [1, 3, 7, 5], [0, 1, 5, 4], [2, 3, 7, 6]],
               RUIN_LIGHT if i % 2 else RUIN)
    apothem = r * math.cos(math.pi / 8)
    box(p, -0.011, 0.0, apothem - 0.004, 0.011, 0.04, apothem + 0.003, DARK)
    box(p, -0.03, h, -0.03, 0.0, h + 0.004, 0.0, MOSS)
    return p


def rubble(name, rng, r=0.06, h=0.035, colours=(RUIN, RUIN_DARK, RUIN_LIGHT)):
    p = Part(name)
    lumpy_mound(p, r, r * 0.8, h, list(colours), rng, y0=FOOT / 2, rings=2, n=7)
    return p


def paving(name, rng):
    """Cracked flagstones with grass between them; tilts with the ground."""
    p = Part(name)
    for i in range(3):
        for j in range(2):
            if (i, j) == (2, 1):
                continue
            x, z = (i - 1) * 0.042, (j - 0.5) * 0.042
            yy = 0.002 * rng.random()
            box(p, x - 0.019, FOOT / 2, z - 0.019, x + 0.019, 0.006 + yy, z + 0.019,
                [PAVING, RUIN_LIGHT, RUIN][(i + j) % 3])
    tufts(p, [(0.06, 0.03), (-0.064, -0.03), (0.02, 0.03), (-0.02, -0.032)], rng)
    return p


def city_ruins():
    rng = random.Random(71)
    return [
        at(ruined_arch("arch", rng), -0.08, -0.52),
        at(tower_stump("tower", rng), 0.56, -0.08),
        at(broken_wall("wall", rng, length=0.22, h=0.11, corner=0.14), -0.42, 0.06, 0.0),
        at(broken_wall("wall_b", rng, length=0.16, h=0.08), 0.44, 0.38, -0.9),
        at(column_row("columns", rng), 0.34, -0.48, -0.4),
        at(fallen_column("fallen", rng), -0.02, 0.6, 0.15),
        at(rubble("rubble", rng), -0.52, -0.36),
        at(paving("ground_paving", rng), -0.42, 0.46, 0.2),
    ]


# --- arcology ruins ---------------------------------------------------------------------------------------------

def shard_tower(name, w, d, h, cut, rng, tilt=0.0, bands=4):
    """A shattered composite tower: a slab broken off at an angle (top falls from h to h - cut across X)."""
    p = Part(name)
    hw, hd = w / 2, d / 2

    def lean(x, y, z):
        return (x + tilt * y, y, z)

    box(p, -hw - 0.008, FOOT, -hd - 0.008, hw + 0.008, 0.012, hd + 0.008, SLAB_DARK, top=SLAB_GREY)
    tops = [h, h - cut, h - cut * 0.7, h - cut * 0.1]  # (-x,-z), (+x,-z), (+x,+z), (-x,+z)
    pts = [lean(-hw, 0.012, -hd), lean(hw, 0.012, -hd), lean(hw, 0.012, hd), lean(-hw, 0.012, hd),
           lean(-hw, tops[0], -hd), lean(hw, tops[1], -hd), lean(hw, tops[2], hd), lean(-hw, tops[3], hd)]
    p.hull(pts, BOX_FACES, [SLAB, SLAB_GREY, SLAB_GREY, SLAB_GREY, SLAB, SLAB_GREY])
    # dark window bands and a dim teal strip on the front, clipped under the broken top
    for i in range(bands):
        y = 0.04 + i * (h - 0.05) / bands
        right = min(tops[2], tops[3])
        if y + 0.014 > right:
            break
        x0, x1 = lean(-hw + 0.006, y, 0)[0], lean(hw - 0.006, y, 0)[0]
        colour = TEAL_DIM if i == 1 else SLAB_DARK
        box(p, x0, y, hd - 0.002, x1, y + (0.006 if i == 1 else 0.012), hd + 0.003, colour)
    # bent beams sticking out of the break
    for k in range(2):
        x = -hw * 0.5 + k * hw
        y = h - cut * (0.5 + 0.5 * k) * (x + hw) / w - 0.004
        a = lean(x, y - 0.01, 0.0)
        b = (a[0] + 0.01, a[1] + 0.04, a[2] + 0.006 * (k - 0.5))
        c = (b[0] + 0.025 * (1 - 2 * k), b[1] + 0.012, b[2])
        beam(p, a, b, 0.006, STEEL)
        beam(p, b, c, 0.006, STEEL)
    return p


def twisted_girders(name, rng):
    """Steel girders twisted out of a heap of composite debris."""
    p = Part(name)
    lumpy_mound(p, 0.06, 0.05, 0.035, [SLAB_GREY, SLAB_DARK, SLAB], rng, y0=FOOT / 2, rings=2, n=7)
    paths = [[(-0.02, 0.02, 0.0), (-0.03, 0.08, 0.01), (0.01, 0.11, 0.0)],
             [(0.02, 0.02, -0.01), (0.05, 0.06, -0.02), (0.07, 0.07, 0.01)],
             [(0.0, 0.02, 0.02), (-0.04, 0.05, 0.05)]]
    for path in paths:
        for a, b in zip(path, path[1:]):
            beam(p, a, b, 0.008, STEEL)
    box(p, -0.03, 0.03, 0.01, 0.0, 0.034, 0.04, TEAL_DIM, yaw=0.4)
    return p


def fallen_slab(name, rng):
    """A broken wall panel lying tilted on the ground with a dim light strip."""
    p = Part(name)
    pts = [(-0.06, FOOT / 2, -0.03), (0.06, FOOT / 2, -0.03), (0.055, FOOT / 2, 0.03), (-0.06, FOOT / 2, 0.03),
           (-0.06, 0.03, -0.03), (0.04, 0.016, -0.03), (0.035, 0.008, 0.03), (-0.06, 0.012, 0.03)]
    p.hull(pts, BOX_FACES, [SLAB_GREY, SLAB, SLAB_GREY, SLAB_DARK, SLAB_GREY, SLAB_GREY])
    box(p, -0.05, 0.012, 0.03, 0.02, 0.016, 0.033, TEAL_DIM)
    return p


def city_ruins_arcology():
    rng = random.Random(83)
    return [
        at(shard_tower("tower", 0.08, 0.06, 0.3, 0.12, rng, tilt=0.06), -0.18, -0.5, 0.1),
        at(shard_tower("tower_b", 0.07, 0.06, 0.2, 0.1, rng, tilt=-0.08, bands=3), 0.46, -0.28, -0.4),
        at(shard_tower("tower_c", 0.07, 0.05, 0.14, 0.07, rng, bands=2), -0.52, 0.06, 0.6),
        at(twisted_girders("girders", rng), 0.5, 0.3, -0.3),
        at(fallen_slab("slab", rng), -0.28, 0.48, 0.3),
        at(rubble("rubble", rng, h=0.022, colours=(SLAB_GREY, SLAB_DARK, SLAB)), 0.12, 0.6),
        at(rubble("rubble_b", rng, r=0.05, h=0.02, colours=(SLAB, SLAB_GREY, SLAB_DARK)), -0.5, -0.32),
    ]


# --- dig site ---------------------------------------------------------------------------------------------------

def excavation(name, w, d, find):
    """A sandy pit with raised spoil lips, a string grid on posts and a find exposed in the floor.
    Tilts with the ground."""
    p = Part(name)
    hw, hd = w / 2, d / 2
    box(p, -hw - 0.012, FOOT / 2, -hd - 0.012, hw + 0.012, 0.004, hd + 0.012, SAND)
    box(p, -hw, 0.004, -hd, hw, 0.006, hd, SAND_DARK)
    for sz in (-1, 1):  # spoil lips on the long sides
        box(p, -hw - 0.012, 0.004, sz * hd, hw + 0.012, 0.012, sz * (hd + 0.012), SAND)
    for sx in (-1, 1):
        box(p, sx * hw, 0.004, -hd, sx * (hw + 0.012), 0.012, hd, SAND)
    xs = np.linspace(-hw, hw, 3)
    for x in xs:
        for z in (-hd, hd):
            box(p, x - 0.003, 0.0, z - 0.003, x + 0.003, 0.03, z + 0.003, TIMBER_LIGHT)
    for z in (-hd, hd):
        box(p, -hw, 0.026, z - 0.001, hw, 0.028, z + 0.001, STRING)
    for x in xs:
        box(p, x - 0.001, 0.026, -hd, x + 0.001, 0.028, hd, STRING)
    find(p)
    return p


def column_find(p):
    """A temple column stump and a fallen drum."""
    cylinder(p, 0.02, 0.0, 0.05, CHURCH_STONE, at=(-0.02, 0, -0.005), n=8, r_top=0.018, top=QUARRY_SHADE)
    box(p, -0.044, 0.006, -0.03, 0.004, 0.012, 0.02, QUARRY_SHADE)  # column base slab
    log(p, 0.012, 0.05, 0.018, 0.008, 0.014, end=QUARRY, bark=CHURCH_STONE, n=8)


def fossil_find(p):
    """The skeleton of a big beast: a curving spine, ribs and a long skull."""
    spine = [(-0.075, 0.009, 0.01), (-0.04, 0.011, -0.004), (0.0, 0.012, -0.008), (0.04, 0.011, 0.0),
             (0.07, 0.009, 0.012)]
    for a, b in zip(spine, spine[1:]):
        beam(p, a, b, 0.008, BONE)
    for x, z in ((-0.03, -0.004), (-0.01, -0.007), (0.01, -0.007), (0.03, -0.002)):
        for sz in (-1, 1):
            beam(p, (x, 0.012, z), (x + 0.004, 0.009, z + sz * 0.028), 0.004, BONE)
    skull = [(-0.075, 0.006, 0.0), (-0.075, 0.006, 0.02), (-0.075, 0.02, 0.01), (-0.11, 0.006, 0.006),
             (-0.11, 0.006, 0.014), (-0.11, 0.012, 0.01)]
    p.hull(skull, [[0, 1, 2], [3, 4, 5], [0, 1, 4, 3], [1, 2, 5, 4], [2, 0, 3, 5]], BONE)


def tent(name, colour=CANVAS):
    """A ridge tent, open flap toward +Z."""
    p = Part(name)
    box(p, -0.04, FOOT / 2, -0.035, 0.04, 0.002, 0.035, SAND_DARK)
    pts = [(-0.04, 0.002, -0.03), (0.04, 0.002, -0.03), (0.04, 0.002, 0.03), (-0.04, 0.002, 0.03),
           (-0.04, 0.05, 0.0), (0.04, 0.05, 0.0)]
    p.hull(pts, [[0, 1, 2, 3], [3, 2, 5, 4], [1, 0, 4, 5], [0, 3, 4], [2, 1, 5]],
           [colour, colour, CANVAS_DARK, CANVAS_DARK, CANVAS_DARK])
    pts = [(0.041, 0.002, -0.012), (0.041, 0.002, 0.012), (0.041, 0.03, 0.0)]
    pts += [(x + 0.002, y, z) for x, y, z in pts]
    p.hull(pts, [[0, 1, 2], [3, 4, 5], [0, 1, 4, 3], [1, 2, 5, 4], [2, 0, 3, 5]], DARK)
    box(p, -0.042, 0.0, -0.002, -0.038, 0.058, 0.002, TIMBER)
    box(p, 0.038, 0.0, -0.002, 0.042, 0.058, 0.002, TIMBER)
    return p


def sieve_and_crates(name):
    p = Part(name)
    # a sieve frame on four legs
    for sx in (-1, 1):
        for sz in (-1, 1):
            beam(p, (sx * 0.026, FOOT / 2, sz * 0.016), (sx * 0.022, 0.03, sz * 0.012), 0.004, TIMBER)
    box(p, -0.03, 0.03, -0.018, 0.03, 0.036, 0.018, TIMBER_LIGHT)
    box(p, -0.026, 0.031, -0.014, 0.026, 0.0365, 0.014, IRON)
    box(p, -0.024, 0.0, -0.016, 0.024, 0.008, 0.016, SAND)  # sifted heap under the sieve
    # crates
    for x, z, y0, s in ((0.06, -0.005, 0.0, 0.02), (0.06, -0.005, 0.04, 0.016), (0.1, 0.01, 0.0, 0.018)):
        box(p, x - s, FOOT / 2 if y0 == 0 else y0, z - s, x + s, y0 + 2 * s, z + s, BOARDS, top=TIMBER_LIGHT)
        box(p, x - s - 0.001, y0 + s - 0.003, z - s - 0.001, x + s + 0.001, y0 + s + 0.003, z + s + 0.001,
            BOARDS_DARK)
    return p


def spoil(name, rng):
    p = Part(name)
    lumpy_mound(p, 0.05, 0.04, 0.035, [SAND, SAND_DARK, SAND], rng, y0=FOOT / 2, rings=2, n=7)
    return p


def exploretemple():
    rng = random.Random(97)
    return [
        at(excavation("ground_dig", 0.17, 0.11, column_find), -0.28, -0.3, 0.15),
        at(excavation("ground_dig_b", 0.2, 0.1, fossil_find), 0.3, 0.38, -0.2),
        at(tent("tent"), 0.42, -0.4, 0.4),
        at(tent("tent_b", colour=srgb(0xD8C9A0)), -0.5, 0.32, -0.9),
        at(sieve_and_crates("sieve"), -0.22, 0.56, 0.1),
        at(spoil("spoil", rng), 0.58, 0.02),
        at(spoil("spoil_b", rng), -0.02, -0.6),
    ]


# --- Machu Picchu -----------------------------------------------------------------------------------------------

def terrace(name, length=0.13, depth=0.026, rise=0.03):
    """One farming terrace: a dry-stone retaining wall with a grassy top, its face toward +Z. The wall drops deep
    so the step stays seated on a steep peak, where it shows as a tall retaining wall."""
    p = Part(name)
    hl = length / 2
    outline = [(-hl, -depth), (hl, -depth), (hl, depth * 0.3), (hl - 0.012, depth), (-hl + 0.012, depth),
               (-hl, depth * 0.3)]
    prism(p, outline, DEEP, rise, GRANITE, top=TERRACE_GRASS)
    for i in range(4):  # coursing lines on the face
        y = rise - 0.012 - i * 0.024
        box(p, -hl + 0.012, y - 0.002, depth - 0.001, hl - 0.012, y, depth + 0.002, GRANITE_DARK)
    return p


def inca_house(name, w=0.09, d=0.06, wall=0.05, rise=0.06):
    """A trapezoid-walled granite house with a steep thatch roof and a trapezoid doorway on +Z."""
    p = Part(name)
    hw, hd = w / 2, d / 2
    box(p, -hw - 0.006, DEEP, -hd - 0.006, hw + 0.006, 0.008, hd + 0.006, GRANITE_DARK, top=GRANITE)
    k = 0.92  # walls lean in
    pts = [(-hw, 0.008, -hd), (hw, 0.008, -hd), (hw, 0.008, hd), (-hw, 0.008, hd),
           (-hw * k, wall, -hd * k), (hw * k, wall, -hd * k), (hw * k, wall, hd * k), (-hw * k, wall, hd * k)]
    p.hull(pts, BOX_FACES, GRANITE)
    # gable walls in stone up to the ridge (ridge along X means gables on +-X)
    for sx in (-1, 1):
        pts = [(sx * hw * k, wall, -hd * k), (sx * hw * k, wall, hd * k), (sx * hw * k * 0.98, wall + rise * 0.9, 0.0),
               (sx * (hw * k - 0.01), wall, -hd * k), (sx * (hw * k - 0.01), wall, hd * k),
               (sx * (hw * k * 0.98 - 0.01), wall + rise * 0.9, 0.0)]
        p.hull(pts, [[0, 1, 2], [3, 4, 5], [0, 1, 4, 3], [1, 2, 5, 4], [2, 0, 3, 5]], GRANITE)
    pts = [(-hw * k + 0.006, wall - 0.004, -hd - 0.012), (hw * k - 0.006, wall - 0.004, -hd - 0.012),
           (hw * k - 0.006, wall - 0.004, hd + 0.012), (-hw * k + 0.006, wall - 0.004, hd + 0.012),
           (-hw * k + 0.006, wall + rise, 0.0), (hw * k - 0.006, wall + rise, 0.0)]
    p.hull(pts, [[0, 1, 2, 3], [3, 2, 5, 4], [1, 0, 4, 5], [0, 3, 4], [2, 1, 5]],
           [THATCH_DARK, THATCH, THATCH, THATCH_DARK, THATCH_DARK])
    z = hd * k + 0.002  # front face sits just inside hd at door height; use a small proud slab
    pts = [(-0.011, 0.008, hd - 0.001), (0.011, 0.008, hd - 0.001), (0.007, 0.038, hd * 0.96 - 0.001),
           (-0.007, 0.038, hd * 0.96 - 0.001)]
    pts += [(x, y, zz + 0.004) for x, y, zz in pts]
    p.hull(pts, BOX_FACES, DARK)
    for sx in (-1, 1):  # trapezoid niche windows
        x = sx * hw * 0.6
        box(p, x - 0.005, 0.024, z - 0.006, x + 0.005, 0.036, z - 0.002, DARK)
    return p


def plaza(name, r=0.075):
    """The levelled central green on a granite platform. Level (not tilted), so it stays flat on a peak."""
    p = Part(name)
    prism(p, ngon(r, 8, math.pi / 8), DEEP, 0.02, GRANITE, top=GRANITE_DARK)
    prism(p, ngon(r - 0.008, 8, math.pi / 8), 0.02, 0.023, TERRACE_GRASS)
    for y in (0.004, -0.02, -0.044):
        prism(p, ngon(r + 0.0015, 8, math.pi / 8), y - 0.002, y, GRANITE_DARK)
    return p


def sun_stone(name):
    """The carved ritual stone on a stepped base."""
    p = Part(name)
    box(p, -0.03, DEEP, -0.03, 0.03, 0.012, 0.03, GRANITE_DARK, top=GRANITE)
    box(p, -0.02, 0.012, -0.02, 0.02, 0.024, 0.02, GRANITE)
    pts = [(-0.012, 0.024, -0.01), (0.012, 0.024, -0.01), (0.012, 0.024, 0.01), (-0.012, 0.024, 0.01),
           (-0.004, 0.06, -0.004), (0.006, 0.056, -0.004), (0.006, 0.05, 0.004), (-0.004, 0.054, 0.004)]
    p.hull(pts, BOX_FACES, GRANITE_DARK)
    return p


def machu_picchu():
    parts = []
    # rings of terraces around the summit; inner rings stand taller so the steps also read on gentler ground
    rings = [(0.64, 0.02, 0.24, (-1.5, -0.5, 0.5, 1.5, 2.6)), (0.44, 0.04, 0.2, (-0.55, 0.55))]
    for k, (r, rise, length, angles) in enumerate(rings):
        for i, theta in enumerate(angles):
            parts.append(at(terrace(f"terrace_{k}{i}", length=length, rise=rise), r * math.sin(theta),
                            r * math.cos(theta), theta))
    return parts + [
        at(inca_house("house", w=0.1), -0.24, -0.26, 0.5),
        at(inca_house("house_b", w=0.08, d=0.055, wall=0.045), 0.26, -0.22, -0.5),
        at(inca_house("house_c", w=0.08, d=0.055, wall=0.045, rise=0.055), -0.06, -0.48, 0.0),
        at(plaza("plaza"), 0.0, 0.1),
        at(sun_stone("sun_stone"), 0.34, 0.12),
    ]


MODELS = {
    "goody_hut": goody_hut,
    "city_ruins": city_ruins,
    "city_ruins_arcology": city_ruins_arcology,
    "improvement_improvement_exploretemple": exploretemple,
    "machu_picchu": machu_picchu,
}
