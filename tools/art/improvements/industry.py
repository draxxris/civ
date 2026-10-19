"""Workshops, factories, mills, wind and geothermal power."""
import random

from .kit import *  # noqa: F401,F403
from .kit import FOOT, Part, at

DEEP = -0.1  # foundations for models that are valid on hills and peaks

BRICK = srgb(0xA0503A)
BRICK_DARK = srgb(0x7A3A2B)
COAL = srgb(0x262422)
COAL_SHADE = srgb(0x3B3835)
SMOKE = srgb(0x9C9A96)
SMOKE_DARK = srgb(0x7F7D7A)
CONCRETE = srgb(0xB9B5AD)
CONCRETE_DARK = srgb(0x8E8A83)
ASPHALT = srgb(0x5A5C5F)
STEEL = srgb(0x77808A)
STEEL_LIGHT = srgb(0xA9B0B8)
GLASS = srgb(0x40607A)
GLASS_ROOF = srgb(0x7E9DB3)
WHITE = srgb(0xEEEEEA)
PALE = srgb(0xCDD0D2)
PALE_DARK = srgb(0xA9AEB2)
TEAL = srgb(0x2CB5B0)
TEAL_DARK = srgb(0x1C7E86)
STRIPE_RED = srgb(0xC23A2C)
SAFETY = srgb(0xD8AE2E)
CONTAINER_BLUE = srgb(0x3F6E9C)
CONTAINER_RED = srgb(0xA9443A)
SACK = srgb(0xDCCDA6)
SACK_SHADE = srgb(0xBFAE86)
SAIL = srgb(0xEAE2CC)
STEAM = srgb(0xF4F4F1)
STEAM_SHADE = srgb(0xDADEE0)
GRAVEL = srgb(0x8A857C)
PIPE = srgb(0xC9CDD1)

# --- primitives ------------------------------------------------------------------------------------------------

def tube(p, a, b, r, colour, n=6, ends=None, phase=0.0):
    """A round bar from point a to point b."""
    a, b = np.asarray(a, dtype=float), np.asarray(b, dtype=float)
    axis = (b - a) / np.linalg.norm(b - a)
    side = np.cross(axis, (0, 1, 0) if abs(axis[1]) < 0.9 else (1, 0, 0))
    side /= np.linalg.norm(side)
    up = np.cross(side, axis)
    ring = [(side * math.cos(phase + i * math.tau / n) + up * math.sin(phase + i * math.tau / n)) * r
            for i in range(n)]
    pts = [a + c for c in ring] + [b + c for c in ring]
    faces = [list(range(n)), list(range(n, 2 * n))] + [[i, (i + 1) % n, n + (i + 1) % n, n + i] for i in range(n)]
    p.hull(pts, faces, [ends or colour, ends or colour] + [colour] * n)


def blob(p, c, r, colours, n=7, rings=4, squash=0.85, hemi=False, twist=0):
    """A low-poly ball (or dome with hemi=True); colours alternate over the facets. twist=1 staggers the rings
    for a lumpier, cloud-like facet pattern."""
    cx, cy, cz = c
    colours = colours if isinstance(colours, list) else [colours]
    steps = range(1, rings + 1) if hemi else range(1, rings)
    span = math.pi / 2 if hemi else math.pi
    pts = [(cx, cy + r * squash, cz)]
    for i in steps:
        phi = i * span / rings
        for k in range(n):
            a = (k + 0.5 * (i % 2) * twist) * math.tau / n
            pts.append((cx + math.cos(a) * math.sin(phi) * r, cy + math.cos(phi) * r * squash,
                        cz + math.sin(a) * math.sin(phi) * r))
    nr = len(steps)
    faces = [[0, 1 + k, 1 + (k + 1) % n] for k in range(n)]
    for i in range(nr - 1):
        a0, b0 = 1 + i * n, 1 + (i + 1) * n
        faces += [[a0 + k, a0 + (k + 1) % n, b0 + (k + 1) % n, b0 + k] for k in range(n)]
    last = 1 + (nr - 1) * n
    if hemi:
        faces.append([last + k for k in range(n)])
    else:
        pts.append((cx, cy - r * squash, cz))
        faces += [[len(pts) - 1, last + k, last + (k + 1) % n] for k in range(n)]
    p.hull(pts, faces, [colours[i % len(colours)] for i in range(len(faces))])


def puffs(p, x, y, z, size=0.02, colours=(STEAM, STEAM_SHADE), drift=(0.008, 0.0), n=7, rings=4):
    """A rising, drifting column of overlapping smoke or steam blobs that swells and then thins out."""
    for i, (s, h) in enumerate(((0.75, 0.5), (1.0, 1.35), (0.85, 2.25), (0.55, 2.95))):
        blob(p, (x + drift[0] * i * i * 1.2, y + size * h, z + drift[1] * i * i * 1.2), size * s,
             list(colours), n=n, rings=rings, squash=0.8, twist=1)


def plate(p, corners, z0, t, colour):
    """A thin slab in an XY plane: corners are four (x, y) points, z0 the slab centre depth."""
    pts = [(x, y, z0 - t / 2) for x, y in corners] + [(x, y, z0 + t / 2) for x, y in corners]
    p.hull(pts, BOX_FACES, colour)


def wheel(p, x0, x1, y, z, r, colour, n=8, hub=None):
    """A wheel or disc turning about the X axis."""
    tube(p, (x0, y, z), (x1, y, z), r, colour, n=n, ends=hub)


def sacks(name, count=5, rng=None):
    """A cluster of grain sacks: plump bags tied at the neck, standing on end."""
    p = Part(name)
    rng = rng or random.Random(3)
    spots = [(-0.024, -0.006), (0.0, 0.004), (0.024, -0.004), (-0.012, -0.026), (0.014, -0.024)][:count]
    for i, (x, z) in enumerate(spots):
        h = 0.026 + 0.006 * rng.random()
        col, shade = (SACK, SACK_SHADE) if i % 2 == 0 else (SACK_SHADE, SACK)
        ph = rng.random()
        cylinder(p, 0.01, FOOT / 2, h * 0.45, col, at=(x, 0, z), n=6, r_top=0.012, phase=ph)
        cylinder(p, 0.012, h * 0.45, h, shade, at=(x, 0, z), n=6, r_top=0.005, phase=ph)
        cylinder(p, 0.004, h, h + 0.006, ROPE, at=(x, 0, z), n=5, r_top=0.006, phase=ph)
    return p


