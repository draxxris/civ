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
    CityProductionResult Calculate(Simulation sim, int city, string id);
}

/// <summary>Optional labels for mod-owned ledger source identities. Null leaves the source to other providers.</summary>
public interface ILedgerSourceLabel
{
    string? Label(Simulation sim, string source);
}

/// <summary>Integer RGB and percent tint metadata. No renderer or engine types cross the mod contract.
/// <paramref name="Spread"/> (terrains only, 0..100) is how far a terrain's ground reaches into its neighbours: lush
/// ground at 100 grows past the tile edge into dry ground at 0.</summary>
public sealed record TileAppearance(int Rgb, int TintPercent = 0, int Marks = 0, string? Scatter = null, int ScatterDensity = 0,
    int Spread = 50);

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
