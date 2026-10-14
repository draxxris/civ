using Civ.Content;

namespace Civ.Game;

/// <summary>Setup fields whose defaults and display order belong to the loaded ruleset.</summary>
public enum SetupField { WorldSize, GameSpeed, Handicap, StartingEra, MapGenerator }

/// <summary>Optional ruleset policy. At most one provider may supply a default for each field.</summary>
public interface ISetupPolicy
{
    string? Default(SetupField field);
    int? SortOrder(SetupField field, string id) => null;
}

public static class SetupPolicies
{
    public static string? Default(ContentDb content, SetupField field) =>
        content.Services.Get<ISetupPolicy>().Select(p => p.Default(field)).Where(id => id != null).SingleOrDefault();

    public static int SortOrder(ContentDb content, SetupField field, string id) =>
        content.Services.Get<ISetupPolicy>().Select(p => p.SortOrder(field, id)).Where(n => n != null).SingleOrDefault() ?? 0;
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

/// <summary>Integer RGB and percent tint metadata. No renderer or engine types cross the mod contract.</summary>
public sealed record TileAppearance(int Rgb, int TintPercent = 0, int Marks = 0, string? Scatter = null, int ScatterDensity = 0);

public enum TerrainSurface { Cliff, Seabed, DeepSeabed, Unexplored, Snow }

public interface ITileAppearanceService
{
    TileAppearance? Terrain(string id);
    TileAppearance? Feature(string id);
    string? Surface(TerrainSurface surface) => null;
    TileAppearance? Scatter(string feature, string terrain) => null;
}
