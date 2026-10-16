using Godot;

namespace Civ.Client;

/// <summary>
/// Soft worn-earth patches where the parts of an improvement model meet the ground. Each footprint is the convex hull
/// of a part's lowest vertices, found once when the model loads. Every rebuild drapes all patches over the height field
/// as one mesh: a fan over the hull widened by a margin, then a feathered rim whose alpha falls to zero. Parts named
/// <c>ground*</c> carry their own surface, and parts named <c>nodecal*</c> opt out. Patches on water are skipped.
/// </summary>
internal sealed partial class ImprovementRenderer
{
    /// <summary>A part's footprint in model space (tile centre at the origin, before the site turn): the hull corners in
    /// order, their mean and its rough radius, which sets the margin, the feather width and the strength.</summary>
    private sealed record Footprint(Vector2[] Hull, Vector2 Centre, float Radius);

    /// <summary>A vertex at or below this height (model units, which are world units) touches the ground.</summary>
    private const float LowY = 0.06f;
    /// <summary>Hull edges are cut into pieces no longer than this, so the patch follows the ground between corners.</summary>
    private const float EdgeStep = 0.2f;
    /// <summary>Hull corners closer together than this merge.</summary>
    private const float MinCorner = 0.05f;
    /// <summary>Corner arcs turn at most this far per step.</summary>
    private const float ArcStep = Mathf.Pi / 3;
    /// <summary>Height above the sampled field, plus <see cref="SlopeLift"/> times the slope (1 - normal.y). The
    /// rendered terrain interpolates a coarser lattice, which can rise above the field on curved slopes; the extra lift
    /// and the shader's depth pull keep the patch on top there.</summary>
    private const float Lift = 0.004f, SlopeLift = 0.06f;
    /// <summary>Below this the ground is the sea surface, where boats and platforms need no earth.</summary>
    private const float SeaLevel = 0.0005f;

    private static readonly Color Earth = new(0.3f, 0.18f, 0.08f);
    private ShaderMaterial? _decalMaterial;

    /// <summary>Triangles in the last ground-patch mesh (one world copy).</summary>
    public int DecalTriangles { get; private set; }

    private static bool HasOwnGround(string name) =>
        name.StartsWith("ground", StringComparison.OrdinalIgnoreCase) || name.StartsWith("nodecal", StringComparison.OrdinalIgnoreCase);

