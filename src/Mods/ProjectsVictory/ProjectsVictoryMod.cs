using Civ.Content;
using Civ.Content.Schema;
using Civ.Game;
using Civ.Modding;
using Civ.Mods;

namespace Civ.Mods.ProjectsVictory;

/// <summary>Projects as a production kind, plus the executable victory conditions.</summary>
public sealed class ProjectsVictoryMod : IPreparableMod
{
    private static readonly ModJsonFile<ProjectDef[]> Projects = new("data/project.json");
    private static readonly ModJsonFile<VictoryDef[]> Victories = new("data/victory.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [Projects, Victories];

    public void Register(IModContext context)
    {
        ContentRegistration.Load(context, Projects);
        ContentRegistration.Load(context, Victories);
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
    /// <summary>A project the team can build only a few more times is covered by the copies already queued: by this
    /// player's cities when a world cap is the tightest limit, or by the whole team's when the team's own cap is. One
    /// with copies to spare keeps the room it has left, so the last few copies can be queued at once without letting the
    /// queues hold more than the team may ever finish.</summary>
    public QueueScope QueuedScope(Simulation sim, int city, ContentDef definition)
    {
        var (copies, shared) = service.ProjectRoom(sim, city, definition.Id);
        return copies == int.MaxValue ? QueueScope.None : shared ? QueueScope.Team : QueueScope.Player;
    }
    /// <summary>The copies the team may still build, capped by every limit <see cref="Validate"/> enforces: a team may
    /// queue its last few copies in parallel, but never more than it can complete.</summary>
    public int QueuedCopies(Simulation sim, int city, ContentDef definition) =>
        service.ProjectRoom(sim, city, definition.Id).Copies;
    public IEnumerable<ContentDef> Candidates(ContentDb content) => content.Get<ProjectDef>().All.Where(p => p.Cost >= 0);
    public ContentDef Definition(ContentDb content, string id) => content.Get<ProjectDef>(id);
    public long BaseCost(ContentDef definition) => ((ProjectDef)definition).Cost;
    public bool IsVisible(RequirementResult eligibility) => !eligibility.Failures.Any(f => Hidden.Contains(f.Explanation));
    public RequirementResult Validate(Simulation sim, int city, string id) => service.ValidateProject(sim, city, id);
    public IReadOnlyList<RequirementResult> ValidateBatch(Simulation sim, int city, IReadOnlyList<string> ids) =>
        ids.Select(id => Validate(sim, city, id)).ToArray();
    public CityProductionResult Calculate(Simulation sim, int city, string id) =>
        sim.Content.Services.Get<ICityProductionService>().Single().Calculate(sim, city, KindId, id);
}
