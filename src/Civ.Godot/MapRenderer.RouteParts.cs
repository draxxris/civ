using Civ.Client.Terrain;
using Civ.Game;
using Godot;
using Flat = System.Numerics.Vector2;

namespace Civ.Client;

internal sealed partial class MapRenderer
{
    /// <summary>
    /// The 3D parts of routes, one opaque mesh for the whole map (route_structure.gdshader): maglev and jumplane decks swept
    /// along each half path with their piers or lattice legs, and the portal masts and contact wire of electric railroads.
    /// A deck stands <see cref="RouteStyle.Lift"/> over the smoothed ground and ramps into it where the route continues on
    /// the ground (<see cref="RouteSample.Lift"/>). Supports stand at evenly spaced stations whose spacing divides each half
    /// path, so they never crowd a tile edge; a station on a shared edge belongs to the link's first tile and the middle of a
    /// through curve to its primary half, so no support is placed twice.
    /// </summary>
    private sealed class RouteParts(TerrainField field)
    {
        private const float PierSpacing = 0.8f, MastSpacing = 0.8f;
        private const float MastHeight = 0.15f, MastReach = 0.105f, WireHeight = 0.13f, WireStep = 0.1f;
        private const float Concrete = 1, Glow = 2, Deck = 3, Metal = 4;

        private static readonly Color ConcreteColour = new(0.74f, 0.73f, 0.70f);
        private static readonly Color GuidewayColour = new(0.42f, 0.43f, 0.45f);
        private static readonly Color DeckColour = new(0.50f, 0.55f, 0.62f);
        private static readonly Color SteelColour = new(0.33f, 0.36f, 0.41f);
        private static readonly Color GlowColour = new(0.35f, 0.78f, 1.0f);
        private static readonly Color MastColour = new(0.56f, 0.57f, 0.58f);
        private static readonly Color WireColour = new(0.16f, 0.16f, 0.17f);

        /// <summary>One edge of a deck cross-section: two points (across, height below the deck top), its colour and surface.</summary>
        private readonly record struct ProfileEdge(Vector2 A, Vector2 B, Color Colour, float Surface);

        /// <summary>A concrete box beam with a sunken guideway down its top.</summary>
        private static readonly ProfileEdge[] MaglevProfile = Loop(
            [new(-0.075f, 0), new(-0.04f, 0), new(-0.04f, -0.016f), new(0.04f, -0.016f), new(0.04f, 0), new(0.075f, 0),
             new(0.075f, -0.05f), new(0.05f, -0.075f), new(-0.05f, -0.075f), new(-0.075f, -0.05f)],
            k => k == 2 ? (GuidewayColour, Metal) : (ConcreteColour, Concrete));

        /// <summary>A slim steel deck with glowing chamfers along both top edges and a keel underneath.</summary>
        private static readonly ProfileEdge[] JumplaneProfile = Loop(
            [new(-0.064f, -0.012f), new(-0.05f, 0), new(0.05f, 0), new(0.064f, -0.012f), new(0.064f, -0.03f),
             new(0.022f, -0.05f), new(-0.022f, -0.05f), new(-0.064f, -0.03f)],
            k => k is 0 or 2 ? (GlowColour, Glow) : k == 1 ? (DeckColour, Deck) : (SteelColour, Metal));

        private readonly List<Vector3> _vertices = new();
        private readonly List<Vector3> _normals = new();
        private readonly List<Vector2> _uvs = new();
        private readonly List<Vector2> _uv2s = new();
        private readonly List<Color> _colors = new();
        private readonly List<int> _indices = new();
        // While sweeping a deck at a junction: the other decks' centre lines (RouteJunction).
        private IReadOnlyList<Flat[]>? _rivals;
        private Flat[] _own = [];
        private float _yield;

        /// <summary>UV2.y (RouteJunction.Margin) of parts that no other deck can cut.</summary>
        private const float NoRival = 9;
        /// <summary>At a junction, deck samples and profile edges at most this far apart, so the interpolated distance to
        /// the other decks stays close to the truth.</summary>
        private const float FineStep = 0.02f;

        private static ProfileEdge[] Loop(Vector2[] points, Func<int, (Color, float)> paint)
        {
            var edges = new ProfileEdge[points.Length];
            for (int k = 0; k < points.Length; k++)
            {
                var (colour, surface) = paint(k);
                edges[k] = new ProfileEdge(points[k], points[(k + 1) % points.Length], colour, surface);
            }
            return edges;
        }

