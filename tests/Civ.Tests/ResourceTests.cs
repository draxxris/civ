using Civ.BaseGame;
using Civ.Client;
using Civ.Content;
using Civ.Game;
using Civ.Modding;
using Xunit;

namespace Civ.Tests;

/// <summary>The core resource and institution API, exercised with small inline mods.</summary>
public class ResourceTests
{
    private sealed class TestMod(Action<IModContext> body) : IMod
    {
        public void Register(IModContext ctx) => body(ctx);
    }

    private static ContentDb With(Action<IModContext> body)
    {
        var baseSrc = ModLoader.FromAssembly(new ModManifest { Id = "base" }, typeof(BaseGameMod).Assembly, AppContext.BaseDirectory);
        var extra = new ModSource { Manifest = new ModManifest { Id = "t", Dependencies = ["base"] }, Instance = new TestMod(body), Root = AppContext.BaseDirectory };
        return ModLoader.Build([baseSrc, extra]);
    }

    private static Simulation Sim(ContentDb db, ulong seed = 7) => new(db, new SimulationSettings
    {
        Seed = seed, PlayerCount = 2, MapRadius = 8, Civilizations = ["base:builders", "base:builders"],
    });

    internal static City Found(Simulation sim, int player, Hex at)
    {
        Assert.True(sim.TryApply(new FoundCityCommand { Player = player, Position = at }, out var err), err);
        return sim.State.Cities.Last();
    }

    /// <summary>Two land sites 3 to 4 apart, so both cities can be founded and are within spreading range.</summary>
    internal static (Hex, Hex) TwoSites(Simulation sim)
    {
        var land = sim.State.Terrain.Keys.Where(h => { var t = sim.TerrainAt(h); return !t.IsWater && t.Passable; })
            .OrderBy(h => h.Q).ThenBy(h => h.R).ToList();
        foreach (var a in land)
            foreach (var b in land)
                if (a.DistanceTo(b) is 3 or 4) return (a, b);
        throw new InvalidOperationException("no two sites");
    }

    private static readonly ResourceDef Gems = new()
    {
        Id = "t:gems", Name = "Gems",
        Rates = [new() { From = "population", Amount = 2 }, new() { From = "building", Id = "base:library", Amount = 5 }],
    };

    [Fact]
    public void PlayerResourcesAccumulateFromEveryCityEachTurn()
    {
        var sim = Sim(With(ctx => ctx.Resources.Register(Gems)));
        var (a, b) = TwoSites(sim);
        var c1 = Found(sim, 0, a);
        Found(sim, 0, b);
        c1.Buildings.Add("base:library");

        Assert.Equal(9, Resources.PlayerRate(sim, 0, "t:gems")); // 2 + 5, plus 2
        new ResourceSystem().Run(sim);
        Assert.Equal(9, sim.State.PlayerById(0).Resources["t:gems"]);
        Assert.Empty(c1.Resources);
    }

    [Fact]
    public void CityScopedResourcesStayInTheirCityAndRespectTheCap()
    {
        var sim = Sim(With(ctx => ctx.Resources.Register(new ResourceDef
        {
            Id = "t:lore", Name = "Lore", Scope = ResourceScopes.City, Cap = 5,
            Rates = [new() { From = "city", Amount = 3 }],
        })));
        var (a, b) = TwoSites(sim);
        var c1 = Found(sim, 0, a);
        var c2 = Found(sim, 1, b);
        new ResourceSystem().Run(sim);
        new ResourceSystem().Run(sim);
        Assert.Equal(5, c1.Resources["t:lore"]);
        Assert.Equal(5, c2.Resources["t:lore"]);
        Assert.Empty(sim.State.PlayerById(0).Resources);
    }

    [Fact]
    public void FlowsAreNotStoredAndCodeSourcesCanConsume()
    {
        var sim = Sim(With(ctx =>
        {
            ctx.Resources.Register(new ResourceDef { Id = "t:water", Name = "Water", Stockpile = false, Rates = [new() { From = "city", Amount = 2 }] });
            ctx.ResourceSources.Register(new Drain());
        }));
        Found(sim, 0, TestGame.FindLand(sim));
        Assert.Equal(-1, Resources.PlayerRate(sim, 0, "t:water"));
        new ResourceSystem().Run(sim);
        Assert.Empty(sim.State.PlayerById(0).Resources);
    }

    private sealed class Drain : IResourceSource
    {
        public string Id => "t:drain";
        public void AddCityRates(Simulation sim, City city, IDictionary<string, long> rates) => rates["t:water"] = rates["t:water"] - 3;
    }

