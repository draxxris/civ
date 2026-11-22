using Ariel.Content;
using Ariel.Game;
using Ariel.Mods.Corporations;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>The source's corporation and guild rules beyond output and maintenance: explicit competition, happiness and
/// health, military production, obsolescence, building-class prerequisites, spread cost and chance, and produced
/// resources. One focused test per rule.</summary>
public sealed class CorporationSourceRulesTests
{
    private const string Carpenters = "corporations:carpenterguild", Masons = "corporations:masonguild";
    private static ICorporationService Corps(Simulation sim) => P6RulesScenario.Service<ICorporationService>(sim);

    [Fact]
    public void Guilds_of_one_group_compete_without_sharing_a_resource()
    {
        var sim = P6RulesScenario.Create(); var carpentersHq = sim.State.Cities[0]; var masonsHq = sim.State.Cities[1];
        var carpenters = sim.Content.Get<CorporationDef>(Carpenters); var masons = sim.Content.Get<CorporationDef>(Masons);
        // The Carpenters consume nothing, so only the explicit list makes them rivals of the Masons.
        Assert.Empty(carpenters.PrereqBonuses.Intersect(masons.PrereqBonuses));
        Assert.Contains(masons.Identity, carpenters.CompetingCorporations);
        GameFactory.SatisfyFounding(sim, carpentersHq, Carpenters);
        GameFactory.FoundCorporation(sim, carpentersHq, Carpenters);
        GameFactory.SatisfyFounding(sim, masonsHq, Masons);
        // Nobody founds a competitor in a headquarters city.
        GameFactory.SatisfyFounding(sim, carpentersHq, Masons);
        Assert.Contains("Cannot displace competing headquarters", Refusals(sim, carpentersHq, GameFactory.Headquarters(sim, Masons)));
        GameFactory.FoundCorporation(sim, masonsHq, Masons);

        var contested = P6RulesScenario.AddCity(sim, 0, 40);
        P6RulesScenario.Resource(sim, contested, "world:stone");
        P6RulesScenario.Apply(sim, new SpreadCorporationCommand { Player = 0, City = contested.Id, Corporation = Carpenters });
        P6RulesScenario.Apply(sim, new SpreadCorporationCommand { Player = 0, City = contested.Id, Corporation = Masons });
        // The newcomer evicts its rival's branch and says so.
        Assert.Equal([Masons], Corps(sim).Present(sim, contested.Id));
        Assert.Contains(sim.Events.Published.OfType<CorporationDisplaced>(), e => e.Corporation == Carpenters && e.By == Masons && e.City == contested.Id);
        // A rival's headquarters is never displaced.
        Assert.Contains("headquarters", P6RulesScenario.Reject(sim, new SpreadCorporationCommand { Player = 0, City = masonsHq.Id, Corporation = Carpenters }));
        // The encyclopedia names the rivals.
        var entry = new Ariel.Client.GameSession(sim).Pedia.Find(Carpenters)!;
        Assert.Contains(entry.Prerequisites, n => n.StartsWith("Competes with ", StringComparison.Ordinal) && n.Contains("Masons Guild", StringComparison.Ordinal));
    }

    /// <summary>Found the corporation in <paramref name="headquarters"/> and open its branch in each city, by state, for
    /// a rule that only reads what a branch does once it is there.</summary>
    internal static void Plant(Simulation sim, City headquarters, string corporation, params City[] branches)
    {
        var game = sim.State.Extensions.Get<CorporationGameState>(sim.Content) ?? new();
        game.Founded[corporation] = new(headquarters.Id, sim.TeamOf(headquarters.Owner).Id, sim.State.Turn);
        sim.State.Extensions.Set(sim.Content, game);
        foreach (var city in branches.Prepend(headquarters))
        {
            var state = city.Extensions.Get<CorporationCityState>(sim.Content) ?? new();
            state.Corporations.Add(corporation);
            city.Extensions.Set(sim.Content, state);
        }
    }

