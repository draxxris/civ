"""Mines and quarries."""
import random

from .kit import *  # noqa: F401,F403
from .kit import FOOT, Part, at


def adit(name, rng):
    """A timber-framed tunnel mouth in a rock face. The back half sinks into a hillside; on flat land it reads
    as a rocky outcrop."""
    p = Part(name)
    lumpy_mound(p, 0.14, 0.12, 0.13, [ROCK, ROCK_DARK, ROCK], rng, at=(0, 0, -0.05))
    # dark opening and timber portal, standing proud of the rock on the +Z side
    z = 0.075
    box(p, -0.04, FOOT, z - 0.06, 0.04, 0.085, z - 0.004, DARK)
    for sx in (-1, 1):
        box(p, sx * 0.048 - 0.009, FOOT, z - 0.06, sx * 0.048 + 0.009, 0.095, z + 0.006, TIMBER)
    box(p, -0.068, 0.088, z - 0.06, 0.068, 0.108, z + 0.01, TIMBER_LIGHT)
    # sloping roof board over the portal
    box(p, -0.075, 0.106, z - 0.065, 0.075, 0.116, z + 0.02, TIMBER)
    return p


def rails(name, length=0.2):
    p = Part(name)
    box(p, -0.06, FOOT / 2, -0.01, 0.06, 0.004, length, SPOIL)  # packed gravel bed
    n = 7
    for i in range(n):
        z = (i + 0.5) * length / n
        box(p, -0.04, 0.004, z - 0.008, 0.04, 0.012, z + 0.008, TIMBER)
    for sx in (-1, 1):
        box(p, sx * 0.024 - 0.003, 0.012, -0.01, sx * 0.024 + 0.003, 0.02, length, IRON)
    return p


def cart(name):
    p = Part(name)
    for sx in (-1, 1):
        for sz in (-1, 1):
            # wheels as short cylinders lying along X
            pts = []
            for i in range(8):
                a = i * math.tau / 8
                for x in (sx * 0.026, sx * 0.034):
                    pts.append((x, 0.016 + math.sin(a) * 0.014, sz * 0.026 + math.cos(a) * 0.014))
            faces = [[2 * i for i in range(8)], [2 * i + 1 for i in range(8)]]
            faces += [[2 * i, 2 * ((i + 1) % 8), 2 * ((i + 1) % 8) + 1, 2 * i + 1] for i in range(8)]
            p.hull(pts, faces, IRON)
    # tub, wider at the top
    pts = [(-0.028, 0.02, -0.04), (0.028, 0.02, -0.04), (0.028, 0.02, 0.04), (-0.028, 0.02, 0.04),
           (-0.036, 0.062, -0.05), (0.036, 0.062, -0.05), (0.036, 0.062, 0.05), (-0.036, 0.062, 0.05)]
    p.hull(pts, BOX_FACES, [TIMBER, ORE, TIMBER, TIMBER_LIGHT, TIMBER, TIMBER_LIGHT])
    # heap of ore
    pts = [(-0.03, 0.062, -0.042), (0.03, 0.062, -0.042), (0.03, 0.062, 0.042), (-0.03, 0.062, 0.042),
           (-0.01, 0.082, -0.01), (0.012, 0.085, 0.012)]
    p.hull(pts, [[0, 1, 2, 3], [0, 1, 4], [1, 2, 5, 4], [2, 3, 5], [3, 0, 4, 5]], [ORE, ORE, ROCK, ORE, ROCK])
    return p


def spoil_heap(name, rng, r=0.075, h=0.06):
    p = Part(name)
    lumpy_mound(p, r, r * 0.85, h, [SPOIL, ROCK_DARK, SPOIL], rng, y0=FOOT / 2, rings=2, n=8)
    return p


def timber_props(name):
    """A stack of pit props: long logs lying along X."""
    p = Part(name)
    for y, z in [(0.01, -0.02), (0.01, 0.0), (0.01, 0.02), (0.027, -0.01), (0.027, 0.01)]:
        log(p, -0.06, 0.06, y, z, 0.01)
    return p


def shed(name):
    """An open-fronted lean-to over the winch."""
    p = Part(name)
    box(p, -0.06, FOOT, -0.045, 0.06, 0.01, 0.045, STONE_DARK, top=STONE)
    for sx in (-1, 1):
        for sz, h in ((-1, 0.085), (1, 0.065)):
            box(p, sx * 0.052 - 0.006, 0.01, sz * 0.038 - 0.006, sx * 0.052 + 0.006, h, sz * 0.038 + 0.006, TIMBER)
    box(p, -0.058, 0.01, -0.045, 0.058, 0.085, -0.035, TIMBER_LIGHT)  # back wall
    pts = [(-0.07, 0.085, -0.055), (0.07, 0.085, -0.055), (0.07, 0.06, 0.055), (-0.07, 0.06, 0.055)]
    pts += [(x, y + 0.01, z) for x, y, z in pts]
    p.hull(pts, BOX_FACES, TIMBER)
    cylinder(p, 0.02, 0.01, 0.04, IRON, n=6)
    return p


