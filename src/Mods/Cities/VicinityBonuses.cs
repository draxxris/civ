using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Cities;

/// <summary>Shared requirement and yield predicate. The caller supplies the nonrecursive city bonus ledger.</summary>
internal static class VicinityBonuses
{
    internal static bool Has(Simulation sim, City city, string bonus, IReadOnlyDictionary<string, int> available, int requiredCount = 1)
    {
        if (available.GetValueOrDefault(bonus) <= 0 || !Known(sim, city.Owner, bonus)) return false;
        int count = CityData.Active(sim, city).Sum(b => BuildingEffects.FreeBonusCount(sim, b, bonus));
        var center = CityData.Tile(sim, city);
        if (sim.State.Plots[center.Value].Bonus == bonus) count++;
        if (count >= requiredCount) return true;
        var connected = sim.Content.Services.Get<IResourceNetwork>().Single().ConnectedTiles(sim, city.Owner, city.Id);
        foreach (var tile in sim.Content.Services.Get<ICityWorkService>().Single().WorkArea(sim, city.Id))
        {
            if (tile == center || !connected.Contains(tile)) continue;
            if (ValidPlot(sim, city, tile, bonus)) count++;
            if (count >= requiredCount) return true;
        }
        return false;
    }
    private static bool Known(Simulation sim, int player, string bonus)
    {
        var def = sim.Content.Get<S.BonusDef>(bonus);
        if (def.RevealTech is { } reveal && !sim.KnowsTech(player, reveal)) return false;
        return def.ObsoleteTech is not { } obsolete || !sim.KnowsTech(player, obsolete);
    }
    private static bool ValidPlot(Simulation sim, City city, TileId tile, string bonus)
    {
        var plot = sim.State.Plots[tile.Value];
        if (plot.Owner != city.Owner || plot.Bonus != bonus) return false;
        return plot.Improvement is { } id && sim.Content.Get<S.ImprovementDef>(id).BonusEffects.GetValueOrDefault(bonus)?.MakesValid == true;
    }
}
