namespace Ariel.Client.Terrain;

/// <summary>Engine-free mesh arrays: xyz positions, unit normals and cavity per vertex (world coordinates), and triangle
/// indices. Cavity is the height's Laplacian (per world unit) measured over two lattice steps: positive in valleys and
/// gullies, negative on ridges and summits. Triangles wind like <c>MeshBuilder</c>:
/// <c>(p1 − p0) × (p2 − p0)</c> points up (+y).</summary>
public sealed record TerrainMeshData(float[] Positions, float[] Normals, float[] Cavity, int[] Indices)
{
    /// <summary>Number of vertices.</summary>
    public int VertexCount => Positions.Length / 3;
}

/// <summary>Samples the height field on global triangle lattices. At resolution k, vertex (a, b) is at
/// <c>x = a·√3/k + (b odd ? √3/(2k) : 0)</c>, <c>z = b·1.5/k</c>; the step divides a tile column and row exactly, and the
/// lattice of k contains the lattice of every divisor of k. Each chunk picks its own resolution (finer around mountains,
/// see <see cref="ChunkResolution"/>). Odd rows also get a vertex on the chunk's west and east edge, so every chunk edge
/// is a straight line. Where a finer chunk meets a coarser one, the finer edge follows the coarser edge exactly, so
/// neighbouring chunks (and the two ends of the wrap) never crack and their shared vertices match.</summary>
public static class TerrainMesher
{
    /// <summary>Default lattice resolution: lattice steps per tile column (about 81 vertices per hex).</summary>
    public const int DefaultResolution = 9;

    /// <summary>Resolution of chunks that mountains reach: three times as fine, for arêtes, gullies and crags.</summary>
    public const int PeakResolution = 27;

    private static readonly double Sqrt3 = Math.Sqrt(3);

    /// <summary>World x of lattice vertex (a, b) at resolution k. The rational step is divided out first, so the same
    /// point gives bit-identical coordinates at every resolution whose lattice contains it.</summary>
    public static double LatticeX(int a, int b, int k) => HalfStepX(2 * a + (b & 1), k);

    /// <summary>World z of lattice row b at resolution k.</summary>
    public static double LatticeZ(int b, int k) => 1.5 * (b / (double)k);

    /// <summary>World x of h half steps at resolution k.</summary>
    private static double HalfStepX(int h, int k) => Sqrt3 * (h / (2.0 * k));

    /// <summary>Mesh one chunk with the same resolution as all its neighbours.</summary>
    public static TerrainMeshData Build(TerrainChunk chunk, TerrainField field, int k = DefaultResolution) =>
        Build(chunk, field, ChunkResolution.Uniform(k));

    /// <summary>Mesh one chunk at its resolution. Each lattice height is sampled once (plus a two-vertex ring); interior
    /// normals are central differences of those heights over the neighbouring lattice vertices, and cavity is their
    /// Laplacian over the six vertices two steps away. Edge vertices use the coarsest resolution of the chunks that
    /// share them, and edge vertices that a coarser neighbour lacks interpolate its edge, so shared vertices agree
    /// exactly whatever each chunk's resolution.</summary>
    public static TerrainMeshData Build(TerrainChunk chunk, TerrainField field, ChunkResolution resolution)
    {
        if (resolution.K < 1) throw new ArgumentOutOfRangeException(nameof(resolution));
        return new ChunkMesher(chunk, field, resolution).Build();
    }

    /// <summary>Laplacian of the lattice heights at vertex (a, b) over the six vertices two steps away (distance 2√3/k),
    /// so features finer than the lattice do not dominate it. Rows b ± 2 share b's offset, so their neighbours are a ± 1.</summary>
    public static double LatticeCavity(Func<int, int, double> height, int a, int b, int k)
    {
        double spacing = 2 * Sqrt3 / k;
        double sum = height(a - 2, b) + height(a + 2, b) + height(a - 1, b - 2) + height(a + 1, b - 2)
            + height(a - 1, b + 2) + height(a + 1, b + 2) - 6 * height(a, b);
        return sum * 2 / (3 * spacing * spacing);
    }