def quarry_face(name, rng):
    """Pale stone cut in stepped benches that climb away from the camera. On a hill the back half sinks in."""
    p = Part(name)
    lumpy_mound(p, 0.17, 0.12, 0.15, [ROCK, QUARRY_DARK, ROCK], rng, at=(0, 0, -0.1))
    benches = [(0.05, 0.045), (0.0, 0.09), (-0.05, 0.135)]  # (front z, top height)
    for i, (z, h) in enumerate(benches):
        w = 0.16 - 0.025 * i
        box(p, -w, FOOT, z - 0.1, w, h, z, QUARRY_SHADE, top=QUARRY)
        # chisel grooves on the riser
        for k in range(3):
            x = -w + (k + 1) * 2 * w / 4
            box(p, x - 0.002, h - 0.035, z - 0.001, x + 0.002, h - 0.004, z + 0.002, QUARRY_DARK)
    return p


def blocks(name, rng, count=6):
    """Cut ashlar blocks stacked two high."""
    p = Part(name)
    shades = [QUARRY, QUARRY_SHADE, CHURCH_STONE]
    spots = [(-0.035, 0.0), (0.0, 0.0), (0.035, 0.0), (-0.018, 1.0), (0.018, 1.0), (0.0, 2.0)][:count]
    for i, (x, level) in enumerate(spots):
        y = level * 0.026
        z = 0.004 * (rng.random() - 0.5)
        box(p, x - 0.016, y if level else FOOT / 2, z - 0.02, x + 0.016, y + 0.026, z + 0.02, shades[i % 3])
    return p


def derrick(name):
    """A timber A-frame with a slanting boom and a block hanging on a rope."""
    p = Part(name)
    box(p, -0.03, FOOT / 2, -0.03, 0.03, 0.006, 0.03, STONE_DARK, top=STONE)
    for sx in (-1, 1):
        beam(p, (sx * 0.035, 0.0, -0.02), (0.0, 0.17, 0.0), 0.009, TIMBER)
    beam(p, (0.0, 0.0, 0.035), (0.0, 0.17, 0.0), 0.009, TIMBER)
    beam(p, (0.0, 0.03, 0.0), (0.0, 0.2, 0.13), 0.008, TIMBER_LIGHT)
    beam(p, (0.0, 0.2, 0.13), (0.0, 0.06, 0.13), 0.003, ROPE)
    box(p, -0.014, 0.035, 0.115, 0.014, 0.06, 0.145, QUARRY_SHADE, top=QUARRY)
    return p


def sledge(name):
    """A wooden sledge carrying one block."""
    p = Part(name)
    for sx in (-1, 1):
        box(p, sx * 0.025 - 0.005, 0.0, -0.05, sx * 0.025 + 0.005, 0.01, 0.05, TIMBER)
    box(p, -0.034, 0.01, -0.04, 0.034, 0.016, 0.04, TIMBER_LIGHT)
    box(p, -0.025, 0.016, -0.028, 0.025, 0.05, 0.028, QUARRY_SHADE, top=QUARRY)
    return p


def rubble(name, rng, r=0.06, h=0.035):
    p = Part(name)
    lumpy_mound(p, r, r * 0.8, h, [QUARRY_SHADE, QUARRY_DARK, QUARRY], rng, y0=FOOT / 2, rings=2, n=7)
    return p


def mine():
    rng = random.Random(5)
    return [
        at(adit("adit", rng), 0.0, -0.2),
        at(rails("ground_rails"), 0.0, -0.06),
        at(cart("cart"), 0.0, 0.26),
        at(spoil_heap("spoil", rng), 0.42, 0.18),
        at(spoil_heap("spoil_b", rng, r=0.05, h=0.04), 0.5, -0.1),
        at(shed("shed"), -0.42, 0.0, 0.35),
        at(timber_props("props"), -0.3, 0.42, -0.2),
    ]


