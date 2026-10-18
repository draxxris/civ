using Civ.Client.Terrain;
using Civ.Game;
using Godot;
using Flat = System.Numerics.Vector2;

namespace Civ.Client;

internal sealed partial class MapRenderer
{
    /// <summary>Segments of a hub disc at a tile centre.</summary>
    private const int HubSegments = 12;

    /// <summary>Junction ribbons: rows at most this far apart (pattern units), with this many vertices across.</summary>
    private const float FineStep = 0.02f;
    private const int FineColumns = 9;

    /// <summary>CUSTOM1.w of a junction ribbon, whose CUSTOM0 = (RouteJunction.Margin less Yield, through).</summary>
    private const float JunctionKind = 3;

    /// <summary>Where a ground ribbon may draw about its tile centre: between the rays Clockwise and Counterclockwise
    /// (unit vectors, turning counterclockwise from the first to the second), or everywhere. Reflex wedges span more than
    /// half a turn.</summary>
    private readonly record struct Wedge(Flat Clockwise, Flat Counterclockwise, WedgeKind Kind);

    private enum WedgeKind { Everywhere, Convex, Reflex }

    private static readonly Wedge Everywhere = new(Flat.UnitX, Flat.UnitX, WedgeKind.Everywhere);

    /// <summary>Plans the routes on a tile (<see cref="RoutePlan"/>): each link to a neighbour with a route is a gently
    /// swerving curve hashed from the unordered tile pair, so each tile draws its own half and the halves meet exactly at the
    /// shared edge. The ground ribbons go to <paramref name="ground"/> and the decks, supports and masts to
    /// <paramref name="parts"/>. Fog is the shaders' job, per pixel, so a half link in fog meets one in sight without a
    /// step.</summary>
    private void DrawRoute(RoadMesh ground, RouteParts parts, TileId tile, Vector3 centre, RevealedPlotSnapshot plot)
    {
        if (plot.Route == null) return;
        var grid = _session.Sim.State.Grid;
        var hex = grid.HexOf(tile);
        var links = new List<RouteLinkInput>();
        for (int d = 0; d < 6; d++)
        {
            if (grid.Neighbor(tile, d) is not TileId n || n == tile || _session.PerceivedPlot(n)?.Route is not { } route) continue;
            // Unwrapped neighbour centre, so a link across the wrap seam stays short.
            var other = PositionOf(hex.Neighbor(d));
            int low = Math.Min(tile.Value, n.Value), high = Math.Max(tile.Value, n.Value);
            links.Add(new RouteLinkInput(d, RouteLooks.Of(_session.Sim.Content, route), new Flat(other.X, other.Z),
                Signed(Hash(low, 2 * high + 1)), Signed(Hash(low, 2 * high + 2)), tile.Value < n.Value));
        }
        var plan = RoutePlan.Plan(RouteLooks.Of(_session.Sim.Content, plot.Route), new Flat(centre.X, centre.Z), links);
        ground.Add(plan);
        parts.Add(plan);
    }

    /// <summary>Route ground ribbons draped on the terrain field, with the ground normal so they are lit like the terrain.
    /// The vertex layout is described in road.gdshader.</summary>
    private sealed class RoadMesh(TerrainField field)
    {
        private readonly List<Vector3> _vertices = new();
        private readonly List<Vector3> _normals = new();
        private readonly List<Vector2> _uvs = new();
        private readonly List<Vector2> _uv2s = new();
        private readonly List<Color> _colors = new();
        private readonly List<float> _sectors = new();
        private readonly List<float> _centres = new();
        // Triangles by layer (see road.gdshader), drawn in this order: whole looks, junction bodies, junction markings.
        private readonly List<int>[] _layers = [new(), new(), new()];
        // While drawing a junction tile: the other paths' centre lines, and whether this path is part of the through curve.
        private IReadOnlyList<Flat[]>? _rivals;
        private Flat[] _own = [];
        private float _yield;
        private bool _through;
        private int _layer;
        private Wedge _wedge = Everywhere;
        private Flat _centre;

        /// <summary>Paths that end at a hub run on past the centre into a round cap, and each draws only in its own wedge
        /// about the centre, bounded halfway to the paths on either side. So the hub is covered exactly once, with no
        /// translucent layers stacking where the paths would overlap; on the bisector the distances to both paths match, so
        /// they meet without a seam. A hub tile without paths gets a disc.</summary>
        public void Add(RouteTilePlan plan)
        {
            _centre = plan.Centre;
            _layer = 0;
            if (RouteJunction.Overlaps(plan))
            {
                Junction(plan);
                return;
            }
            var hubbed = plan.Hub ? plan.Paths.Where(p => p.End is RouteEnd.Centre or RouteEnd.DeadEnd).ToList() : [];
            var wedges = Wedges(hubbed);
            foreach (var path in plan.Paths)
            {
                int i = hubbed.IndexOf(path);
                _wedge = i < 0 ? Everywhere : wedges[i];
                Ribbon(path, i >= 0);
            }
            _wedge = Everywhere;
            if (plan.Hub && hubbed.Count == 0) Hub(plan.Look, plan.Centre, RouteLooks.Style(plan.Look).Ribbon);
        }

