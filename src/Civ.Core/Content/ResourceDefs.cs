namespace Civ.Content;

/// <summary>
/// A mod-defined resource (faith, renown, capital, ...). Every turn each city adds its <see cref="Rates"/> plus whatever
/// registered <see cref="IResourceSource"/>s say; the total lands in the owner's pool (<see cref="ResourceScopes.Player"/>)
/// or the city's own pool (<see cref="ResourceScopes.City"/>). A resource with <see cref="Stockpile"/> false is a flow:
/// nothing is stored, only the current per-turn net counts (map goods a corporation consumes, for example).
/// </summary>
public sealed class ResourceDef : ContentDef
{
    public string Description { get; init; } = "";
    /// <summary><see cref="ResourceScopes.Player"/> or <see cref="ResourceScopes.City"/>.</summary>
    public string Scope { get; init; } = ResourceScopes.Player;
    public bool Stockpile { get; init; } = true;
    /// <summary>Largest amount a pool can hold; 0 = no cap.</summary>
    public long Cap { get; init; }
    /// <summary>Hint for clients, "#rrggbb".</summary>
    public string Color { get; init; } = "#cccccc";
    /// <summary>Data-driven producers (positive) and consumers (negative), all counted per city.</summary>
    public ResourceRate[] Rates { get; init; } = [];
}

public static class ResourceScopes
{
    public const string Player = "player";
    public const string City = "city";
}

/// <summary>
/// One data-driven rate, applied per city. <see cref="From"/> picks what is counted:
/// <c>building</c> (city has building <see cref="Id"/>), <c>specialist</c> (per specialist <see cref="Id"/>),
/// <c>civic</c> (owner holds civic <see cref="Id"/>), <c>terrain</c> (per worked tile, centre included, of terrain <see cref="Id"/>),
/// <c>institution</c> (city hosts institution <see cref="Id"/>), <c>population</c> (per citizen) or <c>city</c> (flat).
/// </summary>
public sealed class ResourceRate
{
    public string From { get; init; } = "";
    public string? Id { get; init; }
    public int Amount { get; init; }

    public static readonly string[] Kinds = ["building", "specialist", "civic", "terrain", "institution", "population", "city"];
}

/// <summary>Code-driven resource rates for one city: add (or subtract) into <paramref name="rates"/> by resource id. Must be deterministic.</summary>
public interface IResourceSource : IHasId
{
    void AddCityRates(Game.Simulation sim, Game.City city, IDictionary<string, long> rates);
}

/// <summary>Flat food/production/commerce/culture a mod adds to a city. The base game's yield rules sum these.</summary>
public readonly record struct CityYield(int Food, int Production, int Commerce, int Culture)
{
    public static CityYield operator +(CityYield a, CityYield b) =>
        new(a.Food + b.Food, a.Production + b.Production, a.Commerce + b.Commerce, a.Culture + b.Culture);
}

/// <summary>Lets a mod change what a city yields (a religion's culture, a corporation's branch output). Must be deterministic.</summary>
public interface ICityYieldSource : IHasId
{
    CityYield YieldFor(Game.Simulation sim, Game.City city);
}

/// <summary>
/// Something founded once in a city and then spread from city to city: a religion, a guild, a corporation.
/// The spreading rules are shared (see <see cref="Game.Institutions"/>); what an institution does is up to the mod.
/// </summary>
public sealed class InstitutionDef : ContentDef
{
    /// <summary>Free-form family such as "religion"; buildings and units can require "any institution of this category".</summary>
    public string Category { get; init; } = "";
    public string Description { get; init; } = "";
    public string Color { get; init; } = "#cccccc";
    /// <summary>Founding needs this tech.</summary>
    public string? RequiresTech { get; init; }
    /// <summary>Founding needs this building in the city.</summary>
    public string? RequiresBuilding { get; init; }
    /// <summary>Resources paid to found it (from the founding city for city-scoped resources).</summary>
    public Dictionary<string, long> FoundCost { get; init; } = new();
    /// <summary>Units that can carry it to a city (missionaries, executives). The unit is used up.</summary>
    public string[] Spreaders { get; init; } = [];
    /// <summary>Resources paid each time a unit spreads it.</summary>
    public Dictionary<string, long> SpreadCost { get; init; } = new();
    /// <summary>Percent chance per turn that it spreads on its own to each city within <see cref="SpreadRange"/> of a city that has it.</summary>
    public int PassiveSpreadPercent { get; init; }
    public int SpreadRange { get; init; } = 4;
    /// <summary>Institutions it cannot share a city with (both directions are checked).</summary>
    public string[] Conflicts { get; init; } = [];
}