    [Fact]
    public void A_branch_brings_its_happiness_and_health_into_the_city_ledger()
    {
        const string Wonka = "corporations:wonka";
        var sim = P6RulesScenario.Create(); var city = sim.State.Cities[0];
        var wonka = sim.Content.Get<CorporationDef>(Wonka);
        Assert.Equal((3, -1), (wonka.Happiness, wonka.Health));
        var wellbeing = P6RulesScenario.Service<ICityWellbeingService>(sim);
        Assert.DoesNotContain(wellbeing.Calculate(sim, city.Id).Ledger.Lines, l => l.Source == Wonka);
        Plant(sim, city, Wonka);
        var lines = wellbeing.Calculate(sim, city.Id).Ledger.Lines.Where(l => l.Source == Wonka).ToList();
        Assert.Contains(lines, l => l.Key == "cities:happiness_good" && l.Amount == 3);
        Assert.Contains(lines, l => l.Key == "cities:health_bad" && l.Amount == 1);
        // Civics that suspend the branch suspend its happiness and health with its output.
        sim.State.PlayerById(0).Civics["test"] = "government:planned";
        Assert.DoesNotContain(wellbeing.Calculate(sim, city.Id).Ledger.Lines, l => l.Source == Wonka);
        // The encyclopedia says what a branch does.
        sim.State.PlayerById(0).Civics.Clear();
        var effects = new Ariel.Client.GameSession(sim).Pedia.Find(Wonka)!.Effects;
        Assert.Contains("+3 happiness in each city with a branch", effects);
        Assert.Contains("-1 health in each city with a branch", effects);
    }

    [Fact]
    public void A_branch_raises_military_unit_production_by_its_percent_and_nothing_else()
    {
        const string Soldiers = "corporations:ultsoldier";
        var sim = P6RulesScenario.Create(); var city = sim.State.Cities[0];
        Assert.Equal(15, sim.Content.Get<CorporationDef>(Soldiers).MilitaryProductionPercent);
        var production = P6RulesScenario.Service<ICityProductionService>(sim);
        int Modifier(string unit) => production.Calculate(sim, city.Id, "cities:unit", unit).ModifierPercent;
        int warrior = Modifier("military:warrior"), worker = Modifier("military:worker");
        Plant(sim, city, Soldiers);
        Assert.Equal(warrior + 15, Modifier("military:warrior"));
        Assert.Equal(worker, Modifier("military:worker"));
    }

    private static void Learn(Simulation sim, int team, string tech)
    {
        sim.State.Teams.Single(t => t.Id == team).Techs.Add(tech);
        sim.Events.Publish(new TechDiscovered(team, tech));
    }

