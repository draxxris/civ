"""Shared kit for the procedural improvement models: palette, primitives, common pieces and the GLB writer.

Units are world units: a hex tile has corner radius 1 (pointy-top; inner radius ~0.87); scatter trees are ~0.4 tall
and units ~0.85. Pieces are authored at roughly life size and multiplied by SCALE when written; layout offsets
passed to at() are world units (not scaled). +Y is up. The default camera looks toward -Z, so fronts face +Z.

Each top-level glTF node is one *part*. The renderer seats every part on the terrain at its own origin, so a
building on a slope stands upright at its local ground height (sea level, y = 0, on water tiles). A part whose name
starts with "ground" is tilted to the surface normal instead (garden plots, rails, fields). Buildings carry a
foundation that reaches below y = 0 (FOOT) so no gap shows on a slope. The renderer draws a soft earth patch
under the vertices of every other part that lie at or below y = 0.06; name a part "nodecal..." to skip it. Geometry is flat shaded with per-face
vertex colours (stored as sRGB; see srgb()) and one white material. Convex pieces orient their faces
automatically (Part.hull), so build anything solid from convex hulls.
"""
from __future__ import annotations

import math
import random  # noqa: F401  (re-exported for model modules)
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from glb_writer import Glb  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parents[3]
OUT = REPO_ROOT / "src/Mods/World/art/models"
FOOT = -0.05  # foundations reach this far below the seat point (before SCALE)
# Pieces are authored life-ish size, then enlarged so they read at the game's camera distance next to 0.85-tall units.
SCALE = 2.0


def srgb(hex_colour: int) -> tuple[float, float, float]:
    """sRGB hex to 0..1 RGB. Stored as-is: Godot's runtime glTF import plus a VertexColorUseAsAlbedo material shows
    COLOR_0 values unconverted, so authoring them in sRGB keeps the swatches true on screen."""
    return tuple(((hex_colour >> shift) & 0xFF) / 255 for shift in (16, 8, 0))


PLASTER = srgb(0xE6D8B8)
PLASTER_WARM = srgb(0xDCC59A)
TIMBER = srgb(0x5E4128)
TIMBER_LIGHT = srgb(0x86603C)
THATCH = srgb(0xC8A253)
THATCH_DARK = srgb(0xA4813C)
STONE = srgb(0x8F877C)
STONE_DARK = srgb(0x6E675F)
DOOR = srgb(0x4A2F1C)
WINDOW = srgb(0x2E2A26)
SOIL = srgb(0x6A4B30)
CROP = srgb(0x6F9A3A)
CROP_DARK = srgb(0x557A2C)
HAY = srgb(0xD8B85E)
ROCK = srgb(0x837A6C)
ROCK_DARK = srgb(0x6A6156)
SPOIL = srgb(0x77706A)
DARK = srgb(0x120E0B)
IRON = srgb(0x3C3C3E)
ORE = srgb(0xA9774A)
WATER = srgb(0x3D6F8F)
TILE = srgb(0xA9573A)
TILE_DARK = srgb(0x87432B)
SLATE = srgb(0x5F6670)
SLATE_DARK = srgb(0x4A5059)
CHURCH_STONE = srgb(0xC9BFA8)
PAVING = srgb(0xA59C8C)
CLOTH_RED = srgb(0xB4473A)
CLOTH_BLUE = srgb(0x4B74A3)
CLOTH_CREAM = srgb(0xE8DDBE)
QUARRY = srgb(0xD3C9B3)
QUARRY_SHADE = srgb(0xB3A88F)
QUARRY_DARK = srgb(0x968B74)
ROPE = srgb(0x3A3026)
BOARDS = srgb(0x7A5434)
BOARDS_DARK = srgb(0x5A3B24)
HORSE_BAY = srgb(0x7B4A2A)
HORSE_DARK = srgb(0x3B2618)
HORSE_GREY = srgb(0xB9B2A6)
PADDOCK = srgb(0x8C7A4E)
SHINGLE = srgb(0x857767)
SHINGLE_DARK = srgb(0x6B5F52)


class Part:
    """Triangle soup with flat normals and per-face colours."""

    def __init__(self, name: str):
        self.name = name
        self.tris: list[tuple[np.ndarray, np.ndarray, np.ndarray, tuple]] = []

    def tri(self, a, b, c, colour, centre=None):
        a, b, c = (np.asarray(p, dtype=float) for p in (a, b, c))
        if centre is not None:
            # Convex pieces: orient every face away from the piece centre.
            n = np.cross(b - a, c - a)
            if np.dot(n, (a + b + c) / 3 - np.asarray(centre, dtype=float)) < 0:
                b, c = c, b
        self.tris.append((a, b, c, colour))

    def quad(self, a, b, c, d, colour, centre=None):
        self.tri(a, b, c, colour, centre)
        self.tri(a, c, d, colour, centre)

    def hull(self, points, faces, colours):
        """A convex solid from points and polygon index lists; colours is one colour or one per face."""
        pts = [np.asarray(p, dtype=float) for p in points]
        centre = sum(pts) / len(pts)
        for i, face in enumerate(faces):
            colour = colours[i] if isinstance(colours, list) else colours
            for k in range(1, len(face) - 1):
                self.tri(pts[face[0]], pts[face[k]], pts[face[k + 1]], colour, centre)


