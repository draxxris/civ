using Ariel.Client.Terrain;
using Ariel.Game;
using Godot;
using Flat = System.Numerics.Vector2;

namespace Ariel.Client;

internal sealed partial class MapRenderer
{
    /// <summary>
    /// The 3D parts of routes, one opaque mesh for the whole map (route_structure.gdshader): maglev and jumplane decks swept
    /// along each half path with their piers or lattice legs, the viaduct under a ground route that stands clear of the
    /// ground (a railroad keeping its grade, or a ground route climbing to meet a deck), bridges over rivers, and the portal
    /// masts and contact wire of electric railroads. Heights come from <see cref="RouteProfile"/>. Where a deck meets
    /// another look, its outline morphs into the other's over the crossfade, so the two structures merge into one.
    /// Supports stand at evenly spaced stations whose spacing divides each half path, so they never crowd a tile edge; a
    /// station on a shared edge belongs to the link's first tile and the middle of a through curve to its primary half, so
    /// no support is placed twice.
    /// </summary>
    private sealed class RouteParts(TerrainField field)
    {
        private const float PierSpacing = 0.8f, MastSpacing = 0.8f;
        private const float MastHeight = 0.15f, MastReach = 0.105f, WireHeight = 0.13f;
        private const float Concrete = 1, Glow = 2, Deck = 3, Metal = 4, Masonry = 5, Planks = 6;
        /// <summary>A support stands where the underside of a deck is at least this high over the ground.</summary>
        private const float SupportClearance = 0.05f;

        private static readonly Color ConcreteColour = new(0.74f, 0.73f, 0.70f);
        private static readonly Color GuidewayColour = new(0.42f, 0.43f, 0.45f);
        private static readonly Color DeckColour = new(0.50f, 0.55f, 0.62f);
        private static readonly Color SteelColour = new(0.33f, 0.36f, 0.41f);
        private static readonly Color GlowColour = new(0.35f, 0.78f, 1.0f);
        private static readonly Color MastColour = new(0.56f, 0.57f, 0.58f);
        private static readonly Color WireColour = new(0.16f, 0.16f, 0.17f);
        private static readonly Color PlankColour = new(0.55f, 0.40f, 0.25f);
        private static readonly Color TimberColour = new(0.38f, 0.27f, 0.17f);
        private static readonly Color StoneColour = new(0.70f, 0.65f, 0.56f);
        private static readonly Color GirderColour = new(0.36f, 0.40f, 0.44f);
        private static readonly Color TrussColour = new(0.30f, 0.35f, 0.38f);

        /// <summary>The colour and surface (UV2.x) of a part.</summary>
        private readonly record struct Paint(Color Colour, float Surface);

        /// <summary>A closed deck cross-section, points (across, height below the deck top) running clockwise from its top
        /// left; edge k runs from point k to the next and has paint k. Fraction k is how far round the outline point k
        /// lies, so two outlines can be morphed point for point.</summary>
        private sealed class Outline
        {
            public Outline(Flat[] points, Func<int, Paint> paint)
            {
                Points = points;
                Paints = Enumerable.Range(0, points.Length).Select(paint).ToArray();
                var lengths = Enumerable.Range(0, points.Length).Select(k => Flat.Distance(points[k], points[(k + 1) % points.Length])).ToArray();
                Perimeter = lengths.Sum();
                Fractions = new float[points.Length];
                for (int k = 1; k < points.Length; k++) Fractions[k] = Fractions[k - 1] + lengths[k - 1] / Perimeter;
            }

            public Flat[] Points { get; }
            public Paint[] Paints { get; }
            public float[] Fractions { get; }
            public float Perimeter { get; }

            private int EdgeAt(float f)
            {
                int k = Points.Length - 1;
                while (k > 0 && Fractions[k] > f) k--;
                return k;
            }

            public Flat At(float f)
            {
                int k = EdgeAt(f);
                float end = k + 1 < Points.Length ? Fractions[k + 1] : 1;
                float t = end > Fractions[k] ? (f - Fractions[k]) / (end - Fractions[k]) : 0;
                return Flat.Lerp(Points[k], Points[(k + 1) % Points.Length], Math.Clamp(t, 0, 1));
            }