def handcart(name, load="planks"):
    """A two-wheeled hand cart with shafts toward +X."""
    p = Part(name)
    for sz in (-1, 1):  # wheels turn about Z
        tube(p, (0.0, 0.022, sz * 0.032 - 0.004), (0.0, 0.022, sz * 0.032 + 0.004), 0.022, TIMBER, n=8,
             ends=TIMBER_LIGHT)
    box(p, -0.045, 0.03, -0.028, 0.035, 0.036, 0.028, TIMBER_LIGHT)
    for sz in (-1, 1):
        box(p, -0.045, 0.036, sz * 0.028 - 0.003, 0.035, 0.05, sz * 0.028 + 0.003, BOARDS)
        beam(p, (0.03, 0.033, sz * 0.02), (0.085, 0.006, sz * 0.02), 0.006, TIMBER)
    if load == "planks":
        for i in range(3):
            box(p, -0.05, 0.036 + i * 0.006, -0.02 + i * 0.004, 0.05, 0.042 + i * 0.006, 0.006 + i * 0.004,
                BOARDS if i % 2 else TIMBER_LIGHT)
    else:
        for x in (-0.024, 0.002):
            box(p, x - 0.012, 0.036, -0.016, x + 0.012, 0.058, 0.004, SACK)
            box(p, x - 0.012, 0.036, 0.006, x + 0.012, 0.056, 0.024, SACK_SHADE)
    return p


# --- workshop --------------------------------------------------------------------------------------------------

def workshop_hall(name):
    """A timber workshop on a stone footing with a big open door and a stone forge chimney on the +X gable."""
    p = Part(name)
    w, d, wall = 0.17, 0.11, 0.07
    hw, hd = w / 2, d / 2
    box(p, -hw - 0.008, FOOT, -hd - 0.008, hw + 0.008, 0.012, hd + 0.008, STONE_DARK, top=STONE)
    box(p, -hw, 0.012, -hd, hw, wall, hd, BOARDS)
    for x in (-hw, -0.02, hw):
        box(p, x - 0.005, 0.012, hd - 0.002, x + 0.005, wall, hd + 0.004, TIMBER)
    box(p, -hw - 0.002, wall - 0.008, hd - 0.002, hw + 0.002, wall, hd + 0.005, TIMBER)
    box(p, -0.072, 0.012, hd - 0.001, -0.03, 0.06, hd + 0.003, DARK)  # open double door
    box(p, -0.08, 0.012, hd + 0.003, -0.074, 0.06, hd + 0.03, BOARDS_DARK)  # door leaf swung out
    box(p, 0.02, 0.035, hd - 0.001, 0.05, 0.054, hd + 0.003, WINDOW)
    top = prism_roof(p, w, d, wall, 0.06, 0.016, SHINGLE, BOARDS)
    box(p, -hw - 0.016, top - 0.008, -0.008, hw + 0.016, top + 0.004, 0.008, SHINGLE_DARK)
    # forge chimney against the +X gable, glowing hearth mouth at its foot
    cx = hw + 0.018
    box(p, cx - 0.02, FOOT, -0.024, cx + 0.02, 0.06, 0.024, STONE_DARK, top=STONE)
    box(p, cx - 0.014, 0.06, -0.016, cx + 0.014, top + 0.04, 0.016, STONE, top=DARK)
    box(p, cx - 0.017, top + 0.03, -0.019, cx + 0.017, top + 0.04, 0.019, STONE_DARK, top=DARK)
    box(p, cx + 0.008, 0.014, -0.012, cx + 0.021, 0.038, 0.012, srgb(0xE07A2C))
    puffs(p, cx, top + 0.04, 0.0, 0.022, (SMOKE, SMOKE_DARK), drift=(0.006, -0.003))
    return p


def smithy(name):
    """An open lean-to over an anvil, a workbench and a quench barrel."""
    p = Part(name)
    box(p, -0.06, FOOT, -0.04, 0.06, 0.008, 0.045, STONE_DARK, top=STONE)
    for sx in (-1, 1):
        for sz, h in ((-1, 0.085), (1, 0.06)):
            box(p, sx * 0.054 - 0.005, 0.008, sz * 0.036 - 0.005, sx * 0.054 + 0.005, h, sz * 0.036 + 0.005,
                TIMBER)
    box(p, -0.058, 0.008, -0.044, 0.058, 0.085, -0.036, BOARDS)  # back wall
    pts = [(-0.068, 0.085, -0.052), (0.068, 0.085, -0.052), (0.068, 0.058, 0.054), (-0.068, 0.058, 0.054)]
    pts += [(x, y + 0.008, z) for x, y, z in pts]
    p.hull(pts, BOX_FACES, SHINGLE)
    # anvil on a stump
    cylinder(p, 0.011, 0.008, 0.03, TIMBER, at=(-0.015, 0, 0.012), n=6, top=TIMBER_LIGHT)
    box(p, -0.03, 0.03, 0.006, 0.0, 0.042, 0.018, IRON)
    p.hull([(0.0, 0.034, 0.008), (0.0, 0.042, 0.008), (0.0, 0.042, 0.016), (0.0, 0.034, 0.016),
            (0.016, 0.041, 0.012)], [[0, 1, 2, 3], [0, 1, 4], [1, 2, 4], [2, 3, 4], [3, 0, 4]], IRON)
    # workbench against the back wall
    box(p, 0.0, 0.034, -0.034, 0.05, 0.04, -0.016, TIMBER_LIGHT)
    for x in (0.004, 0.046):
        box(p, x - 0.003, 0.008, -0.03, x + 0.003, 0.034, -0.02, TIMBER)
    box(p, 0.012, 0.04, -0.03, 0.03, 0.05, -0.022, IRON)
    # quench barrel
    cylinder(p, 0.012, 0.008, 0.034, BOARDS_DARK, at=(0.04, 0, 0.026), n=7, top=WATER)
    return p


