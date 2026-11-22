namespace Ariel.Game;

/// <summary>What the base game asks about corporations in a city: the <c>corporation</c> requirement leaf. Base content
/// names corporations only by their identity (<see cref="Content.Schema.CorporationIdentityDef"/>); the mod that runs
/// corporations answers here. Without one, a city hosts no corporation and is no corporation's headquarters.
/// Queries never change state.</summary>
public interface ICityCorporations
{
    /// <summary>Whether the city has a branch of the corporation with this identity id.</summary>
    bool HasIdentity(Simulation sim, int city, string identity);
    /// <summary>Whether the city is the headquarters of the corporation with this identity id: the city that completed
    /// its headquarters and founded it, or the one that has since taken that city's place by capture.</summary>
    bool IsHeadquartersOf(Simulation sim, int city, string identity);
    /// <summary>What a requirement calls the building that founds the corporation with this identity and is its seat, as
    /// running text ("the headquarters of Cereal Mills", "the Masons Master Guild"), or null when no corporation has the
    /// identity. Content alone answers it, so requirement descriptions can ask.</summary>
    string? SeatPhrase(Content.ContentDb content, string identity);
}
