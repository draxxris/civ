using Ariel.Content;

namespace Ariel.Game;

public enum AttitudeTier { Furious, Annoyed, Cautious, Pleased, Friendly }
public sealed record AttitudeResult(int Score, AttitudeTier Tier, Ledger Ledger);
public enum DiplomaticAgreement { OpenBorders, DefensivePact }
public enum TradeItemKind { Resource, GoldPerTurn, Gold, Technology }
/// <summary>An item given by one participant to the other. Amounts must be positive; technology amounts must be one.</summary>
public sealed record DiplomaticTradeItem(int FromPlayer, TradeItemKind Kind, string ContentId = "", long Amount = 1);
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
}
public sealed record TeamsMet(int FirstTeam, int SecondTeam) : DomainEvent;
public sealed record DiplomaticWarDeclared(int AggressorTeam, int DefenderTeam) : DomainEvent;
public sealed record DiplomaticPeaceSigned(int FirstTeam, int SecondTeam) : DomainEvent;

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
    string? ValidateWar(Simulation sim, int player, int target);
    void DeclareWar(Simulation sim, int player, int target);
    string? ValidatePeace(Simulation sim, int player, int target);
    void MakePeace(Simulation sim, int player, int target);
    string? ValidateProposal(Simulation sim, int player, int target, IReadOnlyList<DiplomaticTradeItem> items);
    int ProposeDeal(Simulation sim, int player, int target, IReadOnlyList<DiplomaticTradeItem> items);
    string? ValidateExecution(Simulation sim, int player, int deal);
    void ExecuteDeal(Simulation sim, int deal);
    string? ValidateCancellation(Simulation sim, int player, int deal);
    void CancelDeal(Simulation sim, int deal);
    IReadOnlyList<DiplomaticDeal> Deals(Simulation sim);
    /// <summary>Net imported minus exported copies. Physical resource counts remain with IResourceNetwork.</summary>
    long ResourceBalance(Simulation sim, int player, string resource);
    /// <summary>Physical connected copies plus diplomatic allocations, clamped to zero.</summary>
    long AvailableResourceCount(Simulation sim, int player, int city, string resource);
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
    public override void Apply(Simulation sim) => sim.Content.Services.Get<IDiplomacyService>().Single().CancelDeal(sim, Deal);
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
