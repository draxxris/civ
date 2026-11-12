namespace Ariel.Game;

/// <summary>What the base game asks about corporations in a city: a <c>corporation</c> requirement leaf, and how a
/// city's branches weigh when the capital moves. Base content names corporations only by their identity
/// (<see cref="Content.Schema.CorporationIdentityDef"/>); the mod that runs corporations answers here. Without one, a
/// city hosts no corporation. Queries never change state.</summary>
public interface ICityCorporations
{
    /// <summary>How many corporations have a branch in the city.</summary>
    int Count(Simulation sim, int city);
    /// <summary>Whether the city has a branch of the corporation with this identity id.</summary>
    bool HasIdentity(Simulation sim, int city, string identity);
}
