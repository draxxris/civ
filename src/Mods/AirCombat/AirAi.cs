using Ariel.Ai;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.AirCombat;

/// <summary>The computer player's aircraft, in the slot the standard AI keeps for them (<see cref="AiPlannerOrder.Air"/>):
/// bomb defences first, strike defenders second, then reposition, stand on intercept or scout.</summary>
internal sealed class AirAiPlanner : IAiPlanner
{
    public string Id => "air_combat:planner";
    public int Order => AiPlannerOrder.Air;
    public void Plan(AiContext ctx)
    {
        if (ctx.Sim.Service<IAirMissionService>() is { } air) new AirPlanner(ctx, air).Plan();
    }
}

internal sealed class AirPlanner(AiContext ctx, IAirMissionService air)
{
    public void Plan()
    {
        var aircraft = ctx.Units.Where(u => ctx.Alive(u) && Aircraft.Is(ctx.Def(u)) && u.Health > 0 && u.MovesLeft > 0).OrderBy(u => u.Id).ToArray();
        foreach (var unit in aircraft) Bomb(unit);
        foreach (var unit in aircraft.Where(u => ctx.Alive(u) && !ctx.HasActed(u))) Strike(unit);
        foreach (var unit in aircraft.Where(u => ctx.Alive(u) && !ctx.HasActed(u))) Idle(unit);
    }

    private void Bomb(Unit unit)
    {
        foreach (var city in ctx.EnemyCities.OrderBy(c => ctx.Grid.Distance(unit.Position, c.Position)).ThenBy(c => c.Id))
            if (Mission(unit, AirMissionKind.AirBomb, city.Position)) return;
        foreach (var tile in ctx.Grid.Range(ctx.Tile(unit.Position), air.AirRange(ctx.Sim, unit.Id))
            .Where(t => ctx.Sim.State.Plots[t.Value].Bonus is not null && ctx.Sim.State.Plots[t.Value].Improvement is not null).OrderBy(t => t.Value))
            if (Mission(unit, AirMissionKind.AirBomb, ctx.Grid.HexOf(tile))) return;
    }

    private void Strike(Unit unit)
    {
        foreach (var stack in ctx.Enemies.Where(ctx.Alive).GroupBy(e => e.Position)
            .OrderByDescending(g => g.Max(e => ctx.Def(e).Strength * e.Health)).ThenBy(g => ctx.Tile(g.Key).Value))
            if (Mission(unit, AirMissionKind.AirStrike, stack.Key)) return;
    }

    private bool Mission(Unit unit, AirMissionKind kind, Hex target)
    {
        if (!ctx.Issue(new AirMissionCommand { Player = ctx.PlayerId, Unit = unit.Id, Kind = kind, Target = target })) return false;
        ctx.MarkActed(unit);
        return true;
    }

    private void Idle(Unit unit)
    {
        if (Rebase(unit)) return;
        if (air.InterceptionProbability(ctx.Sim, unit.Id) > 0)
        {
            if (ctx.Issue(new SetUnitActivityCommand { Player = ctx.PlayerId, Unit = unit.Id, Activity = UnitActivity.Intercept })) ctx.MarkActed(unit);
            return;
        }
        int range = air.AirRange(ctx.Sim, unit.Id);
        foreach (var tile in ctx.Grid.Range(ctx.Tile(unit.Position), range).Where(t => !ctx.Sim.State.Plots[t.Value].Revealed.Contains(ctx.Team.Id))
            .OrderBy(t => t.Value))
            if (Mission(unit, AirMissionKind.Recon, ctx.Grid.HexOf(tile))) return;
    }

    private bool Rebase(Unit unit)
    {
        var target = ctx.EnemyCities.OrderBy(c => ctx.Grid.Distance(unit.Position, c.Position)).ThenBy(c => c.Id).FirstOrDefault();
        if (target is null || ctx.Grid.Distance(unit.Position, target.Position) <= air.AirRange(ctx.Sim, unit.Id)) return false;
        int current = ctx.Grid.Distance(unit.Position, target.Position);
        foreach (var city in ctx.Cities.Where(c => ctx.Grid.Distance(c.Position, target.Position) < current)
            .OrderBy(c => ctx.Grid.Distance(c.Position, target.Position)).ThenBy(c => c.Id))
            if (Mission(unit, AirMissionKind.Rebase, city.Position)) return true;
        return false;
    }
}

/// <summary>At war, a city asks for the best aircraft it can build while the air wing is under a sixth of the land army
/// (and always for the first one).</summary>
internal sealed class AircraftWishes : IProductionWishes
{
    internal const long AircraftScore = 360;
    public string Id => "air_combat:aircraft";

    public IEnumerable<ProductionWish> Wishes(AiContext ctx, City city)
    {
        if (ctx.Team.Wars.Count == 0 || ctx.Sim.Service<IAirMissionService>() is null) yield break;
        int wing = ctx.Units.Count(u => Aircraft.Is(ctx.Def(u))) + ctx.Cities.SelectMany(c => c.ProductionQueue)
            .Count(o => o.Kind == "cities:unit" && ctx.Sim.Content.TryGet<UnitDef>(o.ContentId, out var def) && Aircraft.Is(def));
        if (wing >= Math.Max(1, ctx.LandArmy / 6)) yield break;
        if (ctx.BestUnit(city, Aircraft.Is, u => u.AirCombat * 10 + u.BombRate + u.InterceptionPercent) is { } best)
            yield return new("cities:unit", best.Id, AircraftScore);
    }
}

internal static class Aircraft
{
    public static bool Is(UnitDef def) => def.Domain == AirMissionService.AirDomain;
}
