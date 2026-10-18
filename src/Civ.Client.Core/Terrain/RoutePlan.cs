using System.Numerics;
using Civ.Content;
using Civ.Game;

namespace Civ.Client.Terrain;

/// <summary>The shape rules of one look, in world units (hexes have outer radius 1). Ribbon is the half width of the
/// draped ground strip, out to the faint edge of its verge. Bow and Wiggle scale the hashed sideways bend of a link
/// (<see cref="RoutePlan"/>). Engineered routes run in smooth curves through a tile, organic ones meet at the tile
/// centre. Elevated routes stand on supports; Lift is the height of their deck top over the ground.</summary>
public readonly record struct RouteStyle(float Ribbon, float Bow, float Wiggle, bool Engineered, bool Elevated, float Lift);

/// <summary>The look of each route id and the shape rules of each look.</summary>
public static class RouteLooks
{
    /// <summary>The look of a route id, from the mods' <see cref="ITileAppearanceService"/>. An unknown route is drawn as a
    /// plain road.</summary>
    public static RouteLook Of(ContentDb content, string id) =>
        content.Services.Get<ITileAppearanceService>().Select(s => s.Route(id)).FirstOrDefault(look => look != null) ?? RouteLook.Road;

    public static RouteStyle Style(RouteLook look) => look switch
    {
        RouteLook.Track => new(0.11f, 0.22f, 0.11f, false, false, 0),
        RouteLook.Road => new(0.14f, 0.17f, 0.08f, false, false, 0),
        RouteLook.Paved => new(0.12f, 0.07f, 0.025f, true, false, 0),
        RouteLook.Highway => new(0.13f, 0.03f, 0f, true, false, 0),
        RouteLook.Railroad or RouteLook.ElectricRailroad => new(0.12f, 0.025f, 0f, true, false, 0),
        RouteLook.Maglev or RouteLook.Jumplane => new(0.12f, 0.02f, 0f, true, true, 0.25f),
        _ => throw new ArgumentOutOfRangeException(nameof(look), look, null),
    };

    public static bool Elevated(RouteLook look) => Style(look).Elevated;
}

/// <summary>One route link from a tile toward its neighbour in <c>Hex.Directions</c> order. NeighbourCentre is the
/// unwrapped centre, so a link across the wrap seam stays short. Bow and Wiggle are hashed from the unordered tile pair, in
/// [-1, 1]. First is true on the tile with the lower id, where the shared link curve starts.</summary>
public readonly record struct RouteLinkInput(int Direction, RouteLook Neighbour, Vector2 NeighbourCentre, float Bow, float Wiggle,
    bool First);

/// <summary>A point on a route path. Tangent is a unit vector pointing from the tile edge toward the inner end. Along is
/// arc length from the edge, scaled so the whole path is a whole number of <see cref="RoutePlan.PatternUnit"/>s and
/// repeating patterns meet at both ends. Blend is the share of the neighbour's look (0 inside the tile). Heights are not
/// planned here: <see cref="RouteProfile"/> lays every path over the terrain.</summary>
public readonly record struct RouteSample(Vector2 Position, Vector2 Tangent, float Along, float Blend);

/// <summary>How the inner end of a path meets the rest of the tile.</summary>
public enum RouteEnd
{
    /// <summary>At the tile centre, where other paths and the hub meet.</summary>
    Centre,
    /// <summary>Halfway along a smooth curve through the tile; the partner half starts where this one ends.</summary>
    Through,
    /// <summary>Merges into the through curve of a junction tile.</summary>
    Merge,
    /// <summary>At the tile centre, with no other path: the route stops.</summary>
    DeadEnd,
}

/// <summary>Half of a route on one tile: from the shared edge with one neighbour (sample 0) to the inner end. Own is this
/// tile's look and Other the neighbour's. Primary marks the one half of a through pair that owns their shared inner end
/// (so supports there are placed once). First is the link's <see cref="RouteLinkInput.First"/>: the owner of supports on
/// the shared edge.</summary>
public sealed record RoutePath(RouteLook Own, RouteLook Other, IReadOnlyList<RouteSample> Samples, RouteEnd End, bool Primary,
    bool First)
{
    /// <summary>Scaled length of the path, a whole number of pattern units.</summary>
    public float Length => Samples[^1].Along;
}

