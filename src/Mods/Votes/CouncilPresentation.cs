using Ariel.Content;
using Ariel.Game;
using Ariel.Presentation;

namespace Ariel.Mods.Votes;

/// <summary>The councils that sit: who leads them, what stands, and the vote they are holding, with the local player's
/// ballot and, for the secretary, the resolutions it may put.</summary>
internal sealed class CouncilScreen : IGameScreen
{
    public string Id => "votes:councils";
    public int Order => 40;

    public ScreenButton? Button(ScreenContext context) =>
        context.Sim.Service<IVotesService>() is { } votes && votes.Councils(context.Sim).Count > 0
            ? new("Councils", "The United Nations and other councils: elections, resolutions and your vote") : null;

    public ScreenView Describe(ScreenContext context)
    {
        var sim = context.Sim;
        if (sim.Service<IVotesService>() is not { } votes || votes.Councils(sim).Count == 0)
            return new("Councils", [new Paragraph("none", "No council sits yet.", Tone.Dim)]);
        var body = votes.Councils(sim).Select(source => (ViewElement)Council(context, votes, source)).ToList();
        return new("Councils", body);
    }

    private static Section Council(ScreenContext context, IVotesService votes, string source)
    {
        var sim = context.Sim;
        var council = votes.Council(sim, source)!;
        int me = context.Player;
        var items = new List<ViewElement>();
        if (council.Religion is { } religion) items.Add(new StatLine($"{source}:religion", "Faith", Labels.Name<Ariel.Mods.Religion.ReligionDef>(sim.Content, religion)));
        items.Add(new StatLine($"{source}:secretary", "Secretary", council.Secretary is { } team ? TeamName(context, team) : "none"));
        items.Add(new StatLine($"{source}:member", "You",
            votes.IsFullMember(sim, me, source) ? $"full member, {votes.Votes(sim, me, source)} votes"
            : votes.IsVotingMember(sim, me, source) ? $"voting member, {votes.Votes(sim, me, source)} votes" : "not a member"));
        if (council.Session is null && !council.AwaitingProposal)
            items.Add(new StatLine($"{source}:next", "Next session", council.TurnsToSession <= 0 ? "this turn" : $"in {council.TurnsToSession} turns"));
        if (council.Standing.Count > 0)
            items.Add(new Paragraph($"{source}:standing", "In force: " + string.Join("; ", council.Standing.Select(v => sim.Content.Get<VoteDef>(v).Name)), Tone.Dim, TextSize.Small));
        if (council.Sanctioned.Count > 0)
            items.Add(new Paragraph($"{source}:sanctions", "Under sanctions: " + string.Join(", ", council.Sanctioned.Select(t => TeamName(context, t))), Tone.Warn, TextSize.Small));
        if (council.Defiant.Count > 0)
            items.Add(new Paragraph($"{source}:defiant", "Defied the council: " + string.Join(", ", council.Defiant.Select(p => PlayerName(context, p))), Tone.Dim, TextSize.Small));
        if (council.Session is { } session) items.AddRange(Session(context, votes, source, session));
        else if (council.AwaitingProposal) items.AddRange(Proposals(context, votes, source, council));
        return new(source, sim.Content.Get<VoteSourceDef>(source).Name, items);
    }

    private static IEnumerable<ViewElement> Session(ScreenContext context, IVotesService votes, string source, VoteSession session)
    {
        var sim = context.Sim;
        int me = context.Player;
        var resolution = session.Resolution;
        var def = sim.Content.Get<VoteDef>(resolution.Vote);
        yield return new Heading($"{source}:vote", Describe(context, resolution));
        yield return new Paragraph($"{source}:needs",
            $"Needs {votes.Required(sim, source, def.Id)} of {votes.Possible(sim, source, def.Id)} votes; voting ends this turn.", Tone.Dim, TextSize.Small);
        if (!votes.IsVotingMember(sim, me, source)) yield break;
        var cast = session.Ballots.GetValueOrDefault(me);
        if (cast is not null) yield return new Paragraph($"{source}:cast", "Your vote: " + Ballot(context, cast), Tone.Accent);
        ActionButton Vote(string id, string label, VoteChoice choice, int candidate = -1, string? tooltip = null)
        {
            var ballot = new Ballot(choice, candidate);
            return new($"{source}:{id}", label, new SubmitCommand(new CastVoteCommand { Player = me, Source = source, Choice = choice, Candidate = candidate }),
                votes.ValidateBallot(sim, me, source, ballot), tooltip, Selected: cast == ballot);
        }
        var buttons = new List<ViewElement>();
        if (def.IsElection)
            buttons.AddRange(votes.Candidates(sim, source).Select(t => Vote($"elect:{t}", TeamName(context, t), VoteChoice.Yes, t)));
        else
        {
            buttons.Add(Vote("yes", "Yes", VoteChoice.Yes));
            buttons.Add(Vote("no", "No", VoteChoice.No));
            buttons.Add(Vote("defy", "Defy", VoteChoice.Defy, tooltip: "Block the resolution if it passes; you stop being a full member until you next back one"));
        }
        buttons.Add(Vote("abstain", "Abstain", VoteChoice.Abstain));
        yield return new Row($"{source}:ballot", buttons);
    }

