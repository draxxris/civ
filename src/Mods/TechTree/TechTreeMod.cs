using Civ.Content;
using Civ.Game;
using Civ.Modding;
using TechDef = Civ.Content.Schema.TechDef;
using EraDef = Civ.Content.Schema.EraDef;

namespace Civ.Mods.TechTree;

public sealed class TechTreeMod : IMod
{
    public void Register(IModContext context)
    {
        context.RegisterService<ITechTreePresentation>("presentation", new TechTreePresentation());
        context.RegisterService<ISetupPolicy>("setup", new TreeSetupPolicy());
    }
}

internal sealed class TreeSetupPolicy : ISetupPolicy
{
    public string? Default(SetupField field) => null;
    public IEnumerable<SetupGameOption> GameOptions(ContentDb content) =>
        [new(TechTreePresentation.Option, "Tech Tree", false, "Browse the technology tree and its prerequisites. Select eligible research from the tree.")];
}

internal sealed class TechTreePresentation : ITechTreePresentation
{
    internal const string Option = "tech_tree:enabled";
    public TechTreeView? Describe(Simulation sim, int player)
    {
        if (!sim.IsOptionEnabled(Option)) return null;
        var research = sim.Content.Services.Get<IResearchService>().Single();
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
