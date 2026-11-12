using System.Text.Json.Serialization;
using Ariel.Content;

namespace Ariel.Game;

public readonly record struct TeamPair : IComparable<TeamPair>
{
    public int First { get; }
    public int Second { get; }

    public TeamPair(int a, int b)
    {
        First = Math.Min(a, b);
        Second = Math.Max(a, b);
    }

    public static TeamPair Of(int a, int b) => new(a, b);

    [JsonIgnore]
    public string Key => $"{First}/{Second}";

    public override string ToString() => Key;

    public int CompareTo(TeamPair other)
    {
        int cmp = First.CompareTo(other.First);
        return cmp != 0 ? cmp : Second.CompareTo(other.Second);
    }

    public static implicit operator string(TeamPair pair) => pair.Key;
}

public readonly record struct PlayerDirection : IComparable<PlayerDirection>
{
    public int FromPlayer { get; }
    public int TowardPlayer { get; }

    public PlayerDirection(int fromPlayer, int towardPlayer)
    {
        FromPlayer = fromPlayer;
        TowardPlayer = towardPlayer;
    }

    public static PlayerDirection Of(int from, int toward) => new(from, toward);

    [JsonIgnore]
    public string Key => $"{FromPlayer}/{TowardPlayer}";

    public override string ToString() => Key;

    public int CompareTo(PlayerDirection other)
    {
        int cmp = FromPlayer.CompareTo(other.FromPlayer);
        return cmp != 0 ? cmp : TowardPlayer.CompareTo(other.TowardPlayer);
    }

    public static implicit operator string(PlayerDirection dir) => dir.Key;
}

/// <summary>Memory ids recorded with <see cref="IDiplomacyService.RecordMemory"/>. A leader's attitude weight for one is
/// <c>LeaderDef.MemoryAttitudePercents</c> under the id without its <c>memory_</c> prefix.</summary>
public static class DiplomaticMemories
{
    public const string Inquisition = "memory_inquisition";
    public const string DeclaredWar = "memory_declared_war";
    public const string DeclaredWarOnFriend = "memory_declared_war_on_friend";
    public const string RazedCity = "memory_razed_city";
    public const string RazedHolyCity = "memory_razed_holy_city";
    public const string SpyCaught = "memory_spy_caught";
    public const string TradedTechToUs = "memory_traded_tech_to_us";
    public const string GiveHelp = "memory_give_help";
    public const string StoppedTrading = "memory_stopped_trading";
}

public enum AttitudeTier { Furious, Annoyed, Cautious, Pleased, Friendly }
public sealed record AttitudeResult(int Score, AttitudeTier Tier, Ledger Ledger);
public enum DiplomaticAgreement { OpenBorders, DefensivePact }
public enum TradeItemKind { Resource, GoldPerTurn, Gold, Technology }
/// <summary>An item given by one participant to the other. Amounts must be positive; technology amounts must be one.
/// Canonical Id distinguishes individual trade items within a deal.</summary>
public sealed record DiplomaticTradeItem(int FromPlayer, TradeItemKind Kind, string ContentId = "", long Amount = 1, int Id = 0);
public enum DiplomaticDealStatus { Proposed, Active, Completed, Cancelled }
public sealed class DiplomaticDeal
{
    public int Id { get; set; }
    public int Proposer { get; set; }
    public int Recipient { get; set; }
    public int StartedTurn { get; set; }
    public int LastSettledTurn { get; set; }
    public DiplomaticDealStatus Status { get; set; }
    public List<DiplomaticTradeItem> Items { get; set; } = [];

    public DiplomaticTradeItem? FindItem(int itemId) => Items.FirstOrDefault(i => i.Id == itemId);
    public bool RemoveItem(int itemId) => Items.RemoveAll(i => i.Id == itemId) > 0;
}
public sealed record TeamsMet(int FirstTeam, int SecondTeam) : DomainEvent;
public sealed record DiplomaticWarDeclared(int AggressorTeam, int DefenderTeam) : DomainEvent;
public sealed record DiplomaticPeaceSigned(int FirstTeam, int SecondTeam) : DomainEvent;
public sealed record TradeItemCancelled(int DealId, int ItemId, int Player) : DomainEvent;

