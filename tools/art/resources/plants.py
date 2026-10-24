"""Plant resources: grain and field crops, fruit trees, plantation crops, and the ruins and digs that share their land.

Conventions are in kit.py. Fields are one draped "ground..." part each; trees and bushes are separate parts, so a
grove follows a hill. Fruit trees stand about 0.3 tall, a little under the forest scatter's trees, with fruit big
enough to read as colour from the default camera.
"""
import math
import random

from mathutils import Matrix, Vector

from .kit import (FOOT, UP as UP_VEC, Band, Outline, Paint, Part, at, blob, box, canopy, cylinder, hexc, hull, leaf,
                  plate, rock, shade, spindle, tube)

SOIL = hexc(0x6B4E33)
SOIL_DARK = hexc(0x553D28)
WHEAT = hexc(0xDDB650)
WHEAT_LIGHT = hexc(0xF2D274)
WHEAT_DEEP = hexc(0xC49A3C)
STALK = hexc(0xA98545)
EAR = hexc(0xEBC75E)
TWINE = hexc(0x7E5E34)
POST = hexc(0x6E4E30)
CLOTH_RED = hexc(0xB8433A)
CLOTH_RED_DARK = hexc(0x963229)
BURLAP = hexc(0xCDB283)
HAT = hexc(0xC9A55A)
HAT_BAND = hexc(0x6A4A2A)


def grain(part, centre, rng, stalks=5, height=0.1, spread=0.026, ear_length=0.036, ear_radius=0.0085,
          stalk_colour=None, ear_paint=None, wind=(0.12, -0.06)):
    """A bunch of grain: stalks fanning out from one root, each with a plump ear nodding in the wind."""
    cx, cy, cz = centre
    phase = rng.random() * math.tau
    for k in range(stalks):
        a = phase + k * math.tau / stalks + rng.uniform(-0.4, 0.4)
        out = spread * rng.uniform(0.55, 1.0)
        h = height * rng.uniform(0.82, 1.08)
        base = Vector((cx + math.cos(a) * 0.005, cy + math.sin(a) * 0.005, cz))
        top = Vector((cx + math.cos(a) * out + wind[0] * h, cy + math.sin(a) * out + wind[1] * h, cz + h))
        tube(part, [base, top], [0.0028, 0.002], stalk_colour or STALK, sides=3, cap=False, phase=rng.random())
        direction = ((top - base).normalized() + Vector((wind[0], wind[1], 0)) * 1.5).normalized()
        a, b = top - direction * 0.006, top + direction * ear_length
        tube(part, [a, a.lerp(b, 0.42), b], [0.0, ear_radius, 0.0], ear_paint, sides=4, phase=rng.random())


def sheaf(part, base, top, r, rng):
    """A bound sheaf of grain from base to top: a bundle of stalks, a twine tie and a bushy head of ears."""
    base, top = Vector(base), Vector(top)
    phase = rng.random()
    tube(part, [base.lerp(top, t) for t in (0.0, 0.56)], [r, r * 0.7], Paint([STALK, WHEAT_DEEP], rng=rng),
         sides=5, phase=phase)
    tube(part, [base.lerp(top, t) for t in (0.56, 0.78, 1.0)], [r * 0.7, r * 1.15, 0.0],
         Paint([WHEAT, EAR, WHEAT_LIGHT], rng=rng), sides=5, phase=phase)
    tube(part, [base.lerp(top, 0.5), base.lerp(top, 0.6)], r * 0.76, TWINE, sides=5, phase=phase)


def stook(name, rng, size=1.0):
    """Five sheaves leaning together into a stook."""
    p = Part(name)
    top = Vector((0.0, 0.0, 0.125 * size))
    for k in range(5):
        a = k * math.tau / 5 + 0.4 + rng.uniform(-0.2, 0.2)
        foot = Vector((math.cos(a) * 0.03 * size, math.sin(a) * 0.03 * size, FOOT / 2))
        head = top + Vector((math.cos(a) * 0.008, math.sin(a) * 0.008, rng.uniform(-0.01, 0.004)))
        sheaf(p, foot, head, 0.013 * size, rng)
    return p


def scarecrow(name, rng):
    """A scarecrow in a red coat and a straw hat: the field's landmark."""
    p = Part(name)
    tube(p, [(0, 0, FOOT), (0, 0, 0.2)], 0.0065, POST, sides=5)
    tube(p, [(-0.066, 0, 0.152), (0.066, 0, 0.152)], 0.005, POST, sides=5)
    coat = Paint([CLOTH_RED, CLOTH_RED_DARK], rng=rng)
    hull(p, [(-0.026, -0.016, 0.16), (0.026, -0.016, 0.16), (-0.026, 0.016, 0.16), (0.026, 0.016, 0.16),
             (-0.034, -0.02, 0.085), (0.034, -0.02, 0.085), (-0.034, 0.02, 0.085), (0.034, 0.02, 0.085)], coat)
    for sx in (-1, 1):
        tube(p, [(sx * 0.02, 0, 0.152), (sx * 0.062, 0, 0.148)], [0.012, 0.010], coat, sides=6)
        cylinder(p, (sx * 0.06, 0, 0.148), (sx * 0.078, 0, 0.138), 0.009, 0.0, EAR, sides=5)
    cylinder(p, (0, 0, 0.088), (0, 0, 0.072), 0.03, 0.0, EAR, sides=6)  # straw hanging from the hem
    blob(p, (0, 0, 0.183), (0.021, 0.021, 0.023), BURLAP, subdivisions=1, wobble=0.08, seed=rng.random())
    cylinder(p, (0, 0, 0.199), (0, 0, 0.204), 0.04, 0.038, HAT, sides=10)
    cylinder(p, (0, 0, 0.203), (0, 0, 0.212), 0.021, 0.02, HAT_BAND, sides=9)
    cylinder(p, (0, 0, 0.212), (0, 0, 0.236), 0.02, 0.009, HAT, sides=9)
    return p


def wheat():
    """A golden field rippling in the wind, ears standing out of it, a scarecrow and two stooks at harvest."""
    rng = random.Random(115)
    field = Part("ground_field")
    edge = Outline(0.225, 0.175, rng, centre=(-0.01, 0.035), sides=28, ragged=0.08, square=3.5, turn=0.24)
    plate(field, edge.scaled(1.04, 0.004), SOIL, SOIL_DARK)
    canopy(field, edge.scaled(0.97), 0.03, Paint([WHEAT_DEEP, STALK, WHEAT_DEEP, WHEAT], rng=rng), STALK, rng,
           spacing=0.04, edge=0.5, lumps=0.25)
    ears = Paint([EAR, WHEAT_LIGHT, WHEAT, EAR], rng=rng)
    # Bunches sown in rows across the plot, the rows a little skew to the camera.
    for row in range(5):
        v = -0.13 + row * 0.064
        for col in range(6):
            u = -0.17 + col * 0.066 + (0.033 if row % 2 else 0.0) + rng.uniform(-0.01, 0.01)
            x = edge.centre.x + u * 0.97 - v * 0.24
            y = edge.centre.y + v + u * 0.24 + rng.uniform(-0.008, 0.008)
            if edge.fraction(x, y) > 0.88:
                continue
            grain(field, (x, y, 0.0), rng, stalks=rng.choice((5, 6, 6, 7)), height=rng.uniform(0.08, 0.105),
                  spread=0.03, ear_length=0.042, ear_radius=0.0105, ear_paint=ears)
    return [
        at(field, 0.0, 0.0),
        at(scarecrow("scarecrow", rng), -0.07, 0.09, 0.3),
        at(stook("stook", rng), 0.19, -0.16, 0.3),
    ]


# --- fruit trees ---------------------------------------------------------------------------------------------

LEAF = hexc(0x3F7F2C)
LEAF_DARK = hexc(0x2F6524)
LEAF_LIGHT = hexc(0x5E9C38)
BARK = hexc(0x6A4A30)
BARK_DARK = hexc(0x523822)
APPLE = hexc(0xD0362A)
APPLE_LIGHT = hexc(0xE85A3A)
CRATE = hexc(0xA67C4E)
CRATE_DARK = hexc(0x7E5A36)


def fruit(part, centre, r, colour, rng, stretch=1.0):
    """One round fruit: a small faceted ball, stretched along a random level direction for a lemon or olive."""
    mark = part.mark()
    blob(part, (0, 0, 0), (r * stretch, r, r * 0.92), colour, subdivisions=0 if stretch == 1.0 else 1, wobble=0.0)
    part.transformed(Matrix.Translation(centre) @ Matrix.Rotation(rng.random() * math.tau, 4, "Z"), mark)


def crown(part, centre, radius, leaves: Paint, rng, lobes=4, squash=0.85, wobble=0.1, subdivisions=2):
    """A tree's crown: a big lumpy ball with smaller lobes round it. Returns its Surface."""
    cx, cy, cz = centre
    lumps = [((cx, cy, cz), radius)]
    phase = rng.random() * math.tau
    for k in range(lobes):
        a = phase + k * math.tau / lobes + rng.uniform(-0.4, 0.4)
        r = radius * rng.uniform(0.55, 0.7)
        d = radius * 0.66
        lumps.append(((cx + math.cos(a) * d, cy + math.sin(a) * d, cz - radius * rng.uniform(0.05, 0.3)), r))
    mark = part.mark()
    for (x, y, z), r in lumps:
        blob(part, (x, y, z), (r, r, r * squash), leaves, subdivisions=subdivisions, wobble=wobble,
             seed=rng.random() * 9)
    return part.surface(mark, centre)


def directions(rng, count, low=-0.3, high=0.95, gap=0.45, tries=400):
    """Up to count unit directions between heights low and high, at least gap (radians) apart."""
    out = []
    for _ in range(tries):
        if len(out) >= count:
            break
        z = rng.uniform(low, high)
        a = rng.random() * math.tau
        d = Vector((math.cos(a) * math.sqrt(1 - z * z), math.sin(a) * math.sqrt(1 - z * z), z))
        if all(d.angle(e) >= gap for e in out):
            out.append(d)
    return out


def dot_fruit(part, surface, count, r, colours, rng, sink=0.3, low=-0.3, stretch=1.0, gap=0.45):
    """Fruit set into a crown's surface, spread evenly over its sides and top."""
    for i, d in enumerate(directions(rng, count, low=low, gap=gap)):
        point, normal = surface.at(d)
        s = r * rng.uniform(0.85, 1.1)
        fruit(part, point + normal * s * (1 - sink) - Vector((0, 0, s * 0.2)), s, colours[i % len(colours)], rng,
              stretch)


