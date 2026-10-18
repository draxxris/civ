using Civ.Content;
using Civ.Game;

namespace Civ.Client;

/// <summary>An engine-free colour with components in 0..1.</summary>
public readonly record struct Rgb(float R, float G, float B)
{
    public Rgb Scale(float f) => new(R * f, G * f, B * f);
    public Rgb Lerp(Rgb to, float t) => new(R + (to.R - R) * t, G + (to.G - G) * t, B + (to.B - B) * t);
    public static Rgb Hex(int rgb) => new(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
}

public enum Sight { Hidden, Fogged, Visible }

/// <summary>How a feature is drawn on top of its terrain: a tint over the tile plus a number of small marks.</summary>
public sealed record FeatureStyle(Rgb Tint, float TintStrength, int Marks);

/// <summary>Mod-supplied terrain and feature appearance, with generic route and player colours.</summary>
public static class MapStyle
{


    private static readonly Rgb[] Players =
    [
        Rgb.Hex(0xe8c33a), Rgb.Hex(0xd9453d), Rgb.Hex(0x4a86e8), Rgb.Hex(0x8a4fd0), Rgb.Hex(0xee8a2e), Rgb.Hex(0x30b8a6), Rgb.Hex(0xe86fb0), Rgb.Hex(0xe8e8e8),
    ];

    public static Rgb TerrainColor(ContentDb content, string id) => Rgb.Hex(
        content.Services.Get<ITileAppearanceService>().Select(s => s.Terrain(id)).FirstOrDefault(a => a != null)?.Rgb ?? 0x777777);

    public static FeatureStyle? FeatureOf(ContentDb content, string id)
    {
        var appearance = content.Services.Get<ITileAppearanceService>().Select(s => s.Feature(id)).FirstOrDefault(a => a != null);
        return appearance == null ? null : new(Rgb.Hex(appearance.Rgb), appearance.TintPercent / 100f, appearance.Marks);
    }

    public static Rgb PlayerColor(int player) => Players[player % Players.Length];


    public static readonly Rgb River = Rgb.Hex(0x2f7be0);
}
