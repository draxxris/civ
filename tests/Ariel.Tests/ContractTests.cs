using System.Text.Json;
using Ariel.BaseGame;
using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Xunit;

namespace Ariel.Tests;

public class ContractTests
{
    private sealed class TestMod(Action<IModContext> register) : IMod
    {
        public void Register(IModContext ctx) => register(ctx);
    }
    private static ModSource Source(string id, Action<IModContext> register, string version = "1") => new()
    {
        Manifest = new ModManifest { Id = id, Version = version, Dependencies = ["base"] },
        Instance = new TestMod(register), Root = AppContext.BaseDirectory,
    };
    private static ContentDb Content(params ModSource[] extra) => ModLoader.Build(extra.Prepend(
        ModLoader.FromAssembly(new ModManifest { Id = "base", Version = "1" }, typeof(TestRulesetMod).Assembly, AppContext.BaseDirectory)));

    private sealed class BonusDef : ContentDef
    {
        public long Amount { get; init; }
        public Dictionary<string, int> Values { get; init; } = new();
    }
    public sealed class Counter
    {
        public long Value { get; set; }
        public SortedDictionary<string, long> Values { get; set; } = new(StringComparer.Ordinal);
        public List<int> Samples { get; set; } = [1];
        public SortedSet<string> Tags { get; set; } = new(StringComparer.Ordinal) { "Z", "a" };
    }
    private sealed class SourceModifier(string label, long percent) : IModifierSource
    {
        public void Contribute(ModifierContext context, ModifierSink sink)
        {
            sink.AddYield(label, new(2, 3, 4));
            sink.AddYield(label, new(0, percent, 0), true);
            sink.AddCommerce(label, new(1, 2, 3, 4));
            sink.AddCommerce(label, new(0, percent, 0, 0), true);
            sink.Add(label, "test:health", 5);
        }
    }

    [Fact]
    public void GenericRegistriesFreezeAndContentIdentityIncludesDataAndModVersions()
    {
        ModRegistry<BonusDef>? registration = null;
        ModSource Mod(long amount, string version, bool reverse) => Source("extra", ctx =>
        {
            registration = ctx.Content<BonusDef>();
            registration.Register(new BonusDef
            {
                Id = "extra:stone", Amount = amount,
                Values = reverse ? new() { ["z"] = 3, ["a"] = 1 } : new() { ["a"] = 1, ["z"] = 3 },
            });
        }, version);
        var db = Content(Mod(7, "1", false));
        Assert.Equal(7, db.Get<BonusDef>().Get("extra:stone").Amount);
        Assert.True(db.TryGet<BonusDef>("extra:stone", out _));
        Assert.False(db.TryGet<BonusDef>("extra:missing", out _));
        Assert.Throws<InvalidOperationException>(() => registration!.Register(new BonusDef { Id = "extra:late" }));
        Assert.Equal(db.ContentHash, Content(Mod(7, "1", true)).ContentHash);
        Assert.NotEqual(db.ContentHash, Content(Mod(8, "1", false)).ContentHash);
        Assert.NotEqual(db.ContentHash, Content(Mod(7, "2", false)).ContentHash);
    }

    [Fact]
    public void RequirementsReadCombinedClausesAndExplainFailuresWithoutLeakingFailedAlternatives()
    {
        var sim = TestGame.Sim();
        var requirement = JsonSerializer.Deserialize<Requirement>("""
            {"all":["base:agriculture"],"any":["base:writing",{"kind":"tech","id":"base:mining","count":1,"param":2}],"not":"base:monarchy"}
            """)!;
        var service = sim.Content.Requirements;
        sim.TeamOf(0).Techs.Clear();
        var result = service.Evaluate(requirement, new(sim, 0));
        Assert.False(result.Success);
        Assert.Equal(["base:agriculture", "base:writing", "base:mining"], result.Failures.Select(f => f.Leaf!.Id));
        sim.TeamOf(0).Techs.UnionWith(["base:agriculture", "base:mining"]);
        Assert.True(service.Evaluate(requirement, new(sim, 0)).Success);
        sim.TeamOf(0).Techs.Add("base:monarchy");
        Assert.Equal("base:monarchy", Assert.Single(service.Evaluate(requirement, new(sim, 0)).Failures).Leaf!.Id);
        var roundTrip = JsonSerializer.Deserialize<Requirement>(JsonSerializer.Serialize(requirement))!;
        Assert.Equal(CanonicalJson.Serialize(requirement), CanonicalJson.Serialize(roundTrip));
        Assert.True(service.Evaluate(new Requirement.All([]), new(sim, 0)).Success);
        Assert.False(service.Evaluate(new Requirement.Any([]), new(sim, 0)).Success);
        Assert.NotEmpty(service.Evaluate(new Requirement.Not(new Requirement.All([])), new(sim, 0)).Failures);
    }