def trunk(part, height, r, bark, rng, lean=(0.0, 0.0), forks=2, fork_length=0.05):
    """A tapering, slightly crooked trunk forking into branches at height. Returns the branch tips."""
    lx, ly = lean
    pts = [Vector((0, 0, FOOT)), Vector((0, 0, 0.0)), Vector((lx * 0.4 + rng.uniform(-0.004, 0.004),
                                                              ly * 0.4 + rng.uniform(-0.004, 0.004), height * 0.5)),
           Vector((lx, ly, height))]
    tube(part, pts, [r * 1.15, r * 1.1, r * 0.9, r * 0.75], bark, sides=6, phase=rng.random())
    tips = []
    phase = rng.random() * math.tau
    for k in range(forks):
        a = phase + k * math.tau / forks + rng.uniform(-0.3, 0.3)
        tip = pts[-1] + Vector((math.cos(a) * fork_length * 0.7, math.sin(a) * fork_length * 0.7, fork_length))
        tube(part, [pts[-1] - Vector((0, 0, 0.01)), tip], [r * 0.7, r * 0.45], bark, sides=5, phase=rng.random())
        tips.append(tip)
    return tips


def fruit_tree(name, rng, leaves, fruit_colours, size=1.0, fruits=14, fruit_r=0.0135, bark=BARK, stretch=1.0,
               squash=0.85, radius=0.085):
    """A round fruit tree about 0.3 tall with fruit dotted over its crown."""
    p = Part(name)
    trunk(p, 0.11 * size, 0.014 * size, bark, rng, lean=(rng.uniform(-0.01, 0.01), rng.uniform(-0.01, 0.01)))
    surface = crown(p, (0.0, 0.0, 0.215 * size), radius * size, leaves, rng, squash=squash)
    dot_fruit(p, surface, fruits, fruit_r * size, fruit_colours, rng, stretch=stretch)
    return p


def crate(name, rng, fruit_colours, w=0.072, d=0.05, h=0.036):
    """A slatted wooden crate heaped with fruit."""
    p = Part(name)
    box(p, (-w / 2, -d / 2, FOOT / 2), (w / 2, d / 2, h), Paint([CRATE, CRATE_DARK], top=CRATE_DARK, rng=rng))
    for z in (h * 0.35, h * 0.75):  # slats
        box(p, (-w / 2 - 0.002, -d / 2 - 0.002, z - 0.003), (w / 2 + 0.002, d / 2 + 0.002, z + 0.003), CRATE)
    for i in range(7):
        x, y = rng.uniform(-w / 2 + 0.01, w / 2 - 0.01), rng.uniform(-d / 2 + 0.009, d / 2 - 0.009)
        fruit(p, (x, y, h + 0.004 + 0.006 * (i % 2)), 0.0125, fruit_colours[i % len(fruit_colours)], rng)
    return p


def windfalls(name, rng, colours, count=6, spread=0.05, r=0.0095, stretch=1.0):
    """Fruit lying in the grass."""
    p = Part(name)
    for i in range(count):
        a, d = rng.random() * math.tau, spread * math.sqrt(rng.random())
        fruit(p, (math.cos(a) * d, math.sin(a) * d, r * 0.6), r, colours[i % len(colours)], rng, stretch)
    return p


WICKER = hexc(0xB08850)
WICKER_DARK = hexc(0x8A6638)


def vessel(part, points, radii, wall, paint, sides=12, phase=0.0):
    """An open container (a tub, a pot) through points with outer radii: its wall wall thick, open on top so what
    is inside shows, seen from inside and out."""
    tube(part, points, radii, paint, sides=sides, phase=phase, cap=False)
    inner = [Vector(p) for p in points[1:]]
    inner_radii = [r - wall for r in radii[1:]]
    bottom = Vector(points[0]) + Vector((0, 0, wall))
    # The inside: a reversed tube (its faces look inward) from the rim down to a floor.
    mark = len(part.faces)
    tube(part, [bottom] + inner, [radii[0] - wall] + inner_radii, paint, sides=sides, phase=phase, cap=False)
    part.faces[mark:] = [tuple(reversed(f)) for f in part.faces[mark:]]
    top = Vector(points[-1])
    rim = [top + Vector((math.cos(phase + k * math.tau / sides), math.sin(phase + k * math.tau / sides), 0)) * r
           for r in (radii[-1], radii[-1] - wall) for k in range(sides)]
    part.add(rim, [(k, (k + 1) % sides, sides + (k + 1) % sides, sides + k) for k in range(sides)], paint)
    cylinder(part, Vector(points[0]) - Vector((0, 0, 0.001)), bottom, radii[0] - wall, radii[0] - wall, paint,
             sides=sides, phase=phase)


def basket(part, centre, r, h, rng, wicker=None):
    """A round wicker basket, a little wider at the rim, open at the top. Returns the rim's centre."""
    x, y, z = centre
    wicker = wicker or Paint([WICKER, WICKER_DARK], rng=rng)
    tube(part, [(x, y, z - 0.004), (x, y, z + h * 0.5), (x, y, z + h)], [r * 0.8, r * 0.95, r], wicker, sides=10,
         phase=rng.random())
    tube(part, [(x, y, z + h - 0.002), (x, y, z + h + 0.003)], r * 1.06, WICKER_DARK, sides=10)
    return Vector((x, y, z + h))


def heap(part, top, r, colours, rng, count=7, size=0.011, stretch=1.0):
    """Fruit heaped in a basket or crate whose rim centre is top."""
    for i in range(count):
        a, d = (i * 2.4 + rng.random()), r * 0.62 * math.sqrt((i + 0.5) / count)
        z = top.z + size * (0.35 + 0.6 * (1 - d / (r * 0.62)))
        fruit(part, (top.x + math.cos(a) * d, top.y + math.sin(a) * d, z), size * rng.uniform(0.9, 1.1),
              colours[i % len(colours)], rng, stretch)


def fruit_basket(name, rng, colours, r=0.03, h=0.03, size=0.011, stretch=1.0):
    p = Part(name)
    top = basket(p, (0, 0, 0), r, h, rng)
    blob(p, top - Vector((0, 0, 0.004)), (r * 0.9, r * 0.9, 0.006), WICKER_DARK, subdivisions=1, wobble=0.0)
    heap(p, top, r, colours, rng, size=size, stretch=stretch)
    return p


def apple():
    """Three apple trees heavy with red fruit, windfalls and a full crate."""
    rng = random.Random(101)
    leaves = Paint([LEAF, LEAF_DARK, LEAF, LEAF_LIGHT], top=LEAF_LIGHT, top_cut=0.8, rng=rng)
    apples = [APPLE, APPLE_LIGHT, APPLE]
    return [
        at(fruit_tree("tree_a", rng, leaves, apples, size=1.1), -0.11, 0.08),
        at(fruit_tree("tree_b", rng, leaves, apples, size=0.95), 0.1, 0.1),
        at(fruit_tree("tree_c", rng, leaves, apples, size=0.9), -0.04, -0.11),
        at(windfalls("windfalls", rng, apples), 0.06, -0.04),
        at(crate("crate", rng, apples), 0.16, -0.12, 0.35),
    ]


LEMON = hexc(0xF4D23A)
LEMON_LIGHT = hexc(0xFBE564)
CITRUS = hexc(0x2E6A2C)
CITRUS_DARK = hexc(0x235423)
CITRUS_LIGHT = hexc(0x45863A)


def lemon():
    """Three dark, glossy lemon trees thick with yellow fruit, and a basket of lemons."""
    rng = random.Random(102)
    leaves = Paint([CITRUS, CITRUS_DARK, CITRUS, CITRUS_LIGHT], top=CITRUS_LIGHT, top_cut=0.8, rng=rng)
    lemons = [LEMON, LEMON_LIGHT, LEMON]
    tree = dict(fruits=18, fruit_r=0.0125, stretch=1.3, squash=0.95, radius=0.078)
    return [
        at(fruit_tree("tree_a", rng, leaves, lemons, size=1.05, **tree), -0.1, 0.09),
        at(fruit_tree("tree_b", rng, leaves, lemons, size=0.95, **tree), 0.11, 0.08),
        at(fruit_tree("tree_c", rng, leaves, lemons, size=0.9, **tree), 0.0, -0.1),
        at(fruit_basket("basket", rng, lemons, stretch=1.3), -0.15, -0.12),
        at(windfalls("windfalls", rng, lemons, count=4, stretch=1.3), 0.14, -0.1),
    ]


# --- olives ----------------------------------------------------------------------------------------------------

OLIVE_LEAF = hexc(0x6A8650)
OLIVE_LEAF_DARK = hexc(0x506B3C)
OLIVE_LEAF_LIGHT = hexc(0x8FA76E)
OLIVE_BARK = hexc(0x6E6354)
OLIVE_BARK_DARK = hexc(0x564C40)
OLIVE_BLACK = hexc(0x3C2C40)
OLIVE_GREEN = hexc(0x8C9A3A)
TERRACOTTA = hexc(0xB8653C)
TERRACOTTA_DARK = hexc(0x94502E)


