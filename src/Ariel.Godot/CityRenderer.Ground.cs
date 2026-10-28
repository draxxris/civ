using Ariel.Client.Terrain;
using Ariel.Game;
using Godot;

namespace Ariel.Client;

/// <summary>
/// The ground under a city: dark-brown dirt, cobblestone or concrete (<see cref="CityGround"/>), drawn by the
/// city_ground shader. The patch is a hex that covers the city's buildings or reaches the wall line; without walls its
/// rim feathers out. Each building outside the walls gets a small round patch of its own. Every vertex is seated on the
/// height field, as the improvement ground patches are (ImprovementRenderer.Decals.cs). Vertex colour is the fog shade,
/// and alpha is the feathered edge.
/// </summary>
internal sealed partial class CityRenderer
{
    /// <summary>Points per hex edge on each ring, so the patch follows the ground between corners.</summary>
    private const int EdgeSteps = 5;
    /// <summary>Rings from the centre to the full-strength edge.</summary>
    private const int Rings = 5;
    private const float Lift = 0.004f, SlopeLift = 0.06f, NormalStep = 0.04f, SeaLevel = 0.0005f;

    /// <summary>Shader ground kind for the straw patches under buildings on dirt (after the CityGround values).</summary>
    private const int Hay = 3;
    private readonly Dictionary<int, ShaderMaterial> _groundMaterials = new();

    /// <summary>Triangles in the last ground mesh (one world copy).</summary>
    public int GroundTriangles { get; private set; }

    private sealed class Surface
    {
        public readonly List<Vector3> Vertices = new(), Normals = new();
        public readonly List<Color> Colours = new();
        public readonly List<int> Indices = new();
    }

