using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Presentation;

namespace Ariel.Rules.Victory;

/// <summary>Standings and the game outcome: the score table, who won and how, and the victories that are on.</summary>
internal sealed class VictoryScreen : IGameScreen
{
    public string Id => "victory:screen";
    public int Order => 50;

    public ScreenButton? Button(ScreenContext context) => new("Victory", "Scores, the victory conditions and the outcome");

    public ScreenView Describe(ScreenContext context)
    {
        var sim = context.Sim;
        var rules = sim.Rules();
        var status = rules.Victory.Status(sim);
        string? winner = status.IsGameOver && sim.State.Players.Any(p => p.Id == status.WinningPlayerId)
            ? sim.State.PlayerById(status.WinningPlayerId).Name : null;
        string? victory = status.VictoryId is { } v ? Labels.Name<VictoryDef>(sim.Content, v) : null;
        var items = new List<ViewElement>
        {
            new Paragraph("state", status.IsGameOver
                ? $"{winner ?? "No one"} won by {victory ?? "unknown"} on turn {status.TurnWon}; play continues"
                : status.MaxTurns > 0 ? $"Game in progress  ·  turn limit {status.MaxTurns}" : "Game in progress",
                status.IsGameOver ? Tone.Accent : Tone.Normal),
        };
        var conditions = sim.Content.Get<VictoryDef>().All.Where(d => d.Enabled).OrderBy(d => d.Priority).ThenBy(d => d.Id, StringComparer.Ordinal)
            .Select(d => d.Name).ToList();
        if (conditions.Count > 0) items.Add(new Paragraph("conditions", "Victory: " + string.Join(", ", conditions), Tone.Dim, TextSize.Small));
        var rows = sim.State.Players.Select(p => (Player: p, Score: rules.Scores.Breakdown(sim, p.Id)))
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

/// <summary>A victory, announced to everyone.</summary>
internal sealed class VictoryNotices : INotificationSource
{
    public string Id => "victory:notices";

    public Notice? Describe(NoticeContext context, DomainEvent e) => e is VictoryAchieved v
        ? new("victory", $"{context.PlayerName(v.Player)} won a {Labels.Name<VictoryDef>(context.Sim.Content, v.Victory)}", Tone: Tone.Accent)
        : null;
}

/// <summary>The victory conditions in the encyclopedia.</summary>
internal sealed class VictoryPedia : IPediaSource
{
    public string Id => "victory:victories";
    public string Category => "Victories";
    public bool ListsUnlocks => false;
    public IEnumerable<PediaItem> Items(ContentDb content) => content.Get<VictoryDef>().All.Select(d => new PediaItem(d));
}