def planks(name):
    """Sawn boards stacked on two bearers, with a few poles leaning on the stack."""
    p = Part(name)
    for x in (-0.04, 0.04):
        box(p, x - 0.008, FOOT / 2, -0.03, x + 0.008, 0.01, 0.03, TIMBER)
    for i in range(4):
        y = 0.01 + i * 0.008
        inset = i * 0.003
        box(p, -0.06 + inset, y, -0.026 + inset, 0.06 - inset, y + 0.007, 0.026 - inset,
            TIMBER_LIGHT if i % 2 == 0 else BOARDS)
    for z in (-0.012, 0.004):
        log(p, -0.065, 0.065, 0.048, z, 0.006)
    return p


def workshop():
    rng = random.Random(17)
    return [
        at(workshop_hall("workshop"), -0.18, -0.42),
        at(smithy("smithy"), 0.44, -0.14, -0.55),
        at(planks("planks"), 0.36, 0.4, 0.15),
        at(handcart("cart"), -0.46, 0.24, 0.5),
        at(woodpile("woodpile"), -0.54, -0.06, 1.3),
        at(sacks("sacks", 3, rng), 0.58, 0.16, -0.4),
    ]


# --- industry (factory) ----------------------------------------------------------------------------------------

def sawtooth_hall(name, w=0.26, d=0.15, wall=0.075, teeth=4):
    """A long brick factory hall; the sawtooth roof shows its saw profile on the +Z front."""
    p = Part(name)
    hw, hd = w / 2, d / 2
    box(p, -hw - 0.008, FOOT, -hd - 0.008, hw + 0.008, 0.012, hd + 0.008, STONE_DARK, top=STONE)
    box(p, -hw, 0.012, -hd, hw, wall, hd, BRICK)
    box(p, -hw - 0.003, wall - 0.008, -hd - 0.003, hw + 0.003, wall, hd + 0.003, BRICK_DARK)
    n = 6
    for i in range(n):  # tall arched-looking windows along the front
        x = -hw + (i + 0.5) * w / n
        if i == 2:
            box(p, x - 0.018, 0.012, hd - 0.002, x + 0.018, 0.058, hd + 0.004, DOOR)
            continue
        box(p, x - 0.01, 0.028, hd - 0.002, x + 0.01, 0.06, hd + 0.003, GLASS)
        box(p, x - 0.012, 0.024, hd - 0.002, x + 0.012, 0.028, hd + 0.004, STONE)
    box(p, hw - 0.002, 0.028, -0.03, hw + 0.003, 0.06, 0.03, GLASS)
    tw = w / teeth
    rise = 0.042
    for i in range(teeth):
        x0 = -hw + i * tw
        x1 = x0 + tw
        pts = [(x0, wall, -hd), (x1, wall, -hd), (x1, wall, hd), (x0, wall, hd),
               (x1, wall + rise, -hd), (x1, wall + rise, hd)]
        # faces: bottom, slope (slate), glazed upright facing +X, two brick gable triangles
        p.hull(pts, [[0, 1, 2, 3], [0, 3, 5, 4], [1, 2, 5, 4], [0, 1, 4], [3, 2, 5]],
               [BRICK, SLATE, GLASS_ROOF, BRICK, BRICK])
        box(p, x1 - 0.004, wall, -hd - 0.003, x1 + 0.003, wall + rise + 0.004, hd + 0.003, SLATE_DARK)
    return p


def smokestack(p, x, z, h, r=0.016):
    box(p, x - r - 0.008, DEEP, z - r - 0.008, x + r + 0.008, 0.03, z + r + 0.008, BRICK_DARK, top=STONE)
    cylinder(p, r, 0.03, h, BRICK, at=(x, 0, z), n=8, r_top=r * 0.75, top=DARK)
    cylinder(p, r * 0.85, h - 0.02, h + 0.002, BRICK_DARK, at=(x, 0, z), n=8, top=DARK)
    cylinder(p, r * 0.98, h * 0.55, h * 0.55 + 0.008, BRICK_DARK, at=(x, 0, z), n=8)


def stacks(name, heights=(0.28, 0.23)):
    p = Part(name)
    for (x, z), h in zip(((-0.032, 0.0), (0.036, 0.02)), heights):
        smokestack(p, x, z, h)
        puffs(p, x, h, z, 0.026, (SMOKE, SMOKE_DARK), drift=(0.008, -0.003))
    return p


def coal_heap(name, rng, r=0.075, h=0.05):
    p = Part(name)
    lumpy_mound(p, r, r * 0.8, h, [COAL, COAL_SHADE, COAL], rng, y0=FOOT / 2, rings=2, n=8)
    return p


def siding(name, length=0.3):
    """A rail siding along X on a gravel bed; tilts with the ground."""
    p = Part(name)
    hl = length / 2
    box(p, -hl, FOOT / 2, -0.03, hl, 0.004, 0.03, GRAVEL)
    n = 11
    for i in range(n):
        x = -hl + (i + 0.5) * length / n
        box(p, x - 0.006, 0.004, -0.022, x + 0.006, 0.01, 0.022, TIMBER)
    for sz in (-1, 1):
        box(p, -hl, 0.01, sz * 0.014 - 0.003, hl, 0.017, sz * 0.014 + 0.003, IRON)
    box(p, hl - 0.012, 0.004, -0.02, hl - 0.004, 0.03, 0.02, CLOTH_RED)  # buffer stop
    return p


