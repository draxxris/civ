using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Xunit;

namespace Ariel.Mods.Tests;

public sealed class WorldExtensionTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    [Fact]
    public void Player_phases_are_nested_and_plot_modifiers_have_explainable_rounding()
    {
        var trace = new List<string>();
        var source = new ModSource { Manifest = new() { Id = "probe", Version = "1.0.0" }, Root = mods.Root, Instance = new ProbeMod(trace) };
        var content = ModLoader.Build(mods.Sources.Append(source));
        var sim = GameFactory.Scenario(content);
        var tile = sim.State.Grid.TileAt(3, 3);
        sim.State.SetRiver(tile, 0, true);
        var hash = sim.ComputeHash();
        var result = content.Services.Get<IPlotYieldService>().Single().Calculate(sim, 0, tile);
        Assert.Equal(new YieldVector(4, 0, 1), result.Yields); // (2 + 1) * 150 / 100 = 4.
        Assert.Equal(1, result.Ledger.Total("yield.flat.commerce"));
        Assert.Equal(3, result.Ledger.Total("yield.flat.food"));
        Assert.Equal(50, result.Ledger.Total("yield.percent.food"));
        Assert.Equal(hash, sim.ComputeHash()); // Queries neither mutate state nor draw random numbers.
        sim.AdvanceTurn();
        Assert.Equal(new[] { "begin", "gold:0", "research:0", "growth:0", "culture:0", "production:0",
            "gold:1", "research:1", "growth:1", "culture:1", "production:1", "end" }, trace);
    }

    [Fact]
    public void Shared_teams_pool_starting_techs_and_see_all_members_units()
    {
        var sim = new Simulation(mods.Content, new SimulationSettings
        {
            Seed = 42, WorldSize = "foundation:worldsize_standard", MapGenerator = "world:continents",
            Leaders = ["peoples:leader_gjergj_kastrioti", "peoples:leader_julius_caesar"],
            PlayerCount = 2, PlayerTeams = [0, 0],
        });
        Assert.Single(sim.State.Teams);
        Assert.Equal(new[] { "research:agriculture", "research:fishing", "research:ritualism", "research:stone_tools" }, sim.TeamOf(1).Techs);
        foreach (var player in sim.State.Players) Assert.True(sim.State.Plots[player.StartingPlot!.Value.Value].Visible.Contains(0));
        Assert.All(sim.State.Plots, p => Assert.False(p.Visible.Contains(1)));
    }

    private sealed class ProbeMod(List<string> trace) : IMod
    {
        public void Register(IModContext context)
        {
            context.RegisterService<IModifierSource>("plot_effects", new PlotEffects());
            context.TurnSystems.Register(new Boundary("probe:begin", TurnPhase.GameBegin, "begin", trace));
            context.TurnSystems.Register(new Boundary("probe:end", TurnPhase.GameEnd, "end", trace));
            context.TurnSystems.Register(new PlayerStep("probe:production", TurnPhase.CityProduction, "production", trace));
            context.TurnSystems.Register(new PlayerStep("probe:culture", TurnPhase.CityCulture, "culture", trace));
            context.TurnSystems.Register(new PlayerStep("probe:growth", TurnPhase.CityGrowth, "growth", trace));
            context.TurnSystems.Register(new PlayerStep("probe:research", TurnPhase.Research, "research", trace));
            context.TurnSystems.Register(new PlayerStep("probe:gold", TurnPhase.Gold, "gold", trace));
        }
    }
    private sealed class PlotEffects : IModifierSource
    {
        public void Contribute(ModifierContext context, ModifierSink sink)
        {
            if (context.Scope != ModifierScope.Plot) return;
            sink.AddYield("probe:flat", new(1, 0, 0));
            sink.AddYield("probe:percent", new(50, 0, 0), percent: true);
        }
    }
    private sealed record Boundary(string Id, int Order, string Label, List<string> Trace) : ITurnSystem
    {
        public void Run(Simulation sim) => Trace.Add(Label);
    }
    private sealed record PlayerStep(string Id, int Order, string Label, List<string> Trace) : IPlayerTurnSystem
    {
        public void Run(Simulation sim, int player) => Trace.Add($"{Label}:{player}");
    }
}