    [Fact]
    public void UnknownRequirementKindsAndDuplicateEvaluatorsFailDuringLoad()
    {
        var bad = Source("extra", ctx => ctx.Content<BonusDef>().Register(new BonusDef
        {
            Id = "extra:stone", Requirements = new Requirement.Not(new Requirement.Leaf("bonus", "extra:stone")),
        }));
        var error = Assert.Throws<InvalidDataException>(() => Content(bad));
        Assert.Contains("bonus", error.Message);
        var duplicate = Source("extra", ctx => ctx.RegisterService<IRequirementEvaluator>("duplicate", new TechEvaluator()));
        Assert.Contains("tech", Assert.Throws<InvalidOperationException>(() => Content(duplicate)).Message);
    }
    private sealed class TechEvaluator : IRequirementEvaluator
    {
        public string Kind => "tech";
        public string? Evaluate(Requirement.Leaf leaf, RequirementContext context) => null;
    }

    [Fact]
    public void SharedTeamCombinesStartingTechsAndResearchAndRetainsSwitchedTargetProgress()
    {
        var sim = new Simulation(TestGame.Content(), new SimulationSettings
        {
            Map = new(17, 17), Civilizations = ["base:scholars", "base:builders"], PlayerTeams = [4, 4],
        });
        var team = Assert.Single(sim.State.Teams);
        Assert.Equal([0, 1], team.Members);
        Assert.True(sim.KnowsTech(1, "base:agriculture"));
        Assert.True(sim.KnowsTech(0, "base:mining"));
        foreach (int player in team.Members)
        {
            var site = Sites.Best(sim, player);
            Assert.NotNull(site);
            Assert.True(sim.TryApply(new FoundCityCommand { Player = player, Position = site.Value }, out _));
        }
        Assert.True(sim.TryApply(new SetResearchCommand { Player = 0, Tech = "base:the_wheel" }, out _));
        long expected = team.Members.Sum(p => sim.State.Cities.Where(c => c.Owner == p).Sum(c => (long)Yields.OfCity(sim, c).Commerce) * (100 + Bonuses.Research(sim, p)) / 100);
        new ResearchSystem().Run(sim);
        Assert.Equal(expected, team.ResearchProgress);
        sim.TryApply(new SetResearchCommand { Player = 1, Tech = "base:pottery" }, out _);
        sim.TryApply(new SetResearchCommand { Player = 1, Tech = "base:the_wheel" }, out _);
        Assert.Equal(expected, team.ResearchProgress);
        team.ResearchProgress = 1000;
        new ResearchSystem().Run(sim);
        Assert.True(sim.KnowsTech(0, "base:the_wheel"));
        Assert.True(sim.KnowsTech(1, "base:the_wheel"));
        Assert.Single(sim.Events.Published.OfType<TechDiscovered>());
    }

    [Fact]
    public void WarMatrixAndRiverEdgesAreSymmetricIncludingWrapSeams()
    {
        var sim = new Simulation(TestGame.Content(), new SimulationSettings { Map = new(4, 3, true) });
        sim.State.SetWar(0, 1, true);
        Assert.Contains(1, sim.TeamOf(0).Wars);
        Assert.Contains(0, sim.TeamOf(1).Wars);
        sim.State.SetWar(0, 1, false);
        Assert.Empty(sim.TeamOf(0).Wars);
        Assert.Empty(sim.TeamOf(1).Wars);
        var tile = sim.State.Grid.TileAt(0, 1);
        sim.State.SetRiver(tile, 3, true);
        Assert.Equal(8, sim.State.Plots[tile.Value].RiverEdges);
        Assert.Equal(1, sim.State.Plots[sim.State.Grid.TileAt(3, 1).Value].RiverEdges);
        sim.State.SetRiver(tile, 3, false);
        Assert.All(sim.State.Plots, p => Assert.Equal(0, p.RiverEdges));
    }

