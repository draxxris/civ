using System.Security.Cryptography;
using Civ.Client.Terrain;
using Godot;

namespace Civ.Client;

/// <summary>Chunk-local prop batches. Wrap copies share instance transforms and colours in one MultiMesh.</summary>
internal sealed partial class ScatterRenderer(GameCatalog catalog, float[] copies) : Node3D
{
    private static readonly GltfDocumentExtensionConvertImporterMesh MeshConverter = RegisterMeshConverter();
    private readonly Dictionary<string, ArrayMesh?> _meshes = new(StringComparer.Ordinal);
    private readonly Dictionary<TextureKey, Texture2D> _textures = new();
    private readonly Dictionary<int, (Node3D Root, List<Batch> Batches)> _chunks = new();
    private readonly record struct TextureKey(int Width, int Height, Image.Format Format, string ContentHash);
    private sealed record Batch(MultiMesh Mesh, ScatterInstance[] Instances, Sight[] Sight);

    public void Rebuild(int chunk, IReadOnlyList<ScatterInstance> instances, TerrainSnapshot snapshot)
    {
        if (_chunks.Remove(chunk, out var old)) old.Root.Free();
        var root = new Node3D();
        var batches = new List<Batch>();
        AddChild(root);
        // Keep deterministic spatial thinning without changing the planner's density/blending contract.
        // Measured on the huge-map FPS probe: half density keeps the default camera above 20 FPS.
        foreach (var group in instances.Where(KeepProp).GroupBy(instance => (instance.Set, instance.ModelIndex)))
        {
            var paths = catalog.ScatterModels(group.Key.Set);
            var mesh = Model(paths[group.Key.ModelIndex]);
            if (mesh == null) continue;
            var props = group.ToArray();
            var multi = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true,
                Mesh = mesh, InstanceCount = props.Length,
            };
            for (int i = 0; i < props.Length; i++)
            {
                var prop = props[i];
                var basis = new Basis(Vector3.Up, prop.Yaw).Scaled(Vector3.One * prop.Scale);
                multi.SetInstanceTransform(i, new Transform3D(basis, new Vector3(prop.X, prop.Y, prop.Z)));
            }
            var batch = new Batch(multi, props, new Sight[props.Length]);
            UpdateColours(batch, snapshot, force: true);
            batches.Add(batch);
            foreach (float offset in copies)
                root.AddChild(new MultiMeshInstance3D
                {
                    Multimesh = multi, Position = new Vector3(offset, 0, 0),
                    VisibilityRangeEnd = 34f,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                });
        }
        _chunks[chunk] = (root, batches);
    }

    private static bool KeepProp(ScatterInstance instance)
    {
        // Explicit integer mixing is stable across processes and independent of candidate order or count.
        uint hash = 2166136261u;
        hash = unchecked((hash ^ (uint)instance.Tile) * 16777619u);
        hash = unchecked((hash ^ (uint)instance.ModelIndex) * 16777619u);
        hash = unchecked((hash ^ (uint)BitConverter.SingleToInt32Bits(instance.X)) * 16777619u);
        hash = unchecked((hash ^ (uint)BitConverter.SingleToInt32Bits(instance.Z)) * 16777619u);
        hash ^= hash >> 16;
        hash = unchecked(hash * 0x7feb352du);
        hash ^= hash >> 15;
        return (hash & 1) == 0;
    }

    /// <summary>Fog changes only colours, not candidates, transforms, model loads or wrap copies.</summary>
    public void UpdateVisibility(TerrainSnapshot snapshot)
    {
        foreach (var chunk in _chunks.Values)
            foreach (var batch in chunk.Batches) UpdateColours(batch, snapshot, force: false);
    }

    private static void UpdateColours(Batch batch, TerrainSnapshot snapshot, bool force)
    {
        for (int i = 0; i < batch.Instances.Length; i++)
        {
            var sight = snapshot.Sight[batch.Instances[i].Tile];
            if (!force && batch.Sight[i] == sight) continue;
            float fog = sight == Sight.Visible ? 1f : 0.55f;
            batch.Mesh.SetInstanceColor(i, new Color(fog, fog, fog));
            batch.Sight[i] = sight;
        }
    }

    private ArrayMesh? Model(string path)
    {
        if (_meshes.TryGetValue(path, out var cached)) return cached;
        return _meshes[path] = Load(path);
    }

    private static GltfDocumentExtensionConvertImporterMesh RegisterMeshConverter()
    {
        var converter = new GltfDocumentExtensionConvertImporterMesh();
        GltfDocument.RegisterGltfDocumentExtension(converter);
        return converter;
    }

    private ArrayMesh? Load(string path)
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
            var mesh = new ArrayMesh();
            Extract(root, Transform3D.Identity, mesh);
            if (mesh.GetSurfaceCount() == 0) throw new InvalidOperationException("Model has no renderable surfaces");
            return mesh;
        }
        catch (Exception e)
        {
            GD.PushWarning($"Could not load scatter model {path}: {e.Message}");
            return null;
        }
        finally { root?.Free(); }
    }

    /// <summary>Bake the complete scene transform, keeping every bark, leaf and plant surface and its UVs.
    /// The importer preserves glTF MASK as alpha scissor; duplicating the material keeps its cutoff and culling.</summary>
    private void Extract(Node node, Transform3D parent, ArrayMesh target)
    {
        var transform = node is Node3D spatial ? parent * spatial.Transform : parent;
        if (node is MeshInstance3D { Mesh: not null } instance)
        {
            for (int surface = 0; surface < instance.Mesh.GetSurfaceCount(); surface++)
            {
                using var tool = new SurfaceTool();
                tool.AppendFrom(instance.Mesh, surface, transform);
                var material = instance.GetActiveMaterial(surface)?.Duplicate() as StandardMaterial3D ?? new StandardMaterial3D();
                material.AlbedoTexture = ShareTexture(material.AlbedoTexture);
                material.NormalEnabled = false;
                material.NormalTexture = null;
                material.VertexColorUseAsAlbedo = true;
                tool.SetMaterial(material);
                tool.Commit(target);
            }
        }
        foreach (var child in node.GetChildren()) Extract(child, transform, target);
    }

    private Texture2D? ShareTexture(Texture2D? texture)
    {
        if (texture is null) return null;
        using var image = texture.GetImage();
        if (image.IsEmpty()) return texture;

        var key = new TextureKey(image.GetWidth(), image.GetHeight(), image.GetFormat(),
            Convert.ToHexString(SHA256.HashData(image.GetData())));
        if (_textures.TryGetValue(key, out var shared)) return shared;
        _textures.Add(key, texture);
        return texture;
    }
}
