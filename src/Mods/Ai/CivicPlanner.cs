using Civ.Content.Schema;
using Civ.Game;

namespace Civ.Mods.Ai;

internal sealed record CivicReview(CivicGoal? Goal, List<string> Adopt);

/// <summary>Evaluates civic changes with a flavor/effect estimate and adopts them only when the gain outweighs anarchy.</summary>
internal sealed class CivicPlanner(AiContext ctx)
{
    private const int MinimumTurn = 25, GainThreshold = 45, GainPerAnarchyTurn = 60;

    public void Plan()
    {
        if (ctx.Turn >= MinimumTurn && ctx.Cities.Count > 0)
            ctx.Issue(new ReviewCivicGoalCommand { Player = ctx.PlayerId });
    }

    // Command validation computes a detached decision, including live adoption legality and cost.
    internal CivicReview Evaluate()
    {
        if (ctx.Turn < MinimumTurn || ctx.Cities.Count == 0) return new(null, []);
        string dependencies = Dependencies();
        var held = AiIntent.Read(ctx).Civic;
        if (held is not null && held.Dependencies == dependencies
            && AiIntent.Reusable(ctx, held.EvaluatedTurn, held.ReviewTurn)) return new(held, []);
        var change = Changes();
        var goal = new CivicGoal(ctx.Turn, AiIntent.ReviewTurn(ctx), dependencies, change.Civics);
        return new(goal, CanAdopt(change) ? change.Civics : []);
    }

    internal void Commit(CivicReview review)
    {
        if (review.Goal is not { } goal) return;
        if (review.Adopt.Count > 0 && ctx.Issue(new SetCivicsCommand { Player = ctx.PlayerId, Civics = review.Adopt }))
            goal = goal with { Dependencies = Dependencies() };
        AiIntent.Write(ctx).Civic = goal;
    }

    private bool CanAdopt((List<string> Civics, int Gain) change)
    {
        if (change.Civics.Count == 0) return false;
        var government = ctx.Service<IGovernmentService>();
        if (government.Validate(ctx.Sim, ctx.PlayerId, change.Civics) is not null) return false;
        var cost = government.Calculate(ctx.Sim, ctx.PlayerId, change.Civics);
        return change.Gain >= GainPerAnarchyTurn * cost.AnarchyTurns + GainThreshold;
    }

    private string Dependencies()
    {
        var timers = ctx.Self.Extensions.Get<GovernmentPlayerState>(ctx.Sim.Content);
        var age = ctx.Self.Extensions.Get<CityPlayerState>(ctx.Sim.Content);
        return CanonicalJson.Digest(new
        {
            ctx.Self.Leader, ctx.Self.Civilization, ctx.Self.Civics, ctx.Team.Techs, ctx.Team.Wars,
            Cities = ctx.Cities.OrderBy(c => c.Id).Select(c => new { c.Id, c.Position, c.Buildings }),
            Anarchy = timers?.AnarchyTurns > 0, Cooldown = timers?.RevolutionTimer > 0,
            age?.CurrentEra, age?.GoldenAge, Economy = AiIntent.EconomicStatus(ctx),
        });
    }

    private (List<string> Civics, int Gain) Changes()
    {
        var government = ctx.Service<IGovernmentService>();
        var picked = new List<string>(); int gain = 0;
        foreach (var option in ctx.Sim.Content.Get<CivicOptionDef>().All.OrderBy(o => o.Id, StringComparer.Ordinal))
        {
            ctx.Self.Civics.TryGetValue(option.Id, out var held);
            int heldScore = held is null ? 0 : Score(ctx.Sim.Content.Get<CivicDef>(held));
            var best = ctx.Sim.Content.Get<CivicDef>().All.Where(c => c.Option == option.Id && c.Id != held)
                .Select(c => (Civic: c, Score: Score(c))).Where(x => x.Score - heldScore >= GainThreshold)
                .OrderByDescending(x => x.Score).ThenBy(x => x.Civic.Id, StringComparer.Ordinal)
                .FirstOrDefault(x => government.Validate(ctx.Sim, ctx.PlayerId, [x.Civic.Id]) is null);
            if (best.Civic is null) continue;
            picked.Add(best.Civic.Id); gain += best.Score - heldScore;
        }
        return (picked, gain);
    }

    /// <summary>Integer estimate of a civic's worth to this leader and empire size.</summary>
    internal int Score(CivicDef c)
    {
        int cities = Math.Max(1, ctx.Cities.Count), scale = Math.Max(1, (cities + 1) / 2);
        int score = Math.Clamp(c.AiWeight, -100, 100) + ctx.FlavorScore(c.Flavors);
        if (ctx.Leader?.FavoriteCivic == c.Id) score += 120;
        score += (int)(scale * (c.CommerceModifiers.Gold * 3 + c.CommerceModifiers.Research * 4 + c.CommerceModifiers.Culture * 2));
        score += (int)(scale * (c.YieldModifiers.Food * 5 + c.YieldModifiers.Production * 5 + c.YieldModifiers.Commerce * 3));
        score += scale * (c.CivicHappiness * 8 + c.ExtraHealth * 6 + c.PopulationGrowthPercent / 4);
        score += c.BaseFreeUnits * 2 + c.BaseFreeMilitaryUnits * 2 + c.FreeSpecialist * 8 + c.WorkerSpeedModifier / 4;
        score -= UpkeepCost(c) * cities / 25 + Math.Max(0, c.WarWearinessModifier / 10);
        return score;
    }

    private int UpkeepCost(CivicDef c) => c.Upkeep is { } id ? ctx.Sim.Content.Get<UpkeepDef>(id).CityPercent / 2 : 0;
}
