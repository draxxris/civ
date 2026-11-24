using Ariel.Content;
using Ariel.Game;

namespace Ariel.Mods.Corporations;

public sealed class CorporationCityState
{
    public SortedSet<string> Corporations { get; set; } = new(StringComparer.Ordinal);
}
public sealed class CorporationGameState
{
    public SortedDictionary<string, CorporationFounding> Founded { get; set; } = new(StringComparer.Ordinal);
}
public sealed class CorporationPlayerState
{
    public long MaintenanceRemainder { get; set; }
}
public sealed record CorporationFounding(int Headquarters, int FounderTeam, int Turn);
public sealed record CorporationFounded(string Corporation, int City, int Player) : DomainEvent;
public sealed record CorporationSpread(string Corporation, int City, int Player) : DomainEvent;
public sealed record CorporationDissolved(string Corporation, int City, int Player) : DomainEvent;
/// <summary>A branch closed because the city's team learned the corporation's obsolete technology.</summary>
public sealed record CorporationObsolete(string Corporation, int City) : DomainEvent;
/// <summary>A competing branch was evicted by <paramref name="By"/> without compensation, as in the reference rules.</summary>
public sealed record CorporationDisplaced(string Corporation, string By, int City) : DomainEvent;
/// <summary>A branch closed because its city went without the corporation's resource for too long (Realistic
/// Guilds/Corporations).</summary>
public sealed record CorporationWithdrawn(string Corporation, int City) : DomainEvent;

/// <summary>The chance a city takes a branch of a corporation this turn on its own: the branch opens when a draw in
/// [0, <see cref="Range"/>) falls below <see cref="Threshold"/>.</summary>
public readonly record struct CorporationSpreadOdds(int Threshold, int Range)
{
    /// <summary>The chance in hundredths of a percent.</summary>
    public int BasisPoints => Range <= 0 ? 0 : (int)Math.Min(10000L, (long)Math.Max(0, Threshold) * 10000 / Range);
}

/// <summary>Read-only previews and validated corporation mutations.</summary>
public interface ICorporationService
{
    IReadOnlyList<string> Present(Simulation sim, int city);
    /// <summary>Whether the city is the headquarters of the corporation: the city that completed its headquarters and
    /// founded it, or the one that has since taken that city's place by capture.</summary>
    bool IsHeadquartersOf(Simulation sim, int city, string corporation);
    /// <summary>What a requirement calls the building that founds the corporation and is its seat, as running text ("the
    /// headquarters of Cereal Mills", "the Masons Master Guild"), or null when no corporation has the id. Content alone
    /// answers it, so requirement descriptions can ask.</summary>
    string? SeatPhrase(ContentDb content, string corporation);
    /// <summary>The headquarters city while it exists; null once the corporation is unfounded or dissolved.</summary>
    int? Headquarters(Simulation sim, string corporation);
    bool IsFounded(Simulation sim, string corporation);
    bool IsActive(Simulation sim, int city, string corporation);
    long ConsumedBonusCount(Simulation sim, int city, string corporation);
    long MaintenanceHundredths(Simulation sim, int city);
    /// <summary>Gold the player pays to open a branch of the corporation in the city, as the source prices it: the base
    /// cost with inflation, times the foreign spread percent in another team's city, times (100 + factor)% for each
    /// active competing branch there. Executives and paid spreads both pay it.</summary>
    long SpreadCost(Simulation sim, int player, int city, string corporation);
    /// <summary>Why a player cannot pay to open a branch in one of its own cities without an executive, or null.</summary>
    string? ValidateSpread(Simulation sim, int player, int city, string corporation);
    /// <summary>With Realistic Guilds/Corporations (<see cref="CorporationMode"/>), the city in which the player's nation
    /// would found the corporation if this turn's draw chose it, or null when the nation cannot found it now (it is
    /// founded, the nation lacks its technology or resource, its civics ban corporations, or it waits out a cooldown).
    /// The same answer in either mode; only the realistic mode draws for it. Draws nothing.</summary>
    int? NaturalFoundingCity(Simulation sim, int player, string corporation);
    /// <summary>The odds that the city takes a branch of the corporation this turn on its own with Realistic
    /// Guilds/Corporations; a zero threshold when it cannot take one. Draws nothing.</summary>
    CorporationSpreadOdds NaturalSpreadOdds(Simulation sim, int city, string corporation);
    /// <summary>Open a branch in an own city for the spread cost. An executive spreads through the
    /// <see cref="CorporationMissions.Spread"/> mission instead, which also reaches foreign cities.</summary>
    void Spread(Simulation sim, int player, int city, string corporation);
}
/// <summary>The missions the Corporations mod registers. The subject of each is a corporation id.</summary>
public static class CorporationMissions
{
    /// <summary>An executive opens a branch in the city, own or foreign.</summary>
    public const string Spread = "corporations:spread";
}
/// <summary>Pay to open a branch in one of the player's own cities, without an executive.</summary>
public sealed class SpreadCorporationCommand : GameCommand
{
    public int City { get; init; }
    public string Corporation { get; init; } = "";
    public override string? Validate(Simulation sim) => sim.Service<ICorporationService>()!.ValidateSpread(sim, Player, City, Corporation);
    public override void Apply(Simulation sim) => sim.Service<ICorporationService>()!.Spread(sim, Player, City, Corporation);
}
