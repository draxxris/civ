"""Forts, bunkers and command centres."""
import random

from .kit import *  # noqa: F401,F403
from .kit import FOOT, Part, at

WALL = srgb(0xC2B8A3)
WALL_TOP = srgb(0xA39985)
WALL_SHADE = srgb(0x8C8372)
CONCRETE = srgb(0xA3A199)
CONCRETE_TOP = srgb(0xB9B7AF)
CONCRETE_DARK = srgb(0x77756E)
SANDBAG = srgb(0xB9A57B)
SANDBAG_DARK = srgb(0x9A8862)
OLIVE = srgb(0x5E6640)
TRENCH = srgb(0x4A3624)
BERM = srgb(0x7D6444)
COMPOSITE = srgb(0xE9ECEE)
COMPOSITE_GREY = srgb(0xC3C9CF)
COMPOSITE_DARK = srgb(0x8B939B)
GLASS = srgb(0x23313B)
TEAL = srgb(0x3FD8C8)
PAD = srgb(0xB4BAC0)


def axis_frustum(part, base, axis, r0, r1, colour, n=8, cap=None):
    """A frustum from point base along vector axis (its length is the height); r1 = 0 makes a cone."""
    base, axis = np.asarray(base, dtype=float), np.asarray(axis, dtype=float)
    d = axis / np.linalg.norm(axis)
    u = np.cross(d, (0, 1, 0) if abs(d[1]) < 0.9 else (1, 0, 0))
    u /= np.linalg.norm(u)
    v = np.cross(d, u)
    ring = [math.cos(i * math.tau / n) * u + math.sin(i * math.tau / n) * v for i in range(n)]
    pts = [base + r0 * c for c in ring]
    if r1 > 1e-4:
        pts += [base + axis + r1 * c for c in ring]
        faces = [list(range(n)), list(range(n, 2 * n))] + [[i, (i + 1) % n, n + (i + 1) % n, n + i] for i in range(n)]
        part.hull(pts, faces, [colour, cap or colour] + [colour] * n)
    else:
        pts.append(base + axis)
        faces = [list(range(n))] + [[i, (i + 1) % n, n] for i in range(n)]
        part.hull(pts, faces, colour)


# --- fort -------------------------------------------------------------------------------------------------------

FORT_H = 0.46  # half side of the square ring (world units)
WALL_H = 0.06
WALL_T = 0.02
DEEP = -0.1  # walls and towers reach this deep so they stay seated on hills


def wall_segment(name, length):
    """A crenellated stone curtain wall along X, centred on the origin."""
    p = Part(name)
    hl, ht = length / 2, WALL_T / 2
    box(p, -hl, DEEP, -ht - 0.004, hl, 0.012, ht + 0.004, WALL_SHADE, top=WALL_TOP)
    box(p, -hl, 0.012, -ht, hl, WALL_H, ht, WALL, top=WALL_TOP)
    n = max(2, int(length / 0.034))
    for i in range(n):
        x = -hl + (i + 0.5) * length / n
        box(p, x - 0.008, WALL_H, -ht, x + 0.008, WALL_H + 0.014, ht, WALL, top=WALL_TOP)
    return p


def tower(name, flag=False):
    """A square corner tower with arrow slits and a red pyramid roof."""
    p = Part(name)
    t = 0.036
    h = 0.11
    box(p, -t - 0.005, DEEP, -t - 0.005, t + 0.005, 0.014, t + 0.005, WALL_SHADE, top=WALL_TOP)
    box(p, -t, 0.014, -t, t, h, t, WALL, top=WALL_TOP)
    box(p, -t - 0.004, h - 0.006, -t - 0.004, t + 0.004, h + 0.004, t + 0.004, WALL_TOP)
    for yaw in (0.0, math.pi / 2, math.pi, -math.pi / 2):  # slits on all four faces
        box(p, -0.004, 0.05, t - 0.002, 0.004, 0.08, t + 0.003, DARK, yaw=yaw)
    cylinder(p, t * 1.55, h + 0.004, h + 0.07, TILE, n=4, r_top=0.0, phase=math.pi / 4)
    if flag:
        top = h + 0.07
        box(p, -0.002, top - 0.01, -0.002, 0.002, top + 0.07, 0.002, IRON)
        pts = [(0.002, top + 0.068, 0.0), (0.002, top + 0.038, 0.0), (0.06, top + 0.054, 0.0)]
        pts += [(x, y, 0.004) for x, y, _ in pts]
        p.hull(pts, [[0, 1, 2], [3, 4, 5], [0, 1, 4, 3], [1, 2, 5, 4], [2, 0, 3, 5]], CLOTH_RED)
    return p


