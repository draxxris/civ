using System.Numerics;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Rules.Research;

internal static class ResearchDiffusion
{
    internal static int RateModifier(Simulation sim, Team team, string tech)
    {
        var rules = sim.Content.Get<RuleConstantsDef>().All.Single().Values;
        var relations = sim.Rules().Relations;
        var living = sim.State.Players.Where(p => Eligible(sim, p)).ToArray();
        var teams = sim.State.Teams.Where(t => living.Any(p => p.TeamId == t.Id)).ToArray();
        if (teams.Length == 0) return 100;
        var contacted = teams.Where(t => HasMet(sim, relations, team, t.Id)).ToArray();
        var holders = contacted.Where(t => t.Techs.Contains(tech)).ToArray();
        if (sim.IsOptionEnabled("foundation:gameoption_no_tech_diffusion") || rules.ResearchDiffusionEnabled == 0)
            return checked((int)Math.Max(1, 100L + (long)rules.ResearchKnownTeamsPercent * holders.Length / teams.Length));
        if (holders.Length == 0) return 100;
        // Half units: normal contact 0.5, open borders 2, war 1. No vassal contract exists.
        long halves = holders.Sum(t => Weight(sim, relations, team, t.Id));
        BigInteger numerator = (BigInteger)halves * contacted.Length;
        BigInteger denominator = 2 * (BigInteger)teams.Length;
        if ((long)holders.Length * 3 < teams.Length) denominator *= 100;
        else if ((long)holders.Length * 3 > 2L * teams.Length) numerator *= 3;
        long bonus = ResearchPower.DiffusionBonus(rules.ResearchDiffusionKnownTeamsPercent, numerator, denominator);
        return checked((int)(100 + bonus + Welfare(sim, team, living, contacted, rules)));
    }

    private static bool Eligible(Simulation sim, Player player) => ResearchTimeline.Alive(sim, player.Id)
        && !PlayerRules.IsBarbarian(sim, player.Id);

    private static bool HasMet(Simulation sim, IRelationsService relations, Team team, int other) =>
        relations.HasMet(sim, team.Id, other);

    private static long Weight(Simulation sim, IRelationsService relations, Team team, int other)
    {
        if (other == team.Id) return 1;
        if (relations.MayEnter(sim, team.Id, other)) return 4;
        return team.Wars.Contains(other) ? 2 : 1;
    }

    private static long Welfare(Simulation sim, Team team, Player[] living, Team[] contacted, RuleConstants rules)
    {
        var scores = sim.Rules().ProjectVictory;
        if (scores is null) return 0; // Custom rulesets without a score owner have no welfare score factor.
        var eras = ResearchTimeline.Eras(sim);
        var weights = sim.Content.Get<TechDef>().All.ToDictionary(t => t.Id,
            t => Array.FindIndex(eras, e => e.Id == t.Era) + 1, StringComparer.Ordinal);
        if (weights.Values.Any(w => w == 0)) return 0; // Missing era semantics must not become guessed counts.
        long ourTech = TechScore(team, weights);
        long bestTech = contacted.Select(t => TechScore(t, weights)).Append(ourTech).Max();
        int techPercent = checked((int)(100 * ourTech / Math.Max(1, bestTech)));
        if (techPercent >= rules.ResearchDiffusionWelfareThreshold) return 0;
        int leader = ResearchTimeline.Leader(sim, team);
        long ourScore = scores.GetScore(sim, leader).Total;
        long bestScore = living.Where(p => p.Id != leader).Select(p => (long)scores.GetScore(sim, p.Id).Total).DefaultIfEmpty().Max();
        // Native division by a zero total is undefined. Use neutral welfare, not infinite assistance.
        if (ourScore <= 0 || ourScore >= bestScore) return 0;
        long welfare = ResearchPower.WelfareBonus(rules.ResearchDiffusionWelfarePercent,
            checked(rules.ResearchDiffusionWelfareThreshold - techPercent));
        return checked((long)((Int128)welfare * bestScore / ourScore));
    }

    // CvGameCoreUtils::getTechScore is era index + 1. PlayerScore.Techs is a count,
    // not that native score; only PlayerScore.Total supplies the welfare score factor.
    private static long TechScore(Team team, Dictionary<string, int> weights) =>
        team.Techs.Sum(t => (long)weights.GetValueOrDefault(t));
}
