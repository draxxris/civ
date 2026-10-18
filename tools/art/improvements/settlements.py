"""Cottage, hamlet, village and town: the commerce settlement chain."""
import random

from .kit import *  # noqa: F401,F403
from .kit import FOOT, Part, at


def church(name, w=0.2, d=0.11, wall=0.1, tower=0.24, spire=0.12):
    """Stone nave along X, slate roof, square west tower with a spire at the +X end."""
    p = Part(name)
    hw, hd = w / 2, d / 2
    box(p, -hw - 0.008, FOOT, -hd - 0.008, hw + 0.008, 0.012, hd + 0.008, STONE_DARK, top=STONE)
    box(p, -hw, 0.012, -hd, hw, wall, hd, CHURCH_STONE)
    for i in range(3):  # tall lancet windows along the front
        x = -hw + (i + 0.6) * (w - 0.06) / 3
        box(p, x - 0.008, 0.035, hd - 0.002, x + 0.008, 0.08, hd + 0.003, WINDOW)
    top = prism_roof(p, w, d, wall, 0.08, 0.012, SLATE, CHURCH_STONE)
    box(p, -hw - 0.012, top - 0.01, -0.008, hw - 0.01, top + 0.004, 0.008, SLATE_DARK)
    t = 0.04  # tower half-width
    tx = hw + t - 0.01
    box(p, tx - t, FOOT, -t, tx + t, tower, t, CHURCH_STONE, top=SLATE_DARK)
    box(p, tx - 0.016, 0.012, t - 0.002, tx + 0.016, 0.07, t + 0.004, DOOR)
    for face in ((0, 1), (1, 0)):  # belfry openings on the two visible faces
        cx, cz = tx + face[0] * (t - 0.003), face[1] * (t - 0.003)
        ex, ez = (0.004, 0.012) if face[0] else (0.012, 0.004)
        box(p, cx - ex, tower - 0.06, cz - ez, cx + ex, tower - 0.025, cz + ez, DARK)
    box(p, tx - t - 0.006, tower - 0.01, -t - 0.006, tx + t + 0.006, tower + 0.006, t + 0.006, STONE)
    cylinder(p, t * 1.3, tower + 0.006, tower + 0.006 + spire, SLATE, at=(tx, 0, 0), n=4, r_top=0.0,
             phase=math.pi / 4)
    return p


def stall(part, x, z, cloth, yaw=0.0):
    box(part, -0.03, 0.0, -0.02, 0.03, 0.03, 0.02, TIMBER_LIGHT, at=(x, 0, z), yaw=yaw)
    for sx in (-1, 1):
        box(part, sx * 0.03 - 0.003, 0.0, -0.025, sx * 0.03 + 0.003, 0.055, -0.019, TIMBER, at=(x, 0, z), yaw=yaw)
    prism_roof(part, 0.07, 0.05, 0.05, 0.022, 0.004, cloth, cloth, at=(x, 0, z), yaw=yaw)


def market(name, r=0.13):
    """A paved square with a few awninged stalls; tilts with the ground."""
    p = Part(name)
    n = 8
    pts = [(math.cos(i * math.tau / n + math.pi / 8) * r, y, math.sin(i * math.tau / n + math.pi / 8) * r)
           for y in (FOOT / 2, 0.006) for i in range(n)]
    faces = [list(range(n)), list(range(n, 2 * n))] + [[i, (i + 1) % n, n + (i + 1) % n, n + i] for i in range(n)]
    p.hull(pts, faces, [PAVING, PAVING] + [STONE_DARK] * n)
    stall(p, -0.06, -0.06, CLOTH_RED, 0.2)
    stall(p, 0.065, -0.04, CLOTH_BLUE, -0.3)
    stall(p, -0.02, 0.07, CLOTH_CREAM, 0.1)
    return p


def cottage():
    rng = random.Random(11)
    return [
        at(house("house", hip=0.035, seed=0), 0.14, -0.24, 0.25),
        at(garden("ground_garden", rng=rng), -0.3, 0.08, -0.15),
        at(haystack("haystack"), 0.4, 0.16),
        at(woodpile("woodpile"), 0.44, -0.12, 1.2),
    ]


def hamlet():
    rng = random.Random(23)
    return [
        at(house("house_a", seed=0), 0.05, -0.44, 0.2),
        at(house("house_b", w=0.13, d=0.1, hip=0.03, chimney=False, seed=1), -0.44, -0.12, -0.5),
        at(house("house_c", w=0.16, d=0.12, wall=0.08, walls=PLASTER_WARM, seed=2), 0.46, 0.08, 0.7),
        at(well("well"), -0.02, 0.02),
        at(garden("ground_garden", w=0.22, d=0.14, rng=rng), -0.2, 0.42, 0.1),
        at(haystack("haystack"), 0.24, 0.46),
        at(haystack("haystack_b", r=0.03), 0.38, 0.4),
    ]


def village():
    rng = random.Random(37)
    return [
        at(church("church"), -0.06, -0.46),
        at(house("house_a", hip=0.03, seed=0), -0.48, -0.12, -0.45),
        at(house("house_b", roof=TILE, roof_end=TILE_DARK, walls=PLASTER_WARM, seed=1), 0.5, -0.1, 0.5),
        at(house("house_c", w=0.14, seed=2), -0.18, 0.42, 0.15),
        at(house("house_d", w=0.12, d=0.1, roof=TILE, roof_end=TILE_DARK, chimney=False, seed=3), 0.28, 0.46, -0.3),
        at(well("well"), 0.06, 0.02),
        at(garden("ground_garden", w=0.18, d=0.12, rows=3, rng=rng), -0.55, 0.28, 0.3),
        at(fence("fence"), 0.58, 0.28, 1.1),
        at(haystack("haystack"), 0.5, 0.36),
    ]


def town():
    rng = random.Random(41)
    two = dict(w=0.12, d=0.1, wall=0.13, rise=0.07, roof=TILE, roof_end=TILE_DARK, upper=True)
    return [
        at(church("church", w=0.22, d=0.12, wall=0.11, tower=0.3, spire=0.15), -0.12, -0.5),
        at(market("ground_market"), 0.02, 0.04),
        at(house("house_a", walls=PLASTER, seed=0, **two), 0.42, -0.34, 0.35),
        at(house("house_b", walls=PLASTER_WARM, seed=1, **two), 0.58, 0.04, 1.2),
        at(house("house_c", walls=PLASTER, seed=2, **two), 0.36, 0.44, -0.4),
        at(house("house_d", walls=PLASTER_WARM, seed=3, **two), -0.12, 0.52, 0.1),
        at(house("house_e", walls=PLASTER, seed=4, **two), -0.52, 0.24, -0.6),
        at(house("house_f", w=0.14, d=0.11, roof=SLATE, roof_end=SLATE_DARK, walls=PLASTER, seed=5), -0.56, -0.18, -0.2),
        at(well("well"), 0.22, -0.1),
    ]


MODELS = {
    "improvement_improvement_cottage": cottage,
    "improvement_improvement_hamlet": hamlet,
    "improvement_improvement_village": village,
    "improvement_improvement_town": town,
}
