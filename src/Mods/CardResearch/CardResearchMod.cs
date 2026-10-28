using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using TechDef = Ariel.Content.Schema.TechDef;

namespace Ariel.Mods.CardResearch;

public sealed class CardResearchMod : IMod
{
    public void Register(IModContext context)
    {
        var rules = new CardResearchRules();
        context.RegisterStateComponent<CardHandState>("hand");
        context.RegisterService<IResearchSelectionPolicy>("selection", rules);
        context.RegisterService<IResearchSelectionPresentation>("presentation", rules);
        context.RegisterService<ISetupPolicy>("setup", new CardSetupPolicy());
        context.Subscribe<TurnStarted>("initialize", (sim, _) =>
        {
            foreach (var team in sim.State.Teams.OrderBy(t => t.Id)) rules.Refresh(sim, team.Id);
        });
        context.Subscribe<TechDiscovered>("acquisition", (sim, e) => rules.Reconcile(sim, e.Team, !e.ResearchCompletion));
        context.Subscribe<ResearchCompleted>("completion", (sim, e) => rules.Redraw(sim, e.Team));
    }
}

/// <summary>One saved hand per team, including an initialized empty hand.</summary>
public sealed class CardHandState
{
    public List<string> Offered { get; set; } = [];
    public int Generation { get; set; }
}

internal sealed class CardSetupPolicy : ISetupPolicy
{
    public string? Default(SetupField field) => null;
    public IEnumerable<SetupGameOption> GameOptions(ContentDb content) =>
        [new(CardResearchRules.Option, "Card Research", false, "Choose research from four random eligible technologies. The hand changes after research completes.")];
}

internal sealed class CardResearchRules : IResearchSelectionPolicy, IResearchSelectionPresentation
{
    internal const string Option = "card_research:enabled";
    private static bool Enabled(Simulation sim) => sim.IsOptionEnabled(Option);
    private static CardHandState? Read(Simulation sim, Team team) => team.Extensions.Get<CardHandState>(sim.Content);

    public string? ValidateQueue(Simulation sim, int player, IReadOnlyList<string> queue)
    {
        if (!Enabled(sim) || queue.Count == 0) return null;
        if (queue.Count != 1) return "Card research permits one technology, not a queue";
        return Read(sim, sim.TeamOf(player))?.Offered.Contains(queue[0]) == true
            ? null : "Choose a technology from the offered hand";
    }

    public ResearchSelectionView? Describe(Simulation sim, int player) => !Enabled(sim) ? null :
        new("Choose a research card", ResearchChoiceLayout.Cards, Read(sim, sim.TeamOf(player))?.Offered.ToArray() ?? []);

    public void Refresh(Simulation sim, int team) => Reconcile(sim, team, true);

    internal void Reconcile(Simulation sim, int teamId, bool refillEmpty)
    {
        if (!Enabled(sim)) return;
        var team = sim.State.TeamById(teamId);
        var hand = Read(sim, team);
        if (hand is null) { Redraw(sim, teamId); return; }
        var eligible = Eligible(sim, team).ToHashSet(StringComparer.Ordinal);
        hand.Offered.RemoveAll(id => !eligible.Contains(id));
        RestrictQueue(sim, team, hand);
        // Partial hands do not refill: acquisition cannot exchange the remaining cards.
        if (refillEmpty && hand.Offered.Count == 0 && eligible.Count > 0) Redraw(sim, teamId);
    }

    internal void Redraw(Simulation sim, int teamId)
    {
        if (!Enabled(sim)) return;
        var team = sim.State.TeamById(teamId);
        var pool = Eligible(sim, team).ToList();
        var hand = Read(sim, team) ?? new CardHandState();
        hand.Offered.Clear();
        hand.Generation++;
        for (int i = 0; i < Math.Min(4, pool.Count); i++)
        {
            int pick = i + Uniform(sim.Rng, pool.Count - i);
            (pool[i], pool[pick]) = (pool[pick], pool[i]);
            hand.Offered.Add(pool[i]);
        }
        team.Extensions.Set(sim.Content, hand);
        // A new hand always needs a new selection, even for repeatable technologies.
        var research = team.Extensions.Get<ResearchTeamState>(sim.Content);
        research?.Queue.Clear();
        team.Researching = null;
    }

    private static IEnumerable<string> Eligible(Simulation sim, Team team)
    {
        var research = sim.Content.Services.Get<IResearchService>().Single();
        return sim.Content.Get<TechDef>().All.OrderBy(t => t.Id, StringComparer.Ordinal)
            .Where(t => team.Members.Any(p => research.Validate(sim, p, t.Id).Success)).Select(t => t.Id);
    }

    private static void RestrictQueue(Simulation sim, Team team, CardHandState hand)
    {
        if (team.Researching is { } active && !hand.Offered.Contains(active)) team.Researching = null;
        var state = team.Extensions.Get<ResearchTeamState>(sim.Content);
        if (state is null) return;
        state.Queue.RemoveAll(id => !hand.Offered.Contains(id));
        if (state.Queue.Count > 1) state.Queue.RemoveRange(1, state.Queue.Count - 1);
    }

    // Rejection sampling avoids the modulo bias of the general-purpose bounded RNG helper.
    private static int Uniform(DeterministicRng rng, int count)
    {
        ulong bound = (ulong)count;
        ulong threshold = unchecked(0UL - bound) % bound;
        ulong value;
        do { value = rng.NextUInt64(); } while (value < threshold);
        return (int)(value % bound);
    }
}
