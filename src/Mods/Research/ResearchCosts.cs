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
        long cost = sim.Content.Get<TechDef>(tech).Cost;
        lines.Add(new(tech, "research:base_cost", cost));
        // Preserve the native large-cost guard as well as each sequential truncation.
        int finalMultiplier = cost > int.MaxValue / 10000 ? 10000 : 1;
        cost /= finalMultiplier;
        Scale("research:handicap_percent", team.Members.Any(p => sim.State.PlayerById(p).Ai is null) ? handicap.ResearchPercent : handicap.AiResearchPercent);
        Scale("research:world_percent", sim.Content.Get<WorldSizeDef>(sim.Settings.WorldSize!).ResearchPercent);
        Scale("research:speed_percent", sim.Content.Get<GameSpeedDef>(sim.Settings.GameSpeed).ResearchPercent);
        Scale("research:start_era_percent", sim.Content.Get<EraDef>(sim.Settings.StartingEra).ResearchPercent);
        Scale("research:team_percent", Math.Max(0, 100 + constants.ResearchExtraTeamMemberPercent * (team.Members.Count - 1)));
        var modifiers = ModifierSink.Collect(new(sim, ModifierScope.Team, team.Members.Min, Team: team));
        Scale("research:cost_percent", Math.Max(0, 100 + modifiers.Ledger.Total("research:cost_percent")));
        cost = Math.Max(1, cost * finalMultiplier);
        lines.Add(new(tech, "research:cost", cost));
        return new(cost, new(lines));

        void Scale(string key, long percent)
        {
            cost = cost * percent / 100;
            lines.Add(new("research:rules", key, percent));
            lines.Add(new("research:rules", key + ":cost", cost));
        }
    }
    internal static int RateModifier(Simulation sim, Team team, string tech)
    {
        var met = team.Extensions.Get<ResearchTeamState>(sim.Content)?.MetTeams;
        int holders = sim.State.Teams.Count(t => t.Techs.Contains(tech) && (met?.Contains(t.Id) ?? false));
        int percent = sim.Content.Get<RuleConstantsDef>().All.Single().Values.ResearchKnownTeamsPercent;
        return Math.Max(1, 100 + percent * holders / Math.Max(1, sim.State.Teams.Count));
    }
}
