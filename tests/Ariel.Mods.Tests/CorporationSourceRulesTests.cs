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

    private static IReadOnlyList<string> Refusals(Simulation sim, City city, string building) =>
        P6RulesScenario.Service<ICityProductionService>(sim).Validate(sim, city.Id, "cities:building", building).Failures.Select(f => f.Explanation).ToList();
}