def olive_tree(name, rng, size=1.0):
    """A gnarled olive: a squat, twisting trunk splitting into three limbs, each under a flat clump of silvery
    leaves dotted with olives."""
    p = Part(name)
    bark = Paint([OLIVE_BARK, OLIVE_BARK_DARK], rng=rng)
    leaves = Paint([OLIVE_LEAF, OLIVE_LEAF_DARK, OLIVE_LEAF, OLIVE_LEAF_LIGHT], top=OLIVE_LEAF_LIGHT, top_cut=0.75,
                   rng=rng)
    s = size
    fork = Vector((rng.uniform(-0.01, 0.01), rng.uniform(-0.01, 0.01), 0.07 * s))
    tube(p, [(0, 0, FOOT), (0, 0, 0), (0.012 * s, -0.006 * s, 0.035 * s), fork], [0.03 * s, 0.026 * s, 0.02 * s,
                                                                                  0.018 * s], bark, sides=7,
         phase=rng.random())
    phase = rng.random() * math.tau
    for k in range(3):
        a = phase + k * math.tau / 3 + rng.uniform(-0.3, 0.3)
        out = Vector((math.cos(a), math.sin(a), 0))
        elbow = fork + out * 0.03 * s + Vector((0, 0, 0.035 * s))
        tip = fork + out * 0.065 * s + Vector((0, 0, rng.uniform(0.08, 0.1) * s))
        tube(p, [fork - Vector((0, 0, 0.01 * s)), elbow, tip], [0.012 * s, 0.009 * s, 0.006 * s], bark, sides=6,
             phase=rng.random())
        r = rng.uniform(0.042, 0.05) * s
        mark = p.mark()
        centre = tip + Vector((0, 0, 0.012 * s))
        blob(p, centre, (r, r * 0.9, r * 0.55), leaves, subdivisions=2, wobble=0.2, seed=rng.random() * 9)
        side = Vector((-out.y, out.x, 0)) * rng.choice((-1, 1))
        blob(p, centre + (out * 0.5 + side) * r * 0.75 + Vector((0, 0, -0.008 * s)), (r * 0.7, r * 0.65, r * 0.45),
             leaves, subdivisions=2, wobble=0.2, seed=rng.random() * 9)
        surface = p.surface(mark, centre)
        dot_fruit(p, surface, 4, 0.0068 * s, [OLIVE_BLACK, OLIVE_GREEN, OLIVE_BLACK], rng, low=-0.6, stretch=1.25,
                  gap=0.7)
    return p


def amphora(part, base, h, rng, lean=0.0):
    """A terracotta jar with a narrow neck and two handles."""
    clay = Paint([TERRACOTTA, TERRACOTTA_DARK], rng=rng)
    x, y, z = base
    turn = Matrix.Translation((x, y, z)) @ Matrix.Rotation(lean, 4, "Y")
    mark = part.mark()
    tube(part, [(0, 0, -0.004), (0, 0, h * 0.12), (0, 0, h * 0.45), (0, 0, h * 0.78), (0, 0, h * 0.86),
                (0, 0, h)], [h * 0.12, h * 0.26, h * 0.3, h * 0.16, h * 0.1, h * 0.13], clay, sides=10)
    for side in (-1, 1):
        tube(part, [(side * h * 0.1, 0, h * 0.9), (side * h * 0.24, 0, h * 0.86), (side * h * 0.2, 0, h * 0.68)],
             h * 0.03, TERRACOTTA_DARK, sides=4)
    part.transformed(turn, mark)


def jars(name, rng):
    p = Part(name)
    amphora(p, (0, 0, 0), 0.075, rng)
    amphora(p, (0.04, 0.02, 0), 0.06, rng, lean=0.1)
    return p


def olives():
    """Three gnarled, silvery olive trees and two amphorae of oil."""
    rng = random.Random(103)
    return [
        at(olive_tree("tree_a", rng, size=1.1), -0.11, 0.08),
        at(olive_tree("tree_b", rng, size=0.95), 0.12, 0.07),
        at(olive_tree("tree_c", rng, size=0.9), 0.0, -0.12),
        at(jars("jars", rng), -0.17, -0.1, 0.3),
    ]


# --- bananas ---------------------------------------------------------------------------------------------------

BANANA_LEAF = hexc(0x5DA53A)
BANANA_LEAF_DARK = hexc(0x468A2E)
BANANA_LEAF_LIGHT = hexc(0x80C250)
BANANA_STEM = hexc(0x7E9A44)
BANANA_STEM_DARK = hexc(0x66803A)
BANANA = hexc(0xF2D33E)
BANANA_GREEN = hexc(0xB9C843)
BANANA_BUD = hexc(0x7C2D4C)


def hand(part, centre, r, colour, rng, count=7, length=0.026):
    """A ring of bananas round a point, curving up and out."""
    c = Vector(centre)
    phase = rng.random() * math.tau
    for k in range(count):
        a = phase + k * math.tau / count
        out = Vector((math.cos(a), math.sin(a), 0))
        root = c + out * r * 0.4
        mid = c + out * (r + length * 0.4) + Vector((0, 0, length * 0.25))
        tip = c + out * (r + length * 0.55) + Vector((0, 0, length * 0.75))
        tube(part, [root, mid, tip], [0.0045, 0.0055, 0.0], colour, sides=5)


def bunch(part, top, rng, colour, hands=3):
    """A bunch hanging from top: a stalk, hands of fruit stacked down it and a purple bud below."""
    top = Vector(top)
    bottom = top - Vector((0, 0, 0.065))
    tube(part, [top, top.lerp(bottom, 0.5), bottom], [0.004, 0.0045, 0.004], BANANA_STEM_DARK, sides=5)
    for i in range(hands):
        t = (i + 0.5) / hands
        hand(part, top.lerp(bottom, t * 0.8 + 0.05), 0.012 * (1 - 0.25 * t), colour, rng, count=8 - i)
    cylinder(part, bottom, bottom - Vector((0, 0, 0.028)), 0.009, 0.0, BANANA_BUD, sides=6)


def banana_plant(name, rng, size=1.0, ripe=True):
    """A banana plant: a green false stem crowned with long, arching paddle leaves, and a bunch hanging from it."""
    p = Part(name)
    s = size
    top = Vector((rng.uniform(-0.006, 0.006), rng.uniform(-0.006, 0.006), 0.15 * s))
    tube(p, [(0, 0, FOOT), (0, 0, 0), top * 0.5, top], [0.02 * s, 0.019 * s, 0.015 * s, 0.012 * s],
         Paint([BANANA_STEM, BANANA_STEM_DARK], rng=rng), sides=7, phase=rng.random())
    leaves = Paint([BANANA_LEAF, BANANA_LEAF_DARK, BANANA_LEAF, BANANA_LEAF_LIGHT], top=BANANA_LEAF_LIGHT,
                   top_cut=0.85, rng=rng)
    # Leaves all round but the front (-Y), where the bunch hangs in view.
    count = 6
    for k in range(count):
        a = -math.pi / 2 + (k + 0.5) * math.tau / count + rng.uniform(-0.2, 0.2)
        leaf(p, top + Vector((0, 0, rng.uniform(-0.01, 0.01))), a, rng.uniform(0.12, 0.15) * s, 0.05 * s, leaves,
             lift=rng.uniform(1.0, 1.3), droop=rng.uniform(1.1, 1.5), fold=0.12, segments=6, shape="broad")
    # A young leaf still furled, standing straight up.
    cylinder(p, top, top + Vector((0.004, 0, 0.06 * s)), 0.006 * s, 0.0, BANANA_LEAF_LIGHT, sides=5)
    stalk_top = top + Vector((0.0, -0.022, -0.008))
    bunch(p, stalk_top, rng, BANANA if ripe else BANANA_GREEN)
    return p


def comb(part, base, rng, count=6, length=0.042, colour=BANANA):
    """A hand of bananas lying on the ground: fingers side by side from one crown, curving up at their tips."""
    bx, by, bz = base
    for k in range(count):
        y = by + (k - (count - 1) / 2) * 0.0105
        spread = (k - (count - 1) / 2) * 0.06
        d = Vector((math.cos(spread), math.sin(spread), 0))
        root = Vector((bx, y, bz + 0.006))
        tube(part, [root, root + d * length * 0.5 + Vector((0, 0, 0.002)), root + d * length + Vector((0, 0, 0.012))],
             [0.0052, 0.0062, 0.0], colour, sides=5, phase=rng.random())
    tube(part, [(bx - 0.012, by, bz + 0.008), (bx + 0.002, by, bz + 0.006)], 0.006, BANANA_STEM_DARK, sides=5)


def banana():
    """Three banana plants under arching leaves, two hung with ripe bunches, and a cut bunch on the ground."""
    rng = random.Random(104)
    cut = Part("cut_bunch")
    comb(cut, (0, 0, 0), rng)
    comb(cut, (0.012, 0.03, 0.0), rng, count=5)
    return [
        at(banana_plant("plant_a", rng, size=1.1), -0.1, 0.08),
        at(banana_plant("plant_b", rng, size=0.95, ripe=False), 0.12, 0.09),
        at(banana_plant("plant_c", rng, size=0.9), 0.02, -0.1),
        at(cut, -0.16, -0.12, 0.4),
    ]


# --- rubber ----------------------------------------------------------------------------------------------------

RUBBER_BARK = hexc(0x8C8270)
RUBBER_BARK_DARK = hexc(0x6E6556)
RUBBER_LEAF = hexc(0x3E7A30)
RUBBER_LEAF_DARK = hexc(0x2E6326)
RUBBER_LEAF_LIGHT = hexc(0x5A9640)
LATEX = hexc(0xF2EEE2)
CUP = hexc(0x8A5A3A)


def rubber_tree(name, rng, size=1.0):
    """A tall, straight rubber tree: a pale trunk scored with a spiral tapping cut that bleeds white latex into a
    cup, under a high, narrow crown."""
    p = Part(name)
    s = size
    h = 0.2 * s
    trunk_pts = [Vector((0, 0, FOOT))] + [Vector((rng.uniform(-0.002, 0.002), rng.uniform(-0.002, 0.002), h * t))
                                          for t in (0.0, 0.1, 0.2, 0.3, 0.4, 0.55, 0.7, 0.85, 1.0)]
    trunk_pts[1] = Vector((0, 0, 0))
    radii = [0.017 * s, 0.017 * s] + [0.015 * s * (1 - 0.35 * t) for t in (0.1, 0.2, 0.3, 0.4, 0.55, 0.7, 0.85, 1.0)]
    # The tapping cut: a band slanting round the trunk's front, a little above the cup.
    cut = Band(Paint([RUBBER_BARK, RUBBER_BARK_DARK], rng=rng), LATEX, (0, 0, 0.07 * s), (0.45, 0.0, 1.0), 0.006)
    tube(p, trunk_pts, radii, cut, sides=8, phase=0.06)
    cylinder(p, (0, -0.022 * s, 0.032 * s), (0, -0.022 * s, 0.05 * s), 0.01 * s, 0.013 * s, CUP, sides=8)
    cylinder(p, (0, -0.022 * s, 0.049 * s), (0, -0.022 * s, 0.0505 * s), 0.0118 * s, 0.0118 * s, LATEX, sides=8)
    leaves = Paint([RUBBER_LEAF, RUBBER_LEAF_DARK, RUBBER_LEAF, RUBBER_LEAF_LIGHT], top=RUBBER_LEAF_LIGHT,
                   top_cut=0.8, rng=rng)
    crown(p, (0.0, 0.0, h + 0.035 * s), 0.065 * s, leaves, rng, lobes=4, squash=1.15)
    return p


