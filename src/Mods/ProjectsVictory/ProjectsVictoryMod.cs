using Ariel.Rules;
using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Modding;
using Ariel.Mods;

namespace Ariel.Mods.ProjectsVictory;

/// <summary>Team and world projects as a production kind, and the space race they win, which the mod adds to the base
/// game's victories as a victory condition.</summary>
public sealed class ProjectsVictoryMod : IPreparableMod
{
    private static readonly ModJsonFile<ProjectDef[]> Projects = new("data/project.json");
    private static readonly ModJsonFile<VictoryDef[]> Victories = new("data/victory.json");
    private static readonly ModJsonFile<CivicProjectsDef[]> Civics = new("data/civic-projects.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [Projects, Victories, Civics];

    public void Register(IModContext context)
    {
        ContentRegistration.Load(context, Projects);
        ContentRegistration.Load(context, Victories);
        ContentRegistration.Load(context, Civics);
        var service = new ProjectService();
        context.RegisterService<IProjectService>("queries", service);
        context.RegisterService<IVictoryCondition>("spaceship", new SpaceshipVictory(service));
        context.RegisterService<Ariel.Presentation.INotificationSource>("notices", new ProjectNotices());
        context.RegisterService<Ariel.Presentation.IPediaSource>("projects", new ProjectPedia());
        context.RegisterService<IProductionKind>("project_kind", new ProjectKind(service));
        context.RegisterService<IProductionCompletion>("project_completion", new ProjectCompletion(service));
        context.RegisterStateComponent<TeamProjectState>("team_state");
        context.Subscribe<ProjectCompleted>("check_project", (sim, _) => sim.Rules().Victory.Evaluate(sim, false));
    }
}

internal sealed class ProjectCompletion(ProjectService service) : IProductionCompletion
{
    public string Kind => ProjectKind.KindId;
    public void Complete(Simulation sim, int city, string content) => service.CompleteProject(sim, city, content);
}

internal sealed class ProjectKind(ProjectService service) : IProductionKind
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
        sim.Rules().CityProduction.Calculate(sim, city, KindId, id);
}
