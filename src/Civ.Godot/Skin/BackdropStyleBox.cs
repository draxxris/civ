using Godot;

namespace Civ.Client;

/// <summary>
/// The backdrop behind the widgets, after the reference's ground: warm dark paint with broad, soft clouds that
/// drift between brown and a cooler grey, streaks that rise gently to the right, and a fine grain. It has no rim,
/// no brushwork and no scrapes, so it does not read as a widget plate.
/// Three tiled layers keep it from repeating: clouds with a period wider than most screens, then a streak overlay
/// and a grain overlay, with periods that do not divide each other.
/// Measured on docs/references/ui.png at game size: mean (52, 45, 36); standard deviation about 2.6 in red,
/// 1.9 in green and 1.6 in blue, so the colour varies a little more than the brightness.
/// </summary>
internal sealed partial class BackdropStyleBox : StyleBox
{
    /// <summary>
    /// The cloud texture, in texels of <see cref="CloudTexel"/> units: soft enough to paint coarsely and magnify.
    /// Its period, 960 x 640 units, is wider than most screens. Clouds follow y + 2x / 3, which also tiles.
    /// </summary>
    private const int CloudWidth = 240, CloudHeight = 160, CloudTexel = 4;
    /// <summary>
    /// Streak tile, in units. Streaks follow y + x / 2: after one period across, they have moved one period down,
    /// so the sheared noise still tiles.
    /// </summary>
    private const int StreakWidth = 224, StreakHeight = 112;
    /// <summary>Grain tile, in units: not a multiple of the streak tile, so the two never line up the same way.</summary>
    private const int GrainSize = 160;

    public required Texture2D Clouds { get; init; }
    /// <summary>Screen pixels per cloud texel.</summary>
    public required int CloudMagnify { get; init; }
    public required Texture2D Streaks { get; init; }
    public required Texture2D Grain { get; init; }

    public override void _Draw(Rid toCanvasItem, Rect2 rect)
    {
        if (rect.Size.X <= 0 || rect.Size.Y <= 0) return;
        BrushedStyleBox.DrawTiled(toCanvasItem, rect, Clouds, Vector2.Zero, CloudMagnify);
        BrushedStyleBox.DrawTiled(toCanvasItem, rect, Streaks, Vector2.Zero);
        BrushedStyleBox.DrawTiled(toCanvasItem, rect, Grain, new Vector2(37, 59));
    }

    /// <summary>Paints the three layers at <paramref name="scale"/> pixels per unit around <paramref name="colour"/>.</summary>
    public static BackdropStyleBox Paint(Color colour, int scale)
    {
        var layers = SkinCache.Images(SkinCache.BackdropKey(colour, scale), 3, () =>
        [
            Pixels(CloudWidth, CloudHeight, (x, y) => CloudAt(colour, x + 0.5f, y + 0.5f)),
            Pixels(StreakWidth * scale, StreakHeight * scale, (x, y) => Overlay(StreakAt((x + 0.5f) / scale, (y + 0.5f) / scale))),
            Pixels(GrainSize * scale, GrainSize * scale, (x, y) => Overlay(GrainAt(x, y, GrainSize * scale))),
        ]);
        return new BackdropStyleBox
        {
            Clouds = ImageTexture.CreateFromImage(layers[0]),
            CloudMagnify = CloudTexel * scale,
            Streaks = ImageTexture.CreateFromImage(layers[1]),
            Grain = ImageTexture.CreateFromImage(layers[2]),
        };
    }

    private static readonly PeriodicNoise CloudNoise = new(CloudWidth, CloudHeight, 71);
    private static readonly PeriodicNoise StreakNoise = new(StreakWidth, StreakHeight, 73);

    /// <summary>
    /// The base paint at texel (x, y): broad, soft clouds pulled into streaks that rise to the right, a little
    /// lighter or darker, and warmer or cooler, from place to place.
    /// </summary>
    private static Color CloudAt(Color colour, float x, float y)
    {
        // Shear so the long cells lie along the streaks, and warp them so no edge runs straight.
        var s = y + 2f * x / 3f;
        float wx = 3f * CloudNoise.Sample(x, s, 40, 40, 1), ws = 2f * CloudNoise.Sample(x + 17f, s + 5f, 40, 40, 2);
        x += wx;
        s += ws;
        var light = 0.5f * CloudNoise.Sample(x, s, 24, 16, 3) + 0.3f * CloudNoise.Sample(x, s, 12, 8, 4) + 0.2f * CloudNoise.Sample(x, s, 6, 4, 5);
        var warmth = 0.6f * CloudNoise.Sample(x + 40f, s + 11f, 48, 32, 6) + 0.4f * CloudNoise.Sample(x, s, 12, 8, 7);
        var c = colour * (1f + 0.11f * light);
        // Warm patches lean to brown, cool ones to slate grey: red moves most, blue least.
        c += new Color(0.024f, 0.008f, -0.004f) * warmth;
        return c with { A = 1f };
    }

    /// <summary>
    /// Mid-sized mottles, a little drawn out along lines rising to the right, as a signed shade in about [-1, 1].
    /// The warp is strong, so the lines break up into blotches rather than run parallel.
    /// </summary>
    private static float StreakAt(float x, float y)
    {
        var s = y + 0.5f * x;
        float wx = 6f * StreakNoise.Sample(x, s, 16, 16, 1), ws = 5f * StreakNoise.Sample(x + 3f, s + 9f, 16, 16, 4);
        x += wx;
        s += ws;
        return 0.6f * StreakNoise.Sample(x, s, 16, 8, 2) + 0.4f * StreakNoise.Sample(x, s, 8, 8, 3);
    }

    /// <summary>Fine grain, one or two pixels across, at any scale; signed, in about [-1, 1].</summary>
    private static float GrainAt(int x, int y, int period)
    {
        var noise = new PeriodicNoise(period, period, 79);
        return 0.55f * noise.Sample(x, y, 1, 1) + 0.3f * noise.Sample(x, y, 2, 2) + 0.15f * noise.Sample(x, y, 4, 1, 3);
    }

    /// <summary>
    /// A signed shade as an overlay pixel: white to lighten, black to darken, with alpha for the strength. On the
    /// backdrop's dark paint either extreme moves the brightness by about three levels of 255.
    /// </summary>
    private static Color Overlay(float shade) => shade >= 0f ? new Color(1f, 0.96f, 0.88f, 0.016f * shade) : new Color(0f, 0f, 0f, -0.065f * shade);

    private static Image Pixels(int width, int height, Func<int, int, Color> pixel)
    {
        var bytes = new byte[width * height * 4];
        Parallel.For(0, height, y =>
        {
            for (var x = 0; x < width; x++)
            {
                var c = pixel(x, y);
                var i = (y * width + x) * 4;
                bytes[i] = (byte)Mathf.RoundToInt(Mathf.Clamp(c.R, 0f, 1f) * 255f);
                bytes[i + 1] = (byte)Mathf.RoundToInt(Mathf.Clamp(c.G, 0f, 1f) * 255f);
                bytes[i + 2] = (byte)Mathf.RoundToInt(Mathf.Clamp(c.B, 0f, 1f) * 255f);
                bytes[i + 3] = (byte)Mathf.RoundToInt(Mathf.Clamp(c.A, 0f, 1f) * 255f);
            }
        });
        return Image.CreateFromData(width, height, false, Image.Format.Rgba8, bytes);
    }
}
