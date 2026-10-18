using Civ.Content.Schema;
using Civ.Modding;
using Civ.Game;

namespace Civ.Mods.Research;

/// <summary>Team research, discoveries, and technology requirements.</summary>
public sealed class ResearchMod : IPreparableMod
{
    private static readonly ModJsonFile<TechDef[]> Technologies = new("data/tech.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [Technologies];

    public void Register(IModContext context)
    {
        ContentRegistration.Load(context, Technologies);
        ContentRegistration.Evaluator<TechDef>(context, "tech", (leaf, subject) => subject.Sim.KnowsTech(subject.Player, leaf.Id));
        var research = new ResearchService();
        context.RegisterService<IResearchService>("research", research);
        context.RegisterStateComponent<ResearchTeamState>("team_state");
        context.RegisterCommand<SetTeamResearchCommand>("set_research");
        context.TurnSystems.Register(new ResearchPhase(research));
    }
}
