using System.Numerics;

namespace Civ.Client.Terrain;

/// <summary>Every terrain height tunable in one place. Heights are world units; hexes have outer radius 1.</summary>
public sealed record TerrainTuning
{
    /// <summary>Maximum domain-warp offset per axis applied before blending (organic tile borders).</summary>
    public double WarpAmplitude { get; init; } = 0.3;
    /// <summary>Warp noise frequency, cells per world unit.</summary>
    public double WarpFrequency { get; init; } = 0.45;
    /// <summary>Blend sharpness for land, water and hidden heights (and materials).</summary>
    public double MaterialSharpness { get; init; } = HexBlend.MaterialSharpness;
    /// <summary>Blend sharpness of peak heights.</summary>
    public double PeakSharpness { get; init; } = HexBlend.PeakSharpness;
    /// <summary>Fractal octaves of every profile noise.</summary>
    public int Octaves { get; init; } = 4;

    /// <summary>Ocean floor height.</summary>
    public double DeepWater { get; init; } = -0.45;
    /// <summary>Coast and lake floor height.</summary>
    public double ShallowWater { get; init; } = -0.15;
    /// <summary>Amplitude of the gentle seabed noise.</summary>
    public double WaterNoise { get; init; } = 0.02;
    /// <summary>Seabed noise frequency.</summary>
    public double WaterFrequency { get; init; } = 0.8;

    /// <summary>Flat land: base height.</summary>
    public double FlatBase { get; init; } = 0.03;
    /// <summary>Flat land: fbm amplitude.</summary>
    public double FlatNoise { get; init; } = 0.03;
    /// <summary>Flat land: fbm frequency.</summary>
    public double FlatFrequency { get; init; } = 1.5;

    /// <summary>Hills: base height.</summary>
    public double HillsBase { get; init; } = 0.20;
    /// <summary>Hills: fbm amplitude.</summary>
    public double HillsNoise { get; init; } = 0.14;
    /// <summary>Hills: fbm frequency.</summary>
    public double HillsFrequency { get; init; } = 2.2;

    /// <summary>Peaks: base height.</summary>
    public double PeakBase { get; init; } = 0.45;
    /// <summary>Peaks: ridged-noise amplitude.</summary>
    public double PeakNoise { get; init; } = 0.75;
    /// <summary>Peaks: ridged-noise frequency.</summary>
    public double PeakFrequency { get; init; } = 1.4;

    /// <summary>Maximum river carve depth at a river edge.</summary>
    public double RiverDepth { get; init; } = 0.06;
    /// <summary>Distance from a river edge over which the carve fades out.</summary>
    public double RiverBand { get; init; } = 0.12;

    /// <summary>Half step of the central differences in <see cref="TerrainField.NormalAt"/> (chunk meshes difference their
    /// own lattice heights instead).</summary>
    public double NormalStep { get; init; } = 0.05;

    /// <summary>Largest distance from a point to the centre of a tile that can affect its height or normal. A tile blends
    /// into the six corner triangles around it, which reach its neighbours' centres (√3), widened by the warp; its river
    /// edges (within 1 of its centre) carve within the river band.</summary>
    public double InfluenceRadius => Math.Max(Math.Sqrt(3) + WarpAmplitude * Math.Sqrt(2), 1 + RiverBand) + NormalStep;
}

/// <summary>The terrain height as one pure function of world (x, z) and a <see cref="TerrainSnapshot"/>. Every chunk
/// mesh, normal, scatter point and ray pick samples this function, so tile and chunk borders cannot crack.
/// Profiles use world-space periodic noise, so adjacent peaks form one continuous range and the wrap has no seam.</summary>
public sealed class TerrainField
{
    private static readonly double Sqrt3 = Math.Sqrt(3);
    private static readonly (double X, double Z)[] DirectionOffsets =
        Hex.Directions.Select(d => (Sqrt3 * (d.Q + d.R / 2.0), 1.5 * d.R)).ToArray();

    private readonly PeriodicNoise _noise;
    private readonly bool _wrap;

    /// <summary>Field for a snapshot; the seed fixes the noise and the warp grid.</summary>
    public TerrainField(TerrainSnapshot snapshot, int seed, TerrainTuning? tuning = null)
        : this(snapshot, seed, tuning ?? new TerrainTuning(), new PeriodicNoise(seed, snapshot.Period),
            BakeWarp(snapshot, seed, tuning ?? new TerrainTuning()))
    {
    }

