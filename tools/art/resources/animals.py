"""Animal resources: cattle, deer, horses, pigs, sheep, elephants (ivory) and fur.

Conventions are in kit.py. Animals are built facing +X with their hooves at z = 0, one part each, named "nodecal..."
so no worn earth patch is drawn under them, and turned to show their side to the camera. A herd is two or three
adults, often with a young one, bigger than life against the tile so they read from the default camera.
"""
import math
import random
from dataclasses import dataclass

from mathutils import Matrix, Vector

from .kit import (FOOT, Band, Outline, Paint, Part, Pattern, at, blob, box, cylinder, hexc, hull, plate, solid,
                  spindle, tube)


def scaled(part: Part, factor: float) -> Part:
    return part.transformed(Matrix.Scale(factor, 4))


@dataclass
class Body:
    """A quadruped's proportions, facing +X: the chest and rump blobs, the legs and the head."""
    length: float = 0.15  # chest front to rump back
    height: float = 0.085  # body centre above the ground
    girth: float = 0.04  # body radius, top to bottom
    width: float = 0.034  # body radius, side to side
    leg: float = 0.0095  # leg radius at the top
    leg_spread: float = 0.6  # legs' distance from the centre line, as a fraction of width
    taper: float = 0.68  # lower leg radius as a fraction of the top
    neck: float = 0.022  # neck radius
    head: tuple = (0.026, 0.018, 0.02)  # head radii along, across and up
    head_up: float = 0.105  # head centre height when standing
    reach: float = 0.03  # how far the head sits ahead of the chest
    snout: float = 0.6  # head tilt (radians below horizontal)
    hump: float = 0.0  # extra height over the shoulders
    wobble: float = 0.05  # lumpiness of the body (wool)
    frequency: float = 1.6
    subdivisions: int = 2


def quadruped(part: Part, body: Body, coat, legs, hooves, rng, grazing=False, face=None):
    """The body, legs, neck and head of a four-legged animal. Returns the head centre and its forward direction, for
    adding horns, ears and a muzzle."""
    L, H = body.length, body.height
    chest = Vector((L * 0.2, 0, H + body.hump * 0.5))
    rump = Vector((-L * 0.24, 0, H - 0.002))
    for centre, radii in ((chest, (L * 0.36, body.width * 1.02, body.girth * 1.04 + body.hump * 0.4)),
                          (rump, (L * 0.32, body.width * 0.98, body.girth * 0.96))):
        blob(part, centre, radii, coat, subdivisions=body.subdivisions, wobble=body.wobble,
             frequency=body.frequency, seed=rng.random())
    t = body.taper
    for x in (L * 0.32, -L * 0.33):
        for side in (-1, 1):
            y = side * body.width * body.leg_spread
            top = Vector((x, y, H - body.girth * 0.35))
            knee = Vector((x + (0.006 if x > 0 else -0.004), y, H * 0.42))
            foot = Vector((x + rng.uniform(-0.003, 0.003), y, 0.012))
            tube(part, [top, knee, foot], [body.leg, body.leg * (t + 1) / 2, body.leg * t], legs, sides=6,
                 phase=rng.random())
            cylinder(part, foot + Vector((0, 0, 0.002)), Vector((foot.x, foot.y, -0.004)), body.leg * t * 1.03,
                     body.leg * t * 1.18, hooves, sides=6)
    shoulder = chest + Vector((L * 0.22, 0, body.girth * 0.35))
    if grazing:
        head = Vector((L * 0.5 + body.reach * 0.6, 0, body.head[2] * 1.15))
        direction = Vector((0.45, 0, -0.9)).normalized()
    else:
        head = Vector((L * 0.5 + body.reach, 0, body.head_up))
        direction = Vector((math.cos(body.snout), 0, -math.sin(body.snout)))
    neck_end = head - direction * body.head[0] * 0.6
    tube(part, [shoulder - Vector((0.012, 0, 0)), shoulder.lerp(neck_end, 0.5) + Vector((0, 0, 0.006)), neck_end],
         [body.neck * 1.25, body.neck, body.neck * 0.85], coat, sides=7)
    # The head: a blob stretched along its direction.
    turn = direction.to_track_quat("X", "Z").to_matrix().to_4x4()
    mark = part.mark()
    blob(part, (0, 0, 0), body.head, face or coat, subdivisions=1, wobble=0.06, seed=rng.random())
    part.transformed(Matrix.Translation(head) @ turn, mark)
    return head, direction


