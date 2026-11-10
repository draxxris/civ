namespace Ariel.Client.Terrain;

/// <summary>Engine-free river surface arrays for one chunk, wound like <see cref="TerrainMeshData"/>. Per vertex:
/// <see cref="Lanes"/> = (across, along): across is ±1 at the waterline and ±<see cref="RiverMesher.BankExtent"/> at the
/// outer edge of the wet bank, along is arc length; <see cref="Flow"/> = downstream direction (x, z) times speed;
/// <see cref="Info"/> = (fade, white water): fade drops to 0 where sea or lake water covers the river and toward dead
/// ends; white water rises on steep reaches. <see cref="Rock"/> is how close the water is to a cliff wall's rock columns,
/// 1 at a column's side and 0 a short way out from it, where it churns white against the rock.
/// Flow slows to zero at both ends of every curve, so neighbouring curves never disagree about the ripple drift.</summary>
public sealed record RiverMeshData(float[] Positions, float[] Normals, float[] Lanes, float[] Flow, float[] Info, float[] Rock,
    int[] Indices)
{
    /// <summary>Number of vertices.</summary>
    public int VertexCount => Positions.Length / 3;
}

/// <summary>Builds the river water and wet-bank strips of one chunk from <see cref="TerrainField.Rivers"/>. Strips follow
/// each curve with lanes across its width and drape over the rendered terrain (<see cref="TerrainLattice"/>), so the water
/// lies in the carved channel. Waterfalls are drawn by <see cref="WaterfallMesher"/>: a mouth that falls off a sea cliff
/// gets no strip, a river that feeds a fall runs on under the back of its ledge rock, where the fall's tongue lies on top
/// of it, and a river along the foot of a cliff softens in past the far side of its fall, under its plunge pool. A strip piece belongs to
/// the chunk holding its midpoint, which shares wrap copies and dirty signatures with the chunk meshes.</summary>
public static class RiverMesher
{
    /// <summary>Outer edge of the wet bank, in water half widths.</summary>
    public const float BankExtent = 2f;
    private const double SlopeSpan = 0.2;
    /// <summary>Lanes across a strip, in water half widths: the water (with one lane on each waterline, so it stays level
    /// all the way to the shore) and the wet bank beyond.</summary>
    private static readonly float[] LanesAcross = [-BankExtent, -1.3f, -1f, -0.5f, 0f, 0.5f, 1f, 1.3f, BankExtent];

    /// <summary>Lengths of a foot river, from its start, over which its water and its wet bank soften in.</summary>
    private const double FootWaterIn = 0.06, FootBankIn = 0.2;
    /// <summary>Height above the water beyond which a foot river's outer bank lanes count as climbing a cliff rather than
    /// its bank, and are kept at the water's level too.</summary>
    private const double LiftedLane = 0.06;
    /// <summary>How far out from the side of a rock column the water it stands in churns and streaks white (see
    /// <see cref="RiverMeshData.Rock"/>). Wider than a lane, so the contact falls off across the water the camera sees
    /// rather than within one triangle.</summary>
    private const double RockFoam = 0.14;
    /// <summary>Reach behind <see cref="FallLedge.BrimReach"/> over which the brim a river keeps up to before its fall
    /// eases in, and how far below the brim it starts.</summary>
    private const double BrimEase = 0.15;

    /// <summary>The chunk's river surface, or null when no river passes through it.</summary>
    public static RiverMeshData? Build(TerrainChunk chunk, TerrainField field, int k = TerrainMesher.DefaultResolution) =>
        Build(chunk, field, new TerrainLattice(field, k));

    /// <summary>The chunk's river surface over terrain whose resolution varies by chunk (see
    /// <see cref="TerrainChunks.ResolutionAt"/>), or null when no river passes through it.</summary>
    public static RiverMeshData? Build(TerrainChunk chunk, TerrainField field, Func<double, double, int> resolutionAt) =>
        Build(chunk, field, new TerrainLattice(field, resolutionAt));

    private static RiverMeshData? Build(TerrainChunk chunk, TerrainField field, TerrainLattice lattice)
    {
        var mesh = new Strips(field, lattice, RocksNear(chunk, field));
        double period = field.Period;
        double[] shifts = field.Snapshot.Grid.Spec.Wrap ? [-period, 0, period] : [0];
        foreach (var curve in field.Rivers.Curves)
            foreach (double shift in shifts)
                if (Overlaps(curve, shift, chunk)) mesh.Add(curve, shift, chunk);
        return mesh.Indices.Count == 0 ? null : mesh.ToData();
    }

