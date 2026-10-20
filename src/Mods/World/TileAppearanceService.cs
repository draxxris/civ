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
        // Patch: a neighbouring ground shows through in places, so wide areas of one terrain are not uniform.
        // Desert ripples and grass blades lie with the wind: their textures turn only a little, so the flow stays
        // roughly parallel.
        ["world:desert"] = new(0xdcc37d, Spread: 15, Patch: "world:plains", PatchPercent: 10, TurnDegrees: 14),
        ["world:grass"] = new(0x66ad4b, Spread: 85, Patch: "world:plains", PatchPercent: 14, TurnDegrees: 14),
        ["world:plains"] = new(0xc4b660, Spread: 55, TurnDegrees: 14),
        ["world:hill"] = new(0x8a9a58, Spread: 60, Patch: "world:grass", PatchPercent: 15),
        ["world:marsh"] = new(0x5b8a6a, Spread: 75, Patch: "world:grass", PatchPercent: 15), ["world:peak"] = new(0x8a8a90),
        ["world:snow"] = new(0xf0f5fa, Spread: 35, Patch: "world:tundra", PatchPercent: 10),
        ["world:tundra"] = new(0x9fae9c, Spread: 40, Patch: "world:snow", PatchPercent: 8),
    };

    // Forest props gather into groves with small glades between them.
    private static readonly FeatureLook Groves = new(Clumping: 55);

    private static readonly IReadOnlyDictionary<string, TileAppearance> Features = new Dictionary<string, TileAppearance>(StringComparer.Ordinal)
    {
        // Muted, earthy forest floors: under the graded foliage they read as shade, not as painted green hexes.
        // Scatter densities are percent of candidates; forests and jungles run near full so groves read as dense canopy.
        ["world:forest"] = new(ForestFloor, 40, 4, "forest", 95, Look: Groves), ["world:jungle"] = new(0x3a5236, 45, 5, "jungle", 100),
        // Dead trees and reeds stand in dark pools under a low mist.
        ["world:swamp"] = new(0x3f5a45, 35, 3, "swamp", 60,
            Look: new(FeatureGround.Pools, Haze: FeatureHaze.Mist, HazeRgb: 0xdce4df, HazePercent: 42)),
        ["world:ice"] = new(0xe8f4ff, Look: new(FeatureGround.Floes)),
        ["world:flood_plains"] = new(0x6d9a3c, 30, Look: new(FeatureGround.Fertile)),
        // Palms ring a small pool at the tile centre.
        ["world:oasis"] = new(0x8a9a50, 15, 1, "oasis", 60, Look: new(FeatureGround.Spring, ScatterInner: 48, ScatterOuter: 85)),
        ["world:fallout"] = new(0x8c8a3c, 25, Look: new(FeatureGround.Blight)),
        // Smogs are low, coloured haze only: the ground under them stays as it is.
        ["world:biogas"] = new(0, Look: new(Haze: FeatureHaze.Smog, HazeRgb: 0xb2bd52, HazePercent: 40)),
        ["world:plaguegas"] = new(0, Look: new(Haze: FeatureHaze.Smog, HazeRgb: 0x9a6aa0, HazePercent: 45)),
        ["world:storm"] = new(0, Look: new(FeatureGround.Squall, Haze: FeatureHaze.Storm, HazeRgb: 0x4c5462, HazePercent: 75)),
        ["world:volcano"] = new(0x3b3532, 45, Look: new(FeatureGround.Crater, Haze: FeatureHaze.Smoke, HazeRgb: 0x6e6862, HazePercent: 75)),
    };

    // Jungle over marsh thins a little and keeps the wet ground visible between its trunks.
    private static readonly TileAppearance MarshJungle = new(0x3a5236, 30, 5, "jungle", 80, Look: new(FeatureGround.Pools, 75));

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

    public TileAppearance? Scatter(string feature, string terrain) => (feature, terrain) switch
    {
        ("world:forest", "world:tundra" or "world:snow") => new(ForestFloor, 40, 4, "forest_snow", 95, Look: Groves),
        ("world:jungle", "world:marsh") => MarshJungle,
        _ => null,
    };
}