def wagon(name, load=COAL):
    p = Part(name)
    for sx in (-1, 1):
        for sz in (-1, 1):
            tube(p, (sx * 0.026, 0.026, sz * 0.012), (sx * 0.026, 0.026, sz * 0.018), 0.01, IRON, n=6)
    box(p, -0.04, 0.026, -0.016, 0.04, 0.034, 0.016, IRON)
    box(p, -0.04, 0.034, -0.02, 0.04, 0.062, 0.02, BRICK_DARK, top=load)
    pts = [(-0.034, 0.062, -0.016), (0.034, 0.062, -0.016), (0.034, 0.062, 0.016), (-0.034, 0.062, 0.016),
           (-0.02, 0.074, -0.004), (0.02, 0.074, 0.004)]
    p.hull(pts, [[0, 1, 2, 3], [0, 1, 4], [1, 2, 5, 4], [2, 3, 5], [3, 0, 4, 5]], [load, load, COAL_SHADE, load,
                                                                                   COAL_SHADE])
    return p


def factory():
    rng = random.Random(29)
    return [
        at(sawtooth_hall("hall"), -0.14, -0.4),
        at(stacks("stacks"), 0.34, -0.44),
        at(coal_heap("coal", rng), 0.52, 0.04),
        at(coal_heap("coal_b", rng, r=0.05, h=0.035), 0.6, -0.16),
        at(siding("ground_siding"), -0.06, 0.44),
        at(wagon("wagon"), 0.12, 0.44),
        at(house("office", w=0.11, d=0.09, wall=0.07, rise=0.045, walls=BRICK, roof=SLATE, roof_end=SLATE_DARK,
                 timber=False, chimney=True, seed=1), -0.52, 0.04, 0.5),
    ]


# --- industrial complex ----------------------------------------------------------------------------------------

def plant(name):
    """A concrete plant hall with a glazed band, a taller block at one end and roof units."""
    p = Part(name)
    w, d, h = 0.22, 0.13, 0.075
    hw, hd = w / 2, d / 2
    box(p, -hw, FOOT, -hd, hw, h, hd, CONCRETE, top=STEEL)
    box(p, -hw - 0.003, h - 0.006, -hd - 0.003, hw + 0.003, h + 0.003, hd + 0.003, CONCRETE_DARK, top=STEEL)
    box(p, -hw + 0.01, 0.034, hd - 0.002, hw - 0.06, 0.056, hd + 0.003, GLASS)
    box(p, hw - 0.05, 0.004, hd - 0.002, hw - 0.012, 0.05, hd + 0.003, STEEL_LIGHT)  # roller door
    for i in range(4):
        y = 0.008 + i * 0.011
        box(p, hw - 0.05, y, hd + 0.003, hw - 0.012, y + 0.002, hd + 0.004, STEEL)
    # taller block at -X end
    box(p, -hw, FOOT, -hd, -hw + 0.07, h + 0.05, hd * 0.6, CONCRETE_DARK, top=STEEL)
    for y in (0.09, 0.106):
        box(p, -hw + 0.008, y, hd * 0.6 - 0.002, -hw + 0.062, y + 0.008, hd * 0.6 + 0.003, GLASS)
    box(p, -hw - 0.002, 0.09, -hd + 0.01, -hw + 0.003, 0.114, hd * 0.6 - 0.01, GLASS)
    for x, z in ((0.0, -0.02), (0.05, 0.02)):  # roof units
        box(p, x - 0.016, h + 0.003, z - 0.014, x + 0.016, h + 0.02, z + 0.014, STEEL_LIGHT, top=PALE)
    cylinder(p, 0.009, h + 0.003, h + 0.03, STEEL, at=(-0.02, 0, 0.03), n=6, top=DARK)
    return p


def banded_chimney(name, h=0.34, r=0.014):
    """A tall slim chimney with red and white bands at the top."""
    p = Part(name)
    box(p, -0.026, DEEP, -0.026, 0.026, 0.012, 0.026, CONCRETE_DARK, top=CONCRETE)
    cylinder(p, r, 0.012, h * 0.7, CONCRETE, at=(0, 0, 0), n=8, r_top=r * 0.85)
    bands = 4
    seg = h * 0.3 / bands
    for i in range(bands):
        y0 = h * 0.7 + i * seg
        rr = r * (0.85 - 0.12 * i / bands)
        cylinder(p, rr, y0, y0 + seg, STRIPE_RED if i % 2 == 0 else WHITE, n=8,
                 r_top=r * (0.85 - 0.12 * (i + 1) / bands), top=DARK)
    puffs(p, 0.0, h, 0.0, 0.02, (STEAM, STEAM_SHADE), drift=(0.007, -0.003))
    return p


def tanks(name):
    """Two squat storage tanks with shallow domed lids and ladders."""
    p = Part(name)
    box(p, -0.075, FOOT, -0.05, 0.07, 0.006, 0.055, CONCRETE_DARK, top=CONCRETE)
    for (x, z, r, h, col) in ((-0.036, -0.01, 0.036, 0.075, WHITE), (0.038, 0.02, 0.03, 0.06, PALE)):
        cylinder(p, r, 0.006, h, col, at=(x, 0, z), n=10)
        cylinder(p, r, h, h + 0.016, PALE_DARK, at=(x, 0, z), n=10, r_top=r * 0.3)
        cylinder(p, r + 0.002, h * 0.5, h * 0.5 + 0.006, TEAL_DARK if col is WHITE else SAFETY,
                 at=(x, 0, z), n=10)
        box(p, x - 0.004, 0.006, z + r - 0.002, x + 0.004, h, z + r + 0.004, STEEL)
    return p


def pipe_rack(name, length=0.18):
    """Steel bents along X carrying three pipes."""
    p = Part(name)
    hl = length / 2
    for x in np.linspace(-hl + 0.01, hl - 0.01, 4):
        for sz in (-1, 1):
            box(p, x - 0.003, DEEP, sz * 0.016 - 0.003, x + 0.003, 0.052, sz * 0.016 + 0.003, STEEL)
        box(p, x - 0.003, 0.046, -0.02, x + 0.003, 0.052, 0.02, STEEL)
    for z, r, col in ((-0.01, 0.006, STEEL_LIGHT), (0.004, 0.005, SAFETY), (0.014, 0.004, WHITE)):
        tube(p, (-hl, 0.058 + r - 0.006, z), (hl, 0.058 + r - 0.006, z), r, col, n=6)
    return p


