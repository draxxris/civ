using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.World;

/// <summary>Single-feature disappearance and adjacent growth, in tile then ordinal feature order.</summary>
internal sealed class FeatureTurnRules(Simulation sim, HashSet<TileId> worked)
{
    private readonly int _odds = WorldTurnMath.Range((Int128)10000
        * sim.Content.Get<S.GameSpeedDef>(sim.Settings.GameSpeed).VictoryDelayPercent / 100);

    internal void Advance(TileId tile)
    {
        var plot = sim.State.Plots[tile.Value];
        if (plot.Feature is { } current)
        {
            int disappearance = sim.Content.Get<S.FeatureDef>(current).Disappearance;
            if (disappearance > 0 && sim.Rng.Next(_odds) < disappearance) Change(tile, current, null);
            return; // A disappeared feature cannot regrow in the same turn.
        }
        if (sim.State.CityAt(sim.State.Grid.HexOf(tile)) is not null) return;
        foreach (var feature in sim.Content.Get<S.FeatureDef>().All)
        {
            if (!CanGrow(tile, plot, feature)) continue;
            Int128 probability = GrowthProbability(tile, plot, feature);
            if (probability <= 0 || sim.Rng.Next(_odds) >= probability) continue;
            Change(tile, null, feature.Id);
            break;
        }
    }

    private bool CanGrow(TileId tile, Plot plot, S.FeatureDef feature)
    {
        bool grows = feature.Growth > 0 && !sim.State.UnitsAt(sim.State.Grid.HexOf(tile)).Any();
        if (!grows && feature.Spread <= 0) return false;
        if (!CanOvergrow(tile, plot, feature)) return false;
        if (!Placement.FeatureValid(sim.State.Grid, sim.State.Plots, sim.Content, tile, feature)) return false;
        return plot.Bonus is null || feature.Spread > 0
            || sim.Content.Get<S.BonusDef>(plot.Bonus).Features.GetValueOrDefault(feature.Id);
    }

    private bool CanOvergrow(TileId tile, Plot plot, S.FeatureDef feature) => plot.Improvement is null
        || (feature.CanGrowAnywhere && !worked.Contains(tile) && !Geography.Water(sim.Content, plot));

    private Int128 GrowthProbability(TileId tile, Plot plot, S.FeatureDef feature)
    {
        Int128 probability = feature.CanGrowAnywhere ? feature.Growth : 0;
        foreach (var neighbor in sim.State.Grid.Neighbors(tile))
        {
            var adjacent = sim.State.Plots[neighbor.Value];
            if (adjacent.Feature != feature.Id) continue;
            probability += adjacent.Improvement is { } improvement
                ? sim.Content.Get<S.ImprovementDef>(improvement).FeatureGrowth : feature.Growth;
            probability += feature.Spread;
        }
        probability = probability * Int128.Max(0, (Int128)100 + WorldTurnMath.Raw(sim, "FEATURE_GROWTH_MODIFIER", 25)) / 100;
        if (plot.Route is not null)
            probability = probability * Int128.Max(0, (Int128)100 + WorldTurnMath.Raw(sim, "ROUTE_FEATURE_GROWTH_MODIFIER", -25)) / 100;
        return probability;
    }

    private void Change(TileId tile, string? previous, string? next)
    {
        sim.State.Plots[tile.Value].Feature = next;
        sim.Events.Publish(new FeatureChanged(tile, previous, next));
    }
}
