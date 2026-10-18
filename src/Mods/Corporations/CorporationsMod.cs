using Civ.Content;
using Civ.Content.Schema;
using Civ.Game;
using Civ.Modding;

namespace Civ.Mods.Corporations;

public sealed class CorporationsMod : IPreparableMod
{
    private static readonly ModJsonFile<CorporationDef[]> Corporations = new("data/corporation.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [Corporations];

    public void Register(IModContext context)
    {
        ContentRegistration.Load(context, Corporations);
        var service = new CorporationService();
        context.RegisterService<ICorporationService>("corporations", service);
        context.RegisterService<IModifierSource>("effects", new CorporationEffects(service));
        context.RegisterStateComponent<CorporationCityState>("city_state");
        context.RegisterStateComponent<CorporationGameState>("game_state");
        context.RegisterStateComponent<CorporationPlayerState>("player_state");
        context.RegisterCommand<FoundCorporationCommand>("found_corporation");
        context.RegisterCommand<SpreadCorporationCommand>("spread_corporation");
        context.TurnSystems.Register(new CorporationMaintenance(service));
    }
}
internal sealed class CorporationEffects(ICorporationService service) : IModifierSource
{
    public void Contribute(ModifierContext context, ModifierSink sink)
    {
        if (context.Scope != ModifierScope.City || context.City is not { } city) return;
        var sim = context.Sim;
        foreach (var corporation in service.Present(sim, city.Id))
        {
            if (!service.IsActive(sim, city.Id, corporation)) continue;
            var def = sim.Content.Get<CorporationDef>(corporation);
            long copies = service.ConsumedBonusCount(sim, city.Id, corporation);
            sink.AddYield(def.Id, def.YieldProduced * copies);
            sink.AddCommerce(def.Id, def.CommerceProduced * copies);
            if (service.Headquarters(sim, corporation) == city.Id)
            {
                int branches = sim.State.Cities.Count(c => service.IsActive(sim, c.Id, corporation));
                sink.AddCommerce(def.Id + ":headquarters", def.HeadquartersCommerce * branches);
            }
        }
        long maintenance = service.MaintenanceHundredths(sim, city.Id);
        sink.Add("corporations:maintenance", "corporations:maintenance_hundredths", maintenance);
        sink.Add("corporations:maintenance", "cities:maintenance_flat_hundredths", maintenance);
    }
}
/// <summary>Fallback settlement when Economy is absent. Economy otherwise owns all city maintenance.</summary>
internal sealed class CorporationMaintenance(ICorporationService service) : IPlayerTurnSystem
{
    public string Id => "corporations:maintenance";
    public int Order => TurnPhase.Economy + 1;
    public void Run(Simulation sim, int player)
    {
        if (sim.Content.Services.Get<IPlayerEconomyService>().Any()) return;
        var owner = sim.State.PlayerById(player);
        long cost = sim.State.Cities.Where(c => c.Owner == player).Sum(c => service.MaintenanceHundredths(sim, c.Id));
        var state = owner.Extensions.Get<CorporationPlayerState>(sim.Content);
        if (cost == 0 && state is null) return;
        state ??= new(); owner.Extensions.Set(sim.Content, state);
        long total = state.MaintenanceRemainder + cost;
        long gold = total / 100; state.MaintenanceRemainder = total % 100;

        owner.Treasury = Math.Max(0, owner.Treasury - gold);
    }
}