            public Paint PaintAt(float f) => Paints[EdgeAt(f)];
        }

        /// <summary>An outline placed at one frame: its points and the paint of each edge.</summary>
        private readonly record struct Shape(Flat[] Points, Paint[] Paints);

        /// <summary>A concrete box beam with a sunken guideway down its top.</summary>
        private static readonly Outline MaglevOutline = new(
            [new(-0.075f, 0), new(-0.04f, 0), new(-0.04f, -0.016f), new(0.04f, -0.016f), new(0.04f, 0), new(0.075f, 0),
             new(0.075f, -0.05f), new(0.05f, -0.075f), new(-0.05f, -0.075f), new(-0.075f, -0.05f)],
            k => k == 2 ? new(GuidewayColour, Metal) : new(ConcreteColour, Concrete));

        /// <summary>A slim steel deck with glowing chamfers along both top edges and a keel underneath.</summary>
        private static readonly Outline JumplaneOutline = new(
            [new(-0.064f, -0.012f), new(-0.05f, 0), new(0.05f, 0), new(0.064f, -0.012f), new(0.064f, -0.03f),
             new(0.022f, -0.05f), new(-0.022f, -0.05f), new(-0.064f, -0.03f)],
            k => k is 0 or 2 ? new(GlowColour, Glow) : k == 1 ? new(DeckColour, Deck) : new(SteelColour, Metal));

        /// <summary>The concrete girder under a raised ground route, wide enough for its bed.</summary>
        private static readonly Outline ViaductOutline = new(
            [new(-0.1f, 0), new(0.1f, 0), new(0.1f, -0.035f), new(0.075f, -0.055f), new(-0.075f, -0.055f), new(-0.1f, -0.035f)],
            _ => new(ConcreteColour, Concrete));

        private static readonly Outline WoodOutline = new(
            [new(-0.096f, 0), new(0.096f, 0), new(0.096f, -0.02f), new(-0.096f, -0.02f)],
            k => k == 0 ? new(PlankColour, Planks) : new(TimberColour, Planks));

        private static readonly Outline SteelOutline = new(
            [new(-0.1f, 0), new(0.1f, 0), new(0.1f, -0.032f), new(-0.1f, -0.032f)],
            _ => new(GirderColour, Metal));

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
        /// <summary>At a junction, deck samples and outline edges at most this far apart, so the interpolated distance to
        /// the other decks stays close to the truth.</summary>
        private const float FineStep = 0.02f;

        /// <summary>At a junction the decks overlap. Each keeps only the parts nearer its own centre line than any other
        /// deck's (the shader cuts the rest), so their tops join as one deck with open channels and no curb or glowing edge
        /// across another deck.</summary>
        public void Add(RouteTilePlan plan, RouteProfile profile)
        {
            bool junction = RouteJunction.Overlaps(plan);
            foreach (var path in plan.Paths)
            {
                var laid = profile.Of(path);
                _rivals = junction ? RouteJunction.Others(plan, path) : null;
                _own = path.Samples.Select(s => s.Position).ToArray();
                _yield = RouteJunction.Yield(path);
                if (RouteLooks.Elevated(path.Own)) DeckAlong(path, laid, profile);
                else Viaduct(path, laid, profile);
                _rivals = null;
                if (laid.Bridge is { } bridge) Bridge(bridge, path.Own);
                if (path.Own == RouteLook.ElectricRailroad) Catenary(path, laid);
            }
            // A lone tile of elevated route: one support under a short deck stub.
            if (plan.Paths.Count == 0 && RouteLooks.Elevated(plan.Look))
            {
                float top = profile.DeckTop(plan.Centre, RouteLooks.Style(plan.Look).Lift);
                var outline = OutlineOf(plan.Look);
                var shape = Place(outline, null, 0, Breaks(outline, null, false));
                Support(plan.Look, plan.Centre, Flat.UnitX, top + Depth(outline));
                var a = new Frame(plan.Centre - Flat.UnitX * 0.12f, Flat.UnitX, top, 0);
                var b = new Frame(plan.Centre + Flat.UnitX * 0.12f, Flat.UnitX, top, 0.24f);
                Sweep(a, shape, b, shape);
                Cap(a, shape, -1);
                Cap(b, shape, 1);
            }
        }

