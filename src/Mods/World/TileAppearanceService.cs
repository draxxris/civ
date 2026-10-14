using Civ.Game;

namespace Civ.Mods.World;

internal sealed class TileAppearanceService : ITileAppearanceService
{
    private static readonly IReadOnlyDictionary<string, TileAppearance> Terrains = new Dictionary<string, TileAppearance>(StringComparer.Ordinal)
    {
        ["world:coast"] = new(0x4a8fd0), ["world:ocean"] = new(0x234f9c),
        ["world:slimy_coast"] = new(0x4a8fd0), ["world:slimy_ocean"] = new(0x234f9c),
        ["world:desert"] = new(0xdcc37d), ["world:grass"] = new(0x66ad4b), ["world:plains"] = new(0xc4b660),
        ["world:hill"] = new(0x8a9a58), ["world:marsh"] = new(0x5b8a6a), ["world:peak"] = new(0x8a8a90),
        ["world:snow"] = new(0xf0f5fa), ["world:tundra"] = new(0x9fae9c),
    };

    private static readonly IReadOnlyDictionary<string, TileAppearance> Features = new Dictionary<string, TileAppearance>(StringComparer.Ordinal)
    {
        ["world:forest"] = new(0x1f6b2a, 45, 4), ["world:jungle"] = new(0x0f7a4c, 55, 5),
        ["world:swamp"] = new(0x3f6b55, 40, 3), ["world:ice"] = new(0xe8f4ff, 70),
        ["world:flood_plains"] = new(0x8ec26a, 35), ["world:oasis"] = new(0x3fb6c9, 50, 1),
        ["world:fallout"] = new(0x9a9a2a, 40, 2),
    };

    public TileAppearance? Terrain(string id) => Terrains.GetValueOrDefault(id);
    public TileAppearance? Feature(string id) => Features.GetValueOrDefault(id);
}
