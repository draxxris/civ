using Ariel.Content;
using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Rules.Cities;

/// <summary>Building effects use the same Core modifier boundary as civic and trait effects.
/// <para>Every city calculation collects these for each of the city's buildings and each building of the owner's
/// other cities, so a source name made by concatenation is built only when the line it names is not zero: the sink
/// drops zero amounts, and most buildings have nothing for most techs, resources and global effects.</para></summary>
internal sealed class BuildingEffects : IModifierSource
{
    public void Contribute(ModifierContext context, ModifierSink sink) =>
        CityReadScope.Run(context.Sim, _ => { ContributeScoped(context, sink); return true; });
    private static void ContributeScoped(ModifierContext context, ModifierSink sink)
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
        var running = CityData.Active(sim, city).ToArray();
        foreach (var building in running) Local(sim, city, building, running, bonuses, sink);
        foreach (var other in CityData.Owned(sim, city.Owner))
            foreach (var building in CityData.Active(sim, other)) Global(sim, city, other, building, running, sink);
        foreach (var building in CityData.EffectiveBuildings(sim, city))
            if (building.ObsoleteSafeCommerceChanges != default)
                sink.AddCommerce(building.Id + ":safe", building.ObsoleteSafeCommerceChanges);
        foreach (var bonus in bonuses.Keys.Order(StringComparer.Ordinal))
        {
            var def = sim.Content.Get<S.BonusDef>(bonus);
            sink.Add(bonus, "cities:happiness", def.Happiness);
            sink.Add(bonus, HealthKeys.Bonus, def.Health);
        }
    }
    internal static IReadOnlyDictionary<string, int> Bonuses(Simulation sim, City city) =>
        Bonuses(sim, city, CityData.Active(sim, city).ToArray());
    internal static IReadOnlyDictionary<string, int> Bonuses(Simulation sim, City city, IReadOnlyList<S.BuildingDef> buildings)
    {
        var result = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var pair in Available(sim, city)) result[pair.Key] = pair.Value;
        foreach (var building in buildings)
        {
            foreach (var bonus in building.ExtraFreeBonuses.Keys.Append(building.FreeBonus).OfType<string>().Distinct(StringComparer.Ordinal))
            {
                int count = FreeBonusCount(sim, building, bonus);
                if (count > 0) result[bonus] = result.GetValueOrDefault(bonus) + count;
            }
        }
        foreach (var building in buildings)
            if (building.NoBonus is { } bonus) result.Remove(bonus);
        return result;
    }
    // A count of -1 uses the world size. Zero grants no copies.
    internal static int FreeBonusCount(Simulation sim, S.BuildingDef building, string bonus)
    {
        int count = building.FreeBonus == bonus ? building.FreeBonusCount : 0;
        if (count == -1) count = sim.Content.TryGet<S.WorldSizeDef>(sim.Settings.WorldSize ?? "", out var world) ? world.NumFreeBuildingBonuses : 0;
        return Math.Max(0, count) + Math.Max(0, building.ExtraFreeBonuses.GetValueOrDefault(bonus));
    }
    /// <summary>Connected copies after exchanges with other players, plus what the city's own rules supply it
    /// (<see cref="ResourceAccess.Supplied"/>), so this agrees with <see cref="ResourceAccess.Counts"/>. Only resources
    /// the owner exchanges need a second lookup.</summary>
    private static IReadOnlyDictionary<string, int> Available(Simulation sim, City city) =>
        CityReadScope.Run(sim, scope => CityReadScope.Memo(scope.Available, city, () => AvailableUncached(sim, city)));
    private static IReadOnlyDictionary<string, int> AvailableUncached(Simulation sim, City city)
    {
        var network = sim.Rules().Resources;
        var physical = network.ConnectedCounts(sim, city.Owner, city.Id);
        var exchanges = sim.Rules().Diplomacy;
        var traded = exchanges.ExchangedResources(sim, city.Owner);
        var supplied = ResourceAccess.Supplied(sim, city.Owner, city.Id);
        if (traded.Count == 0 && supplied.Count == 0) return physical;
        var result = new SortedDictionary<string, int>(StringComparer.Ordinal);
        if (traded.Count == 0) foreach (var pair in physical) result[pair.Key] = pair.Value;
        else
        {
            // One batched answer: asking per resource repeated the city's connectivity query and every deal scan.
            var wanted = physical.Keys.Concat(traded).Distinct(StringComparer.Ordinal).ToArray();
            foreach (var (bonus, available) in exchanges.AvailableResourceCounts(sim, city.Owner, city.Id, wanted))
                if ((int)available > 0) result[bonus] = (int)available;
        }
        foreach (var (bonus, copies) in supplied) result[bonus] = result.GetValueOrDefault(bonus) + (int)copies;
        return result;
    }
    private static void Local(Simulation sim, City city, S.BuildingDef b, S.BuildingDef[] running, IReadOnlyDictionary<string, int> bonuses, ModifierSink sink)
    {
        sink.AddYield(b.Id, b.YieldChanges); sink.AddYield(b.Id, b.YieldModifiers, true);
        if (b.PowerYieldModifiers != default && CityData.IsPowered(sim, city))
            sink.AddYield(b.Id + ":power", b.PowerYieldModifiers, true);
        sink.AddCommerce(b.Id, b.CommerceChanges); sink.AddCommerce(b.Id, b.CommerceModifiers, true);
        sink.Add(b.Id, "cities:hurry_anger_percent", b.HurryAngerModifier);
        AddNumbers(b, sink);
        AddMaps(b, running, bonuses, sink);
        AddVicinity(sim, city, b, bonuses, sink);
        if (b.TechYieldChanges.IsEmpty && b.TechYieldModifiers.IsEmpty && b.TechCommerceChanges.IsEmpty && b.TechSpecialistChanges.IsEmpty)
            return;
        foreach (var tech in sim.TeamOf(city.Owner).Techs)
        {
            var yields = b.TechYieldChanges.GetValueOrDefault(tech);
            var percent = b.TechYieldModifiers.GetValueOrDefault(tech);
            var commerce = b.TechCommerceChanges.GetValueOrDefault(tech);
            if (yields != default || percent != default || commerce != default)
            {
                string source = b.Id + ":" + tech;
                sink.AddYield(source, yields);
                sink.AddYield(source, percent, true);
                sink.AddCommerce(source, commerce);
            }
            if (b.TechSpecialistChanges.TryGetValue(tech, out var changes))
                foreach (var slot in changes.Slots.OrderBy(p => p.Key, StringComparer.Ordinal)) sink.Add(b.Id, "cities:slots:" + slot.Key, slot.Value);
        }
    }
    private static void AddNumbers(S.BuildingDef b, ModifierSink sink)
    {
        sink.Add(b.Id, "cities:happiness", b.Happiness);
        sink.Add(b.Id, "cities:food_kept_percent", b.FoodKept);
        sink.Add(b.Id, "cities:growth_percent", b.PopulationGrowthPercent);
        sink.Add(b.Id, "cities:free_specialists", b.FreeSpecialist);
        sink.Add(b.Id, "cities:maintenance_percent", b.MaintenanceModifier);
        sink.Add(b.Id, "cities:distance_maintenance_percent", b.DistanceMaintenanceModifier);
        sink.Add(b.Id, "cities:coastal_maintenance_percent", b.CoastalDistanceMaintenanceModifier);
        sink.Add(b.Id, "cities:count_maintenance_percent", b.CityCountMaintenancePercent);
        sink.Add(b.Id, "cities:great_people_percent", b.GreatPeopleRateModifier);
        sink.Add(b.Id, "cities:experience", b.Experience);
        sink.Add(b.Id, "cities:military_production_percent", b.MilitaryProductionModifier);
        sink.Add(b.Id, "cities:no_unhappiness", b.NoUnhappiness ? 1 : 0);
        sink.Add(b.Id, "cities:no_unhealthy_population", b.NoUnhealthyPopulation ? 1 : 0);
        sink.Add(b.Id, "cities:building_only_healthy", b.BuildingOnlyHealthy ? 1 : 0);
        sink.Add(b.Id, HealthKeys.Building, b.Health);
    }
    private static void AddMaps(S.BuildingDef b, S.BuildingDef[] running, IReadOnlyDictionary<string, int> bonuses, ModifierSink sink)
    {
        // The per-bonus and per-building maps are empty for most buildings, and each is looked up for every bonus or
        // running building of the city, so an empty map is passed over: it would add only zero lines.
        if (!b.BonusYieldChanges.IsEmpty || !b.BonusYieldModifiers.IsEmpty || !b.BonusHappinessChanges.IsEmpty || !b.BonusHealthChanges.IsEmpty)
        {
            foreach (var bonus in bonuses.Keys.Order(StringComparer.Ordinal))
            {
                var yields = b.BonusYieldChanges.GetValueOrDefault(bonus);
                var percent = b.BonusYieldModifiers.GetValueOrDefault(bonus);
                if (yields != default || percent != default)
                {
                    string source = b.Id + ":" + bonus;
                    sink.AddYield(source, yields);
                    sink.AddYield(source, percent, true);
                }
                sink.Add(b.Id, "cities:happiness", b.BonusHappinessChanges.GetValueOrDefault(bonus));
                sink.Add(b.Id, HealthKeys.Bonus, b.BonusHealthChanges.GetValueOrDefault(bonus));
            }
        }
        foreach (var slot in b.SpecialistSlots.OrderBy(p => p.Key, StringComparer.Ordinal)) sink.Add(b.Id, "cities:slots:" + slot.Key, slot.Value);
        foreach (var free in b.FreeSpecialistCounts.OrderBy(p => p.Key, StringComparer.Ordinal)) sink.Add(b.Id, "cities:free:" + free.Key, free.Value);
        if (!b.BuildingHappinessChanges.IsEmpty)
            foreach (var other in running) sink.Add(b.Id, "cities:happiness", b.BuildingHappinessChanges.GetValueOrDefault(other.DefaultBuilding));
    }
    private static void AddVicinity(Simulation sim, City city, S.BuildingDef b, IReadOnlyDictionary<string, int> bonuses, ModifierSink sink)
    {
        foreach (var pair in b.VicinityBonusYieldChanges.OrderBy(p => p.Key, StringComparer.Ordinal))
            if (VicinityBonuses.Has(sim, city, pair.Key, bonuses)) sink.AddYield(b.Id + ":vicinity:" + pair.Key, pair.Value);
    }
    private static void Global(Simulation sim, City city, City origin, S.BuildingDef b, S.BuildingDef[] running, ModifierSink sink)
    {
        if (b.GlobalYieldModifiers != default) sink.AddYield(b.Id + ":global", b.GlobalYieldModifiers, true);
        if (b.GlobalCommerceModifiers != default) sink.AddCommerce(b.Id + ":global", b.GlobalCommerceModifiers, true);
        sink.Add(b.Id, "cities:happiness", b.GlobalHappiness); sink.Add(b.Id, HealthKeys.Building, b.GlobalHealth);
        sink.Add(b.Id, "cities:free_specialists", b.GlobalFreeSpecialist);
        sink.Add(b.Id, "cities:great_people_percent", b.GlobalGreatPeopleRateModifier);
        sink.Add(b.Id, "cities:maintenance_percent", b.GlobalMaintenanceModifier);
        sink.Add(b.Id, "cities:experience", b.GlobalExperience);
        // Every building of the owner's cities reaches here, and few have extra commerce for buildings elsewhere.
        if (!b.GlobalBuildingExtraCommerces.IsEmpty)
            foreach (var local in running) sink.AddCommerce(b.Id, b.GlobalBuildingExtraCommerces.GetValueOrDefault(local.DefaultBuilding));
        if (sim.State.PlotAt(city.Position).AreaId != sim.State.PlotAt(origin.Position).AreaId) return;
        sink.Add(b.Id, "cities:happiness", b.AreaHappiness); sink.Add(b.Id, HealthKeys.Building, b.AreaHealth);
        sink.Add(b.Id, "cities:free_specialists", b.AreaFreeSpecialist);
        sink.Add(b.Id, "cities:maintenance_percent", b.AreaMaintenanceModifier);
    }
}