        /// <summary>At a junction the decks overlap. Each keeps only the parts nearer its own centre line than any other
        /// deck's (the shader cuts the rest), so their tops join as one deck with open channels and no curb or glowing edge
        /// across another deck.</summary>
        public void Add(RouteTilePlan plan)
        {
            bool junction = RouteJunction.Overlaps(plan);
            foreach (var path in plan.Paths)
            {
                if (RouteLooks.Elevated(path.Own))
                {
                    _rivals = junction ? RouteJunction.Others(plan, path) : null;
                    _own = path.Samples.Select(s => s.Position).ToArray();
                    _yield = RouteJunction.Yield(path);
                    DeckAlong(path);
                    _rivals = null;
                }
                if (path.Own == RouteLook.ElectricRailroad) Catenary(path);
            }
            // A lone tile of elevated route: one support under a short deck stub.
            if (plan.Paths.Count == 0 && RouteLooks.Elevated(plan.Look))
            {
                float top = DeckTop(plan.Centre, RouteLooks.Style(plan.Look).Lift);
                var profile = ProfileOf(plan.Look);
                Support(plan.Look, plan.Centre, Flat.UnitX, top + profile.Min(e => Math.Min(e.A.Y, e.B.Y)));
                var a = new Frame(plan.Centre - Flat.UnitX * 0.12f, Flat.UnitX, top, 0);
                var b = new Frame(plan.Centre + Flat.UnitX * 0.12f, Flat.UnitX, top, 0.24f);
                Sweep(profile, a, b);
                Cap(profile, a, -1);
                Cap(profile, b, 1);
            }
        }

        /// <summary>A point on a path: position, unit direction toward the inner end, deck top height and along.</summary>
        private readonly record struct Frame(Flat At, Flat Tangent, float Top, float Along)
        {
            public Vector3 Side => new(-Tangent.Y, 0, Tangent.X);
            public Vector3 Point(float across, float up) => new Vector3(At.X, Top + up, At.Y) + Side * across;
        }

        private float Height(Flat p) => (float)field.SurfaceHeightAt(p.X, p.Y);

        /// <summary>The highest ground under a deck's width about a point.</summary>
        private float Ground(Flat at)
        {
            float high = Height(at);
            for (int k = 0; k < 6; k++)
                high = Math.Max(high, Height(at + new Flat(MathF.Cos(k * MathF.PI / 3), MathF.Sin(k * MathF.PI / 3)) * 0.07f));
            return high;
        }

        /// <summary>Deck top over a point at full height: Lift over the ground averaged over a disc about the point, so it
        /// rides over bumps instead of following them, but never closer than half its Lift to the ground under it. It
        /// depends on the point alone, so the two tiles of an edge agree and the decks of a junction meet flush.</summary>
        private float DeckTop(Flat at, float lift)
        {
            float sum = Height(at);
            int count = 1;
            foreach (float r in new[] { 0.3f, 0.6f })
                for (int k = 0; k < 6; k++, count++)
                    sum += Height(at + new Flat(MathF.Cos(k * MathF.PI / 3 + r), MathF.Sin(k * MathF.PI / 3 + r)) * r);
            return Math.Max(sum / count + lift, Ground(at) + lift * 0.5f);
        }

        private void DeckAlong(RoutePath path)
        {
            var style = RouteLooks.Style(path.Own);
            var profile = ProfileOf(path.Own);
            // Along runs the same way on both sides of every edge (toward +x), so the deck's chevrons all point one way.
            float sign = path.Samples[^1].Position.X >= path.Samples[0].Position.X ? 1 : -1;
            // The top eases from level with the ground, where a ramp meets it, to full height.
            Frame FrameOf(RouteSample s)
            {
                float ground = Ground(s.Position);
                float top = ground + (DeckTop(s.Position, style.Lift) - ground) * s.Lift;
                return new Frame(s.Position, s.Tangent, top, s.Along * sign);
            }
            var samples = _rivals != null ? RouteJunction.Resample(path, FineStep) : path.Samples;
            var frames = samples.Select(FrameOf).ToArray();
            for (int i = 1; i < frames.Length; i++) Sweep(profile, frames[i - 1], frames[i]);
            // Two different decks meet at the shared edge: close both ends there.
            if (path.Other != path.Own && RouteLooks.Elevated(path.Other)) Cap(profile, frames[0], -1);
            if (path.End == RouteEnd.DeadEnd) Cap(profile, frames[^1], 1);
            float depth = profile.Min(e => Math.Min(e.A.Y, e.B.Y));
            foreach (var (s, own) in Stations(path, PierSpacing))
            {
                if (!own) continue;
                var f = FrameOf(s);
                // No support where the ramp has nearly reached the ground.
                if (f.Top + depth - Ground(s.Position) < 0.05f) continue;
                Support(path.Own, s.Position, s.Tangent, f.Top + depth);
            }
        }

