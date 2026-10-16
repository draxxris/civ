using System.Collections.Immutable;
using Civ.Content;
using Civ.Game;
using Civ.Modding;
using Xunit;
using S = Civ.Content.Schema;

namespace Civ.Mods.Tests;

public sealed partial class WorldRulesTests
{
    private const string Evolution = "evolution_probe:";
    private static readonly Lazy<ContentDb> EvolutionContentDb = new(() =>
    {
        var mods = new LoadedMods();
        return ModLoader.Build(mods.Sources.Append(new ModSource
        {
            Manifest = new() { Id = "evolution_probe", Version = "1", Dependencies = ["world", "foundation", "research"] },
            Root = mods.Root, Instance = new EvolutionContent(),
        }));
    });

    [Fact]
    public void Feature_disappearance_publishes_change_and_does_not_regrow_in_the_same_turn()
    {
        var sim = EvolutionScenario();
        var tile = EvolutionPlot(sim, "fading_soil");
        sim.State.Plots[tile.Value].Feature = Evolution + "fading";
        MapTurn(sim);
        Assert.Null(sim.State.Plots[tile.Value].Feature);
        Assert.Equal(new FeatureChanged(tile, Evolution + "fading", null), Assert.Single(sim.Events.Published.OfType<FeatureChanged>()));
        MapTurn(sim); // CanGrowAnywhere supplies base growth without an adjacent feature.
        Assert.Equal(Evolution + "fading", sim.State.Plots[tile.Value].Feature);
        Assert.Equal(new FeatureChanged(tile, null, Evolution + "fading"), sim.Events.Published.OfType<FeatureChanged>().Last());
    }

    [Fact]
    public void Feature_growth_scales_preserve_contributions_routes_and_game_speed()
    {
        var sim = EvolutionScenario();
        var tile = EvolutionPlot(sim);
        var plot = sim.State.Plots[tile.Value];
        plot.Route = "world:route_route_road";
        var neighbor = sim.State.Grid.Neighbors(tile).First();
        sim.State.Plots[neighbor.Value].Feature = Evolution + "growth";
        sim.State.Plots[neighbor.Value].Improvement = Evolution + "preserve";
        int successes = 0;
        for (int i = 0; i < 48; i++)
        {
            plot.Feature = null;
            var expected = CopyRng(sim);
            // Preserve replaces the feature's 2000 growth: 8000 * 125% * 75% = 7500.
            bool grows = expected.Next(20000) < 7500; // Victory delay = 200%.
            int events = sim.Events.Published.Count;
            MapTurn(sim);
            Assert.Equal(grows ? Evolution + "growth" : null, plot.Feature);
            Assert.Equal(expected.State, sim.Rng.State);
            Assert.Equal(grows ? 1 : 0, sim.Events.Published.Skip(events).OfType<FeatureChanged>().Count());
            if (grows) successes++;
        }
        Assert.InRange(successes, 1, 47); // Exercise both sides of the threshold.
    }

    [Theory]
    [InlineData("unit", false)]
    [InlineData("bonus", false)]
    [InlineData("improvement", false)]
    [InlineData("city", false)]
    [InlineData("peak", false)]
    [InlineData("spread_over_unit_and_bonus", true)]
    [InlineData("spread_over_unused_improvement", true)]
    [InlineData("spread_over_worked_improvement", false)]
    public void Dynamic_feature_growth_respects_occupation_and_spread_overgrowth_rules(string condition, bool changes)
    {
        var sim = EvolutionScenario();
        bool spread = condition.StartsWith("spread", StringComparison.Ordinal);
        var tile = EvolutionPlot(sim, spread ? "spreading_soil" : "soil");
        var plot = sim.State.Plots[tile.Value];
        string feature = Evolution + (spread ? "spread" : "growth");
        sim.State.Plots[sim.State.Grid.Neighbors(tile).First().Value].Feature = feature;
        if (condition is "unit" or "spread_over_unit_and_bonus") AddWorker(sim, tile);
        if (condition is "bonus" or "spread_over_unit_and_bonus") plot.Bonus = Evolution + "ore";
        if (condition.Contains("improvement", StringComparison.Ordinal)) plot.Improvement = Evolution + "preserve";
        if (condition == "city") AddCity(sim, tile);
        if (condition == "peak") plot.Kind = PlotKind.Peak;
        if (condition == "spread_over_worked_improvement") WorkEvolutionPlot(sim, tile);
        MapTurn(sim);
        Assert.Equal(changes ? feature : null, plot.Feature);
        Assert.Equal(changes ? 1 : 0, sim.Events.Published.OfType<FeatureChanged>().Count());
    }

