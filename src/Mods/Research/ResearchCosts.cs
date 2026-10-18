using Civ.Content;
using Civ.Content.Schema;
using Civ.Game;
using TechDef = Civ.Content.Schema.TechDef;

namespace Civ.Mods.Research;

internal static class ResearchCosts
{
    internal static ResearchCostResult Calculate(Simulation sim, int teamId, string tech)
    {
        var team = sim.State.TeamById(teamId);
        var handicap = sim.Content.Get<HandicapDef>(sim.Settings.Handicap);
        var constants = sim.Content.Get<RuleConstantsDef>().All.Single().Values;
        var lines = new List<LedgerLine>();
var definition = sim.Content.Get<TechDef>(tech);
        long cost = definition.Cost;
        lines.Add(new(tech, "research:base_cost", cost));
        Scale("research:global_percent", Math.Max(0, 100L + constants.ResearchCostPercent));
        int eraModifier = sim.Content.TryGet<EraDef>(definition.Era ?? "", out var era) ? era.TechCostModifier : 0;
        Scale("research:technology_era_percent", Math.Max(0, 100L + eraModifier));
        // Preserve the native large-cost guard as well as each sequential truncation.
        int finalMultiplier = cost > int.MaxValue / 10000 ? 10000 : 1;
        cost /= finalMultiplier;
        Scale("research:handicap_percent", team.Members.Any(p => sim.State.PlayerById(p).Ai is null) ? handicap.ResearchPercent : handicap.AiResearchPercent);
        Scale("research:world_percent", sim.Content.Get<WorldSizeDef>(sim.Settings.WorldSize!).ResearchPercent);
        Scale("research:speed_percent", sim.Content.Get<GameSpeedDef>(sim.Settings.GameSpeed).ResearchPercent);
        Scale("research:start_era_percent", sim.Content.Get<EraDef>(sim.Settings.StartingEra).ResearchPercent);
        Scale("research:team_percent", Math.Max(0, 100L + (long)constants.ResearchExtraTeamMemberPercent * (team.Members.Count - 1)));
        var modifiers = ModifierSink.Collect(new(sim, ModifierScope.Team, team.Members.Min, Team: team));
        Scale("research:cost_percent", Math.Max(0, 100 + modifiers.Ledger.Total("research:cost_percent")));
        if (!sim.IsOptionEnabled("foundation:gameoption_no_revolution"))
            Scale("research:revolution_percent", constants.RevolutionResearchCostPercent);
        Scale("research:timeline_percent", ResearchTimeline.Percent(sim, team, lines));
        Scale("research:barbarian_percent", BarbarianPercent(sim, team));
        cost = Math.Max(1, checked(cost * finalMultiplier));
        lines.Add(new(tech, "research:cost", cost));
        return new(cost, new(lines));

        void Scale(string key, long percent)
        {
            if (percent < 0) throw new InvalidDataException("Research cost percentages must not be negative");
            cost = checked((long)((Int128)cost * percent / 100));
            lines.Add(new("research:rules", key, percent));
            lines.Add(new("research:rules", key + ":cost", cost));
        }
    }
    private static int BarbarianPercent(Simulation sim, Team team)
    {
        if (!team.Members.Any(p => sim.State.PlayerById(p).Extensions.TryGet<MilitaryPlayerState>(sim.Content)?.Barbarian == true)) return 100;
        return sim.IsOptionEnabled("foundation:gameoption_raging_barbarians") || sim.IsOptionEnabled("foundation:gameoption_barbarian_world") ? 1200 : 300;
    }

    internal static int RateModifier(Simulation sim, Team team, string tech)
    {
        return ResearchDiffusion.RateModifier(sim, team, tech);
    }
}
