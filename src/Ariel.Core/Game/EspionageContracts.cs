namespace Ariel.Game;

public enum PassiveEspionageKind { Demographics, CityVisibility, InspectResearch, InvestigateCity }

/// <summary>
/// Saved command format and mission button order. Ordinals are persisted in commands, so append new members
/// and never reorder existing ones: a reordering silently invalidates replays and saved commands.
/// Use <see cref="EspionageMissionOrder.Display"/> for presentation order instead of enum order.
/// </summary>
public enum EspionageMissionKind
{
    StealTreasury = 0,
    SabotageProduction = 1,
    InciteRevolt = 2,
    FomentUnhappiness = 3,
    StealTech = 4,
    CounterEspionage = 5
}

/// <summary>Mission order offered by the spy panel, independent of the persisted ordinals above.</summary>
public static class EspionageMissionOrder
{
    public static readonly IReadOnlyList<EspionageMissionKind> Display =
    [
        EspionageMissionKind.StealTreasury, EspionageMissionKind.StealTech, EspionageMissionKind.SabotageProduction,
        EspionageMissionKind.InciteRevolt, EspionageMissionKind.FomentUnhappiness, EspionageMissionKind.CounterEspionage
    ];
}

/// <summary>Team balances and lifetime allocation. Balances use whole points; running a mission spends them.</summary>
public sealed class EspionageTeamState
{
    public SortedDictionary<int, long> Points { get; set; } = new();
    /// <summary>Points allocated against a rival. Mission costs are not credited here, so spending lowers investment.</summary>
    public SortedDictionary<int, long> Spent { get; set; } = new();
    public SortedDictionary<int, CounterEspionageEffect> CounterEspionage { get; set; } = new();
}

/// <summary>Protection owned by this team against one rival. Inactive at ExpiresTurn; CostPercent is a multiplier, not an addition.</summary>
public sealed record CounterEspionageEffect(int ExpiresTurn, int CostPercent);

public sealed class EspionagePlayerState
{
    public SortedDictionary<int, int> Weights { get; set; } = new();
    public long RemainderHundredths { get; set; }
}

/// <summary>Per-city spy deadlines as absolute turns. Zero means "no active effect".</summary>
public sealed class EspionageCityState
{
    public int UnhappyUntilTurn { get; set; }
    /// <summary>Disorder imposed by a spy lasts until this turn. No other system writes it.</summary>
    public int RevoltUntilTurn { get; set; }
}

/// <summary>Threshold and unlock state for one passive tier, computed once per rival.</summary>
public sealed record PassiveEspionageStatus(PassiveEspionageKind Kind, long Threshold, bool Unlocked);

public sealed record EspionageMissionResult(int Unit, int City, EspionageMissionKind Mission, bool Success, long Cost) : DomainEvent;

public interface IEspionageService
{
    long Points(Simulation sim, int team, int targetTeam);
    int Weight(Simulation sim, int player, int targetTeam);
    /// <summary>Known, living rival teams for this player, lowest id first. The one rival query every caller shares.</summary>
    IReadOnlyList<int> KnownRivals(Simulation sim, int player);
    /// <summary>Whole points held back because every weight is zero. Does not change state.</summary>
    long Held(Simulation sim, int player);
    /// <summary>Forecast whole points per rival, including held points. Does not change state.</summary>
    IReadOnlyDictionary<int, long> Spending(Simulation sim, int player, long espionageHundredths);
    string? ValidateWeight(Simulation sim, int player, int targetTeam, int weight);
    void SetWeight(Simulation sim, int player, int targetTeam, int weight);
    bool HasPassiveEffect(Simulation sim, int team, int targetTeam, PassiveEspionageKind kind);
    long PassiveThreshold(Simulation sim, int team, int targetTeam, PassiveEspionageKind kind);
    /// <summary>Every passive tier against one rival, with one investment calculation instead of four.</summary>
    IReadOnlyList<PassiveEspionageStatus> PassiveStatus(Simulation sim, int team, int targetTeam);
    long MissionCost(Simulation sim, int player, int city, EspionageMissionKind mission, string? tech = null);
    /// <summary>Technologies the target city's team knows, this player does not, and this player could research.
    /// Lowest id first; the caller prices and orders them.</summary>
    IReadOnlyList<string> StealableTechnologies(Simulation sim, int player, int city);
    /// <summary>Active protection owned by defendingTeam against rivalTeam, or null at and after expiry.</summary>
    CounterEspionageEffect? CounterEspionage(Simulation sim, int defendingTeam, int rivalTeam);
    int SuccessChance(Simulation sim, int player, int city);
    string? ValidateMission(Simulation sim, int player, int unit, int city, EspionageMissionKind mission, string? tech = null);
    void ExecuteMission(Simulation sim, int player, int unit, int city, EspionageMissionKind mission, string? tech = null);
}

public sealed class SetEspionageWeightCommand : GameCommand
{
    public int TargetTeam { get; init; }
    public int Weight { get; init; } = 1;
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<IEspionageService>().Single().ValidateWeight(sim, Player, TargetTeam, Weight);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<IEspionageService>().Single().SetWeight(sim, Player, TargetTeam, Weight);
}

public sealed class ExecuteEspionageMissionCommand : GameCommand
{
    public int Unit { get; init; }
    public int City { get; init; }
    public EspionageMissionKind Mission { get; init; }
    public string? Tech { get; init; }
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<IEspionageService>().Single().ValidateMission(sim, Player, Unit, City, Mission, Tech);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<IEspionageService>().Single().ExecuteMission(sim, Player, Unit, City, Mission, Tech);
}