def quarry():
    rng = random.Random(53)
    return [
        at(quarry_face("face", rng), 0.0, -0.18),
        at(blocks("blocks", rng), 0.36, 0.22, -0.3),
        at(blocks("blocks_b", rng, count=4), -0.12, 0.34, 0.4),
        at(derrick("derrick"), -0.4, -0.02, 0.6),
        at(sledge("sledge"), 0.12, 0.28, -0.2),
        at(rubble("rubble", rng), 0.44, -0.06),
        at(rubble("rubble_b", rng, r=0.045, h=0.025), -0.42, 0.32),
    ]



# --- shaft mine (renaissance), modern mine and depleted mine ---------------------------------------------------

BRICK = srgb(0x9C4E34)
BRICK_DARK = srgb(0x7B3C28)
STEEL = srgb(0x7E8790)
STEEL_DARK = srgb(0x59616A)
CONCRETE = srgb(0xBDB8AE)
CONCRETE_DARK = srgb(0x9A958B)
HAUL_YELLOW = srgb(0xE3AE22)
HAUL_YELLOW_DARK = srgb(0xB7881A)
TYRE = srgb(0x26241F)
PIT_ROCK = srgb(0x8A7D6C)
PIT_ROCK_TOP = srgb(0xA39582)
PIT_ROCK_DARK = srgb(0x6B6155)
PIT_FLOOR = srgb(0x5E554A)
WEATHERED = srgb(0x8E887E)
WEATHERED_DARK = srgb(0x6C675F)
RUST = srgb(0x6E4E3C)
TUFT = srgb(0x9AA24E)
TUFT_DARK = srgb(0x6E7E3A)
OLD_BOARDS = srgb(0x7F705E)
OLD_BOARDS_DARK = srgb(0x625547)


def turned(part, roll=0.0, pitch=0.0, lift=0.0):
    """Rotate a finished part about Z (roll) and X (pitch) and raise it by lift: for things lying on their side."""
    cr, sr, cp, sp = math.cos(roll), math.sin(roll), math.cos(pitch), math.sin(pitch)
    rz = np.array([[cr, -sr, 0], [sr, cr, 0], [0, 0, 1]])
    rx = np.array([[1, 0, 0], [0, cp, -sp], [0, sp, cp]])
    m = rx @ rz
    part.tris = [(m @ a + (0, lift, 0), m @ b + (0, lift, 0), m @ c + (0, lift, 0), col)
                 for a, b, c, col in part.tris]
    return part


def shifted(part, dx=0.0, dy=0.0, dz=0.0):
    """Move a finished part's geometry, for example to put its origin (its seat and tilt pivot) in its middle."""
    d = np.array([dx, dy, dz])
    part.tris = [(a + d, b + d, c + d, col) for a, b, c, col in part.tris]
    return part


