using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.World;

internal sealed class ImprovementEligibility(IWaterService water)
{
    internal string? Validate(Simulation sim, int player, TileId tile, S.BuildDef build)
    {
        if (build.ImprovementType is not { } id) return null;
        var improvement = sim.Content.Get<S.ImprovementDef>(id);
        var plot = sim.State.Plots[tile.Value];
        return Existing(sim, plot, improvement) ?? Permission(sim, player, plot, improvement)
            ?? WaterRules(sim, player, tile, improvement) ?? ReliefRules(plot, improvement)
            ?? GroundRules(sim, player, tile, build, improvement);
    }
    private static string? Existing(Simulation sim, Plot plot, S.ImprovementDef improvement)
    {
        if (plot.Improvement == improvement.Id) return "Improvement already exists";
        return plot.Improvement is { } old && sim.Content.Get<S.ImprovementDef>(old).Permanent ? "Existing improvement is permanent" : null;
    }
    private static string? Permission(Simulation sim, int player, Plot plot, S.ImprovementDef improvement)
    {
        if (plot.Owner != player && !improvement.OutsideBorders) return "Improvement requires owned territory";
        if (improvement.PrereqTech is { } tech && !sim.KnowsTech(player, tech)) return "Improvement requires a technology";
        return Geography.Water(sim.Content, plot) != improvement.IsWater ? "Improvement domain does not match terrain" : null;
    }
    private string? WaterRules(Simulation sim, int player, TileId tile, S.ImprovementDef improvement)
    {
        if (improvement.NoFreshWater && water.IsFreshWater(sim, tile)) return "Improvement prohibits fresh water";
        if (!improvement.RequiresIrrigation || water.IsIrrigated(sim, player, tile)) return null;
        return sim.TeamOf(player).Techs.Any(t => sim.Content.Get<S.TechDef>(t).IgnoreIrrigation) ? null : "Improvement requires irrigation";
    }
    private static string? ReliefRules(Plot plot, S.ImprovementDef improvement)
    {
        if (improvement.RequiresRiverSide && plot.RiverEdges == 0) return "Improvement requires a river";
        if (improvement.RequiresFlatlands && plot.Kind != PlotKind.Flat) return "Improvement requires flatlands";
        return improvement.RequiresFeature && plot.Feature is null ? "Improvement requires a feature" : null;
    }
    private string? GroundRules(Simulation sim, int player, TileId tile, S.BuildDef build, S.ImprovementDef improvement)
    {
        var plot = sim.State.Plots[tile.Value];
        if (!ValidGround(plot, improvement, water.IsFreshWater(sim, tile))) return "Improvement is not valid on this terrain or feature";
        bool removes = plot.Feature is { } f && build.FeatureEffects.GetValueOrDefault(f)?.Remove == true;
        if (!removes && plot.Feature is { } feature && sim.Content.Get<S.FeatureDef>(feature).NoImprovement) return "Feature prohibits improvements";
        var nature = sim.Content.Services.Get<IPlotYieldService>().Single().CalculateNature(sim, player, tile, removes).Yields;
        var required = improvement.PrereqNatureYields;
        return nature.Food >= required.Food && nature.Production >= required.Production && nature.Commerce >= required.Commerce
            ? null : "Plot does not meet minimum natural yields";
    }
    private static bool ValidGround(Plot plot, S.ImprovementDef improvement, bool fresh) =>
        improvement.ValidTerrains.GetValueOrDefault(plot.Terrain) || FeatureOrBonus(plot, improvement) || LocationMakesValid(plot, improvement, fresh);
    private static bool FeatureOrBonus(Plot plot, S.ImprovementDef improvement) =>
        (plot.Feature is { } feature && improvement.ValidFeatures.GetValueOrDefault(feature))
        || (plot.Bonus is { } bonus && improvement.BonusEffects.GetValueOrDefault(bonus)?.MakesValid == true);
    private static bool LocationMakesValid(Plot plot, S.ImprovementDef improvement, bool fresh)
    {
        if (fresh && improvement.FreshWaterMakesValid) return true;
        if (plot.RiverEdges != 0 && improvement.RiverSideMakesValid) return true;
        return plot.Kind switch { PlotKind.Hills => improvement.HillsMakesValid, PlotKind.Peak => improvement.PeakMakesValid, _ => false };
    }
}
