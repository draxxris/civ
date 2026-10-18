using Civ.BaseGame;
using Civ.Game;
using Xunit;

namespace Civ.Tests;

public class SimulationTests
{
    [Fact]
    public void SameSeedSameMapAndHash()
    {
        Assert.Equal(TestGame.Sim(5).ComputeHash(), TestGame.Sim(5).ComputeHash());
        Assert.NotEqual(TestGame.Sim(5).ComputeHash(), TestGame.Sim(6).ComputeHash());
    }

    [Fact]
    public void CannotFoundCityOnWaterOrNearAnotherCity()
    {
        var sim = TestGame.Sim();
        var water = sim.State.Grid.HexOf(sim.State.Grid.Tiles.First(t => sim.State.Plots[t.Value].Terrain == "base:ocean"));
        Assert.False(sim.TryApply(new FoundCityCommand { Player = 0, Position = water }, out var err));
        Assert.NotNull(err);

        var land = TestGame.FindLand(sim);
        Assert.True(sim.TryApply(new FoundCityCommand { Player = 0, Position = land }, out _));
        Assert.False(sim.TryApply(new FoundCityCommand { Player = 1, Position = land }, out _));
    }

    [Fact]
    public void ResearchBuildAndCultureProgressAcrossTurns()
    {
        var sim = TestGame.Sim();
        Assert.True(sim.TryApply(new FoundCityCommand { Player = 0, Position = TestGame.FindLand(sim) }, out _));
        Assert.True(sim.TryApply(new SetResearchCommand { Player = 0, Tech = "base:agriculture" }, out _));
        Assert.False(sim.TryApply(new SetResearchCommand { Player = 0, Tech = "base:monarchy" }, out _)); // prereqs

        for (int i = 0; i < 30; i++)
        {
            sim.TryApply(new EndTurnCommand { Player = 0 }, out _);
            sim.TryApply(new EndTurnCommand { Player = 1 }, out _);
        }
        Assert.Equal(31, sim.State.Turn);
        Assert.Contains("base:agriculture", sim.TeamOf(0).Techs);

        Assert.True(sim.TryApply(new StartBuildingCommand { Player = 0, CityId = 1, Building = "base:granary" }, out _));
        for (int i = 0; i < 30; i++)
        {
            sim.TryApply(new EndTurnCommand { Player = 0 }, out _);
            sim.TryApply(new EndTurnCommand { Player = 1 }, out _);
        }
        Assert.Contains("base:granary", sim.State.Cities[0].Buildings);
        Assert.True(sim.State.PlayerById(0).Culture >= 60);
    }

    [Fact]
    public void CommandsRoundTripThroughCodec()
    {
        var sim = TestGame.Sim();
        var codec = new CommandCodec(sim.Content);
        var text = codec.Encode(new FoundCityCommand { Player = 1, Position = new Hex(2, -1) });
        var back = Assert.IsType<FoundCityCommand>(codec.Decode(text));
        Assert.Equal(1, back.Player);
        Assert.Equal(new Hex(2, -1), back.Position);
        Assert.IsType<EndTurnCommand>(codec.Decode(codec.Encode(new EndTurnCommand { Player = 0 })));
    }
}
