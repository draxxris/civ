namespace Ariel.Client;

/// <summary>
/// Value noise in [-1, 1] that repeats every periodX x periodY pixels, so a texture
/// painted from it tiles without seams. Each sample uses its own cell size per axis, so one call can give
/// round mottles (equal cells) or brush streaks (long cells along the stroke). Cell sizes must divide the periods.
/// Pure C#, so painters can sample it from worker threads.
/// </summary>
internal readonly struct PeriodicNoise(int periodX, int periodY, int seed)
{
    public float Sample(float x, float y, int cellX, int cellY, int salt)
    {
        int cellsX = Math.Max(1, periodX / cellX), cellsY = Math.Max(1, periodY / cellY);
        float fx = x / cellX, fy = y / cellY;
        int ix = (int)MathF.Floor(fx), iy = (int)MathF.Floor(fy);
        float tx = Smooth(fx - ix), ty = Smooth(fy - iy);
        int x0 = PositiveMod(ix, cellsX), x1 = PositiveMod(ix + 1, cellsX);
        int y0 = PositiveMod(iy, cellsY), y1 = PositiveMod(iy + 1, cellsY);
        var top = Lerp(Hash(x0, y0, salt), Hash(x1, y0, salt), tx);
        var bottom = Lerp(Hash(x0, y1, salt), Hash(x1, y1, salt), tx);
        return Lerp(top, bottom, ty);
    }

    /// <summary>Round mottles of one cell size.</summary>
    public float Sample(float x, float y, int cell, int salt) => Sample(x, y, cell, cell, salt);

    private float Hash(int x, int y, int salt)
    {
        unchecked
        {
            var h = (uint)(x * 374761393 + y * 668265263 + (salt + seed) * 2147483647);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0xFFFFFF * 2f - 1f;
        }
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static int PositiveMod(int a, int m) => ((a % m) + m) % m;
}
