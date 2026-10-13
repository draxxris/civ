using Godot;

namespace Civ.Client;

/// <summary>Accumulates coloured (and optionally textured) triangles on the ground plane, then freezes them into one mesh surface.</summary>
internal sealed class MeshBuilder
{
    private readonly List<Vector3> _vertices = new();
    private readonly List<Color> _colors = new();
    private readonly List<Vector2> _uvs = new();
    private readonly List<int> _indices = new();

    /// <summary>UV used by untextured shapes when the mesh shares a texture atlas: a pixel that is solid white.</summary>
    public Vector2 DefaultUv { get; set; }

    public int VertexCount => _vertices.Count;

    public int Vertex(Vector3 p, Color c, Vector2? uv = null)
    {
        _vertices.Add(p); _colors.Add(c); _uvs.Add(uv ?? DefaultUv);
        return _vertices.Count - 1;
    }

    public void Triangle(int a, int b, int c)
    {
        _indices.Add(a); _indices.Add(b); _indices.Add(c);
    }

    public void Tri(Vector3 a, Vector3 b, Vector3 c, Color color) => Triangle(Vertex(a, color), Vertex(b, color), Vertex(c, color));

    public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
    {
        int i = Vertex(a, color); Vertex(b, color); Vertex(c, color); Vertex(d, color);
        Triangle(i, i + 1, i + 2); Triangle(i, i + 2, i + 3);
    }

    /// <summary>A flat hexagon (pointy top) with a centre colour and a rim colour.</summary>
    public void Hex(Vector3 centre, float radius, Color inner, Color rim)
    {
        int c = Vertex(centre, inner);
        int first = _vertices.Count;
        for (int k = 0; k < 6; k++)
            Vertex(centre + Corner(k, radius), rim);
        for (int k = 0; k < 6; k++) Triangle(c, first + (k + 1) % 6, first + k);
    }

    /// <summary>Corner k of a pointy-top hex in (x, z) with z down: angle 60k-30 degrees.</summary>
    public static Vector3 Corner(int k, float radius)
    {
        float a = Mathf.DegToRad(60f * k - 30f);
        return new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius);
    }

    /// <summary>Corner at angle (degrees) for a direction-indexed edge: direction i points at -60 i degrees.</summary>
    public static Vector3 EdgeCorner(int direction, int side, float radius)
    {
        float a = Mathf.DegToRad(-60f * direction + (side == 0 ? -30f : 30f));
        return new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius);
    }

    public void Line(Vector3 a, Vector3 b, float width, Color color)
    {
        var dir = b - a;
        if (dir.LengthSquared() < 1e-8f) return;
        var side = new Vector3(-dir.Z, 0, dir.X).Normalized() * (width / 2);
        Quad(a - side, a + side, b + side, b - side, color);
    }

    public void Disc(Vector3 centre, float radius, Color color, int segments = 14)
    {
        int c = Vertex(centre, color);
        int first = _vertices.Count;
        for (int k = 0; k < segments; k++)
        {
            float a = Mathf.Tau * k / segments;
            Vertex(centre + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius, color);
        }
        for (int k = 0; k < segments; k++) Triangle(c, first + (k + 1) % segments, first + k);
    }

    public void Ring(Vector3 centre, float radius, float width, Color color, int segments = 18)
    {
        for (int k = 0; k < segments; k++)
        {
            float a0 = Mathf.Tau * k / segments, a1 = Mathf.Tau * (k + 1) / segments;
            var p0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
            var p1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
            Quad(centre + p0 * (radius - width), centre + p0 * radius, centre + p1 * radius, centre + p1 * (radius - width), color);
        }
    }

    /// <summary>A textured square facing up, centred on <paramref name="centre"/>, with uv rectangle (x, y, w, h).</summary>
    public void Icon(Vector3 centre, float size, Rect2 uv, Color tint)
    {
        float h = size / 2;
        int i = Vertex(centre + new Vector3(-h, 0, -h), tint, uv.Position);
        Vertex(centre + new Vector3(h, 0, -h), tint, uv.Position + new Vector2(uv.Size.X, 0));
        Vertex(centre + new Vector3(h, 0, h), tint, uv.Position + uv.Size);
        Vertex(centre + new Vector3(-h, 0, h), tint, uv.Position + new Vector2(0, uv.Size.Y));
        Triangle(i, i + 1, i + 2); Triangle(i, i + 2, i + 3);
    }

    public ArrayMesh? Build(Material material)
    {
        if (_indices.Count == 0) return null;
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = _vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = _colors.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = _uvs.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = _indices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }
}
