using Civ.Client.Terrain;
using Godot;

namespace Civ.Client;

/// <summary>Raymarched haze over features: fog and storm clouds from the baked <see cref="HazeVolume"/> maps, drawn in one
/// box per hazy terrain chunk (cloud_volume.gdshader), and a smoke plume over each smoking crater (plume_volume.gdshader).
/// Every box is its own instance, so the engine sorts them back to front; wrap copies share the box meshes.</summary>
internal sealed partial class HazeRenderer : Node3D
{
    private const int NoiseSize = 64;
    /// <summary>Plume boxes reach this far from the crater sideways and this high above it.</summary>
    private const float PlumeReach = 1.4f, PlumeHeight = 1.95f;
    private static Texture3D? _noise;

    private readonly IReadOnlyList<TerrainChunk> _chunks;
    private readonly float[] _copies;
    private readonly ShaderMaterial _clouds, _plumes;
    private readonly List<MeshInstance3D> _instances = new();
    private ImageTexture? _shape, _look;

    public HazeRenderer(IReadOnlyList<TerrainChunk> chunks, float[] copies, double period)
    {
        _chunks = chunks;
        _copies = copies;
        var noise = _noise ??= NoiseTexture();
        // A whole number of noise tiles fits the map period, so the wrap shows no seam.
        float frequency = (float)(Math.Max(1, Math.Round(period * 0.9)) / period);
        _clouds = Material("res://shaders/cloud_volume.gdshader", noise, frequency);
        _plumes = Material("res://shaders/plume_volume.gdshader", noise, frequency);
    }

    /// <summary>Re-bake the haze of the field's snapshot and rebuild every box.</summary>
    public void Rebuild(TerrainField field, IReadOnlyList<FeatureStyle?> grounds)
    {
        var volume = HazeVolume.Bake(field, grounds);
        foreach (var instance in _instances) instance.QueueFree();
        _instances.Clear();
        if (volume.Any)
        {
            Upload(volume);
            foreach (var chunk in _chunks)
                if (volume.BoxIn(chunk) is { } box) Add(BoxMesh(box, new Color(Enc(box.Y0), Enc(box.Y1), 0, 1), _clouds));
        }
        foreach (var plume in volume.Plumes)
        {
            var box = new HazeBox(plume.X - PlumeReach, plume.Y - 0.02, plume.Z - PlumeReach,
                plume.X + PlumeReach, plume.Y + PlumeHeight, plume.Z + PlumeReach);
            Add(BoxMesh(box, new Color(Enc(box.Y0), Enc(box.Y1), plume.Fog, (float)plume.Density), _plumes));
        }
    }

    private void Upload(HazeVolume volume)
    {
        using var shape = Image.CreateFromData(volume.Width, volume.Height, false, Image.Format.Rgba8, volume.Shape);
        using var look = Image.CreateFromData(volume.Width, volume.Height, false, Image.Format.Rgba8, volume.Look);
        if (_shape is null || _shape.GetWidth() != volume.Width || _shape.GetHeight() != volume.Height)
        {
            _shape = ImageTexture.CreateFromImage(shape);
            _look = ImageTexture.CreateFromImage(look);
            _clouds.SetShaderParameter("haze_shape", _shape);
            _clouds.SetShaderParameter("haze_look", _look);
            _clouds.SetShaderParameter("period", (float)volume.Period);
            _clouds.SetShaderParameter("z_min", (float)volume.ZMin);
            _clouds.SetShaderParameter("z_span", (float)volume.ZSpan);
        }
        else
        {
            _shape.Update(shape);
            _look!.Update(look);
        }
    }

    private void Add(ArrayMesh mesh)
    {
        foreach (float offset in _copies)
        {
            var instance = new MeshInstance3D
            {
                Mesh = mesh, Position = new Vector3(offset, 0, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(instance);
            _instances.Add(instance);
        }
    }

    private static float Enc(double height) => HazeVolume.EncodeHeight(height) / 255f;

    /// <summary>A closed box whose every vertex carries the box itself (see volume_common.gdshaderinc).</summary>
    private static ArrayMesh BoxMesh(HazeBox box, Color heights, Material material)
    {
        var lo = new Vector3((float)box.X0, (float)box.Y0, (float)box.Z0);
        var hi = new Vector3((float)box.X1, (float)box.Y1, (float)box.Z1);
        var corners = new Vector3[8];
        for (int i = 0; i < 8; i++)
            corners[i] = new Vector3((i & 1) != 0 ? hi.X : lo.X, (i & 2) != 0 ? hi.Y : lo.Y, (i & 4) != 0 ? hi.Z : lo.Z);
        // Faces wound clockwise seen from outside (Godot's front faces).
        int[][] faces = [[0, 2, 3, 1], [4, 5, 7, 6], [0, 1, 5, 4], [2, 6, 7, 3], [0, 4, 6, 2], [1, 3, 7, 5]];
        var indices = new List<int>();
        foreach (var f in faces) indices.AddRange([f[0], f[2], f[1], f[0], f[3], f[2]]);
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = corners;
        arrays[(int)Mesh.ArrayType.TexUV] = Enumerable.Repeat(new Vector2(lo.X, lo.Z), 8).ToArray();
        arrays[(int)Mesh.ArrayType.TexUV2] = Enumerable.Repeat(new Vector2(hi.X, hi.Z), 8).ToArray();
        arrays[(int)Mesh.ArrayType.Color] = Enumerable.Repeat(heights, 8).ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    private static ShaderMaterial Material(string path, Texture3D noise, float frequency)
    {
        // After the water, so the sea shows through.
        var material = new ShaderMaterial { Shader = GD.Load<Shader>(path), RenderPriority = 2 };
        material.SetShaderParameter("cloud_noise", noise);
        material.SetShaderParameter("noise_frequency", frequency);
        return material;
    }

    private static Texture3D NoiseTexture()
    {
        var data = CloudNoise.Build(NoiseSize);
        var slices = new Godot.Collections.Array<Image>();
        int slice = NoiseSize * NoiseSize * 4;
        for (int z = 0; z < NoiseSize; z++)
            slices.Add(Image.CreateFromData(NoiseSize, NoiseSize, false, Image.Format.Rgba8, data.AsSpan(z * slice, slice).ToArray()));
        var texture = new ImageTexture3D();
        var error = texture.Create(Image.Format.Rgba8, NoiseSize, NoiseSize, NoiseSize, false, slices);
        if (error != Error.Ok) throw new InvalidOperationException($"Cloud noise texture creation failed: {error}");
        return texture;
    }
}
