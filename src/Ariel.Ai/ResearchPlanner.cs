using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Ai;

/// <summary>Chooses the next technology: AI weight, flavors times personality, and what the technology unlocks.</summary>
internal sealed class ResearchPlanner(AiContext ctx)
{
    private const int ParallelTechs = 6;
    public void Plan()
    {
        var team = ctx.Team;
        SpendFreeCredits();
        var research = ctx.Service<IResearchService>();
        if (team.Researching is { } current && research.ValidateSelection(ctx.Sim, ctx.PlayerId, current).Success) return;
        var owned = OwnedBonuses();
        var candidates = ctx.Catalog.Techs.ToArray();
        var scores = ParallelScoring.Select(ctx.Sim, candidates, ParallelTechs, t =>
            research.ValidateSelection(ctx.Sim, ctx.PlayerId, t.Id).Success ? Score(research, t, owned) : (long?)null);
        var goals = GoalScores(research, owned, candidates.Where((_, i) => scores[i] is not null).Select(t => t.Id).ToHashSet(StringComparer.Ordinal));
        var best = candidates.Select((t, i) => (Tech: t, Score: scores[i] is { } own ? Math.Max(own, goals.GetValueOrDefault(t.Id)) : (long?)null))
            .Where(x => x.Score is not null)
            .OrderByDescending(x => x.Score).ThenBy(x => x.Tech.Id, StringComparer.Ordinal).FirstOrDefault();
        if (best.Tech is null) return;
        ctx.Issue(new SetTeamResearchCommand { Player = ctx.PlayerId, Tech = best.Tech.Id });
    }

    private void SpendFreeCredits()
    {
        var research = ctx.Service<IResearchService>();
        while (ctx.Team.Extensions.Get<ResearchTeamState>(ctx.Sim.Content)?.FreeTechs > 0)
        {
            var best = ctx.Catalog.Techs.Where(t => research.ValidateSelection(ctx.Sim, ctx.PlayerId, t.Id).Success)
                .OrderByDescending(t => research.CalculateCost(ctx.Sim, ctx.Team.Id, t.Id).Cost + ctx.FlavorScore(t.Flavors))
                .ThenBy(t => t.Id, StringComparer.Ordinal).FirstOrDefault();
            if (best is null || !ctx.Issue(new SetTeamResearchCommand { Player = ctx.PlayerId, Tech = best.Id, FreeTech = true })) break;
        }
    }

    /// <summary>Integer score: value points scaled by what a beaker buys, so cheap useful techs come first.</summary>
    internal long Score(IResearchService research, TechDef tech, HashSet<string> ownedBonuses) =>
        Value(tech, ownedBonuses) * 100_000 / RemainingCost(research, tech);

    internal long Value(TechDef tech, HashSet<string> ownedBonuses)
    {
        long value = 40 + ctx.FlavorScore(tech.Flavors) * 3 + Math.Min(tech.AiWeight, 1000) / 20;
        return value + UnlockValue(tech.Id) + ResourceValue(tech.Id, ownedBonuses) + CapabilityValue(tech);
    }

    private long RemainingCost(IResearchService research, TechDef tech)
    {
        long cost = Math.Max(1, research.CalculateCost(ctx.Sim, ctx.Team.Id, tech.Id).Cost);
        long progress = ctx.Team.Research.GetValueOrDefault(tech.Id);
        return Math.Max(1, cost - progress / 2);
    }

    internal const int MaximumGoalSteps = 5;

    /// <summary>Score long-range goals: a tech that is not yet selectable is valued with every missing prerequisite on
    /// its cheapest route, as total value per total beaker. Each selectable first step on that route inherits the
    /// goal's score, so a valuable unlock two or three techs away can beat a slightly better immediate pick.</summary>
    public Dictionary<string, long> GoalScores(IResearchService research, HashSet<string> owned, HashSet<string> selectable)
    {
        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        if (selectable.Count == 0) return result;
        var costs = new Dictionary<string, long>(StringComparer.Ordinal);
        var values = new Dictionary<string, long>(StringComparer.Ordinal);
        long CostOf(TechDef t) { if (!costs.TryGetValue(t.Id, out var c)) costs[t.Id] = c = RemainingCost(research, t); return c; }
        long ValueOf(TechDef t) { if (!values.TryGetValue(t.Id, out var v)) values[t.Id] = v = Value(t, owned); return v; }
        var routes = new Dictionary<string, List<TechDef>?>(StringComparer.Ordinal);
        foreach (var goal in ctx.Catalog.Techs)
        {
            if (Known(goal.Id) || selectable.Contains(goal.Id)) continue;
            var route = Route(goal, routes, CostOf);
            if (route is null || route.Count < 2 || route.Count > MaximumGoalSteps) continue;
            long total = route.Sum(ValueOf), cost = Math.Max(1, route.Sum(CostOf));
            long score = total * 100_000 / cost;
            foreach (var step in route.Where(t => selectable.Contains(t.Id)))
                if (score > result.GetValueOrDefault(step.Id)) result[step.Id] = score;
        }
        return result;
    }

    private bool Known(string tech) => ctx.Team.Techs.Contains(tech);

