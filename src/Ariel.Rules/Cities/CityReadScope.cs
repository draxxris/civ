using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Rules.Cities;

/// <summary>Per-thread memo for one read-only city query. Building activity, power and grants depend on every city
/// of the team, so nested queries would otherwise repeat whole-empire work for each city they visit. A scope only
/// lives inside a read-only call, so the state cannot change while it is open; it is never saved or shared.
/// <para>While the simulation holds a read epoch (<see cref="Simulation.ReadEpoch"/>), whose opener promises no
/// writes and which the kernel ends before any, every query on a thread shares one scope for that epoch instead, so
/// an AI segment's many queries resolve each city once.</para></summary>
internal sealed class CityReadScope
{
    [ThreadStatic] private static CityReadScope? t_current;
    // The last epoch's scope on this thread. Epoch values are never reused by a simulation, so a stale one never
    // matches.
    [ThreadStatic] private static CityReadScope? t_epoch;
    private readonly Simulation _sim;
    private readonly long _epoch;
    public readonly Dictionary<City, IReadOnlyList<S.BuildingDef>> Active = new(ReferenceEqualityComparer.Instance);
    public readonly Dictionary<City, IReadOnlyList<S.BuildingDef>> Effective = new(ReferenceEqualityComparer.Instance);
    public readonly Dictionary<City, S.CultureLevelDef> Level = new(ReferenceEqualityComparer.Instance);
    public readonly Dictionary<City, IReadOnlyList<S.BuildingDef>> ActiveWithoutPower = new(ReferenceEqualityComparer.Instance);
    public readonly Dictionary<City, S.BuildingDef[]> LocalPowerSources = new(ReferenceEqualityComparer.Instance);
    public readonly Dictionary<City, string[]> FreeBuildings = new(ReferenceEqualityComparer.Instance);
    public readonly Dictionary<City, IReadOnlyDictionary<string, int>> Available = new(ReferenceEqualityComparer.Instance);
    public readonly Dictionary<int, SortedSet<string>> GrantCategories = new();
    public readonly Dictionary<(int Team, int Area), bool> AreaCleanPower = new();

    private CityReadScope(Simulation sim, long epoch) { _sim = sim; _epoch = epoch; }

    /// <summary>Run a read-only query inside the current scope for this simulation, opening one if needed.</summary>
    public static T Run<T>(Simulation sim, Func<CityReadScope, T> query)
    {
        if (t_current is { } current && ReferenceEquals(current._sim, sim)) return query(current);
        var previous = t_current;
        t_current = ForEpoch(sim) ?? new CityReadScope(sim, 0);
        try { return query(t_current); }
        finally { t_current = previous; }
    }

    private static CityReadScope? ForEpoch(Simulation sim)
    {
        long epoch = sim.ReadEpoch;
        if (epoch == 0) return null;
        if (t_epoch is not { } scope || !ReferenceEquals(scope._sim, sim) || scope._epoch != epoch) t_epoch = scope = new(sim, epoch);
        return scope;
    }

    /// <summary>Memoize one value in a scope table.</summary>
    public static TValue Memo<TKey, TValue>(Dictionary<TKey, TValue> table, TKey key, Func<TValue> compute) where TKey : notnull
    {
        if (table.TryGetValue(key, out var value)) return value;
        value = compute();
        table[key] = value;
        return value;
    }
}
