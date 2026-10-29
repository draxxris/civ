namespace Ariel.Mods.World;

/// <summary>Wrapped value noise: coarse land fields plus progressively finer integer detail.</summary>
public static class FractalNoise
{
    /// <summary>Smooth noise in 0..999 per tile.</summary>
    public static int[] Generate(HexGrid grid, DeterministicRng rng, int scale, int octaves)
    {
        var result = new int[grid.Count];
        int weight = 1 << (octaves - 1), total = 0;
        for (int octave = 0; octave < octaves; octave++)
        {
            AddOctave(grid, rng, Math.Max(1, scale >> octave), weight, result);
            total += weight;
            weight = Math.Max(1, weight / 2);
        }
        for (int i = 0; i < result.Length; i++) result[i] /= total;
        return result;
    }

    private static void AddOctave(HexGrid grid, DeterministicRng rng, int scale, int weight, int[] result)
    {
        int columns = Math.Max(2, (grid.Spec.Width + scale - 1) / scale);
        int rows = Math.Max(2, (grid.Spec.Height + scale - 1) / scale + 1);
        var samples = Enumerable.Range(0, columns * rows).Select(_ => rng.Next(1000)).ToArray();
        foreach (var tile in grid.Tiles)
        {
            var (x, y) = grid.OffsetOf(tile);
            int sx = x * columns / grid.Spec.Width;
            int fx = x * columns % grid.Spec.Width;
            int sy = y / scale, fy = y % scale;
            int nx = grid.Spec.Wrap ? (sx + 1) % columns : Math.Min(columns - 1, sx + 1);
            int top = Interpolate(samples[sy * columns + sx], samples[sy * columns + nx], fx, grid.Spec.Width);
            int bottom = Interpolate(samples[(sy + 1) * columns + sx], samples[(sy + 1) * columns + nx], fx, grid.Spec.Width);
            result[tile.Value] += weight * Interpolate(top, bottom, fy, scale);
        }
    }
    private static int Interpolate(int a, int b, int n, int d) => (a * (d - n) + b * n) / d;
}