    private void AddGround(IReadOnlyList<CitySite> sites)
    {
        GroundTriangles = 0;
        var surfaces = new SortedDictionary<int, Surface>();
        Surface Of(int kind) => surfaces.TryGetValue(kind, out var s) ? s : surfaces[kind] = new Surface();
        foreach (var site in sites)
        {
            var surface = Of((int)site.Ground);
            Patch(surface, site);
            foreach (var lot in site.Plan.Lots.Where(l => l.Outside))
                Spot(surface, site, lot, 0.025f);
            // Straw and dry grass round the foot of every building on dirt; drawn after the dirt, so on top of it.
            if (site.Ground == CityGround.Dirt)
                foreach (var lot in site.Plan.Lots)
                    Spot(Of(Hay), site, lot, 0.01f);
        }
        var mesh = new ArrayMesh();
        foreach (var (kind, s) in surfaces)
        {
            if (s.Indices.Count == 0) continue;
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = s.Vertices.ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = s.Normals.ToArray();
            arrays[(int)Mesh.ArrayType.Color] = s.Colours.ToArray();
            arrays[(int)Mesh.ArrayType.Index] = s.Indices.ToArray();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            mesh.SurfaceSetMaterial(mesh.GetSurfaceCount() - 1, GroundMaterial(kind));
            GroundTriangles += s.Indices.Count / 3;
        }
        if (mesh.GetSurfaceCount() == 0) return;
        foreach (float offset in copies)
            AddChild(new MeshInstance3D
            {
                Mesh = mesh, Position = new Vector3(offset, 0, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
    }

    private ShaderMaterial GroundMaterial(int kind)
    {
        if (_groundMaterials.TryGetValue(kind, out var material)) return material;
        material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/city_ground.gdshader") };
        material.SetShaderParameter("ground_kind", kind);
        material.RenderPriority = kind == Hay ? 1 : 0;
        return _groundMaterials[kind] = material;
    }

    /// <summary>The hex patch: a fan of rings out to the full-strength edge, then a feathered rim.</summary>
    private void Patch(Surface s, CitySite site)
    {
        if (ground(site.Centre.X, site.Centre.Z) <= SeaLevel) return;
        // Corner radius of a hex whose edges lie at the planned hex distance.
        float corner = (float)site.Plan.GroundRadius / 0.8660254f;
        float feather = site.Walled ? 0.06f : 0.14f;
        var outline = new List<Vector2>();
        for (int k = 0; k < 6; k++)
        {
            float a0 = Mathf.DegToRad(30 + 60 * k), a1 = Mathf.DegToRad(90 + 60 * k);
            var p0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
            var p1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
            for (int step = 0; step < EdgeSteps; step++) outline.Add(p0.Lerp(p1, step / (float)EdgeSteps));
        }
        int count = outline.Count, first = s.Vertices.Count;
        uint seed = (uint)(Mathf.RoundToInt(site.Centre.X * 64) * 73856093 ^ Mathf.RoundToInt(site.Centre.Z * 64) * 19349663);
        Seat(s, site.Centre, site.Fog, 1);
        for (int ring = 1; ring <= Rings; ring++)
            foreach (var p in outline) Seat(s, site.Centre + Flat(p * corner * ring / Rings), site.Fog, 1);
        for (int k = 0; k < count; k++)
        {
            float wobble = 0.6f + 0.8f * Jitter(seed, k);
            var p = outline[k];
            Seat(s, site.Centre + Flat(p * corner + p.Normalized() * feather * wobble), site.Fog, 0);
        }
        for (int k = 0; k < count; k++)
        {
            int next = (k + 1) % count;
            s.Indices.Add(first); s.Indices.Add(first + 1 + k); s.Indices.Add(first + 1 + next);
            for (int ring = 0; ring < Rings; ring++)
            {
                int inner = first + 1 + ring * count, outer = inner + count;
                s.Indices.Add(inner + k); s.Indices.Add(outer + k); s.Indices.Add(outer + next);
                s.Indices.Add(inner + k); s.Indices.Add(outer + next); s.Indices.Add(inner + next);
            }
        }
    }

    /// <summary>A small round patch under a building: ground outside the walls, or straw on dirt. The patch reaches
    /// <paramref name="margin"/> past the footprint before its rim feathers out.</summary>
    private void Spot(Surface s, CitySite site, CityLot lot, float margin)
    {
        const int n = 12;
        var centre = site.Centre + new Vector3((float)lot.X, 0, (float)lot.Z);
        if (ground(centre.X, centre.Z) <= SeaLevel) return;
        float radius = (float)lot.Reach + margin;
        int first = s.Vertices.Count;
        Seat(s, centre, site.Fog, 1);
        for (int ring = 0; ring < 2; ring++)
            for (int k = 0; k < n; k++)
            {
                float a = k * Mathf.Tau / n;
                float r = ring == 0 ? radius : radius + 0.05f * (0.6f + 0.8f * Jitter((uint)lot.Variant, k));
                Seat(s, centre + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r), site.Fog, ring == 0 ? 1 : 0);
            }
        for (int k = 0; k < n; k++)
        {
            int next = (k + 1) % n;
            s.Indices.Add(first); s.Indices.Add(first + 1 + k); s.Indices.Add(first + 1 + next);
            int inner = first + 1, outer = inner + n;
            s.Indices.Add(inner + k); s.Indices.Add(outer + k); s.Indices.Add(outer + next);
            s.Indices.Add(inner + k); s.Indices.Add(outer + next); s.Indices.Add(inner + next);
        }
    }

    private static Vector3 Flat(Vector2 p) => new(p.X, 0, p.Y);

    private void Seat(Surface s, Vector3 at, float fog, float alpha)
    {
        float dx = ground(at.X + NormalStep, at.Z) - ground(at.X - NormalStep, at.Z);
        float dz = ground(at.X, at.Z + NormalStep) - ground(at.X, at.Z - NormalStep);
        var normal = new Vector3(-dx, 2 * NormalStep, -dz).Normalized();
        at.Y = ground(at.X, at.Z) + Lift + SlopeLift * (1 - normal.Y);
        s.Vertices.Add(at);
        s.Normals.Add(normal);
        s.Colours.Add(new Color(fog, fog, fog, alpha));
    }

    private static float Jitter(uint seed, int k)
    {
        uint x = seed ^ (uint)k * 0x9E3779B9u;
        x ^= x >> 16; x *= 0x7feb352du; x ^= x >> 15; x *= 0x846ca68bu; x ^= x >> 16;
        return (x & 0xffff) / 65535f;
    }
}
