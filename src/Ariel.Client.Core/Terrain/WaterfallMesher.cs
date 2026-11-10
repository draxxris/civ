namespace Ariel.Client.Terrain;

/// <summary>Engine-free waterfall arrays for one chunk, wound like <see cref="TerrainMeshData"/>. The water surface holds
/// every fall's lip, sheets and plunge pool; per vertex <see cref="WaterLanes"/> = (across, down): across runs −1 to 1
/// over the lip (or round the pool), down is the distance the water has travelled from the brink (negative on the ledge;
/// across the pool, the share of its radius), and <see cref="WaterInfo"/> = (part, seed, foam, fade): part is
/// <see cref="WaterfallMesher.Lip"/>, <see cref="WaterfallMesher.Sheet"/>, <see cref="WaterfallMesher.Veil"/> or
/// <see cref="WaterfallMesher.Pool"/>, seed
/// shifts the noise between layers and falls, foam is how churned the water is and fade is 0 where the surface runs out
/// into the river behind the ledge. The mist is a set of camera-facing
/// puffs: every corner of a puff carries its centre in <see cref="MistPositions"/>, its corner (±1, ±1) in
/// <see cref="MistCorners"/> and (size, phase, rise, density) in <see cref="MistInfo"/>; the shader turns the quad to
/// the camera and drifts it.</summary>
public sealed record WaterfallMeshData(
    float[] WaterPositions, float[] WaterNormals, float[] WaterLanes, float[] WaterInfo, int[] WaterIndices,
    float[] MistPositions, float[] MistCorners, float[] MistInfo, int[] MistIndices)
{
    /// <summary>Number of water vertices.</summary>
    public int WaterVertexCount => WaterPositions.Length / 3;
    /// <summary>Number of mist vertices.</summary>
    public int MistVertexCount => MistPositions.Length / 3;
}

/// <summary>A fall's plunge pool: its centre, in the fall's link's frame, and its radius.</summary>
public readonly record struct PlungeSpot(double X, double Z, double Radius);

/// <summary>Builds the waterfalls of one chunk from <see cref="RiverNetwork.Falls"/>. A waterfall is its own thing, not
/// the river surface bent over the cliff: water slides off the ledge rock (<see cref="FallLedge"/>) as a glassy tongue,
/// breaks into a white sheet thrown clear of the rock wall, with a thinner veil in front of it for body, and lands in a
/// round churned pool under rising mist. A fall belongs to the chunk holding its corner, which shares wrap copies and
/// dirty signatures with the chunk meshes.</summary>
public static class WaterfallMesher
{
    /// <summary>Part of the water surface: the tongue running off the ledge.</summary>
    public const float Lip = 0;
    /// <summary>Part of the water surface: a falling sheet.</summary>
    public const float Sheet = 1;
    /// <summary>Part of the water surface: the plunge pool.</summary>
    public const float Pool = 2;
    /// <summary>Part of the water surface: the thin veil hung in front of the main sheet, which tears into strands.</summary>
    public const float Veil = 3;

