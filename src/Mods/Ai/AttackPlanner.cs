using Civ.Game;

namespace Civ.Mods.Ai;

/// <summary>One army per landmass. Gather first, advance at the slowest unit's pace, then prepare and assault.</summary>
internal sealed class AttackPlanner(AiContext ctx, UnitOrders orders)
{
    private const int ParallelTiles = 2;
    private readonly CampaignTargets _targets = new(ctx);

    public List<Unit> Plan(List<Unit> free, IReadOnlyList<CityView> views)
    {
        var remaining = new List<Unit>();
        var theaters = free.GroupBy(u => _targets.Area(u.Position)).OrderBy(g => g.Key).ToArray();
        if (theaters.Length > 0 && (ctx.EnemyCities.Count > 0 || AiIntent.Read(ctx).Campaigns.Count > 0))
            ctx.Issue(new ReviewCampaignGoalsCommand { Player = ctx.PlayerId, Areas = theaters.Select(g => g.Key).ToArray() });
        foreach (var theater in theaters)
        {
            var force = theater.OrderBy(u => u.Id).ToList();
            if (_targets.SelectPrimaryTargetCity(theater.Key) is not { } target) { remaining.AddRange(force); continue; }
            int defenders = ctx.Sim.State.UnitsAt(target.Position).Count(u => ctx.IsEnemy(u.Owner));
            if (force.Count < Math.Max(3, defenders + 2)) { remaining.AddRange(force); continue; }
            Campaign(force, target);
        }
        return remaining;
    }

    private void Campaign(List<Unit> force, City target)
    {
        var stage = StagingTile(force, target);
        foreach (var unit in force) ctx.Done.Add(unit.Id);
        // Units already at the front wait for the rear and for slow siege pieces.
        if (force.Any(u => ctx.Grid.Distance(u.Position, target.Position) > 2))
        {
            foreach (var unit in force.Where(u => u.Position != stage)) ctx.MoveToward(unit, stage);
            Prepare(target.Position);
            return;
        }
        var assault = AssaultTile(force, target);
        if (assault is null)
        {
            foreach (var unit in force.Where(u => u.Position != stage)) ctx.MoveToward(unit, stage);
            Prepare(target.Position);
            return;
        }
        foreach (var unit in force.Where(u => u.Position != assault.Value)) ctx.MoveToward(unit, assault.Value);
        Prepare(target.Position);
        if (force.Any(u => ctx.Role(u) == UnitRole.Melee && u.MovesLeft <= 0)) return;
        foreach (var unit in force.OrderBy(u => ctx.Role(u) == UnitRole.Siege ? 0 : 1).ThenBy(u => u.Id))
            Engage(unit, target.Position);
    }

    /// <summary>All legal support fire is resolved before any close combat, including opportunistic stack attacks.</summary>
    public void Prepare(Hex target)
    {
        var support = ctx.Units.Where(u => ctx.Alive(u) && u.MovesLeft > 0 && ctx.MilitaryState(u).TransportId is null)
            .OrderBy(u => u.Id).ToArray();
        foreach (var unit in support.Where(u => ctx.Def(u).BombardRate > 0))
            Fire(unit, new BombardCityCommand { Player = ctx.PlayerId, Unit = unit.Id, Target = target });
        foreach (var unit in support.Where(u => ctx.Def(u).BombRange > 0))
            Fire(unit, new RangedStrikeCommand { Player = ctx.PlayerId, Unit = unit.Id, Target = target });
    }

    private void Fire(Unit unit, GameCommand command)
    {
        if (ctx.Issue(command)) ctx.Done.Add(unit.Id);
    }

    private void Engage(Unit unit, Hex target)
    {
        if (!ctx.Alive(unit) || ctx.MilitaryState(unit).Attacked) return;
        var odds = orders.Odds(unit, target);
        if (odds is null || odds >= orders.ThresholdFor(unit)) orders.Attack(unit, target);
    }

    private Hex? AssaultTile(List<Unit> force, City target)
    {
        var tiles = ctx.Grid.Neighbors(ctx.Tile(target.Position)).Where(_targets.Land)
            .OrderBy(t => force.Sum(u => ctx.Grid.Distance(u.Position, ctx.Grid.HexOf(t)))).ThenBy(t => t.Value).ToArray();
        return FirstReachable(tiles, force, CanReachThisTurn);
    }

    /// <summary>The first tile in the given order that every unit of the force can reach. Tiles are checked in parallel;
    /// the selection still follows the original order.</summary>
    private Hex? FirstReachable(TileId[] tiles, List<Unit> force, Func<Unit, Hex, bool> reaches)
    {
        var hexes = tiles.Select(ctx.Grid.HexOf).ToArray();
        int threshold = force.Count > 1 ? ParallelTiles : int.MaxValue;
        var reachable = ParallelScoring.Select(ctx.Sim, hexes, threshold, hex => force.All(u => reaches(u, hex)));
        int index = Array.IndexOf(reachable, true);
        return index < 0 ? null : hexes[index];
    }

    private bool CanReachThisTurn(Unit unit, Hex hex)
    {
        if (unit.Position == hex) return true;
        var path = ctx.FindPath(unit, hex);
        // A slow unit may finish beside the city; the wave then waits until the next turn.
        return path is not null && path.Cost <= unit.MovesLeft;
    }

    private Hex StagingTile(List<Unit> force, City target)
    {
        var leader = force[0];
        var tiles = ctx.Grid.Ring(ctx.Tile(target.Position), 2).Where(_targets.Land)
            .Where(t => ctx.Sim.State.Plots[t.Value].AreaId == _targets.Area(leader.Position))
            .OrderBy(t => ctx.Grid.Distance(ctx.Tile(leader.Position), t)).ThenBy(t => t.Value).ToArray();
        return FirstReachable(tiles, force, (u, hex) => u.Position == hex || ctx.FindPath(u, hex) is not null)
            ?? leader.Position;
    }
}
