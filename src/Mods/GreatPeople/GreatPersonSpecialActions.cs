using System.Globalization;
using Ariel.Content;
using Ariel.Game;
using Ariel.Mods.Religion;
using Ariel.Mods.Trade;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.GreatPeople;

internal static partial class GreatPersonActions
{
    private static Unit[] LeadUnits(Simulation sim, Unit leader, S.UnitDef def, Hex position) => sim.State.UnitsAt(position)
        .Where(u => u.Owner == leader.Owner && u.Id != leader.Id && u.Health > 0)
        .Where(u => sim.Content.Get<S.UnitDef>(u.Type).Strength > 0 && !GreatPeopleData.Unit(sim.Content, u.Type).GreatGeneral)
        .Where(u => !u.Promotions.Contains(GreatPeopleData.Unit(sim.Content, def.Id).LeaderPromotion ?? "") && u.Extensions.Get<MilitaryUnitState>(sim.Content)?.AttachedLeaderUnitType is null)
        .OrderBy(u => u.Id).ToArray();
    /// <summary>The part of <see cref="Available"/> at <paramref name="position"/> that leading a unit there offers.</summary>
    internal static void AddLeadActions(Simulation sim, Unit unit, S.UnitDef def, Hex position, List<GreatPersonAction> actions)
    {
        var gp = GreatPeopleData.Unit(sim.Content, def.Id);
        if (gp.LeaderPromotion is null && gp.LeaderExperience <= 0) return;
        var eligible = LeadUnits(sim, unit, def, position);
        long xp = (long)gp.LeaderExperience * (100 + Math.Min(50, (eligible.Length - 1) * GreatPeopleData.Raw(sim, "WARLORD_EXTRA_EXPERIENCE_PER_UNIT_PERCENT", 0))) / 100;
        foreach (var target in eligible)
            actions.Add(new(GreatPersonMissions.Lead, target.Id.ToString(CultureInfo.InvariantCulture), xp));
    }
    internal static void Lead(Simulation sim, Unit leader, GreatPersonAction action)
    {
        var def = sim.Content.Get<S.UnitDef>(leader.Type);
        var eligible = LeadUnits(sim, leader, def, leader.Position);
        var target = sim.State.UnitById(int.Parse(action.Subject, CultureInfo.InvariantCulture));
        if (GreatPeopleData.Unit(sim.Content, def.Id).LeaderPromotion is { } promotion) target.Promotions.Add(promotion);
        var state = target.Extensions.Get<MilitaryUnitState>(sim.Content) ?? new();
        state.AttachedLeaderUnitType = leader.Type;
        target.Extensions.Set(sim.Content, state);
        long minXp = action.Amount / eligible.Length, remainder = action.Amount % eligible.Length;
        for (int i = 0; i < eligible.Length; i++) eligible[i].Experience += (int)(minXp + (i < remainder ? 1 : 0));
    }
    private static void AddConstructionActions(Simulation sim, City city, S.UnitDef def, List<GreatPersonAction> actions)
    {
        var civ = sim.Content.Get<S.CivilizationDef>(sim.State.PlayerById(city.Owner).Civilization);
        var force = GreatPeopleData.Unit(sim.Content, def.Id).ForceBuildings;
        var buildings = def.ConstructibleBuildings.Where(p => p.Value).Select(p => p.Key)
            .Concat(force.Where(p => p.Value).Select(p => p.Key)).Distinct().Order(StringComparer.Ordinal);
        var resolved = new SortedDictionary<string, bool>(StringComparer.Ordinal);
        foreach (string id in buildings)
        {
            var source = sim.Content.Get<S.BuildingDef>(id);
            string? replacement = civ.BuildingOverrides.GetValueOrDefault(source.DefaultBuilding, id);
            if (replacement is null) continue;
            resolved[replacement] = resolved.GetValueOrDefault(replacement) || force.GetValueOrDefault(id);
        }
        foreach (var pair in resolved)
        {
            var building = sim.Content.Get<S.BuildingDef>(pair.Key);
            if (sim.Content.Attachment<BuildingReligionDef>(building.Id)?.GlobalReligionCommerce is not null || city.Buildings.Contains(building.Id)) continue;
            // A building that founds something (a religion, a corporation) is founded only by building it in a city; a
            // great person that lists it does not construct it.
            if (building.Founds is not null) continue;
            if (!pair.Value && !CanConstruct(sim, city, building.Id)) continue;
            actions.Add(new(GreatPersonMissions.Construct, building.Id, 1));
        }
    }
    private static bool CanConstruct(Simulation sim, City city, string building) =>
        sim.Rules().CityProduction.Validate(sim, city.Id, "cities:building", building)
            .Failures.All(ProductionFailures.IsCostOnly);

    /// <summary>The part of <see cref="Available"/> at <paramref name="city"/> that a trade mission offers, or null.
    /// <paramref name="terms"/> holds what the gold depends on besides the city.</summary>
    internal static GreatPersonAction? Trade(Simulation sim, Unit unit, S.UnitDef def, City? city, TradeTerms terms)
    {
        if (def.BaseTrade <= 0 && def.TradeMultiplier <= 0) return null;
        if (city is null || sim.TeamOf(city.Owner).Id == sim.TeamOf(unit.Owner).Id) return null;
        if (sim.TeamOf(unit.Owner).Wars.Contains(sim.TeamOf(city.Owner).Id)) return null;
        // The route's profit counts only through the multiplier, so without one the route is not weighed.
        long profit = def.TradeMultiplier == 0 || terms.Capital is not { } capital ? 0
            : (sim.Service<ITradeRouteService>()?.CalculateProfit(sim, city.Id, capital.Id).ProfitHundredths ?? 0) / 100;
        long gold = ((long)def.BaseTrade + def.TradeMultiplier * profit) * GreatPeopleData.SpeedRules(sim).UnitTradePercent / 100;
        gold = gold * Math.Max(0, 100 + terms.Modifier) / 100;
        return gold > 0 ? new(GreatPersonMissions.Trade, city.Id.ToString(CultureInfo.InvariantCulture), gold) : null;
    }

    /// <summary>What a trade mission's gold depends on besides the destination city: the owner's capital and the
    /// team's trade mission modifier, read on first use. One instance serves a person's offers at many cities while
    /// nothing writes.</summary>
    internal sealed class TradeTerms(Simulation sim, int player)
    {
        private bool _read;
        private City? _capital;
        private long _modifier;
        public City? Capital { get { Read(); return _capital; } }
        public long Modifier { get { Read(); return _modifier; } }
        private void Read()
        {
            if (_read) return;
            _capital = sim.State.Cities.Where(c => c.Owner == player).OrderBy(c => c.Id)
                .FirstOrDefault(c => GreatPeopleData.Active(sim, c).Any(b => b.Capital));
            _modifier = TeamTradeMissionModifier(sim, player);
            _read = true;
        }
    }
    private static long TeamTradeMissionModifier(Simulation sim, int player)
    {
        var team = sim.TeamOf(player);
        long techs = team.Techs.Order(StringComparer.Ordinal).Sum(t => (long)(sim.Content.Attachment<TechGreatPeopleDef>(t) ?? TechGreatPeopleDef.None).TradeMissionModifier);
        long buildings = sim.State.Cities.Where(c => sim.TeamOf(c.Owner).Id == team.Id)
            .Sum(c => GreatPeopleData.Active(sim, c).Sum(b => (long)GreatPeopleData.Building(sim.Content, b.Id).TradeMissionModifier));
        return techs + buildings;
    }
}