    [Fact]
    public void PlayerPoolsTakeTheNetOfAllCitiesSoOrderDoesNotMatter()
    {
        var sim = Sim(With(ctx =>
        {
            ctx.Resources.Register(new ResourceDef { Id = "t:coin", Name = "Coin", Rates = [new() { From = "building", Id = "base:market", Amount = 5 }] });
            ctx.ResourceSources.Register(new Upkeep());
        }));
        var (a, b) = TwoSites(sim);
        Found(sim, 0, a);
        Found(sim, 0, b).Buildings.Add("base:market"); // city 1 costs 2, city 2 earns 5 - 2
        new ResourceSystem().Run(sim);
        Assert.Equal(1, sim.State.PlayerById(0).Resources["t:coin"]);
    }

    private sealed class Upkeep : IResourceSource
    {
        public string Id => "t:upkeep";
        public void AddCityRates(Simulation sim, City city, IDictionary<string, long> rates) =>
            rates["t:coin"] = rates.TryGetValue("t:coin", out var v) ? v - 2 : -2;
    }

    [Fact]
    public void BuildingsWithAResourceCostPayWhenStartedAndCanBeRefusedForLackOfIt()
    {
        var sim = Sim(With(ctx =>
        {
            ctx.Resources.Register(Gems);
            ctx.Buildings.Register(new BuildingDef { Id = "t:vault", Name = "Vault", Cost = 10, ResourceCost = new() { ["t:gems"] = 4 } });
        }));
        var city = Found(sim, 0, TestGame.FindLand(sim));
        var start = new StartBuildingCommand { Player = 0, CityId = city.Id, Building = "t:vault" };
        Assert.Equal("needs 4 Gems (have 0)", start.Validate(sim));

        sim.State.PlayerById(0).Resources["t:gems"] = 6;
        Assert.True(sim.TryApply(start, out _));
        Assert.Equal(2, sim.State.PlayerById(0).Resources["t:gems"]);
        Assert.True(sim.TryApply(start, out _)); // re-picking the same building is free
        Assert.Equal(2, sim.State.PlayerById(0).Resources["t:gems"]);
    }

    [Fact]
    public void YieldSourcesFeedTheBaseGameYields()
    {
        var sim = Sim(With(ctx => ctx.YieldSources.Register(new Bonus())));
        var city = Found(sim, 0, TestGame.FindLand(sim));
        var expected = new Yield(0, 1, 1) + Yields.OfTile(sim, city.Position) + Yields.OfTile(sim, city.Worked[0]) + new Yield(1, 2, 3, 4);
        Assert.Equal(expected, Yields.OfCity(sim, city));
    }

    private sealed class Bonus : ICityYieldSource
    {
        public string Id => "t:bonus";
        public CityYield YieldFor(Simulation sim, City city) => new(1, 2, 3, 4);
    }

    [Theory]
    [InlineData("bad scope")]
    [InlineData("unknown rate target")]
    [InlineData("unknown cost")]
    [InlineData("spending a flow")]
    [InlineData("unknown spreader")]
    public void BadReferencesFailAtLoad(string which)
    {
        Action<IModContext> body = which switch
        {
            "bad scope" => ctx => ctx.Resources.Register(new ResourceDef { Id = "t:x", Scope = "planet" }),
            "unknown rate target" => ctx => ctx.Resources.Register(new ResourceDef { Id = "t:x", Rates = [new() { From = "building", Id = "base:nope", Amount = 1 }] }),
            "unknown cost" => ctx => ctx.Units.Register(new UnitDef { Id = "t:u", ResourceCost = new() { ["t:missing"] = 1 } }),
            "spending a flow" => ctx =>
            {
                ctx.Resources.Register(new ResourceDef { Id = "t:x", Stockpile = false });
                ctx.Units.Register(new UnitDef { Id = "t:u", ResourceCost = new() { ["t:x"] = 1 } });
            },
            _ => ctx => ctx.Institutions.Register(new InstitutionDef { Id = "t:i", Spreaders = ["base:nobody"] }),
        };
        Assert.Throws<InvalidOperationException>(() => With(body));
    }

    // --- institutions ---------------------------------------------------------------------------

    private static ContentDb Cults(int passive = 0) => With(ctx =>
    {
        ctx.Resources.Register(new ResourceDef { Id = "t:zeal", Name = "Zeal" });
        ctx.Units.Register(new UnitDef { Id = "t:preacher", Name = "Preacher", Moves = 9 });
        ctx.Institutions.Register(new InstitutionDef
        {
            Id = "t:red", Name = "Red", Category = "cult", FoundCost = new() { ["t:zeal"] = 10 }, Spreaders = ["t:preacher"],
            PassiveSpreadPercent = passive, SpreadRange = 4, Conflicts = ["t:blue"],
        });
        ctx.Institutions.Register(new InstitutionDef { Id = "t:blue", Name = "Blue", Category = "cult" });
    });

