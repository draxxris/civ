using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Modding;

namespace Ariel.Mods.Corporations;

public sealed class CorporationsMod : IPreparableMod
{
    private static readonly ModJsonFile<CorporationDef[]> Corporations = new("data/corporation.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [Corporations];

    public void Register(IModContext context)
    {
        var definitions = Corporations.Read(context.Data);
        foreach (var definition in definitions) context.Content<CorporationDef>().Register(definition);
        // The schema covers reference resolution and sign-free fields; these invariants are rules-level.
        CorporationContentValidation.Validate(definitions);
        var service = new CorporationService();
        context.RegisterService<ICorporationService>("corporations", service);
        context.RegisterService<IModifierSource>("effects", new CorporationEffects(service));
        context.RegisterStateComponent<CorporationCityState>("city_state");
        context.RegisterStateComponent<CorporationGameState>("game_state");
        context.RegisterStateComponent<CorporationPlayerState>("player_state");
        context.RegisterService<IMission>("found", new FoundCorporationMission(service));
        context.RegisterService<IMission>("spread", new SpreadCorporationMission(service));
        context.RegisterCommand<SpreadCorporationCommand>("spread_corporation");
        context.TurnSystems.Register(new CorporationMaintenance(service));
        context.Subscribe<CityRazed>("corporations:dissolve", (sim, razed) => Dissolve(sim, razed.City, razed.Player));
    }

    /// <summary>A razed headquarters dissolves the corporation: its branches leave every city and it may be founded again.</summary>
    private static void Dissolve(Simulation sim, int city, int player)
    {
        var state = sim.State.Extensions.Get<CorporationGameState>(sim.Content);
        if (state is null) return;
        foreach (var corporation in state.Founded.Where(pair => pair.Value.Headquarters == city).Select(pair => pair.Key).ToArray())
        {
            foreach (var other in sim.State.Cities)
            {
                var branches = other.Extensions.Get<CorporationCityState>(sim.Content);
                branches?.Corporations.Remove(corporation);
            }
            state.Founded.Remove(corporation);
            sim.Events.Publish(new CorporationDissolved(corporation, city, player));
        }
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
        // One key: the city maintenance service folds flat costs into its own total. A second corporate key
        // would be skipped there as the same source and would suggest a settlement that never happens.
        sink.Add("corporations:maintenance", "cities:maintenance_flat_hundredths", service.MaintenanceHundredths(sim, city.Id));
    }
}
/// <summary>Fallback settlement when Economy is absent. Economy otherwise owns all city maintenance.</summary>
internal sealed class CorporationMaintenance(ICorporationService service) : IPlayerTurnSystem
{
    public string Id => "corporations:maintenance";
    public int Order => TurnPhase.Economy + 1;
    public void Run(Simulation sim, int player)
    {
        var owner = sim.State.PlayerById(player);
        var state = owner.Extensions.Get<CorporationPlayerState>(sim.Content);
        if (sim.Content.Services.Get<IPlayerEconomyService>().Any())
        {
            // Economy settles city maintenance without a remainder; one carried over from a save made without it
            // would otherwise resurface if Economy were removed again.
            if (state is not null) state.MaintenanceRemainder = 0;
            return;
        }
        long cost = sim.State.Cities.Where(c => c.Owner == player).Sum(c => service.MaintenanceHundredths(sim, c.Id));
        // MaintenanceHundredths is stripped of inflation for the economy's aggregate pass; nobody re-inflates it
        // here, so the fallback must, or a corporation would cost less without Economy loaded.
        cost = cost * (CorporationService.InflationPercent(sim, player) + 100) / 100;
        if (cost == 0 && state is null) return;
        state ??= new(); owner.Extensions.Set(sim.Content, state);
        long total = state.MaintenanceRemainder + cost;
        long gold = total / 100; state.MaintenanceRemainder = total % 100;

        owner.Treasury = Math.Max(0, owner.Treasury - gold);
    }
}
