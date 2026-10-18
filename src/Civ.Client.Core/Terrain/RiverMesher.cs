namespace Civ.Client.Terrain;

/// <summary>Engine-free river surface arrays for one chunk, wound like <see cref="TerrainMeshData"/>. Per vertex:
/// <see cref="Lanes"/> = (across, along): across is ±1 at the waterline and ±<see cref="RiverMesher.BankExtent"/> at the
/// outer edge of the wet bank, along is arc length; <see cref="Flow"/> = downstream direction (x, z) times speed;
/// <see cref="Info"/> = (fade, white water): fade drops to 0 where sea or lake water covers the river and toward dead ends.
/// Flow slows to zero at both ends of every curve, so neighbouring curves never disagree about the ripple drift.</summary>
public sealed record RiverMeshData(float[] Positions, float[] Normals, float[] Lanes, float[] Flow, float[] Info, int[] Indices)
{
    /// <summary>Number of vertices.</summary>
    public int VertexCount => Positions.Length / 3;
}

/// <summary>Builds the river water and wet-bank strips of one chunk from <see cref="TerrainField.Rivers"/>. Strips follow
/// each curve with lanes across its width and drape over the rendered terrain (<see cref="TerrainLattice"/>), so the water
/// lies in the carved channel. A strip piece belongs to the chunk holding its midpoint, which shares wrap copies and dirty
/// signatures with the chunk meshes.</summary>
public static class RiverMesher
{
    /// <summary>Outer edge of the wet bank, in water half widths.</summary>
    public const float BankExtent = 2f;
    private const double SlopeSpan = 0.2;
    private static readonly float[] LanesAcross = [-BankExtent, -1.3f, -0.65f, 0f, 0.65f, 1.3f, BankExtent];

    /// <summary>The chunk's river surface, or null when no river passes through it.</summary>
    public static RiverMeshData? Build(TerrainChunk chunk, TerrainField field, int k = TerrainMesher.DefaultResolution)
    {
        var mesh = new Strips(field, new TerrainLattice(field, k));
        double period = field.Period;
        double[] shifts = field.Snapshot.Grid.Spec.Wrap ? [-period, 0, period] : [0];
        foreach (var curve in field.Rivers.Curves)
            foreach (double shift in shifts)
                if (Overlaps(curve, shift, chunk)) mesh.Add(curve, shift, chunk);
        return mesh.Indices.Count == 0 ? null : mesh.ToData();
    }

    private static bool Overlaps(RiverCurve curve, double shift, TerrainChunk chunk)
    {
        double minX = curve.X.Min() + shift, maxX = curve.X.Max() + shift, minZ = curve.Z.Min(), maxZ = curve.Z.Max();
        return maxX >= chunk.X0 && minX <= chunk.X1 && maxZ >= chunk.Z0 && minZ <= chunk.Z1;
    }

    private sealed class Strips(TerrainField field, TerrainLattice lattice)
    {
        private readonly List<float> _positions = [], _normals = [], _lanes = [], _flow = [], _info = [];
        public List<int> Indices { get; } = [];

        /// <summary>Emit every run of consecutive curve pieces whose midpoints lie in the chunk.</summary>
        public void Add(RiverCurve curve, double shift, TerrainChunk chunk)
        {
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

        /// <summary>Sections at samples first..last, joined lane by lane.</summary>
        private void Strip(RiverCurve c, int first, int last, double shift, int sign)
        {
            int lanes = LanesAcross.Length;
            int start = _positions.Count / 3;
            for (int i = first; i <= last; i++) Section(c, i, shift, sign);
            for (int s = 0; s < last - first; s++)
                for (int l = 0; l < lanes - 1; l++)
                {
                    int v00 = start + s * lanes + l, v01 = v00 + 1, v10 = v00 + lanes, v11 = v10 + 1;
                    Triangle(v00, v10, v01);
                    Triangle(v01, v10, v11);
                }
        }

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
            double bend = Bend(c, i);
            foreach (float across in LanesAcross)
            {
                // Lanes on the inside of a tight bend stop short of its centre of curvature, so the strip never folds.
                double lateral = across * c.Width[i];
                if (lateral * bend > 0) lateral = Math.Sign(lateral) * Math.Min(Math.Abs(lateral), 0.85 / Math.Abs(bend));
                double x = cx - tz * lateral, z = cz + tx * lateral;
                var (y, nx, ny, nz) = lattice.Sample(x, z);
                // Water lanes face straight up; bank lanes keep the ground's shading.
                double flat = Math.Clamp((1.5 - Math.Abs(across)) / 0.85, 0, 1);
                nx *= 1 - flat; nz *= 1 - flat; ny += (1 - ny) * flat;
                double n = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                _positions.AddRange([(float)x, (float)y, (float)z]);
                _normals.AddRange([(float)(nx / n), (float)(ny / n), (float)(nz / n)]);
                _lanes.AddRange([across, (float)c.Arc[i]]);
                _flow.AddRange([(float)(tx * sign * speed), (float)(tz * sign * speed)]);
                _info.AddRange([(float)(Fade(x, y, z) * dry), foam]);
            }
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
            new([.. _positions], [.. _normals], [.. _lanes], [.. _flow], [.. _info], [.. Indices]);
    }

    private static double SmoothStep(double edge0, double edge1, double x)
    {
        double t = Math.Clamp((x - edge0) / (edge1 - edge0), 0, 1);
        return t * t * (3 - 2 * t);
    }
}