def gatehouse(name):
    """Two squat piers over a timber gate, facing +Z."""
    p = Part(name)
    hw, d, h = 0.06, 0.03, 0.09
    box(p, -hw, DEEP, -d, hw, 0.012, d, WALL_SHADE, top=WALL_TOP)
    for sx in (-1, 1):
        box(p, sx * 0.024, 0.012, -d, sx * hw, h, d, WALL, top=WALL_TOP)
    box(p, -0.024, 0.06, -d, 0.024, h, d, WALL, top=WALL_TOP)  # lintel block
    box(p, -0.024, 0.012, -0.006, 0.024, 0.06, 0.006, DOOR)  # gate leaves
    box(p, -0.002, 0.012, 0.005, 0.002, 0.06, 0.008, TIMBER)
    for i in range(4):  # battlements over the gate
        x = -hw + (i + 0.5) * 2 * hw / 4
        box(p, x - 0.009, h, -d, x + 0.009, h + 0.015, d, WALL, top=WALL_TOP)
    box(p, -0.01, 0.07, d - 0.002, 0.01, 0.082, d + 0.003, DARK)  # murder hole window over the gate
    return p


def fort():
    s = FORT_H
    side = s / 2  # authored length of half a side wall (world length s)
    gate = 0.11  # world half width of the gatehouse opening
    front = (s - gate) / 2
    walls = []
    for name, x, z, yaw in (("wall_back_l", -s / 2, -s, 0.0), ("wall_back_r", s / 2, -s, 0.0),
                            ("wall_left_b", -s, -s / 2, math.pi / 2), ("wall_left_f", -s, s / 2, math.pi / 2),
                            ("wall_right_b", s, -s / 2, math.pi / 2), ("wall_right_f", s, s / 2, math.pi / 2)):
        walls.append(at(wall_segment(name, side + 0.01), x, z, yaw))
    for sx in (-1, 1):
        walls.append(at(wall_segment(f"wall_front_{'lr'[sx > 0]}", front + 0.01), sx * (gate + s) / 2, s))
    towers = [at(tower(f"tower_{i}", flag=(i == 0)), x, z) for i, (x, z) in
              enumerate(((-s, -s), (s, -s), (-s, s), (s, s)))]
    return walls + towers + [at(gatehouse("gatehouse"), 0.0, s)]


# --- bunker -----------------------------------------------------------------------------------------------------

def sandbag(part, x, y, z, yaw=0.0):
    pts = [(-0.014, 0.0, -0.008), (0.014, 0.0, -0.008), (0.014, 0.0, 0.008), (-0.014, 0.0, 0.008),
           (-0.012, 0.011, -0.006), (0.012, 0.011, -0.006), (0.012, 0.011, 0.006), (-0.012, 0.011, 0.006)]
    shade = SANDBAG if int((x + z) * 1000) % 2 else SANDBAG_DARK
    part.hull(place([(px, py + y, pz) for px, py, pz in pts], (x, 0, z), yaw), BOX_FACES,
              [shade, SANDBAG, shade, shade, shade, shade])


def pillbox(name, r=0.065):
    """A squat hexagonal concrete pillbox: firing slits on the front faces and a slab roof."""
    p = Part(name)
    phase = math.pi / 6
    cylinder(p, r + 0.006, DEEP, 0.008, CONCRETE_DARK, n=6, phase=phase)
    cylinder(p, r, 0.008, 0.045, CONCRETE, n=6, phase=phase)
    cylinder(p, r + 0.01, 0.045, 0.058, CONCRETE_DARK, n=6, phase=phase, top=CONCRETE_TOP)
    cylinder(p, r * 0.55, 0.058, 0.066, CONCRETE_TOP, n=6, phase=phase)
    apothem = r * math.cos(math.pi / 6)
    for i in range(6):
        a_mid = phase + (i + 0.5) * math.tau / 6
        yaw = math.pi / 2 - a_mid
        if math.cos(yaw) > -0.1:  # slits on the faces that look forward or sideways
            box(p, -0.02, 0.026, apothem - 0.004, 0.02, 0.036, apothem + 0.002, DARK, yaw=yaw)
    return p


