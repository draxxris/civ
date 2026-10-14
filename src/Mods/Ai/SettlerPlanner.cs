using Civ.Game;

namespace Civ.Mods.Ai;

/// <summary>Found cities at scored sites. Settlers wait for a spare escort under nearby threat;
/// an escort never strips a reserved city garrison.</summary>
internal sealed class SettlerPlanner(AiContext ctx, SitePlanner sites, ArmyPlan army)
{
    private readonly List<TileId> _targets = new();

    public void Plan()
    {
        foreach (var settler in ctx.UnitsWithRole(UnitRole.Settler).ToList())
        {
            if (settler.MovesLeft <= 0 && ctx.Cities.Count > 0) continue;
            ctx.Done.Add(settler.Id);
            Act(settler);
        }
    }

    private void Act(Unit settler)
    {
        if (ctx.Cities.Count == 0) { FoundFirst(settler); return; }
        var target = ChooseSite(settler);
        if (target is null) { if (ctx.Turn > 5) TryFound(settler); return; }
        if (target.Tile == ctx.Tile(settler.Position)) { TryFound(settler); return; }
        bool danger = Dangerous(settler, target);
        var escort = danger ? Escort(settler) : null;
        if (danger && AwaitEscort(settler, escort)) return;
        MoveAndFound(settler, target, escort);
    }

    private bool AwaitEscort(Unit settler, Unit? escort)
    {
        if (escort is null) return true;
        ctx.Done.Add(escort.Id);
        if (escort.Position == settler.Position) return false;
        ctx.MoveToward(escort, settler.Position);
        return true;
    }

    private void MoveAndFound(Unit settler, Site target, Unit? escort)
    {
        ctx.MoveToward(settler, ctx.Grid.HexOf(target.Tile));
        if (!ctx.Alive(settler)) return;
        if (escort is not null) ctx.MoveToward(escort, settler.Position);
        if (ctx.Tile(settler.Position) == target.Tile) TryFound(settler);
    }

    private void FoundFirst(Unit settler)
    {
        if (sites.FirstSite(settler) is { } site && site.Tile != ctx.Tile(settler.Position))
            ctx.MoveToward(settler, ctx.Grid.HexOf(site.Tile));
        TryFound(settler);
    }

    private bool TryFound(Unit settler) => ctx.Issue(new FoundCityWithSettlerCommand { Player = ctx.PlayerId, Settler = settler.Id });

    private Site? ChooseSite(Unit settler)
    {
        int area = ctx.Sim.State.PlotAt(settler.Position).AreaId;
        var site = sites.DistinctSites().Where(s => s.Area == area && _targets.All(t => ctx.Grid.Distance(t, s.Tile) > 3))
            .OrderBy(s => ctx.ThreatAround(ctx.Grid.HexOf(s.Tile), 3))
            .ThenByDescending(s => s.Score - 8 * ctx.Grid.Distance(ctx.Tile(settler.Position), s.Tile))
            .ThenBy(s => s.Tile.Value).FirstOrDefault();
        if (site is not null) _targets.Add(site.Tile);
        return site;
    }

    private bool Dangerous(Unit settler, Site target) =>
        ctx.ThreatAround(settler.Position, 3) > 0 || ctx.ThreatAround(ctx.Grid.HexOf(target.Tile), 3) > 0;

    private Unit? Escort(Unit settler) => ctx.UnitsWithRole(UnitRole.Melee)
        .Where(u => !ctx.Done.Contains(u.Id) && u.MovesLeft > 0 && Spare(u))
        .OrderBy(u => ctx.Grid.Distance(u.Position, settler.Position)).ThenByDescending(u => ctx.Def(u).Strength)
        .ThenBy(u => u.Id).FirstOrDefault();

    private bool Spare(Unit soldier)
    {
        if (ctx.CityAtUnit(soldier) is not { } city) return true;
        int garrison = ctx.Sim.State.UnitsAt(city.Position).Count(u => u.Owner == ctx.PlayerId && ctx.Role(u) == UnitRole.Melee);
        return garrison > army.DesiredGarrison(CityView.Build(ctx, city));
    }
}