def head_point(head: Vector, direction: Vector, forward: float, side: float, up: float) -> Vector:
    """A point on the head: forward along its direction, side across it (+Y), up roughly toward +Z."""
    across = Vector((0, 1, 0))
    upward = across.cross(direction).normalized() * -1
    if upward.z < 0:
        upward = -upward
    return head + direction * forward + across * side + upward * up


# --- cattle -------------------------------------------------------------------------------------------------------

HIDE_WHITE = hexc(0xEDE8DC)
HIDE_BLACK = hexc(0x2B2724)
HIDE_RED = hexc(0x8E4A2A)
HIDE_RED_DARK = hexc(0x6E3820)
MUZZLE = hexc(0xE0A79A)
MUZZLE_DARK = hexc(0x3A302C)
HORN = hexc(0xE6D9B8)
HOOF = hexc(0x2A2420)
HAY = hexc(0xD9B95E)
HAY_LIGHT = hexc(0xE8CF7A)
HAY_DARK = hexc(0xB8963E)


def cow(name, rng, coat, legs, face, grazing=False, horns=True, size=1.15):
    p = Part(name)
    body = Body()
    head, d = quadruped(p, body, coat, legs, HOOF, rng, grazing=grazing)
    blob(p, head_point(head, d, 0.022, 0, -0.004), (0.015, 0.017, 0.013), face, subdivisions=1, wobble=0.04)
    for side in (-1, 1):
        root = head_point(head, d, -0.012, side * 0.013, 0.014)
        if horns:
            tip = root + Vector((0.004, side * 0.016, 0.014))
            tube(p, [root, root.lerp(tip, 0.5) + Vector((0, side * 0.004, 0)), tip], [0.0045, 0.0035, 0.0],
                 HORN, sides=5)
        ear = head_point(head, d, -0.016, side * 0.02, 0.004)
        blob(p, ear + Vector((0, side * 0.006, 0)), (0.007, 0.011, 0.004), legs, subdivisions=0, wobble=0.0)
    # Tail with a tuft, and the udder.
    tail = [Vector((-0.1, 0, 0.11)), Vector((-0.11, 0, 0.08)), Vector((-0.108, 0, 0.05))]
    tube(p, tail, [0.004, 0.0035, 0.003], legs, sides=4)
    blob(p, (-0.108, 0, 0.045), (0.006, 0.006, 0.01), HIDE_BLACK, subdivisions=0, wobble=0.0)
    blob(p, (-0.03, 0, 0.055), (0.018, 0.015, 0.012), MUZZLE, subdivisions=1, wobble=0.05)
    return scaled(p, size)


TWINE = hexc(0x8A6A3A)


def bale(part, centre, size, yaw, hay, rng):
    """A square bale of hay with two twine ties, its edges a little rounded."""
    w, d, h = size
    turn = Matrix.Rotation(yaw, 3, "Z")
    centre = Vector(centre)
    corners = [centre + turn @ Vector((sx * w / 2, sy * d / 2, sz * h / 2)) for sx in (-1, 1) for sy in (-1, 1)
               for sz in (-1, 1)]
    axis = turn @ Vector((1, 0, 0))
    paint, cuts = hay, []
    for t in (-0.25, 0.25):
        tie = centre + axis * w * t
        paint = Band(paint, TWINE, tie, axis, 0.0035)
        cuts += [(tie - axis * 0.0035, axis), (tie + axis * 0.0035, axis)]
    solid(part, corners, paint, chamfer=min(size) * 0.18, cuts=cuts)