def sandbag_wall(name, n=6, rows=2, curve=0.6):
    """A shallow arc of sandbags two layers high, bulging toward +Z."""
    p = Part(name)
    box(p, -0.014 * n, FOOT / 2, -0.012, 0.014 * n, 0.0, 0.012, SANDBAG_DARK)
    for row in range(rows):
        k = n - row
        for i in range(k):
            t = (i - (k - 1) / 2) / max(n - 1, 1)
            x = t * 0.026 * (n - 1)
            z = -curve * 0.1 * t * t
            sandbag(p, x, row * 0.011, z, yaw=curve * 1.2 * t)
    return p


def trench(name, segments):
    """Zigzag trench: a dark cut with earth berms on both lips. Tilts with the ground."""
    p = Part(name)
    for (x0, z0), (x1, z1) in zip(segments, segments[1:]):
        length = math.hypot(x1 - x0, z1 - z0)
        yaw = math.atan2(z1 - z0, x1 - x0)
        c = ((x0 + x1) / 2, 0, (z0 + z1) / 2)
        hl = length / 2 + 0.008
        box(p, -hl, FOOT, -0.018, hl, 0.002, 0.018, BERM, at=c, yaw=-yaw)
        box(p, -hl, 0.002, -0.009, hl, 0.004, 0.009, TRENCH, at=c, yaw=-yaw)
        for side in (-1, 1):
            pts = [(-hl, 0.002, side * 0.009), (hl, 0.002, side * 0.009), (hl, 0.002, side * 0.024),
                   (-hl, 0.002, side * 0.024), (-hl, 0.013, side * 0.015), (hl, 0.013, side * 0.015)]
            p.hull(place(pts, c, -yaw), [[0, 1, 2, 3], [0, 1, 5, 4], [3, 2, 5, 4], [0, 3, 4], [1, 2, 5]],
                   [BERM, BERM, BERM, BERM, BERM])
    return p


def radar(name):
    """A lattice mast on a concrete pad carrying a tilted radar paddle and a whip antenna."""
    p = Part(name)
    box(p, -0.03, DEEP, -0.03, 0.03, 0.008, 0.03, CONCRETE_DARK, top=CONCRETE)
    for sx in (-1, 1):
        for sz in (-1, 1):
            beam(p, (sx * 0.022, 0.008, sz * 0.022), (sx * 0.006, 0.13, sz * 0.006), 0.005, IRON)
    for y, w in ((0.05, 0.016), (0.095, 0.01)):
        box(p, -w, y - 0.003, -w, w, y + 0.003, w, IRON)
    box(p, -0.012, 0.13, -0.012, 0.012, 0.142, 0.012, OLIVE)
    paddle = [(-0.045, 0.145, -0.004), (0.045, 0.145, -0.004), (0.045, 0.145, 0.004), (-0.045, 0.145, 0.004),
              (-0.045, 0.18, -0.016), (0.045, 0.18, -0.016), (0.045, 0.18, -0.008), (-0.045, 0.18, -0.008)]
    p.hull(paddle, BOX_FACES, [OLIVE, OLIVE, OLIVE, OLIVE, CONCRETE_TOP, OLIVE])
    box(p, -0.002, 0.142, 0.014, 0.002, 0.22, 0.018, IRON)
    box(p, -0.006, 0.0, 0.026, 0.006, 0.02, 0.04, OLIVE)  # generator box
    return p


def tank_traps(name, count=4):
    """A row of concrete dragon's teeth."""
    p = Part(name)
    for i in range(count):
        x = (i - (count - 1) / 2) * 0.04
        pts = [(x - 0.013, FOOT / 2, -0.013), (x + 0.013, FOOT / 2, -0.013), (x + 0.013, FOOT / 2, 0.013),
               (x - 0.013, FOOT / 2, 0.013), (x - 0.004, 0.032, -0.004), (x + 0.004, 0.032, -0.004),
               (x + 0.004, 0.032, 0.004), (x - 0.004, 0.032, 0.004)]
        p.hull(pts, BOX_FACES, [CONCRETE, CONCRETE_TOP, CONCRETE, CONCRETE_DARK, CONCRETE, CONCRETE_DARK])
    return p