        /// <summary>The paths of a junction overlap (<see cref="RouteJunction"/>). Each draws its look without markings only
        /// where its centre line is the nearest, so they join as one surface and edge lines stop at the mouth of each spur;
        /// then all draw their markings on top, so the rails of every path run through a switch. Finely divided, so the
        /// distance to the other paths, interpolated between vertices, stays close to the truth.</summary>
        private void Junction(RouteTilePlan plan)
        {
            foreach (int layer in new[] { 1, 2 })
            {
                _layer = layer;
                foreach (var path in plan.Paths)
                {
                    _rivals = RouteJunction.Others(plan, path);
                    _own = path.Samples.Select(s => s.Position).ToArray();
                    _yield = RouteJunction.Yield(path);
                    _through = path.End == RouteEnd.Through;
                    Ribbon(path, false, fine: true);
                }
            }
            _rivals = null;
            _layer = 0;
        }

        /// <summary>The wedge of each path: from halfway to the next path clockwise to halfway to the next one
        /// counterclockwise, by the headings out of the centre. Neighbouring wedges share the very same boundary vector, so the
        /// shader's side tests split the plane between them with no gap or overlap.</summary>
        private static List<Wedge> Wedges(List<RoutePath> paths)
        {
            if (paths.Count < 2) return paths.Select(_ => Everywhere).ToList();
            var headings = paths.Select(p => MathF.Atan2(-p.Samples[^1].Tangent.Y, -p.Samples[^1].Tangent.X)).ToList();
            var order = Enumerable.Range(0, paths.Count).OrderBy(i => headings[i]).ToList();
            // gaps[k] is the turn from the k-th path in heading order to the next; bounds[k] is the ray halfway between.
            var gaps = new float[order.Count];
            var bounds = new Flat[order.Count];
            for (int k = 0; k < order.Count; k++)
            {
                float from = headings[order[k]];
                gaps[k] = Mathf.PosMod(headings[order[(k + 1) % order.Count]] - from, Mathf.Tau);
                if (k == order.Count - 1 && gaps[k] == 0) gaps[k] = Mathf.Tau;
                bounds[k] = new Flat(MathF.Cos(from + gaps[k] / 2), MathF.Sin(from + gaps[k] / 2));
            }
            var wedges = new Wedge[paths.Count];
            for (int k = 0; k < order.Count; k++)
            {
                int before = (k + order.Count - 1) % order.Count;
                float span = (gaps[before] + gaps[k]) / 2;
                wedges[order[k]] = new Wedge(bounds[before], bounds[k], span > MathF.PI ? WedgeKind.Reflex : WedgeKind.Convex);
            }
            return wedges.ToList();
        }

        /// <summary>One half path, three vertices across so it follows the ground under its width. The ribbon is as wide as
        /// the wider of the two looks it blends; the shader eases the drawn width. A capped path runs on past its inner end
        /// by the half width, with the distance past the end in CUSTOM1.z, so the shader rounds it off.</summary>
        private void Ribbon(RoutePath path, bool cap, bool fine = false)
        {
            var samples = fine ? RouteJunction.Resample(path, FineStep) : path.Samples;
            int columns = fine ? FineColumns : 3;
            float halfWidth = Math.Max(RouteLooks.Style(path.Own).Ribbon, RouteLooks.Style(path.Other).Ribbon);
            var looks = Looks(path.Own, path.Other);
            // The samples run toward this tile's centre on both sides of an edge, so flip across on one side to keep its
            // sign over the edge.
            float across = path.First ? -halfWidth : halfWidth;
            for (int i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                var side = new Vector3(-s.Tangent.Y, 0, s.Tangent.X) * halfWidth;
                var point = new Vector3(s.Position.X, 0, s.Position.Y);
                float inner = path.End == RouteEnd.Through ? 9f : path.Length - s.Along;
                var extra = new Vector2(s.Blend, inner);
                int row = _vertices.Count;
                for (int c = 0; c < columns; c++)
                {
                    float t = 2f * c / (columns - 1) - 1;
                    Vertex(point + side * t, new Vector2(across * t, s.Along), extra, looks);
                }
                if (i > 0) Quads(row, columns);
            }
            if (!cap) return;
            var end = path.Samples[^1];
            var beyond = new Vector3(end.Tangent.X, 0, end.Tangent.Y) * halfWidth;
            var endSide = new Vector3(-end.Tangent.Y, 0, end.Tangent.X) * halfWidth;
            var tip = new Vector3(end.Position.X, 0, end.Position.Y) + beyond;
            var tipExtra = new Vector2(end.Blend, path.Length - end.Along - halfWidth);
            int last = Vertex(tip - endSide, new Vector2(-across, end.Along + halfWidth), tipExtra, looks, halfWidth);
            Vertex(tip, new Vector2(0, end.Along + halfWidth), tipExtra, looks, halfWidth);
            Vertex(tip + endSide, new Vector2(across, end.Along + halfWidth), tipExtra, looks, halfWidth);
            Quads(last, 3);
        }

