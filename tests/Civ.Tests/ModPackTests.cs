using Civ.BaseGame;
using Civ.Client;
using Civ.Content;
using Civ.Game;
using Civ.Modding;
using Xunit;

namespace Civ.Tests;

/// <summary>
/// The religion, guilds and corporations mods, loaded from their own folders (mod.json + DLL + data) in separate load
/// contexts exactly as the client loads them. They use only the public API, so these tests only talk to them through ids.
/// </summary>
public class ModPackTests
{
    private static readonly string ModsRoot = Path.Combine(AppContext.BaseDirectory, "mods");

    private static ContentDb Content()
    {
        var baseSrc = ModLoader.FromAssembly(new ModManifest { Id = "base" }, typeof(BaseGameMod).Assembly, AppContext.BaseDirectory);
        return ModLoader.Build(ModDiscovery.LoadMods(ModsRoot).Prepend(baseSrc));
    }

    private static Simulation Sim(ulong seed = 7) => new(Content(), new SimulationSettings
    {
        Seed = seed, PlayerCount = 2, MapRadius = 8, Civilizations = ["base:builders", "base:builders"],
    });

    private static void Give(Simulation sim, int player, string resource, long amount) =>
        sim.State.PlayerById(player).Resources[resource] = sim.State.PlayerById(player).Resources.GetValueOrDefault(resource) + amount;

    [Fact]
    public void AllThreeModsLoadFromTheirFolders()
    {
        var db = Content();
        Assert.Equal(["base", "corporations", "guilds", "religion"], db.LoadedMods);
        Assert.Equal(["corporations:capital", "corporations:fish", "corporations:grain", "corporations:stone", "guilds:renown", "religion:faith"],
            db.Resources.All.Select(r => r.Id));
        Assert.Equal(3, db.Institutions.All.Count(i => i.Category == "religion"));
        Assert.Equal(3, db.Institutions.All.Count(i => i.Category == "guild"));
        Assert.Equal(3, db.Institutions.All.Count(i => i.Category == "corporation"));
        // Corporations sharing a good conflict; the mod derives that from its own data.
        Assert.Equal(["corporations:merchant_fleet"], db.Institutions.Get("corporations:granary_trust").Conflicts);
        Assert.Empty(db.Institutions.Get("corporations:quarry_combine").Conflicts);
    }

    // --- religion -------------------------------------------------------------------------------

    [Fact]
    public void PriestsAndShrinesMakeFaithWhichFoundsAReligion()
    {
        var sim = Sim();
        var (a, b) = ResourceTests.TwoSites(sim);
        var holy = ResourceTests.Found(sim, 0, a);
        var me = sim.State.PlayerById(0);
        me.Techs.Add("religion:mysticism");
        holy.Buildings.Add("religion:shrine");
        holy.Buildings.Add("base:temple");
        CityRules.Assign(holy, [], new Dictionary<string, int> { ["base:priest"] = 1 });

        Assert.Equal(2 + 1 + 2, Resources.PlayerRate(sim, 0, "religion:faith")); // priest, temple, shrine
        new ResourceSystem().Run(sim);
        Assert.Equal(5, me.Resources["religion:faith"]);

        var found = new FoundInstitutionCommand { Player = 0, CityId = holy.Id, Institution = "religion:sun_cult" };
        Assert.Equal("needs 40 Faith (have 5)", found.Validate(sim));
        Give(sim, 0, "religion:faith", 40);
        int cultureBefore = Yields.OfCity(sim, holy).Culture;
        Assert.True(sim.TryApply(found, out _));
        Assert.Equal(5, me.Resources["religion:faith"]);
        Assert.Equal(cultureBefore + 3, Yields.OfCity(sim, holy).Culture); // +1 follower, +2 holy city

        // A missionary needs a religion in the city and Faith, and carries it to another civ's city.
        var other = ResourceTests.Found(sim, 1, b);
        var train = new StartTrainingCommand { Player = 0, CityId = holy.Id, Unit = "religion:missionary" };
        Assert.Equal("needs 15 Faith (have 5)", train.Validate(sim));
        Give(sim, 0, "religion:faith", 10);
        Assert.True(sim.TryApply(train, out _));
        sim.State.PlayerById(1).Techs.Add("religion:mysticism");
        Assert.Equal(0, me.Resources["religion:faith"]);
        Assert.Equal("needs a religion in the city",
            new StartTrainingCommand { Player = 1, CityId = other.Id, Unit = "religion:missionary" }.Validate(sim));

        var missionary = UnitRules.Spawn(sim, 0, "religion:missionary", other.Position);
        Assert.True(sim.TryApply(new SpreadInstitutionCommand { Player = 0, UnitId = missionary.Id, Institution = "religion:sun_cult" }, out var err), err);
        Assert.Contains("religion:sun_cult", other.Institutions);
        // The holy city tithes 1 Faith per other follower city.
        Assert.Equal(5 + 1, Resources.PlayerRate(sim, 0, "religion:faith"));
    }

