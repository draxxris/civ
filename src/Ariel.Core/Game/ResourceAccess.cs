namespace Ariel.Game;

/// <summary>One definition of "how many copies of a resource this city can use". The Diplomacy rules answer with
/// physical connectivity plus imports and minus exports when they are loaded; otherwise plain connectivity is the
/// answer. Callers must not re-implement the fallback: four copies of it drifted apart before.
/// <para>The per-city memo belongs to the city read scope, not here: this is the uncached answer.</para></summary>
public static class ResourceAccess
{
    public static long Count(Simulation sim, int player, int city, string resource) =>
        sim.Content.Services.Get<IDiplomacyService>().SingleOrDefault()?.AvailableResourceCount(sim, player, city, resource)
        ?? sim.Content.Services.Get<IResourceNetwork>().Single().ConnectedCount(sim, player, city, resource);

    public static IReadOnlyDictionary<string, long> Counts(Simulation sim, int player, int city, IReadOnlyCollection<string> resources)
    {
        if (sim.Content.Services.Get<IDiplomacyService>().SingleOrDefault() is { } diplomacy)
            return diplomacy.AvailableResourceCounts(sim, player, city, resources);
        var connected = sim.Content.Services.Get<IResourceNetwork>().Single().ConnectedCounts(sim, player, city);
        return resources.Distinct(StringComparer.Ordinal)
            .ToDictionary(resource => resource, resource => (long)connected.GetValueOrDefault(resource), StringComparer.Ordinal);
    }
}
