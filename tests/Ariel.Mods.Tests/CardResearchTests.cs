using Ariel.Client;
using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Xunit;
using TechDef = Ariel.Content.Schema.TechDef;

namespace Ariel.Mods.Tests;

// These tests own command admission, hand lifecycle, persistence and presentation at the loaded-DLL boundary.
public sealed class CardResearchTests
{
    private static readonly Lazy<ContentDb> Content = new(() => ModLoader.Build(new LoadedMods().Sources.Concat(
        new[] { "CardResearch", "TechTree", "Diplomacy" }.Select(n => ModLoader.FromDirectory(
            Path.Combine(AppContext.BaseDirectory, "optional-mods", n))))));
    private static Simulation Scenario(bool cards = true, bool tree = true, bool shared = false, bool terminal = false) =>
        Simulation.CreateScenario(Content.Value, GameFactory.Settings() with
        {
            PlayerTeams = shared ? [0, 0] : [0, 1],
            GameOptions = new(StringComparer.Ordinal) { ["card_research:enabled"] = cards, ["tech_tree:enabled"] = tree },
        }, new(12, 8, true), Enumerable.Range(0, 96).Select(_ => new Plot { Terrain = "world:grass" }).ToArray(), sim =>
        {
            foreach (var team in sim.State.Teams)
            {
                team.Techs.Clear();
                if (terminal) team.Techs.UnionWith(sim.Content.Get<TechDef>().All.Select(t => t.Id));
            }
        });
    private static IResearchService Research(Simulation sim) => sim.Rules().Research;
    private static string[] Hand(Simulation sim) => new GameSession(sim).ResearchChoices().Select(t => t.Id).ToArray();
    private static void Apply(Simulation sim, GameCommand command) => Assert.True(sim.TryApply(command, out var error), error);