def latex_pail(name, rng):
    p = Part(name)
    cylinder(p, (0, 0, -0.004), (0, 0, 0.03), 0.018, 0.02, Paint([CUP, shade(CUP, 0.85)], rng=rng), sides=10)
    cylinder(p, (0, 0, 0.027), (0, 0, 0.0285), 0.019, 0.019, LATEX, sides=10)
    return p


def rubber():
    """A row of tall rubber trees, each tapped and dripping white latex into a cup, and a pail of latex."""
    rng = random.Random(105)
    return [
        at(rubber_tree("tree_a", rng, size=1.1), -0.12, 0.1),
        at(rubber_tree("tree_b", rng, size=1.0), 0.02, 0.04),
        at(rubber_tree("tree_c", rng, size=0.92), 0.15, -0.02),
        at(latex_pail("pail", rng), -0.05, -0.1),
    ]


# --- silk ------------------------------------------------------------------------------------------------------

MULBERRY = hexc(0x4C8C36)
MULBERRY_DARK = hexc(0x3A7429)
MULBERRY_LIGHT = hexc(0x6AA848)
COCOON = hexc(0xF4F0E0)
COCOON_SHADE = hexc(0xE2DCC6)
SILK_RED = hexc(0xC4283C)
SILK_GOLD = hexc(0xE8B42C)
SILK_BLUE = hexc(0x2A8EB8)


def skein(part, top, length, colour, rng):
    """A hank of silk hanging from a rail: a flattened loop, twisted at the bottom."""
    top = Vector(top)
    tube(part, [top, top - Vector((0, 0, length * 0.55)), top - Vector((0.002, 0, length))],
         [0.005, 0.0047, 0.0025], colour, sides=6, squash=2.6, phase=0.5)  # wide across the rail (X)


def silk_rack(name, rng):
    """A drying rail hung with hanks of dyed silk."""
    p = Part(name)
    for x in (-0.075, 0.075):
        tube(p, [(x, 0, FOOT), (x, 0, 0.13)], 0.005, POST, sides=5)
    tube(p, [(-0.085, 0, 0.125), (0.085, 0, 0.125)], 0.0045, POST, sides=5)
    for i, colour in enumerate([SILK_RED, SILK_GOLD, SILK_BLUE, SILK_RED, SILK_GOLD]):
        skein(p, (-0.056 + i * 0.028, 0, 0.122), rng.uniform(0.07, 0.085), colour, rng)
    return p


def mulberry(name, rng, size=1.0):
    """A mulberry tree with white silk cocoons spun among its leaves."""
    leaves = Paint([MULBERRY, MULBERRY_DARK, MULBERRY, MULBERRY_LIGHT], top=MULBERRY_LIGHT, top_cut=0.8, rng=rng)
    return fruit_tree(name, rng, leaves, [COCOON, COCOON_SHADE], size=size, fruits=12, fruit_r=0.011, stretch=1.5,
                      squash=0.8)


def silk():
    """Two mulberry trees spun with white cocoons, a basket of cocoons and a rail of dyed silk."""
    rng = random.Random(106)
    return [
        at(mulberry("tree_a", rng, size=1.05), -0.1, 0.09),
        at(mulberry("tree_b", rng, size=0.9), 0.12, 0.1),
        at(silk_rack("rack", rng), 0.02, -0.08, -0.25),
        at(fruit_basket("basket", rng, [COCOON, COCOON_SHADE], size=0.01, stretch=1.5), -0.16, -0.12),
    ]


# --- shrub crops ----------------------------------------------------------------------------------------------

BURLAP_DARK = hexc(0xAE9468)


def bush(part, centre, radii, paint, rng, wobble=0.14, subdivisions=2) -> "Surface":
    """A rounded shrub sitting on the ground at centre, its base sunk below the ground. Returns its Surface."""
    x, y, z = centre
    rx, ry, rz = radii
    mark = part.mark()
    blob(part, (x, y, z + rz * 0.8), (rx, ry, rz), paint, subdivisions=subdivisions, wobble=wobble,
         seed=rng.random() * 9)
    return part.surface(mark, (x, y, z + rz * 0.8))


def sack(part, base, r, h, rng, fill=None, fill_paint=None):
    """An open burlap sack, its mouth rolled down, heaped with fill_paint (beans, cotton, spice)."""
    x, y, z = base
    cloth = Paint([BURLAP, BURLAP_DARK], rng=rng)
    tube(part, [(x, y, z - 0.004), (x, y, z + h * 0.35), (x, y, z + h * 0.8), (x, y, z + h)],
         [r * 0.9, r, r * 0.92, r * 0.86], cloth, sides=9, phase=rng.random())
    tube(part, [(x, y, z + h * 0.88), (x, y, z + h * 1.02)], r * 0.97, BURLAP_DARK, sides=9)
    if fill_paint is not None:
        blob(part, (x, y, z + h * 0.98), (r * 0.84, r * 0.84, r * 0.42), fill_paint, subdivisions=fill or 1,
             wobble=0.12, seed=rng.random())


COFFEE_LEAF = hexc(0x2D5C2A)
COFFEE_LEAF_DARK = hexc(0x224A20)
COFFEE_LEAF_LIGHT = hexc(0x427A36)
CHERRY = hexc(0xC42A26)
CHERRY_DARK = hexc(0x9A1E1E)
CHERRY_UNRIPE = hexc(0xE0962C)
BEAN = hexc(0x5A3820)
BEAN_DARK = hexc(0x45291A)


def coffee_bush(name, rng, size=1.0):
    p = Part(name)
    leaves = Paint([COFFEE_LEAF, COFFEE_LEAF_DARK, COFFEE_LEAF, COFFEE_LEAF_LIGHT], top=COFFEE_LEAF_LIGHT,
                   top_cut=0.8, rng=rng)
    tube(p, [(0, 0, FOOT), (0, 0, 0.03 * size)], 0.008 * size, BARK, sides=5)
    surface = bush(p, (0, 0, 0.012 * size), (0.048 * size, 0.046 * size, 0.06 * size), leaves, rng)
    dot_fruit(p, surface, 16, 0.0068 * size, [CHERRY, CHERRY_DARK, CHERRY, CHERRY_UNRIPE], rng, low=-0.5, gap=0.38,
              sink=0.2)
    return p


def coffee():
    """Two rows of glossy coffee bushes hung with red cherries, and an open sack of roasted beans."""
    rng = random.Random(107)
    beans = Paint([BEAN, BEAN_DARK], rng=rng)
    parts = []
    for i, (x, y, s) in enumerate([(-0.15, 0.11, 1.0), (-0.03, 0.13, 1.05), (0.09, 0.15, 0.95),
                                   (-0.1, -0.01, 1.0), (0.02, 0.01, 1.1), (0.14, 0.03, 0.95)]):
        parts.append(at(coffee_bush(f"bush_{i}", rng, s), x, y))
    sack_part = Part("sack")
    sack(sack_part, (0, 0, 0), 0.03, 0.055, rng, fill_paint=beans, fill=2)
    return parts + [at(sack_part, 0.1, -0.13)]


TEA = hexc(0x5AA63A)
TEA_DARK = hexc(0x46892E)
TEA_LIGHT = hexc(0x86C654)


def hedge(name, rng, length, height=0.085, width=0.07, bend=0.02):
    """A clipped row of tea bushes: overlapping rounded clumps along a gentle curve, flat on top."""
    p = Part(name)
    leaves = Paint([TEA, TEA_DARK, TEA], top=TEA_LIGHT, top_cut=0.7, rng=rng)
    count = max(3, int(length / (width * 0.55)))
    for i in range(count):
        t = i / (count - 1) - 0.5
        x, y = t * length, bend * (1 - (2 * t) ** 2)
        r = width * rng.uniform(0.62, 0.72)
        blob(p, (x, y, height * 0.5), (r, width * 0.55, height * 0.62), leaves, subdivisions=2, wobble=0.1,
             seed=rng.random() * 9, cut=0.45)
    return p


def picking_basket(name, rng):
    """A tall picker's basket, wider at the mouth, full of fresh leaves."""
    p = Part(name)
    top = basket(p, (0, 0, 0), 0.026, 0.05, rng)
    blob(p, top, (0.024, 0.024, 0.012), Paint([TEA_LIGHT, TEA]), subdivisions=1, wobble=0.25)
    return p


def tea():
    """Three clipped rows of bright tea bushes following the slope, and a picker's basket of leaves."""
    rng = random.Random(108)
    return [
        at(hedge("row_a", rng, 0.36), 0.0, 0.15, 0.1),
        at(hedge("row_b", rng, 0.42), -0.01, 0.03, 0.1),
        at(hedge("row_c", rng, 0.3), -0.05, -0.09, 0.1),
        at(picking_basket("basket", rng), 0.17, -0.12),
    ]


COTTON = hexc(0xF6F4EE)
COTTON_SHADE = hexc(0xE4E0D6)
COTTON_LEAF = hexc(0x5E7E3C)
COTTON_LEAF_DARK = hexc(0x4A6630)
COTTON_STEM = hexc(0x7A5A3A)


def boll(part, centre, r, rng):
    """An open cotton boll: three or four fluffy white lobes on a brown husk."""
    c = Vector(centre)
    cylinder(part, c - Vector((0, 0, r * 0.9)), c - Vector((0, 0, r * 0.2)), r * 0.3, r * 0.8, COTTON_STEM, sides=5)
    blob(part, c, (r, r, r * 0.8), Paint([COTTON, COTTON_SHADE, COTTON]), subdivisions=1, wobble=0.35,
         frequency=3.0, seed=rng.random() * 9)


def cotton_plant(name, rng, size=1.0):
    """A low, branching cotton plant with a few leaves and many white bolls."""
    p = Part(name)
    s = size
    tube(p, [(0, 0, FOOT), (0, 0, 0.05 * s)], 0.004 * s, COTTON_STEM, sides=5)
    leaves = Paint([COTTON_LEAF, COTTON_LEAF_DARK], rng=rng)
    bush(p, (0, 0, 0.012 * s), (0.034 * s, 0.034 * s, 0.03 * s), leaves, rng, wobble=0.3, subdivisions=1)
    for d in directions(rng, 7, low=0.1, high=0.95, gap=0.6):
        boll(p, Vector((d.x * 0.034 * s, d.y * 0.034 * s, 0.05 * s + d.z * 0.028 * s)), 0.012 * s, rng)
    return p