def hay_bales(name, rng):
    """Two bales of hay on the ground and one across them."""
    p = Part(name)
    hay = Paint([HAY, HAY_LIGHT, HAY_DARK, HAY], top=HAY_LIGHT, top_cut=0.8, rng=rng)
    size = (0.07, 0.04, 0.036)
    bale(p, (0, -0.021, 0.014), size, 0.04, hay, rng)
    bale(p, (0.004, 0.021, 0.014), size, -0.03, hay, rng)
    bale(p, (0.002, 0.0, 0.05), size, math.pi / 2 + 0.12, hay, rng)
    return p


def cattle():
    """A Holstein cow, a red cow grazing, and their calf, with a pile of hay."""
    rng = random.Random(201)
    holstein = Pattern(HIDE_WHITE, HIDE_BLACK, scale=28, cover=0.38, seed=1.3)
    red = Pattern(HIDE_RED, HIDE_WHITE, scale=22, cover=0.18, seed=4.1)
    return [
        at(cow("nodecal_cow", rng, holstein, holstein, MUZZLE), -0.07, 0.07, 0.25),
        at(cow("nodecal_cow_b", rng, red, Paint(HIDE_RED_DARK), MUZZLE, grazing=True), 0.08, -0.07, math.pi + 0.35),
        at(cow("nodecal_calf", rng, Pattern(HIDE_WHITE, HIDE_RED, scale=30, cover=0.4, seed=2.2),
               Paint(HIDE_RED), MUZZLE, horns=False, size=0.7), 0.17, 0.06, math.pi - 0.5),
        at(hay_bales("hay", rng), -0.15, -0.12, 0.5),
    ]


def ears(part, head, d, paint, size, forward=-0.012, side=0.011, up=0.009, lift=0.6, length=1.0):
    """A pair of pointed ears: flattened cones from the back of the head, out to the sides and up by lift."""
    for s in (-1, 1):
        root = head_point(head, d, forward * size, s * side * size, up * size)
        out = Vector((-0.2, s * (1 - lift), lift)).normalized()
        tube(part, [root, root + out * 0.009 * size * length, root + out * 0.016 * size * length],
             [0.0045 * size, 0.004 * size, 0.0], paint, sides=4, squash=0.4)


# --- deer ---------------------------------------------------------------------------------------------------------

DEER = hexc(0x9C6236)
DEER_DARK = hexc(0x7E4C28)
DEER_BELLY = hexc(0xDCC29C)
ANTLER = hexc(0xD8C69C)
NOSE = hexc(0x2A2420)
SPOT = hexc(0xF2E8D6)


def antler(part, root, s, size):
    """One antler: a beam sweeping up, back and out, with tines rising off its front."""
    r = Vector(root)
    beam = [r, r + Vector((-0.008, s * 0.012, 0.028)) * size, r + Vector((-0.02, s * 0.024, 0.052)) * size,
            r + Vector((-0.016, s * 0.034, 0.07)) * size]
    tube(part, beam, [0.003 * size, 0.0026 * size, 0.002 * size, 0.0], ANTLER, sides=4)
    for i, (fwd, up) in enumerate([(0.016, 0.014), (0.012, 0.018), (0.008, 0.014)]):
        base = beam[i].lerp(beam[i + 1], 0.6)
        tube(part, [base, base + Vector((fwd, s * 0.002, up)) * size], [0.0021 * size, 0.0], ANTLER, sides=3)


