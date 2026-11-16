using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Rules.Research;

/// <summary>The setup switch that turns on the browsable technology tree. Off by default; ordinary research and any
/// research selection mod work with or without it.</summary>
internal sealed class TechTreeSetupPolicy : ISetupPolicy
{
    public string? Default(SetupField field) => null;
    public IEnumerable<SetupGameOption> GameOptions(ContentDb content) =>
        [new(TechTreePresentation.Option, "Tech Tree", false, "Browse the technology tree and its prerequisites. Select eligible research from the tree.")];
}

/// <summary>The whole technology tree as one player sees it, while the game option is on: every technology with its
/// place in the grid, its requirements, and whether the player knows, may research or may select it now.</summary>
internal sealed class TechTreePresentation : ITechTreePresentation
{
    internal const string Option = "research:tech_tree";

    public TechTreeView? Describe(Simulation sim, int player)
    {
        if (!sim.IsOptionEnabled(Option)) return null;
        var research = sim.Rules().Research;
        var team = sim.TeamOf(player);
        var nodes = sim.Content.Get<TechDef>().All.OrderBy(t => t.Id, StringComparer.Ordinal).Select(t =>
        {
            var eligibility = research.Validate(sim, player, t.Id);
            var selection = research.ValidateSelection(sim, player, t.Id);
            string era = t.Era is { } id && sim.Content.TryGet<EraDef>(id, out var def) ? def.Name : "";
            return new TechTreeNode(t.Id, t.Name, era, t.Help, t.GridX, t.GridY, t.Requirements,
                team.Techs.Contains(t.Id), t.Repeat, eligibility.Success, selection.Success, team.Researching == t.Id,
                research.CalculateCost(sim, team.Id, t.Id).Cost, team.Research.GetValueOrDefault(t.Id),
                selection.Failures.Select(f => f.Explanation).ToArray());
        }).ToArray();
        return new(nodes);
    }
}
