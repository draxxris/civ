using System.Numerics;
using Ariel.Game;

namespace Ariel.Client.Terrain;

/// <summary>The structure that carries a route over a river.</summary>
public enum BridgeKind
{
    /// <summary>Planks on timber piles with a post-and-rail fence: cart paths and roads.</summary>
    Wood,
    /// <summary>Masonry arches under a deck with parapets: paved roads and highways.</summary>
    Stone,
    /// <summary>A steel deck between two trusses: railroads.</summary>
    Steel,
}

/// <summary>A point of a path laid over the terrain. Distance is the arc length from the shared edge in world units (along
/// a bridge, the signed distance from the edge: negative in the neighbour's tile). Top is the height of the route's
/// surface: the deck top of an elevated route, the bed of a ground route. Ground is the terrain height under the centre
/// line.</summary>
public readonly record struct RoutePoint(RouteSample Sample, float Distance, float Top, float Ground)
{
    /// <summary>Height of the route surface over the ground under it.</summary>
    public float Clearance => Top - Ground;
}

/// <summary>A bridge over a river, drawn whole by one tile of the link: points from bank to bank in that tile's frame, with
/// tangents and distances running into that tile. Low is the lowest ground under the span (the river bed).</summary>
public sealed record RouteBridge(BridgeKind Kind, IReadOnlyList<RoutePoint> Points, float Low)
{
    public float Start => Points[0].Distance;
    public float End => Points[^1].Distance;
    public float Length => End - Start;

    /// <summary>The point at a distance in [Start, End], interpolated.</summary>
    public RoutePoint At(float distance) => RouteProfile.Interpolate(Points, distance, p => p.Distance);
}

/// <summary>One half path laid over the terrain: its points every <see cref="RouteProfile.Step"/> pattern units, and the
/// part of a bridge it crosses.</summary>
public sealed class RoutePathProfile
{
    internal RoutePathProfile(RoutePath path, IReadOnlyList<RoutePoint> points, (float Start, float End)? span, bool bridged,
        RouteBridge? bridge)
    {
        Path = path;
        Bridged = bridged;
        Points = points;
        Span = span;
        Bridge = bridge;
    }

    public RoutePath Path { get; }
    public IReadOnlyList<RoutePoint> Points { get; }
    /// <summary>The bridge span on this half's link, in this half's distances (it may start in the neighbour's tile).</summary>
    public (float Start, float End)? Span { get; }
    /// <summary>Whether a bridge is drawn over the span (by this half or its partner). Without one, a raised ground route
    /// crosses the river on a plain viaduct.</summary>
    public bool Bridged { get; }
    /// <summary>The bridge this half draws: the whole span, on the link's first tile when both tiles are ground routes.</summary>
    public RouteBridge? Bridge { get; }

    /// <summary>Whether any of the path stands clear of the ground, on a ramp, viaduct or bridge.</summary>
    public bool Raised => Span != null || Points.Any(p => p.Clearance > RouteProfile.RaisedClearance);

    /// <summary>The point at an along value (pattern units), interpolated.</summary>
    public RoutePoint At(float along) => RouteProfile.Interpolate(Points, along, p => p.Sample.Along);

    /// <summary>Whether a distance from the edge lies on a drawn bridge's span, widened by a margin.</summary>
    public bool OnBridge(float distance, float margin = 0) =>
        Bridged && Span is { } s && distance >= s.Start - margin && distance <= s.End + margin;
}

