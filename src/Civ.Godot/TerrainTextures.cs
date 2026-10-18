using Civ.Client.Terrain;
using Godot;

namespace Civ.Client;

/// <summary>Albedo RGB and texture height alpha in a mipmapped, uniformly sized RGBA8 array.</summary>
internal sealed class TerrainTextures
{
    private const int Size = 1024;

    public Texture2DArray Array { get; }

    public TerrainTextures(TerrainPalette palette)
    {
        var images = new Godot.Collections.Array<Image>();
        foreach (var layer in palette.Layers) images.Add(LayerImage(layer));
        Array = new Texture2DArray();
        var error = Array.CreateFromImages(images);
        foreach (var image in images) image.Dispose();
        if (error != Error.Ok) throw new InvalidOperationException($"Terrain texture array creation failed: {error}");
    }

    private static Image LayerImage(TerrainPaletteLayer layer)
    {
        Image? image = layer.TexturePath is { } path ? Image.LoadFromFile(path) : null;
        if (image is null || image.IsEmpty())
        {
            image?.Dispose();
            if (layer.TexturePath is not null) GD.PushWarning($"Terrain texture could not load: {layer.TexturePath}");
            image = Image.CreateEmpty(4, 4, false, Image.Format.Rgba8);
            image.Fill(new Color(layer.Fallback.R, layer.Fallback.G, layer.Fallback.B, 0.5f));
        }
        image.ClearMipmaps();
        image.Convert(Image.Format.Rgba8);
        image.Resize(Size, Size, Image.Interpolation.Lanczos);
        var error = image.GenerateMipmaps();
        if (error != Error.Ok) throw new InvalidOperationException($"Terrain mipmap generation failed for {layer.Id}: {error}");
        return image;
    }
}
