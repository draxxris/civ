namespace Civ.Client.Terrain;

/// <summary>
/// The colour table the terrain and water shaders read with <c>texelFetch</c>: 256 texels wide, one row per shader table,
/// RGBA8 and sRGB. The layout lives here rather than in the renderer so the C# half of the shader-parity formula is
/// engine-free and testable.
/// <para>Rows: layer colours (alpha = ground spread), feature ground tints (alpha = strength), owner colours by
/// owner + 1, water tints per layer (alpha = <see cref="WaterTintStrength"/>), ragged-edge strength per layer (alpha; 0
/// for water, red = deep ocean, green = patch layer + 1, blue = patch share), feature looks (R =
/// <see cref="Game.FeatureGround"/>, G = its strength, B = <see cref="Game.FeatureHaze"/>, A = haze density), feature haze
/// colours, and texture turn per layer (R = 0..1 of 180°).</para>
/// </summary>
public static class PaletteTable
{
    public const int Width = 256, Rows = 8;

    public const int LayerRow = 0, FeatureRow = 1, OwnerRow = 2, WaterTintRow = 3, EdgeRow = 4, LookRow = 5, HazeRow = 6, TurnRow = 7;

    /// <summary>Alpha the water-tint row gives every tinted layer.</summary>
    public const float WaterTintStrength = 0.6f;

    /// <summary>Build the whole table for a palette. Index zero of the feature and owner rows means "no tint".</summary>
    public static byte[] Bytes(TerrainPalette palette, Func<string, Rgb?> waterTintOf)
    {
        var bytes = new byte[Width * Rows * 4];
        for (int i = 0; i < palette.Layers.Count; i++)
        {
            var layer = palette.Layers[i];
            Put(LayerRow, i, layer.Fallback, layer.Spread);
            if (waterTintOf(layer.Id) is Rgb tint) Put(WaterTintRow, i, tint, WaterTintStrength);
            Put(TurnRow, i, new Rgb(layer.Turn, 0, 0), 1);
            Put(EdgeRow, i, new Rgb(layer.Ocean ? 1 : 0, (layer.PatchLayer + 1) / 255f, layer.PatchShare), layer.Water ? 0 : 1);
        }
        for (int i = 1; i < palette.FeatureGrounds.Count; i++)
            if (palette.FeatureGrounds[i] is { } style)
            {
                Put(FeatureRow, i, style.Tint, style.TintStrength);
                var look = style.Look;
                Put(LookRow, i, new Rgb((int)look.Ground / 255f, Math.Clamp(look.GroundPercent, 0, 100) / 100f, (int)look.Haze / 255f),
                    look.Haze == Game.FeatureHaze.None ? 0 : Math.Clamp(look.HazePercent, 0, 100) / 100f);
                Put(HazeRow, i, Rgb.Hex(look.HazeRgb), 1);
            }
        for (int i = 1; i < Width; i++) Put(OwnerRow, i, MapStyle.PlayerColor(i - 1), 1);
        return bytes;

        void Put(int row, int index, Rgb colour, float alpha)
        {
            int at = (row * Width + index) * 4;
            bytes[at] = ToByte(colour.R);
            bytes[at + 1] = ToByte(colour.G);
            bytes[at + 2] = ToByte(colour.B);
            bytes[at + 3] = ToByte(alpha);
        }
    }

    /// <summary>One colour channel or alpha as a byte.</summary>
    public static byte ToByte(float v) => (byte)Math.Round(Math.Clamp(v, 0f, 1f) * 255);
}