    /// <summary>The goal and its unknown prerequisites, choosing the cheapest branch of each alternative.
    /// Null when the goal is unreachable through technology alone or the route is too long.</summary>
    private List<TechDef>? Route(TechDef goal, Dictionary<string, List<TechDef>?> memo, Func<TechDef, long> cost)
    {
        if (memo.TryGetValue(goal.Id, out var held)) return held;
        memo[goal.Id] = null; // Guard against cyclic content.
        var needed = Missing(goal.Requirements, memo, cost);
        List<TechDef>? route = null;
        if (needed is not null)
        {
            route = needed.DistinctBy(t => t.Id).ToList();
            route.Add(goal);
            if (route.Count > MaximumGoalSteps) route = null;
        }
        memo[goal.Id] = route;
        return route;
    }

    private List<TechDef>? Missing(Requirement? requirement, Dictionary<string, List<TechDef>?> memo, Func<TechDef, long> cost)
    {
        switch (requirement)
        {
            case null: return [];
            case Requirement.Leaf { Kind: "tech" } leaf:
                if (Known(leaf.Id)) return [];
                if (!ctx.Sim.Content.TryGet<TechDef>(leaf.Id, out var tech) || tech.Disable) return null;
                return Route(tech, memo, cost);
            case Requirement.Leaf: return [];
            case Requirement.All all:
                var union = new List<TechDef>();
                foreach (var item in all.Items)
                {
                    if (Missing(item, memo, cost) is not { } part) return null;
                    union.AddRange(part);
                }
                return union;
            case Requirement.Any any:
                List<TechDef>? cheapest = null; long best = long.MaxValue;
                foreach (var item in any.Items)
                {
                    if (Missing(item, memo, cost) is not { } part) continue;
                    long total = part.DistinctBy(t => t.Id).Sum(cost);
                    if (total < best) { best = total; cheapest = part; }
                }
                return cheapest;
            default: return [];
        }
    }

    private long UnlockValue(string tech)
    {
        if (!ctx.Catalog.TechUnlocks.TryGetValue(tech, out var ids)) return 0;
        long total = 0;
        foreach (var id in ids)
        {
            if (ctx.Sim.Content.TryGet<BuildingDef>(id, out var b)) total += BuildingValue(b);
            else if (ctx.Sim.Content.TryGet<UnitDef>(id, out var u)) total += UnitValue(u);
        }
        return Math.Min(total, 400);
    }

    private long BuildingValue(BuildingDef b) =>
        ctx.FlavorScore(b.Flavors) + b.Happiness * 6 + b.Health * 5 + (b.YieldChanges.Food + b.YieldChanges.Production) * 6 + b.CommerceChanges.Research * 5
        + b.CommerceChanges.Gold * 4 + b.CommerceChanges.Culture * 3 + (b.Limits.World >= 0 ? 30 : 0);

    private long UnitValue(UnitDef u) => ctx.Catalog.Roles.GetValueOrDefault(u.Id) switch
    {
        UnitRole.Settler or UnitRole.Worker => 25,
        UnitRole.Melee or UnitRole.Siege => u.Strength * ctx.Weight("military") / 4,
        UnitRole.Air => Math.Max(u.Strength, u.AirCombat) * ctx.Weight("military") / 4,
        UnitRole.NavalCombat when Coastal => u.Strength * ctx.Weight("military") / 6,
        UnitRole.NavalTransport when Coastal => 20 + Math.Max(u.Cargo, u.TotalCargo) * 4,
        UnitRole.NavalExplorer when Coastal => 10,
        UnitRole.Explorer => 5,
        _ => 0,
    };

    // Scoring runs in parallel, so the shared answer is computed once under Lazy's lock.
    private readonly Lazy<bool> _coastal = new(() => ctx.Cities.Any(c => ctx.Grid.Neighbors(ctx.Tile(c.Position))
        .Any(n => ctx.Sim.Content.Get<TerrainDef>(ctx.Sim.State.Plots[n.Value].Terrain).IsWater && !ctx.Sim.State.Plots[n.Value].IsLake)));
    /// <summary>Ships matter only to an empire that can build them.</summary>
    private bool Coastal => _coastal.Value;

    private long ResourceValue(string tech, HashSet<string> owned)
    {
        long total = 0;
        foreach (var bonus in ctx.Sim.Content.Get<BonusDef>().All)
            if ((bonus.RevealTech == tech || bonus.TradeTech == tech) && owned.Contains(bonus.Id))
                total += 20 + 10 * (bonus.Happiness + bonus.Health);
        return total;
    }

    private static long CapabilityValue(TechDef t) =>
        (t.Irrigation ? 15 : 0) + (t.BridgeBuilding ? 8 : 0) + (t.WaterWork ? 8 : 0) + (t.Trade ? 12 : 0) + t.Happiness * 10 + t.Health * 8 + t.WorkerSpeedModifier / 5;

    private HashSet<string> OwnedBonuses()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var plot in ctx.Sim.State.Plots)
            if (plot.Owner == ctx.PlayerId && plot.Bonus is { } b) set.Add(b);
        return set;
    }
}
