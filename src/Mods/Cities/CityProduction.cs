using Civ.Content;
using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.Cities;

internal sealed class CityProduction(ICityYieldService yields, ICityGrowthService growth) : ICityProductionService
{
    public RequirementResult Validate(Simulation sim, int cityId, string kind, string content) => ProductionEligibility.Validate(sim, sim.State.CityById(cityId), kind, content);
    public CityProductionResult Calculate(Simulation sim, int cityId, string kind, string content)
    {
        var city = sim.State.CityById(cityId);
        var result = yields.Calculate(sim, cityId);
        if (kind == "economy:process") return ProcessProduction(result);
        var sink = CityData.Effects(sim, city);
        int modifier = (int)result.Ledger.Total("yield.percent.production");
        modifier += ItemModifiers(sim, city, kind, content, sink);
        int percent = Math.Max(0, 100 + modifier);
        var state = CityData.Read(sim, city);
        long production = (result.BaseYields.Production + state.Overflow + state.FeatureProduction) * percent / 100;
        if (kind == "cities:unit" && sim.Content.Get<S.UnitDef>(content).FoodProduction)
        {
            // Food-production excludes angry mouths, matching foodConsumption(true).
            long consumption = growth.Calculate(sim, cityId).Consumption;
            int angry = sim.Content.Services.Get<ICityWellbeingService>().Single().Calculate(sim, cityId).Angry;
            consumption -= angry * CityData.Constants(sim).FoodConsumptionPerPopulation;
            long food = Math.Max(0, result.Yields.Food - consumption);
            production += food; sink.Add(content, "cities:food_production", food);
        }
        if (state.Disorder) production = 0;
        sink.Add("cities:base", "cities:base_production", result.BaseYields.Production);
        sink.Add("cities:overflow", "cities:overflow", state.Overflow + state.FeatureProduction);
        sink.Add("cities:total", "cities:production_factor_percent", percent);
        sink.Add("cities:total", "cities:production", production);
        long cost = Cost(sim, city, kind, content);
        sink.Add(content, "cities:production_cost", cost);
        return new(cost, production, percent, sink.Ledger);
    }
    private static CityProductionResult ProcessProduction(CityYieldResult yields)
    {
        var lines = yields.Ledger.Lines.ToList();
        int percent = (int)Math.Max(0, 100 + yields.Ledger.Total("yield.percent.production"));
        lines.Add(new("cities:process", "cities:production", yields.Yields.Production));
        lines.Add(new("cities:process", "cities:production_factor_percent", percent));
        return new(0, yields.Yields.Production, percent, new(lines));
    }
    private static long Cost(Simulation sim, City city, string kind, string id)
    {
        bool unit = kind == "cities:unit";
        long cost = unit ? sim.Content.Get<S.UnitDef>(id).Cost : sim.Content.Get<S.BuildingDef>(id).Cost;
        var speed = CityData.Speed(sim);
        var era = sim.Content.Get<S.EraDef>(sim.Settings.StartingEra);
        cost = cost * (unit ? speed.TrainPercent : speed.ConstructPercent) / 100;
        cost = cost * (unit ? era.TrainPercent : era.ConstructPercent) / 100;
        if (sim.State.PlayerById(city.Owner).Ai is not null) cost = cost * (unit ? CityData.Handicap(sim).AiTrainPercent : CityData.Handicap(sim).AiConstructPercent) / 100;
        return Math.Max(1, cost);
    }
    private static int ItemModifiers(Simulation sim, City city, string kind, string id, ModifierSink sink)
    {
        var bonuses = BuildingEffects.Bonuses(sim, city);
        int modifier = (int)CityData.Effect(sink, "production_percent:" + id);
        if (kind == "cities:building")
        {
            var def = sim.Content.Get<S.BuildingDef>(id);
            modifier += bonuses.Keys.Sum(b => def.BonusProductionModifiers.GetValueOrDefault(b));
            foreach (var b in CityData.Active(sim, city)) modifier += b.BuildingClassProductionModifiers.GetValueOrDefault(def.DefaultBuilding);
            foreach (var other in CityData.Owned(sim, city.Owner))
                foreach (var b in CityData.Active(sim, other)) modifier += b.GlobalBuildingClassProductionModifiers.GetValueOrDefault(def.DefaultBuilding);
        }
        else modifier += UnitModifiers(sim, city, sim.Content.Get<S.UnitDef>(id), bonuses, sink);
        return modifier;
    }
    private static int UnitModifiers(Simulation sim, City city, S.UnitDef unit, IReadOnlyDictionary<string, int> bonuses, ModifierSink sink)
    {
        int modifier = unit.MilitaryProduction ? (int)CityData.Effect(sink, "military_production_percent") : 0;
        modifier += bonuses.Keys.Sum(b => unit.BonusProductionModifiers.GetValueOrDefault(b));
        foreach (var b in CityData.Active(sim, city))
        {
            modifier += b.UnitClassProductionModifiers.GetValueOrDefault(unit.DefaultUnit);
            if (unit.Domain is { } domain) modifier += b.DomainProductionModifiers.GetValueOrDefault(domain);
        }
        return modifier;
    }
    internal void Work(Simulation sim, City city)
    {
        var state = CityData.Edit(sim, city);
        state.PendingCompletion = null;
        if (state.Disorder || city.ProductionQueue.Count == 0) return;
        var order = city.ProductionQueue[0];
        if (order.Kind == "economy:process") return; // Conversion is settled as commerce; do not consume overflow.
        if (!Validate(sim, city.Id, order.Kind, order.ContentId).Success) return;
        var result = Calculate(sim, city.Id, order.Kind, order.ContentId);
        order = order with { Progress = order.Progress + result.Production };
        city.ProductionQueue[0] = order;
        state.Overflow = 0; state.FeatureProduction = 0;
        if (order.Progress < result.Cost) return;
        state.PendingCompletion = order;
        state.PendingCost = result.Cost;
        state.PendingProduction = result.Production;
        state.PendingModifierPercent = result.ModifierPercent;
    }
    internal void Complete(Simulation sim, City city)
    {
        var state = CityData.Edit(sim, city);
        if (state.PendingCompletion is not { } order) return;
        state.PendingCompletion = null;
        // Earlier city completions may consume a global/team/player class limit.
        if (!Validate(sim, city.Id, order.Kind, order.ContentId).Success) return;
        long excess = order.Progress - state.PendingCost;
        long cap = Math.Max(state.PendingCost, state.PendingProduction);
        state.Overflow = Math.Min(excess, cap) * 100 / Math.Max(1, state.PendingModifierPercent);
        state.PendingCost = 0; state.PendingProduction = 0; state.PendingModifierPercent = 0;
        city.ProductionQueue.RemoveAt(0);
        if (order.Kind == "cities:building")
        {
            city.Buildings.Add(order.ContentId);
            sim.Events.Publish(new BuildingCompleted(city.Id, order.ContentId));
        }
        else Spawn(sim, city, order.ContentId);
    }
    internal static void Spawn(Simulation sim, City city, string id)
    {
        var def = sim.Content.Get<S.UnitDef>(id);
        int experience = (int)CityData.Effect(CityData.Effects(sim, city), "experience");
        foreach (var b in CityData.Active(sim, city))
        {
            if (def.Domain is { } domain) experience += b.DomainFreeExperiences.GetValueOrDefault(domain);
            if (def.CombatType is { } combat) experience += b.UnitCombatFreeExperiences.GetValueOrDefault(combat);
        }
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = city.Owner, Position = city.Position,
            Type = id, Experience = Math.Max(0, experience), MovesLeft = def.Moves * CityData.Constants(sim).MoveDenominator };
        sim.State.AddUnit(unit);
        sim.Events.Publish(new UnitCreated(unit.Id, unit.Owner, unit.Type, unit.Position));
    }
}