def bunker():
    return [
        at(pillbox("pillbox", r=0.075), -0.34, -0.34, 0.3),
        at(pillbox("pillbox_b", r=0.062), 0.46, -0.06, -0.5),
        at(radar("radar"), 0.12, -0.56),
        at(trench("ground_trench", [(-0.11, 0.0), (-0.05, 0.035), (0.02, -0.01), (0.1, 0.03)]), -0.3, 0.42, -0.25),
        at(trench("ground_trench_b", [(-0.1, 0.02), (-0.03, -0.02), (0.04, 0.025), (0.1, -0.01)]), 0.28, 0.44, 0.3),
        at(sandbag_wall("sandbags"), -0.52, 0.04, 1.3),
        at(sandbag_wall("sandbags_b", n=5), 0.52, 0.3, -1.1),
        at(tank_traps("traps"), 0.0, 0.66),
    ]


# --- command centre ---------------------------------------------------------------------------------------------

def command_building(name):
    """An angular armoured block: vertical base with a teal band, sloped armour, a glazed command deck."""
    p = Part(name)
    hw, hd = 0.12, 0.07
    box(p, -hw - 0.006, DEEP, -hd - 0.006, hw + 0.006, 0.01, hd + 0.006, COMPOSITE_DARK, top=COMPOSITE_GREY)
    # chamfered base walls
    c = 0.02
    base = [(-hw + c, -hd), (hw - c, -hd), (hw, -hd + c), (hw, hd - c), (hw - c, hd), (-hw + c, hd),
            (-hw, hd - c), (-hw, -hd + c)]
    n = len(base)
    pts = [(x, 0.01, z) for x, z in base] + [(x, 0.045, z) for x, z in base]
    faces = [list(range(n)), list(range(n, 2 * n))] + [[i, (i + 1) % n, n + (i + 1) % n, n + i] for i in range(n)]
    p.hull(pts, faces, COMPOSITE_GREY)
    # sloped armour skirt
    k = 0.75
    pts = [(x, 0.045, z) for x, z in base] + [(x * k, 0.085, z * k) for x, z in base]
    p.hull(pts, faces, [COMPOSITE] * 2 + [COMPOSITE, COMPOSITE_GREY] * 4)
    # teal light band round the front and sides of the base
    box(p, -hw + c, 0.034, hd - 0.002, hw - c, 0.04, hd + 0.003, TEAL)
    for sx in (-1, 1):
        box(p, sx * hw - 0.0025, 0.034, -hd + c, sx * hw + 0.0025, 0.04, hd - c, TEAL)
    # entrance
    box(p, -0.022, 0.01, hd - 0.002, 0.022, 0.032, hd + 0.004, GLASS)
    # command deck with a wraparound window strip
    dw, dd = 0.055, 0.035
    box(p, -dw, 0.085, -dd - 0.01, dw, 0.13, dd - 0.01, COMPOSITE, top=COMPOSITE_GREY)
    box(p, -dw - 0.002, 0.1, dd - 0.012, dw + 0.002, 0.118, dd - 0.007, GLASS)
    for sx in (-1, 1):
        box(p, sx * dw - 0.003, 0.1, -dd - 0.005, sx * dw + 0.003, 0.118, dd - 0.012, GLASS)
    box(p, -dw - 0.003, 0.124, dd - 0.013, dw + 0.003, 0.128, dd - 0.006, TEAL)
    # roof spine and mast
    box(p, -0.025, 0.13, -0.03, 0.025, 0.142, 0.0, COMPOSITE_GREY)
    box(p, -0.002, 0.142, -0.02, 0.002, 0.2, -0.016, COMPOSITE_DARK)
    box(p, -0.004, 0.196, -0.022, 0.004, 0.204, -0.014, TEAL)
    return p


def dish(name):
    """A slim pylon carrying a dish tilted toward the sky."""
    p = Part(name)
    cylinder(p, 0.024, DEEP, 0.01, COMPOSITE_DARK, n=6, top=COMPOSITE_GREY)
    cylinder(p, 0.008, 0.01, 0.1, COMPOSITE, n=6)
    box(p, -0.012, 0.09, -0.012, 0.012, 0.104, 0.012, COMPOSITE_GREY)
    axis = np.array([0.0, 0.8, 0.6])
    axis /= np.linalg.norm(axis)
    centre = np.array([0.0, 0.112, 0.0])
    axis_frustum(p, centre - axis * 0.004, axis * 0.026, 0.012, 0.056, COMPOSITE, n=10, cap=COMPOSITE_GREY)
    axis_frustum(p, centre + axis * 0.026, axis * 0.003, 0.012, 0.01, TEAL, n=6)
    beam(p, centre + axis * 0.02, centre + axis * 0.075, 0.004, COMPOSITE_DARK)
    return p


