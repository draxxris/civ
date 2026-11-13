using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using CivicDef = Ariel.Content.Schema.CivicDef;
using BuildingDef = Ariel.Content.Schema.BuildingDef;
using UnitDef = Ariel.Content.Schema.UnitDef;

namespace Ariel.Rules.Government;

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
        // The civic's plain modifiers are data; the typed rules below need more than the calculation's subject.
        foreach (var effect in civic.Effects)
            if (effect.Scope == context.Scope) sink.Apply(civic.Id, effect);
        if (context.Scope == ModifierScope.City && context.City is { } city) City(context.Sim, city, civic, sink);
        if (context.Scope == ModifierScope.Player) Player(context.Sim, civic, sink);
        if (context.Scope == ModifierScope.Plot && context.Plot is { } tile)
        {
            var plot = context.ProposedPlot ?? context.Sim.State.Plots[tile.Value];
            if (plot.Improvement is { } improvement) sink.AddYield(civic.Id, civic.ImprovementYieldChanges.GetValueOrDefault(improvement));
        }
    }
    private static void Player(Simulation sim, CivicDef c, ModifierSink sink)
    {
        sink.Add(c.Id, "cities:max_conscript", GetWorldSizeMaxConscript(sim, c));
        // Unit support, supply, hurry permissions and civic upkeep already use the active definitions in Economy.
    }
    private static int GetWorldSizeMaxConscript(Simulation sim, CivicDef civic)
    {
        int modifier = string.IsNullOrEmpty(sim.Settings.WorldSize) ? 0
            : sim.Content.Get<WorldSizeDef>(sim.Settings.WorldSize).MaxConscriptModifier;
        return civic.MaxConscript * Math.Max(0, 100 + modifier) / 100;
    }
    private static IEnumerable<BuildingDef> ActiveBuildings(Simulation sim, City city) => CityBuildingRules.Running(sim, city);
    private static void City(Simulation sim, City city, CivicDef c, ModifierSink sink)
    {
        var buildings = ActiveBuildings(sim, city).ToArray();
        Capital(city, c, buildings, sink);
        Wellbeing(sim, city, c, buildings, sink);
        SpecialistCommerce(c.Id, "all", c.SpecialistExtraCommerces * 100, sink);
        foreach (var b in buildings)
        {
            sink.AddCommerce(c.Id, c.BuildingClassCommerceChanges.GetValueOrDefault(b.DefaultBuilding));
            sink.Add(c.Id, "cities:happiness", c.BuildingHappinessChanges.GetValueOrDefault(b.DefaultBuilding));
            // Cities counts a civic's change to a kind of building as building health (HealthKeys.Building).
            sink.Add(c.Id, "cities:building_health", c.BuildingHealthChanges.GetValueOrDefault(b.DefaultBuilding));
        }
    }
    private static void CivicAnger(Simulation sim, City city, ModifierSink sink)
    {
        var others = sim.State.Players.Where(p => p.Id != city.Owner && !PlayerRules.IsBarbarian(sim, p.Id)
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
        foreach (var tile in sim.Rules().CityWork.WorkArea(sim, city.Id))
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
    private static void SpecialistCommerce(string source, string specialist, CommerceVector value, ModifierSink sink)
    {
        sink.Add(source, "cities:specialist_gold_hundredths:" + specialist, value.Gold);
        sink.Add(source, "cities:specialist_research_hundredths:" + specialist, value.Research);
        sink.Add(source, "cities:specialist_culture_hundredths:" + specialist, value.Culture);
        sink.Add(source, "cities:specialist_espionage_hundredths:" + specialist, value.Espionage);
    }
}
