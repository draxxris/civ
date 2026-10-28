using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.World;

internal sealed class BuildEligibility(IWaterService water)
{
    private readonly ImprovementEligibility _improvements = new(water);
    internal string? Validate(Simulation sim, int player, int unitId, string buildId, bool continuing = false)
    {
        var unit = sim.State.FindUnit(unitId);
        if (unit is null || unit.Owner != player) return "Worker is not owned by this player";
        return ValidateAt(sim, player, unitId, sim.State.Grid.TileAt(unit.Position), buildId, continuing);
    }
    internal string? ValidateAt(Simulation sim, int player, int unitId, TileId tile, string buildId, bool continuing = false)
    {
        var unit = sim.State.FindUnit(unitId);
        if (unit is null || unit.Owner != player) return "Worker is not owned by this player";
        if (tile.Value < 0 || tile.Value >= sim.State.Plots.Length) return "Off map";
        if (!sim.Content.TryGet<S.BuildDef>(buildId, out var build)) return "Unknown build";
        return Worker(sim, unit, build, continuing) ?? Prerequisites(sim, unit, tile, build)
            ?? Location(sim, player, tile, build);
    }
    private static string? Worker(Simulation sim, Unit unit, S.BuildDef build, bool continuing)
    {
        var player = sim.State.PlayerById(unit.Owner);
        if (!continuing && player.EndedTurn) return "Player has ended the turn";
        if (!continuing && player.Treasury < build.Cost) return "Not enough gold";
        var definition = sim.Content.Get<S.UnitDef>(unit.Type);
        return definition.WorkRate <= 0 || !definition.Builds.GetValueOrDefault(build.Id) ? "Unit cannot perform this build" : null;
    }
    private static string? Prerequisites(Simulation sim, Unit unit, TileId tile, S.BuildDef build)
    {
        var context = new Ariel.Content.RequirementContext(sim, unit.Owner, Plot: tile, Unit: unit);
        if (!sim.Content.Requirements.Evaluate(build.Requirements, context).Success) return "Build requirements are not satisfied";
        if (build.ObsoleteTech is { } obsolete && sim.KnowsTech(unit.Owner, obsolete)) return "Build is obsolete";
        var plot = sim.State.Plots[tile.Value];
        if (plot.Feature is not { } f || !build.FeatureEffects.TryGetValue(f, out var effect)) return null;
        return effect.AllowRemovalWithoutProduction || sim.Content.Requirements.Evaluate(effect.Requirements, context).Success
            ? null : "Feature removal requirements are not satisfied";
    }
    private string? Location(Simulation sim, int player, TileId tile, S.BuildDef build)
    {
        var plot = sim.State.Plots[tile.Value];
        if (plot.Owner is { } owner && owner != player) return "Cannot build in foreign territory";
        if (plot.DepletedMine && build.Mine) return "Mine is depleted on this plot";
        if (Unreachable(sim, player, plot)) return "Plot is impassable";
        if (sim.State.CityAt(sim.State.Grid.HexOf(tile)) is not null && build.ImprovementType is not null) return "Cannot improve a city plot";
        return Route(sim, plot, build) ?? _improvements.Validate(sim, player, tile, build) ?? PlotChange(sim, tile, build);
    }
    private static bool Unreachable(Simulation sim, int player, Plot plot) => plot.Kind == PlotKind.Peak
        ? !sim.TeamOf(player).Techs.Any(t => sim.Content.Get<S.TechDef>(t).CanPassPeaks)
        : Geography.Impassable(sim.Content, plot);
    private static string? Route(Simulation sim, Plot plot, S.BuildDef build)
    {
        if (build.RouteType is not { } route) return null;
        var next = sim.Content.Get<S.RouteDef>(route);
        if (Geography.Water(sim.Content, plot) != next.SeaTunnel) return "Route is not valid on this terrain";
        return plot.Route is { } old && sim.Content.Get<S.RouteDef>(old).Value >= next.Value ? "An equal or better route already exists" : null;
    }
    private static string? PlotChange(Simulation sim, TileId tile, S.BuildDef build)
    {
        var plot = sim.State.Plots[tile.Value];
        if (build.TerrainChange is { } terrain && terrain == plot.Terrain) return "Terrain already matches";
        if (build.FeatureChange is { } feature) return NewFeature(sim, tile, feature);
        bool removes = plot.Feature is { } f && build.FeatureEffects.GetValueOrDefault(f)?.Remove == true;
        bool changes = build.RouteType is not null || build.ImprovementType is not null || build.TerrainChange is not null;
        return changes || removes ? null : "Build has no effect here";
    }
    private static string? NewFeature(Simulation sim, TileId tile, string feature) =>
        sim.State.Plots[tile.Value].Feature != feature && Placement.FeatureValid(sim.State.Grid, sim.State.Plots, sim.Content, tile, sim.Content.Get<S.FeatureDef>(feature))
            ? null : "Feature is not valid here";
}
