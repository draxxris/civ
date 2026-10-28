namespace Ariel.Game;

public enum PassiveEspionageKind { Demographics, CityVisibility, InspectResearch, InvestigateCity }
public enum EspionageMissionKind { StealTreasury, SabotageProduction, InciteRevolt, FomentUnhappiness, StealTech, CounterEspionage }
/// <summary>Team balances and lifetime spending. Balances use whole points.</summary>
public sealed class EspionageTeamState
{
    public SortedDictionary<int, long> Points { get; set; } = new();
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
public sealed class EspionageCityState
{
    public int UnhappyUntilTurn { get; set; }
    public int RevoltUntilTurn { get; set; }
    public bool OwnsDisorder { get; set; }
}
public sealed record EspionageMissionResult(int Unit, int City, EspionageMissionKind Mission, bool Success, long Cost) : DomainEvent;

public interface IEspionageService
{
    long Points(Simulation sim, int team, int targetTeam);
    int Weight(Simulation sim, int player, int targetTeam);
    /// <summary>Forecast whole points per rival, including held points. Does not change state.</summary>
    IReadOnlyDictionary<int, long> Spending(Simulation sim, int player, long espionageHundredths);
    string? ValidateWeight(Simulation sim, int player, int targetTeam, int weight);
    void SetWeight(Simulation sim, int player, int targetTeam, int weight);
    bool HasPassiveEffect(Simulation sim, int team, int targetTeam, PassiveEspionageKind kind);
    long PassiveThreshold(Simulation sim, int team, int targetTeam, PassiveEspionageKind kind);
    long MissionCost(Simulation sim, int player, int city, EspionageMissionKind mission, string? tech = null);
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
