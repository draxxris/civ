using System.Text.Json;
using Civ.BaseGame;
using Civ.Content;
using Civ.Game;
using Civ.Modding;
using Xunit;

namespace Civ.Tests;

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
        var from = sim.State.Cities[0].Position;
        var to = sim.State.Grid.Neighbors(from).First();
        sim.State.PlotAt(to).Terrain = "base:grassland";
        var mine = UnitRules.Spawn(sim, 0, "base:warrior", from);
        UnitRules.Spawn(sim, 1, "base:warrior", to);
        Assert.False(sim.TryApply(new AttackCommand { Player = 0, UnitId = mine.Id, Target = to }, out _));
        Assert.True(sim.TryApply(new MoveUnitCommand { Player = 0, UnitId = mine.Id, To = to }, out _));
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
        string future = save.Replace("\"SchemaVersion\":1", "\"SchemaVersion\":99", StringComparison.Ordinal);
        Assert.Contains("schema", Assert.Throws<InvalidDataException>(() => SaveGame.Load(a, future, force: true)).Message);
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
}