def apron(name, w=0.24, d=0.16):
    """A concrete loading apron with yellow bay lines; tilts with the ground."""
    p = Part(name)
    hw, hd = w / 2, d / 2
    box(p, -hw, FOOT / 2, -hd, hw, 0.004, hd, ASPHALT, top=CONCRETE_DARK)
    for x in np.linspace(-hw + 0.03, hw - 0.03, 5):
        box(p, x - 0.002, 0.004, -hd + 0.01, x + 0.002, 0.0065, -0.01, SAFETY)
    box(p, -hw + 0.01, 0.004, 0.02, hw - 0.01, 0.0065, 0.024, SAFETY)
    return p


def lorry(p, x, z, cab=CLOTH_BLUE, load=CONTAINER_RED, yaw=0.0):
    """A small articulated lorry pointing +X."""
    def b(x0, y0, z0, x1, y1, z1, c, top=None):
        box(p, x0, y0, z0, x1, y1, z1, c, at=(x, 0, z), yaw=yaw, top=top)
    b(-0.05, 0.012, -0.014, 0.03, 0.044, 0.014, load)
    b(0.032, 0.008, -0.013, 0.054, 0.04, 0.013, cab)
    b(0.046, 0.026, -0.012, 0.0545, 0.038, 0.012, GLASS)
    b(-0.05, 0.006, -0.012, 0.054, 0.012, 0.012, IRON)
    for wx in (-0.038, -0.022, 0.044):
        for sz in (-1, 1):
            a = np.add(rot_y((wx, 0.008, sz * 0.012), yaw), (x, 0, z))
            b2 = np.add(rot_y((wx, 0.008, sz * 0.016), yaw), (x, 0, z))
            tube(p, a, b2, 0.008, DARK, n=6)


def containers(name):
    """Stacked shipping containers and a parked lorry on the apron."""
    p = Part(name)
    for (x, y, z, col) in ((-0.06, 0.0, -0.04, CONTAINER_BLUE), (-0.06, 0.026, -0.04, CONTAINER_RED),
                           (-0.06, 0.0, -0.008, SAFETY)):
        box(p, x - 0.04, y, z - 0.014, x + 0.04, y + 0.026, z + 0.014, col)
        box(p, x - 0.041, y + 0.004, z + 0.012, x + 0.041, y + 0.022, z + 0.0145, col, top=col)
    lorry(p, 0.04, 0.03)
    return p


def industrial_complex():
    return [
        at(plant("plant"), -0.26, -0.4),
        at(banded_chimney("chimney"), 0.1, -0.62),
        at(pipe_rack("piperack"), 0.16, -0.36),
        at(tanks("tanks"), 0.48, -0.26),
        at(apron("ground_apron"), -0.26, 0.4),
        at(containers("containers"), -0.26, 0.4),
        at(lorry_part("lorry"), 0.5, 0.3, 0.6),
    ]


def lorry_part(name):
    p = Part(name)
    lorry(p, 0.0, 0.0, cab=CLOTH_RED, load=WHITE)
    return p


# --- watermill -------------------------------------------------------------------------------------------------

def mill_house(name):
    """Stone ground floor, timber upper floor, tiled roof; the wheel hangs on the +X gable."""
    p = Part(name)
    w, d, wall = 0.13, 0.11, 0.11
    hw, hd = w / 2, d / 2
    box(p, -hw - 0.006, FOOT, -hd - 0.006, hw + 0.006, 0.01, hd + 0.006, STONE_DARK, top=STONE)
    box(p, -hw, 0.01, -hd, hw, 0.055, hd, STONE)
    box(p, -hw - 0.003, 0.055, -hd - 0.003, hw + 0.003, wall, hd + 0.003, PLASTER_WARM)
    for x in (-hw, 0.0, hw):
        box(p, x - 0.005, 0.055, hd, x + 0.005, wall, hd + 0.006, TIMBER)
    box(p, -hw - 0.004, 0.053, -hd - 0.004, hw + 0.004, 0.06, hd + 0.006, TIMBER)
    box(p, -0.04, 0.01, hd - 0.002, -0.012, 0.05, hd + 0.004, DOOR)
    box(p, 0.016, 0.026, hd - 0.002, 0.04, 0.044, hd + 0.003, WINDOW)
    for x in (-0.034, 0.034):
        box(p, x - 0.01, 0.072, hd + 0.002, x + 0.01, 0.092, hd + 0.007, WINDOW)
    # loft hoist door with a beam over it
    box(p, -0.012, 0.07, hd + 0.002, 0.012, 0.1, hd + 0.007, DOOR)
    box(p, -0.004, wall + 0.006, hd - 0.01, 0.004, wall + 0.014, hd + 0.03, TIMBER)
    top = prism_roof(p, w, d, wall, 0.07, 0.014, TILE, PLASTER_WARM)
    box(p, -hw - 0.014, top - 0.009, -0.008, hw + 0.014, top + 0.004, 0.008, TILE_DARK)
    return p


def water_wheel(name, r=0.07):
    """An undershot wheel turning about X, so it stands in the YZ plane, along the river. The part's origin is on the
    water, so the bottom paddles dip into the river. The axle runs from the mill wall (-X) to a timber trestle that
    stands in the water on the far side (+X)."""
    p = Part(name)
    y = r - 0.016
    # axle from the mill wall to the trestle
    tube(p, (-0.06, y, 0.0), (0.034, y, 0.0), 0.006, IRON, n=6)
    # two rims (thin rings as discs) with paddles between them
    for x0 in (-0.022, 0.012):
        wheel(p, x0, x0 + 0.006, y, 0.0, r, TIMBER, n=12, hub=TIMBER_LIGHT)
    n = 12
    for i in range(n):
        a = i * math.tau / n + 0.13
        c, s = math.cos(a), math.sin(a)
        ra, rb = r * 0.72, r * 1.06
        pts = []
        for x in (-0.018, 0.014):
            for rr, t in ((ra, -0.004), (ra, 0.004), (rb, 0.004), (rb, -0.004)):
                pts.append((x, y + s * rr + c * t, c * rr - s * t))
        p.hull(pts, BOX_FACES, TIMBER_LIGHT)

    # trestle in the river: two raked legs under a bearing block
    for sz in (-1, 1):
        beam(p, (0.03, -0.03, sz * 0.03), (0.03, y - 0.006, sz * 0.006), 0.006, TIMBER)
    box(p, 0.024, y - 0.01, -0.012, 0.036, y - 0.004, 0.012, TIMBER_LIGHT)
    return p