def deer(name, rng, stag=False, fawn=False, grazing=False, size=1.25):
    p = Part(name)
    coat = Pattern(Paint([DEER, DEER_DARK, DEER], under=DEER_BELLY), SPOT, scale=70, cover=0.12) if fawn else \
        Paint([DEER, DEER_DARK, DEER], under=DEER_BELLY)
    body = Body(length=0.12, height=0.088, girth=0.029, width=0.025, leg=0.0062, leg_spread=0.55, taper=0.6,
                neck=0.0125, head=(0.022, 0.012, 0.013), head_up=0.145, reach=0.03, snout=0.4)
    head, d = quadruped(p, body, coat, Paint([DEER, DEER_DARK]), NOSE, rng, grazing=grazing)
    blob(p, head_point(head, d, 0.02, 0, -0.002), (0.004, 0.004, 0.004), NOSE, subdivisions=0, wobble=0.0)
    ears(p, head, d, Paint(DEER_DARK), 1.0, forward=-0.01, side=0.008, up=0.006, lift=0.45, length=1.2)
    if stag:
        for s in (-1, 1):
            antler(p, head_point(head, d, -0.006, s * 0.006, 0.011), s, 1.0)
    blob(p, (-0.066, 0, 0.1), (0.008, 0.012, 0.012), SPOT, subdivisions=1, wobble=0.0)  # white rump and tail
    return scaled(p, size * (0.65 if fawn else 1.0))


def deers():
    """A stag with a full spread of antlers, a grazing doe and a spotted fawn."""
    rng = random.Random(202)
    return [
        at(deer("nodecal_stag", rng, stag=True), -0.06, 0.07, 0.35),
        at(deer("nodecal_doe", rng, grazing=True), 0.1, -0.05, math.pi + 0.4),
        at(deer("nodecal_fawn", rng, fawn=True), 0.13, 0.1, math.pi - 0.6),
    ]


# --- horses -------------------------------------------------------------------------------------------------------

CHESTNUT = hexc(0x9A5228)
CHESTNUT_DARK = hexc(0x7C4020)
BAY_DARK = hexc(0x3A2A22)
GREY_HORSE = hexc(0xDCD8D0)
GREY_HORSE_DARK = hexc(0xBDB8AE)
MANE_PALE = hexc(0xEEEAE2)
BLAZE = hexc(0xF2EEE6)
RAIL = hexc(0x8A6640)
RAIL_DARK = hexc(0x6E4E30)


def horse(name, rng, coat, mane, size=1.2, grazing=False, foal=False):
    """A horse: long legs, an arched neck carrying a mane, a long head and a flowing tail."""
    p = Part(name)
    body = Body(length=0.16, height=0.1, girth=0.035, width=0.029, leg=0.0082, leg_spread=0.55, taper=0.62,
                neck=0.017, head=(0.032, 0.012, 0.014), head_up=0.175, reach=0.045, snout=0.95)
    if foal:
        body.length, body.girth, body.width = 0.13, 0.028, 0.024
    head, d = quadruped(p, body, coat, coat, HOOF, rng, grazing=grazing)
    # The mane: a crest from behind the ears down the top of the neck to the withers.
    poll = head_point(head, d, -0.024, 0, 0.012)
    withers = Vector((body.length * 0.3, 0, body.height + body.girth * 0.95))
    crest = [poll, poll.lerp(withers, 0.35) + Vector((0, 0, 0.012)), poll.lerp(withers, 0.7) + Vector((0, 0, 0.008)),
             withers]
    tube(p, crest, [0.006, 0.008, 0.007, 0.004], mane, sides=5, squash=0.45)
    tube(p, [poll, head_point(head, d, 0.0, 0, 0.016)], [0.005, 0.0], mane, sides=4)  # forelock
    ears(p, head, d, coat, 1.0, forward=-0.022, side=0.006, up=0.01, lift=0.8)
    tail_root = Vector((-body.length * 0.48, 0, body.height + body.girth * 0.55))
    tube(p, [tail_root, tail_root + Vector((-0.02, 0, -0.01)), tail_root + Vector((-0.028, 0, -0.05)),
             tail_root + Vector((-0.026, 0, -0.075))], [0.006, 0.009, 0.008, 0.0], mane, sides=6)
    return scaled(p, size * (0.72 if foal else 1.0))


def fence(name, rng, length=0.2):
    """A short run of post-and-rail fence."""
    p = Part(name)
    wood = Paint([RAIL, RAIL_DARK], rng=rng)
    for x in (-length / 2, 0.0, length / 2):
        box(p, (x - 0.005, -0.005, FOOT), (x + 0.005, 0.005, 0.075), wood)
    for z in (0.03, 0.062):
        box(p, (-length / 2 - 0.008, -0.0025, z - 0.004), (length / 2 + 0.008, 0.0025, z + 0.004), wood)
    return p


