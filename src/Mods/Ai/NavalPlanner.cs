using Civ.Game;

namespace Civ.Mods.Ai;

/// <summary>Coastal reconnaissance, warship support and transport pickup/delivery through normal commands.</summary>
internal sealed class NavalPlanner(AiContext ctx, UnitOrders orders, AttackPlanner attack)
{
    private readonly CampaignTargets _targets = new(ctx);
    private readonly ExplorerPlanner _explorer = new(ctx);
    private readonly HashSet<int> _embarking = new();
    private IUnitService Units => ctx.Service<IUnitService>();

    public void Support()
    {
        foreach (var city in _targets.Enemies.OrderBy(c => c.Id)) attack.Prepare(city.Position);
    }

    public List<Unit> Sealift(List<Unit> free)
    {
        var remaining = new List<Unit>();
        foreach (var unit in free)
        {
            int area = _targets.Area(unit.Position);
            if (_targets.SelectPrimaryTargetCity(area) is not null || _targets.OverseasTarget(area) is null || _targets.Port(area) is not { } port)
            { remaining.Add(unit); continue; }
            ctx.Done.Add(unit.Id);
            if (unit.Position != port.Position) ctx.MoveToward(unit, port.Position);
            if (unit.Position == port.Position) _embarking.Add(unit.Id);
        }
        return remaining;
    }

    public void Plan()
    {
        foreach (var transport in ctx.UnitsWithRole(UnitRole.NavalTransport).Where(Ready).OrderBy(u => u.Id).ToArray()) Transport(transport);
        foreach (var ship in ctx.Units.Where(u => ctx.Role(u) is UnitRole.NavalCombat or UnitRole.NavalExplorer).Where(Ready).OrderBy(u => u.Id).ToArray()) Warship(ship);
    }

    private bool Ready(Unit unit) => ctx.Alive(unit) && unit.Health > 0 && unit.MovesLeft > 0 && !ctx.Done.Contains(unit.Id);

    private void Transport(Unit transport)
    {
        ctx.Done.Add(transport.Id);
        if (Units.CargoUnits(ctx.Sim, transport.Id).Count == 0 && !Pickup(transport)) return;
        var cargo = Units.CargoUnits(ctx.Sim, transport.Id).Select(ctx.Sim.State.UnitById).ToArray();
        if (cargo.Length == 0) return;
        // Loaded ships have a water area id. Cargo must use the enemy land area rather than the ship's area.
        var target = _targets.Enemies.Where(c => !ctx.Cities.Any(own => _targets.Area(own.Position) == _targets.Area(c.Position)))
            .OrderBy(c => ctx.Grid.Distance(transport.Position, c.Position)).ThenBy(c => c.Id).FirstOrDefault()
            ?? _targets.Enemies.OrderBy(c => ctx.Grid.Distance(transport.Position, c.Position)).ThenBy(c => c.Id).FirstOrDefault();
        if (target is null) { ReturnCargo(transport, cargo); return; }
        Deliver(transport, cargo, target);
    }

    private bool Pickup(Unit transport)
    {
        foreach (var port in ctx.Cities.Where(_targets.Coastal).Where(c => _targets.OverseasTarget(_targets.Area(c.Position)) is not null)
            .OrderByDescending(c => Waiting(c).Count()).ThenBy(c => ctx.Grid.Distance(transport.Position, c.Position)).ThenBy(c => c.Id))
        {
            if (!Waiting(port).Any()) continue;
            if (transport.Position != port.Position && !ctx.MoveToward(transport, port.Position)) continue;
            if (transport.Position != port.Position) return false;
            foreach (var unit in Waiting(port).OrderBy(u => ctx.Role(u) == UnitRole.Siege ? 1 : 0).ThenBy(u => u.Id).ToArray())
            {
                if (Units.CargoUnits(ctx.Sim, transport.Id).Count >= Units.CargoCapacity(ctx.Sim, transport.Id)) break;
                if (ctx.Issue(new LoadUnitCommand { Player = ctx.PlayerId, Unit = unit.Id, Transport = transport.Id })) ctx.Done.Add(unit.Id);
            }
            return Units.CargoUnits(ctx.Sim, transport.Id).Count > 0;
        }
        return false;
    }

    private IEnumerable<Unit> Waiting(City port) => ctx.Units.Where(u => ctx.Alive(u) && u.Position == port.Position
        && ctx.Role(u) is UnitRole.Melee or UnitRole.Siege && ctx.MilitaryState(u).TransportId is null
        && _embarking.Contains(u.Id) && u.MovesLeft > 0);

    private void Deliver(Unit transport, Unit[] cargo, City target)
    {
        cargo = AssaultFromSea(transport, cargo, target);
        if (cargo.Length == 0) return;
        if (Landing(transport, target) is not { } landing) return;
        if (transport.Position != landing.Sea) ctx.MoveToward(transport, landing.Sea);
        if (transport.Position != landing.Sea) return;
        attack.Prepare(target.Position);
        foreach (var unit in cargo)
        {
            if (!ctx.Alive(unit)) continue;
            ctx.Done.Add(unit.Id);
            if (ctx.Grid.Distance(unit.Position, target.Position) == 1 && Assault(unit, target)) continue;
            ctx.Issue(new UnloadUnitCommand { Player = ctx.PlayerId, Unit = unit.Id, Destination = landing.Land });
        }
    }

