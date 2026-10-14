using Civ.Content.Schema;
using Civ.Game;

namespace Civ.Mods.Ai;

/// <summary>Exploration: reveal the map from the nearest frontier, and walk onto goody huts.</summary>
internal sealed class ExplorerPlanner(AiContext ctx)
{
    private const int Range = 12;
    private readonly List<TileId> _targets = new();

    /// <summary>Explore with this unit. Returns false when nothing is left to explore.</summary>
    public bool Explore(Unit unit)
    {
        if (unit.MovesLeft <= 0) return true;
        var area = ctx.Sim.State.PlotAt(unit.Position).AreaId;
        var here = ctx.Grid.TileAt(unit.Position);
        var candidates = ctx.Grid.Range(here, Range).Where(t => t != here && Interesting(t, area) && _targets.All(x => ctx.Grid.Distance(x, t) > 2))
            .OrderBy(t => Priority(t) + ctx.Grid.Distance(here, t) * 10).ThenBy(t => t.Value).Take(4).ToArray();
        foreach (var tile in candidates)
        {
            _targets.Add(tile);
            if (ctx.MoveToward(unit, ctx.Grid.HexOf(tile))) return true;
        }
        return false;
    }

    private int Priority(TileId tile) => IsGoody(tile) ? -40 : 0;

    private bool IsGoody(TileId tile) => ctx.Sim.State.Plots[tile.Value].Improvement is { } id && ctx.Sim.Content.Get<ImprovementDef>(id).Goody;

    private bool Interesting(TileId tile, int area)
    {
        var plot = ctx.Sim.State.Plots[tile.Value];
        if (plot.AreaId != area || !plot.Revealed.Contains(ctx.Team.Id)) return false;
        if (plot.Owner is { } owner && owner != ctx.PlayerId && !ctx.IsEnemy(owner)) return false;
        if (IsGoody(tile)) return true;
        return ctx.Grid.Neighbors(tile).Any(n => !ctx.Sim.State.Plots[n.Value].Revealed.Contains(ctx.Team.Id));
    }
}
