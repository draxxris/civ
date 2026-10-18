namespace Civ.Client.Terrain;

/// <summary>Seeded 2D value noise that repeats exactly every <see cref="Period"/> world units in x.
/// Each octave snaps its frequency so that a whole number of lattice cells fits one period, and wraps lattice
/// indices modulo that cell count, so the east-west map wrap has no seam. The result is deterministic for a seed
/// on every platform (integer hashing only; no <see cref="HashCode"/>).</summary>
public sealed class PeriodicNoise
{
    private readonly uint _seed;

    /// <summary>Create noise for one seed and a positive x period in world units.</summary>
    public PeriodicNoise(int seed, double period)
    {
        if (!(period > 0)) throw new ArgumentOutOfRangeException(nameof(period));
        _seed = Mix((uint)seed ^ 0x9e3779b9u);
        Period = period;
    }

    /// <summary>Horizontal distance after which the noise repeats.</summary>
    public double Period { get; }

    /// <summary>Lattice cells per period for a requested frequency: <c>round(Period · frequency)</c>, at least one.</summary>
    public int CellsFor(double frequency) => Math.Max(1, (int)Math.Round(Period * frequency));

    /// <summary>Smooth value noise in [-1, 1] at about <paramref name="frequency"/> cells per world unit.</summary>
    public double Value(double x, double z, double frequency, int salt = 0)
    {
        int cells = CellsFor(frequency);
        double f = cells / Period;
        double u = x * f, v = z * f;
        double fu = Math.Floor(u), fv = Math.Floor(v);
        int i0 = Wrap((long)fu, cells), i1 = i0 + 1 == cells ? 0 : i0 + 1;
        int j0 = (int)fv;
        double tx = Fade(u - fu), tz = Fade(v - fv);
        uint s = _seed ^ Mix((uint)salt * 0x85ebca6bu + 0x27d4eb2fu);
        double a = Lattice(i0, j0, s), b = Lattice(i1, j0, s);
        double c = Lattice(i0, j0 + 1, s), d = Lattice(i1, j0 + 1, s);
        double top = a + (b - a) * tx, bottom = c + (d - c) * tx;
        return top + (bottom - top) * tz;
    }

    /// <summary>Fractal sum of <paramref name="octaves"/> value-noise layers (lacunarity 2, gain 0.5), normalised to [-1, 1].</summary>
    public double Fbm(double x, double z, double frequency, int octaves = 4, int salt = 0)
    {
        double sum = 0, norm = 0, amplitude = 1;
        for (int o = 0; o < octaves; o++)
        {
            sum += amplitude * Value(x, z, frequency, salt * 31 + o);
            norm += amplitude;
            amplitude *= 0.5;
            frequency *= 2;
        }
        return sum / norm;
    }

    /// <summary>Ridged fractal noise in [0, 1]: sharp crests where the underlying noise crosses zero.</summary>
    public double Ridged(double x, double z, double frequency, int octaves = 4, int salt = 0)
    {
        double sum = 0, norm = 0, amplitude = 1;
        for (int o = 0; o < octaves; o++)
        {
            double r = 1 - Math.Abs(Value(x, z, frequency, salt * 31 + 1000 + o));
            sum += amplitude * r * r;
            norm += amplitude;
            amplitude *= 0.5;
            frequency *= 2;
        }
        return sum / norm;
    }

    private static int Wrap(long i, int cells) => (int)(((i % cells) + cells) % cells);

    /// <summary>Quintic fade: C2-continuous interpolation between lattice values.</summary>
    private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);

    private static double Lattice(int i, int j, uint seed)
    {
        uint h = Mix((uint)i * 0x8da6b343u ^ Mix((uint)j * 0xd8163841u ^ seed));
        return h / (double)uint.MaxValue * 2 - 1;
    }

    private static uint Mix(uint h)
    {
        h ^= h >> 16; h *= 0x7feb352du;
        h ^= h >> 15; h *= 0x846ca68bu;
        h ^= h >> 16;
        return h;
    }
}

/// <summary>Domain-warp offsets baked once over one period × the map depth, about four samples per world unit.
/// <see cref="Data"/> holds interleaved (dx, dz) pairs in [-1, 1], row-major (row = z sample, column = x sample),
/// so a renderer can upload the same grid as a texture and sample it exactly like <see cref="Sample"/>:
/// bilinear, wrapping in x over the period and clamping in z.</summary>
public sealed class WarpGrid
{
    /// <summary>Bake the grid from two decorrelated noise channels.</summary>
    public WarpGrid(int seed, double period, double zMin, double zMax, double frequency, double samplesPerUnit = 4)
    {
        if (!(zMax > zMin)) throw new ArgumentOutOfRangeException(nameof(zMax));
        var noise = new PeriodicNoise(seed ^ 0x5bd1e995, period);
        Period = period;
        ZMin = zMin;
        Columns = Math.Max(1, (int)Math.Round(period * samplesPerUnit));
        Rows = Math.Max(2, (int)Math.Ceiling((zMax - zMin) * samplesPerUnit) + 1);
        StepX = period / Columns;
        StepZ = (zMax - zMin) / (Rows - 1);
        Data = new float[Columns * Rows * 2];
        for (int row = 0; row < Rows; row++)
            for (int col = 0; col < Columns; col++)
            {
                double x = col * StepX, z = zMin + row * StepZ;
                int i = (row * Columns + col) * 2;
                Data[i] = (float)noise.Fbm(x, z, frequency, 2, salt: 1);
                Data[i + 1] = (float)noise.Fbm(x, z, frequency, 2, salt: 2);
            }
    }

    /// <summary>Horizontal wrap period of the grid.</summary>
    public double Period { get; }
    /// <summary>World z of the first sample row.</summary>
    public double ZMin { get; }
    /// <summary>Samples per row; the sample after the last column is column 0 (one period later).</summary>
    public int Columns { get; }
    /// <summary>Sample rows; z beyond the first or last row clamps.</summary>
    public int Rows { get; }
    /// <summary>World distance between columns.</summary>
    public double StepX { get; }
    /// <summary>World distance between rows.</summary>
    public double StepZ { get; }
    /// <summary>Interleaved (dx, dz) offsets in [-1, 1].</summary>
    public float[] Data { get; }

    /// <summary>Bilinear offset at a world position, in [-1, 1] per component.</summary>
    public (double X, double Z) Sample(double x, double z)
    {
        double u = x / StepX;
        double fu = Math.Floor(u);
        int c0 = (int)(((long)fu % Columns + Columns) % Columns), c1 = c0 + 1 == Columns ? 0 : c0 + 1;
        double tx = u - fu;
        double v = Math.Clamp((z - ZMin) / StepZ, 0, Rows - 1);
        int r0 = Math.Min((int)v, Rows - 2);
        double tz = v - r0;
        int a = (r0 * Columns + c0) * 2, b = (r0 * Columns + c1) * 2;
        int c = ((r0 + 1) * Columns + c0) * 2, d = ((r0 + 1) * Columns + c1) * 2;
        return (Bilerp(Data[a], Data[b], Data[c], Data[d], tx, tz),
            Bilerp(Data[a + 1], Data[b + 1], Data[c + 1], Data[d + 1], tx, tz));
    }

    private static double Bilerp(double a, double b, double c, double d, double tx, double tz)
    {
        double top = a + (b - a) * tx, bottom = c + (d - c) * tx;
        return top + (bottom - top) * tz;
    }
}
