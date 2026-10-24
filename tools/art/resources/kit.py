"""Kit for the map resource (bonus) models: geometry, colour, baked occlusion and the GLB export, all in Blender.

Units are world units (a hex tile has corner radius 1, scatter trees are ~0.4 tall, units ~0.85). Coordinates are
Blender's: +Z up, +X right and +Y away from the default camera (the game looks along -Z, which is +Y here), so a
model's front faces -Y. The exporter turns this into glTF's +Y up.

A model is a list of parts. Each part becomes one top-level glTF node, and the game seats every part on the terrain
under its own origin, so build a cluster from several small parts and it follows hills and mountain flanks. A part
named "ground..." is tilted to the slope instead (fields, crusts, slicks). The game draws a soft earth patch under
every other part where its vertices come within 0.06 of its origin's height; name a part "nodecal..." to skip that
(animals). Parts reach below z = 0 (FOOT, DEEP) so no gap shows on a slope. Sea resources stand at sea level (z = 0):
parts reach a little below the translucent surface and stand above it.

Resource models stand on a spot off the tile centre (BonusSpot in src/Civ.Client.Core/Terrain/BonusSpot.cs), so a
unit, a city or an improvement can share the tile. Keep land models within R of the origin and sea models a little
wider (BonusModelTests checks the reach). With no icon on the map, the model alone must say what the resource is from
the default camera: one bold silhouette and a clear colour signature beat fine detail.

Colours are authored as sRGB hex (palette()), stored linear in COLOR_0 as glTF wants, and the game's material shows
them as authored. Faces are flat shaded; each face corner gets its colour times a little per-face variation and the
ambient occlusion ray-cast against the whole model and the ground, which grounds the model under the game's
shadowless light.
"""
from __future__ import annotations

import math
import random
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector, geometry, noise
from mathutils.bvhtree import BVHTree

REPO_ROOT = Path(__file__).resolve().parents[3]
OUT = REPO_ROOT / "src/Mods/World/art/models"
R = 0.29  # land models stay within this reach of their origin
FOOT = -0.05  # most parts reach this far below their seat point
DEEP = -0.16  # rocks reach this deep, for steep mountain flanks
UP = Vector((0, 0, 1))


def hexc(value: int) -> tuple[float, float, float]:
    """An sRGB hex colour as 0..1 floats."""
    return tuple(((value >> shift) & 0xFF) / 255 for shift in (16, 8, 0))


def linear(c):
    c = np.asarray(c, dtype=float)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def mix(a, b, t):
    return tuple(x + (y - x) * t for x, y in zip(a, b))


def shade(c, f):
    """Scale a colour's brightness, clamped."""
    return tuple(min(1.0, x * f) for x in c)


class Paint:
    """How faces are coloured: a base colour (or several, picked per face), an optional colour for faces looking up
    (normal z above top_cut) and one for faces looking down."""

    def __init__(self, base, top=None, under=None, top_cut=0.75, rng=None):
        self.base = base if isinstance(base, list) else [base]
        self.top, self.under, self.top_cut = top, under, top_cut
        self.rng = rng or random.Random(len(self.base))

    def __call__(self, normal: Vector, centre: Vector):
        if self.top is not None and normal.z > self.top_cut:
            return self.top
        if self.under is not None and normal.z < -0.5:
            return self.under
        return self.base[self.rng.randrange(len(self.base))] if len(self.base) > 1 else self.base[0]


class Pattern:
    """Faces coloured by 3D noise at their centre: spots or patches of `spot` over `base` (cow hides, ore veins).
    cover is roughly the fraction of faces that take the spot colour."""

    def __init__(self, base, spot, scale=30.0, cover=0.35, seed=0.0, under=None):
        self.base, self.spot, self.scale, self.under = paint_of(base), spot, scale, under
        self.cut = 0.55 - 1.1 * cover
        self.offset = Vector((seed * 13.7, seed * 7.1, seed * 3.3))

    def __call__(self, normal: Vector, centre: Vector):
        if self.under is not None and normal.z < -0.6:
            return self.under
        if noise.noise(centre * self.scale + self.offset) > self.cut:
            return self.spot(normal, centre) if callable(self.spot) else self.spot
        return self.base(normal, centre)


