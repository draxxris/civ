using Ariel.Game;

namespace Ariel.BaseGame;

/// <summary>What a city's single production order makes.</summary>
public enum ProductionKind { Building, Unit }

/// <summary>A city's one production order, held in <see cref="City.Extensions"/>. A city with nothing in production
/// carries no component, so idle cities stay as the kernel created them.</summary>
public sealed class CityProduction
{
    /// <summary>Whether the order makes a building or trains a unit.</summary>
    public ProductionKind Kind { get; set; }
    /// <summary>Building or unit definition id.</summary>
    public string Item { get; set; } = "";
    /// <summary>Production accumulated toward the item's cost.</summary>
    public long Progress { get; set; }
}

/// <summary>Reads and writes a city's production order. Reads never attach state, so asking (as the AI does on
/// speculative forks) leaves the city untouched.</summary>
public static class Production
{
    /// <summary>The city's current order, or null when it is idle.</summary>
    public static CityProduction? Of(Simulation sim, City city) => city.Extensions.Get<CityProduction>(sim.Content);

    /// <summary>The building in production, or null.</summary>
    public static string? Building(Simulation sim, City city) => Of(sim, city) is { Kind: ProductionKind.Building } order ? order.Item : null;

    /// <summary>The unit in training, or null.</summary>
    public static string? Training(Simulation sim, City city) => Of(sim, city) is { Kind: ProductionKind.Unit } order ? order.Item : null;

    /// <summary>Put an item into production. Re-ordering the item already in production keeps its progress; anything
    /// else replaces the order and starts from <paramref name="progress"/>.</summary>
    public static void Start(Simulation sim, City city, ProductionKind kind, string item, long progress = 0)
    {
        if (Of(sim, city) is { } current && current.Kind == kind && current.Item == item) return;
        city.Extensions.Set(sim.Content, new CityProduction { Kind = kind, Item = item, Progress = progress });
    }

    /// <summary>Finish or abandon the current order, leaving the city idle.</summary>
    public static void Clear(Simulation sim, City city) => city.Extensions.Remove<CityProduction>(sim.Content);
}
