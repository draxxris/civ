using Ariel.Content;
using Ariel.Game;

namespace Ariel.Mods.Diplomacy;

/// <summary>Memory kinds the Diplomacy mod records (<c>data/memory-kind.json</c>). Contact, war and peace, and the
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

/// <summary>Attitudes, agreements and deals. The service also answers the base game's questions about agreements
/// (<see cref="ITeamAgreements"/>), dispositions (<see cref="IDispositions"/>, the attitude tier), exchanges
/// (<see cref="IPlayerExchanges"/>, the deals' resources and gold) and trade terms (<see cref="ITradeTerms"/>, the
/// embargoes deals lay). Queries are RNG-free and do not mutate state. Team
/// ids are not player ids.</summary>
public interface IDiplomacyService : ITeamAgreements, IDispositions, IPlayerExchanges, ITradeTerms
{
    AttitudeResult GetAttitude(Simulation sim, int fromPlayer, int towardPlayer);
    bool HasOpenBorders(Simulation sim, int teamA, int teamB);
    bool HasDefensivePact(Simulation sim, int teamA, int teamB);
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
}

/// <summary>Commands reach the service through the registry, so they only run where the Diplomacy mod is loaded.</summary>
internal static class Diplomacy
{
    internal static IDiplomacyService Service(Simulation sim) => sim.Service<IDiplomacyService>()
        ?? throw new InvalidOperationException("the Diplomacy mod is not loaded");
}

public sealed class SignOpenBordersCommand : GameCommand
{
    public int Target { get; init; }
    public override string? Validate(Simulation sim) => Diplomacy.Service(sim).ValidateAgreement(sim, Player, Target, DiplomaticAgreement.OpenBorders);
    public override void Apply(Simulation sim) => Diplomacy.Service(sim).SignAgreement(sim, Player, Target, DiplomaticAgreement.OpenBorders);
}
public sealed class SignDefensivePactCommand : GameCommand
{
    public int Target { get; init; }
    public override string? Validate(Simulation sim) => Diplomacy.Service(sim).ValidateAgreement(sim, Player, Target, DiplomaticAgreement.DefensivePact);
    public override void Apply(Simulation sim) => Diplomacy.Service(sim).SignAgreement(sim, Player, Target, DiplomaticAgreement.DefensivePact);
}
public sealed class CancelAgreementCommand : GameCommand
{
    public int Target { get; init; }
    public DiplomaticAgreement Agreement { get; init; }
    public override string? Validate(Simulation sim) => Diplomacy.Service(sim).ValidateCancelAgreement(sim, Player, Target, Agreement);
    public override void Apply(Simulation sim) => Diplomacy.Service(sim).CancelAgreement(sim, Player, Target, Agreement);
}
public sealed class ProposeDealCommand : GameCommand
{
    public int Target { get; init; }
    public List<DiplomaticTradeItem> Items { get; init; } = [];
    public DealIntent Intent { get; init; }
    /// <summary>The proposal to the proposer that this one answers with other terms; null for a fresh proposal.</summary>
    public int? CounterTo { get; init; }
    public override string? Validate(Simulation sim) => Diplomacy.Service(sim).ValidateProposal(sim, Player, Target, Items, Intent, CounterTo);
    public override void Apply(Simulation sim) => Diplomacy.Service(sim).ProposeDeal(sim, Player, Target, Items, Intent, CounterTo);
}
/// <summary>Only the recipient can accept a proposal. Execution revalidates its assets.</summary>
public sealed class ExecuteDealCommand : GameCommand
{
    public int Deal { get; init; }
    public override string? Validate(Simulation sim) => Diplomacy.Service(sim).ValidateExecution(sim, Player, Deal);
    public override void Apply(Simulation sim) => Diplomacy.Service(sim).ExecuteDeal(sim, Deal);
}
public sealed class CancelDealCommand : GameCommand
{
    public int Deal { get; init; }
    public override string? Validate(Simulation sim) => Diplomacy.Service(sim).ValidateCancellation(sim, Player, Deal);
    public override void Apply(Simulation sim) => Diplomacy.Service(sim).CancelDeal(sim, Player, Deal);
}
public sealed class CancelTradeItemCommand : GameCommand
{
    public int Deal { get; init; }
    public int ItemId { get; init; }
    public override string? Validate(Simulation sim) => Diplomacy.Service(sim).ValidateCancelTradeItem(sim, Player, Deal, ItemId);
    public override void Apply(Simulation sim) => Diplomacy.Service(sim).CancelTradeItem(sim, Player, Deal, ItemId);
}
