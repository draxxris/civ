using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Cities;

internal static class PuppetProduction
{
    internal static void Choose(Simulation sim, City city, ICityProductionService production)
    {
        // A puppet cannot build wonders: the player cannot edit its queue, so a wonder here would block the same one in
        // a city the player does control.
        var building = sim.Content.Get<S.BuildingDef>().All
            .Where(b => BuildingScope.Of(b) != QueueScope.Player)
            .Where(b => production.Validate(sim, city.Id, "cities:building", b.Id).Success)
            .OrderByDescending(Value).ThenBy(b => b.Id, StringComparer.Ordinal).FirstOrDefault();
        if (building is not null)
        {
            city.ProductionQueue.Add(new("cities:building", building.Id));
            return;
        }
        foreach (var id in new[] { "economy:process_wealth", "economy:process_research", "economy:process_idle" })
        {
            if (!production.Validate(sim, city.Id, "economy:process", id).Success) continue;
            city.ProductionQueue.Add(new("economy:process", id));
            return;
        }
        var defender = sim.Content.Get<S.UnitDef>().All.Where(u => u.Strength > 0 && u.Domain == "military:domain_land")
            .Where(u => production.Validate(sim, city.Id, "cities:unit", u.Id).Success)
            .OrderByDescending(u => u.Strength).ThenBy(u => u.Id, StringComparer.Ordinal).FirstOrDefault();
        if (defender is not null) city.ProductionQueue.Add(new("cities:unit", defender.Id));
    }
    private static long Value(S.BuildingDef building) =>
        (building.YieldChanges.Food + building.YieldChanges.Production * 3L + building.YieldChanges.Commerce * 2L
            + building.Health * 2L + building.Happiness * 2L + building.DefensePercent) * 1000 / Math.Max(1, building.Cost);
}