        /// <summary>Stations evenly spaced from the edge to the inner end. Own is false where another path or tile places
        /// the support: on the shared edge unless this is the link's first tile (or the neighbour has no such parts), at
        /// the middle of a through curve unless this is its primary half, and where a spur merges.</summary>
        private static IEnumerable<(RouteSample Sample, bool Own)> Stations(RoutePath path, float spacing, bool edgeAlways = false)
        {
            int n = Math.Max(1, (int)Math.Round(path.Length / spacing));
            for (int k = 0; k <= n; k++)
            {
                bool own = k == 0 ? path.First || edgeAlways
                    : k < n || path.End == RouteEnd.DeadEnd || (path.End == RouteEnd.Through && path.Primary);
                yield return (SampleAt(path, path.Length * k / n), own);
            }
        }

        private static RouteSample SampleAt(RoutePath path, float along)
        {
            var samples = path.Samples;
            for (int i = 1; i < samples.Count; i++)
            {
                if (samples[i].Along < along && i < samples.Count - 1) continue;
                var (a, b) = (samples[i - 1], samples[i]);
                float t = b.Along > a.Along ? Math.Clamp((along - a.Along) / (b.Along - a.Along), 0, 1) : 0;
                return new RouteSample(Flat.Lerp(a.Position, b.Position, t), Flat.Normalize(Flat.Lerp(a.Tangent, b.Tangent, t)),
                    along, a.Blend + (b.Blend - a.Blend) * t, a.Lift + (b.Lift - a.Lift) * t);
            }
            return samples[0];
        }

        private static ProfileEdge[] ProfileOf(RouteLook look) => look == RouteLook.Maglev ? MaglevProfile : JumplaneProfile;

        /// <summary>One length of deck between two frames, each profile edge a quad with its own flat normal. At a junction
        /// the edges are split finely, for the distance to the other decks.</summary>
        private void Sweep(ProfileEdge[] profile, Frame a, Frame b)
        {
            foreach (var e in _rivals != null ? profile.SelectMany(Split) : profile)
            {
                // Profiles run clockwise (across to the right, up), so the outward normal is the edge turned left.
                var edge = e.B - e.A;
                var local = new Vector2(-edge.Y, edge.X).Normalized();
                Vector3 Normal(Frame f) => (f.Side * local.X + Vector3.Up * local.Y).Normalized();
                int i0 = Vertex(a.Point(e.A.X, e.A.Y), Normal(a), new Vector2(a.Along, e.A.X), e);
                int i1 = Vertex(a.Point(e.B.X, e.B.Y), Normal(a), new Vector2(a.Along, e.B.X), e);
                int i2 = Vertex(b.Point(e.B.X, e.B.Y), Normal(b), new Vector2(b.Along, e.B.X), e);
                int i3 = Vertex(b.Point(e.A.X, e.A.Y), Normal(b), new Vector2(b.Along, e.A.X), e);
                Quad(i0, i1, i2, i3, Normal(a) + Normal(b));
            }
        }

        private static IEnumerable<ProfileEdge> Split(ProfileEdge e)
        {
            int n = Math.Max(1, (int)MathF.Ceiling((e.B - e.A).Length() / FineStep));
            for (int k = 0; k < n; k++) yield return e with { A = e.A.Lerp(e.B, (float)k / n), B = e.A.Lerp(e.B, (float)(k + 1) / n) };
        }

