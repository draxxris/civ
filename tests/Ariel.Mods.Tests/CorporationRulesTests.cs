using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;
using Ariel.Mods.Corporations;
using Xunit;

namespace Ariel.Mods.Tests;

internal static class P6RulesScenario
{
    private static readonly Lazy<ContentDb> Content = new(() => BuildContent());
    private static readonly Lazy<ContentDb> HalfMaintenanceContent = new(() => BuildContent(new HalfMaintenanceMod()));
    private static ContentDb BuildContent(IMod? extra = null)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        string configuration = dir.Parent!.Name;
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Ariel.slnx"))) dir = dir.Parent;
        var names = new[] { "Corporations", "Espionage" };
        var sources = names.Select(name => ModLoader.FromDirectory(Path.Combine(dir!.FullName, "src", "Mods", name, "bin", configuration, "net10.0"))).ToArray();
        var loaded = new LoadedMods();
        var all = loaded.Sources.Where(s => !sources.Any(n => n.Manifest.Id == s.Manifest.Id)).Concat(sources);
        if (extra is not null) all = all.Append(new ModSource { Manifest = new() { Id = "probe", Version = "1", References = ["cities", "military", "foundation"] }, Root = loaded.Root, Instance = extra });
        return ModLoader.Build(all);
    }
    /// <summary>The scenario's content with one more mod, built fresh.</summary>
    public static ContentDb With(IMod extra) => BuildContent(extra);
    private sealed class HalfMaintenanceMod : IMod
    {
        public void Register(IModContext context) => context.Content<Ariel.Content.Schema.BuildingDef>().Override(new()
        {
            Id = "cities:courthouse", Name = "Courthouse", DefaultBuilding = "cities:courthouse", MaintenanceModifier = -50,
        });
    }
    public static T Service<T>(Simulation sim) where T : class => sim.Content.Services.Get<T>().Single();
    public static Simulation Create(bool halfMaintenance = false)
    {
        var sim = GameFactory.Scenario(halfMaintenance ? HalfMaintenanceContent.Value : Content.Value);
        GameFactory.NeutralPolitics(sim);
        foreach (var plot in sim.State.Plots) plot.Owner = null;
        AddCity(sim, 0, 14); AddCity(sim, 0, 18); AddCity(sim, 1, 57);
        foreach (var player in sim.State.Players) player.Treasury = 10000;
        sim.TeamOf(0).Techs.Add("research:currency");
        sim.Rules().Relations.EstablishContact(sim, 0, 1);
        return sim;
    }
    public static City AddCity(Simulation sim, int owner, int tile)
    {
        var city = new City { Id = sim.State.NextCityId++, Owner = owner, Position = sim.State.Grid.HexOf(new TileId(tile)) };
        sim.State.AddCity(city); sim.State.PlotAt(city.Position).Owner = owner; return city;
    }
    public static Unit AddUnit(Simulation sim, int owner, City city, string type)
    {
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = owner, Position = city.Position, Type = type, MovesLeft = 60 };
        sim.State.AddUnit(unit); return unit;
    }
    public static void Resource(Simulation sim, City city, string bonus)
    {
        sim.State.PlotAt(city.Position).Bonus = bonus;
        var def = sim.Content.Get<Ariel.Content.Schema.BonusDef>(bonus);
        if (def.RevealTech is { } reveal) sim.TeamOf(city.Owner).Techs.Add(reveal);
        if (def.TradeTech is { } trade) sim.TeamOf(city.Owner).Techs.Add(trade);
    }
    public static void Apply(Simulation sim, GameCommand command) => Assert.True(sim.TryApply(command, out var error), error);
    public static string Reject(Simulation sim, GameCommand command)
    {
        ulong hash = sim.ComputeHash();
        Assert.False(sim.TryApply(command, out var error));
        Assert.Equal(hash, sim.ComputeHash()); return error!;
    }
}

