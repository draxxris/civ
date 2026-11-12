namespace Ariel.Game;

/// <summary>What else makes a city a good new capital, beyond its size, yields, level and culture, when the old one is
/// lost. The capital goes to the city with the highest score; each registered weight adds its points for the city.
/// Religion counts the faiths present, and features add their own (corporation branches, settled great people).
/// Every implementation is asked; queries never change state or use the RNG.</summary>
public interface ICapitalWeight
{
    /// <summary>Points this city earns toward becoming the capital, on the scale of one citizen = 4.</summary>
    int Weight(Simulation sim, int city);
}

/// <summary>Building categories granted to every city of a player besides those its constructed buildings grant
/// (<see cref="Content.Schema.BuildingDef.FreeBuilding"/>). Each city receives its civilization's building of every
/// granted category. Every implementation is asked; queries never change state or use the RNG.</summary>
public interface IFreeBuildingGrants
{
    /// <summary>Granted building categories for every city of this player, in any order.</summary>
    IReadOnlyList<string> Grants(Simulation sim, int player);
}

/// <summary>How restless a city is, answered by the feature that tracks unrest, or null without one
/// (<see cref="GameRules.CityUnrest"/>). Rules that would pay a city's owner can calm the city instead, and planners
/// prefer to act where unrest is highest.</summary>
public interface ICityUnrest
{
    /// <summary>The city's unrest, zero for a calm city. Reads saved state only and never attaches it.</summary>
    int Unrest(Simulation sim, int city);
    /// <summary>Spend <paramref name="amount"/> calming the city: unrest falls by up to that amount. Returns false,
    /// changing nothing, when the city keeps no unrest record; the caller then pays the amount some other way.</summary>
    bool Relieve(Simulation sim, int city, long amount);
}

/// <summary>Whether two cities trade with each other, answered by the feature that runs trade routes, or null without
/// one (<see cref="GameRules.TradeLinks"/>). Religion spreads along such links as well as along city connections.
/// Queries never change state.</summary>
public interface ICityTradeLinks
{
    /// <summary>Whether an active trade route runs between the two cities, in either direction.</summary>
    bool Linked(Simulation sim, int city, int other);
}

/// <summary>Completion rules for a production queue kind that Cities does not own. Cities hands a finished order of
/// that kind to every completion registered for it.</summary>
public interface IProductionCompletion
{
    string Kind { get; }
    void Complete(Simulation sim, int city, string content);
}
