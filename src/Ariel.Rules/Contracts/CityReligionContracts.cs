using Ariel.Content.Schema;

namespace Ariel.Game;

/// <summary>What the base game asks about religion: the Foundation <c>religion</c> requirement leaf, the
/// <c>foundation:any_religion</c> and <c>foundation:state_religion</c> conditions, whether a religious building or unit
/// may be produced, and whether a conquered city is a holy city. Base content names religions only by their identity
/// (<see cref="ReligionIdentityDef"/>); the mod that runs religion answers here. Without one, no city follows a
/// religion, so religion-gated content stays unavailable rather than free. Queries never change state or draw from
/// the random numbers.</summary>
public interface ICityReligions
{
    /// <summary>Whether the religion with this identity id is present in the city.</summary>
    bool Has(Simulation sim, int city, string identity);
    /// <summary>Whether the player's state religion has this identity id.</summary>
    bool IsStateReligion(Simulation sim, int player, string identity);
    /// <summary>Whether the city is the holy city of the religion with this identity id.</summary>
    bool IsHolyCityOf(Simulation sim, int city, string identity);
    /// <summary>Whether the city is the holy city of any religion.</summary>
    bool IsHolyCity(Simulation sim, int city);
    /// <summary>Whether any religion is present in the city.</summary>
    bool HasAnyReligion(Simulation sim, int city);
    /// <summary>Whether the player has a state religion and it is present in the city.</summary>
    bool HasStateReligion(Simulation sim, int player, int city);
    /// <summary>Why the religion rules forbid the city to build this building now, or null. The building's religion
    /// fields (<see cref="BuildingDef.ReligionType"/>, <see cref="BuildingDef.FoundsReligion"/> and the state religion
    /// flags) are what the rules read.</summary>
    string? RefuseBuilding(Simulation sim, City city, BuildingDef building);
    /// <summary>Why the religion rules forbid the city to train this unit now, or null.</summary>
    string? RefuseUnit(Simulation sim, City city, UnitDef unit);
}
