using Godot;

namespace Civ.Client;

/// <summary>Packs the icons the map needs into one texture so a whole layer of icons is a single draw call.</summary>
internal sealed class IconAtlas
{
    private const int Size = 2048;
    private const int Cell = 64;
    private const int PerRow = Size / Cell;

    private readonly IconCache _icons;
    private readonly Image _image = Image.CreateEmpty(Size, Size, false, Image.Format.Rgba8);
    private readonly Dictionary<string, Rect2?> _cells = new();
    private bool _dirty;
    private int _next = 1;

    /// <summary>UV of a solid white pixel (cell 0), so coloured shapes can share the textured material.</summary>
    public Vector2 WhiteUv => new(2f / Size, 2f / Size);

    public ImageTexture Texture { get; }

    public IconAtlas(IconCache icons)
    {
        _icons = icons;
        _image.Fill(new Color(0, 0, 0, 0));
        _image.FillRect(new Rect2I(0, 0, 4, 4), Colors.White);
        Texture = ImageTexture.CreateFromImage(_image);
    }

    /// <summary>UV rectangle of an icon, adding it on first use; null when the id has no art or the atlas is full.</summary>
    public Rect2? UvOf(string? id)
    {
        if (id == null) return null;
        if (_cells.TryGetValue(id, out var cell)) return cell;
        var source = _icons.ImageOf(id);
        if (source == null || _next >= PerRow * PerRow) return _cells[id] = null;
        int x = _next % PerRow * Cell, y = _next / PerRow * Cell;
        _next++;
        var sized = (Image)source.Duplicate();
        if (sized.GetWidth() != Cell || sized.GetHeight() != Cell) sized.Resize(Cell, Cell, Image.Interpolation.Bilinear);
        _image.BlitRect(sized, new Rect2I(0, 0, Cell, Cell), new Vector2I(x, y));
        _dirty = true;
        const float inset = 0.5f / Size;
        return _cells[id] = new Rect2(x / (float)Size + inset, y / (float)Size + inset, (float)Cell / Size - 2 * inset, (float)Cell / Size - 2 * inset);
    }

    /// <summary>Upload newly packed icons to the GPU; call once after building a layer.</summary>
    public void Flush()
    {
        if (!_dirty) return;
        Texture.Update(_image);
        _dirty = false;
    }
}
