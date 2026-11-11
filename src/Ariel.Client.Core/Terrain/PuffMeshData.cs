namespace Ariel.Client.Terrain;

/// <summary>Engine-free arrays for a set of puffs: small quads the shader shapes and animates on their own, such as a
/// waterfall's mist or a spring's bubbles. Every corner of a puff carries its centre in <see cref="Positions"/>, its corner
/// (±1, ±1) in <see cref="Corners"/> and four numbers in <see cref="Info"/>: (size, phase, rise, density), where size is
/// the puff's half size in world units, phase (0..1) offsets its cycle, rise is how far it travels over one cycle and
/// density sets how thick and quick it is. Two triangles per puff.</summary>
public sealed record PuffMeshData(float[] Positions, float[] Corners, float[] Info, int[] Indices)
{
    /// <summary>Number of vertices.</summary>
    public int VertexCount => Positions.Length / 3;
}

/// <summary>Collects puffs into a <see cref="PuffMeshData"/>.</summary>
internal sealed class PuffBuilder
{
    private readonly List<float> _positions = [], _corners = [], _info = [];
    private readonly List<int> _indices = [];

    public bool Empty => _indices.Count == 0;

    /// <summary>A puff centred at (x, y, z): see <see cref="PuffMeshData"/> for the other numbers.</summary>
    public void Add(double x, double y, double z, double size, double phase, double rise, double density)
    {
        int start = _positions.Count / 3;
        foreach (var (cu, cv) in Corners)
        {
            _positions.AddRange([(float)x, (float)y, (float)z]);
            _corners.AddRange([cu, cv]);
            _info.AddRange([(float)size, (float)phase, (float)rise, (float)density]);
        }
        _indices.AddRange([start, start + 1, start + 2, start, start + 2, start + 3]);
    }

    public PuffMeshData ToData() => new([.. _positions], [.. _corners], [.. _info], [.. _indices]);

    private static readonly (float U, float V)[] Corners = [(-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f)];
}
