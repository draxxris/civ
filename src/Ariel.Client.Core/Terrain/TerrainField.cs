using System.Numerics;

namespace Ariel.Client.Terrain;

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
    /// <summary>Peaks: amplitude of the fine rock relief (cliffs, buttresses) on the upper slopes. It needs a fine mesh;
    /// see <see cref="TerrainMesher.PeakResolution"/>.</summary>
    public double PeakRoughness { get; init; } = 0.09;
    /// <summary>Peaks: frequency of the fine rock relief.</summary>
    public double PeakRoughnessFrequency { get; init; } = 4.5;
    /// <summary>Volcanoes (peaks with a crater): crater radius at the rim, in the summit's normalised footprint units.</summary>
    public double CraterRadius { get; init; } = 0.17;
    /// <summary>Volcanoes: depth of the crater floor below its rim, in summit rise units.</summary>
    public double CraterDepth { get; init; } = 0.16;
    /// <summary>Volcanoes: exponent of the cone falloff. Below <see cref="PeakCurve"/>, so the flanks are straighter.</summary>
    public double VolcanoCurve { get; init; } = 1.25;

    /// <summary>Largest river carve depth, under the middle of a full-width river.</summary>
    public double RiverDepth { get; init; } = 0.075;
    /// <summary>Water half width of a lowland river; sources, hills, peaks and mouths scale it (see <see cref="RiverNetwork"/>).</summary>
    public double RiverWidth { get; init; } = 0.16;
    /// <summary>Half width of the carved channel and its banks, as a multiple of the local water half width.</summary>
    public double RiverBankRatio { get; init; } = 2.2;
    /// <summary>Distance beyond a river's banks, in world units, over which the terrain is drawn down to the river's valley floor
    /// (see <see cref="RiverNetwork.Level"/>).</summary>
    public double RiverDrawdown { get; init; } = 1.1;

    /// <summary>Cliffs: lift of the raised side of a cliff, over the whole tile, so a bluff stands above the ground below
    /// it. Hills and peaks on such a tile stand this much higher too.</summary>
    public double CliffRise { get; init; } = 0.22;
    /// <summary>Cliffs: distance (world units, about one tile across) beyond a cliff tile over which the neighbouring
    /// ground climbs to the lifted side, so the rise starts before the cliff tile instead of at the edge.</summary>
    public double CliffRamp { get; init; } = Lattice.CliffRamp;
    /// <summary>Cliffs: blend sharpness across a cliff edge. A bluff is a wall, so the blend is sharp enough that the whole
    /// drop falls inside one lattice step of a cliff chunk (resolved at <see cref="TerrainMesher.PeakResolution"/>, 0.064
    /// world units): the drawn face is then one near-vertical quad, and the rock wall stands against it.</summary>
    public double CliffSharpness { get; init; } = 45;
    /// <summary>Cliffs: distance from the edge (world units) over which the cliff shape is full.</summary>
    public double CliffInner { get; init; } = 0.16;
    /// <summary>Cliffs: distance at which the cliff shape has faded out. With <see cref="CliffNoise"/> it stays within half
    /// a tile of the edge, so only the corner triangles that share the edge feel it (the field stays continuous).</summary>
    public double CliffOuter { get; init; } = 0.42;
    /// <summary>Cliffs: how high the last edge of a cliff run stands, as a share of <see cref="CliffRise"/>. A bluff that
    /// simply stops leaves the ground beside its end with the whole rise to absorb in a tile or two; ending it lower means
    /// the grass beyond has less to climb down.</summary>
    public double CliffEndScale { get; init; } = 0.64;
    /// <summary>Cliffs: tiles over which a cliff climbs from <see cref="CliffEndScale"/> at the end of its run to full
    /// height. A short run climbs over fewer tiles, so it can still reach full height in the middle.</summary>
    public double CliffEndTaper { get; init; } = 2.5;
    /// <summary>Cliffs: distance over which the ground beyond the end of a cliff run falls from its lowered height to
    /// ground on the same level. Longer than <see cref="CliffRamp"/>, because the end has no wall to hide the descent.</summary>
    public double CliffEndRamp { get; init; } = 2.4;
    /// <summary>Cliffs: how far noise moves the cliff's reach along the edge, so a bluff does not end in a straight line.</summary>
    public double CliffNoise { get; init; } = 0.06;
    /// <summary>Cliffs: how far edge noise moves the cliff line from the hex edge, either way. It stays inside
    /// <see cref="CliffInner"/>, where the cliff shape is full, and the rock wall moves with it. Small, so a bluff is still
    /// a straight side of the map: enough to break the machining of a face grown from one edge.</summary>
    public double CliffWander { get; init; } = 0.04;
    /// <summary>Cliffs: radius of the arc that rounds a corner where two faces of one tile meet, in world units. The
    /// arc leaves each face R/√3 from the corner and cuts the corner by about 0.18 R.</summary>
    public double CliffFillet { get; init; } = 0.6;
    /// <summary>Sea stacks: chance that a coastal cliff edge carries a second lone rock off its foot. Every cliff whose
    /// lower side is water always carries at least one, so no stretch of coast is left bare.</summary>
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
    private readonly bool _hasCrater;
    /// <summary>Whether any known tile carries a cliff bit. Without one the ground shader input is always zero.</summary>
    private readonly bool _hasCliffs;
    /// <summary>Per tile whether it is the raised side of any cliff. The edge blend biases by this, and only a lifted land
    /// tile carries the ramp.</summary>
    private readonly bool[] _lifted;
    /// <summary>Hex distance from a sample's tile to the farthest cliff whose ramp can reach it.</summary>
    private const int CliffReach = 3;
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
        _hasCrater = Array.IndexOf(snapshot.Crater, true) >= 0;
        _hasCliffs = Array.IndexOf(snapshot.Cliffs, (byte)0) < snapshot.Cliffs.Length;
        _lifted = BuildLifted();
        _liftScale = BuildLiftScale();
        _bareEdges = BuildBareEdges();
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
        double h = 0;
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
            if (k == 0) pa = p; else if (k == 1) pb = p; else pc = p;
            if (w[k] <= 0) continue;
            if (relief == ReliefClass.Hidden) hidden += w[k];
            else h += w[k] * p;
        }
        // The lift comes in two forms. Away from a cliff edge it is the ramp from nearby bare edges: the ground climbs
        // to the bluff over the tiles beside it, so a bluff with no cliff along a given edge still meets the ground
        // instead of ending in a wall. Along a cliff edge it is the sharp blend, which puts the whole rise on the tile
        // itself and leaves the foot flat, so the edge is a step. The ramp list holds bare edges only, so it cannot cross
        // a cliff edge and raise the tiles behind a wall.
        // A ramp reaches well past the tiles that carry cliff bits, so it applies wherever a bare edge is in reach, not
        // only in corner sets that touch a cliff; stopping it there leaves a bank where the slope is cut off.
        if (!corners.HasCliff)
            return hidden >= 1 ? h : h + Tuning.CliffRise * LiftRamp(corners);
        var (cliff, near) = CliffShares(corners, x, z);
        var sharp = CliffWeights(corners, x, z);
        // The lift belongs to the raised ground: a step at a cliff edge, level ground at its foot, and a ramp where an
        // edge carries no cliff. Walls stand only on edges that actually step, so the step is always the face of a rock wall.
        // The sharp weights decide only whether a point is on raised ground; how high that ground stands is blended
        // linearly between the lifted tiles, so two raised tiles of a run with different heights meet in a slope across
        // the top rather than a step.
        double raised = 0, scaleSum = 0, lambdaSum = 0;
        var lambda = corners.Triangle.Lambda;
        for (int k = 0; k < 3; k++)
        {
            var relief = corners.Relief(k);
            if (relief == ReliefClass.Hidden || IsWater(relief) || !_lifted[corners.Tile(k)]) continue;
            raised += sharp[k];
            double l = Math.Max(lambda[k], 0);
            scaleSum += l * _liftScale[corners.Tile(k)];
            lambdaSum += l;
        }
        double bluff = lambdaSum > 0 ? raised * scaleSum / lambdaSum : 0;
        // The ramp carries the bluff down to the ground over the tiles beside a bare edge, so a side with no cliff on it
        // is a slope rather than a wall. It needs no window at a cliff: each slope is one edge wide and fades out at the
        // corner where its edge meets a face, so it never reaches the ground at the foot of the wall.
        double lift = Math.Max(bluff, LiftRamp(corners));
        h += Tuning.CliffRise * lift;
        return CliffGround(corners, h, new Blend3(pa, pb, pc), sharp, lift, cliff, near, x, z);
    }

    private double CliffGround(in CornerSet corners, double h, Blend3 profiles, Blend3 sharp, double lift, double cliff,
        double near, double x, double z)
    {
        var t = Tuning;
        if (near > 0)
        {
            // The sharp version of the same ground: the cliff's own high/low blend of the profiles, with the sharp lift on
            // top, capped at the flat top a peak on a bluff may reach.
            double hc = 0, flat = t.FlatBase + t.FlatNoise * _noise.Fbm(x, z, t.FlatFrequency, t.Octaves, salt: 2);
            double peakShare = 0, peakLambda = 0;
            for (int k = 0; k < 3; k++)
            {
                var relief = corners.Relief(k);
                if (relief == ReliefClass.Hidden) continue;
                if (relief == ReliefClass.Peak)
                {
                    peakShare += sharp[k];
                    peakLambda += corners.Triangle.Lambda[k];
                    hc += sharp[k] * Math.Min(profiles[k], flat);
                }
                else hc += sharp[k] * profiles[k];
            }
            hc += t.CliffRise * lift;
            double blend = cliff + (near - cliff) * peakShare;
            double delta = (hc - h) * blend;
            if (delta > 0) delta *= 1 - SmoothStep(0.05, 0.3, peakLambda);
            h += delta;
        }
        foreach (var (high, low) in CliffPairs(corners))
            if (IsWater(corners.Relief(low)))
                foreach (var stack in StacksOf(corners, high, low))
                {
                double dx = corners.X - stack.X, dz = corners.Z - stack.Z, r2 = (dx * dx + dz * dz) / (stack.Radius * stack.Radius);
                if (r2 >= 1.5) continue;
                // A paraboloid shoal from just under the surface down past the seabed at its rim, merged softly.
                h = SoftMax(h, t.StackShoal - 0.2 * r2, 0.015);
            }
        return h;
    }

    /// <summary>Sharp high/low weights for cliffs, λ^s with the lifted corners biased by edge noise so the cliff line can
    /// wander by up to <see cref="TerrainTuning.CliffWander"/> instead of following the straight hex edge. At an edge
    /// midpoint ln λ_high − ln λ_low changes by 4/√3 per world unit, so a bias b on the high side and −b on the low side
    /// moves the line by b·√3/2. The weights stay continuous: λ = 0 still gives zero weight. Unbiased, the line falls
    /// where the high and low weights are equal, which is the hex edge itself.</summary>
    private Blend3 CliffWeights(in CornerSet corners, double x, double z)
    {
        var t = Tuning;
        var lambda = corners.Triangle.Lambda;
        double b = t.CliffWander * CliffLineNoise(x, z) * 2 / Math.Sqrt(3);
        var fillet = FilletAt(corners);
        double wa = 0, wb = 0, wc = 0, sum = 0;
        for (int k = 0; k < 3; k++)
        {
            var relief = corners.Relief(k);
            // The bias is a property of the tile alone, so the weights stay continuous across corner triangles. A lifted
            // tile leans the face towards itself; the ground below the cliff stays where it is.
            double bias = relief == ReliefClass.Hidden ? 0 : _lifted[corners.Tile(k)] ? b : -b;
            // The tile both faces of a corner belong to gives up a fillet of ground, so the corner is rounded. Moving the
            // line by d needs a bias of d times the rate ln λ_A − ln λ_other changes across it, (1/λ_A + 1/λ_other)/√3.
            if (k == fillet.Tile && fillet.Shift > 0)
                bias -= fillet.Shift * (1 / Math.Max(lambda[k], 0.05) + 1 / Math.Max(lambda[fillet.Other], 0.05)) / Math.Sqrt(3);
            double w = Math.Pow(Math.Clamp(lambda[k], 0, 1), t.CliffSharpness) * Math.Exp(t.CliffSharpness * bias);
            if (k == 0) wa = w; else if (k == 1) wb = w; else wc = w;
            sum += w;
        }
        return sum > 0 ? new(wa / sum, wb / sum, wc / sum) : new(1 / 3.0, 1 / 3.0, 1 / 3.0);
    }

    /// <summary>Edge noise, in [-1, 1], that moves a cliff line off its hex edge. Canonical coordinates: the same value at
    /// the same place whoever asks, so the rock wall can stand where the face does.</summary>
    private double CliffLineNoise(double x, double z) =>
        Math.Clamp(3.2 * (_noise.Fbm(x, z, 1.4, 3, salt: 10) - 0.5), -1, 1);

    /// <summary>How far the cliff line at a canonical point stands out from the hex edge, in world units: positive towards
    /// the ground below the cliff. <see cref="CliffWeights"/> moves the face by exactly this, so anything placed against
    /// the face can find it.</summary>
    private double CliffLineAt(double x, double z) => Tuning.CliffWander * CliffLineNoise(x, z);

    /// <summary>Where two cliff faces meet at the corner of a corner triangle and both belong to the same tile's side
    /// (that tile is the high side of both, or the low side of both), the corner is rounded with an arc of radius
    /// <see cref="TerrainTuning.CliffFillet"/> tangent to both faces. Tile is that tile's corner index (-1 for none), Other
    /// the index of the far tile of the face the point lies nearest, and Shift how far that face moves into the tile at the
    /// point, in world units: the gap between the straight edge and the arc.</summary>
    private readonly record struct Fillet(int Tile, int Other, double Shift, bool High);

    private Fillet FilletAt(in CornerSet corners)
    {
        double radius = Tuning.CliffFillet;
        if (radius <= 0) return new(-1, -1, 0, false);
        int count = 0, h1 = -1, l1 = -1, h2 = -1, l2 = -1;
        for (int k = 0; k < 3; k++)
        {
            int a = k, b = (k + 1) % 3;
            int side = CliffHigh(corners.Triangle[a], corners.Tile(a), corners.Triangle[b], corners.Tile(b));
            if (side < 0) continue;
            int high = side == 0 ? a : b, low = side == 0 ? b : a;
            if (count == 0) { h1 = high; l1 = low; } else { h2 = high; l2 = low; }
            count++;
        }
        if (count != 2) return new(-1, -1, 0, false);
        int tile, o1, o2;
        bool convex;
        if (h1 == h2) { tile = h1; o1 = l1; o2 = l2; convex = true; }
        else if (l1 == l2) { tile = l1; o1 = h1; o2 = h2; convex = false; }
        else return new(-1, -1, 0, false);
        // The corner is the triangle's centroid; each face runs from it to the midpoint of its edge.
        var (ax, az) = corners.Triangle[tile].ToPixel(1);
        var (bx, bz) = corners.Triangle[o1].ToPixel(1);
        var (cx, cz) = corners.Triangle[o2].ToPixel(1);
        double vx = (ax + bx + cx) / 3, vz = (az + bz + cz) / 3;
        double px = corners.X - vx, pz = corners.Z - vz;
        double t1 = Along(ax, az, bx, bz), t2 = Along(ax, az, cx, cz);
        double s1 = Across(ax, az, bx, bz), s2 = Across(ax, az, cx, cz);
        bool first = s1 <= s2;
        double along = Math.Max(first ? t1 : t2, 0);
        // The faces meet at 120°, so the arc leaves each face R/√3 from the corner.
        double tangent = radius / Math.Sqrt(3);
        if (along >= tangent) return new(tile, first ? o1 : o2, 0, convex);
        double d = tangent - along;
        return new(tile, first ? o1 : o2, radius - Math.Sqrt(radius * radius - d * d), convex);

        double Along(double x0, double z0, double x1, double z1)
        {
            double ex = 0.5 * (x0 + x1) - vx, ez = 0.5 * (z0 + z1) - vz, len = Math.Sqrt(ex * ex + ez * ez);
            return (px * ex + pz * ez) / len;
        }
        double Across(double x0, double z0, double x1, double z1)
        {
            double ex = 0.5 * (x0 + x1) - vx, ez = 0.5 * (z0 + z1) - vz, len = Math.Sqrt(ex * ex + ez * ez);
            return Math.Abs(px * ez - pz * ex) / len;
        }
    }

    /// <summary>Where the cliff face nearest a point stands, and its normal. A point within <paramref name="radius"/> of a
    /// cliff line is moved onto the line (or onto the corner where two faces meet, within twice the radius): the line the
    /// height field's drop is centred on, offset by the corner fillet like <see cref="CliffWeights"/>. Rock walls use it to
    /// stand their columns against the face. The mesh does not: its vertices stay on the lattice, because pulling them onto
    /// the line flips the triangles that cross it on a slant. X and Z are the position, NX/NZ the horizontal normal of the
    /// face (zero when no face is in reach).</summary>
    public (double X, double Z, double NX, double NZ) DrawPoint(double x, double z, double radius)
    {
        if (!_hasCliffs) return (x, z, 0, 0);
        double CornerReach = 2 * radius;
        double cx = x, cz = z;
        if (_wrap) cx -= Period * Math.Floor(cx / Period);
        var corners = Corners(cx, cz);
        if (!corners.HasCliff) return (x, z, 0, 0);
        double best = radius, drawX = 0, drawZ = 0, nx = 0, nz = 0;
        bool moved = false;
        var fillet = FilletAt(corners);
        foreach (var (high, low) in CliffPairs(corners))
        {
            // A rounded corner has no point to snap to: the two faces run into one arc, followed by the line offset below.
            bool rounded = fillet.Tile == high || fillet.Tile == low;
            var (hx, hz) = corners.Triangle[high].ToPixel(1);
            var (lx, lz) = corners.Triangle[low].ToPixel(1);
            // u runs from the ground below the cliff to the bluff, one hex edge apart, so it is the face's own normal.
            double ux = hx - lx, uz = hz - lz, len = Math.Sqrt(ux * ux + uz * uz);
            ux /= len; uz /= len;
            double mx = 0.5 * (hx + lx), mz = 0.5 * (hz + lz);
            // Where the edge ends: the corners where this face meets the next one. Points there move onto the corner
            // itself, or the two faces leave a slanted gusset between them instead of meeting in a vertical corner. The
            // reach is wider than the line's: every point of the lattice step either side of a corner belongs to the
            // corner, and one left behind would fan the two faces into a rounded cove instead of an arris.
            for (int end = -1; end <= 1 && !rounded; end += 2)
            {
                double ex = mx - uz * 0.5 * end, ez = mz + ux * 0.5 * end;
                double edx = corners.X - ex, edz = corners.Z - ez;
                if (edx * edx + edz * edz >= CornerReach * CornerReach) continue;
                var (qx, qz) = Unwarp(ex, ez);
                double mdx = qx - cx, mdz = qz - cz, md = Math.Sqrt(mdx * mdx + mdz * mdz);
                if (md >= best) continue;
                best = md;
                drawX = x + mdx; drawZ = z + mdz; nx = -ux; nz = -uz;
                moved = true;
            }
            double along = (corners.X - mx) * -uz + (corners.Z - mz) * ux;
            if (Math.Abs(along) > 0.5) continue;
            double off = (corners.X - mx) * ux + (corners.Z - mz) * uz + CliffLineAt(cx, cz);
            // The fillet moves the face into the tile both faces belong to: towards the bluff at an outside corner,
            // towards the ground below at an inside one.
            if (rounded) off += fillet.High ? -fillet.Shift : fillet.Shift;
            if (Math.Abs(off) >= best) continue;
            // The face line lies in warped space; the point that lands on it is the inverse image of the projection.
            var (px, pz) = Unwarp(corners.X - ux * off, corners.Z - uz * off);
            double dx = px - cx, dz = pz - cz, d = Math.Sqrt(dx * dx + dz * dz);
            if (d >= best) continue;
            best = d;
            drawX = x + dx; drawZ = z + dz; nx = -ux; nz = -uz;
            moved = true;
        }
        return moved ? (drawX, drawZ, nx, nz) : (x, z, 0, 0);
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

    /// <summary>Per-vertex ground shader inputs at world (x, z): the sharp share of a cliff edge in [0, 1] (1 at the edge,
    /// gone at <see cref="TerrainTuning.CliffOuter"/> — the band the ground's own high/low blend uses), and how deep into a
    /// volcano's crater floor the point lies. The ground shader uses the cliff share to take the palette transition back to
    /// the hex edge, so a raised tile's top keeps its own ground instead of taking the colour of the tile below it, and the
    /// crater share for the lava glow. Both read one corner set, which is most of the cost of either.</summary>
    public (double CliffFace, double Crater) SurfaceAt(double x, double z)
    {
        if (!_hasCliffs && !_hasCrater) return (0, 0);
        if (_wrap) x -= Period * Math.Floor(x / Period);
        var corners = Corners(x, z);
        double face = _hasCliffs && corners.HasCliff ? CliffShares(corners, x, z).Cliff : 0;
        return (face, _hasCrater ? CraterOf(corners) : 0);
    }

    /// <summary>Cliff shares at a corner set's point: the sharp-blend share (full within <see cref="TerrainTuning.CliffInner"/>,
    /// gone at <see cref="TerrainTuning.CliffOuter"/>, both moved by coastal noise) and the linear closeness for surf. Both
    /// reach less than half a tile from the edge.</summary>
    private (double Cliff, double Near) CliffShares(in CornerSet corners, double x, double z)
    {
        var t = Tuning;
        var lambda = corners.Triangle.Lambda;
        double cliff = 0, near = 0, noise = double.NaN;
        foreach (var (high, low) in CliffPairs(corners))
        {
            double d = EdgeDistance(corners, high, low);
            if (d >= 0.48) continue;
            if (double.IsNaN(noise)) noise = 2 * _noise.Fbm(x, z, 2.6, 2, salt: 9) - 1;
            double dn = d + t.CliffNoise * noise;
            cliff = Math.Max(cliff, 1 - SmoothStep(t.CliffInner, t.CliffOuter, dn));
            near = Math.Max(near, Math.Clamp(1 - dn / t.CliffOuter, 0, 1));
        }
        return (cliff, near);
    }

    /// <summary>How tall the cliff around each lifted tile stands, as a share of <see cref="TerrainTuning.CliffRise"/>: full
    /// height in the middle of a run, easing down to <see cref="TerrainTuning.CliffEndScale"/> where the raised ground
    /// ends. An end is the cap of a strip of raised ground, not every side tile: ground along a bluff has to reach the
    /// rise, but only the last tile has nothing beyond it except ground on the same level.</summary>
    private readonly double[] _liftScale;

    /// <summary>How tall each lifted tile's cliff stands, from the distance along its strip to the nearest cap. A strip's
    /// caps are its degree-one tiles: they have raised ground on one side and ground on the same level beyond. A tile on
    /// the long side of a strip also touches low ground, but it is not an end; only distance along the strip lowers it.</summary>
    private double[] BuildLiftScale()
    {
        var s = Snapshot;
        var t = Tuning;
        var scale = new double[s.Relief.Length];
        var component = new int[s.Relief.Length];
        var degree = new int[s.Relief.Length];
        var dist = new int[s.Relief.Length];
        Array.Fill(component, -1);
        Array.Fill(dist, int.MaxValue);
        int components = 0;
        var walk = new Queue<int>();
        for (int tile = 0; tile < s.Relief.Length; tile++)
        {
            if (!_lifted[tile] || component[tile] >= 0) continue;
            component[tile] = components;
            walk.Enqueue(tile);
            while (walk.Count > 0)
            {
                int here = walk.Dequeue();
                var hex = s.Grid.HexOf(new TileId(here));
                for (int d = 0; d < 6; d++)
                {
                    var other = hex + Hex.Directions[d];
                    if (!s.TryTile(other, out int there) || !_lifted[there]) continue;
                    degree[here]++;
                    if (component[there] < 0)
                    {
                        component[there] = components;
                        walk.Enqueue(there);
                    }
                }
            }
            components++;
        }
        var longest = new int[components];
        var queue = new Queue<int>();
        for (int tile = 0; tile < s.Relief.Length; tile++)
        {
            if (!_lifted[tile] || degree[tile] != 1) continue;
            dist[tile] = 0;
            queue.Enqueue(tile);
        }
        // Spread along the raised ground only, so a run's height is read from its own length.
        while (queue.Count > 0)
        {
            int tile = queue.Dequeue();
            longest[component[tile]] = Math.Max(longest[component[tile]], dist[tile]);
            var hex = s.Grid.HexOf(new TileId(tile));
            for (int d = 0; d < 6; d++)
            {
                var other = hex + Hex.Directions[d];
                if (!s.TryTile(other, out int o) || !_lifted[o] || dist[o] <= dist[tile] + 1) continue;
                dist[o] = dist[tile] + 1;
                queue.Enqueue(o);
            }
        }
        for (int tile = 0; tile < scale.Length; tile++)
        {
            if (!_lifted[tile]) continue;
            if (degree[tile] == 0)
            {
                scale[tile] = 1;
                continue;
            }
            // A short run cannot spread its climb over the full taper, so shorten the taper to its own length. The ends
            // still stand exactly at the end scale; the middle still reaches full height.
            double taper = longest[component[tile]] > 0 ? Math.Min(t.CliffEndTaper, longest[component[tile]]) : t.CliffEndTaper;
            double along = dist[tile] == int.MaxValue ? taper : dist[tile];
            scale[tile] = t.CliffEndScale + (1 - t.CliffEndScale) * SmoothStep(0, taper, along);
        }
        return scale;
    }

    /// <summary>One edge of a lifted tile with no cliff on it: the midpoint and unit normal pointing away from the raised
    /// ground, the share of the rise it carries, and the distance over which the ground beyond it falls to low ground.</summary>
    private readonly record struct BareEdge(double X, double Z, double NX, double NZ, double Scale, double Reach,
        double FadeMinus, double FadePlus);

    /// <summary>How far a ramp spreads sideways past the end of its edge where that end meets a cliff face of the same
    /// tile. Short enough that the slope does not spill round the corner in front of the wall.</summary>
    private const double RampFaceFade = 0.45;

    /// <summary>How much the sideways fade beside a face widens per unit the slope runs out, so the end of a run eases
    /// into a grassy shoulder rather than a bank. A little above tan 30° (the angle between a bare edge's normal and the line
    /// of the face at its corner), so the shoulder may reach just past the line of the face as it runs out.</summary>
    private const double RampFaceSpread = 0.7;

    /// <summary>Per tile, the bare edges of nearby lifted tiles. These are the only places the ground climbs to a bluff: an
    /// edge that carries a cliff steps, and the ground at its foot is level.</summary>
    private readonly BareEdge[]?[] _bareEdges;

    /// <summary>The bare edges of every lifted tile, gathered per tile within the ramp's reach.</summary>
    private BareEdge[]?[] BuildBareEdges()
    {
        var s = Snapshot;
        var tune = Tuning;
        var near = new List<BareEdge>?[s.Relief.Length];
        for (int tile = 0; tile < s.Relief.Length; tile++)
        {
            if (!_lifted[tile]) continue;
            var hex = s.Grid.HexOf(new TileId(tile));
            var (ax, az) = hex.ToPixel(1);
            for (int d = 0; d < 6; d++)
            {
                var other = hex + Hex.Directions[d];
                if (!s.TryTile(other, out int t) || _lifted[t]) continue;
                if (CliffHigh(hex, tile, other, t) >= 0) continue;
                var (bx, bz) = other.ToPixel(1);
                double nx = bx - ax, nz = bz - az, len = Math.Sqrt(nx * nx + nz * nz);
                // An edge near the end of a run carries a lower rise and gets a longer descent, so the last of the bluff
                // spreads over more ground instead of ending in a bank. Full-height edges keep the normal ramp.
                double run = Math.Clamp((_liftScale[tile] - tune.CliffEndScale) / (1 - tune.CliffEndScale), 0, 1);
                double descent = tune.CliffEndRamp + (tune.CliffRamp - tune.CliffEndRamp) * SmoothStep(0, 1, run);
                nx /= len; nz /= len;
                double ex = 0.5 * (ax + bx), ez = 0.5 * (az + bz);
                // Each end of the edge meets the next edge of the same tile. Where that one is a cliff, the ramp stops at
                // the corner; elsewhere it spreads sideways and fills the gap to the next slope.
                double fadeMinus = descent, fadePlus = descent;
                for (int turn = -1; turn <= 1; turn += 2)
                {
                    int nd = (d + turn + 6) % 6;
                    var side = hex + Hex.Directions[nd];
                    if (!s.TryTile(side, out int st) || CliffHigh(hex, tile, side, st) != 0) continue;
                    var (sx, sz) = side.ToPixel(1);
                    double sideAlong = (0.5 * (ax + sx) - ex) * -nz + (0.5 * (az + sz) - ez) * nx;
                    if (sideAlong < 0) fadeMinus = RampFaceFade; else fadePlus = RampFaceFade;
                }
                var edge = new BareEdge(ex, ez, nx, nz, _liftScale[tile], descent, fadeMinus, fadePlus);
                foreach (var reach in Hex.Range(hex, CliffReach))
                    if (s.TryTile(reach, out int r)) (near[r] ??= new()).Add(edge);
            }
        }
        return near.Select(list => list?.ToArray()).ToArray();
    }

    /// <summary>How far the ground at a point has climbed to a bluff along an edge with no cliff: full on the edge itself,
    /// falling to nothing over that edge's descent distance. Zero on ground that has cliffs all round it, because the
    /// only edges it could climb from are the ones that step.</summary>
    private double LiftRamp(in CornerSet corners)
    {
        var t = Tuning;
        double lift = 0;
        for (int k = 0; k < 3; k++)
        {
            if (_bareEdges[corners.Tile(k)] is not { } list) continue;
            foreach (var edge in list)
            {
                double dx = corners.X - edge.X, dz = corners.Z - edge.Z;
                // The slope runs straight out from the edge, one edge wide: forward distance sets its height, and past
                // either end it fades sideways, quickly where that end meets a cliff face so the slope stays off the
                // ground in front of the wall.
                double forward = dx * edge.NX + dz * edge.NZ;
                // Behind the edge the raised tile's own bluff takes over; the ramp reaches only a little way back, so it
                // never climbs onto ground across the tile.
                if (forward >= edge.Reach || forward <= -0.4) continue;
                double along = dx * -edge.NZ + dz * edge.NX;
                double over = Math.Abs(along) - 0.5;
                double width = along < 0 ? edge.FadeMinus : edge.FadePlus;
                // Beside a face the fade widens as the slope runs out, so the side of the slope eases down instead of
                // standing as a bank, while still staying behind the line of the face.
                if (width < edge.Reach) width += RampFaceSpread * Math.Max(forward, 0);
                double fade = over <= 0 ? 1 : 1 - SmoothStep(0, width, over);
                if (fade <= 0) continue;
                double ramp = forward >= 0 ? 1 - SmoothStep(0, edge.Reach, forward) : 1 - SmoothStep(0.2, 0.4, -forward);
                lift = Math.Max(lift, ramp * fade * edge.Scale);
            }
        }
        return lift;
    }

    /// <summary>For each tile, whether it is the raised side of any cliff. Water is never raised.</summary>
    private bool[] BuildLifted()
    {
        var s = Snapshot;
        var lifted = new bool[s.Relief.Length];
        for (int tile = 0; tile < s.Relief.Length; tile++)
        {
            if (s.Relief[tile] == ReliefClass.Hidden || IsWater(s.Relief[tile])) continue;
            var hex = s.Grid.HexOf(new TileId(tile));
            for (int d = 0; d < 6; d++)
                if (s.TryTile(hex + Hex.Directions[d], out int other) && CliffHigh(hex, tile, hex + Hex.Directions[d], other) == 0)
                {
                    lifted[tile] = true;
                    break;
                }
        }
        return lifted;
    }

    /// <summary>Every hex within <see cref="CliffReach"/> of the given lifted one that a walk from it reaches without
    /// crossing a cliff edge, itself included. An edge is a wall exactly when this tile is its high side.</summary>
    private IEnumerable<Hex> OpenGround(Hex start)
    {
        var s = Snapshot;
        var seen = new HashSet<Hex> { start };
        var open = new List<Hex> { start };
        for (int head = 0; head < open.Count; head++)
        {
            var hex = open[head];
            if (hex.DistanceTo(start) >= CliffReach) continue;
            if (!s.TryTile(hex, out int here)) continue;
            for (int d = 0; d < 6; d++)
            {
                var next = hex + Hex.Directions[d];
                if (seen.Contains(next) || !s.TryTile(next, out int there)) continue;
                if (CliffHigh(hex, here, next, there) == 0) continue;
                seen.Add(next);
                open.Add(next);
            }
        }
        return open;
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

    /// <summary>The corner pairs (lifted index, lower index) of a corner set whose shared edge is a known cliff. An edge
    /// with no bit is not one: the ground there climbs to the bluff over the tiles beside it, so it has no face, no wall and
    /// nothing to snap.</summary>
    private IEnumerable<(int High, int Low)> CliffPairs(CornerSet corners)
    {
        for (int k = 0; k < 3; k++)
        {
            int a = k, b = (k + 1) % 3;
            int side = CliffHigh(corners.Triangle[a], corners.Tile(a), corners.Triangle[b], corners.Tile(b));
            if (side < 0) continue;
            yield return side == 0 ? (a, b) : (b, a);
        }
    }

    /// <summary>Which of two adjacent corners' tiles is the lifted side of the cliff on the edge between them: 0 or 1 for
    /// that corner, -1 when the edge carries no cliff. A cliff needs both tiles known and on the map, either side to
    /// remember the bit, and exactly one side lifted. Without a lift side, a land/water edge lifts the land.</summary>
    private int CliffHigh(Hex a, int ta, Hex b, int tb)
    {
        var s = Snapshot;
        if (s.Cliffs[ta] == 0 && s.Cliffs[tb] == 0) return -1;
        var ra = s.Relief[ta]; var rb = s.Relief[tb];
        if (ra == ReliefClass.Hidden || rb == ReliefClass.Hidden) return -1;
        if (!s.TryTile(a, out _) || !s.TryTile(b, out _)) return -1;
        int d = DirectionOf(a, b);
        if (d < 0 || ((s.Cliffs[ta] >> d) & 1) == 0 && ((s.Cliffs[tb] >> ((d + 3) % 6)) & 1) == 0) return -1;
        int mirror = (d + 3) % 6;
        bool aHigh = ((s.CliffLifts[ta] >> d) & 1) != 0, bHigh = ((s.CliffLifts[tb] >> mirror) & 1) != 0;
        if (aHigh != bHigh) return aHigh ? 0 : 1;
        // A memory written before the lifted side existed only knows the old land/water rule; fog without the far tile
        // still knows which side the cliff drops towards.
        if (IsWater(ra) != IsWater(rb)) return IsWater(ra) ? 1 : 0;
        return -1;
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

    /// <summary>Sea stacks of a cliff whose lower side is water, in warped space. Each lies 0.17 to 0.29 out from the edge
    /// with a shoal radius up to 0.13, so it stays inside the reach of the edge's own corner triangles.</summary>
    private IEnumerable<StackSpot> StacksOf(CornerSet corners, int high, int low)
    {
        int tile = corners.Tile(high), direction = DirectionOf(corners.Triangle[high], corners.Triangle[low]);
        var (ax, az) = corners.Triangle[high].ToPixel(1);
        var (bx, bz) = corners.Triangle[low].ToPixel(1);
        return StacksOfEdge(tile, direction, ax, az, bx, bz, corners.Tile(low));
    }

    private IEnumerable<StackSpot> StacksOfEdge(int highTile, int direction, double ax, double az, double bx, double bz, int lowTile)
    {
        uint salt = 50 + 10 * (uint)direction;
        double roll = TileHash(highTile, salt);
        // A cliff that meets water always stands on rock; the roll only adds a second column for body.
        int count = roll < Tuning.StackChance ? 2 : 1;
        double nx = bx - ax, nz = bz - az, len = Math.Sqrt(nx * nx + nz * nz);
        nx /= len; nz /= len;
        double mx = 0.5 * (ax + bx), mz = 0.5 * (az + bz);
        for (int i = 0; i < count; i++)
        {
            uint s = salt + 1 + 4 * (uint)i;
            double along = (TileHash(highTile, s) - 0.5) * 0.7, outward = 0.17 + 0.12 * TileHash(highTile, s + 1);
            double radius = 0.09 + 0.04 * TileHash(highTile, s + 2);
            yield return new(mx - nz * along + nx * outward, mz + nx * along + nz * outward, radius, lowTile,
                TileHash(highTile, s + 3), TileHash(highTile, s + 4), TileHash(highTile, s + 5));
        }
    }

    /// <summary>A placed sea stack: world position of its foot on the shoal, the water tile it stands in (for sight), and
    /// model, turn and scale hashes in [0, 1).</summary>
    public readonly record struct SeaStack(double X, double Y, double Z, int Tile, double Model, double Yaw, double Scale);

    private readonly Lazy<IReadOnlyList<SeaStack>> _stacks;

    /// <summary>Every sea stack of the known cliffs, in canonical world coordinates.</summary>
    public IReadOnlyList<SeaStack> SeaStacks => _stacks.Value;

    /// <summary>One rock column of a cliff wall: world position of its foot, the land tile it belongs to (for sight), its
    /// model hash in [0, 1), turn in radians, uniform scale, extra vertical stretch and lean (the horizontal direction its
    /// top tips towards, with the angle in radians as its length).</summary>
    public readonly record struct CliffRock(double X, double Y, double Z, int Tile, double Model, double Yaw, double Scale,
        double Stretch, double TiltX = 0, double TiltZ = 0);

    /// <summary>How far a wall column may look for its cliff's face: the domain warp moves a face by up to its amplitude and
    /// the line's wander adds a little, so the search has to reach past both without reaching a neighbouring edge.</summary>
    public const double FaceSnap = 0.45;

    /// <summary>How far outside the face a land wall's columns stand at their foot. Each column then leans back into the
    /// face (see <see cref="Column"/>), so its broad foot stands proud of the face and its narrow top rests against the
    /// brink with no strip of bare face behind it.</summary>
    public const double FaceOut = 0.06;

    /// <summary>How far past the face a leaning column's top reaches, so it tucks into the brink rather than stopping just
    /// short of it.</summary>
    private const double LeanPast = 0.015;

    /// <summary>Largest lean of a wall column from vertical, in radians.</summary>
    private const double MaxLean = 0.4;

    /// <summary>Distances from a land cliff's face, in world units, at which a wall column reads the ground for its top
    /// (inland) and its foot (outside). Several, because one sample a fixed distance out can land on the ground above the
    /// face where the line wanders, and a column based there would stand at the brink with the face below it bare.</summary>
    private static readonly double[] WallProbe = { 0.12, 0.25, 0.4 };

    /// <summary>Rock buttresses per coastal cliff edge. Few and broad: the wall reads as a run of separate blocks, not a
    /// smooth face and not a fence of spikes.</summary>
    public const int CliffRocksPerEdge = 12;

    /// <summary>Rock buttresses per land/land cliff edge. A little denser than the coast's: a land cliff has no water to
    /// hide its foot and no dark backdrop, so the wall itself has to cover the face the terrain shader tints as bare
    /// stone.</summary>
    public const int LandRocksPerEdge = 16;

    /// <summary>Height of the rock model geometry at scale 1, in world units: the column placement solves the scale from
    /// the height it must span so its thick body, not just its pointed tip, reaches the bluff.</summary>
    public const double CliffRockHeight = 0.16;

    /// <summary>The rock wall of every known cliff whose lifted tile centre lies within 1.5 of the rectangle (x0..x1,
    /// z0..z1), canonical x: a cliff that drops into water stands its columns on the waterline, a cliff between two land
    /// tiles on the edge itself. Each column reaches a little above the bluff top, found by sampling the height field
    /// across the edge, so the wall follows the wandering cliff line.</summary>
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
                // Only the lifted side draws the wall, so a cliff between two land tiles (both of which hold the edge bit)
                // still gets exactly one. An edge whose lifted side is unknown stays bare, because the wall would stand
                // with its body in the open instead of against the face.
                if (!s.TryTile(other, out int low) || CliffHigh(hex, tile, other, low) != 0) continue;
                var (bx, bz) = other.ToPixel(1);
                var wall = IsWater(s.Relief[low]) ? EdgeRocks(tile, d, ax, az, bx, bz) : LandEdgeRocks(tile, d, ax, az, bx, bz);
                foreach (var rock in wall) yield return rock;
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
            var (px, pz) = Unwarp(mx - nz * along, mz + nx * along);
            // The waterline across the edge: the first wet sample on the way out from just inland of it. A column stands
            // there, so the wall follows the shore. The offset is clamped, because the blended terrain can push the
            // waterline a long way inland or out to sea, and the wall must stay one column thick along its own edge
            // rather than scattering into a bay or a shoal and leaving the edge looking bare.
            double off = double.NaN;
            for (double o = -0.15; o <= 0.45; o += 0.03)
            {
                if (HeightAt(px + nx * o, pz + nz * o) >= 0) continue;
                off = o;
                break;
            }
            if (double.IsNaN(off)) off = 0.05;
            off = Math.Clamp(off, -0.05, 0.3);
            double sx = px + nx * off, sz = pz + nz * off;
            double foot = Math.Min(HeightAt(px + nx * (off + 0.04), pz + nz * (off + 0.04)), -0.005);
            // The brink is the top of the rock face: the bluff just inland of the waterline, never below the cliff the
            // tile stands on.
            double top = Math.Max(HeightAt(px + nx * (off - 0.12), pz + nz * (off - 0.12)), Tuning.FlatBase + Tuning.CliffRise +
                Tuning.FlatNoise * _noise.Fbm(sx, sz, Tuning.FlatFrequency, Tuning.Octaves, salt: 2));
            // Lean back towards the land, a little: the waterline column only has to rest its top against the bluff.
            yield return Column(tile, r, sx, sz, foot, top, lip: 0.015, -nx, -nz, 0.03);
        }
    }

    /// <summary>The rock wall of a cliff between two land tiles. There is no waterline to follow, so the wall asks the field
    /// where the face is: <see cref="DrawPoint"/> snaps a point onto the drawn cliff line, so each column stands against the
    /// face the mesh drops over, wherever the warp and the line's wander have put it, and reads its top and foot there. An
    /// edge whose ground does not step at all gets no wall: rocks there would stand on flat ground with nothing behind
    /// them.</summary>
    private IEnumerable<CliffRock> LandEdgeRocks(int tile, int direction, double ax, double az, double bx, double bz)
    {
        double nx = bx - ax, nz = bz - az, len = Math.Sqrt(nx * nx + nz * nz);
        nx /= len; nz /= len;
        double mx = 0.5 * (ax + bx), mz = 0.5 * (az + bz);
        uint salt = 400 + 64 * (uint)direction;
        // The face at the middle of the edge, and whether there is one. Both bounds sit well under what a face of one
        // CliffRise measures, so no edge that does carry a face is dropped.
        var (faceX, faceZ, faceNX, faceNZ) = DrawPoint(mx, mz, FaceSnap);
        if (faceNX == 0 && faceNZ == 0) yield break;
        const double StepProbe = 0.12;
        double drop = HeightAt(faceX - faceNX * StepProbe, faceZ - faceNZ * StepProbe)
            - HeightAt(faceX + faceNX * StepProbe, faceZ + faceNZ * StepProbe);
        if (drop < 0.5 * Tuning.CliffRise) yield break;
        for (int i = 0; i < LandRocksPerEdge; i++)
        {
            uint r = salt + 5 * (uint)i;
            // The first and last pillar stand on the corners themselves, where the next edge's wall starts, so the two
            // meet; only the pillars between them are jittered, and never far enough to open a gap.
            double along = -0.5 + i / (double)(LandRocksPerEdge - 1);
            if (i > 0 && i < LandRocksPerEdge - 1) along += 0.6 * (TileHash(tile, r) - 0.5) / LandRocksPerEdge;
            var (px, pz) = Unwarp(mx - nz * along, mz + nx * along);
            // Each column finds the face itself: the warp tilts the line across an edge, so one offset for the whole edge
            // would walk the wall onto the ground above it at one end and into the face at the other.
            var (fx, fz, fnx, fnz) = DrawPoint(px, pz, FaceSnap);
            if (fnx == 0 && fnz == 0) { fx = px + (faceX - mx); fz = pz + (faceZ - mz); fnx = faceNX; fnz = faceNZ; }
            double sx = fx + fnx * FaceOut, sz = fz + fnz * FaceOut;
            double above = HeightAt(sx, sz), floor = above;
            foreach (double t in WallProbe) floor = Math.Min(floor, HeightAt(sx + fnx * t, sz + fnz * t));
            // The top is the brink, the first probe inland that stands on the raised ground. Deeper probes only matter
            // where the line wanders and the near one still lands on the face; taking the highest of all of them would
            // carry the wall up any hill that rises behind the brink.
            foreach (double t in WallProbe)
            {
                above = Math.Max(above, HeightAt(sx - fnx * t, sz - fnz * t));
                if (above - floor >= 0.5 * drop) break;
            }
            // The rock models are broadest at their foot and taper to the top, so one squat column per position covers
            // the foot of the face and its tip just reaches the brink.
            // Where the face has run out (the shoulder at the end of a run) a column would be a loose boulder on the grass.
            if (above - floor < 0.3 * Tuning.CliffRise) continue;
            yield return Column(tile, r, sx, sz, floor - 0.03, above, lip: 0.005, -fnx, -fnz, FaceOut + LeanPast);
        }
    }

    /// <summary>Least vertical stretch of a wall column over its model's own proportions. At or a little under 1, so the
    /// columns keep the models' broad foot and narrow top and read as squat buttresses, not spikes.</summary>
    private const double ColumnStretch = 0.8;

    /// <summary>Most extra vertical stretch a wall column takes on top of <see cref="ColumnStretch"/>.</summary>
    private const double ColumnStretchSpread = 0.3;

    /// <summary>One column of a wall: a broad buttress from below the ground at its foot to <paramref name="lip"/> above the
    /// given top. Its scale is solved from the height it must span, so its body hides the mesh face and its narrow top just
    /// crests the bluff. The stretch stays near the models' own proportions, so a taller face takes a wider rock rather
    /// than a thinner spike.</summary>
    private CliffRock Column(int tile, uint r, double x, double z, double foot, double top, double lip, double intoX,
        double intoZ, double reach)
    {
        double stretch = ColumnStretch + ColumnStretchSpread * TileHash(tile, r + 1);
        double height = top + lip * (1 + TileHash(tile, r + 2)) - foot;
        double scale = Math.Max(0.4, height / (CliffRockHeight * stretch));
        if (_wrap) x -= Period * Math.Floor(x / Period);
        // Lean the column into the face so its top travels <paramref name="reach"/> towards it over its height; the lean
        // varies a little per column, so the tops do not line up.
        double lean = Math.Min(MaxLean, Math.Atan2(reach, Math.Max(height, 0.05)) * (0.8 + 0.4 * TileHash(tile, r + 5)));
        // Leaning shortens the column; stand it a little taller so its top still reaches the brink.
        scale /= Math.Cos(lean);
        return new(x, foot, z, tile, TileHash(tile, r + 3), Math.Tau * TileHash(tile, r + 4), scale, stretch,
            intoX * lean, intoZ * lean);
    }

    /// <summary>The world point that warps onto the given canonical point (the warp is smooth and small, so a few
    /// iterations of the inverse are enough).</summary>
    private (double X, double Z) Unwarp(double x, double z)
    {
        double px = x, pz = z;
        for (int k = 0; k < 6; k++)
        {
            var (ox, oz) = Warp.Sample(px, pz);
            px = x - ox * Tuning.WarpAmplitude;
            pz = z - oz * Tuning.WarpAmplitude;
        }
        return (px, pz);
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
                if (!s.TryTile(other, out int low) || !IsWater(s.Relief[low]) || CliffHigh(hex, tile, other, low) != 0) continue;
                var (ax, az) = hex.ToPixel(1);
                var (bx, bz) = other.ToPixel(1);
                foreach (var spot in StacksOfEdge(tile, d, ax, az, bx, bz, low))
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
                    result.Add(new(px, HeightAt(px, pz), pz, low, spot.Model, spot.Yaw, spot.Scale));
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
        bool crater = Snapshot.Crater[corners.Tile(k)];
        double shape = crater ? VolcanoShape(m, u, v, spur, detail.Wander) : Summit(m.Main, u, v, spur, detail.Wander);
        foreach (var spire in m.Spires)
        {
            double su = (u - spire.U) / spire.Size, sv = (v - spire.V) / spire.Size;
            shape = SoftMax(shape, spire.Height * Summit(spire.Faces, su, sv, spur, detail.Wander), 0.14);
        }
        // A broad, low massif under the summits fills the tile, so their feet reach the saddles.
        shape = SoftMax(shape, t.PeakMassif * Cone(Math.Sqrt(u * u + v * v) * spur / 1.4, 1.2), 0.08);
        // Ridges to neighbouring peaks, in absolute rise units so both tiles of a pair build the same crest.
        double rise = m.Summit * shape;
        foreach (var ridge in m.Ridges)
            rise = SoftMax(rise, RidgeShape(ridge, corners.X - cx, corners.Z - cz, spur, detail.Wander), 0.05);
        if (crater && m.Ridges.Length > 0)
        {
            // Ridges climb to a volcano's rim but never fill its crater.
            double inside = Math.Sqrt(u * u + v * v) / t.CraterRadius;
            double cone = m.Summit * VolcanoShape(m, u, v, spur, detail.Wander);
            rise += (Math.Min(rise, cone) - rise) * (1 - SmoothStep(1.0, 1.6, inside));
        }
        shape = rise / m.Summit;
        // A volcano's crater rim and floor stay smooth; crags and cliffs belong to its lower flanks.
        double rough = crater ? 1 - SmoothStep(0.6, 0.85, shape) : 1;
        return t.PeakBase + t.PeakRise * rise + rough * (t.PeakNoise * detail.Crag * (0.6 + 0.4 * shape)
            + t.PeakRoughness * detail.Rough * SmoothStep(0.08, 0.45, shape));
    }

    /// <summary>A volcano's cone in [0, 1]: a mostly round, straight-flanked cone (a little of the faceted summit keeps its
    /// gullies) cut off at the crater rim, with a bowl-shaped crater sunk into the top.</summary>
    private double VolcanoShape(Mountain m, double u, double v, double spur, double wander)
    {
        var t = Tuning;
        double r = Math.Sqrt(u * u + v * v);
        double cone = 0.7 * Cone(r * (1 + 0.4 * (spur - 1)), t.VolcanoCurve) + 0.3 * Summit(m.Main, u, v, spur, wander);
        double rim = Cone(t.CraterRadius, t.VolcanoCurve);
        double shape = -SoftMax(-cone, -rim, 0.04);
        double inside = r / t.CraterRadius;
        if (inside < 1) shape -= t.CraterDepth * (1 - inside * inside) * (1 - inside * inside);
        return shape;
    }

    /// <summary>World position of the middle of a crater tile's floor (canonical x), or null for a tile without a crater.
    /// The crater is shaped in warped space; a few fixed-point steps undo the warp.</summary>
    public (double X, double Y, double Z)? CraterCentre(int tile)
    {
        if (!Snapshot.Crater[tile] || Snapshot.Relief[tile] != ReliefClass.Peak) return null;
        var m = MountainOf(tile);
        var (cx, cz) = Snapshot.Grid.HexOf(new TileId(tile)).ToPixel(1);
        double wx = cx + m.OffsetX, wz = cz + m.OffsetZ, x = wx, z = wz;
        for (int i = 0; i < 4; i++)
        {
            var (ox, oz) = Warp.Sample(x, z);
            x = wx - ox * Tuning.WarpAmplitude;
            z = wz - oz * Tuning.WarpAmplitude;
        }
        if (_wrap) x -= Period * Math.Floor(x / Period);
        return (x, HeightAt(x, z), z);
    }

    /// <summary>How deep into a volcano's crater floor (x, z) lies, in [0, 1]: 1 at the middle of the floor, 0 from the rim
    /// outwards. The terrain shader draws the lava glow from it. Zero everywhere when no known tile has a crater.</summary>
    public double CraterAt(double x, double z)
    {
        if (!_hasCrater) return 0;
        if (_wrap) x -= Period * Math.Floor(x / Period);
        return CraterOf(Corners(x, z));
    }

    /// <summary>See <see cref="CraterAt"/>: the same share for a corner set already in hand.</summary>
    private double CraterOf(in CornerSet corners)
    {
        var w = HeightWeights(corners);
        double glow = 0;
        for (int k = 0; k < 3; k++)
        {
            int tile = corners.Tile(k);
            if (w[k] <= 0 || !Snapshot.Crater[tile] || corners.Relief(k) != ReliefClass.Peak) continue;
            var m = MountainOf(tile);
            var (cx, cz) = corners.Triangle[k].ToPixel(1);
            double dx = corners.X - cx - m.OffsetX, dz = corners.Z - cz - m.OffsetZ;
            double u = (dx * m.AxisCos + dz * m.AxisSin) * m.ScaleAlong, v = (dz * m.AxisCos - dx * m.AxisSin) * m.ScaleAcross;
            double inside = Math.Sqrt(u * u + v * v) / Tuning.CraterRadius;
            glow = Math.Max(glow, w[k] * (1 - SmoothStep(0.45, 1.0, inside)));
        }
        return glow;
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
        bool crater = Snapshot.Crater[tile];
        double radius = t.PeakRadius * (0.85 + 0.3 * TileHash(tile, 5)) * (crater ? 1.1 : 1);
        // A volcano is a single round cone: no ridge stretch and no lesser spires.
        double stretch = crater ? 1 : t.PeakStretch * (0.75 + 0.5 * TileHash(tile, 6));
        // Lesser spires: the first two sit along the ridge on either side, up to two more anywhere around.
        var spires = new Spire[crater ? 0 : 2 + (int)(TileHash(tile, 7) * 3)];
        for (int i = 0; i < spires.Length; i++)
        {
            uint salt = 30 + 10 * (uint)i;
            double direction = (i == 0 ? 0 : i == 1 ? Math.PI : Math.Tau * TileHash(tile, salt)) + 0.8 * (TileHash(tile, salt + 1) - 0.5);
            double reach = 0.25 + 0.2 * TileHash(tile, salt + 2);
            spires[i] = new(reach * Math.Cos(direction), reach * Math.Sin(direction), 0.5 + 0.25 * TileHash(tile, salt + 3),
                t.PeakSpires * (0.45 + 0.45 * TileHash(tile, salt + 4)), FacesOf(tile, salt + 5));
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

    /// <summary>A summit's height in rise units. Volcanoes stand at the full rise, above the peaks around them.</summary>
    private double SummitHeight(int tile) => Snapshot.Crater[tile] ? 1 : 1 - Tuning.PeakVariation * TileHash(tile, 3);

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
    /// flat tops and feet, so the tile reads as a raised, rolling hill rather than a cliff, and neighbouring hills form
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