def cotton():
    """Rows of low cotton plants white with open bolls, and a sack of picked cotton."""
    rng = random.Random(109)
    parts = []
    spots = [(-0.17, 0.12), (-0.07, 0.15), (0.04, 0.16), (0.14, 0.12), (-0.13, 0.02), (-0.02, 0.04), (0.09, 0.03),
             (0.19, 0.0), (-0.08, -0.08), (0.03, -0.07)]
    for i, (x, y) in enumerate(spots):
        parts.append(at(cotton_plant(f"plant_{i}", rng, rng.uniform(0.9, 1.1)), x, y, rng.random() * math.tau))
    sack_part = Part("sack")
    sack(sack_part, (0, 0, 0), 0.03, 0.05, rng, fill_paint=Paint([COTTON, COTTON_SHADE]), fill=2)
    return parts + [at(sack_part, 0.15, -0.13)]


TOBACCO = hexc(0x8DB04A)
TOBACCO_DARK = hexc(0x6E9338)
TOBACCO_LIGHT = hexc(0xA9C664)
TOBACCO_FLOWER = hexc(0xE680A0)
TOBACCO_DRY = hexc(0xA8742E)
TOBACCO_DRY_DARK = hexc(0x8A5A24)


def tobacco_plant(name, rng, size=1.0):
    """A tall tobacco plant: a stalk with huge leaves spiralling up it, smaller toward the pink flowers on top."""
    p = Part(name)
    s = size
    h = 0.17 * s
    tube(p, [(0, 0, FOOT), (0, 0, h)], [0.006 * s, 0.004 * s], TOBACCO_DARK, sides=5)
    leaves = Paint([TOBACCO, TOBACCO_DARK, TOBACCO, TOBACCO_LIGHT], top=TOBACCO_LIGHT, top_cut=0.9, rng=rng)
    phase = rng.random() * math.tau
    count = 7
    for k in range(count):
        t = k / count
        a = phase + k * 2.4
        leaf(p, (0, 0, 0.015 * s + h * 0.75 * t), a, (0.1 - 0.06 * t) * s, (0.05 - 0.025 * t) * s, leaves,
             lift=0.7 + 0.5 * t, droop=1.0, fold=0.15, segments=5, shape="broad")
    for k in range(5):
        a = k * math.tau / 5
        blob(p, (math.cos(a) * 0.007 * s, math.sin(a) * 0.007 * s, h + 0.006 * s), (0.006 * s,) * 3, TOBACCO_FLOWER,
             subdivisions=0, wobble=0.0)
    return p


def tobacco_rack(name, rng):
    """A rail hung with leaves drying gold and brown."""
    p = Part(name)
    for x in (-0.06, 0.06):
        tube(p, [(x, 0, FOOT), (x, 0, 0.11)], 0.005, POST, sides=5)
    tube(p, [(-0.07, 0, 0.105), (0.07, 0, 0.105)], 0.004, POST, sides=5)
    dry = Paint([TOBACCO_DRY, TOBACCO_DRY_DARK], rng=rng)
    for i in range(7):
        x = -0.048 + i * 0.016
        for side in (-1, 1):
            top = Vector((x, side * 0.002, 0.103))
            tip = top + Vector((rng.uniform(-0.004, 0.004), side * 0.012, -0.06))
            tube(p, [top, top.lerp(tip, 0.5) + Vector((0, side * 0.004, 0)), tip], [0.002, 0.006, 0.0], dry,
                 sides=4, squash=0.35, phase=0.0)
    return p


def tobacco():
    """Tall tobacco plants with broad pale leaves and pink flowers, and a rack of leaves drying brown."""
    rng = random.Random(110)
    return [
        at(tobacco_plant("plant_a", rng, 1.1), -0.12, 0.09),
        at(tobacco_plant("plant_b", rng, 1.0), 0.02, 0.13),
        at(tobacco_plant("plant_c", rng, 0.95), 0.15, 0.07),
        at(tobacco_plant("plant_d", rng, 0.9), -0.04, -0.03),
        at(tobacco_rack("rack", rng), 0.08, -0.12, -0.15),
    ]


CANNABIS = hexc(0x4C9A38)
CANNABIS_DARK = hexc(0x387A2A)
CANNABIS_LIGHT = hexc(0x6CB64A)
CANNABIS_BUD = hexc(0xA8C878)


def palmate(part, base, azimuth, size, paint, leaflets=7, lift=0.35):
    """A fan leaf: a stalk out from base toward azimuth, ending in narrow, toothless leaflets spread like fingers,
    the middle one longest."""
    base = Vector(base)
    heading = Vector((math.cos(azimuth), math.sin(azimuth), 0))
    hub = base + heading * size * 0.35 + Vector((0, 0, size * 0.12))
    tube(part, [base, hub], 0.0016, CANNABIS_DARK, sides=3, cap=False)
    for k in range(leaflets):
        f = (k - (leaflets - 1) / 2) / ((leaflets - 1) / 2)
        a = azimuth + f * 1.25
        length = size * (1.0 - 0.45 * abs(f))
        leaf(part, hub, a, length, size * 0.2, paint, lift=lift, droop=0.6, fold=0.2, segments=3, shape="lance",
             thickness=0.002)


def cannabis_plant(name, rng, size=1.0):
    """A cannabis plant: a stem with pairs of fan leaves, wide at the base and narrowing to a pale flowering top."""
    p = Part(name)
    s = size
    h = 0.18 * s
    tube(p, [(0, 0, FOOT), (0, 0, h)], [0.005 * s, 0.003 * s], CANNABIS_DARK, sides=5)
    leaves = Paint([CANNABIS, CANNABIS_DARK, CANNABIS, CANNABIS_LIGHT], top=CANNABIS_LIGHT, top_cut=0.9, rng=rng)
    phase = rng.random() * math.tau
    for k in range(3):
        t = k / 3
        for side in (0, math.pi):
            palmate(p, (0, 0, 0.035 * s + h * 0.65 * t), phase + k * math.pi / 2 + side, (0.075 - 0.035 * t) * s,
                    leaves, leaflets=7 if k == 0 else 5, lift=0.3 + 0.4 * t)
    spindle(p, (0, 0, h * 0.8), (0, 0, h + 0.03 * s), 0.012 * s, Paint([CANNABIS_BUD, CANNABIS_LIGHT], rng=rng),
            sides=6, bulge=0.4)
    return p


def cannabis():
    """A patch of cannabis plants with their unmistakable fan leaves."""
    rng = random.Random(111)
    return [
        at(cannabis_plant("plant_a", rng, 1.1), -0.1, 0.1),
        at(cannabis_plant("plant_b", rng, 1.0), 0.07, 0.12),
        at(cannabis_plant("plant_c", rng, 0.95), 0.15, -0.03),
        at(cannabis_plant("plant_d", rng, 0.9), -0.06, -0.07),
    ]


# --- field crops ----------------------------------------------------------------------------------------------

CORN_STALK = hexc(0x8AA64A)
CORN_LEAF = hexc(0x6E9C3E)
CORN_LEAF_DARK = hexc(0x588532)
CORN_LEAF_LIGHT = hexc(0x8DB858)
CORN_COB = hexc(0xF2C63A)
CORN_COB_LIGHT = hexc(0xFFDA5A)
CORN_HUSK = hexc(0xA8BC6A)
CORN_TASSEL = hexc(0xC8A85A)


def cob(part, base, direction, length, rng, peeled=True):
    """An ear of corn: a fat yellow spindle, its husk folded back in pale leaves at the base."""
    base, d = Vector(base), Vector(direction).normalized()
    spindle(part, base, base + d * length, length * 0.24, Paint([CORN_COB, CORN_COB_LIGHT]), sides=6, bulge=0.4,
            phase=rng.random())
    if peeled:
        side = d.cross(UP_VEC).normalized() if abs(d.z) < 0.95 else Vector((1, 0, 0))
        for k in (-1, 1):
            tip = base + d * length * 0.35 + side * k * length * 0.32 - Vector((0, 0, length * 0.15))
            tube(part, [base, base.lerp(tip, 0.5) + side * k * 0.004, tip], [0.004, 0.005, 0.0], CORN_HUSK, sides=3,
                 squash=0.4)


def corn_stalk(part, base, rng, size=1.0, cobs=1):
    """A tall corn plant: a stalk, long strap leaves arching off it, a tassel on top and ears on its side."""
    base = Vector(base)
    h = 0.22 * size
    lean = Vector((rng.uniform(-0.01, 0.01), rng.uniform(-0.01, 0.01), 0))
    top = base + Vector((0, 0, h)) + lean
    tube(part, [base + Vector((0, 0, FOOT / 2)), base, top], [0.0055 * size, 0.005 * size, 0.003 * size],
         CORN_STALK, sides=5, phase=rng.random())
    leaves = Paint([CORN_LEAF, CORN_LEAF_DARK, CORN_LEAF, CORN_LEAF_LIGHT], top=CORN_LEAF_LIGHT, top_cut=0.9, rng=rng)
    phase = rng.random() * math.tau
    for k in range(4):
        t = 0.18 + k * 0.2
        leaf(part, base.lerp(top, t), phase + k * math.pi + rng.uniform(-0.4, 0.4), 0.11 * size, 0.017 * size,
             leaves, lift=0.9, droop=1.2, fold=0.25, segments=4, shape="strap", thickness=0.002)
    for k in range(4):
        a = k * math.tau / 4 + phase
        tube(part, [top - Vector((0, 0, 0.004)), top + Vector((math.cos(a) * 0.014, math.sin(a) * 0.014, 0.022))],
             [0.0018, 0.0], CORN_TASSEL, sides=3, cap=False)
    for k in range(cobs):
        a = phase + math.pi / 2 + k * math.pi
        root = base.lerp(top, 0.45 + 0.12 * k)
        cob(part, root, (math.cos(a) * 0.55, math.sin(a) * 0.55, 0.85), 0.05 * size, rng)