def plank(part, a, b, w, t, colour):
    """A flat board from a to b, w wide (kept horizontal across its length) and t thick."""
    a, b = np.asarray(a, dtype=float), np.asarray(b, dtype=float)
    axis = (b - a) / np.linalg.norm(b - a)
    side = np.cross(axis, (0, 1, 0))
    side /= np.linalg.norm(side)
    up = np.cross(side, axis)
    corners = [side * sx * w / 2 + up * sy * t / 2 for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    part.hull([a + c for c in corners] + [b + c for c in corners], BOX_FACES, colour)


def wheel(part, centre, r, t, colour, spokes=4, n=10):
    """A spoked wheel standing in the XY plane (axle along Z), built from beams."""
    cx, cy, cz = centre
    rim = [(cx + math.cos(i * math.tau / n) * r, cy + math.sin(i * math.tau / n) * r, cz) for i in range(n)]
    for i in range(n):
        beam(part, rim[i], rim[(i + 1) % n], t, colour)
    for k in range(spokes):
        a = k * math.pi / spokes
        d = (math.cos(a) * r, math.sin(a) * r, 0)
        beam(part, (cx - d[0], cy - d[1], cz), (cx + d[0], cy + d[1], cz), t * 0.6, colour)
    box(part, cx - t, cy - t, cz - t * 1.6, cx + t, cy + t, cz + t * 1.6, IRON)


def headframe(name, rope_to=(-0.15, 0.08)):
    """A timber pithead tower with a winding wheel on top, raking back struts toward the winding house (-X) and a
    winding rope that runs into the house (rope_to is the rope's end, x and height, inside the house)."""
    p = Part(name)
    box(p, -0.06, FOOT * 2, -0.045, 0.06, 0.01, 0.045, STONE_DARK, top=STONE)
    box(p, -0.03, 0.01, -0.025, 0.03, 0.013, 0.025, DARK)  # shaft mouth
    top_y = 0.25
    feet = [(sx * 0.05, sz * 0.038) for sx in (-1, 1) for sz in (-1, 1)]
    heads = [(sx * 0.02, sz * 0.016) for sx in (-1, 1) for sz in (-1, 1)]
    for (fx, fz), (hx, hz) in zip(feet, heads):
        beam(p, (fx, 0.0, fz), (hx, top_y, hz), 0.011, TIMBER)
    for y in (0.08, 0.165):  # ring braces
        t = y / top_y
        ring = [(fx + (hx - fx) * t, y, fz + (hz - fz) * t) for (fx, fz), (hx, hz) in zip(feet, heads)]
        for i, j in ((0, 1), (1, 3), (3, 2), (2, 0)):
            beam(p, ring[i], ring[j], 0.007, TIMBER_LIGHT)
    # X brace on the front face
    beam(p, (-0.05, 0.005, 0.04), (0.026, 0.165, 0.026), 0.006, TIMBER_LIGHT)
    beam(p, (0.05, 0.005, 0.04), (-0.026, 0.165, 0.026), 0.006, TIMBER_LIGHT)
    # raking back struts
    for sz in (-1, 1):
        beam(p, (-0.13, 0.0, sz * 0.03), (-0.02, top_y - 0.01, sz * 0.016), 0.01, TIMBER)
    box(p, -0.145, FOOT, -0.045, -0.115, 0.012, 0.045, STONE_DARK, top=STONE)
    # head platform and the winding wheel
    box(p, -0.032, top_y, -0.026, 0.032, top_y + 0.01, 0.026, TIMBER_LIGHT)
    wc = (0.0, top_y + 0.048, 0.0)
    wheel(p, wc, 0.042, 0.007, IRON)
    for sz in (-1, 1):  # wheel bearings
        beam(p, (-0.022, top_y + 0.01, sz * 0.016), (0.0, wc[1], sz * 0.016), 0.006, TIMBER)
        beam(p, (0.022, top_y + 0.01, sz * 0.016), (0.0, wc[1], sz * 0.016), 0.006, TIMBER)
    # ropes: down the shaft, and back to the winding house
    beam(p, (0.03, wc[1] - 0.03, 0.0), (0.02, 0.012, 0.0), 0.003, ROPE)
    beam(p, (0.0, wc[1] + 0.042, 0.0), (rope_to[0], rope_to[1], 0.0), 0.003, ROPE)
    return p


def winding_house(name, w=0.13, d=0.09, wall=0.09):
    """A brick engine house with a slate roof, arched windows and a tall chimney at the back."""
    p = Part(name)
    hw, hd = w / 2, d / 2
    box(p, -hw - 0.008, FOOT * 2, -hd - 0.008, hw + 0.008, 0.012, hd + 0.008, STONE_DARK, top=STONE)
    box(p, -hw, 0.012, -hd, hw, wall, hd, BRICK)
    box(p, -0.016, 0.012, hd - 0.002, 0.016, 0.062, hd + 0.004, DOOR)
    for sx in (-1, 1):
        x = sx * hw * 0.6
        box(p, x - 0.01, 0.03, hd - 0.002, x + 0.01, 0.07, hd + 0.003, WINDOW)
        box(p, x - 0.013, 0.07, hd - 0.002, x + 0.013, 0.077, hd + 0.004, BRICK_DARK)
    box(p, hw - 0.002, 0.03, -0.012, hw + 0.003, 0.07, 0.012, WINDOW)  # rope window on the shaft side
    box(p, -hw - 0.002, wall - 0.008, -hd - 0.002, hw + 0.002, wall, hd + 0.003, BRICK_DARK)  # cornice
    top = prism_roof(p, w, d, wall, 0.045, 0.012, SLATE, BRICK)
    box(p, -hw - 0.012, top - 0.008, -0.007, hw + 0.012, top + 0.004, 0.007, SLATE_DARK)
    # chimney stack behind the house, tapering, with a dark cap
    cx, cz = -hw * 0.45, -hd - 0.02
    box(p, cx - 0.026, FOOT * 2, cz - 0.026, cx + 0.026, 0.03, cz + 0.026, BRICK_DARK)
    cylinder(p, 0.022, 0.03, 0.3, BRICK, at=(cx, 0, cz), n=4, r_top=0.015, phase=math.pi / 4)
    cylinder(p, 0.02, 0.3, 0.315, BRICK_DARK, at=(cx, 0, cz), n=4, r_top=0.02, phase=math.pi / 4, top=DARK)
    return p


def slag_heap(name, rng, r=0.1, h=0.11):
    """A tall, dark conical spoil tip."""
    p = Part(name)
    lumpy_mound(p, r, r * 0.85, h, [SPOIL, ROCK_DARK, SLATE_DARK], rng, y0=FOOT, rings=3, n=8)
    return p


def shaft_mine():
    rng = random.Random(71)
    hx, hz = 0.06, -0.3
    rail_yaw = 0.75
    rx, rz = 0.1, -0.16
    dx, dz = math.sin(rail_yaw), math.cos(rail_yaw)
    return [
        at(headframe("headframe", rope_to=(-0.16, 0.075)), hx, hz),
        at(winding_house("winding_house"), hx - 0.36, hz),
        # two short track lengths pivoting on their middles, so each follows the hill's curve; a cart on each
        at(shifted(rails("ground_rails", length=0.11), dz=-0.05), rx + dx * 0.12, rz + dz * 0.12, rail_yaw),
        at(shifted(rails("ground_rails_b", length=0.11), dz=-0.05), rx + dx * 0.34, rz + dz * 0.34, rail_yaw),
        at(cart("cart"), rx + dx * 0.12, rz + dz * 0.12, rail_yaw),
        at(cart("cart_b"), rx + dx * 0.34, rz + dz * 0.34, rail_yaw),
        at(slag_heap("spoil", rng), 0.48, -0.2),
        at(spoil_heap("spoil_b", rng, r=0.055, h=0.04), 0.56, 0.2),
        at(timber_props("props"), -0.34, 0.3, -0.3),
    ]


def open_pit(name, rng):
    """Stepped benches in a horseshoe round the pit, opening toward the camera. Like the quarry face, the back half
    sinks into a hillside; on flat land it reads as a terraced crater rim."""
    p = Part(name)
    lumpy_mound(p, 0.2, 0.1, 0.11, [PIT_ROCK_DARK, ROCK_DARK, PIT_ROCK], rng, at=(0, 0, -0.17))
    a0, a1, n = math.radians(155), math.radians(385), 7
    levels = [(0.06, 0.035), (0.095, 0.07), (0.13, 0.105)]  # (inner radius, top height)
    for li, (r_in, h) in enumerate(levels):
        r_top = levels[li + 1][0] + 0.004 if li + 1 < len(levels) else 0.16
        r_bot = r_top + 0.05
        for i in range(n):
            ta, tb = a0 + (a1 - a0) * i / n, a0 + (a1 - a0) * (i + 1) / n
            pts = []
            for y, ro in ((FOOT, r_bot), (h, r_top)):
                for a in (ta, tb):
                    for r in (r_in, ro):
                        pts.append((math.cos(a) * r, y, math.sin(a) * r * 0.85))
            # 0..3 bottom, 4..7 top; per angle (inner, outer)
            faces = [[0, 1, 3, 2], [4, 5, 7, 6], [0, 2, 6, 4], [1, 3, 7, 5], [0, 1, 5, 4], [2, 3, 7, 6]]
            riser = PIT_ROCK if (i + li) % 2 == 0 else PIT_ROCK_DARK
            p.hull(pts, faces, [riser, PIT_ROCK_TOP, riser, riser, riser, riser])
    return p


def pit_floor(name):
    p = Part(name)
    n = 8
    pts = [(math.cos(i * math.tau / n) * 0.07, y, math.sin(i * math.tau / n) * 0.06)
           for y in (FOOT / 2, 0.004) for i in range(n)]
    faces = [list(range(n)), list(range(n, 2 * n))] + [[i, (i + 1) % n, n + (i + 1) % n, n + i] for i in range(n)]
    p.hull(pts, faces, [PIT_FLOOR, PIT_FLOOR] + [PIT_ROCK_DARK] * n)
    cylinder(p, 0.022, 0.0, 0.0065, WATER, at=(-0.02, 0, -0.012), n=6)
    return p


def conveyor_between(name, low, high, rise=0.13):
    """Place a conveyor from world point low (x, z) up to world point high."""
    dx, dz = high[0] - low[0], high[1] - low[1]
    length = math.hypot(dx, dz) / SCALE
    yaw = math.atan2(-dz, dx)
    return at(conveyor(name, length, rise), (low[0] + high[0]) / 2, (low[1] + high[1]) / 2, yaw)


def conveyor(name, length=0.3, rise=0.15):
    """A steel belt conveyor along +X climbing from x = -length/2 (low) to +length/2 (high), on trestle legs."""
    p = Part(name)
    x0, x1, y0, y1 = -length / 2, length / 2, 0.03, 0.03 + rise
    w = 0.018
    pts = [(x0, y0 - 0.012, -w), (x1, y1 - 0.012, -w), (x1, y1 - 0.012, w), (x0, y0 - 0.012, w),
           (x0, y0, -w), (x1, y1, -w), (x1, y1, w), (x0, y0, w)]
    p.hull(pts, BOX_FACES, [STEEL_DARK, ORE, STEEL, STEEL, STEEL, STEEL])
    # a light cover hood over the upper half
    for k, t in enumerate((0.1, 0.35, 0.6, 0.85)):
        x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
        box(p, x - 0.004, FOOT, -w - 0.006, x + 0.004, y - 0.01, -w + 0.002, STEEL_DARK)
        box(p, x - 0.004, FOOT, w - 0.002, x + 0.004, y - 0.01, w + 0.006, STEEL_DARK)
        box(p, x - 0.004, y - 0.02, -w - 0.006, x + 0.004, y - 0.012, w + 0.006, STEEL_DARK)
    for sz in (-1, 1):  # side rails
        beam(p, (x0, y0 + 0.006, sz * w), (x1, y1 + 0.006, sz * w), 0.004, STEEL_DARK)
    return p


def plant(name, w=0.15, d=0.1, wall=0.11):
    """A concrete and steel processing shed with a lean roof, a dark loading door and a band of windows."""
    p = Part(name)
    hw, hd = w / 2, d / 2
    box(p, -hw - 0.008, FOOT * 2, -hd - 0.008, hw + 0.008, 0.01, hd + 0.008, CONCRETE_DARK, top=CONCRETE)
    box(p, -hw, 0.01, -hd, hw, wall, hd, CONCRETE)
    box(p, -hw - 0.003, wall - 0.004, -hd - 0.003, hw + 0.003, wall + 0.012, hd + 0.003, STEEL, top=STEEL_DARK)
    box(p, -0.03, 0.01, hd - 0.002, 0.012, 0.07, hd + 0.004, STEEL_DARK)  # roller door
    box(p, -hw + 0.01, 0.08, hd - 0.002, hw - 0.01, 0.094, hd + 0.003, srgb(0x5C7F96))  # window band
    # tall crusher tower on the roof's +X end, where the conveyor arrives
    box(p, hw - 0.05, wall, -0.03, hw - 0.005, wall + 0.06, 0.03, STEEL, top=STEEL_DARK)
    return p


def silo(name, r=0.035, h=0.2):
    p = Part(name)
    box(p, -r - 0.006, FOOT * 2, -r - 0.006, r + 0.006, 0.01, r + 0.006, CONCRETE_DARK, top=CONCRETE)
    cylinder(p, r, 0.01, h, CONCRETE, n=10)
    cylinder(p, r, h, h + 0.035, STEEL, n=10, r_top=0.0)
    box(p, -r - 0.002, h - 0.025, -0.01, r + 0.002, h - 0.018, 0.01, STEEL_DARK)  # band
    return p


def haul_truck(name):
    """A big yellow dump truck facing +X with a load of ore."""
    p = Part(name)
    for x in (-0.042, 0.04):
        for sz in (-1, 1):
            pts = []
            for i in range(8):
                a = i * math.tau / 8 + math.pi / 8
                for z in (sz * 0.03, sz * 0.054):
                    pts.append((x + math.cos(a) * 0.027, 0.027 + math.sin(a) * 0.027, z))
            faces = [[2 * i for i in range(8)], [2 * i + 1 for i in range(8)]]
            faces += [[2 * i, 2 * ((i + 1) % 8), 2 * ((i + 1) % 8) + 1, 2 * i + 1] for i in range(8)]
            p.hull(pts, faces, TYRE)
    box(p, -0.07, 0.02, -0.028, 0.07, 0.04, 0.028, HAUL_YELLOW_DARK)  # chassis
    # dump body: sloping floor, tall sides, canopy over the cab
    pts = [(-0.075, 0.045, -0.04), (0.02, 0.045, -0.04), (0.02, 0.045, 0.04), (-0.075, 0.045, 0.04),
           (-0.08, 0.095, -0.044), (0.05, 0.105, -0.044), (0.05, 0.105, 0.044), (-0.08, 0.095, 0.044)]
    p.hull(pts, BOX_FACES, [HAUL_YELLOW_DARK, ORE, HAUL_YELLOW, HAUL_YELLOW, HAUL_YELLOW, HAUL_YELLOW])
    pts = [(-0.06, 0.095, -0.032), (0.02, 0.1, -0.032), (0.02, 0.1, 0.032), (-0.06, 0.095, 0.032),
           (-0.035, 0.112, -0.01), (0.0, 0.114, 0.01)]
    p.hull(pts, [[0, 1, 2, 3], [0, 1, 4], [1, 2, 5, 4], [2, 3, 5], [3, 0, 4, 5]], [ORE, ORE, ROCK, ORE, ROCK])
    # cab and engine at the front
    box(p, 0.022, 0.045, -0.04, 0.072, 0.072, 0.04, HAUL_YELLOW, top=HAUL_YELLOW_DARK)
    box(p, 0.03, 0.072, 0.006, 0.064, 0.1, 0.04, HAUL_YELLOW)
    box(p, 0.063, 0.078, 0.01, 0.066, 0.096, 0.036, WINDOW)
    box(p, 0.034, 0.078, 0.039, 0.06, 0.096, 0.042, WINDOW)
    box(p, 0.071, 0.048, -0.03, 0.075, 0.068, 0.03, IRON)  # radiator grille
    return p


def ore_pile(name, rng, r=0.05, h=0.045):
    p = Part(name)
    lumpy_mound(p, r, r * 0.9, h, [ORE, srgb(0x8E6440), ORE], rng, y0=FOOT / 2, rings=2, n=7)
    return p


def modern_mine():
    rng = random.Random(89)
    return [
        at(open_pit("pit", rng), 0.1, -0.3),
        at(pit_floor("ground_pit"), 0.1, -0.28),
        conveyor_between("conveyor", (0.04, -0.24), (-0.34, 0.12)),
        at(plant("plant"), -0.46, 0.14, -0.6),
        at(silo("silo"), -0.46, -0.24),
        at(haul_truck("truck"), 0.34, 0.34, 0.45),
        at(ore_pile("ore", rng), 0.5, 0.0),
    ]


def collapsed_adit(name, rng):
    """The ancient mine's tunnel mouth, abandoned: grey timbers, a sagging lintel and planks nailed across."""
    p = Part(name)
    lumpy_mound(p, 0.14, 0.12, 0.13, [WEATHERED_DARK, ROCK_DARK, WEATHERED], rng, at=(0, 0, -0.05))
    z = 0.075
    box(p, -0.04, FOOT, z - 0.06, 0.04, 0.085, z - 0.004, DARK)
    box(p, -0.057, FOOT, z - 0.06, -0.039, 0.095, z + 0.006, WEATHERED_DARK)
    beam(p, (0.048, FOOT, z - 0.002), (0.058, 0.07, z - 0.002), 0.017, WEATHERED_DARK)  # leaning post
    beam(p, (-0.07, 0.1, z - 0.02), (0.07, 0.06, z - 0.02), 0.02, WEATHERED)  # sagging lintel
    # planks nailed across the opening
    beam(p, (-0.045, 0.012, z + 0.002), (0.045, 0.075, z + 0.002), 0.012, WEATHERED)
    beam(p, (-0.045, 0.072, z + 0.008), (0.045, 0.016, z + 0.008), 0.012, WEATHERED)
    box(p, -0.046, 0.036, z - 0.002, 0.046, 0.05, z + 0.004, WEATHERED_DARK)
    # fallen rocks at the mouth
    for x, s in ((-0.03, 0.016), (0.035, 0.012), (0.005, 0.01)):
        cylinder(p, s, FOOT / 2, s * 0.9, ROCK_DARK, at=(x, 0, z + 0.02), n=5, r_top=s * 0.4, top=WEATHERED)
    return p


def broken_rails(name, length=0.2):
    """Old track: a few skewed sleepers, one rail missing a length, one lifted and bent away."""
    p = Part(name)
    box(p, -0.05, FOOT / 2, -0.01, 0.05, 0.003, length * 0.75, srgb(0x6E6A5E))
    n = 6
    skew = [0.0, 0.25, -0.15, 0.4, 0.1, -0.3]
    for i in range(n):
        z = (i + 0.5) * length / n
        if i == 4:
            continue  # a missing sleeper
        box(p, -0.036, 0.003, -0.007, 0.036, 0.01, 0.007, WEATHERED_DARK, at=(0, 0, z), yaw=skew[i])
    box(p, -0.027, 0.01, -0.01, -0.021, 0.017, length * 0.55, RUST)
    box(p, 0.021, 0.01, -0.01, 0.027, 0.017, length * 0.3, RUST)
    beam(p, (0.024, 0.014, length * 0.3), (0.06, 0.016, length * 0.85), 0.006, RUST)
    return p


def tipped_cart(name):
    """An ore cart on its side with a wheel gone."""
    p = Part(name)
    for sx, sz in ((-1, -1), (1, -1), (1, 1)):
        pts = []
        for i in range(8):
            a = i * math.tau / 8
            for x in (sx * 0.026, sx * 0.034):
                pts.append((x, 0.016 + math.sin(a) * 0.014, sz * 0.026 + math.cos(a) * 0.014))
        faces = [[2 * i for i in range(8)], [2 * i + 1 for i in range(8)]]
        faces += [[2 * i, 2 * ((i + 1) % 8), 2 * ((i + 1) % 8) + 1, 2 * i + 1] for i in range(8)]
        p.hull(pts, faces, RUST)
    # an open tub of boards, so the empty inside shows once it lies on its side
    box(p, -0.03, 0.02, -0.042, 0.03, 0.026, 0.042, OLD_BOARDS_DARK)
    for sx in (-1, 1):
        box(p, sx * 0.033 - 0.003, 0.02, -0.046, sx * 0.033 + 0.003, 0.062, 0.046, OLD_BOARDS)
    for sz in (-1, 1):
        box(p, -0.036, 0.02, sz * 0.046 - 0.003, 0.036, 0.062, sz * 0.046 + 0.003, OLD_BOARDS_DARK)
        box(p, -0.037, 0.03, sz * 0.046 - 0.004, 0.037, 0.036, sz * 0.046 + 0.004, RUST)  # iron band
    turned(p, roll=math.radians(-100))
    low = min(v[1] for tri in p.tris for v in tri[:3])
    return shifted(p, dy=-0.008 - low)  # rest on the ground, sunk a touch


def ruined_shed(name):
    """The lean-to, fallen in: a stone footing, one post standing and the roof boards slid to the ground."""
    p = Part(name)
    box(p, -0.06, FOOT, -0.045, 0.06, 0.01, 0.045, WEATHERED_DARK, top=WEATHERED)
    box(p, -0.058, 0.0, -0.045, 0.058, 0.04, -0.035, WEATHERED_DARK)  # broken back wall
    box(p, 0.046, 0.01, -0.044, 0.058, 0.085, -0.032, WEATHERED_DARK)  # one post still standing
    box(p, -0.058, 0.01, -0.044, -0.046, 0.05, -0.032, WEATHERED_DARK)
    for k, x in enumerate((-0.05, -0.018, 0.016, 0.05)):  # roof planks slid down to the ground
        top = 0.04 + 0.045 * (x + 0.07) / 0.14
        plank(p, (x, 0.012, 0.045 + 0.01 * k), (x + 0.004 * k, top, -0.04), 0.03, 0.007,
              WEATHERED if k % 2 else WEATHERED_DARK)
    beam(p, (-0.06, 0.006, 0.07), (0.05, 0.006, 0.09), 0.012, WEATHERED_DARK)
    return p


def tufts(name, rng, blades=5):
    """A small clump of grass and weeds: a few thin cones."""
    p = Part(name)
    for k in range(blades):
        a = k * math.tau / blades + rng.random()
        ox, oz = math.cos(a) * 0.012, math.sin(a) * 0.012
        h = 0.04 + 0.025 * rng.random()
        cylinder(p, 0.011, FOOT / 2, h, TUFT if k % 2 else TUFT_DARK, at=(ox, 0, oz), n=4, r_top=0.0, phase=a)
    return p


def overgrown_heap(name, rng, r=0.07, h=0.045):
    p = Part(name)
    lumpy_mound(p, r, r * 0.85, h, [WEATHERED_DARK, TUFT_DARK, WEATHERED], rng, y0=FOOT / 2, rings=2, n=8)
    return p


def depleted_mine():
    rng = random.Random(97)
    return [
        at(collapsed_adit("adit", rng), 0.0, -0.22),
        at(shifted(broken_rails("ground_rails", length=0.18), dz=-0.065), 0.0, 0.05),
        at(tipped_cart("cart"), 0.24, 0.28, -1.2),
        at(spoil_heap("spill", rng, r=0.035, h=0.018), 0.24, 0.42),
        at(overgrown_heap("spoil", rng), 0.44, -0.1),
        at(ruined_shed("shed"), -0.42, 0.0, 0.35),
        *[at(tufts(f"tufts_{i}", rng), x, z) for i, (x, z) in enumerate(
            [(-0.2, -0.06), (0.18, 0.06), (0.14, -0.1), (-0.38, 0.32), (0.46, 0.34), (-0.12, 0.44)])],
    ]


MODELS = {
    "improvement_improvement_mine": mine,
    "improvement_improvement_quarry": quarry,
    "improvement_improvement_shaft_mine": shaft_mine,
    "improvement_improvement_modern_mine": modern_mine,
    "depleted_mine": depleted_mine,
}