    [Fact]
    public void PlayerAndTeamStateEnforcesForeignKeysAndBidirectionalMembership()
    {
        var state = new GameState { Map = new(4, 3, false), Plots = Enumerable.Range(0, 12).Select(_ => new Plot()).ToArray() };
        var team0 = new Team { Id = 0 };
        var team1 = new Team { Id = 1 };
        state.AddTeam(team0);
        state.AddTeam(team1);
        Assert.Throws<ArgumentException>(() => state.AddTeam(new Team { Id = 0 }));

        Assert.Equal(team0, state.FindTeam(0));
        Assert.Equal(team1, state.TeamById(1));
        Assert.Null(state.FindTeam(99));

        // AddPlayer validates foreign key to Team
        Assert.Throws<ArgumentException>(() => state.AddPlayer(new Player { Id = 0, TeamId = 99 }));

        var p0 = new Player { Id = 0, TeamId = 0 };
        var p1 = new Player { Id = 1, TeamId = 0 };
        state.AddPlayer(p0);
        state.AddPlayer(p1);
        Assert.Throws<ArgumentException>(() => state.AddPlayer(new Player { Id = 0, TeamId = 0 }));

        Assert.Equal(p0, state.FindPlayer(0));
        Assert.Equal(p1, state.PlayerById(1));
        Assert.Null(state.FindPlayer(99));

        // Bidirectional membership is synchronized on add
        Assert.True(team0.HasMember(0));
        Assert.True(team0.HasMember(1));
        Assert.Equal([0, 1], team0.Members);
        Assert.Empty(team1.Members);
        Assert.Equal(team0, state.TeamOf(0));
        Assert.Equal(team0, state.TeamOf(1));

        // Transfer player between teams
        state.SetPlayerTeam(1, 1);
        Assert.Equal(1, p1.TeamId);
        Assert.True(team0.HasMember(0));
        Assert.False(team0.HasMember(1));
        Assert.Equal([0], team0.Members);
        Assert.True(team1.HasMember(1));
        Assert.Equal([1], team1.Members);
        Assert.Equal(team1, state.TeamOf(1));

        // Invalid team transfer throws
        Assert.Throws<ArgumentException>(() => state.SetPlayerTeam(1, 99));
        Assert.Throws<ArgumentException>(() => state.SetPlayerTeam(99, 0));

        // RebuildIndexes validates foreign keys
        p1.TeamId = 99;
        Assert.Throws<InvalidDataException>(() => state.RebuildIndexes());
        p1.TeamId = 1;

        // RebuildIndexes synchronizes bidirectional membership and purges orphaned member IDs
        team0.Members.Add(999); // phantom player
        team1.Members.Clear(); // desynced missing member
        state.RebuildIndexes();
        Assert.Equal([0], team0.Members);
        Assert.Equal([1], team1.Members);
        Assert.False(team0.HasMember(999));
    }

    [Fact]
    public void MonotonicEntityIdentifiersAdvancePastExplicitIdsAndAutoAllocateWhenUnassigned()
    {
        var state = new GameState { Map = new(6, 6, false), Plots = Enumerable.Range(0, 36).Select(_ => new Plot()).ToArray() };
        Assert.Equal(1, state.NextCityId);
        Assert.Equal(1, state.NextUnitId);

        // Auto-allocation when Id <= 0
        var c1 = new City { Position = new Hex(0, 0), Name = "First" };
        var u1 = new Unit { Position = new Hex(1, 0), Type = "base:warrior" };
        state.AddCity(c1);
        state.AddUnit(u1);
        Assert.Equal(1, c1.Id);
        Assert.Equal(1, u1.Id);
        Assert.Equal(2, state.NextCityId);
        Assert.Equal(2, state.NextUnitId);

        // Explicit Id >= NextId advances the monotonic counter
        var c50 = new City { Id = 50, Position = new Hex(2, 0), Name = "Fifty" };
        var u100 = new Unit { Id = 100, Position = new Hex(3, 0), Type = "base:warrior" };
        state.AddCity(c50);
        state.AddUnit(u100);
        Assert.Equal(51, state.NextCityId);
        Assert.Equal(101, state.NextUnitId);

        // Subsequent allocations avoid colliding with the injected IDs
        var c51 = new City { Position = new Hex(4, 0), Name = "Fifty-One" };
        var u101 = new Unit { Position = new Hex(5, 0), Type = "base:warrior" };
        state.AddCity(c51);
        state.AddUnit(u101);
        Assert.Equal(51, c51.Id);
        Assert.Equal(101, u101.Id);
        Assert.Equal(52, state.NextCityId);
        Assert.Equal(102, state.NextUnitId);

        // Duplicate IDs throw
        Assert.Throws<ArgumentException>(() => state.AddCity(new City { Id = 50, Position = new Hex(0, 1) }));
        Assert.Throws<ArgumentException>(() => state.AddUnit(new Unit { Id = 100, Position = new Hex(1, 1), Type = "base:warrior" }));

        // RebuildIndexes repairs lower NextId values and preserves monotonicity
        state.NextCityId = 1;
        state.NextUnitId = 1;
        state.RebuildIndexes();
        Assert.True(state.NextCityId > 51);
        Assert.True(state.NextUnitId > 101);
        Assert.Equal(52, state.NextCityId);
        Assert.Equal(102, state.NextUnitId);

        // RebuildIndexes rejects duplicate entity IDs
        var badDuplicateCity = new GameState
        {
            Map = new(6, 6, false),
            Plots = Enumerable.Range(0, 36).Select(_ => new Plot()).ToArray(),
            Cities = [new City { Id = 10, Position = new Hex(0, 0) }, new City { Id = 10, Position = new Hex(1, 0) }],
        };
        Assert.Throws<ArgumentException>(() => badDuplicateCity.RebuildIndexes());

        var badDuplicateUnit = new GameState
        {
            Map = new(6, 6, false),
            Plots = Enumerable.Range(0, 36).Select(_ => new Plot()).ToArray(),
            Units = [new Unit { Id = 20, Position = new Hex(0, 0) }, new Unit { Id = 20, Position = new Hex(1, 0) }],
        };
        Assert.Throws<ArgumentException>(() => badDuplicateUnit.RebuildIndexes());
    }

