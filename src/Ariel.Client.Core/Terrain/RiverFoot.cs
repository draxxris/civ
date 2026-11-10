namespace Ariel.Client.Terrain;

/// <summary>A river edge that is also a cliff edge: its river runs along the foot of the wall, on the side of the
/// <see cref="Low"/> tile, under the <see cref="High"/> one. (<see cref="DirectionX"/>, <see cref="DirectionZ"/>) is the
/// unit direction from the edge across into the low tile, in the unwrapped frame of the edge's link.</summary>
/// <remarks>Running along the foot says nothing about how the water got there. It may pour over the brink at one corner
/// (a <see cref="RiverFall"/> of <see cref="FallCourse.Foot"/>), or arrive already at the foot and simply pass under the
/// wall.</remarks>
public sealed record RiverFoot(int High, int Low, double DirectionX, double DirectionZ)
{
    /// <summary>The foot a link's river runs along, or null when the link is not an edge along a cliff with one known
    /// lifted side.</summary>
    public static RiverFoot? Of(TerrainSnapshot s, RiverLink link)
    {
        if (link.Kind != RiverLinkKind.Edge || !EdgeTiles(s, link, out int plus, out int minus)) return null;
        int high = HighSide(s, plus, minus);
        if (high < 0) return null;
        int low = high == plus ? minus : plus;
        var (dx, dz) = Toward(s, (link.AX + link.BX) / 2, (link.AZ + link.BZ) / 2, low);
        return new RiverFoot(high, low, dx, dz);
    }

    /// <summary>The two tiles either side of an edge link's chord, stepping a little way into each.</summary>
    internal static bool EdgeTiles(TerrainSnapshot s, RiverLink link, out int plus, out int minus)
    {
        minus = -1;
        double mx = (link.AX + link.BX) / 2, mz = (link.AZ + link.BZ) / 2;
        double ux = (link.BX - link.AX) / link.Length, uz = (link.BZ - link.AZ) / link.Length;
        return s.TryTile(Hex.FromPixel(mx - uz * 0.25, mz + ux * 0.25, 1), out plus) &&
            s.TryTile(Hex.FromPixel(mx + uz * 0.25, mz - ux * 0.25, 1), out minus) && plus != minus;
    }

    /// <summary>The lifted tile of a cliff edge between neighbours a and b, or −1 when they share no cliff with one known
    /// lifted side.</summary>
    internal static int HighSide(TerrainSnapshot s, int a, int b)
    {
        for (int d = 0; d < 6; d++)
        {
            if (s.Grid.Neighbor(new TileId(a), d) is not { } n || n.Value != b) continue;
            int mirror = (d + 3) % 6;
            if ((s.Cliffs[a] & (1 << d)) == 0 && (s.Cliffs[b] & (1 << mirror)) == 0) return -1;
            bool aLifted = (s.CliffLifts[a] & (1 << d)) != 0, bLifted = (s.CliffLifts[b] & (1 << mirror)) != 0;
            return aLifted == bLifted ? -1 : aLifted ? a : b;
        }
        return -1;
    }

    /// <summary>Unit direction from a point to a tile's centre, in the point's frame.</summary>
    internal static (double X, double Z) Toward(TerrainSnapshot s, double x, double z, int tile)
    {
        var (tx, tz) = s.Grid.HexOf(new TileId(tile)).ToPixel(1);
        double dx = tx - x, dz = tz - z;
        // The tile may lie a whole period away from the link's unwrapped frame.
        if (s.Grid.Spec.Wrap) dx -= s.Period * Math.Round(dx / s.Period);
        double length = Math.Sqrt(dx * dx + dz * dz);
        return (dx / length, dz / length);
    }
}