def turret(name):
    """A turret pod: a low hex base, a faceted dome and twin barrels pointing +Z."""
    p = Part(name)
    cylinder(p, 0.038, DEEP, 0.016, COMPOSITE_DARK, n=6, top=COMPOSITE_GREY)
    box(p, -0.014, 0.004, 0.031, 0.014, 0.009, 0.0345, TEAL)
    cylinder(p, 0.03, 0.016, 0.034, COMPOSITE, n=8, r_top=0.026)
    cylinder(p, 0.026, 0.034, 0.05, COMPOSITE, n=8, r_top=0.012, top=COMPOSITE_GREY)
    for sx in (-1, 1):
        beam(p, (sx * 0.009, 0.034, 0.015), (sx * 0.009, 0.038, 0.07), 0.007, COMPOSITE_DARK)
    box(p, -0.014, 0.04, 0.022, 0.014, 0.045, 0.026, TEAL)
    return p


def landing_pad(name, r=0.1):
    """An octagonal pad with a teal ring and landing marks; tilts with the ground."""
    p = Part(name)
    for rad, y0, y1, top in ((r, FOOT / 2, 0.006, PAD), (r * 0.8, 0.006, 0.008, TEAL), (r * 0.72, 0.008, 0.01, PAD)):
        n = 8
        pts = [(math.cos(i * math.tau / n + math.pi / 8) * rad, y, math.sin(i * math.tau / n + math.pi / 8) * rad)
               for y in (y0, y1) for i in range(n)]
        faces = [list(range(n)), list(range(n, 2 * n))]
        faces += [[i, (i + 1) % n, n + (i + 1) % n, n + i] for i in range(n)]
        p.hull(pts, faces, [top, top] + [COMPOSITE_DARK if rad == r else top] * n)
    # an H in pale grey-dark
    for sx in (-1, 1):
        box(p, sx * 0.022 - 0.006, 0.01, -0.03, sx * 0.022 + 0.006, 0.0125, 0.03, COMPOSITE_DARK)
    box(p, -0.016, 0.01, -0.005, 0.016, 0.0125, 0.005, COMPOSITE_DARK)
    for i in range(4):  # corner light posts
        a = math.pi / 4 + i * math.pi / 2
        x, z = math.cos(a) * r * 0.92, math.sin(a) * r * 0.92
        box(p, x - 0.004, 0.006, z - 0.004, x + 0.004, 0.016, z + 0.004, TEAL)
    return p


def future_bunker():
    return [
        at(command_building("command"), 0.0, -0.42),
        at(dish("dish"), -0.5, -0.12, 0.6),
        at(turret("turret"), 0.48, 0.3, 0.4),
        at(turret("turret_b"), -0.46, 0.36, -0.4),
        at(landing_pad("ground_pad"), 0.5, -0.16),
        at(tank_traps_future("barrier"), 0.02, 0.64),
    ]


def tank_traps_future(name):
    """A low composite barrier with teal running lights."""
    p = Part(name)
    for sx in (-1, 1):
        pts = [(sx * 0.02, FOOT / 2, -0.012), (sx * 0.09, FOOT / 2, -0.012), (sx * 0.09, FOOT / 2, 0.012),
               (sx * 0.02, FOOT / 2, 0.012), (sx * 0.024, 0.026, -0.006), (sx * 0.086, 0.026, -0.006),
               (sx * 0.086, 0.026, 0.004), (sx * 0.024, 0.026, 0.004)]
        p.hull(pts, BOX_FACES, [COMPOSITE_GREY, COMPOSITE_GREY, COMPOSITE, COMPOSITE_GREY, COMPOSITE, COMPOSITE_GREY])
        box(p, sx * 0.03, 0.012, 0.008, sx * 0.08, 0.016, 0.0105, TEAL)
    return p


MODELS = {
    "improvement_improvement_fort": fort,
    "improvement_improvement_bunker": bunker,
    "improvement_improvement_future_bunker": future_bunker,
}
