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

/// <summary>Client-side palette keyed by content id (the data carries no colours): terrains, features, routes and player colours.</summary>
public static class MapStyle
{
    private static readonly Dictionary<string, Rgb> Terrain = new()
    {
        ["coast"] = Rgb.Hex(0x4a8fd0), ["ocean"] = Rgb.Hex(0x234f9c), ["slimy_coast"] = Rgb.Hex(0x4a8fd0), ["slimy_ocean"] = Rgb.Hex(0x234f9c),
        ["desert"] = Rgb.Hex(0xdcc37d), ["grass"] = Rgb.Hex(0x66ad4b), ["plains"] = Rgb.Hex(0xc4b660), ["hill"] = Rgb.Hex(0x8a9a58),
        ["marsh"] = Rgb.Hex(0x5b8a6a), ["peak"] = Rgb.Hex(0x8a8a90), ["snow"] = Rgb.Hex(0xf0f5fa), ["tundra"] = Rgb.Hex(0x9fae9c),
    };

    private static readonly Dictionary<string, FeatureStyle> Features = new()
    {
        ["forest"] = new(Rgb.Hex(0x1f6b2a), 0.45f, 4), ["jungle"] = new(Rgb.Hex(0x0f7a4c), 0.55f, 5), ["swamp"] = new(Rgb.Hex(0x3f6b55), 0.4f, 3),
        ["ice"] = new(Rgb.Hex(0xe8f4ff), 0.7f, 0), ["flood_plains"] = new(Rgb.Hex(0x8ec26a), 0.35f, 0), ["oasis"] = new(Rgb.Hex(0x3fb6c9), 0.5f, 1),
        ["fallout"] = new(Rgb.Hex(0x9a9a2a), 0.4f, 2),
    };

    private static readonly Rgb[] Players =
    [
        Rgb.Hex(0xe8c33a), Rgb.Hex(0xd9453d), Rgb.Hex(0x4a86e8), Rgb.Hex(0x8a4fd0), Rgb.Hex(0xee8a2e), Rgb.Hex(0x30b8a6), Rgb.Hex(0xe86fb0), Rgb.Hex(0xe8e8e8),
    ];

    private static string Local(string id) => id[(id.IndexOf(':') + 1)..];

    public static Rgb TerrainColor(string id) => Terrain.GetValueOrDefault(Local(id), Rgb.Hex(0x777777));

    public static FeatureStyle? FeatureOf(string id) => Features.GetValueOrDefault(Local(id));

    public static Rgb PlayerColor(int player) => Players[player % Players.Length];

    public static readonly Rgb Road = Rgb.Hex(0x7a5a38);
    public static readonly Rgb River = Rgb.Hex(0x2f7be0);
}
