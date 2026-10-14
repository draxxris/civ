using Godot;

namespace Civ.Client;

/// <summary>Loads each unit's .glb once (at runtime, through Godot's glTF importer) and hands out cheap copies of it.</summary>
internal sealed class ModelCache(GameCatalog catalog)
{
    private const float TargetSize = 0.85f;
    private readonly Dictionary<string, Node3D?> _templates = new();

    public bool Has(string type) => Template(type) != null;

    /// <summary>A fresh copy of the unit's model, scaled to fit one tile and standing on y = 0; null without a .glb.</summary>
    public Node3D? Instantiate(string type) => Template(type)?.Duplicate() as Node3D;

    private Node3D? Template(string type)
    {
        if (_templates.TryGetValue(type, out var cached)) return cached;
        return _templates[type] = Load(catalog.ModelPath(type));
    }

    private static Node3D? Load(string? path)
    {
        if (path == null) return null;
        try
        {
            var state = new GltfState();
            if (new GltfDocument().AppendFromFile(path, state) != Error.Ok) return null;
            if (new GltfDocument().GenerateScene(state) is not Node3D scene) return null;
            return Normalize(scene);
        }
        catch (Exception e)
        {
            GD.PushWarning($"Could not load model {path}: {e.Message}");
            return null;
        }
    }

    private static Node3D Normalize(Node3D scene)
    {
        var box = new Aabb();
        bool any = false;
        Measure(scene, Transform3D.Identity, ref box, ref any);
        var holder = new Node3D { Name = "Model" };
        holder.AddChild(scene);
        if (!any || box.Size.LengthSquared() < 1e-8f) return holder;
        float scale = TargetSize / Mathf.Max(box.Size.Y, Mathf.Max(box.Size.X, box.Size.Z) * 0.8f);
        var centre = new Vector3(box.GetCenter().X, box.Position.Y, box.GetCenter().Z);
        holder.Scale = Vector3.One * scale;
        holder.Position = -centre * scale + new Vector3(0, 0.03f, 0);
        return holder;
    }

    private static void Measure(Node node, Transform3D parent, ref Aabb box, ref bool any)
    {
        var local = parent;
        if (node is Node3D n3) local = parent * n3.Transform;
        if (node is MeshInstance3D { Mesh: not null } mesh)
        {
            var b = local * mesh.GetAabb();
            box = any ? box.Merge(b) : b;
            any = true;
        }
        foreach (var child in node.GetChildren()) Measure(child, local, ref box, ref any);
    }
}
