using Civ.Content;
using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.World;

/// <summary>CvPlot::calculateNatureYield / calculateImprovementYieldChange / calculateYield.
/// Nature clamps before improvement changes; final yields clamp after modifier percentages.</summary>
internal sealed class PlotYieldService(IWaterService water) : IPlotYieldService
{
    public PlotYieldResult Calculate(Simulation sim, int player, TileId tile)
    {
        var plot = sim.State.Plots[tile.Value];
        var sink = new ModifierSink();
        if (Unworkable(sim, player, plot)) return new(default, sink.Ledger);
        AddNature(sim, player, plot, sink);
        var nature = Clamp(sink.FlatYield);
        sink.AddYield("world:nature_clamp", Difference(nature, sink.FlatYield));
        if (plot.Improvement is { } id) AddImprovement(sim, player, tile, sim.Content.Get<S.ImprovementDef>(id), sink);
        if (plot.Route is { } route) sink.AddYield(route, sim.Content.Get<S.RouteDef>(route).Yields);
        foreach (var source in sim.Content.Services.Get<IModifierSource>()) source.Contribute(new(sim, ModifierScope.Plot, player, Plot: tile), sink);
        AddThresholds(sim, sink);
        var total = Clamp(sink.FlatYield.WithPercent(sink.PercentYield));
        sink.Add("world:total", "world:food", total.Food);
        sink.Add("world:total", "world:production", total.Production);
        sink.Add("world:total", "world:commerce", total.Commerce);
        return new(total, sink.Ledger);
    }
    private static void AddThresholds(Simulation sim, ModifierSink sink)
    {
        long Extra(string yield, long value)
        {
            long threshold = sink.Ledger.Lines.Where(l => l.Key == "world:extra_" + yield + "_threshold" && l.Amount > 0)
                .Select(l => l.Amount).DefaultIfEmpty(long.MaxValue).Min();
            return value >= threshold ? int.Parse(sim.Content.Get<S.RuleConstantsDef>().All.Single().Raw.GetValueOrDefault("EXTRA_YIELD", "1"), System.Globalization.CultureInfo.InvariantCulture) : 0;
        }
        var flat = sink.FlatYield;
        sink.AddYield("peoples:yield_threshold", new(Extra("food", flat.Food), Extra("production", flat.Production), Extra("commerce", flat.Commerce)));
    }
    public PlotYieldResult CalculateNature(Simulation sim, int player, TileId tile, bool ignoreFeature = false)
    {
        var plot = sim.State.Plots[tile.Value];
        var sink = new ModifierSink();
        if (Unworkable(sim, player, plot)) return new(default, sink.Ledger);
        var natural = new Plot { Terrain = plot.Terrain, Kind = plot.Kind, IsLake = plot.IsLake, RiverEdges = plot.RiverEdges,
            Bonus = plot.Bonus, Feature = ignoreFeature ? null : plot.Feature };
        AddNature(sim, player, natural, sink);
        var total = Clamp(sink.FlatYield);
        sink.AddYield("world:nature_clamp", Difference(total, sink.FlatYield));
        return new(total, sink.Ledger);
    }
    private static bool Unworkable(Simulation sim, int player, Plot plot)
    {
        if (plot.Kind == PlotKind.Peak) return !sim.TeamOf(player).Techs.Any(t => sim.Content.Get<S.TechDef>(t).CanPassPeaks);
        return Geography.Impassable(sim.Content, plot);
    }
    private static void AddNature(Simulation sim, int player, Plot plot, ModifierSink sink)
    {
        var terrain = sim.Content.Get<S.TerrainDef>(plot.Terrain);
        sink.AddYield(terrain.Id, plot.Kind == PlotKind.Peak ? default : terrain.Yields);
        sink.AddYield("world:elevation", Elevation(sim.Content, plot));
        if (plot.Feature is { } id) sink.AddYield(id, sim.Content.Get<S.FeatureDef>(id).YieldChanges);
        if (RevealedBonus(sim, player, plot) is { } bonus) sink.AddYield(bonus.Id, bonus.YieldChanges);
        if (plot.RiverEdges == 0) return;
        var river = plot.Feature is { } feature ? sim.Content.Get<S.FeatureDef>(feature).RiverYieldChange : terrain.RiverYieldChange;
        // Native route synergy suppresses the river contribution per yield when it is at least as large.
        if (plot.Improvement is { } improvement && plot.Route is { } route)
            river = SuppressRiver(river, sim.Content.Get<S.ImprovementDef>(improvement).RouteYieldChanges.GetValueOrDefault(route));
        sink.AddYield("world:river", river);
    }
    private static YieldVector SuppressRiver(YieldVector river, YieldVector route) =>
        new(route.Food >= river.Food ? 0 : river.Food, route.Production >= river.Production ? 0 : river.Production, route.Commerce >= river.Commerce ? 0 : river.Commerce);
    private static YieldVector Elevation(ContentDb db, Plot plot)
    {
        long Change(string id)
        {
            var yield = db.Get<S.YieldDef>(id);
            int elevation = plot.Kind switch { PlotKind.Hills => yield.HillsChange, PlotKind.Peak => yield.PeakChange, _ => 0 };
            return elevation + (plot.IsLake ? yield.LakeChange : 0);
        }
        return new(Change("foundation:yield_food"), Change("foundation:yield_production"), Change("foundation:yield_commerce"));
    }
    private void AddImprovement(Simulation sim, int player, TileId tile, S.ImprovementDef improvement, ModifierSink sink)
    {
        var plot = sim.State.Plots[tile.Value];
        sink.AddYield(improvement.Id, improvement.YieldChanges);
        if (plot.RiverEdges != 0) sink.AddYield(improvement.Id + ":river", improvement.RiverSideYieldChange);
        if (water.IsIrrigated(sim, player, tile)) sink.AddYield(improvement.Id + ":irrigation", improvement.IrrigatedYieldChange);
        if (plot.Route is { } route) sink.AddYield(improvement.Id + ":route", improvement.RouteYieldChanges.GetValueOrDefault(route));
        if (RevealedBonus(sim, player, plot) is { } bonus && improvement.BonusEffects.TryGetValue(bonus.Id, out var effect))
            sink.AddYield(improvement.Id + ":bonus", effect.Yields);
        foreach (var tech in sim.TeamOf(player).Techs)
            if (improvement.TechYieldChanges.TryGetValue(tech, out var yields)) sink.AddYield(tech, yields);
    }
    internal static S.BonusDef? RevealedBonus(Simulation sim, int player, Plot plot)
    {
        if (plot.Bonus is not { } id) return null;
        var bonus = sim.Content.Get<S.BonusDef>(id);
        if (bonus.RevealTech is { } reveal && !sim.KnowsTech(player, reveal)) return null;
        if (bonus.ObsoleteTech is { } obsolete && sim.KnowsTech(player, obsolete)) return null;
        return bonus;
    }
    private static YieldVector Difference(YieldVector a, YieldVector b) => new(a.Food - b.Food, a.Production - b.Production, a.Commerce - b.Commerce);
    private static YieldVector Clamp(YieldVector y) => new(Math.Max(0, y.Food), Math.Max(0, y.Production), Math.Max(0, y.Commerce));
}