    // Down the fall: lanes from the river's channel behind the ledge onto its top and over its nose, then the sheet.
    private const int ChannelLanes = 1, LedgeLanes = 3, SheetLanes = 9;
    /// <summary>Length of the river, in world units, behind the slab's back edge over which the tongue fades in on top
    /// of it; and how far under the slab the river runs on, so no ground shows between them.</summary>
    private const double Handover = 0.04, UnderSlab = 0.03;
    /// <summary>How churned the water is as it leaves the nose: the tongue whitens up to this on the ledge and the sheet
    /// goes on from it, so the water has no seam where it turns down.</summary>
    public const float LipFoam = 0.3f;
    /// <summary>Sections across a waterfall's lip.</summary>
    private const int Sections = 9;
    /// <summary>The veil in front of the main sheet: how far out it hangs, how much of the lip it spans, and how far
    /// down it reaches before it has torn into spray.</summary>
    private const double VeilOut = 0.014, VeilSpan = 0.8, VeilReach = 0.75;
    /// <summary>Plunge pool: rings out from where the sheet lands and segments round it; its radius in lip half widths
    /// (about the half width of the river it falls from), with a cap for the widest rivers.</summary>
    private const int PoolRings = 5, PoolSegments = 20;
    private const double PoolWidths = 1.6, MaxPoolRadius = 0.22;
    /// <summary>Largest pool radius in a river along the foot, as a share of that river's half width.</summary>
    private const double PoolInChannel = 0.65;
    /// <summary>Share of a pool's radius out to which its foam is solid; beyond it the pool thins out to its rim.</summary>
    private const double PoolSolid = 0.6;
    /// <summary>Spray where the sheet lands: small dense puffs that burst up from the pool, and their sizes in lip half
    /// widths.</summary>
    private const int SplashPuffs = 4;
    private const double SplashMin = 0.5, SplashMax = 0.9;
    /// <summary>Mist drifting off the pool: puffs, how high above the foot the tallest one starts, and their sizes in lip
    /// half widths.</summary>
    private const int MistPuffs = 6;
    private const double MistHeight = 0.5, MistMin = 1.0, MistMax = 2.0;
    /// <summary>Height above the highest drawn ground nearby that the tongue's channel water lies at, and the offsets
    /// (along the fall, across it) at which that ground is read.</summary>
    private const double ChannelLift = 0.012;
    private static readonly (double Along, double Across)[] GroundProbes =
        [(0, 0), (0.03, 0), (-0.03, 0), (0, 0.02), (0, -0.02), (0.03, 0.02), (0.03, -0.02), (-0.03, 0.02), (-0.03, -0.02)];
    /// <summary>Height above the ground or water that the pool floats at.</summary>
    private const double PoolLift = 0.012;

    /// <summary>The chunk's waterfalls, or null when none stands in it.</summary>
    public static WaterfallMeshData? Build(TerrainChunk chunk, TerrainField field, int k = TerrainMesher.DefaultResolution) =>
        Build(chunk, field, new TerrainLattice(field, k));

    /// <summary>The chunk's waterfalls over terrain whose resolution varies by chunk (see
    /// <see cref="TerrainChunks.ResolutionAt"/>), or null when none stands in it.</summary>
    public static WaterfallMeshData? Build(TerrainChunk chunk, TerrainField field, Func<double, double, int> resolutionAt) =>
        Build(chunk, field, new TerrainLattice(field, resolutionAt));

    /// <summary>How far along a fall (from its brink) the river that feeds it runs on: a little way under the back of
    /// the ledge rock, where the tongue already lies on top of it.</summary>
    public static double RiverRunsTo(FallLedge ledge) => -ledge.BackIn + UnderSlab;

    /// <summary>The plunge pool of a fall: a disc of churned water round where the sheet lands, in its link's frame. A
    /// fall into the sea gets a wide pool on the water. A fall into a river along the foot keeps its pool inside that
    /// river's channel: the pool is no wider than <see cref="PoolInChannel"/> of the river's half width, and its centre
    /// moves from the river's centre line toward where the sheet lands only as far as the banks allow.</summary>
    public static PlungeSpot PoolOf(TerrainField field, RiverFall fall)
    {
        var (lx, lz) = field.LedgeOf(fall).Landing;
        double radius = Math.Min(PoolWidths * fall.HalfWidth, MaxPoolRadius);
        if (!fall.AlongFoot || field.Rivers.Curves.FirstOrDefault(c => c.Fall == fall) is not { } foot) return new(lx, lz, radius);
        // The point of the river's centre line nearest where the sheet lands, and the river's half width there.
        double best = double.MaxValue, cx = lx, cz = lz, width = foot.Width[0];
        for (int i = 0; i < foot.Count - 1; i++)
        {
            double sx = foot.X[i + 1] - foot.X[i], sz = foot.Z[i + 1] - foot.Z[i], len2 = sx * sx + sz * sz;
            double t = len2 > 0 ? Math.Clamp(((lx - foot.X[i]) * sx + (lz - foot.Z[i]) * sz) / len2, 0, 1) : 0;
            double px = foot.X[i] + sx * t, pz = foot.Z[i] + sz * t;
            double d = (px - lx) * (px - lx) + (pz - lz) * (pz - lz);
            if (d < best) (best, cx, cz, width) = (d, px, pz, foot.Width[i] + (foot.Width[i + 1] - foot.Width[i]) * t);
        }
        radius = Math.Min(radius, PoolInChannel * width);
        double ox = lx - cx, oz = lz - cz, off = Math.Sqrt(ox * ox + oz * oz);
        double reach = Math.Max(0, width - radius);
        double k = off > reach ? reach / off : 1;
        return new(cx + ox * k, cz + oz * k, radius);
    }

