using Civ.Game;

namespace Civ.Mods.Ai;

/// <summary>Found cities at scored sites. Continue safe destinations and retreat immediately from nearby threats.</summary>
internal sealed class SettlerPlanner(AiContext ctx, SitePlanner sites)
{
    private readonly List<TileId> _targets = new();
    private SortedDictionary<int, UnitMission> _missions = new();

    public void Plan()
    {
        ctx.Issue(new PrepareSettlerMissionsCommand { Player = ctx.PlayerId });
        foreach (var settler in ctx.UnitsWithRole(UnitRole.Settler).OrderBy(u => u.Id).ToList())
        {
            if (!AiIntent.EligibleUnit(ctx, settler)) continue;
            if (settler.MovesLeft <= 0 && ctx.Cities.Count > 0) continue;
            ctx.Done.Add(settler.Id);
            Act(settler);
        }
    }

    private void Act(Unit settler)
    {
        if (ctx.Cities.Count == 0) { FoundFirst(settler); return; }
        if (ctx.ThreatAround(settler.Position, 3) > 0) { Retreat(settler); return; }
        var mission = CurrentMission(settler);
        if (mission is null) { if (ctx.Turn > 5) TryFound(settler); return; }
        if (mission.Target == ctx.Tile(settler.Position)) { TryFound(settler); return; }
        MoveAndFound(settler, mission.Target);
    }

    private UnitMission? CurrentMission(Unit settler)
    {
        var mission = AiIntent.Read(ctx).Missions.GetValueOrDefault(settler.Id);
        if (mission is null || Valid(settler, mission)) return mission;
        // A previous founding can invalidate spacing in this same pass.
        ctx.Issue(new PrepareSettlerMissionsCommand { Player = ctx.PlayerId });
        return AiIntent.Read(ctx).Missions.GetValueOrDefault(settler.Id);
    }

    private void MoveAndFound(Unit settler, TileId target)
    {
        var before = settler.Position;
        ctx.MoveToward(settler, ctx.Grid.HexOf(target));
        if (!ctx.Alive(settler)) { Forget(settler); return; }
        if (settler.Position == before) { Forget(settler); Retreat(settler); return; }
        if (ctx.Tile(settler.Position) == target) TryFound(settler);
    }

    private void FoundFirst(Unit settler)
    {
        if (sites.FirstSite(settler) is { } site && site.Tile != ctx.Tile(settler.Position))
            ctx.MoveToward(settler, ctx.Grid.HexOf(site.Tile));
        TryFound(settler);
    }

    private bool TryFound(Unit settler)
    {
        if (!ctx.Issue(new FoundCityWithSettlerCommand { Player = ctx.PlayerId, Settler = settler.Id })) return false;
        Forget(settler);
        return true;
    }

    private void Forget(Unit settler) => ctx.Issue(new ForgetUnitMissionCommand { Player = ctx.PlayerId, Unit = settler.Id });

    private void Retreat(Unit settler)
    {
        if (ctx.NearestCity(settler.Position) is { } city) ctx.MoveToward(settler, city.Position);
    }

    // Command validation evaluates a detached intent value. Routes remain segment-local queries.
    internal SortedDictionary<int, UnitMission> EvaluateMissions()
    {
        _missions = AiIntent.CleanMissions(ctx);
        if (ctx.Cities.Count == 0) return _missions; // Initial founding keeps its existing one-step policy.
        foreach (var settler in ctx.UnitsWithRole(UnitRole.Settler).OrderBy(u => u.Id)) Assign(settler);
        return _missions;
    }

    private void Assign(Unit settler)
    {
        var held = _missions.GetValueOrDefault(settler.Id);
        if (held is not null && Valid(settler, held)) { _targets.Add(held.Target); return; }
        _missions.Remove(settler.Id);
        if (!AiIntent.EligibleUnit(ctx, settler) || settler.MovesLeft <= 0) return;
        if (ctx.ThreatAround(settler.Position, 3) > 0) return;
        var site = ChooseSite(settler);
        if (site is not null)
            _missions[settler.Id] = AiIntent.Create(ctx, settler, AiMissionKind.Settle, site.Tile);
    }

    private bool Valid(Unit settler, UnitMission mission) => AiIntent.Matches(ctx, settler, mission, AiMissionKind.Settle)
        && Destination(settler, mission.Target) && AiIntent.Reachable(ctx, settler, mission.Target);

    private bool Destination(Unit settler, TileId tile) => sites.LegalDestination(tile)
        && ctx.Sim.State.Plots[tile.Value].AreaId == ctx.Sim.State.PlotAt(settler.Position).AreaId
        && _targets.All(t => ctx.Grid.Distance(t, tile) > 3)
        && ctx.ThreatAround(settler.Position, 3) == 0 && ctx.ThreatAround(ctx.Grid.HexOf(tile), 3) == 0;

    private Site? ChooseSite(Unit settler)
    {
        int area = ctx.Sim.State.PlotAt(settler.Position).AreaId;
        var site = sites.DistinctSites().Where(s => s.Area == area && Destination(settler, s.Tile))
            .OrderBy(s => ctx.ThreatAround(ctx.Grid.HexOf(s.Tile), 3))
            .ThenByDescending(s => s.Score - 8 * ctx.Grid.Distance(ctx.Tile(settler.Position), s.Tile))
            .ThenBy(s => s.Tile.Value).FirstOrDefault(s => AiIntent.Reachable(ctx, settler, s.Tile));
        if (site is not null) _targets.Add(site.Tile);
        return site;
    }
}