public sealed class CorporationRulesTests
{
    private const string Cereal = "corporations:1", Ethanol = "corporations:3", Masons = "corporations:masonguild";
    private static ICorporationService Corps(Simulation sim) => P6RulesScenario.Service<ICorporationService>(sim);
    private static void Found(Simulation sim, City city, string corporation = Cereal)
    {
        GameFactory.GrantFoundingTechs(sim, city, corporation);
        P6RulesScenario.Resource(sim, city, "world:corn");
        Assert.True(Production(sim).Validate(sim, city.Id, "cities:building", GameFactory.Headquarters(sim, corporation)).Success);
        GameFactory.FoundCorporation(sim, city, corporation);
    }
    private static ICityProductionService Production(Simulation sim) => P6RulesScenario.Service<ICityProductionService>(sim);
    private static IReadOnlyList<string> Refusals(Simulation sim, City city, string building) =>
        Production(sim).Validate(sim, city.Id, "cities:building", building).Failures.Select(f => f.Explanation).ToList();
    private static void RunCityPhase(Simulation sim, string phase, int player) =>
        ((IPlayerTurnSystem)sim.Content.TurnSystems.Get("cities:" + phase)).Run(sim, player);
    /// <summary>Queue the building in the city with its whole cost already paid, as an order about to finish.</summary>
    private static void QueueFinished(Simulation sim, City city, string building)
    {
        P6RulesScenario.Apply(sim, new SetCityProductionCommand { Player = city.Owner, CityId = city.Id, Kind = "cities:building", ContentId = building });
        city.ProductionQueue[0] = city.ProductionQueue[0] with { Progress = Production(sim).Calculate(sim, city.Id, "cities:building", building).Cost };
    }
    [Fact]
    public void The_source_corporations_and_guilds_are_all_defined_each_with_one_imported_founding_building()
    {
        var sim = P6RulesScenario.Create();
        var corporations = sim.Content.Get<CorporationDef>().All;
        // Every corporation identity the base game names has its rules: 21 corporations and 15 guilds.
        Assert.Equal(sim.Content.Get<Ariel.Content.Schema.CorporationIdentityDef>().All.Select(i => i.Id).Order(),
            corporations.Select(c => c.Identity).Order());
        Assert.Equal(21, corporations.Count(c => c.Kind == CorporationKind.Corporation));
        Assert.Equal(15, corporations.Count(c => c.Kind == CorporationKind.Guild));
        foreach (var corporation in corporations)
        {
            // The founding building is the imported headquarters or guild hall: world-unique, priced, and gated by a
            // technology (a guild's also by the buildings of its trade).
            var building = sim.Content.Get<Ariel.Content.Schema.BuildingDef>(GameFactory.Headquarters(sim, corporation.Id));
            Assert.StartsWith("cities:corporation_", building.Id);
            Assert.Equal(1, building.Limits.World);
            Assert.True(building.Cost > 0, building.Id);
            Assert.NotEmpty(GameFactory.FoundingTechs(sim, corporation.Id));
            if (corporation.Kind == CorporationKind.Guild)
                Assert.Contains(ContentReferences.Leaves(building.Requirements!), l => l.Kind == "building-class");
        }
        // The source's prices: guild halls 300; headquarters, which it left to a great person, by era.
        Assert.Equal(300, sim.Content.Get<Ariel.Content.Schema.BuildingDef>(GameFactory.Headquarters(sim, Masons)).Cost);
        Assert.Equal(700, sim.Content.Get<Ariel.Content.Schema.BuildingDef>(GameFactory.Headquarters(sim, Cereal)).Cost);
        Assert.Equal(1300, sim.Content.Get<Ariel.Content.Schema.BuildingDef>(GameFactory.Headquarters(sim, "corporations:7")).Cost);
    }

