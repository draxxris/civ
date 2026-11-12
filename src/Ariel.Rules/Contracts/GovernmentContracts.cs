using System.Text.Json.Serialization;
using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.Game;

/// <summary>Saved civic-change timers. Each boundary consumes one anarchy turn at PlayerEnd.</summary>
public sealed class GovernmentPlayerState
{
    private int _anarchyTurns;
    private int _revolutionTimer;

    public int AnarchyTurns
    {
        get => _anarchyTurns;
        set
        {
            _anarchyTurns = Math.Max(0, value);
            if (_revolutionTimer < _anarchyTurns)
                _revolutionTimer = _anarchyTurns;
        }
    }

    /// <summary>Cooldown before the next civic change. Unrelated to city instability, which the Revolution mod keeps
    /// in <see cref="RevolutionCityState"/>.</summary>
    public int RevolutionTimer
    {
        get => _revolutionTimer;
        set => _revolutionTimer = Math.Max(_anarchyTurns, Math.Max(0, value));
    }

    [JsonIgnore]
    public bool IsAnarchy => _anarchyTurns > 0;

    public void StartRevolution(int anarchyTurns, int cooldownTurns)
    {
        _anarchyTurns = Math.Max(0, anarchyTurns);
        _revolutionTimer = Math.Max(_anarchyTurns, Math.Max(0, cooldownTurns));
    }

    public void AdvanceTurn()
    {
        _anarchyTurns = Math.Max(0, _anarchyTurns - 1);
        _revolutionTimer = Math.Max(0, _revolutionTimer - 1);
    }
}

public sealed record CivicSwitchResult(int AnarchyTurns, int CooldownTurns, Ledger Ledger);
public interface IGovernmentService
{
    string? Validate(Simulation sim, int player, IReadOnlyList<string> civics);
    CivicSwitchResult Calculate(Simulation sim, int player, IReadOnlyList<string> civics);
    void Adopt(Simulation sim, int player, IReadOnlyList<string> civics);
}

/// <summary>Read-only identity and the complete imported AI personality. Queries never consume RNG.</summary>
public interface IPeoplesService
{
    LeaderDef? Leader(Simulation sim, int player);
    Ariel.Content.Schema.CivilizationDef? Civilization(Simulation sim, int player);
    IReadOnlyList<TraitDef> Traits(Simulation sim, int player);
}

public static class GovernmentStatus
{
    public static bool IsAnarchy(Simulation sim, int player) =>
        sim.State.PlayerById(player).Extensions.TryGet<GovernmentPlayerState>(sim.Content)?.IsAnarchy == true;
    public static bool IsDisorder(Simulation sim, City city) =>
        IsAnarchy(sim, city.Owner)
        || (city.Extensions.TryGet<CityRuleState>(sim.Content) is { } state && (state.Disorder || state.OccupationTurns > 0));
}

/// <summary>One atomic revolution. Omitted civic options retain their current choice.</summary>
public sealed class SetCivicsCommand : GameCommand
{
    public List<string> Civics { get; init; } = [];
    public override string? Validate(Simulation sim) => sim.Rules().Government.Validate(sim, Player, Civics);
    public override void Apply(Simulation sim) => sim.Rules().Government.Adopt(sim, Player, Civics);
}
