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
    /// <summary>Remembered by the other participant when a player stops an active trade.</summary>
    public const string StoppedTrading = "diplomacy:stopped_trading";
}

/// <summary>An attitude: its score, the tier the score falls in, and the ledger that explains it.</summary>
public sealed record AttitudeResult(int Score, Disposition Tier, Ledger Ledger);
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
public sealed record TradeItemCancelled(int DealId, int ItemId, int Player) : DomainEvent;

/// <summary>Attitudes, agreements and deals. The service also answers the base game's questions about agreements
/// (<see cref="ITeamAgreements"/>), dispositions (<see cref="IDispositions"/>, the attitude tier) and exchanges
/// (<see cref="IPlayerExchanges"/>, the deals' resources and gold). Queries are RNG-free and do not mutate state. Team
/// ids are not player ids.</summary>
public interface IDiplomacyService : ITeamAgreements, IDispositions, IPlayerExchanges
{
    AttitudeResult GetAttitude(Simulation sim, int fromPlayer, int towardPlayer);
    bool HasOpenBorders(Simulation sim, int teamA, int teamB);
    bool HasDefensivePact(Simulation sim, int teamA, int teamB);
    string? ValidateAgreement(Simulation sim, int player, int target, DiplomaticAgreement agreement);
    void SignAgreement(Simulation sim, int player, int target, DiplomaticAgreement agreement);
    string? ValidateCancelAgreement(Simulation sim, int player, int target, DiplomaticAgreement agreement);
    void CancelAgreement(Simulation sim, int player, int target, DiplomaticAgreement agreement);
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
    public override string? Validate(Simulation sim) => Diplomacy.Service(sim).ValidateProposal(sim, Player, Target, Items);
    public override void Apply(Simulation sim) => Diplomacy.Service(sim).ProposeDeal(sim, Player, Target, Items);
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
