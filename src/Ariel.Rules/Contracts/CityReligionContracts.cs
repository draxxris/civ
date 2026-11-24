namespace Ariel.Game;

/// <summary>What the base game asks about religion: the Foundation <c>religion</c> requirement leaf and the
/// <c>foundation:any_religion</c> and <c>foundation:state_religion</c> conditions. Base content names religions only by
/// their identity (<see cref="Ariel.Content.Schema.ReligionIdentityDef"/>); the mod that runs religion answers here.
/// Without one, no city follows a religion, so religion-gated content stays unavailable rather than free. Queries
/// never change state or draw from the random numbers.</summary>
public interface ICityReligions
{
    /// <summary>Whether the religion with this identity id is present in the city.</summary>
    bool Has(Simulation sim, int city, string identity);
    /// <summary>Whether the player's state religion has this identity id.</summary>
    bool IsStateReligion(Simulation sim, int player, string identity);
    /// <summary>Whether the city is the holy city of the religion with this identity id.</summary>
    bool IsHolyCityOf(Simulation sim, int city, string identity);
    /// <summary>Whether any religion is present in the city.</summary>
    bool HasAnyReligion(Simulation sim, int city);
    /// <summary>Whether the player has a state religion and it is present in the city.</summary>
    bool HasStateReligion(Simulation sim, int player, int city);
}
