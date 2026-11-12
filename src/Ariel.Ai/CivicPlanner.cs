using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Ai;

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

    internal const int BundleThreshold = 15;

    /// <summary>Best civic per option, then bundled into one revolution: once one option clears the full threshold,
    /// smaller gains in other options ride along, because the anarchy is paid only once. A civic that needs a state
    /// religion the empire cannot hold is skipped, and a set the rules reject loses its weakest change until it passes.</summary>
    private (List<string> Civics, int Gain) Changes()
    {
        var government = ctx.Service<IGovernmentService>();
        var options = new List<(string Civic, int Gain)>();
        foreach (var option in ctx.Sim.Content.Get<CivicOptionDef>().All.OrderBy(o => o.Id, StringComparer.Ordinal))
        {
            ctx.Self.Civics.TryGetValue(option.Id, out var held);
            int heldScore = held is null ? 0 : Score(ctx.Sim.Content.Get<CivicDef>(held));
            var best = ctx.Sim.Content.Get<CivicDef>().All.Where(c => c.Option == option.Id && c.Id != held && ReligionFits(c))
                .Select(c => (Civic: c, Score: Score(c))).Where(x => x.Score - heldScore >= BundleThreshold)
                .OrderByDescending(x => x.Score).ThenBy(x => x.Civic.Id, StringComparer.Ordinal)
                .FirstOrDefault(x => government.Validate(ctx.Sim, ctx.PlayerId, [x.Civic.Id]) is null);
            if (best.Civic is not null) options.Add((best.Civic.Id, best.Score - heldScore));
        }
        if (!options.Any(o => o.Gain >= GainThreshold)) return ([], 0);
        var picked = options.OrderByDescending(o => o.Gain).ThenBy(o => o.Civic, StringComparer.Ordinal).ToList();
        while (picked.Count > 1 && government.Validate(ctx.Sim, ctx.PlayerId, picked.Select(p => p.Civic).ToList()) is not null)
            picked.RemoveAt(picked.Count - 1);
        if (!picked.Any(o => o.Gain >= GainThreshold)) return ([], 0);
        return (picked.Select(p => p.Civic).OrderBy(c => c, StringComparer.Ordinal).ToList(), picked.Sum(p => p.Gain));
    }

    /// <summary>A civic that needs a state religion is only worth adopting when one of the empire's religions is present.</summary>
    private bool ReligionFits(CivicDef c)
    {
        if (!c.RequiresStateReligion) return true;
        var religion = ctx.Sim.Rules().Religion;
        if (religion is null) return false;
        return religion.StateReligion(ctx.Sim, ctx.PlayerId) is not null
            || ctx.Cities.Any(city => religion.Present(ctx.Sim, city.Id).Count > 0);
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
        if (ctx.AtWarWithPlayers) score += WarValue(c, scale);
        return score;
    }

    /// <summary>Extra worth during a war with another civilization: cheap armies, veteran units, less weariness.
    /// Wars are part of the review dependencies, so entering or leaving one triggers a fresh evaluation.</summary>
    public static int WarValue(CivicDef c, int scale) =>
        c.BaseFreeMilitaryUnits * 4 + c.FreeExperience * 10 + c.ExtraCityDefense / 2 + c.HappyPerMilitaryUnit * 6 * scale
        - c.WarWearinessModifier / 5 + c.UnitCombatProductionModifiers.Values.Sum() / 5 + c.GreatGeneralRateModifier / 10;

    private int UpkeepCost(CivicDef c) => c.Upkeep is { } id ? ctx.Sim.Content.Get<UpkeepDef>(id).CityPercent / 2 : 0;
}
