using Civ.Game;

namespace Civ.Mods.Ai;

/// <summary>Offensive operations: gather free soldiers, stage two tiles from the nearest enemy city, then
/// attack in waves (siege first) while respecting odds thresholds. Stateless: every turn recomputes the same plan.</summary>
internal sealed class AttackPlanner(AiContext ctx, UnitOrders orders)
{
    private const int AdvanceRange = 3;

    public List<Unit> Plan(List<Unit> free, IReadOnlyList<CityView> views)
    {
        if (free.Count == 0 || TargetCity() is not { } target) return free;
        int defenders = ctx.Sim.State.UnitsAt(target.Position).Count(u => ctx.IsEnemy(u.Owner));
        if (free.Count < Math.Max(3, defenders + 2)) return free;
        var stage = StagingTile(target);
        int near = free.Count(u => ctx.Grid.Distance(u.Position, target.Position) <= AdvanceRange);
        foreach (var unit in free.OrderBy(u => ctx.Role(u) == UnitRole.Siege ? 0 : 1).ThenBy(u => u.Id))
        {
            ctx.Done.Add(unit.Id);
            if (!ctx.Alive(unit)) continue;
            Order(unit, target, stage, near * 10 >= free.Count * 6);
        }
        return [];
    }

    private void Order(Unit unit, City target, Hex stage, bool assembled)
    {
        int distance = ctx.Grid.Distance(unit.Position, target.Position);
        if (distance <= 1) Engage(unit, target);
        else if (distance <= AdvanceRange && assembled)
        {
            orders.Approach(unit, target.Position);
            if (ctx.Alive(unit) && ctx.Grid.Distance(unit.Position, target.Position) <= 1) Engage(unit, target);
        }
        else if (unit.Position != stage) ctx.MoveToward(unit, stage);
    }

    private void Engage(Unit unit, City target)
    {
        var odds = orders.Odds(unit, target.Position);
        int threshold = orders.ThresholdFor(unit);
        if (odds is null || odds >= threshold) orders.Attack(unit, target.Position);
    }

    /// <summary>Nearest enemy city (any owner at war with us, barbarians included) on a continent we hold a city on.</summary>
    public City? TargetCity()
    {
        var areas = ctx.Cities.Select(c => ctx.Sim.State.PlotAt(c.Position).AreaId).ToHashSet();
        return ctx.Sim.State.Cities.Where(c => ctx.IsEnemy(c.Owner) && areas.Contains(ctx.Sim.State.PlotAt(c.Position).AreaId))
            .OrderBy(c => ctx.Cities.Min(own => ctx.Grid.Distance(own.Position, c.Position))).ThenBy(c => c.Id).FirstOrDefault();
    }

    /// <summary>Walkable tile on our nearest city's land area, two steps from the target, closest to that city.</summary>
    private Hex StagingTile(City target)
    {
        var home = ctx.NearestCity(target.Position)!;
        int area = ctx.Sim.State.PlotAt(home.Position).AreaId;
        // Offshore ring tiles cannot be reached by land from home, so the army stages on home's landmass.
        var ring = ctx.Grid.Ring(ctx.Grid.TileAt(target.Position), 2)
            .Where(t => Walkable(t) && ctx.Sim.State.Plots[t.Value].AreaId == area).ToList();
        if (ring.Count == 0) return home.Position;
        return ctx.Grid.HexOf(ring.OrderBy(t => ctx.Grid.Distance(t, ctx.Grid.TileAt(home.Position))).ThenBy(t => t.Value).First());
    }

    private bool Walkable(TileId tile)
    {
        var plot = ctx.Sim.State.Plots[tile.Value];
        var terrain = ctx.Sim.Content.Get<Civ.Content.Schema.TerrainDef>(plot.Terrain);
        return !terrain.IsWater && !terrain.Impassable && plot.Kind != PlotKind.Peak;
    }
}