    [Theory]
    [InlineData(false, 10)]
    [InlineData(true, 1)]
    public void Resource_discovery_stays_active_without_depletion_and_is_ten_times_faster_with_it(bool depletion, int odds)
    {
        var sim = EvolutionScenario(depletion);
        var tile = EvolutionPlot(sim);
        var plot = sim.State.Plots[tile.Value];
        plot.Improvement = Evolution + "prospect";
        WorkEvolutionPlot(sim, tile);
        int discoveries = 0;
        for (int i = 0; i < 128; i++)
        {
            plot.Bonus = null;
            plot.DepletedMine = true;
            var expected = CopyRng(sim);
            bool discovers = expected.Next(odds) == 0;
            MapTurn(sim);
            Assert.Equal(discovers ? Evolution + "ore" : null, plot.Bonus);
            Assert.Equal(!discovers, plot.DepletedMine);
            Assert.Equal(expected.State, sim.Rng.State);
            if (discovers) discoveries++;
        }
        Assert.True(discoveries > 0);
        Assert.Equal(discoveries, sim.Events.Published.OfType<BonusDiscovered>().Count());
        Assert.All(sim.Events.Published.OfType<BonusDiscovered>(), e => Assert.Equal(new BonusDiscovered(tile, Evolution + "ore", 0), e));
        Assert.Empty(sim.Events.Published.OfType<MineDepleted>());
        Assert.Empty(sim.Events.Published.OfType<BonusDepleted>());
    }

