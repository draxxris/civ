namespace Ariel.Client.Terrain;

/// <summary>Engine-free arrays for the springs of one chunk: <see cref="Bubbles"/> lie flat on each spring's water and
/// rise, swell and pop on their own cycles; <see cref="Mist"/> is a few thin camera-facing wisps drifting up off it. Both
/// are <see cref="PuffMeshData"/>.</summary>
public sealed record SpringMeshData(PuffMeshData Bubbles, PuffMeshData Mist);

/// <summary>Builds what marks a spring (<see cref="RiverPond.Spring"/>) out from a plain pond: bubbles welling up on its
/// water and a breath of mist over it. The pool itself is the river's own water, drawn by <see cref="RiverMesher"/>. A
/// spring belongs to the chunk holding its centre, which shares wrap copies and dirty signatures with the chunk
/// meshes.</summary>
public static class SpringMesher
{
    /// <summary>Bubbles per spring, how far from its centre they come up (a share of its radius) and their half sizes
    /// (shares of its radius).</summary>
    public const int Bubbles = 7;
    private const double BubbleSpread = 0.6, BubbleMin = 0.14, BubbleMax = 0.26;
    /// <summary>Height above the water the bubbles lie at, so the water never hides them.</summary>
    public const double BubbleLift = 0.004;
    /// <summary>Wisps of mist per spring, their half sizes (shares of its radius) and how high they drift over a cycle.</summary>
    public const int Wisps = 3;
    private const double WispMin = 1.0, WispMax = 1.6, WispRiseMin = 0.1, WispRiseMax = 0.18;
    /// <summary>How thick the wisps are, on the mist shader's density scale: thin and slow.</summary>
    private const double WispDensity = 0.15;

    /// <summary>The chunk's springs, or null when none lies in it.</summary>
    public static SpringMeshData? Build(TerrainChunk chunk, TerrainField field, int k = TerrainMesher.DefaultResolution) =>
        Build(chunk, field, new TerrainLattice(field, k));

    /// <summary>The chunk's springs over terrain whose resolution varies by chunk (see
    /// <see cref="TerrainChunks.ResolutionAt"/>), or null when none lies in it.</summary>
    public static SpringMeshData? Build(TerrainChunk chunk, TerrainField field, Func<double, double, int> resolutionAt) =>
        Build(chunk, field, new TerrainLattice(field, resolutionAt));

    private static SpringMeshData? Build(TerrainChunk chunk, TerrainField field, TerrainLattice lattice)
    {
        var bubbles = new PuffBuilder();
        var mist = new PuffBuilder();
        double period = field.Period;
        double[] shifts = field.Snapshot.Grid.Spec.Wrap ? [-period, 0, period] : [0];
        foreach (var curve in field.Rivers.Curves)
            foreach (var (spring, atStart) in new[] { (curve.StartPond, true), (curve.EndPond, false) })
            {
                if (spring is not { Spring: true }) continue;
                foreach (double shift in shifts)
                {
                    if (!chunk.Contains(spring.X + shift, spring.Z)) continue;
                    double level = RiverMesher.PondLevel(field, lattice, curve, atStart, shift);
                    Add(bubbles, mist, spring, spring.X + shift, level, lattice);
                }
            }
        return bubbles.Empty ? null : new(bubbles.ToData(), mist.ToData());
    }

    private static void Add(PuffBuilder bubbles, PuffBuilder mist, RiverPond spring, double x, double level, TerrainLattice lattice)
    {
        double r = spring.Radius;
        for (int b = 0; b < Bubbles; b++)
        {
            // Spread evenly over the disc (the square root keeps the middle from crowding).
            double angle = 2 * Math.PI * Hash(spring.Hash, b, 1), reach = BubbleSpread * r * Math.Sqrt(Hash(spring.Hash, b, 2));
            double size = r * (BubbleMin + (BubbleMax - BubbleMin) * Hash(spring.Hash, b, 3));
            // The rise slot holds how much slower than the shader's own cycle this bubble swells and pops.
            double bx = x + reach * Math.Cos(angle), bz = spring.Z + reach * Math.Sin(angle);
            bubbles.Add(bx, Surface(lattice, bx, bz, level) + BubbleLift, bz, size, Hash(spring.Hash, b, 4),
                0.7 + 0.6 * Hash(spring.Hash, b, 5), 0);
        }
        for (int w = 0; w < Wisps; w++)
        {
            double angle = 2 * Math.PI * Hash(spring.Hash, w + 20, 1), reach = 0.5 * r * Hash(spring.Hash, w + 20, 2);
            double size = r * (WispMin + (WispMax - WispMin) * Hash(spring.Hash, w + 20, 3));
            double rise = WispRiseMin + (WispRiseMax - WispRiseMin) * Hash(spring.Hash, w + 20, 5);
            mist.Add(x + reach * Math.Cos(angle), level + 0.25 * size, spring.Z + reach * Math.Sin(angle), size,
                Hash(spring.Hash, w + 20, 4), rise, WispDensity);
        }
    }

    /// <summary>The height of the drawn water at a point of a spring whose still water lies at <paramref name="level"/>: as
    /// <see cref="RiverMesher"/> draws it, on the ground where that stands no more than
    /// <see cref="RiverMesher.WaterOnGround"/> above the level.</summary>
    private static double Surface(TerrainLattice lattice, double x, double z, double level)
    {
        double ground = lattice.Sample(x, z).Height;
        return ground > level && ground - level <= RiverMesher.WaterOnGround ? ground : level;
    }

    private static double Hash(uint seed, int i, int salt)
    {
        double s = Math.Sin(seed * 0.000917 + i * 12.9898 + salt * 78.233) * 43758.5453;
        return s - Math.Floor(s);
    }
}