    // --- guilds ---------------------------------------------------------------------------------

    [Fact]
    public void RenownIsEarnedAndSpentPerCity()
    {
        var sim = Sim();
        var (a, b) = ResourceTests.TwoSites(sim);
        var forge = ResourceTests.Found(sim, 0, a);
        var quiet = ResourceTests.Found(sim, 0, b);
        sim.State.PlayerById(0).Techs.Add("guilds:craftsmanship");
        forge.Buildings.Add("base:workshop");
        forge.Buildings.Add("guilds:guildhall");
        CityRules.Assign(forge, [], new Dictionary<string, int> { ["base:engineer"] = 1 });

        new ResourceSystem().Run(sim);
        Assert.Equal(2 + 1 + 2, forge.Resources["guilds:renown"]); // engineer, workshop, guildhall
        Assert.False(quiet.Resources.ContainsKey("guilds:renown"));
        Assert.Empty(sim.State.PlayerById(0).Resources);

        // Founding draws from the founding city's own pool.
        var found = new FoundInstitutionCommand { Player = 0, CityId = forge.Id, Institution = "guilds:smiths" };
        Assert.Equal("needs 50 Renown (have 5)", found.Validate(sim));
        forge.Resources["guilds:renown"] = 60;
        int productionBefore = Yields.OfCity(sim, forge).Production;
        Assert.True(sim.TryApply(found, out _));
        Assert.Equal(10, forge.Resources["guilds:renown"]);
        Assert.Equal(productionBefore + 1 + 1, Yields.OfCity(sim, forge).Production); // smiths, plus founding city bonus

        // A guild tech's Renown is drawn from all your cities, lowest id first.
        quiet.Resources["guilds:renown"] = 25;
        Assert.True(sim.TryApply(new SetResearchCommand { Player = 0, Tech = "guilds:trade_secrets" }, out var err), err);
        Assert.Equal(0, forge.Resources["guilds:renown"]);
        Assert.Equal(5, quiet.Resources["guilds:renown"]);

        // Huscarls need the Smiths' Guild in the training city.
        sim.State.PlayerById(0).Techs.Add("guilds:trade_secrets");
        Assert.Equal("needs Smiths' Guild in the city", new StartTrainingCommand { Player = 0, CityId = quiet.Id, Unit = "guilds:huscarl" }.Validate(sim));
        Assert.Equal("needs 25 Renown (have 0)", new StartTrainingCommand { Player = 0, CityId = forge.Id, Unit = "guilds:huscarl" }.Validate(sim));
    }

    // --- corporations ---------------------------------------------------------------------------

    [Fact]
    public void BranchesConsumeGoodsAndCapitalAndReturnYields()
    {
        var sim = Sim();
        var (a, b) = ResourceTests.TwoSites(sim);
        var hq = ResourceTests.Found(sim, 0, a);
        var branch = ResourceTests.Found(sim, 0, b);
        sim.State.PlayerById(0).Techs.Add("corporations:joint_stock");
        hq.Buildings.Add("corporations:exchange");
        long grain = Resources.PlayerRate(sim, 0, "corporations:grain");
        long fish = Resources.PlayerRate(sim, 0, "corporations:fish");
        Assert.True(grain > 0, "the test map should give some grain");

        var found = new FoundInstitutionCommand { Player = 0, CityId = hq.Id, Institution = "corporations:granary_trust" };
        Assert.Equal("needs 60 Capital (have 0)", found.Validate(sim));
        Give(sim, 0, "corporations:capital", 60);
        int foodBefore = Yields.OfCity(sim, hq).Food;
        Assert.True(sim.TryApply(found, out _));

        // One branch (the HQ): -1 grain, -1 fish, -1 Capital upkeep, +2 Capital at HQ, +3 from the exchange.
        Assert.Equal(grain - 1, Resources.PlayerRate(sim, 0, "corporations:grain"));
        Assert.Equal(fish - 1, Resources.PlayerRate(sim, 0, "corporations:fish"));
        Assert.Equal(3 - 1 + 2, Resources.PlayerRate(sim, 0, "corporations:capital"));
        if (fish >= 1)
            Assert.Equal(foodBefore + (int)((grain - 1 + fish - 1) * 25 / 100), Yields.OfCity(sim, hq).Food);
        else
            Assert.Equal(foodBefore, Yields.OfCity(sim, hq).Food); // short of fish: the branch stalls

        // Corporations that share a good cannot share a city.
        Assert.Equal("Merchant Fleet cannot share a city with Granary Trust",
            new FoundInstitutionCommand { Player = 0, CityId = hq.Id, Institution = "corporations:merchant_fleet" }.Validate(sim));

        // Executives cost Capital and open more branches, which pay the HQ.
        var exec = UnitRules.Spawn(sim, 0, "corporations:executive", branch.Position);
        Assert.True(sim.TryApply(new SpreadInstitutionCommand { Player = 0, UnitId = exec.Id, Institution = "corporations:granary_trust" }, out var err), err);
        Assert.Equal(3 - 2 + 4, Resources.PlayerRate(sim, 0, "corporations:capital"));
    }