/// <summary>Optional diplomacy owner. Queries are RNG-free and do not mutate state. Team ids are not player ids.</summary>
public interface IDiplomacyService
{
    bool HasMet(Simulation sim, int teamA, int teamB);
    void EstablishContact(Simulation sim, int teamA, int teamB);
    void DiscoverContacts(Simulation sim);
    AttitudeResult GetAttitude(Simulation sim, int fromPlayer, int towardPlayer);
    void RecordMemory(Simulation sim, int fromPlayer, int towardPlayer, string memory);
    bool HasOpenBorders(Simulation sim, int teamA, int teamB);
    bool HasDefensivePact(Simulation sim, int teamA, int teamB);
    string? ValidateAgreement(Simulation sim, int player, int target, DiplomaticAgreement agreement);
    void SignAgreement(Simulation sim, int player, int target, DiplomaticAgreement agreement);
    string? ValidateCancelAgreement(Simulation sim, int player, int target, DiplomaticAgreement agreement);
    void CancelAgreement(Simulation sim, int player, int target, DiplomaticAgreement agreement);
    string? ValidateWar(Simulation sim, int player, int target);
    void DeclareWar(Simulation sim, int player, int target);
    string? ValidatePeace(Simulation sim, int player, int target);
    void MakePeace(Simulation sim, int player, int target);
    string? ValidateProposal(Simulation sim, int player, int target, IReadOnlyList<DiplomaticTradeItem> items);
    int ProposeDeal(Simulation sim, int player, int target, IReadOnlyList<DiplomaticTradeItem> items);
    string? ValidateExecution(Simulation sim, int player, int deal);
    void ExecuteDeal(Simulation sim, int deal);
    string? ValidateCancellation(Simulation sim, int player, int deal);
    void CancelDeal(Simulation sim, int player, int deal);
    string? ValidateCancelTradeItem(Simulation sim, int player, int dealId, int itemId);
    void CancelTradeItem(Simulation sim, int player, int dealId, int itemId);
    IReadOnlyList<DiplomaticDeal> Deals(Simulation sim);
    /// <summary>Net imported minus exported copies. Physical resource counts remain with IResourceNetwork.</summary>
    long ResourceBalance(Simulation sim, int player, string resource);
    /// <summary>Physical connected copies plus diplomatic allocations, clamped to zero.</summary>
    long AvailableResourceCount(Simulation sim, int player, int city, string resource);
    /// <summary>The same answer for several resources, reusing one connectivity query.</summary>
    IReadOnlyDictionary<string, long> AvailableResourceCounts(Simulation sim, int player, int city, IReadOnlyCollection<string> resources);
}

public sealed class SignOpenBordersCommand : GameCommand
{
    public int Target { get; init; }
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<IDiplomacyService>().Single().ValidateAgreement(sim, Player, Target, DiplomaticAgreement.OpenBorders);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<IDiplomacyService>().Single().SignAgreement(sim, Player, Target, DiplomaticAgreement.OpenBorders);
}
public sealed class SignDefensivePactCommand : GameCommand
{
    public int Target { get; init; }
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<IDiplomacyService>().Single().ValidateAgreement(sim, Player, Target, DiplomaticAgreement.DefensivePact);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<IDiplomacyService>().Single().SignAgreement(sim, Player, Target, DiplomaticAgreement.DefensivePact);
}
public sealed class CancelAgreementCommand : GameCommand
{
    public int Target { get; init; }
    public DiplomaticAgreement Agreement { get; init; }
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<IDiplomacyService>().Single().ValidateCancelAgreement(sim, Player, Target, Agreement);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<IDiplomacyService>().Single().CancelAgreement(sim, Player, Target, Agreement);
}
public sealed class ProposeDealCommand : GameCommand
{
    public int Target { get; init; }
    public List<DiplomaticTradeItem> Items { get; init; } = [];
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<IDiplomacyService>().Single().ValidateProposal(sim, Player, Target, Items);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<IDiplomacyService>().Single().ProposeDeal(sim, Player, Target, Items);
}
/// <summary>Only the recipient can accept a proposal. Execution revalidates its assets.</summary>
public sealed class ExecuteDealCommand : GameCommand
{
    public int Deal { get; init; }
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<IDiplomacyService>().Single().ValidateExecution(sim, Player, Deal);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<IDiplomacyService>().Single().ExecuteDeal(sim, Deal);
}
public sealed class CancelDealCommand : GameCommand
{
    public int Deal { get; init; }
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<IDiplomacyService>().Single().ValidateCancellation(sim, Player, Deal);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<IDiplomacyService>().Single().CancelDeal(sim, Player, Deal);
}
public sealed class CancelTradeItemCommand : GameCommand
{
    public int Deal { get; init; }
    public int ItemId { get; init; }
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<IDiplomacyService>().Single().ValidateCancelTradeItem(sim, Player, Deal, ItemId);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<IDiplomacyService>().Single().CancelTradeItem(sim, Player, Deal, ItemId);
}
public sealed class DiplomaticDeclareWarCommand : GameCommand
{
    public int Target { get; init; }
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<IDiplomacyService>().Single().ValidateWar(sim, Player, Target);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<IDiplomacyService>().Single().DeclareWar(sim, Player, Target);
}
public sealed class DiplomaticMakePeaceCommand : GameCommand
{
    public int Target { get; init; }
    public override string? Validate(Simulation sim) => sim.Content.Services.Get<IDiplomacyService>().Single().ValidatePeace(sim, Player, Target);
    public override void Apply(Simulation sim) => sim.Content.Services.Get<IDiplomacyService>().Single().MakePeace(sim, Player, Target);
}
