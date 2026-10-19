using Civ.Game;

namespace Civ.Mods.World;

internal sealed class TileAppearanceService : ITileAppearanceService
{
    private const int ForestFloor = 0x4a5736;

    private static readonly IReadOnlyDictionary<string, TileAppearance> Terrains = new Dictionary<string, TileAppearance>(StringComparer.Ordinal)
    {
        ["world:coast"] = new(0x4a8fd0), ["world:ocean"] = new(0x234f9c),
        ["world:slimy_coast"] = new(0x4a8fd0), ["world:slimy_ocean"] = new(0x234f9c),
        // Spread: lush ground grows past the tile edge into drier ground (grass into plains, plains into desert).
        ["world:desert"] = new(0xdcc37d, Spread: 15), ["world:grass"] = new(0x66ad4b, Spread: 85),
        ["world:plains"] = new(0xc4b660, Spread: 55), ["world:hill"] = new(0x8a9a58, Spread: 60),
        ["world:marsh"] = new(0x5b8a6a, Spread: 75), ["world:peak"] = new(0x8a8a90),
        ["world:snow"] = new(0xf0f5fa, Spread: 35), ["world:tundra"] = new(0x9fae9c, Spread: 40),
    };

    private static readonly IReadOnlyDictionary<string, TileAppearance> Features = new Dictionary<string, TileAppearance>(StringComparer.Ordinal)
    {
        // Muted, earthy forest floors: under the graded foliage they read as shade, not as painted green hexes.
        // Scatter densities are percent of candidates; forests and jungles run near full so groves read as dense canopy.
        ["world:forest"] = new(ForestFloor, 40, 4, "forest", 95), ["world:jungle"] = new(0x3a5236, 45, 5, "jungle", 100),
        ["world:swamp"] = new(0x3f6b55, 40, 3, "swamp", 40), ["world:ice"] = new(0xe8f4ff, 70),
        ["world:flood_plains"] = new(0x8ec26a, 35), ["world:oasis"] = new(0x3fb6c9, 50, 1, "oasis", 25),
        ["world:fallout"] = new(0x9a9a2a, 40, 2),
    };

    private static readonly IReadOnlyDictionary<string, RouteLook> Routes = new Dictionary<string, RouteLook>(StringComparer.Ordinal)
    {
        ["world:route_route_track"] = RouteLook.Track, ["world:route_route_road"] = RouteLook.Road,
        ["world:route_route_paved"] = RouteLook.Paved, ["world:route_route_modern_road"] = RouteLook.Highway,
        ["world:route_route_railroad"] = RouteLook.Railroad, ["world:route_route_railroad_electric"] = RouteLook.ElectricRailroad,
        ["world:route_route_gravtube"] = RouteLook.Maglev, ["world:route_route_jumplane"] = RouteLook.Jumplane,
    };

    public TileAppearance? Terrain(string id) => Terrains.GetValueOrDefault(id);
    public TileAppearance? Feature(string id) => Features.GetValueOrDefault(id);
    public RouteLook? Route(string id) => Routes.TryGetValue(id, out var look) ? look : null;

    public string? Surface(TerrainSurface surface) => surface switch
    {
        TerrainSurface.Cliff => "world:rock",
        TerrainSurface.Seabed => "world:seabed",
        TerrainSurface.DeepSeabed => "world:deep_seabed",
        TerrainSurface.Unexplored => "world:unexplored",
        TerrainSurface.Snow => "world:snow",
        _ => null,
    };

    public TileAppearance? Scatter(string feature, string terrain) =>
        feature == "world:forest" && terrain is "world:tundra" or "world:snow"
            ? new(ForestFloor, 40, 4, "forest_snow", 95)
            : null;
}
