"""Water improvements: fishing and whaling boats, offshore platforms and the future sea structures.

Every part sits at sea level (y = 0). Hulls and decks ride above it with a little draft below; legs, pilings and
the submerged town's lower levels reach down into the translucent water, where they show faintly.
"""
import random

from scipy.spatial import ConvexHull

from .kit import *  # noqa: F401,F403
from .kit import Part, at

HULL_PAINT = srgb(0x2F3438)
WHALE = srgb(0x464B52)
WHALE_LIGHT = srgb(0x5E646C)
BUOY = srgb(0xD8662A)
STEEL = srgb(0x7C8288)
STEEL_DARK = srgb(0x555B62)
STEEL_DECK = srgb(0x646A70)
SAFETY = srgb(0xD9A62B)
RIG_RED = srgb(0xB8412F)
WHITE = srgb(0xE9ECEC)
PALE = srgb(0xC9CFD2)
PALE_DARK = srgb(0xA7AFB4)
TEAL = srgb(0x2FD3C4)
TEAL_DARK = srgb(0x1F9C93)
FLAME = srgb(0xF29A2E)
FLAME_CORE = srgb(0xFFD966)
HELIPAD = srgb(0x3F5A4A)
NAVY = srgb(0x6F7780)
NAVY_DARK = srgb(0x4E555C)
GLASS = srgb(0xBFEFF2)
GLASS_SHADE = srgb(0x9CD9E0)


def solid(part, points, colour, top=None, at=(0, 0, 0), yaw=0.0, top_cut=0.8):
    """A convex solid around any point cloud; faces facing up more than top_cut take the top colour."""
    pts = np.array(place(points, at, yaw))
    hull = ConvexHull(pts)
    centre = pts.mean(axis=0)
    for (i, j, k), eq in zip(hull.simplices, hull.equations):
        colour_ = top if top is not None and eq[1] > top_cut else colour
        part.tri(pts[i], pts[j], pts[k], colour_, centre)


def tilt_x(points, angle):
    """Rotate points about the X axis (positive tips +Y toward +Z)."""
    c, s = math.cos(angle), math.sin(angle)
    return [(x, c * y - s * z, s * y + c * z) for x, y, z in points]


def ring_points(r, y, n, phase=0.0, rz=None):
    rz = r if rz is None else rz
    return [(math.cos(phase + i * math.tau / n) * r, y, math.sin(phase + i * math.tau / n) * rz) for i in range(n)]


def dome(part, r, y, colour, top=None, at=(0, 0, 0), n=10, squash=1.0):
    """A faceted half sphere on a base at height y."""
    pts = []
    for k, lat in enumerate((0.0, 0.5, 0.95)):
        pts += ring_points(r * math.cos(lat), y + r * squash * math.sin(lat), n, phase=0.5 * k * math.tau / n)
    pts.append((0, y + r * squash, 0))
    solid(part, pts, colour, top, at=at, top_cut=0.75)


def hull_points(length, width, height, draft, sheer=0.008):
    """A pointed boat hull along X, bow at +X: a deck outline at height, a narrow keel below the waterline."""
    hl, hw = length / 2, width / 2
    pts = [(-hl, height, -hw * 0.75), (-hl, height, hw * 0.75),
           (-hl * 0.3, height, -hw), (-hl * 0.3, height, hw),
           (hl * 0.3, height, -hw * 0.92), (hl * 0.3, height, hw * 0.92),
           (hl, height + sheer, 0.0)]
    pts += [(-hl * 0.88, -draft, -hw * 0.3), (-hl * 0.88, -draft, hw * 0.3),
            (hl * 0.45, -draft, -hw * 0.25), (hl * 0.45, -draft, hw * 0.25), (hl * 0.86, -draft * 0.3, 0.0)]
    return pts


def deck_outline(length, width, y, inset=0.8):
    hl, hw = length / 2 * inset, width / 2 * inset
    return [(-hl, y, -hw * 0.75), (-hl, y, hw * 0.75), (-hl * 0.3, y, -hw), (-hl * 0.3, y, hw),
            (hl * 0.3, y, -hw * 0.92), (hl * 0.3, y, hw * 0.92), (hl * 0.85, y, 0.0)]


def boat(part, length, width, height, draft, hull, inside, at=(0, 0, 0), yaw=0.0):
    """An open boat: a hull with a darker well inset into the top."""
    solid(part, hull_points(length, width, height, draft), hull, inside, at=at, yaw=yaw)
    well_ = deck_outline(length, width, height + 0.002) + deck_outline(length, width, height - 0.006)
    solid(part, well_, inside, BOARDS_DARK, at=at, yaw=yaw)


# --- fishing boats -----------------------------------------------------------------------------------------------

def fishing_boat(name, length=0.12, sail=None, hull=TIMBER, mast=True):
    """A small open fishing boat, bow at +X, with an optional mast and a lateen sail."""
    p = Part(name)
    width, height = length * 0.36, 0.016
    boat(p, length, width, height, 0.012, hull, BOARDS)
    box(p, -length * 0.38, height - 0.004, -width * 0.36, -length * 0.38 + 0.008, height + 0.004, width * 0.36,
        TIMBER_LIGHT)  # stern thwart
    box(p, -0.012, height, -0.012, 0.012, height + 0.014, 0.012, THATCH_DARK, top=srgb(0x9FB4B8))  # fish basket
    if mast:
        x = length * 0.18
        top = height + length * 0.85
        beam(p, (x, height - 0.004, 0), (x, top, 0), 0.005, TIMBER)
        if sail:
            # a lateen sail on a slanting yard, bellied out toward +Z
            a, b, c = (x + 0.012, top - 0.004, 0.0), (x + 0.02, height + 0.022, 0.0), (x - length * 0.5, height + 0.03, 0.0)
            belly = tuple(np.mean([a, b, c], axis=0) + (0, 0, 0.01))
            solid(p, [a, b, c, (a[0], a[1], -0.002), (b[0], b[1], -0.002), (c[0], c[1], -0.002), belly], sail)
            beam(p, (x + 0.016, top + 0.004, 0.002), (x - length * 0.54, height + 0.024, 0.002), 0.003, TIMBER)
    else:
        # oars resting across the gunwales
        for sz in (-1, 1):
            beam(p, (-0.01, height + 0.002, sz * width * 0.3), (0.03, height - 0.004, sz * width * 1.4), 0.003,
                 TIMBER_LIGHT)
    return p