def millstones(name):
    """Two spare millstones, one flat, one leaning on it."""
    p = Part(name)
    cylinder(p, 0.03, FOOT / 2, 0.012, QUARRY_SHADE, n=10, top=QUARRY)
    cylinder(p, 0.006, 0.012, 0.0125, DARK, n=6)
    tube(p, (-0.01, 0.036, -0.03), (0.002, 0.036, -0.03), 0.03, QUARRY_SHADE, n=10, ends=QUARRY)
    return p


def watermill():
    """Convention for faceRiver models: the game puts the origin on the near waterline of the river, with +X straight
    across the water and Z along the river. The wheel stands in the water; everything else stays on the bank (-X)."""
    rng = random.Random(41)
    return [
        at(mill_house("mill"), -0.17, -0.06),
        at(water_wheel("nodecal_wheel"), 0.05, 0.0),
        at(sacks("sacks", 5, rng), -0.2, 0.2),
        at(handcart("cart", load="sacks"), -0.52, 0.2, 0.4),
        at(millstones("millstones"), -0.5, -0.2),
        at(fence("fence", 0.18), -0.36, -0.42, 0.1),
    ]


# --- windmill --------------------------------------------------------------------------------------------------

def sail_frame(p, hub, angle, length, colour, inner=0.026, width=0.032, z=0.0, lattice=4):
    """One sail in the XY plane: a stock (spar), a cloth panel on one side and lattice bars across it."""
    hx, hy = hub
    c, s = math.cos(angle), math.sin(angle)

    def pt(r, t):
        return (hx + r * c - t * s, hy + r * s + t * c)

    beam(p, (*pt(0.0, 0.0), z), (*pt(length, 0.0), z), 0.006, TIMBER)
    plate(p, [pt(inner, 0.003), pt(length - 0.004, 0.003), pt(length - 0.004, width), pt(inner, width)],
          z - 0.002, 0.002, colour)
    for k in range(lattice + 1):
        r = inner + k * (length - 0.004 - inner) / lattice
        beam(p, (*pt(r, 0.0), z + 0.0015), (*pt(r, width + 0.002), z + 0.0015), 0.003, TIMBER_LIGHT)
    beam(p, (*pt(inner, width + 0.002), z + 0.0015), (*pt(length - 0.004, width + 0.002), z + 0.0015), 0.003,
         TIMBER_LIGHT)


def tower_mill(name, h=0.17):
    """A tapering whitewashed tower on a stone base with a shingled cap and four lattice sails on the +Z face."""
    p = Part(name)
    cylinder(p, 0.058, DEEP, 0.02, STONE_DARK, n=8, r_top=0.056, top=STONE, phase=math.pi / 8)
    cylinder(p, 0.052, 0.02, h, PLASTER, n=8, r_top=0.038, phase=math.pi / 8)
    box(p, -0.013, 0.02, 0.044, 0.013, 0.06, 0.054, DOOR)
    for y, z in ((0.09, 0.046), (0.13, 0.041)):
        box(p, -0.008, y, z - 0.006, 0.008, y + 0.016, z + 0.004, WINDOW)
    # gallery ring and cap
    cylinder(p, 0.046, h - 0.006, h + 0.002, TIMBER, n=8, phase=math.pi / 8)
    cap = [(-0.044, h, -0.05), (0.044, h, -0.05), (0.044, h, 0.05), (-0.044, h, 0.05),
           (-0.012, h + 0.05, -0.03), (0.012, h + 0.05, -0.03), (0.012, h + 0.05, 0.038),
           (-0.012, h + 0.05, 0.038)]
    p.hull(cap, BOX_FACES, [SHINGLE, SHINGLE_DARK, SHINGLE, SHINGLE_DARK, SHINGLE, SHINGLE_DARK])
    # windshaft and hub poking out of the cap toward +Z
    hub = (0.0, h + 0.02)
    tube(p, (0.0, hub[1], 0.03), (0.0, hub[1], 0.066), 0.008, TIMBER, n=6)
    for k in range(4):
        sail_frame(p, hub, math.radians(38) + k * math.pi / 2, 0.15, SAIL, z=0.062)
    return p


def windmill():
    rng = random.Random(7)
    return [
        at(tower_mill("mill"), -0.12, -0.36),
        at(house("store", w=0.12, d=0.09, wall=0.06, rise=0.055, walls=BOARDS, roof=THATCH, timber=False,
                 chimney=False), 0.46, -0.16, -0.45),
        at(sacks("sacks", 5, rng), 0.36, 0.34, 0.2),
        at(handcart("cart", load="sacks"), -0.46, 0.24, 0.6),
        at(haystack("haystack"), 0.6, 0.12),
    ]


# --- windfarm --------------------------------------------------------------------------------------------------

def turbine(name, h=0.3, phase=0.0, blade=0.1):
    """A slim white three-bladed wind turbine; the rotor faces +Z."""
    p = Part(name)
    cylinder(p, 0.022, DEEP, 0.006, CONCRETE_DARK, n=8, top=CONCRETE)
    cylinder(p, 0.008, 0.006, h, WHITE, n=8, r_top=0.0045)
    box(p, -0.007, h - 0.004, -0.024, 0.007, h + 0.01, 0.012, WHITE, top=PALE)
    cylinder(p, 0.0075, 0.0, 0.016, PALE, at=(0, 0, 0), n=6)  # base band, slightly greyed
    hub_y, hub_z = h + 0.003, 0.02
    tube(p, (0, hub_y, 0.01), (0, hub_y, hub_z), 0.007, WHITE, n=6)
    p.hull([(0, hub_y, hub_z + 0.01)] + [(math.cos(a) * 0.007, hub_y + math.sin(a) * 0.007, hub_z)
                                         for a in np.linspace(0, math.tau, 6, endpoint=False)],
           [[1, 2, 3, 4, 5, 6]] + [[0, 1 + i, 1 + (i + 1) % 6] for i in range(6)], PALE)
    for k in range(3):
        a = phase + k * math.tau / 3
        c, s = math.cos(a), math.sin(a)

        def pt(r, t):
            return (r * c - t * s, hub_y + r * s + t * c)
        corners = [pt(0.006, -0.004), pt(0.022, 0.007), pt(blade, 0.002), pt(blade, -0.002)]
        plate(p, corners, hub_z + 0.004, 0.003, WHITE if k else PALE)
    return p