/// <summary>
/// Lays every route path of the map over the terrain, engine-free. Ground routes follow the ground, except that:
/// <list type="bullet">
/// <item>Railroads keep a gentle grade (<see cref="RailBed"/>): a line climbs onto a viaduct before a hill in one straight
/// ramp to the hill's apex, runs straight from apex to apex over the dips between hills, and comes back down to the
/// ground beyond in one straight ramp.</item>
/// <item>Any ground route crossing a river rises onto a bridge from bank to bank.</item>
/// <item>Where a ground route meets an elevated one, the ground route climbs and the deck descends, each by half the height
/// between them, so they meet in the middle at the shared edge with a matching slope.</item>
/// </list>
/// Heights are found on a graph of all ground path points, joined where paths meet, so both tiles of an edge and all paths
/// of a junction agree.
/// </summary>
public sealed class RouteProfile
{
    /// <summary>Spacing of profile points in pattern units.</summary>
    public const float Step = 0.04f;
    /// <summary>Steepest rise of a railroad bed, in height per unit of distance.</summary>
    public const float RailGrade = 0.15f;
    /// <summary>Dips in the ground this shallow under a railroad are filled in, not bridged.</summary>
    public const float RailSlack = 0.02f;
    /// <summary>Half the length over which the foot of a railroad ramp is rounded.</summary>
    public const float SagRadius = 0.16f;
    /// <summary>Distance from the shared edge over which a ground route climbs and a deck descends to meet it.</summary>
    public const float RampLength = 0.6f;
    /// <summary>A bridge deck stands this far over the higher bank.</summary>
    public const float BridgeClearance = 0.03f;
    /// <summary>A bridge starts this far beyond the river's banks.</summary>
    public const float BridgeMargin = 0.05f;
    /// <summary>Steepest approach to a bridge.</summary>
    public const float BridgeGrade = 0.35f;
    /// <summary>A river this far from the edge, along the link, is crossed by the link.</summary>
    private const float CrossingReach = 0.3f;
    /// <summary>Clearance above which a ground route counts as raised.</summary>
    public const float RaisedClearance = 0.01f;

    private readonly Func<float, float, float> _ground;
    private readonly Dictionary<RoutePath, RoutePathProfile> _profiles = new(ReferenceEqualityComparer.Instance);

    /// <summary>Lays out the paths of the given tile plans. Ground is the visible ground height, river whether a point is in
    /// a river channel or its banks, crosses whether a short straight step crosses a river's centre line; period is the
    /// map's wrap period in x (0 when it does not wrap).</summary>
    public RouteProfile(IEnumerable<RouteTilePlan> plans, Func<float, float, float> ground, Func<float, float, bool> river,
        Func<Vector2, Vector2, bool> crosses, float period = 0)
    {
        _ground = ground;
        var halves = new List<Half>();
        var index = new NodeIndex(period);
        foreach (var plan in plans)
        {
            var through = new List<Half>();
            foreach (var path in plan.Paths)
            {
                var half = new Half(path, Layout(path), index);
                halves.Add(half);
                if (path.End == RouteEnd.Through) through.Add(half);
            }
            if (through.Count == 2) (through[0].Twin, through[1].Twin) = (through[1], through[0]);
        }
        var links = halves.GroupBy(h => h.Nodes[0]).Where(g => g.Count() == 2).Select(g => (g.First(), g.Last())).ToList();
        foreach (var (a, b) in links) (a.Partner, b.Partner) = (b, a);
        foreach (var (a, b) in links) FindBridge(a, b, river, crosses);

        var top = Grade(halves, index.Count);
        foreach (var (a, b) in links)
        {
            if (Elevated(a) == Elevated(b)) continue;
            var (low, high) = Elevated(a) ? (b, a) : (a, b);
            var edge = low.Points[0].Sample.Position;
            float delta = DeckTop(edge, RouteLooks.Style(high.Path.Own).Lift) - top[low.Nodes[0]];
            (low.Ramp, high.Ramp) = (delta, delta);
        }
        foreach (var half in halves) half.Tops = Tops(half, top);
        foreach (var half in halves)
            _profiles[half.Path] = new RoutePathProfile(half.Path, half.Profile(), half.Span is { } s ? half.SpanOf(s) : null,
                half.Bridged, half.Draws ? WholeBridge(half) : null);
    }

    /// <summary>The laid-out path.</summary>
    public RoutePathProfile Of(RoutePath path) => _profiles[path];

    /// <summary>Highest ground under a deck's width about a point.</summary>
    public float GroundUnder(Vector2 at)
    {
        float high = _ground(at.X, at.Y);
        for (int k = 0; k < 6; k++)
            high = MathF.Max(high, _ground(at.X + MathF.Cos(k * MathF.PI / 3) * 0.07f, at.Y + MathF.Sin(k * MathF.PI / 3) * 0.07f));
        return high;
    }

