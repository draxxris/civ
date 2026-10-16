using Civ.Content;
using Civ.Content.Schema;
using Civ.Game;
using CivicDef = Civ.Content.Schema.CivicDef;
using BuildingDef = Civ.Content.Schema.BuildingDef;
using UnitDef = Civ.Content.Schema.UnitDef;

namespace Civ.Mods.Government;

internal sealed class CivicEffects : IModifierSource
{
    public void Contribute(ModifierContext context, ModifierSink sink)
    {
        foreach (var civic in context.Sim.State.PlayerById(context.Player).Civics.Values.Order(StringComparer.Ordinal).Select(context.Sim.Content.Get<CivicDef>))
            Contribute(context, civic, sink);
        if (context.Scope == ModifierScope.City && context.City is { } affectedCity)
            CivicAnger(context.Sim, affectedCity, sink);
        if (context.Scope != ModifierScope.Player) return;
        foreach (var city in context.Sim.State.Cities.Where(city => city.Owner == context.Player))
            foreach (var building in ActiveBuildings(context.Sim, city)) sink.Add(building.Id, "government:anarchy_percent", building.AnarchyModifier);
    }
    private static void Contribute(ModifierContext context, CivicDef civic, ModifierSink sink)
    {
        if (context.Scope == ModifierScope.City && context.City is { } city) City(context.Sim, city, civic, sink);
        if (context.Scope == ModifierScope.Player) Player(context.Sim, context.Player, civic, sink);
        if (context.Scope == ModifierScope.Plot && context.Plot is { } tile)
        {
            var plot = context.ProposedPlot ?? context.Sim.State.Plots[tile.Value];
            if (plot.Improvement is { } improvement) sink.AddYield(civic.Id, civic.ImprovementYieldChanges.GetValueOrDefault(improvement));
        }
        if (context.Scope == ModifierScope.Unit)
        {
            sink.Add(civic.Id, "military:initial_experience", civic.FreeExperience);
            sink.Add(civic.Id, "military:experience_in_borders_percent", civic.ExpInBorderModifier);
            sink.Add(civic.Id, "military:city_defense", civic.ExtraCityDefense);
        }
    }
    private static void Player(Simulation sim, int player, CivicDef c, ModifierSink sink)
    {
        var values = new (string Key, int Value)[] {
            ("cities:distance_maintenance_percent", c.DistanceMaintenanceModifier), ("cities:count_maintenance_percent", c.CityCountMaintenancePercent),
            ("cities:corporation_maintenance_percent", sim.IsOptionEnabled("foundation:gameoption_realistic_corporations")
                ? c.RealCorporationMaintenanceModifier : c.CorporationMaintenanceModifier), ("cities:growth_percent", c.PopulationGrowthPercent),
            ("cities:max_conscript", GetWorldSizeMaxConscript(sim, c)),
            ("world:worker_speed_percent", c.WorkerSpeedModifier), ("world:improvement_upgrade_percent", c.ImprovementUpgradeRateModifier),
            ("military:general_rate_percent", c.GreatGeneralRateModifier), ("military:domestic_general_rate_percent", c.DomesticGreatGeneralRateModifier) };
        foreach (var value in values) sink.Add(c.Id, value.Key, value.Value);
        // Unit support, supply, hurry permissions and civic upkeep already use the active definitions in Economy.

    }
    private static int GetWorldSizeMaxConscript(Simulation sim, CivicDef civic)
    {
        int modifier = string.IsNullOrEmpty(sim.Settings.WorldSize) ? 0
            : sim.Content.Get<WorldSizeDef>(sim.Settings.WorldSize).MaxConscriptModifier;
        return civic.MaxConscript * Math.Max(0, 100 + modifier) / 100;
    }
    private static IEnumerable<BuildingDef> ActiveBuildings(Simulation sim, City city) => city.Buildings.Order(StringComparer.Ordinal)
        .Select(sim.Content.Get<BuildingDef>).Where(b => IsActive(sim, city, b));
    private static bool IsActive(Simulation sim, City city, BuildingDef b)
    {
        if (b.ObsoleteTech is { } obsolete && sim.KnowsTech(city.Owner, obsolete)) return false;
        return !b.RequiresActiveCivics || sim.Content.Requirements.EvaluateKind(b.Requirements, new(sim, city.Owner, city), "civic").Success;
    }
    private static void City(Simulation sim, City city, CivicDef c, ModifierSink sink)
    {
        var buildings = ActiveBuildings(sim, city).ToArray();
        sink.AddYield(c.Id, c.YieldModifiers, true);
        sink.AddCommerce(c.Id, c.CommerceModifiers, true);
        sink.Add(c.Id, "cities:foreigner_unhappy_percent", c.ForeignerUnhappyPercent);

        var values = new (string Key, int Value)[] {
            ("happiness", c.CivicHappiness), ("health", c.ExtraHealth), ("free_specialists", c.FreeSpecialist),
            ("great_people_percent", c.GreatPeopleRateModifier), ("military_production_percent", c.MilitaryProductionModifier),
            ("military_food_production", c.MilitaryFoodProduction ? 1 : 0), ("war_weariness_percent", c.WarWearinessModifier),
            ("population_anger_percent", c.PopulationAngerModifier), ("no_unhealthy_population", c.NoUnhealthyPopulation ? 1 : 0),
            ("building_only_healthy", c.BuildingOnlyHealthy ? 1 : 0) };
        foreach (var value in values) sink.Add(c.Id, "cities:" + value.Key, value.Value);
        Capital(city, c, buildings, sink);
        Wellbeing(sim, city, c, buildings, sink);
        Production(sim, c, sink);
        SpecialistEffects(c, sink);
        foreach (var b in buildings)
        {
            sink.AddCommerce(c.Id, c.BuildingClassCommerceChanges.GetValueOrDefault(b.DefaultBuilding));
            sink.Add(c.Id, "cities:happiness", c.BuildingHappinessChanges.GetValueOrDefault(b.DefaultBuilding));
            sink.Add(c.Id, "cities:health", c.BuildingHealthChanges.GetValueOrDefault(b.DefaultBuilding));
        }
    }
    private static void CivicAnger(Simulation sim, City city, ModifierSink sink)
    {
        var others = sim.State.Players.Where(p => p.Id != city.Owner && !IsBarbarian(sim, p.Id)
            && sim.TeamOf(p.Id).Id != sim.TeamOf(city.Owner).Id)
            .Select(p => new { Player = p, Cities = sim.State.Cities.Count(c => c.Owner == p.Id) }).ToArray();
        int possible = others.Sum(p => p.Cities);
        if (possible == 0) return;
        var owner = sim.State.PlayerById(city.Owner);
        foreach (var civic in sim.Content.Get<CivicDef>().All.Where(c => c.CivicPercentAnger != 0 && c.Option is not null))
        {
            if (owner.Civics.GetValueOrDefault(civic.Option!) == civic.Id) continue;
            int count = others.Where(p => p.Player.Civics.GetValueOrDefault(civic.Option!) == civic.Id).Sum(p => p.Cities);
            sink.Add(civic.Id, "cities:civic_percent_anger", (long)civic.CivicPercentAnger * count / possible);
        }
    }
    private static bool IsBarbarian(Simulation sim, int player) =>
        sim.State.PlayerById(player).Extensions.TryGet<MilitaryPlayerState>(sim.Content)?.Barbarian == true;
    private static void Capital(City city, CivicDef c, BuildingDef[] buildings, ModifierSink sink)
    {
        if (!buildings.Any(b => b.Capital)) return;
        sink.AddYield(c.Id + ":capital", c.CapitalYieldModifiers, true);
        sink.AddCommerce(c.Id + ":capital", c.CapitalCommerceModifiers, true);
        sink.Add(c.Id, "cities:no_unhappiness", c.NoCapitalUnhappiness ? 1 : 0);
    }
    private static void Wellbeing(Simulation sim, City city, CivicDef c, BuildingDef[] buildings, ModifierSink sink)
    {
        if (c.HappyPerMilitaryUnit != 0)
        {
            int military = sim.State.UnitsAt(city.Position).Count(u => u.Owner == city.Owner && sim.Content.Get<UnitDef>(u.Type).MilitaryHappiness);
            sink.Add(c.Id, "cities:happiness", military * c.HappyPerMilitaryUnit);
        }
        if (c.LargestCityHappiness != 0) LargestCities(sim, city, c, sink);
        if (c.FeatureHappinessChanges.Count == 0 && c.ImprovementHealthPercentChanges.Count == 0) return;
        long featureHappy = 0, improvementHealth = 0;
        foreach (var tile in sim.Content.Services.Get<ICityWorkService>().Single().WorkArea(sim, city.Id))
        {
            var plot = sim.State.Plots[tile.Value];
            if (plot.Owner != city.Owner) continue;
            if (plot.Feature is { } f) featureHappy += c.FeatureHappinessChanges.GetValueOrDefault(f);
            if (plot.Improvement is { } i) improvementHealth += c.ImprovementHealthPercentChanges.GetValueOrDefault(i);
        }
        sink.Add(c.Id + ":features", "cities:happiness", featureHappy);
        sink.Add(c.Id + ":improvements", "cities:health", improvementHealth / 100);
    }
    private static void LargestCities(Simulation sim, City city, CivicDef c, ModifierSink sink)
    {
        int count = sim.Content.Get<WorldSizeDef>(sim.Settings.WorldSize!).TargetNumCities;
        bool largest = sim.State.Cities.Where(x => x.Owner == city.Owner).OrderByDescending(x => x.Population).ThenBy(x => x.Id)
            .Take(count).Any(x => x.Id == city.Id);
        if (largest) sink.Add(c.Id + ":largest", "cities:happiness", c.LargestCityHappiness);
    }
    private static void Production(Simulation sim, CivicDef c, ModifierSink sink)
    {
        foreach (var pair in c.BuildingClassProductionModifiers.OrderBy(p => p.Key, StringComparer.Ordinal))
            sink.Add(c.Id, "cities:building_class_production_percent:" + pair.Key, pair.Value);
        foreach (var pair in c.UnitCombatProductionModifiers.OrderBy(p => p.Key, StringComparer.Ordinal))
            sink.Add(c.Id, "cities:combat_production_percent:" + pair.Key, pair.Value);
    }
    private static void SpecialistEffects(CivicDef c, ModifierSink sink)
    {
        foreach (var valid in c.SpecialistValids.OrderBy(p => p.Key, StringComparer.Ordinal))
            sink.Add(c.Id, "cities:unlimited:" + valid.Key, valid.Value ? 1 : 0);
        foreach (var pair in c.SpecialistYieldPercentChanges.OrderBy(p => p.Key, StringComparer.Ordinal))
            SpecialistYield(c.Id, pair.Key, pair.Value, sink);
        foreach (var pair in c.SpecialistCommercePercentChanges.OrderBy(p => p.Key, StringComparer.Ordinal))
            SpecialistCommerce(c.Id, pair.Key, pair.Value, sink);
        SpecialistCommerce(c.Id, "all", c.SpecialistExtraCommerces * 100, sink);
    }
    private static void SpecialistYield(string source, string specialist, YieldVector value, ModifierSink sink)
    {
        sink.Add(source, "cities:specialist_food_hundredths:" + specialist, value.Food);
        sink.Add(source, "cities:specialist_production_hundredths:" + specialist, value.Production);
        sink.Add(source, "cities:specialist_commerce_hundredths:" + specialist, value.Commerce);
    }
    private static void SpecialistCommerce(string source, string specialist, CommerceVector value, ModifierSink sink)
    {
        sink.Add(source, "cities:specialist_gold_hundredths:" + specialist, value.Gold);
        sink.Add(source, "cities:specialist_research_hundredths:" + specialist, value.Research);
        sink.Add(source, "cities:specialist_culture_hundredths:" + specialist, value.Culture);
        sink.Add(source, "cities:specialist_espionage_hundredths:" + specialist, value.Espionage);
    }
}
