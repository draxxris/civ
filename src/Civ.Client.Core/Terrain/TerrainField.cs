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

    /// <summary>Hills: height of a hill's own profile away from its mounds; the saddle level between neighbouring hills.</summary>
    public double HillsBase { get; init; } = 0.05;
    /// <summary>Hills: largest mound rise above <see cref="HillsBase"/>. Base plus rise stays at or below
    /// <see cref="PeakBase"/>, so a hill never outgrows a mountain's saddle.</summary>
    public double HillsRise { get; init; } = 0.33;
    /// <summary>Hills: distance across a mound's long axis at which its rise reaches zero (along it, times
    /// <see cref="HillsStretch"/>).</summary>
    public double HillsRadius { get; init; } = 0.68;
    /// <summary>Hills: largest offset of the main mound from the tile centre.</summary>
    public double HillsJitter { get; init; } = 0.18;
    /// <summary>Hills: how much longer a mound is along its axis than across it.</summary>
    public double HillsStretch { get; init; } = 1.3;
    /// <summary>Hills: fraction of <see cref="HillsRise"/> by which the lowest main mound falls short of the highest.</summary>
    public double HillsVariation { get; init; } = 0.3;
    /// <summary>Hills: height of the smaller shoulder knoll beside the main mound, as a fraction of the main rise.</summary>
    public double HillsKnoll { get; init; } = 0.6;
    /// <summary>Hills: fbm amplitude.</summary>
    public double HillsNoise { get; init; } = 0.04;
    /// <summary>Hills: fbm frequency.</summary>
    public double HillsFrequency { get; init; } = 2.2;

    /// <summary>Peaks: height of a peak's own profile away from its summit; the saddle level of a connected range.
    /// It stays above the hills so adjacent peaks keep one ridge.</summary>
    public double PeakBase { get; init; } = 0.38;
    /// <summary>Peaks: largest summit rise above <see cref="PeakBase"/>; each tile scales it by its own variation.</summary>
    public double PeakRise { get; init; } = 0.9;
    /// <summary>Peaks: distance across the ridge axis at which a summit's rise reaches zero (along it, times
    /// <see cref="PeakStretch"/>).</summary>
    public double PeakRadius { get; init; } = 1.1;
    /// <summary>Peaks: exponent of the summit falloff. Above 1 gives steep, pointed tops over gentle feet.</summary>
    public double PeakCurve { get; init; } = 1.7;
    /// <summary>Peaks: largest summit offset from the tile centre.</summary>
    public double PeakJitter { get; init; } = 0.22;
    /// <summary>Peaks: fraction of <see cref="PeakRise"/> by which the lowest summit falls short of the highest.</summary>
    public double PeakVariation { get; init; } = 0.35;
    /// <summary>Peaks: how much longer a summit is along its ridge axis than across it. Axes follow a slow world-space
    /// field, so neighbouring summits line up into ranges.</summary>
    public double PeakStretch { get; init; } = 2.2;
    /// <summary>Peaks: relative distortion of the summit distance, which grows spurs and irregular footprints.</summary>
    public double PeakSpur { get; init; } = 0.3;
    /// <summary>Peaks: ridged-noise (crag) amplitude.</summary>
    public double PeakNoise { get; init; } = 0.36;
    /// <summary>Peaks: ridged-noise frequency.</summary>
    public double PeakFrequency { get; init; } = 1.3;

    /// <summary>Largest river carve depth, under the middle of a full-width river.</summary>
    public double RiverDepth { get; init; } = 0.075;
    /// <summary>Water half width of a lowland river; sources, hills, peaks and mouths scale it (see <see cref="RiverNetwork"/>).</summary>
    public double RiverWidth { get; init; } = 0.16;
    /// <summary>Half width of the carved channel and its banks, as a multiple of the local water half width.</summary>
    public double RiverBankRatio { get; init; } = 2.2;

    /// <summary>Half step of the central differences in <see cref="TerrainField.NormalAt"/> (chunk meshes difference their
    /// own lattice heights instead).</summary>
    public double NormalStep { get; init; } = 0.05;

    /// <summary>Largest distance from a point to the centre of a tile whose relief can affect its height or normal away from
    /// rivers. A tile blends into the six corner triangles around it, which reach its neighbours' centres (√3), widened by the
    /// warp. River geometry reaches further; see <see cref="RiverReach"/>.</summary>
    public double InfluenceRadius => Math.Sqrt(3) + WarpAmplitude * Math.Sqrt(2) + NormalStep;

    /// <summary>Largest distance from a point to the centre of a tile whose river bits or relief can change river geometry
    /// (carve, banks or water surface) at that point. A point lies within one carve band of a curve sample; the sample lies
    /// within one link span (a bowed bridge across a tile) of the link's corner nodes; a node depends on the river tiles
    /// around it (1) and on their bridge choices, which read their own neighbours (√3). River surfaces also overhang their
    /// chunk by up to one band and read heights there.</summary>
    public double RiverReach
    {
        get
        {
            double band = RiverWidth * RiverNetwork.MaxWidthScale * RiverBankRatio;
            return band + Math.Max(InfluenceRadius, band + RiverNetwork.MaxLinkSpan + 1 + Math.Sqrt(3));
        }
    }
}

