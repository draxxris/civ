"""Animal resources: cattle, deer, horses, pigs, sheep, elephants (ivory) and fur.

Conventions for resource models are in bonus_kit.py. Animals face +X, stand with their hooves at y = 0 and are
named "nodecal..." so no worn earth patch is drawn under them. They are a little larger than the pasture's
horses so that a herd reads from the default camera.
"""
import random

from .bonus_kit import blobby, leg, puffs, scaled, spindle  # noqa: F401
from .farming import HORSE_BAY, HORSE_DARK, HORSE_GREY, blob, horse, tube
from .kit import *  # noqa: F401,F403
from .kit import FOOT, Part, at
from .water import solid

COW_WHITE = srgb(0xEDE8DE)
COW_BLACK = srgb(0x2A2622)
COW_BROWN = srgb(0x8A5A34)
COW_BROWN_LIGHT = srgb(0xA8744A)
HORN = srgb(0xE2D6B4)
MUZZLE = srgb(0xD8A890)
DEER = srgb(0x9A6438)
DEER_DARK = srgb(0x6E4426)
DEER_BELLY = srgb(0xD8C4A0)
ANTLER = srgb(0xD6C29A)
PIG = srgb(0xE8A6A0)
PIG_DARK = srgb(0xC88480)
MUD = srgb(0x5A4430)
MUD_WET = srgb(0x4A3826)
WOOL = srgb(0xF0ECE0)
WOOL_SHADE = srgb(0xDCD6C6)
SHEEP_FACE = srgb(0x2E2A26)
ELEPHANT = srgb(0x8A8580)
ELEPHANT_DARK = srgb(0x6E6A66)
IVORY = srgb(0xF2EAD4)
FOX = srgb(0xC8662A)
FOX_LIGHT = srgb(0xF2E8DA)
FOX_DARK = srgb(0x3A2A20)
PELT = srgb(0x7A5232)
PELT_LIGHT = srgb(0x9A7048)
HAY_PILE = srgb(0xD8B85E)


def cow(name, coat=COW_WHITE, patch=COW_BLACK, grazing=False):
    """A stocky cow facing +X: a deep body, short legs, a broad head with horns, patches on the coat."""
    p = Part(name)
    blobby(p, 0.0, 0.05, 0.0, 0.05, 0.022, 0.02, coat, n=8, rings=3)
    blobby(p, -0.012, 0.062, 0.012, 0.022, 0.012, 0.012, patch, n=6, rings=2)
    blobby(p, 0.024, 0.056, -0.012, 0.016, 0.012, 0.01, patch, n=6, rings=2)
    for x in (-0.034, 0.032):
        for z in (-0.011, 0.011):
            leg(p, x, 0.04, z, coat, t=0.009, hoof=COW_BLACK)
    if grazing:
        neck, head = (0.054, 0.04, 0.0), (0.07, 0.016, 0.0)
    else:
        neck, head = (0.056, 0.062, 0.0), (0.074, 0.06, 0.0)
    beam(p, (0.04, 0.056, 0.0), neck, 0.022, coat)
    blobby(p, head[0], head[1], 0.0, 0.016, 0.012, 0.011, patch if grazing else coat, n=6, rings=2)
    box(p, head[0] + 0.01, head[1] - 0.01, -0.007, head[0] + 0.02, head[1] + 0.0, 0.007, MUZZLE)
    for sz in (-1, 1):
        beam(p, (head[0] - 0.004, head[1] + 0.008, sz * 0.008), (head[0] - 0.002, head[1] + 0.02, sz * 0.02), 0.004,
             HORN)
    beam(p, (-0.048, 0.06, 0.0), (-0.058, 0.026, 0.0), 0.004, coat)
    return p


def hay(name, rng):
    p = Part(name)
    blob(p, 0.03, 0.022, HAY_PILE, rng, y=0.004, n=8)
    blobby(p, 0.0, 0.006, 0.0, 0.024, 0.012, 0.018, HAY_PILE, top=srgb(0xE6CA70), n=7, rings=2)
    return p


