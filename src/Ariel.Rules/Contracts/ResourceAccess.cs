namespace Ariel.Game;

/// <summary>One definition of "how many copies of a resource this city can use": physical connectivity plus imports and
/// minus exports of the players' running deals (<see cref="IDiplomacyService.AvailableResourceCount"/>), plus the copies
/// the city's own rules supply it (<see cref="ICityResourceSupply"/>, a corporation branch's produced resource, say).
/// Callers must not re-implement it: four copies of it drifted apart before.
/// <para>Supplied copies serve only their city: they are not on the trade network, so nothing can export them. A
/// question about trade (what a player can offer, what it holds to sell) asks <see cref="TradeCount"/>, the network and
/// deal part alone, which is what deal validation checks.</para>
/// <para>The per-city memo belongs to the city read scope, not here: this is the uncached answer.</para></summary>
public static class ResourceAccess
{
    public static long Count(Simulation sim, int player, int city, string resource) =>
        Counts(sim, player, city, [resource]).GetValueOrDefault(resource);

    public static IReadOnlyDictionary<string, long> Counts(Simulation sim, int player, int city, IReadOnlyCollection<string> resources)
    {
        var counts = TradeCounts(sim, player, city, resources);
        var supplied = Supplied(sim, player, city);
        if (supplied.Count == 0) return counts;
        var result = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (var resource in resources.Distinct(StringComparer.Ordinal))
            result[resource] = counts.GetValueOrDefault(resource) + supplied.GetValueOrDefault(resource);
        return result;
    }

    /// <summary>Copies on the trade network and through deals, without what the city supplies itself.</summary>
    public static long TradeCount(Simulation sim, int player, int city, string resource) =>
        sim.Rules().Diplomacy.AvailableResourceCount(sim, player, city, resource);

    /// <summary>Copies on the trade network and through deals, without what the city supplies itself.</summary>
    public static IReadOnlyDictionary<string, long> TradeCounts(Simulation sim, int player, int city, IReadOnlyCollection<string> resources) =>
        sim.Rules().Diplomacy.AvailableResourceCounts(sim, player, city, resources);

    /// <summary>The copies every <see cref="ICityResourceSupply"/> gives the player's city, by resource; empty for a city
    /// the player does not own, which can use none of its resources.</summary>
    public static IReadOnlyDictionary<string, long> Supplied(Simulation sim, int player, int city)
    {
        var suppliers = sim.Content.Services.Get<ICityResourceSupply>();
        if (suppliers.Count == 0 || sim.State.FindCity(city) is not { } target || target.Owner != player) return Empty;
        SortedDictionary<string, long>? result = null;
        foreach (var supplier in suppliers)
            foreach (var (resource, copies) in supplier.Supply(sim, target, wanted => TradeCounts(sim, player, city, wanted)))
            {
                if (copies <= 0) continue;
                result ??= new(StringComparer.Ordinal);
                result[resource] = result.GetValueOrDefault(resource) + copies;
            }
        return result ?? Empty;
    }

    private static readonly IReadOnlyDictionary<string, long> Empty = new Dictionary<string, long>();
}

/// <summary>Copies of resources a city's own rules give it beyond the trade network: a corporation branch that
/// produces a resource from the ones it consumes, for instance. They count wherever the city uses resources
/// (<see cref="ResourceAccess.Counts"/>) and never for trade.
/// <para>A rule that counts resources asks every supplier, so the contract bounds what a supplier reads: it answers
/// from the city's buildings, holdings, techs and resource counts, never from who works the city's plots or what the
/// city is building (<see cref="CityStateDomains.Work"/>, <see cref="CityStateDomains.Queue"/>). That is the default
/// declaration (<see cref="Ariel.Content.IStateReader"/>); a supplier that reads less may say so.</para></summary>
public interface ICityResourceSupply : Ariel.Content.IStateReader
{
    IReadOnlyList<string>? Ariel.Content.IStateReader.UnreadDomains(Ariel.Content.ContentDb content) => CityStateDomains.WorkAndQueue;
    /// <summary>The copies supplied to the city, by resource. <paramref name="networkCounts"/> answers the city's network
    /// and deal copies of the resources asked for; a supplier must read resources through it, never through
    /// <see cref="ResourceAccess.Counts"/>, which would ask the suppliers again. Read-only.</summary>
    IEnumerable<KeyValuePair<string, long>> Supply(Simulation sim, City city,
        Func<IReadOnlyCollection<string>, IReadOnlyDictionary<string, long>> networkCounts);
}