class Band:
    """Faces whose centre lies within width of a plane (through point, across normal) take the band colour: a
    vein through rock, a stripe on a shell. Otherwise the base paint."""

    def __init__(self, base, band, point, normal, width, wobble=0.0, scale=40.0):
        self.base, self.band = paint_of(base), band
        self.point, self.normal = Vector(point), Vector(normal).normalized()
        self.width, self.wobble, self.scale = width, wobble, scale

    def __call__(self, normal: Vector, centre: Vector):
        d = abs((centre - self.point).dot(self.normal) + self.wobble * noise.noise(centre * self.scale))
        if d < self.width:
            return self.band(normal, centre) if callable(self.band) else self.band
        return self.base(normal, centre)


def paint_of(colour):
    return colour if callable(colour) else Paint(colour)


class Part:
    """One top-level node: faces with a colour each, around the part's own origin (its seat point)."""

    def __init__(self, name: str, at=(0.0, 0.0), yaw: float = 0.0):
        self.name, self.at, self.yaw = name, at, yaw
        self.verts: list[Vector] = []
        self.faces: list[tuple[int, ...]] = []
        self.colours: list[tuple[float, float, float]] = []

    def add(self, verts, faces, colour):
        """Append a piece; colour is a colour or a Paint, asked once per face with the face normal."""
        paint = paint_of(colour)
        base = len(self.verts)
        verts = [Vector(v) for v in verts]
        self.verts += verts
        for face in faces:
            corners = [verts[i] for i in face]
            self.faces.append(tuple(base + i for i in face))
            self.colours.append(paint(face_normal(corners), sum(corners, Vector()) / len(corners)))
        return self

    def transformed(self, matrix: Matrix, start: int = 0):
        """Transform the vertices added since vertex index start."""
        for i in range(start, len(self.verts)):
            self.verts[i] = matrix @ self.verts[i]
        return self

    def mark(self) -> int:
        return len(self.verts)

    def surface(self, start: int, centre) -> "Surface":
        """The Surface of the faces whose vertices were all added since vertex index start."""
        faces = [[i - start for i in f] for f in self.faces if min(f) >= start]
        return Surface(BVHTree.FromPolygons(self.verts[start:], faces, epsilon=0.0), centre)

    def extent(self) -> float:
        """Reach from the model origin in the ground plane (after this part's offset and turn)."""
        turn = Matrix.Rotation(self.yaw, 3, "Z")
        return max((Vector((*self.at, 0)) + turn @ v).xy.length for v in self.verts) if self.verts else 0.0


def at(part: Part, x: float, y: float, yaw: float = 0.0) -> Part:
    part.at, part.yaw = (x, y), yaw
    return part


def face_normal(points) -> Vector:
    n = Vector((0.0, 0.0, 0.0))
    for i, a in enumerate(points):  # Newell's method, robust for quads and n-gons
        b = points[(i + 1) % len(points)]
        n.x += (a.y - b.y) * (a.z + b.z)
        n.y += (a.z - b.z) * (a.x + b.x)
        n.z += (a.x - b.x) * (a.y + b.y)
    return n.normalized() if n.length > 1e-12 else Vector((0, 0, 1))


# --- primitives ---------------------------------------------------------------------------------------------------

_ICOSPHERES: dict[int, tuple[list[Vector], list[tuple[int, ...]]]] = {}


def unit_icosphere(subdivisions: int):
    if subdivisions not in _ICOSPHERES:
        bm = bmesh.new()
        bmesh.ops.create_icosphere(bm, subdivisions=subdivisions, radius=1.0)
        bm.verts.index_update()
        _ICOSPHERES[subdivisions] = ([v.co.copy() for v in bm.verts], [tuple(v.index for v in f.verts) for f in bm.faces])
        bm.free()
    return _ICOSPHERES[subdivisions]


