using Civ.Game;

namespace Civ.Mods.World;

/// <summary>Integer center-to-center hex rays. Tied edge rays may pass on either side.
/// Every shortest wrap image is considered, including ties on narrow maps.</summary>
internal static class SightLine
{
    internal static bool Clear(HexGrid grid, TileId observer, TileId target, Func<TileId, bool, bool> passable)
    {
        var start = grid.HexOf(observer);
        int distance = grid.Distance(observer, target);
        return Images(grid, start, grid.HexOf(target), distance)
            .Any(end => ClearImage(grid, start, end, distance, passable));
    }

    private static IEnumerable<Hex> Images(HexGrid grid, Hex start, Hex end, int distance)
    {
        if (!grid.Spec.Wrap) { yield return end; yield break; }
        int width = grid.Spec.Width, dq = end.Q - start.Q;
        int first = (-distance - dq) / width - 1, last = (distance - dq) / width + 1;
        for (int shift = first; shift <= last; shift++)
        {
            var image = new Hex(end.Q + shift * width, end.R);
            if (start.DistanceTo(image) == distance) yield return image;
        }
    }

    private static bool ClearImage(HexGrid grid, Hex start, Hex end, int distance, Func<TileId, bool, bool> passable)
    {
        for (int step = 1; step < distance; step++)
        {
            long q = (long)start.Q * (distance - step) + (long)end.Q * step;
            long r = (long)start.R * (distance - step) + (long)end.R * step;
            if (!Nearest(q, r, distance).Any(h => grid.TryTile(h, out var tile) && passable(tile, step == distance - 1)))
                return false;
        }
        return true;
    }

    private static IEnumerable<Hex> Nearest(long q, long r, int denominator)
    {
        int q0 = Floor(q, denominator), r0 = Floor(r, denominator);
        var candidates = new List<(Hex Hex, long Error)>();
        for (int x = q0; x <= q0 + 1; x++)
            for (int y = r0; y <= r0 + 1; y++)
            {
                long dq = x * (long)denominator - q, dr = y * (long)denominator - r;
                candidates.Add((new(x, y), dq * dq + dq * dr + dr * dr));
            }
        long minimum = candidates.Min(c => c.Error);
        return candidates.Where(c => c.Error == minimum).Select(c => c.Hex);
    }

    private static int Floor(long value, int denominator) =>
        checked((int)(value / denominator - (value % denominator < 0 ? 1 : 0)));
}
