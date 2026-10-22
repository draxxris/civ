using Civ.Client.Terrain;
using Civ.Game;
using Godot;

namespace Civ.Client;

/// <summary>A tile improvement to draw: its content id, tile centre on the flat map, layout turn and fog shade.</summary>
internal readonly record struct ImprovementSite(string Id, Vector3 Centre, float Yaw, float Fog);

/// <summary>
/// 3D terrain improvements (cottages, mines, ...) and map resources (wheat, whales, gold, ...) from each content id's
/// <c>art/models/&lt;id suffix&gt;.glb</c>. Every top-level
/// node of the model is one part, placed at its offset from the tile centre and seated on the ground there, so each
/// building stands upright on a slope. A part whose name starts with <c>ground</c> is tilted to the surface instead
/// (garden plots, rails). Scene extras <c>{"clear": "centre"}</c> keep scatter props on the tile. Scene extras
/// <c>{"faceRiver": true}</c> ask the map to stand the model on the river's near waterline, +X straight across the water
/// (the watermill wheel stands in the river). Parts are batched per model and part into MultiMeshes, with fog as instance colour.
/// Soft earth patches under the parts that meet the ground are in ImprovementRenderer.Decals.cs.
/// </summary>
internal sealed partial class ImprovementRenderer(GameCatalog catalog, float[] copies, Func<float, float, float> ground) : Node3D
{
    private const float NormalStep = 0.04f;
    private static readonly GltfDocumentExtensionConvertImporterMesh MeshConverter = RegisterMeshConverter();
    private sealed record Piece(ArrayMesh Mesh, Vector3 Offset, Basis Basis, bool Drape);
    /// <summary>A loaded model. ClearsTile is false when the model asks (scene extras <c>"clear": "centre"</c>) to keep
    /// scatter props around it, as camps and lumbermills standing among trees do.</summary>
    private sealed record Model(Piece[] Pieces, bool ClearsTile, bool FacesRiver, int CityRing, Footprint[] Footprints,
        CityWalls? Walls);
    private readonly Dictionary<string, Model?> _models = new(StringComparer.Ordinal);
    private readonly StandardMaterial3D _material = new()
    {
        VertexColorUseAsAlbedo = true, Roughness = 0.9f, MetallicSpecular = 0.3f,
    };

    /// <summary>Instances drawn by the last rebuild (one world copy).</summary>
    public int Instances { get; private set; }

    /// <summary>Triangles drawn by the last rebuild (one world copy).</summary>
    public int Triangles { get; private set; }

    /// <summary>True when the improvement ships a model, so its map icon can be skipped.</summary>
    public bool Has(string id) => ModelOf(id) != null;

    /// <summary>True when the improvement's model fills its tile, so no scatter props may grow through it.</summary>
    public bool ClearsTile(string id) => ModelOf(id)?.ClearsTile == true;

    /// <summary>True when the model asks (scene extras <c>"faceRiver": true</c>) to stand on the river bank, +X across the water.</summary>
    public bool FacesRiver(string id) => ModelOf(id)?.FacesRiver == true;

    /// <summary>The highest-tier ring among a city's completed buildings. Scene extras <c>cityRing</c> define the tier.</summary>
    public string? CityRing(City? city) => city?.Buildings
        .Where(id => ModelOf(id)?.CityRing > 0)
        .OrderByDescending(id => ModelOf(id)!.CityRing)
        .ThenBy(id => id, StringComparer.Ordinal).FirstOrDefault();

