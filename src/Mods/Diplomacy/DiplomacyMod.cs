using Ariel.Content;
using Ariel.Ai;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Modding;
using Ariel.Rules;

namespace Ariel.Mods.Diplomacy;

/// <summary>Attitudes, agreements (open borders, defensive pacts) and deals between players, and the computer player's
/// use of them. Contact, war and peace and the memory ledger are the base game's; this mod answers the base game's
/// questions about agreements, dispositions and exchanges, and adds the memories deals leave.</summary>
public sealed class DiplomacyMod : IPreparableMod
{
    private static readonly ModJsonFile<MemoryKindDef[]> Memories = new("data/memory-kind.json");
    private static readonly ModJsonFile<CivicDiplomacyDef[]> Civics = new("data/civic-diplomacy.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [Memories, Civics];

    public void Register(IModContext context)
    {
        MemoryKinds.Load(context, Memories);
        ContentRegistration.Load(context, Civics);
        var service = new DiplomacyService();
        context.RegisterService<IDiplomacyService>("diplomacy", service);
        context.RegisterService<ITeamAgreements>("agreements", service);
        context.RegisterService<IDispositions>("dispositions", service);
        context.RegisterService<IPlayerExchanges>("exchanges", service);
        context.RegisterService<IAiPlanner>("planner", new DiplomacyAiPlanner());
        context.RegisterStateComponent<DiplomacyState>("state");
        context.RegisterCommand<SignOpenBordersCommand>("open_borders");
        context.RegisterCommand<SignDefensivePactCommand>("defensive_pact");
        context.RegisterCommand<CancelAgreementCommand>("cancel_agreement");
        context.RegisterCommand<ProposeDealCommand>("propose_deal");
        context.RegisterCommand<ExecuteDealCommand>("execute_deal");
        context.RegisterCommand<CancelDealCommand>("cancel_deal");
        context.RegisterCommand<CancelTradeItemCommand>("cancel_trade_item");
        context.Subscribe<WarDeclared>("war_ends_agreements", DiplomacyService.EndAgreements);
        context.TurnSystems.Register(new DiplomacyDeals(service));
        context.TurnSystems.Register(new DiplomacyHistory(service));
    }
}
internal sealed class DiplomacyDeals(DiplomacyService service) : ITurnSystem
{
    public string Id => "diplomacy:deals";
    // Player phases all run before Map, so settling here sees each player's Gold income first.
    public int Order => TurnPhase.Map + 1;
    public void Run(Simulation sim) => service.SettleDeals(sim);
}
/// <summary>Grows the religion and favourite-civic counters attitudes read. The base game has already met this
/// turn's contacts (<c>relations:turn</c>, before <see cref="TurnPhase.GameEnd"/>).</summary>
internal sealed class DiplomacyHistory(DiplomacyService service) : ITurnSystem
{
    public string Id => "diplomacy:history";
    public int Order => TurnPhase.GameEnd;
    public void Run(Simulation sim) => service.UpdateHistory(sim);
}