def rot_y(p, yaw):
    c, s = math.cos(yaw), math.sin(yaw)
    x, y, z = p
    return (c * x + s * z, y, -s * x + c * z)


def place(points, at=(0, 0, 0), yaw=0.0):
    return [tuple(np.add(rot_y(p, yaw), at)) for p in points]


BOX_FACES = [[0, 1, 2, 3], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]]


def box(part, x0, y0, z0, x1, y1, z1, colour, at=(0, 0, 0), yaw=0.0, top=None):
    pts = [(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1),
           (x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1)]
    colours = [colour, top or colour, colour, colour, colour, colour]
    part.hull(place(pts, at, yaw), BOX_FACES, colours)


def prism_roof(part, w, d, y, rise, over, colour, gable_colour, at=(0, 0, 0), yaw=0.0, hip=0.0):
    """Ridge along X. hip > 0 pulls the ridge ends in (a hipped roof)."""
    hw, hd = w / 2 + over, d / 2 + over
    rw = max(hw - hip, 0.001)
    eave = y - over * rise / (d / 2 + over) * 0.6
    pts = [(-hw, eave, -hd), (hw, eave, -hd), (hw, eave, hd), (-hw, eave, hd),
           (-rw, y + rise, 0), (rw, y + rise, 0)]
    faces = [[0, 1, 2, 3], [3, 2, 5, 4], [1, 0, 4, 5], [0, 3, 4], [2, 1, 5]]
    end = colour if hip > 0 else gable_colour
    part.hull(place(pts, at, yaw), faces, [colour, colour, colour, end, end])
    return y + rise


def cylinder(part, r, y0, y1, colour, at=(0, 0, 0), n=8, r_top=None, top=None, phase=0.0):
    r_top = r if r_top is None else r_top
    pts = []
    for i in range(n):
        a = phase + i * math.tau / n
        pts.append((math.cos(a) * r, y0, math.sin(a) * r))
    for i in range(n):
        a = phase + i * math.tau / n
        pts.append((math.cos(a) * r_top, y1, math.sin(a) * r_top))
    faces = [list(range(n)), list(range(n, 2 * n))]
    faces += [[i, (i + 1) % n, n + (i + 1) % n, n + i] for i in range(n)]
    colours = [colour, top or colour] + [colour] * n
    if r_top < 1e-4:  # cone: collapse the top ring into one apex
        pts = pts[:n] + [(0, y1, 0)]
        faces = [list(range(n))] + [[i, (i + 1) % n, n] for i in range(n)]
        colours = [colour] * (n + 1)
    part.hull(place(pts, at), faces, colours)


def lumpy_mound(part, rx, rz, h, colours, rng, at=(0, 0, 0), y0=FOOT, rings=3, n=9):
    """An irregular, roughly convex rock pile, faceted and with alternating shades."""
    pts = [(0, h, 0)]
    for ring in range(rings):
        t = (ring + 1) / rings
        y = h * math.cos(t * math.pi / 2) if ring < rings - 1 else y0
        s = math.sin(t * math.pi / 2)
        for i in range(n):
            a = (i + 0.5 * ring) * math.tau / n
            j = 0.85 + 0.3 * rng.random()
            pts.append((math.cos(a) * rx * s * j, y + (0.01 * rng.random() if ring < rings - 1 else 0),
                        math.sin(a) * rz * s * j))
    pts = place(pts, at)
    centre = np.asarray(at, dtype=float) + (0, h * 0.35, 0)
    k = 0
    for i in range(n):
        part.tri(pts[0], pts[1 + i], pts[1 + (i + 1) % n], colours[k % len(colours)], centre)
        k += 1
    for ring in range(rings - 1):
        a0, b0 = 1 + ring * n, 1 + (ring + 1) * n
        for i in range(n):
            i1 = (i + 1) % n
            part.tri(pts[a0 + i], pts[b0 + i], pts[a0 + i1], colours[k % len(colours)], centre)
            part.tri(pts[a0 + i1], pts[b0 + i], pts[b0 + i1], colours[(k + 1) % len(colours)], centre)
            k += 1


