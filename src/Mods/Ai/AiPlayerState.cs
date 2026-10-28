using Ariel.Game;

namespace Ariel.Mods.Ai;

/// <summary>Saved strategic intent. Missing components mean that nothing has been evaluated or assigned.</summary>
public sealed class AiPlayerState
{
    public CivicGoal? Civic { get; set; }
    public SortedDictionary<int, CampaignGoal> Campaigns { get; set; } = new();
    public SortedDictionary<int, UnitMission> Missions { get; set; } = new();
}

/// <summary>Civic ids preferred at the last evaluation, including an empty choice when no change was worthwhile.</summary>
public sealed record CivicGoal(int EvaluatedTurn, int ReviewTurn, string Dependencies, List<string> Civics);

/// <summary>A land campaign destination, or a remembered absence of an eligible enemy city.</summary>
public sealed record CampaignGoal(int EvaluatedTurn, int ReviewTurn, string Dependencies, int? City);

public enum AiMissionKind { Settle, Improve }

/// <summary>A destination and task, not a route or a reference to a live entity.</summary>
public sealed record UnitMission(int Unit, string UnitType, AiMissionKind Kind, TileId Target, string? Build,
    int CreatedTurn, int ExpiryTurn);

internal static class AiIntent
{
    internal const int MaximumAge = 3, MissionLifetime = 12;

    internal static AiPlayerState Read(AiContext ctx) => ctx.Self.Extensions.Get<AiPlayerState>(ctx.Sim.Content) ?? new();

    // Only command application calls Write. Planners outside commands read this component but never mutate it.
    internal static AiPlayerState Write(AiContext ctx)
    {
        var state = ctx.Self.Extensions.Get<AiPlayerState>(ctx.Sim.Content);
        if (state is not null) return state;
        state = new();
        ctx.Self.Extensions.Set(ctx.Sim.Content, state);
        return state;
    }

    internal static int ReviewTurn(AiContext ctx) => ctx.Turn + MaximumAge - (ctx.Turn + ctx.PlayerId) % MaximumAge;
    internal static bool Reusable(AiContext ctx, int evaluated, int review) => evaluated <= ctx.Turn
        && ctx.Turn < review && ctx.Turn - evaluated < MaximumAge;

    internal static int EconomicStatus(AiContext ctx)
    {
        var economy = ctx.Self.Extensions.Get<EconomyPlayerState>(ctx.Sim.Content);
        int status = ctx.Self.Treasury < EconomyPlanner.Reserve(ctx.Cities.Count) ? 1 : 0;
        if (economy?.UnpaidGold > 0) status |= 2;
        if (economy?.StrikeTurns > 0) status |= 4;
        if (ctx.Service<IPlayerEconomyService>().Calculate(ctx.Sim, ctx.PlayerId).NetGold < 0) status |= 8;
        return status;
    }

    internal static bool EligibleUnit(AiContext ctx, Unit unit) => unit.Owner == ctx.PlayerId && unit.Health > 0
        && ctx.Def(unit).Domain == "military:domain_land" && ctx.MilitaryState(unit).TransportId is null;

    internal static bool Matches(AiContext ctx, Unit unit, UnitMission mission, AiMissionKind kind) =>
        EligibleUnit(ctx, unit) && mission.Unit == unit.Id && mission.UnitType == unit.Type && mission.Kind == kind
        && mission.CreatedTurn <= ctx.Turn && ctx.Turn < mission.ExpiryTurn && OnMap(ctx, mission.Target);

    internal static bool OnMap(AiContext ctx, TileId tile) => tile.Value >= 0 && tile.Value < ctx.Grid.Count;

    internal static void RemoveIfEmpty(AiContext ctx)
    {
        var state = ctx.Self.Extensions.Get<AiPlayerState>(ctx.Sim.Content);
        if (state is { Civic: null, Campaigns.Count: 0, Missions.Count: 0 })
            ctx.Self.Extensions.Remove<AiPlayerState>(ctx.Sim.Content);
    }

    internal static bool Reachable(AiContext ctx, Unit unit, TileId target)
    {
        if (ctx.Tile(unit.Position) == target) return true;
        var path = ctx.FindPath(unit, ctx.Grid.HexOf(target));
        // Civilian intent never adds transport coordination.
        return path is { Steps.Count: > 0 } && path.Steps.All(h =>
            !ctx.Sim.Content.Get<Ariel.Content.Schema.TerrainDef>(ctx.Sim.State.PlotAt(h).Terrain).IsWater);
    }

    internal static UnitMission Create(AiContext ctx, Unit unit, AiMissionKind kind, TileId tile, string? build = null) =>
        new(unit.Id, unit.Type, kind, tile, build, ctx.Turn, ctx.Turn + MissionLifetime);

    internal static void Prune(AiContext ctx)
    {
        var state = ctx.Self.Extensions.Get<AiPlayerState>(ctx.Sim.Content);
        if (state is null) return;
        state.Missions = CleanMissions(ctx);
        if (ctx.Cities.Count == 0) { state.Civic = null; state.Campaigns.Clear(); state.Missions.Clear(); }
        if (ctx.Team.Wars.Count == 0) state.Campaigns.Clear();
    }

    internal static SortedDictionary<int, UnitMission> CleanMissions(AiContext ctx) => ctx.Cities.Count == 0 ? new() :
        new(AiIntent.Read(ctx).Missions.Where(p => StillOwned(ctx, p.Key, p.Value)).ToDictionary(p => p.Key, p => p.Value));

    internal static void CommitMissions(AiContext ctx, SortedDictionary<int, UnitMission> missions)
    {
        if (missions.Count > 0 || ctx.Self.Extensions.Get<AiPlayerState>(ctx.Sim.Content) is not null)
            Write(ctx).Missions = missions;
        RemoveIfEmpty(ctx);
    }

    private static bool StillOwned(AiContext ctx, int id, UnitMission mission)
    {
        var unit = ctx.Sim.State.FindUnit(id);
        if (unit is null || !Matches(ctx, unit, mission, mission.Kind)) return false;
        return mission.Kind switch
        {
            AiMissionKind.Settle => ctx.Role(unit) == UnitRole.Settler,
            AiMissionKind.Improve => ctx.Role(unit) == UnitRole.Worker,
            _ => false,
        };
    }
}