def net_line(name, count=6, span=0.24, rng=None):
    """Floats on a curved net line: orange and cream buoys joined by a rope."""
    p = Part(name)
    spots = []
    for i in range(count):
        t = i / (count - 1)
        spots.append((span * (t - 0.5), 0.0, 0.035 * math.sin(t * math.pi) + 0.004 * (rng.random() - 0.5)))
    for a, b in zip(spots, spots[1:]):
        beam(p, (a[0], 0.004, a[2]), (b[0], 0.004, b[2]), 0.0025, ROPE)
    for i, (x, _, z) in enumerate(spots):
        cylinder(p, 0.011, -0.008, 0.01, BUOY if i % 2 == 0 else CLOTH_CREAM, at=(x, 0, z), n=6)
        cylinder(p, 0.011, 0.01, 0.017, BUOY if i % 2 == 0 else CLOTH_CREAM, at=(x, 0, z), n=6, r_top=0.004)
    # a marker pole with a pennant at the far end
    x, _, z = spots[-1]
    beam(p, (x, -0.01, z), (x, 0.07, z), 0.004, TIMBER)
    solid(p, [(x, 0.068, z), (x, 0.048, z), (x + 0.03, 0.058, z), (x, 0.068, z + 0.002), (x, 0.048, z + 0.002)],
          CLOTH_RED)
    return p


def fish_trap(name):
    """A ring of weir stakes round a wicker trap that shows faintly below the surface, with a flagged marker."""
    p = Part(name)
    cylinder(p, 0.026, -0.05, -0.004, THATCH_DARK, n=8, top=THATCH)
    cylinder(p, 0.012, -0.004, 0.004, THATCH_DARK, n=8)  # the trap's neck just breaking the surface
    for i in range(7):
        a = i * math.tau / 7 + 0.3
        h = 0.03 + 0.012 * ((i * 3) % 4) / 3
        x, z = math.cos(a) * 0.045, math.sin(a) * 0.045
        beam(p, (x, -0.04, z), (x * 1.05, h, z * 1.05), 0.006, TIMBER if i % 2 else TIMBER_LIGHT)
    beam(p, (0.0, -0.03, 0.0), (0.0, 0.1, 0.0), 0.005, TIMBER)
    solid(p, [(0.002, 0.098, 0), (0.002, 0.07, 0), (0.04, 0.084, 0), (0.002, 0.098, 0.003), (0.002, 0.07, 0.003)],
          BUOY)
    return p


def fishing_boats():
    rng = random.Random(11)
    return [
        at(fishing_boat("boat_sail", 0.16, sail=CLOTH_CREAM), -0.42, -0.22, 0.35),
        at(fishing_boat("boat_mast", 0.15, hull=srgb(0x4C6E8A)), 0.44, 0.24, 2.7),
        at(fishing_boat("boat_oars", 0.12, hull=TIMBER_LIGHT, mast=False), 0.12, -0.5, -0.25),
        at(net_line("nets", span=0.3, rng=rng), -0.3, 0.42, -0.2),
        at(fish_trap("trap"), 0.5, -0.28),
    ]


# --- whaling boats -----------------------------------------------------------------------------------------------

def whaling_ship(name, length=0.28, width=0.075):
    """A two-masted whaler with furled sails, a painted gunport band, a try-works and a bowsprit; bow at +X."""
    p = Part(name)
    height = 0.032
    solid(p, hull_points(length, width, height, 0.022, sheer=0.014), HULL_PAINT, BOARDS)
    band = [(x * 1.01, y, z * 1.06) for x, y, z in deck_outline(length, width, height - 0.012, inset=1.0)]
    band += [(x, y + 0.006, z) for x, y, z in band]
    solid(p, band, CLOTH_CREAM)
    for i in range(6):  # black painted ports on the band
        x = -length * 0.33 + i * length * 0.12
        for sz in (-1, 1):
            z = sz * (width / 2 * 1.04 + 0.0005)
            box(p, x - 0.006, height - 0.0115, z - 0.002, x + 0.006, height - 0.0065, z + 0.002, DARK)
    hl = length / 2
    box(p, -hl * 0.98, height - 0.004, -width * 0.36, -hl * 0.62, height + 0.016, width * 0.36, BOARDS_DARK,
        top=BOARDS)  # raised quarterdeck
    box(p, -0.016, height - 0.002, -0.014, 0.014, height + 0.022, 0.014, TILE, top=STONE_DARK)  # try-works
    box(p, -0.008, height + 0.022, -0.005, 0.0, height + 0.04, 0.005, STONE_DARK)
    beam(p, (hl * 0.9, height + 0.01, 0), (hl * 1.35, height + 0.04, 0), 0.005, TIMBER)  # bowsprit
    for x, h in ((hl * 0.42, 0.22), (-hl * 0.32, 0.2)):
        beam(p, (x, height - 0.004, 0), (x, height + h, 0), 0.007, TIMBER)
        for y, half in ((0.07, 0.05), (0.14, 0.04), (0.195, 0.028)):
            if y > h - 0.01:
                continue
            beam(p, (x, height + y, -half), (x, height + y, half), 0.004, TIMBER)
            beam(p, (x + 0.004, height + y - 0.007, -half * 0.92), (x + 0.004, height + y - 0.007, half * 0.92),
                 0.01, CLOTH_CREAM)  # furled sail under the yard
        beam(p, (x, height + h, 0), (hl * 1.33 if x > 0 else hl * 0.42, height + (0.04 if x > 0 else h), 0),
             0.002, ROPE)
        beam(p, (x, height + h, 0), (-hl * 0.95, height + 0.016, 0), 0.002, ROPE)
    box(p, -hl * 0.32 - 0.003, height + 0.2, -0.0005, -hl * 0.32 + 0.025, height + 0.214, 0.0005, CLOTH_RED)
    return p


