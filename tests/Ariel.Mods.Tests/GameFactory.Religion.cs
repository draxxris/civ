using Ariel.Game;
using Ariel.Mods.Religion;

namespace Ariel.Mods.Tests;

/// <summary>Religion fixtures. They read the Religion mod's API, so they live beside the tests rather than in the
/// shared harness the sync checker compiles.</summary>
public static partial class GameFactory
{
    /// <summary>The world wonder whose completion founds the religion.</summary>
    public static string FoundingWonder(Simulation sim, string religion) =>
        sim.Content.Get<Ariel.Content.Schema.BuildingDef>().All.Single(b => b.FoundsReligion == ReligionIdentities.Identity(sim.Content, religion)).Id;

    /// <summary>Complete a religion's founding wonder in a city, as finished production does.</summary>
    public static void FoundReligion(Simulation sim, City city, string religion)
    {
        string wonder = FoundingWonder(sim, religion);
        city.Buildings.Add(wonder);
        sim.Events.Publish(new BuildingCompleted(city.Id, wonder));
    }
}