    /// <summary>The footprint of a city ring model (<see cref="CityRing"/>), for laying out the buildings inside.</summary>
    public CityWalls? WallsOf(string ring) => ModelOf(ring)?.Walls;
    public void Rebuild(IEnumerable<ImprovementSite> sites)
    {
        foreach (var child in GetChildren()) child.QueueFree();
        var batches = new Dictionary<(string Id, int Piece), List<(Transform3D At, Color Fog)>>();
        foreach (var site in sites)
        {
            if (ModelOf(site.Id)?.Pieces is not { } pieces) continue;
            var turn = new Basis(Vector3.Up, site.Yaw);
            for (int i = 0; i < pieces.Length; i++)
            {
                var key = (site.Id, i);
                if (!batches.TryGetValue(key, out var list)) batches[key] = list = new();
                list.Add((Seat(pieces[i], site.Centre, turn), new Color(site.Fog, site.Fog, site.Fog)));
            }
        }
        Instances = Triangles = 0;
        foreach (var ((id, index), list) in batches)
        {
            var piece = _models[id]!.Pieces[index];
            var multi = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true,
                Mesh = piece.Mesh, InstanceCount = list.Count,
            };
            for (int i = 0; i < list.Count; i++)
            {
                multi.SetInstanceTransform(i, list[i].At);
                multi.SetInstanceColor(i, list[i].Fog);
            }
            Instances += list.Count;
            for (int s = 0; s < piece.Mesh.GetSurfaceCount(); s++)
            {
                int indices = piece.Mesh.SurfaceGetArrayIndexLen(s);
                Triangles += list.Count * (indices > 0 ? indices : piece.Mesh.SurfaceGetArrayLen(s)) / 3;
            }
            foreach (float offset in copies)
                AddChild(new MultiMeshInstance3D
                {
                    Multimesh = multi, Position = new Vector3(offset, 0, 0),
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                });
        }
        AddDecals(sites);
    }

    private Transform3D Seat(Piece piece, Vector3 centre, Basis turn)
    {
        var at = centre + turn * piece.Offset;
        at.Y = ground(at.X, at.Z);
        var basis = turn * piece.Basis;
        if (piece.Drape) basis = new Basis(new Quaternion(Vector3.Up, NormalAt(at.X, at.Z))) * basis;
        return new Transform3D(basis, at);
    }

    private Vector3 NormalAt(float x, float z)
    {
        float dx = ground(x + NormalStep, z) - ground(x - NormalStep, z);
        float dz = ground(x, z + NormalStep) - ground(x, z - NormalStep);
        return new Vector3(-dx, 2 * NormalStep, -dz).Normalized();
    }

    private Model? ModelOf(string id)
    {
        if (_models.TryGetValue(id, out var cached)) return cached;
        return _models[id] = catalog.ModelPath(id) is { } path ? Load(path) : null;
    }

    private static GltfDocumentExtensionConvertImporterMesh RegisterMeshConverter()
    {
        var converter = new GltfDocumentExtensionConvertImporterMesh();
        GltfDocument.RegisterGltfDocumentExtension(converter);
        return converter;
    }

    private Model? Load(string path)
    {
        Node? root = null;
        try
        {
            _ = MeshConverter;
            using var document = new GltfDocument();
            using var state = new GltfState();
            var error = document.AppendFromFile(path, state);
            if (error != Error.Ok) throw new InvalidOperationException($"glTF import failed: {error}");
            root = document.GenerateScene(state);
            var pieces = new List<Piece>();
            var footprints = new List<Footprint>();
            var outlines = new List<(string Name, Vector3 Offset, (double X, double Z)[] Hull)>();
            foreach (var child in root.GetChildren())
            {
                if (child is not Node3D part) continue;
                var mesh = new ArrayMesh();
                Extract(part, Transform3D.Identity, mesh);
                if (mesh.GetSurfaceCount() == 0) continue;
                string name = part.Name.ToString();
                pieces.Add(new Piece(mesh, part.Position, part.Basis, name.StartsWith("ground", StringComparison.OrdinalIgnoreCase)));
                if (FootprintOf(name, mesh, part.Position, part.Basis) is { } footprint) footprints.Add(footprint);
                outlines.Add((name, part.Position, OutlineOf(mesh, part.Position, part.Basis)));
            }
            if (pieces.Count == 0) throw new InvalidOperationException("Model has no renderable parts");
            int cityRing = ExtraOf(state, "cityRing")?.AsInt32() ?? 0;
            return new Model(pieces.ToArray(), ExtraOf(state, "clear")?.AsString() != "centre",
                ExtraOf(state, "faceRiver")?.AsBool() == true, cityRing,
                footprints.ToArray(), cityRing > 0 ? WallsFrom(outlines) : null);
        }
        catch (Exception e)
        {
            GD.PushWarning($"Could not load improvement model {path}: {e.Message}");
            return null;
        }
        finally { root?.Free(); }
    }

    /// <summary>The convex hull, seen from above, of every vertex of a part.</summary>
    private static (double X, double Z)[] OutlineOf(ArrayMesh mesh, Vector3 offset, Basis basis)
    {
        var points = new List<Vector2>();
        for (int s = 0; s < mesh.GetSurfaceCount(); s++)
            foreach (var v in mesh.SurfaceGetArrays(s)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
            {
                var p = offset + basis * v;
                points.Add(new Vector2(p.X, p.Z));
            }
        return Hull(points).Select(p => ((double)p.X, (double)p.Y)).ToArray();
    }

    /// <summary>A wall ring from its parts: every part is an obstacle, the wall line is the median hex distance of the
    /// part centres (curtains and towers stand on it), and a part named <c>gate*</c> gives the gate direction.</summary>
    private static CityWalls WallsFrom(List<(string Name, Vector3 Offset, (double X, double Z)[] Hull)> parts)
    {
        var solid = parts.Where(p => p.Hull.Length >= 3).ToList();
        var lines = solid.Where(p => !HasOwnGround(p.Name))
            .Select(p => CityLayout.HexDistance(p.Offset.X, p.Offset.Z)).Order().ToList();
        double radius = lines.Count > 0 ? lines[lines.Count / 2] : CityLayout.OpenEdge;
        double outer = solid.SelectMany(p => p.Hull).Select(v => CityLayout.HexDistance(v.X, v.Z)).DefaultIfEmpty(radius).Max();
        var gate = parts.FirstOrDefault(p => p.Name.StartsWith("gate", StringComparison.OrdinalIgnoreCase));
        double? gateAngle = gate.Name is null ? null : Math.Atan2(gate.Offset.Z, gate.Offset.X);
        return new CityWalls(solid.Select(p => p.Hull).ToArray(), radius, outer, gateAngle);
    }

    private static Variant? ExtraOf(GltfState state, string key)
    {
        var json = state.Json;
        if (!json.TryGetValue("scenes", out var scenes) || scenes.AsGodotArray() is not { Count: > 0 } list) return null;
        var scene = list[0].AsGodotDictionary();
        if (!scene.TryGetValue("extras", out var extras)) return null;
        return extras.AsGodotDictionary().TryGetValue(key, out var value) ? (Variant?)value : null;
    }

    /// <summary>Bake a part's mesh and its descendants, relative to the part's own origin, into one surface.</summary>
    private void Extract(Node node, Transform3D transform, ArrayMesh target)
    {
        if (node is MeshInstance3D { Mesh: not null } instance)
            for (int surface = 0; surface < instance.Mesh.GetSurfaceCount(); surface++)
            {
                using var tool = new SurfaceTool();
                tool.AppendFrom(instance.Mesh, surface, transform);
                tool.SetMaterial(_material);
                tool.Commit(target);
            }
        foreach (var child in node.GetChildren())
            if (child is Node3D spatial) Extract(spatial, transform * spatial.Transform, target);
    }
}