    private static IEnumerable<ViewElement> Proposals(ScreenContext context, IVotesService votes, string source, CouncilView council)
    {
        var sim = context.Sim;
        if (council.Secretary != sim.State.PlayerById(context.Player).TeamId)
        {
            yield return new Paragraph($"{source}:waiting", "The secretary is choosing a resolution.", Tone.Dim);
            yield break;
        }
        yield return new Heading($"{source}:propose", "Put a resolution to the council this turn");
        foreach (var resolution in votes.Proposals(sim, source))
            yield return new ActionButton($"{source}:propose:{resolution.Vote}:{resolution.Target}", Describe(context, resolution),
                new SubmitCommand(new ProposeResolutionCommand { Player = context.Player, Source = source, Vote = resolution.Vote, Target = resolution.Target }),
                Tooltip: $"Needs {votes.Required(sim, source, resolution.Vote)} of {votes.Possible(sim, source, resolution.Vote)} votes");
    }

    internal static string Describe(ScreenContext context, Resolution resolution)
    {
        string name = context.Sim.Content.Get<VoteDef>(resolution.Vote).Name;
        return resolution.Target >= 0 ? $"{name}: {PlayerName(context, resolution.Target)}" : name;
    }

    private static string Ballot(ScreenContext context, Ballot ballot) => ballot.Choice switch
    {
        VoteChoice.Yes when ballot.Candidate >= 0 => TeamName(context, ballot.Candidate),
        var choice => choice.ToString(),
    };

    private static string PlayerName(ScreenContext context, int player) => player == context.Player ? "You"
        : context.Sim.State.Players.FirstOrDefault(p => p.Id == player)?.Name ?? $"Player {player}";

    private static string TeamName(ScreenContext context, int team) =>
        string.Join(" & ", context.Sim.State.TeamById(team).Members.Order().Select(p => PlayerName(context, p)));
}

/// <summary>A council founded, a vote the player can cast, and every result.</summary>
internal sealed class CouncilNotices : INotificationSource
{
    public string Id => "votes:notices";

    public Notice? Describe(NoticeContext context, DomainEvent e)
    {
        var sim = context.Sim;
        string Name(string source) => sim.Content.Get<VoteSourceDef>(source).Name;
        string Text(Resolution r) => sim.Content.Get<VoteDef>(r.Vote).Name + (r.Target >= 0 ? $" ({context.PlayerName(r.Target)})" : "");
        return e switch
        {
            CouncilFounded f => new("council", $"The {Name(f.Source)} sits for the first time", Tone: Tone.Accent),
            SessionOpened o when sim.Service<IVotesService>()?.IsVotingMember(sim, context.Player, o.Source) == true =>
                new("council", $"The {Name(o.Source)} votes this turn: {Text(o.Resolution)}"),
            ResolutionDecided d => new("council", d.Winner >= 0
                ? $"The {Name(d.Source)} elected {string.Join(" & ", sim.State.TeamById(d.Winner).Members.Order().Select(context.PlayerName))}: {Text(d.Resolution)}"
                : $"The {Name(d.Source)} {(d.Passed ? "passed" : "rejected")} {Text(d.Resolution)}", Tone: d.Passed ? Tone.Accent : Tone.Normal),
            _ => null,
        };
    }
}

/// <summary>The councils and their votes in the encyclopedia.</summary>
internal sealed class CouncilPedia : IPediaSource
{
    public string Id => "votes:councils";
    public string Category => "Councils";
    public bool ListsUnlocks => false;
    public IEnumerable<PediaItem> Items(ContentDb content) =>
        content.Get<VoteSourceDef>().All.Select(d => new PediaItem(d))
            .Concat(content.Get<VoteDef>().All.Where(v => v.IsSupported).Select(d => new PediaItem(d)));
}
