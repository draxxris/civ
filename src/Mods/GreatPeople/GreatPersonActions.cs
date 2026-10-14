using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.GreatPeople;

internal static class GreatPersonActions
{
    internal static IReadOnlyList<GreatPersonAction> Available(Simulation sim, int player, int unitId)
    {
        var unit = sim.State.FindUnit(unitId);
        if (unit is null || unit.Owner != player || unit.Health <= 0) return [];
        var def = sim.Content.Get<S.UnitDef>(unit.Type);
        var actions = new List<GreatPersonAction>();
        var city = sim.State.CityAt(unit.Position);
        if (city is not null && city.Owner == player && !GovernmentStatus.IsDisorder(sim, city))
            CityActions(sim, city, def, actions);
        if (Discovery(sim, player, def) is { } discovery) actions.Add(discovery);
        if (def.GoldenAge && GoldenAgeUnits(sim, player, unit, []).Length == Required(sim, player))
            actions.Add(new("great_people:golden_age", null, GoldenAges.Length(sim, player)));
        return actions.AsReadOnly();
    }
    private static void CityActions(Simulation sim, City city, S.UnitDef def, List<GreatPersonAction> actions)
    {
        foreach (var specialist in def.GreatPeoples.Where(p => p.Value).OrderBy(p => p.Key, StringComparer.Ordinal))
            actions.Add(new("great_people:join_city", specialist.Key, 1));
        if (Hurry(sim, city, def) is { } hurry) actions.Add(hurry);
        long culture = Math.Max(0, (long)def.GreatWorkCulture * GreatPeopleData.Speed(sim).UnitGreatWorkPercent / 100);
        if (culture > 0) actions.Add(new("great_people:culture_bomb", null, culture));
    }
    private static GreatPersonAction? Hurry(Simulation sim, City city, S.UnitDef def)
    {
        var order = city.ProductionQueue.FirstOrDefault();
        if (order is null || order.Kind != "cities:building") return null;
        var production = sim.Content.Services.Get<ICityProductionService>().Single();
        if (!production.Validate(sim, city.Id, order.Kind, order.ContentId).Success) return null;
        long capacity = Math.Max(0, ((long)def.BaseHurry + (long)def.HurryMultiplier * city.Population) * GreatPeopleData.Speed(sim).UnitHurryPercent / 100);
        long amount = Math.Min(capacity, Math.Max(0, production.Calculate(sim, city.Id, order.Kind, order.ContentId).Cost - order.Progress));
        return amount > 0 ? new("great_people:hurry_production", order.ContentId, amount) : null;
    }
    private static GreatPersonAction? Discovery(Simulation sim, int player, S.UnitDef def)
    {
        long population = sim.State.Cities.Where(c => sim.TeamOf(c.Owner).Id == sim.TeamOf(player).Id).Sum(c => (long)c.Population);
        long capacity = Math.Max(0, ((long)def.BaseDiscover + (long)def.DiscoverMultiplier * population) * GreatPeopleData.Speed(sim).UnitDiscoverPercent / 100);
        if (capacity <= 0) return null;
        var research = sim.Content.Services.Get<IResearchService>().Single();
        var choice = sim.Content.Get<S.TechDef>().All.Where(t => research.Validate(sim, player, t.Id).Success)
            .Select(t => (Tech: t, Score: t.Flavors.Sum(f => (long)f.Value * def.Flavors.GetValueOrDefault(f.Key))))
            .Where(t => t.Score > 0).OrderByDescending(t => t.Score).ThenBy(t => t.Tech.Id, StringComparer.Ordinal).FirstOrDefault();
        if (choice.Tech is null) return null;
        long remaining = research.CalculateCost(sim, sim.TeamOf(player).Id, choice.Tech.Id).Cost - sim.TeamOf(player).Research.GetValueOrDefault(choice.Tech.Id);
        long amount = Math.Min(capacity, Math.Max(0, remaining));
        return amount > 0 ? new("great_people:discover_tech", choice.Tech.Id, amount) : null;
    }
    internal static int Required(Simulation sim, int player) => sim.Content.Services.Get<IGreatPeopleService>().Single().GoldenAgeUnitsRequired(sim, player);
    internal static int[] GoldenAgeUnits(Simulation sim, int player, Unit initiating, IReadOnlyList<int> requested)
    {
        int required = Required(sim, player);
        if (requested.Count > 0) return ValidateSelection(sim, player, initiating, requested, required) ? requested.ToArray() : [];
        var selected = new List<int> { initiating.Id };
        var classes = new HashSet<string>(StringComparer.Ordinal) { sim.Content.Get<S.UnitDef>(initiating.Type).DefaultUnit };
        var origin = sim.State.Grid.TileAt(initiating.Position);
        foreach (var unit in sim.State.Units.Where(u => u.Owner == player && u.Id != initiating.Id && u.Health > 0)
            .OrderBy(u => sim.State.Grid.Distance(origin, sim.State.Grid.TileAt(u.Position))).ThenBy(u => u.Id))
        {
            if (selected.Count == required) break;
            var def = sim.Content.Get<S.UnitDef>(unit.Type);
            if (def.GoldenAge && classes.Add(def.DefaultUnit)) selected.Add(unit.Id);
        }
        return selected.Count == required ? selected.ToArray() : [];
    }
    private static bool ValidateSelection(Simulation sim, int player, Unit initiating, IReadOnlyList<int> requested, int required)
    {
        if (requested.Count != required || !requested.Contains(initiating.Id) || requested.Distinct().Count() != required) return false;
        var classes = new HashSet<string>(StringComparer.Ordinal);
        foreach (int id in requested)
        {
            var unit = sim.State.FindUnit(id);
            if (unit is null || unit.Owner != player || unit.Health <= 0) return false;
            var def = sim.Content.Get<S.UnitDef>(unit.Type);
            if (!def.GoldenAge || !classes.Add(def.DefaultUnit)) return false;
        }
        return true;
    }
}

