using Ariel.Game;

namespace Ariel.Mods.Espionage;

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
}

/// <summary>Protection owned by this team against one rival: a timer of kind <c>espionage:counter_espionage</c> held by
/// the pair of teams, whose <see cref="GameTimer.Value"/> is the cost percentage. Inactive at ExpiresTurn;
/// CostPercent is a multiplier, not an addition.</summary>
public sealed record CounterEspionageEffect(int ExpiresTurn, int CostPercent);

public sealed class EspionagePlayerState
{
    public SortedDictionary<int, int> Weights { get; set; } = new();
    public long RemainderHundredths { get; set; }
}

/// <summary>Threshold and unlock state for one passive tier, computed once per rival.</summary>
public sealed record PassiveEspionageStatus(PassiveEspionageKind Kind, long Threshold, bool Unlocked);

/// <summary>A spy of <paramref name="Player"/> ran a mission; a failed spy is destroyed.</summary>
public sealed record EspionageMissionResult(int Unit, int City, EspionageMissionKind Mission, bool Success, long Cost, int Player) : DomainEvent;

/// <summary>The commerce type this mod registers (<c>data/commerce.json</c>): its vector key and a player's output of it.</summary>
public static class EspionageCommerce
{
    /// <summary>Vector key of the espionage commerce type, in vectors and in keys derived from it
    /// (<c>commerce.percent.espionage</c>).</summary>
    public const string Key = "espionage";

    extension(Ariel.Content.CommerceVector vector)
    {
        /// <summary>The espionage amount; zero in a vector that does not carry it.</summary>
        public long Espionage => vector[Key];
    }

    /// <summary>The player's espionage this turn, in hundredths: what settlement recorded once the player settled, else
    /// the current calculation.</summary>
    public static long Hundredths(Simulation sim, int player) =>
        sim.Content.Commerce.TryGet(Key, out var type) ? sim.Rules().Economy.CommerceHundredths(sim, player, type) : 0;
}

/// <summary>Espionage points, weights, passive tiers and mission prices.</summary>
public interface IEspionageService
{
    /// <summary>Forecast whole points per target team for this much espionage output (hundredths). Does not change
    /// state.</summary>
    IReadOnlyDictionary<int, long> Spending(Simulation sim, int player, long espionageHundredths);
    long Points(Simulation sim, int team, int targetTeam);
    int Weight(Simulation sim, int player, int targetTeam);
    /// <summary>Known, living rival teams for this player, lowest id first. The one rival query every caller shares.</summary>
    IReadOnlyList<int> KnownRivals(Simulation sim, int player);
    /// <summary>Whole points held back because every weight is zero. Does not change state.</summary>
    long Held(Simulation sim, int player);
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
}

public sealed class SetEspionageWeightCommand : GameCommand
{
    public int TargetTeam { get; init; }
    public int Weight { get; init; } = 1;
    /// <summary>Weights are the Espionage player state, in its private points domain.</summary>
    public override IReadOnlyList<string>? WriteDomains => Points;
    private static readonly IReadOnlyList<string> Points = ["espionage:points"];
    public override string? Validate(Simulation sim) => Espionage.Service(sim).ValidateWeight(sim, Player, TargetTeam, Weight);
    public override void Apply(Simulation sim) => Espionage.Service(sim).SetWeight(sim, Player, TargetTeam, Weight);
}

internal static class Espionage
{
    internal static IEspionageService Service(Simulation sim) => sim.Service<IEspionageService>()
        ?? throw new InvalidOperationException("the Espionage mod is not loaded");
}

/// <summary>The missions the Espionage mod registers, one per <see cref="EspionageMissionKind"/>. Only a technology
/// theft has a subject: the technology to steal.</summary>
public static class EspionageMissions
{
    /// <summary>The mission id of a kind, such as "espionage:steal_treasury".</summary>
    public static string Of(EspionageMissionKind kind) => kind switch
    {
        EspionageMissionKind.StealTreasury => "espionage:steal_treasury",
        EspionageMissionKind.SabotageProduction => "espionage:sabotage_production",
        EspionageMissionKind.InciteRevolt => "espionage:incite_revolt",
        EspionageMissionKind.FomentUnhappiness => "espionage:foment_unhappiness",
        EspionageMissionKind.StealTech => "espionage:steal_tech",
        EspionageMissionKind.CounterEspionage => "espionage:counter_espionage",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>The order for a spy to run a mission where it stands.</summary>
    public static PerformMissionCommand Order(int player, int spy, EspionageMissionKind kind, string? tech = null) =>
        new() { Player = player, Unit = spy, Mission = Of(kind), Subject = tech ?? "" };
}
