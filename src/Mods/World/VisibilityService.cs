using System.Collections.Immutable;
using Ariel.Content;
using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.World;

internal sealed class VisibilityService : IVisibilityService, ITurnSystem
{
    public string Id => "world:visibility";
    public int Order => TurnPhase.Visibility;
    public void Run(Simulation sim) => Recompute(sim);
    public void Recompute(Simulation sim)
    {
        // Capture before clearing: visible edits since acquisition are still observations,
        // even when the observer has just moved, died, or lost its city.
        foreach (var tile in sim.State.Grid.Tiles)
        {
            var plot = sim.State.Plots[tile.Value];
            foreach (int team in plot.Visible) plot.RevealedSnapshots[team] = Capture(sim, tile, team);
            plot.Visible.Clear();
        }
        var tuning = Geography.Tuning(sim.Content);
        // Only sight and memory change here; terrain and team technology stay fixed.
        var levels = new RecomputeLevels(sim);
        foreach (var unit in sim.State.Units.OrderBy(u => u.Id)) RevealUnit(sim, unit, tuning.UnitSight, levels);
        foreach (var city in sim.State.Cities.OrderBy(c => c.Id))
            RevealTeam(sim, sim.TeamOf(city.Owner).Id, city.Position, tuning.CitySight, levels);
        if (sim.State.Extensions.Get<MilitaryGameState>(sim.Content) is { } military)
            foreach (var sight in military.ReconSights) RevealTeam(sim, sight.Team, sight.Center, sight.Radius, levels, aerial: true);
    }

    private void RevealUnit(Simulation sim, Unit unit, int range, RecomputeLevels levels)
    {
        var def = sim.Content.Get<S.UnitDef>(unit.Type);
        if (def.NoRevealMap || unit.Health <= 0) return;
        int radius = Math.Max(0, range + unit.Promotions.Sum(p => sim.Content.Get<S.PromotionDef>(p).VisibilityChange));
        RevealTeam(sim, sim.TeamOf(unit.Owner).Id, unit.Position, radius, levels, def.Domain == "military:domain_air");
    }

    public bool CanSeePlot(Simulation sim, TileId observer, TileId target, int team, int range, bool aerial = false) =>
        CanSeePlot(sim, observer, target, team, range, aerial, null);

    private static bool CanSeePlot(Simulation sim, TileId observer, TileId target, int team, int range,
        bool aerial, RecomputeLevels? levels)
    {
        if (range < 0) return false;
        var grid = sim.State.Grid;
        int distance = grid.Distance(observer, target);
        if (aerial) return distance <= range;
        if ((long)distance > (long)range + 1) return false;
        if (distance <= 1) return true;
        int level = SeeFromLevel(sim, observer, team, levels);
        bool outer = distance > range;
        return SightLine.Clear(grid, observer, target, (step, last) =>
        {
            int through = SeeThroughLevel(sim, step, levels);
            if (level < through) return false;
            return !outer || !last || level > through || SeeFromLevel(sim, target, team, levels) > level;
        });
    }

    private static int SeeFromLevel(Simulation sim, TileId tile, int team, RecomputeLevels? levels)
    {
        if (levels is not null) return levels.SeeFrom(tile, team);
        var (level, water) = SeeFromTerrainLevel(sim, tile);
        return level + (water && HasWaterSight(sim, team) ? 1 : 0);
    }

    private static (int Level, bool Water) SeeFromTerrainLevel(Simulation sim, TileId tile)
    {
        var plot = sim.State.Plots[tile.Value];
        var terrain = sim.Content.Get<S.TerrainDef>(plot.Terrain);
        int level = ElevationLevel(sim, plot, terrain.SeeFrom, "FROM");
        if (terrain.IsWater) level += WorldTurnMath.Raw(sim, "SEAWATER_SEE_FROM_CHANGE", 0);
        return (level, terrain.IsWater);
    }

    private static bool HasWaterSight(Simulation sim, int team) =>
        sim.State.TeamById(team).Techs.Any(t => sim.Content.Get<S.TechDef>(t).ExtraWaterSeeFrom);

    private static int SeeThroughLevel(Simulation sim, TileId tile, RecomputeLevels? levels = null)
    {
        if (levels is not null) return levels.SeeThrough(tile);
        var plot = sim.State.Plots[tile.Value];
        var terrain = sim.Content.Get<S.TerrainDef>(plot.Terrain);
        int level = ElevationLevel(sim, plot, terrain.SeeThrough, "THROUGH");
        if (plot.Feature is { } feature) level += sim.Content.Get<S.FeatureDef>(feature).SeeThrough;
        if (terrain.IsWater) level += WorldTurnMath.Raw(sim, "SEAWATER_SEE_THROUGH_CHANGE", 0);
        return level;
    }

