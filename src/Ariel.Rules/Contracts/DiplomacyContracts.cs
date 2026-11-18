using Ariel.Content;

namespace Ariel.Game;

/// <summary>Memory kinds the Diplomacy module records (<c>data/memory-kind.json</c>). Contact, war and peace, and the
/// memories they leave, are the base game's (<see cref="IRelationsService"/>, <see cref="BaseMemories"/>).</summary>
public static class DiplomacyMemories
{
    /// <summary>Remembered by the recipient for each technology a deal gave it.</summary>
    public const string TradedTechToUs = "diplomacy:traded_tech_to_us";
    /// <summary>Remembered by the recipient of a deal that gave without taking, up to the kind's limit.</summary>
    public const string GiveHelp = "diplomacy:give_help";
    /// <summary>Remembered by the other participant when a player stops an active trade, and by every member of a team
    /// an embargo is laid against toward the team that lays it.</summary>
    public const string StoppedTrading = "diplomacy:stopped_trading";
    /// <summary>Remembered by the members of a team an embargo is laid against toward the player who asked for it.</summary>
    public const string HiredTradeEmbargo = "diplomacy:hired_trade_embargo";
    /// <summary>Remembered by a player who asked for an embargo toward the player who laid it.</summary>
    public const string AcceptedStopTrading = "diplomacy:accepted_stop_trading";
    /// <summary>Remembered by a player who asked for an embargo toward the player who refused it.</summary>
    public const string DeniedStopTrading = "diplomacy:denied_stop_trading";
    /// <summary>Remembered by a player who made a demand toward the player who gave in to it.</summary>
    public const string AcceptDemand = "diplomacy:accept_demand";
    /// <summary>Remembered by a player who made a demand toward the player who refused it.</summary>
    public const string RejectedDemand = "diplomacy:rejected_demand";
    /// <summary>Remembered by a player toward whoever made a demand of it, given in to or not.</summary>
    public const string MadeDemand = "diplomacy:made_demand";
    /// <summary>Remembered by a player who asked for help toward the player who refused it.</summary>
    public const string RefusedHelp = "diplomacy:refused_help";
}

/// <summary>An attitude: its score, the tier the score falls in, and the ledger that explains it.</summary>
public sealed record AttitudeResult(int Score, Disposition Tier, Ledger Ledger);
public enum DiplomaticAgreement { OpenBorders, DefensivePact }
/// <summary>What a deal item is. <see cref="Resource"/>, <see cref="GoldPerTurn"/> and <see cref="Embargo"/> run for as
/// long as the deal stays active; the rest happen once, when the deal is accepted.
/// <para><see cref="PeaceTreaty"/>, <see cref="OpenBorders"/> and <see cref="DefensivePact"/> bind both participants,
/// whichever of them the item names as giver. A deal between teams at war must hold a peace treaty, and one holding a
/// peace treaty cannot also sign an agreement.</para></summary>
public enum TradeItemKind { Resource, GoldPerTurn, Gold, Technology, Embargo, PeaceTreaty, OpenBorders, DefensivePact }
/// <summary>An item given by one participant to the other. Amounts must be positive; technology and treaty amounts must
/// be one. <paramref name="Against"/> names, for an <see cref="TradeItemKind.Embargo"/> only, a player of the team the
/// giver's team stops trading with; it is -1 for every other kind. Canonical Id distinguishes individual trade items
/// within a deal.</summary>
public sealed record DiplomaticTradeItem(int FromPlayer, TradeItemKind Kind, string ContentId = "", long Amount = 1, int Id = 0, int Against = -1)
{
    /// <summary>Whether the item binds both participants rather than passing from one to the other.</summary>
    public bool Mutual => Kind is TradeItemKind.PeaceTreaty or TradeItemKind.OpenBorders or TradeItemKind.DefensivePact;
}
public enum DiplomaticDealStatus { Proposed, Active, Completed, Cancelled }
/// <summary>Why a deal is proposed, which decides what its answer leaves in memory. A <see cref="Demand"/> (tribute)
/// and a <see cref="Request"/> (for help) give the proposer something for nothing: every item comes from the
/// recipient. A <see cref="Trade"/> is any other deal, a gift included.</summary>
public enum DealIntent { Trade, Demand, Request }
public sealed class DiplomaticDeal
{
    public int Id { get; set; }
    public int Proposer { get; set; }
    public int Recipient { get; set; }
    /// <summary>The turn the deal was proposed.</summary>
    public int ProposedTurn { get; set; }
    public int StartedTurn { get; set; }
    public int LastSettledTurn { get; set; }
    public DiplomaticDealStatus Status { get; set; }
    public List<DiplomaticTradeItem> Items { get; set; } = [];
    public DealIntent Intent { get; set; }
    /// <summary>The deal this one answers with other terms, or null when it was proposed on its own.</summary>
    public int? CounterTo { get; set; }

