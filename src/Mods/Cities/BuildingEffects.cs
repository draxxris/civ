using Civ.Content;
using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.Cities;

/// <summary>Building effects use the same Core modifier boundary as civic and trait effects.</summary>
internal sealed class BuildingEffects : IModifierSource
{
    public void Contribute(ModifierContext context, ModifierSink sink)
    {
        if (context.Scope == ModifierScope.Player)
        {
            foreach (var ownedCity in CityData.Owned(context.Sim, context.Player))
                foreach (var building in CityData.Active(context.Sim, ownedCity))
                    sink.Add(building.Id, "economy:hurry_percent", building.GlobalHurryModifier);
            return;
        }
        if (context.Scope != ModifierScope.City || context.City is not { } city) return;
        var sim = context.Sim;
        var bonuses = Bonuses(sim, city);
        foreach (var building in CityData.Active(sim, city)) Local(sim, city, building, bonuses, sink);
        foreach (var other in CityData.Owned(sim, city.Owner))
            foreach (var building in CityData.Active(sim, other)) Global(sim, city, other, building, sink);
        foreach (var id in city.Buildings.Order(StringComparer.Ordinal))
            sink.AddCommerce(id + ":safe", sim.Content.Get<S.BuildingDef>(id).ObsoleteSafeCommerceChanges);
        foreach (var bonus in bonuses.Keys.Order(StringComparer.Ordinal))
        {
            var def = sim.Content.Get<S.BonusDef>(bonus);
            sink.Add(bonus, "cities:happiness", def.Happiness);
            sink.Add(bonus, "cities:health", def.Health);
        }
    }
    internal static IReadOnlyDictionary<string, int> Bonuses(Simulation sim, City city)
    {
        var result = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var pair in Available(sim, city)) result[pair.Key] = pair.Value;
        foreach (var building in CityData.Active(sim, city))
        {
            if (building.FreeBonus is { } bonus) result[bonus] = result.GetValueOrDefault(bonus) + Math.Max(1, building.FreeBonusCount);
            foreach (var extra in building.ExtraFreeBonuses.OrderBy(p => p.Key, StringComparer.Ordinal)) result[extra.Key] = result.GetValueOrDefault(extra.Key) + extra.Value;
        }
        foreach (var building in CityData.Active(sim, city))
            if (building.NoBonus is { } bonus) result.Remove(bonus);
        return result;
    }
    /// <summary>Connected copies after diplomatic allocations. Only resources named in the owner's live deals need a second lookup.</summary>
    private static IReadOnlyDictionary<string, int> Available(Simulation sim, City city)
    {
        var network = sim.Content.Services.Get<IResourceNetwork>().Single();
        IReadOnlyDictionary<string, int> physical = sim.State.Plots.Any(p => p.Bonus is not null)
            ? network.ConnectedCounts(sim, city.Owner, city.Id) : new Dictionary<string, int>();
        var diplomacy = sim.Content.Services.Get<IDiplomacyService>().SingleOrDefault();
        if (diplomacy is null) return physical;
        var traded = TradedResources(sim, diplomacy, city.Owner);
        if (traded.Count == 0) return physical;
        var result = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var bonus in physical.Keys.Concat(traded).Distinct(StringComparer.Ordinal))
        {
            int count = (int)diplomacy.AvailableResourceCount(sim, city.Owner, city.Id, bonus);
            if (count > 0) result[bonus] = count;
        }
        return result;
    }
    private static HashSet<string> TradedResources(Simulation sim, IDiplomacyService diplomacy, int player) => diplomacy.Deals(sim)
        .Where(d => d.Status == DiplomaticDealStatus.Active && (d.Proposer == player || d.Recipient == player))
        .SelectMany(d => d.Items).Where(i => i.Kind == TradeItemKind.Resource).Select(i => i.ContentId).ToHashSet(StringComparer.Ordinal);
    private static void Local(Simulation sim, City city, S.BuildingDef b, IReadOnlyDictionary<string, int> bonuses, ModifierSink sink)
    {
        sink.AddYield(b.Id, b.YieldChanges); sink.AddYield(b.Id, b.YieldModifiers, true);
        sink.AddCommerce(b.Id, b.CommerceChanges); sink.AddCommerce(b.Id, b.CommerceModifiers, true);
        sink.Add(b.Id, "cities:hurry_anger_percent", b.HurryAngerModifier);
        AddNumbers(b, sink);
        AddMaps(sim, city, b, bonuses, sink);
        AddVicinity(sim, city, b, sink);
        foreach (var tech in sim.TeamOf(city.Owner).Techs)
        {
            sink.AddYield(b.Id + ":" + tech, b.TechYieldChanges.GetValueOrDefault(tech));
            sink.AddYield(b.Id + ":" + tech, b.TechYieldModifiers.GetValueOrDefault(tech), true);
            sink.AddCommerce(b.Id + ":" + tech, b.TechCommerceChanges.GetValueOrDefault(tech));
            if (b.TechSpecialistChanges.TryGetValue(tech, out var changes))
                foreach (var slot in changes.Slots.OrderBy(p => p.Key, StringComparer.Ordinal)) sink.Add(b.Id, "cities:slots:" + slot.Key, slot.Value);
        }
    }
    private static void AddNumbers(S.BuildingDef b, ModifierSink sink)
    {
        var values = new (string Key, int Value)[] {
            ("happiness", b.Happiness), ("health", b.Health), ("food_kept_percent", b.FoodKept),
            ("growth_percent", b.PopulationGrowthPercent), ("free_specialists", b.FreeSpecialist),
            ("maintenance_percent", b.MaintenanceModifier), ("distance_maintenance_percent", b.DistanceMaintenanceModifier),
            ("coastal_maintenance_percent", b.CoastalDistanceMaintenanceModifier), ("count_maintenance_percent", b.CityCountMaintenancePercent),
            ("great_people_percent", b.GreatPeopleRateModifier), ("experience", b.Experience),
            ("military_production_percent", b.MilitaryProductionModifier), ("no_unhappiness", b.NoUnhappiness ? 1 : 0),
            ("no_unhealthy_population", b.NoUnhealthyPopulation ? 1 : 0), ("building_only_healthy", b.BuildingOnlyHealthy ? 1 : 0) };
        foreach (var value in values) sink.Add(b.Id, "cities:" + value.Key, value.Value);
    }
    private static void AddMaps(Simulation sim, City city, S.BuildingDef b, IReadOnlyDictionary<string, int> bonuses, ModifierSink sink)
    {
        foreach (var bonus in bonuses.Keys.Order(StringComparer.Ordinal))
        {
            sink.AddYield(b.Id + ":" + bonus, b.BonusYieldChanges.GetValueOrDefault(bonus));
            sink.AddYield(b.Id + ":" + bonus, b.BonusYieldModifiers.GetValueOrDefault(bonus), true);
            sink.Add(b.Id, "cities:happiness", b.BonusHappinessChanges.GetValueOrDefault(bonus));
            sink.Add(b.Id, "cities:health", b.BonusHealthChanges.GetValueOrDefault(bonus));
        }
        foreach (var slot in b.SpecialistSlots.OrderBy(p => p.Key, StringComparer.Ordinal)) sink.Add(b.Id, "cities:slots:" + slot.Key, slot.Value);
        foreach (var free in b.FreeSpecialistCounts.OrderBy(p => p.Key, StringComparer.Ordinal)) sink.Add(b.Id, "cities:free:" + free.Key, free.Value);
        foreach (var other in CityData.Active(sim, city)) sink.Add(b.Id, "cities:happiness", b.BuildingHappinessChanges.GetValueOrDefault(other.DefaultBuilding));
    }
    private static void AddVicinity(Simulation sim, City city, S.BuildingDef b, ModifierSink sink)
    {
        if (b.VicinityBonusYieldChanges.Count == 0) return;
        var present = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var tile in sim.Content.Services.Get<ICityWorkService>().Single().WorkArea(sim, city.Id))
        {
            var plot = sim.State.Plots[tile.Value];
            if (plot.Owner != city.Owner || plot.Bonus is not { } id) continue;
            if (sim.Content.Get<S.BonusDef>(id).RevealTech is { } tech && !sim.KnowsTech(city.Owner, tech)) continue;
            present.Add(id);
        }
        foreach (var id in present)
            if (b.VicinityBonusYieldChanges.TryGetValue(id, out var delta)) sink.AddYield(b.Id + ":vicinity:" + id, delta);
    }
    private static void Global(Simulation sim, City city, City origin, S.BuildingDef b, ModifierSink sink)
    {
        sink.AddYield(b.Id + ":global", b.GlobalYieldModifiers, true);
        sink.AddCommerce(b.Id + ":global", b.GlobalCommerceModifiers, true);
        sink.Add(b.Id, "cities:happiness", b.GlobalHappiness); sink.Add(b.Id, "cities:health", b.GlobalHealth);
        sink.Add(b.Id, "cities:free_specialists", b.GlobalFreeSpecialist);
        sink.Add(b.Id, "cities:great_people_percent", b.GlobalGreatPeopleRateModifier);
        sink.Add(b.Id, "cities:maintenance_percent", b.GlobalMaintenanceModifier);
        sink.Add(b.Id, "cities:experience", b.GlobalExperience);
        foreach (var local in CityData.Active(sim, city)) sink.AddCommerce(b.Id, b.GlobalBuildingExtraCommerces.GetValueOrDefault(local.DefaultBuilding));
        if (sim.State.PlotAt(city.Position).AreaId != sim.State.PlotAt(origin.Position).AreaId) return;
        sink.Add(b.Id, "cities:happiness", b.AreaHappiness); sink.Add(b.Id, "cities:health", b.AreaHealth);
        sink.Add(b.Id, "cities:free_specialists", b.AreaFreeSpecialist);
        sink.Add(b.Id, "cities:maintenance_percent", b.AreaMaintenanceModifier);
    }
}
