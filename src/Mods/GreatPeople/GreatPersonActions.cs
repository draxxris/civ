using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.GreatPeople;

internal static partial class GreatPersonActions
{
    internal static IReadOnlyList<GreatPersonAction> Available(Simulation sim, int player, int unitId, Hex? destination = null)
    {
        var unit = sim.State.FindUnit(unitId);
        if (unit is null || unit.Owner != player || unit.Health <= 0) return [];
        var def = sim.Content.Get<S.UnitDef>(unit.Type);
        var actions = new List<GreatPersonAction>();
        var position = destination ?? unit.Position;
        var city = sim.State.CityAt(position);
        if (city is not null) OwnCityActions(sim, unit, def, city, actions);
        AddLeadActions(sim, unit, def, position, actions);
        if (Trade(sim, unit, def, city, new TradeTerms(sim, unit.Owner)) is { } trade) actions.Add(trade);
        if (Discovery(sim, player, def) is { } discovery) actions.Add(discovery);
        if (def.GoldenAge && GoldenAgeUnits(sim, player, unit, []).Length == Required(sim, player))
            actions.Add(new(GreatPersonMissions.GoldenAge, "", GoldenAges.Length(sim, player)));
        return actions.AsReadOnly();
    }
    /// <summary>The part of <see cref="Available"/> at <paramref name="city"/> that its owner's own city offers: joining,
    /// building, hurrying and a great work, added to <paramref name="actions"/>. Nothing for another player's city or one
    /// in disorder.</summary>
    internal static void OwnCityActions(Simulation sim, Unit unit, S.UnitDef def, City city, List<GreatPersonAction> actions)
    {
        if (city.Owner == unit.Owner && !GovernmentStatus.IsDisorder(sim, city)) CityActions(sim, city, def, actions);
    }
    private static void CityActions(Simulation sim, City city, S.UnitDef def, List<GreatPersonAction> actions)
    {
        foreach (var specialist in GreatPeopleData.Unit(sim.Content, def.Id).GreatPeoples.Where(p => p.Value).OrderBy(p => p.Key, StringComparer.Ordinal))
            actions.Add(new(GreatPersonMissions.Join, specialist.Key, 1));
        AddConstructionActions(sim, city, def, actions);
        if (Hurry(sim, city, def) is { } hurry) actions.Add(hurry);
        long culture = Math.Max(0, (long)GreatPeopleData.Unit(sim.Content, def.Id).GreatWorkCulture * GreatPeopleData.SpeedRules(sim).UnitGreatWorkPercent / 100);
        if (culture > 0) actions.Add(new(GreatPersonMissions.Culture, "", culture));
    }
    private static GreatPersonAction? Hurry(Simulation sim, City city, S.UnitDef def)
    {
        var order = city.ProductionQueue.FirstOrDefault();
        if (order is null || order.Kind != "cities:building") return null;
        var production = sim.Rules().CityProduction;
        if (!production.Validate(sim, city.Id, order.Kind, order.ContentId).Success) return null;
        var gp = GreatPeopleData.Unit(sim.Content, def.Id);
        long capacity = Math.Max(0, ((long)gp.BaseHurry + (long)gp.HurryMultiplier * city.Population) * GreatPeopleData.SpeedRules(sim).UnitHurryPercent / 100);
        long amount = Math.Min(capacity, Math.Max(0, production.Calculate(sim, city.Id, order.Kind, order.ContentId).Cost - order.Progress));
        return amount > 0 ? new(GreatPersonMissions.Hurry, order.ContentId, amount) : null;
    }
    private static GreatPersonAction? Discovery(Simulation sim, int player, S.UnitDef def)
    {
        long population = sim.State.Cities.Where(c => sim.TeamOf(c.Owner).Id == sim.TeamOf(player).Id).Sum(c => (long)c.Population);
        var gp = GreatPeopleData.Unit(sim.Content, def.Id);
        long capacity = Math.Max(0, ((long)gp.BaseDiscover + (long)gp.DiscoverMultiplier * population) * GreatPeopleData.SpeedRules(sim).UnitDiscoverPercent / 100);
        if (capacity <= 0) return null;
        var research = sim.Rules().Research;
        var choice = sim.Content.Get<S.TechDef>().All.Where(t => research.Validate(sim, player, t.Id).Success)
            .Select(t => (Tech: t, Score: t.Flavors.Sum(f => (long)f.Value * def.Flavors.GetValueOrDefault(f.Key))))
            .Where(t => t.Score > 0).OrderByDescending(t => t.Score).ThenBy(t => t.Tech.Id, StringComparer.Ordinal).FirstOrDefault();
        if (choice.Tech is null) return null;
        long remaining = research.CalculateCost(sim, sim.TeamOf(player).Id, choice.Tech.Id).Cost - sim.TeamOf(player).Research.GetValueOrDefault(choice.Tech.Id);
        long amount = Math.Min(capacity, Math.Max(0, remaining));
        return amount > 0 ? new(GreatPersonMissions.Discover, choice.Tech.Id, amount) : null;
    }
    internal static int Required(Simulation sim, int player) => sim.Service<IGreatPeopleService>()!.GoldenAgeUnitsRequired(sim, player);
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