def substation(name):
    """Transformers, insulator posts and a control hut."""
    p = Part(name)
    box(p, -0.06, FOOT, -0.045, -0.02, 0.04, -0.005, CONCRETE, top=CONCRETE_DARK)
    box(p, -0.036, 0.004, -0.0055, -0.024, 0.03, -0.004, DOOR)
    for x in (0.01, 0.045):
        box(p, x - 0.012, 0.0, -0.034, x + 0.012, 0.026, -0.014, STEEL, top=STEEL_LIGHT)
        for dx in (-0.006, 0.006):
            cylinder(p, 0.0025, 0.026, 0.042, PALE, at=(x + dx, 0, -0.024), n=5)
    for x in (-0.04, 0.0, 0.04):
        box(p, x - 0.002, 0.0, 0.026, x + 0.002, 0.05, 0.03, STEEL)
    box(p, -0.042, 0.046, 0.026, 0.042, 0.05, 0.03, STEEL)
    return p


def gravel_pad(name, w=0.15, d=0.11):
    p = Part(name)
    hw, hd = w / 2, d / 2
    box(p, -hw, FOOT / 2, -hd, hw, 0.003, hd, GRAVEL)
    for x in np.linspace(-hw, hw, 7):
        for z in (-hd, hd):
            box(p, x - 0.002, 0.0, z - 0.002, x + 0.002, 0.024, z + 0.002, STEEL_LIGHT)
    for x in (-hw, hw):
        for z in np.linspace(-hd, hd, 5):
            box(p, x - 0.002, 0.0, z - 0.002, x + 0.002, 0.024, z + 0.002, STEEL_LIGHT)
    for z in (-hd, hd):
        box(p, -hw, 0.021, z - 0.001, hw, 0.024, z + 0.001, STEEL_LIGHT)
    for x in (-hw, hw):
        box(p, x - 0.001, 0.021, -hd, x + 0.001, 0.024, hd, STEEL_LIGHT)
    return p


def windfarm():
    return [
        at(turbine("turbine", 0.3, 0.3), -0.38, -0.36, 0.25),
        at(turbine("turbine_b", 0.25, 1.1), 0.36, -0.5, -0.15),
        at(turbine("turbine_c", 0.22, 0.75), 0.54, 0.22, -0.35),
        at(gravel_pad("ground_substation"), -0.36, 0.36),
        at(substation("substation"), -0.36, 0.36),
    ]


# --- windtrap --------------------------------------------------------------------------------------------------

def spire(name, h=0.26):
    """A slender white composite spire with three teal vanes and a glowing tip."""
    p = Part(name)
    cylinder(p, 0.026, DEEP, 0.012, PALE_DARK, n=6, top=PALE)
    cylinder(p, 0.014, 0.012, h, WHITE, n=6, r_top=0.003)
    cylinder(p, 0.0145, 0.03, 0.036, TEAL, n=6, r_top=0.0142)
    for k in range(3):
        a = k * math.tau / 3 + 0.3
        c, s = math.cos(a), math.sin(a)
        fin = [(0.008, 0.04), (0.034, 0.07), (0.016, h * 0.8), (0.004, h * 0.82)]  # (radius, height)
        pts = []
        for t in (-0.0015, 0.0015):
            for r, y in fin:
                pts.append((c * r - s * t, y, s * r + c * t))
        p.hull(pts, BOX_FACES, [TEAL] * 6)
    blob(p, (0, h, 0), 0.006, [TEAL, srgb(0x7FE3DC)], n=5, rings=3, squash=1.0)
    return p


def condenser(name):
    """A tall white condensation tank with a ball top, teal bands, and a squat collector beside it."""
    p = Part(name)
    box(p, -0.05, DEEP, -0.034, 0.05, 0.008, 0.034, PALE_DARK, top=PALE)
    cylinder(p, 0.026, 0.008, 0.1, WHITE, at=(-0.016, 0, 0), n=10)
    for y in (0.03, 0.07):
        cylinder(p, 0.0272, y, y + 0.005, TEAL, at=(-0.016, 0, 0), n=10)
    blob(p, (-0.016, 0.1, 0), 0.026, [WHITE, PALE], n=10, rings=2, squash=0.8, hemi=True)
    box(p, -0.021, 0.008, 0.024, -0.011, 0.1, 0.0285, TEAL_DARK)  # gauge strip
    cylinder(p, 0.016, 0.008, 0.042, PALE, at=(0.03, 0, 0.004), n=8, top=TEAL)
    tube(p, (-0.0, 0.034, 0.002), (0.02, 0.034, 0.004), 0.004, TEAL_DARK, n=5)
    return p


def dome(name):
    """A small white dome with a teal ring and an entrance."""
    p = Part(name)
    cylinder(p, 0.06, DEEP, 0.008, PALE_DARK, n=10, top=PALE)
    cylinder(p, 0.052, 0.008, 0.016, TEAL, n=10)
    blob(p, (0, 0.016, 0), 0.052, [WHITE, PALE], n=10, rings=3, squash=0.85, hemi=True)
    box(p, -0.012, 0.008, 0.036, 0.012, 0.04, 0.058, WHITE, top=PALE)
    box(p, -0.008, 0.008, 0.057, 0.008, 0.032, 0.0595, TEAL_DARK)
    return p


