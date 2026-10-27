using Civ.Content;

namespace Civ.Game;

/// <summary>Setup fields whose defaults and display order belong to the loaded ruleset.</summary>
public enum SetupField { WorldSize, GameSpeed, Handicap, StartingEra, MapGenerator }

/// <summary>Optional ruleset policy. At most one provider may supply a default for each field.</summary>
public sealed record SetupGameOption(string Id, string Name, bool Default, string Help = "");

public interface ISetupPolicy
{
    string? Default(SetupField field);
    int? SortOrder(SetupField field, string id) => null;
    IEnumerable<SetupGameOption> GameOptions(ContentDb content) => [];
}

public static class SetupPolicies
{
    public static string? Default(ContentDb content, SetupField field) =>
        content.Services.Get<ISetupPolicy>().Select(p => p.Default(field)).Where(id => id != null).SingleOrDefault();

    public static int SortOrder(ContentDb content, SetupField field, string id) =>
        content.Services.Get<ISetupPolicy>().Select(p => p.SortOrder(field, id)).Where(n => n != null).SingleOrDefault() ?? 0;

    public static IReadOnlyList<SetupGameOption> GameOptions(ContentDb content) =>
        content.Services.Get<ISetupPolicy>().SelectMany(p => p.GameOptions(content))
            .OrderBy(option => option.Name, StringComparer.Ordinal).ThenBy(option => option.Id, StringComparer.Ordinal).ToList();
}

/// <summary>A mod-owned queue kind and its read-only catalog and query behavior.</summary>
public interface IProductionKind : IHasId
{
    string Label { get; }
    bool IsContinuous { get; }
    IEnumerable<ContentDef> Candidates(ContentDb content);
    ContentDef Definition(ContentDb content, string id);
    long BaseCost(ContentDef definition);
    bool IsVisible(RequirementResult eligibility);
    RequirementResult Validate(Simulation sim, int city, string id);
    /// <summary>Validates a batch of ids in one read-only pass and returns the results in the order of <paramref name="ids"/>,
    /// each equal to <see cref="Validate"/> for that id. Mods that repeat whole-empire work for every id (building, power
    /// and resource state) override this to share it across the batch.</summary>
    IReadOnlyList<RequirementResult> ValidateBatch(Simulation sim, int city, IReadOnlyList<string> ids) =>
        ids.Select(id => Validate(sim, city, id)).ToArray();
    CityProductionResult Calculate(Simulation sim, int city, string id);
}

/// <summary>Optional labels for mod-owned ledger source identities. Null leaves the source to other providers.</summary>
public interface ILedgerSourceLabel
{
    string? Label(Simulation sim, string source);
}

/// <summary>Integer RGB and percent tint metadata. No renderer or engine types cross the mod contract.
/// <paramref name="Spread"/> (terrains only, 0..100) is how far a terrain's ground reaches into its neighbours: lush
/// ground at 100 grows past the tile edge into dry ground at 0. <paramref name="Patch"/> (terrains only) is another terrain
/// whose ground shows through in scattered patches over <paramref name="PatchPercent"/> (0..100) of the ground, so a
/// large area of one terrain does not look uniform. <paramref name="TurnDegrees"/> (terrains and surfaces, 0..180) is
/// the largest random turn the renderer may give the ground texture to hide its repeat; ground with a direction, such
/// as wind-blown sand ripples, keeps a small value so its pattern stays roughly parallel.</summary>
public sealed record TileAppearance(int Rgb, int TintPercent = 0, int Marks = 0, string? Scatter = null, int ScatterDensity = 0,
    int Spread = 50, FeatureLook? Look = null, string? Patch = null, int PatchPercent = 0, int TurnDegrees = 180);

