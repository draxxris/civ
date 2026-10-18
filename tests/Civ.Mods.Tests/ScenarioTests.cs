using Civ.Game;
using Xunit;

namespace Civ.Mods.Tests;

public sealed class ScenarioTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private static SimulationSettings Settings => new()
    {
        Seed = 19, WorldSize = "foundation:worldsize_standard", PlayerCount = 1,
        Leaders = ["peoples:leader_julius_caesar"],
    };
    private static Plot[] Plots() => Enumerable.Range(0, 96).Select(_ => new Plot { Terrain = "world:grass" }).ToArray();

    [Fact]
    public void Ruleset_defaults_apply_without_generated_units_or_start_normalization()
    {
        var sim = Simulation.CreateScenario(mods.Content, Settings, new(12, 8, true), Plots());
        var player = Assert.Single(sim.State.Players);
        Assert.Equal("peoples:rome", player.Civilization);
        Assert.Equal("peoples:leader_julius_caesar", player.Leader);
        Assert.Equal("government:barter", player.Civics["government:civicoption_economy"]);
        Assert.Equal(8, player.Civics.Count);
        Assert.Equal(0, player.Treasury);
        Assert.Equal(new[] { "research:fishing", "research:stone_tools" }, sim.TeamOf(0).Techs);
        Assert.Null(player.StartingPlot);
        Assert.Empty(sim.State.Units);
        Assert.Equal(new(12, 8, true), sim.Settings.Map);
        Assert.Equal(sim.Settings.Map, sim.State.Map);
        Assert.Equal(new DeterministicRng(19).State, sim.Rng.State);
        Assert.All(sim.State.Plots, plot =>
        {
            Assert.Equal("world:grass", plot.Terrain);
            Assert.Null(plot.Bonus);
            Assert.False(plot.Revealed.Contains(0));
            Assert.Empty(plot.RevealedSnapshots);
        });
    }

    [Fact]
    public void Authored_entities_initialize_sight_and_memory_without_inheriting_old_exploration()
    {
        var plots = Plots();
        plots[95].Visible.Add(0); plots[95].Revealed.Add(0);
        plots[95].RevealedSnapshots[0] = new("world:coast", PlotKind.Flat, false, null, null, null, null, null);
        var sim = Simulation.CreateScenario(mods.Content, Settings, new(12, 8, true), plots, game =>
        {
            game.TeamOf(0).Techs.Clear();
            game.State.Cities.Add(new() { Id = 21, Owner = 0, Name = "Harbor", Position = new(0, 0) });
            game.State.Units.Add(new() { Id = 31, Owner = 0, Type = "military:warrior", Position = new(0, 0) });
        });
        Assert.Empty(sim.TeamOf(0).Techs);
        Assert.True(plots[0].Visible.Contains(0));
        Assert.Equal("Harbor", plots[0].RevealedSnapshots[0].City!.Name);
        Assert.False(plots[95].Visible.Contains(0));
        Assert.False(plots[95].Revealed.Contains(0));
        Assert.Empty(plots[95].RevealedSnapshots);
        Assert.NotNull(sim.State.UnitById(31).Extensions.Get<MilitaryUnitState>(mods.Content));
        var loaded = SaveGame.Load(mods.Content, SaveGame.Save(sim));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        sim.AdvanceTurn(); loaded.AdvanceTurn();
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }
}
