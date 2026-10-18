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
    /// <summary>A ribbon on a deck lies this far over the deck top.</summary>
    private const float DeckSkin = 0.004f;
    /// <summary>A shade ribbon's COLOR.a is its strength times this, below the 0.5 that starts the raise of other ribbons.</summary>
    private const float ShadeScale = 0.45f;

    private enum RibbonMode { Surface, DeckTop, Shadow }

    /// <summary>One row of a ribbon: its point, how far it is raised off the ground, its blend toward the other look, and
    /// for a shade ribbon its strength.</summary>
    private readonly record struct Row(RoutePoint Point, float Raise, float Blend, float Shade = 0);

    private readonly record struct Wedge(Flat Clockwise, Flat Counterclockwise, WedgeKind Kind);

    private enum WedgeKind { Everywhere, Convex, Reflex }

    private static readonly Wedge Everywhere = new(Flat.UnitX, Flat.UnitX, WedgeKind.Everywhere);

    /// <summary>Plans the routes on a tile (<see cref="RoutePlan"/>): each link to a neighbour with a route is a gently
    /// swerving curve hashed from the unordered tile pair, so each tile draws its own half and the halves meet exactly at the
    /// shared edge. Null when the tile has no route.</summary>
    private RouteTilePlan? PlanRoute(TileId tile, Vector3 centre, RevealedPlotSnapshot plot)
    {
        if (plot.Route == null) return null;
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
        return RoutePlan.Plan(RouteLooks.Of(_session.Sim.Content, plot.Route), new Flat(centre.X, centre.Z), links);
    }

    /// <summary>Lays all planned routes over the terrain (<see cref="RouteProfile"/>), then sends the ground ribbons to
    /// <paramref name="ground"/> and the decks, bridges, supports and masts to <paramref name="parts"/>. Fog is the shaders'
    /// job, per pixel, so a half link in fog meets one in sight without a step.</summary>
    private void DrawRoutes(RoadMesh ground, RouteParts parts, IReadOnlyList<RouteTilePlan> plans)
    {
        var field = TerrainField;
        var profile = new RouteProfile(plans, (x, z) => (float)field.SurfaceHeightAt(x, z), (x, z) => field.Rivers.Near(x, z),
            (from, to) => field.Rivers.Crosses(from.X, from.Y, to.X, to.Y),
            field.Snapshot.Grid.Spec.Wrap ? (float)field.Period : 0);
        foreach (var plan in plans)
        {
            ground.Add(plan, profile);
            parts.Add(plan, profile);
        }
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
        /// they meet without a seam. A hub tile without paths gets a disc. A raised ground route also casts a shadow, and
        /// where a deck meets a ground route, the ground look runs on over the deck top, fading out.</summary>
        public void Add(RouteTilePlan plan, RouteProfile profile)
        {
            _centre = plan.Centre;
            _layer = 0;
            if (RouteJunction.Overlaps(plan))
            {
                Junction(plan, profile);
                return;
            }
            var hubbed = plan.Hub ? plan.Paths.Where(p => p.End is RouteEnd.Centre or RouteEnd.DeadEnd).ToList() : [];
            var wedges = Wedges(hubbed);
            foreach (var path in plan.Paths)
            {
                int i = hubbed.IndexOf(path);
                _wedge = i < 0 ? Everywhere : wedges[i];
                var laid = profile.Of(path);
                Ribbon(path, laid, i >= 0);
                _wedge = Everywhere;
                if (RouteLooks.Elevated(path.Own))
                {
                    if (!RouteLooks.Elevated(path.Other)) Overlay(path, laid, RibbonMode.DeckTop);
                }
                else if (laid.Raised) Overlay(path, laid, RibbonMode.Shadow);
            }
            _wedge = Everywhere;
            if (plan.Hub && hubbed.Count == 0) Hub(plan.Look, plan.Centre, RouteLooks.Style(plan.Look).Ribbon);
        }

        /// <summary>The paths of a junction overlap (<see cref="RouteJunction"/>). Each draws its look without markings only
        /// where its centre line is the nearest, so they join as one surface and edge lines stop at the mouth of each spur;
        /// then all draw their markings on top, so the rails of every path run through a switch. Finely divided, so the
        /// distance to the other paths, interpolated between vertices, stays close to the truth.</summary>
        private void Junction(RouteTilePlan plan, RouteProfile profile)
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
                    Ribbon(path, profile.Of(path), false, fine: true);
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
        /// by the half width, with the distance past the end in CUSTOM1.z, so the shader rounds it off. A ground route lies
        /// on its laid-out bed: draped over the ground, or level across where it stands clear of it. An elevated route's
        /// ribbon is the shade on the ground under its deck.</summary>
        private void Ribbon(RoutePath path, RoutePathProfile laid, bool cap, bool fine = false)
        {
            bool high = RouteLooks.Elevated(path.Own);
            // Under a deck only the shade remains; the ground look of a neighbour runs over the deck top instead.
            var other = high && !RouteLooks.Elevated(path.Other) ? path.Own : path.Other;
            var points = fine ? RouteJunction.Resample(path, FineStep).Select(s => laid.At(s.Along)).ToList()
                : laid.Raised ? laid.Points.ToList() : path.Samples.Select(s => laid.At(s.Along)).ToList();
            var rows = points.Select(p => new Row(p, high ? 0 : Raise(p.Clearance), other == path.Other ? p.Sample.Blend : 0)).ToList();
            Strip(path.Own, other, path, rows, fine);
            if (!cap) return;
            var end = rows[^1];
            float halfWidth = HalfWidth(path.Own, other);
            float across = path.First ? -halfWidth : halfWidth;
            var looks = Looks(path.Own, other, end.Raise);
            var tangent = end.Point.Sample.Tangent;
            var beyond = new Vector3(tangent.X, 0, tangent.Y) * halfWidth;
            var endSide = new Vector3(-tangent.Y, 0, tangent.X) * halfWidth;
            var at = end.Point.Sample.Position;
            var tip = new Vector3(at.X, 0, at.Y) + beyond;
            var tipExtra = new Vector2(end.Blend, path.Length - end.Point.Sample.Along - halfWidth);
            int last = Vertex(tip - endSide, end, new Vector2(-across, end.Point.Sample.Along + halfWidth), tipExtra, looks, halfWidth);
            Vertex(tip, end, new Vector2(0, end.Point.Sample.Along + halfWidth), tipExtra, looks, halfWidth);
            Vertex(tip + endSide, end, new Vector2(across, end.Point.Sample.Along + halfWidth), tipExtra, looks, halfWidth);
            Quads(last, 3);
        }

        /// <summary>A second ribbon along part of a path: the shade under a raised ground route, or the ground look of the
        /// neighbour fading out over the top of a deck.</summary>
        private void Overlay(RoutePath path, RoutePathProfile laid, RibbonMode mode)
        {
            var points = laid.Points;
            var rows = new List<Row>();
            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                bool keep = mode == RibbonMode.DeckTop
                    ? p.Sample.Blend > 0 || (i > 0 && points[i - 1].Sample.Blend > 0)
                    : Near(points, i, q => q.Clearance > RouteProfile.RaisedClearance);
                if (keep)
                {
                    rows.Add(mode == RibbonMode.DeckTop ? new Row(p, 1, p.Sample.Blend) : new Row(p, 0, 0, Shade(p.Clearance)));
                    continue;
                }
                if (rows.Count > 1) Strip(path.Own, mode == RibbonMode.DeckTop ? path.Other : path.Own, path, rows, false, mode);
                rows.Clear();
            }
            if (rows.Count > 1) Strip(path.Own, mode == RibbonMode.DeckTop ? path.Other : path.Own, path, rows, false, mode);
        }

        private static bool Near(IReadOnlyList<RoutePoint> points, int i, Func<RoutePoint, bool> test) =>
            test(points[i]) || (i > 0 && test(points[i - 1])) || (i < points.Count - 1 && test(points[i + 1]));

        /// <summary>How far a ground route's ribbon has left the ground: 0 draped, 1 level across on a deck.</summary>
        private static float Raise(float clearance) => Mathf.SmoothStep(0.004f, 0.02f, clearance);

        /// <summary>Strength of the shade under a raised ground route.</summary>
        private static float Shade(float clearance) => Mathf.SmoothStep(0.02f, 0.1f, clearance);

        private static float HalfWidth(RouteLook own, RouteLook other) =>
            Math.Max(RouteLooks.Style(own).Ribbon, RouteLooks.Style(other).Ribbon);

        /// <summary>The rows of a ribbon strip.</summary>
        private void Strip(RouteLook own, RouteLook other, RoutePath path, List<Row> rows, bool fine, RibbonMode mode = RibbonMode.Surface)
        {
            int columns = fine ? FineColumns : 3;
            // The shade is drawn as an elevated look, at the width of a deck's shade.
            if (mode == RibbonMode.Shadow) (own, other) = (RouteLook.Maglev, RouteLook.Maglev);
            float halfWidth = HalfWidth(own, other);
            // The samples run toward this tile's centre on both sides of an edge, so flip across on one side to keep its
            // sign over the edge.
            float across = path.First ? -halfWidth : halfWidth;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var s = row.Point.Sample;
                var side = new Vector3(-s.Tangent.Y, 0, s.Tangent.X) * halfWidth;
                var point = new Vector3(s.Position.X, 0, s.Position.Y);
                float inner = path.End == RouteEnd.Through ? 9f : path.Length - s.Along;
                var extra = new Vector2(row.Blend, inner);
                var looks = mode == RibbonMode.Shadow ? ShadowLooks(row.Shade) : Looks(own, other, mode == RibbonMode.DeckTop ? 1 : row.Raise);
                int start = _vertices.Count;
                for (int c = 0; c < columns; c++)
                {
                    float t = 2f * c / (columns - 1) - 1;
                    Vertex(point + side * t, row, new Vector2(across * t, s.Along), extra, looks);
                }
                if (i > 0) Quads(start, columns);
            }
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
            var looks = Looks(look, look, 0);
            var at = new Vector3(centre.X, 0, centre.Y);
            var extra = new Vector2(0, -1);
            var row = new Row(default, 0, 0);
            int middle = Vertex(at, row, Vector2.Zero, extra, looks);
            for (int k = 0; k < HubSegments; k++)
            {
                float a = Mathf.Tau * k / HubSegments;
                Vertex(at + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius, row, new Vector2(radius, 0), extra, looks);
            }
            for (int k = 0; k < HubSegments; k++) Triangle(middle, middle + 1 + (k + 1) % HubSegments, middle + 1 + k);
        }

        /// <summary>COLOR: the two looks, the layer and, in alpha, how far the ribbon is raised off the ground (0.5 to 1).</summary>
        private Color Looks(RouteLook own, RouteLook other, float raise) =>
            new((int)own / 15f, (int)other / 15f, _layer / 2f, 0.5f + 0.5f * raise);

        /// <summary>COLOR of a shade ribbon: alpha below 0.5 holds the shade's strength.</summary>
        private Color ShadowLooks(float shade) => new((int)RouteLook.Maglev / 15f, (int)RouteLook.Maglev / 15f, _layer / 2f, ShadeScale * shade);

        /// <summary>A ribbon vertex: draped on the ground under it, or, as far as the row is raised, level with the row's
        /// top.</summary>
        private int Vertex(Vector3 p, Row row, Vector2 uv, Vector2 uv2, Color looks, float past = 0)
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
            float raise = looks.A >= 0.5f ? row.Raise : 0;
            float y = Mathf.Lerp((float)surface + YLines, row.Point.Top + DeckSkin, raise);
            _vertices.Add(new Vector3(p.X, y, p.Z));
            _normals.Add(new Vector3(n.X, n.Y, n.Z).Lerp(Vector3.Up, raise).Normalized());
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