def horses():
    """A chestnut, a grey grazing and a dark foal, by a run of post-and-rail fence."""
    rng = random.Random(203)
    chestnut = Paint([CHESTNUT, CHESTNUT_DARK, CHESTNUT], rng=rng)
    grey = Paint([GREY_HORSE, GREY_HORSE_DARK, GREY_HORSE], rng=rng)
    bay = Paint([CHESTNUT_DARK, BAY_DARK], rng=rng)
    return [
        at(horse("nodecal_chestnut", rng, chestnut, Paint(BAY_DARK)), -0.07, 0.03, 0.2),
        at(horse("nodecal_grey", rng, grey, Paint(MANE_PALE), grazing=True), 0.09, -0.1, math.pi + 0.3),
        at(horse("nodecal_foal", rng, bay, Paint(BAY_DARK), foal=True), 0.14, 0.09, math.pi - 0.5),
        at(fence("fence", rng), -0.06, 0.18, -0.15),
    ]


# --- pigs ---------------------------------------------------------------------------------------------------------

PIG = hexc(0xEAA8A0)
PIG_DARK = hexc(0xD58E88)
SNOUT = hexc(0xD47E80)
MUD = hexc(0x5E4630)
MUD_DARK = hexc(0x4C3826)


def pig(name, rng, size=1.2):
    """A pink pig: a round barrel of a body on short legs, a flat snout, floppy ears and a curly tail."""
    p = Part(name)
    skin = Paint([PIG, PIG_DARK, PIG], rng=rng)
    body = Body(length=0.12, height=0.056, girth=0.04, width=0.035, leg=0.0085, leg_spread=0.55, taper=0.75,
                neck=0.028, head=(0.026, 0.023, 0.022), head_up=0.06, reach=0.014, snout=0.25)
    head, d = quadruped(p, body, skin, skin, SNOUT, rng)
    tip = head_point(head, d, 0.028, 0, -0.002)
    cylinder(p, tip - d * 0.008, tip + d * 0.003, 0.011, 0.0105, SNOUT, sides=8)
    for s in (-1, 1):
        root = head_point(head, d, -0.008, s * 0.013, 0.017)
        hull(p, [root, root + Vector((-0.004, s * 0.004, 0.002)), root + Vector((0.016, s * 0.01, -0.002)),
                 root + Vector((0.002, s * 0.012, 0.004))], PIG_DARK)
    tail = Vector((-0.07, 0, 0.068))
    tube(p, [tail, tail + Vector((-0.008, 0, 0.006)), tail + Vector((-0.012, 0.005, 0.0)),
             tail + Vector((-0.008, 0.006, -0.004))], 0.0022, PIG_DARK, sides=4)
    return scaled(p, size)


def pigs():
    """A sow and two piglets by a wallow of mud."""
    rng = random.Random(204)
    mud = Part("ground_mud")
    plate(mud, Outline(0.075, 0.055, rng, sides=14, ragged=0.3), Paint([MUD, MUD_DARK], rng=rng), MUD_DARK)
    return [
        at(mud, -0.1, -0.08),
        at(pig("nodecal_sow", rng, 1.35), 0.02, 0.06, 0.3),
        at(pig("nodecal_piglet", rng, 0.7), 0.15, -0.06, math.pi + 0.5),
        at(pig("nodecal_piglet_b", rng, 0.65), -0.12, 0.1, -0.6),
    ]


# --- sheep --------------------------------------------------------------------------------------------------------

WOOL = hexc(0xF2EDE0)
WOOL_SHADE = hexc(0xDCD4C0)
SHEEP_FACE = hexc(0x2E2A28)