/// <summary>The routes on one tile: half paths toward each linked neighbour, meeting at the centre when Hub (drawn as round caps, or a disc on a tile with no links).</summary>
public sealed record RouteTilePlan(RouteLook Look, Vector2 Centre, IReadOnlyList<RoutePath> Paths, bool Hub);

/// <summary>
/// Plans the route curves of one tile, engine-free. Each link between two tile centres is one gently swerving curve: a bow
/// plus a weaker S-bend, both zero at the centres, scaled by the straightness of the looks at each end. Both tiles of a
/// link compute the same curve, so their halves meet at the shared edge with the same point and tangent.
/// Organic looks (track, road) run each half link to the centre and meet at a hub. Engineered looks join two links with
/// one smooth curve; at a junction the straightest pair forms that curve and the other links merge into its middle, like
/// a rail switch. Where looks change, Blend crossfades them about the shared edge: slowly between two ground looks, and
/// over a shorter stretch where a ground route climbs to meet a descending deck, so the structures merge where they meet.
/// </summary>
public static class RoutePlan
{
    /// <summary>Repeating patterns (sleepers, dashes, masts) have periods that divide this length.</summary>
    public const float PatternUnit = 0.2f;
    /// <summary>Half the length of the crossfade between two ground looks, centred on the shared edge.</summary>
    public const float BlendHalf = 0.36f;
    /// <summary>Half the length of the crossfade where a deck is involved, centred on the shared edge: the deck shapes of
    /// the two looks morph into each other over this stretch.</summary>
    public const float MorphHalf = 0.2f;
    private const int HalfSteps = 8, CurveSteps = 10;

    public static RouteTilePlan Plan(RouteLook look, Vector2 centre, IReadOnlyList<RouteLinkInput> links)
    {
        var style = RouteLooks.Style(look);
        var paths = new List<RoutePath>();
        if (!style.Engineered || links.Count == 1)
        {
            var end = style.Engineered ? RouteEnd.DeadEnd : RouteEnd.Centre;
            foreach (var link in links) paths.Add(HalfLink(look, centre, link, end));
            bool hub = !style.Engineered || look is RouteLook.Paved or RouteLook.Highway;
            return new RouteTilePlan(look, centre, paths, hub || links.Count == 0);
        }
        if (links.Count == 0) return new RouteTilePlan(look, centre, paths, true);

        var (a, b) = ThroughPair(links);
        var ea = Edge(centre, look, links[a]);
        var eb = Edge(centre, look, links[b]);
        float k = Vector2.Distance(ea.Point, eb.Point);
        Vector2 At(float t) => Hermite(ea.Point, -ea.Out * k, eb.Point, eb.Out * k, t);
        var first = Sample(At, 0f, 0.5f, CurveSteps);
        var second = Sample(At, 1f, 0.5f, CurveSteps);
        paths.Add(Path(look, links[a], first, RouteEnd.Through, true));
        paths.Add(Path(look, links[b], second, RouteEnd.Through, false));

        var middle = At(0.5f);
        var heading = Vector2.Normalize(At(0.5f + 1e-3f) - At(0.5f - 1e-3f));
        for (int i = 0; i < links.Count; i++)
        {
            if (i == a || i == b) continue;
            var e = Edge(centre, look, links[i]);
            // Join the through curve heading along it, away from this spur's edge.
            var join = Vector2.Dot(heading, middle - e.Point) >= 0 ? heading : -heading;
            float span = Vector2.Distance(e.Point, middle) * 1.2f;
            Vector2 Spur(float t) => Hermite(e.Point, -e.Out * span, middle, join * span, t);
            paths.Add(Path(look, links[i], Sample(Spur, 0f, 1f, CurveSteps), RouteEnd.Merge, false));
        }
        return new RouteTilePlan(look, centre, paths, false);
    }

    /// <summary>The links whose directions are furthest apart (opposite first), lowest directions on a tie.</summary>
    private static (int, int) ThroughPair(IReadOnlyList<RouteLinkInput> links)
    {
        (int, int) best = (0, 1);
        int bestGap = -1;
        for (int i = 0; i < links.Count; i++)
            for (int j = i + 1; j < links.Count; j++)
            {
                int gap = Math.Abs(links[i].Direction - links[j].Direction);
                gap = Math.Min(gap, 6 - gap);
                if (gap > bestGap) { bestGap = gap; best = (i, j); }
            }
        return best;
    }