    /// <summary>Each corporation and guild in a game of its own (two in one game could compete for a resource): its
    /// founding building's requirements met in a city, then built through the real production phases.</summary>
    public static TheoryData<string> AllCorporations()
    {
        var sim = P6RulesScenario.Create();
        var data = new TheoryData<string>();
        foreach (var corporation in sim.Content.Get<CorporationDef>().All.OrderBy(c => c.Id, StringComparer.Ordinal)) data.Add(corporation.Id);
        return data;
    }
    [Theory]
    [MemberData(nameof(AllCorporations))]
    public void Every_corporation_and_guild_is_founded_by_building_its_headquarters(string corporation)
    {
        var sim = P6RulesScenario.Create(); var city = sim.State.Cities[0];
        string building = GameFactory.Headquarters(sim, corporation);
        Assert.Contains(Refusals(sim, city, building), r => r.StartsWith("Required tech:", StringComparison.Ordinal));
        GameFactory.SatisfyFounding(sim, city, corporation);
        Assert.Empty(Refusals(sim, city, building));
        QueueFinished(sim, city, building);
        RunCityPhase(sim, "production", 0);
        RunCityPhase(sim, "completion", 0);
        Assert.Contains(building, city.Buildings);
        Assert.Equal(city.Id, Corps(sim).Headquarters(sim, corporation));
        Assert.Equal([corporation], Corps(sim).Present(sim, city.Id));
        Assert.Contains("Corporation is already founded", Refusals(sim, sim.State.Cities[1], building));
    }
    [Fact]
    public void The_headquarters_is_unavailable_before_the_technology_and_needs_a_connected_resource()
    {
        var sim = P6RulesScenario.Create(); var city = sim.State.Cities[0];
        string headquarters = GameFactory.Headquarters(sim, Cereal);
        Assert.Contains("Required tech: research:organic_chemistry", Refusals(sim, city, headquarters));
        sim.TeamOf(0).Techs.Add("research:organic_chemistry");
        Assert.Equal(["Requires a connected corporation resource"], Refusals(sim, city, headquarters));
        P6RulesScenario.Resource(sim, city, "world:corn");
        Assert.Empty(Refusals(sim, city, headquarters));
        // Knowing the technology founds nothing by itself.
        Assert.False(Corps(sim).IsFounded(sim, Cereal));
    }
    [Fact]
    public void Completing_the_headquarters_founds_the_corporation_there_and_a_second_founder_is_refused()
    {
        var sim = P6RulesScenario.Create(); var city = sim.State.Cities[1]; var rival = sim.State.Cities[2];
        string headquarters = GameFactory.Headquarters(sim, Cereal);
        foreach (var (player, at) in new[] { (0, city), (1, rival) })
        {
            sim.TeamOf(player).Techs.Add("research:organic_chemistry");
            P6RulesScenario.Resource(sim, at, "world:corn");
            QueueFinished(sim, at, headquarters);
        }

        RunCityPhase(sim, "production", 0);
        RunCityPhase(sim, "completion", 0);

        Assert.Contains(headquarters, city.Buildings);
        Assert.Equal(city.Id, Corps(sim).Headquarters(sim, Cereal));
        Assert.Equal([Cereal], Corps(sim).Present(sim, city.Id));
        Assert.Empty(Corps(sim).Present(sim, sim.State.Cities[0].Id));
        var founded = Assert.Single(sim.Events.Published.OfType<CorporationFounded>());
        Assert.Equal((Cereal, city.Id, 0), (founded.Corporation, founded.City, founded.Player));

        // The rival's copy can no longer be built: its order drops at completion and nothing is founded twice.
        Assert.Contains("Corporation is already founded", Refusals(sim, rival, headquarters));
        RunCityPhase(sim, "production", 1);
        RunCityPhase(sim, "completion", 1);
        Assert.DoesNotContain(rival.ProductionQueue, o => o.ContentId == headquarters);
        Assert.DoesNotContain(headquarters, rival.Buildings);
        Assert.Single(sim.Events.Published.OfType<CorporationFounded>());
        Assert.Equal(city.Id, Corps(sim).Headquarters(sim, Cereal));
    }
    [Fact]
    public void Barbarians_cannot_found_a_corporation()
    {
        var sim = P6RulesScenario.Create(); var city = sim.State.Cities[2];
        GameFactory.MakeBarbarian(sim, 1);
        sim.TeamOf(1).Techs.Add("research:organic_chemistry");
        P6RulesScenario.Resource(sim, city, "world:corn");
        Assert.Contains("Barbarians cannot found anything", Refusals(sim, city, GameFactory.Headquarters(sim, Cereal)));
    }
    [Fact]
    public void The_civic_valuer_weighs_corporation_rules_by_the_branches_in_the_players_cities()
    {
        var sim = P6RulesScenario.Create(); var hq = sim.State.Cities[0];
        var valuer = sim.Content.Services.Get<Ariel.Ai.IAiValuer<Ariel.Ai.CivicSubject>>().Single(v => v.Id == "corporations:civic_valuer");
        int Adjust(string civic) => valuer.Adjust((Ariel.Ai.AiContext)AiBindings.NewAi(sim, "AiContext", sim, 0),
            new Ariel.Ai.CivicSubject(sim.Content.Get<Ariel.Content.Schema.CivicDef>(civic)));
        // Before any corporation operates in its cities, corporation rules mean nothing to the player.
        Assert.Equal(0, Adjust("government:planned"));
        Assert.Equal(0, Adjust("government:corporatist"));
        Found(sim, hq);
        // A ban loses the branch's food (whole points: three quarters of a food per copy, one copy) and saves its
        // maintenance, in planner points.
        long maintenance = P6RulesScenario.Service<ICorporationService>(sim).MaintenanceHundredths(sim, hq.Id);
        var cereal = sim.Content.Get<CorporationDef>(Cereal);
        long food = cereal.YieldProducedHundredths.Food * Corps(sim).ConsumedBonusCount(sim, hq.Id, Cereal) / 100;
        Assert.Equal(-food * Ariel.Ai.PlannerPoints.Food + maintenance * Ariel.Ai.PlannerPoints.Gold / 100, Adjust("government:planned"));
        Assert.True(Adjust("government:corporatist") > 0, "a civic that cuts maintenance gains");
        Assert.Equal(0, Adjust("government:nationalist")); // bans only foreign corporations; this one is the player's own
        Assert.Equal(0, Adjust("government:chiefdom"));
    }
    [Fact]
    public void Spreading_charges_gold_consumes_executives_and_counts_worldwide_branches()
    {
        var sim = P6RulesScenario.Create(); var hq = sim.State.Cities[0]; var domestic = sim.State.Cities[1]; var foreign = sim.State.Cities[2];
        Found(sim, hq); P6RulesScenario.Resource(sim, domestic, "world:corn"); P6RulesScenario.Resource(sim, foreign, "world:corn");
        long initial = sim.State.PlayerById(0).Treasury;
        var spread = new SpreadCorporationCommand { Player = 0, City = domestic.Id, Corporation = Cereal };
        long cost = sim.Content.Get<CorporationDef>(Cereal).SpreadCost;
        Assert.Equal(50, cost);
        sim.State.PlayerById(0).Treasury = cost - 1;
        Assert.Contains("gold", P6RulesScenario.Reject(sim, spread)); sim.State.PlayerById(0).Treasury = initial;
        P6RulesScenario.Apply(sim, spread);
        Assert.Contains("executive", P6RulesScenario.Reject(sim, new SpreadCorporationCommand { Player = 0, City = foreign.Id, Corporation = Cereal }));
        var executive = P6RulesScenario.AddUnit(sim, 0, foreign, "military:executive_1");
        P6RulesScenario.Apply(sim, new PerformMissionCommand { Player = 0, Unit = executive.Id, Mission = CorporationMissions.Spread, Subject = Cereal });
        Assert.Null(sim.State.FindUnit(executive.Id)); Assert.Equal(initial - 2 * cost, sim.State.PlayerById(0).Treasury);
        var effects = ModifierSink.Collect(new(sim, ModifierScope.City, 0, hq));
        Assert.Equal(12, effects.Ledger.Lines.Where(l => l.Source == Cereal + ":headquarters" && l.Key == "commerce.flat.gold").Sum(l => l.Amount));
        Assert.True(Corps(sim).IsActive(sim, domestic.Id, Cereal));
    }
    [Fact]
    public void A_guild_is_founded_by_its_guild_hall_after_the_buildings_of_its_trade_and_spreads_like_a_corporation()
    {
        var sim = P6RulesScenario.Create(); var city = sim.State.Cities[0]; var branch = sim.State.Cities[1]; var rival = sim.State.Cities[2];
        string hall = GameFactory.Headquarters(sim, Masons);
        Assert.Equal("cities:corporation_masonguild", hall);
        // The technology alone is not enough: the guild hall also wants the buildings of its trade across the empire.
        GameFactory.GrantFoundingTechs(sim, city, Masons);
        P6RulesScenario.Resource(sim, city, "world:stone");
        Assert.Contains(Refusals(sim, city, hall), r => r.Contains("empire buildings of class: cities:treadmill_crane", StringComparison.Ordinal));
        GameFactory.SatisfyFounding(sim, city, Masons);
        Assert.Empty(Refusals(sim, city, hall));
        QueueFinished(sim, city, hall);
        RunCityPhase(sim, "production", 0);
        RunCityPhase(sim, "completion", 0);
        Assert.Equal(city.Id, Corps(sim).Headquarters(sim, Masons));
        var founded = Assert.Single(sim.Events.Published.OfType<CorporationFounded>());
        Assert.Equal(Masons, founded.Corporation);

        // One founder only, world-wide.
        GameFactory.SatisfyFounding(sim, rival, Masons);
        Assert.Contains("Corporation is already founded", Refusals(sim, rival, hall));

        // A branch, paid for at home, consumes stone as a corporation's branch consumes its resource.
        P6RulesScenario.Resource(sim, branch, "world:stone");
        P6RulesScenario.Apply(sim, new SpreadCorporationCommand { Player = 0, City = branch.Id, Corporation = Masons });
        Assert.Contains(Masons, Corps(sim).Present(sim, branch.Id));
        var masons = sim.Content.Get<CorporationDef>(Masons);
        var effects = ModifierSink.Collect(new(sim, ModifierScope.City, 0, branch)).Ledger.Lines.Where(l => l.Source == Masons).ToList();
        // One stone: a whole production per copy and the branch's own production.
        Assert.Equal(masons.YieldProducedHundredths.Production / 100 + masons.YieldChanges.Production,
            effects.Where(l => l.Key == "yield.flat.production").Sum(l => l.Amount));

        // The guild's own executive trains in its guild hall; the crafts master, which spreads five guilds, trains
        // where the crafts guild hall stands, and both open branches abroad.
        const string Executive = "military:executive_masonguild";
        Assert.Empty(UnitRefusals(sim, city, Executive));
        Assert.Equal(["Requires the guild hall of Masons Guild"], UnitRefusals(sim, branch, Executive));
        P6RulesScenario.Resource(sim, rival, "world:stone");
        var master = P6RulesScenario.AddUnit(sim, 0, rival, "military:bgmaster");
        P6RulesScenario.Apply(sim, new PerformMissionCommand { Player = 0, Unit = master.Id, Mission = CorporationMissions.Spread, Subject = Masons });
        Assert.Contains(Masons, Corps(sim).Present(sim, rival.Id));
        sim.TeamOf(0).Techs.Add("research:juris_prudence");
        var crafts = sim.State.Cities.First(c => c.Owner == 0 && c.Buildings.Contains("cities:cguildhall"));
        Assert.Empty(UnitRefusals(sim, crafts, "military:bgmaster"));
    }