    [Fact]
    public void PlotVisibilitySupportsMoreThan64TeamsAndHasHistoryIndependentEncoding()
    {
        var sim = TestGame.Sim();
        var plot = sim.State.Plots[0];
        ulong initial = sim.ComputeHash();
        foreach (int team in new[] { 127, 0, 63, 64 }) Assert.True(plot.Revealed.Add(team));
        Assert.False(plot.Revealed.Add(63));
        plot.Visible.Add(64);
        Assert.Equal([0, 63, 64, 127], plot.Revealed);
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.Equal([0, 63, 64, 127], loaded.State.Plots[0].Revealed);
        Assert.Equal([64], loaded.State.Plots[0].Visible);
        Assert.True(loaded.State.Plots[0].Revealed.Contains(127));
        foreach (int team in new[] { 127, 0, 63, 64 }) Assert.True(plot.Revealed.Remove(team));
        plot.Visible.Clear();
        Assert.Equal(initial, sim.ComputeHash());
    }

    [Fact]
    public void StoredPlotHeightSurvivesCloneAndSaveLoad()
    {
        var sim = TestGame.Sim();
        var plot = sim.State.Plots[sim.State.Grid.TileAt(3, 3).Value];
        plot.Height = Levels.Hills;
        Assert.Equal(Levels.Hills, sim.ForkSpeculative().State.Plots[sim.State.Grid.TileAt(3, 3).Value].Height);
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.Equal(Levels.Hills, loaded.State.Plots[sim.State.Grid.TileAt(3, 3).Value].Height);
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        plot.Height = Levels.Flat;
        Assert.NotEqual(sim.ComputeHash(), loaded.ComputeHash());
    }

    [Fact]
    public void ModifiersAreSummedInRegistrationOrderAndLedgerExplainsTypedAndKeyedTotals()
    {
        var extra = Source("extra", ctx =>
        {
            ctx.RegisterService<IModifierSource>("z_first", new SourceModifier("First", 25));
            ctx.RegisterService<IModifierSource>("a_second", new SourceModifier("Second", 50));
        });
        var sim = new Simulation(Content(extra), new SimulationSettings { PlayerCount = 1, Civilizations = ["base:builders"] });
        var effects = ModifierSink.Collect(new(sim, ModifierScope.City, 0));
        Assert.Equal(new YieldVector(4, 6, 8), effects.FlatYield);
        Assert.Equal(new CommerceVector(2, 4, 6, 8), effects.FlatCommerce);
        Assert.Equal(new YieldVector(4, 10, 8), effects.FlatYield.WithPercent(effects.PercentYield));
        Assert.Equal(new CommerceVector(2, 7, 6, 8), effects.FlatCommerce.WithPercent(effects.PercentCommerce));
        Assert.Equal(10, effects.Ledger.Total("test:health"));
        Assert.Equal(75, effects.Ledger.Total("yield.percent.production"));
        Assert.Equal(["First", "Second"], effects.Ledger.Lines.Select(l => l.Source).Distinct());
        Assert.Equal(25 + 50 + 25, Bonuses.Production(sim, 0)); // civilization source participates too
        Assert.Throws<ArgumentException>(() => effects.Add("bad", "health", 1));
    }