def beam(part, a, b, t, colour):
    """A square-section timber from point a to point b."""
    a, b = np.asarray(a, dtype=float), np.asarray(b, dtype=float)
    axis = (b - a) / np.linalg.norm(b - a)
    side = np.cross(axis, (0, 1, 0) if abs(axis[1]) < 0.9 else (1, 0, 0))
    side /= np.linalg.norm(side)
    up = np.cross(side, axis)
    corners = [(side * sx + up * sy) * t / 2 for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    pts = [a + c for c in corners] + [b + c for c in corners]
    part.hull(pts, BOX_FACES, colour)


def log(part, x0, x1, y, z, r, end=TIMBER_LIGHT, bark=TIMBER, n=6):
    """A log lying along X, centred at height y and depth z."""
    pts = []
    for k in range(n):
        a = k * math.tau / n
        for x in (x0, x1):
            pts.append((x, y + math.sin(a) * r, z + math.cos(a) * r))
    faces = [[2 * k for k in range(n)], [2 * k + 1 for k in range(n)]]
    faces += [[2 * k, 2 * ((k + 1) % n), 2 * ((k + 1) % n) + 1, 2 * k + 1] for k in range(n)]
    part.hull(pts, faces, [end, end] + [bark] * n)


def house(name, w=0.15, d=0.11, wall=0.075, rise=0.075, walls=PLASTER, roof=THATCH, roof_end=THATCH_DARK,
          hip=0.0, chimney=True, timber=True, upper=False, seed=0):
    p = Part(name)
    hw, hd = w / 2, d / 2
    box(p, -hw - 0.008, FOOT, -hd - 0.008, hw + 0.008, 0.012, hd + 0.008, STONE_DARK, top=STONE)
    box(p, -hw, 0.012, -hd, hw, wall, hd, walls)
    if timber:
        t = 0.009
        for sx in (-1, 1):
            for sz in (-1, 1):
                box(p, sx * hw - t / 2 * (sx + 1), 0.012, sz * hd - t / 2 * (sz + 1),
                    sx * hw - t / 2 * (sx - 1), wall, sz * hd - t / 2 * (sz - 1), TIMBER)
        box(p, -hw - 0.002, wall - 0.008, hd - 0.002, hw + 0.002, wall, hd + 0.003, TIMBER)
    # door and windows on the +Z front
    box(p, -0.016, 0.012, hd - 0.002, 0.016, 0.058, hd + 0.004, DOOR)
    for sx in (-1, 1):
        if w > 0.12:
            x = sx * (hw * 0.58)
            box(p, x - 0.012, 0.035, hd - 0.002, x + 0.012, 0.055, hd + 0.003, WINDOW)
    box(p, hw - 0.002, 0.035, -0.012, hw + 0.003, 0.055, 0.012, WINDOW)
    if upper:
        # second storey: a timber band at the floor line and a row of upper windows
        band = wall * 0.52
        box(p, -hw - 0.002, band - 0.004, hd - 0.002, hw + 0.002, band + 0.004, hd + 0.003, TIMBER)
        box(p, hw - 0.002, band - 0.004, -hd - 0.002, hw + 0.003, band + 0.004, hd + 0.002, TIMBER)
        n = max(2, int(w / 0.05))
        for i in range(n):
            x = -hw + (i + 0.5) * w / n
            box(p, x - 0.009, band + 0.016, hd - 0.002, x + 0.009, band + 0.036, hd + 0.003, WINDOW)
    top = prism_roof(p, w, d, wall, rise, 0.016, roof, walls, hip=hip)
    # ridge cap, a shade darker, a touch shorter than the ridge so hip ends stay clean
    ridge = max(w / 2 + 0.016 - hip, 0.004)
    pts = [(-ridge, top - 0.012, -0.012), (ridge, top - 0.012, -0.012), (ridge, top - 0.012, 0.012),
           (-ridge, top - 0.012, 0.012), (-ridge, top + 0.006, 0), (ridge, top + 0.006, 0)]
    p.hull(pts, [[0, 1, 2, 3], [3, 2, 5, 4], [1, 0, 4, 5], [0, 3, 4], [2, 1, 5]], roof_end)
    if chimney:
        cx = -hw * 0.55 if seed % 2 == 0 else hw * 0.55
        box(p, cx - 0.012, wall, -0.03, cx + 0.012, wall + rise + 0.02, -0.006, STONE, top=STONE_DARK)
    return p


def garden(name, w=0.2, d=0.13, rows=4, rng=None):
    p = Part(name)
    hw, hd = w / 2, d / 2
    box(p, -hw, FOOT / 2, -hd, hw, 0.006, hd, SOIL)
    for i in range(rows):
        z = -hd + (i + 0.5) * d / rows
        colour = CROP if i % 2 == 0 else CROP_DARK
        box(p, -hw + 0.012, 0.006, z - 0.009, hw - 0.012, 0.022, z + 0.009, colour)
    # low wattle fence posts round the plot
    for x in np.linspace(-hw, hw, 6):
        for z in (-hd, hd):
            box(p, x - 0.004, 0.0, z - 0.004, x + 0.004, 0.03, z + 0.004, TIMBER_LIGHT)
    for z in (-hd, hd):
        box(p, -hw, 0.018, z - 0.002, hw, 0.024, z + 0.002, TIMBER_LIGHT)
    return p


def haystack(name, r=0.035):
    """A beehive rick: a bulging drum under a steep thatched cap."""
    p = Part(name)
    cylinder(p, r * 0.9, FOOT / 2, 0.012, HAY, n=8)
    cylinder(p, r * 0.9, 0.012, 0.03, HAY, n=8, r_top=r * 1.05)
    cylinder(p, r * 1.05, 0.03, 0.05, HAY, n=8, r_top=r * 0.8, phase=0.2)
    cylinder(p, r * 0.8, 0.05, 0.085, THATCH_DARK, n=8, r_top=0.0, phase=0.2)
    return p


def well(name):
    p = Part(name)
    cylinder(p, 0.026, FOOT, 0.03, STONE, n=8, top=WATER)
    for sx in (-1, 1):
        box(p, sx * 0.024 - 0.004, 0.02, -0.004, sx * 0.024 + 0.004, 0.075, 0.004, TIMBER)
    prism_roof(p, 0.05, 0.03, 0.07, 0.025, 0.006, THATCH, TIMBER)
    return p


def woodpile(name):
    p = Part(name)
    r = 0.008
    for row, count in enumerate((3, 2)):
        for k in range(count):
            z = (k - (count - 1) / 2) * 2 * r
            log(p, -0.03, 0.03, r + row * 1.7 * r, z, r)
    return p


def fence(name, length=0.22, posts=6):
    p = Part(name)
    for x in np.linspace(-length / 2, length / 2, posts):
        box(p, x - 0.004, FOOT / 2, -0.004, x + 0.004, 0.032, 0.004, TIMBER_LIGHT)
    for y in (0.012, 0.026):
        box(p, -length / 2, y - 0.003, -0.002, length / 2, y + 0.003, 0.002, TIMBER_LIGHT)
    return p


def at(part, x, z, yaw=0.0):
    part.offset = (x, 0.0, z)
    part.yaw = yaw
    return part


def write(path: Path, parts: list[Part], clear: str = "tile", **extras) -> tuple[int, int]:
    """Write one model. clear is "tile" (scatter props removed from the whole tile) or "centre" (only the
    middle kept clear, for improvements that live among trees). It is stored in the scene's extras, as are any
    further keyword extras; faceRiver=True puts the model's origin on the near waterline of the tile's river, with +X
    straight across the water and Z along the river."""
    glb = Glb()
    glb.doc["asset"]["generator"] = "improvement-models"
    glb.doc["materials"].append({"name": "Painted", "pbrMetallicRoughness": {
        "baseColorFactor": [1, 1, 1, 1], "metallicFactor": 0.0, "roughnessFactor": 0.9}})
    nodes = []
    triangles = 0
    for part in parts:
        if not part.tris:
            continue
        pos = np.array([v for a, b, c, _ in part.tris for v in (a, b, c)], dtype=np.float32) * SCALE
        normals = []
        for a, b, c, _ in part.tris:
            n = np.cross(b - a, c - a)
            n = n / (np.linalg.norm(n) or 1)
            normals += [n, n, n]
        cols = np.array([(*col, 1.0) for *_, col in part.tris for _ in range(3)], dtype=np.float32)
        attributes = {
            "POSITION": glb.accessor(pos, "VEC3", target=34962, bounds=True),
            "NORMAL": glb.accessor(np.array(normals, dtype=np.float32), "VEC3", target=34962),
            "COLOR_0": glb.accessor(cols, "VEC4", target=34962),
        }
        glb.doc["meshes"].append({"name": part.name, "primitives": [{"attributes": attributes, "material": 0}]})
        yaw = getattr(part, "yaw", 0.0)
        node = {"name": part.name, "mesh": len(glb.doc["meshes"]) - 1,
                "translation": list(getattr(part, "offset", (0, 0, 0)))}
        if yaw:
            node["rotation"] = [0.0, math.sin(yaw / 2), 0.0, math.cos(yaw / 2)]
        glb.doc["nodes"].append(node)
        nodes.append(len(glb.doc["nodes"]) - 1)
        triangles += len(part.tris)
    # Drop the writer's default "Unit" root: parts are the scene's top-level nodes.
    glb.doc["nodes"] = glb.doc["nodes"][1:]
    if clear not in ("tile", "centre"):
        raise ValueError(f"clear must be tile or centre, not {clear}")
    glb.doc["scenes"] = [{"nodes": [n - 1 for n in nodes], "extras": {"clear": clear, **extras}}]
    for node in glb.doc["nodes"]:
        node.pop("children", None)
    return glb.write(path), triangles
