using Godot;

namespace Civ.Client;

/// <summary>
/// Procedural "steel lapis" button skin (option A2 in docs/references/button-pcg.png,
/// mocked up by tools/ui-mockups/button_pcg.py). Two layers:
/// - a 9-slice frame per state (gradient, inner glow, rim, lip): fixed corners, a middle
///   that tiles horizontally (the lip noise wraps, so tiles join without seams) and
///   stretches vertically (the gradient spans the button);
/// - a shared stone-grain tile drawn on top at 1:1, so the grain never stretches into streaks.
/// Each state has frames at a few bevel scales; short buttons get a thinner bevel.
/// </summary>
internal static class ButtonSkin
{
    public enum State { Normal, Hover, Pressed, Disabled }

    // Texture layout. The middle tile is one noise period wide.
    private const int Margin = 12, Period = 128, Height = 32, VerticalMargin = 10;
    private const int Width = Margin + Period + Margin;
    private const int Supersample = 4;

    private const float Groove = 0.18f, Mottle = 0.28f, NickDepth = 0.55f, Grain = 0.22f;
    private const int Nicks = 7, Seed = 7;
    private const float GlowStrength = 0.5f;

    /// <summary>Bevel scale by button height: the full bevel from 34 px, thinner below.</summary>
    private static readonly (float MinHeight, float Scale)[] Sizes = [(34f, 1f), (26f, 0.75f), (0f, 0.55f)];

    private static readonly Color RimColor = Hex("#05080b");

    /// <summary>Colours per look. Disabled is charcoal stone, after docs/references/button-disabled.png.</summary>
    private readonly record struct Palette(Color Top, Color Bottom, Color Glow, Color Lip);

    private static readonly Palette Lapis = new(Hex("#2a3d51"), Hex("#233548"), Hex("#5f9ad6"), Hex("#6495bb"));
    private static readonly Palette Charcoal = new(Hex("#2e3236"), Hex("#272a2e"), Hex("#8a9096"), Hex("#4a4e52"));

    private static readonly Texture2D GrainTile = MakeGrainTile();

    public static StyleBox Make(State state)
    {
        var nicks = MakeNicks();
        var frames = new GrainedStyleBox.Frame[Sizes.Length];
        for (var i = 0; i < Sizes.Length; i++)
        {
            var bevel = Bevel.At(Sizes[i].Scale);
            var texture = new Painter(state, bevel, nicks).Paint();
            // Inset past the rim and the corner cut, so grain never lands outside the button.
            frames[i] = new GrainedStyleBox.Frame(Sizes[i].MinHeight, MakeFrame(texture), bevel.Rim + 1f);
        }
        var box = new GrainedStyleBox { Frames = frames, Grain = GrainTile };
        // Content margins at least the texture margins, so a button is never smaller than its corners.
        var pressed = state == State.Pressed;
        box.SetContentMargin(Side.Left, Margin);
        box.SetContentMargin(Side.Right, Margin);
        box.SetContentMargin(Side.Top, pressed ? 6 : 5);
        box.SetContentMargin(Side.Bottom, pressed ? 4 : 5);
        return box;
    }

    private static StyleBoxTexture MakeFrame(Texture2D texture)
    {
        var frame = new StyleBoxTexture
        {
            Texture = texture,
            AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.TileFit,
            AxisStretchVertical = StyleBoxTexture.AxisStretchMode.Stretch,
        };
        frame.SetTextureMargin(Side.Left, Margin);
        frame.SetTextureMargin(Side.Right, Margin);
        frame.SetTextureMargin(Side.Top, VerticalMargin);
        frame.SetTextureMargin(Side.Bottom, VerticalMargin);
        return frame;
    }

    /// <summary>Bevel geometry in pixels; <see cref="At"/> scales the full-size bevel.</summary>
    private readonly record struct Bevel(float Chamfer, float Rim, float LipPeak, float LipWidth, float GrooveOffset, float GlowWidth, float Shadow)
    {
        public static Bevel At(float s) => new(
            Chamfer: 4f * s,
            Rim: Mathf.Max(1f, 2f * s),
            LipPeak: 1.4f * s,
            LipWidth: Mathf.Max(0.6f, s),
            GrooveOffset: 2f * s,
            GlowWidth: 5f * s,
            Shadow: 4f * s);
    }

    /// <summary>Paints one frame texture for one state at one bevel size.</summary>
    private sealed class Painter(State state, Bevel bevel, Nick[] nicks)
    {
        private readonly Palette _palette = state == State.Disabled ? Charcoal : Lapis;

        public Texture2D Paint()
        {
            var image = Image.CreateEmpty(Width, Height, false, Image.Format.Rgba8);
            for (var y = 0; y < Height; y++)
                for (var x = 0; x < Width; x++)
                    image.SetPixel(x, y, SamplePixel(x, y));
            return ImageTexture.CreateFromImage(image);
        }