def cattle():
    rng = random.Random(201)
    return [
        at(cow("nodecal_cow_a"), -0.06, -0.04, 0.3),
        at(cow("nodecal_cow_b", COW_BROWN, COW_BROWN_LIGHT, grazing=True), 0.09, 0.05, 2.6),
        at(hay("hay", rng), 0.1, -0.1),
    ]


def deer_body(name, stag=True, grazing=False):
    """A slender deer facing +X with a pale rump and, on a stag, branching antlers."""
    p = Part(name)
    blobby(p, 0.0, 0.058, 0.0, 0.04, 0.016, 0.014, DEER, DEER_DARK, n=8, rings=3, top_cut=0.9)
    blobby(p, -0.036, 0.058, 0.0, 0.008, 0.012, 0.01, DEER_BELLY, n=5, rings=2)
    for x in (-0.026, 0.026):
        for z in (-0.007, 0.007):
            leg(p, x, 0.05, z, DEER, t=0.0055, hoof=DEER_DARK)
    if grazing:
        neck, head = (0.048, 0.04, 0.0), (0.06, 0.018, 0.0)
    else:
        neck, head = (0.046, 0.088, 0.0), (0.06, 0.092, 0.0)
    beam(p, (0.03, 0.062, 0.0), neck, 0.012, DEER)
    blobby(p, head[0], head[1], 0.0, 0.014, 0.008, 0.008, DEER, n=6, rings=2)
    box(p, head[0] + 0.01, head[1] - 0.004, -0.003, head[0] + 0.016, head[1] + 0.002, 0.003, DEER_DARK)
    for sz in (-1, 1):  # ears
        beam(p, (head[0] - 0.006, head[1] + 0.004, sz * 0.004), (head[0] - 0.012, head[1] + 0.012, sz * 0.014), 0.004,
             DEER_DARK)
        if stag:
            base = (head[0] - 0.004, head[1] + 0.006, sz * 0.004)
            tip = (head[0] - 0.014, head[1] + 0.05, sz * 0.03)
            beam(p, base, tip, 0.0035, ANTLER)
            for t, (dx, dy, dz) in ((0.35, (0.014, 0.012, 0.004)), (0.65, (0.012, 0.014, 0.0)),
                                    (0.85, (-0.006, 0.014, 0.006))):
                fork = tuple(base[i] + (tip[i] - base[i]) * t for i in range(3))
                beam(p, fork, (fork[0] + dx, fork[1] + dy, fork[2] + sz * dz), 0.003, ANTLER)
    return p


def deer():
    return [
        at(deer_body("nodecal_stag"), -0.04, -0.03, 0.4),
        at(deer_body("nodecal_doe", stag=False, grazing=True), 0.1, 0.07, 2.4),
        at(scaled(deer_body("nodecal_fawn", stag=False), 0.65), 0.0, 0.12, 2.9),
    ]


def horses():
    rng = random.Random(203)
    return [
        at(scaled(horse("nodecal_horse_a", coat=HORSE_BAY, mane=HORSE_DARK), 1.25), -0.06, -0.04, 0.35),
        at(scaled(horse("nodecal_horse_b", coat=HORSE_GREY, mane=HORSE_DARK, grazing=True), 1.15), 0.1, 0.06, 2.5),
        at(scaled(horse("nodecal_foal", coat=HORSE_DARK, mane=HORSE_DARK), 0.8), 0.02, 0.13, 2.9),
        at(hay("hay", rng), 0.12, -0.1),
    ]


def pig_body(name, grazing=False):
    """A round pink pig facing +X: a barrel body, short legs, a snout and a curly tail."""
    p = Part(name)
    blobby(p, 0.0, 0.032, 0.0, 0.036, 0.02, 0.019, PIG, n=8, rings=3)
    for x in (-0.022, 0.022):
        for z in (-0.009, 0.009):
            leg(p, x, 0.022, z, PIG_DARK, t=0.008)
    hy = 0.022 if grazing else 0.034
    blobby(p, 0.038, hy, 0.0, 0.014, 0.013, 0.014, PIG, n=6, rings=2)
    tube(p, (0.046, hy - 0.002, 0.0), (0.058, hy - 0.003, 0.0), 0.0075, PIG, end=PIG_DARK)  # snout
    for sz in (-1, 1):  # ears
        beam(p, (0.034, hy + 0.01, sz * 0.007), (0.036, hy + 0.018, sz * 0.014), 0.005, PIG_DARK)
    beam(p, (-0.036, 0.038, 0.0), (-0.042, 0.046, 0.004), 0.0028, PIG_DARK)
    beam(p, (-0.042, 0.046, 0.004), (-0.046, 0.04, -0.002), 0.0028, PIG_DARK)
    return p


