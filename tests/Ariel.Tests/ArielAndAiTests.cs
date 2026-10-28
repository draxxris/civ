using Ariel.BaseGame;
using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Xunit;

namespace Ariel.Tests;

public class ArielAndAiTests
{
    [Fact]
    [Trait("Category", "E2E")]
    public void BasicAiPlaysAWholeGameDeterministically()
    {
        ulong Run()
        {
            var sim = new Simulation(TestGame.Content(), new SimulationSettings
            {
                Seed = 11, Map = new(17, 17), PlayerCount = 2, PlayerAis = ["base:basic", "base:basic"],
            });
            for (int i = 0; i < 60; i++) sim.AdvanceTurn();
            Assert.Equal(2, sim.State.Cities.Count);
            Assert.All(sim.State.Teams, t => Assert.NotEmpty(t.Techs));
            Assert.All(sim.State.Cities, c => Assert.NotEmpty(c.Buildings));
            return sim.ComputeHash();
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void HumanAndAiShareATurn()
    {
        var sim = new Simulation(TestGame.Content(), new SimulationSettings { Seed = 5, Map = new(17, 17), PlayerCount = 2, PlayerAis = [null, "base:basic"] });
        Assert.True(sim.State.Players[1].EndedTurn);
        Assert.False(sim.State.Players[0].EndedTurn);
        sim.TryApply(new EndTurnCommand { Player = 0 }, out _);
        Assert.Equal(2, sim.State.Turn);
        Assert.True(sim.State.Players[1].EndedTurn); // AI already acted for the new turn
    }

    private sealed class SilentAi : IPlayerAI
    {
        public string Id => "mymod:silent";
        public IEnumerable<GameCommand> Decide(Simulation sim, int playerId) => [];
    }

    [Fact]
    public void ModsCanRegisterTheirOwnCivilizationAndAi()
    {
        var baseSrc = ModLoader.FromAssembly(new ModManifest { Id = "base" }, typeof(TestRulesetMod).Assembly, AppContext.BaseDirectory);
        var extra = new ModSource
        {
            Manifest = new ModManifest { Id = "mymod", Dependencies = ["base"] },
            Instance = new Extra(), Root = AppContext.BaseDirectory,
        };
        var db = ModLoader.Build([baseSrc, extra]);
        var sim = new Simulation(db, new SimulationSettings { Seed = 1, Map = new(13, 13), PlayerCount = 1, Civilizations = ["mymod:nomads"], PlayerAis = ["mymod:silent"] });
        Assert.Contains("base:the_wheel", sim.TeamOf(0).Techs);
        Assert.Empty(sim.State.Cities); // the silent AI does nothing
    }

    private sealed class Extra : IMod
    {
        public void Register(IModContext ctx)
        {
            ctx.Civilizations.Register(new CivilizationDef { Id = "mymod:nomads", Name = "Nomads", StartingTechs = ["base:the_wheel"] });
            ctx.PlayerAIs.Register(new SilentAi());
        }
    }
}