    /// <summary>The footprint of one part, or null when it carries its own ground, opts out or never touches the ground.</summary>
    private static Footprint? FootprintOf(string name, ArrayMesh mesh, Vector3 offset, Basis basis)
    {
        if (HasOwnGround(name)) return null;
        var low = new List<Vector2>();
        for (int s = 0; s < mesh.GetSurfaceCount(); s++)
            foreach (var v in mesh.SurfaceGetArrays(s)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                if (v.Y <= LowY)
                {
                    var p = offset + basis * v;
                    low.Add(new Vector2(p.X, p.Z));
                }
        if (low.Count == 0) return null;
        var hull = Simplify(Hull(low));
        var centre = hull.Aggregate(Vector2.Zero, (sum, p) => sum + p) / hull.Length;
        float radius = Mathf.Max(0.02f, hull.Average(p => p.DistanceTo(centre)));
        return new Footprint(hull, centre, radius);
    }

    /// <summary>Convex hull (monotone chain) of points in the XZ plane. <see cref="Outline"/> reads its winding.</summary>
    private static Vector2[] Hull(List<Vector2> points)
    {
        var sorted = points.Distinct().OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
        if (sorted.Count < 3) return sorted.ToArray();
        var hull = new List<Vector2>();
        for (int pass = 0; pass < 2; pass++)
        {
            int start = hull.Count;
            foreach (var p in sorted)
            {
                while (hull.Count >= start + 2 && (hull[^1] - hull[^2]).Cross(p - hull[^2]) <= 1e-7f) hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            hull.RemoveAt(hull.Count - 1);
            sorted.Reverse();
        }
        return hull.ToArray();
    }

    /// <summary>Drop hull corners closer than <see cref="MinCorner"/> to the last kept one: round parts have dozens,
    /// and the feathered rim hides the difference.</summary>
    private static Vector2[] Simplify(Vector2[] hull)
    {
        if (hull.Length <= 4) return hull;
        var kept = new List<Vector2> { hull[0] };
        foreach (var p in hull.Skip(1))
            if (p.DistanceTo(kept[^1]) >= MinCorner && p.DistanceTo(kept[0]) >= MinCorner) kept.Add(p);
        return kept.Count >= 3 ? kept.ToArray() : hull;
    }

    /// <summary>Points around the hull with outward directions: subdivided edges plus rounded corners. A ring at
    /// distance d is then point + direction * d, which is the hull grown by d with round corners.</summary>
    private static List<(Vector2 At, Vector2 Out)> Outline(Vector2[] hull, Vector2 centre)
    {
        var outline = new List<(Vector2, Vector2)>();
        if (hull.Length < 3)
        {
            // A post or a single edge: round it off as a small disc around its middle.
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.Tau / 8;
                outline.Add((centre, new Vector2(Mathf.Cos(a), Mathf.Sin(a))));
            }
            return outline;
        }
        int n = hull.Length;
        float winding = Mathf.Sign((hull[1] - hull[0]).Cross(hull[2] - hull[1]));
        Vector2 Normal(int i)
        {
            var edge = (hull[(i + 1) % n] - hull[i]).Normalized();
            return winding > 0 ? new Vector2(edge.Y, -edge.X) : new Vector2(-edge.Y, edge.X);
        }
        for (int i = 0; i < n; i++)
        {
            var before = Normal((i + n - 1) % n);
            var after = Normal(i);
            float turn = Mathf.Abs(before.AngleTo(after));
            int arcs = Mathf.CeilToInt(turn / ArcStep);
            // A gentle corner (round parts have many) takes one point on the bisector instead of an arc.
            if (turn <= ArcStep / 2) outline.Add((hull[i], (before + after).Normalized()));
            else for (int k = 0; k <= arcs; k++) outline.Add((hull[i], before.Slerp(after, k / (float)arcs).Normalized()));
            var a = hull[i];
            var b = hull[(i + 1) % n];
            int pieces = Mathf.CeilToInt(a.DistanceTo(b) / EdgeStep);
            for (int k = 1; k < pieces; k++) outline.Add((a.Lerp(b, k / (float)pieces), after));
        }
        return outline;
    }

    /// <summary>Drape every footprint of every site into one mesh and add it once per world copy.</summary>
    private void AddDecals(IEnumerable<ImprovementSite> sites)
    {
        DecalTriangles = 0;
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var colours = new List<Color>();
        var indices = new List<int>();
        foreach (var site in sites)
        {
            if (ModelOf(site.Id)?.Footprints is not { } footprints) continue;
            var turn = new Basis(Vector3.Up, site.Yaw);
            for (int f = 0; f < footprints.Length; f++)
                Drape(footprints[f], site, turn, Seed(site.Centre, f), vertices, normals, colours, indices);
        }
        if (indices.Count == 0) return;
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colours.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, _decalMaterial ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/ground_decal.gdshader") });
        DecalTriangles = indices.Count / 3;
        foreach (float offset in copies)
            AddChild(new MeshInstance3D
            {
                Mesh = mesh, Position = new Vector3(offset, 0, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
    }

    private static uint Seed(Vector3 centre, int footprint) =>
        (uint)(Mathf.RoundToInt(centre.X * 64) * 73856093 ^ Mathf.RoundToInt(centre.Z * 64) * 19349663 ^ footprint * 83492791);

    private static float Jitter(uint seed, int k)
    {
        uint x = seed ^ (uint)k * 0x9E3779B9u;
        x ^= x >> 16; x *= 0x7feb352du; x ^= x >> 15; x *= 0x846ca68bu; x ^= x >> 16;
        return (x & 0xffff) / 65535f;
    }

    /// <summary>One patch: a fan over the grown hull, then two feathered rings, each vertex seated on the ground.</summary>
    private void Drape(Footprint footprint, ImprovementSite site, Basis turn, uint seed,
        List<Vector3> vertices, List<Vector3> normals, List<Color> colours, List<int> indices)
    {
        Vector3 World(Vector2 p) => site.Centre + turn * new Vector3(p.X, 0, p.Y);
        var middle = World(footprint.Centre);
        if (ground(middle.X, middle.Z) <= SeaLevel) return;

        float r = footprint.Radius;
        float margin = 0.03f + 0.15f * r;
        float feather = Mathf.Clamp(0.06f + 0.45f * r, 0.06f, 0.18f);
        float strength = 0.48f * Mathf.Clamp(r / 0.1f, 0.5f, 1f) * (0.85f + 0.3f * Jitter(seed, -1));
        var tint = new Color(Earth.R * site.Fog, Earth.G * site.Fog, Earth.B * site.Fog);
        int first = vertices.Count;
        void Add(Vector3 at, float alpha)
        {
            var normal = NormalAt(at.X, at.Z);
            at.Y = ground(at.X, at.Z) + Lift + SlopeLift * (1 - normal.Y);
            vertices.Add(at);
            normals.Add(normal);
            colours.Add(new Color(tint, alpha));
        }
        Add(middle, strength);
        var outline = Outline(footprint.Hull, footprint.Centre);
        int count = outline.Count;
        // Rings: grown hull (full strength), mid feather, outer edge (transparent). The outer edges wander a little.
        foreach (var (at, dir) in outline) Add(World(at + dir * margin), strength);
        for (int k = 0; k < count; k++)
        {
            var (at, dir) = outline[k];
            float wobble = 0.75f + 0.5f * Jitter(seed, k);
            Add(World(at + dir * (margin + 0.45f * feather * wobble)), strength * 0.45f);
        }
        for (int k = 0; k < count; k++)
        {
            var (at, dir) = outline[k];
            float wobble = 0.7f + 0.6f * Jitter(seed, k + 997);
            Add(World(at + dir * (margin + feather * wobble)), 0f);
        }
        for (int k = 0; k < count; k++)
        {
            int next = (k + 1) % count;
            indices.Add(first); indices.Add(first + 1 + k); indices.Add(first + 1 + next);
            for (int ring = 0; ring < 2; ring++)
            {
                int inner = first + 1 + ring * count, outer = inner + count;
                indices.Add(inner + k); indices.Add(outer + k); indices.Add(outer + next);
                indices.Add(inner + k); indices.Add(outer + next); indices.Add(inner + next);
            }
        }
    }
}
