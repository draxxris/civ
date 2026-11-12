using Ariel.Content;
using Ariel.Game;

namespace Ariel.Mods.GreatPeople;

/// <summary>Historical progress and current whole-point rate. Class points are unmodified source weights.</summary>
public sealed record GreatPeopleCityResult(long Progress, long Threshold, long Rate,
    IReadOnlyDictionary<string, long> ClassPoints, Ledger Ledger);
public sealed record GreatPersonAction(string Command, string? Target, long Amount);
/// <summary>Read-only queries. Queries must not change state or consume RNG.</summary>
public interface IGreatPeopleService
{
    GreatPeopleCityResult Calculate(Simulation sim, int city);
    long Threshold(Simulation sim, int player);
    long GeneralThreshold(Simulation sim, int player);
    long GeneralProgress(Simulation sim, int player);
    IReadOnlyDictionary<string, int> SettledSpecialists(Simulation sim, int city);
    int GoldenAgeTurnsRemaining(Simulation sim, int player);
    int GoldenAgeUnitsRequired(Simulation sim, int player);
    /// <summary>Preview at the current position, or at a proposed destination without moving the unit.</summary>
    IReadOnlyList<GreatPersonAction> AvailableActions(Simulation sim, int player, int unit, Hex? destination = null);
}
/// <summary>A city's great people threshold and rate, as the city screen shows them.</summary>
public sealed record CityGreatPeopleResult(long Threshold, long Rate, Ledger Ledger);
/// <summary>The city screen's view of a city's great people. Queries must not change state or consume RNG.</summary>
public interface ICityGreatPeopleService
{
    CityGreatPeopleResult Calculate(Simulation sim, int city);
}
/// <summary>Command boundary shared by previews and execution.</summary>
public interface IGreatPeopleCommandService
{
    string? Validate(Simulation sim, int player, int unit, string action, string? target, IReadOnlyList<int> units);
    void Execute(Simulation sim, int player, int unit, string action, string? target, IReadOnlyList<int> units);
}
public abstract class GreatPersonCommand : GameCommand
{
    public int UnitId { get; init; }
    public string? Target { get; init; }
    public int[] Units { get; init; } = [];
    protected abstract string Action { get; }
    public override string? Validate(Simulation sim) => Commands(sim).Validate(sim, Player, UnitId, Action, Target, Units);
    public override void Apply(Simulation sim) => Commands(sim).Execute(sim, Player, UnitId, Action, Target, Units);
    private static IGreatPeopleCommandService Commands(Simulation sim) => sim.Service<IGreatPeopleCommandService>()
        ?? throw new InvalidOperationException("the Great People mod is not loaded");
}
public sealed class LeadGreatPersonCommand : GreatPersonCommand { protected override string Action => "lead"; }
public sealed class ConstructGreatPersonCommand : GreatPersonCommand { protected override string Action => "construct"; }
public sealed class TradeGreatPersonCommand : GreatPersonCommand { protected override string Action => "trade"; }
public sealed class JoinGreatPersonCommand : GreatPersonCommand { protected override string Action => "join"; }
public sealed class DiscoverGreatPersonCommand : GreatPersonCommand { protected override string Action => "discover"; }
public sealed class HurryGreatPersonCommand : GreatPersonCommand { protected override string Action => "hurry"; }
public sealed class CultureGreatPersonCommand : GreatPersonCommand { protected override string Action => "culture"; }
public sealed class GoldenAgeGreatPersonCommand : GreatPersonCommand { protected override string Action => "golden-age"; }
public sealed class GreatPeopleCityState
{
    public long Progress { get; set; }
    public SortedDictionary<string, long> ClassPoints { get; set; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, int> SettledSpecialists { get; set; } = new(StringComparer.Ordinal);
}
public sealed class GreatPeoplePlayerState
{
    public int Births { get; set; }
    public int ThresholdModifier { get; set; }
    public int GoldenAgeTurns { get; set; }
    public int UnitGoldenAges { get; set; }
    public int FirstGoldenAgeTurn { get; set; }
}
public sealed record GreatGeneralBorn(int Player, int City, int Unit, string UnitType) : DomainEvent;
public sealed record GreatPersonBorn(int Player, int City, int Unit, string UnitClass) : DomainEvent;
public sealed record GoldenAgeStarted(int Player, int Turns) : DomainEvent;
public sealed record GreatPersonUsed(int Player, int Unit, string Action, string? Target, long Amount) : DomainEvent;
