namespace Ariel.Game;

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
/// <summary>A competing branch was evicted by <paramref name="By"/> without compensation, as in AND.</summary>
public sealed record CorporationDisplaced(string Corporation, string By, int City) : DomainEvent;

/// <summary>Read-only previews and validated corporation mutations.</summary>
public interface ICorporationService
{
    IReadOnlyList<string> Present(Simulation sim, int city);
    /// <summary>Whether the city has a branch of the corporation with this AND identity id, which is the id content
    /// references use (see <see cref="Schema.CorporationDef.Identity"/>).</summary>
    bool HasIdentity(Simulation sim, int city, string identity);
    /// <summary>The headquarters city while it exists; null once the corporation is unfounded or dissolved.</summary>
    int? Headquarters(Simulation sim, string corporation);
    bool IsFounded(Simulation sim, string corporation);
    bool IsActive(Simulation sim, int city, string corporation);
    long ConsumedBonusCount(Simulation sim, int city, string corporation);
    long MaintenanceHundredths(Simulation sim, int city);
    string? ValidateFound(Simulation sim, int player, int unit, string corporation);
    void Found(Simulation sim, int player, int unit, string corporation);
    string? ValidateSpread(Simulation sim, int player, int city, string corporation, int? unit = null);
    void Spread(Simulation sim, int player, int city, string corporation, int? unit = null);
}
public sealed class FoundCorporationCommand : GameCommand
{
    public int Unit { get; init; }
    public string Corporation { get; init; } = "";
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<ICorporationService>().Single().ValidateFound(sim, Player, Unit, Corporation);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<ICorporationService>().Single().Found(sim, Player, Unit, Corporation);
}
public sealed class SpreadCorporationCommand : GameCommand
{
    public int City { get; init; }
    public int? Unit { get; init; }
    public string Corporation { get; init; } = "";
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<ICorporationService>().Single().ValidateSpread(sim, Player, City, Corporation, Unit);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<ICorporationService>().Single().Spread(sim, Player, City, Corporation, Unit);
}
