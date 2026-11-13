using Ariel.Game;

namespace Ariel.Mods.AirCombat;

public enum AirMissionKind { Rebase, Recon, AirStrike, AirBomb }

public sealed record AirMissionReport(AirMissionKind Kind, int Unit, Hex Target, int? Interceptor,
    int AttackerDamage, int InterceptorDamage, int? TargetUnit, int TargetDamage,
    int CityDefenseReduction, string? DestroyedImprovement);

/// <summary>Published after every air mission, intercepted or not.</summary>
public sealed record AirMissionResolved(AirMissionReport Report) : DomainEvent;

/// <summary>The AirCombat mod's published service; read it with <c>sim.Service&lt;IAirMissionService&gt;()</c>, null
/// when the mod is not loaded. Queries never change state or draw random numbers.</summary>
public interface IAirMissionService
{
    /// <summary>How far an aircraft flies on a mission; rebasing reaches twice as far.</summary>
    int AirRange(Simulation sim, int unit);
    /// <summary>The percent chance that a unit on intercept stops an air mission in its range.</summary>
    int InterceptionProbability(Simulation sim, int unit);
    string? Validate(Simulation sim, int player, int unit, AirMissionKind kind, Hex target);
    AirMissionReport Execute(Simulation sim, int unit, AirMissionKind kind, Hex target);
    /// <summary>Active aerial reconnaissance sectors for the current turn.</summary>
    IReadOnlyList<ReconSight> ActiveRecon(Simulation sim);
    /// <summary>Active aerial reconnaissance sectors for a team for the current turn.</summary>
    IReadOnlyList<ReconSight> ActiveRecon(Simulation sim, int team);
}

/// <summary>Fly an aircraft on a mission.</summary>
public sealed class AirMissionCommand : GameCommand
{
    public int Unit { get; init; }
    public AirMissionKind Kind { get; init; }
    public Hex Target { get; init; }
    // Only the AirCombat mod registers this command, and it registers the service with it.
    public override string? Validate(Simulation sim) => sim.Service<IAirMissionService>()!.Validate(sim, Player, Unit, Kind, Target);
    public override void Apply(Simulation sim) => sim.Service<IAirMissionService>()!.Execute(sim, Unit, Kind, Target);
}