    public DiplomaticTradeItem? FindItem(int itemId) => Items.FirstOrDefault(i => i.Id == itemId);
    public bool RemoveItem(int itemId) => Items.RemoveAll(i => i.Id == itemId) > 0;
}
public sealed record TradeItemCancelled(int DealId, int ItemId, int Player) : DomainEvent;

/// <summary>Attitudes, agreements (open borders, defensive pacts) and deals between players: the Diplomacy module,
/// read through <c>GameRules.Diplomacy</c>. Relations asks it which teams an agreement opens borders to or pledges to
/// each other, how players regard each other and which teams an embargo keeps apart; Cities and Economy ask it what
/// running deals exchange. Queries are RNG-free and do not mutate state. Team ids are not player ids.</summary>
public interface IDiplomacyService
{
    AttitudeResult GetAttitude(Simulation sim, int fromPlayer, int towardPlayer);
    /// <summary>Whether the teams are one team, or at peace with open borders signed.</summary>
    bool HasOpenBorders(Simulation sim, int teamA, int teamB);
    /// <summary>Whether two different teams at peace have pledged to defend each other.</summary>
    bool HasDefensivePact(Simulation sim, int teamA, int teamB);
    /// <summary>Called once when <paramref name="aggressor"/> declares war on <paramref name="defender"/>, before the war
    /// starts: settles the agreements the declaration breaks and returns, in order, the teams obliged to join the
    /// defender.</summary>
    IReadOnlyList<int> CallToArms(Simulation sim, int aggressor, int defender);
    string? ValidateAgreement(Simulation sim, int player, int target, DiplomaticAgreement agreement);
    void SignAgreement(Simulation sim, int player, int target, DiplomaticAgreement agreement);
    string? ValidateCancelAgreement(Simulation sim, int player, int target, DiplomaticAgreement agreement);
    void CancelAgreement(Simulation sim, int player, int target, DiplomaticAgreement agreement);
    string? ValidateProposal(Simulation sim, int player, int target, IReadOnlyList<DiplomaticTradeItem> items,
        DealIntent intent = DealIntent.Trade, int? counterTo = null);
    int ProposeDeal(Simulation sim, int player, int target, IReadOnlyList<DiplomaticTradeItem> items,
        DealIntent intent = DealIntent.Trade, int? counterTo = null);
    string? ValidateExecution(Simulation sim, int player, int deal);
    void ExecuteDeal(Simulation sim, int deal);
    string? ValidateCancellation(Simulation sim, int player, int deal);
    void CancelDeal(Simulation sim, int player, int deal);
    string? ValidateCancelTradeItem(Simulation sim, int player, int dealId, int itemId);
    void CancelTradeItem(Simulation sim, int player, int dealId, int itemId);
    IReadOnlyList<DiplomaticDeal> Deals(Simulation sim);
    /// <summary>Whether a live deal has the first team embargo the second, in either direction.</summary>
    bool HasEmbargo(Simulation sim, int teamA, int teamB);
    /// <summary>Net imported minus exported copies. Physical resource counts remain with IResourceNetwork.</summary>
    long ResourceBalance(Simulation sim, int player, string resource);
    /// <summary>Resources named in the player's running deals, in either direction. Counts of any other resource are
    /// the connected copies alone.</summary>
    IReadOnlyCollection<string> ExchangedResources(Simulation sim, int player);
    /// <summary>Copies the city can use: its connected copies less what exports take from its group, plus imports when
    /// it is linked to the capital. Never below zero.</summary>
    long AvailableResourceCount(Simulation sim, int player, int city, string resource);
    /// <summary>The same answer for several resources, reusing one connectivity query.</summary>
    IReadOnlyDictionary<string, long> AvailableResourceCounts(Simulation sim, int player, int city, IReadOnlyCollection<string> resources);
    /// <summary>Gold per turn the player's live deals promise it and ask of it; the economy books it as
    /// <c>economy:deal_income</c> and <c>economy:deal_expenses</c>, and the deals settle it after every Gold phase.</summary>
    (long Income, long Expenses) GoldPerTurn(Simulation sim, int player);
}

