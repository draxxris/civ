using Ariel.Client.Terrain;
using Ariel.Game;
using Godot;

namespace Ariel.Client;

/// <summary>A city to draw: its tile centre on the flat map, the planned buildings, the ground under them and the fog shade.</summary>
internal readonly record struct CitySite(Vector3 Centre, CityPlan Plan, CityStyle Style, CityGround Ground, bool Walled, float Fog);

/// <summary>
/// 3D city buildings from <see cref="CityLayout"/> plans. Each building is made of a few unit parts (box, gable roof,
/// cone, pyramid, cylinder), scaled and turned per instance and batched per part into MultiMeshes. Instance colour is
/// the albedo times fog, with fog in alpha. Instance custom data is the part size in world units and a facade kind; the
/// city_building shader draws windows and timber framing from them. Each building's base sits at the lowest ground under
/// its corners, so it never floats on a slope. The ground underlay (dirt, cobblestone or concrete) is in
/// CityRenderer.Ground.cs.
/// </summary>
internal sealed partial class CityRenderer(float[] copies, Func<float, float, float> ground) : Node3D
{
    private enum Part { Box, Gable, Cone, Pyramid, Cylinder }

    /// <summary>Facade kinds for the shader (instance custom alpha).</summary>
    private const float Plain = 0, Timber = 1, Sash = 2, Ribbon = 3, Glass = 4;
    /// <summary>Bodies reach this far below the lowest corner, so no gap shows on uneven ground.</summary>
    private const float Sink = 0.01f;
    /// <summary>Fixed part sizes (chimneys, spires, masts) grow with the buildings.</summary>
    private const float S = (float)CityLayout.BuildingScale;

    private readonly record struct Instance(Transform3D At, Color Colour, Color Custom);

    private static readonly Color Mud = new(0.5f, 0.38f, 0.25f), Thatch = new(0.6f, 0.48f, 0.27f),
        Plaster = new(0.86f, 0.81f, 0.7f), OldTiles = new(0.42f, 0.27f, 0.18f),
        Brick = new(0.58f, 0.28f, 0.2f), Slate = new(0.27f, 0.29f, 0.33f), Soot = new(0.3f, 0.24f, 0.22f),
        Stone = new(0.72f, 0.69f, 0.62f), Concrete = new(0.74f, 0.73f, 0.7f), Roof = new(0.36f, 0.36f, 0.38f);
    private static readonly Color[] BrickTints = [new(0.58f, 0.28f, 0.2f), new(0.5f, 0.3f, 0.24f), new(0.66f, 0.4f, 0.28f), new(0.75f, 0.68f, 0.55f)];
    private static readonly Color[] FacadeTints = [new(0.78f, 0.77f, 0.73f), new(0.62f, 0.64f, 0.68f), new(0.82f, 0.76f, 0.64f), new(0.55f, 0.52f, 0.5f)];
    private static readonly Color[] GlassTints = [new(0.32f, 0.44f, 0.55f), new(0.5f, 0.58f, 0.62f), new(0.26f, 0.33f, 0.36f), new(0.42f, 0.5f, 0.48f)];

    private readonly Dictionary<Part, ArrayMesh> _meshes = new();
    private ShaderMaterial? _material;

    /// <summary>Building instances drawn by the last rebuild (one world copy).</summary>
    public int Buildings { get; private set; }

    /// <summary>Triangles drawn by the last rebuild (one world copy).</summary>
    public int Triangles { get; private set; }

