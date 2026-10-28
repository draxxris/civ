using Ariel.Content;

namespace Ariel.Game;

public enum CityStabilityStatus { Stable, Warning, Mutiny, Rebellion }

/// <summary>Saved city instability and absolute turn deadlines. Queries must not attach this component.</summary>
public sealed class RevolutionCityState
{
    public int InstabilityIndex { get; set; }
    public int LastUpdatedTurn { get; set; }
    public int RebellionCooldownUntilTurn { get; set; }
    public int UnrestUntilTurn { get; set; }
}

public sealed record CityStabilityDescription(int City, int InstabilityIndex, CityStabilityStatus Status,
    int ChangePerTurn, int CooldownTurns, int UnrestTurns, Ledger Ledger);

public interface IRevolutionService
{
    CityStabilityDescription DescribeCityStability(Simulation sim, int cityId);
    string? ValidateSuppression(Simulation sim, int player, int cityId, int? garrison);
    void Suppress(Simulation sim, int player, int cityId, int? garrison);
}

/// <summary>Pay gold, or consume the remaining movement of an owned military garrison.</summary>
public sealed class SuppressRebellionCommand : GameCommand
{
    public int CityId { get; init; }
    public int? Garrison { get; init; }
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<IRevolutionService>().Single()
        .ValidateSuppression(sim, Player, CityId, Garrison);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<IRevolutionService>().Single()
        .Suppress(sim, Player, CityId, Garrison);
}

public sealed record CityRebellionTriggered(int City, int Player, int RebelPlayer,
    IReadOnlyList<int> Units, int InstabilityReleased) : DomainEvent;
public sealed record CityRebellionSuppressed(int City, int Player, int? Garrison, long Gold, int Reduction) : DomainEvent;
