"""City walls: a ring of curtain walls, corner towers and a gate round the city's own tile (Cities mod buildings).

The ring follows the pointy-top hex: one tower at each of the six corners (corner radius RING) and one wall part
along each edge, so every piece seats on the terrain on its own. Local +Z of every edge piece faces outward. Edge k
runs from corner k to corner k + 1; corner k points at angle 30 + 60k degrees in the x-z plane (+Z toward the camera),
so edge 0 is the front-right edge and edge 1, where the gate stands, the front-left one. The game shows a unit in a
city at its front-right, so the gate keeps clear of it. The middle of the tile stays empty for the city itself.
"""
import math

from .kit import *  # noqa: F401,F403
from .kit import FOOT, SCALE, Part, at

# Walls (ancient): warm rubble sandstone, square towers with open battlements, a timber gate.
SAND = srgb(0xC2AA80)
SAND_TOP = srgb(0xA58E67)
SAND_SHADE = srgb(0x8A7553)
# High Walls (classical): cool grey ashlar, round towers under slate cones, a gatehouse with a portcullis, a ditch.
ASHLAR = srgb(0xB3AFA6)
ASHLAR_TOP = srgb(0x949088)
ASHLAR_SHADE = srgb(0x77736C)
CONE = srgb(0x55606F)
CONE_DARK = srgb(0x434C58)
DITCH = srgb(0x26332F)
DITCH_LIP = srgb(0x6B5A40)
PLANK = srgb(0x8A6440)

DEEP = -0.1  # foundations reach this deep so the ring stays seated on hills
CORNER = [math.radians(30 + 60 * k) for k in range(6)]
GATE_EDGE = 1


def corner(k, r):
    return math.cos(CORNER[k % 6]) * r, math.sin(CORNER[k % 6]) * r


def edge_frame(k, r):
    """Centre, outward yaw and along-edge unit vector (world x, z) of edge k of a hex with corner radius r."""
    (x0, z0), (x1, z1) = corner(k, r), corner(k + 1, r)
    phi = math.radians(60 + 60 * k)
    yaw = math.pi / 2 - phi  # turns local +Z to the outward normal; local +X then runs along the edge
    return ((x0 + x1) / 2, (z0 + z1) / 2), yaw, (math.cos(yaw), -math.sin(yaw))


TOP_FACES = BOX_FACES[1:]  # a box without its bottom face: for blocks that stand on something


def block(part, x0, y0, z0, x1, y1, z1, colour, top=None, at=(0, 0, 0), yaw=0.0):
    pts = [(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1),
           (x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1)]
    part.hull(place(pts, at, yaw), TOP_FACES, [top or colour] + [colour] * 4)


def battered(part, hl, y0, y1, z0, z1, batter, colour, top):
    """A wall base along X whose outer (+Z) face leans in by batter from y0 to y1."""
    pts = [(-hl, y0, z0), (hl, y0, z0), (hl, y0, z1 + batter), (-hl, y0, z1 + batter),
           (-hl, y1, z0), (hl, y1, z0), (hl, y1, z1), (-hl, y1, z1)]
    part.hull(pts, TOP_FACES, [top, colour, colour, colour, colour])


def merlons(part, hl, y, h, z0, z1, pitch, colour, top, w_frac=0.5):
    """A row of merlons along X between -hl and hl, depth z0..z1."""
    n = max(2, round(2 * hl / pitch))
    w = 2 * hl / n * w_frac / 2
    for i in range(n):
        x = -hl + (i + 0.5) * 2 * hl / n
        block(part, x - w, y, z0, x + w, y + h, z1, colour, top)


def decal(part, x0, y0, x1, y1, z, colour, yaw=0.0):
    """A flat rectangle on a +Z facing wall face at depth z (pass z a little proud of the face)."""
    pts = place([(x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)], yaw=yaw)
    behind = np.asarray(place([((x0 + x1) / 2, (y0 + y1) / 2, z - 1)], yaw=yaw)[0])
    part.quad(*pts, colour, centre=behind)


def drum(part, r0, r1, y0, y1, colour, at=(0, 0, 0), n=10, top=None, phase=0.0):
    """The side wall of a frustum (no caps: they hide under a roof or in the ground); r1 = 0 makes a cone.
    top adds a lid of that colour."""
    x, _, z = at
    ring0 = [(x + math.cos(phase + i * math.tau / n) * r0, y0, z + math.sin(phase + i * math.tau / n) * r0)
             for i in range(n)]
    ring1 = [(x + math.cos(phase + i * math.tau / n) * r1, y1, z + math.sin(phase + i * math.tau / n) * r1)
             for i in range(n)]
    centre = (x, (y0 + y1) / 2 if r1 > 0 else y0 + (y1 - y0) * 0.25, z)
    for i in range(n):
        j = (i + 1) % n
        if r1 > 0:
            part.quad(ring0[i], ring0[j], ring1[j], ring1[i], colour, centre=centre)
        else:
            part.tri(ring0[i], ring0[j], (x, y1, z), colour, centre=centre)
    if top and r1 > 0:
        for i in range(1, n - 1):
            part.tri(ring1[0], ring1[i], ring1[i + 1], top, centre=centre)


