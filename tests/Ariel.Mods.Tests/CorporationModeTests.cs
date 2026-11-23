using Ariel.Content;
using Ariel.Game;
using Ariel.Mods.Corporations;
using Xunit;
using BuildingDef = Ariel.Content.Schema.BuildingDef;
using CivicDef = Ariel.Content.Schema.CivicDef;
using UnitDef = Ariel.Content.Schema.UnitDef;

namespace Ariel.Mods.Tests;

/// <summary>The two ways guilds and corporations work, chosen by the Realistic Guilds/Corporations option: founded and
/// spread on their own (realistic, the default), or by building and by executives (manual).</summary>
public sealed class CorporationModeTests
{
    private const string Cereal = "corporations:1", Carpenters = "corporations:carpenterguild", Masons = "corporations:masonguild";
    private static ICorporationService Corps(Simulation sim) => P6RulesScenario.Service<ICorporationService>(sim);
    private static void Natural(Simulation sim) => sim.Content.TurnSystems.Get("corporations:natural").Run(sim);
    private static IReadOnlyList<string> Refusals(Simulation sim, City city, string kind, string id) =>
        P6RulesScenario.Service<ICityProductionService>(sim).Validate(sim, city.Id, kind, id).Failures.Select(f => f.Explanation).ToList();
    /// <summary>Move the game's random stream on by a seed's worth of draws, so each seed plays out its own luck.</summary>
    private static void Skip(Simulation sim, ulong seed) { for (ulong i = 0; i < seed * 7; i++) sim.Rng.NextUInt64(); }
    private static CorporationRulesDef Rules(Simulation sim) => sim.Content.Get<CorporationRulesDef>().All.Single();

    [Fact]
    public void The_option_is_on_by_default_and_named_for_guilds_and_corporations()
    {
        var sim = GameFactory.Scenario(P6RulesScenario.With(new NothingMod()));
        Assert.True(CorporationMode.Realistic(sim));
        Assert.Equal("Realistic Guilds/Corporations", CorporationMode.OptionName(sim.Content));
        var option = sim.Content.Services.Get<ISetupPolicy>().SelectMany(p => p.GameOptions(sim.Content)).Single(o => o.Id == CorporationMode.Option);
        Assert.Equal("Realistic Guilds/Corporations", option.Name);
        Assert.True(option.Default);
    }

    private sealed class NothingMod : Ariel.Modding.IMod
    {
        public void Register(Ariel.Modding.IModContext context) { }
    }

    [Fact]
    public void Realistic_mode_refuses_founding_buildings_executives_and_paid_spreads()
    {
        var sim = P6RulesScenario.Create(realistic: true); var city = sim.State.Cities[0]; var other = sim.State.Cities[1];
        GameFactory.SatisfyFounding(sim, city, Cereal);
        string headquarters = GameFactory.Headquarters(sim, Cereal);
        Assert.Contains("Founded automatically with Realistic Guilds/Corporations: a nation with Organic Chemistry and Wheat or Corn or Rice or Potatoes connected",
            Refusals(sim, city, "cities:building", headquarters));
        GameFactory.SatisfyFounding(sim, city, Masons);
        Assert.Contains(Refusals(sim, city, "cities:building", GameFactory.Headquarters(sim, Masons)), r => r.StartsWith("Founded automatically", StringComparison.Ordinal));

        // A corporation founded on its own trains no executive in its headquarters, and nobody pays for a branch.
        CorporationSourceRulesTests.Plant(sim, city, Cereal);
        const string Refused = "Executives are not used with Realistic Guilds/Corporations: guilds and corporations spread on their own";
        Assert.Contains(Refused, Refusals(sim, city, "cities:unit", "corporations:executive_1"));
        GameFactory.SatisfyFounding(sim, city, Masons); CorporationSourceRulesTests.Plant(sim, city, Masons);
        Assert.Contains(Refused, Refusals(sim, city, "cities:unit", "corporations:executive_masonguild"));
        Assert.DoesNotContain(Refused, Refusals(sim, city, "cities:unit", "military:warrior"));
        P6RulesScenario.Resource(sim, other, "world:corn");
        Assert.Equal("Guilds and corporations spread on their own with Realistic Guilds/Corporations",
            P6RulesScenario.Reject(sim, new SpreadCorporationCommand { Player = 0, City = other.Id, Corporation = Cereal }));
        var executive = P6RulesScenario.AddUnit(sim, 0, other, "corporations:executive_1");
        Assert.Equal("Guilds and corporations spread on their own with Realistic Guilds/Corporations",
            P6RulesScenario.Reject(sim, new PerformMissionCommand { Player = 0, Unit = executive.Id, Mission = CorporationMissions.Spread, Subject = Cereal }));
    }