    [Fact]
    public void A_guild_goes_obsolete_with_its_technology_for_the_team_that_learns_it()
    {
        var sim = P6RulesScenario.Create(); var hq = sim.State.Cities[0]; var branch = sim.State.Cities[1]; var foreign = sim.State.Cities[2];
        var masons = sim.Content.Get<CorporationDef>(Masons);
        Assert.Equal("research:corporation", masons.ObsoleteTech);
        string hall = GameFactory.Headquarters(sim, Masons);
        hq.Buildings.Add(hall);
        Plant(sim, hq, Masons, branch, foreign);
        Assert.True(Corps(sim).IsActive(sim, foreign.Id, Masons));

        // Knowing the technology suspends the guild in that team's cities at once, before any cleanup.
        sim.TeamOf(1).Techs.Add("research:corporation");
        Assert.False(Corps(sim).IsActive(sim, foreign.Id, Masons));
        Assert.DoesNotContain(ModifierSink.Collect(new(sim, ModifierScope.City, 1, foreign)).Ledger.Lines, l => l.Source == Masons);
        sim.TeamOf(1).Techs.Remove("research:corporation");

        // Learning it closes the team's branches; the other team's branch stays and works.
        Learn(sim, sim.TeamOf(1).Id, "research:corporation");
        Assert.DoesNotContain(Masons, Corps(sim).Present(sim, foreign.Id));
        Assert.Contains(sim.Events.Published.OfType<CorporationObsolete>(), e => e.Corporation == Masons && e.City == foreign.Id);
        Assert.True(Corps(sim).IsActive(sim, branch.Id, Masons));
        // Nothing opens in, or is spread by, a team that knows it.
        var master = P6RulesScenario.AddUnit(sim, 0, foreign, "military:bgmaster");
        Assert.Contains("obsolete for this city's owner",
            P6RulesScenario.Reject(sim, new PerformMissionCommand { Player = 0, Unit = master.Id, Mission = CorporationMissions.Spread, Subject = Masons }));

        // When the headquarters' team learns it, the Master Guild comes down and the guild dissolves; a team that does not
        // know the technology may found it again, but one that does may not.
        Learn(sim, sim.TeamOf(0).Id, "research:corporation");
        Assert.False(Corps(sim).IsFounded(sim, Masons));
        Assert.DoesNotContain(hall, hq.Buildings);
        Assert.Empty(Corps(sim).Present(sim, branch.Id));
        Assert.Contains(sim.Events.Published.OfType<CorporationDissolved>(), e => e.Corporation == Masons && e.City == hq.Id);
        GameFactory.SatisfyFounding(sim, hq, Masons);
        Assert.Contains(Refusals(sim, hq, hall), r => r.Contains("obsolete for your team", StringComparison.Ordinal));
        Assert.Contains("Obsolete with Corporation", new Ariel.Client.GameSession(sim).Pedia.Find(Masons)!.Effects);
    }

    [Fact]
    public void A_guild_branch_needs_its_group_hall_in_the_owners_empire()
    {
        var sim = P6RulesScenario.Create(); var hq = sim.State.Cities[0]; var foreign = sim.State.Cities[2];
        Assert.Equal(1, sim.Content.Get<CorporationDef>(Masons).PrereqBuildingClasses["cities:cguildhall"]);
        Plant(sim, hq, Masons);
        P6RulesScenario.Resource(sim, foreign, "world:stone");
        var master = P6RulesScenario.AddUnit(sim, 0, foreign, "military:bgmaster");
        var spread = new PerformMissionCommand { Player = 0, Unit = master.Id, Mission = CorporationMissions.Spread, Subject = Masons };
        // The spreader's halls do not count: the city's owner needs one.
        sim.State.Cities[1].Buildings.Add("cities:cguildhall");
        Assert.Equal("Requires 1 Crafts Guild Hall in the city owner's empire", P6RulesScenario.Reject(sim, spread));
        // Anywhere in the owner's empire will do.
        var other = P6RulesScenario.AddCity(sim, 1, 70);
        other.Buildings.Add("cities:cguildhall");
        P6RulesScenario.Apply(sim, spread);
        Assert.Contains(Masons, Corps(sim).Present(sim, foreign.Id));
    }

    private const string Cereal = "corporations:1", Ethanol = "corporations:3";

