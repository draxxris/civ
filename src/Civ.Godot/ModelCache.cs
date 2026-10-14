using Godot;

namespace Civ.Client;

/// <summary>Loads each unit's .glb once (at runtime, through Godot's glTF importer) and hands out cheap copies of it.</summary>
internal sealed class ModelCache(GameCatalog catalog)
{
    private const float TargetSize = 0.85f;
    private static readonly GltfDocumentExtensionConvertImporterMesh MeshConverter = RegisterMeshConverter();
    private readonly Dictionary<string, PackedScene?> _templates = new();

    public bool Has(string type) => Template(type) != null;

    /// <summary>A fresh copy of the unit's model, scaled to fit one tile and standing on y = 0; null without a .glb.</summary>
    public Node3D? Instantiate(string type) => Template(type)?.Instantiate<Node3D>();

    private PackedScene? Template(string type)
    {
        if (_templates.TryGetValue(type, out var cached)) return cached;
        return _templates[type] = Load(catalog.ModelPath(type));
    }

    private static GltfDocumentExtensionConvertImporterMesh RegisterMeshConverter()
    {
        // Runtime glTF import otherwise leaves non-rendering ImporterMeshInstance3D nodes.
        var converter = new GltfDocumentExtensionConvertImporterMesh();
        GltfDocument.RegisterGltfDocumentExtension(converter);
        return converter;
    }

    private static PackedScene? Load(string? path)
    {
        if (path == null) return null;
        Node3D? root = null;
        try
        {
            // Keep the registered extension alive before creating the document.
            _ = MeshConverter;
            using var document = new GltfDocument();
            using var state = new GltfState();
            var error = document.AppendFromFile(path, state);
            if (error != Error.Ok) throw new InvalidOperationException($"glTF import failed: {error}");
            if (document.GenerateScene(state) is not Node3D scene) return null;
            root = scene;
            root = Normalize(scene);
            SetOwner(root, root);
            var packed = new PackedScene();
            error = packed.Pack(root);
            if (error == Error.Ok) return packed;
            packed.Dispose();
            throw new InvalidOperationException($"Scene packing failed: {error}");
        }
        catch (Exception e)
        {
            GD.PushWarning($"Could not load model {path}: {e.Message}");
            return null;
        }
        finally
        {
            root?.Free();
        }
    }

    private static void SetOwner(Node node, Node root)
    {
        foreach (var child in node.GetChildren())
        {
            child.Owner = root;
            SetOwner(child, root);
        }
    }

    private static Node3D Normalize(Node3D scene)
    {
        var box = new Aabb();
        bool any = false;
        Measure(scene, Transform3D.Identity, ref box, ref any);
        if (!any || box.Size.LengthSquared() < 1e-8f) throw new InvalidOperationException("Model has no renderable geometry");
        var holder = new Node3D { Name = "Model" };
        holder.AddChild(scene);
        float scale = TargetSize / Mathf.Max(box.Size.Y, Mathf.Max(box.Size.X, box.Size.Z) * 0.8f);
        var centre = new Vector3(box.GetCenter().X, box.Position.Y, box.GetCenter().Z);
        holder.Scale = Vector3.One * scale;
        holder.Position = -centre * scale + new Vector3(0, 0.03f, 0);
        return holder;
    }

    private static Aabb Bounds(MeshInstance3D mesh, Transform3D local)
    {
        if (mesh.Skin is not { } skin || mesh.GetNodeOrNull<Skeleton3D>(mesh.Skeleton) is not { } skeleton)
            return local * mesh.GetAabb();

        // Raw mesh vertices are in bind space, not the character's standing pose.
        // Godot reparents skinned meshes beneath the skeleton during import.
        var binds = RestBinds(skin, skeleton);
        var box = new Aabb();
        bool any = false;
        for (int surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
        {
            var arrays = mesh.Mesh.SurfaceGetArrays(surface);
            var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            if (vertices.Length == 0) continue;
            var joints = arrays[(int)Mesh.ArrayType.Bones].AsInt32Array();
            var weights = arrays[(int)Mesh.ArrayType.Weights].AsFloat32Array();
            int influences = joints.Length / vertices.Length;
            for (int i = 0; i < vertices.Length; i++)
            {
                var point = SkinVertex(vertices[i], i * influences, influences, joints, weights, binds);
                box = any ? box.Expand(point) : new Aabb(point, Vector3.Zero);
                any = true;
            }
        }
        return ModelTransform(skeleton) * box;
    }

    private static Transform3D[] RestBinds(Skin skin, Skeleton3D skeleton)
    {
        var binds = new Transform3D[skin.GetBindCount()];
        for (int i = 0; i < binds.Length; i++)
        {
            int bone = skin.GetBindBone(i);
            if (bone < 0) bone = skeleton.FindBone(skin.GetBindName(i));
            if (bone < 0) throw new InvalidOperationException("Model has an unresolved skin binding");
            binds[i] = skeleton.GetBoneGlobalRest(bone) * skin.GetBindPose(i);
        }
        return binds;
    }

    private static Vector3 SkinVertex(Vector3 vertex, int offset, int influences, int[] joints, float[] weights, Transform3D[] binds)
    {
        var point = Vector3.Zero;
        for (int i = offset; i < offset + influences; i++) point += (binds[joints[i]] * vertex) * weights[i];
        return point;
    }

    private static Transform3D ModelTransform(Node3D node)
    {
        var transform = node.Transform;
        for (var parent = node.GetParent(); parent != null; parent = parent.GetParent())
            if (parent is Node3D spatial) transform = spatial.Transform * transform;
        return transform;
    }

    private static void Measure(Node node, Transform3D parent, ref Aabb box, ref bool any)
    {
        var local = parent;
        if (node is Node3D n3) local = parent * n3.Transform;
        if (node is MeshInstance3D { Mesh: not null } mesh)
        {
            var b = Bounds(mesh, local);
            box = any ? box.Merge(b) : b;
            any = true;
        }
        foreach (var child in node.GetChildren()) Measure(child, local, ref box, ref any);
    }
}