def hull_faces(points):
    """Faces (as point index tuples, wound outward) of the convex hull of points; interior points are left unused."""
    bm = bmesh.new()
    verts = [bm.verts.new(p) for p in points]
    for i, v in enumerate(verts):
        v.index = i
    result = bmesh.ops.convex_hull(bm, input=verts, use_existing_faces=False)
    # The two lists can share elements; delete each once.
    unused = list({*result["geom_interior"], *result["geom_unused"]})
    bmesh.ops.delete(bm, geom=unused, context="VERTS")
    centre = sum((Vector(p) for p in points), Vector()) / len(points)
    faces = []
    for f in bm.faces:
        idx = tuple(v.index for v in f.verts)
        pts = [Vector(points[i]) for i in idx]
        mid = sum(pts, Vector()) / len(pts)
        if face_normal(pts).dot(mid - centre) < 0:
            idx = idx[::-1]
        faces.append(idx)
    bm.free()
    return faces


def hull(part: Part, points, colour):
    """A convex solid around points."""
    pts = [Vector(p) for p in points]
    part.add(pts, hull_faces(pts), colour)
    return part


def blob(part: Part, centre, radii, colour, subdivisions=1, wobble=0.12, seed=0.0, frequency=1.6, cut=None):
    """A lumpy ellipsoid: an icosphere pushed in and out by noise. cut flattens everything below that height (in the
    blob's frame, -1..1) onto a flat base."""
    verts, faces = unit_icosphere(subdivisions)
    offset = Vector((seed * 7.31, seed * 3.17, seed * 5.53))
    rx, ry, rz = radii
    out = []
    for v in verts:
        r = 1.0 + wobble * noise.noise(v * frequency + offset)
        p = v * r
        if cut is not None and p.z < cut:
            p.z = cut
        out.append(Vector((centre[0] + p.x * rx, centre[1] + p.y * ry, centre[2] + p.z * rz)))
    part.add(out, faces, colour)
    return part


class Surface:
    """The outside of a solid (or a clump of them), for setting things into it."""

    def __init__(self, tree: BVHTree, centre):
        self.tree, self.centre = tree, Vector(centre)

    def at(self, direction) -> tuple[Vector, Vector]:
        """The point and outward normal on the outermost surface toward direction from the centre."""
        d = Vector(direction).normalized()
        location, normal, _, _ = self.tree.ray_cast(self.centre + d * 2.0, -d, 4.0)
        if location is None:
            return self.centre.copy(), d
        return location, normal


class Strata:
    """Faces coloured by layer: the colours in turn between the planes at offsets along normal from point. Slice
    the solid on the same planes (rock(strata=...)) so each face lies in one layer."""

    def __init__(self, colours, point, normal, offsets):
        self.colours = [paint_of(c) for c in colours]
        self.point, self.normal, self.offsets = Vector(point), Vector(normal).normalized(), sorted(offsets)

    def __call__(self, normal: Vector, centre: Vector):
        s = (centre - self.point).dot(self.normal)
        layer = sum(1 for o in self.offsets if s > o)
        return self.colours[layer % len(self.colours)](normal, centre)


def solid(part: Part, points, colour, merge=0.0, chamfer=0.0, cuts=(), centre=None) -> Surface:
    """The convex hull of points as a solid: facets within merge degrees of each other join into one, every edge is
    chamfered by chamfer (world units), and cuts (plane point, plane normal) slice it so layers can take their own
    colour. Returns its Surface."""
    bm = bmesh.new()
    verts = [bm.verts.new(p) for p in points]
    result = bmesh.ops.convex_hull(bm, input=verts, use_existing_faces=False)
    bmesh.ops.delete(bm, geom=list({*result["geom_interior"], *result["geom_unused"]}), context="VERTS")
    if merge:
        bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(merge), verts=bm.verts[:], edges=bm.edges[:])
    if chamfer:
        bmesh.ops.bevel(bm, geom=bm.edges[:], offset=chamfer, segments=1, affect="EDGES", clamp_overlap=True,
                        profile=0.5)
    for co, no in cuts:
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=Vector(co),
                               plane_no=Vector(no))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.verts.index_update()
    centre = Vector(centre) if centre is not None else sum((Vector(p) for p in points), Vector()) / len(points)
    surface = Surface(BVHTree.FromBMesh(bm), centre)
    part.add([v.co.copy() for v in bm.verts], [tuple(v.index for v in f.verts) for f in bm.faces], colour)
    bm.free()
    return surface


