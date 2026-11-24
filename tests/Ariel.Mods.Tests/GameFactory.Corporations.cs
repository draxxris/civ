using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Mods.Corporations;

namespace Ariel.Mods.Tests;

/// <summary>Corporation fixtures. They read the Corporations mod's API, so they live beside the tests rather than in
/// the shared harness the sync checker compiles.</summary>
public static partial class GameFactory
{
    /// <summary>The building whose completion founds the corporation, its headquarters or Master Guild: the building
    /// whose <see cref="BuildingDef.Founds"/> names it, which the Corporations mod's founder claims.</summary>
    public static string Headquarters(Simulation sim, string corporation) =>
        sim.Content.Get<BuildingDef>().All
            .Single(b => b.Founds is { Kind: "corporation" } founds && founds.Id == corporation && sim.Rules().Founders.Any(f => f.Claims(sim.Content, b))).Id;

    /// <summary>The technologies the corporation's founding building requires.</summary>
    public static IReadOnlyList<string> FoundingTechs(Simulation sim, string corporation) =>
        ContentReferences.Leaves(sim.Content.Get<BuildingDef>(Headquarters(sim, corporation)).Requirements ?? new Requirement.All([]))
            .Where(l => l.Kind == "tech").Select(l => l.Id).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>Grant the city's team the founding building's technologies.</summary>
    public static void GrantFoundingTechs(Simulation sim, City city, string corporation) =>
        sim.TeamOf(city.Owner).Techs.UnionWith(FoundingTechs(sim, corporation));

    /// <summary>Meet everything the corporation's founding building asks of the city and its owner: its technologies, the
    /// buildings of its class a guild needs across the empire (each in another of the owner's cities, founding new
    /// cities on the free tiles when the owner has too few) and one connected resource the corporation consumes. The
    /// real requirement evaluator decides when a building-class count is met, so world-size scaling stays its own.</summary>
    public static void SatisfyFounding(Simulation sim, City city, string corporation)
    {
        GrantFoundingTechs(sim, city, corporation);
        var def = sim.Content.Get<CorporationDef>(corporation);
        // The first resource it consumes that the city then counts as connected (some need more than the plot).
        var corporations = sim.Content.Services.Get<ICorporationService>().Single();
        foreach (var bonus in def.PrereqBonuses)
        {
            PlaceResource(sim, city, bonus);
            if (corporations.ConsumedBonusCount(sim, city.Id, corporation) > 0) break;
        }
        var building = sim.Content.Get<BuildingDef>(Headquarters(sim, corporation));
        foreach (var leaf in ContentReferences.Leaves(building.Requirements ?? new Requirement.All([])).Where(l => l.Kind == "building-class"))
        {
            for (int guard = 0; Unmet(sim, city, leaf); guard++)
            {
                if (guard > 32) throw new InvalidOperationException($"{building.Id}: could not meet {leaf.Kind} {leaf.Id}");
                var host = leaf.Param == 1
                    ? sim.State.Cities.FirstOrDefault(c => c.Owner == city.Owner && !c.Buildings.Contains(leaf.Id)) ?? NewCity(sim, city.Owner)
                    : city;
                host.Buildings.Add(leaf.Id);
            }
        }
    }

    private static bool Unmet(Simulation sim, City city, Requirement.Leaf leaf) =>
        !sim.Content.Requirements.Evaluate(leaf, new RequirementContext(sim, city.Owner, city)).Success;

    /// <summary>A resource on the city's own plot, with the technologies that reveal and trade it.</summary>
    public static void PlaceResource(Simulation sim, City city, string bonus)
    {
        sim.State.PlotAt(city.Position).Bonus = bonus;
        var def = sim.Content.Get<BonusDef>(bonus);
        if (def.RevealTech is { } reveal) sim.TeamOf(city.Owner).Techs.Add(reveal);
        if (def.TradeTech is { } trade) sim.TeamOf(city.Owner).Techs.Add(trade);
    }

    /// <summary>A new city for the player on a tile at least two tiles from every city.</summary>
    private static City NewCity(Simulation sim, int owner)
    {
        var grid = sim.State.Grid;
        var position = Enumerable.Range(0, sim.State.Plots.Length).Select(t => grid.HexOf(new TileId(t)))
            .First(h => sim.State.Cities.All(c => grid.Distance(h, c.Position) >= 2));
        var city = new City { Id = sim.State.NextCityId++, Owner = owner, Position = position };
        sim.State.AddCity(city); sim.State.PlotAt(position).Owner = owner;
        return city;
    }

    /// <summary>Complete a corporation's founding building in a city, as finished production does.</summary>
    public static void FoundCorporation(Simulation sim, City city, string corporation)
    {
        string headquarters = Headquarters(sim, corporation);
        city.Buildings.Add(headquarters);
        sim.Events.Publish(new BuildingCompleted(city.Id, headquarters));
    }
}