/// <summary>What a deal is worth to the leader it is offered to, the least that leader asks at its attitude toward the
/// proposer, whether it would accept, and why it would refuse the deal whatever else it holds (an item its personality
/// will not give at this attitude), or null.</summary>
public sealed record DealAppraisal(long Value, long Margin, bool Accepts, string? Refusal = null);

/// <summary>How a computer leader would answer a deal proposed to it, answered by the computer player
/// (<c>GameRules.DealAppraiser</c>), which owns that judgement. The diplomacy screen shows it before a proposal is
/// sent; without an answer the screen makes no prediction. Never changes state or uses the RNG.</summary>
public interface IDealAppraiser
{
    DealAppraisal Appraise(Simulation sim, int leader, int proposer, IReadOnlyList<DiplomaticTradeItem> items, DealIntent intent);
}

public sealed class SignOpenBordersCommand : GameCommand
{
    public int Target { get; init; }
    public override string? Validate(Simulation sim) => sim.Rules().Diplomacy.ValidateAgreement(sim, Player, Target, DiplomaticAgreement.OpenBorders);
    public override void Apply(Simulation sim) => sim.Rules().Diplomacy.SignAgreement(sim, Player, Target, DiplomaticAgreement.OpenBorders);
}
public sealed class SignDefensivePactCommand : GameCommand
{
    public int Target { get; init; }
    public override string? Validate(Simulation sim) => sim.Rules().Diplomacy.ValidateAgreement(sim, Player, Target, DiplomaticAgreement.DefensivePact);
    public override void Apply(Simulation sim) => sim.Rules().Diplomacy.SignAgreement(sim, Player, Target, DiplomaticAgreement.DefensivePact);
}
public sealed class CancelAgreementCommand : GameCommand
{
    public int Target { get; init; }
    public DiplomaticAgreement Agreement { get; init; }
    public override string? Validate(Simulation sim) => sim.Rules().Diplomacy.ValidateCancelAgreement(sim, Player, Target, Agreement);
    public override void Apply(Simulation sim) => sim.Rules().Diplomacy.CancelAgreement(sim, Player, Target, Agreement);
}
public sealed class ProposeDealCommand : GameCommand
{
    public int Target { get; init; }
    public List<DiplomaticTradeItem> Items { get; init; } = [];
    public DealIntent Intent { get; init; }
    /// <summary>The proposal to the proposer that this one answers with other terms; null for a fresh proposal.</summary>
    public int? CounterTo { get; init; }
    public override string? Validate(Simulation sim) => sim.Rules().Diplomacy.ValidateProposal(sim, Player, Target, Items, Intent, CounterTo);
    public override void Apply(Simulation sim) => sim.Rules().Diplomacy.ProposeDeal(sim, Player, Target, Items, Intent, CounterTo);
}
/// <summary>Only the recipient can accept a proposal. Execution revalidates its assets.</summary>
public sealed class ExecuteDealCommand : GameCommand
{
    public int Deal { get; init; }
    public override string? Validate(Simulation sim) => sim.Rules().Diplomacy.ValidateExecution(sim, Player, Deal);
    public override void Apply(Simulation sim) => sim.Rules().Diplomacy.ExecuteDeal(sim, Deal);
}
public sealed class CancelDealCommand : GameCommand
{
    public int Deal { get; init; }
    public override string? Validate(Simulation sim) => sim.Rules().Diplomacy.ValidateCancellation(sim, Player, Deal);
    public override void Apply(Simulation sim) => sim.Rules().Diplomacy.CancelDeal(sim, Player, Deal);
}
public sealed class CancelTradeItemCommand : GameCommand
{
    public int Deal { get; init; }
    public int ItemId { get; init; }
    public override string? Validate(Simulation sim) => sim.Rules().Diplomacy.ValidateCancelTradeItem(sim, Player, Deal, ItemId);
    public override void Apply(Simulation sim) => sim.Rules().Diplomacy.CancelTradeItem(sim, Player, Deal, ItemId);
}

