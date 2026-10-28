using Ariel.Game;

namespace Ariel.Mods.Ai;

/// <summary>Bomb defenses first, strike defenders second, then reposition or provide air cover.</summary>
internal sealed class AirPlanner(AiContext ctx)
{
    private readonly CampaignTargets _targets = new(ctx);
    public void Plan()
    {
        var aircraft = ctx.UnitsWithRole(UnitRole.Air).Where(u => u.Health > 0 && u.MovesLeft > 0).OrderBy(u => u.Id).ToArray();
        foreach (var unit in aircraft) Bomb(unit);
        foreach (var unit in aircraft.Where(u => ctx.Alive(u) && !ctx.Done.Contains(u.Id))) Strike(unit);
        foreach (var unit in aircraft.Where(u => ctx.Alive(u) && !ctx.Done.Contains(u.Id))) Idle(unit);
    }

    private void Bomb(Unit unit)
    {
        foreach (var city in _targets.Enemies.OrderBy(c => ctx.Grid.Distance(unit.Position, c.Position)).ThenBy(c => c.Id))
            if (Mission(unit, AirMissionKind.AirBomb, city.Position)) return;
        foreach (var tile in ctx.Grid.Range(ctx.Tile(unit.Position), ctx.Service<IAirMissionService>().AirRange(ctx.Sim, unit.Id))
            .Where(t => ctx.Sim.State.Plots[t.Value].Bonus is not null && ctx.Sim.State.Plots[t.Value].Improvement is not null).OrderBy(t => t.Value))
            if (Mission(unit, AirMissionKind.AirBomb, ctx.Grid.HexOf(tile))) return;
    }

    private void Strike(Unit unit)
    {
        foreach (var stack in ctx.Enemies.Where(e => ctx.Alive(e)).GroupBy(e => e.Position)
            .OrderByDescending(g => g.Max(e => ctx.Def(e).Strength * e.Health)).ThenBy(g => ctx.Tile(g.Key).Value))
            if (Mission(unit, AirMissionKind.AirStrike, stack.Key)) return;
    }

    private bool Mission(Unit unit, AirMissionKind kind, Hex target)
    {
        if (!ctx.Issue(new AirMissionCommand { Player = ctx.PlayerId, Unit = unit.Id, Kind = kind, Target = target })) return false;
        ctx.Done.Add(unit.Id);
        return true;
    }

    private void Idle(Unit unit)
    {
        if (Rebase(unit)) return;
        if (ctx.Service<IAirMissionService>().InterceptionProbability(ctx.Sim, unit.Id) > 0)
        {
            if (ctx.Issue(new SetUnitActivityCommand { Player = ctx.PlayerId, Unit = unit.Id, Activity = UnitActivity.Intercept })) ctx.Done.Add(unit.Id);
            return;
        }
        int range = ctx.Service<IAirMissionService>().AirRange(ctx.Sim, unit.Id);
        foreach (var tile in ctx.Grid.Range(ctx.Tile(unit.Position), range).Where(t => !ctx.Sim.State.Plots[t.Value].Revealed.Contains(ctx.Team.Id))
            .OrderBy(t => t.Value))
            if (Mission(unit, AirMissionKind.Recon, ctx.Grid.HexOf(tile))) return;
    }

    private bool Rebase(Unit unit)
    {
        var target = _targets.Enemies.OrderBy(c => ctx.Grid.Distance(unit.Position, c.Position)).ThenBy(c => c.Id).FirstOrDefault();
        if (target is null || ctx.Grid.Distance(unit.Position, target.Position) <= ctx.Service<IAirMissionService>().AirRange(ctx.Sim, unit.Id)) return false;
        int current = ctx.Grid.Distance(unit.Position, target.Position);
        foreach (var city in ctx.Cities.Where(c => ctx.Grid.Distance(c.Position, target.Position) < current)
            .OrderBy(c => ctx.Grid.Distance(c.Position, target.Position)).ThenBy(c => c.Id))
            if (Mission(unit, AirMissionKind.Rebase, city.Position)) return true;
        return false;
    }
}
