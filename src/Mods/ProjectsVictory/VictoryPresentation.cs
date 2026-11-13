using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Presentation;

namespace Ariel.Mods.ProjectsVictory;

/// <summary>Standings and the game outcome: the score table, who won and how, and the victory rules that are on.</summary>
internal sealed class VictoryScreen : IGameScreen
{
    public string Id => "projects_victory:victory";
    public int Order => 50;

    public ScreenButton? Button(ScreenContext context) => context.Sim.Service<IProjectVictoryService>() is null ? null
        : new("Victory", "Scores, the victory conditions and the outcome");

    public ScreenView Describe(ScreenContext context)
    {
        var sim = context.Sim;
        if (sim.Service<IProjectVictoryService>() is not { } service)
            return new("Victory", [new Paragraph("off", "No victory rules in this ruleset.", Tone.Dim)]);
        var status = service.GetVictoryStatus(sim);
        string? winner = status.IsGameOver && sim.State.Players.Any(p => p.Id == status.WinningPlayerId)
            ? sim.State.PlayerById(status.WinningPlayerId).Name : null;
        string? victory = status.VictoryId is { } v ? Labels.Name<VictoryDef>(sim.Content, v) : null;
        var items = new List<ViewElement>
        {
            new Paragraph("state", status.IsGameOver
                ? $"{winner ?? "No one"} won by {victory ?? "unknown"} on turn {status.TurnWon}; play continues"
                : $"Game in progress  ·  turn limit {status.MaxTurns}", status.IsGameOver ? Tone.Accent : Tone.Normal),
        };
        var conditions = sim.Content.Get<VictoryDef>().All.Where(d => d.Enabled).OrderBy(d => d.Priority).ThenBy(d => d.Id, StringComparer.Ordinal)
            .Select(d => d.Name).ToList();
        if (conditions.Count > 0) items.Add(new Paragraph("conditions", "Victory: " + string.Join(", ", conditions), Tone.Dim, TextSize.Small));
        var rows = sim.State.Players.Select(p => (Player: p, Score: service.GetScore(sim, p.Id)))
            .OrderByDescending(r => r.Score.Total).ThenBy(r => r.Player.Id)
            .Select(r => new TableRow($"player:{r.Player.Id}", [
                r.Player.Name, Labels.Name<CivilizationDef>(sim.Content, r.Player.Civilization), r.Score.Total.ToString(),
                r.Score.Population.ToString(), r.Score.Land.ToString(), r.Score.Techs.ToString(), r.Score.Wonders.ToString()],
                Tone: r.Player.Id == context.Player ? Tone.Accent : Tone.Normal)).ToList();
        items.Add(new Heading("scores", "Score"));
        items.Add(new Table("table", ["Player", "Civilization", "Score", "Population", "Land", "Techs", "Wonders"], rows));
        return new("Victory", items);
    }
}

/// <summary>Projects the player's team completes, and any victory.</summary>
internal sealed class VictoryNotices : INotificationSource
{
    public string Id => "projects_victory:notices";

    public Notice? Describe(NoticeContext context, DomainEvent e) => e switch
    {
        ProjectCompleted p when p.Player == context.Player || p.Team == context.Team =>
            new("project", $"{context.CityName(p.City)} completed {Labels.Name<ProjectDef>(context.Sim.Content, p.Project)}", p.City),
        VictoryAchieved v =>
            new("victory", $"{context.PlayerName(v.Player)} won a {Labels.Name<VictoryDef>(context.Sim.Content, v.Victory)}", Tone: Tone.Accent),
        _ => null,
    };
}

/// <summary>Projects in the encyclopedia, each under the technology it needs.</summary>
internal sealed class ProjectPedia : IPediaSource
{
    public string Id => "projects_victory:projects";
    public string Category => "Projects";
    public IEnumerable<PediaItem> Items(ContentDb content) => content.Get<ProjectDef>().All.Select(d => new PediaItem(d, UnlockedBy: d.TechPrereq));
}

/// <summary>The victory conditions in the encyclopedia.</summary>
internal sealed class VictoryPedia : IPediaSource
{
    public string Id => "projects_victory:victories";
    public string Category => "Victories";
    public bool ListsUnlocks => false;
    public IEnumerable<PediaItem> Items(ContentDb content) => content.Get<VictoryDef>().All.Select(d => new PediaItem(d));
}