def sheep(name, rng, size=1.25, grazing=False):
    """A sheep: a fat, curly white fleece on thin black legs, with a black face and ears."""
    p = Part(name)
    wool = Paint([WOOL, WOOL_SHADE, WOOL], rng=rng)
    body = Body(length=0.115, height=0.068, girth=0.042, width=0.038, leg=0.0052, leg_spread=0.5, taper=0.85,
                neck=0.018, head=(0.02, 0.012, 0.014), head_up=0.09, reach=0.026, snout=0.55, wobble=0.12,
                frequency=5.0, subdivisions=3)
    head, d = quadruped(p, body, wool, Paint(SHEEP_FACE), SHEEP_FACE, rng, grazing=grazing, face=Paint(SHEEP_FACE))
    blob(p, head_point(head, d, -0.014, 0, 0.008), (0.012, 0.013, 0.01), wool, subdivisions=1, wobble=0.1)
    for s in (-1, 1):
        root = head_point(head, d, -0.008, s * 0.01, 0.004)
        tube(p, [root, root + Vector((-0.004, s * 0.014, -0.004))], [0.0035, 0.0], SHEEP_FACE, sides=4, squash=0.5)
    return scaled(p, size)


def flock():
    """A small flock: three sheep, one grazing, and a lamb."""
    rng = random.Random(205)
    return [
        at(sheep("nodecal_ewe", rng), -0.09, 0.07, 0.3),
        at(sheep("nodecal_ewe_b", rng, grazing=True), 0.08, 0.12, math.pi - 0.2),
        at(sheep("nodecal_ram", rng, 1.3, grazing=True), 0.05, -0.09, 0.6),
        at(sheep("nodecal_lamb", rng, 0.75), -0.15, -0.1, -0.4),
    ]


# --- elephants ------------------------------------------------------------------------------------------------------

ELEPHANT = hexc(0x8E8882)
ELEPHANT_DARK = hexc(0x76716C)
ELEPHANT_EAR = hexc(0x9C928C)
TUSK = hexc(0xF6F0DE)
NAIL = hexc(0xC8C0B4)


def elephant(name, rng, size=1.0, tusks=True):
    """An elephant: a great domed body on pillar legs, big fanned ears, a hanging trunk and curving white tusks."""
    p = Part(name)
    hide = Paint([ELEPHANT, ELEPHANT_DARK, ELEPHANT], rng=rng)
    body = Body(length=0.19, height=0.12, girth=0.056, width=0.047, leg=0.019, leg_spread=0.55, taper=0.92,
                neck=0.036, head=(0.038, 0.034, 0.04), head_up=0.155, reach=0.01, snout=0.25, hump=0.012)
    head, d = quadruped(p, body, hide, hide, NAIL, rng)
    for s in (-1, 1):
        ear = head_point(head, d, -0.012, s * 0.04, -0.004)
        blob(p, ear, (0.024, 0.007, 0.032), ELEPHANT_EAR, subdivisions=2, wobble=0.08, seed=rng.random())
    root = head_point(head, d, 0.03, 0, -0.012)
    trunk = [root, root + Vector((0.016, 0, -0.03)), root + Vector((0.018, 0, -0.07)), root + Vector((0.026, 0, -0.1)),
             root + Vector((0.036, 0, -0.105))]
    tube(p, trunk, [0.015, 0.012, 0.009, 0.007, 0.006], hide, sides=7)
    if tusks:
        for s in (-1, 1):
            base = head_point(head, d, 0.022, s * 0.014, -0.026)
            tube(p, [base, base + Vector((0.024, s * 0.006, -0.018)), base + Vector((0.05, s * 0.004, -0.006))],
                 [0.0058, 0.0045, 0.0], TUSK, sides=6)
    tail = Vector((-0.094, 0, 0.15))
    tube(p, [tail, tail + Vector((-0.006, 0, -0.03)), tail + Vector((-0.004, 0, -0.06))], [0.004, 0.003, 0.002],
         hide, sides=4)
    return scaled(p, size)


def ivory():
    """A tusked elephant and her calf."""
    rng = random.Random(206)
    return [
        at(elephant("nodecal_elephant", rng, 1.25), -0.04, 0.04, 0.3),
        at(elephant("nodecal_calf", rng, 0.7, tusks=False), 0.14, -0.1, 0.7),
    ]