    /// <summary>The wall columns a strip piece owned by the chunk can touch (pieces reach a little past it), with their
    /// waterline radius, in the chunk's frame (on a wrapping map, those just across the seam moved by a period) and in
    /// order of x.</summary>
    private static (double X, double Z, double Radius)[] RocksNear(TerrainChunk chunk, TerrainField field)
    {
        const double reach = 0.5;
        double x0 = chunk.X0 - reach, x1 = chunk.X1 + reach, z0 = chunk.Z0 - reach, z1 = chunk.Z1 + reach, period = field.Period;
        var rocks = field.CliffRocks(x0, x1, z0, z1);
        if (field.Snapshot.Grid.Spec.Wrap)
        {
            if (x0 < 0) rocks = rocks.Concat(field.CliffRocks(x0 + period, period, z0, z1));
            if (x1 > period) rocks = rocks.Concat(field.CliffRocks(0, x1 - period, z0, z1));
        }
        double centre = (chunk.X0 + chunk.X1) / 2;
        return [.. rocks.Select(r => (X: field.Snapshot.Nearest(r.X, centre), r.Z, Radius: TerrainField.CliffRockRadius * r.Scale)).OrderBy(r => r.X)];
    }

    private static bool Overlaps(RiverCurve curve, double shift, TerrainChunk chunk)
    {
        double minX = curve.X.Min() + shift, maxX = curve.X.Max() + shift, minZ = curve.Z.Min(), maxZ = curve.Z.Max();
        return maxX >= chunk.X0 && minX <= chunk.X1 && maxZ >= chunk.Z0 && minZ <= chunk.Z1;
    }

    private sealed class Strips(TerrainField field, TerrainLattice lattice, (double X, double Z, double Radius)[] rocks)
    {
        private readonly List<float> _positions = [], _normals = [], _lanes = [], _flow = [], _info = [], _rock = [];
        // How far from a point a column's foot can be and still have its side within the foam's reach.
        private readonly double _rockReach = RockFoam + (rocks.Length == 0 ? 0 : rocks.Max(r => r.Radius));
        public List<int> Indices { get; } = [];

        /// <summary>Emit every run of consecutive curve pieces whose midpoints lie in the chunk.</summary>
        public void Add(RiverCurve curve, double shift, TerrainChunk chunk)
        {
            // A mouth off a sea cliff is nothing but its waterfall.
            if (curve.Fall is { Course: FallCourse.Sea }) return;
            int sign = curve.Downstream != 0 ? curve.Downstream
                : field.HeightAt(curve.X[0], curve.Z[0]) >= field.HeightAt(curve.X[^1], curve.Z[^1]) ? 1 : -1;
            int i = 0;
            while (i < curve.Count - 1)
            {
                if (!Owned(curve, i, shift, chunk)) { i++; continue; }
                int end = i;
                while (end < curve.Count - 1 && Owned(curve, end, shift, chunk)) end++;
                Strip(curve, i, end, shift, sign);
                i = end;
            }
        }

        private static bool Owned(RiverCurve c, int i, double shift, TerrainChunk chunk) =>
            chunk.Contains((c.X[i] + c.X[i + 1]) / 2 + shift, (c.Z[i] + c.Z[i + 1]) / 2);

        /// <summary>Sections at samples first..last, joined lane by lane. A curve that feeds a fall runs on past the
        /// sample at that end, onto the ledge rock (see <see cref="RunOn"/>).</summary>
        private void Strip(RiverCurve c, int first, int last, double shift, int sign)
        {
            int start = _positions.Count / 3, sections = 0;
            if (first == 0 && c.Feeds != null && c.FeedsAtStart) sections += RunOn(c, shift, sign);
            for (int i = first; i <= last; i++, sections++) Section(c, i, shift, sign);
            if (last == c.Count - 1 && c.Feeds != null && !c.FeedsAtStart) sections += RunOn(c, shift, sign);
            Join(start, sections, LanesAcross.Length);
        }

        /// <summary>Triangles between consecutive sections of <paramref name="lanes"/> vertices each.</summary>
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

        /// <summary>One cross-section of a strip: centre, unit tangent along the curve, water half width, arc length,
        /// flow speed, white water, and how much of the water shows there.</summary>
        private readonly record struct Cut(double X, double Z, double TX, double TZ, double Width, double Arc, double Speed,
            float Foam, double Dry, double Bend, double FromStart = double.MaxValue);

