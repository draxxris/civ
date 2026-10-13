using Civ.Content;
using Civ.Content.Schema;
using Civ.Game;
using UnitDef = Civ.Content.Schema.UnitDef;
using BuildingDef = Civ.Content.Schema.BuildingDef;

namespace Civ.Mods.Economy;

internal sealed class CityHurry : ICityHurryService, IModifierSource
{
    public HurryResult Calculate(Simulation sim, int cityId, string hurry)
    {
        var city = sim.State.CityById(cityId);
        var order = city.ProductionQueue[0];
        var production = sim.Content.Services.Get<ICityProductionService>().Single().Calculate(sim, cityId, order.Kind, order.ContentId);
        var def = sim.Content.Get<HurryDef>(hurry);
        long left = Math.Max(0, production.Cost - order.Progress);
        int itemModifier = order.Kind == "cities:unit" ? sim.Content.Get<UnitDef>(order.ContentId).HurryCostModifier
            : sim.Content.Get<BuildingDef>(order.ContentId).HurryCostModifier;
        long modifier = Math.Max(0, 100 + itemModifier);
        if (order.Progress == 0) modifier = modifier * Math.Max(0, 100 + EconomyData.Rules(sim).Values.NewHurryCostPercent) / 100;
        modifier = modifier * Math.Max(0, 100 + EconomyData.Effects(sim, city.Owner).Ledger.Total("economy:hurry_percent")) / 100;
        modifier = modifier * Math.Max(0, 100 + EconomyData.Civics(sim, city.Owner).Sum(c => c.HurryCostModifier)) / 100;
        modifier = Math.Max(1, modifier);
        long hurryCost = (left * modifier + 99) / 100;
        long gold = def.GoldPerProduction == 0 ? 0 : Math.Max(1, hurryCost * def.GoldPerProduction);
        int population = 0;
        long grant = left;
        if (def.ProductionPerPopulation > 0)
        {
            long extra = hurryCost * production.ModifierPercent / 100;
            long adjusted = extra == 0 ? hurryCost : (hurryCost * hurryCost + extra - 1) / extra;
            long perPopulation = Math.Max(1, def.ProductionPerPopulation * 100 / Math.Max(1, EconomyData.Speed(sim).HurryPercent));
            population = (int)Math.Max(1, (adjusted + perPopulation - 1) / perPopulation);
            grant = 100 * (population * perPopulation * production.ModifierPercent / 100) / modifier;
        }
        return new(gold, population, grant, new([
            new(hurry, "economy:hurry_factor_percent", modifier), new(hurry, "economy:hurry_cost", hurryCost),
            new(hurry, "economy:hurry_gold", gold), new(hurry, "economy:hurry_population", population),
            new(hurry, "economy:hurry_production", grant)]));
    }
    public string? Validate(Simulation sim, int player, int cityId, string hurry)
    {
        var city = sim.State.FindCity(cityId);
        if (city is null || city.Owner != player) return "Not your city";
        if (ValidateMethod(sim, player, hurry) is { } methodError) return methodError;
        if (ValidateProduction(sim, city) is { } productionError) return productionError;
        return ValidatePayment(sim, player, city, Calculate(sim, cityId, hurry));
    }
    private static string? ValidateMethod(Simulation sim, int player, string hurry)
    {
        if (!sim.Content.TryGet<HurryDef>(hurry, out _)) return "Unknown hurry method";
        return EconomyData.Civics(sim, player).Any(c => c.Hurrys.GetValueOrDefault(hurry)) ? null : "No active civic permits this hurry method";
    }
    private static string? ValidateProduction(Simulation sim, City city)
    {
        if (city.Extensions.Get<CityRuleState>(sim.Content)?.Disorder == true) return "City is in disorder";
        if (city.ProductionQueue.FirstOrDefault() is not { } order) return "No production to hurry";
        if (order.Kind == "economy:process") return "Processes cannot be hurried";
        var service = sim.Content.Services.Get<ICityProductionService>().Single();
        if (!service.Validate(sim, city.Id, order.Kind, order.ContentId).Success) return "Production requirements are not met";
        return order.Progress >= service.Calculate(sim, city.Id, order.Kind, order.ContentId).Cost ? "Production is already complete" : null;
    }
    private static string? ValidatePayment(Simulation sim, int player, City city, HurryResult result)
    {
        if (sim.State.PlayerById(player).Treasury < result.Gold) return "Not enough gold";
        if (result.Population > city.Population / 2) return "Not enough population";
        return null;
    }
    public void Hurry(Simulation sim, int cityId, string hurry)
    {
        var city = sim.State.CityById(cityId);
        var result = Calculate(sim, cityId, hurry);
        sim.State.PlayerById(city.Owner).Treasury -= result.Gold;
        city.Population -= result.Population;
        city.ProductionQueue[0] = city.ProductionQueue[0] with { Progress = city.ProductionQueue[0].Progress + result.Production };
        if (!sim.Content.Get<HurryDef>(hurry).Anger) return;
        var state = city.Extensions.Get<EconomyCityState>(sim.Content) ?? new();
        state.HurryAngerTurns += AngerLength(sim);
        city.Extensions.Set(sim.Content, state);
    }
    private static int AngerLength(Simulation sim) => Math.Max(1, EconomyData.Rules(sim).Values.HurryAngerDivisor * EconomyData.Speed(sim).HurryConscriptAngerPercent / 100);
    public void Contribute(ModifierContext context, ModifierSink sink)
    {
        if (context.Scope != ModifierScope.City || context.City is not { } city) return;
        int turns = city.Extensions.Get<EconomyCityState>(context.Sim.Content)?.HurryAngerTurns ?? 0;
        int length = AngerLength(context.Sim);
        int anger = (turns + length - 1) / length * EconomyData.Rules(context.Sim).Values.HurryPopulationAnger;
        sink.Add("economy:hurry", "cities:unhappiness", anger);
    }
    internal static void DecayAnger(Simulation sim, int player)
    {
        foreach (var city in EconomyData.Cities(sim, player))
        {
            var state = city.Extensions.Get<EconomyCityState>(sim.Content);
            if (state is not null) state.HurryAngerTurns = Math.Max(0, state.HurryAngerTurns - 1);
        }
    }
}
