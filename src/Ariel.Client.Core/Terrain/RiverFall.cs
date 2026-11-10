namespace Ariel.Client.Terrain;

/// <summary>A waterfall: where a river pours over a cliff. It sits on one hex corner on the cliff line
/// (<see cref="X"/>, <see cref="Z"/>), the corner the water reaches the brink at, and drops in the unit direction
/// (<see cref="DirectionX"/>, <see cref="DirectionZ"/>) into the low tile. Its lip runs across that direction,
/// <see cref="HalfWidth"/> to either side of the corner. Positions are in the unwrapped frame of the curve's link.</summary>
/// <remarks>Two shapes reach a cliff (<see cref="Course"/>). A river edge between two land tiles that is also a cliff edge
/// (a <see cref="RiverFoot"/>) has the water arrive at the corner where both other tiles stand high; it falls there and
/// runs on along the foot of the cliff to the edge's other corner (<see cref="AtStart"/> says which end of the curve the
/// fall is at). Where neither corner has a raised third tile, the water reaches the edge already at the foot and runs
/// along it without falling. A mouth that leaves a corner on a cliff coast falls at its start, straight out into the sea.
/// A river that simply ends at a corner on a cliff, above or below it, has no fall.</remarks>
public sealed record RiverFall(double X, double Z, double DirectionX, double DirectionZ, double HalfWidth, bool AtStart)
{
    /// <summary>Lip half width, in water half widths of the river before it gathers to the fall: a waterfall pours through
    /// a notch narrower than the river above it.</summary>
    private const double LipWidths = 0.65;
    /// <summary>Narrowest lip half width, so a thin stream still gets a ledge that reads as rock.</summary>
    private const double MinHalfWidth = 0.07;

    /// <summary>Where the water goes after it falls.</summary>
    public FallCourse Course { get; init; }

    /// <summary>Whether the fall's curve runs on along the foot of the cliff after it falls.</summary>
    public bool AlongFoot => Course == FallCourse.Foot;

    /// <summary>The waterfall on a curve, or null when no water pours over a cliff along it. <paramref name="foot"/> is
    /// the cliff foot the curve runs along, if any (see <see cref="RiverFoot.Of"/>).</summary>
    public static RiverFall? Of(TerrainSnapshot snapshot, RiverGraph graph, RiverCurve curve, RiverFoot? foot) => curve.Link.Kind switch
    {
        RiverLinkKind.Edge => foot is null ? null : OfEdge(snapshot, graph, curve, foot),
        RiverLinkKind.Mouth => OfMouth(snapshot, curve),
        _ => null,
    };

    /// <summary>Whether a river's dead end at a corner of the given link stands at the foot of a cliff: the tile beyond
    /// the corner is lifted above both tiles of the edge, with a cliff between it and them. The river springs from under
    /// the wall there.</summary>
    public static bool AtFoot(TerrainSnapshot s, RiverLink link, bool start)
    {
        if (!RiverFoot.EdgeTiles(s, link, out int plus, out int minus)) return false;
        var (x, z, ux, uz) = End(link, start);
        return s.TryTile(Hex.FromPixel(x + ux * 0.3, z + uz * 0.3, 1), out int third) && third != plus && third != minus &&
            RiverFoot.HighSide(s, third, plus) == third && RiverFoot.HighSide(s, third, minus) == third;
    }

    private static RiverFall? OfEdge(TerrainSnapshot s, RiverGraph graph, RiverCurve c, RiverFoot foot)
    {
        var link = c.Link;
        double ux = (link.BX - link.AX) / link.Length, uz = (link.BZ - link.AZ) / link.Length;
        // The edge carried on past a corner runs into the third tile there. The water reaches the brink at the corner whose
        // third tile stands above the low side too, because there it comes down between two raised tiles. When neither
        // corner does, the water is at the foot already and nothing falls. When both do, the corner the rest of the river
        // reaches decides, then the curve's start.
        bool startHigh = ThirdStandsHigh(s, link.AX, link.AZ, -ux, -uz, foot.Low);
        bool endHigh = ThirdStandsHigh(s, link.BX, link.BZ, ux, uz, foot.Low);
        if (!startHigh && !endHigh) return null;
        bool atStart = startHigh != endHigh ? startHigh : graph.LinksAt(link.A).Count >= graph.LinksAt(link.B).Count;
        double cx = atStart ? link.AX : link.BX, cz = atStart ? link.AZ : link.BZ;
        var (dx, dz) = RiverFoot.Toward(s, cx, cz, foot.Low);
        return new RiverFall(cx, cz, dx, dz, LipHalf(atStart ? c.Width[0] : c.Width[^1]), atStart) { Course = FallCourse.Foot };
    }

    /// <summary>A corner of a link and the unit direction out of the link through it.</summary>
    private static (double X, double Z, double UX, double UZ) End(RiverLink link, bool start)
    {
        double ux = (link.BX - link.AX) / link.Length, uz = (link.BZ - link.AZ) / link.Length;
        return start ? (link.AX, link.AZ, -ux, -uz) : (link.BX, link.BZ, ux, uz);
    }

    private static RiverFall? OfMouth(TerrainSnapshot s, RiverCurve c)
    {
        var link = c.Link;
        double length = link.Length;
        if (length < 1e-6 || !s.TryTile(Hex.FromPixel(link.BX, link.BZ, 1), out int water)) return null;
        double dx = (link.BX - link.AX) / length, dz = (link.BZ - link.AZ) / length;
        // The mouth starts on a corner of the water tile; the land tiles on either side of it hold the shore edges.
        foreach (double side in new[] { 0.2, -0.2 })
        {
            double x = link.AX - dx * 0.2 - dz * side, z = link.AZ - dz * 0.2 + dx * side;
            if (s.TryTile(Hex.FromPixel(x, z, 1), out int land) && land != water && RiverFoot.HighSide(s, land, water) == land)
                return new RiverFall(link.AX, link.AZ, dx, dz, LipHalf(c.Width[0]), AtStart: true) { Course = FallCourse.Sea };
        }
        return null;
    }

    private static double LipHalf(double width) => Math.Max(MinHalfWidth, LipWidths * width);

    private static bool ThirdStandsHigh(TerrainSnapshot s, double x, double z, double ux, double uz, int low) =>
        s.TryTile(Hex.FromPixel(x + ux * 0.3, z + uz * 0.3, 1), out int third) && third != low &&
        s.Heights[third] > s.Heights[low];
}

/// <summary>Where a waterfall's water goes once it has fallen.</summary>
public enum FallCourse
{
    /// <summary>On along the foot of the cliff, as the curve that falls (a river edge along the cliff).</summary>
    Foot,
    /// <summary>Into the sea (a mouth off a cliff coast); the curve is nothing but the fall.</summary>
    Sea,
}