        /// <summary>Average of Supersample² samples, so the outline and chamfers are anti-aliased.</summary>
        private Color SamplePixel(int px, int py)
        {
            var sum = new Color(0, 0, 0, 0);
            for (var sy = 0; sy < Supersample; sy++)
            {
                for (var sx = 0; sx < Supersample; sx++)
                {
                    var c = Shade(px + (sx + 0.5f) / Supersample, py + (sy + 0.5f) / Supersample);
                    sum += new Color(c.R * c.A, c.G * c.A, c.B * c.A, c.A);
                }
            }
            sum /= Supersample * Supersample;
            return sum.A <= 0f ? new Color(0, 0, 0, 0) : new Color(sum.R / sum.A, sum.G / sum.A, sum.B / sum.A, sum.A);
        }

        private Color Shade(float x, float y)
        {
            var inset = -Distance(x, y);
            if (inset <= 0f) return new Color(0, 0, 0, 0);
            if (inset < bevel.Rim) return RimColor;

            var normal = Normal(x, y);
            var p = inset - bevel.Rim;
            var colour = Fill(x, y);
            colour *= 1f - Groove * Gauss(p - bevel.LipPeak - bevel.GrooveOffset, bevel.LipWidth);
            colour = colour.Lerp(LipColour(x, y, normal), LipAmount(p));
            colour *= 1f - BottomShadow(y, p, normal);
            colour.A = 1f;
            return colour;
        }

        /// <summary>Vertical gradient with the inner glow on top. The grain tile is drawn over it at draw time.</summary>
        private Color Fill(float x, float y)
        {
            var t = y / Height;
            var (top, bottom) = state switch
            {
                State.Hover => (Scale(_palette.Top, 1.18f, 0.02f), Scale(_palette.Bottom, 1.12f, 0.01f)),
                State.Pressed => (Scale(_palette.Bottom, 0.8f, 0f), Scale(_palette.Top, 0.85f, 0f)),
                _ => (_palette.Top, _palette.Bottom),
            };
            var fill = top.Lerp(bottom, t);

            var glowFalloff = GlowFalloff(x, y);
            var strength = GlowStrength * state switch { State.Hover => 1.6f, State.Pressed => 0.35f, State.Disabled => 0.2f, _ => 1f };
            fill += _palette.Glow * (glowFalloff * strength * 0.55f);
            if (state == State.Pressed && t < 0.6f) fill *= 1f - 0.45f * glowFalloff; // inner shadow under the top edge
            fill.A = 1f;
            return fill;
        }

        /// <summary>
        /// Inner glow from the nearest edge. Glow from the sides fades out inside the side margins,
        /// so the tiled middle only carries glow from the top and bottom and the tiles join cleanly.
        /// </summary>
        private float GlowFalloff(float x, float y)
        {
            float FromEdge(float d) => Mathf.Exp(-Mathf.Max(d - bevel.Rim - bevel.LipPeak, 0f) / bevel.GlowWidth);
            var dy = Mathf.Min(y, Height - y);
            var dx = Mathf.Min(x, Width - x);
            var side = FromEdge(dx) * Mathf.SmoothStep(Margin, Margin - 4f, dx);
            return Mathf.Max(FromEdge(dy), side);
        }

        /// <summary>The raised lip: lit from the top and right (flipped when pressed), mottled and nicked like worn stone.</summary>
        private Color LipColour(float x, float y, Vector2 normal)
        {
            var sign = state == State.Pressed ? -1f : 1f;
            var lit = 0.83f + sign * (0.17f * -normal.Y + 0.07f * normal.X);
            var mottle = 1f + Mottle * (0.6f * Noise(x - Margin, y, 2, 21) + 0.4f * Noise(x - Margin, y, 8, 22));
            var brightness = lit * mottle * NickMask(nicks, x, y) * (state == State.Hover ? 1.12f : 1f);
            var c = _palette.Lip * brightness;
            return new Color(Mathf.Clamp(c.R, 0f, 1f), Mathf.Clamp(c.G, 0f, 1f), Mathf.Clamp(c.B, 0f, 1f));
        }

        /// <summary>Asymmetric gaussian: sharper toward the rim, softer toward the fill.</summary>
        private float LipAmount(float p) => Gauss(p - bevel.LipPeak, p < bevel.LipPeak ? bevel.LipWidth * 0.7f : bevel.LipWidth);

        /// <summary>The rim reads thicker along the bottom edge.</summary>
        private float BottomShadow(float y, float p, Vector2 normal)
        {
            if (normal.Y <= 0.5f) return 0f;
            return 0.45f * Mathf.Clamp((y - (Height - bevel.Shadow)) / 2f, 0f, 1f) * Mathf.Exp(-Mathf.Max(p, 0f) / 1.2f);
        }

