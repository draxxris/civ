using Ariel.Content;
using Ariel.Content.Schema;
using BuildingDef = Ariel.Content.Schema.BuildingDef;

namespace Ariel.Game;

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
}
public sealed record ReligionFounding(int HolyCity, int Turn);
public sealed record ReligionConversion(int AnarchyTurns, int CooldownTurns, Ledger Ledger);
public sealed record ReligionSpreadOdds(int Threshold, int RandomRange);
public sealed record ReligionFounded(string Religion, int City, int Player) : DomainEvent;
public sealed record ReligionSpread(string Religion, int City) : DomainEvent;
public sealed record ReligionRemoved(string Religion, int City, string Reason) : DomainEvent;
public sealed record InquisitionAttempt(int Unit, int City, int Player, bool Success, IReadOnlyList<string> RemovedReligions, long CompensationGold) : DomainEvent;
public sealed record StateReligionChanged(int Player, string? Religion) : DomainEvent;

/// <summary>Core-only UI and AI hook. Queries never mutate state or consume RNG.</summary>
public interface IReligionService
{
    string? StateReligion(Simulation sim, int player);
    IReadOnlyList<string> Present(Simulation sim, int city);
    int? HolyCity(Simulation sim, string religion);
    IReadOnlyList<ReligionDef> Choices(Simulation sim, int player);
    string? ValidateConversion(Simulation sim, int player, string? religion);
    ReligionConversion PreviewConversion(Simulation sim, int player);
    void Convert(Simulation sim, int player, string? religion);
    ReligionSpreadOdds NaturalSpreadOdds(Simulation sim, int city, string religion);
    int DecayChancePermille(Simulation sim, int city, string religion);
    bool IsInquisitionEnabled(Simulation sim, int player);
    bool HasInquisitionTargets(Simulation sim, int city);
    string? EvaluateCondition(Requirement.Leaf leaf, RequirementContext context);
    string? ValidateBuilding(Simulation sim, City city, BuildingDef building);
}
public sealed class SetStateReligionCommand : GameCommand
{
    public string? Religion { get; init; }
    public override string? Validate(Simulation sim) => sim.Rules().Religion.ValidateConversion(sim, Player, Religion);
    public override void Apply(Simulation sim) => sim.Rules().Religion.Convert(sim, Player, Religion);
}
/// <summary>The missions the Religion module registers.</summary>
public static class ReligionMissions
{
    /// <summary>A missionary tries to bring its religion to the city. The subject is the religion id.</summary>
    public const string Spread = "religion:spread";
    /// <summary>An inquisitor tries to remove every religion but the state religion from the city. No subject.</summary>
    public const string Inquisition = "religion:inquisition";
    /// <summary>A prophet builds a religion's shrine in its holy city. The subject is the building id.</summary>
    public const string Shrine = "religion:shrine";
}