    [Fact]
    public void EventsDispatchInModAndSubscriptionOrderAndNestedPublishesAreFifo()
    {
        var calls = new List<string>();
        var a = Source("a", ctx =>
        {
            ctx.Subscribe<CityFounded>("z_first", (sim, e) => { calls.Add("a:first"); sim.Events.Publish(new CityGrew(e.City, 2)); });
            ctx.Subscribe<CityFounded>("a_second", (_, _) => calls.Add("a:second"));
            ctx.Subscribe<CityGrew>("grew", (_, _) => calls.Add("a:grew"));
        });
        var b = Source("b", ctx => ctx.Subscribe<DomainEvent>("all", (_, e) => calls.Add("b:" + e.GetType().Name)));
        var sim = new Simulation(Content(b, a), new SimulationSettings());
        calls.Clear();
        ulong before = sim.ComputeHash();
        sim.Events.Publish(new CityFounded(99, 0, new Hex(0, 0)));
        Assert.Equal(["a:first", "a:second", "b:CityFounded", "a:grew", "b:CityGrew"], calls);
        Assert.Equal(before, sim.ComputeHash()); // presentation-only history
        sim.AdvanceTurn();
        Assert.Contains(sim.Events.PreviousTurn, e => e is CityFounded);
        Assert.IsType<TurnEnded>(sim.Events.PreviousTurn.Last());
        Assert.IsType<TurnStarted>(sim.Events.Published.First());
    }

    [Fact]
    public void BaseGamePublishesFoundingGrowthProductionUnitAndCombatEvents()
    {
        var sim = TestGame.Sim();
        var site = Sites.Best(sim, 0)!.Value;
        sim.TryApply(new FoundCityCommand { Player = 0, Position = site }, out _);
        var city = sim.State.Cities[0];
        city.FoodStored = 1000;
        new GrowthSystem().Run(sim);
        city.Building = "base:granary"; city.BuildProgress = 1000;
        new ProductionSystem().Run(sim);
        var a = UnitRules.Spawn(sim, 0, "base:warrior", site);
        var b = UnitRules.Spawn(sim, 1, "base:warrior", site);
        UnitRules.Fight(sim, a, b);
        Assert.Single(sim.Events.Published.OfType<CityFounded>());
        Assert.Single(sim.Events.Published.OfType<CityGrew>());
        Assert.Single(sim.Events.Published.OfType<BuildingCompleted>());
        Assert.Equal(2, sim.Events.Published.OfType<UnitCreated>().Count());
        var combat = Assert.Single(sim.Events.Published.OfType<CombatResolved>());
        Assert.Equal(sim.State.Units.Single().Id, combat.Winner);
    }

    [Fact]
    public void SpatialIndexesTrackCreationMovementRemovalAndLoad()
    {
        var sim = TestGame.Sim();
        var site = Sites.Best(sim, 0)!.Value;
        sim.TryApply(new FoundCityCommand { Player = 0, Position = site }, out _);
        var city = sim.State.CityAt(site)!;
        Assert.Equal(city, sim.State.CityById(city.Id));
        var a = UnitRules.Spawn(sim, 0, "base:warrior", site);
        var b = UnitRules.Spawn(sim, 0, "base:warrior", site);
        Assert.Equal([a.Id, b.Id], sim.State.UnitsAt(site).Select(u => u.Id));
        var next = sim.State.Grid.Neighbors(site).First();
        sim.State.MoveUnit(a.Id, next);
        Assert.Equal(b.Id, Assert.Single(sim.State.UnitsAt(site)).Id);
        Assert.Equal(a.Id, Assert.Single(sim.State.UnitsAt(next)).Id);
        sim.State.RemoveUnit(b.Id);
        Assert.Empty(sim.State.UnitsAt(site));
        Assert.Null(sim.State.FindUnit(b.Id));
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.Equal(a.Id, Assert.Single(loaded.State.UnitsAt(next)).Id);
        Assert.Equal(city.Id, loaded.State.CityAt(site)!.Id);
        loaded.State.RemoveCity(city.Id);
        Assert.Null(loaded.State.CityAt(site));
        Assert.Null(loaded.State.FindCity(city.Id));
    }