    private static WaterfallMeshData? Build(TerrainChunk chunk, TerrainField field, TerrainLattice lattice)
    {
        var mesh = new Builder(field, lattice);
        double period = field.Period;
        double[] shifts = field.Snapshot.Grid.Spec.Wrap ? [-period, 0, period] : [0];
        foreach (var fall in field.Rivers.Falls)
            foreach (double shift in shifts)
                if (chunk.Contains(fall.X + shift, fall.Z)) mesh.Add(fall, field.LedgeOf(fall), shift);
        return mesh.Empty ? null : mesh.ToData();
    }

    private sealed class Builder(TerrainField field, TerrainLattice lattice)
    {
        private readonly List<float> _positions = [], _normals = [], _lanes = [], _info = [];
        private readonly List<int> _indices = [];
        private readonly List<float> _mistPositions = [], _mistCorners = [], _mistInfo = [];
        private readonly List<int> _mistIndices = [];

        public bool Empty => _indices.Count == 0;

        public void Add(RiverFall fall, FallLedge ledge, double shift)
        {
            // One seed per fall, so neighbouring falls do not churn in step.
            float seed = (float)(Math.Abs(Math.Sin(fall.X * 12.9898 + fall.Z * 78.233)) % 1.0);
            Fall(ledge, fall.HalfWidth, shift, span: 1, outward: 0, reach: 1, seed, lip: true);
            Fall(ledge, fall.HalfWidth, shift, VeilSpan, VeilOut, VeilReach, seed + 0.37f, lip: false);
            PlungePool(PoolOf(field, fall), ledge, shift, seed);
            Mist(ledge, fall.HalfWidth, shift, seed);
        }

        /// <summary>One sheet across the lip: <paramref name="span"/> of its width, hung <paramref name="outward"/> in front
        /// of the main sheet and falling <paramref name="reach"/> of the way to the foot. The main sheet starts on the
        /// river just behind the ledge and runs over the ledge first.</summary>
        private void Fall(FallLedge ledge, double halfWidth, double shift, double span, double outward, double reach, float seed,
            bool lip)
        {
            int start = _positions.Count / 3, lanes = (lip ? ChannelLanes + LedgeLanes : 0) + SheetLanes;
            for (int i = 0; i < Sections; i++)
            {
                double across = 2.0 * i / (Sections - 1) - 1;
                Section(ledge, across, across * halfWidth * span, shift, outward, reach, seed, lip);
            }
            Join(start, Sections, lanes);
        }