def mud(name, rng):
    p = Part(name)
    blob(p, 0.05, 0.036, MUD, rng, y=0.003, n=9)
    blob(p, 0.03, 0.02, MUD_WET, rng, at=(0.008, 0, 0.004), y=0.004, n=8)
    return p


def pigs():
    rng = random.Random(204)
    return [
        at(mud("ground_mud", rng), 0.02, 0.0),
        at(pig_body("nodecal_pig_a"), -0.06, -0.03, 0.4),
        at(pig_body("nodecal_pig_b", grazing=True), 0.08, 0.04, 2.7),
        at(scaled(pig_body("nodecal_piglet"), 0.6), 0.0, 0.1, 1.2),
    ]


def sheep_body(name, rng, grazing=False):
    """A woolly sheep facing +X: a lumpy fleece, a dark face and thin dark legs."""
    p = Part(name)
    blobby(p, 0.0, 0.04, 0.0, 0.032, 0.02, 0.022, WOOL, WOOL_SHADE, n=8, rings=3, top_cut=0.95)
    puffs(p, [(-0.014, 0.056, 0.008, 0.012), (0.012, 0.058, -0.006, 0.012), (-0.02, 0.05, -0.012, 0.01)], WOOL, rng)
    for x in (-0.02, 0.02):
        for z in (-0.009, 0.009):
            leg(p, x, 0.026, z, SHEEP_FACE, t=0.005)
    hy = 0.022 if grazing else 0.05
    hx = 0.04 if grazing else 0.038
    blobby(p, hx, hy, 0.0, 0.013, 0.009, 0.008, SHEEP_FACE, n=6, rings=2)
    blobby(p, hx - 0.008, hy + 0.006, 0.0, 0.009, 0.007, 0.009, WOOL, n=5, rings=2)
    for sz in (-1, 1):
        beam(p, (hx - 0.004, hy + 0.002, sz * 0.006), (hx - 0.006, hy - 0.002, sz * 0.016), 0.004, SHEEP_FACE)
    return p


def sheep():
    rng = random.Random(205)
    return [
        at(sheep_body("nodecal_sheep_a", rng), -0.07, -0.04, 0.3),
        at(sheep_body("nodecal_sheep_b", rng, grazing=True), 0.07, -0.06, 2.8),
        at(sheep_body("nodecal_sheep_c", rng, grazing=True), 0.03, 0.08, 1.4),
        at(scaled(sheep_body("nodecal_lamb", rng), 0.7), -0.06, 0.1, 0.9),
    ]


def elephant(name, tusks=True):
    """An elephant facing +X: a big rounded body on pillar legs, a domed head, fan ears, a hanging trunk and
    long curved ivory tusks."""
    p = Part(name)
    blobby(p, 0.0, 0.08, 0.0, 0.056, 0.036, 0.032, ELEPHANT, n=9, rings=3)
    for x in (-0.034, 0.03):
        for z in (-0.018, 0.018):
            beam(p, (x, 0.07, z), (x, -0.006, z), 0.017, ELEPHANT)
            box(p, x - 0.01, -0.006, z - 0.01, x + 0.01, 0.006, z + 0.01, ELEPHANT_DARK)
    blobby(p, 0.06, 0.098, 0.0, 0.026, 0.028, 0.024, ELEPHANT, n=7, rings=2)
    for sz in (-1, 1):  # ears
        solid(p, [(0.05, 0.12, sz * 0.02), (0.05, 0.072, sz * 0.022), (0.044, 0.112, sz * 0.05),
                  (0.042, 0.08, sz * 0.048), (0.054, 0.1, sz * 0.022)], ELEPHANT_DARK)
    trunk = [(0.08, 0.092, 0.0), (0.094, 0.07, 0.0), (0.1, 0.044, 0.0), (0.098, 0.02, 0.004), (0.104, 0.008, 0.008)]
    for k in range(len(trunk) - 1):
        beam(p, trunk[k], trunk[k + 1], 0.016 - 0.0025 * k, ELEPHANT)
    if tusks:
        for sz in (-1, 1):
            curve = [(0.078, 0.078, sz * 0.014), (0.096, 0.058, sz * 0.02), (0.114, 0.054, sz * 0.022),
                     (0.126, 0.066, sz * 0.02)]
            for k in range(len(curve) - 1):
                beam(p, curve[k], curve[k + 1], 0.007 - 0.0015 * k, IVORY)
    beam(p, (-0.054, 0.09, 0.0), (-0.064, 0.05, 0.0), 0.004, ELEPHANT_DARK)
    return p


