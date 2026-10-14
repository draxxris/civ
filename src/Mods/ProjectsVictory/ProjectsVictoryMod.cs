using Civ.Content;
using Civ.Content.Schema;
using Civ.Game;
using Civ.Modding;
using Civ.Mods;

namespace Civ.Mods.ProjectsVictory;

/// <summary>Projects as a production kind, plus the executable victory conditions.</summary>
public sealed class ProjectsVictoryMod : IMod
{
    public void Register(IModContext context)
    {
        ContentRegistration.Load<ProjectDef>(context, "project.json");
        ContentRegistration.Load<VictoryDef>(context, "victory.json");
        var service = new ProjectVictoryService();
        context.RegisterService<IProjectVictoryService>("queries", service);
        context.RegisterService<IProductionKind>("project_kind", new ProjectKind(service));
        context.RegisterService<IProductionCompletion>("project_completion", new ProjectCompletion(service));
        context.RegisterStateComponent<VictoryGameState>("game_state");
        context.RegisterStateComponent<TeamProjectState>("team_state");
        context.Subscribe<CityCaptured>("check_capture", (sim, _) => service.Evaluate(sim, false));
        context.Subscribe<CityRazed>("check_raze", (sim, _) => service.Evaluate(sim, false));
        context.Subscribe<ProjectCompleted>("check_project", (sim, _) => service.Evaluate(sim, false));
        context.TurnSystems.Register(new VictoryCheck(service));
    }
}

internal sealed class VictoryCheck(ProjectVictoryService service) : ITurnSystem
{
    public string Id => "projects_victory:check";
    public int Order => TurnPhase.GameEnd;
    public void Run(Simulation sim) => service.Evaluate(sim, true);
}

internal sealed class ProjectCompletion(ProjectVictoryService service) : IProductionCompletion
{
    public string Kind => ProjectKind.KindId;
    public void Complete(Simulation sim, int city, string content) => service.CompleteProject(sim, city, content);
}

internal sealed class ProjectKind(ProjectVictoryService service) : IProductionKind
{
    internal const string KindId = "projects_victory:project";
    private static readonly string[] Hidden = ["Unknown project", "Project cannot be built", "The game is over"];
    public string Id => KindId;
    public string Label => "Projects";
    public bool IsContinuous => false;
    public IEnumerable<ContentDef> Candidates(ContentDb content) => content.Get<ProjectDef>().All.Where(p => p.Cost >= 0);
    public ContentDef Definition(ContentDb content, string id) => content.Get<ProjectDef>(id);
    public long BaseCost(ContentDef definition) => ((ProjectDef)definition).Cost;
    public bool IsVisible(RequirementResult eligibility) => !eligibility.Failures.Any(f => Hidden.Contains(f.Explanation));
    public RequirementResult Validate(Simulation sim, int city, string id) => service.ValidateProject(sim, city, id);
    public CityProductionResult Calculate(Simulation sim, int city, string id) =>
        sim.Content.Services.Get<ICityProductionService>().Single().Calculate(sim, city, KindId, id);
}
