using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.Cities;

/// <summary>Per-thread memo for one read-only city query. Building activity, power and grants depend on every city
/// of the team, so nested queries would otherwise repeat whole-empire work for each city they visit. A scope only
/// lives inside a read-only call, so the state cannot change while it is open; it is never saved or shared.</summary>
internal sealed class CityReadScope
{
    [ThreadStatic] private static CityReadScope? t_current;
    private readonly Simulation _sim;
    public readonly Dictionary<City, IReadOnlyList<S.BuildingDef>> Active = new(ReferenceEqualityComparer.Instance);
    public readonly Dictionary<City, IReadOnlyList<S.BuildingDef>> ActiveWithoutPower = new(ReferenceEqualityComparer.Instance);
    public readonly Dictionary<City, S.BuildingDef[]> LocalPowerSources = new(ReferenceEqualityComparer.Instance);
    public readonly Dictionary<City, string[]> FreeBuildings = new(ReferenceEqualityComparer.Instance);
    public readonly Dictionary<City, IReadOnlyDictionary<string, int>> Available = new(ReferenceEqualityComparer.Instance);
    public readonly Dictionary<int, SortedSet<string>> GrantCategories = new();
    public readonly Dictionary<(int Team, int Area), bool> AreaCleanPower = new();

    private CityReadScope(Simulation sim) => _sim = sim;

    /// <summary>Run a read-only query inside the current scope for this simulation, opening one if needed.</summary>
    public static T Run<T>(Simulation sim, Func<CityReadScope, T> query)
    {
        if (t_current is { } current && ReferenceEquals(current._sim, sim)) return query(current);
        var previous = t_current;
        t_current = new CityReadScope(sim);
        try { return query(t_current); }
        finally { t_current = previous; }
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