        /// <summary>A point on a path: position, unit direction toward the inner end, deck top height and along.</summary>
        private readonly record struct Frame(Flat At, Flat Tangent, float Top, float Along)
        {
            public Vector3 Side => new(-Tangent.Y, 0, Tangent.X);
            public Vector3 Point(float across, float up) => new Vector3(At.X, Top + up, At.Y) + Side * across;
            public Vector3 Ground => new(At.X, Top, At.Y);
        }

        private float Height(Flat p) => (float)field.SurfaceHeightAt(p.X, p.Y);

        private static Outline OutlineOf(RouteLook look) => look switch
        {
            RouteLook.Maglev => MaglevOutline,
            RouteLook.Jumplane => JumplaneOutline,
            _ => ViaductOutline,
        };

        private static float Depth(Outline outline) => outline.Points.Min(p => p.Y);

        /// <summary>The fractions at which outlines are placed: every corner of both, so each keeps its corners exactly
        /// while it morphs, and at a junction enough more that no edge is longer than FineStep.</summary>
        private static float[] Breaks(Outline a, Outline? b, bool fine)
        {
            var breaks = a.Fractions.Concat(b?.Fractions ?? []).Order().ToList();
            var unique = new List<float>();
            foreach (float f in breaks)
                if (unique.Count == 0 || f - unique[^1] > 1e-4f) unique.Add(f);
            if (!fine) return unique.ToArray();
            float perimeter = Math.Max(a.Perimeter, b?.Perimeter ?? 0);
            var split = new List<float>();
            for (int k = 0; k < unique.Count; k++)
            {
                float next = k + 1 < unique.Count ? unique[k + 1] : 1;
                int n = Math.Max(1, (int)MathF.Ceiling((next - unique[k]) * perimeter / FineStep));
                for (int j = 0; j < n; j++) split.Add(unique[k] + (next - unique[k]) * j / n);
            }
            return split.ToArray();
        }

        /// <summary>Outline a morphed toward b by t, at the given fractions; each edge takes the paint of the nearer.</summary>
        private static Shape Place(Outline a, Outline? b, float t, float[] breaks)
        {
            var points = new Flat[breaks.Length];
            var paints = new Paint[breaks.Length];
            for (int k = 0; k < breaks.Length; k++)
            {
                var p = a.At(breaks[k]);
                points[k] = b == null ? p : Flat.Lerp(p, b.At(breaks[k]), t);
                float middle = (breaks[k] + (k + 1 < breaks.Length ? breaks[k + 1] : 1)) / 2;
                paints[k] = (b != null && t > 0.5f ? b : a).PaintAt(middle);
            }
            return new Shape(points, paints);
        }

        /// <summary>The frames of a path: its laid-out points, or at a junction finer ones.</summary>
        private List<RoutePoint> Points(RoutePath path, RoutePathProfile laid) =>
            _rivals != null ? RouteJunction.Resample(path, FineStep).Select(s => laid.At(s.Along)).ToList() : laid.Points.ToList();

        private void DeckAlong(RoutePath path, RoutePathProfile laid, RouteProfile profile)
        {
            var own = OutlineOf(path.Own);
            var other = path.Other == path.Own ? null : OutlineOf(path.Other);
            var breaks = Breaks(own, other, _rivals != null);
            // Along runs the same way on both sides of every edge (toward +x), so the deck's chevrons all point one way.
            float sign = path.Samples[^1].Position.X >= path.Samples[0].Position.X ? 1 : -1;
            var points = Points(path, laid);
            Frame? last = null;
            Shape lastShape = default;
            foreach (var p in points)
            {
                var frame = new Frame(p.Sample.Position, p.Sample.Tangent, p.Top, p.Sample.Along * sign);
                var shape = Place(own, other, p.Sample.Blend, breaks);
                if (last is { } from) Sweep(from, lastShape, frame, shape);
                (last, lastShape) = (frame, shape);
            }
            if (path.End == RouteEnd.DeadEnd && last is { } end) Cap(end, lastShape, 1);
            float depth = Depth(own);
            foreach (var (s, owned) in Stations(path, PierSpacing))
            {
                if (!owned) continue;
                var p = laid.At(s.Along);
                // No support where the deck has come down close to the ground.
                if (p.Top + depth - profile.GroundUnder(s.Position) < SupportClearance) continue;
                Support(path.Own, s.Position, s.Tangent, p.Top + depth);
            }
        }