    [Fact]
    public void Spread_cost_carries_the_foreign_percent_and_each_active_competitors_factor()
    {
        var sim = P6RulesScenario.Create(); var hq = sim.State.Cities[0]; var own = sim.State.Cities[1]; var rivalHq = sim.State.Cities[2];
        var foreign = P6RulesScenario.AddCity(sim, 1, 70);
        Plant(sim, hq, Cereal); Plant(sim, rivalHq, Ethanol, foreign);
        foreach (var city in new[] { own, foreign }) P6RulesScenario.Resource(sim, city, "world:corn");
        var cereal = sim.Content.Get<CorporationDef>(Cereal);
        Assert.Equal((50, 200), (cereal.SpreadCost, sim.Content.Get<CorporationDef>(Ethanol).SpreadFactorPercent));
        Assert.Equal(50, Corps(sim).SpreadCost(sim, 0, own.Id, Cereal));
        // Another team's city: the foreign percent (200); with an active rival there, three times that again.
        Assert.Equal(50 * 2 * 3, Corps(sim).SpreadCost(sim, 0, foreign.Id, Cereal));
        // A suspended rival adds nothing.
        sim.State.PlayerById(1).Civics["test"] = "government:planned";
        Assert.Equal(50 * 2, Corps(sim).SpreadCost(sim, 0, foreign.Id, Cereal));
        sim.State.PlayerById(1).Civics.Clear();

        // The paid spread charges the same price, and the executive's terms show it.
        long before = sim.State.PlayerById(0).Treasury;
        P6RulesScenario.Apply(sim, new SpreadCorporationCommand { Player = 0, City = own.Id, Corporation = Cereal });
        Assert.Equal(before - 50, sim.State.PlayerById(0).Treasury);
        var executive = P6RulesScenario.AddUnit(sim, 0, foreign, "military:executive_1");
        var terms = P6RulesScenario.Service<IMissionService>(sim).Terms(sim, 0, executive.Id, CorporationMissions.Spread, Cereal)!;
        Assert.StartsWith($"{50 * 2 * 3} gold", terms.Note);
    }

    [Fact]
    public void An_executive_spreads_with_the_source_chance_and_pays_whether_or_not_it_takes()
    {
        var sim = P6RulesScenario.Create(); var hq = sim.State.Cities[0]; var own = sim.State.Cities[1]; var foreign = sim.State.Cities[2];
        Plant(sim, hq, Cereal);
        foreach (var city in new[] { own, foreign }) P6RulesScenario.Resource(sim, city, "world:corn");
        var missions = P6RulesScenario.Service<IMissionService>(sim);
        int total = sim.Content.Get<CorporationDef>().Count;
        int? Chance(City city, out Unit unit)
        {
            unit = P6RulesScenario.AddUnit(sim, 0, city, "military:executive_1");
            return missions.Terms(sim, 0, unit.Id, CorporationMissions.Spread, Cereal)!.Chance;
        }
        // The executive's 40, raised by the share of corporations the city lacks: certain in an empty own city.
        Assert.Equal(40 + total * 60 / total, Chance(own, out var first));
        sim.State.RemoveUnit(first.Id);
        // In another team's city crowded with every other corporation: halved, then raised by one share in 36.
        var crowded = foreign.Extensions.Get<CorporationCityState>(sim.Content) ?? new();
        foreign.Extensions.Set(sim.Content, crowded);
        void Crowd()
        {
            crowded.Corporations.Clear();
            foreach (var other in sim.Content.Get<CorporationDef>().All.Where(c => c.Id != Cereal)) crowded.Corporations.Add(other.Id);
        }
        Crowd();
        int expected = 20 + (total - (total - 1)) * 80 / total;
        bool failed = false, succeeded = false;
        for (int attempt = 0; attempt < 64 && !(failed && succeeded); attempt++)
        {
            Assert.Equal(expected, Chance(foreign, out var unit));
            Assert.Contains($"{expected}% chance", missions.Terms(sim, 0, unit.Id, CorporationMissions.Spread, Cereal)!.Summary);
            // The roll is the simulation's next draw; terms and validation draw nothing.
            var (s0, s1, s2, s3) = sim.Rng.State;
            bool takes = DeterministicRng.Restore(s0, s1, s2, s3).Next(100) < expected;
            long gold = sim.State.PlayerById(0).Treasury, cost = Corps(sim).SpreadCost(sim, 0, foreign.Id, Cereal);
            P6RulesScenario.Apply(sim, new PerformMissionCommand { Player = 0, Unit = unit.Id, Mission = CorporationMissions.Spread, Subject = Cereal });
            Assert.Equal(takes, Corps(sim).Present(sim, foreign.Id).Contains(Cereal));
            Assert.Equal(gold - cost, sim.State.PlayerById(0).Treasury);
            Assert.Null(sim.State.FindUnit(unit.Id));
            Assert.Contains(sim.Events.Published.OfType<MissionResolved>(), e => e.Unit == unit.Id && e.Success == takes);
            if (takes) succeeded = true; else failed = true;
            Crowd();
        }
        Assert.True(failed && succeeded, "the fixed seed rolls both outcomes within 64 attempts");
    }

