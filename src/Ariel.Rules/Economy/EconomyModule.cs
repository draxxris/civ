using Ariel.Content.Schema;
using Ariel.Modding;
using Ariel.Content;
using Ariel.Game;

namespace Ariel.Rules.Economy;

/// <summary>Commerce allocation, treasury, recurring costs, processes and hurry.</summary>
public sealed class EconomyModule : IPreparableMod
{
    private static readonly ModJsonFile<ProcessDef[]> Processes = new("data/process.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [Processes];

    public void Register(IModContext context)
    {
        ContentRegistration.Load(context, Processes);
        var commerce = new CityCommerce();
        var economy = new PlayerEconomy(commerce);
        var hurry = new CityHurry();
        context.RegisterService<IProductionKind>("process_kind", new ProcessKind(commerce));
        context.RegisterService<IPlayerEconomyService>("economy", economy);
        context.RegisterService<IPlayerResearchSource>("research_source", economy);
        context.RegisterService<ICityCultureSource>("culture", commerce);
        context.RegisterService<ICityProcessService>("processes", commerce);
        context.RegisterService<ICityHurryService>("hurry", hurry);
        context.RegisterService<IModifierSource>("hurry_anger", hurry);
        context.RegisterStateComponent<EconomyPlayerState>("player_state");
        context.RegisterStateComponent<EconomyCityState>("city_state");
        context.RegisterStateDomain("treasury", "Players/*/Treasury", "Players/*/Extensions/economy:player_state");
        context.RegisterStateDomain("hurry_anger", "Cities/*/Extensions/economy:city_state");
        context.RegisterCommand<SetCommerceRatesCommand>("set_commerce_rates");
        context.RegisterCommand<HurryCityCommand>("hurry");
        context.Subscribe<BuildingCompleted>("hurry_anger", CityHurry.BuildingCompleted);
        context.TurnSystems.Register(new EconomyPhase("gold", TurnPhase.Gold, economy.Settle, EconomyStateDomains.TreasuryOnly));
        context.TurnSystems.Register(new EconomyPhase("reconcile_gold", TurnPhase.Map + 2, economy.ReconcileGold));
        context.TurnSystems.Register(new EconomyPhase("inflation", TurnPhase.Economy, Inflation.Update, EconomyStateDomains.TreasuryOnly));
        context.TurnSystems.Register(new EconomyPhase("anger", TurnPhase.PlayerEnd, CityHurry.DecayAnger, EconomyStateDomains.HurryAngerOnly));
    }
}