    [Fact]
    public void A_master_guild_asks_for_its_technology_and_trade_buildings_and_no_guild_hall()
    {
        var sim = P6RulesScenario.Create(); var city = sim.State.Cities[0];
        string masterGuild = GameFactory.Headquarters(sim, Masons);
        var leaves = ContentReferences.Leaves(sim.Content.Get<BuildingDef>(masterGuild).Requirements!).ToList();
        Assert.Contains(leaves, l => l.Kind == "tech");
        Assert.Contains(leaves, l => l.Kind == "building-class" && l.Id == "cities:treadmill_crane");
        Assert.DoesNotContain(leaves, l => l.Id.Contains("guildhall", StringComparison.Ordinal) || l.Kind == "game-option");
        // Manual mode: the technology, the trade buildings and the stone found it, as a corporation's headquarters.
        GameFactory.SatisfyFounding(sim, city, Masons);
        Assert.DoesNotContain(sim.State.Cities, c => c.Buildings.Any(b => b.Contains("guildhall", StringComparison.Ordinal)));
        Assert.Empty(Refusals(sim, city, "cities:building", masterGuild));
        // Realistic mode: the one refusal says how it is founded.
        sim.Settings.GameOptions[CorporationMode.Option] = true;
        var refusal = Assert.Single(Refusals(sim, city, "cities:building", masterGuild));
        Assert.StartsWith("Founded automatically with Realistic Guilds/Corporations: a nation with ", refusal);
        Assert.EndsWith(" and Stone connected", refusal);
    }

    [Fact]
    public void A_nation_founds_on_its_own_with_the_technology_and_the_resource_and_civics_that_allow_it()
    {
        var sim = P6RulesScenario.Create(realistic: true); var city = sim.State.Cities[0]; var other = sim.State.Cities[1];
        Assert.Null(Corps(sim).NaturalFoundingCity(sim, 0, Cereal));
        GameFactory.GrantFoundingTechs(sim, city, Cereal);
        Assert.Null(Corps(sim).NaturalFoundingCity(sim, 0, Cereal)); // No resource yet.
        P6RulesScenario.Resource(sim, city, "world:corn");
        Assert.Equal(city.Id, Corps(sim).NaturalFoundingCity(sim, 0, Cereal));
        sim.State.PlayerById(0).Civics["test"] = "government:planned";
        Assert.Null(Corps(sim).NaturalFoundingCity(sim, 0, Cereal)); // Its civics ban corporations.
        sim.State.PlayerById(0).Civics.Clear();
        // With as many copies in each, the more populous city is the seat.
        P6RulesScenario.Resource(sim, other, "world:corn");
        other.Population = city.Population + 2;
        Assert.Equal(Corps(sim).ConsumedBonusCount(sim, city.Id, Cereal), Corps(sim).ConsumedBonusCount(sim, other.Id, Cereal));
        Assert.Equal(other.Id, Corps(sim).NaturalFoundingCity(sim, 0, Cereal));
        // The other nation has neither.
        Assert.Null(Corps(sim).NaturalFoundingCity(sim, 1, Cereal));
    }