/// <summary>Agreements, deals and the counters attitudes grow with. Contact, war and peace, and memories are the base
/// game's (<see cref="RelationsState"/>). No derived caches or RNG are stored here.</summary>
public sealed class DiplomacyState
{
    /// <summary>Agreements by <see cref="TeamPair.Key"/>. A war between the teams ends them.</summary>
    public SortedDictionary<string, TeamAgreement> Agreements { get; set; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, AttitudeHistory> Histories { get; set; } = new(StringComparer.Ordinal);
    public List<DiplomaticDeal> Deals { get; set; } = [];
    public int NextDealId { get; set; } = 1;
    public int LastHistoryTurn { get; set; }

    public TeamAgreement? GetAgreement(int teamA, int teamB) => Agreements.GetValueOrDefault(TeamPair.Of(teamA, teamB).Key);
    public TeamAgreement GetOrCreateAgreement(int teamA, int teamB)
    {
        string key = TeamPair.Of(teamA, teamB).Key;
        if (!Agreements.TryGetValue(key, out var agreement)) Agreements[key] = agreement = new();
        return agreement;
    }

    public AttitudeHistory? GetHistory(int fromPlayer, int towardPlayer) => Histories.GetValueOrDefault(PlayerDirection.Of(fromPlayer, towardPlayer).Key);
    public AttitudeHistory GetOrCreateHistory(int fromPlayer, int towardPlayer)
    {
        string key = PlayerDirection.Of(fromPlayer, towardPlayer).Key;
        if (!Histories.TryGetValue(key, out var history)) Histories[key] = history = new();
        return history;
    }
}

/// <summary>The agreements two teams have signed, each with the turn it began, or null when not in force.</summary>
public sealed class TeamAgreement
{
    public int? OpenBordersStarted { get; set; }
    public int? DefensivePactStarted { get; set; }

    public int? Started(DiplomaticAgreement agreement) =>
        agreement == DiplomaticAgreement.OpenBorders ? OpenBordersStarted : DefensivePactStarted;

    public void Sign(DiplomaticAgreement agreement, int turn)
    {
        if (agreement == DiplomaticAgreement.OpenBorders) OpenBordersStarted = turn;
        else if (agreement == DiplomaticAgreement.DefensivePact) DefensivePactStarted = turn;
    }

    public void Cancel(DiplomaticAgreement agreement)
    {
        if (agreement == DiplomaticAgreement.OpenBorders) OpenBordersStarted = null;
        else if (agreement == DiplomaticAgreement.DefensivePact) DefensivePactStarted = null;
    }
}

/// <summary>How long one player has shared a religion or a favourite civic with another.</summary>
public sealed class AttitudeHistory
{
    public int SameReligionTurns { get; set; }
    public int DifferentReligionTurns { get; set; }
    public int FavoriteCivicTurns { get; set; }
}
