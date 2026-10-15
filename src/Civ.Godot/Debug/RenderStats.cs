using Civ.Client.Terrain;

namespace Civ.Client;

/// <summary>Numbers behind a capture: props per scatter set before and after thinning, triangle counts, and height
/// statistics per relief class. Printed and written next to a screenshot by <c>--stats</c>.</summary>
internal static class RenderStats
{
    public static readonly Dictionary<int, int> TerrainTriangles = new();
    private static readonly Dictionary<int, Dictionary<string, (int Planned, int Kept)>> ScatterByChunk = new();

    public static void CountScatter(int chunk, IReadOnlyList<ScatterInstance> instances, Func<ScatterInstance, bool> keep)
    {
        var counts = new Dictionary<string, (int Planned, int Kept)>(StringComparer.Ordinal);
        foreach (var instance in instances)
        {
            counts.TryGetValue(instance.Set, out var c);
            counts[instance.Set] = (c.Planned + 1, c.Kept + (keep(instance) ? 1 : 0));
        }
        ScatterByChunk[chunk] = counts;
    }

    public static Dictionary<string, object> Scatter()
    {
        var total = new SortedDictionary<string, (int Planned, int Kept)>(StringComparer.Ordinal);
        foreach (var counts in ScatterByChunk.Values)
            foreach (var (set, c) in counts)
            {
                total.TryGetValue(set, out var t);
                total[set] = (t.Planned + c.Planned, t.Kept + c.Kept);
            }
        return total.ToDictionary(kv => kv.Key, kv => (object)new { planned = kv.Value.Planned, kept = kv.Value.Kept });
    }

    /// <summary>Height min, max and mean per relief class over the tile centre plus six points half a tile out.</summary>
    public static Dictionary<string, object> Heights(TerrainSnapshot snapshot, TerrainField field)
    {
        var stats = new Dictionary<ReliefClass, (double Min, double Max, double Sum, int N)>();
        for (int i = 0; i < snapshot.Relief.Length; i++)
        {
            var relief = snapshot.Relief[i];
            if (relief == ReliefClass.Hidden) continue;
            var (cx, cz) = snapshot.Grid.HexOf(new TileId(i)).ToPixel(1);
            for (int k = -1; k < 6; k++)
            {
                double x = cx + (k < 0 ? 0 : 0.5 * Math.Cos(k * Math.PI / 3)), z = cz + (k < 0 ? 0 : 0.5 * Math.Sin(k * Math.PI / 3));
                double h = field.HeightAt(x, z);
                var s = stats.TryGetValue(relief, out var old) ? old : (double.MaxValue, double.MinValue, 0.0, 0);
                stats[relief] = (Math.Min(s.Item1, h), Math.Max(s.Item2, h), s.Item3 + h, s.Item4 + 1);
            }
        }
        return stats.OrderBy(kv => kv.Key).ToDictionary(kv => kv.Key.ToString(),
            kv => (object)new { min = Math.Round(kv.Value.Min, 3), max = Math.Round(kv.Value.Max, 3), mean = Math.Round(kv.Value.Sum / kv.Value.N, 3) });
    }
}
