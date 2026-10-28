using Ariel.BaseGame;
using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Xunit;

namespace Ariel.Tests;

public sealed class ScenarioTests
{
    [Fact]
    public void Authored_state_is_indexed_before_turn_start_and_ai_and_load_does_not_repeat_startup()
    {
        var trace = new List<string>();
        var probe = new StartupProbe(trace);
        var content = ModLoader.Build([
            ModLoader.FromAssembly(typeof(TestRulesetMod).Assembly, AppContext.BaseDirectory),
            new ModSource { Manifest = new() { Id = "scenario", Version = "1", Dependencies = ["base"] },
                Root = AppContext.BaseDirectory, Instance = probe },
        ]);
        var sim = Simulation.CreateScenario(content, new()
        {
            Seed = 71, MapGenerator = "scenario:probe", PlayerCount = 2,
            Civilizations = ["base:builders", "base:scholars"], PlayerTeams = [7, 7],
            PlayerAis = ["scenario:probe"],
        }, new(4, 3, true), Plots(12), game =>
        {
            trace.Add("configure");
            Assert.Empty(game.Events.Published);
            Assert.Equal(new DeterministicRng(71).State, game.Rng.State);
            Assert.Equal(new[] { "base:agriculture", "base:mining" }, game.TeamOf(0).Techs);
            Assert.Equal(new[] { 0, 1 }, game.TeamOf(1).Members);
            game.State.Cities.Add(new() { Id = 42, Owner = 0, Position = new(4, 0), Name = "Port" });
            game.State.Units.Add(new() { Id = 9, Owner = 0, Position = new(4, 0), Type = "base:warrior" });
        });
        Assert.Equal(new[] { "configure", "turn:Port", "ai:Port:37" }, trace);
        Assert.Equal(new Hex(0, 0), sim.State.UnitById(9).Position);
        Assert.Equal(42, sim.State.CityAt(new(0, 0))!.Id);
        Assert.Equal(9, Assert.Single(sim.State.UnitsAt(new(0, 0))).Id);
        Assert.Equal(43, sim.State.NextCityId);
        Assert.Equal(10, sim.State.NextUnitId);
        Assert.Equal("base:writing", sim.TeamOf(0).Researching);
        Assert.True(sim.State.PlayerById(0).EndedTurn);
        Assert.False(sim.State.PlayerById(1).EndedTurn);
        Assert.Equal(1, sim.State.Turn);
        Assert.IsType<TurnStarted>(Assert.Single(sim.Events.Published));
        var loaded = SaveGame.Load(content, SaveGame.Save(sim));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.Equal(3, trace.Count);
        Assert.Empty(loaded.Events.Published);
        Assert.Equal(sim.Rng.NextUInt64(), loaded.Rng.NextUInt64());
    }

    [Theory]
    [InlineData(0, 3, 0)]
    [InlineData(4, 3, 11)]
    public void Invalid_map_shape_is_rejected_before_configuration(int width, int height, int count)
    {
        bool configured = false;
        Assert.ThrowsAny<ArgumentException>(() => Simulation.CreateScenario(TestGame.Content(), new(),
            new(width, height), Plots(count), _ => configured = true));
        Assert.False(configured);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("position")]
    [InlineData("type")]
    [InlineData("duplicate")]
    [InlineData("id")]
    public void Invalid_authored_units_are_rejected_before_startup(string invalid)
    {
        var content = TestGame.Content();
        var error = Record.Exception(() => Simulation.CreateScenario(content, new(), new(4, 3), Plots(12), sim =>
        {
            sim.State.Units.Add(new()
            {
                Id = invalid == "id" ? 0 : 3, Owner = invalid == "owner" ? 99 : 0,
                Position = invalid == "position" ? new(0, 99) : new(0, 0),
                Type = invalid == "type" ? "base:missing" : "base:warrior",
            });
            if (invalid == "duplicate") sim.State.Units.Add(new() { Id = 3, Type = "base:warrior" });
        }));
        var expected = invalid switch
        {
            "owner" or "type" => typeof(KeyNotFoundException),
            "position" => typeof(ArgumentOutOfRangeException),
            "duplicate" => typeof(ArgumentException),
            _ => typeof(InvalidDataException),
        };
        Assert.IsType(expected, error);
    }

    private static Plot[] Plots(int count) => Enumerable.Range(0, count)
        .Select(_ => new Plot { Terrain = "base:grassland" }).ToArray();

    private sealed class StartupProbe(List<string> trace) : IMod, IPlayerAI, IMapGenerator
    {
        public string Id => "scenario:probe";
        public void Register(IModContext context)
        {
            context.MapGenerators.Register(this);
            context.PlayerAIs.Register(this);
            context.Subscribe<TurnStarted>("start", (sim, _) =>
            {
                trace.Add($"turn:{sim.State.CityById(42).Name}");
                sim.State.PlayerById(0).Treasury = 37;
            });
        }
        public IEnumerable<GameCommand> Decide(Simulation sim, int playerId)
        {
            trace.Add($"ai:{sim.State.CityById(42).Name}:{sim.State.PlayerById(playerId).Treasury}");
            return [new SetResearchCommand { Player = playerId, Tech = "base:writing" }];
        }
        public void Generate(MapSpec map, Plot[] plots, DeterministicRng rng, ContentDb content) =>
            throw new InvalidOperationException("Scenario creation must not generate a world");
    }
}