def ring(parts_for_edge, tower, r, gate=None, gate_half=0.0):
    """Six towers at the corners, one wall part per edge, and the gate edge split round a gate part.

    parts_for_edge(name, length) builds a wall along X of that authored length; gate_half is the gate's world half
    width along the edge."""
    parts = []
    tower_half = 0.07  # world: how far a wall end tucks under a tower
    for k in range(6):
        (cx, cz), yaw, (ux, uz) = edge_frame(k, r)
        span = r - 2 * tower_half + 0.03  # world length between towers, with a little overlap
        if k == GATE_EDGE and gate is not None:
            half = (span / 2 - gate_half + 0.01) / 2
            mid = gate_half - 0.01 + half
            for side, s in (("l", -1), ("r", 1)):
                parts.append(at(parts_for_edge(f"wall_{k}{side}", 2 * half / SCALE), cx + s * mid * ux,
                                cz + s * mid * uz, yaw))
            parts.append(at(gate("gate"), cx, cz, yaw))
        else:
            parts.append(at(parts_for_edge(f"wall_{k}", span / SCALE), cx, cz, yaw))
    for k in range(6):
        x, z = corner(k, r)
        parts.append(at(tower(f"tower_{k}"), x, z, math.pi / 2 - CORNER[k]))
    return parts


# --- Walls ------------------------------------------------------------------------------------------------------

W_H = 0.066  # authored wall height
W_T = 0.022  # authored wall thickness


def walls_segment(name, length):
    """A rubble curtain wall: plinth, wall walk and merlons on the outer (+Z) edge."""
    p = Part(name)
    hl, ht = length / 2, W_T / 2
    block(p, -hl, DEEP, -ht - 0.004, hl, 0.012, ht + 0.006, SAND_SHADE, top=SAND_TOP)
    block(p, -hl, 0.012, -ht, hl, W_H, ht, SAND, top=SAND_TOP)
    block(p, -hl, W_H - 0.016, ht - 0.001, hl, W_H - 0.012, ht + 0.003, SAND_SHADE)  # string course
    merlons(p, hl - 0.006, W_H, 0.016, ht - 0.008, ht, 0.034, SAND, SAND_TOP)
    return p


def walls_tower(name):
    """A square tower, open on top, with corner merlons and arrow slits facing out."""
    p = Part(name)
    t, h = 0.036, 0.1
    block(p, -t - 0.006, DEEP, -t - 0.006, t + 0.006, 0.016, t + 0.006, SAND_SHADE, top=SAND_TOP)
    block(p, -t, 0.016, -t, t, h - 0.008, t, SAND, top=SAND_TOP)
    block(p, -t - 0.005, h - 0.008, -t - 0.005, t + 0.005, h, t + 0.005, SAND_SHADE, top=SAND_TOP)
    m = 0.012
    for sx in (-1, 1):
        for sz in (-1, 1):
            x, z = sx * (t + 0.005 - m), sz * (t + 0.005 - m)
            block(p, x - m, h, z - m, x + m, h + 0.018, z + m, SAND, top=SAND_TOP)
    for yaw in (0.0, math.pi / 2, -math.pi / 2):  # slits on the outer and side faces
        decal(p, -0.004, 0.05, 0.004, 0.078, t + 0.0025, DARK, yaw=yaw)
    return p