    private TerrainField(TerrainSnapshot snapshot, int seed, TerrainTuning tuning, PeriodicNoise noise, WarpGrid warp)
    {
        Snapshot = snapshot;
        Seed = seed;
        Tuning = tuning;
        _noise = noise;
        Warp = warp;
        _wrap = snapshot.Grid.Spec.Wrap;
        MaxHeight = new[] { 0, tuning.DeepWater + tuning.WaterNoise, tuning.ShallowWater + tuning.WaterNoise,
            tuning.FlatBase + tuning.FlatNoise, tuning.HillsBase + tuning.HillsNoise, tuning.PeakBase + tuning.PeakNoise }.Max();
        MinHeight = new[] { 0, tuning.DeepWater - tuning.WaterNoise, tuning.ShallowWater - tuning.WaterNoise,
            tuning.FlatBase - tuning.FlatNoise, tuning.HillsBase - tuning.HillsNoise, tuning.PeakBase }.Min() - tuning.RiverDepth;
    }

    /// <summary>Tile inputs.</summary>
    public TerrainSnapshot Snapshot { get; }
    /// <summary>Map seed shared by deterministic scatter hashes.</summary>
    public int Seed { get; }
    /// <summary>Height tunables.</summary>
    public TerrainTuning Tuning { get; }
    /// <summary>Baked domain warp shared with the terrain shader.</summary>
    public WarpGrid Warp { get; }
    /// <summary>Map period in world units.</summary>
    public double Period => Snapshot.Period;
    /// <summary>Upper bound of <see cref="HeightAt"/> anywhere.</summary>
    public double MaxHeight { get; }
    /// <summary>Lower bound of <see cref="HeightAt"/> anywhere.</summary>
    public double MinHeight { get; }

    /// <summary>A field over a newer snapshot of the same map that reuses this field's noise and warp grid.</summary>
    public TerrainField With(TerrainSnapshot snapshot)
    {
        if (snapshot.Grid.Spec != Snapshot.Grid.Spec) throw new ArgumentException("Snapshot is for a different map", nameof(snapshot));
        return new(snapshot, Seed, Tuning, _noise, Warp);
    }

    /// <summary>Terrain height at a world position.</summary>
    public double HeightAt(double x, double z)
    {
        if (_wrap) x -= Period * Math.Floor(x / Period);
        var w = Weights(x, z, out var c);
        double h = 0, hidden = 0;
        Add(w.A, c.A); Add(w.B, c.B); Add(w.C, c.C);
        if (hidden < 1) h -= (1 - hidden) * RiverCarve(x, z);
        return h;

        void Add(double weight, ReliefClass relief)
        {
            if (weight <= 0) return;
            if (relief == ReliefClass.Hidden) hidden += weight;
            else h += weight * Profile(relief, x, z);
        }
    }

    /// <summary>Whether a water tile contributes to the blend here. Land-only river troughs stay dry.</summary>
    public bool NearWater(double x, double z)
    {
        var (ox, oz) = Warp.Sample(x, z);
        var tri = HexBlend.CornerTriangle(x + ox * Tuning.WarpAmplitude, z + oz * Tuning.WarpAmplitude);
        for (int k = 0; k < 3; k++)
            if (Snapshot.Relief[Snapshot.ClampedTile(tri[k])] is ReliefClass.DeepWater or ReliefClass.ShallowWater) return true;
        return false;
    }

    /// <summary>Visible ground or water height for surface objects and picking. The seabed remains at HeightAt.</summary>
    public double SurfaceHeightAt(double x, double z)
    {
        double height = HeightAt(x, z);
        return height < 0 && NearWater(x, z) ? 0 : height;
    }

    /// <summary>Unit surface normal from central differences of <see cref="HeightAt"/>.</summary>
    public Vector3 NormalAt(double x, double z)
    {
        double e = Tuning.NormalStep;
        double dx = (HeightAt(x + e, z) - HeightAt(x - e, z)) / (2 * e);
        double dz = (HeightAt(x, z + e) - HeightAt(x, z - e)) / (2 * e);
        return Vector3.Normalize(new Vector3((float)-dx, 1f, (float)-dz));
    }

