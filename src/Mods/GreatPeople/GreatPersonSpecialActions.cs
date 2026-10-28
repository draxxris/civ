using System.Globalization;
using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.GreatPeople;

internal static partial class GreatPersonActions
{
    private static Unit[] LeadUnits(Simulation sim, Unit leader, S.UnitDef def, Hex position) => sim.State.UnitsAt(position)
        .Where(u => u.Owner == leader.Owner && u.Id != leader.Id && u.Health > 0)
        .Where(u => sim.Content.Get<S.UnitDef>(u.Type).Strength > 0 && !sim.Content.Get<S.UnitDef>(u.Type).GreatGeneral)
        .Where(u => !u.Promotions.Contains(def.LeaderPromotion ?? "") && u.Extensions.Get<MilitaryUnitState>(sim.Content)?.AttachedLeaderUnitType is null)
        .OrderBy(u => u.Id).ToArray();
    private static void AddLeadActions(Simulation sim, Unit unit, S.UnitDef def, Hex position, List<GreatPersonAction> actions)
    {
        if (def.LeaderPromotion is null && def.LeaderExperience <= 0) return;
        var eligible = LeadUnits(sim, unit, def, position);
        long xp = (long)def.LeaderExperience * (100 + Math.Min(50, (eligible.Length - 1) * GreatPeopleData.Raw(sim, "WARLORD_EXTRA_EXPERIENCE_PER_UNIT_PERCENT", 0))) / 100;
        foreach (var target in eligible)
            actions.Add(new("great_people:lead_unit", target.Id.ToString(CultureInfo.InvariantCulture), xp));
    }
    internal static void Lead(Simulation sim, Unit leader, GreatPersonAction action)
    {
        var def = sim.Content.Get<S.UnitDef>(leader.Type);
        var eligible = LeadUnits(sim, leader, def, leader.Position);
        var target = sim.State.UnitById(int.Parse(action.Target!, CultureInfo.InvariantCulture));
        if (def.LeaderPromotion is { } promotion) target.Promotions.Add(promotion);
        var state = target.Extensions.Get<MilitaryUnitState>(sim.Content) ?? new();
        state.AttachedLeaderUnitType = leader.Type;
        target.Extensions.Set(sim.Content, state);
        long minXp = action.Amount / eligible.Length, remainder = action.Amount % eligible.Length;
        for (int i = 0; i < eligible.Length; i++) eligible[i].Experience += (int)(minXp + (i < remainder ? 1 : 0));
    }
    private static void AddConstructionActions(Simulation sim, City city, S.UnitDef def, List<GreatPersonAction> actions)
    {
        var civ = sim.Content.Get<S.CivilizationDef>(sim.State.PlayerById(city.Owner).Civilization);
        var buildings = def.ConstructibleBuildings.Where(p => p.Value).Select(p => p.Key)
            .Concat(def.ForceBuildings.Where(p => p.Value).Select(p => p.Key)).Distinct().Order(StringComparer.Ordinal);
        var resolved = new SortedDictionary<string, bool>(StringComparer.Ordinal);
        foreach (string id in buildings)
        {
            var source = sim.Content.Get<S.BuildingDef>(id);
            string? replacement = civ.BuildingOverrides.GetValueOrDefault(source.DefaultBuilding, id);
            if (replacement is null) continue;
            resolved[replacement] = resolved.GetValueOrDefault(replacement) || def.ForceBuildings.GetValueOrDefault(id);
        }
        foreach (var pair in resolved)
        {
            var building = sim.Content.Get<S.BuildingDef>(pair.Key);
            if (building.GlobalReligionCommerce is not null || city.Buildings.Contains(building.Id)) continue;
            if (!pair.Value && !CanConstruct(sim, city, building.Id)) continue;
            actions.Add(new("great_people:construct_building", building.Id, 1));
        }
    }
    private static bool CanConstruct(Simulation sim, City city, string building) =>
        sim.Content.Services.Get<ICityProductionService>().Single().Validate(sim, city.Id, "cities:building", building)
            .Failures.All(f => f.Explanation == "Building cannot be constructed");

    private static GreatPersonAction? Trade(Simulation sim, Unit unit, S.UnitDef def, Hex position)
    {
        if (def.BaseTrade <= 0 && def.TradeMultiplier <= 0) return null;
        var city = sim.State.CityAt(position);
        if (city is null || sim.TeamOf(city.Owner).Id == sim.TeamOf(unit.Owner).Id) return null;
        if (sim.TeamOf(unit.Owner).Wars.Contains(sim.TeamOf(city.Owner).Id)) return null;
        long profit = CalculateCapitalTradeProfit(sim, unit.Owner, city);
        long gold = ((long)def.BaseTrade + def.TradeMultiplier * profit) * GreatPeopleData.Speed(sim).UnitTradePercent / 100;
        gold = gold * Math.Max(0, 100 + TeamTradeMissionModifier(sim, unit.Owner)) / 100;
        return gold > 0 ? new("great_people:trade_mission", city.Id.ToString(CultureInfo.InvariantCulture), gold) : null;
    }
    private static long CalculateCapitalTradeProfit(Simulation sim, int player, City city)
    {
        var capital = sim.State.Cities.Where(c => c.Owner == player).OrderBy(c => c.Id)
            .FirstOrDefault(c => GreatPeopleData.Active(sim, c).Any(b => b.Capital));
        if (capital is null) return 0;
        return (sim.Content.Services.Get<ITradeRouteService>().SingleOrDefault()?.CalculateProfit(sim, city.Id, capital.Id).ProfitHundredths ?? 0) / 100;
    }
    private static long TeamTradeMissionModifier(Simulation sim, int player)
    {
        var team = sim.TeamOf(player);
        long techs = team.Techs.Order(StringComparer.Ordinal).Sum(t => (long)sim.Content.Get<S.TechDef>(t).TradeMissionModifier);
        long buildings = sim.State.Cities.Where(c => sim.TeamOf(c.Owner).Id == team.Id)
            .Sum(c => GreatPeopleData.Active(sim, c).Sum(b => (long)b.TradeMissionModifier));
        return techs + buildings;
    }
}