    [Fact]
    public void Initial_hand_is_distinct_eligible_shared_and_queries_and_rejections_do_not_reroll()
    {
        var sim = Scenario(shared: true);
        var hand = Hand(sim);
        Assert.Equal(4, hand.Length);
        Assert.Equal(4, hand.Distinct().Count());
        Assert.All(hand, t => Assert.True(Research(sim).Validate(sim, 0, t).Success));
        var outside = sim.Content.Get<TechDef>().All.First(t => Research(sim).Validate(sim, 0, t.Id).Success && !hand.Contains(t.Id)).Id;
        var before = SaveGame.Save(sim);
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(hand, Hand(sim));
            Assert.NotNull(new GameSession(sim).DescribeTechTree());
            Assert.False(sim.TryApply(new SetTeamResearchCommand { Player = 0, Tech = outside }, out _));
            Assert.False(sim.TryApply(new SetTeamResearchCommand { Player = 0, Queue = hand[..2] }, out _));
        }
        Assert.Equal(before, SaveGame.Save(sim));
        Apply(sim, new SetTeamResearchCommand { Player = 1, Tech = hand[0] });
        sim.TeamOf(0).Extensions.Get<ResearchTeamState>(sim.Content)!.FreeTechs = 1;
        before = SaveGame.Save(sim);
        Assert.False(sim.TryApply(new SetTeamResearchCommand { Player = 0, Tech = outside, FreeTech = true }, out _));
        Assert.Equal(before, SaveGame.Save(sim));
        Research(sim).Contribute(sim, 1, 1);
        Apply(sim, new SetTeamResearchCommand { Player = 0, Tech = hand[1] });
        Apply(sim, new SetTeamResearchCommand { Player = 0 });
        Assert.Equal(hand, Hand(sim));
        Assert.True(sim.TeamOf(0).Research[hand[0]] > 0);
        sim.TeamOf(0).Extensions.Get<ResearchTeamState>(sim.Content)!.Queue = [outside];
        Research(sim).Contribute(sim, 0, 1);
        Assert.Null(sim.TeamOf(0).Researching);
        Assert.Equal(0, sim.TeamOf(0).Research.GetValueOrDefault(outside));
    }

    [Fact]
    public void Completion_redraws_once_and_save_load_replays_the_same_hand_hash_and_rng()
    {
        var sim = Scenario();
        var twin = Scenario();
        Assert.Equal(sim.ComputeHash(), twin.ComputeHash());
        string tech = Hand(sim)[0];
        Apply(sim, new SetTeamResearchCommand { Player = 0, Tech = tech });
        var saved = SaveGame.Save(sim);
        twin = SaveGame.Load(Content.Value, saved);
        Assert.Equal(saved, SaveGame.Save(twin));
        var rng = sim.Rng.State;
        foreach (var game in new[] { sim, twin }) Research(game).Contribute(game, 0, 100000);
        Assert.NotEqual(rng, sim.Rng.State);
        Assert.Contains(tech, sim.TeamOf(0).Techs);
        Assert.DoesNotContain(tech, Hand(sim));
        Assert.Null(sim.TeamOf(0).Researching);
        Assert.Equal(sim.ComputeHash(), twin.ComputeHash());
        Assert.Equal(Hand(sim), Hand(twin));
        Assert.Equal(SaveGame.Save(sim), SaveGame.Save(twin));
    }

    private static int Generation(Simulation sim, int team) =>
        (int)sim.State.TeamById(team).Extensions.Values["card_research:hand"].GetType().GetProperty("Generation")!.GetValue(sim.State.TeamById(team).Extensions.Values["card_research:hand"])!;

    [Fact]
    public void Traded_cards_leave_without_refilling_or_changing_the_remaining_hand()
    {
        var sim = Scenario();
        var hand = Hand(sim);
        string tech = hand[0];
        Apply(sim, new SetTeamResearchCommand { Player = 0, Tech = tech });
        sim.TeamOf(1).Techs.UnionWith([tech, "research:alphabet"]);
        var diplomacy = sim.Content.Services.Get<IDiplomacyService>().Single();
        diplomacy.EstablishContact(sim, 0, 1);
        var rng = sim.Rng.State;
        int genBefore = Generation(sim, 0);
        Apply(sim, new ProposeDealCommand { Player = 1, Target = 0, Items = [new(1, TradeItemKind.Technology, tech)] });
        Apply(sim, new ExecuteDealCommand { Player = 0, Deal = diplomacy.Deals(sim).Single().Id });
        Assert.Equal(hand.Skip(1), Hand(sim));
        Assert.Null(sim.TeamOf(0).Researching);
        Assert.Equal(rng, sim.Rng.State);
        Assert.Equal(genBefore + 1, Generation(sim, 0));
    }

    [Fact]
    public void Hand_state_sanitizes_duplicates_and_enforces_capacity()
    {
        var sim = Scenario();
        var handObj = sim.State.TeamById(0).Extensions.Values["card_research:hand"];
        var type = handObj.GetType();
        var dealMethod = type.GetMethod("Deal")!;
        dealMethod.Invoke(handObj, [new[] { "research:mining", "research:mining", "", "  ", "research:wheel", "research:pottery", "research:writing", "research:masonry" }]);
        var offered = (List<string>)type.GetProperty("Offered")!.GetValue(handObj)!;
        Assert.Equal(4, offered.Count);
        Assert.Equal(["research:mining", "research:wheel", "research:pottery", "research:writing"], offered);
    }

    [Fact]
    public void Free_selection_obeys_cards_and_repeatable_terminal_hand_remains_playable()
    {
        var sim = Scenario(terminal: true);
        Assert.Equal(["research:future_tech"], Hand(sim));
        Apply(sim, new SetTeamResearchCommand { Player = 0, Tech = "research:future_tech" });
        var state = sim.TeamOf(0).Extensions.Get<ResearchTeamState>(sim.Content)!;
        state.FreeTechs = 1;
        Apply(sim, new SetTeamResearchCommand { Player = 0, Tech = "research:future_tech", FreeTech = true });
        Assert.Equal(0, state.FreeTechs);
        Assert.Equal(1, state.Completions["research:future_tech"]);
        Assert.Equal(["research:future_tech"], Hand(sim));
        Assert.Null(sim.TeamOf(0).Researching);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Independent_options_preserve_normal_research_and_tree_selection_contract(bool cards, bool tree)
    {
        var sim = Scenario(cards, tree);
        var session = new GameSession(sim);
        var before = SaveGame.Save(sim);
        var view = session.DescribeTechTree();
        Assert.Equal(tree, view is not null);
        Assert.Equal(cards, session.ResearchSelection() is not null);
        if (view is not null)
        {
            Assert.Equal(sim.Content.Get<TechDef>().Count, view.Nodes.Count);
            Assert.Contains(view.Nodes, n => !n.Researchable);
            Assert.Contains(view.Nodes, n => n.Requirements is Requirement.All);
            Assert.Equal(session.ResearchChoices().Select(t => t.Id).Order(), view.Nodes.Where(n => n.Selectable).Select(n => n.Id).Order());
            var bronze = view.Nodes.Single(n => n.Id == "research:bronze_working");
            Assert.Equal(sim.Content.Get<TechDef>(bronze.Id).Requirements, bronze.Requirements);
        }
        Assert.Equal(before, SaveGame.Save(sim));
        if (!cards) Assert.True(session.ResearchChoices().Count > 4);
    }

    [Fact]
    public void Standard_ai_selects_from_the_authoritative_hand()
    {
        var sim = new Simulation(Content.Value, GameFactory.Settings() with
        {
            PlayerAis = [null, "ai:standard"],
            GameOptions = new(StringComparer.Ordinal) { ["card_research:enabled"] = true },
        });
        var choice = sim.TeamOf(1).Researching;
        Assert.NotNull(choice);
        Assert.True(Research(sim).ValidateSelection(sim, 1, choice!).Success);
    }
}
