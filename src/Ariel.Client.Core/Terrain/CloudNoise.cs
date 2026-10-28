namespace Ariel.Client.Terrain;

/// <summary>A cube of noise that tiles in all three axes, for the shapes of clouds and fog. Engine-free bytes in RGBA8
/// order, slice by slice (z), row by row (y): R is billowy Perlin-Worley noise (smooth fbm whose low values are carved
/// into round cells), G is finer Worley noise that erodes edges into wisps; B and A are unused. Deterministic.</summary>
public static class CloudNoise
{
    public static byte[] Build(int size = 64)
    {
        int n = size * size * size;
        var billows = new double[n];
        var details = new double[n];
        Parallel.For(0, size, z =>
        {
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    double u = (double)x / size, v = (double)y / size, w = (double)z / size;
                    double fbm = 0.55 * Gradient(u, v, w, 4, 1) + 0.3 * Gradient(u, v, w, 8, 2) + 0.15 * Gradient(u, v, w, 16, 3);
                    fbm = Math.Clamp(fbm * 0.5 + 0.5, 0, 1);
                    double cells = 0.6 * Worley(u, v, w, 4, 4) + 0.3 * Worley(u, v, w, 8, 5) + 0.1 * Worley(u, v, w, 16, 6);
                    // Perlin-Worley: the fbm stays, remapped so the Worley cells round off its billows.
                    double billow = Math.Clamp(Remap(fbm, cells - 1, 1, 0, 1), 0, 1);
                    double detail = 0.6 * Worley(u, v, w, 12, 7) + 0.4 * Worley(u, v, w, 24, 8);
                    int at = (z * size + y) * size + x;
                    billows[at] = billow;
                    details[at] = detail;
                }
        });
        // Stretch both channels over the full byte range, so thresholds in the shaders cut real gaps.
        var data = new byte[n * 4];
        Stretch(billows, data, 0);
        Stretch(details, data, 1);
        for (int i = 0; i < n; i++) data[i * 4 + 3] = 255;
        return data;
    }

    private static void Stretch(double[] values, byte[] data, int channel)
    {
        double low = values.Min(), high = values.Max(), span = Math.Max(high - low, 1e-9);
        for (int i = 0; i < values.Length; i++) data[i * 4 + channel] = (byte)Math.Round((values[i] - low) / span * 255);
    }

    private static double Remap(double v, double from0, double from1, double to0, double to1) =>
        to0 + (v - from0) / (from1 - from0) * (to1 - to0);

    /// <summary>Inverted cellular noise in [0, 1] with <paramref name="cells"/> cells per unit, tiling over the unit cube:
    /// 1 at feature points, falling to 0 between them.</summary>
    private static double Worley(double u, double v, double w, int cells, uint seed)
    {
        double px = u * cells, py = v * cells, pz = w * cells;
        int cx = (int)Math.Floor(px), cy = (int)Math.Floor(py), cz = (int)Math.Floor(pz);
        double best = 9;
        for (int k = -1; k <= 1; k++)
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int x = cx + i, y = cy + j, z = cz + k;
                    uint h = Hash(Wrap(x, cells), Wrap(y, cells), Wrap(z, cells), seed);
                    double fx = x + (h & 1023) / 1023.0, fy = y + ((h >> 10) & 1023) / 1023.0, fz = z + ((h >> 20) & 1023) / 1023.0;
                    double d = (fx - px) * (fx - px) + (fy - py) * (fy - py) + (fz - pz) * (fz - pz);
                    best = Math.Min(best, d);
                }
        return 1 - Math.Clamp(Math.Sqrt(best), 0, 1);
    }

    /// <summary>Gradient noise in about [-1, 1] with <paramref name="cells"/> lattice cells per unit, tiling over the unit cube.</summary>
    private static double Gradient(double u, double v, double w, int cells, uint seed)
    {
        double px = u * cells, py = v * cells, pz = w * cells;
        int x0 = (int)Math.Floor(px), y0 = (int)Math.Floor(py), z0 = (int)Math.Floor(pz);
        double fx = px - x0, fy = py - y0, fz = pz - z0;
        double sx = Fade(fx), sy = Fade(fy), sz = Fade(fz);
        double Corner(int i, int j, int k)
        {
            uint h = Hash(Wrap(x0 + i, cells), Wrap(y0 + j, cells), Wrap(z0 + k, cells), seed);
            // One of twelve edge directions of a cube.
            double dx = fx - i, dy = fy - j, dz = fz - k;
            return (h % 12) switch
            {
                0 => dx + dy, 1 => -dx + dy, 2 => dx - dy, 3 => -dx - dy,
                4 => dx + dz, 5 => -dx + dz, 6 => dx - dz, 7 => -dx - dz,
                8 => dy + dz, 9 => -dy + dz, 10 => dy - dz, _ => -dy - dz,
            };
        }
        double Lerp(double a, double b, double t) => a + (b - a) * t;
        double x00 = Lerp(Corner(0, 0, 0), Corner(1, 0, 0), sx), x10 = Lerp(Corner(0, 1, 0), Corner(1, 1, 0), sx);
        double x01 = Lerp(Corner(0, 0, 1), Corner(1, 0, 1), sx), x11 = Lerp(Corner(0, 1, 1), Corner(1, 1, 1), sx);
        return Lerp(Lerp(x00, x10, sy), Lerp(x01, x11, sy), sz);
    }

    private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);
    private static int Wrap(int i, int n) => ((i % n) + n) % n;

    private static uint Hash(int x, int y, int z, uint seed)
    {
        uint h = (uint)x * 0x8da6b343u ^ (uint)y * 0xd8163841u ^ (uint)z * 0xcb1ab31fu ^ seed * 0x9e3779b9u;
        h ^= h >> 16; h *= 0x7feb352du;
        h ^= h >> 15; h *= 0x846ca68bu;
        return h ^ (h >> 16);
    }
}