def windtrap():
    return [
        at(spire("spire", 0.27), -0.38, -0.4),
        at(spire("spire_b", 0.22), 0.16, -0.58),
        at(spire("spire_c", 0.19), 0.56, -0.1),
        at(condenser("condenser"), 0.42, 0.36),
        at(dome("dome"), -0.42, 0.28),
    ]


# --- geothermal ------------------------------------------------------------------------------------------------

def power_house(name):
    """A light concrete turbine hall with a vented roof monitor and steel doors."""
    p = Part(name)
    w, d, h = 0.17, 0.11, 0.07
    hw, hd = w / 2, d / 2
    box(p, -hw, DEEP, -hd, hw, h, hd, CONCRETE, top=STEEL)
    box(p, -hw - 0.003, h - 0.006, -hd - 0.003, hw + 0.003, h + 0.003, hd + 0.003, CONCRETE_DARK, top=STEEL)
    box(p, -hw * 0.7, h + 0.003, -hd * 0.4, hw * 0.7, h + 0.02, hd * 0.4, STEEL_LIGHT, top=STEEL)
    box(p, -hw * 0.7 + 0.004, h + 0.006, hd * 0.4, hw * 0.7 - 0.004, h + 0.017, hd * 0.4 + 0.002, DARK)
    box(p, -hw + 0.012, 0.03, hd - 0.002, hw - 0.05, 0.05, hd + 0.003, GLASS)
    box(p, hw - 0.04, 0.0, hd - 0.002, hw - 0.012, 0.046, hd + 0.003, STEEL_LIGHT)
    box(p, hw - 0.002, 0.03, -0.03, hw + 0.003, 0.05, 0.03, GLASS)
    return p


def steam_stack(p, x, z, h, r):
    """A short flared cooling stack with a puff of steam."""
    cylinder(p, r * 1.15, DEEP, 0.008, CONCRETE_DARK, at=(x, 0, z), n=8, top=CONCRETE)
    cylinder(p, r, 0.008, h * 0.65, CONCRETE, at=(x, 0, z), n=8, r_top=r * 0.78)
    cylinder(p, r * 0.78, h * 0.65, h, CONCRETE, at=(x, 0, z), n=8, r_top=r * 0.86, top=DARK)
    cylinder(p, r * 0.8, h * 0.85, h * 0.85 + 0.006, CONCRETE_DARK, at=(x, 0, z), n=8)
    puffs(p, x, h, z, r * 1.05, drift=(0.006, -0.002))


def steam_stacks(name):
    p = Part(name)
    steam_stack(p, -0.03, 0.0, 0.1, 0.034)
    steam_stack(p, 0.045, 0.03, 0.08, 0.028)
    return p


def wellhead(name):
    """A borehole head: valve tree on a pad, venting steam."""
    p = Part(name)
    box(p, -0.03, DEEP, -0.03, 0.03, 0.008, 0.03, CONCRETE_DARK, top=CONCRETE)
    cylinder(p, 0.01, 0.008, 0.05, STEEL_LIGHT, n=8, top=STEEL)
    for y in (0.022, 0.04):
        cylinder(p, 0.014, y, y + 0.006, CLOTH_RED, n=8)
    tube(p, (0.0, 0.03, 0.0), (0.0, 0.03, -0.03), 0.006, PIPE, n=6)
    puffs(p, 0.0, 0.05, 0.0, 0.016, drift=(0.005, -0.003), n=6, rings=3)
    return p


def pipe_run(name, length, r=0.007, y=0.026, loop_h=0.04, loop_w=0.034):
    """An insulated pipe along X on short trestles, with an up-and-over expansion loop in the middle."""
    p = Part(name)
    hl = length / 2
    lw = loop_w / 2
    top = y + loop_h
    pts = [(-hl, y, 0.0), (-lw, y, 0.0), (-lw, top, 0.0), (lw, top, 0.0), (lw, y, 0.0), (hl, y, 0.0)]
    for a, b in zip(pts, pts[1:]):
        d = (np.subtract(b, a)) / np.linalg.norm(np.subtract(b, a)) * r  # overlap at the bends
        tube(p, np.subtract(a, d) if a != pts[0] else a, np.add(b, d) if b != pts[-1] else b, r, PIPE, n=6)
    for x in list(np.linspace(-hl + 0.01, -lw - 0.012, 2)) + list(np.linspace(lw + 0.012, hl - 0.01, 2)):
        box(p, x - 0.004, DEEP, -0.008, x + 0.004, y - r, 0.008, STEEL)
        box(p, x - 0.005, y - r - 0.003, -0.01, x + 0.005, y - r, 0.01, STEEL)
    return p


def run(name, a, b, trim=(0.04, 0.04)):
    """A pipe_run part laid from (x, z) point a to point b, shortened by trim at each end (world units)."""
    dx, dz = b[0] - a[0], b[1] - a[1]
    dist = math.hypot(dx, dz)
    ux, uz = dx / dist, dz / dist
    mid_t = (trim[0] + dist - trim[1]) / 2
    length = (dist - trim[0] - trim[1]) / SCALE
    return at(pipe_run(name, length), a[0] + ux * mid_t, a[1] + uz * mid_t, math.atan2(-dz, dx))


def geothermal():
    well, well_b = (0.56, 0.18), (-0.54, 0.14)
    return [
        at(power_house("plant"), -0.3, -0.4),
        at(steam_stacks("stacks"), 0.34, -0.42),
        at(wellhead("well"), *well),
        at(wellhead("well_b"), *well_b),
        run("pipes", well, (0.44, -0.26), (0.08, 0.06)),
        run("pipes_b", well_b, (-0.36, -0.22), (0.08, 0.04)),
    ]


MODELS = {
    "improvement_improvement_workshop": workshop,
    "improvement_improvement_3factory": factory,
    "improvement_improvement_5factory": industrial_complex,
    "improvement_improvement_watermill": (watermill, "tile", {"faceRiver": True}),
    "improvement_improvement_windmill": windmill,
    "improvement_improvement_desertwindmill": windfarm,
    "improvement_improvement_windtrap": windtrap,
    "improvement_improvement_geothermal_borehole": geothermal,
}