def walls_gate(name):
    """A square gate tower astride the wall: a timber gate under a lintel, corner merlons on top."""
    p = Part(name)
    hw, d, h, ow, oh = 0.046, W_T / 2 + 0.012, 0.094, 0.022, 0.056
    block(p, -hw - 0.006, DEEP, -d - 0.006, hw + 0.006, 0.014, d + 0.006, SAND_SHADE, top=SAND_TOP)
    block(p, -hw, 0.014, -d, hw, h - 0.008, d, SAND, top=SAND_TOP)
    block(p, -hw - 0.005, h - 0.008, -d - 0.005, hw + 0.005, h, d + 0.005, SAND_SHADE, top=SAND_TOP)
    m = 0.012
    for sx in (-1, 1):
        for sz in (-1, 1):
            x, z = sx * (hw + 0.005 - m), sz * (d + 0.005 - m)
            block(p, x - m, h, z - m, x + m, h + 0.018, z + m, SAND, top=SAND_TOP)
    for z, sign in ((d, 1), (-d, -1)):  # the passage shows dark on both faces, the gate leaves on the outside
        yaw = 0.0 if sign > 0 else math.pi
        decal(p, -ow - 0.006, oh - 0.002, ow + 0.006, oh + 0.008, d + 0.001, SAND_SHADE, yaw=yaw)  # lintel
        decal(p, -ow, 0.014, ow, oh - 0.002, d + 0.001, DOOR if sign > 0 else DARK, yaw=yaw)
    for y in (0.026, 0.044):  # strap boards on the gate leaves
        decal(p, -ow, y - 0.002, ow, y + 0.002, d + 0.002, TIMBER)
    decal(p, -0.0015, 0.014, 0.0015, oh - 0.002, d + 0.0025, TIMBER)
    decal(p, -0.004, 0.068, 0.004, 0.084, d + 0.001, DARK)  # slit over the gate
    return p


def walls():
    return ring(walls_segment, walls_tower, 0.86, walls_gate, gate_half=0.09)


# --- High Walls -------------------------------------------------------------------------------------------------

H_H = 0.11  # authored wall height
H_T = 0.03  # authored wall thickness


def high_segment(name, length):
    """A tall ashlar curtain: battered base, machicolated parapet overhanging the outer face, tall merlons."""
    p = Part(name)
    hl, ht = length / 2, H_T / 2
    battered(p, hl, DEEP, 0.03, -ht - 0.004, ht, 0.012, ASHLAR_SHADE, ASHLAR_TOP)
    block(p, -hl, 0.03, -ht, hl, H_H - 0.016, ht, ASHLAR, top=ASHLAR_TOP)
    machicolations(p, hl, H_H - 0.016, ht)
    block(p, -hl, H_H - 0.016, -ht, hl, H_H, ht + 0.009, ASHLAR, top=ASHLAR_TOP)  # parapet walk
    merlons(p, hl - 0.004, H_H, 0.02, ht - 0.002, ht + 0.009, 0.034, ASHLAR, ASHLAR_TOP, w_frac=0.55)
    return p


def machicolations(p, hl, y, face):
    """Under an overhanging parapet at height y: a dark band of drop holes broken by light corbels (decals)."""
    decal(p, -hl, y - 0.012, hl, y, face + 0.001, DARK)
    n = max(3, round(2 * hl / 0.026))
    for i in range(n + 1):
        x = -hl + i * 2 * hl / n
        decal(p, max(x - 0.004, -hl), y - 0.012, min(x + 0.004, hl), y, face + 0.0022, ASHLAR_SHADE)


def drum_tower(p, r, h, at_=(0, 0, 0), slits=True, n=10, cone=0.075):
    """A round tower: battered foot, shaft, corbelled crown and a slate cone with a finial."""
    x, _, z = at_
    drum(p, r + 0.01, r, DEEP, 0.03, ASHLAR_SHADE, at=at_, n=n)
    drum(p, r, r, 0.03, h - 0.03, ASHLAR, at=at_, n=n)
    drum(p, r, r + 0.008, h - 0.03, h - 0.022, ASHLAR_SHADE, at=at_, n=n)
    drum(p, r + 0.008, r + 0.008, h - 0.022, h, ASHLAR, at=at_, n=n)
    drum(p, r + 0.014, 0.0, h - 0.004, h + cone, CONE, at=at_, n=n, phase=math.pi / n)
    box(p, x - 0.0015, h + cone - 0.005, z - 0.0015, x + 0.0015, h + cone + 0.016, z + 0.0015, IRON)
    if slits:  # arrow slits on the faces looking out (a tower at the part origin, local +Z outward)
        apothem = r * math.cos(math.pi / n)
        for i in (-1, 0, 1):  # with n = 10 and no phase, one face looks straight down +Z
            decal(p, -0.0035, 0.06, 0.0035, 0.09, apothem + 0.0012, DARK, yaw=i * math.tau / n)


def high_tower(name):
    p = Part(name)
    drum_tower(p, 0.05, 0.155)
    return p