    [Fact]
    public void ExtensionStateOnEveryOwnerSurvivesSaveAndMutationsChangeHash()
    {
        var db = Content(Source("extra", ctx => ctx.RegisterStateComponent<Counter>("counter")));
        var sim = new Simulation(db, new SimulationSettings { PlayerCount = 1 });
        var site = Sites.Best(sim, 0)!.Value;
        sim.TryApply(new FoundCityCommand { Player = 0, Position = site }, out _);
        var unit = UnitRules.Spawn(sim, 0, "base:warrior", site);
        var owners = new[] { sim.State.Extensions, sim.State.Players[0].Extensions, sim.TeamOf(0).Extensions, sim.State.Cities[0].Extensions, unit.Extensions };
        for (int i = 0; i < owners.Length; i++) owners[i].Set(db, new Counter { Value = i, Values = new(StringComparer.Ordinal) { ["z"] = 1, ["a"] = 2 } });
        string save = SaveGame.Save(sim);
        var loaded = SaveGame.Load(db, save);
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        string unknown = save.Replace("extra:counter", "extra:missing", StringComparison.Ordinal);
        Assert.Contains("Unknown state component", Assert.Throws<InvalidDataException>(() => SaveGame.Load(db, unknown, force: true)).Message);
        var loadedOwners = new[] { loaded.State.Extensions, loaded.State.Players[0].Extensions, loaded.TeamOf(0).Extensions, loaded.State.Cities[0].Extensions, loaded.State.Units[0].Extensions };
        for (int i = 0; i < owners.Length; i++)
        {
            var counter = loadedOwners[i].Get<Counter>(db)!;
            Assert.Equal(i, counter.Value);
            Assert.Equal([1], counter.Samples);
            Assert.Equal(["Z", "a"], counter.Tags);
            ulong before = loaded.ComputeHash();
            loadedOwners[i].Get<Counter>(db)!.Value++;
            Assert.NotEqual(before, loaded.ComputeHash());
        }
        Assert.Throws<InvalidOperationException>(() => sim.State.Extensions.Set(db, new object()));
    }

    private sealed class FloatingState { public double Value { get; set; } }