    [Fact]
    public void FoundingPaysTheCostRecordsTheHomeCityAndHappensOnce()
    {
        var sim = Sim(Cults());
        var (a, b) = TwoSites(sim);
        var c1 = Found(sim, 0, a);
        var c2 = Found(sim, 1, b);
        var found = new FoundInstitutionCommand { Player = 0, CityId = c1.Id, Institution = "t:red" };
        Assert.Equal("needs 10 Zeal (have 0)", found.Validate(sim));

        sim.State.PlayerById(0).Resources["t:zeal"] = 12;
        Assert.True(sim.TryApply(found, out _));
        Assert.Equal(2, sim.State.PlayerById(0).Resources["t:zeal"]);
        Assert.Contains("t:red", c1.Institutions);
        Assert.Equal(c1.Id, Institutions.HomeCity(sim, "t:red")!.Id);

        sim.State.PlayerById(1).Resources["t:zeal"] = 50;
        Assert.Equal("Red has already been founded", new FoundInstitutionCommand { Player = 1, CityId = c2.Id, Institution = "t:red" }.Validate(sim));
        Assert.Equal("not your city", new FoundInstitutionCommand { Player = 1, CityId = c1.Id, Institution = "t:blue" }.Validate(sim));
        Assert.Equal("Blue cannot share a city with Red", new FoundInstitutionCommand { Player = 0, CityId = c1.Id, Institution = "t:blue" }.Validate(sim));
    }

    [Fact]
    public void SpreaderUnitsCarryAnInstitutionToTheCityTheyStandIn()
    {
        var sim = Sim(Cults());
        var (a, b) = TwoSites(sim);
        var home = Found(sim, 0, a);
        var foreign = Found(sim, 1, b);
        sim.State.PlayerById(0).Resources["t:zeal"] = 10;
        Assert.True(sim.TryApply(new FoundInstitutionCommand { Player = 0, CityId = home.Id, Institution = "t:red" }, out _));

        var preacher = UnitRules.Spawn(sim, 0, "t:preacher", home.Position);
        var spread = new SpreadInstitutionCommand { Player = 0, UnitId = preacher.Id, Institution = "t:red" };
        Assert.Equal("City 1 already has Red", spread.Validate(sim));
        var warrior = UnitRules.Spawn(sim, 0, "base:warrior", foreign.Position);
        Assert.Equal("this unit cannot spread Red", new SpreadInstitutionCommand { Player = 0, UnitId = warrior.Id, Institution = "t:red" }.Validate(sim));

        preacher.Position = foreign.Position;
        Assert.True(sim.TryApply(spread, out var err), err);
        Assert.Contains("t:red", foreign.Institutions);
        Assert.DoesNotContain(sim.State.Units, u => u.Id == preacher.Id);
    }

    [Fact]
    public void PassiveSpreadIsDeterministic()
    {
        ulong Run()
        {
            var sim = Sim(Cults(passive: 30), seed: 3);
            var (a, b) = TwoSites(sim);
            var home = Found(sim, 0, a);
            var other = Found(sim, 1, b);
            sim.State.PlayerById(0).Resources["t:zeal"] = 10;
            Assert.True(sim.TryApply(new FoundInstitutionCommand { Player = 0, CityId = home.Id, Institution = "t:red" }, out _));
            for (int i = 0; i < 40 && !other.Institutions.Contains("t:red"); i++) sim.AdvanceTurn();
            Assert.Contains("t:red", other.Institutions);
            return sim.ComputeHash();
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void ResourcesAndInstitutionsAreCoveredByTheStateHash()
    {
        var sim = Sim(Cults());
        var city = Found(sim, 0, TestGame.FindLand(sim));
        var h0 = sim.ComputeHash();
        sim.State.PlayerById(0).Resources["t:zeal"] = 1;
        var h1 = sim.ComputeHash();
        city.Institutions.Add("t:blue");
        var h2 = sim.ComputeHash();
        Assert.NotEqual(h0, h1);
        Assert.NotEqual(h1, h2);
    }

    [Fact]
    public void CommandsTravelThroughTheCodec()
    {
        var db = Cults();
        var codec = new CommandCodec(db);
        var back = codec.Decode(codec.Encode(new SpreadInstitutionCommand { Player = 1, UnitId = 4, Institution = "t:red" }));
        var cmd = Assert.IsType<SpreadInstitutionCommand>(back);
        Assert.Equal((1, 4, "t:red"), (cmd.Player, cmd.UnitId, cmd.Institution));
    }
}