    /// <summary>The shared link curve, from the first tile's centre (t = 0) to the second's (t = 1): offset sideways by
    /// <c>bow·sin(πt) + wiggle·sin(2πt)</c>, with amplitudes eased from one end's look to the other's.</summary>
    private static Vector2 LinkAt(Vector2 centre, RouteLook look, RouteLinkInput link, float t)
    {
        var (from, to) = link.First ? (centre, link.NeighbourCentre) : (link.NeighbourCentre, centre);
        var (start, end) = link.First ? (look, link.Neighbour) : (link.Neighbour, look);
        var s0 = RouteLooks.Style(start);
        var s1 = RouteLooks.Style(end);
        float ease = t * t * (3 - 2 * t);
        float bow = Lerp(s0.Bow, s1.Bow, ease) * link.Bow, wiggle = Lerp(s0.Wiggle, s1.Wiggle, ease) * link.Wiggle;
        var d = to - from;
        var normal = Vector2.Normalize(new Vector2(-d.Y, d.X));
        return Vector2.Lerp(from, to, t) + normal * (bow * MathF.Sin(MathF.PI * t) + wiggle * MathF.Sin(MathF.Tau * t));
    }

    /// <summary>The crossing of the shared edge and the outward unit direction of the link there.</summary>
    private static (Vector2 Point, Vector2 Out) Edge(Vector2 centre, RouteLook look, RouteLinkInput link)
    {
        var point = LinkAt(centre, look, link, 0.5f);
        float step = link.First ? 1e-3f : -1e-3f;
        var ahead = LinkAt(centre, look, link, 0.5f + step);
        var behind = LinkAt(centre, look, link, 0.5f - step);
        return (point, Vector2.Normalize(ahead - behind));
    }

    private static RoutePath HalfLink(RouteLook look, Vector2 centre, RouteLinkInput link, RouteEnd end)
    {
        // t runs from the shared edge (0.5) back to this tile's centre.
        var curve = Sample(t => LinkAt(centre, look, link, t), 0.5f, link.First ? 0f : 1f, HalfSteps);
        return Path(look, link, curve, end, true);
    }

    /// <summary>Points of a curve from parameter t0 (the edge) to t1, with unit tangents toward t1.</summary>
    private static (Vector2[] Points, Vector2[] Tangents) Sample(Func<float, Vector2> curve, float t0, float t1, int steps)
    {
        var points = new Vector2[steps + 1];
        var tangents = new Vector2[steps + 1];
        float h = MathF.Sign(t1 - t0) * 1e-3f;
        for (int i = 0; i <= steps; i++)
        {
            float t = Lerp(t0, t1, (float)i / steps);
            points[i] = curve(t);
            tangents[i] = Vector2.Normalize(curve(t + h) - curve(t - h));
        }
        return (points, tangents);
    }

    private static RoutePath Path(RouteLook own, RouteLinkInput link, (Vector2[] Points, Vector2[] Tangents) curve, RouteEnd end,
        bool primary)
    {
        var points = curve.Points;
        var lengths = new float[points.Length];
        for (int i = 1; i < points.Length; i++) lengths[i] = lengths[i - 1] + Vector2.Distance(points[i - 1], points[i]);
        float length = lengths[^1];
        float scale = length > 0 ? MathF.Max(1, MathF.Round(length / PatternUnit)) * PatternUnit / length : 1;
        var samples = new RouteSample[points.Length];
        for (int i = 0; i < points.Length; i++)
            samples[i] = new RouteSample(points[i], curve.Tangents[i], lengths[i] * scale, BlendAt(own, link.Neighbour, lengths[i]));
        return new RoutePath(own, link.Neighbour, samples, end, primary, link.First);
    }