        /// <summary>Signed distance (negative inside) to a chamfered box filling the texture.</summary>
        private float Distance(float x, float y)
        {
            float bx = Width / 2f, by = Height / 2f;
            float ax = Mathf.Abs(x - bx), ay = Mathf.Abs(y - by);
            float qx = ax - bx, qy = ay - by;
            var box = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).Length() + Mathf.Min(Mathf.Max(qx, qy), 0f);
            var cut = (ax + ay - (bx + by - bevel.Chamfer)) / Mathf.Sqrt2;
            return Mathf.Max(box, cut);
        }

        private Vector2 Normal(float x, float y)
        {
            const float e = 0.25f;
            var n = new Vector2(Distance(x + e, y) - Distance(x - e, y), Distance(x, y + e) - Distance(x, y - e));
            return n.LengthSquared() < 1e-12f ? Vector2.Zero : n.Normalized();
        }
    }

    // ---- Nicks ------------------------------------------------------------------------------------

    private readonly record struct Nick(bool Horizontal, float X, float Y, float Along);

    /// <summary>Nicks on the top/bottom edges sit in the tiled middle; side nicks sit in the margins.</summary>
    private static Nick[] MakeNicks()
    {
        var rng = new RandomNumberGenerator { Seed = Seed };
        var nicks = new Nick[Nicks];
        for (var i = 0; i < Nicks; i++)
        {
            var side = rng.RandiRange(0, 3);
            var along = rng.RandfRange(1.5f, 4f);
            nicks[i] = side < 2
                ? new Nick(true, Margin + rng.RandfRange(0, Period), side == 0 ? 0 : Height, along)
                : new Nick(false, side == 2 ? 0 : Width, rng.RandfRange(6, Height - 6), along);
        }
        return nicks;
    }

    private static float NickMask(Nick[] nicks, float x, float y)
    {
        var mask = 1f;
        foreach (var nick in nicks)
        {
            var dist = nick.Horizontal
                ? new Vector2(Wrap(x - nick.X) / nick.Along, (y - nick.Y) / 4f).Length()
                : new Vector2((x - nick.X) / 4f, (y - nick.Y) / nick.Along).Length();
            mask *= 1f - NickDepth * Mathf.Pow(Mathf.Clamp(1f - dist, 0f, 1f), 0.7f);
        }
        return mask;
    }

    /// <summary>Horizontal offset on the tiling period, so a nick near one tile edge shows on the other.</summary>
    private static float Wrap(float dx) => dx - Period * Mathf.Round(dx / Period);

    // ---- Noise ------------------------------------------------------------------------------------

    /// <summary>
    /// Stone grain as a seamless Period x Period tile: white or black at low alpha, which brightens
    /// or darkens whatever fill is under it by about ±Grain.
    /// </summary>
    private static Texture2D MakeGrainTile()
    {
        var image = Image.CreateEmpty(Period, Period, false, Image.Format.Rgba8);
        for (var y = 0; y < Period; y++)
        {
            for (var x = 0; x < Period; x++)
            {
                var n = 0.35f * Noise(x, y, 1, 11) + 0.35f * Noise(x, y, 4, 12) + 0.3f * Noise(x, y, 16, 13);
                // Over a fill of ~0.2, white at alpha a adds ~0.8a and black removes ~0.2a.
                image.SetPixel(x, y, n >= 0 ? new Color(1, 1, 1, Grain * n * 0.25f) : new Color(0, 0, 0, Grain * -n));
            }
        }
        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>Value noise in [-1, 1], periodic in x and y with Period (cell must divide Period).</summary>
    private static float Noise(float x, float y, int cell, int salt)
    {
        var cells = Period / cell;
        float fx = x / cell, fy = y / cell;
        int ix = Mathf.FloorToInt(fx), iy = Mathf.FloorToInt(fy);
        float tx = Smooth(fx - ix), ty = Smooth(fy - iy);
        float Corner(int cx, int cy) => Hash(PositiveMod(cx, cells), PositiveMod(cy, cells), salt);
        var top = Mathf.Lerp(Corner(ix, iy), Corner(ix + 1, iy), tx);
        var bottom = Mathf.Lerp(Corner(ix, iy + 1), Corner(ix + 1, iy + 1), tx);
        return Mathf.Lerp(top, bottom, ty);
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    private static int PositiveMod(int a, int m) => ((a % m) + m) % m;

    private static float Hash(int x, int y, int salt)
    {
        unchecked
        {
            var h = (uint)(x * 374761393 + y * 668265263 + (salt + Seed) * 2147483647);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0xFFFFFF * 2f - 1f;
        }
    }

    // ---- Helpers ----------------------------------------------------------------------------------

    private static float Gauss(float v, float width) => Mathf.Exp(-(v / width) * (v / width));

    private static Color Scale(Color c, float mul, float add) => new(c.R * mul + add, c.G * mul + add, c.B * mul + add);

    private static Color Hex(string hex) => Color.FromHtml(hex);
}
