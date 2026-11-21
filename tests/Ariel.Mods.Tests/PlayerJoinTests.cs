using Ariel.Game;
using Xunit;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Tests;

/// <summary>A player can join a running game through the same path that seats players at setup.</summary>
public sealed class PlayerJoinTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private static (S.CivilizationDef Civilization, string Leader) Unused(Simulation sim)
    {
        var taken = sim.State.Players.Select(p => p.Civilization).ToHashSet(StringComparer.Ordinal);
        var civilization = sim.Content.Get<S.CivilizationDef>().All.Where(c => c.Playable && c.AiPlayable && !taken.Contains(c.Id))
            .OrderBy(c => c.Id, StringComparer.Ordinal).First();
        return (civilization, civilization.Leaders.Where(l => l.Value).Select(l => l.Key).Order(StringComparer.Ordinal).First());
    }

    [Fact]
    public void A_nation_joins_on_its_own_team_with_starting_civics_and_technologies()
    {
        var sim = GameFactory.CreateAi(mods.Content, players: 3);
        var (civilization, leader) = Unused(sim);
        int players = sim.State.Players.Count, teams = sim.State.Teams.Count;

        int id = sim.Rules().Peoples.AddPlayer(sim, new(civilization.Id, leader, Ai: "ai:standard",
            Techs: ["research:mysticism"], Treasury: 25));

        var player = sim.State.PlayerById(id);
        Assert.Equal(players, id);
        Assert.Equal(players + 1, sim.State.Players.Count);
        Assert.Equal(teams + 1, sim.State.Teams.Count);
        Assert.Equal(civilization.ShortDescription, player.Name);
        Assert.Equal("ai:standard", player.Ai);
        Assert.Equal(25, player.Treasury);
        Assert.False(player.EndedTurn);
        Assert.True(PlayerRules.IsNation(sim, id));
        // The same civics a player seated at setup holds.
        Assert.Equal(sim.State.PlayerById(0).Civics.Keys, player.Civics.Keys);
        Assert.Contains("research:mysticism", sim.TeamOf(id).Techs);
        foreach (var tech in civilization.StartingTechs.Where(t => t.Value)) Assert.Contains(tech.Key, sim.TeamOf(id).Techs);
        Assert.Empty(sim.TeamOf(id).Wars);
        Assert.Contains(sim.Events.Published.OfType<PlayerJoined>(), e => e.Player == id && e.Team == player.TeamId);
    }

    [Fact]
    public void An_always_at_war_faction_is_at_war_with_a_nation_that_joins_later()
    {
        var sim = GameFactory.CreateAi(mods.Content, players: 3);
        int barbarian = GameFactory.EnsureBarbarian(sim);
        var (civilization, leader) = Unused(sim);

        int id = sim.Rules().Peoples.AddPlayer(sim, new(civilization.Id, leader));

        Assert.Equal([sim.TeamOf(barbarian).Id], sim.TeamOf(id).Wars);
        Assert.Contains(sim.TeamOf(id).Id, sim.TeamOf(barbarian).Wars);
    }

    [Fact]
    public void A_joined_player_may_share_an_existing_team()
    {
        var sim = GameFactory.CreateAi(mods.Content, players: 3);
        var (civilization, leader) = Unused(sim);
        int teams = sim.State.Teams.Count;

        int id = sim.Rules().Peoples.AddPlayer(sim, new(civilization.Id, leader, Team: sim.TeamOf(1).Id));

        Assert.Equal(teams, sim.State.Teams.Count);
        Assert.Equal(sim.TeamOf(1).Id, sim.TeamOf(id).Id);
        Assert.Contains(id, sim.TeamOf(1).Members);
    }

    [Fact]
    public void A_joined_computer_player_takes_turns_and_survives_save_and_reload()
    {
        var sim = GameFactory.CreateAi(mods.Content, players: 3);
        var (civilization, leader) = Unused(sim);
        int id = sim.Rules().Peoples.AddPlayer(sim, new(civilization.Id, leader, Ai: "ai:standard",
            Techs: sim.TeamOf(0).Techs.ToArray()));
        var start = sim.State.Grid.HexOf(sim.State.PlayerById(0).StartingPlot!.Value);
        string settler = sim.Content.Get<S.UnitDef>().All.Where(u => u.CanFoundCity && u.DefaultUnit == u.Id)
            .OrderBy(u => u.Cost).ThenBy(u => u.Id, StringComparer.Ordinal).First().Id;
        var spot = sim.State.Grid.HexOf(sim.State.Grid.Tiles.Where(t => sim.State.Plots[t.Value].Owner is null
                && !sim.Content.Get<S.TerrainDef>(sim.State.Plots[t.Value].Terrain).IsWater && sim.State.Plots[t.Value].Kind != PlotKind.Peak
                && !sim.State.UnitsAt(sim.State.Grid.HexOf(t)).Any())
            .OrderBy(t => sim.State.Grid.Distance(sim.State.Grid.HexOf(t), start) < 6 ? int.MaxValue : sim.State.Grid.Distance(sim.State.Grid.HexOf(t), start))
            .ThenBy(t => t.Value).First());
        sim.Rules().Units.Create(sim, id, settler, spot);

        var copy = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.Equal(sim.ComputeHash(), copy.ComputeHash());
        for (int turn = 0; turn < 8; turn++)
        {
            sim.AdvanceTurn(); copy.AdvanceTurn();
            Assert.Equal(sim.ComputeHash(), copy.ComputeHash());
        }

        // The newcomer was given a settler and a computer player: it has founded a city of its own.
        Assert.Contains(sim.State.Cities, c => c.Owner == id);
    }
}