    private Unit[] AssaultFromSea(Unit transport, Unit[] cargo, City target)
    {
        if (ctx.Grid.Distance(transport.Position, target.Position) != 1) return cargo;
        attack.Prepare(target.Position);
        foreach (var unit in cargo.Where(u => ctx.Alive(u)))
            if (Assault(unit, target)) ctx.Done.Add(unit.Id);
        return cargo.Where(u => ctx.Alive(u) && ctx.MilitaryState(u).TransportId == transport.Id).ToArray();
    }

    private bool Assault(Unit unit, City target)
    {
        if (ctx.MilitaryState(unit).Attacked) return false;
        var odds = orders.Odds(unit, target.Position);
        return (odds is null || odds >= orders.ThresholdFor(unit)) && orders.Attack(unit, target.Position);
    }

    private (Hex Sea, Hex Land)? Landing(Unit transport, City target)
    {
        var options = new List<(Hex Sea, Hex Land, long Cost)>();
        foreach (var beach in _targets.Beaches(target))
        {
            if (ctx.Sim.State.CityAt(beach) is { } city && city.Owner != ctx.PlayerId) continue;
            if (ctx.Sim.State.UnitsAt(beach).Any(u => ctx.IsEnemy(u.Owner))) continue;
            foreach (var tile in ctx.Grid.Neighbors(ctx.Tile(beach)).Where(_targets.Water))
            {
                var sea = ctx.Grid.HexOf(tile);
                var path = ctx.FindPath(transport, sea);
                if (path is not null) options.Add((sea, beach, path.Cost));
            }
        }
        if (options.Count == 0) return null;
        var best = options.OrderBy(o => o.Cost + ctx.Grid.Distance(o.Land, target.Position) * 100L)
            .ThenBy(o => ctx.Tile(o.Sea).Value).ThenBy(o => ctx.Tile(o.Land).Value).First();
        return (best.Sea, best.Land);
    }

    private void ReturnCargo(Unit transport, Unit[] cargo)
    {
        foreach (var port in ctx.Cities.Where(_targets.Coastal).OrderBy(c => ctx.Grid.Distance(transport.Position, c.Position)).ThenBy(c => c.Id))
        {
            if (transport.Position != port.Position && !ctx.MoveToward(transport, port.Position)) continue;
            if (transport.Position == port.Position)
                foreach (var unit in cargo) ctx.Issue(new UnloadUnitCommand { Player = ctx.PlayerId, Unit = unit.Id });
            return;
        }
    }

    private void Warship(Unit ship)
    {
        ctx.Done.Add(ship.Id);
        if (Hunt(ship)) return;
        foreach (var city in _targets.Enemies.Where(_targets.Coastal).OrderBy(c => ctx.Grid.Distance(ship.Position, c.Position)).ThenBy(c => c.Id))
        {
            attack.Prepare(city.Position);
            if (ctx.MilitaryState(ship).Attacked) return;
        }
        var transport = ctx.UnitsWithRole(UnitRole.NavalTransport).Where(u => Units.CargoUnits(ctx.Sim, u.Id).Count > 0)
            .OrderBy(u => ctx.Grid.Distance(ship.Position, u.Position)).ThenBy(u => u.Id).FirstOrDefault();
        if (transport is not null) { ctx.MoveToward(ship, transport.Position); return; }
        if (ApproachCoastalTarget(ship)) return;
        if (_explorer.Explore(ship)) return;
        foreach (var port in ctx.Cities.Where(_targets.Coastal).OrderBy(c => ctx.Grid.Distance(ship.Position, c.Position)).ThenBy(c => c.Id))
            if (ctx.MoveToward(ship, port.Position)) return;
    }

    private bool ApproachCoastalTarget(Unit ship)
    {
        if (ctx.Def(ship).BombardRate <= 0 && ctx.Def(ship).BombRange <= 0) return false;
        foreach (var city in _targets.Enemies.Where(_targets.Coastal).OrderBy(c => ctx.Grid.Distance(ship.Position, c.Position)).ThenBy(c => c.Id))
            foreach (var tile in ctx.Grid.Neighbors(ctx.Tile(city.Position)).Where(_targets.Water).OrderBy(t => t.Value))
            {
                var hex = ctx.Grid.HexOf(tile);
                if (ship.Position != hex && !ctx.MoveToward(ship, hex)) continue;
                attack.Prepare(city.Position);
                return true;
            }
        return false;
    }

    private bool Hunt(Unit ship)
    {
        foreach (var enemy in ctx.Enemies.Where(e => ctx.Alive(e) && ctx.Def(e).Domain == "military:domain_sea"
            && ctx.Sim.State.PlotAt(e.Position).Visible.Contains(ctx.Team.Id))
            .OrderBy(e => ctx.Grid.Distance(ship.Position, e.Position)).ThenBy(e => e.Id))
        {
            if (ctx.Grid.Distance(ship.Position, enemy.Position) > 4) continue;
            if (orders.Odds(ship, enemy.Position) is not { } odds || odds < orders.Threshold) continue;
            if (ctx.Grid.Distance(ship.Position, enemy.Position) > 1) orders.Approach(ship, enemy.Position);
            if (orders.Odds(ship, enemy.Position) >= orders.Threshold && orders.Attack(ship, enemy.Position)) return true;
        }
        return false;
    }
}
