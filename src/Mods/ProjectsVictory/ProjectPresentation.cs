using Ariel.Content;
using Ariel.Game;
using Ariel.Presentation;

namespace Ariel.Mods.ProjectsVictory;

/// <summary>Projects the player's team completes.</summary>
internal sealed class ProjectNotices : INotificationSource
{
    public string Id => "projects_victory:notices";

    public Notice? Describe(NoticeContext context, DomainEvent e) => e is ProjectCompleted p && (p.Player == context.Player || p.Team == context.Team)
        ? new("project", $"{context.CityName(p.City)} completed {Labels.Name<ProjectDef>(context.Sim.Content, p.Project)}", p.City)
        : null;
}

/// <summary>Projects in the encyclopedia, each under the technology it needs.</summary>
internal sealed class ProjectPedia : IPediaSource
{
    public string Id => "projects_victory:projects";
    public string Category => "Projects";
    public IEnumerable<PediaItem> Items(ContentDb content) => content.Get<ProjectDef>().All.Select(d => new PediaItem(d, UnlockedBy: d.TechPrereq));
}