    [Fact]
    public void A_guild_that_consumes_no_resource_founds_with_the_buildings_of_its_trade_and_no_guild_hall()
    {
        var sim = P6RulesScenario.Create(realistic: true); var city = sim.State.Cities[0];
        var carpenters = sim.Content.Get<CorporationDef>(Carpenters);
        Assert.Empty(carpenters.PrereqBonuses);
        Assert.Equal(4, carpenters.FoundingBuildingClasses["cities:siege_workshop"]);
        GameFactory.GrantFoundingTechs(sim, city, Carpenters);
        Assert.Null(Corps(sim).NaturalFoundingCity(sim, 0, Carpenters));
        GameFactory.SatisfyFounding(sim, city, Carpenters);
        Assert.NotNull(Corps(sim).NaturalFoundingCity(sim, 0, Carpenters));
        var workshop = sim.State.Cities.First(c => c.Owner == 0 && c.Buildings.Contains("cities:siege_workshop"));
        workshop.Buildings.Remove("cities:siege_workshop");
        Assert.Null(Corps(sim).NaturalFoundingCity(sim, 0, Carpenters));
    }

    /// <summary>Two nations that can found Cereal Mills; the natural turn runs until one does.</summary>
    private static (int Turn, int Player, int City) FoundRace(ulong seed)
    {
        var sim = P6RulesScenario.Create(realistic: true);
        Skip(sim, seed);
        foreach (var city in new[] { sim.State.Cities[0], sim.State.Cities[2] })
        {
            GameFactory.GrantFoundingTechs(sim, city, Cereal);
            P6RulesScenario.Resource(sim, city, "world:corn");
        }
        Assert.NotNull(Corps(sim).NaturalFoundingCity(sim, 0, Cereal));
        Assert.NotNull(Corps(sim).NaturalFoundingCity(sim, 1, Cereal));
        for (int turn = 1; turn <= 2000; turn++)
        {
            Natural(sim);
            if (Corps(sim).Headquarters(sim, Cereal) is int hq)
            {
                var founded = Assert.Single(sim.Events.Published.OfType<CorporationFounded>(), e => e.Corporation == Cereal);
                var seat = sim.State.CityById(hq);
                Assert.Equal((founded.City, founded.Player), (hq, seat.Owner));
                // The founding building stands in the seat, as if it had been built there; nobody built anything.
                Assert.Contains(GameFactory.Headquarters(sim, Cereal), seat.Buildings);
                Assert.DoesNotContain(sim.Events.Published.OfType<BuildingCompleted>(), e => e.Building == GameFactory.Headquarters(sim, Cereal));
                Assert.True(Corps(sim).IsHeadquartersOf(sim, hq, sim.Content.Get<CorporationDef>(Cereal).Identity));
                Assert.Null(Corps(sim).NaturalFoundingCity(sim, 0, Cereal));
                return (turn, seat.Owner, hq);
            }
        }
        throw new InvalidOperationException("Cereal Mills was never founded");
    }

    [Fact]
    public void Founding_on_its_own_has_one_founder_and_repeats_for_a_seed()
    {
        var results = Enumerable.Range(1, 8).Select(seed => FoundRace((ulong)seed)).ToArray();
        Assert.Equal(results, Enumerable.Range(1, 8).Select(seed => FoundRace((ulong)seed)).ToArray());
        // The wait is a matter of luck, and either nation may win the race.
        Assert.True(results.Select(r => r.Turn).Distinct().Count() > 1);
        Assert.True(results.Average(r => r.Turn) < Rules(P6RulesScenario.Create()).FoundingMeanTurns * 3);
    }