    private sealed class RecomputeLevels(Simulation sim)
    {
        private readonly (int Level, bool Water)?[] _from = new (int, bool)?[sim.State.Plots.Length];
        private readonly int?[] _through = new int?[sim.State.Plots.Length];
        private readonly Dictionary<int, bool> _waterSight = new();

        internal int SeeFrom(TileId tile, int team)
        {
            var (level, water) = _from[tile.Value] ??= SeeFromTerrainLevel(sim, tile);
            if (!water) return level;
            if (!_waterSight.TryGetValue(team, out bool extra))
            {
                extra = HasWaterSight(sim, team);
                _waterSight.Add(team, extra);
            }
            return level + (extra ? 1 : 0);
        }

        internal int SeeThrough(TileId tile) => _through[tile.Value] ??= SeeThroughLevel(sim, tile);
    }

    private static int ElevationLevel(Simulation sim, Plot plot, int level, string direction) => plot.Kind switch
    {
        // Mountain terrain is a graphical placeholder with zero levels; use the land base.
        PlotKind.Peak => Math.Max(1, level) + WorldTurnMath.Raw(sim, "PEAK_SEE_" + direction + "_CHANGE", 2),
        PlotKind.Hills => level + WorldTurnMath.Raw(sim, "HILLS_SEE_" + direction + "_CHANGE", 1),
        _ => level,
    };

    public RevealedPlotSnapshot? SnapshotOf(Simulation sim, TileId tile, int team)
    {
        var plot = sim.State.Plots[tile.Value];
        if (plot.Visible.Contains(team)) return Capture(sim, tile, team);
        // Older saves can lack memory. Never manufacture an observation from unseen state.
        return plot.Revealed.Contains(team) ? plot.RevealedSnapshots.GetValueOrDefault(team) : null;
    }

    public void RevealPlot(Simulation sim, TileId tile, int team)
    {
        var plot = sim.State.Plots[tile.Value];
        plot.Revealed.Add(team);
        plot.RevealedSnapshots[team] = Capture(sim, tile, team);
    }

    private static RevealedPlotSnapshot Capture(Simulation sim, TileId tile, int team)
    {
        var plot = sim.State.Plots[tile.Value];
        var city = sim.State.CityAt(sim.State.Grid.HexOf(tile));
        var memory = city is null ? null : new RevealedCitySnapshot(city.Id, city.Owner, city.Name,
            city.Population, city.Buildings.ToImmutableArray());
        string? bonus = plot.Bonus;
        if (bonus is { } id && sim.Content.Get<S.BonusDef>(id).RevealTech is { } tech
            && !sim.State.TeamById(team).Techs.Contains(tech)) bonus = null;
        return new(plot.Terrain, plot.Kind, plot.IsLake, plot.Feature, bonus,
            plot.Improvement, plot.Route, plot.Owner, plot.RiverEdges, memory, plot.CliffEdges);
    }

    public bool IsUnitVisible(Simulation sim, int unitId, int observerTeam)
    {
        var unit = sim.State.FindUnit(unitId);
        if (unit is null || unit.Health <= 0) return false;
        if (sim.TeamOf(unit.Owner).Id == observerTeam) return true;
        if (!sim.State.PlotAt(unit.Position).Visible.Contains(observerTeam)) return false;
        if (unit.Extensions.Get<MilitaryUnitState>(sim.Content)?.TransportId is not null) return false;
        return Detected(sim, unit, observerTeam);
    }

    private static bool Detected(Simulation sim, Unit unit, int observerTeam)
    {
        var def = sim.Content.Get<S.UnitDef>(unit.Type);
        if (def.Invisibility is null && !def.Invisible) return true;
        return sim.State.Units.Any(detector => detector.Health > 0 && sim.TeamOf(detector.Owner).Id == observerTeam
            && Detects(sim, detector, unit, def));
    }

    private static bool Detects(Simulation sim, Unit detector, Unit target, S.UnitDef targetDef)
    {
        int distance = sim.State.Grid.Distance(detector.Position, target.Position);
        int sight = Math.Max(1, Geography.Tuning(sim.Content).UnitSight
            + detector.Promotions.Sum(p => sim.Content.Get<S.PromotionDef>(p).VisibilityChange));
        if (distance > sight) return false;
        var def = sim.Content.Get<S.UnitDef>(detector.Type);
        return targetDef.Invisibility is { } kind
            ? def.VisibleInvisibilityTypes.Contains(kind, StringComparer.Ordinal)
            : distance <= 1 && !def.OnlyDefensive;
    }

    private void RevealTeam(Simulation sim, int team, Hex position, int radius, RecomputeLevels levels, bool aerial = false)
    {
        var origin = sim.State.Grid.TileAt(position);
        foreach (var tile in sim.State.Grid.Range(origin, radius + (aerial ? 0 : 1)))
        {
            if (!CanSeePlot(sim, origin, tile, team, radius, aerial, levels)) continue;
            sim.State.Plots[tile.Value].Visible.Add(team);
            RevealPlot(sim, tile, team);
        }
    }
}