    /// <summary>Deck top over a point at full height: lift over the ground averaged over a disc about the point, so it rides
    /// over bumps instead of following them, but never closer than half its lift to the ground under it. It depends on the
    /// point alone, so the two tiles of an edge agree and the decks of a junction meet flush.</summary>
    public float DeckTop(Vector2 at, float lift)
    {
        float sum = _ground(at.X, at.Y);
        int count = 1;
        foreach (float r in new[] { 0.3f, 0.6f })
            for (int k = 0; k < 6; k++, count++)
                sum += _ground(at.X + MathF.Cos(k * MathF.PI / 3 + r) * r, at.Y + MathF.Sin(k * MathF.PI / 3 + r) * r);
        return MathF.Max(sum / count + lift, GroundUnder(at) + lift * 0.5f);
    }

    /// <summary>The bridge kind of a link between two looks, or null when neither runs on the ground.</summary>
    public static BridgeKind? BridgeOf(RouteLook a, RouteLook b)
    {
        var ground = new[] { a, b }.Where(l => !RouteLooks.Elevated(l)).ToList();
        if (ground.Count == 0) return null;
        return ground.Max() switch
        {
            RouteLook.Track or RouteLook.Road => BridgeKind.Wood,
            RouteLook.Paved or RouteLook.Highway => BridgeKind.Stone,
            _ => BridgeKind.Steel,
        };
    }

    private static bool Elevated(Half h) => RouteLooks.Elevated(h.Path.Own);
    private static bool Rail(Half h) => h.Path.Own is RouteLook.Railroad or RouteLook.ElectricRailroad;

    /// <summary>The path's samples every Step, with distances from the edge and the ground under each.</summary>
    private List<(RouteSample Sample, float Distance, float Ground)> Layout(RoutePath path)
    {
        var samples = RouteJunction.Resample(path, Step);
        var points = new List<(RouteSample, float, float)>(samples.Count);
        float distance = 0;
        for (int i = 0; i < samples.Count; i++)
        {
            if (i > 0) distance += Vector2.Distance(samples[i - 1].Position, samples[i].Position);
            var p = samples[i].Position;
            points.Add((samples[i], distance, _ground(p.X, p.Y)));
        }
        return points;
    }

    /// <summary>Finds the river crossing of a link, if any: of the wet stretches of the link curve that cross a river's
    /// centre line and are bounded by dry ground on both sides, the one nearest the shared edge. A curve that only passes
    /// a river's bank, at a bend, is not crossing it. Positions along the link are u: negative in a's tile, positive in
    /// b's.</summary>
    private static void FindBridge(Half a, Half b, Func<float, float, bool> river, Func<Vector2, Vector2, bool> crosses)
    {
        if (BridgeOf(a.Path.Own, b.Path.Own) is not { } kind) return;
        var shift = a.Points[0].Sample.Position - b.Points[0].Sample.Position;
        var line = new List<(float U, Vector2 At, float Ground)>();
        for (int i = a.Points.Count - 1; i >= 0; i--) line.Add((-a.Points[i].Distance, a.Points[i].Sample.Position, a.Points[i].Ground));
        for (int i = 1; i < b.Points.Count; i++) line.Add((b.Points[i].Distance, b.Points[i].Sample.Position + shift, b.Points[i].Ground));
        var wet = line.Select(p => river(p.At.X, p.At.Y)).ToArray();
        int first = -1, last = -1;
        float best = float.MaxValue;
        for (int i = 0; i < line.Count; i++)
        {
            if (!wet[i] || (i > 0 && wet[i - 1])) continue;
            int j = i;
            while (j < line.Count - 1 && wet[j + 1]) j++;
            // A river running alongside the whole link is not crossed.
            if (i == 0 || j == line.Count - 1) continue;
            bool crossed = false;
            float near = float.MaxValue;
            for (int k = i - 1; k <= j; k++)
            {
                crossed |= crosses(line[k].At, line[k + 1].At);
                if (k >= i) near = MathF.Min(near, MathF.Abs(line[k].U));
            }
            if (crossed && near <= CrossingReach && near < best) (first, last, best) = (i, j, near);
        }
        if (first < 0) return;
        float u0 = (line[first - 1].U + line[first].U) / 2 - BridgeMargin;
        float u1 = (line[last].U + line[last + 1].U) / 2 + BridgeMargin;
        float bank = MathF.Max(line[first - 1].Ground, line[last + 1].Ground);
        float low = line.Skip(first).Take(last - first + 1).Min(p => p.Ground);
        var span = new Span(kind, u0, u1, bank + BridgeClearance, low);
        a.Span = span with { Sign = -1 };
        b.Span = span;
        // A deck on one side makes its own way over: no bridge, but the ground half still rises over the water.
        bool bothGround = !Elevated(a) && !Elevated(b);
        (a.Bridged, b.Bridged) = (bothGround, bothGround);
        a.Draws = bothGround && a.Path.First;
        b.Draws = bothGround && b.Path.First;
    }