def rock(part: Part, centre, radii, colour, rng: random.Random, points=14, flat=0.25, bottom=None, merge=0.0,
         chamfer=0.0, strata=None) -> Surface:
    """An angular boulder: the hull of random points on an ellipsoid. flat squashes the top a little; bottom sets
    the height (absolute) its base reaches down to. merge and chamfer (a fraction of the smallest radius) are as in
    solid(); strata = (normal, offsets) slices it into layers for a Strata paint with the same planes through
    centre. Returns its Surface."""
    rx, ry, rz = radii
    pts = []
    for k in range(points):
        # Fibonacci directions with jitter, so the facets stay even.
        t = (k + 0.5) / points
        z = 1 - 2 * t
        a = k * 2.399963 + rng.uniform(-0.5, 0.5)
        s = math.sqrt(max(0.0, 1 - z * z))
        d = Vector((math.cos(a) * s, math.sin(a) * s, z))
        d += Vector((rng.uniform(-0.25, 0.25), rng.uniform(-0.25, 0.25), rng.uniform(-0.2, 0.2)))
        d.normalize()
        if d.z > 0:
            d.z *= 1 - flat
        r = rng.uniform(0.82, 1.08)
        pts.append(Vector((centre[0] + d.x * rx * r, centre[1] + d.y * ry * r, centre[2] + d.z * rz * r)))
    if bottom is not None:
        # A skirt down to the foot, so the rock shows no gap where the ground falls away.
        pts += [Vector((centre[0] + math.cos(a) * rx * 0.75, centre[1] + math.sin(a) * ry * 0.75, bottom))
                for a in (k * math.tau / 6 + rng.random() for k in range(6))]
    cuts = []
    if strata:
        normal, offsets = strata
        n = Vector(normal).normalized()
        cuts = [(Vector(centre) + n * o, n) for o in offsets]
    return solid(part, pts, colour, merge=merge, chamfer=chamfer * min(radii), cuts=cuts, centre=centre)


def nugget(part: Part, point, normal, size, colour, rng: random.Random, sink=0.45, points=8):
    """A small angular lump set into a surface at point, sunk sink of its size along -normal (ore, a crystal
    root)."""
    n = Vector(normal).normalized()
    c = Vector(point) - n * size * sink
    pts = [c + Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1))).normalized() * size *
           rng.uniform(0.7, 1.1) for _ in range(points)]
    hull(part, pts, colour)


def crystal(part: Part, base, direction, length, r, colour, sides=6, tip=0.3, phase=0.0):
    """A prism crystal from base along direction, ending in a point (the last tip of its length)."""
    base, d = Vector(base), Vector(direction).normalized()
    tube(part, [base - d * r, base + d * length * (1 - tip), base + d * length], [r * 0.9, r, 0.0], colour,
         sides=sides, phase=phase)


def frame(direction: Vector):
    """Two unit vectors perpendicular to direction."""
    d = direction.normalized()
    side = d.cross(UP if abs(d.z) < 0.95 else Vector((1, 0, 0))).normalized()
    return side, side.cross(d).normalized()


def tube(part: Part, points, radii, colour, sides=6, cap=True, phase=0.0, squash=1.0):
    """A tapered tube through points (a trunk, branch, stem, leg, horn). A radius of 0 at an end makes a point."""
    pts = [Vector(p) for p in points]
    if isinstance(radii, (int, float)):
        radii = [radii] * len(pts)
    rings = []
    side, up = frame(pts[1] - pts[0])
    verts = []
    for i, p in enumerate(pts):
        tangent = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        # Parallel transport: keep the previous side vector, made perpendicular to the new tangent.
        side = (side - tangent * side.dot(tangent)).normalized()
        up = side.cross(tangent).normalized()
        if radii[i] <= 1e-6:  # a point: the tube closes to an apex here
            rings.append([len(verts)])
            verts.append(p.copy())
            continue
        ring = []
        for k in range(sides):
            a = phase + k * math.tau / sides
            ring.append(len(verts))
            verts.append(p + (side * math.cos(a) + up * math.sin(a) * squash) * radii[i])
        rings.append(ring)
    faces = []
    for i in range(len(pts) - 1):
        a, b = rings[i], rings[i + 1]
        for k in range(sides):
            k1 = (k + 1) % sides
            if len(a) == 1 and len(b) == 1:
                break
            if len(a) == 1:
                faces.append((a[0], b[k], b[k1]))
            elif len(b) == 1:
                faces.append((a[k], b[0], a[k1]))
            else:
                faces.append((a[k], b[k], b[k1], a[k1]))
    if cap:  # ring order runs clockwise seen down the tangent
        if len(rings[0]) > 1:
            faces.append(tuple(rings[0]))
        if len(rings[-1]) > 1:
            faces.append(tuple(reversed(rings[-1])))
    part.add(verts, faces, colour)
    return part