    public void Rebuild(IReadOnlyList<CitySite> sites)
    {
        foreach (var child in GetChildren()) child.QueueFree();
        var batches = new Dictionary<Part, List<Instance>>();
        Buildings = 0;
        foreach (var site in sites)
            foreach (var lot in site.Plan.Lots)
            {
                AddLot(batches, site, lot);
                Buildings++;
            }
        Triangles = 0;
        if (_material == null)
        {
            _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/city_building.gdshader") };
            _material.SetShaderParameter("building_scale", S);
        }
        foreach (var (part, list) in batches)
        {
            var mesh = MeshOf(part);
            var multi = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true,
                Mesh = mesh, InstanceCount = list.Count,
            };
            for (int i = 0; i < list.Count; i++)
            {
                multi.SetInstanceTransform(i, list[i].At);
                multi.SetInstanceColor(i, list[i].Colour);
                multi.SetInstanceCustomData(i, list[i].Custom);
            }
            Triangles += list.Count * mesh.SurfaceGetArrayLen(0) / 3;
            foreach (float offset in copies)
                AddChild(new MultiMeshInstance3D
                {
                    Multimesh = multi, Position = new Vector3(offset, 0, 0), MaterialOverride = _material,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                });
        }
        AddGround(sites);
    }

    /// <summary>Adds the parts of one building.</summary>
    private void AddLot(Dictionary<Part, List<Instance>> batches, CitySite site, CityLot lot)
    {
        float w = (float)lot.Width, d = (float)lot.Depth, h = (float)lot.Height, yaw = (float)lot.Yaw;
        var turn = new Basis(Vector3.Up, yaw);
        var centre = site.Centre + new Vector3((float)lot.X, 0, (float)lot.Z);
        float low = float.MaxValue, high = float.MinValue;
        foreach (var (cx, cz) in lot.Corners().Append((lot.X, lot.Z)))
        {
            float y = ground(site.Centre.X + (float)cx, site.Centre.Z + (float)cz);
            low = Mathf.Min(low, y);
            high = Mathf.Max(high, y);
        }
        float baseY = low - Sink, top = high + h;
        float fog = site.Fog;
        uint seed = (uint)lot.Variant;

        // Adds a part at local (x, z) from the lot centre, from y0 to y1, size sx by sz, turned by extra yaw.
        void Add(Part part, float x, float z, float y0, float y1, float sx, float sz, Color colour, float facade, float extraYaw = 0)
        {
            var local = turn * new Vector3(x, 0, z);
            var basis = new Basis(Vector3.Up, yaw + extraYaw) * Basis.FromScale(new Vector3(sx, y1 - y0, sz));
            var at = new Transform3D(basis, new Vector3(centre.X + local.X, y0, centre.Z + local.Z));
            if (!batches.TryGetValue(part, out var list)) batches[part] = list = new();
            var tint = Shade(colour, seed, (int)part) * fog;
            list.Add(new Instance(at, new Color(tint, fog), new Color(sx, y1 - y0, sz, facade)));
        }

        switch (lot.Kind)
        {
            case CityLotKind.RoundHut:
                Add(Part.Cylinder, 0, 0, baseY, top, w, d, Mud, Plain);
                Add(Part.Cone, 0, 0, top - 0.004f, top + w * 0.85f, w * 1.3f, d * 1.3f, Thatch, Plain);
                break;
            case CityLotKind.Hut:
                Add(Part.Box, 0, 0, baseY, top, w, d, Mud, Plain);
                Add(Part.Gable, 0, 0, top, top + d * 0.7f, w * 1.12f, d * 1.25f, Thatch, Plain);
                break;
            case CityLotKind.TimberHouse:
                Add(Part.Box, 0, 0, baseY, top, w, d, Plaster, Timber);
                Add(Part.Gable, 0, 0, top, top + d * 0.8f, w * 1.08f, d * 1.18f, OldTiles, Plain);
                break;
            case CityLotKind.House:
            case CityLotKind.Terrace:
            {
                var brick = BrickTints[seed % BrickTints.Length];
                float roof = d * 0.5f;
                Add(Part.Box, 0, 0, baseY, top, w, d, brick, Sash);
                Add(Part.Gable, 0, 0, top, top + roof, w * 1.04f, d * 1.12f, Slate, Plain);
                // Chimney stacks on the ridge: one per house in a row.
                int stacks = lot.Kind == CityLotKind.House ? 1 : Mathf.Max(2, Mathf.RoundToInt(w / (0.055f * S)));
                for (int k = 0; k < stacks; k++)
                {
                    float x = stacks == 1 ? w * 0.32f : -w / 2 + w * (k + 0.5f) / stacks;
                    Add(Part.Box, x, 0, top + roof * 0.4f, top + roof + 0.02f * S, 0.014f * S, 0.022f * S, Soot, Plain);
                }
                break;
            }
            case CityLotKind.Church:
            {
                // The nave runs along the lot's depth; the tower and spire stand at its front (-Z) end.
                float roof = w * 0.6f;
                Add(Part.Box, 0, 0.02f * S, baseY, top, w, d - 0.04f * S, Stone, Sash);
                Add(Part.Gable, 0, 0.02f * S, top, top + roof, d * 1.04f - 0.04f * S, w * 1.12f, Slate, Plain, Mathf.Pi / 2);
                float tower = w * 0.6f, towerTop = top + 0.08f * S, z = -d / 2 + tower / 2;
                Add(Part.Box, 0, z, baseY, towerTop, tower, tower, Stone, Sash);
                Add(Part.Pyramid, 0, z, towerTop, towerTop + 0.16f * S, tower * 1.1f, tower * 1.1f, Slate, Plain);
                break;
            }
            case CityLotKind.Block:
            {
                var facade = FacadeTints[seed % FacadeTints.Length];
                Add(Part.Box, 0, 0, baseY, top, w, d, facade, Ribbon);
                if (lot.Floors >= 3) Add(Part.Box, w * 0.15f, -d * 0.1f, top, top + 0.016f * S, w * 0.35f, d * 0.35f, Roof, Plain);
                break;
            }
            case CityLotKind.Tower:
            {
                var glass = GlassTints[seed % GlassTints.Length];
                if (lot.Floors >= 12)
                {
                    // A setback crown and a mast above the main shaft.
                    float storey = (float)CityLayout.StoreyHeight(site.Style);
                    float shaft = top - Mathf.Round(lot.Floors * 0.2f) * storey;
                    Add(Part.Box, 0, 0, baseY, shaft, w, d, glass, Glass);
                    Add(Part.Box, 0, 0, shaft, top, w * 0.72f, d * 0.72f, glass, Glass);
                    Add(Part.Box, 0, 0, top, top + 0.1f * S, 0.006f * S, 0.006f * S, Roof, Plain);
                }
                else Add(Part.Box, 0, 0, baseY, top, w, d, glass, Glass);
                break;
            }
        }
    }

    /// <summary>A small, repeatable shift in brightness and hue, so neighbours of the same kind differ.</summary>
    private static Color Shade(Color colour, uint seed, int salt)
    {
        uint x = seed * 0x9E3779B9u ^ (uint)salt * 0x85EBCA6Bu;
        x ^= x >> 15; x *= 0x2C1B3C6Du; x ^= x >> 12;
        float bright = 0.88f + 0.24f * (x & 0xff) / 255f;
        float warm = ((x >> 8) & 0xff) / 255f - 0.5f;
        return new Color(colour.R * bright * (1 + 0.06f * warm), colour.G * bright, colour.B * bright * (1 - 0.06f * warm));
    }

    private ArrayMesh MeshOf(Part part)
    {
        if (_meshes.TryGetValue(part, out var mesh)) return mesh;
        var faces = new List<Vector3[]>();
        switch (part)
        {
            case Part.Box:
            {
                Vector3 P(float x, float y, float z) => new(x - 0.5f, y, z - 0.5f);
                faces.Add([P(0, 1, 0), P(1, 1, 0), P(1, 1, 1), P(0, 1, 1)]);
                faces.Add([P(0, 0, 1), P(1, 0, 1), P(1, 1, 1), P(0, 1, 1)]);
                faces.Add([P(1, 0, 0), P(0, 0, 0), P(0, 1, 0), P(1, 1, 0)]);
                faces.Add([P(1, 0, 1), P(1, 0, 0), P(1, 1, 0), P(1, 1, 1)]);
                faces.Add([P(0, 0, 0), P(0, 0, 1), P(0, 1, 1), P(0, 1, 0)]);
                break;
            }
            case Part.Gable:
            {
                // Ridge along X at the top, eaves at the bottom of the front (+Z) and back (-Z).
                Vector3 a = new(-0.5f, 0, 0.5f), b = new(0.5f, 0, 0.5f), c = new(0.5f, 0, -0.5f), e = new(-0.5f, 0, -0.5f);
                Vector3 r0 = new(-0.5f, 1, 0), r1 = new(0.5f, 1, 0);
                faces.Add([a, b, r1, r0]);
                faces.Add([c, e, r0, r1]);
                faces.Add([b, c, r1]);
                faces.Add([e, a, r0]);
                faces.Add([e, c, b, a]);
                break;
            }
            case Part.Cone:
            case Part.Pyramid:
            {
                int n = part == Part.Cone ? 10 : 4;
                float start = part == Part.Pyramid ? Mathf.Pi / 4 : 0;
                for (int k = 0; k < n; k++)
                {
                    float a0 = start + k * Mathf.Tau / n, a1 = start + (k + 1) * Mathf.Tau / n;
                    var p0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0, Mathf.Sin(a0) * 0.5f);
                    var p1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0, Mathf.Sin(a1) * 0.5f);
                    faces.Add([p1, p0, new Vector3(0, 1, 0)]);
                }
                break;
            }
            case Part.Cylinder:
            {
                const int n = 10;
                var top = new List<Vector3>();
                for (int k = 0; k < n; k++)
                {
                    float a0 = k * Mathf.Tau / n, a1 = (k + 1) * Mathf.Tau / n;
                    var p0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0, Mathf.Sin(a0) * 0.5f);
                    var p1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0, Mathf.Sin(a1) * 0.5f);
                    faces.Add([p1, p0, p0 + Vector3.Up, p1 + Vector3.Up]);
                    top.Add(p0 + Vector3.Up);
                }
                top.Reverse();
                faces.Add(top.ToArray());
                break;
            }
        }
        return _meshes[part] = Build(faces, new Vector3(0, part is Part.Box or Part.Cylinder ? 0.5f : 0.3f, 0));
    }

    /// <summary>Flat-shaded triangles from convex faces. Each normal points away from <paramref name="inside"/>, and the
    /// triangles wind clockwise seen from that side, which Godot draws as the front.</summary>
    private static ArrayMesh Build(List<Vector3[]> faces, Vector3 inside)
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        foreach (var face in faces)
        {
            var cross = (face[1] - face[0]).Cross(face[2] - face[0]).Normalized();
            var middle = face.Aggregate(Vector3.Zero, (sum, p) => sum + p) / face.Length;
            bool flip = cross.Dot(middle - inside) > 0;
            var normal = flip ? cross : -cross;
            for (int k = 1; k + 1 < face.Length; k++)
            {
                vertices.Add(face[0]);
                if (flip) { vertices.Add(face[k + 1]); vertices.Add(face[k]); }
                else { vertices.Add(face[k]); vertices.Add(face[k + 1]); }
                normals.Add(normal); normals.Add(normal); normals.Add(normal);
            }
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
}
