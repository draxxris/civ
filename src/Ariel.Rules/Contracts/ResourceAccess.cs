namespace Ariel.Game;

/// <summary>One definition of "how many copies of a resource this city can use": physical connectivity plus imports and
/// minus exports of the players' running deals (<see cref="IDiplomacyService.AvailableResourceCount"/>). Callers must
/// not re-implement it: four copies of it drifted apart before.
/// <para>The per-city memo belongs to the city read scope, not here: this is the uncached answer.</para></summary>
public static class ResourceAccess
{
    public static long Count(Simulation sim, int player, int city, string resource) =>
        sim.Rules().Diplomacy.AvailableResourceCount(sim, player, city, resource);

    public static IReadOnlyDictionary<string, long> Counts(Simulation sim, int player, int city, IReadOnlyCollection<string> resources) =>
        sim.Rules().Diplomacy.AvailableResourceCounts(sim, player, city, resources);
}