    /// <summary>The bed height at every node of the ground route graph, see <see cref="RailBed"/>.</summary>
    private static float[] Grade(List<Half> halves, int count)
    {
        var bed = new RailBed(count);
        foreach (var half in halves.Where(h => !Elevated(h)))
            for (int i = 0; i < half.Points.Count; i++)
            {
                bed.Add(half.Nodes[i], half.Floor(i), Rail(half), half.OnSpan(i));
                if (i > 0) bed.Join(half.Nodes[i - 1], half.Nodes[i], half.Points[i].Distance - half.Points[i - 1].Distance);
            }
        return bed.Solve();
    }

    /// <summary>Weight of a ramp at a distance from its edge: 1 at the edge, easing to 0 at RampLength.</summary>
    private static float RampWeight(float distance)
    {
        float t = Math.Clamp(distance / RampLength, 0, 1);
        return 1 - t * t * (3 - 2 * t);
    }

    /// <summary>Top of every point: the graded bed of a ground route or the deck of an elevated one, met halfway by the
    /// other at a ramp on its own edge or, for a through curve, on its twin's edge.</summary>
    private float[] Tops(Half half, float[] grade)
    {
        bool high = Elevated(half);
        float lift = RouteLooks.Style(half.Path.Own).Lift;
        float length = half.Points[^1].Distance;
        float far = half.Twin?.Ramp ?? 0, farOffset = half.Twin?.Points[^1].Distance ?? 0;
        var tops = new float[half.Points.Count];
        for (int i = 0; i < tops.Length; i++)
        {
            var p = half.Points[i];
            float shift = 0.5f * (half.Ramp * RampWeight(p.Distance) + far * RampWeight(farOffset + length - p.Distance));
            tops[i] = high ? DeckTop(p.Sample.Position, lift) - shift : grade[half.Nodes[i]] + shift;
        }
        return tops;
    }

    /// <summary>The whole bridge in the drawing half's frame: the partner's points first (reversed, shifted across any
    /// wrap and with distances negated), then its own, cut to the span.</summary>
    private static RouteBridge WholeBridge(Half half)
    {
        var span = half.SpanOf(half.Span!.Value);
        var partner = half.Partner!;
        var shift = half.Points[0].Sample.Position - partner.Points[0].Sample.Position;
        var line = new List<RoutePoint>();
        var theirs = partner.Profile();
        for (int i = theirs.Count - 1; i >= 1; i--)
        {
            var p = theirs[i];
            var s = p.Sample with { Position = p.Sample.Position + shift, Tangent = -p.Sample.Tangent, Along = -p.Sample.Along };
            line.Add(p with { Sample = s, Distance = -p.Distance });
        }
        line.AddRange(half.Profile());
        var cut = new List<RoutePoint> { Interpolate(line, span.Start, p => p.Distance) };
        cut.AddRange(line.Where(p => p.Distance > span.Start && p.Distance < span.End));
        cut.Add(Interpolate(line, span.End, p => p.Distance));
        return new RouteBridge(half.Span.Value.Kind, cut, half.Span.Value.Low);
    }