def longboat(name, oars=3):
    """A white whaleboat with oars out, pointed at both ends."""
    p = Part(name)
    length, width, height = 0.085, 0.026, 0.012
    solid(p, hull_points(length, width, height, 0.01) + [(-length / 2 - 0.004, height + 0.006, 0)],
          CLOTH_CREAM, BOARDS)
    solid(p, deck_outline(length, width, height + 0.002) + deck_outline(length, width, height - 0.004),
          BOARDS, BOARDS_DARK)
    for i in range(oars):
        x = -0.02 + i * 0.02
        for sz in (-1, 1):
            beam(p, (x, height + 0.008, sz * width * 0.3), (x - 0.012, -0.002, sz * width * 2.2), 0.0028, TIMBER_LIGHT)
        box(p, x - 0.005, height, -0.005, x + 0.005, height + 0.016, 0.005, CLOTH_BLUE, top=PLASTER_WARM)  # rower
    box(p, length * 0.38 - 0.005, height, -0.005, length * 0.38 + 0.005, height + 0.022, 0.005, CLOTH_RED,
        top=PLASTER_WARM)  # harpooner standing in the bow
    beam(p, (length * 0.38, height + 0.018, 0.004), (length * 0.38 + 0.03, height + 0.03, 0.004), 0.002, IRON)
    return p


def whale_back(name, rng):
    """A surfacing whale, head at +X: a low dark back breaking the surface over a long body that shows faintly
    under the water, a small dorsal fin, flippers and a spout."""
    p = Part(name)
    pts = []
    for x, top, bottom, rz in ((-0.1, -0.022, -0.032, 0.008), (-0.06, -0.004, -0.04, 0.02),
                               (-0.01, 0.008, -0.046, 0.032), (0.04, 0.012, -0.044, 0.034),
                               (0.085, 0.004, -0.036, 0.026), (0.11, -0.012, -0.03, 0.014)):
        mid, half = (top + bottom) / 2, (top - bottom) / 2
        for i in range(8):
            a = i * math.tau / 8
            pts.append((x, mid + half * math.sin(a), rz * math.cos(a)))
    solid(p, pts, WHALE, WHALE_LIGHT, top_cut=0.95)
    solid(p, [(-0.045, 0.0, -0.005), (-0.045, 0.0, 0.005), (-0.015, 0.006, -0.007), (-0.015, 0.006, 0.007),
              (-0.04, 0.026, 0.0)], WHALE)  # dorsal fin
    for sz in (-1, 1):  # flippers, just under the surface
        solid(p, [(0.05, -0.02, sz * 0.026), (0.07, -0.02, sz * 0.026), (0.04, -0.03, sz * 0.07),
                  (0.05, -0.024, sz * 0.026), (0.065, -0.024, sz * 0.026)], WHALE_LIGHT)
    # tail flukes trailing flat under the surface
    for sz in (-1, 1):
        solid(p, [(-0.095, -0.025, 0.0), (-0.1, -0.03, 0.0), (-0.135, -0.028, sz * 0.04), (-0.125, -0.026, sz * 0.045),
                  (-0.1, -0.026, sz * 0.012)], WHALE)
    # spout: a few pale droplets above the blowhole
    spray = srgb(0xDDEFF2)
    for dx, dy, dz, r in ((0.075, 0.035, 0.0, 0.008), (0.07, 0.058, 0.008, 0.009), (0.082, 0.056, -0.008, 0.009),
                          (0.076, 0.08, 0.0, 0.01), (0.065, 0.075, -0.01, 0.007), (0.088, 0.074, 0.01, 0.007)):
        cylinder(p, r, dy - r, dy + r, spray, at=(dx, 0, dz), n=5, phase=rng.random())
    return p


def whale_tail(name):
    """A second whale diving: its tail stock and broad two-lobed fluke raised out of the water."""
    p = Part(name)
    solid(p, [(-0.018, -0.05, -0.016), (0.018, -0.05, -0.016), (-0.018, -0.05, 0.016), (0.018, -0.05, 0.016),
              (-0.006, 0.075, -0.005), (0.006, 0.075, -0.005), (-0.006, 0.075, 0.005), (0.006, 0.075, 0.005)],
          WHALE)
    for sx in (-1, 1):
        lobe = [(0.0, 0.068, 0.0), (sx * 0.008, 0.086, 0.0), (sx * 0.07, 0.118, 0.0), (sx * 0.075, 0.106, 0.0),
                (sx * 0.04, 0.08, 0.0)]
        solid(p, lobe + [(x, y, 0.007) for x, y, _ in lobe], WHALE, WHALE_LIGHT, top_cut=0.95)
    # white water where the tail leaves the surface
    for i in range(5):
        a = i * math.tau / 5
        cylinder(p, 0.008, -0.004, 0.006, srgb(0xDDEFF2), at=(math.cos(a) * 0.024, 0, math.sin(a) * 0.024), n=5)
    return p