    /// <summary>Unit normal of lattice vertex (a, b): central differences of the lattice heights around it. Rows b ± 1 are
    /// offset by half a step; the mean of the two vertices either side lies straight north/south.</summary>
    public static (double X, double Y, double Z) LatticeNormal(Func<int, int, double> height, int a, int b, int k)
    {
        double stepX = Sqrt3 / k, stepZ = 1.5 / k;
        int w = (b & 1) == 0 ? a - 1 : a;
        double north = (height(w, b - 1) + height(w + 1, b - 1)) / 2, south = (height(w, b + 1) + height(w + 1, b + 1)) / 2;
        double dx = (height(a + 1, b) - height(a - 1, b)) / (2 * stepX), dz = (south - north) / (2 * stepZ);
        double len = Math.Sqrt(dx * dx + 1 + dz * dz);
        return (-dx / len, 1 / len, -dz / len);
    }

    /// <summary>One vertex's height, normal and cavity.</summary>
    private readonly record struct Sample(double Height, double NX, double NY, double NZ, double Cavity)
    {
        public static Sample Lerp(in Sample a, in Sample b, double t)
        {
            double nx = a.NX + (b.NX - a.NX) * t, ny = a.NY + (b.NY - a.NY) * t, nz = a.NZ + (b.NZ - a.NZ) * t;
            double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            return new(a.Height + (b.Height - a.Height) * t, nx / len, ny / len, nz / len, a.Cavity + (b.Cavity - a.Cavity) * t);
        }
    }

    /// <summary>Builds one chunk. Vertices are addressed by (h, b): h half steps east of x = 0 and lattice row b, both at
    /// the chunk's resolution. A vertex is on the lattice when h and b have the same parity; otherwise it is an odd row's
    /// vertex on the west or east edge.</summary>
    private sealed class ChunkMesher
    {
        private readonly TerrainChunk _chunk;
        private readonly TerrainField _field;
        private readonly ChunkResolution _resolution;
        private readonly int _k, _a0, _a1, _b0, _b1, _rx;
        private readonly double[] _heights;

        public ChunkMesher(TerrainChunk chunk, TerrainField field, ChunkResolution resolution)
        {
            _chunk = chunk;
            _field = field;
            _resolution = resolution;
            _k = resolution.K;
            _a0 = chunk.UnitX0 * _k; _a1 = chunk.UnitX1 * _k; _b0 = chunk.UnitZ0 * _k; _b1 = chunk.UnitZ1 * _k;
            // Heights over the chunk lattice and a two-vertex ring, indexed [(b - b0 + 2) * rx + (a - a0 + 2)].
            _rx = _a1 - _a0 + 5;
            _heights = new double[_rx * (_b1 - _b0 + 5)];
            for (int b = _b0 - 2; b <= _b1 + 2; b++)
            {
                double z = LatticeZ(b, _k);
                for (int a = _a0 - 2; a <= _a1 + 2; a++)
                    _heights[(b - _b0 + 2) * _rx + (a - _a0 + 2)] = field.HeightAt(LatticeX(a, b, _k), z);
            }
        }

        private double H(int a, int b) => _heights[(b - _b0 + 2) * _rx + (a - _a0 + 2)];

        /// <summary>Half-step positions of row b's vertices, west to east.</summary>
        private List<int> Row(int b)
        {
            var row = new List<int>(_a1 - _a0 + 3);
            if ((b & 1) == 0)
                for (int a = _a0; a <= _a1; a++) row.Add(2 * a);
            else
            {
                row.Add(2 * _a0);
                for (int a = _a0; a < _a1; a++) row.Add(2 * a + 1);
                row.Add(2 * _a1);
            }
            return row;
        }

        public TerrainMeshData Build()
        {
            var positions = new List<float>();
            var normals = new List<float>();
            var cavity = new List<float>();
            var indices = new List<int>();
            int previousStart = -1;
            List<int>? previous = null;
            for (int b = _b0; b <= _b1; b++)
            {
                var row = Row(b);
                int start = cavity.Count;
                double z = LatticeZ(b, _k);
                for (int i = 0; i < row.Count; i++)
                {
                    int h = row[i];
                    var sample = Vertex(h, b, i == 0 || i == row.Count - 1 || b == _b0 || b == _b1);
                    positions.Add((float)HalfStepX(h, _k)); positions.Add((float)sample.Height); positions.Add((float)z);
                    normals.Add((float)sample.NX); normals.Add((float)sample.NY); normals.Add((float)sample.NZ);
                    cavity.Add((float)sample.Cavity);
                }
                if (previous != null) Zip(previous, previousStart, row, start, indices);
                previous = row;
                previousStart = start;
            }
            return new([.. positions], [.. normals], [.. cavity], [.. indices]);
        }