/// <summary>How a feature paints the ground (or water) under it. The client draws each pattern procedurally.</summary>
public enum FeatureGround
{
    /// <summary>Only the soft tint: shade under props, such as a forest floor.</summary>
    Shade,
    /// <summary>Dark, still pools between tussocks of the ground.</summary>
    Pools,
    /// <summary>Lush green growth and dark silt, richest along rivers.</summary>
    Fertile,
    /// <summary>A small pool of clear water at the tile centre, ringed by damp ground.</summary>
    Spring,
    /// <summary>Scorched, discoloured ground with a faint sickly glow.</summary>
    Blight,
    /// <summary>Broken floes of sea ice on the water, packed tight at the shore.</summary>
    Floes,
    /// <summary>Dark, wind-torn water with whitecaps under a cloud shadow.</summary>
    Squall,
    /// <summary>A summit crater with a glowing floor and dark ash slopes. The height field gains the crater.</summary>
    Crater,
}

/// <summary>A translucent layer of air over a feature. The terrain under it stays readable.</summary>
public enum FeatureHaze
{
    None,
    /// <summary>Low, pale wisps that hug the ground.</summary>
    Mist,
    /// <summary>Low, coloured, slowly rolling haze.</summary>
    Smog,
    /// <summary>Dark, torn storm clouds high over the tile.</summary>
    Storm,
    /// <summary>A plume of smoke rising from the tile's summit.</summary>
    Smoke,
}

/// <summary>Extra feature look. <paramref name="GroundPercent"/> is the strength of the ground pattern. The haze has a
/// colour and a density (0..100). <paramref name="ScatterInner"/> and <paramref name="ScatterOuter"/> limit props to a
/// ring around the tile centre, in percent of the hex radius. <paramref name="Clumping"/> (0..100) gathers props into
/// groves with glades between them.</summary>
public sealed record FeatureLook(FeatureGround Ground = FeatureGround.Shade, int GroundPercent = 100,
    FeatureHaze Haze = FeatureHaze.None, int HazeRgb = 0, int HazePercent = 0,
    int ScatterInner = 0, int ScatterOuter = 100, int Clumping = 0);

public enum TerrainSurface { Cliff, Seabed, DeepSeabed, Unexplored, Snow }

/// <summary>How a land route is drawn. The client draws an unknown route as a plain road.</summary>
public enum RouteLook
{
    /// <summary>Two narrow wheel ruts that wander over the grass.</summary>
    Track,
    /// <summary>A packed earth road with a worn middle and ruts.</summary>
    Road,
    /// <summary>Grey stone setts between soft verges.</summary>
    Paved,
    /// <summary>Dark asphalt with white edge lines and a dashed yellow centre line.</summary>
    Highway,
    /// <summary>Two steel rails on wooden sleepers in a ballast bed.</summary>
    Railroad,
    /// <summary>A railroad on concrete sleepers under portal masts and a contact wire.</summary>
    ElectricRailroad,
    /// <summary>A concrete guideway beam on piers.</summary>
    Maglev,
    /// <summary>A slim steel beam with glowing edges on lattice legs.</summary>
    Jumplane,
}

public interface ITileAppearanceService
{
    TileAppearance? Terrain(string id);
    TileAppearance? Feature(string id);
    string? Surface(TerrainSurface surface) => null;
    TileAppearance? Scatter(string feature, string terrain) => null;
    /// <summary>The look of a route id, or null to leave it to other providers.</summary>
    RouteLook? Route(string id) => null;
}

/// <summary>How a city's 3D buildings look.</summary>
public enum CityStyle { Huts, Victorian, Modern }

/// <summary>What lies under a city's 3D buildings.</summary>
public enum CityGround { Dirt, Cobblestone, Concrete }

/// <summary>The look of a city in an era. No renderer types cross the mod contract.</summary>
public sealed record CityAppearance(CityStyle Style, CityGround Ground);

/// <summary>Maps an era id to the look of its cities. Null leaves the era to other providers.</summary>
public interface ICityAppearanceService
{
    CityAppearance? Era(string era);
}