    /// <summary>Linear interpolation of points by a key that grows along them; clamped at the ends.</summary>
    internal static RoutePoint Interpolate(IReadOnlyList<RoutePoint> points, float key, Func<RoutePoint, float> keyOf)
    {
        if (key <= keyOf(points[0])) return points[0];
        for (int i = 1; i < points.Count; i++)
        {
            float k1 = keyOf(points[i]);
            if (k1 < key && i < points.Count - 1) continue;
            var (a, b) = (points[i - 1], points[i]);
            float k0 = keyOf(a);
            float t = k1 > k0 ? Math.Clamp((key - k0) / (k1 - k0), 0, 1) : 0;
            var s = new RouteSample(Vector2.Lerp(a.Sample.Position, b.Sample.Position, t),
                Vector2.Normalize(Vector2.Lerp(a.Sample.Tangent, b.Sample.Tangent, t)),
                Lerp(a.Sample.Along, b.Sample.Along, t), Lerp(a.Sample.Blend, b.Sample.Blend, t));
            return new RoutePoint(s, Lerp(a.Distance, b.Distance, t), Lerp(a.Top, b.Top, t), Lerp(a.Ground, b.Ground, t));
        }
        return points[^1];
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// <summary>A river crossing in link coordinates u (see <see cref="FindBridge"/>); Sign maps a half's distance to u.
    /// The deck rises a little in the middle (an arch for wood and stone) and the approaches climb to it at
    /// BridgeGrade.</summary>
    private readonly record struct Span(BridgeKind Kind, float From, float To, float Deck, float Low, int Sign = 1)
    {
        public float Arch => Kind switch { BridgeKind.Wood => 0.012f, BridgeKind.Stone => 0.025f, _ => 0f };

        public float LineAt(float distance)
        {
            float u = distance * Sign;
            if (u < From) return Deck - BridgeGrade * (From - u);
            if (u > To) return Deck - BridgeGrade * (u - To);
            float x = (2 * u - From - To) / (To - From);
            return Deck + Arch * (1 - x * x);
        }
    }

    /// <summary>One half path while it is laid out.</summary>
    private sealed class Half(RoutePath path, List<(RouteSample Sample, float Distance, float Ground)> points, NodeIndex index)
    {
        public RoutePath Path { get; } = path;
        public List<(RouteSample Sample, float Distance, float Ground)> Points { get; } = points;
        public int[] Nodes { get; } = points.Select(p => index.Id(p.Sample.Position)).ToArray();
        /// <summary>The other through half of the tile, if this is one.</summary>
        public Half? Twin { get; set; }
        /// <summary>The neighbour's half of the link.</summary>
        public Half? Partner { get; set; }
        public Span? Span { get; set; }
        public bool Bridged { get; set; }
        public bool Draws { get; set; }
        /// <summary>At a ground/deck edge: the deck height at the edge less the ground route's bed there.</summary>
        public float Ramp { get; set; }
        public float[] Tops { get; set; } = [];

        /// <summary>Whether a point is carried by a bridge deck over the ground.</summary>
        public bool OnSpan(int i) => Span is { } s && s.LineAt(Points[i].Distance) > Points[i].Ground;

        /// <summary>Ground under a point, or the bridge deck over it.</summary>
        public float Floor(int i) => Span is { } s ? MathF.Max(Points[i].Ground, s.LineAt(Points[i].Distance)) : Points[i].Ground;

        public (float Start, float End) SpanOf(Span s) => s.Sign > 0 ? (s.From, s.To) : (-s.To, -s.From);

        public List<RoutePoint> Profile() =>
            Points.Select((p, i) => new RoutePoint(p.Sample, p.Distance, Tops[i], p.Ground)).ToList();
    }

    /// <summary>Joins points that coincide (within a small tolerance, across the wrap) into one node id.</summary>
    private sealed class NodeIndex(float period)
    {
        private const float Cell = 0.01f, Tolerance = 2e-3f;
        private readonly Dictionary<(int, int), List<(Vector2 At, int Id)>> _cells = new();
        private readonly int _columns = period > 0 ? (int)MathF.Ceiling(period / Cell) : 0;

        public int Count { get; private set; }

        public int Id(Vector2 at)
        {
            if (period > 0) at.X -= period * MathF.Floor(at.X / period);
            int cx = (int)MathF.Floor(at.X / Cell), cz = (int)MathF.Floor(at.Y / Cell);
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                    if (_cells.TryGetValue((Column(cx + dx), cz + dz), out var list))
                        foreach (var (p, id) in list)
                            if (Near(p, at)) return id;
            (_cells.TryGetValue((Column(cx), cz), out var cell) ? cell : _cells[(Column(cx), cz)] = []).Add((at, Count));
            return Count++;
        }

        private int Column(int c) => _columns > 0 ? ((c % _columns) + _columns) % _columns : c;

        private bool Near(Vector2 a, Vector2 b)
        {
            float dx = MathF.Abs(a.X - b.X);
            if (period > 0) dx = MathF.Min(dx, period - dx);
            return dx < Tolerance && MathF.Abs(a.Y - b.Y) < Tolerance;
        }
    }
}