    [Fact]
    public void BranchesStallWhenTheirGoodsRunShort()
    {
        var sim = Sim();
        var hq = ResourceTests.Found(sim, 0, TestGame.FindLand(sim));
        sim.State.PlayerById(0).Techs.Add("corporations:joint_stock");
        hq.Buildings.Add("corporations:exchange");
        Give(sim, 0, "corporations:capital", 60);
        Assert.True(sim.TryApply(new FoundInstitutionCommand { Player = 0, CityId = hq.Id, Institution = "corporations:quarry_combine" }, out _));
        // Work no hills: stone runs short and the branch yields nothing extra.
        foreach (var h in hq.Worked) sim.State.Terrain[h] = "base:grassland";
        sim.State.Terrain[hq.Position] = "base:grassland";
        Assert.Equal(-1, Resources.PlayerRate(sim, 0, "corporations:stone"));
        var withBranch = Yields.OfCity(sim, hq);
        hq.Institutions.Remove("corporations:quarry_combine");
        Assert.Equal(Yields.OfCity(sim, hq), withBranch);
    }

    // --- whole game -----------------------------------------------------------------------------

    [Fact]
    public void AiGamesWithEveryModStayDeterministic()
    {
        ulong Run()
        {
            var sim = new Simulation(Content(), new SimulationSettings
            {
                Seed = 11, PlayerCount = 3, MapRadius = 10, PlayerAis = ["base:basic", "base:basic", "base:basic"],
            });
            for (int i = 0; i < 80; i++) sim.AdvanceTurn();
            Assert.Contains(sim.State.Players, p => p.Resources.Count > 0 || sim.State.Cities.Any(c => c.Owner == p.Id && c.Resources.Count > 0));
            return sim.ComputeHash();
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void CityScreenShowsModResourcesAndWhatCanBeFounded()
    {
        var s = GameSession.Start(ModDiscovery.LoadMods(ModsRoot), mapRadius: 8);
        var settler = s.Sim.State.Units.First(u => u.Owner == GameSession.Human && s.Sim.Content.Units.Get(u.Type).CanFoundCity);
        s.Submit(new FoundCityCommand { Player = GameSession.Human, Position = settler.Position, UnitId = settler.Id });
        var city = s.Sim.State.Cities.First(c => c.Owner == GameSession.Human);
        s.Me.Techs.Add("religion:mysticism");
        city.Buildings.Add("religion:shrine");

        var screen = CityScreen.Of(s.Sim, city);
        var faith = Assert.Single(screen.Resources, r => r.Id == "religion:faith");
        Assert.Equal((false, (long?)null, 2L), (faith.CityScoped, faith.Stored, faith.PerTurn));
        Assert.Equal(["religion:earth_mother", "religion:sun_cult"], screen.Foundable.Select(f => f.Id));
        Assert.All(screen.Foundable, f => Assert.Equal("needs 40 Faith (have 0)", f.Blocked));
        Assert.DoesNotContain(screen.Units, u => u.Id == "religion:missionary"); // needs a religion here first

        s.Me.Resources["religion:faith"] = 40;
        s.FoundInstitution(city, "religion:sun_cult");
        Assert.Equal("", s.Message);
        screen = CityScreen.Of(s.Sim, city);
        Assert.Equal(("religion:sun_cult", true), (screen.Institutions.Single().Id, screen.Institutions.Single().IsHome));
        Assert.Contains(screen.Units, u => u.Id == "religion:missionary" && u.Effects.Contains("costs 15 Faith"));
        Assert.Contains(ResourceView.Player(s.Sim, GameSession.Human), r => r.Id == "religion:faith" && r.PerTurn == 2);
    }
}