def corn_row(name, rng, count, spacing=0.06, size=1.0):
    p = Part(name)
    for i in range(count):
        x = (i - (count - 1) / 2) * spacing + rng.uniform(-0.006, 0.006)
        corn_stalk(p, (x, rng.uniform(-0.006, 0.006), 0), rng, size * rng.uniform(0.88, 1.06),
                   cobs=1 if rng.random() < 0.7 else 2)
    return p


def corn_pile(name, rng):
    """Harvested ears heaped on the ground."""
    p = Part(name)
    for i in range(7):
        a = i * 2.4
        d = 0.018 * math.sqrt(i)
        x, y = math.cos(a) * d, math.sin(a) * d
        z = 0.008 + (0.012 if i == 0 else 0.0)
        turn = rng.random() * math.tau
        dvec = Vector((math.cos(turn), math.sin(turn), 0.05))
        cob(p, Vector((x, y, z)) - dvec * 0.025, dvec, 0.052, rng, peeled=i % 2 == 0)
    return p


def corn():
    """Rows of tall corn, tasselled and bearing yellow ears, and a heap of picked ears."""
    rng = random.Random(112)
    return [
        at(corn_row("row_a", rng, 5), -0.02, 0.15, 0.08),
        at(corn_row("row_b", rng, 5), -0.04, 0.05, 0.08),
        at(corn_row("row_c", rng, 4), -0.06, -0.05, 0.08),
        at(corn_pile("pile", rng), 0.15, -0.12),
    ]


POTATO = hexc(0xC89A5E)
POTATO_DARK = hexc(0xA97C46)
POTATO_LEAF = hexc(0x3E7A30)
POTATO_LEAF_DARK = hexc(0x2F6426)
POTATO_FLOWER = hexc(0xF0EAF6)
POTATO_FLOWER_PURPLE = hexc(0xB89AD8)


def potato():
    """Earthed-up rows of bushy potato plants in flower, and a heap of fresh-dug potatoes by a sack."""
    rng = random.Random(113)
    field = Part("ground_field")
    edge = Outline(0.2, 0.16, rng, centre=(-0.03, 0.04), sides=28, ragged=0.08, square=3.5, turn=0.2)
    plate(field, edge.scaled(1.02, 0.004), SOIL, SOIL_DARK)
    leaves = Paint([POTATO_LEAF, POTATO_LEAF_DARK, POTATO_LEAF], rng=rng)
    soil = Paint([SOIL, SOIL_DARK], rng=rng)
    turn = Matrix.Rotation(0.2, 3, "Z")
    for row in range(4):
        v = -0.105 + row * 0.07
        # The earthed-up ridge.
        ridge = [edge.centre.to_3d() + turn @ Vector((u, v, 0.004)) for u in (-0.17, -0.06, 0.06, 0.17)]
        tube(field, ridge, 0.016, soil, sides=6, squash=0.6, phase=0.25)
        for col in range(6):
            u = -0.15 + col * 0.06 + rng.uniform(-0.006, 0.006)
            c = edge.centre.to_3d() + turn @ Vector((u, v, 0.012))
            if edge.fraction(c.x, c.y) > 0.9:
                continue
            surface = bush(field, c, (0.026, 0.024, 0.022), leaves, rng, wobble=0.25, subdivisions=1)
            dot_fruit(field, surface, 2, 0.0045, [POTATO_FLOWER, POTATO_FLOWER_PURPLE], rng, low=0.5, sink=0.2)
    pile = Part("pile")
    tubers = Paint([POTATO, POTATO_DARK], rng=rng)
    for i in range(11):
        a, d = i * 2.4, 0.019 * math.sqrt(i)
        z = 0.009 + (0.013 if i < 3 else 0.0)
        fruit(pile, (math.cos(a) * d, math.sin(a) * d, z), 0.0135 * rng.uniform(0.85, 1.1), tubers, rng, stretch=1.3)
    sack(pile, (0.065, 0.035, 0), 0.03, 0.055, rng, fill_paint=tubers, fill=1)
    return [at(field, 0.0, 0.0), at(pile, 0.14, -0.15)]


PADDY = hexc(0x8DB6C4)
PADDY_DEEP = hexc(0x76A2B4)
BUND = hexc(0x7A6A44)
RICE = hexc(0x6CB43C)
RICE_LIGHT = hexc(0x94D05A)
RICE_DARK = hexc(0x55982E)


def tuft(part, base, rng, height=0.05, blades=6, paint=None):
    """A clump of rice: thin blades fanning up and out from one point."""
    base = Vector(base)
    phase = rng.random() * math.tau
    for k in range(blades):
        a = phase + k * math.tau / blades + rng.uniform(-0.3, 0.3)
        out = rng.uniform(0.25, 0.5)
        tip = base + Vector((math.cos(a) * height * out, math.sin(a) * height * out, height * rng.uniform(0.8, 1.1)))
        mid = base.lerp(tip, 0.5) + Vector((0, 0, height * 0.08))
        tube(part, [base, mid, tip], [0.0026, 0.002, 0.0], paint, sides=3, cap=False, phase=rng.random())


def rice():
    """A flooded paddy behind a low earth bank, planted in neat rows of bright green rice."""
    rng = random.Random(114)
    paddy = Part("ground_paddy")
    edge = Outline(0.22, 0.18, rng, centre=(0.0, 0.02), sides=28, ragged=0.06, square=3.0, turn=-0.15)
    plate(paddy, edge.scaled(1.0, 0.012), BUND, shade(BUND, 0.85), z=0.008)
    plate(paddy, edge.scaled(0.92), Paint([PADDY, PADDY_DEEP, PADDY], rng=rng), PADDY, z=0.0095)
    blades = Paint([RICE, RICE_DARK, RICE_LIGHT], rng=rng)
    turn = Matrix.Rotation(-0.15, 3, "Z")
    for row in range(6):
        v = -0.14 + row * 0.054
        for col in range(8):
            u = -0.19 + col * 0.054 + rng.uniform(-0.004, 0.004)
            c = edge.centre.to_3d() + turn @ Vector((u, v + rng.uniform(-0.004, 0.004), 0.006))
            if edge.fraction(c.x, c.y) > 0.82:
                continue
            tuft(paddy, c, rng, height=rng.uniform(0.045, 0.06), paint=blades)
    return [at(paddy, 0.0, 0.0)]


CANE = hexc(0xB9BE5C)
CANE_DARK = hexc(0x8E9A44)
CANE_NODE = hexc(0x7A6A3A)
CANE_LEAF = hexc(0x6EA642)
CANE_LEAF_DARK = hexc(0x558A34)


def cane(part, base, top, r, rng, leaves=None):
    """A sugar cane: a jointed stalk of short segments, ringed at each node, with a spray of long blades on top."""
    base, top = Vector(base), Vector(top)
    segments = 4
    for i in range(segments):
        a, b = base.lerp(top, i / segments), base.lerp(top, (i + 1) / segments)
        tube(part, [a, b], [r, r * 0.92], Paint([CANE, CANE_DARK], rng=rng), sides=5, phase=0.1, cap=False)
        tube(part, [b - Vector((0, 0, 0.002)), b + Vector((0, 0, 0.002))], r * 1.12, CANE_NODE, sides=5, phase=0.1,
             cap=False)
    if leaves is not None:
        phase = rng.random() * math.tau
        for k in range(3):
            leaf(part, top, phase + k * math.tau / 3 + rng.uniform(-0.3, 0.3), 0.09, 0.012, leaves, lift=1.1,
                 droop=1.5, fold=0.2, segments=4, shape="lance", thickness=0.002)


def cane_clump(name, rng, count=7, size=1.0):
    """A dense clump of tall canes."""
    p = Part(name)
    leaves = Paint([CANE_LEAF, CANE_LEAF_DARK, CANE_LEAF], rng=rng)
    for k in range(count):
        a, d = k * 2.4 + rng.random(), 0.02 * math.sqrt(k + 0.3)
        base = Vector((math.cos(a) * d, math.sin(a) * d, FOOT / 2))
        top = Vector((base.x * 1.8 + rng.uniform(-0.01, 0.01), base.y * 1.8 + rng.uniform(-0.01, 0.01),
                      rng.uniform(0.19, 0.25) * size))
        cane(p, base, top, 0.0055 * size, rng, leaves)
    return p