        private void Section(RiverCurve c, int i, double shift, int sign)
        {
            double tx = c.TX[i], tz = c.TZ[i];
            double cx = c.X[i] + shift, cz = c.Z[i];
            // Slope along the exact tangent, so curves meeting at a node agree there.
            double slope = Math.Abs(field.HeightAt(cx + tx * SlopeSpan, cz + tz * SlopeSpan)
                - field.HeightAt(cx - tx * SlopeSpan, cz - tz * SlopeSpan)) / (2 * SlopeSpan);
            double fromEnd = Math.Min(c.Arc[i], c.Arc[^1] - c.Arc[i]);
            double speed = (0.5 + Math.Min(2 * slope, 1.5)) * SmoothStep(0, 0.3, fromEnd);
            float foam = (float)SmoothStep(0.25, 0.7, slope);
            double dry = (c.StartsDry ? SmoothStep(0, 0.35, c.Arc[i]) : 1) * (c.EndsDry ? SmoothStep(0, 0.35, c.Arc[^1] - c.Arc[i]) : 1);
            // A river along the foot of a cliff starts against the rocks past the far side of its fall; it softens in
            // there, its water quickly and its wider wet bank more gently, so its end never shows as a cut.
            double start = c.Fall is { AlongFoot: true } fall ? (fall.AtStart ? c.Arc[i] : c.Arc[^1] - c.Arc[i]) : double.MaxValue;
            Lanes(c, new Cut(cx, cz, tx, tz, c.Width[i], c.Arc[i], speed, foam, dry, Bend(c, i), start), shift, sign);
        }

        /// <summary>The sections a curve that feeds a fall runs on by past its end sample: straight along the fall, at
        /// the width it reaches the lip with, from the fall's corner up under the back of the ledge rock (see
        /// <see cref="WaterfallMesher.RiverRunsTo"/>), where the fall's tongue takes over on top. The corner can lie well
        /// behind the brink, and without this the ground between would show. Returns the number of sections added, which
        /// come before the curve's own when it feeds the fall at its start.</summary>
        private int RunOn(RiverCurve c, double shift, int sign)
        {
            var fall = c.Feeds!;
            var ledge = field.LedgeOf(fall);
            bool atStart = c.FeedsAtStart;
            int e = atStart ? 0 : c.Count - 1;
            double dx = ledge.DirectionX, dz = ledge.DirectionZ;
            // The ledge is in the frame of the fall's own curve, which may lie a period away from this one's.
            double along = (c.X[e] - field.Snapshot.Nearest(ledge.X, c.X[e])) * dx + (c.Z[e] - ledge.Z) * dz;
            double length = WaterfallMesher.RiverRunsTo(ledge) - along;
            if (length <= 0) return 0;
            int n = Math.Max(1, (int)Math.Ceiling(length / RiverNetwork.SampleStep));
            // The strip's tangent keeps the curve's orientation, so its lanes line up with the curve's.
            double orient = c.TX[e] * dx + c.TZ[e] * dz >= 0 ? 1 : -1;
            for (int k = 1; k <= n; k++)
            {
                // Before the curve's own sections, the furthest comes first.
                int step = atStart ? n + 1 - k : k;
                double s = length * step / n;
                double arc = atStart ? c.Arc[0] - s : c.Arc[^1] + s;
                Lanes(c, new Cut(c.X[e] + dx * s + shift, c.Z[e] + dz * s, dx * orient, dz * orient, c.Width[e], arc, 0, 0, 1, 0),
                    shift, sign);
            }
            return n;
        }

        private void Lanes(RiverCurve c, Cut cut, double shift, int sign)
        {
            double tx = cut.TX, tz = cut.TZ, cx = cut.X, cz = cut.Z;
            double fx = tx * sign * cut.Speed, fz = tz * sign * cut.Speed;
            // Water is level across: every water lane lies at the ground of the lower waterline, and where the ground
            // rises above that level (towards the higher bank, or the foot of a wall) it hides the water, so the shore
            // follows the ground instead of the water tilting up onto it.
            double water = Math.Min(lattice.Sample(At(cut, -1).X, At(cut, -1).Z).Height, lattice.Sample(At(cut, 1).X, At(cut, 1).Z).Height);
            // A river along the foot of a cliff runs its inner half under the wall. Its bank lanes on that side keep to the
            // water's level, so they slip under the face and the rocks instead of climbing onto the brink, and so do its
            // outer ones where they would climb a cliff rather than a bank.
            double wallSide = c.Foot is { } foot ? (-tz * foot.DirectionX + tx * foot.DirectionZ > 0 ? -1 : 1) : 0;
            // A river that feeds a fall keeps its water up to the ledge's brim over its last reach (see FallLedge.Brim).
            if (c.Feeds is { } fed)
            {
                var ledge = field.LedgeOf(fed);
                double behind = -((cx - field.Snapshot.Nearest(ledge.X, cx)) * ledge.DirectionX + (cz - ledge.Z) * ledge.DirectionZ);
                // Full from BrimReach behind the brink on, easing in over the reach before, so the water never steps.
                water = Math.Max(water, ledge.Brim - BrimEase * SmoothStep(FallLedge.BrimReach, FallLedge.BrimReach + BrimEase, behind));
            }
            foreach (float across in LanesAcross)
            {
                var (x, z) = At(cut, across);
                var (y, nx, ny, nz) = lattice.Sample(x, z);
                bool wet = Math.Abs(across) <= 1;
                if (wet || wallSide != 0 && y > (across * wallSide > 0 ? water : water + LiftedLane)) (y, nx, ny, nz) = (water, 0, 1, 0);
                // Bank lanes keep the ground's shading, turning up towards the water.
                double flat = Math.Clamp((1.5 - Math.Abs(across)) / 0.85, 0, 1);
                nx *= 1 - flat; nz *= 1 - flat; ny += (1 - ny) * flat;
                double shows = cut.Dry * SmoothStep(0, wet ? FootWaterIn : FootBankIn, cut.FromStart);
                Vertex(x, y, z, nx, ny, nz, across, cut.Arc, fx, fz, Fade(x, y, z) * shows, cut.Foam, Math.Abs(across) <= 1.5 ? Rock(x, z) : 0);
            }
        }

