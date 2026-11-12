using Ariel.Game;

namespace Ariel.Rules.World;

/// <summary>Whether sight passes one tile of a ray. A struct, so a sight test allocates nothing.</summary>
internal interface ISightStep
{
    /// <summary>Whether the ray passes the tile; <paramref name="last"/> marks the tile just before the target.</summary>
    bool Passable(TileId tile, bool last);
}

/// <summary>Integer center-to-center hex rays. Tied edge rays may pass on either side.
/// Every shortest wrap image is considered, including ties on narrow maps.</summary>
internal static class SightLine
{
    internal static bool Clear<TStep>(HexGrid grid, TileId observer, TileId target, TStep step) where TStep : struct, ISightStep
    {
        var start = grid.HexOf(observer);
        var end = grid.HexOf(target);
        int distance = grid.Distance(observer, target);
        if (!grid.Spec.Wrap) return ClearImage(grid, start, end, distance, ref step);
        int width = grid.Spec.Width, dq = end.Q - start.Q;
        int first = (-distance - dq) / width - 1, last = (distance - dq) / width + 1;
        for (int shift = first; shift <= last; shift++)
        {
            var image = new Hex(end.Q + shift * width, end.R);
            if (start.DistanceTo(image) == distance && ClearImage(grid, start, image, distance, ref step)) return true;
        }
        return false;
    }

    private static bool ClearImage<TStep>(HexGrid grid, Hex start, Hex end, int distance, ref TStep passable)
        where TStep : struct, ISightStep
    {
        for (int step = 1; step < distance; step++)
        {
            long q = (long)start.Q * (distance - step) + (long)end.Q * step;
            long r = (long)start.R * (distance - step) + (long)end.R * step;
            if (!AnyNearest(grid, q, r, distance, step == distance - 1, ref passable)) return false;
        }
        return true;
    }

    /// <summary>Whether any of the hexes nearest the exact point (q, r) / denominator passes. The point lies in the
    /// cell of four hexes around its floor; all that tie for the least error count.</summary>
    private static bool AnyNearest<TStep>(HexGrid grid, long q, long r, int denominator, bool last, ref TStep passable)
        where TStep : struct, ISightStep
    {
        int q0 = Floor(q, denominator), r0 = Floor(r, denominator);
        long minimum = long.MaxValue;
        for (int corner = 0; corner < 4; corner++) minimum = Math.Min(minimum, Error(q0 + (corner >> 1), r0 + (corner & 1)));
        for (int corner = 0; corner < 4; corner++)
        {
            int x = q0 + (corner >> 1), y = r0 + (corner & 1);
            if (Error(x, y) == minimum && grid.TryTile(new Hex(x, y), out var tile) && passable.Passable(tile, last)) return true;
        }
        return false;

        long Error(int x, int y)
        {
            long dq = x * (long)denominator - q, dr = y * (long)denominator - r;
            return dq * dq + dq * dr + dr * dr;
        }
    }

    private static int Floor(long value, int denominator) =>
        checked((int)(value / denominator - (value % denominator < 0 ? 1 : 0)));
}
