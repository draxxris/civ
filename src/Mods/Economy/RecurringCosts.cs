using Civ.Content;
using Civ.Content.Schema;
using Civ.Game;
using CivicDef = Civ.Content.Schema.CivicDef;
using UnitDef = Civ.Content.Schema.UnitDef;

namespace Civ.Mods.Economy;

internal static class RecurringCosts
{
    internal static long Calculate(Simulation sim, int player, List<LedgerLine> lines)
    {
        var civics = EconomyData.Civics(sim, player);
        int population = EconomyData.Cities(sim, player).Sum(c => c.Population);
        long maintenanceHundredths = 0, totalCorporateHundredths = 0;
        foreach (var city in EconomyData.Cities(sim, player))
        {
            var cost = sim.Content.Services.Get<ICityMaintenanceService>().Single().Calculate(sim, city.Id);
            maintenanceHundredths += cost.TotalHundredths;
            totalCorporateHundredths += cost.CorporateHundredths;
            lines.Add(new("economy:city:" + city.Id, "economy:corporate_maintenance_hundredths", cost.CorporateHundredths));
            lines.Add(new("economy:city:" + city.Id, "economy:maintenance_hundredths", cost.TotalHundredths));
            lines.Add(new("economy:city:" + city.Id, "economy:distance_maintenance_hundredths", cost.DistanceHundredths));
            lines.Add(new("economy:city:" + city.Id, "economy:count_maintenance_hundredths", cost.CityCountHundredths));
        }
        long upkeep = CivicUpkeep(sim, player, civics, population, lines);
        long units = UnitUpkeep(sim, player, civics, population, lines);
        long supply = Supply(sim, player, civics, lines);
        long preInflated = maintenanceHundredths / 100 + units + supply;
        int inflation = Inflation.Rate(sim, player);
        long inflationCost = preInflated * Math.Max(0, 100 + inflation) / 100 - preInflated;
        // Civic upkeep already carries its own inflation, so the aggregate rate must not apply to it again.
        long total = preInflated + inflationCost + upkeep;
        lines.Add(new("economy:rules", "economy:maintenance", maintenanceHundredths / 100));
        lines.Add(new("economy:rules", "economy:corporate_maintenance", totalCorporateHundredths / 100));
        lines.Add(new("economy:rules", "economy:pre_inflated", preInflated));
        lines.Add(new("economy:rules", "economy:inflation_percent", inflation));
        lines.Add(new("economy:rules", "economy:inflation", inflationCost));

        return total;
    }
    private static long CivicUpkeep(Simulation sim, int player, CivicDef[] civics, int population, List<LedgerLine> lines)
    {
        if (GovernmentStatus.IsAnarchy(sim, player))
        {
            lines.Add(new("government:anarchy", "economy:civic_upkeep", 0));
            return 0;
        }
        long total = 0;
        var rules = EconomyData.Rules(sim).Values;
        int cities = EconomyData.Cities(sim, player).Count();
        long upkeepModifier = EconomyData.Effects(sim, player).Ledger.Total("economy:upkeep_percent");
        foreach (var civic in civics)
        {
            if (civic.Upkeep is null) continue;
            var upkeep = sim.Content.Get<UpkeepDef>(civic.Upkeep);
            long cost = Math.Max(0, (population + rules.UpkeepPopulationOffset) * upkeep.PopulationPercent / 100);
            cost += Math.Max(0, (cities + rules.UpkeepCityOffset) * upkeep.CityPercent / 100);
            // Native civic upkeep itself includes inflation, before the aggregate expense multiplier.
            cost = cost * Math.Max(0, 100 + Inflation.Rate(sim, player)) / 100;
            cost = cost * Math.Max(0, 100 + upkeepModifier) / 100;
            cost = cost * EconomyData.Handicap(sim).CivicUpkeepPercent / 100;
            cost = EconomyData.AiScale(sim, player, cost, EconomyData.Handicap(sim).AiCivicUpkeepPercent);
            cost = cost * EconomyData.Speed(sim).GoldModifier / 100;
            total += cost;
            lines.Add(new(civic.Id, "economy:civic_upkeep", cost));
        }
        return total;
    }
    private static long UnitUpkeep(Simulation sim, int player, CivicDef[] civics, int population, List<LedgerLine> lines)
    {
        var rules = EconomyData.Rules(sim).Values;
        var handicap = EconomyData.Handicap(sim);
        var units = sim.State.Units.Where(u => u.Owner == player).Select(u => sim.Content.Get<UnitDef>(u.Type)).ToArray();
        long free = handicap.FreeUnits + rules.InitialFreeUnits + civics.Sum(c => c.BaseFreeUnits)
            + population * (rules.InitialFreeUnitsPopulationPercent + civics.Sum(c => c.FreeUnitsPopulationPercent)) / 100;
        long freeMilitary = EconomyData.Raw(sim, "INITIAL_BASE_FREE_MILITARY_UNITS", 0) + civics.Sum(c => c.BaseFreeMilitaryUnits)
            + population * (EconomyData.Raw(sim, "INITIAL_FREE_MILITARY_UNITS_POPULATION_PERCENT", 0) + civics.Sum(c => c.FreeMilitaryUnitsPopulationPercent)) / 100;
        long paid = Math.Max(0, units.Length - free);
        long paidMilitary = Math.Max(0, units.Count(u => u.MilitarySupport) - freeMilitary);
        long cost = paid * Math.Max(0, EconomyData.Raw(sim, "INITIAL_GOLD_PER_UNIT", 1) + civics.Sum(c => c.GoldPerUnit));
        cost += paidMilitary * Math.Max(0, EconomyData.Raw(sim, "INITIAL_GOLD_PER_MILITARY_UNIT", 0) + civics.Sum(c => c.GoldPerMilitaryUnit));
        cost += units.Sum(u => u.ExtraCost);
        cost = cost * handicap.UnitCostPercent / 100;
        cost = EconomyData.AiScale(sim, player, cost, handicap.AiUnitCostPercent);
        cost = cost * EconomyData.Speed(sim).GoldModifier / 100;
        lines.Add(new("economy:units", "economy:free_units", free));
        lines.Add(new("economy:units", "economy:paid_units", paid));
        lines.Add(new("economy:units", "economy:free_military_units", freeMilitary));
        lines.Add(new("economy:units", "economy:paid_military_units", paidMilitary));
        lines.Add(new("economy:units", "economy:unit_upkeep", cost));
        return cost;
    }
    private static long Supply(Simulation sim, int player, CivicDef[] civics, List<LedgerLine> lines)
    {
        int team = sim.State.PlayerById(player).TeamId;
        int outside = sim.State.Units.Count(u => u.Owner == player && !Friendly(sim, u, team));
        int paid = Math.Max(0, outside - EconomyData.Raw(sim, "INITIAL_FREE_OUTSIDE_UNITS", 0));
        long cost = paid * EconomyData.Raw(sim, "INITIAL_OUTSIDE_UNIT_GOLD_PERCENT", 50) / 100;
        cost = cost * Math.Max(0, 100 + civics.Sum(c => c.DistantUnitSupportCostModifier)) / 100;
        cost = EconomyData.AiScale(sim, player, cost, EconomyData.Handicap(sim).AiUnitSupplyPercent);
        cost = cost * EconomyData.Speed(sim).GoldModifier / 100;
        lines.Add(new("economy:supply", "economy:outside_units", outside));
        lines.Add(new("economy:supply", "economy:unit_supply", cost));
        return cost;
    }
    private static bool Friendly(Simulation sim, Unit unit, int team) => sim.State.PlotAt(unit.Position).Owner is { } owner
        && sim.State.PlayerById(owner).TeamId == team;
}

