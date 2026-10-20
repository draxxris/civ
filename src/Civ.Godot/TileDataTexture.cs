using Civ.Client.Terrain;
using Civ.Game;
using Godot;

namespace Civ.Client;

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

    /// <summary>Colour table rows.</summary>
    private const int PaletteWidth = 256, LayerRow = 0, FeatureRow = 1, OwnerRow = 2, WaterTintRow = 3, EdgeRow = 4, LookRow = 5, HazeRow = 6, TurnRow = 7, PaletteRows = 8;

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

    /// <summary>Colour table: layer colours (alpha = ground spread), feature ground tints (alpha = strength), owner colours by
    /// owner + 1, water tints per layer (alpha = strength), ragged-edge strength per layer (alpha; 0 for water, red = deep ocean,
    /// green = patch layer + 1, blue = patch share),
    /// feature looks (R = <see cref="FeatureGround"/>, G = its strength, B = <see cref="FeatureHaze"/>, A = haze density) and
    /// feature haze colours and texture turn per layer (R, 0..1 of 180°), all as sRGB bytes.</summary>
    public static ImageTexture PaletteTexture(TerrainPalette palette, Func<string, Rgb?> waterTintOf)
    {
        var bytes = new byte[PaletteWidth * PaletteRows * 4];
        for (int i = 0; i < palette.Layers.Count; i++)
        {
            var layer = palette.Layers[i];
            Put(LayerRow, i, layer.Fallback, layer.Spread);
            if (waterTintOf(layer.Id) is Rgb tint) Put(WaterTintRow, i, tint, 0.6f);
            Put(TurnRow, i, new Rgb(layer.Turn, 0, 0), 1);
            Put(EdgeRow, i, new Rgb(layer.Ocean ? 1 : 0, (layer.PatchLayer + 1) / 255f, layer.PatchShare), layer.Water ? 0 : 1);
        }
        for (int i = 1; i < palette.FeatureGrounds.Count; i++)
            if (palette.FeatureGrounds[i] is { } style)
            {
                Put(FeatureRow, i, style.Tint, style.TintStrength);
                var look = style.Look;
                Put(LookRow, i, new Rgb((int)look.Ground / 255f, Math.Clamp(look.GroundPercent, 0, 100) / 100f, (int)look.Haze / 255f),
                    look.Haze == FeatureHaze.None ? 0 : Math.Clamp(look.HazePercent, 0, 100) / 100f);
                Put(HazeRow, i, Rgb.Hex(look.HazeRgb), 1);
            }
        for (int i = 1; i < PaletteWidth; i++) Put(OwnerRow, i, MapStyle.PlayerColor(i - 1), 1);
        return ImageTexture.CreateFromImage(Image.CreateFromData(PaletteWidth, PaletteRows, false, Image.Format.Rgba8, bytes));

        void Put(int row, int index, Rgb colour, float alpha)
        {
            int at = (row * PaletteWidth + index) * 4;
            bytes[at] = ToByte(colour.R);
            bytes[at + 1] = ToByte(colour.G);
            bytes[at + 2] = ToByte(colour.B);
            bytes[at + 3] = ToByte(alpha);
        }
    }

    private static byte ToByte(float v) => (byte)Math.Round(Math.Clamp(v, 0f, 1f) * 255);
}