def ivory():
    return [
        at(elephant("nodecal_elephant"), -0.04, -0.03, 0.35),
        at(scaled(elephant("nodecal_calf", tusks=False), 0.6), 0.11, 0.08, 0.6),
    ]


def fox(name, rng):
    """A red fox facing +X with a white chest and a long brush tail."""
    p = Part(name)
    blobby(p, 0.0, 0.032, 0.0, 0.03, 0.012, 0.011, FOX, n=7, rings=2)
    for x in (-0.018, 0.018):
        for z in (-0.006, 0.006):
            leg(p, x, 0.028, z, FOX_DARK, t=0.0045)
    blobby(p, 0.034, 0.044, 0.0, 0.013, 0.01, 0.009, FOX, n=6, rings=2)
    solid(p, [(0.044, 0.04, -0.004), (0.044, 0.04, 0.004), (0.062, 0.036, 0.0), (0.044, 0.048, 0.0)], FOX_LIGHT)
    for sz in (-1, 1):
        solid(p, [(0.03, 0.05, sz * 0.002), (0.03, 0.05, sz * 0.009), (0.036, 0.05, sz * 0.006),
                  (0.032, 0.066, sz * 0.007)],
              FOX_DARK)
    spindle(p, (-0.024, 0.036, 0.0), (-0.07, 0.028, 0.006), 0.011, FOX, bulge=0.55)
    blobby(p, -0.068, 0.028, 0.006, 0.008, 0.006, 0.006, FOX_LIGHT, n=5, rings=2)
    return p


def pelt_frame(name):
    """A hide stretched on a round willow frame, propped on two stakes."""
    p = Part(name)
    for sx in (-1, 1):
        beam(p, (sx * 0.03, FOOT / 2, -0.012), (sx * 0.026, 0.08, 0.0), 0.005, TIMBER)
    n = 10
    for k in range(n):
        a0, a1 = k * math.tau / n, (k + 1) * math.tau / n
        beam(p, (math.cos(a0) * 0.03, 0.044 + math.sin(a0) * 0.032, 0.0),
             (math.cos(a1) * 0.03, 0.044 + math.sin(a1) * 0.032, 0.0), 0.0035, TIMBER_LIGHT)
    pts = [(math.cos(k * math.tau / 8) * 0.024, 0.044 + math.sin(k * math.tau / 8) * 0.026, z)
           for k in range(8) for z in (-0.0015, 0.0015)]
    solid(p, pts, PELT, PELT_LIGHT)
    return p


def fur():
    rng = random.Random(207)
    bundle = Part("pelts")
    for i in range(3):
        y = 0.004 * i
        box(bundle, -0.024 + 0.002 * i, FOOT / 2 if i == 0 else y, -0.016, 0.024 - 0.002 * i, y + 0.005, 0.016,
            PELT if i % 2 else PELT_LIGHT)
    return [
        at(scaled(fox("nodecal_fox", rng), 1.3), -0.06, 0.02, 0.5),
        at(scaled(fox("nodecal_fox_b", rng), 1.1), 0.1, 0.1, 2.6),
        at(pelt_frame("frame"), 0.07, -0.08, -0.3),
        at(bundle, -0.06, -0.1, 0.2),
    ]


MODELS = {
    "cow": cattle,
    "deer": deer,
    "horse": horses,
    "pig": pigs,
    "sheep": sheep,
    "ivory": ivory,
    "fur": fur,
}