internal static class Inflation
{
    internal static int Rate(Simulation sim, int player) => EconomyData.Raw(sim, "DISABLE_INFLATION", 0) != 0 ? 0
        : (int)((EconomyData.Read(sim, player).AccruedCostRatioTimes10000 - 10000) / 100);
    internal static void Update(Simulation sim, int player)
    {
        var speed = EconomyData.Speed(sim);
        var civics = EconomyData.Civics(sim, player);
        var state = EconomyData.Edit(sim, player);
        int hurryInflationMod = civics.Sum(c => c.HurryInflationModifier);
        long decayInterval = Math.Max(1, (long)EconomyData.Raw(sim, "HURRY_INFLATION_DECAY_RATE", 0)
            * speed.TrainPercent / 100 * Math.Max(0, 100 + hurryInflationMod) / 100);
        long elapsedTurns = Math.Max(0, sim.State.Turn - 1);
        bool decayHurryCount = state.HurriedCount > 0 && elapsedTurns % decayInterval == 0;
        if (EconomyData.Raw(sim, "DISABLE_INFLATION", 0) != 0)
        {
            if (decayHurryCount) state.HurriedCount--;
            return;
        }
        var handicap = EconomyData.Handicap(sim);
        long perTurn = speed.InflationPercent * handicap.InflationPercent;
        perTurn = perTurn * Math.Max(0, 100 + state.HurriedCount) / 100;
        long modifier = EconomyData.Effects(sim, player).Ledger.Total("economy:inflation_percent");
        if (sim.State.PlayerById(player).Ai is not null)
            modifier += handicap.AiInflationPercent * Math.Max(0, 100 + handicap.AiPerEraModifier * EconomyData.EraIndex(sim, player)) / 100 - 100;
        perTurn = perTurn * Math.Max(0, 100 + modifier) / 100;
        perTurn = perTurn * Math.Max(0, 100 + civics.Sum(c => c.Inflation)) / 100;
        if (decayHurryCount) state.HurriedCount--;
        perTurn /= 100;
        // Engine turn one represents elapsed turn zero. No maximum-turn option exists yet.
        long turns = Math.Clamp(sim.State.Turn - 1 + speed.InflationOffset, 0, EconomyData.Raw(sim, "MAX_INFLATION_EFFECT_WINDOW", 200));
        long equilibrium = 10000 + turns * perTurn + turns * (turns - 1) * perTurn * perTurn / 20000;
        equilibrium = equilibrium / 100 * 100; // Native equilibrium modifier drops fractional percent.
        int momentum = Math.Max(1, EconomyData.Raw(sim, "INFLATION_RATE_MOMENTUM", 50) * speed.ImprovementPercent / 100);
        long difference = equilibrium - state.AccruedCostRatioTimes10000;
        state.AccruedCostRatioTimes10000 += (difference + Math.Sign(difference) * (momentum - 1)) / momentum;
    }
}