        /// <summary>The girder under each stretch of a ground route that stands clear of the ground and is not on a bridge,
        /// morphing into the neighbour's deck where it meets one, on lattice legs where it is high enough.</summary>
        private void Viaduct(RoutePath path, RoutePathProfile laid, RouteProfile profile)
        {
            var other = RouteLooks.Elevated(path.Other) ? OutlineOf(path.Other) : null;
            var breaks = Breaks(ViaductOutline, other, _rivals != null);
            var points = Points(path, laid);
            bool Raised(RoutePoint p) => (p.Clearance > RouteProfile.RaisedClearance || (other != null && p.Sample.Blend > 0))
                && !laid.OnBridge(p.Distance, -0.02f);
            var run = new List<RoutePoint>();
            for (int i = 0; i <= points.Count; i++)
            {
                bool keep = i < points.Count && (Raised(points[i]) || (i > 0 && Raised(points[i - 1]))
                    || (i + 1 < points.Count && Raised(points[i + 1])));
                if (keep)
                {
                    run.Add(points[i]);
                    continue;
                }
                if (run.Count > 1) Girder(run, other, breaks, run[0].Distance > 0, run[^1].Distance < laid.Points[^1].Distance - 1e-3f);
                run.Clear();
            }
            float depth = Depth(ViaductOutline);
            foreach (var (s, owned) in Stations(path, PierSpacing))
            {
                if (!owned) continue;
                var p = laid.At(s.Along);
                if (laid.OnBridge(p.Distance, 0.08f) || field.Rivers.Near(s.Position.X, s.Position.Y)) continue;
                if (p.Top + depth - profile.GroundUnder(s.Position) < SupportClearance) continue;
                Support(RouteLook.Jumplane, s.Position, s.Tangent, p.Top + depth);
            }
        }