    [Fact]
    public void FloatingPointStateComponentsAreRejectedBeforePlay()
    {
        var mod = Source("extra", ctx => ctx.RegisterStateComponent<FloatingState>("floating"));
        Assert.Contains("non-integer", Assert.Throws<InvalidOperationException>(() => Content(mod)).Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(17)]
    [InlineData(41)]
    [Trait("Category", "E2E")]
    public void AiSaveReloadResumesWithoutRerunningSetupOrAiAndMatchesEveryFollowingTurn(int checkpoint)
    {
        var sim = new Simulation(TestGame.Content(), new SimulationSettings
        {
            Seed = 11, Map = new(17, 13, true), PlayerAis = ["base:basic", "base:basic"],
        });
        for (int i = 0; i < checkpoint; i++) sim.AdvanceTurn();
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.Empty(loaded.Events.Published);
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        for (int i = checkpoint; i < 65; i++)
        {
            sim.AdvanceTurn(); loaded.AdvanceTurn();
            Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        }
    }

    [Fact]
    public void SaveRejectsWrongSchemaAndContentButSupportsExplicitForcedContentLoad()
    {
        var a = Content(Source("extra", ctx => ctx.Content<BonusDef>().Register(new BonusDef { Id = "extra:stone", Amount = 1 })));
        var b = Content(Source("extra", ctx => ctx.Content<BonusDef>().Register(new BonusDef { Id = "extra:stone", Amount = 2 })));
        var sim = new Simulation(a, new SimulationSettings());
        string save = SaveGame.Save(sim);
        Assert.Contains("content mismatch", Assert.Throws<InvalidDataException>(() => SaveGame.Load(b, save)).Message);
        Assert.Equal(sim.ComputeHash(), SaveGame.Load(b, save, force: true).ComputeHash());
        string future = save.Replace($"\"SchemaVersion\":{SaveGame.SchemaVersion}", "\"SchemaVersion\":99", StringComparison.Ordinal);
        Assert.Contains("schema", Assert.Throws<InvalidDataException>(() => SaveGame.Load(a, future, force: true)).Message);
    }

    [Fact]
    public void SaveAcceptanceIncludesThePreRenameContentDigestButNothingElse()
    {
        var content = TestGame.Content();
        var sim = new Simulation(content, new SimulationSettings());
        string save = SaveGame.Save(sim);
        Assert.NotEqual(content.ContentHash, content.LegacyContentHash);
        // A save written before the namespace rename carries the digest this same content produced then.
        string legacy = save.Replace(content.ContentHash, content.LegacyContentHash, StringComparison.Ordinal);
        Assert.NotEqual(save, legacy);
        Assert.Equal(sim.ComputeHash(), SaveGame.Load(content, legacy).ComputeHash());
        // Only that exact digest is accepted: any other digest is still a mismatch.
        string unknown = save.Replace(content.ContentHash, "0" + content.LegacyContentHash[1..], StringComparison.Ordinal);
        Assert.Contains("content mismatch", Assert.Throws<InvalidDataException>(() => SaveGame.Load(content, unknown)).Message);
    }

    [Fact]
    public void AreaSizeFollowsAreaIdChangesAndReplacedPlots()
    {
        var state = new GameState { Map = new(3, 1, false), Plots = [new() { AreaId = 0 }, new() { AreaId = 0 }, new() { AreaId = 2 }] };
        state.RebuildIndexes();
        Assert.Equal(2, state.AreaSize(0));
        Assert.Equal(1, state.AreaSize(2));
        Assert.Equal(0, state.AreaSize(1));
        state.Plots[1].AreaId = 2;
        Assert.Equal(1, state.AreaSize(0));
        Assert.Equal(2, state.AreaSize(2));
        state.Plots = [new() { AreaId = 5 }, new() { AreaId = 5 }, new() { AreaId = 5 }];
        Assert.Equal(3, state.AreaSize(5));
        Assert.Equal(0, state.AreaSize(2));
        Assert.Equal(3, state.Clone().AreaSize(5));
    }

    [Fact]
    public void HashIncludesPlotTeamTreasuryQueueAndIdentityFields()
    {
        var sim = TestGame.Sim();
        var plot = sim.State.Plots[0];
        var actions = new Action[]
        {
            () => plot.Terrain = "base:hills", () => plot.Feature = "world:forest", () => plot.Bonus = "world:iron",
            () => plot.Improvement = "world:mine", () => plot.Route = "world:road", () => plot.RiverEdges = 1,
            () => plot.Owner = 0, () => plot.Culture[0] = 2, () => plot.Revealed.Add(0), () => plot.Visible.Add(0),
            () => plot.AreaId = 4, () => sim.TeamOf(0).Techs.Add("base:writing"),
            () => sim.TeamOf(0).Research["base:pottery"] = 2, () => sim.TeamOf(0).Researching = "base:pottery",
            () => sim.State.SetWar(0, 1, true), () => sim.State.Players[0].Treasury = 3,
        };
        foreach (var action in actions) { ulong before = sim.ComputeHash(); action(); Assert.NotEqual(before, sim.ComputeHash()); }
        var site = Sites.Best(sim, 0)!.Value;
        sim.TryApply(new FoundCityCommand { Player = 0, Position = site }, out _);
        ulong hash = sim.ComputeHash();
        sim.State.Cities[0].ProductionQueue.Add(new("cities:building", "base:granary", 2));
        Assert.NotEqual(hash, sim.ComputeHash());
    }

    [Fact]
    public void EndTurnRejectsSecondEndForSamePlayerWithoutChangingState()
    {
        var sim = TestGame.Sim();
        Assert.True(sim.TryApply(new EndTurnCommand { Player = 0 }, out _));
        ulong before = sim.ComputeHash();
        Assert.False(sim.TryApply(new EndTurnCommand { Player = 0 }, out var error));
        Assert.Equal("Player has already ended turn.", error);
        Assert.Equal("unknown player", new EndTurnCommand { Player = 9 }.Validate(sim));
        Assert.Equal(before, sim.ComputeHash());
        Assert.Equal(1, sim.State.Turn);
        Assert.True(sim.TryApply(new EndTurnCommand { Player = 1 }, out _));
        Assert.Equal(2, sim.State.Turn);
    }

    [Fact]
    public void PercentModifiersClampAtZeroInsteadOfInvertingYields()
    {
        Assert.Equal(new YieldVector(0, 20, 0), new YieldVector(10, 20, 30).WithPercent(new(-150, 0, -100)));
        Assert.Equal(new YieldVector(0, 0, 0), new YieldVector(10, 20, 30).WithPercent(new(-1000, -1000, -1000)));
        Assert.Equal(new YieldVector(5, 30, 45), new YieldVector(10, 20, 30).WithPercent(new(-50, 50, 50)));
        Assert.Equal(new CommerceVector(0, 0, 0, 40), new CommerceVector(10, 20, 30, 40).WithPercent(new(-200, -150, -101, 0)));
        Assert.Equal(new CommerceVector(5, 30, 45, 0), new CommerceVector(10, 20, 30, 0).WithPercent(new(-50, 50, 50, 0)));
    }

    [Fact]
    public void OptionalStateReadsReturnNullWhenTheOwningModIsNotLoaded()
    {
        var sim = TestGame.Sim();
        var player = sim.State.PlayerById(0);
        Assert.Null(player.Extensions.TryGet<GovernmentPlayerState>(sim.Content));
        Assert.Throws<InvalidOperationException>(() => player.Extensions.Get<GovernmentPlayerState>(sim.Content));
        Assert.False(GovernmentStatus.IsAnarchy(sim, 0));
        Assert.True(sim.TryApply(new FoundCityCommand { Player = 0, Position = TestGame.FindLand(sim) }, out _));
        Assert.False(GovernmentStatus.IsDisorder(sim, sim.State.Cities[0]));
    }

    [Fact]
    public void RejectedCityAndUnitInsertsAndMovesLeaveAllIndexesUnchanged()
    {
        var state = new GameState { Map = new(4, 3) };
        state.AddCity(new City { Id = 1, Owner = 0, Position = new Hex(0, 0) });
        state.AddUnit(new Unit { Id = 1, Owner = 0, Type = "base:warrior", Position = new Hex(1, 0) });
        Assert.Throws<ArgumentException>(() => state.AddCity(new City { Id = 2, Owner = 0, Position = new Hex(0, 0) })); // plot taken
        Assert.Throws<ArgumentException>(() => state.AddCity(new City { Id = 1, Owner = 0, Position = new Hex(2, 0) })); // id taken
        Assert.Throws<ArgumentException>(() => state.AddUnit(new Unit { Id = 1, Owner = 0, Type = "base:warrior", Position = new Hex(2, 0) }));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.MoveUnit(1, new Hex(9, 9)));
        Assert.Equal([1], state.Cities.Select(c => c.Id));
        Assert.Null(state.FindCity(2));
        Assert.Null(state.CityAt(new Hex(2, 0)));
        Assert.Equal(1, state.CityAt(new Hex(0, 0))!.Id);
        Assert.Equal([1], state.Units.Select(u => u.Id));
        Assert.Equal(new Hex(1, 0), state.UnitById(1).Position);
        Assert.Equal(1, Assert.Single(state.UnitsAt(new Hex(1, 0))).Id);
        Assert.Empty(state.UnitsAt(new Hex(2, 0)));
        state.MoveUnit(1, new Hex(1, 1));
        Assert.Empty(state.UnitsAt(new Hex(1, 0)));
        Assert.Equal(1, Assert.Single(state.UnitsAt(new Hex(1, 1))).Id);
    }