    [Fact]
    public void Natural_spread_falls_with_distance_and_rises_with_the_resource_supply()
    {
        var sim = P6RulesScenario.Create(realistic: true); var hq = sim.State.Cities[0];
        var near = P6RulesScenario.AddCity(sim, 0, 16); var far = sim.State.Cities[1];
        GameFactory.GrantFoundingTechs(sim, hq, Cereal);
        CorporationSourceRulesTests.Plant(sim, hq, Cereal);
        // No resource, no branch.
        Assert.Equal(0, Corps(sim).NaturalSpreadOdds(sim, near.Id, Cereal).Threshold);
        P6RulesScenario.Resource(sim, near, "world:corn"); P6RulesScenario.Resource(sim, far, "world:corn");
        var nearOdds = Corps(sim).NaturalSpreadOdds(sim, near.Id, Cereal);
        var farOdds = Corps(sim).NaturalSpreadOdds(sim, far.Id, Cereal);
        Assert.True(sim.State.Grid.Distance(hq.Position, near.Position) < sim.State.Grid.Distance(hq.Position, far.Position));
        Assert.True(nearOdds.Threshold > 0);
        Assert.True(nearOdds.Threshold > farOdds.Threshold, $"near {nearOdds.Threshold}, far {farOdds.Threshold}");
        // The draw is out of the source's spread range, scaled to the game's pace.
        Assert.Equal(2500 * GamePace.Percent(sim) / 100, nearOdds.Range);
        // A second copy of the resource in the nation's reach raises the chance.
        var tile = sim.State.Grid.Neighbors(sim.State.Grid.TileAt(near.Position)).First(t => sim.State.CityAt(sim.State.Grid.HexOf(t)) is null);
        var plot = sim.State.Plots[tile.Value];
        plot.Owner = 0; plot.Bonus = "world:corn"; plot.Improvement = "world:improvement_improvement_farm"; plot.Route = "world:road";
        Assert.True(Corps(sim).ConsumedBonusCount(sim, near.Id, Cereal) > 1);
        Assert.True(Corps(sim).NaturalSpreadOdds(sim, near.Id, Cereal).Threshold > nearOdds.Threshold);
        // Civics that ban corporations, or the manual mode's turn, open nothing.
        sim.State.PlayerById(0).Civics["test"] = "government:planned";
        Assert.Equal(0, Corps(sim).NaturalSpreadOdds(sim, near.Id, Cereal).Threshold);
        sim.State.PlayerById(0).Civics.Clear();
    }

    private static int SpreadTurn(ulong seed)
    {
        var sim = P6RulesScenario.Create(realistic: true); var hq = sim.State.Cities[0];
        Skip(sim, seed);
        var near = P6RulesScenario.AddCity(sim, 0, 16);
        CorporationSourceRulesTests.Plant(sim, hq, Cereal);
        P6RulesScenario.Resource(sim, near, "world:corn");
        for (int turn = 1; turn <= 5000; turn++)
        {
            Natural(sim);
            if (Corps(sim).Present(sim, near.Id).Contains(Cereal))
            {
                Assert.Contains(sim.Events.Published.OfType<CorporationSpread>(), e => e.Corporation == Cereal && e.City == near.Id && e.Player == 0);
                return turn;
            }
        }
        throw new InvalidOperationException("Cereal Mills never spread");
    }

    [Fact]
    public void A_branch_opens_on_its_own_and_repeats_for_a_seed()
    {
        int first = SpreadTurn(7);
        Assert.Equal(first, SpreadTurn(7));
        // The manual mode never opens one by itself.
        var sim = P6RulesScenario.Create(); var hq = sim.State.Cities[0];
        var near = P6RulesScenario.AddCity(sim, 0, 16);
        CorporationSourceRulesTests.Plant(sim, hq, Cereal);
        P6RulesScenario.Resource(sim, near, "world:corn");
        for (int turn = 0; turn < first + 50; turn++) Natural(sim);
        Assert.DoesNotContain(Cereal, Corps(sim).Present(sim, near.Id));
    }