    [Theory]
    [InlineData("unworked")]
    [InlineData("unowned")]
    [InlineData("hidden")]
    [InlineData("invalid_terrain")]
    [InlineData("existing_bonus")]
    public void Resource_discovery_requires_work_ownership_reveal_valid_ground_and_an_empty_deposit(string condition)
    {
        var sim = EvolutionScenario(true);
        var tile = EvolutionPlot(sim);
        var plot = sim.State.Plots[tile.Value];
        plot.Improvement = Evolution + "prospect";
        if (condition != "unworked") WorkEvolutionPlot(sim, tile);
        if (condition == "unowned") plot.Owner = null;
        if (condition == "hidden") sim.TeamOf(0).Techs.Clear();
        if (condition == "invalid_terrain") plot.Terrain = "world:grass";
        if (condition == "existing_bonus") plot.Bonus = Evolution + "ore";
        var rng = sim.Rng.State;
        MapTurn(sim);
        Assert.Equal(condition == "existing_bonus" ? Evolution + "ore" : null, plot.Bonus);
        Assert.Equal(rng, sim.Rng.State);
        Assert.Empty(sim.Events.Published.OfType<BonusDiscovered>());
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void Mine_exhaustion_requires_the_option_and_work_and_persists_across_reload(bool enabled, bool worked, bool exhausts)
    {
        var sim = EvolutionScenario(enabled);
        var tile = EvolutionPlot(sim);
        var plot = sim.State.Plots[tile.Value];
        plot.Improvement = Evolution + "mine";
        plot.ImprovementTurns = 9;
        if (worked) WorkEvolutionPlot(sim, tile);
        var rng = sim.Rng.State;
        MapTurn(sim);
        Assert.Equal(exhausts, plot.DepletedMine);
        Assert.Equal(exhausts ? "world:depleted_mine" : Evolution + "mine", plot.Improvement);
        Assert.Equal(exhausts ? 0 : 9, plot.ImprovementTurns);
        if (!exhausts) Assert.Equal(rng, sim.Rng.State);
        Assert.Equal(exhausts ? 1 : 0, sim.Events.Published.OfType<MineDepleted>().Count());
        if (exhausts) Assert.Equal(new MineDepleted(tile, Evolution + "mine", "world:depleted_mine", 0), Assert.Single(sim.Events.Published.OfType<MineDepleted>()));
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.Equal(exhausts, loaded.State.Plots[tile.Value].DepletedMine);
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        MapTurn(sim); MapTurn(loaded);
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    public void Revealed_valid_deposits_protect_mines_even_after_bonus_obsolescence(bool revealed, bool obsolete, bool exhausts)
    {
        var sim = EvolutionScenario(true);
        var tile = EvolutionPlot(sim);
        var plot = sim.State.Plots[tile.Value];
        plot.Improvement = Evolution + "mine";
        plot.Bonus = Evolution + "ore";
        if (!revealed) sim.TeamOf(0).Techs.Clear();
        if (obsolete) sim.TeamOf(0).Techs.Add("research:iron_working");
        WorkEvolutionPlot(sim, tile);
        MapTurn(sim);
        Assert.Equal(exhausts, plot.DepletedMine);
        Assert.Equal(Evolution + "ore", plot.Bonus);
    }

    [Theory]
    [InlineData("foundation:worldsize_duel", 1)]
    [InlineData("foundation:worldsize_standard", 4)]
    [InlineData("foundation:worldsize_gigantic", 8)]
    public void Deposit_exhaustion_scales_by_world_size_and_counts_all_map_copies_without_requiring_work(string world, int odds)
    {
        var sim = EvolutionScenario(true, world);
        var tile = EvolutionPlot(sim);
        var plot = sim.State.Plots[tile.Value];
        foreach (var p in sim.State.Plots) p.Bonus = Evolution + "ore";
        plot.Improvement = Evolution + "deposit";
        int depletions = 0;
        for (int i = 0; i < 48; i++)
        {
            plot.Bonus = Evolution + "ore";
            var expected = CopyRng(sim);
            bool depletes = expected.Next(odds) == 0; // 8 * 100% * 12 * size / 96 map copies.
            MapTurn(sim);
            Assert.Equal(depletes ? null : Evolution + "ore", plot.Bonus);
            Assert.Equal(expected.State, sim.Rng.State);
            if (depletes) depletions++;
        }
        Assert.True(depletions > 0);
        Assert.Equal(depletions, sim.Events.Published.OfType<BonusDepleted>().Count());
        Assert.All(sim.Events.Published.OfType<BonusDepleted>(), e => Assert.Equal(new BonusDepleted(tile, Evolution + "ore", 0), e));
    }

    [Fact]
    public void Disabled_depletion_keeps_mines_and_deposits_without_consuming_random_numbers()
    {
        var sim = EvolutionScenario(); // No override: default is off.
        var mine = EvolutionPlot(sim);
        var deposit = sim.State.Grid.Neighbors(mine).First();
        sim.State.Plots[mine.Value].Improvement = Evolution + "mine";
        WorkEvolutionPlot(sim, mine);
        var plot = sim.State.Plots[deposit.Value];
        plot.Owner = 0;
        plot.Bonus = Evolution + "ore";
        plot.Improvement = Evolution + "deposit";
        var rng = sim.Rng.State;
        MapTurn(sim);
        Assert.False(sim.IsOptionEnabled("foundation:gameoption_resource_depletion"));
        Assert.Equal(Evolution + "mine", sim.State.Plots[mine.Value].Improvement);
        Assert.Equal(Evolution + "ore", plot.Bonus);
        Assert.Equal(rng, sim.Rng.State);
        Assert.Empty(sim.Events.Published.OfType<MineDepleted>());
        Assert.Empty(sim.Events.Published.OfType<BonusDepleted>());
    }

    [Theory]
    [InlineData("mine")]
    [InlineData("deposit")]
    [InlineData("prospect")]
    public void Very_large_resource_odds_do_not_overflow_or_accidentally_force_exhaustion(string kind)
    {
        var sim = EvolutionScenario(true, speed: "extreme_speed");
        var tile = EvolutionPlot(sim);
        var plot = sim.State.Plots[tile.Value];
        plot.Improvement = Evolution + "extreme_" + kind;
        if (kind == "deposit") plot.Bonus = Evolution + "ore";
        WorkEvolutionPlot(sim, tile);
        var expected = CopyRng(sim);
        Assert.NotEqual(0, expected.Next(int.MaxValue));
        MapTurn(sim);
        Assert.Equal(expected.State, sim.Rng.State);
        Assert.Equal(Evolution + "extreme_" + kind, plot.Improvement);
        Assert.Equal(kind == "deposit" ? Evolution + "ore" : null, plot.Bonus);
    }

    [Fact]
    public void Depletion_odds_that_truncate_to_zero_do_not_exhaust_or_consume_rng()
    {
        var sim = EvolutionScenario(true, speed: "tiny_construct");
        var mine = EvolutionPlot(sim);
        sim.State.Plots[mine.Value].Improvement = Evolution + "mine";
        WorkEvolutionPlot(sim, mine);
        var deposit = sim.State.Plots[sim.State.Grid.Neighbors(mine).First().Value];
        deposit.Owner = 0; deposit.Bonus = Evolution + "ore"; deposit.Improvement = Evolution + "deposit";
        var rng = sim.Rng.State;
        MapTurn(sim);
        Assert.Equal(rng, sim.Rng.State);
        Assert.False(sim.State.Plots[mine.Value].DepletedMine);
        Assert.Equal(Evolution + "ore", deposit.Bonus);
    }

    [Fact]
    public void Mine_builds_stay_blocked_until_the_owning_team_reveals_the_resource()
    {
        var sim = GameFactory.Scenario(Content);
        var tile = sim.State.Grid.TileAt(3, 3);
        var plot = sim.State.Plots[tile.Value];
        plot.Kind = PlotKind.Hills;
        plot.Improvement = "world:depleted_mine";
        plot.DepletedMine = true;
        plot.Bonus = "world:copper";
        var foreign = sim.State.Plots[sim.State.Grid.TileAt(6, 3).Value];
        foreign.Owner = 1; foreign.Bonus = "world:copper"; foreign.DepletedMine = true;
        var noBonus = sim.State.Plots[sim.State.Grid.TileAt(7, 3).Value];
        noBonus.DepletedMine = true;
        sim.TeamOf(0).Techs.Add("research:mining");
        var worker = AddWorker(sim, tile);
        var builds = Service<IWorkerBuildService>(sim);
        const string mine = "world:build_build_mine";
        Assert.Equal("Mine is depleted on this plot", builds.Validate(sim, 0, worker.Id, mine));
        Assert.Equal("Mine is depleted on this plot", builds.Preview(sim, 0, worker.Id, tile, mine).Error);
        sim.Events.Publish(new TechDiscovered(0, "research:agriculture"));
        Assert.True(plot.DepletedMine);
        sim.TeamOf(0).Techs.Add("research:bronze_working");
        sim.Events.Publish(new TechDiscovered(0, "research:bronze_working"));
        Assert.False(plot.DepletedMine);
        Assert.True(foreign.DepletedMine);
        Assert.True(noBonus.DepletedMine);
        Assert.Null(builds.Validate(sim, 0, worker.Id, mine));
        Assert.True(sim.TryApply(new StartBuildCommand { Player = 0, Unit = worker.Id, Build = mine }, out var error), error);
    }

    private Simulation EvolutionScenario(bool? depletion = null, string world = "foundation:worldsize_duel", string speed = "speed")
    {
        var options = new SortedDictionary<string, bool>(StringComparer.Ordinal);
        if (depletion is { } enabled) options["foundation:gameoption_resource_depletion"] = enabled;
        var sim = new Simulation(EvolutionContentDb.Value, new SimulationSettings
        {
            WorldSize = world, GameSpeed = Evolution + speed, GameOptions = options,
            Leaders = ["peoples:leader_gjergj_kastrioti", "peoples:leader_julius_caesar"],
        });
        sim.State.Map = new(12, 8, true);
        sim.State.Plots = Enumerable.Range(0, 96).Select(_ => new Plot { Terrain = "world:peak", Kind = PlotKind.Peak }).ToArray();
        sim.State.Units.Clear();
        foreach (var player in sim.State.Players) player.StartingPlot = null;
        foreach (var team in sim.State.Teams) team.Techs.Clear();
        sim.TeamOf(0).Techs.Add("research:bronze_working");
        sim.State.RebuildIndexes();
        return sim;
    }

    private static TileId EvolutionPlot(Simulation sim, string ground = "soil")
    {
        var tile = sim.State.Grid.TileAt(3, 3);
        sim.State.Plots[tile.Value] = new Plot { Terrain = Evolution + ground, Owner = 0 };
        return tile;
    }

    private static void WorkEvolutionPlot(Simulation sim, TileId tile)
    {
        var city = AddCity(sim, sim.State.Grid.TileAt(5, 3));
        city.Worked.Add(sim.State.Grid.HexOf(tile));
    }

    private static DeterministicRng CopyRng(Simulation sim)
    {
        var (a, b, c, d) = sim.Rng.State;
        return DeterministicRng.Restore(a, b, c, d);
    }

    private static void MapTurn(Simulation sim) => sim.Content.TurnSystems.Get("world:improvements").Run(sim);

    private sealed class EvolutionContent : IMod
    {
        public void Register(IModContext context)
        {
            foreach (string ground in new[] { "soil", "spreading_soil", "fading_soil" })
                context.Content<S.TerrainDef>().Register(new() { Id = Evolution + ground });
            context.Content<S.GameSpeedDef>().Register(Speed("speed", 100));
            context.Content<S.GameSpeedDef>().Register(Speed("extreme_speed", int.MaxValue));
            context.Content<S.GameSpeedDef>().Register(Speed("tiny_construct", 1));
            context.Content<S.BonusDef>().Register(new()
            {
                Id = Evolution + "ore", RevealTech = "research:bronze_working", ObsoleteTech = "research:iron_working",
                Terrains = Valid("soil"), Flatlands = true, Hills = true, MaxLatitude = 90,
            });
            context.Content<S.FeatureDef>().Register(new()
            {
                Id = Evolution + "growth", Terrains = Valid("soil"), Growth = 2000,
            });
            context.Content<S.FeatureDef>().Register(new()
            {
                Id = Evolution + "spread", Terrains = Valid("spreading_soil"), Spread = 20000, CanGrowAnywhere = true,
            });
            context.Content<S.FeatureDef>().Register(new()
            {
                Id = Evolution + "fading", Terrains = Valid("fading_soil"), Disappearance = 20000,
                Growth = 20000, CanGrowAnywhere = true,
            });
            context.Content<S.ImprovementDef>().Register(new() { Id = Evolution + "preserve", FeatureGrowth = 8000 });
            foreach (bool extreme in new[] { false, true })
            {
                string prefix = Evolution + (extreme ? "extreme_" : "");
                context.Content<S.ImprovementDef>().Register(new()
                {
                    Id = prefix + "prospect", BonusEffects = Effects(new() { DiscoveryRandomRange = extreme ? int.MaxValue : 10 }),
                });
                context.Content<S.ImprovementDef>().Register(new()
                {
                    Id = prefix + "mine", DepletionRand = extreme ? int.MaxValue : 1, BonusEffects = Effects(new() { MakesValid = true }),
                });
                context.Content<S.ImprovementDef>().Register(new()
                {
                    Id = prefix + "deposit", BonusEffects = Effects(new() { MakesValid = true, DepletionRandomRange = extreme ? int.MaxValue : 8 }),
                });
            }
        }

        private static ImmutableDictionary<string, bool> Valid(string ground) => ImmutableDictionary<string, bool>.Empty.Add(Evolution + ground, true);
        private static ImmutableDictionary<string, S.ImprovementBonus> Effects(S.ImprovementBonus effect) =>
            ImmutableDictionary<string, S.ImprovementBonus>.Empty.Add(Evolution + "ore", effect);
        private static S.GameSpeedDef Speed(string id, int construct) => new()
        {
            Id = Evolution + id, ConstructPercent = construct, VictoryDelayPercent = 200,
            BuildPercent = 100, ImprovementPercent = 100, GrowthPercent = 100, ResearchPercent = 100,
        };
    }
}