        /// <summary>Closes the deck at a frame: a fan over the profile, facing along the tangent times sign.</summary>
        private void Cap(ProfileEdge[] profile, Frame f, int sign)
        {
            var normal = new Vector3(f.Tangent.X, 0, f.Tangent.Y) * sign;
            var paint = new ProfileEdge(default, default, profile.Any(e => e.Surface == Concrete) ? ConcreteColour : SteelColour,
                profile.Any(e => e.Surface == Concrete) ? Concrete : Metal);
            int middle = Vertex(f.Point(0, -0.04f), normal, new Vector2(f.Along, 0), paint);
            foreach (var e in profile)
            {
                int a = Vertex(f.Point(e.A.X, e.A.Y), normal, new Vector2(f.Along, e.A.X), paint);
                int b = Vertex(f.Point(e.B.X, e.B.Y), normal, new Vector2(f.Along, e.B.X), paint);
                Triangle(middle, a, b, normal);
            }
        }

        /// <summary>The support under a deck whose underside is at height bottom.</summary>
        private void Support(RouteLook look, Flat at, Flat tangent, float bottom)
        {
            var side = new Vector3(-tangent.Y, 0, tangent.X);
            var along = new Vector3(tangent.X, 0, tangent.Y);
            var foot = new Vector3(at.X, (float)field.SurfaceHeightAt(at.X, at.Y), at.Y);
            float lowest = Math.Min(foot.Y, Math.Min(
                (float)field.SurfaceHeightAt(at.X + side.X * 0.08f, at.Y + side.Z * 0.08f),
                (float)field.SurfaceHeightAt(at.X - side.X * 0.08f, at.Y - side.Z * 0.08f))) - 0.03f;
            var top = foot with { Y = bottom };
            if (look == RouteLook.Maglev)
            {
                // A square column on a footing, under a cap as wide as the beam.
                Box(foot with { Y = lowest }, top - new Vector3(0, 0.02f, 0), along, side, 0.024f, 0.03f, ConcreteColour, Concrete);
                Box(top - new Vector3(0, 0.026f, 0), top, along, side, 0.03f, 0.07f, ConcreteColour, Concrete);
                Box(foot with { Y = lowest }, foot + new Vector3(0, 0.012f, 0), along, side, 0.045f, 0.05f, ConcreteColour, Concrete);
                return;
            }
            // Two splayed steel legs with a cross tie and an X brace, on pads.
            var leftFoot = foot with { Y = lowest } + side * 0.078f;
            var rightFoot = foot with { Y = lowest } - side * 0.078f;
            var leftTop = top + side * 0.026f;
            var rightTop = top - side * 0.026f;
            Strut(leftFoot, leftTop, along, 0.009f, SteelColour);
            Strut(rightFoot, rightTop, along, 0.009f, SteelColour);
            var tieLeft = leftFoot.Lerp(leftTop, 0.62f);
            var tieRight = rightFoot.Lerp(rightTop, 0.62f);
            Strut(tieLeft, tieRight, along, 0.006f, SteelColour);
            Strut(leftFoot.Lerp(leftTop, 0.15f), tieRight, along, 0.004f, SteelColour);
            Strut(rightFoot.Lerp(rightTop, 0.15f), tieLeft, along, 0.004f, SteelColour);
            Box(top - new Vector3(0, 0.01f, 0), top, along, side, 0.02f, 0.04f, SteelColour, Metal);
            foreach (var f in new[] { leftFoot, rightFoot })
                Box(f, f + new Vector3(0, 0.045f, 0), along, side, 0.016f, 0.016f, ConcreteColour, Concrete);
        }

        /// <summary>Portal masts over an electric railroad and the contact wire strung between them.</summary>
        private void Catenary(RoutePath path)
        {
            // The electric line ends at a portal on the edge where it meets plain track.
            bool edgeAlways = path.Other != RouteLook.ElectricRailroad;
            // The wire follows the track in short spans, so it stays over the rails on a curve.
            Vector3? last = null;
            foreach (var s in RouteJunction.Resample(path, WireStep))
            {
                var wire = WireAt(s.Position);
                if (last is { } from) Strut(from, wire, new Vector3(-s.Tangent.Y, 0, s.Tangent.X), 0.0028f, WireColour);
                last = wire;
            }
            foreach (var (s, own) in Stations(path, MastSpacing, edgeAlways))
            {
                var side = new Vector3(-s.Tangent.Y, 0, s.Tangent.X);
                var along = new Vector3(s.Tangent.X, 0, s.Tangent.Y);
                var at = new Vector3(s.Position.X, 0, s.Position.Y);
                float y = (float)field.SurfaceHeightAt(at.X, at.Z);
                var wire = WireAt(s.Position);
                if (!own) continue;
                foreach (float k in new[] { -1f, 1f })
                {
                    var pole = at + side * (MastReach * k);
                    float g = (float)field.SurfaceHeightAt(pole.X, pole.Z);
                    Box(pole with { Y = g - 0.02f }, pole with { Y = y + MastHeight }, along, side, 0.0055f, 0.0055f, MastColour, Metal);
                }
                var beam = at + new Vector3(0, y + MastHeight - 0.006f, 0);
                Box(beam, beam + new Vector3(0, 0.008f, 0), along, side, 0.004f, MastReach + 0.01f, MastColour, Metal);
                Strut(beam, wire, along, 0.0025f, WireColour);
            }
        }

