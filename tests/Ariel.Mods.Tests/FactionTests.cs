using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Xunit;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Tests;

/// <summary>A faction is a row attached to a civilization. The base rules read the row's restrictions and never ask
/// which faction a player is, so a second faction beside the barbarians needs data only.</summary>
public sealed class FactionTests
{
    private const string Invaders = "invasion_probe:invaders";
    private const string InvaderCivilization = "invasion_probe:invader_state";

    // xunit builds a new test class instance per test, so the probe content must be static to be built once.
    private static readonly Lazy<ContentDb> Content = new(() =>
    {
        var loaded = new LoadedMods();
        var probe = new ModSource
        {
            Manifest = new() { Id = "invasion_probe", Version = "1", References = ["peoples"] },
            Root = loaded.Root, Instance = new InvasionProbe(),
        };
        return ModLoader.Build(loaded.Sources.Append(probe));
    });

    private static Simulation Scenario()
    {
        var sim = GameFactory.CreateAi(Content.Value, players: 3);
        return sim;
    }
    private static S.FactionDef Invader(Simulation sim) => sim.Content.Get<S.FactionDef>(Invaders);

    [Fact]
    public void A_player_is_a_faction_by_its_civilization_and_nations_have_no_restrictions()
    {
        var sim = Scenario();
        Assert.All(sim.State.Players, p =>
        {
            Assert.True(PlayerRules.IsNation(sim, p.Id));
            Assert.True(PlayerRules.Negotiates(sim, p.Id));
            Assert.False(PlayerRules.AlwaysAtWar(sim, p.Id));
            Assert.False(PlayerRules.IsHandicapFoe(sim, p.Id));
        });
        Assert.Null(GameFactory.BarbarianPlayer(sim));
        Assert.Null(PlayerRules.FactionPlayer(sim, Invader(sim)));
    }

    [Fact]
    public void Each_faction_gets_one_player_of_its_own_with_its_own_restrictions()
    {
        var sim = Scenario();
        int nations = sim.State.Players.Count;
        int barbarian = GameFactory.EnsureBarbarian(sim);
        int invader = sim.Rules().Peoples.EnsureFaction(sim, Invader(sim));

        Assert.NotEqual(barbarian, invader);
        Assert.Equal(nations + 2, sim.State.Players.Count);
        Assert.Equal(barbarian, GameFactory.EnsureBarbarian(sim));
        Assert.Equal(invader, sim.Rules().Peoples.EnsureFaction(sim, Invader(sim)));
        Assert.Equal(nations + 2, sim.State.Players.Count);
        Assert.Equal("Invaders", sim.State.PlayerById(invader).Name);
        Assert.Equal(InvaderCivilization, sim.State.PlayerById(invader).Civilization);

        // Barbarians: never met, the difficulty level's combat rules, dearer research.
        Assert.False(PlayerRules.IsNation(sim, barbarian));
        Assert.False(PlayerRules.Negotiates(sim, barbarian));
        Assert.True(PlayerRules.AlwaysAtWar(sim, barbarian));
        Assert.True(PlayerRules.IsHandicapFoe(sim, barbarian));
        // The probe's invaders: also always at war, but an even fight that can be met.
        Assert.False(PlayerRules.IsNation(sim, invader));
        Assert.True(PlayerRules.Negotiates(sim, invader));
        Assert.True(PlayerRules.AlwaysAtWar(sim, invader));
        Assert.False(PlayerRules.IsHandicapFoe(sim, invader));
    }

    [Fact]
    public void An_always_at_war_faction_is_at_war_with_everyone_and_cannot_make_peace()
    {
        var sim = Scenario();
        int barbarian = GameFactory.EnsureBarbarian(sim);
        int invader = sim.Rules().Peoples.EnsureFaction(sim, Invader(sim));
        int invaderTeam = sim.TeamOf(invader).Id;

        foreach (var team in sim.State.Teams.Where(t => t.Id != invaderTeam))
            Assert.Contains(team.Id, sim.TeamOf(invader).Wars);
        Assert.Contains(sim.TeamOf(barbarian).Id, sim.TeamOf(invader).Wars);
        Assert.NotNull(sim.Rules().Relations.ValidatePeace(sim, 0, invader));
        Assert.NotNull(sim.Rules().Relations.ValidatePeace(sim, 0, barbarian));
    }

    [Fact]
    public void Contact_follows_the_negotiates_restriction()
    {
        var sim = Scenario();
        int barbarian = GameFactory.EnsureBarbarian(sim);
        int invader = sim.Rules().Peoples.EnsureFaction(sim, Invader(sim));
        var relations = sim.Rules().Relations;

        relations.EstablishContact(sim, sim.TeamOf(0).Id, sim.TeamOf(barbarian).Id);
        relations.EstablishContact(sim, sim.TeamOf(0).Id, sim.TeamOf(invader).Id);

        Assert.False(relations.HasMet(sim, sim.TeamOf(0).Id, sim.TeamOf(barbarian).Id));
        Assert.True(relations.HasMet(sim, sim.TeamOf(0).Id, sim.TeamOf(invader).Id));
    }

    [Fact]
    public void Research_cost_comes_from_the_faction_row_and_its_game_options()
    {
        var sim = Scenario();
        int barbarian = GameFactory.EnsureBarbarian(sim);
        int invader = sim.Rules().Peoples.EnsureFaction(sim, Invader(sim));
        var research = sim.Rules().Research;
        string tech = sim.Content.Get<S.TechDef>().All.OrderBy(t => t.Cost).ThenBy(t => t.Id, StringComparer.Ordinal).First(t => t.Cost > 0).Id;
        long Percent(int player) => research.CalculateCost(sim, sim.TeamOf(player).Id, tech).Ledger.Lines
            .Where(l => l.Key == "research:barbarian_percent").Select(l => l.Amount).DefaultIfEmpty(100).Single();

        Assert.Equal(100, Percent(0));
        Assert.Equal(300, Percent(barbarian));
        Assert.Equal(150, Percent(invader));
        sim.Settings.GameOptions["foundation:gameoption_raging_barbarians"] = true;
        Assert.Equal(1200, Percent(barbarian));
        Assert.Equal(150, Percent(invader));
    }

    [Fact]
    public void Uprisings_join_the_faction_that_says_so_not_every_faction()
    {
        var sim = Scenario();
        Assert.Equal("barbarians:barbarian", PlayerRules.UprisingFaction(sim)!.Id);
    }

    [Fact]
    public void A_faction_survives_save_and_reload_without_any_saved_marker()
    {
        var sim = Scenario();
        int barbarian = GameFactory.EnsureBarbarian(sim);
        int invader = sim.Rules().Peoples.EnsureFaction(sim, Invader(sim));

        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));

        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.Equal(barbarian, GameFactory.BarbarianPlayer(loaded));
        Assert.Equal(invader, PlayerRules.FactionPlayer(loaded, Invader(loaded)));
        Assert.Equal(invader, loaded.Rules().Peoples.EnsureFaction(loaded, Invader(loaded)));
    }

    private sealed class InvasionProbe : IMod
    {
        public void Register(IModContext context)
        {
            context.Content<S.CivilizationDef>().Register(new()
            {
                Id = InvaderCivilization, Name = "Invader State", ShortDescription = "Invaders", Adjective = "Invader",
            });
            context.Content<S.FactionDef>().Register(new()
            {
                Id = Invaders, Name = "Invaders", Target = InvaderCivilization, Leader = "peoples:leader_barbarian",
                Negotiates = true, AlwaysAtWar = true, ResearchCostPercent = 150,
            });
        }
    }
}
