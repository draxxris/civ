using Civ.Content.Schema;
using Civ.Modding;
using Civ.Content;
using Civ.Game;

namespace Civ.Mods.Economy;

/// <summary>Commerce allocation, treasury, recurring costs, processes and hurry.</summary>
public sealed class EconomyMod : IMod
{
    public void Register(IModContext context)
    {
        ContentRegistration.Load<ProcessDef>(context, "process.json");
        var commerce = new CityCommerce();
        var economy = new PlayerEconomy(commerce);
        var hurry = new CityHurry();
        context.RegisterService<IProductionKind>("process_kind", new ProcessKind(commerce));
        context.RegisterService<IPlayerEconomyService>("economy", economy);
        context.RegisterService<IPlayerResearchSource>("research_source", economy);
        context.RegisterService<IPlayerEspionageSource>("espionage_source", economy);
        context.RegisterService<ICityCultureSource>("culture", commerce);
        context.RegisterService<ICityProcessService>("processes", commerce);
        context.RegisterService<ICityHurryService>("hurry", hurry);
        context.RegisterService<IModifierSource>("hurry_anger", hurry);
        context.RegisterStateComponent<EconomyPlayerState>("player_state");
        context.RegisterStateComponent<EconomyCityState>("city_state");
        context.RegisterCommand<SetCommerceRatesCommand>("set_commerce_rates");
        context.RegisterCommand<HurryCityCommand>("hurry");
        context.Subscribe<BuildingCompleted>("hurry_anger", CityHurry.BuildingCompleted);
        context.TurnSystems.Register(new EconomyPhase("gold", TurnPhase.Gold, economy.Settle));
        context.TurnSystems.Register(new EconomyPhase("reconcile_gold", TurnPhase.Map + 2, economy.ReconcileGold));
        context.TurnSystems.Register(new EconomyPhase("inflation", TurnPhase.Economy, Inflation.Update));
        context.TurnSystems.Register(new EconomyPhase("anger", TurnPhase.PlayerEnd, CityHurry.DecayAnger));
        context.TurnSystems.Register(new EconomyPhase("espionage", TurnPhase.Espionage, (sim, player) =>
        {
            if (sim.Content.Services.Get<IEspionageService>().Any()) return;
            var state = EconomyData.Edit(sim, player);
            state.EspionageHundredths += economy.EspionageHundredths(sim, player);
        }));
    }
}
