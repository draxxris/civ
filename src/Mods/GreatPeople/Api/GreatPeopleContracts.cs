using Ariel.Content;
using Ariel.Game;

namespace Ariel.Mods.GreatPeople;

/// <summary>Historical progress and current whole-point rate. Class points are unmodified source weights.</summary>
public sealed record GreatPeopleCityResult(long Progress, long Threshold, long Rate,
    IReadOnlyDictionary<string, long> ClassPoints, Ledger Ledger);
/// <summary>One action open to a great person: the mission (<see cref="GreatPersonMissions"/>), its subject (empty when
/// there is nothing to choose) and what it yields (gold, research, production, culture, experience or turns).</summary>
public sealed record GreatPersonAction(string Mission, string Subject, long Amount);
/// <summary>The great person missions, carried out with <see cref="PerformMissionCommand"/>. Each uses up the person.</summary>
public static class GreatPersonMissions
{
    /// <summary>Settle in an own city as a free specialist. The subject is the specialist id.</summary>
    public const string Join = "great_people:join_city";
    /// <summary>Put research into the technology the person favours. The subject is the technology id; no city needed.</summary>
    public const string Discover = "great_people:discover_tech";
    /// <summary>Add production to the building an own city is constructing. The subject is the building id.</summary>
    public const string Hurry = "great_people:hurry_production";
    /// <summary>A great work: culture for an own city. No subject.</summary>
    public const string Culture = "great_people:culture_bomb";
    /// <summary>Start a golden age, anywhere. The subject is empty, so the nearest great people of distinct classes
    /// join in, or the participants' unit ids separated by commas, the performing unit included.</summary>
    public const string GoldenAge = "great_people:golden_age";
    /// <summary>A great general joins a combat unit on its plot, outside a city too. The subject is that unit's id.</summary>
    public const string Lead = "great_people:lead_unit";
    /// <summary>Construct a special building in an own city. The subject is the building id.</summary>
    public const string Construct = "great_people:construct_building";
    /// <summary>Gold from a trade mission to a foreign city at peace. The subject is that city's id.</summary>
    public const string Trade = "great_people:trade_mission";
}
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
/// <summary>A great person carried out a mission (<see cref="GreatPersonMissions"/>) and what it yielded.</summary>
public sealed record GreatPersonUsed(int Player, int Unit, string Mission, string Subject, long Amount) : DomainEvent;