def whaling_boats():
    rng = random.Random(7)
    return [
        at(whaling_ship("ship"), -0.08, -0.4, 0.12),
        at(longboat("longboat_a"), -0.46, 0.2, -0.35),
        at(longboat("longboat_b"), 0.06, 0.48, 0.15),
        at(whale_back("whale_back", rng), 0.36, 0.24, 0.5),
        at(whale_tail("whale_tail"), 0.56, -0.12, 0.3),
    ]


# --- offshore platform -------------------------------------------------------------------------------------------

DECK_Y = 0.075
DECK_X, DECK_Z = 0.1, 0.075


def platform_jacket(name):
    """Four steel legs reaching well below the surface, cross bracing and a two-level steel deck."""
    p = Part(name)
    for sx in (-1, 1):
        for sz in (-1, 1):
            x, z = sx * (DECK_X - 0.018), sz * (DECK_Z - 0.016)
            cylinder(p, 0.012, -0.22, DECK_Y, SAFETY, at=(x, 0, z), n=8)
            cylinder(p, 0.016, -0.012, 0.012, STEEL_DARK, at=(x, 0, z), n=8)  # wave-zone collar
    for sz in (-1, 1):  # X braces on the long sides, above and below the surface
        z = sz * (DECK_Z - 0.016)
        for y0, y1 in ((-0.13, -0.02), (0.012, DECK_Y - 0.008)):
            for sx in (-1, 1):
                beam(p, (sx * (DECK_X - 0.018), y0, z), (-sx * (DECK_X - 0.018), y1, z), 0.006, STEEL_DARK)
    for sx in (-1, 1):
        x = sx * (DECK_X - 0.018)
        beam(p, (x, 0.04, -DECK_Z + 0.016), (x, 0.04, DECK_Z - 0.016), 0.006, STEEL_DARK)
    box(p, -DECK_X, DECK_Y - 0.012, -DECK_Z, DECK_X, DECK_Y, DECK_Z, STEEL, top=STEEL_DECK)
    box(p, -DECK_X + 0.006, DECK_Y - 0.03, -DECK_Z + 0.006, DECK_X - 0.006, DECK_Y - 0.018, DECK_Z - 0.006,
        STEEL_DARK)  # lower cellar deck
    for i in range(3):  # process modules: pipes, tanks
        x = 0.02 + i * 0.026
        cylinder(p, 0.01, DECK_Y, DECK_Y + 0.035, PALE if i % 2 else WHITE, at=(x, 0, DECK_Z - 0.02), n=8,
                 top=PALE_DARK)
    box(p, 0.0, DECK_Y, -DECK_Z + 0.008, 0.075, DECK_Y + 0.022, -0.02, PALE_DARK, top=PALE)
    return p


def accommodation(name):
    """A white quarters block with rows of windows and a helipad overhanging the -X edge."""
    p = Part(name)
    x0, x1, z0, z1 = -DECK_X + 0.004, -0.02, -DECK_Z + 0.006, DECK_Z - 0.006
    box(p, x0, DECK_Y, z0, x1, DECK_Y + 0.06, z1, WHITE, top=PALE)
    for k in range(3):
        y = DECK_Y + 0.012 + k * 0.017
        box(p, x0 + 0.008, y, z1 - 0.002, x1 - 0.008, y + 0.007, z1 + 0.002, WINDOW)
        box(p, x1 - 0.002, y, z0 + 0.012, x1 + 0.002, y + 0.007, z1 - 0.012, WINDOW)
    box(p, x0 + 0.01, DECK_Y + 0.06, -0.02, x0 + 0.03, DECK_Y + 0.072, 0.0, RIG_RED)
    # helipad on struts out past the edge
    hx, hy = x0 - 0.02, DECK_Y + 0.07
    for sz in (-1, 1):
        beam(p, (x0, DECK_Y + 0.03, sz * 0.03), (hx, hy, sz * 0.02), 0.005, STEEL_DARK)
    cylinder(p, 0.052, hy, hy + 0.008, STEEL_DARK, at=(hx, 0, 0), n=8, top=HELIPAD, phase=math.pi / 8)
    for x in (-0.014, 0.014):
        box(p, hx + x - 0.004, hy + 0.008, -0.02, hx + x + 0.004, hy + 0.0105, 0.02, WHITE)
    box(p, hx - 0.012, hy + 0.008, -0.004, hx + 0.012, hy + 0.0105, 0.004, WHITE)
    return p


