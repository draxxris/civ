using Civ.Content;
using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.Military;

internal static class UnitUpgrades
{
    internal static long Price(Simulation sim, Unit unit, S.UnitDef target)
    {
        if (MilitaryData.Player(sim, unit.Owner).Barbarian) return 0;
        var constants = MilitaryData.Constants(sim);
        long difference = ProductionCost(sim, target) - ProductionCost(sim, MilitaryData.Definition(sim, unit));
        long price = constants.BaseUnitUpgradeCost + Math.Max(0, difference * constants.UnitUpgradeCostPerProduction / 100);
        if (sim.State.PlayerById(unit.Owner).Ai is not null)
        {
            var handicap = sim.Content.Get<S.HandicapDef>(sim.Settings.Handicap);
            price = price * handicap.AiUnitUpgradePercent / 100;
            var currentEra = sim.Content.Get<S.EraDef>(sim.State.PlayerById(unit.Owner).Extensions.Get<CityPlayerState>(sim.Content)?.CurrentEra ?? sim.Settings.StartingEra);
            int era = sim.Content.Get<S.EraDef>().All.Count(e => e.StartPercent < currentEra.StartPercent);
            price = price * Math.Max(0, 100 + handicap.AiPerEraModifier * era) / 100;
        }
        var effects = MilitaryData.Effects(sim, unit);
        price = price * Math.Max(0, 100 + MilitaryData.Effect(effects, "upgrade_price_percent")) / 100;
        int discount = Math.Clamp(MilitaryData.Effect(effects, "upgrade_discount"), 0, 100);
        price -= price * discount / 100;
        price = price * Math.Max(100, 100 + 5 * (MilitaryData.Read(sim, unit).Level - 4)) / 100;
        return Math.Max(0, price);
    }
    private static long ProductionCost(Simulation sim, S.UnitDef def)
    {
        long cost = def.Cost * (long)sim.Content.Get<S.GameSpeedDef>(sim.Settings.GameSpeed).TrainPercent / 100;
        return cost * sim.Content.Get<S.EraDef>(sim.Settings.StartingEra).TrainPercent / 100;
    }
    internal static string? Validate(Simulation sim, Unit unit, string targetId)
    {
        if (unit.MovesLeft <= 0) return "No moves left";
        if (!sim.Content.TryGet<S.UnitDef>(targetId, out var target)) return "Unknown unit type";
        if (target.Cost < 0) return "Unit cannot be trained";
        if (!Reachable(sim, MilitaryData.Definition(sim, unit), target.DefaultUnit, new(StringComparer.Ordinal))) return "No upgrade path";
        if (!Replacement(sim, unit.Owner, target)) return "Civilization has a different class replacement";
        if (!MilitaryData.Friendly(sim, unit.Owner, sim.State.PlotAt(unit.Position).Owner)) return "Upgrade requires friendly territory";
        if (!TrainingCity(sim, unit, targetId)) return "No eligible nearby training city";
        if (sim.State.PlayerById(unit.Owner).Treasury < Price(sim, unit, target)) return "Insufficient gold";
        return null;
    }
    private static bool Reachable(Simulation sim, S.UnitDef from, string targetClass, HashSet<string> visited)
    {
        if (!visited.Add(from.Id)) return false;
        foreach (var pair in from.Upgrades.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (!pair.Value) continue;
            var next = sim.Content.Get<S.UnitDef>(pair.Key);
            if (next.DefaultUnit == targetClass || Reachable(sim, next, targetClass, visited)) return true;
        }
        return false;
    }
    private static bool Replacement(Simulation sim, int player, S.UnitDef target)
    {
        var civId = sim.State.PlayerById(player).Civilization;
        if (civId.Length == 0) return target.Id == target.DefaultUnit;
        var civ = sim.Content.Get<S.CivilizationDef>(civId);
        return civ.UnitOverrides.GetValueOrDefault(target.DefaultUnit, target.DefaultUnit) == target.Id;
    }
    private static bool TrainingCity(Simulation sim, Unit unit, string target)
    {
        var service = sim.Content.Services.Get<ICityProductionService>().Single();
        return sim.State.Cities.Where(c => c.Owner == unit.Owner).OrderBy(c => c.Id).Any(c =>
            sim.State.Grid.Distance(c.Position, unit.Position) <= MilitaryData.Raw(sim, "MAX_UNIT_UPGRADE_CITY_DISTANCE", 3)
            && sim.State.PlotAt(c.Position).AreaId == sim.State.PlotAt(unit.Position).AreaId && service.Validate(sim, c.Id, "cities:unit", target).Success);
    }
    internal static void Apply(Simulation sim, Unit unit, string targetId)
    {
        var target = sim.Content.Get<S.UnitDef>(targetId);
        sim.State.PlayerById(unit.Owner).Treasury -= Price(sim, unit, target);
        var converted = new Unit { Id = unit.Id, Owner = unit.Owner, Type = targetId, Position = unit.Position,
            Experience = Math.Min(unit.Experience, MilitaryData.Constants(sim).MaxExperienceAfterUpgrade), Health = unit.Health,
            Promotions = new(unit.Promotions, StringComparer.Ordinal), Extensions = unit.Extensions, MovesLeft = 0 };
        MilitaryData.Edit(sim, converted).ExperienceRemainder = 0;
        sim.State.RemoveUnit(unit.Id); sim.State.AddUnit(converted);
        foreach (var p in target.FreePromotions.Where(p => p.Value).OrderBy(p => p.Key, StringComparer.Ordinal)) converted.Promotions.Add(p.Key);
        MilitaryData.Sight(sim);
    }
}
