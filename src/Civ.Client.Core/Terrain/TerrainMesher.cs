namespace Civ.Client.Terrain;

/// <summary>Engine-free mesh arrays: xyz positions and unit normals per vertex (world coordinates), and triangle indices.
/// Triangles wind like <c>MeshBuilder</c>: <c>(p1 − p0) × (p2 − p0)</c> points up (+y).</summary>
public sealed record TerrainMeshData(float[] Positions, float[] Normals, int[] Indices)
{
    /// <summary>Number of vertices.</summary>
    public int VertexCount => Positions.Length / 3;
}

/// <summary>Samples the height field on one global triangle lattice. Vertex (a, b) is at
/// <c>x = a·√3/k + (b odd ? √3/(2k) : 0)</c>, <c>z = b·1.5/k</c>; the step divides a tile column and row exactly, so chunk
/// edges lie on lattice lines and neighbouring chunks (and the two ends of the wrap) share identical border vertices.</summary>
public static class TerrainMesher
{
    /// <summary>Default lattice resolution: lattice steps per tile column (about 81 vertices per hex, fine enough for pointed summits and crags).</summary>
    public const int DefaultResolution = 9;

    private static readonly double Sqrt3 = Math.Sqrt(3);

    /// <summary>World x of lattice vertex (a, b) at resolution k.</summary>
    public static double LatticeX(int a, int b, int k) => a * Sqrt3 / k + ((b & 1) != 0 ? Sqrt3 / (2 * k) : 0);

    /// <summary>World z of lattice row b at resolution k.</summary>
    public static double LatticeZ(int b, int k) => b * 1.5 / k;

    /// <summary>Mesh one chunk. Each lattice height is sampled once (plus a one-vertex ring), and normals are central
    /// differences of those heights over the neighbouring lattice vertices. Lattice heights are a pure function of the
    /// global (a, b), so normals match exactly across chunk borders.</summary>
    public static TerrainMeshData Build(TerrainChunk chunk, TerrainField field, int k = DefaultResolution)
    {
        if (k < 1) throw new ArgumentOutOfRangeException(nameof(k));
        int a0 = chunk.UnitX0 * k, a1 = chunk.UnitX1 * k, b0 = chunk.UnitZ0 * k, b1 = chunk.UnitZ1 * k;
        int nx = a1 - a0 + 1, nz = b1 - b0 + 1;
        // Heights over the chunk lattice and a one-vertex ring, indexed [(b - b0 + 1) * rx + (a - a0 + 1)].
        int rx = nx + 2;
        var heights = new double[rx * (nz + 2)];
        for (int b = b0 - 1; b <= b1 + 1; b++)
        {
            double z = LatticeZ(b, k);
            for (int a = a0 - 1; a <= a1 + 1; a++)
                heights[(b - b0 + 1) * rx + (a - a0 + 1)] = field.HeightAt(LatticeX(a, b, k), z);
        }
        double H(int a, int b) => heights[(b - b0 + 1) * rx + (a - a0 + 1)];
        var positions = new float[nx * nz * 3];
        var normals = new float[nx * nz * 3];
        for (int b = b0; b <= b1; b++)
            for (int a = a0; a <= a1; a++)
            {
                int i = ((b - b0) * nx + (a - a0)) * 3;
                positions[i] = (float)LatticeX(a, b, k);
                positions[i + 1] = (float)H(a, b);
                positions[i + 2] = (float)LatticeZ(b, k);
                var normal = LatticeNormal(H, a, b, k);
                normals[i] = (float)normal.X; normals[i + 1] = (float)normal.Y; normals[i + 2] = (float)normal.Z;
            }
        var indices = new int[(nx - 1) * (nz - 1) * 6];
        int t = 0;
        for (int b = b0; b < b1; b++)
            for (int a = a0; a < a1; a++)
            {
                int v00 = (b - b0) * nx + (a - a0), v10 = v00 + 1, v01 = v00 + nx, v11 = v01 + 1;
                if ((b & 1) == 0)
                {
                    // Row b+1 is shifted east by half a step.
                    Emit(v00, v01, v10); Emit(v10, v01, v11);
                }
                else
                {
                    // Row b is shifted east by half a step.
                    Emit(v00, v01, v11); Emit(v00, v11, v10);
                }
            }
        return new(positions, normals, indices);

        void Emit(int p, int q, int r) { indices[t++] = p; indices[t++] = q; indices[t++] = r; }
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
}

/// <summary>The chunk meshes' exact surface between lattice vertices: the height and smooth normal that the rendered
/// triangles interpolate at any point, so surfaces draped over the terrain (river water) sit on what is drawn rather than
/// on the finer field. Caches lattice heights; use one instance per thread.</summary>
public sealed class TerrainLattice(TerrainField field, int k = TerrainMesher.DefaultResolution)
{
    private static readonly double Sqrt3 = Math.Sqrt(3);
    private readonly Dictionary<(int, int), double> _heights = new();

    /// <summary>Field height at lattice vertex (a, b).</summary>
    public double Height(int a, int b)
    {
        if (!_heights.TryGetValue((a, b), out double h))
            _heights[(a, b)] = h = field.HeightAt(TerrainMesher.LatticeX(a, b, k), TerrainMesher.LatticeZ(b, k));
        return h;
    }

    /// <summary>Height and normal of the rendered terrain at (x, z), interpolated across its lattice triangle.</summary>
    public (double Height, double NX, double NY, double NZ) Sample(double x, double z)
    {
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
        Add(a, b, w00); Add(a + 1, b, w10); Add(a, b + 1, w01); Add(a + 1, b + 1, w11);
        double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
        return (h, nx / len, ny / len, nz / len);

        void Add(int va, int vb, double w)
        {
            if (w <= 0) return;
            h += w * Height(va, vb);
            var n = TerrainMesher.LatticeNormal(Height, va, vb, k);
            nx += w * n.X; ny += w * n.Y; nz += w * n.Z;
        }
    }
}