    [Fact]
    public void A_great_person_that_lists_a_headquarters_does_not_construct_it_where_production_may_build_it()
    {
        var sim = P6RulesScenario.Create(); var city = sim.State.Cities[0];
        string headquarters = GameFactory.Headquarters(sim, Cereal);
        Assert.True(sim.Content.Get<Ariel.Content.Schema.UnitDef>("military:merchant").ConstructibleBuildings.GetValueOrDefault(headquarters));
        GameFactory.SatisfyFounding(sim, city, Cereal);
        Assert.Empty(Refusals(sim, city, headquarters));
        var merchant = P6RulesScenario.AddUnit(sim, 0, city, "military:merchant");
        sim.Rules().Units.Initialize(sim, merchant.Id);
        Assert.False(sim.TryApply(new PerformMissionCommand
        {
            Player = 0, Unit = merchant.Id, Mission = Ariel.Mods.GreatPeople.GreatPersonMissions.Construct, Subject = headquarters,
        }, out _));
        Assert.DoesNotContain(headquarters, city.Buildings);
        Assert.False(Corps(sim).IsFounded(sim, Cereal));
    }

    [Fact]
    public void A_guild_that_consumes_no_resource_is_founded_and_spread_without_one()
    {
        const string Carpenters = "corporations:carpenterguild";
        var sim = P6RulesScenario.Create(); var city = sim.State.Cities[0]; var branch = sim.State.Cities[1];
        var carpenters = sim.Content.Get<CorporationDef>(Carpenters);
        Assert.Empty(carpenters.PrereqBonuses);
        GameFactory.SatisfyFounding(sim, city, Carpenters);
        Assert.Null(sim.State.PlotAt(city.Position).Bonus);
        Assert.Empty(Refusals(sim, city, GameFactory.Headquarters(sim, Carpenters)));
        GameFactory.FoundCorporation(sim, city, Carpenters);
        P6RulesScenario.Apply(sim, new SpreadCorporationCommand { Player = 0, City = branch.Id, Corporation = Carpenters });
        // Its branches give their flat production and gold whatever the city holds.
        var lines = ModifierSink.Collect(new(sim, ModifierScope.City, 0, branch)).Ledger.Lines.Where(l => l.Source == Carpenters).ToList();
        Assert.Equal(carpenters.YieldChanges.Production, lines.Where(l => l.Key == "yield.flat.production").Sum(l => l.Amount));
        Assert.Equal(carpenters.CommerceChanges.Gold, lines.Where(l => l.Key == "commerce.flat.gold").Sum(l => l.Amount));
        Assert.True(carpenters.YieldChanges.Production > 0);
    }
    [Fact]
    public void An_executive_trained_in_the_headquarters_it_was_built_for_spreads_into_a_foreign_city()
    {
        var sim = P6RulesScenario.Create(); var branch = sim.State.Cities[0]; var hq = sim.State.Cities[1]; var foreign = sim.State.Cities[2];
        const string Executive = "military:executive_1";
        sim.TeamOf(0).Techs.Add("research:organic_chemistry");
        foreach (var city in sim.State.Cities) P6RulesScenario.Resource(sim, city, "world:corn");
        Assert.Contains(HeadquartersRefusal, UnitRefusals(sim, hq, Executive));
        Assert.Contains(BranchRefusal, UnitRefusals(sim, hq, Executive));

        // Found the corporation by building its headquarters, as production finishes it.
        QueueFinished(sim, hq, GameFactory.Headquarters(sim, Cereal));
        RunCityPhase(sim, "production", 0);
        RunCityPhase(sim, "completion", 0);
        Assert.Equal(hq.Id, Corps(sim).Headquarters(sim, Cereal));

        // The headquarters city trains the executive; a city with only a branch does not, as in the reference rules.
        P6RulesScenario.Apply(sim, new SpreadCorporationCommand { Player = 0, City = branch.Id, Corporation = Cereal });
        Assert.Empty(UnitRefusals(sim, hq, Executive));
        // The branch is there, so only the headquarters is missing, and the reason says so.
        Assert.Equal([HeadquartersRefusal], UnitRefusals(sim, branch, Executive));
        // The city screen offers it there, and only there, and shows the branch city why.
        var session = new Ariel.Client.GameSession(sim);
        Assert.True(Assert.Single(session.ProductionChoices(hq, "cities:unit"), c => c.Id == Executive).Available);
        Assert.Equal([HeadquartersRefusal], Assert.Single(session.ProductionChoices(branch, "cities:unit"), c => c.Id == Executive).Blockers);

        P6RulesScenario.Apply(sim, new SetCityProductionCommand { Player = 0, CityId = hq.Id, Kind = "cities:unit", ContentId = Executive });
        hq.ProductionQueue[0] = hq.ProductionQueue[0] with { Progress = Production(sim).Calculate(sim, hq.Id, "cities:unit", Executive).Cost };
        RunCityPhase(sim, "production", 0);
        RunCityPhase(sim, "completion", 0);
        var executive = Assert.Single(sim.State.Units, u => u.Owner == 0 && u.Type == Executive);
        Assert.Equal(hq.Position, executive.Position);

        // Walking it there is movement's business; the spread itself is what an executive is for.
        sim.State.MoveUnit(executive.Id, foreign.Position); executive.MovesLeft = 60;
        P6RulesScenario.Apply(sim, new PerformMissionCommand { Player = 0, Unit = executive.Id, Mission = CorporationMissions.Spread, Subject = Cereal });
        Assert.Contains(Cereal, Corps(sim).Present(sim, foreign.Id));
        Assert.Null(sim.State.FindUnit(executive.Id));
    }
    [Fact]
    public void An_executive_whose_training_needs_a_building_no_city_can_build_fails_the_load()
    {
        // The shape the executives had while the imported headquarters were not buildable: no city could train one.
        var error = Assert.Throws<InvalidDataException>(() => P6RulesScenario.With(new UntrainableExecutiveMod()));
        Assert.Contains("military:executive_1: spreads foundation:1 but can never be trained: it needs building 'cities:corporation_1'", error.Message);
        Assert.Contains("cities:corporation_1: founds foundation:1 but has a negative cost, so no city can build it", error.Message);
    }
    private sealed class UntrainableExecutiveMod : IMod
    {
        public void Register(IModContext context)
        {
            // The headquarters as the source left it, for a great person only.
            context.Content<Ariel.Content.Schema.BuildingDef>().Override(new()
            {
                Id = "cities:corporation_1", Name = "Cereal Mills", DefaultBuilding = "cities:corporation_1", Cost = -1, FoundsCorporation = "foundation:1",
            });
            context.Content<Ariel.Content.Schema.UnitDef>().Override(new()
            {
                Id = "military:executive_1", Name = "Executive", DefaultUnit = "military:executive_1", Cost = 100,
                Requirements = new Requirement.All([new Requirement.Leaf("building", "cities:corporation_1"), new Requirement.Leaf("corporation", "foundation:1")]),
            });
        }
    }
    [Fact]
    public void A_corporation_identity_with_no_rules_fails_the_load_where_a_building_or_unit_names_it()
    {
        // An identity only the base game knows would leave its headquarters and executives inert, as 29 were before
        // the corporation rules were imported.
        var error = Assert.Throws<InvalidDataException>(() => P6RulesScenario.With(new OrphanCorporationMod()));
        Assert.Contains("probe:orphan_hq: founds 'probe:orphan', which no corporation of this mod has as its identity", error.Message);
        Assert.Contains("military:warrior: spreads 'probe:orphan', which no corporation of this mod has as its identity", error.Message);
    }
    private sealed class OrphanCorporationMod : IMod
    {
        public void Register(IModContext context)
        {
            context.Content<Ariel.Content.Schema.CorporationIdentityDef>().Register(new() { Id = "probe:orphan", Name = "Orphan" });
            context.Content<Ariel.Content.Schema.BuildingDef>().Register(new()
            {
                Id = "probe:orphan_hq", Name = "Orphan Headquarters", DefaultBuilding = "probe:orphan_hq", Cost = 100,
                FoundsCorporation = "probe:orphan",
            });
            context.Content<UnitCorporationsDef>().Register(new()
            {
                Id = "probe:orphan_spread", Target = "military:warrior",
                CorporationSpreads = System.Collections.Immutable.ImmutableDictionary<string, int>.Empty.Add("probe:orphan", 40),
            });
        }
    }
    private static IReadOnlyList<string> UnitRefusals(Simulation sim, City city, string unit) =>
        Production(sim).Validate(sim, city.Id, "cities:unit", unit).Failures.Select(f => f.Explanation).ToList();
    private const string HeadquartersRefusal = "Requires the headquarters of Cereal Mills", BranchRefusal = "Requires a branch of Cereal Mills";
    [Fact]
    public void A_corporation_leaf_explains_itself_by_its_parameter_and_the_corporations_name()
    {
        var sim = P6RulesScenario.Create(); var city = sim.State.Cities[0];
        var requirements = sim.Content.Requirements;
        string? Explain(int? param) => requirements.Evaluate(new Requirement.Leaf("corporation", "foundation:1", Param: param),
            new RequirementContext(sim, 0, city)).Failures.Single().Explanation;
        Assert.Equal(BranchRefusal, Explain(null));
        Assert.Equal(BranchRefusal, Explain(0));
        Assert.Equal(HeadquartersRefusal, Explain(2));
        // A parameter the leaf does not define keeps the generic reason.
        Assert.Equal("Required corporation: foundation:1", Explain(1));
        Assert.Equal("the headquarters of Cereal Mills", requirements.Describe(new("corporation", "foundation:1", Param: 2), sim.Content));
        // A guild's seat is its guild hall, by the guild's kind; its branches read as a corporation's do.
        Assert.Equal("the guild hall of Masons Guild", requirements.Describe(new("corporation", "foundation:masonguild", Param: 2), sim.Content));
        Assert.Equal("a branch of Masons Guild", requirements.Describe(new("corporation", "foundation:masonguild"), sim.Content));

        // The pedia lists the same phrases among an executive's prerequisites.
        var entry = new Ariel.Client.GameSession(sim).Pedia.Find("military:executive_1")!;
        Assert.Contains("the headquarters of Cereal Mills", entry.Prerequisites);
        Assert.Contains("a branch of Cereal Mills", entry.Prerequisites);
    }
    [Fact]
    public void Queries_answer_a_stale_city_id_instead_of_throwing()
    {
        var sim = P6RulesScenario.Create(); var corporations = Corps(sim);
        Assert.Empty(corporations.Present(sim, 9999));
        Assert.False(corporations.IsActive(sim, 9999, Cereal));
        Assert.Equal(0, corporations.ConsumedBonusCount(sim, 9999, Cereal));
    }
    [Fact]
    public void Corporation_gated_buildings_require_a_present_branch()
    {
        var sim = P6RulesScenario.Create(); var hq = sim.State.Cities[0];
        var production = P6RulesScenario.Service<ICityProductionService>(sim);
        static bool CorporationFailure(RequirementResult result) =>
            result.Failures.Any(f => f.Leaf?.Kind == "corporation");
        Assert.True(CorporationFailure(production.Validate(sim, hq.Id, "cities:building", "cities:cereal_mills_factory")));
        Found(sim, hq);
        Assert.False(CorporationFailure(production.Validate(sim, hq.Id, "cities:building", "cities:cereal_mills_factory")));
    }
    [Fact]
    public void Competing_branches_are_displaced_but_headquarters_are_protected()
    {
        var sim = P6RulesScenario.Create(); var hq = sim.State.Cities[0]; var branch = sim.State.Cities[1]; var otherHq = sim.State.Cities[2];
        Found(sim, hq); Found(sim, otherHq, Ethanol);
        P6RulesScenario.Resource(sim, branch, "world:corn");
        P6RulesScenario.Apply(sim, new SpreadCorporationCommand { Player = 0, City = branch.Id, Corporation = Cereal });
        var unit = P6RulesScenario.AddUnit(sim, 1, branch, "military:executive_3");
        P6RulesScenario.Apply(sim, new PerformMissionCommand { Player = 1, Unit = unit.Id, Mission = CorporationMissions.Spread, Subject = Ethanol });
        Assert.DoesNotContain(Cereal, Corps(sim).Present(sim, branch.Id)); Assert.Contains(Ethanol, Corps(sim).Present(sim, branch.Id));
        // Displacement is silent in the rules, so it must at least be reported.
        Assert.Contains(sim.Events.Published.OfType<CorporationDisplaced>(),
            e => e.Corporation == Cereal && e.By == Ethanol && e.City == branch.Id);
        Assert.Equal(2, P6RulesScenario.Service<IPlayerEconomyService>(sim).CalculateCity(sim, branch.Id).Ledger.Lines.Single(l => l.Source == Ethanol && l.Key == "commerce.flat.research").Amount);
        var attacker = P6RulesScenario.AddUnit(sim, 1, hq, "military:executive_3");
        Assert.Contains("headquarters", P6RulesScenario.Reject(sim, new PerformMissionCommand { Player = 1, Unit = attacker.Id, Mission = CorporationMissions.Spread, Subject = Ethanol }));
    }
    [Fact]
    public void Resource_copies_scale_output_and_maintenance_and_civics_suppress_without_erasing_presence()
    {
        var sim = P6RulesScenario.Create(); var hq = sim.State.Cities[0]; Found(sim, hq);
        var tile = sim.State.Grid.Neighbors(sim.State.Grid.TileAt(hq.Position)).First();
        var second = sim.State.Plots[tile.Value]; second.Owner = 0; second.Bonus = "world:corn"; second.Improvement = "world:improvement_improvement_farm"; second.Route = "world:road";
        Assert.Equal(2, Corps(sim).ConsumedBonusCount(sim, hq.Id, Cereal));
        // Three quarters of a food per copy: two copies make one and a half, of which the city gets the whole one.
        Assert.Equal(75, sim.Content.Get<CorporationDef>(Cereal).YieldProducedHundredths.Food);
        Assert.Equal(1, ModifierSink.Collect(new(sim, ModifierScope.City, 0, hq)).Ledger.Lines.Single(l => l.Source == Cereal && l.Key == "yield.flat.food").Amount);
        Assert.Equal(750, Corps(sim).MaintenanceHundredths(sim, hq.Id)); // (400 headquarters + 200 resources) * 100/80.
        sim.State.PlayerById(0).Civics["test"] = "government:guilds";
        Assert.Equal(-105, ModifierSink.Collect(new(sim, ModifierScope.Player, 0)).Ledger.Total("corporations:maintenance_percent"));
        Assert.Equal(0, Corps(sim).MaintenanceHundredths(sim, hq.Id));
        sim.State.PlayerById(0).Civics["test"] = "government:planned";
        Assert.False(Corps(sim).IsActive(sim, hq.Id, Cereal)); Assert.Equal(0, Corps(sim).MaintenanceHundredths(sim, hq.Id));
        Assert.Contains(Cereal, Corps(sim).Present(sim, hq.Id));
        Assert.DoesNotContain(ModifierSink.Collect(new(sim, ModifierScope.City, 0, hq)).Ledger.Lines, l => l.Source == Cereal);
        sim.State.PlayerById(0).Civics.Clear(); Assert.True(Corps(sim).IsActive(sim, hq.Id, Cereal));
        second.Bonus = null; sim.State.PlotAt(hq.Position).Bonus = null;
        Assert.Equal(500, Corps(sim).MaintenanceHundredths(sim, hq.Id)); // Headquarters commerce remains in the base without resources.
    }
    [Fact]
    public void Foreign_corporation_civics_block_spread_and_suspend_existing_foreign_branches()
    {
        var sim = P6RulesScenario.Create(); var hq = sim.State.Cities[0]; var foreign = sim.State.Cities[2]; Found(sim, hq);
        P6RulesScenario.Resource(sim, foreign, "world:corn");
        var executive = P6RulesScenario.AddUnit(sim, 0, foreign, "military:executive_1");
        var command = new PerformMissionCommand { Player = 0, Unit = executive.Id, Mission = CorporationMissions.Spread, Subject = Cereal };
        sim.State.PlayerById(1).Civics["test"] = "government:mercantilism";
        Assert.Contains("Civics", P6RulesScenario.Reject(sim, command));
        sim.State.PlayerById(1).Civics.Clear(); P6RulesScenario.Apply(sim, command);
        sim.State.PlayerById(1).Civics["test"] = "government:mercantilism";
        Assert.False(Corps(sim).IsActive(sim, foreign.Id, Cereal)); Assert.Equal(0, Corps(sim).MaintenanceHundredths(sim, foreign.Id));
        Assert.Contains(Cereal, Corps(sim).Present(sim, foreign.Id)); Assert.True(Corps(sim).IsActive(sim, hq.Id, Cereal));
    }
    [Fact]
    public void Captured_headquarters_moves_the_mercantilism_boundary_to_the_new_owner()
    {
        var sim = P6RulesScenario.Create(); var hq = sim.State.Cities[0]; var branch = sim.State.Cities[1];
        hq.Population = 3; // The headquarters must survive conquest instead of auto-razing.
        Found(sim, hq);
        P6RulesScenario.Resource(sim, branch, "world:corn");
        P6RulesScenario.Apply(sim, new SpreadCorporationCommand { Player = 0, City = branch.Id, Corporation = Cereal });
        sim.State.PlayerById(0).Civics["test"] = "government:mercantilism";
        Assert.True(Corps(sim).IsActive(sim, branch.Id, Cereal)); // The branch shares a team with the headquarters.
        sim.State.SetWar(0, 1, true);
        P6RulesScenario.Service<ICityCaptureService>(sim).Capture(sim, hq.Id, 1);
        Assert.False(Corps(sim).IsActive(sim, branch.Id, Cereal)); // The headquarters now belongs to team 1.
        Assert.Equal(0, Corps(sim).MaintenanceHundredths(sim, branch.Id));
        Assert.Contains(Cereal, Corps(sim).Present(sim, branch.Id));
        Assert.Equal(hq.Id, Corps(sim).Headquarters(sim, Cereal));
        Assert.True(Corps(sim).IsActive(sim, hq.Id, Cereal)); // Domestic to the new owner, which has no mercantilism.
    }
    [Theory]
    [InlineData(0)]
    [InlineData(10000)]
    public void Courthouse_reduces_corporate_costs_before_economy_adjusts_sliders_and_settles_once(long treasury)
    {
        var sim = P6RulesScenario.Create(halfMaintenance: true); var branch = sim.State.Cities[1]; var hq = sim.State.Cities[2];
        sim.State.RemoveCity(sim.State.Cities[0].Id);
        foreach (var city in sim.State.Cities) city.Extensions.Set(sim.Content, new CityRuleState { ManualWork = true });
        Found(sim, hq); P6RulesScenario.Resource(sim, branch, "world:corn");
        var tile = sim.State.Grid.Neighbors(sim.State.Grid.TileAt(branch.Position)).First();
        var extra = sim.State.Plots[tile.Value]; extra.Owner = 0; extra.Bonus = "world:corn";
        extra.Improvement = "world:improvement_improvement_farm"; extra.Route = "world:road";
        var executive = P6RulesScenario.AddUnit(sim, 1, branch, "military:executive_1");
        P6RulesScenario.Apply(sim, new PerformMissionCommand { Player = 1, Unit = executive.Id, Mission = CorporationMissions.Spread, Subject = Cereal });
        var maintenance = P6RulesScenario.Service<ICityMaintenanceService>(sim);
        var economy = P6RulesScenario.Service<IPlayerEconomyService>(sim);
        branch.Buildings.Add("cities:palace"); // Enough commerce to cover the full corporate formula at zero treasury.
        long corporateCost = Corps(sim).MaintenanceHundredths(sim, branch.Id);
        Assert.Equal(750, corporateCost);
        var full = maintenance.Calculate(sim, branch.Id);
        Assert.Equal(corporateCost, full.CorporateHundredths);
        Assert.Equal(full.DistanceHundredths + full.CityCountHundredths + corporateCost, full.TotalHundredths);
        branch.Buildings.Add("cities:courthouse");
        var reduced = maintenance.Calculate(sim, branch.Id);
        Assert.Equal(full.TotalHundredths / 2, reduced.TotalHundredths);
        Assert.Equal(reduced.TotalHundredths, reduced.Ledger.Total("cities:maintenance_hundredths"));
        economy.SetRates(sim, 0, new(0, 100, 0, 0));
        var before = economy.Calculate(sim, 0);
        Assert.Equal(reduced.TotalHundredths, before.Ledger.Total("economy:maintenance_hundredths"));
        Assert.Equal(corporateCost, before.Ledger.Total("economy:corporate_maintenance_hundredths"));
        Assert.Equal(7, before.Ledger.Total("economy:corporate_maintenance"));
        Assert.Equal(3, before.Expenses);
        var owner = sim.State.PlayerById(0); owner.Treasury = treasury;
        sim.AdvanceTurn();
        var settled = owner.Extensions.Get<EconomyPlayerState>(sim.Content)!;
        Assert.Equal(treasury + settled.SettledCommerceHundredths.Gold / 100 - before.Expenses, owner.Treasury);
        Assert.Equal(0, settled.UnpaidGold);
        if (treasury == 0) Assert.True(economy.Rates(sim, 0).Gold > 0);
        else Assert.Equal(new CommerceVector(0, 100, 0, 0), economy.Rates(sim, 0));
    }
    [Fact]
    public void Razing_the_headquarters_dissolves_every_branch_and_allows_refounding()
    {
        var sim = P6RulesScenario.Create(); var hq = sim.State.Cities[0]; var branch = sim.State.Cities[1];
        Found(sim, hq); P6RulesScenario.Resource(sim, branch, "world:corn");
        P6RulesScenario.Apply(sim, new SpreadCorporationCommand { Player = 0, City = branch.Id, Corporation = Cereal });
        Assert.Contains(Cereal, Corps(sim).Present(sim, branch.Id));
        Assert.True(Corps(sim).MaintenanceHundredths(sim, branch.Id) > 0);
        P6RulesScenario.Service<ICityCaptureService>(sim).Raze(sim, hq.Id);
        Assert.False(Corps(sim).IsFounded(sim, Cereal));
        Assert.DoesNotContain(Cereal, Corps(sim).Present(sim, branch.Id));
        Assert.Equal(0, Corps(sim).MaintenanceHundredths(sim, branch.Id));
        Assert.Contains(sim.Events.Published.OfType<CorporationDissolved>(), e => e.Corporation == Cereal && e.City == hq.Id);
        Found(sim, branch);
        Assert.Equal(branch.Id, Corps(sim).Headquarters(sim, Cereal));
        Assert.True(Corps(sim).IsFounded(sim, Cereal));
    }

