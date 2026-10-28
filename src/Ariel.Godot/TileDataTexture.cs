using Ariel.Client.Terrain;
using Ariel.Game;
using Godot;

namespace Ariel.Client;

/// <summary>
/// The per-tile data the terrain and water shaders read with <c>texelFetch</c>: two RGBA8 images of Width × Height texels
/// (offset coordinates, texel (col, row) is tile <c>row · Width + col</c>), the baked domain-warp grid and the colour table.
/// Everything is RGBA8 with nearest filtering, which the Compatibility renderer supports everywhere; the warp offsets are
/// packed as two 16-bit values per texel instead of using float textures.
/// </summary>
internal sealed class TileDataTexture
{
    /// <summary>Overlay marks in <c>tileB.G</c>.</summary>
    public const byte MarkNone = 0, MarkWorkable = 1, MarkWorked = 2, MarkLocked = 3;

    private readonly int _width, _height;
    private readonly byte[] _a, _b;
    private readonly Image _imageA, _imageB;

    public TileDataTexture(int width, int height)
    {
        _width = width;
        _height = height;
        _a = new byte[width * height * 4];
        _b = new byte[width * height * 4];
        _imageA = Image.CreateFromData(width, height, false, Image.Format.Rgba8, _a);
        _imageB = Image.CreateFromData(width, height, false, Image.Format.Rgba8, _b);
        TileA = ImageTexture.CreateFromImage(_imageA);
        TileB = ImageTexture.CreateFromImage(_imageB);
    }

    /// <summary>R = terrain layer, G = feature ground, B = owner + 1, A = river edge bits.</summary>
    public ImageTexture TileA { get; }
    /// <summary>R = sight, G = overlay mark, B = hover flag (255), A = reserved.</summary>
    public ImageTexture TileB { get; }

    /// <summary>Upload the world data of a snapshot (hidden tiles are already zero in the snapshot).</summary>
    public void WriteTileA(TerrainSnapshot snapshot)
    {
        for (int i = 0; i < _width * _height; i++)
        {
            _a[i * 4] = snapshot.Layer[i];
            _a[i * 4 + 1] = snapshot.FeatureGround[i];
            _a[i * 4 + 2] = snapshot.Owner[i];
            _a[i * 4 + 3] = snapshot.Rivers[i];
        }
        _imageA.SetData(_width, _height, false, Image.Format.Rgba8, _a);
        TileA.Update(_imageA);
    }

    /// <summary>Upload sight, overlay marks and the hovered tile (or -1).</summary>
    public void WriteTileB(TerrainSnapshot snapshot, byte[] marks, int hover)
    {
        for (int i = 0; i < _width * _height; i++)
        {
            _b[i * 4] = (byte)snapshot.Sight[i];
            _b[i * 4 + 1] = marks[i];
            _b[i * 4 + 2] = i == hover ? (byte)255 : (byte)0;
            _b[i * 4 + 3] = 0;
        }
        _imageB.SetData(_width, _height, false, Image.Format.Rgba8, _b);
        TileB.Update(_imageB);
    }

    /// <summary>The warp grid as Columns × Rows texels: (dx, dz) each quantised to 16 bits over [-1, 1].</summary>
    public static ImageTexture WarpTexture(WarpGrid warp)
    {
        var bytes = new byte[warp.Columns * warp.Rows * 4];
        for (int i = 0; i < warp.Columns * warp.Rows; i++)
        {
            Pack(warp.Data[i * 2], bytes, i * 4);
            Pack(warp.Data[i * 2 + 1], bytes, i * 4 + 2);
        }
        return ImageTexture.CreateFromImage(Image.CreateFromData(warp.Columns, warp.Rows, false, Image.Format.Rgba8, bytes));

        static void Pack(float v, byte[] to, int at)
        {
            int u = (int)Math.Round((Math.Clamp(v, -1f, 1f) + 1) / 2 * 65535);
            to[at] = (byte)(u >> 8);
            to[at + 1] = (byte)(u & 255);
        }
    }

    /// <summary>Colour table: the row layout is <see cref="PaletteTable"/>'s, which is where the shader parity is tested.</summary>
    public static ImageTexture PaletteTexture(TerrainPalette palette, Func<string, Rgb?> waterTintOf) =>
        ImageTexture.CreateFromImage(Image.CreateFromData(PaletteTable.Width, PaletteTable.Rows, false, Image.Format.Rgba8,
            PaletteTable.Bytes(palette, waterTintOf)));
}