    /// <summary>Height blend weights of the corner triangle at a world position, after domain warp. Non-peak tiles use
    /// the material sharpness. Peak tiles use the peak sharpness and the non-peak tiles share the remainder in proportion
    /// to their material weights, so the weights always sum to 1 and stay continuous across triangle borders.</summary>
    private Blend3 Weights(double x, double z, out (ReliefClass A, ReliefClass B, ReliefClass C) classes)
    {
        double a = Tuning.WarpAmplitude;
        double wx = x, wz = z;
        if (a != 0)
        {
            var (ox, oz) = Warp.Sample(x, z);
            wx += ox * a; wz += oz * a;
        }
        var tri = HexBlend.CornerTriangle(wx, wz);
        var s = Snapshot;
        classes = (s.Relief[s.ClampedTile(tri.A)], s.Relief[s.ClampedTile(tri.B)], s.Relief[s.ClampedTile(tri.C)]);
        bool pa = classes.A == ReliefClass.Peak, pb = classes.B == ReliefClass.Peak, pc = classes.C == ReliefClass.Peak;
        var w = HexBlend.Weights(tri.Lambda, Tuning.MaterialSharpness);
        if (!pa && !pb && !pc) return w;
        var wp = HexBlend.Weights(tri.Lambda, Tuning.PeakSharpness);
        double peak = (pa ? wp.A : 0) + (pb ? wp.B : 0) + (pc ? wp.C : 0);
        double rest = (pa ? 0 : w.A) + (pb ? 0 : w.B) + (pc ? 0 : w.C);
        double scale = rest > 1e-12 ? (1 - peak) / rest : 0;
        return new(pa ? wp.A : w.A * scale, pb ? wp.B : w.B * scale, pc ? wp.C : w.C * scale);
    }

    private double Profile(ReliefClass relief, double x, double z)
    {
        var t = Tuning;
        return relief switch
        {
            ReliefClass.DeepWater => t.DeepWater + t.WaterNoise * _noise.Fbm(x, z, t.WaterFrequency, t.Octaves, salt: 1),
            ReliefClass.ShallowWater => t.ShallowWater + t.WaterNoise * _noise.Fbm(x, z, t.WaterFrequency, t.Octaves, salt: 1),
            ReliefClass.Flat => t.FlatBase + t.FlatNoise * _noise.Fbm(x, z, t.FlatFrequency, t.Octaves, salt: 2),
            ReliefClass.Hills => t.HillsBase + t.HillsNoise * _noise.Fbm(x, z, t.HillsFrequency, t.Octaves, salt: 3),
            ReliefClass.Peak => t.PeakBase + t.PeakNoise * _noise.Ridged(x, z, t.PeakFrequency, t.Octaves, salt: 4),
            _ => 0,
        };
    }

    /// <summary>River carve: a smooth trough along every river edge, from the distance to the nearest river edge among the
    /// edges of the containing hex and of its six neighbours (every edge that can lie within the band). An edge counts when
    /// either side's (mirrored) bit is set, so the carve is continuous across the edge. Hidden tiles carry no river bits.</summary>
    private double RiverCarve(double x, double z)
    {
        double best = RiverDistance(x, z);
        double band = Tuning.RiverBand;
        if (best >= band) return 0;
        double t = 1 - best / band * (best / band);
        return Tuning.RiverDepth * t * t;
    }

    /// <summary>Distance to a known river edge, capped at the carve band width.</summary>
    public double RiverDistance(double x, double z)
    {
        var s = Snapshot;
        var centre = Hex.FromPixel(x, z, 1);
        double band = Tuning.RiverBand;
        double best = band;
        for (int n = -1; n < 6; n++)
        {
            var hex = n < 0 ? centre : centre.Neighbor(n);
            if (!s.TryTile(hex, out int tile)) continue;
            byte bits = s.Rivers[tile];
            if (bits == 0) continue;
            var (cx, cz) = hex.ToPixel(1);
            for (int d = 0; d < 6; d++)
                if ((bits & (1 << d)) != 0) best = Math.Min(best, EdgeDistance(cx, cz, d, x, z));
        }
        return best;
    }

    /// <summary>Distance from (x, z) to the edge of the hex centred at (cx, cz) that faces direction d.</summary>
    private static double EdgeDistance(double cx, double cz, int d, double x, double z)
    {
        var (vx, vz) = DirectionOffsets[d];
        double mx = cx + vx / 2, mz = cz + vz / 2;
        double ux = -vz / Sqrt3, uz = vx / Sqrt3;
        double px = x - mx, pz = z - mz;
        double along = Math.Clamp(px * ux + pz * uz, -0.5, 0.5);
        double dx = px - along * ux, dz = pz - along * uz;
        return Math.Sqrt(dx * dx + dz * dz);
    }

    private static WarpGrid BakeWarp(TerrainSnapshot snapshot, int seed, TerrainTuning tuning) =>
        new(seed, snapshot.Period, -4.5, 1.5 * (snapshot.Height - 1) + 4.5, tuning.WarpFrequency);
}
