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

    private static IReadOnlyList<string> Refusals(Simulation sim, City city, string building) =>
        P6RulesScenario.Service<ICityProductionService>(sim).Validate(sim, city.Id, "cities:building", building).Failures.Select(f => f.Explanation).ToList();
}