    [Fact]
    public void A_branch_produces_its_resource_for_its_city_only()
    {
        const string MoonBean = "corporations:moonbean", Cannabis = "world:cannabis";
        var sim = P6RulesScenario.Create(); var city = sim.State.Cities[0]; var other = sim.State.Cities[1];
        Assert.Equal(Cannabis, sim.Content.Get<CorporationDef>(MoonBean).BonusProduced);
        P6RulesScenario.Resource(sim, city, "world:sugar");
        Plant(sim, city, MoonBean);
        // Not before the team can trade the produced resource.
        sim.TeamOf(0).Techs.Remove("research:calendar");
        Assert.Equal(0, ResourceAccess.Count(sim, 0, city.Id, Cannabis));
        sim.TeamOf(0).Techs.Add("research:calendar");

        // One copy per consumed copy: one sugar, one cannabis, which the city uses as a connected copy...
        Assert.Equal(1, ResourceAccess.Count(sim, 0, city.Id, Cannabis));
        var happy = P6RulesScenario.Service<ICityWellbeingService>(sim).Calculate(sim, city.Id).Ledger.Lines;
        Assert.Contains(happy, l => l.Source == Cannabis && l.Key == "cities:happiness_good");
        // ...but it is not on the trade network: not another city's, not tradeable.
        Assert.Equal(0, ResourceAccess.TradeCount(sim, 0, city.Id, Cannabis));
        Assert.Equal(0, ResourceAccess.Count(sim, 0, other.Id, Cannabis));
        // A suspended branch produces nothing.
        sim.State.PlayerById(0).Civics["test"] = "government:planned";
        Assert.Equal(0, ResourceAccess.Count(sim, 0, city.Id, Cannabis));
        sim.State.PlayerById(0).Civics.Clear();
        Assert.Contains("Produces Hemp in each city with a branch, one per resource consumed",
            new Ariel.Client.GameSession(sim).Pedia.Find(MoonBean)!.Effects);
    }

    [Fact]
    public void A_produced_resource_feeds_another_branch_that_consumes_it()
    {
        const string Tours = "corporations:adventure", Equestrians = "corporations:equestguild";
        var sim = P6RulesScenario.Create(); var city = sim.State.Cities[0];
        var tours = sim.Content.Get<CorporationDef>(Tours); var equestrians = sim.Content.Get<CorporationDef>(Equestrians);
        Assert.Contains("world:horse", tours.PrereqBonuses);
        Assert.False(CorporationsCompete(tours, equestrians));
        P6RulesScenario.Resource(sim, city, "world:wheat");
        sim.TeamOf(0).Techs.Add("research:animal_husbandry");
        Plant(sim, city, Tours);
        Assert.Equal(0, Corps(sim).ConsumedBonusCount(sim, city.Id, Tours));
        // The Equestrians turn the wheat into a horse, which Adventure Tours consumes.
        Plant(sim, sim.State.Cities[1], Equestrians, city);
        Assert.Equal(1, ResourceAccess.Count(sim, 0, city.Id, "world:horse"));
        Assert.Equal(1, Corps(sim).ConsumedBonusCount(sim, city.Id, Tours));
    }

    private static bool CorporationsCompete(CorporationDef a, CorporationDef b) =>
        a.CompetingCorporations.Contains(b.Identity) || b.CompetingCorporations.Contains(a.Identity) || a.PrereqBonuses.Intersect(b.PrereqBonuses).Any();

    private static IReadOnlyList<string> Refusals(Simulation sim, City city, string building) =>
        P6RulesScenario.Service<ICityProductionService>(sim).Validate(sim, city.Id, "cities:building", building).Failures.Select(f => f.Explanation).ToList();
}