        /// <summary>Triangulate the strip between a northern row and the row south of it, walking both west to east and
        /// always advancing the row whose next vertex lies further west. Inside the chunk this is the regular lattice.</summary>
        private static void Zip(List<int> north, int northStart, List<int> south, int southStart, List<int> indices)
        {
            int i = 0, j = 0;
            while (i < north.Count - 1 || j < south.Count - 1)
            {
                bool advanceNorth = j == south.Count - 1 || (i < north.Count - 1 && north[i + 1] <= south[j + 1]);
                if (advanceNorth)
                {
                    indices.Add(northStart + i); indices.Add(southStart + j); indices.Add(northStart + i + 1);
                    i++;
                }
                else
                {
                    indices.Add(northStart + i); indices.Add(southStart + j); indices.Add(southStart + j + 1);
                    j++;
                }
            }
        }

        private Sample Vertex(int h, int b, bool edge)
        {
            if (!edge) return Lattice(h, b);
            bool west = h == 2 * _a0, east = h == 2 * _a1, north = b == _b0, south = b == _b1;
            var r = _resolution;
            int coarsest = _k;
            if (west) coarsest = Math.Min(coarsest, r.West);
            if (east) coarsest = Math.Min(coarsest, r.East);
            if (north) coarsest = Math.Min(coarsest, r.North);
            if (south) coarsest = Math.Min(coarsest, r.South);
            if (west && north) coarsest = Math.Min(coarsest, r.NorthWest);
            if (east && north) coarsest = Math.Min(coarsest, r.NorthEast);
            if (west && south) coarsest = Math.Min(coarsest, r.SouthWest);
            if (east && south) coarsest = Math.Min(coarsest, r.SouthEast);
            if (coarsest == _k) return ((h ^ b) & 1) == 0 ? Lattice(h, b) : Direct(h, b, _k);
            if (_k % coarsest != 0) throw new InvalidOperationException($"Resolution {_k} is not a multiple of {coarsest}");
            int m = _k / coarsest;
            if (west || east)
            {
                // A straight vertical edge: coarse vertices on every coarse row, interpolated in between.
                if (b % m == 0 || north || south) return Coarse(h, b, coarsest);
                int below = b - Mod(b, m);
                return Sample.Lerp(Vertex(h, below, true), Vertex(h, below + m, true), Mod(b, m) / (double)m);
            }
            // A horizontal edge: the coarse row's vertices are its lattice points plus its two edge vertices.
            int parity = (b / m) & 1;
            if (h % m == 0 && (Mod(h / m, 2) == parity)) return Coarse(h, b, coarsest);
            int west0 = 2 * _a0, east0 = 2 * _a1;
            // Coarse vertices sit every 2m half steps, offset by the coarse row's parity.
            int lower = FloorTo(h - parity * m, 2 * m) + parity * m;
            int lo = Math.Max(lower, west0), hi = Math.Min(lower + 2 * m, east0);
            return Sample.Lerp(Vertex(lo, b, true), Vertex(hi, b, true), (h - lo) / (double)(hi - lo));
        }

        /// <summary>A vertex that the coarser lattice of resolution c also has: its lattice normal and cavity at c, or a
        /// direct one when it is a coarse odd row's edge vertex.</summary>
        private Sample Coarse(int h, int b, int c)
        {
            int m = _k / c, hc = h / m, bc = b / m;
            if (((hc ^ bc) & 1) != 0) return Direct(hc, bc, c);
            int a = (hc - (bc & 1)) / 2;
            double Height(int va, int vb) => _field.HeightAt(LatticeX(va, vb, c), LatticeZ(vb, c));
            var n = LatticeNormal(Height, a, bc, c);
            return new(Height(a, bc), n.X, n.Y, n.Z, LatticeCavity(Height, a, bc, c));
        }

        private Sample Lattice(int h, int b)
        {
            int a = (h - (b & 1)) / 2;
            var n = LatticeNormal(H, a, b, _k);
            return new(H(a, b), n.X, n.Y, n.Z, LatticeCavity(H, a, b, _k));
        }

