using Ariel.Content;

namespace Ariel.Game;

public enum CityStabilityStatus { Stable, Warning, Mutiny, Rebellion }

/// <summary>Saved city instability and absolute turn deadlines. Queries must not attach this component.</summary>
public sealed class RevolutionCityState
{
    private int _instabilityIndex;
    private int _lastUpdatedTurn;
    private int _rebellionCooldownUntilTurn;
    private int _unrestUntilTurn;

    public int InstabilityIndex
    {
        get => _instabilityIndex;
        set => _instabilityIndex = Math.Max(0, value);
    }

    public int LastUpdatedTurn
    {
        get => _lastUpdatedTurn;
        set => _lastUpdatedTurn = Math.Max(0, value);
    }

    public int RebellionCooldownUntilTurn
    {
        get => _rebellionCooldownUntilTurn;
        set => _rebellionCooldownUntilTurn = Math.Max(0, value);
    }

    public int UnrestUntilTurn
    {
        get => _unrestUntilTurn;
        set => _unrestUntilTurn = Math.Max(0, value);
    }

    public bool IsRebellionOnCooldown(int currentTurn) => currentTurn < _rebellionCooldownUntilTurn;

    public bool IsInUnrest(int currentTurn) => currentTurn < _unrestUntilTurn;

    public int CooldownTurnsRemaining(int currentTurn) => Math.Max(0, _rebellionCooldownUntilTurn - currentTurn);

    public int UnrestTurnsRemaining(int currentTurn) => Math.Max(0, _unrestUntilTurn - currentTurn);

    public void RecordInstabilityChange(long delta, int turn)
    {
        _instabilityIndex = (int)Math.Clamp(_instabilityIndex + delta, 0, int.MaxValue);
        _lastUpdatedTurn = Math.Max(0, turn);
    }

    public void TriggerRebellion(int currentTurn, int threshold, int release, int cooldownTurns, int unrestTurns)
    {
        _instabilityIndex = Math.Max(0, Math.Min(_instabilityIndex, threshold) - release);
        _rebellionCooldownUntilTurn = currentTurn + 1 + cooldownTurns;
        _unrestUntilTurn = currentTurn + 1 + unrestTurns;
    }

    public void Suppress(int reduction)
    {
        _instabilityIndex = Math.Max(0, _instabilityIndex - reduction);
    }

    public void Incite(int currentTurn, int additionalInstability, int unrestTurns)
    {
        _instabilityIndex = (int)Math.Clamp(_instabilityIndex + additionalInstability, 0, int.MaxValue);
        _unrestUntilTurn = Math.Max(_unrestUntilTurn, currentTurn + 1 + unrestTurns);
    }
}

public sealed record CityStabilityDescription(int City, int InstabilityIndex, CityStabilityStatus Status,
    int ChangePerTurn, int CooldownTurns, int UnrestTurns, long SuppressionGold, Ledger Ledger);

public interface IRevolutionService
{
    CityStabilityDescription DescribeCityStability(Simulation sim, int cityId);
    string? ValidateSuppression(Simulation sim, int player, int cityId, int? garrison);
    void Suppress(Simulation sim, int player, int cityId, int? garrison);
    /// <summary>Outside agents start a revolt: raises instability and unrest without spawning units directly.
    /// A city already in disorder is left alone. Returns false when the city cannot be incited.</summary>
    bool Incite(Simulation sim, int cityId);
    /// <summary>Own cities at or above the warning tier, lowest id first. Reads saved instability only, so a caller
    /// never pays for a wellbeing ledger just to count them.</summary>
    IReadOnlyList<int> CitiesAtRisk(Simulation sim, int player);
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

/// <summary>RebelPlayer is the barbarian or rebel player that owns the spawned units, or -1 when no legal tile
/// existed and no faction had to be created.</summary>
public sealed record CityRebellionTriggered(int City, int Player, int RebelPlayer,
    IReadOnlyList<int> Units, int InstabilityReleased) : DomainEvent;
public sealed record CityRebellionSuppressed(int City, int Player, int? Garrison, long Gold, int Reduction) : DomainEvent;