        /// <summary>Joins the row of vertices at <paramref name="row"/> to the row before it.</summary>
        private void Quads(int row, int columns)
        {
            for (int k = 0; k < columns - 1; k++)
            {
                int a = row - columns + k, b = row + k;
                Triangle(a, a + 1, b + 1);
                Triangle(a, b + 1, b);
            }
        }

        /// <summary>A disc whose UV.x is the distance from its centre, so it ends like a ribbon cross-section.</summary>
        private void Hub(RouteLook look, Flat centre, float radius)
        {
            var looks = Looks(look, look);
            var at = new Vector3(centre.X, 0, centre.Y);
            var extra = new Vector2(0, -1);
            int middle = Vertex(at, Vector2.Zero, extra, looks);
            for (int k = 0; k < HubSegments; k++)
            {
                float a = Mathf.Tau * k / HubSegments;
                Vertex(at + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius, new Vector2(radius, 0), extra, looks);
            }
            for (int k = 0; k < HubSegments; k++) Triangle(middle, middle + 1 + (k + 1) % HubSegments, middle + 1 + k);
        }

        private Color Looks(RouteLook own, RouteLook other) => new((int)own / 15f, (int)other / 15f, _layer / 2f, 1);

        private int Vertex(Vector3 p, Vector2 uv, Vector2 uv2, Color looks, float past = 0)
        {
            if (_rivals != null)
            {
                _sectors.AddRange([Math.Min(9, RouteJunction.Margin(_own, _rivals, new Flat(p.X, p.Z)) - _yield), _through ? 1 : 0, 0, 0]);
                _centres.AddRange([_centre.X, _centre.Y, 0, JunctionKind]);
            }
            else
            {
                _sectors.AddRange([_wedge.Clockwise.X, _wedge.Clockwise.Y, _wedge.Counterclockwise.X, _wedge.Counterclockwise.Y]);
                _centres.AddRange([_centre.X, _centre.Y, past, (float)_wedge.Kind]);
            }
            double surface = field.SurfaceHeightAt(p.X, p.Z);
            // Over water the surface is flat; elsewhere the field's own normal, as the terrain chunks use.
            var n = surface == field.HeightAt(p.X, p.Z) ? field.NormalAt(p.X, p.Z) : System.Numerics.Vector3.UnitY;
            _vertices.Add(new Vector3(p.X, (float)surface + YLines, p.Z));
            _normals.Add(new Vector3(n.X, n.Y, n.Z));
            _uvs.Add(uv);
            _uv2s.Add(uv2);
            _colors.Add(looks);
            return _vertices.Count - 1;
        }

        /// <summary>Wound to face up (Godot's front faces are clockwise), so the shader never sees a flipped normal.</summary>
        private void Triangle(int a, int b, int c)
        {
            var up = (_vertices[b] - _vertices[a]).Cross(_vertices[c] - _vertices[a]).Y;
            if (up > 0) (b, c) = (c, b);
            _layers[_layer].AddRange([a, b, c]);
        }

        public ArrayMesh? Build(Material material)
        {
            int[] indices = [.. _layers[0], .. _layers[1], .. _layers[2]];
            if (indices.Length == 0) return null;
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = _vertices.ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = _normals.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV] = _uvs.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV2] = _uv2s.ToArray();
            arrays[(int)Mesh.ArrayType.Color] = _colors.ToArray();
            arrays[(int)Mesh.ArrayType.Custom0] = _sectors.ToArray();
            arrays[(int)Mesh.ArrayType.Custom1] = _centres.ToArray();
            arrays[(int)Mesh.ArrayType.Index] = indices;
            var mesh = new ArrayMesh();
            var format = (Mesh.ArrayFormat)(((long)Mesh.ArrayCustomFormat.RgbaFloat << (int)Mesh.ArrayFormat.FormatCustom0Shift)
                | ((long)Mesh.ArrayCustomFormat.RgbaFloat << (int)Mesh.ArrayFormat.FormatCustom1Shift));
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays, flags: format);
            mesh.SurfaceSetMaterial(0, material);
            return mesh;
        }
    }
}