def drilling_derrick(name, base=0.026, height=0.21):
    """A tapering lattice derrick over the drill floor, red cap and crown block."""
    p = Part(name)
    x, z = 0.045, -0.01
    y0 = DECK_Y
    box(p, x - base - 0.006, y0, z - base - 0.006, x + base + 0.006, y0 + 0.018, z + base + 0.006, STEEL_DARK,
        top=STEEL)
    y0 += 0.018
    top = base * 0.3
    corners = [(sx, sz) for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    for sx, sz in corners:
        beam(p, (x + sx * base, y0, z + sz * base), (x + sx * top, y0 + height, z + sz * top), 0.005, WHITE)
    for k in range(1, 5):  # horizontal girts and a diagonal on the visible faces
        t0, t1 = (k - 1) / 4, k / 4
        r0, r1 = base + (top - base) * t0, base + (top - base) * t1
        y_0, y_1 = y0 + height * t0, y0 + height * t1
        for (ax, az), (bx, bz) in zip(corners, corners[1:] + corners[:1]):
            beam(p, (x + ax * r1, y_1, z + az * r1), (x + bx * r1, y_1, z + bz * r1), 0.003, WHITE)
        beam(p, (x - r0, y_0, z + r0), (x + r1, y_1, z + r1), 0.003, RIG_RED)
        beam(p, (x + r0, y_0, z - r0), (x + r1, y_1, z + r1), 0.003, RIG_RED)
    box(p, x - top - 0.004, y0 + height, z - top - 0.004, x + top + 0.004, y0 + height + 0.014, z + top + 0.004,
        RIG_RED)
    return p


def crane(name):
    """A pedestal crane at the front corner with its boom reaching out over the water."""
    p = Part(name)
    x, z = DECK_X - 0.02, DECK_Z - 0.016
    cylinder(p, 0.01, DECK_Y, DECK_Y + 0.04, SAFETY, at=(x, 0, z), n=8)
    box(p, x - 0.014, DECK_Y + 0.04, z - 0.012, x + 0.01, DECK_Y + 0.058, z + 0.012, SAFETY, top=WHITE)
    box(p, x - 0.013, DECK_Y + 0.044, z + 0.01, x - 0.001, DECK_Y + 0.054, z + 0.0125, WINDOW)
    tip = (x + 0.07, DECK_Y + 0.12, z + 0.06)
    beam(p, (x, DECK_Y + 0.05, z), tip, 0.007, SAFETY)
    beam(p, (x - 0.012, DECK_Y + 0.07, z - 0.008), tip, 0.002, ROPE)
    beam(p, tip, (tip[0], DECK_Y + 0.03, tip[2]), 0.002, ROPE)
    box(p, tip[0] - 0.005, DECK_Y + 0.022, tip[2] - 0.005, tip[0] + 0.005, DECK_Y + 0.032, tip[2] + 0.005, RIG_RED)
    return p


def flare_boom(name):
    """A lattice boom leaning out from the -Z/+X corner with a gas flame at its tip."""
    p = Part(name)
    a, b = (DECK_X - 0.01, DECK_Y, -DECK_Z + 0.01), (DECK_X + 0.12, DECK_Y + 0.1, -DECK_Z - 0.03)
    for dz in (-0.006, 0.006):
        beam(p, (a[0], a[1], a[2] + dz), (b[0], b[1], b[2] + dz * 0.4), 0.004, STEEL)
    for t in (0.25, 0.5, 0.75):
        y = a[1] + (b[1] - a[1]) * t
        x = a[0] + (b[0] - a[0]) * t
        zc = a[2] + (b[2] - a[2]) * t
        beam(p, (x, y, zc - 0.006), (x + 0.012, y + 0.025, zc + 0.006), 0.0025, STEEL)
    cylinder(p, 0.005, b[1] - 0.004, b[1] + 0.006, STEEL_DARK, at=(b[0], 0, b[2]), n=6)
    cylinder(p, 0.024, b[1] + 0.006, b[1] + 0.075, FLAME, at=(b[0], 0, b[2]), n=6, r_top=0.0)
    cylinder(p, 0.016, b[1] + 0.006, b[1] + 0.05, FLAME, at=(b[0] + 0.016, 0, b[2] - 0.006), n=5, r_top=0.0)
    cylinder(p, 0.014, b[1] + 0.008, b[1] + 0.048, FLAME_CORE, at=(b[0] + 0.004, 0, b[2] + 0.014), n=5,
             r_top=0.0)
    cylinder(p, 0.024, b[1] + 0.002, b[1] + 0.014, FLAME, at=(b[0], 0, b[2]), n=6, r_top=0.018)
    return p


def supply_boat(name):
    """A small orange and white supply vessel tending the platform."""
    p = Part(name)
    length, width = 0.11, 0.034
    solid(p, hull_points(length, width, 0.016, 0.012), BUOY, STEEL_DECK)
    box(p, 0.0, 0.014, -width * 0.36, 0.036, 0.04, width * 0.36, WHITE, top=PALE)
    box(p, 0.032, 0.026, -width * 0.3, 0.037, 0.035, width * 0.3, WINDOW)
    box(p, 0.006, 0.04, -0.004, 0.012, 0.056, 0.004, PALE_DARK)
    return p


def offshore_platform():
    x, z = -0.06, -0.28
    return [
        at(platform_jacket("jacket"), x, z),
        at(accommodation("quarters"), x, z),
        at(drilling_derrick("derrick"), x, z),
        at(crane("crane"), x, z),
        at(flare_boom("flare"), x, z),
        at(supply_boat("supply_boat"), 0.42, 0.3, 2.6),
    ]


# --- extraction facility (transhuman) ----------------------------------------------------------------------------

def pontoon(part, r, colour=WHITE, deck=PALE, n=8, at=(0, 0, 0), strip=True):
    """A low, many-sided white float with a teal light strip round its rim."""
    x, _, z = at
    cylinder(part, r * 0.86, -0.03, -0.004, PALE_DARK, at=(x, 0, z), n=n, r_top=r, phase=math.pi / n)
    cylinder(part, r, -0.004, 0.022, colour, at=(x, 0, z), n=n, top=deck, phase=math.pi / n)
    if strip:
        cylinder(part, r + 0.002, 0.008, 0.013, TEAL, at=(x, 0, z), n=n, phase=math.pi / n)


def extraction_hub(name):
    """The main float: a big dome, a small dome and a slim drilling spire ringed with teal."""
    p = Part(name)
    pontoon(p, 0.1)
    dome(p, 0.045, 0.022, WHITE, PALE, at=(-0.035, 0, 0.02))
    cylinder(p, 0.046, 0.034, 0.038, TEAL, at=(-0.035, 0, 0.02), n=10, phase=0.0)
    dome(p, 0.026, 0.022, PALE, WHITE, at=(0.02, 0, 0.055), n=8)
    # spire on a tripod
    sx, sz = 0.04, -0.03
    for i in range(3):
        a = i * math.tau / 3 + 0.4
        beam(p, (sx + math.cos(a) * 0.03, 0.022, sz + math.sin(a) * 0.03), (sx, 0.12, sz), 0.004, PALE)
    cylinder(p, 0.009, 0.022, 0.34, WHITE, at=(sx, 0, sz), n=6, r_top=0.002)
    for y, r in ((0.12, 0.012), (0.2, 0.01), (0.27, 0.007)):
        cylinder(p, r, y, y + 0.008, TEAL, at=(sx, 0, sz), n=6)
    return p


def tank_float(name):
    """A satellite float carrying three capped storage tanks."""
    p = Part(name)
    pontoon(p, 0.06, n=6)
    for i, (x, z) in enumerate(((-0.02, -0.018), (0.022, -0.012), (0.0, 0.022))):
        cylinder(p, 0.016, 0.022, 0.07, WHITE if i != 1 else PALE, at=(x, 0, z), n=8)
        dome(p, 0.016, 0.07, PALE, WHITE, at=(x, 0, z), n=8, squash=0.6)
        cylinder(p, 0.0165, 0.05, 0.055, TEAL, at=(x, 0, z), n=8)
    return p


def extraction_bridge(name, a, b):
    """A slim covered walkway between two floats (points in world units, relative to the part)."""
    p = Part(name)
    ax, az = a[0] / SCALE, a[1] / SCALE
    bx, bz = b[0] / SCALE, b[1] / SCALE
    beam(p, (ax, 0.026, az), (bx, 0.026, bz), 0.014, PALE)
    beam(p, (ax, 0.034, az), (bx, 0.034, bz), 0.004, TEAL)
    mx, mz = (ax + bx) / 2, (az + bz) / 2
    cylinder(p, 0.008, -0.04, 0.02, PALE_DARK, at=(mx, 0, mz), n=6)
    return p


def beacon_buoy(name):
    """A slim marker pylon on a small float."""
    p = Part(name)
    cylinder(p, 0.016, -0.02, 0.012, WHITE, n=6, top=PALE)
    cylinder(p, 0.005, 0.012, 0.09, PALE, n=6, r_top=0.003)
    cylinder(p, 0.008, 0.09, 0.1, TEAL, n=6)
    return p


def extraction_facility():
    hub, tanks = (-0.22, -0.26), (0.38, -0.04)
    return [
        at(extraction_hub("hub"), *hub),
        at(tank_float("tanks"), *tanks),
        at(extraction_bridge("bridge", (hub[0] + 0.19, hub[1] + 0.07), (tanks[0] - 0.1, tanks[1] - 0.04)), 0, 0),
        at(beacon_buoy("beacon_a"), -0.5, 0.3),
        at(beacon_buoy("beacon_b"), 0.42, 0.42),
    ]


# --- aquatic anti-air defence ------------------------------------------------------------------------------------

def gun_barge(name, n=6, r=0.1):
    """A grey hexagonal gun platform with two twin AA turrets."""
    p = Part(name)
    cylinder(p, r * 0.85, -0.03, -0.004, NAVY_DARK, n=n, r_top=r, phase=math.pi / n)
    cylinder(p, r, -0.004, 0.024, NAVY, n=n, top=STEEL_DECK, phase=math.pi / n)
    cylinder(p, r + 0.002, 0.012, 0.017, srgb(0xC9C2A6), n=n, phase=math.pi / n)  # hazard band
    for i, (x, z, yaw) in enumerate(((-0.04, 0.02, 0.5), (0.045, -0.02, -0.4))):
        cylinder(p, 0.022, 0.024, 0.034, NAVY_DARK, at=(x, 0, z), n=8)
        turret = [(-0.02, 0.034, -0.018), (0.014, 0.034, -0.018), (0.014, 0.034, 0.018), (-0.02, 0.034, 0.018),
                  (-0.016, 0.056, -0.014), (0.008, 0.056, -0.014), (0.008, 0.056, 0.014), (-0.016, 0.056, 0.014),
                  (0.02, 0.046, 0.0)]
        solid(p, turret, NAVY, STEEL, at=(x, 0, z), yaw=yaw)
        for dz in (-0.007, 0.007):
            a, b = place([(0.012, 0.048, dz), (0.06, 0.1, dz)], (x, 0, z), yaw)
            beam(p, a, b, 0.004, NAVY_DARK)
    box(p, -0.01, 0.024, -0.075, 0.03, 0.044, -0.05, NAVY, top=STEEL)  # ammunition locker
    return p


def missile_barge(name):
    """A smaller float with a four-cell missile launcher raised toward the sky."""
    p = Part(name)
    cylinder(p, 0.055, -0.026, -0.004, NAVY_DARK, n=6, r_top=0.064, phase=math.pi / 6)
    cylinder(p, 0.064, -0.004, 0.022, NAVY, n=6, top=STEEL_DECK, phase=math.pi / 6)
    cylinder(p, 0.018, 0.022, 0.044, NAVY_DARK, n=8)
    box(p, -0.014, 0.044, -0.026, 0.014, 0.056, 0.026, NAVY, top=STEEL)
    # a launcher arm raised toward +X carrying two white missiles with red noses
    a, b = np.array((-0.045, 0.05, 0.0)), np.array((0.055, 0.12, 0.0))
    for dz in (-0.016, 0.016):
        beam(p, a + (0, 0, dz), b + (0, 0, dz), 0.006, NAVY_DARK)  # rail
        lift = np.array((-0.004, 0.011, dz))
        tail, body = a + lift + (0.004, 0.003, 0), b + lift - (0.02, 0.014, 0)
        beam(p, tail, body, 0.013, WHITE)
        cone_along(p, body, body + (b - a) * 0.2, 0.0065, RIG_RED)
        for side in (-1, 1):  # tail fins
            beam(p, tail + (0.004, 0.003, 0), tail + (0.014, 0.005, side * 0.014), 0.003, WHITE)
    beam(p, (0.0, 0.052, 0.0), (0.0, 0.08, 0.0), 0.016, NAVY_DARK)  # elevating strut
    return p


def cone_along(part, a, b, r, colour, n=6):
    """A cone with its base disc at a and its tip at b."""
    a, b = np.asarray(a, dtype=float), np.asarray(b, dtype=float)
    axis = (b - a) / np.linalg.norm(b - a)
    side = np.cross(axis, (0, 1, 0) if abs(axis[1]) < 0.9 else (1, 0, 0))
    side /= np.linalg.norm(side)
    up = np.cross(side, axis)
    ring = [a + (side * math.cos(i * math.tau / n) + up * math.sin(i * math.tau / n)) * r for i in range(n)]
    solid(part, ring + [b], colour)


def radar_tower(name):
    """A lattice mast on a float carrying a tilted radar dish."""
    p = Part(name)
    cylinder(p, 0.035, -0.026, -0.004, NAVY_DARK, n=8, r_top=0.042)
    cylinder(p, 0.042, -0.004, 0.02, NAVY, n=8, top=STEEL_DECK)
    for i in range(4):
        a = i * math.tau / 4 + math.pi / 4
        beam(p, (math.cos(a) * 0.025, 0.02, math.sin(a) * 0.025), (math.cos(a) * 0.008, 0.15, math.sin(a) * 0.008),
             0.004, STEEL)
    for y, r in ((0.06, 0.019), (0.105, 0.013)):
        box(p, -r, y, -r, r, y + 0.004, r, STEEL)
    box(p, -0.014, 0.15, -0.014, 0.014, 0.162, 0.014, NAVY_DARK, top=NAVY)
    dish = [(math.cos(i * math.tau / 10) * 0.045, math.sin(i * math.tau / 10) * 0.045, 0.012) for i in range(10)]
    dish += [(0.0, 0.0, 0.0), (0.0, 0.0, 0.004)]
    dish = [(x, y + 0.19, z + 0.006) for x, y, z in tilt_x(dish, -0.5)]
    solid(p, dish, WHITE, PALE)
    beam(p, (0.0, 0.16, 0.0), (0.0, 0.19, 0.006), 0.006, NAVY_DARK)
    beam(p, (0.0, 0.19, 0.012), (0.0, 0.2, 0.05), 0.003, NAVY_DARK)  # feed horn
    return p


def sea_air_defense():
    return [
        at(gun_barge("gun_barge"), -0.06, -0.38),
        at(missile_barge("missiles"), -0.48, 0.2, 0.4),
        at(radar_tower("radar"), 0.48, 0.12),
    ]


# --- aquatic forcefield (future defence) -------------------------------------------------------------------------

PYLON_R = 0.58  # world units
PYLON_H = 0.15  # authored


def pylon(name):
    """A slim white pylon on a float, a teal emitter at its head."""
    p = Part(name)
    cylinder(p, 0.022, -0.026, -0.004, PALE_DARK, n=6, r_top=0.026)
    cylinder(p, 0.026, -0.004, 0.012, WHITE, n=6, top=PALE)
    cylinder(p, 0.008, 0.012, PYLON_H, WHITE, n=6, r_top=0.005)
    cylinder(p, 0.0095, 0.05, 0.056, TEAL, n=6)
    solid(p, [(0.0, PYLON_H - 0.004, 0.0), (0.0, PYLON_H + 0.026, 0.0)] + ring_points(0.01, PYLON_H + 0.01, 4),
          TEAL, TEAL_DARK)
    return p


def generator(name):
    """The central drum: white casing, a glowing teal core and a floating ring above."""
    p = Part(name)
    pontoon(p, 0.065, n=8, strip=False)
    cylinder(p, 0.045, 0.022, 0.075, WHITE, n=8, top=PALE, phase=math.pi / 8)
    cylinder(p, 0.047, 0.05, 0.056, TEAL, n=8, phase=math.pi / 8)
    cylinder(p, 0.026, 0.075, 0.13, TEAL, n=8, r_top=0.014, phase=math.pi / 8)
    for i in range(3):
        a = i * math.tau / 3
        beam(p, (math.cos(a) * 0.046, 0.022, math.sin(a) * 0.046), (math.cos(a) * 0.03, 0.15, math.sin(a) * 0.03),
             0.007, WHITE)
    n = 10
    ring = ring_points(0.04, 0.155, n)
    for a, b in zip(ring, ring[1:] + ring[:1]):
        beam(p, a, b, 0.006, TEAL)
    return p


def shield(name, count=6):
    """Teal arcs between the pylon heads, rising a little at their middles, read as the edge of the field."""
    p = Part(name)
    r = PYLON_R / SCALE
    heads = [(math.cos(math.pi / 6 + i * math.tau / count) * r, PYLON_H + 0.012,
              math.sin(math.pi / 6 + i * math.tau / count) * r) for i in range(count)]
    for a, b in zip(heads, heads[1:] + heads[:1]):
        a, b = np.array(a), np.array(b)
        pts = [a + (b - a) * t + (0, 0.06 * math.sin(t * math.pi), 0) for t in np.linspace(0, 1, 5)]
        for s, e in zip(pts, pts[1:]):
            beam(p, s, e, 0.005, TEAL)
    # a fainter ring just above the water, joining the pylon floats
    for a, b in zip(heads, heads[1:] + heads[:1]):
        beam(p, (a[0], 0.03, a[2]), (b[0], 0.03, b[2]), 0.003, TEAL_DARK)
    return p


def sea_fort():
    parts = []
    for i in range(6):
        a = math.pi / 6 + i * math.tau / 6
        parts.append(at(pylon(f"pylon_{i}"), math.cos(a) * PYLON_R, math.sin(a) * PYLON_R))
    parts.append(at(generator("generator"), 0.0, -0.05))
    parts.append(at(shield("shield"), 0.0, 0.0))
    return parts


# --- submerged town ----------------------------------------------------------------------------------------------

def glass_dome(name, r=0.06, rise=0.03, towers=()):
    """A glass dome whose cap breaks the surface, on a white collar and drum below the water."""
    p = Part(name)
    base = rise - r
    cylinder(p, r * 1.05, -0.09, base + 0.004, PALE, n=10, top=WHITE)
    cylinder(p, r * 1.1, base - 0.004, base + 0.006, TEAL, n=10)
    dome(p, r, base + 0.004, GLASS_SHADE, GLASS, n=10)
    for x, z, h in towers:
        cylinder(p, 0.008, rise * 0.5, h, WHITE, at=(x, 0, z), n=6, r_top=0.005)
        cylinder(p, 0.008, h - 0.03, h - 0.024, TEAL, at=(x, 0, z), n=6)
    return p


def city_tower(name, h=0.2, r=0.016):
    """A slim tower rising out of the sea: white shaft, glass crown, teal bands."""
    p = Part(name)
    cylinder(p, r * 1.6, -0.09, -0.012, PALE_DARK, n=6, r_top=r * 1.2)
    cylinder(p, r * 1.2, -0.012, 0.02, WHITE, n=6, r_top=r)
    cylinder(p, r, 0.02, h, WHITE, n=6, r_top=r * 0.75, top=PALE)
    cylinder(p, r * 1.15, h * 0.55, h * 0.55 + 0.008, TEAL, n=6)
    cylinder(p, r * 0.75, h, h + 0.03, GLASS_SHADE, n=6, r_top=r * 0.45, top=GLASS)
    cylinder(p, 0.002, h + 0.03, h + 0.07, PALE, n=4, r_top=0.0)
    return p


def undersea(name, links):
    """Lower decks and tubes just under the surface that join the domes and towers; they show through the water."""
    p = Part(name)
    for (ax, az), (bx, bz) in links:
        a, b = (ax / SCALE, -0.03, az / SCALE), (bx / SCALE, -0.03, bz / SCALE)
        beam(p, a, b, 0.018, PALE)
        beam(p, (a[0], -0.019, a[2]), (b[0], -0.019, b[2]), 0.005, TEAL)
    return p


def submerged_deck(name, w=0.12, d=0.07):
    """A flat-topped hall whose roof lies just below the surface, with a teal edge."""
    p = Part(name)
    box(p, -w / 2, -0.09, -d / 2, w / 2, -0.016, d / 2, PALE, top=WHITE)
    box(p, -w / 2 - 0.003, -0.022, -d / 2 - 0.003, w / 2 + 0.003, -0.018, d / 2 + 0.003, TEAL)
    for i in range(3):
        x = -w / 2 + (i + 0.5) * w / 3
        box(p, x - 0.012, -0.04, d / 2 - 0.001, x + 0.012, -0.028, d / 2 + 0.003, GLASS_SHADE)
    return p


def submerged_town():
    spots = {
        "dome_a": (-0.36, -0.3), "dome_b": (0.4, -0.22), "dome_c": (-0.42, 0.34),
        "tower_a": (0.02, -0.52), "tower_b": (0.55, 0.22), "deck": (0.18, 0.46),
    }
    links = [(spots["dome_a"], spots["tower_a"]), (spots["tower_a"], spots["dome_b"]),
             (spots["dome_b"], spots["tower_b"]), (spots["tower_b"], spots["deck"]),
             (spots["dome_a"], spots["dome_c"])]
    return [
        at(glass_dome("dome_a", 0.07, 0.035, towers=((0.03, -0.03, 0.12),)), *spots["dome_a"]),
        at(glass_dome("dome_b", 0.055, 0.03), *spots["dome_b"]),
        at(glass_dome("dome_c", 0.045, 0.022), *spots["dome_c"]),
        at(city_tower("tower_a", 0.22), *spots["tower_a"]),
        at(city_tower("tower_b", 0.15, 0.013), *spots["tower_b"]),
        at(submerged_deck("deck"), *spots["deck"], 0.3),
        at(undersea("tubes", links), 0.0, 0.0),
    ]


MODELS = {
    "improvement_improvement_fishing_boats": fishing_boats,
    "improvement_improvement_whaling_boats": whaling_boats,
    "improvement_improvement_offshore_platform": offshore_platform,
    "improvement_improvement_extraction_facility": extraction_facility,
    "improvement_improvement_sea_air_defense": sea_air_defense,
    "improvement_improvement_sea_fort": sea_fort,
    "improvement_improvement_submerged_town": submerged_town,
}