    [Fact]
    public void A_branch_without_its_resource_closes_and_a_headquarters_without_access_dissolves_and_founds_anew_elsewhere()
    {
        var sim = P6RulesScenario.Create(realistic: true); var hq = sim.State.Cities[0]; var branch = sim.State.Cities[1]; var rival = sim.State.Cities[2];
        GameFactory.GrantFoundingTechs(sim, hq, Cereal);
        CorporationSourceRulesTests.Plant(sim, hq, Cereal, branch);
        hq.Buildings.Add(GameFactory.Headquarters(sim, Cereal));
        var rules = Rules(sim);

        // The branch loses its corn: it waits out the grace, then closes. The headquarters keeps its own.
        sim.State.PlotAt(branch.Position).Bonus = null;
        Assert.False(Corps(sim).IsActive(sim, branch.Id, Cereal));
        var events = new List<DomainEvent>();
        void Turn() { sim.AdvanceTurn(); events.AddRange(sim.Events.PreviousTurn); events.AddRange(sim.Events.Published); }
        int turns = 0;
        while (Corps(sim).Present(sim, branch.Id).Contains(Cereal) && turns < 50) { Turn(); turns++; }
        Assert.InRange(turns, GamePace.Scale(sim, rules.BranchDecayTurns), GamePace.Scale(sim, rules.BranchDecayTurns) + 2);
        Assert.Contains(events.OfType<CorporationWithdrawn>(), e => e.Corporation == Cereal && e.City == branch.Id);
        Assert.Equal(hq.Id, Corps(sim).Headquarters(sim, Cereal));

        // Corn that comes back in time stops the clock.
        sim.State.PlotAt(hq.Position).Bonus = null;
        Turn(); Turn();
        P6RulesScenario.Resource(sim, hq, "world:corn");
        for (int i = 0; i < rules.HeadquartersDecayTurns + 2; i++) Turn();
        Assert.Equal(hq.Id, Corps(sim).Headquarters(sim, Cereal));

        // The nation loses the corn everywhere for long enough: the corporation dissolves with its seat.
        sim.State.PlotAt(hq.Position).Bonus = null;
        turns = 0;
        while (Corps(sim).IsFounded(sim, Cereal) && turns < 50) { Turn(); turns++; }
        Assert.InRange(turns, GamePace.Scale(sim, rules.HeadquartersDecayTurns), GamePace.Scale(sim, rules.HeadquartersDecayTurns) + 2);
        Assert.Contains(events.OfType<CorporationDissolved>(), e => e.Corporation == Cereal && e.City == hq.Id);
        Assert.DoesNotContain(GameFactory.Headquarters(sim, Cereal), hq.Buildings);
        // The nation that lost it waits out a cooldown even with the corn back; another nation may found it at once.
        P6RulesScenario.Resource(sim, hq, "world:corn");
        Assert.Null(Corps(sim).NaturalFoundingCity(sim, 0, Cereal));
        GameFactory.GrantFoundingTechs(sim, rival, Cereal);
        P6RulesScenario.Resource(sim, rival, "world:corn");
        Assert.Equal(rival.Id, Corps(sim).NaturalFoundingCity(sim, 1, Cereal));
        sim.TeamOf(0).Techs.Remove("research:organic_chemistry"); // Leave the race to the other nation.
        for (int i = 0; i < 2000 && !Corps(sim).IsFounded(sim, Cereal); i++) Natural(sim);
        Assert.Equal(rival.Id, Corps(sim).Headquarters(sim, Cereal));
    }

    [Fact]
    public void Manual_mode_never_dissolves_for_a_lost_resource_the_branch_just_stops_working()
    {
        var sim = P6RulesScenario.Create(); var hq = sim.State.Cities[0]; var branch = sim.State.Cities[1];
        GameFactory.SatisfyFounding(sim, hq, Cereal);
        GameFactory.FoundCorporation(sim, hq, Cereal);
        P6RulesScenario.Resource(sim, branch, "world:corn");
        P6RulesScenario.Apply(sim, new SpreadCorporationCommand { Player = 0, City = branch.Id, Corporation = Cereal });
        sim.State.PlotAt(hq.Position).Bonus = null; sim.State.PlotAt(branch.Position).Bonus = null;
        for (int i = 0; i < 40; i++) sim.AdvanceTurn();
        Assert.Equal(hq.Id, Corps(sim).Headquarters(sim, Cereal));
        Assert.Contains(Cereal, Corps(sim).Present(sim, branch.Id));
        Assert.False(Corps(sim).IsActive(sim, branch.Id, Cereal));
        Assert.Equal(0, Corps(sim).MaintenanceHundredths(sim, branch.Id));
        Assert.DoesNotContain(sim.Events.Published, e => e is CorporationDissolved or CorporationWithdrawn);
        // The resource back, it works again.
        P6RulesScenario.Resource(sim, branch, "world:corn");
        Assert.True(Corps(sim).IsActive(sim, branch.Id, Cereal));
    }

