namespace Ariel.Game;

/// <summary>What the base game asks about corporations in a city: the <c>corporation</c> requirement leaf. Base content
/// names corporations only by their identity (<see cref="Content.Schema.CorporationIdentityDef"/>); the mod that runs
/// corporations answers here. Without one, a city hosts no corporation. Queries never change state.</summary>
public interface ICityCorporations
{
    /// <summary>Whether the city has a branch of the corporation with this identity id.</summary>
    bool HasIdentity(Simulation sim, int city, string identity);
}
