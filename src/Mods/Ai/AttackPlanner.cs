using Ariel.Game;

namespace Ariel.Mods.Ai;

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
            if (!Ready(force, target)) { remaining.AddRange(force); continue; }
            Campaign(force, target);
        }
        return remaining;
    }

    internal const int MinimumForce = 3, CollateralCredit = 300_000, DefenderNeed = 800_000, ReserveNeed = 1_000_000, UnknownOdds = 500_000;

    /// <summary>A force launches when its expected wins clear the defending stack: each unit counts its success odds
    /// against the current best defender, and siege adds credit for collateral and bombard damage it deals first.</summary>
    public bool Ready(List<Unit> force, City target)
    {
        if (force.Count < MinimumForce) return false;
        int defenders = ctx.Sim.State.UnitsAt(target.Position).Count(u => ctx.IsEnemy(u.Owner) && u.Health > 0);
        return ExpectedWins(force, target.Position) >= (long)defenders * DefenderNeed + ReserveNeed;
    }

    /// <summary>Sum of success odds (millionths) of every unit against the tile's best defender, with collateral credit.</summary>
    public long ExpectedWins(IEnumerable<Unit> force, Hex target)
    {
        long total = 0;
        foreach (var unit in force)
        {
            var def = ctx.Def(unit);
            // Unseen defenders cannot be previewed: an unseen city counts as an even fight, a seen empty one as a sure win.
            total += orders.Odds(unit, target) ?? (ctx.Sim.State.PlotAt(target).Visible.Contains(ctx.Team.Id) ? 1_000_000 : UnknownOdds);
            if (def.CollateralDamage > 0 || def.BombardRate > 0) total += CollateralCredit;
        }
        return total;
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
        Assault(force, target.Position);
    }

    /// <summary>Attack one unit at a time, choosing again after every fight. Collateral siege goes first while more than
    /// one defender stands, then the unit with the best odds against whoever defends now. The assault stops when no
    /// remaining unit clears its odds threshold, so the rest of the stack is kept.</summary>
    public void Assault(List<Unit> force, Hex target)
    {
        var pending = force.ToList();
        while (true)
        {
            pending.RemoveAll(u => !ctx.Alive(u) || ctx.MilitaryState(u).Attacked || u.MovesLeft <= 0
                || ctx.Grid.Distance(u.Position, target) > 1);
            if (pending.Count == 0) return;
            int defenders = ctx.Sim.State.UnitsAt(target).Count(u => ctx.IsEnemy(u.Owner) && u.Health > 0);
            if (defenders == 0)
            {
                // Walk in with a unit that can hold the city.
                var taker = pending.OrderBy(u => ctx.Role(u) == UnitRole.Melee ? 0 : 1).ThenBy(u => u.Id).First();
                orders.Attack(taker, target);
                return;
            }
            var pick = pending.Select(u => (Unit: u, Odds: orders.Odds(u, target) ?? 0))
                .Where(x => x.Odds >= orders.ThresholdFor(x.Unit))
                .OrderByDescending(x => defenders > 1 && ctx.Def(x.Unit).CollateralDamage > 0)
                .ThenByDescending(x => x.Odds).ThenBy(x => x.Unit.Id).FirstOrDefault();
            if (pick.Unit is null) return;
            pending.Remove(pick.Unit);
            if (!orders.Attack(pick.Unit, target)) continue;
        }
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

    private Hex? AssaultTile(List<Unit> force, City target)
    {
        // Enemy strength next to a tile, not counting the city itself, then the tile's own defense, then distance.
        int garrison = ctx.ThreatAround(target.Position, 0);
        var tiles = ctx.Grid.Neighbors(ctx.Tile(target.Position)).Where(_targets.Land)
            .OrderBy(t => ctx.ThreatAround(ctx.Grid.HexOf(t), 1) - garrison).ThenByDescending(TileDefense)
            .ThenBy(t => force.Sum(u => ctx.Grid.Distance(u.Position, ctx.Grid.HexOf(t)))).ThenBy(t => t.Value).ToArray();
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
            .OrderBy(t => StagingCost(t, leader)).ThenBy(t => t.Value).ToArray();
        return FirstReachable(tiles, force, (u, hex) => u.Position == hex || ctx.FindPath(u, hex) is not null)
            ?? leader.Position;
    }

    /// <summary>Lower is better: exposure to enemy field units dominates, then defense against 15 points per hex of travel.</summary>
    private long StagingCost(TileId tile, Unit leader) => ctx.ThreatAround(ctx.Grid.HexOf(tile), 1) * 1000L - TileDefense(tile)
        + 15L * ctx.Grid.Distance(ctx.Tile(leader.Position), tile);

    /// <summary>Defense percent a unit gets from the tile: terrain or feature, plus hills.</summary>
    public int TileDefense(TileId tile)
    {
        var plot = ctx.Sim.State.Plots[tile.Value];
        int defense = plot.Feature is { } feature ? ctx.Sim.Content.Get<Ariel.Content.Schema.FeatureDef>(feature).DefensePercent
            : ctx.Sim.Content.Get<Ariel.Content.Schema.TerrainDef>(plot.Terrain).DefensePercent;
        return defense + (plot.Kind == PlotKind.Hills ? HillsDefense : 0);
    }

    internal const int HillsDefense = 25;
}
