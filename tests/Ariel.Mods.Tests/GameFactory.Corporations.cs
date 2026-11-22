using Ariel.Game;
using Ariel.Mods.Corporations;

namespace Ariel.Mods.Tests;

/// <summary>Corporation fixtures. They read the Corporations mod's API, so they live beside the tests rather than in
/// the shared harness the sync checker compiles.</summary>
public static partial class GameFactory
{
    /// <summary>The headquarters whose completion founds the corporation: the building naming its identity that a
    /// loaded founder claims (the imported headquarters of the same identity is not claimed).</summary>
    public static string Headquarters(Simulation sim, string corporation)
    {
        string identity = sim.Content.Get<CorporationDef>(corporation).Identity;
        return sim.Content.Get<Ariel.Content.Schema.BuildingDef>().All
            .Single(b => b.FoundsCorporation == identity && sim.Rules().Founders.Any(f => f.Claims(sim.Content, b))).Id;
    }

    /// <summary>Complete a corporation's headquarters in a city, as finished production does.</summary>
    public static void FoundCorporation(Simulation sim, City city, string corporation)
    {
        string headquarters = Headquarters(sim, corporation);
        city.Buildings.Add(headquarters);
        sim.Events.Publish(new BuildingCompleted(city.Id, headquarters));
    }
}