        private void Section(FallLedge ledge, double across, double side, double shift, double outward, double reach, float seed,
            bool lip)
        {
            double dx = ledge.DirectionX, dz = ledge.DirectionZ;
            double px = ledge.X + shift - dz * side, pz = ledge.Z + dx * side;
            double noseY = ledge.TopAt(ledge.NoseOut) + 0.008;
            double drop = noseY - ledge.Foot - 0.005;
            double v = 0, lastO = 0, lastY = 0;
            if (lip)
            {
                for (int lane = 0; lane < ChannelLanes + LedgeLanes; lane++)
                {
                    double o, y;
                    if (lane < ChannelLanes)
                    {
                        // On the river just behind the ledge, fading in over it. It lies on the river, which lies on the
                        // drawn ground or keeps up to the brim where the ground rounds down, so no ground shows through it.
                        o = -ledge.BackIn - Handover * (ChannelLanes - lane) / ChannelLanes;
                        y = Math.Max(Ground(px + dx * o, pz + dz * o, dx, dz), ledge.Brim) + ChannelLift;
                    }
                    else
                    {
                        o = -ledge.BackIn + (ledge.BackIn + ledge.NoseOut) * (lane - ChannelLanes) / (LedgeLanes - 1);
                        y = ledge.TopAt(o) + 0.008;
                    }
                    v = o - ledge.NoseOut;
                    // The tongue speeds up and starts to whiten as it reaches the nose.
                    float foam = (float)Math.Clamp((o + ledge.BackIn) / (ledge.BackIn + ledge.NoseOut), 0, 1) * LipFoam;
                    Vertex(px + dx * o, y, pz + dz * o, 0, 1, 0, (float)across, (float)v, Lip, seed, foam, lane == 0 ? 0 : 1);
                    lastO = o; lastY = y;
                }
            }
            for (int lane = 0; lane < SheetLanes; lane++)
            {
                double t = reach * lane / (SheetLanes - 1);
                // A thrown jet: its reach grows with the square root of the drop.
                double o = ledge.NoseOut + ledge.Reach * Math.Sqrt(t) + outward;
                double y = noseY - drop * t;
                double dO = ledge.Reach * 0.5 / Math.Sqrt(Math.Max(t, 0.02)), dy = -drop;
                double len = Math.Sqrt(dO * dO + dy * dy);
                if (lane > 0 || lip) v += Math.Sqrt((o - lastO) * (o - lastO) + (y - lastY) * (y - lastY));
                Vertex(px + dx * o, y, pz + dz * o, dx * -dy / len, dO / len, dz * -dy / len, (float)across, (float)v,
                    lip ? Sheet : Veil,
                    seed, (float)(LipFoam + (1 - LipFoam) * t), 1);
                lastO = o; lastY = y;
            }
        }

        /// <summary>A round pool of churned water centred where the sheet lands, lying on the river below or the sea. Its
        /// down lane is the share of the radius out from the centre.</summary>
        private void PlungePool(PlungeSpot pool, FallLedge ledge, double shift, float seed)
        {
            var (cx, cz, radius) = pool;
            cx += shift;
            int centre = _positions.Count / 3;
            Vertex(cx, Surface(cx, cz, ledge), cz, 0, 1, 0, 0, 0, Pool, seed, 1, 1);
            for (int ring = 1; ring <= PoolRings; ring++)
            {
                double r = radius * ring / PoolRings;
                for (int s = 0; s < PoolSegments; s++)
                {
                    double a = Math.Tau * s / PoolSegments;
                    double x = cx + Math.Cos(a) * r, z = cz + Math.Sin(a) * r;
                    Vertex(x, Surface(x, z, ledge), z, 0, 1, 0, (float)(2.0 * s / PoolSegments - 1), (float)(r / radius),
                        Pool, seed, (float)(1 - (double)ring / PoolRings), ring == PoolRings ? 0 : 1);
                }
            }
            for (int s = 0; s < PoolSegments; s++)
            {
                int n = (s + 1) % PoolSegments;
                Triangle(centre, centre + 1 + n, centre + 1 + s);
                for (int ring = 1; ring < PoolRings; ring++)
                {
                    int a = centre + 1 + (ring - 1) * PoolSegments + s, b = centre + 1 + (ring - 1) * PoolSegments + n;
                    int c = a + PoolSegments, d = b + PoolSegments;
                    Triangle(a, b, c);
                    Triangle(b, d, c);
                }
            }
        }

        /// <summary>The highest drawn ground within half a lane of (x, z), along the fall (dx, dz) and across it, so the
        /// channel water clears the bumps between its vertices.</summary>
        private double Ground(double x, double z, double dx, double dz)
        {
            double best = double.MinValue;
            foreach (var (a, c) in GroundProbes)
                best = Math.Max(best, lattice.Sample(x + dx * a - dz * c, z + dz * a + dx * c).Height);
            return best;
        }

