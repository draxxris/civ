using Civ.Content;
using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.Peoples;

internal sealed class PeoplesService : IPeoplesService
{
    public S.LeaderDef? Leader(Simulation sim, int player) => sim.Content.TryGet<S.LeaderDef>(sim.State.PlayerById(player).Leader, out var def) ? def : null;
    public S.CivilizationDef? Civilization(Simulation sim, int player) => sim.Content.TryGet<S.CivilizationDef>(sim.State.PlayerById(player).Civilization, out var def) ? def : null;
    public IReadOnlyList<S.TraitDef> Traits(Simulation sim, int player) => Leader(sim, player)?.Traits.Where(t => t.Value)
        .OrderBy(t => t.Key, StringComparer.Ordinal).Select(t => sim.Content.Get<S.TraitDef>(t.Key)).ToArray() ?? [];
}

internal sealed class TraitEffects(IPeoplesService peoples) : IModifierSource
{
    public void Contribute(ModifierContext context, ModifierSink sink)
    {
        foreach (var trait in peoples.Traits(context.Sim, context.Player))
        {
            if (context.Scope == ModifierScope.City && context.City is { } city) City(context.Sim, city, trait, sink);
            if (context.Scope == ModifierScope.Player) Player(context.Sim, trait, sink);
            if (context.Scope == ModifierScope.Plot && context.Plot is { } tile) Plot(context.Sim, context.ProposedPlot ?? context.Sim.State.Plots[tile.Value], trait, sink);
            if (context.Scope == ModifierScope.Unit && context.Unit is { } unit) Unit(context.Sim, unit, trait, sink);
        }
    }
    private static void Player(Simulation sim, S.TraitDef t, ModifierSink sink)
    {
        sink.Add(t.Id, "economy:upkeep_percent", t.UpkeepModifier);
        sink.Add(t.Id, "military:general_rate_percent", t.GreatGeneralRateModifier);
        sink.Add(t.Id, "military:domestic_general_rate_percent", t.DomesticGreatGeneralRateModifier);
        if (t.MaximumAnarchyTurns >= 0)
            sink.Add(t.Id, "government:anarchy_cap_delta", t.MaximumAnarchyTurns - sim.Content.Get<S.RuleConstantsDef>().All.Single().Values.MaximumAnarchyTurns);
    }
    private static void City(Simulation sim, City city, S.TraitDef t, ModifierSink sink)
    {
        sink.AddYield(t.Id, t.YieldChanges);
        sink.AddCommerce(t.Id, t.CommerceChanges);
        sink.AddCommerce(t.Id, t.CommerceModifiers, true);
        sink.Add(t.Id, "cities:happiness", t.Happiness);
        sink.Add(t.Id, "cities:health", t.Health);
        sink.Add(t.Id, "cities:great_people_percent", t.GreatPeopleRateModifier);
        sink.Add(t.Id, "cities:world_wonder_production_percent", t.WorldWonderProductionPercent);
        sink.Add(t.Id, "cities:team_wonder_production_percent", t.TeamWonderProductionPercent);
        sink.Add(t.Id, "cities:national_wonder_production_percent", t.NationalWonderProductionPercent);
        sink.Add(t.Id, "cities:extra_culture_level", t.ExtraCultureLevel ? 1 : 0);
        // Production traits live on the produced definition, not on TraitDef.
        sink.Add(t.Id, "cities:trait:" + t.Id, 1);
        foreach (var id in city.Buildings.Order(StringComparer.Ordinal))
        {
            var b = sim.Content.Get<S.BuildingDef>(id);
            if (b.ObsoleteTech is not null && sim.KnowsTech(city.Owner, b.ObsoleteTech)) continue;
            if (b.RequiresActiveCivics && !sim.Content.Requirements.EvaluateKind(b.Requirements, new(sim, city.Owner, city), "civic").Success) continue;
            sink.Add(t.Id + ":" + id, "cities:happiness", b.TraitHappinessChanges.GetValueOrDefault(t.Id));
        }
        BonusHappiness(sim, city, t, sink);
    }
    private static void BonusHappiness(Simulation sim, City city, S.TraitDef t, ModifierSink sink)
    {
        if (t.BonusHappinessChanges.Count == 0) return;
        var network = sim.Content.Services.Get<IResourceNetwork>().Single();
        foreach (var pair in t.BonusHappinessChanges.OrderBy(p => p.Key, StringComparer.Ordinal))
            if ((sim.Content.Services.Get<IDiplomacyService>().SingleOrDefault()?.AvailableResourceCount(sim, city.Owner, city.Id, pair.Key)
                ?? network.ConnectedCount(sim, city.Owner, city.Id, pair.Key)) > 0) sink.Add(t.Id + ":" + pair.Key, "cities:happiness", pair.Value);
    }
private static void Plot(Simulation sim, Plot plot, S.TraitDef t, ModifierSink sink)
    {
        if (plot.Improvement is { } improvement)
            sink.AddYield(t.Id, sim.Content.Get<S.ImprovementDef>(improvement).TraitYieldChanges.GetValueOrDefault(t.Id));
        // Thresholds are tested after all flat plot contributions. World resolves the smallest positive threshold.
        sink.Add(t.Id, "world:extra_food_threshold", t.ExtraYieldThresholds.Food);
        sink.Add(t.Id, "world:extra_production_threshold", t.ExtraYieldThresholds.Production);
        sink.Add(t.Id, "world:extra_commerce_threshold", t.ExtraYieldThresholds.Commerce);
    }
    private static void Unit(Simulation sim, Unit unit, S.TraitDef t, ModifierSink sink)
    {
        sink.Add(t.Id, "military:level_experience_percent", t.ExperienceThresholdPercent);
        string combat = sim.Content.Get<S.UnitDef>(unit.Type).CombatType ?? "";
        foreach (var grant in t.CombatTypePromotions.OrderBy(p => p.Key, StringComparer.Ordinal))
            if (grant.Value.Contains(combat)) sink.Add(t.Id, "military:free_promotion:" + grant.Key, 1);
    }
}