def cut_cane(name, rng):
    """A bundle of cut canes lying on the ground, tied in the middle."""
    p = Part(name)
    for k in range(6):
        y, z = (k % 3 - 1) * 0.011, 0.006 + (k // 3) * 0.009
        cane(p, (-0.08, y, z), (0.08, y + rng.uniform(-0.004, 0.004), z), 0.0055, rng)
    tube(p, [(-0.004, 0, 0.0), (0.004, 0, 0.0)], 0.024, TWINE, sides=8, cap=True)
    return p


def sugar():
    """Clumps of tall, jointed sugar cane crowned with long blades, and a tied bundle of cut cane."""
    rng = random.Random(116)
    return [
        at(cane_clump("clump_a", rng, 7, 1.05), -0.1, 0.09),
        at(cane_clump("clump_b", rng, 6, 0.95), 0.1, 0.11),
        at(cane_clump("clump_c", rng, 5, 0.9), 0.03, -0.04),
        at(cut_cane("bundle", rng), -0.1, -0.13, 0.25),
    ]


VINE = hexc(0x4C7E30)
VINE_DARK = hexc(0x3A6626)
VINE_LIGHT = hexc(0x679A40)
GRAPE = hexc(0x5C2A6E)
GRAPE_LIGHT = hexc(0x7A3E8E)
OAK = hexc(0x8A5A34)
OAK_DARK = hexc(0x6E4428)
HOOP = hexc(0x3E3A38)


def bead(part, centre, r, colour):
    """A tiny round thing at little cost: an octahedron (a grape, a berry, a pebble)."""
    x, y, z = centre
    hull(part, [(x + r, y, z), (x - r, y, z), (x, y + r, z), (x, y - r, z), (x, y, z + r), (x, y, z - r)], colour)


def grapes(part, top, size, rng):
    """A bunch of grapes hanging from top: berries in shrinking rings down to a point."""
    top = Vector(top)
    colours = [GRAPE, GRAPE_LIGHT, GRAPE]
    k = size / 0.01
    for ring, (n, r) in enumerate([(5, 0.0095), (3, 0.006), (1, 0.0)]):
        phase = rng.random() * math.tau
        for j in range(n):
            a = phase + j * math.tau / n
            c = top + Vector((math.cos(a) * r, math.sin(a) * r, -ring * 0.0095 - 0.004)) * k
            bead(part, c, 0.0062 * k, colours[(ring + j) % 3])


def vine_row(name, rng, length=0.34):
    """A trellised row of vines: posts and a wire carrying a hedge of leaves, purple bunches hanging beneath."""
    p = Part(name)
    for x in (-length / 2, 0.0, length / 2):
        tube(p, [(x, 0, FOOT), (x, 0, 0.115)], 0.0045, POST, sides=5)
    tube(p, [(-length / 2, 0, 0.1), (length / 2, 0, 0.1)], 0.0015, HOOP, sides=3)
    leaves = Paint([VINE, VINE_DARK, VINE, VINE_LIGHT], top=VINE_LIGHT, top_cut=0.8, rng=rng)
    count = 6
    for i in range(count):
        x = (i + 0.5) / count * length - length / 2
        tube(p, [(x, 0, FOOT), (x + 0.004, 0, 0.05), (x - 0.003, 0, 0.09)], [0.005, 0.004, 0.003], BARK_DARK,
             sides=4)
        blob(p, (x, 0, 0.105), (length / count * 0.62, 0.032, 0.03), leaves, subdivisions=1, wobble=0.25,
             seed=rng.random() * 9)
        for side in (-1, 1) if i % 2 else (-1,):
            grapes(p, (x + rng.uniform(-0.012, 0.012), side * 0.022, 0.08), 0.0105, rng)
    return p


def barrel(part, base, r, h, rng):
    """An oak barrel standing on end: bulging staves, dark hoops and a lid."""
    x, y, z = base
    staves = Paint([OAK, OAK_DARK, OAK], rng=rng)
    tube(part, [(x, y, z - 0.004), (x, y, z + h * 0.5), (x, y, z + h)], [r * 0.86, r, r * 0.86], staves, sides=12)
    for t in (0.14, 0.4, 0.6, 0.86):
        rr = r * (0.86 + 0.14 * math.sin(math.pi * t))
        tube(part, [(x, y, z + h * t - 0.0025), (x, y, z + h * t + 0.0025)], rr * 1.04, HOOP, sides=12)


def wine():
    """Three trellised rows of vines heavy with purple grapes, and a wine barrel."""
    rng = random.Random(117)
    cask = Part("barrel")
    barrel(cask, (0, 0, 0), 0.03, 0.07, rng)
    return [
        at(vine_row("row_a", rng), 0.0, 0.15, 0.12),
        at(vine_row("row_b", rng), -0.02, 0.04, 0.12),
        at(vine_row("row_c", rng, 0.28), -0.05, -0.07, 0.12),
        at(cask, 0.17, -0.13),
    ]


# --- spices, dyes and incense ------------------------------------------------------------------------------------

CHILI = hexc(0xD0301E)
PAPRIKA = hexc(0xC24224)
TURMERIC = hexc(0xEBA81E)
CINNAMON = hexc(0x94532A)
CARDAMOM = hexc(0x8C9A34)
BUSH_GREEN = hexc(0x3F7A2E)
BUSH_GREEN_DARK = hexc(0x2F6324)
BUSH_GREEN_LIGHT = hexc(0x5A9440)


def cone_heap(part, top, r, colour, rng, height=None):
    """A heaped cone of powder or grain standing in a container whose rim centre is top."""
    top = Vector(top)
    height = height or r * 0.9
    tube(part, [top - Vector((0, 0, 0.004)), top + Vector((0, 0, height * 0.35)), top + Vector((0, 0, height))],
         [r * 1.0, r * 0.72, 0.0], Paint([colour, shade(colour, 0.92)], rng=rng), sides=10, phase=rng.random())


def spice_basket(part, base, r, colour, rng):
    top = basket(part, base, r, r * 0.65, rng)
    cone_heap(part, top, r * 0.98, colour, rng)


def chili_bush(name, rng, size=1.0):
    """A chili bush hung with long red pods."""
    p = Part(name)
    leaves = Paint([BUSH_GREEN, BUSH_GREEN_DARK, BUSH_GREEN, BUSH_GREEN_LIGHT], top=BUSH_GREEN_LIGHT, top_cut=0.8,
                   rng=rng)
    tube(p, [(0, 0, FOOT), (0, 0, 0.03 * size)], 0.006 * size, BARK, sides=5)
    surface = bush(p, (0, 0, 0.02 * size), (0.045 * size, 0.045 * size, 0.042 * size), leaves, rng)
    for d in directions(rng, 12, low=-0.3, high=0.8, gap=0.5):
        point, normal = surface.at(d)
        root = point + normal * 0.002
        tip = root + Vector((normal.x * 0.008, normal.y * 0.008, -0.03)) * size
        tube(p, [root, root.lerp(tip, 0.3) + normal * 0.003, tip], [0.005 * size, 0.0055 * size, 0.0], CHILI,
             sides=5)
    return p


def spices():
    """Baskets heaped with bright cones of paprika, turmeric, cinnamon and cardamom before two chili bushes."""
    rng = random.Random(118)
    market = Part("baskets")
    for (x, y, r), colour in zip([(-0.065, 0.025, 0.045), (0.04, 0.035, 0.041), (-0.02, -0.06, 0.043),
                                  (0.09, -0.045, 0.038)], [PAPRIKA, TURMERIC, CINNAMON, CARDAMOM]):
        spice_basket(market, (x, y, 0), r, colour, rng)
    return [
        at(chili_bush("bush_a", rng, 1.25), -0.12, 0.13),
        at(chili_bush("bush_b", rng, 1.1), 0.1, 0.14),
        at(market, 0.0, -0.06),
    ]


INDIGO = hexc(0x2C3C9E)
INDIGO_DEEP = hexc(0x232F80)
CRIMSON = hexc(0xB4243A)
SAFFRON = hexc(0xEAB024)
INDIGO_FLOWER = hexc(0x6E58C8)


def vat(part, base, r, h, liquid, rng):
    """A wooden dye vat: a wide, low tub of staves and hoops, full nearly to the brim."""
    x, y, z = base
    vessel(part, [(x, y, z - 0.004), (x, y, z + h)], [r * 0.92, r], 0.004, Paint([OAK, OAK_DARK, OAK], rng=rng),
           sides=12)
    for t in (0.25, 0.8):
        tube(part, [(x, y, z + h * t - 0.002), (x, y, z + h * t + 0.002)], r * (0.94 + 0.06 * t) * 1.04, HOOP,
             sides=12)
    cylinder(part, (x, y, z + h * 0.6), (x, y, z + h * 0.93), r * 0.9, r * 0.95, liquid, sides=12)


def indigo_bush(name, rng, size=1.0):
    """An indigo plant: a leafy bush with spikes of violet flowers."""
    p = Part(name)
    leaves = Paint([BUSH_GREEN, BUSH_GREEN_DARK, BUSH_GREEN_LIGHT], rng=rng)
    surface = bush(p, (0, 0, 0.0), (0.045 * size, 0.042 * size, 0.045 * size), leaves, rng)
    for d in directions(rng, 9, low=0.1, high=0.95, gap=0.55):
        point, normal = surface.at(d)
        spindle(p, point - normal * 0.006, point + (normal + UP_VEC).normalized() * 0.024 * size, 0.0055 * size,
                INDIGO_FLOWER, sides=4, bulge=0.5)
    return p


def dye():
    """Three vats of indigo, crimson and saffron dye, and indigo bushes in flower."""
    rng = random.Random(119)
    vats = Part("vats")
    vat(vats, (-0.045, 0.012, 0), 0.04, 0.04, Paint([INDIGO, INDIGO_DEEP]), rng)
    vat(vats, (0.045, 0.02, 0), 0.036, 0.036, CRIMSON, rng)
    vat(vats, (0.0, -0.058, 0), 0.034, 0.034, SAFFRON, rng)
    return [
        at(indigo_bush("bush_a", rng, 1.1), -0.12, 0.12),
        at(indigo_bush("bush_b", rng, 0.95), 0.07, 0.15),
        at(indigo_bush("bush_c", rng, 0.85), 0.17, 0.04),
        at(vats, -0.02, -0.06),
    ]


INCENSE_BARK = hexc(0xA49478)
INCENSE_BARK_DARK = hexc(0x857658)
INCENSE_LEAF = hexc(0x6E8A42)
INCENSE_LEAF_DARK = hexc(0x566E34)
RESIN = hexc(0xE8A83A)
BRONZE = hexc(0x9A6A34)
BRONZE_DARK = hexc(0x7A5228)
SMOKE = hexc(0xE6E2EC)
SMOKE_SHADE = hexc(0xCFCAD8)


def incense_tree(name, rng, size=1.0):
    """A frankincense tree: a short, crooked, pale trunk branching into twisting limbs, each tipped with a small
    clump of leaves, amber resin weeping from cuts in the bark."""
    p = Part(name)
    s = size
    bark = Paint([INCENSE_BARK, INCENSE_BARK_DARK], rng=rng)
    leaves = Paint([INCENSE_LEAF, INCENSE_LEAF_DARK, INCENSE_LEAF], rng=rng)
    fork = Vector((rng.uniform(-0.012, 0.012), rng.uniform(-0.012, 0.012), 0.06 * s))
    tube(p, [(0, 0, FOOT), (0, 0, 0), (0.008 * s, 0.004 * s, 0.03 * s), fork], [0.016 * s, 0.015 * s, 0.012 * s,
                                                                                 0.01 * s], bark, sides=6)
    phase = rng.random() * math.tau
    for k in range(4):
        a = phase + k * math.tau / 4 + rng.uniform(-0.3, 0.3)
        out = Vector((math.cos(a), math.sin(a), 0))
        elbow = fork + out * 0.03 * s + Vector((0, 0, 0.025 * s))
        tip = fork + out * 0.055 * s + Vector((0, 0, rng.uniform(0.055, 0.075) * s))
        tube(p, [fork - Vector((0, 0, 0.008 * s)), elbow, tip], [0.008 * s, 0.006 * s, 0.004 * s], bark, sides=5)
        blob(p, tip + Vector((0, 0, 0.01 * s)), (0.042 * s, 0.04 * s, 0.022 * s), leaves, subdivisions=2,
             wobble=0.25, seed=rng.random() * 9)
    for k in range(3):
        a = -math.pi / 2 + (k - 1) * 0.9
        bead(p, (math.cos(a) * 0.014 * s, math.sin(a) * 0.014 * s, (0.015 + 0.012 * k) * s), 0.0055 * s, RESIN)
    return p


def censer(name, rng):
    """A bronze censer on three legs, a plume of fragrant smoke curling up from it."""
    p = Part(name)
    for k in range(3):
        a = k * math.tau / 3
        tube(p, [(math.cos(a) * 0.012, math.sin(a) * 0.012, 0.022), (math.cos(a) * 0.022, math.sin(a) * 0.022,
                                                                     -0.004)], 0.0035, BRONZE_DARK, sides=4)
    tube(p, [(0, 0, 0.016), (0, 0, 0.03), (0, 0, 0.042)], [0.012, 0.026, 0.028], Paint([BRONZE, BRONZE_DARK]),
         sides=10)
    cylinder(p, (0, 0, 0.036), (0, 0, 0.0405), 0.025, 0.025, INCENSE_BARK_DARK, sides=10)
    smoke = Paint([SMOKE, SMOKE_SHADE, SMOKE], rng=rng)
    pos = Vector((0, 0, 0.052))
    for k in range(5):
        r = 0.017 - 0.002 * k
        blob(p, pos, (r, r, r * 0.9), smoke, subdivisions=2, wobble=0.2, seed=rng.random() * 9)
        pos += Vector((0.01 * math.sin(k * 1.3 + 0.5), 0.006 * math.cos(k * 1.7), r * 1.5))
    return p


def incense():
    """Crooked frankincense trees weeping amber resin, and a bronze censer sending up a plume of smoke."""
    rng = random.Random(120)
    return [
        at(incense_tree("tree_a", rng, 1.15), -0.11, 0.08),
        at(incense_tree("tree_b", rng, 0.95), 0.12, 0.1),
        at(censer("censer", rng), 0.03, -0.1),
    ]


# --- ruins and digs --------------------------------------------------------------------------------------------

SANDSTONE = hexc(0xCDB68A)
SANDSTONE_DARK = hexc(0xAE9668)
SANDSTONE_LIGHT = hexc(0xE0CDA2)
MOSS = hexc(0x6E8E3E)


def column(part, base, h, r, paint, rng, broken=True):
    """A fluted column on a square plinth; a broken one ends in a jagged slant."""
    x, y, z = base
    box(part, (x - r * 1.35, y - r * 1.35, z - 0.01), (x + r * 1.35, y + r * 1.35, z + r * 0.6), paint)
    if broken:
        top = Vector((x, y, z + h))
        tube(part, [(x, y, z + r * 0.6), (x, y, z + h * 0.92)], r, paint, sides=8, cap=False)
        tilt = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0)).normalized() * r
        hull(part, [Vector((x + math.cos(a) * r, y + math.sin(a) * r, z + h * 0.92)) for a in
                    (k * math.tau / 8 for k in range(8))] +
             [top + tilt + Vector((0, 0, h * 0.06)), top - tilt * 0.6 - Vector((0, 0, h * 0.02)),
              top + Vector((-tilt.y, tilt.x, 0)) * 0.9], paint)
    else:
        tube(part, [(x, y, z + r * 0.6), (x, y, z + h)], r, paint, sides=8)
        box(part, (x - r * 1.3, y - r * 1.3, z + h), (x + r * 1.3, y + r * 1.3, z + h + r * 0.7), paint)