/// <summary>The terrain height as one pure function of world (x, z) and a <see cref="TerrainSnapshot"/>. Every chunk
/// mesh, normal, scatter point and ray pick samples this function, so tile and chunk borders cannot crack.
/// Profiles use world-space periodic noise, so adjacent peaks form one continuous range and the wrap has no seam.</summary>
public sealed class TerrainField
{
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
        Rivers = new RiverNetwork(snapshot, tuning, noise);
        _wrap = snapshot.Grid.Spec.Wrap;
        MaxHeight = new[] { 0, tuning.DeepWater + tuning.WaterNoise, tuning.ShallowWater + tuning.WaterNoise,
            tuning.FlatBase + tuning.FlatNoise, tuning.HillsBase + tuning.HillsRise + tuning.HillsNoise,
            tuning.PeakBase + tuning.PeakRise + tuning.PeakNoise }.Max();
        MinHeight = new[] { 0, tuning.DeepWater - tuning.WaterNoise, tuning.ShallowWater - tuning.WaterNoise,
            tuning.FlatBase - tuning.FlatNoise, tuning.HillsBase - tuning.HillsNoise, tuning.PeakBase }.Min() - tuning.RiverDepth;
    }

    /// <summary>Tile inputs.</summary>
    public TerrainSnapshot Snapshot { get; }
    /// <summary>Map seed shared by deterministic scatter hashes.</summary>
    public int Seed { get; }
    /// <summary>Height tunables.</summary>
    public TerrainTuning Tuning { get; }
    /// <summary>River curves derived from the snapshot's known river bits; they carve the height field.</summary>
    public RiverNetwork Rivers { get; }
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
        var corners = Corners(x, z);
        var w = HeightWeights(corners);
        var peak = corners.HasPeak ? PeakNoise(x, z) : default;
        double h = 0, hidden = 0;
        for (int k = 0; k < 3; k++)
        {
            if (w[k] <= 0) continue;
            var relief = corners.Relief(k);
            if (relief == ReliefClass.Hidden) hidden += w[k];
            else if (relief == ReliefClass.Peak) h += w[k] * PeakProfile(corners, k, peak);
            else if (relief == ReliefClass.Hills) h += w[k] * HillProfile(corners, k, x, z);
            else h += w[k] * Profile(relief, x, z);
        }
        if (hidden < 1) h -= (1 - hidden) * Rivers.Carve(x, z);
        return h;
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

    /// <summary>The warped corner triangle at a point, its tiles and relief classes, and the height blend weights.</summary>
    private readonly record struct CornerSet(TileTriangle Triangle, double X, double Z, int TileA, int TileB, int TileC,
        ReliefClass A, ReliefClass B, ReliefClass C)
    {
        public bool HasPeak => A == ReliefClass.Peak || B == ReliefClass.Peak || C == ReliefClass.Peak;
        public int Tile(int k) => k switch { 0 => TileA, 1 => TileB, _ => TileC };
        public ReliefClass Relief(int k) => k switch { 0 => A, 1 => B, _ => C };
    }

    /// <summary>Peak noise shared by every peak corner at one point: crag ridges in [0, 1] and spur distortion in [-1, 1].</summary>
    private readonly record struct PeakDetail(double Crag, double Spur);

    /// <summary>Resolve the warped corner triangle and its known relief classes.</summary>
    private CornerSet Corners(double x, double z)
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
        int ta = s.ClampedTile(tri.A), tb = s.ClampedTile(tri.B), tc = s.ClampedTile(tri.C);
        var (ra, rb, rc) = (s.Relief[ta], s.Relief[tb], s.Relief[tc]);
        return new(tri, wx, wz, ta, tb, tc, ra, rb, rc);
    }

    /// <summary>Peaks use softer height weights. Other profiles share the remaining weight in proportion to their
    /// material weights, preserving a continuous blend whose weights sum to one.</summary>
    private Blend3 HeightWeights(in CornerSet corners)
    {
        var w = HexBlend.Weights(corners.Triangle.Lambda, Tuning.MaterialSharpness);
        if (!corners.HasPeak) return w;
        var wp = HexBlend.Weights(corners.Triangle.Lambda, Tuning.PeakSharpness);
        double peak = 0, rest = 0;
        for (int k = 0; k < 3; k++)
        {
            if (corners.Relief(k) == ReliefClass.Peak) peak += wp[k];
            else rest += w[k];
        }
        double scale = rest > 1e-12 ? (1 - peak) / rest : 0;
        return new(corners.A == ReliefClass.Peak ? wp.A : w.A * scale,
            corners.B == ReliefClass.Peak ? wp.B : w.B * scale,
            corners.C == ReliefClass.Peak ? wp.C : w.C * scale);
    }

    private PeakDetail PeakNoise(double x, double z)
    {
        var t = Tuning;
        return new(_noise.Ridged(x, z, t.PeakFrequency, t.Octaves, salt: 4), _noise.Fbm(x, z, 0.9, 2, salt: 5));
    }

    /// <summary>One peak tile's own mountain: a jittered summit near its centre whose rise falls off with a convex power
    /// curve (steep pointed top, gentle feet) over a distorted distance, on the <see cref="TerrainTuning.PeakBase"/> saddle level,
    /// plus crags that are strongest high up. The tile's weight fades this to zero wherever the tile stops shaping the
    /// height, so a neighbouring peak forms a connected ridge with a saddle between two distinct summits.</summary>
    private double PeakProfile(in CornerSet corners, int k, PeakDetail detail)
    {
        var t = Tuning;
        int tile = corners.Tile(k);
        var (cx, cz) = corners.Triangle[k].ToPixel(1);
        double angle = TileHash(tile, 1) * Math.Tau, offset = t.PeakJitter * TileHash(tile, 2);
        double dx = corners.X - cx - offset * Math.Cos(angle), dz = corners.Z - cz - offset * Math.Sin(angle);
        // Ridge axis from a slow field at the (canonical) tile centre, so all copies of a tile agree.
        double axis = Math.PI * _noise.Value(cx - Period * Math.Floor(cx / Period), cz, 0.2, salt: 6) + 0.6 * (TileHash(tile, 4) - 0.5);
        double along = dx * Math.Cos(axis) + dz * Math.Sin(axis), across = dz * Math.Cos(axis) - dx * Math.Sin(axis);
        along /= t.PeakStretch;
        double distance = Math.Sqrt(along * along + across * across) * (1 + t.PeakSpur * detail.Spur);
        double rise = Math.Pow(Math.Max(0, 1 - distance / t.PeakRadius), t.PeakCurve);
        double summit = 1 - t.PeakVariation * TileHash(tile, 3);
        return t.PeakBase + t.PeakRise * summit * rise + t.PeakNoise * detail.Crag * (0.5 + 0.5 * rise);
    }

    /// <summary>One hill tile's own landform: a rounded main mound near its centre and a smaller shoulder knoll to one side,
    /// merged with a soft union, on the <see cref="TerrainTuning.HillsBase"/> saddle level, plus gentle fbm. Both bells have
    /// flat tops and feet, so the tile reads as a raised, rolling hill rather than a plateau, and neighbouring hills form
    /// one range of distinct crowns. The tile's blend weight fades this to zero at its borders.</summary>
    private double HillProfile(in CornerSet corners, int k, double x, double z)
    {
        var t = Tuning;
        int tile = corners.Tile(k);
        var (cx, cz) = corners.Triangle[k].ToPixel(1);
        double axis = TileHash(tile, 11) * Math.PI;
        double jitterAngle = TileHash(tile, 12) * Math.Tau, jitter = t.HillsJitter * TileHash(tile, 13);
        double mx = cx + jitter * Math.Cos(jitterAngle), mz = cz + jitter * Math.Sin(jitterAngle);
        double main = Bell(corners.X - mx, corners.Z - mz, axis, t.HillsRadius, t.HillsStretch);
        main *= 1 - t.HillsVariation * TileHash(tile, 14);
        // The knoll sits on the far side of the main mound, roughly along its axis, so the pair reads as one ridge.
        double knollAngle = axis + (TileHash(tile, 15) < 0.5 ? 0 : Math.PI) + 0.8 * (TileHash(tile, 16) - 0.5);
        double reach = 0.42 + 0.12 * TileHash(tile, 17);
        double kx = mx + reach * Math.Cos(knollAngle), kz = mz + reach * Math.Sin(knollAngle);
        double knoll = t.HillsKnoll * (0.6 + 0.4 * TileHash(tile, 18))
            * Bell(corners.X - kx, corners.Z - kz, knollAngle, t.HillsRadius * 0.62, 1.15);
        double shape = main + knoll - main * knoll;
        return t.HillsBase + t.HillsRise * shape + t.HillsNoise * _noise.Fbm(x, z, t.HillsFrequency, t.Octaves, salt: 3);
    }

    /// <summary>A smooth bell in [0, 1]: (1 − u²)² over an elliptical distance u, flat at its crown and its rim.</summary>
    private static double Bell(double dx, double dz, double axis, double radius, double stretch)
    {
        double along = (dx * Math.Cos(axis) + dz * Math.Sin(axis)) / stretch, across = dz * Math.Cos(axis) - dx * Math.Sin(axis);
        double u2 = (along * along + across * across) / (radius * radius);
        if (u2 >= 1) return 0;
        double f = 1 - u2;
        return f * f;
    }

    /// <summary>Deterministic per-tile value in [0, 1) for the map seed.</summary>
    private double TileHash(int tile, uint channel)
    {
        uint h = Mix((uint)tile * 0x8da6b343u ^ Mix((uint)Seed ^ channel * 0x9e3779b9u));
        return Mix(h) / 4294967296.0;
    }

    private static uint Mix(uint h)
    {
        h ^= h >> 16; h *= 0x7feb352du;
        h ^= h >> 15; h *= 0x846ca68bu;
        return h ^ (h >> 16);
    }

    private double Profile(ReliefClass relief, double x, double z)
    {
        var t = Tuning;
        return relief switch
        {
            ReliefClass.DeepWater => t.DeepWater + t.WaterNoise * _noise.Fbm(x, z, t.WaterFrequency, t.Octaves, salt: 1),
            ReliefClass.ShallowWater => t.ShallowWater + t.WaterNoise * _noise.Fbm(x, z, t.WaterFrequency, t.Octaves, salt: 1),
            ReliefClass.Flat => t.FlatBase + t.FlatNoise * _noise.Fbm(x, z, t.FlatFrequency, t.Octaves, salt: 2),

            _ => 0,
        };
    }

    private static WarpGrid BakeWarp(TerrainSnapshot snapshot, int seed, TerrainTuning tuning) =>
        new(seed, snapshot.Period, -4.5, 1.5 * (snapshot.Height - 1) + 4.5, tuning.WarpFrequency);
}
