using Civ.Content;
using Civ.Content.Schema;
using Civ.Game;

namespace Civ.Mods.Ai;

/// <summary>Chooses the next technology: AI weight, flavors times personality, and what the technology unlocks.</summary>
internal sealed class ResearchPlanner(AiContext ctx)
{
    private const int ParallelTechs = 6;
    public void Plan()
    {
        var team = ctx.Team;
        SpendFreeCredits();
        if (team.Researching is { } current && !team.Techs.Contains(current)) return;
        var research = ctx.Service<IResearchService>();
        var owned = OwnedBonuses();
        var candidates = ctx.Catalog.Techs.Where(t => !team.Techs.Contains(t.Id)).ToArray();
        var scores = ParallelScoring.Select(ctx.Sim, candidates, ParallelTechs, t =>
            research.Validate(ctx.Sim, ctx.PlayerId, t.Id).Success ? Score(research, t, owned) : (long?)null);
        var best = candidates.Select((t, i) => (Tech: t, Score: scores[i])).Where(x => x.Score is not null)
            .OrderByDescending(x => x.Score).ThenBy(x => x.Tech.Id, StringComparer.Ordinal).FirstOrDefault();
        if (best.Tech is null) return;
        ctx.Issue(new SetTeamResearchCommand { Player = ctx.PlayerId, Tech = best.Tech.Id });
    }

    private void SpendFreeCredits()
    {
        var research = ctx.Service<IResearchService>();
        while (ctx.Team.Extensions.Get<ResearchTeamState>(ctx.Sim.Content)?.FreeTechs > 0)
        {
            var best = ctx.Catalog.Techs.Where(t => research.Validate(ctx.Sim, ctx.PlayerId, t.Id).Success)
                .OrderByDescending(t => research.CalculateCost(ctx.Sim, ctx.Team.Id, t.Id).Cost + ctx.FlavorScore(t.Flavors))
                .ThenBy(t => t.Id, StringComparer.Ordinal).FirstOrDefault();
            if (best is null || !ctx.Issue(new SetTeamResearchCommand { Player = ctx.PlayerId, Tech = best.Id, FreeTech = true })) break;
        }
    }

    /// <summary>Integer score: value points scaled by what a beaker buys, so cheap useful techs come first.</summary>
    internal long Score(IResearchService research, TechDef tech, HashSet<string> ownedBonuses)
    {
        long value = 40 + ctx.FlavorScore(tech.Flavors) * 3 + Math.Min(tech.AiWeight, 1000) / 20;
        value += UnlockValue(tech.Id) + ResourceValue(tech.Id, ownedBonuses) + CapabilityValue(tech);
        long cost = Math.Max(1, research.CalculateCost(ctx.Sim, ctx.Team.Id, tech.Id).Cost);
        long progress = ctx.Team.Research.GetValueOrDefault(tech.Id);
        return value * 100_000 / Math.Max(1, cost - progress / 2);
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
        _ => 0,
    };

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
