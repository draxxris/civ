using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.Game;

/// <summary>Saved revolution timers. Each boundary consumes one anarchy turn at PlayerEnd.</summary>
public sealed class GovernmentPlayerState
{
    public int AnarchyTurns { get; set; }
    public int RevolutionTimer { get; set; }
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
        sim.State.PlayerById(player).Extensions.TryGet<GovernmentPlayerState>(sim.Content)?.AnarchyTurns > 0;
    public static bool IsDisorder(Simulation sim, City city) =>
        IsAnarchy(sim, city.Owner)
        || (city.Extensions.TryGet<CityRuleState>(sim.Content) is { } state && (state.Disorder || state.OccupationTurns > 0));
}

/// <summary>One atomic revolution. Omitted civic options retain their current choice.</summary>
public sealed class SetCivicsCommand : GameCommand
{
    public List<string> Civics { get; init; } = [];
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<IGovernmentService>().Single().Validate(sim, Player, Civics);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<IGovernmentService>().Single().Adopt(sim, Player, Civics);
}