    /// <summary>One row per command payload shape: a command, and the envelope type id the wire must carry for it.</summary>
    public static TheoryData<GameCommand, string> CommandEnvelopes => new()
    {
        { new FoundCityCommand { Player = 1, Position = new Hex(2, -1) }, "base:found_city" },
        { new EndTurnCommand { Player = 0 }, "core:end_turn" },
        { new AttackCommand { Player = 0, UnitId = 4, Target = new Hex(1, 1) }, "base:attack" },
        { new PromoteUnitCommand { Player = 0, UnitId = 4, Promotion = "base:shock" }, "base:promote_unit" },
        { new SetCityWorkCommand { Player = 0, CityId = 1, Tiles = [new Hex(1, 0)], Specialists = new() { ["base:priest"] = 1 } }, "base:set_city_work" },
        { new SetCivicCommand { Player = 0, Civic = "base:monarchy" }, "base:set_civic" },
    };

    /// <summary>Every payload shape survives encode→decode→encode byte for byte, so no field can be silently dropped or
    /// reordered by the shared JSON settings, and the envelope names the registered command type.</summary>
    [Theory]
    [MemberData(nameof(CommandEnvelopes))]
    public void Command_envelopes_round_trip_without_losing_a_field(GameCommand command, string typeId)
    {
        var codec = new CommandCodec(TestGame.Content());
        var text = codec.Encode(command);

        Assert.Equal(typeId, JsonSerializer.Deserialize<JsonElement>(text, ModLoader.Json).GetProperty("Type").GetString());
        var decoded = codec.Decode(text);

        Assert.Equal(command.GetType(), decoded.GetType());
        Assert.Equal(text, codec.Encode(decoded));
    }
}