    /// <summary>Share of the neighbour's look at distance d inside this tile from the shared edge. The two tiles of a link
    /// agree: one tile's share of the other is one minus the other's share of the first.</summary>
    private static float BlendAt(RouteLook own, RouteLook other, float d)
    {
        if (own == other) return 0;
        float half = RouteLooks.Elevated(own) || RouteLooks.Elevated(other) ? MorphHalf : BlendHalf;
        // Linear, so the shader's per-pixel thresholds spread evenly over the whole crossfade.
        return Math.Clamp(0.5f - d / (2 * half), 0, 1);
    }

    private static Vector2 Hermite(Vector2 p0, Vector2 m0, Vector2 p1, Vector2 m1, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return (2 * t3 - 3 * t2 + 1) * p0 + (t3 - 2 * t2 + t) * m0 + (-2 * t3 + 3 * t2) * p1 + (t3 - t2) * m1;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}

/// <summary>
/// Where the paths of a junction tile overlap, every point belongs to the path whose centre line is nearest, so the
/// renderers draw the union of the paths: each path draws only where no other centre line is nearer, and the borders of
/// one path stop where another takes over. The two halves of the through curve are one line, so neither sees the other.
/// </summary>
public static class RouteJunction
{
    /// <summary>True when the tile's paths overlap: some path merges into another.</summary>
    public static bool Overlaps(RouteTilePlan plan) => plan.Paths.Any(p => p.End == RouteEnd.Merge);

    /// <summary>The centre lines of the tile's other paths, as seen by <paramref name="path"/>.</summary>
    public static IReadOnlyList<Vector2[]> Others(RouteTilePlan plan, RoutePath path) =>
        plan.Paths.Where(p => !ReferenceEquals(p, path) && !(p.End == RouteEnd.Through && path.End == RouteEnd.Through))
            .Select(p => p.Samples.Select(s => s.Position).ToArray()).ToList();

    /// <summary>How much nearer a point is to <paramref name="own"/> than to the nearest of <paramref name="rivals"/>:
    /// negative where another centre line is nearer. Both distances are measured the same way, so on a seam the margins of
    /// the two paths agree and no gap opens between them.</summary>
    public static float Margin(Vector2[] own, IReadOnlyList<Vector2[]> rivals, Vector2 p) =>
        rivals.Count == 0 ? float.MaxValue : Distance(rivals, p) - Distance([own], p);

    /// <summary>The margin below which a path gives a point up. Spurs merge into the through curve almost tangentially,
    /// so near the merge several centre lines lie closer together than the renderers' vertices, and the margin, a minimum
    /// of distances, is not linear between them. So the through curve keeps a point unless a spur is clearly nearer, and
    /// where it gives a point up the spur's own margin is safely positive: no gap opens, at the cost of a thin band where
    /// both draw the same flush surface.</summary>
    public static float Yield(RoutePath path) => path.End == RouteEnd.Through ? -0.012f : -0.001f;

    /// <summary>Distance from a point to the nearest of some polylines.</summary>
    public static float Distance(IReadOnlyList<Vector2[]> lines, Vector2 p)
    {
        float best = float.MaxValue;
        foreach (var line in lines)
            for (int i = 1; i < line.Length; i++)
            {
                var (a, b) = (line[i - 1], line[i]);
                var ab = b - a;
                float t = ab.LengthSquared() > 0 ? Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0, 1) : 0;
                best = MathF.Min(best, Vector2.Distance(p, a + ab * t));
            }
        return best;
    }

    /// <summary>The path's samples, with points added in between so no step is longer than <paramref name="step"/>
    /// (in Along units). Positions, tangents and the rest are interpolated linearly.</summary>
    public static IReadOnlyList<RouteSample> Resample(RoutePath path, float step)
    {
        var samples = path.Samples;
        var result = new List<RouteSample> { samples[0] };
        for (int i = 1; i < samples.Count; i++)
        {
            var (a, b) = (samples[i - 1], samples[i]);
            int n = Math.Max(1, (int)MathF.Ceiling((b.Along - a.Along) / step));
            for (int k = 1; k <= n; k++)
            {
                float t = (float)k / n;
                result.Add(new RouteSample(Vector2.Lerp(a.Position, b.Position, t), Vector2.Normalize(Vector2.Lerp(a.Tangent, b.Tangent, t)),
                    a.Along + (b.Along - a.Along) * t, a.Blend + (b.Blend - a.Blend) * t));
            }
        }
        return result;
    }
}