def cylinder(part: Part, base, top, r0, r1, colour, sides=8, phase=0.0):
    return tube(part, [base, top], [r0, r1], colour, sides=sides, phase=phase)


def box(part: Part, lo, hi, colour, matrix: Matrix | None = None):
    x0, y0, z0 = lo
    x1, y1, z1 = hi
    verts = [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0), (x0, y0, z1), (x1, y0, z1), (x1, y1, z1),
             (x0, y1, z1)]
    faces = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    if matrix is not None:
        verts = [matrix @ Vector(v) for v in verts]
    part.add(verts, faces, colour)
    return part


def leaf(part: Part, base, azimuth, length, width, colour, lift=0.6, droop=0.9, fold=0.25, segments=5,
         thickness=0.004, shape="lance", twist=0.0):
    """A curved leaf from base: it leaves at angle lift above the horizon toward azimuth, droops under its own weight
    and tapers to a tip. fold creases it along the midrib. Built two-sided so it shows from below too."""
    base = Vector(base)
    heading = Vector((math.cos(azimuth), math.sin(azimuth), 0.0))
    side_dir = Vector((-heading.y, heading.x, 0.0))
    # Integrate the bending spine.
    pos = base.copy()
    points = [pos.copy()]
    step = length / segments
    for i in range(segments):
        t = (i + 0.5) / segments
        angle = lift - droop * t * 1.6
        pos = pos + (heading * math.cos(angle) + UP * math.sin(angle)) * step
        points.append(pos.copy())
    top, bottom = [], []
    verts = []
    for i, p in enumerate(points):
        t = i / segments
        if shape == "lance":
            w = width * (math.sin(math.pi * min(1.0, t * 1.1)) ** 0.7) if i < segments else 0.0
        elif shape == "broad":
            w = width * (math.sin(math.pi * (0.15 + 0.85 * t)) ** 0.5) if i < segments else 0.0
        elif shape == "strap":
            w = width * (1.0 - t ** 3) if i < segments else 0.0
        else:  # "round"
            w = width * math.sqrt(max(0.0, 1 - (2 * t - 1) ** 2))
        tw = twist * t
        s = side_dir * math.cos(tw) + UP * math.sin(tw)
        crease = UP * (fold * w)
        row_top = [p - s * w * 0.5, p + crease * 0.5, p + s * w * 0.5]
        row = []
        for q in row_top:
            row.append(len(verts))
            verts.append(q)
        top.append(row)
        row_b = []
        for q in row_top:
            row_b.append(len(verts))
            verts.append(q - UP * thickness)
        bottom.append(row_b)
    faces = []
    for i in range(segments):
        a, b = top[i], top[i + 1]
        faces += [(a[0], b[0], b[1], a[1]), (a[1], b[1], b[2], a[2])]
        a, b = bottom[i], bottom[i + 1]
        faces += [(a[1], b[1], b[0], a[0]), (a[2], b[2], b[1], a[1])]
    part.add(verts, [dedupe(f, verts) for f in faces if len(dedupe(f, verts)) >= 3], colour)
    return part


def dedupe(face, verts):
    """A face without repeated corners (a leaf's tip closes its last quads into triangles)."""
    out = []
    for i in face:
        if not out or (verts[i] - verts[out[-1]]).length > 1e-7:
            out.append(i)
    if len(out) > 1 and (verts[out[0]] - verts[out[-1]]).length <= 1e-7:
        out.pop()
    return tuple(out)