    [Fact]
    public void Maintenance_is_settled_once_with_economy_rounding_and_founding_records_survive_save_load()
    {
        var sim = P6RulesScenario.Create(); var hq = sim.State.Cities[0]; Found(sim, hq);
        sim.State.PlayerById(0).Civics["test"] = "government:corporatist";
        foreach (var city in sim.State.Cities) city.Extensions.Set(sim.Content, new CityRuleState { ManualWork = true });
        Assert.Equal(156, Corps(sim).MaintenanceHundredths(sim, hq.Id)); // (400 + 100) * 25/100 * 100/80.
        long net = P6RulesScenario.Service<IPlayerEconomyService>(sim).Calculate(sim, 0).NetGold;
        long gold = sim.State.PlayerById(0).Treasury;
        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim)); Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        sim.AdvanceTurn(); loaded.AdvanceTurn();
        Assert.Equal(gold + net, sim.State.PlayerById(0).Treasury); // Economy rounds the combined city costs, not each corporation.
        Assert.Null(sim.State.PlayerById(0).Extensions.Get<CorporationPlayerState>(sim.Content));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        net = P6RulesScenario.Service<IPlayerEconomyService>(sim).Calculate(sim, 0).NetGold; gold = sim.State.PlayerById(0).Treasury;
        sim.AdvanceTurn(); loaded.AdvanceTurn(); Assert.Equal(gold + net, sim.State.PlayerById(0).Treasury);
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        P6RulesScenario.Service<ICityCaptureService>(sim).Raze(sim, hq.Id);
        Assert.Null(Corps(sim).Headquarters(sim, Cereal)); Assert.False(Corps(sim).IsFounded(sim, Cereal));
        var rival = sim.State.Cities.Last();
        Found(sim, rival);
        Assert.Equal(rival.Id, Corps(sim).Headquarters(sim, Cereal));
    }
}
