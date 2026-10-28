using Ariel.Game;

namespace Ariel.Mods.Military;

internal static class UnitHealing
{
    internal static int Rate(Simulation sim, Unit unit)
    {
        var plot = sim.State.PlotAt(unit.Position);
        var constants = MilitaryData.Constants(sim);
        var sink = MilitaryData.Effects(sim, unit);
        var city = sim.State.CityAt(unit.Position);
        int rate;
        if (city is not null && MilitaryData.Friendly(sim, unit.Owner, city.Owner))
            rate = constants.CityHealRate + MilitaryData.Effect(sink, "friendly_heal") + MilitaryData.Buildings(sim, city).Sum(b => b.HealRateChange);
        else if (MilitaryData.Friendly(sim, unit.Owner, plot.Owner)) rate = constants.FriendlyHealRate + MilitaryData.Effect(sink, "friendly_heal");
        else if (plot.Owner is { } owner && MilitaryData.Enemy(sim, unit.Owner, owner)) rate = constants.EnemyHealRate + MilitaryData.Effect(sink, "enemy_heal");
        else rate = constants.NeutralHealRate + MilitaryData.Effect(sink, "neutral_heal");
        return Math.Max(0, rate + Medic(sim, unit));
    }
    private static int Medic(Simulation sim, Unit unit)
    {
        int best = 0;
        var plot = sim.State.PlotAt(unit.Position);
        foreach (var other in sim.State.UnitsAt(unit.Position).Where(u => MilitaryData.Friendly(sim, unit.Owner, u.Owner)))
            best = Math.Max(best, MilitaryData.Effect(MilitaryData.Effects(sim, other), "same_tile_heal"));
        foreach (var tile in sim.State.Grid.Neighbors(sim.State.Grid.TileAt(unit.Position)))
        {
            if (sim.State.Plots[tile.Value].AreaId != plot.AreaId) continue;
            foreach (var other in sim.State.UnitsAt(sim.State.Grid.HexOf(tile)).Where(u => MilitaryData.Friendly(sim, unit.Owner, u.Owner)))
                best = Math.Max(best, MilitaryData.Effect(MilitaryData.Effects(sim, other), "adjacent_heal"));
        }
        return best;
    }
}