        private void Girder(List<RoutePoint> run, Outline? other, float[] breaks, bool capStart, bool capEnd)
        {
            Frame? last = null;
            Shape lastShape = default;
            foreach (var p in run)
            {
                var frame = new Frame(p.Sample.Position, p.Sample.Tangent, p.Top, p.Sample.Along);
                var shape = Place(ViaductOutline, other, other == null ? 0 : p.Sample.Blend, breaks);
                if (last is { } from) Sweep(from, lastShape, frame, shape);
                else if (capStart) Cap(frame, shape, -1);
                (last, lastShape) = (frame, shape);
            }
            if (capEnd && last is { } end) Cap(end, lastShape, 1);
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
                    along, a.Blend + (b.Blend - a.Blend) * t);
            }
            return samples[0];
        }

        /// <summary>Evenly spaced positions over a bridge, including both ends; the spacing is the nearest to the given one
        /// that divides the span.</summary>
        private static IEnumerable<float> Spaced(RouteBridge bridge, float spacing)
        {
            int n = Math.Max(1, (int)MathF.Round(bridge.Length / spacing));
            for (int k = 0; k <= n; k++) yield return bridge.Start + bridge.Length * k / n;
        }

        private static Frame FrameOf(RoutePoint p) => new(p.Sample.Position, p.Sample.Tangent, p.Top, p.Distance);

        /// <summary>The bridge over a river, drawn whole by one tile of the link.</summary>
        private void Bridge(RouteBridge bridge, RouteLook look)
        {
            switch (bridge.Kind)
            {
                case BridgeKind.Wood: WoodBridge(bridge); break;
                case BridgeKind.Stone: StoneBridge(bridge); break;
                default: SteelBridge(bridge); break;
            }
        }

        /// <summary>A plank deck on timber trestles, with a post-and-rail fence along both sides.</summary>
        private void WoodBridge(RouteBridge bridge)
        {
            SweepAlong(bridge.Points, WoodOutline);
            var paint = new Paint(TimberColour, Planks);
            // Fence posts with a top and a middle rail.
            foreach (float k in new[] { -1f, 1f })
            {
                Vector3? lastTop = null, lastMiddle = null;
                foreach (float d in Spaced(bridge, 0.07f))
                {
                    var f = FrameOf(bridge.At(d));
                    var foot = f.Point(0.088f * k, -0.012f);
                    var top = f.Point(0.088f * k, 0.036f);
                    Box(foot, top, Along(f), f.Side, 0.0055f, 0.0055f, paint);
                    var rail = f.Point(0.088f * k, 0.032f);
                    var middle = f.Point(0.088f * k, 0.016f);
                    if (lastTop is { } a) Strut(a, rail, f.Side, 0.0035f, paint);
                    if (lastMiddle is { } m) Strut(m, middle, f.Side, 0.0028f, paint);
                    (lastTop, lastMiddle) = (rail, middle);
                }
            }
            // Trestles: a pair of piles under a cap beam, braced across, between the banks.
            var inner = Spaced(bridge, 0.17f).ToList();
            foreach (float d in inner.Skip(1).SkipLast(1))
            {
                var f = FrameOf(bridge.At(d));
                var cap = f.Point(0, -0.02f);
                Box(cap - new Vector3(0, 0.012f, 0), cap, Along(f), f.Side, 0.01f, 0.085f, paint);
                var feet = new List<Vector3>();
                foreach (float k in new[] { -1f, 1f })
                {
                    var top = f.Point(0.065f * k, -0.026f);
                    var foot = top with { Y = Height(new Flat(top.X, top.Z)) - 0.04f };
                    Strut(foot, top, Along(f), 0.0065f, paint);
                    feet.Add(foot.Lerp(top, 0.25f));
                }
                Strut(feet[0], f.Point(0.065f, -0.03f), Along(f), 0.0035f, paint);
                Strut(feet[1], f.Point(-0.065f, -0.03f), Along(f), 0.0035f, paint);
            }
        }

        /// <summary>A masonry bridge with parapets on round arches. The arches spring from the river bed; between two
        /// arches their springs meet as a pier, and at the banks they sink into the ground.</summary>
        private void StoneBridge(RouteBridge bridge)
        {
            int arches = Math.Max(1, (int)MathF.Round(bridge.Length / 0.3f));
            float width = bridge.Length / arches, spring = bridge.Low - 0.03f;
            float crown = bridge.Points.Max(p => p.Top) - 0.04f;
            var paint = new Paint(StoneColour, Masonry);
            Shape ShapeAt(RoutePoint p)
            {
                float x = ((p.Distance - bridge.Start) % width) / width * 2 - 1;
                if (p.Distance >= bridge.End - 1e-4f) x = 1;
                // Piers a little thicker than the arches' springs.
                float r = Math.Min(1, Math.Abs(x) * 1.12f);
                float under = spring + (crown - spring) * MathF.Sqrt(1 - r * r);
                float bottom = Math.Min(-0.03f, under - p.Top);
                Flat[] points = [new(-0.1f, 0.028f), new(-0.084f, 0.028f), new(-0.084f, 0), new(0.084f, 0), new(0.084f, 0.028f),
                    new(0.1f, 0.028f), new(0.1f, bottom), new(-0.1f, bottom)];
                return new Shape(points, Enumerable.Repeat(paint, points.Length).ToArray());
            }
            int steps = Math.Max(8, (int)MathF.Ceiling(bridge.Length / 0.012f));
            Frame? last = null;
            Shape lastShape = default;
            for (int i = 0; i <= steps; i++)
            {
                var p = bridge.At(bridge.Start + bridge.Length * i / steps);
                var frame = FrameOf(p);
                var shape = ShapeAt(p);
                if (last is { } from) Sweep(from, lastShape, frame, shape);
                else Cap(frame, shape, -1);
                (last, lastShape) = (frame, shape);
            }
            Cap(last!.Value, lastShape, 1);
        }

        /// <summary>A steel deck between two through trusses, on concrete abutments at the banks, with a pier in the
        /// river under a long span.</summary>
        private void SteelBridge(RouteBridge bridge)
        {
            SweepAlong(bridge.Points, SteelOutline);
            var concrete = new Paint(ConcreteColour, Concrete);
            var truss = new Paint(TrussColour, Metal);
            foreach (float d in new[] { bridge.Start, bridge.End })
            {
                var f = FrameOf(bridge.At(d));
                var top = f.Point(0, -0.03f);
                Box(top with { Y = Height(f.At) - 0.05f }, top, Along(f), f.Side, 0.035f, 0.11f, concrete);
            }
            if (bridge.Length > 0.6f)
            {
                var f = FrameOf(bridge.At(bridge.Start + bridge.Length / 2));
                var top = f.Point(0, -0.03f);
                Box(top with { Y = bridge.Low - 0.05f }, top, Along(f), f.Side, 0.03f, 0.08f, concrete);
            }
            int panels = Math.Max(2, (int)MathF.Round(bridge.Length / 0.085f));
            const float rise = 0.085f, offset = 0.095f;
            var lows = new Vector3[2][];
            var highs = new Vector3[2][];
            for (int s = 0; s < 2; s++)
            {
                float k = s == 0 ? -1 : 1;
                lows[s] = new Vector3[panels + 1];
                highs[s] = new Vector3[panels + 1];
                for (int j = 0; j <= panels; j++)
                {
                    var f = FrameOf(bridge.At(bridge.Start + bridge.Length * j / panels));
                    lows[s][j] = f.Point(offset * k, -0.006f);
                    highs[s][j] = f.Point(offset * k, rise);
                }
                var side = FrameOf(bridge.Points[0]).Side;
                for (int j = 0; j < panels; j++) Strut(lows[s][j], lows[s][j + 1], Vector3.Up, 0.006f, truss);
                for (int j = 1; j < panels - 1; j++) Strut(highs[s][j], highs[s][j + 1], Vector3.Up, 0.006f, truss);
                Strut(lows[s][0], highs[s][1], side, 0.006f, truss);
                Strut(lows[s][panels], highs[s][panels - 1], side, 0.006f, truss);
                for (int j = 1; j < panels; j++) Strut(lows[s][j], highs[s][j], side, 0.0035f, truss);
                // Diagonals slope down toward the middle of the span.
                for (int j = 1; j < panels - 1; j++)
                    if (j < panels / 2) Strut(highs[s][j], lows[s][j + 1], side, 0.003f, truss);
                    else Strut(lows[s][j], highs[s][j + 1], side, 0.003f, truss);
            }
            for (int j = 1; j < panels; j++) Strut(highs[0][j], highs[1][j], Vector3.Up, 0.003f, truss);
        }

        private static Vector3 Along(Frame f) => new(f.Tangent.X, 0, f.Tangent.Y);

        private void SweepAlong(IReadOnlyList<RoutePoint> points, Outline outline)
        {
            var shape = Place(outline, null, 0, Breaks(outline, null, false));
            for (int i = 1; i < points.Count; i++) Sweep(FrameOf(points[i - 1]), shape, FrameOf(points[i]), shape);
            Cap(FrameOf(points[0]), shape, -1);
            Cap(FrameOf(points[^1]), shape, 1);
        }

        /// <summary>One length of deck between two frames, each outline edge a quad with its own flat normal.</summary>
        private void Sweep(Frame a, Shape sa, Frame b, Shape sb)
        {
            int n = sa.Points.Length;
            for (int k = 0; k < n; k++)
            {
                var (a0, a1) = (sa.Points[k], sa.Points[(k + 1) % n]);
                var (b0, b1) = (sb.Points[k], sb.Points[(k + 1) % n]);
                if ((a1 - a0).LengthSquared() < 1e-10f && (b1 - b0).LengthSquared() < 1e-10f) continue;
                // Outlines run clockwise (across to the right, up), so the outward normal is the edge turned left.
                Vector3 Normal(Frame f, Flat p0, Flat p1)
                {
                    var edge = p1 - p0;
                    if (edge.LengthSquared() < 1e-10f) edge = (b1 - b0) + (a1 - a0);
                    var local = Flat.Normalize(new Flat(-edge.Y, edge.X));
                    return (f.Side * local.X + Vector3.Up * local.Y).Normalized();
                }
                var na = Normal(a, a0, a1);
                var nb = Normal(b, b0, b1);
                var paint = sa.Paints[k];
                int i0 = Vertex(a.Point(a0.X, a0.Y), na, new Vector2(a.Along, a0.X), paint);
                int i1 = Vertex(a.Point(a1.X, a1.Y), na, new Vector2(a.Along, a1.X), paint);
                int i2 = Vertex(b.Point(b1.X, b1.Y), nb, new Vector2(b.Along, b1.X), paint);
                int i3 = Vertex(b.Point(b0.X, b0.Y), nb, new Vector2(b.Along, b0.X), paint);
                Quad(i0, i1, i2, i3, na + nb);
            }
        }

        /// <summary>Closes a deck at a frame: a fan over the outline, facing along the tangent times sign.</summary>
        private void Cap(Frame f, Shape shape, int sign)
        {
            var normal = new Vector3(f.Tangent.X, 0, f.Tangent.Y) * sign;
            var paint = shape.Paints.FirstOrDefault(p => p.Surface is Concrete or Masonry or Planks, new Paint(SteelColour, Metal));
            var centre = shape.Points.Aggregate(Flat.Zero, (sum, p) => sum + p) / shape.Points.Length;
            int middle = Vertex(f.Point(centre.X, centre.Y), normal, new Vector2(f.Along, 0), paint);
            for (int k = 0; k < shape.Points.Length; k++)
            {
                var (p0, p1) = (shape.Points[k], shape.Points[(k + 1) % shape.Points.Length]);
                int a = Vertex(f.Point(p0.X, p0.Y), normal, new Vector2(f.Along, p0.X), paint);
                int b = Vertex(f.Point(p1.X, p1.Y), normal, new Vector2(f.Along, p1.X), paint);
                Triangle(middle, a, b, normal);
            }
        }

        /// <summary>The support under a deck whose underside is at height bottom: a concrete column for the maglev,
        /// splayed steel legs for everything else.</summary>
        private void Support(RouteLook look, Flat at, Flat tangent, float bottom)
        {
            var side = new Vector3(-tangent.Y, 0, tangent.X);
            var along = new Vector3(tangent.X, 0, tangent.Y);
            var foot = new Vector3(at.X, (float)field.SurfaceHeightAt(at.X, at.Y), at.Y);
            float lowest = Math.Min(foot.Y, Math.Min(
                (float)field.SurfaceHeightAt(at.X + side.X * 0.08f, at.Y + side.Z * 0.08f),
                (float)field.SurfaceHeightAt(at.X - side.X * 0.08f, at.Y - side.Z * 0.08f))) - 0.03f;
            var top = foot with { Y = bottom };
            var concrete = new Paint(ConcreteColour, Concrete);
            if (look == RouteLook.Maglev)
            {
                // A square column on a footing, under a cap as wide as the beam.
                Box(foot with { Y = lowest }, top - new Vector3(0, 0.02f, 0), along, side, 0.024f, 0.03f, concrete);
                Box(top - new Vector3(0, 0.026f, 0), top, along, side, 0.03f, 0.07f, concrete);
                Box(foot with { Y = lowest }, foot + new Vector3(0, 0.012f, 0), along, side, 0.045f, 0.05f, concrete);
                return;
            }
            // Two splayed steel legs with a cross tie and an X brace, on pads.
            var steel = new Paint(SteelColour, Metal);
            var leftFoot = foot with { Y = lowest } + side * 0.078f;
            var rightFoot = foot with { Y = lowest } - side * 0.078f;
            var leftTop = top + side * 0.026f;
            var rightTop = top - side * 0.026f;
            Strut(leftFoot, leftTop, along, 0.009f, steel);
            Strut(rightFoot, rightTop, along, 0.009f, steel);
            var tieLeft = leftFoot.Lerp(leftTop, 0.62f);
            var tieRight = rightFoot.Lerp(rightTop, 0.62f);
            Strut(tieLeft, tieRight, along, 0.006f, steel);
            Strut(leftFoot.Lerp(leftTop, 0.15f), tieRight, along, 0.004f, steel);
            Strut(rightFoot.Lerp(rightTop, 0.15f), tieLeft, along, 0.004f, steel);
            Box(top - new Vector3(0, 0.01f, 0), top, along, side, 0.02f, 0.04f, steel);
            foreach (var f in new[] { leftFoot, rightFoot })
                Box(f, f + new Vector3(0, 0.045f, 0), along, side, 0.016f, 0.016f, concrete);
        }

        /// <summary>Portal masts over an electric railroad and the contact wire strung between them, over the rails
        /// wherever they run: on the ground, a viaduct or a bridge.</summary>
        private void Catenary(RoutePath path, RoutePathProfile laid)
        {
            // The electric line ends at a portal on the edge where it meets another look.
            bool edgeAlways = path.Other != RouteLook.ElectricRailroad;
            var wirePaint = new Paint(WireColour, Metal);
            var mastPaint = new Paint(MastColour, Metal);
            // The wire follows the track in short spans, so it stays over the rails on a curve.
            Vector3? last = null;
            foreach (var p in laid.Points)
            {
                var wire = WireAt(p);
                var s = p.Sample;
                if (last is { } from) Strut(from, wire, new Vector3(-s.Tangent.Y, 0, s.Tangent.X), 0.0028f, wirePaint);
                last = wire;
            }
            foreach (var (s, own) in Stations(path, MastSpacing, edgeAlways))
            {
                if (!own) continue;
                var p = laid.At(s.Along);
                var side = new Vector3(-s.Tangent.Y, 0, s.Tangent.X);
                var along = new Vector3(s.Tangent.X, 0, s.Tangent.Y);
                var at = new Vector3(s.Position.X, 0, s.Position.Y);
                bool raised = p.Clearance > RouteProfile.RaisedClearance;
                foreach (float k in new[] { -1f, 1f })
                {
                    var pole = at + side * (MastReach * k);
                    // On a deck the masts stand on its edge; on the ground they are sunk into it.
                    float foot = raised ? p.Top - 0.035f : (float)field.SurfaceHeightAt(pole.X, pole.Z) - 0.02f;
                    Box(pole with { Y = foot }, pole with { Y = p.Top + MastHeight }, along, side, 0.0055f, 0.0055f, mastPaint);
                }
                var beam = at + new Vector3(0, p.Top + MastHeight - 0.006f, 0);
                Box(beam, beam + new Vector3(0, 0.008f, 0), along, side, 0.004f, MastReach + 0.01f, mastPaint);
                Strut(beam, WireAt(p), along, 0.0025f, wirePaint);
            }
        }

        private static Vector3 WireAt(RoutePoint p) => new(p.Sample.Position.X, p.Top + WireHeight, p.Sample.Position.Y);

        /// <summary>A box from the middle of its base to the middle of its top, half sizes along and across.</summary>
        private void Box(Vector3 from, Vector3 to, Vector3 along, Vector3 side, float halfAlong, float halfSide, Paint paint)
        {
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
        private void Strut(Vector3 from, Vector3 to, Vector3 flat, float half, Paint paint)
        {
            var axis = (to - from).Normalized();
            if (Math.Abs(flat.Normalized().Dot(axis)) > 0.99f) flat = Math.Abs(axis.Y) < 0.9f ? Vector3.Up : Vector3.Right;
            var u = (flat - axis * flat.Dot(axis)).Normalized() * half;
            var v = axis.Cross(u).Normalized() * half;
            Vector3[] corners = [u + v, u - v, -u - v, -u + v];
            for (int k = 0; k < 4; k++)
            {
                var c0 = corners[k];
                var c1 = corners[(k + 1) % 4];
                Face(from + c0, from + c1, to + c1, to + c0, (c0 + c1).Normalized(), paint);
            }
        }

        private void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Paint paint)
        {
            int i0 = Vertex(a, normal, Vector2.Zero, paint);
            int i1 = Vertex(b, normal, Vector2.Zero, paint);
            int i2 = Vertex(c, normal, Vector2.Zero, paint);
            int i3 = Vertex(d, normal, Vector2.Zero, paint);
            Quad(i0, i1, i2, i3, normal);
        }

        private int Vertex(Vector3 p, Vector3 normal, Vector2 uv, Paint paint)
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