        private double Surface(double x, double z, FallLedge ledge) => Math.Max(lattice.Sample(x, z).Height, ledge.Foot) + PoolLift;

        /// <summary>Spray bursting up where the sheet lands, and mist drifting off the pool, larger and higher toward the
        /// back; the shader lifts and fades each puff on its own cycle.</summary>
        private void Mist(FallLedge ledge, double halfWidth, double shift, float seed)
        {
            var (lx, lz) = ledge.Landing;
            double height = ledge.Y - ledge.Foot;
            for (int p = 0; p < SplashPuffs; p++)
            {
                double size = halfWidth * (SplashMin + (SplashMax - SplashMin) * Hash(seed, p, 4));
                Puff(ledge, lx + shift, lz, halfWidth * 0.6, ledge.Foot + 0.01, size, Hash(seed, p, 5), height * 0.35, 1, seed, p);
            }
            for (int p = 0; p < MistPuffs; p++)
            {
                double size = halfWidth * (MistMin + (MistMax - MistMin) * Hash(seed, p + 10, 4));
                double y = ledge.Foot + height * MistHeight * Hash(seed, p + 10, 3) * 0.6 + 0.02;
                Puff(ledge, lx + shift, lz, halfWidth, y, size, Hash(seed, p + 10, 5), height * (0.4 + 0.4 * Hash(seed, p + 10, 3)),
                    0.45, seed, p + 10);
            }
        }

        private void Puff(FallLedge ledge, double lx, double lz, double spread, double y, double size, double phase, double rise,
            double density, float seed, int p)
        {
            double dx = ledge.DirectionX, dz = ledge.DirectionZ;
            double side = (Hash(seed, p, 1) - 0.5) * 2 * spread, ahead = (Hash(seed, p, 2) - 0.3) * spread;
            double x = lx - dz * side + dx * ahead, z = lz + dx * side + dz * ahead;
            int start = _mistPositions.Count / 3;
            foreach (var (cu, cv) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
            {
                _mistPositions.AddRange([(float)x, (float)y, (float)z]);
                _mistCorners.AddRange([cu, cv]);
                _mistInfo.AddRange([(float)size, (float)phase, (float)rise, (float)density]);
            }
            _mistIndices.AddRange([start, start + 1, start + 2, start, start + 2, start + 3]);
        }

        private static double Hash(float seed, int i, int salt)
        {
            double s = Math.Sin(seed * 91.7 + i * 12.9898 + salt * 78.233) * 43758.5453;
            return s - Math.Floor(s);
        }

        private void Join(int start, int sections, int lanes)
        {
            for (int s = 0; s < sections - 1; s++)
                for (int l = 0; l < lanes - 1; l++)
                {
                    int v00 = start + s * lanes + l, v01 = v00 + 1, v10 = v00 + lanes, v11 = v10 + 1;
                    Triangle(v00, v10, v01);
                    Triangle(v01, v10, v11);
                }
        }

        private void Triangle(int a, int b, int c) => _indices.AddRange([a, b, c]);

        private void Vertex(double x, double y, double z, double nx, double ny, double nz, float across, float down, float part,
            float seed, float foam, float fade)
        {
            double n = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            _positions.AddRange([(float)x, (float)y, (float)z]);
            _normals.AddRange([(float)(nx / n), (float)(ny / n), (float)(nz / n)]);
            _lanes.AddRange([across, down]);
            _info.AddRange([part, seed, foam, fade]);
        }

        public WaterfallMeshData ToData() => new([.. _positions], [.. _normals], [.. _lanes], [.. _info], [.. _indices],
            [.. _mistPositions], [.. _mistCorners], [.. _mistInfo], [.. _mistIndices]);
    }

    private static double SmoothStep(double edge0, double edge1, double x)
    {
        double t = Math.Clamp((x - edge0) / (edge1 - edge0), 0, 1);
        return t * t * (3 - 2 * t);
    }
}