        /// <summary>The point <paramref name="across"/> water half widths across a section from its centre. Lanes on the
        /// inside of a tight bend stop short of its centre of curvature, so the strip never folds.</summary>
        private static (double X, double Z) At(Cut cut, double across)
        {
            double lateral = across * cut.Width;
            if (lateral * cut.Bend > 0) lateral = Math.Sign(lateral) * Math.Min(Math.Abs(lateral), 0.85 / Math.Abs(cut.Bend));
            return (cut.X - cut.TZ * lateral, cut.Z + cut.TX * lateral);
        }

        private void Vertex(double x, double y, double z, double nx, double ny, double nz, double across, double along,
            double fx, double fz, double fade, double foam, double rock)
        {
            double n = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            _positions.AddRange([(float)x, (float)y, (float)z]);
            _normals.AddRange([(float)(nx / n), (float)(ny / n), (float)(nz / n)]);
            _lanes.AddRange([(float)across, (float)along]);
            _flow.AddRange([(float)fx, (float)fz]);
            _info.AddRange([(float)fade, (float)foam]);
            _rock.Add((float)rock);
        }

        /// <summary>How hard the water at (x, z) runs against a cliff wall's rock column: 1 at the side of the nearest column,
        /// falling to 0 <see cref="RockFoam"/> out from it.</summary>
        private double Rock(double x, double z)
        {
            // Only columns within the foam's reach of the point count; they lie in a window of the x-sorted list.
            double reach = _rockReach, gap = RockFoam;
            int i = FirstAtOrAfter(x - reach);
            for (; i < rocks.Length && rocks[i].X <= x + reach; i++)
            {
                double dx = rocks[i].X - x, dz = rocks[i].Z - z;
                if (Math.Abs(dz) > reach) continue;
                gap = Math.Min(gap, Math.Sqrt(dx * dx + dz * dz) - rocks[i].Radius);
            }
            return 1 - SmoothStep(0, RockFoam, gap);
        }

        private int FirstAtOrAfter(double x)
        {
            int lo = 0, hi = rocks.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (rocks[mid].X < x) lo = mid + 1; else hi = mid;
            }
            return lo;
        }

        /// <summary>Signed curvature at sample i: positive when the curve turns toward its +across side (-tz, tx).</summary>
        private static double Bend(RiverCurve c, int i)
        {
            int prev = Math.Max(0, i - 1), next = Math.Min(c.Count - 1, i + 1);
            double ds = c.Arc[next] - c.Arc[prev];
            if (ds <= 1e-9) return 0;
            return ((c.TX[next] - c.TX[prev]) * -c.TZ[i] + (c.TZ[next] - c.TZ[prev]) * c.TX[i]) / ds;
        }

        /// <summary>Sea and lake water already covers river ground below sea level next to a water tile.</summary>
        private double Fade(double x, double y, double z) => y < 0 && field.NearWater(x, z) ? 1 - Math.Min(1, -y / 0.03) : 1;

        /// <summary>Wind every triangle counter-clockwise seen from above, like the chunk meshes.</summary>
        private void Triangle(int a, int b, int c)
        {
            double ax = _positions[a * 3], az = _positions[a * 3 + 2];
            double ux = _positions[b * 3] - ax, uz = _positions[b * 3 + 2] - az;
            double vx = _positions[c * 3] - ax, vz = _positions[c * 3 + 2] - az;
            bool up = uz * vx - ux * vz > 0;
            Indices.AddRange(up ? [a, b, c] : [a, c, b]);
        }

        public RiverMeshData ToData() =>
            new([.. _positions], [.. _normals], [.. _lanes], [.. _flow], [.. _info], [.. _rock], [.. Indices]);
    }

    private static double SmoothStep(double edge0, double edge1, double x)
    {
        double t = Math.Clamp((x - edge0) / (edge1 - edge0), 0, 1);
        return t * t * (3 - 2 * t);
    }
}