        /// <summary>An odd row's edge vertex at resolution c: the field's own height, and a normal and cavity from field
        /// differences at that resolution's spacing, which every chunk sharing the vertex computes alike.</summary>
        private Sample Direct(int h, int b, int c)
        {
            double x = HalfStepX(h, c), z = LatticeZ(b, c);
            double step = Sqrt3 / c, stepZ = 1.5 / c;
            double f(double dx, double dz) => _field.HeightAt(x + dx, z + dz);
            double y = f(0, 0);
            double gx = (f(step, 0) - f(-step, 0)) / (2 * step), gz = (f(0, stepZ) - f(0, -stepZ)) / (2 * stepZ);
            double len = Math.Sqrt(gx * gx + 1 + gz * gz);
            double ring = 0, r = 2 * step;
            for (int i = 0; i < 6; i++) ring += f(r * Math.Cos(i * Math.PI / 3), r * Math.Sin(i * Math.PI / 3));
            return new(y, -gx / len, 1 / len, -gz / len, (ring - 6 * y) * 2 / (3 * r * r));
        }

        private static int Mod(int x, int m) => ((x % m) + m) % m;
        private static int FloorTo(int x, int m) => x - Mod(x, m);
    }
}

/// <summary>The chunk meshes' surface between lattice vertices: the height and smooth normal that the rendered triangles
/// interpolate at any point, so surfaces draped over the terrain (river water) sit on what is drawn rather than on the
/// finer field. Each point uses the lattice of its chunk's resolution; along a chunk edge that follows a coarser neighbour,
/// and between an odd row's last lattice vertex and the chunk edge, the drawn surface differs slightly. Caches lattice
/// heights; use one instance per thread.</summary>
public sealed class TerrainLattice
{
    private static readonly double Sqrt3 = Math.Sqrt(3);
    private readonly TerrainField _field;
    private readonly Func<double, double, int> _resolutionAt;
    private readonly Dictionary<(int, int, int), double> _heights = new();

    /// <summary>One resolution everywhere.</summary>
    public TerrainLattice(TerrainField field, int k = TerrainMesher.DefaultResolution) : this(field, (_, _) => k)
    {
    }

    /// <summary>Each point at the resolution of the chunk that holds it.</summary>
    public TerrainLattice(TerrainField field, Func<double, double, int> resolutionAt)
    {
        _field = field;
        _resolutionAt = resolutionAt;
    }

    private double Height(int a, int b, int k)
    {
        if (!_heights.TryGetValue((a, b, k), out double h))
            _heights[(a, b, k)] = h = _field.HeightAt(TerrainMesher.LatticeX(a, b, k), TerrainMesher.LatticeZ(b, k));
        return h;
    }

    /// <summary>Height and normal of the rendered terrain at (x, z), interpolated across its lattice triangle.</summary>
    public (double Height, double NX, double NY, double NZ) Sample(double x, double z)
    {
        int k = _resolutionAt(x, z);
        double step = Sqrt3 / k;
        double v = z / (1.5 / k);
        int b = (int)Math.Floor(v);
        double fz = v - b;
        bool even = (b & 1) == 0;
        double u = even ? x / step - 0.5 * fz : (x - step / 2) / step + 0.5 * fz;
        int a = (int)Math.Floor(u);
        double fx = u - a;
        // Weights of v00, v10, v01, v11, following the mesher's triangle split.
        double w00, w10, w01, w11;
        if (even && fx + fz <= 1) (w00, w10, w01, w11) = (1 - fx - fz, fx, fz, 0);
        else if (even) (w00, w10, w01, w11) = (0, 1 - fz, 1 - fx, fx + fz - 1);
        else if (fz >= fx) (w00, w10, w01, w11) = (1 - fz, 0, fz - fx, fx);
        else (w00, w10, w01, w11) = (1 - fx, fx - fz, 0, fz);
        double h = 0, nx = 0, ny = 0, nz = 0;
        double H(int va, int vb) => Height(va, vb, k);
        Add(a, b, w00); Add(a + 1, b, w10); Add(a, b + 1, w01); Add(a + 1, b + 1, w11);
        double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
        return (h, nx / len, ny / len, nz / len);

        void Add(int va, int vb, double w)
        {
            if (w <= 0) return;
            h += w * H(va, vb);
            var n = TerrainMesher.LatticeNormal(H, va, vb, k);
            nx += w * n.X; ny += w * n.Y; nz += w * n.Z;
        }
    }
}
