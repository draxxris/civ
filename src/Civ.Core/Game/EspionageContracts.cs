namespace Civ.Game;

public enum PassiveEspionageKind { Demographics, CityVisibility, InspectResearch, InvestigateCity }
public enum EspionageMissionKind { StealTreasury, SabotageProduction, InciteRevolt, FomentUnhappiness, StealTech }
/// <summary>Team balances and lifetime spending. Balances use whole points.</summary>
public sealed class EspionageTeamState
{
    public SortedDictionary<int, long> Points { get; set; } = new();
    public SortedDictionary<int, long> Spent { get; set; } = new();
}
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
    string? ValidateWeight(Simulation sim, int player, int targetTeam, int weight);
    void SetWeight(Simulation sim, int player, int targetTeam, int weight);
    bool HasPassiveEffect(Simulation sim, int team, int targetTeam, PassiveEspionageKind kind);
    long PassiveThreshold(Simulation sim, int team, int targetTeam, PassiveEspionageKind kind);
    long MissionCost(Simulation sim, int player, int city, EspionageMissionKind mission, string? tech = null);
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