class Outline:
    """A ragged, roughly elliptical outline around centre, for fields and patches."""

    def __init__(self, rx, ry, rng: random.Random, centre=(0.0, 0.0), sides=18, ragged=0.16, square=2.0, turn=0.0):
        """square is the superellipse power: 2 for an ellipse, 4 or more for a rounded rectangle turned by turn."""
        self.centre = Vector((centre[0], centre[1]))
        self.sides = sides
        phase = rng.random() * math.tau
        self.radii = []
        for k in range(sides):
            a = k * math.tau / sides
            j = 1 + ragged * (rng.random() - 0.5) + ragged * 0.5 * math.sin(2 * a + phase)
            c, s = abs(math.cos(a - turn)) / rx, abs(math.sin(a - turn)) / ry
            self.radii.append((c ** square + s ** square) ** (-1 / square) * j)

    def angle(self, k) -> float:
        return k * math.tau / self.sides

    def radius(self, a: float) -> float:
        """The outline radius toward angle a, interpolated between the sides."""
        u = (a % math.tau) / math.tau * self.sides
        k = int(u) % self.sides
        f = u - int(u)
        return self.radii[k] * (1 - f) + self.radii[(k + 1) % self.sides] * f

    def point(self, k, t=1.0) -> Vector:
        a = self.angle(k)
        return self.centre + Vector((math.cos(a), math.sin(a))) * self.radii[k % self.sides] * t

    def fraction(self, x, y) -> float:
        """How far (x, y) lies from the centre toward the outline: 0 at the centre, 1 on it."""
        d = Vector((x, y)) - self.centre
        return d.length / max(1e-6, self.radius(math.atan2(d.y, d.x)))

    def scaled(self, factor: float, extra: float = 0.0) -> "Outline":
        """The same outline, its radii times factor plus extra."""
        out = Outline.__new__(Outline)
        out.centre, out.sides = self.centre.copy(), self.sides
        out.radii = [r * factor + extra for r in self.radii]
        return out

    def inside(self, rng: random.Random, margin=0.85) -> tuple[float, float]:
        while True:
            a, t = rng.random() * math.tau, margin * math.sqrt(rng.random())
            p = self.centre + Vector((math.cos(a), math.sin(a))) * self.radius(a) * t
            return p.x, p.y


def plate(part: Part, outline: Outline, colour, rim=None, z=0.003, depth=FOOT / 2):
    """A flat ground plate on outline (soil, a crust, a slick) with a skirt reaching below the ground for slopes."""
    n = outline.sides
    ring = [outline.point(k) for k in range(n)]
    c = outline.centre
    verts = [Vector((c.x, c.y, z))] + [Vector((p.x, p.y, z)) for p in ring] + [Vector((p.x, p.y, depth)) for p in ring]
    part.add(verts, [(0, 1 + k, 1 + (k + 1) % n) for k in range(n)], colour)
    part.add(verts, [(1 + k, 1 + n + k, 1 + n + (k + 1) % n, 1 + (k + 1) % n) for k in range(n)], rim or colour)
    return part


def surface(outline: Outline, rng: random.Random, spacing: float):
    """An even triangulation of the area inside outline: jittered grid points inside it, joined by a constrained
    Delaunay triangulation. Returns 2D points and counter-clockwise triangles."""
    pts = [outline.point(k) for k in range(outline.sides)]
    c = outline.centre
    reach = max(outline.radii)
    rows = int(reach / (spacing * 0.866)) + 1
    for row in range(-rows, rows + 1):
        for col in range(-rows - 1, rows + 2):
            x = c.x + (col + 0.5 * (row % 2)) * spacing + rng.uniform(-0.28, 0.28) * spacing
            y = c.y + row * spacing * 0.866 + rng.uniform(-0.28, 0.28) * spacing
            if outline.fraction(x, y) < 1 - 0.45 * spacing / reach:
                pts.append(Vector((x, y)))
    out, _, faces, *_ = geometry.delaunay_2d_cdt(pts, [], [list(range(outline.sides))], 1, 1e-6)
    return out, faces