    /// <summary>The civic valuer's adjustment of a civic for player 0.</summary>
    private static int Adjust(Simulation sim, string civic)
    {
        var valuer = sim.Content.Services.Get<Ariel.Ai.IAiValuer<Ariel.Ai.CivicSubject>>().Single(v => v.Id == "corporations:civic_valuer");
        return valuer.Adjust((Ariel.Ai.AiContext)AiBindings.NewAi(sim, "AiContext", sim, 0), new Ariel.Ai.CivicSubject(sim.Content.Get<CivicDef>(civic)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_ban_costs_the_corporations_a_nation_could_still_get(bool realistic)
    {
        var sim = P6RulesScenario.Create(realistic: realistic); var city = sim.State.Cities[0];
        // Nothing to found and nothing to receive: a ban is free.
        Assert.Equal(0, Adjust(sim, "government:planned"));
        GameFactory.GrantFoundingTechs(sim, city, Cereal);
        P6RulesScenario.Resource(sim, city, "world:corn");
        // Cereal Mills could be founded now, by building it or on its own: banning it costs.
        int founding = Adjust(sim, "government:planned");
        Assert.True(founding < 0, $"planned scores {founding}");
        // A civic that bans only foreign corporations leaves one the player would found itself.
        Assert.Equal(0, Adjust(sim, "government:nationalist"));
        // Founded abroad, the corporation could still come by spread, which a foreign ban stops.
        var foreign = sim.State.Cities[2];
        GameFactory.GrantFoundingTechs(sim, foreign, Cereal);
        CorporationSourceRulesTests.Plant(sim, foreign, Cereal);
        Assert.True(Adjust(sim, "government:nationalist") < 0);
    }

    [Fact]
    public void Every_corporation_and_guild_has_what_both_modes_need()
    {
        var sim = P6RulesScenario.Create();
        var content = sim.Content;
        var corporations = content.Get<CorporationDef>().All.ToList();
        Assert.Equal(36, corporations.Count);
        foreach (var def in corporations)
        {
            // Manual: one founding building with a technology, and a unit that spreads it.
            var building = content.Get<BuildingDef>(GameFactory.Headquarters(sim, def.Id));
            Assert.NotEmpty(GameFactory.FoundingTechs(sim, def.Id));
            Assert.True(building.Cost > 0, $"{def.Id}: its founding building cannot be built");
            Assert.Contains(content.Get<UnitDef>().All, u => (content.Attachment<UnitCorporationsDef>(u.Id)?.CorporationSpreads.GetValueOrDefault(def.Identity) ?? 0) > 0);
            // Realistic: a spread factor, and something besides the technology to gate founding.
            Assert.True(def.NaturalSpreadPercent > 0, $"{def.Id}: never spreads on its own");
            Assert.True(def.PrereqBonuses.Length > 0 ^ def.FoundingBuildingClasses.Count > 0, $"{def.Id}: founding needs a resource or the buildings of its trade, not both");
            // A guild that consumes nothing founds on the trade buildings its Master Guild asks for, without the hall.
            foreach (var (buildingClass, count) in def.FoundingBuildingClasses)
            {
                var leaf = Assert.Single(ContentReferences.Leaves(building.Requirements!), l => l.Kind == "building-class" && l.Id == buildingClass);
                Assert.Equal(count, leaf.Count);
            }
        }
        // Guilds are corporations of older eras: each is spread by its own executive alone, and no Guild Hall or guild
        // master is left.
        foreach (var gone in new[] { "cities:cguildhall", "cities:sguildhall", "cities:vguildhall", "military:bgmaster", "military:ggmaster", "military:wgmaster" })
            Assert.Null(content.Find(gone));
        foreach (var unit in content.Get<UnitDef>().All)
            Assert.True((content.Attachment<UnitCorporationsDef>(unit.Id)?.CorporationSpreads.Count(p => p.Value > 0) ?? 0) <= 1, $"{unit.Id} spreads several");
        // The four guilds that consume no resource are the ones with buildings to found on.
        Assert.Equal(["corporations:apotheguild", "corporations:artistguild", "corporations:carpenterguild", "corporations:clerksguild"],
            corporations.Where(d => d.FoundingBuildingClasses.Count > 0).Select(d => d.Id).Order(StringComparer.Ordinal));
    }
}
