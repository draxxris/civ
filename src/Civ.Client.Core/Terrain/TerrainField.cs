using System.Numerics;

namespace Civ.Client.Terrain;

/// <summary>Every terrain height tunable in one place. Heights are world units; hexes have outer radius 1.</summary>
public sealed record TerrainTuning
{
    /// <summary>A copy with the named properties replaced (names are case-insensitive), for debug sweeps and A/B captures.
    /// Throws with the list of valid names for an unknown or non-numeric property.</summary>
    public TerrainTuning WithOverrides(IEnumerable<KeyValuePair<string, double>> overrides)
    {
        var clone = this with { };
        var properties = typeof(TerrainTuning).GetProperties()
            .Where(p => p.CanWrite && (p.PropertyType == typeof(double) || p.PropertyType == typeof(int))).ToArray();
        foreach (var (name, value) in overrides)
        {
            var property = properties.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"Unknown tuning '{name}'. Valid: {string.Join(", ", properties.Select(p => p.Name))}.");
            property.SetValue(clone, property.PropertyType == typeof(int) ? (object)(int)Math.Round(value) : value);
        }
        return clone;
    }

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
    public double PeakRise { get; init; } = 1.15;
    /// <summary>Peaks: distance across the ridge axis at which a summit's rise reaches zero (along it, times
    /// <see cref="PeakStretch"/>).</summary>
    public double PeakRadius { get; init; } = 1.25;
    /// <summary>Peaks: exponent of the summit falloff. Above 1 gives concave faces over gentle feet.</summary>
    public double PeakCurve { get; init; } = 1.5;
    /// <summary>Peaks: largest summit offset from the tile centre.</summary>
    public double PeakJitter { get; init; } = 0.22;
    /// <summary>Peaks: fraction of <see cref="PeakRise"/> by which the lowest summit falls short of the highest.</summary>
    public double PeakVariation { get; init; } = 0.4;
    /// <summary>Peaks: how much longer a summit is along its ridge axis than across it. Axes follow a slow world-space
    /// field, so neighbouring summits line up into ranges.</summary>
    public double PeakStretch { get; init; } = 1.5;
    /// <summary>Peaks: relative distortion of the summit distance, which grows spurs and irregular footprints.</summary>
    public double PeakSpur { get; init; } = 0.22;
    /// <summary>Peaks: how far a summit is a pyramid rather than a cone, in [0, 1]. Each summit has three to five faces of
    /// unequal width, and straight arêtes run down between them.</summary>
    public double PeakFacet { get; init; } = 0.85;
    /// <summary>Peaks: largest height of the lesser spires around the main summit, as a fraction of the main rise.</summary>
    public double PeakSpires { get; init; } = 0.75;
    /// <summary>Peaks: ridge height midway between two neighbouring peaks, as a fraction of their summits; each pair
    /// varies it by ±25%.</summary>
    public double PeakSaddle { get; init; } = 0.5;
    /// <summary>Peaks: ridge height where it meets a summit, as a fraction of that summit.</summary>
    public double PeakRidgeTop { get; init; } = 0.8;
    /// <summary>Peaks: half width of a ridge's base, in world units.</summary>
    public double PeakRidgeWidth { get; init; } = 0.75;
    /// <summary>Peaks: height of the broad massif under the summits, as a fraction of the main rise.</summary>
    public double PeakMassif { get; init; } = 0.4;
    /// <summary>Peaks: largest fraction of a face's height that its erosion gullies cut away. Gullies run down the faces,
    /// leave sharp ribs between them and fade out at the apex and the feet.</summary>
    public double PeakErosion { get; init; } = 0.5;
    /// <summary>Peaks: most gullies on one face; each face picks one up to this many.</summary>
    public int PeakGullies { get; init; } = 4;
    /// <summary>Peaks: wander of the gullies across a face (in face widths), so they bend instead of running straight.</summary>
    public double PeakMeander { get; init; } = 0.25;
    /// <summary>Peaks: ridged-noise (crag) amplitude.</summary>
    public double PeakNoise { get; init; } = 0.22;
    /// <summary>Peaks: ridged-noise frequency.</summary>
    public double PeakFrequency { get; init; } = 1.3;
    /// <summary>Peaks: amplitude of the fine rock relief (ledges, buttresses) on the upper slopes. It needs a fine mesh;
    /// see <see cref="TerrainMesher.PeakResolution"/>.</summary>
    public double PeakRoughness { get; init; } = 0.09;
    /// <summary>Peaks: frequency of the fine rock relief.</summary>
    public double PeakRoughnessFrequency { get; init; } = 4.5;

    /// <summary>Largest river carve depth, under the middle of a full-width river.</summary>
    public double RiverDepth { get; init; } = 0.075;
    /// <summary>Water half width of a lowland river; sources, hills, peaks and mouths scale it (see <see cref="RiverNetwork"/>).</summary>
    public double RiverWidth { get; init; } = 0.16;
    /// <summary>Half width of the carved channel and its banks, as a multiple of the local water half width.</summary>
    public double RiverBankRatio { get; init; } = 2.2;
    /// <summary>Distance beyond a river's banks, in world units, over which the terrain is drawn down to the river's valley floor
    /// (see <see cref="RiverNetwork.Level"/>).</summary>
    public double RiverDrawdown { get; init; } = 1.1;

    /// <summary>Cliffs: lift of every land tile with a cliff edge, over the whole tile, so the coast stands as a plateau
    /// above the sea. Hills and peaks on such a tile stand this much higher too.</summary>
    public double CliffRise { get; init; } = 0.22;
    /// <summary>Cliffs: distance (world units, about one tile across) beyond a cliff tile over which the neighbouring land
    /// climbs to the plateau, so the rise starts before the coast tile instead of at the cliff.</summary>
    public double CliffRamp { get; init; } = 1.6;
    /// <summary>Cliffs: blend sharpness across a cliff edge. Far above <see cref="MaterialSharpness"/>, so land drops to
    /// the seabed within about a lattice step.</summary>
    public double CliffSharpness { get; init; } = 14;
    /// <summary>Cliffs: distance from the edge (world units) over which the cliff shape is full.</summary>
    public double CliffInner { get; init; } = 0.16;
    /// <summary>Cliffs: distance at which the cliff shape has faded out. With <see cref="CliffNoise"/> it stays within half
    /// a tile of the edge, so only the corner triangles that share the edge feel it (the field stays continuous).</summary>
    public double CliffOuter { get; init; } = 0.42;
    /// <summary>Cliffs: how far noise moves the cliff's reach along the coast, so a bluff does not end in a straight line.</summary>
    public double CliffNoise { get; init; } = 0.06;
    /// <summary>Cliffs: how far coastal noise moves the cliff line from the hex edge, either way. It stays inside
    /// <see cref="CliffInner"/>, where the cliff shape is full.</summary>
    public double CliffWander { get; init; } = 0.1;
    /// <summary>Sea stacks: chance that a cliff edge has a lone rock off its foot (and a fifth of that for a second).</summary>
    public double StackChance { get; init; } = 0.2;
    /// <summary>Sea stacks: height of the shoal top each rock stands on. Just under the surface, so shore foam rings it.</summary>
    public double StackShoal { get; init; } = -0.012;

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
            // The drawdown reaches further than the carve, and the valley floor reads the ground at the banks.
            double band = RiverWidth * RiverNetwork.MaxWidthScale * RiverBankRatio;
            double reach = band + RiverDrawdown;
            return reach + Math.Max(InfluenceRadius + band, band + RiverNetwork.MaxLinkSpan + 1 + Math.Sqrt(3));
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
    /// <summary>Per-tile mountain shapes, built on first use. Racing threads build equal shapes, so either may win.</summary>
    private readonly Mountain?[] _mountains;
    /// <summary>Per tile: hex offsets to the plateau tiles (land with a cliff edge) within <see cref="PlateauReach"/>.
    /// Null for most tiles.</summary>
    private readonly Hex[]?[] _plateaus;
    /// <summary>Hex distance from a sample's tile to the farthest plateau whose ramp can reach it.</summary>
    private const int PlateauReach = 3;
    private static readonly (double X, double Z)[] EdgeNormals = Enumerable.Range(0, 3).Select(d =>
    {
        var (x, z) = Hex.Directions[d].ToPixel(1);
        double len = Math.Sqrt(x * x + z * z);
        return (x / len, z / len);
    }).ToArray();

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
        _mountains = new Mountain?[snapshot.Relief.Length];
        _plateaus = BuildPlateaus();
        Rivers = new RiverNetwork(snapshot, tuning, noise, (x, z) =>
        {
            if (_wrap) x -= Period * Math.Floor(x / Period);
            return GroundAt(x, z, out _);
        });
        MaxHeight = new[] { 0, tuning.DeepWater + tuning.WaterNoise, tuning.ShallowWater + tuning.WaterNoise,
            tuning.FlatBase + tuning.FlatNoise, tuning.HillsBase + tuning.HillsRise + tuning.HillsNoise,
            tuning.PeakBase + tuning.PeakRise + tuning.PeakNoise + tuning.PeakRoughness }.Max() + tuning.CliffRise;
        _stacks = new Lazy<IReadOnlyList<SeaStack>>(BuildStacks);
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
        double h = GroundAt(x, z, out double hidden);
        if (hidden < 1) h += (1 - hidden) * (Rivers.Level(x, z, h) - h - Rivers.Carve(x, z));
        return h;
    }

    /// <summary>The blended relief profiles before rivers level and carve them, and the hidden share of the blend.</summary>
    private double GroundAt(double x, double z, out double hidden)
    {
        var corners = Corners(x, z);
        var w = HeightWeights(corners);
        var peak = corners.HasPeak ? PeakNoise(x, z) : default;
        double h = 0, rise = Tuning.CliffRise * PlateauLift(corners);
        hidden = 0;
        // Profiles of all three corners: the cliff blend below needs the ones the regular weights ignore.
        double pa = 0, pb = 0, pc = 0;
        for (int k = 0; k < 3; k++)
        {
            var relief = corners.Relief(k);
            double p = relief switch
            {
                ReliefClass.Hidden => 0,
                ReliefClass.Peak => w[k] > 0 || corners.HasCliff ? PeakProfile(corners, k, peak) : 0,
                ReliefClass.Hills => w[k] > 0 || corners.HasCliff ? HillProfile(corners, k, x, z) : 0,
                _ => Profile(relief, x, z),
            };
            if (rise > 0 && relief != ReliefClass.Hidden && !IsWater(relief)) p += rise;
            if (k == 0) pa = p; else if (k == 1) pb = p; else pc = p;
            if (w[k] <= 0) continue;
            if (relief == ReliefClass.Hidden) hidden += w[k];
            else h += w[k] * p;
        }
        return corners.HasCliff ? CliffGround(corners, h, new Blend3(pa, pb, pc), x, z) : h;
    }

    /// <summary>The ground near cliff edges: within <see cref="TerrainTuning.CliffOuter"/> of a cliff edge it blends towards a
    /// much sharper land/sea blend of the (plateau-lifted) profiles, so the coast stands as a bluff over a steep rock face.
    /// Sea stacks then raise small shoals off the foot.</summary>
    private double CliffGround(in CornerSet corners, double h, Blend3 profiles, double x, double z)
    {
        var t = Tuning;
        var (cliff, _) = CliffShares(corners, x, z);
        if (cliff > 0)
        {
            var sharp = CliffWeights(corners, x, z);
            double hc = 0;
            for (int k = 0; k < 3; k++)
            {
                var relief = corners.Relief(k);
                if (relief == ReliefClass.Hidden) continue;
                hc += sharp[k] * profiles[k];
            }
            h += (hc - h) * cliff;
        }
        foreach (var (land, water) in CliffPairs(corners))
            foreach (var stack in StacksOf(corners, land, water))
            {
                double dx = corners.X - stack.X, dz = corners.Z - stack.Z, r2 = (dx * dx + dz * dz) / (stack.Radius * stack.Radius);
                if (r2 >= 1.5) continue;
                // A paraboloid shoal from just under the surface down past the seabed at its rim, merged softly.
                h = SoftMax(h, t.StackShoal - 0.2 * r2, 0.015);
            }
        return h;
    }

    /// <summary>Sharp land/sea weights for cliffs, λ^s with land corners biased by coastal noise so the cliff line wanders
    /// by up to <see cref="TerrainTuning.CliffWander"/> instead of following the straight hex edge. At an edge midpoint
    /// ln λ_land − ln λ_sea changes by 4/√3 per world unit, so a bias b on land and −b on sea moves the line by b·√3/2.
    /// The weights stay continuous: λ = 0 still gives zero weight.</summary>
    private Blend3 CliffWeights(in CornerSet corners, double x, double z)
    {
        var t = Tuning;
        var lambda = corners.Triangle.Lambda;
        double n = Math.Clamp(3.2 * (_noise.Fbm(x, z, 1.4, 3, salt: 10) - 0.5), -1, 1);
        double b = t.CliffWander * n * 2 / Math.Sqrt(3);
        double wa = 0, wb = 0, wc = 0, sum = 0;
        for (int k = 0; k < 3; k++)
        {
            var relief = corners.Relief(k);
            double bias = relief == ReliefClass.Hidden ? 0 : IsWater(relief) ? -b : b;
            double w = Math.Pow(Math.Clamp(lambda[k], 0, 1), t.CliffSharpness) * Math.Exp(t.CliffSharpness * bias);
            if (k == 0) wa = w; else if (k == 1) wb = w; else wc = w;
            sum += w;
        }
        return sum > 0 ? new(wa / sum, wb / sum, wc / sum) : new(1 / 3.0, 1 / 3.0, 1 / 3.0);
    }

    /// <summary>Closeness to a known cliff edge at world (x, z), in [0, 1]: 1 on the edge, falling linearly to 0 at
    /// <see cref="TerrainTuning.CliffOuter"/> (with the same coastal noise as the cliff). Zero far from cliffs. The water
    /// surface uses it to place surf at the cliff foot, and scatter keeps props off cliff faces.</summary>
    public double CliffAt(double x, double z)
    {
        if (_wrap) x -= Period * Math.Floor(x / Period);
        var corners = Corners(x, z);
        return corners.HasCliff ? CliffShares(corners, x, z).Near : 0;
    }

    /// <summary>Cliff shares at a corner set's point: the sharp-blend share (full within <see cref="TerrainTuning.CliffInner"/>,
    /// gone at <see cref="TerrainTuning.CliffOuter"/>, both moved by coastal noise) and the linear closeness for surf. Both
    /// reach less than half a tile from the edge.</summary>
    private (double Cliff, double Near) CliffShares(in CornerSet corners, double x, double z)
    {
        var t = Tuning;
        double cliff = 0, near = 0, noise = double.NaN;
        foreach (var (land, water) in CliffPairs(corners))
        {
            double d = EdgeDistance(corners, land, water);
            if (d >= 0.48) continue;
            if (double.IsNaN(noise)) noise = 2 * _noise.Fbm(x, z, 2.6, 2, salt: 9) - 1;
            double dn = d + t.CliffNoise * noise;
            cliff = Math.Max(cliff, 1 - SmoothStep(t.CliffInner, t.CliffOuter, dn));
            near = Math.Max(near, Math.Clamp(1 - dn / t.CliffOuter, 0, 1));
        }
        return (cliff, near);
    }

    /// <summary>Plateau share at a corner set's point, in [0, 1]: 1 anywhere on a land tile with a cliff edge, easing down
    /// to 0 over <see cref="TerrainTuning.CliffRamp"/> beyond it (measured in warped space to the tile's hexagon), so the
    /// land climbs to the plateau across the tiles before the coast.</summary>
    private double PlateauLift(in CornerSet corners)
    {
        var lambda = corners.Triangle.Lambda;
        int home = lambda[0] >= lambda[1] ? (lambda[0] >= lambda[2] ? 0 : 2) : (lambda[1] >= lambda[2] ? 1 : 2);
        if (_plateaus[corners.Tile(home)] is not { } near) return 0;
        var hex = corners.Triangle[home];
        double gap = double.MaxValue;
        foreach (var offset in near)
        {
            var (cx, cz) = (hex + offset).ToPixel(1);
            double dx = corners.X - cx, dz = corners.Z - cz, slab = 0;
            // Beyond the hexagon's corners plus the ramp (and any nearer plateau), this one cannot matter.
            double reach = Math.Min(gap, Tuning.CliffRamp) + 1;
            if (dx * dx + dz * dz >= reach * reach) continue;
            // A hexagon is three slabs of half width √3/2; a soft maximum keeps the ramp free of creases off its corners.
            foreach (var (nx, nz) in EdgeNormals) slab = SoftMax(slab, Math.Abs(dx * nx + dz * nz), 0.15);
            gap = Math.Min(gap, slab - Math.Sqrt(3) / 2);
        }
        return gap >= Tuning.CliffRamp ? 0 : 1 - SmoothStep(0, Tuning.CliffRamp, gap);
    }

    /// <summary>For each tile, the offsets to the plateau tiles within <see cref="PlateauReach"/>: tiles whose plateau ramp
    /// can reach a point of it.</summary>
    private Hex[]?[] BuildPlateaus()
    {
        var s = Snapshot;
        var near = new List<Hex>?[s.Relief.Length];
        for (int tile = 0; tile < s.Relief.Length; tile++)
        {
            if (s.Relief[tile] == ReliefClass.Hidden || IsWater(s.Relief[tile]) || !HasCliffEdge(tile)) continue;
            var hex = s.Grid.HexOf(new TileId(tile));
            foreach (var other in Hex.Range(hex, PlateauReach))
                if (s.TryTile(other, out int t)) (near[t] ??= new()).Add(hex - other);
        }
        return near.Select(list => list?.ToArray()).ToArray();
    }

    private bool HasCliffEdge(int tile)
    {
        var s = Snapshot;
        var hex = s.Grid.HexOf(new TileId(tile));
        for (int d = 0; d < 6; d++)
            if (s.TryTile(hex + Hex.Directions[d], out int other) && IsCliff(hex, tile, hex + Hex.Directions[d], other)) return true;
        return false;
    }

    /// <summary>Distance in warped space from the corner set's point to the hex edge between corners a and b.</summary>
    private static double EdgeDistance(in CornerSet corners, int a, int b)
    {
        var (ax, az) = corners.Triangle[a].ToPixel(1);
        var (bx, bz) = corners.Triangle[b].ToPixel(1);
        double nx = bx - ax, nz = bz - az, len = Math.Sqrt(nx * nx + nz * nz);
        nx /= len; nz /= len;
        double mx = 0.5 * (ax + bx), mz = 0.5 * (az + bz);
        double along = Math.Clamp((corners.X - mx) * -nz + (corners.Z - mz) * nx, -0.5, 0.5);
        double ex = mx - nz * along, ez = mz + nx * along;
        return Math.Sqrt((corners.X - ex) * (corners.X - ex) + (corners.Z - ez) * (corners.Z - ez));
    }

    /// <summary>The corner pairs (land index, water index) of a corner set whose shared edge is a known cliff.</summary>
    private IEnumerable<(int Land, int Water)> CliffPairs(CornerSet corners)
    {
        for (int k = 0; k < 3; k++)
        {
            int a = k, b = (k + 1) % 3;
            if (!IsCliff(corners.Triangle[a], corners.Tile(a), corners.Triangle[b], corners.Tile(b))) continue;
            yield return IsWater(corners.Relief(a)) ? (b, a) : (a, b);
        }
    }

    /// <summary>A cliff stands on the edge between two known, on-map tiles when either remembers the bit and exactly one of
    /// them is water.</summary>
    private bool IsCliff(Hex a, int ta, Hex b, int tb)
    {
        var s = Snapshot;
        if (s.Cliffs[ta] == 0 && s.Cliffs[tb] == 0) return false;
        var ra = s.Relief[ta]; var rb = s.Relief[tb];
        if (ra == ReliefClass.Hidden || rb == ReliefClass.Hidden || IsWater(ra) == IsWater(rb)) return false;
        if (!s.TryTile(a, out _) || !s.TryTile(b, out _)) return false;
        int d = DirectionOf(a, b);
        return d >= 0 && (((s.Cliffs[ta] >> d) & 1) != 0 || ((s.Cliffs[tb] >> ((d + 3) % 6)) & 1) != 0);
    }

    private static int DirectionOf(Hex a, Hex b)
    {
        var step = new Hex(b.Q - a.Q, b.R - a.R);
        for (int d = 0; d < 6; d++) if (Hex.Directions[d] == step) return d;
        return -1;
    }

    private static bool IsWater(ReliefClass relief) => relief is ReliefClass.DeepWater or ReliefClass.ShallowWater;

    /// <summary>A lone rock off a cliff's foot: its warped-space centre and shoal radius, the water tile it stands in, and
    /// its model choice, turn and scale hashes.</summary>
    private readonly record struct StackSpot(double X, double Z, double Radius, int WaterTile, double Model, double Yaw, double Scale);

    /// <summary>Sea stacks of the cliff edge between corners land and water, in warped space. Each lies 0.17 to 0.29 out from
    /// the edge with a shoal radius up to 0.13, so it stays inside the reach of the edge's own corner triangles.</summary>
    private IEnumerable<StackSpot> StacksOf(CornerSet corners, int land, int water)
    {
        int tile = corners.Tile(land), direction = DirectionOf(corners.Triangle[land], corners.Triangle[water]);
        var (ax, az) = corners.Triangle[land].ToPixel(1);
        var (bx, bz) = corners.Triangle[water].ToPixel(1);
        return StacksOfEdge(tile, direction, ax, az, bx, bz, corners.Tile(water));
    }

    private IEnumerable<StackSpot> StacksOfEdge(int landTile, int direction, double ax, double az, double bx, double bz, int waterTile)
    {
        uint salt = 50 + 10 * (uint)direction;
        double roll = TileHash(landTile, salt);
        int count = roll < Tuning.StackChance * 0.2 ? 2 : roll < Tuning.StackChance ? 1 : 0;
        double nx = bx - ax, nz = bz - az, len = Math.Sqrt(nx * nx + nz * nz);
        nx /= len; nz /= len;
        double mx = 0.5 * (ax + bx), mz = 0.5 * (az + bz);
        for (int i = 0; i < count; i++)
        {
            uint s = salt + 1 + 4 * (uint)i;
            double along = (TileHash(landTile, s) - 0.5) * 0.7, outward = 0.17 + 0.12 * TileHash(landTile, s + 1);
            double radius = 0.09 + 0.04 * TileHash(landTile, s + 2);
            yield return new(mx - nz * along + nx * outward, mz + nx * along + nz * outward, radius, waterTile,
                TileHash(landTile, s + 3), TileHash(landTile, s + 4), TileHash(landTile, s + 5));
        }
    }

    /// <summary>A placed sea stack: world position of its foot on the shoal, the water tile it stands in (for sight), and
    /// model, turn and scale hashes in [0, 1).</summary>
    public readonly record struct SeaStack(double X, double Y, double Z, int Tile, double Model, double Yaw, double Scale);

    private readonly Lazy<IReadOnlyList<SeaStack>> _stacks;

    /// <summary>Every sea stack of the known cliffs, in canonical world coordinates.</summary>
    public IReadOnlyList<SeaStack> SeaStacks => _stacks.Value;

    /// <summary>One rock column of a cliff wall: world position of its foot, the land tile it belongs to (for sight), its
    /// model hash in [0, 1), turn in radians, uniform scale and extra vertical stretch.</summary>
    public readonly record struct CliffRock(double X, double Y, double Z, int Tile, double Model, double Yaw, double Scale, double Stretch);

    /// <summary>Rock columns per cliff edge. The wall reads as a rampart of separate pillars, not a smooth face.</summary>
    public const int CliffRocksPerEdge = 13;

    /// <summary>The rock wall of every known cliff edge whose land tile centre lies within 1.5 of the rectangle (x0..x1,
    /// z0..z1), canonical x. Each column stands on the seabed at the cliff line and reaches a little above the bluff top,
    /// found by sampling the height field across the edge, so it follows the wandering cliff line.</summary>
    public IEnumerable<CliffRock> CliffRocks(double x0, double x1, double z0, double z1)
    {
        var s = Snapshot;
        for (int tile = 0; tile < s.Cliffs.Length; tile++)
        {
            if (s.Cliffs[tile] == 0 || s.Relief[tile] == ReliefClass.Hidden || IsWater(s.Relief[tile])) continue;
            var hex = s.Grid.HexOf(new TileId(tile));
            var (ax, az) = hex.ToPixel(1);
            if (az < z0 - 1.5 || az > z1 + 1.5 || WrappedGap(ax, x0, x1) > 1.5) continue;
            for (int d = 0; d < 6; d++)
            {
                var other = hex + Hex.Directions[d];
                if (!s.TryTile(other, out int water) || !IsCliff(hex, tile, other, water)) continue;
                var (bx, bz) = other.ToPixel(1);
                foreach (var rock in EdgeRocks(tile, d, ax, az, bx, bz)) yield return rock;
            }
        }
    }

    private double WrappedGap(double x, double x0, double x1)
    {
        double gap = x < x0 ? x0 - x : x > x1 ? x - x1 : 0;
        if (_wrap && gap > 0) gap = Math.Min(gap, Math.Min(Math.Abs(x + Period - x0), Math.Abs(x - Period - x1)));
        return gap;
    }

    private IEnumerable<CliffRock> EdgeRocks(int tile, int direction, double ax, double az, double bx, double bz)
    {
        double nx = bx - ax, nz = bz - az, len = Math.Sqrt(nx * nx + nz * nz);
        nx /= len; nz /= len;
        double mx = 0.5 * (ax + bx), mz = 0.5 * (az + bz);
        uint salt = 200 + 64 * (uint)direction;
        for (int i = 0; i < CliffRocksPerEdge; i++)
        {
            uint r = salt + 5 * (uint)i;
            double along = -0.5 + (i + 0.5 + 0.6 * (TileHash(tile, r) - 0.5)) / CliffRocksPerEdge;
            // The spot on the edge lives in warped space; find the world point that warps onto it.
            double qx = mx - nz * along, qz = mz + nx * along, px = qx, pz = qz;
            for (int k = 0; k < 6; k++)
            {
                var (ox, oz) = Warp.Sample(px, pz);
                px = qx - ox * Tuning.WarpAmplitude;
                pz = qz - oz * Tuning.WarpAmplitude;
            }
            // March out to sea across the edge: the last ground above the water is the cliff top, the first below it the foot.
            double top = double.MinValue, foot = 0, line = double.NaN;
            for (double o = -0.35; o <= 0.4; o += 0.015)
            {
                double h = HeightAt(px + nx * o, pz + nz * o);
                if (h >= 0) { top = Math.Max(top, h); continue; }
                line = o;
                foot = HeightAt(px + nx * (o + 0.05), pz + nz * (o + 0.05));
                break;
            }
            // Columns only where the bluff really stands (its ends fade where a beach takes over).
            if (double.IsNaN(line) || top < 0.05) continue;
            double cx = px + nx * (line - 0.015), cz = pz + nz * (line - 0.015);
            if (_wrap) cx -= Period * Math.Floor(cx / Period);
            // Chunky columns about flush with the bluff top: a few stand a little proud, a few a little short.
            double stretch = 1.1 + 0.6 * TileHash(tile, r + 1);
            double height = top - 0.01 + 0.035 * TileHash(tile, r + 2) - foot;
            double scale = Math.Clamp(height / (0.16 * stretch), 0.4, 2.8);
            yield return new(cx, foot, cz, tile, TileHash(tile, r + 3), Math.Tau * TileHash(tile, r + 4), scale, stretch);
        }
    }

    private IReadOnlyList<SeaStack> BuildStacks()
    {
        var s = Snapshot;
        var result = new List<SeaStack>();
        for (int tile = 0; tile < s.Cliffs.Length; tile++)
        {
            if (s.Cliffs[tile] == 0 || s.Relief[tile] == ReliefClass.Hidden || IsWater(s.Relief[tile])) continue;
            var hex = s.Grid.HexOf(new TileId(tile));
            for (int d = 0; d < 6; d++)
            {
                var other = hex + Hex.Directions[d];
                if (!s.TryTile(other, out int water) || !IsCliff(hex, tile, other, water)) continue;
                var (ax, az) = hex.ToPixel(1);
                var (bx, bz) = other.ToPixel(1);
                foreach (var spot in StacksOfEdge(tile, d, ax, az, bx, bz, water))
                {
                    // Spots live in warped space; find the world point that warps onto it (the warp is smooth and small).
                    double px = spot.X, pz = spot.Z;
                    for (int i = 0; i < 6; i++)
                    {
                        var (ox, oz) = Warp.Sample(px, pz);
                        px = spot.X - ox * Tuning.WarpAmplitude;
                        pz = spot.Z - oz * Tuning.WarpAmplitude;
                    }
                    if (_wrap) px -= Period * Math.Floor(px / Period);
                    result.Add(new(px, HeightAt(px, pz), pz, water, spot.Model, spot.Yaw, spot.Scale));
                }
            }
        }
        return result;
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
        ReliefClass A, ReliefClass B, ReliefClass C, bool HasCliff = false)
    {
        public bool HasPeak => A == ReliefClass.Peak || B == ReliefClass.Peak || C == ReliefClass.Peak;
        public int Tile(int k) => k switch { 0 => TileA, 1 => TileB, _ => TileC };
        public ReliefClass Relief(int k) => k switch { 0 => A, 1 => B, _ => C };
    }

    /// <summary>Peak noise shared by every peak corner at one point: crag ridges in [0, 1], spur distortion in [-1, 1],
    /// gully wander in [-1, 1] and fine rock relief in [0, 1].</summary>
    private readonly record struct PeakDetail(double Crag, double Spur, double Wander, double Rough);

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
        // Cheap pre-check; CliffPairs decides which edges really are cliffs.
        bool cliff = (s.Cliffs[ta] | s.Cliffs[tb] | s.Cliffs[tc]) != 0;
        return new(tri, wx, wz, ta, tb, tc, ra, rb, rc, cliff);
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
        return new(_noise.Ridged(x, z, t.PeakFrequency, t.Octaves, salt: 4), _noise.Fbm(x, z, 0.9, 2, salt: 5),
            _noise.Fbm(x, z, 1.7, 2, salt: 7), _noise.Ridged(x, z, t.PeakRoughnessFrequency, 2, salt: 8));
    }

    /// <summary>One peak tile's own mountain: a jittered main summit near its centre and two or three lesser spires around
    /// it, mostly along its ridge, all on a low massif at the <see cref="TerrainTuning.PeakBase"/> saddle level. Each summit
    /// is a pyramid with straight arêtes and gullied faces (<see cref="Summit"/>). Crags are strongest high up, and fine
    /// rock relief roughens the upper slopes. Footprint, faces, spires, gullies and height all vary per tile, so no two
    /// mountains look alike. The tile's weight fades this to zero wherever the tile stops shaping the height, so a
    /// neighbouring peak forms a connected ridge with a saddle between two distinct summits.</summary>
    private double PeakProfile(in CornerSet corners, int k, PeakDetail detail)
    {
        var t = Tuning;
        var m = MountainOf(corners.Tile(k));
        var (cx, cz) = corners.Triangle[k].ToPixel(1);
        double dx = corners.X - cx - m.OffsetX, dz = corners.Z - cz - m.OffsetZ;
        double u = (dx * m.AxisCos + dz * m.AxisSin) * m.ScaleAlong, v = (dz * m.AxisCos - dx * m.AxisSin) * m.ScaleAcross;
        double spur = 1 + t.PeakSpur * detail.Spur;
        double shape = Summit(m.Main, u, v, spur, detail.Wander);
        foreach (var spire in m.Spires)
        {
            double su = (u - spire.U) / spire.Size, sv = (v - spire.V) / spire.Size;
            shape = SoftMax(shape, spire.Height * Summit(spire.Faces, su, sv, spur, detail.Wander), 0.05);
        }
        // A broad, low massif under the summits fills the tile, so their feet reach the saddles.
        shape = SoftMax(shape, t.PeakMassif * Cone(Math.Sqrt(u * u + v * v) * spur / 1.4, 1.2), 0.08);
        // Ridges to neighbouring peaks, in absolute rise units so both tiles of a pair build the same crest.
        double rise = m.Summit * shape;
        foreach (var ridge in m.Ridges)
            rise = SoftMax(rise, RidgeShape(ridge, corners.X - cx, corners.Z - cz, spur, detail.Wander), 0.05);
        shape = rise / m.Summit;
        return t.PeakBase + t.PeakRise * rise + t.PeakNoise * detail.Crag * (0.6 + 0.4 * shape)
            + t.PeakRoughness * detail.Rough * SmoothStep(0.08, 0.45, shape);
    }

    /// <summary>One pyramid's faces: unit normals of the face directions (unequal widths), the half angle from each face
    /// centre to its two arêtes, and how many gullies each face carries.</summary>
    private sealed record Faces(double[] Cos, double[] Sin, double[] Angle, double[] HalfEast, double[] HalfWest, int[] Gullies);

    private sealed record Spire(double U, double V, double Size, double Height, Faces Faces);

    /// <summary>A ridge from this tile's summit (A, relative to the tile centre) to a neighbouring peak's summit (B), with
    /// their summit heights, the saddle height as a fraction of them and the number of spurs on each flank.</summary>
    private sealed record Ridge(double AX, double AZ, double BX, double BZ, double HeightA, double HeightB, double Saddle,
        int Spurs, double Phase);

    /// <summary>Everything about one peak tile's mountain that does not depend on the sample point.</summary>
    private sealed record Mountain(double OffsetX, double OffsetZ, double AxisCos, double AxisSin, double ScaleAlong,
        double ScaleAcross, double Summit, Faces Main, Spire[] Spires, Ridge[] Ridges);

    private Mountain MountainOf(int tile) => _mountains[tile] ??= BuildMountain(tile);

    private Mountain BuildMountain(int tile)
    {
        var t = Tuning;
        var hex = Snapshot.Grid.HexOf(new TileId(tile));
        var (cx, cz) = hex.ToPixel(1);
        var (ox, oz) = SummitOffset(tile);
        var neighbours = PeakNeighbours(hex);
        // Ridge axis: along the line through neighbouring peaks, so a chain of mountains reads as one range; a lone peak
        // takes a slow field at the canonical tile centre, so all wrap copies of a tile agree.
        double axis = neighbours.Count > 0
            ? 0.5 * Math.Atan2(neighbours.Sum(n => Math.Sin(2 * n.Angle)), neighbours.Sum(n => Math.Cos(2 * n.Angle)))
                + 0.3 * (TileHash(tile, 4) - 0.5)
            : Math.PI * _noise.Value(cx - Period * Math.Floor(cx / Period), cz, 0.2, salt: 6) + 0.6 * (TileHash(tile, 4) - 0.5);
        // Normalised footprint coordinates: the main summit's rise reaches zero at radius 1.
        double radius = t.PeakRadius * (0.85 + 0.3 * TileHash(tile, 5));
        double stretch = t.PeakStretch * (0.75 + 0.5 * TileHash(tile, 6));
        // Lesser spires: the first two sit along the ridge on either side, up to two more anywhere around.
        var spires = new Spire[2 + (int)(TileHash(tile, 7) * 3)];
        for (int i = 0; i < spires.Length; i++)
        {
            uint salt = 30 + 10 * (uint)i;
            double direction = (i == 0 ? 0 : i == 1 ? Math.PI : Math.Tau * TileHash(tile, salt)) + 0.8 * (TileHash(tile, salt + 1) - 0.5);
            double reach = 0.42 + 0.3 * TileHash(tile, salt + 2);
            spires[i] = new(reach * Math.Cos(direction), reach * Math.Sin(direction), 0.42 + 0.25 * TileHash(tile, salt + 3),
                t.PeakSpires * (0.45 + 0.55 * TileHash(tile, salt + 4)), FacesOf(tile, salt + 5));
        }
        double height = SummitHeight(tile);
        var ridges = neighbours.Select(n =>
        {
            var (nx, nz) = SummitOffset(n.Tile);
            int lo = Math.Min(tile, n.Tile), hi = Math.Max(tile, n.Tile);
            double pair = TileHash(lo * 7919 + hi, 40);
            return new Ridge(ox, oz, n.X + nx, n.Z + nz, height, SummitHeight(n.Tile), t.PeakSaddle * (0.75 + 0.5 * pair),
                3 + (int)(TileHash(lo * 7919 + hi, 41) * 3), TileHash(lo * 7919 + hi, 42));
        }).ToArray();
        return new(ox, oz, Math.Cos(axis), Math.Sin(axis), 1 / (stretch * radius), 1 / radius, height, FacesOf(tile, 20),
            spires, ridges);
    }

    private (double X, double Z) SummitOffset(int tile)
    {
        double angle = TileHash(tile, 1) * Math.Tau, offset = Tuning.PeakJitter * TileHash(tile, 2);
        return (offset * Math.Cos(angle), offset * Math.Sin(angle));
    }

    private double SummitHeight(int tile) => 1 - Tuning.PeakVariation * TileHash(tile, 3);

    /// <summary>Peak tiles next to a hex: their tile, centre relative to the hex centre and direction.</summary>
    private List<(int Tile, double X, double Z, double Angle)> PeakNeighbours(Hex hex)
    {
        var result = new List<(int, double, double, double)>();
        var s = Snapshot;
        foreach (var direction in Hex.Directions)
        {
            var (col, row) = HexGrid.ToOffset(hex + direction);
            if (row < 0 || row >= s.Height || (!s.Grid.Spec.Wrap && (col < 0 || col >= s.Width))) continue;
            int tile = s.ClampedTile(hex + direction);
            if (s.Relief[tile] != ReliefClass.Peak) continue;
            var (x, z) = direction.ToPixel(1);
            result.Add((tile, x, z, Math.Atan2(z, x)));
        }
        return result;
    }

    /// <summary>A ridge's height in rise units at (x, z) relative to the tile centre: a sharp crest that dips from just
    /// under each summit to a saddle midway, with V-shaped spurs and gullies down both flanks. A pure function of the
    /// pair, so the two tiles that share it agree.</summary>
    private double RidgeShape(Ridge ridge, double x, double z, double spur, double wander)
    {
        var t = Tuning;
        double vx = ridge.BX - ridge.AX, vz = ridge.BZ - ridge.AZ, length = Math.Sqrt(vx * vx + vz * vz);
        vx /= length; vz /= length;
        double along = (x - ridge.AX) * vx + (z - ridge.AZ) * vz, f = Math.Clamp(along / length, 0, 1);
        double across = (z - ridge.AZ) * vx - (x - ridge.AX) * vz;
        double fromEnd = along < 0 ? -along : along > length ? along - length : 0;
        double mid = Math.Abs(2 * f - 1);
        // The crest wanders sideways a little, most at the saddle.
        across += 0.12 * wander * (1 - mid * mid);
        double width = t.PeakRidgeWidth * (0.7 + 0.3 * mid);
        double distance = Math.Sqrt(across * across + fromEnd * fromEnd) * spur / width;
        double crest = (ridge.HeightA + (ridge.HeightB - ridge.HeightA) * f)
            * (ridge.Saddle + (t.PeakRidgeTop - ridge.Saddle) * Math.Pow(mid, 1.6));
        double height = crest * Cone(distance, t.PeakCurve);
        if (height <= 0) return 0;
        double wave = TriangleWave(Math.PI * (ridge.Spurs * f + ridge.Phase) + t.PeakMeander * Math.PI * wander);
        double ramp = SmoothStep(0.1, 0.35, distance) * (1 - SmoothStep(0.75, 1, distance));
        return height * (1 - t.PeakErosion * wave * ramp);
    }

    /// <summary>Three to five faces of unequal width at a random turn, with one to <see cref="TerrainTuning.PeakGullies"/>
    /// gullies each.</summary>
    private Faces FacesOf(int tile, uint salt)
    {
        int n = 3 + (int)(TileHash(tile, salt) * 3);
        double theta0 = Math.Tau * TileHash(tile, salt + 1);
        var angles = new double[n];
        for (int i = 0; i < n; i++) angles[i] = theta0 + Math.Tau * (i + 0.3 * (TileHash(tile, salt + 2 + (uint)i) - 0.5)) / n;
        var east = new double[n];
        var west = new double[n];
        for (int i = 0; i < n; i++)
        {
            east[i] = 0.5 * ((i + 1 < n ? angles[i + 1] : angles[0] + Math.Tau) - angles[i]);
            west[i] = 0.5 * (angles[i] - (i > 0 ? angles[i - 1] : angles[n - 1] - Math.Tau));
        }
        var gullies = Enumerable.Range(0, n).Select(i => 1 + (int)(TileHash(tile, salt + 7 + (uint)i) * Tuning.PeakGullies)).ToArray();
        return new(angles.Select(Math.Cos).ToArray(), angles.Select(Math.Sin).ToArray(), angles, east, west, gullies);
    }

    /// <summary>One pyramidal summit in [0, 1] at normalised coordinates (u, v): 1 at its apex, 0 from distance 1 outwards.
    /// The distance blends from round to polygonal (<see cref="TerrainTuning.PeakFacet"/>), so straight arêtes run down
    /// between the faces. Each face's V-shaped gullies are zero at its arêtes, its apex and its foot.</summary>
    private double Summit(Faces faces, double u, double v, double spur, double wander)
    {
        var t = Tuning;
        double r = Math.Sqrt(u * u + v * v);
        if (0.6 * r * spur >= 1) return 0;
        int n = faces.Cos.Length, best = 0;
        double reach = double.MinValue;
        for (int i = 0; i < n; i++)
        {
            double d = u * faces.Cos[i] + v * faces.Sin[i];
            if (d > reach) { reach = d; best = i; }
        }
        double polygon = reach / (0.5 * (1 + Math.Cos(Math.PI / n)));
        // The floor keeps a narrow face's corner from reaching far beyond the footprint.
        double distance = Math.Max(r + t.PeakFacet * (polygon - r), 0.6 * r) * spur;
        double height = Cone(distance, t.PeakCurve);
        if (height <= 0 || r < 1e-9) return height;
        // Lateral position across the face: -1 and 1 on its two arêtes (the bisectors with the neighbouring faces).
        double phi = Math.IEEERemainder(Math.Atan2(v, u) - faces.Angle[best], Math.Tau);
        double s = Math.Clamp(phi / (phi >= 0 ? faces.HalfEast[best] : faces.HalfWest[best]), -1, 1);
        double wave = TriangleWave(0.5 * Math.PI * faces.Gullies[best] * (s + 1) + t.PeakMeander * Math.PI * wander);
        double envelope = 1 - SmoothStep(0.7, 1, Math.Abs(s));
        double ramp = SmoothStep(0.04, 0.3, distance) * (1 - SmoothStep(0.7, 1, distance));
        return height * (1 - t.PeakErosion * wave * envelope * ramp);
    }

    /// <summary>Smooth maximum: max(a, b) with a rounded crease of width k.</summary>
    private static double SoftMax(double a, double b, double k)
    {
        double h = Math.Clamp(0.5 + 0.5 * (a - b) / k, 0, 1);
        return b + (a - b) * h + k * h * (1 - h);
    }

    /// <summary>Convex summit falloff in [0, 1] over a normalised distance: 1 at the top, 0 from distance 1 outwards.</summary>
    private static double Cone(double distance, double curve) => Math.Pow(Math.Max(0, 1 - distance), curve);

    /// <summary>Triangle wave with period π in [0, 1]: 0 at multiples of π, 1 halfway between.</summary>
    private static double TriangleWave(double x) => Math.Asin(Math.Abs(Math.Sin(x))) / (0.5 * Math.PI);

    private static double SmoothStep(double edge0, double edge1, double x)
    {
        double s = Math.Clamp((x - edge0) / (edge1 - edge0), 0, 1);
        return s * s * (3 - 2 * s);
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