def canopy(part: Part, outline: Outline, height, top, side, rng: random.Random, spacing=0.034, edge=0.5,
           wave=0.0, wave_length=0.12, lumps=0.1, foot=FOOT / 2):
    """A field's crop as one surface over a ragged outline, about height tall and sloping down to edge times that at
    the rim, with wind waves and lumps, and a skirt of stalks from the rim down below the ground. Returns
    height(x, y), the canopy's height anywhere inside, for setting ears or plants on top."""
    seed = Vector((rng.random() * 50, rng.random() * 50, 0))

    def h(x, y):
        t = min(1.0, outline.fraction(x, y))
        rim = 1 - (1 - edge) * max(0.0, (t - 0.5) / 0.5) ** 1.6
        z = height * rim * (1 + lumps * noise.noise(Vector((x * 14, y * 14, 0)) + seed))
        if wave:
            z += wave * rim * math.sin((x * 0.8 + y * 0.6) * math.tau / wave_length)
        return z

    points, faces = surface(outline, rng, spacing)
    part.add([Vector((p.x, p.y, h(p.x, p.y))) for p in points], faces, top)
    # The skirt: stalks seen from the side, from the rim down below the ground.
    n = outline.sides
    rim = [outline.point(k) for k in range(n)]
    skirt = [Vector((p.x, p.y, h(p.x, p.y))) for p in rim] + [Vector((p.x, p.y, foot)) for p in rim]
    part.add(skirt, [(k, n + k, n + (k + 1) % n, (k + 1) % n) for k in range(n)], side)
    return h


def spindle(part: Part, a, b, r, colour, sides=4, bulge=0.45, phase=0.0):
    """A body tapering to points at a and b, thickest at fraction bulge (an ear of grain, a seed, a fish)."""
    a, b = Vector(a), Vector(b)
    return tube(part, [a, a.lerp(b, bulge * 0.5), a.lerp(b, bulge), a.lerp(b, (1 + bulge) / 2), b],
                [0.0, r * 0.75, r, r * 0.7, 0.0], colour, sides=sides, phase=phase)


def scatter_disc(rng: random.Random, count, radius, min_gap, centre=(0.0, 0.0), tries=400):
    """Up to count points in a disc, at least min_gap apart (dart throwing)."""
    out = []
    for _ in range(tries):
        if len(out) >= count:
            break
        a, r = rng.random() * math.tau, radius * math.sqrt(rng.random())
        p = (centre[0] + math.cos(a) * r, centre[1] + math.sin(a) * r)
        if all((p[0] - q[0]) ** 2 + (p[1] - q[1]) ** 2 >= min_gap ** 2 for q in out):
            out.append(p)
    return out


# --- finishing ----------------------------------------------------------------------------------------------------

_HEMISPHERE: list[Vector] = []


def hemisphere(n=40):
    """Cosine-weighted directions about +Z (a Fibonacci spiral), fixed so every build matches."""
    if not _HEMISPHERE:
        for k in range(n):
            u = (k + 0.5) / n
            r, a = math.sqrt(u), k * 2.399963
            _HEMISPHERE.append(Vector((r * math.cos(a), r * math.sin(a), math.sqrt(max(0.0, 1 - u)))))
    return _HEMISPHERE


def to_object(part: Part, collection) -> bpy.types.Object:
    mesh = bpy.data.meshes.new(part.name)
    mesh.from_pydata([tuple(v) for v in part.verts], [], part.faces)
    obj = bpy.data.objects.new(part.name, mesh)
    obj.location = (part.at[0], part.at[1], 0.0)
    obj.rotation_euler = (0.0, 0.0, part.yaw)
    collection.objects.link(obj)
    if len(mesh.polygons) != len(part.colours):
        raise ValueError(f"{part.name}: {len(mesh.polygons)} faces built from {len(part.colours)}")
    return obj


