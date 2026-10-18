using Civ.Content;
using Civ.Content.Schema;
using BuildingDef = Civ.Content.Schema.BuildingDef;

namespace Civ.Game;

/// <summary>Saved city presence; multiple religions can coexist.</summary>
public sealed class ReligionCityState
{
    public SortedSet<string> Religions { get; set; } = new(StringComparer.Ordinal);
}
/// <summary>Saved player choice. Secular civics suppress, but do not erase, the last choice.</summary>
public sealed class ReligionPlayerState
{
    public string? LastStateReligion { get; set; }
    public int ConversionTimer { get; set; }
}
/// <summary>Founding records survive destruction of a holy city; a religion cannot be founded twice.</summary>
public sealed class ReligionGameState
{
    public SortedDictionary<string, ReligionFounding> Founded { get; set; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, int> PendingTeams { get; set; } = new(StringComparer.Ordinal);
    public SortedSet<string> PendingAwards { get; set; } = new(StringComparer.Ordinal);
}
public sealed record ReligionFounding(int HolyCity, int Turn);
public sealed record ReligionConversion(int AnarchyTurns, int CooldownTurns, Ledger Ledger);
public sealed record ReligionSpreadOdds(int Threshold, int RandomRange);
public sealed record ReligionFounded(string Religion, int City, int Player) : DomainEvent;
public sealed record ReligionSpread(string Religion, int City) : DomainEvent;
public sealed record ReligionRemoved(string Religion, int City, string Reason) : DomainEvent;
public sealed record InquisitionAttempt(int Unit, int City, int Player, bool Success, IReadOnlyList<string> RemovedReligions, long CompensationGold) : DomainEvent;
public sealed record StateReligionChanged(int Player, string? Religion) : DomainEvent;
public sealed record MissionaryAttempt(int Unit, int City, string Religion, bool Success) : DomainEvent;

/// <summary>Core-only UI and AI hook. Queries never mutate state or consume RNG.</summary>
public interface IReligionService
{
    string? StateReligion(Simulation sim, int player);
    IReadOnlyList<string> Present(Simulation sim, int city);
    int? HolyCity(Simulation sim, string religion);
    IReadOnlyList<ReligionDef> Choices(Simulation sim, int player);
    IReadOnlyList<Civ.Content.Schema.UnitDef> Missionaries(Simulation sim, string religion);
    string? ValidateConversion(Simulation sim, int player, string? religion);
    ReligionConversion PreviewConversion(Simulation sim, int player);
    void Convert(Simulation sim, int player, string? religion);
    string? ValidateMissionary(Simulation sim, int player, int unit, string religion);
    int MissionaryChance(Simulation sim, int unit, string religion);
    void SpreadMissionary(Simulation sim, int unit, string religion);
    ReligionSpreadOdds NaturalSpreadOdds(Simulation sim, int city, string religion);
    int DecayChancePermille(Simulation sim, int city, string religion);
    bool IsInquisitionEnabled(Simulation sim, int player);
    bool HasInquisitionTargets(Simulation sim, int city);
    string? ValidateInquisition(Simulation sim, int player, int unit);
    int InquisitionChance(Simulation sim, int unit);
    void PerformInquisition(Simulation sim, int unit);
    string? ValidateShrine(Simulation sim, int player, int unit, string building);
    void BuildShrine(Simulation sim, int unit, string building);
    string? EvaluateCondition(Requirement.Leaf leaf, RequirementContext context);
    string? ValidateBuilding(Simulation sim, City city, BuildingDef building);
}
public sealed class SetStateReligionCommand : GameCommand
{
    public string? Religion { get; init; }
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<IReligionService>().Single().ValidateConversion(sim, Player, Religion);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<IReligionService>().Single().Convert(sim, Player, Religion);
}
public sealed class BuildShrineCommand : GameCommand
{
    public int Unit { get; init; }
    public string Building { get; init; } = "";
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<IReligionService>().Single().ValidateShrine(sim, Player, Unit, Building);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<IReligionService>().Single().BuildShrine(sim, Unit, Building);
}
public sealed class PerformInquisitionCommand : GameCommand
{
    public int Unit { get; init; }
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<IReligionService>().Single().ValidateInquisition(sim, Player, Unit);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<IReligionService>().Single().PerformInquisition(sim, Unit);
}
public sealed class SpreadReligionCommand : GameCommand
{
    public int Unit { get; init; }
    public string Religion { get; init; } = "";
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<IReligionService>().Single().ValidateMissionary(sim, Player, Unit, Religion);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<IReligionService>().Single().SpreadMissionary(sim, Unit, Religion);
}