        private Vector3 WireAt(Flat p) => new(p.X, Height(p) + WireHeight, p.Y);

        /// <summary>A box from the middle of its base to the middle of its top, half sizes along and across.</summary>
        private void Box(Vector3 from, Vector3 to, Vector3 along, Vector3 side, float halfAlong, float halfSide, Color colour, float surface)
        {
            var paint = new ProfileEdge(default, default, colour, surface);
            var a = along * halfAlong;
            var s = side * halfSide;
            Vector3[] corners = [-a - s, a - s, a + s, -a + s];
            for (int k = 0; k < 4; k++)
            {
                var c0 = corners[k];
                var c1 = corners[(k + 1) % 4];
                var normal = (c0 + c1).Normalized();
                Face(from + c0, from + c1, to + c1, to + c0, normal, paint);
            }
            Face(to + corners[0], to + corners[1], to + corners[2], to + corners[3], Vector3.Up, paint);
        }

        /// <summary>A thin square bar between two points; flat is any direction across the bar.</summary>
        private void Strut(Vector3 from, Vector3 to, Vector3 flat, float half, Color colour)
        {
            var axis = (to - from).Normalized();
            var u = (flat - axis * flat.Dot(axis)).Normalized() * half;
            var v = axis.Cross(u).Normalized() * half;
            var paint = new ProfileEdge(default, default, colour, Metal);
            Vector3[] corners = [u + v, u - v, -u - v, -u + v];
            for (int k = 0; k < 4; k++)
            {
                var c0 = corners[k];
                var c1 = corners[(k + 1) % 4];
                Face(from + c0, from + c1, to + c1, to + c0, (c0 + c1).Normalized(), paint);
            }
        }

        private void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, ProfileEdge paint)
        {
            int i0 = Vertex(a, normal, Vector2.Zero, paint);
            int i1 = Vertex(b, normal, Vector2.Zero, paint);
            int i2 = Vertex(c, normal, Vector2.Zero, paint);
            int i3 = Vertex(d, normal, Vector2.Zero, paint);
            Quad(i0, i1, i2, i3, normal);
        }

        private int Vertex(Vector3 p, Vector3 normal, Vector2 uv, ProfileEdge paint)
        {
            _vertices.Add(p);
            _normals.Add(normal);
            _uvs.Add(uv);
            _uv2s.Add(new Vector2(paint.Surface, _rivals != null ? Math.Min(NoRival, RouteJunction.Margin(_own, _rivals, new Flat(p.X, p.Z)) - _yield) : NoRival));
            _colors.Add(paint.Colour);
            return _vertices.Count - 1;
        }

        private void Quad(int a, int b, int c, int d, Vector3 normal)
        {
            Triangle(a, b, c, normal);
            Triangle(a, c, d, normal);
        }

        /// <summary>Wound so the front face (clockwise in Godot) looks along the normal.</summary>
        private void Triangle(int a, int b, int c, Vector3 normal)
        {
            var face = (_vertices[b] - _vertices[a]).Cross(_vertices[c] - _vertices[a]);
            if (face.Dot(normal) > 0) (b, c) = (c, b);
            _indices.Add(a); _indices.Add(b); _indices.Add(c);
        }

        public ArrayMesh? Build(Material material)
        {
            if (_indices.Count == 0) return null;
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = _vertices.ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = _normals.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV] = _uvs.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV2] = _uv2s.ToArray();
            arrays[(int)Mesh.ArrayType.Color] = _colors.ToArray();
            arrays[(int)Mesh.ArrayType.Index] = _indices.ToArray();
            var mesh = new ArrayMesh();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            mesh.SurfaceSetMaterial(0, material);
            return mesh;
        }
    }
}