def occlusion(objects, ground: bool, reach=0.12, strength=0.75, rng_seed=1):
    """Write each face corner's colour: the face colour, varied a little per face, times the ambient light that
    reaches it (rays against every part and, on land, the ground plane)."""
    depsgraph = bpy.context.evaluated_depsgraph_get()
    verts, polys = [], []
    for obj in objects:
        m = obj.matrix_world
        base = len(verts)
        verts += [m @ v.co for v in obj.data.vertices]
        polys += [[base + i for i in p.vertices] for p in obj.data.polygons]
    if ground:
        g = len(verts)
        verts += [Vector((-3, -3, 0)), Vector((3, -3, 0)), Vector((3, 3, 0)), Vector((-3, 3, 0))]
        polys.append([g, g + 1, g + 2, g + 3])
    tree = BVHTree.FromPolygons(verts, polys, epsilon=0.0)
    directions = hemisphere()
    for obj in objects:
        rng = random.Random(f"{obj.name}:{rng_seed}")
        mesh = obj.data
        m = obj.matrix_world
        rot = m.to_3x3()
        attribute = mesh.color_attributes.new("Color", "FLOAT_COLOR", "CORNER")
        colours = obj["face_colours"]
        values = np.zeros((len(mesh.loops), 4), dtype=np.float32)
        for poly in mesh.polygons:
            normal = (rot @ poly.normal).normalized()
            centre = m @ poly.center
            side, up = frame(normal)
            spin = rng.random() * math.tau
            cs, sn = math.cos(spin), math.sin(spin)
            vary = 1.0 + rng.uniform(-0.05, 0.05)
            base = linear(colours[poly.index]) * vary
            for li in poly.loop_indices:
                corner = m @ mesh.vertices[mesh.loops[li].vertex_index].co
                origin = corner.lerp(centre, 0.2) + normal * 0.0015
                if ground and origin.z < 0.0005:
                    origin.z = 0.0005
                hits = 0.0
                for d in directions:
                    x, y = d.x * cs - d.y * sn, d.x * sn + d.y * cs
                    ray = side * x + up * y + normal * d.z
                    location, _, _, distance = tree.ray_cast(origin, ray, reach)
                    if location is not None:
                        hits += 1.0 - 0.5 * distance / reach
                ao = 1.0 - hits / len(directions)
                values[li, :3] = base * (1.0 - strength * (1.0 - ao))
                values[li, 3] = 1.0
        attribute.data.foreach_set("color", np.clip(values, 0, 1).ravel())
        mesh.color_attributes.active_color = attribute
        del obj["face_colours"]


def material():
    mat = bpy.data.materials.get("Painted")
    if mat is None:
        mat = bpy.data.materials.new("Painted")
        mat.use_nodes = True
        nodes, links = mat.node_tree.nodes, mat.node_tree.links
        bsdf = nodes["Principled BSDF"]
        bsdf.inputs["Roughness"].default_value = 0.9
        bsdf.inputs["Specular IOR Level"].default_value = 0.3
        attribute = nodes.new("ShaderNodeVertexColor")
        attribute.layer_name = "Color"
        links.new(attribute.outputs["Color"], bsdf.inputs["Base Color"])
    return mat


def clear_scene():
    for block in list(bpy.data.objects):
        bpy.data.objects.remove(block)
    for collection in (bpy.data.meshes, bpy.data.materials):
        for block in list(collection):
            if block.users == 0:
                collection.remove(block)


def build(name: str, parts: list[Part], sea: bool = False, collection=None, reach=0.12, strength=0.75):
    """Turn parts into shaded Blender objects in collection (the scene's by default) and return them."""
    collection = collection or bpy.context.scene.collection
    objects = []
    mat = material()
    for part in parts:
        if not part.faces:
            continue
        obj = to_object(part, collection)
        obj["face_colours"] = [list(c) for c in part.colours]
        obj.data.materials.append(mat)
        objects.append(obj)
    bpy.context.view_layer.update()
    occlusion(objects, ground=not sea, reach=reach, strength=strength)
    for obj in objects:
        obj.data.shade_flat()
    return objects


def export(path: Path, objects, extras: dict):
    """Write objects as a GLB: one top-level node per object, scene extras for the renderer."""
    scene = bpy.context.scene
    for key in list(scene.keys()):
        del scene[key]
    for key, value in extras.items():
        scene[key] = value
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=str(path), export_format="GLB", use_selection=True, export_extras=True, export_yup=True,
        export_apply=True, export_normals=True, export_texcoords=False, export_tangents=False,
        export_materials="EXPORT", export_vertex_color="ACTIVE", export_all_vertex_colors=False,
        export_cameras=False, export_lights=False, export_animations=False)
    return path.stat().st_size, sum(len(p.vertices) - 2 for o in objects for p in o.data.polygons)
