using Civ.Content;
using Civ.Game;
using Civ.Modding;

namespace Civ.Mods.Diplomacy;

public sealed class DiplomacyMod : IMod
{
    public void Register(IModContext context)
    {
        var service = new DiplomacyService();
        context.RegisterService<IDiplomacyService>("relations", service);
        context.RegisterStateComponent<DiplomacyState>("state");
        context.RegisterCommand<SignOpenBordersCommand>("open_borders");
        context.RegisterCommand<SignDefensivePactCommand>("defensive_pact");
        context.RegisterCommand<ProposeDealCommand>("propose_deal");
        context.RegisterCommand<ExecuteDealCommand>("execute_deal");
        context.RegisterCommand<CancelDealCommand>("cancel_deal");
        context.RegisterCommand<DiplomaticDeclareWarCommand>("declare_war");
        context.RegisterCommand<DiplomaticMakePeaceCommand>("make_peace");
        context.Subscribe<TurnStarted>("contact_turn", (sim, _) => service.DiscoverContacts(sim));
        context.Subscribe<CityFounded>("contact_city", (sim, _) => service.DiscoverContacts(sim));
        context.Subscribe<UnitCreated>("contact_unit", (sim, _) => service.DiscoverContacts(sim));
        context.TurnSystems.Register(new DiplomacyDeals(service));
        context.TurnSystems.Register(new DiplomacyHistory(service));
    }
}
internal sealed class DiplomacyDeals(DiplomacyService service) : ITurnSystem
{
    public string Id => "diplomacy:deals";
    public int Order => TurnPhase.Deals;
    public void Run(Simulation sim) => service.SettleDeals(sim);
}
internal sealed class DiplomacyHistory(DiplomacyService service) : ITurnSystem
{
    public string Id => "diplomacy:history";
    public int Order => TurnPhase.GameEnd;
    public void Run(Simulation sim)
    {
        service.DiscoverContacts(sim);
        service.UpdateHistory(sim);
    }
}