def ancienttemple():
    """A ruined temple: a stepped platform, broken columns, a lintel still standing on two of them, and fallen
    drums in the grass."""
    rng = random.Random(121)
    stone = Paint([SANDSTONE, SANDSTONE_DARK, SANDSTONE], top=SANDSTONE_LIGHT, top_cut=0.8, rng=rng)
    mossy = Paint([SANDSTONE, SANDSTONE_DARK], top=MOSS, top_cut=0.8, rng=rng)
    temple = Part("temple")
    for i, (w, d, h) in enumerate([(0.42, 0.3, 0.025), (0.36, 0.25, 0.022), (0.3, 0.2, 0.02)]):
        z0 = sum(s[2] for s in [(0, 0, 0.025), (0, 0, 0.022), (0, 0, 0.02)][:i])
        box(temple, (-w / 2, -d / 2, z0 - (0.05 if i == 0 else 0)), (w / 2, d / 2, z0 + h), mossy if i == 0 else stone)
    floor = 0.067
    r = 0.016
    for i, (x, y, h, broken) in enumerate([(-0.11, -0.065, 0.15, False), (-0.03, -0.065, 0.15, False),
                                          (0.05, -0.065, 0.09, True), (0.11, -0.065, 0.05, True),
                                          (-0.11, 0.065, 0.11, True), (0.11, 0.065, 0.16, True)]):
        column(temple, (x, y, floor), h, r, stone, rng, broken)
    # The lintel across the two whole columns.
    box(temple, (-0.135, -0.085, floor + 0.161), (-0.005, -0.045, floor + 0.19), stone)
    fallen = Part("fallen")
    for k, (x, y) in enumerate([(0.0, 0.0), (0.045, 0.012), (0.085, 0.03)]):
        turn = Matrix.Translation((x, y, r)) @ Matrix.Rotation(math.pi / 2, 4, "Y") @ Matrix.Rotation(0.3, 4, "X")
        mark = fallen.mark()
        tube(fallen, [(0, 0, -0.018), (0, 0, 0.018)], r, stone, sides=8)
        fallen.transformed(turn, mark)
    return [at(temple, 0.0, 0.03), at(fallen, 0.08, -0.17, 0.2)]


BONE = hexc(0xEEE4C8)
BONE_SHADE = hexc(0xD6CAA8)
DIG_SAND = hexc(0xCDB078)
DIG_SAND_DARK = hexc(0xAE9260)
SPOIL = hexc(0x8E7650)
STRING = hexc(0xE8DCC0)


def skeleton(part, rng, z=0.004):
    """A dinosaur skeleton lying on its side: a curving spine of vertebrae, arched ribs, a long skull with an open
    jaw, a leg and a tail tapering away."""
    bone = Paint([BONE, BONE_SHADE, BONE], rng=rng)
    spine = [Vector((-0.16 + 0.32 * t, 0.03 * math.sin(t * 3.0 + 0.6), z + 0.008)) for t in
             (i / 15 for i in range(16))]
    for i, v in enumerate(spine):
        bead(part, v, 0.0075 * (1.0 - 0.5 * max(0.0, 0.45 - i / 15)), bone)
    tube(part, spine, 0.0035, bone, sides=4)
    # Ribs: arches from the spine down to the ground, on the camera's side.
    for i in range(7, 13):
        root = spine[i]
        length = 0.06 * (1 - abs(i - 9.5) / 6)
        tip = root + Vector((0.01, -length, -0.002))
        tube(part, [root, root + Vector((0.004, -length * 0.4, 0.02)), tip], [0.0034, 0.003, 0.0022], bone, sides=4)
    # The skull and its open lower jaw.
    head = spine[-1] + Vector((0.03, 0.005, 0.004))
    hull(part, [head + Vector(v) for v in [(-0.02, -0.014, -0.004), (-0.02, 0.014, -0.004), (-0.022, 0, 0.018),
                                           (0.035, -0.006, -0.002), (0.035, 0.006, -0.002), (0.03, 0, 0.008),
                                           (0.0, -0.012, 0.012), (0.0, 0.012, 0.012)]], bone)
    tube(part, [head + Vector((-0.016, -0.008, -0.006)), head + Vector((0.03, -0.022, -0.006))], [0.0045, 0.003],
         bone, sides=4)
    # A hind leg bone and a claw.
    hip = spine[5]
    tube(part, [hip, hip + Vector((0.01, -0.045, -0.004)), hip + Vector((0.035, -0.07, -0.004))],
         [0.006, 0.005, 0.004], bone, sides=5)


def fossils():
    """A dig: a pegged-out pit of pale sand with a dinosaur skeleton laid bare in it, spoil heaped at its side."""
    rng = random.Random(122)
    dig = Part("ground_dig")
    edge = Outline(0.23, 0.15, rng, sides=24, ragged=0.08, square=3.0, turn=0.1)
    plate(dig, edge.scaled(1.0, 0.012), SPOIL, shade(SPOIL, 0.85), z=0.004)
    plate(dig, edge.scaled(0.95), Paint([DIG_SAND, DIG_SAND_DARK, DIG_SAND], rng=rng), DIG_SAND_DARK, z=0.0055)
    skeleton(dig, rng, z=0.006)
    # Pegs at the corners, strung together.
    corners = [edge.point(k, 1.02) for k in (3, 9, 15, 21)]
    for c in corners:
        tube(dig, [(c.x, c.y, FOOT / 2), (c.x, c.y, 0.04)], 0.0035, POST, sides=4)
    for a, b in zip(corners, corners[1:] + corners[:1]):
        tube(dig, [(a.x, a.y, 0.036), (b.x, b.y, 0.036)], 0.0012, STRING, sides=3, cap=False)
    spoil = Part("spoil")
    blob(spoil, (0, 0, -0.01), (0.05, 0.04, 0.035), Paint([SPOIL, shade(SPOIL, 0.9)], rng=rng), subdivisions=2,
         wobble=0.15, seed=rng.random())
    tube(spoil, [(0.02, -0.01, 0.0), (0.05, 0.02, 0.07)], 0.003, POST, sides=4)  # a shovel stuck in the heap
    box(spoil, (0.012, -0.022, -0.012), (0.03, -0.004, 0.012), HOOP,
        Matrix.Translation((0.012, -0.004, 0)) @ Matrix.Rotation(0.6, 4, "Z") @ Matrix.Translation((-0.012, 0.004, 0)))
    return [at(dig, 0.0, 0.03), at(spoil, 0.17, -0.15)]


MODELS = {
    "wheat": wheat,
    "spices": spices,
    "dye": dye,
    "incense": incense,
    "ancienttemple": ancienttemple,
    "fossils": fossils,
    "corn": corn,
    "potato": potato,
    "rice": rice,
    "sugar": sugar,
    "wine": wine,
    "coffee": coffee,
    "tea": tea,
    "cotton": cotton,
    "tobacco": tobacco,
    "cannabis": cannabis,
    "apple": apple,
    "lemon": lemon,
    "olives": olives,
    "banana": banana,
    "rubber": rubber,
    "silk": silk,
}