def high_gatehouse(name):
    """A projecting gate block between two drum towers: portcullis in a dark arch, machicolations, battlements."""
    p = Part(name)
    gw, ow, d0, d1, h = 0.052, 0.024, -H_T / 2 - 0.006, H_T / 2 + 0.022, 0.135
    block(p, -gw - 0.004, DEEP, d0 - 0.004, gw + 0.004, 0.02, d1 + 0.006, ASHLAR_SHADE, top=ASHLAR_TOP)
    block(p, -gw, 0.02, d0, gw, h - 0.016, d1, ASHLAR, top=ASHLAR_TOP)
    # gate arch: dark passage, an arch head, and the iron portcullis grid in front
    decal(p, -ow, 0.02, ow, 0.074, d1 + 0.001, DARK)
    pts = [(-ow, 0.074, d1 + 0.001), (ow, 0.074, d1 + 0.001), (0.0, 0.088, d1 + 0.001)]
    p.tri(*pts, DARK, centre=(0, 0.08, 0))
    for i in range(5):
        x = -ow + (i + 0.5) * 2 * ow / 5
        block(p, x - 0.0018, 0.03, d1 + 0.001, x + 0.0018, 0.08, d1 + 0.0045, IRON)
    for y in (0.042, 0.062):
        block(p, -ow, y - 0.0018, d1 + 0.0045, ow, y + 0.0018, d1 + 0.006, IRON)
    block(p, -ow - 0.006, 0.088, d1, ow + 0.006, 0.096, d1 + 0.004, ASHLAR_SHADE)  # hood moulding
    machicolations(p, gw, h - 0.016, d1)
    block(p, -gw, h - 0.016, d0, gw, h, d1 + 0.01, ASHLAR, top=ASHLAR_TOP)
    merlons(p, gw - 0.004, h, 0.02, d1 - 0.002, d1 + 0.01, 0.026, ASHLAR, ASHLAR_TOP, w_frac=0.55)
    decal(p, -0.01, 0.105, 0.01, 0.112, d1 + 0.001, DARK)  # window over the gate
    for sx in (-1, 1):
        drum_tower(p, 0.032, 0.16, at_=(sx * (gw + 0.014), 0, d1 - 0.024), slits=False, n=8, cone=0.06)
    return p


def ditch(name, length):
    """A dark, water-logged ditch between two low spoil banks, so it reads sunken. A ground part: it tilts with the
    slope."""
    p = Part(name)
    hl = length / 2
    block(p, -hl, FOOT / 2, -0.011, hl, 0.003, 0.011, DITCH)
    for sz in (-1, 1):  # banks: a gentle outer slope, a steeper inner one
        z0, z1, zt = sz * 0.009, sz * 0.024, sz * 0.014
        pts = [(-hl, FOOT / 2, z0), (hl, FOOT / 2, z0), (hl, FOOT / 2, z1), (-hl, FOOT / 2, z1),
               (-hl, 0.008, zt), (hl, 0.008, zt)]
        p.hull(pts, [[0, 1, 5, 4], [3, 2, 5, 4], [0, 3, 4], [1, 2, 5]], DITCH_LIP)
    return p


def bridge(name):
    """A plank causeway over the ditch in front of the gate (ground part)."""
    p = Part(name)
    block(p, -0.026, FOOT / 2, -0.03, 0.026, 0.008, 0.03, PLANK, top=PLANK)
    for i in range(5):
        z = -0.03 + (i + 0.5) * 0.012
        block(p, -0.026, 0.008, z - 0.001, 0.026, 0.0095, z + 0.001, BOARDS_DARK)
    for sx in (-1, 1):
        block(p, sx * 0.026 - 0.003, 0.008, -0.03, sx * 0.026 + 0.003, 0.018, 0.03, TIMBER)
    return p


def ditches(r_ditch, gate_gap):
    """Two ground strips per edge outside the curtain, leaving the corners (towers) and the gate front free."""
    parts = []
    for k in range(6):
        (cx, cz), yaw, (ux, uz) = edge_frame(k, r_ditch)
        side = r_ditch
        if k == GATE_EDGE:
            length = (side / 2 - 0.13 - gate_gap)
            mid = gate_gap + length / 2
        else:
            length = side / 2 - 0.13
            mid = length / 2 + 0.005
        for s, tag in ((-1, "a"), (1, "b")):
            parts.append(at(ditch(f"ground_ditch_{k}{tag}", length / SCALE), cx + s * mid * ux, cz + s * mid * uz, yaw))
        if k == GATE_EDGE:
            parts.append(at(bridge("ground_bridge"), cx, cz, yaw))
    return parts


def high_walls():
    return ring(high_segment, high_tower, 0.82, high_gatehouse, gate_half=0.19) + ditches(0.935, 0.06)


MODELS = {
    "walls": (walls, "tile", {"mod": "Cities", "cityRing": 1}),
    "high_walls": (high_walls, "tile", {"mod": "Cities", "cityRing": 2}),
}