# --- fur ----------------------------------------------------------------------------------------------------------

FOX = hexc(0xD2702C)
FOX_DARK = hexc(0xB45A22)
FOX_WHITE = hexc(0xF2EDE4)
FOX_LEG = hexc(0x3A2A22)
BEAVER = hexc(0x6E4A2C)
BEAVER_DARK = hexc(0x573822)
HOOP_WOOD = hexc(0xB0905E)


def fox(name, rng, size=1.3):
    """A red fox: slim body on dark legs, white chest, pointed ears and muzzle, and a big brush tipped white."""
    p = Part(name)
    coat = Paint([FOX, FOX_DARK, FOX], under=FOX_WHITE, rng=rng)
    body = Body(length=0.1, height=0.052, girth=0.021, width=0.019, leg=0.0048, leg_spread=0.55, taper=0.75,
                neck=0.012, head=(0.018, 0.013, 0.012), head_up=0.072, reach=0.026, snout=0.15)
    head, d = quadruped(p, body, coat, Paint(FOX_LEG), FOX_LEG, rng)
    spindle(p, head_point(head, d, 0.006, 0, -0.003), head_point(head, d, 0.032, 0, -0.006), 0.0075, coat, sides=5,
            bulge=0.2)
    blob(p, head_point(head, d, 0.032, 0, -0.005), (0.0028,) * 3, FOX_LEG, subdivisions=0, wobble=0.0)
    ears(p, head, d, Paint(FOX_DARK), 1.0, forward=-0.006, side=0.007, up=0.008, lift=0.8, length=1.1)
    root = Vector((-0.052, 0, 0.06))
    spindle(p, root, root + Vector((-0.055, 0, -0.012)), 0.013, coat, sides=7, bulge=0.55)
    spindle(p, root + Vector((-0.045, 0, -0.01)), root + Vector((-0.06, 0, -0.0125)), 0.007, FOX_WHITE, sides=6,
            bulge=0.3)
    return scaled(p, size)


def pelt(part, centre, r, colour, rng, hoop=True):
    """A pelt stretched on a round willow hoop, standing on edge facing the camera."""
    c = Vector(centre)
    mark = part.mark()
    blob(part, (0, 0, 0), (r * 0.82, r * 0.82, 0.004), colour, subdivisions=2, wobble=0.08, seed=rng.random())
    if hoop:
        ring = [Vector((math.cos(a) * r, math.sin(a) * r, 0)) for a in (k * math.tau / 12 for k in range(13))]
        tube(part, ring, 0.0025, HOOP_WOOD, sides=4, cap=False)
    part.transformed(Matrix.Translation(c) @ Matrix.Rotation(math.pi / 2 - 0.25, 4, "X"), mark)


def pelts(name, rng):
    """Pelts drying on hoops, leaning against a rail."""
    p = Part(name)
    for x in (-0.07, 0.07):
        tube(p, [(x, 0.012, FOOT), (x, 0.012, 0.085)], 0.0045, RAIL_DARK, sides=5)
    tube(p, [(-0.078, 0.012, 0.08), (0.078, 0.012, 0.08)], 0.004, RAIL_DARK, sides=5)
    for x, r, colour in [(-0.045, 0.036, Paint([BEAVER, BEAVER_DARK])), (0.0, 0.04, Paint([FOX, FOX_DARK])),
                         (0.046, 0.034, Paint([FOX_WHITE, hexc(0xDCD6CC)]))]:
        pelt(p, (x, 0.0, r + 0.002), r, colour, rng)
    return p


def fur():
    """A red fox by a trapper's rail of pelts stretched on hoops."""
    rng = random.Random(207)
    return [
        at(fox("nodecal_fox", rng), 0.07, -0.1, 0.5),
        at(pelts("pelts", rng), -0.03, 0.08, 0.1),
    ]


MODELS = {
    "cow": cattle,
    "deer": deers,
    "horse": horses,
    "pig": pigs,
    "sheep": flock,
    "ivory": ivory,
    "fur": fur,
}