internal sealed class GreatPeopleCommands : IGreatPeopleCommandService
{
    private static string Command(string action) => action switch
    {
        "join" => "great_people:join_city", "discover" => "great_people:discover_tech", "hurry" => "great_people:hurry_production",
        "culture" => "great_people:culture_bomb", "golden-age" => "great_people:golden_age", _ => "",
    };
    private static GreatPersonAction? Preview(Simulation sim, int player, int unit, string action, string? target) =>
        GreatPersonActions.Available(sim, player, unit).FirstOrDefault(a => a.Command == Command(action) && (target is null || a.Target == target));
    public string? Validate(Simulation sim, int player, int unit, string action, string? target, IReadOnlyList<int> units)
    {
        if (Preview(sim, player, unit, action, target) is null) return "Great Person action is not available";
        if (action != "golden-age") return units.Count == 0 ? null : "This action uses one unit";
        return GreatPersonActions.GoldenAgeUnits(sim, player, sim.State.UnitById(unit), units).Length == GreatPersonActions.Required(sim, player)
            ? null : "Select the required number of distinct Great Person classes";
    }
    public void Execute(Simulation sim, int player, int unitId, string action, string? target, IReadOnlyList<int> units)
    {
        var preview = Preview(sim, player, unitId, action, target)!;
        var unit = sim.State.UnitById(unitId);
        var city = sim.State.CityAt(unit.Position);
        switch (action)
        {
            case "join":
                var state = GreatPeopleData.Edit(sim, city!);
                state.SettledSpecialists[preview.Target!] = state.SettledSpecialists.GetValueOrDefault(preview.Target!) + 1;
                break;
            case "discover": sim.Content.Services.Get<IResearchService>().Single().AddProgress(sim, player, preview.Target!, preview.Amount); break;
            case "hurry": city!.ProductionQueue[0] = city.ProductionQueue[0] with { Progress = city.ProductionQueue[0].Progress + preview.Amount }; break;
            case "culture":
                int turns = GreatPeopleData.Raw(sim, "GREAT_WORKS_CULTURE_TURNS", 20) * GreatPeopleData.Speed(sim).UnitGreatWorkPercent / 100;
                sim.Content.Services.Get<ICityCultureService>().Single().AddCulture(sim, city!.Id, preview.Amount, turns);
                break;
            case "golden-age":
                foreach (int id in GreatPersonActions.GoldenAgeUnits(sim, player, unit, units)) sim.State.RemoveUnit(id);
                GreatPeopleData.Edit(sim, player).UnitGoldenAges++;
                GoldenAges.Start(sim, player, false);
                break;
        }
        if (sim.State.FindUnit(unitId) is not null) sim.State.RemoveUnit(unitId);
        sim.Content.Services.Get<IVisibilityService>().SingleOrDefault()?.Recompute(sim);
        sim.Events.Publish(new GreatPersonUsed(player, unitId, preview.Command, preview.Target, preview.Amount));
    }
}
