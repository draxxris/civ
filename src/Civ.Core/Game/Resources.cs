using Civ.Content;

namespace Civ.Game;

/// <summary>
/// The shared resource rules: per-city rates (data plus <see cref="IResourceSource"/>s), pools, affordability and spending.
/// Pure functions of the simulation state, so mods, the base game, the AI and clients all agree on the numbers.
/// </summary>
public static class Resources
{
    public static bool IsCityScoped(ResourceDef def) => def.Scope == ResourceScopes.City;

    /// <summary>Per-turn change every resource gets from this city (positive produced, negative consumed), by id, zeros left out.</summary>
    public static SortedDictionary<string, long> CityRates(Simulation sim, City city)
    {
        var rates = new SortedDictionary<string, long>(StringComparer.Ordinal);
        var civics = sim.State.PlayerById(city.Owner).Civics.Values;
        foreach (var def in sim.Content.Resources.All)
        {
            long total = 0;
            foreach (var rate in def.Rates)
                total += rate.Amount * (long)(rate.From switch
                {
                    "building" => city.Buildings.Count(b => b == rate.Id),
                    "specialist" => city.Specialists.GetValueOrDefault(rate.Id ?? ""),
                    "civic" => civics.Contains(rate.Id) ? 1 : 0,
                    "terrain" => (sim.State.Terrain.GetValueOrDefault(city.Position) == rate.Id ? 1 : 0)
                                 + city.Worked.Count(h => sim.State.Terrain.GetValueOrDefault(h) == rate.Id),
                    "institution" => city.Institutions.Contains(rate.Id ?? "") ? 1 : 0,
                    "population" => city.Population,
                    "city" => 1,
                    _ => 0,
                });
            if (total != 0) rates[def.Id] = total;
        }
        foreach (var source in sim.Content.ResourceSources.All) source.AddCityRates(sim, city, rates);
        foreach (var key in rates.Where(kv => kv.Value == 0).Select(kv => kv.Key).ToList()) rates.Remove(key);
        return rates;
    }

    /// <summary>Net per-turn change of a player's player-scoped resources (stockpiles and flows): the sum over its cities.</summary>
    public static SortedDictionary<string, long> PlayerRates(Simulation sim, int playerId)
    {
        var total = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (var city in sim.State.Cities.Where(c => c.Owner == playerId).OrderBy(c => c.Id))
            foreach (var (id, amount) in CityRates(sim, city))
                if (sim.Content.Resources.TryGet(id, out var def) && !IsCityScoped(def))
                    total[id] = total.GetValueOrDefault(id) + amount;
        return total;
    }

    /// <summary>Net per-turn amount of one player-scoped resource, e.g. how much of a flow good is available.</summary>
    public static long PlayerRate(Simulation sim, int playerId, string resourceId) => PlayerRates(sim, playerId).GetValueOrDefault(resourceId);

    /// <summary>
    /// What can be spent: the player's pool for a player-scoped resource; for a city-scoped one the given city's pool, or with no
    /// city the sum over all the player's cities. A flow resource can never be spent.
    /// </summary>
    public static long Available(Simulation sim, int playerId, City? city, string resourceId)
    {
        var def = sim.Content.Resources.Get(resourceId);
        if (!def.Stockpile) return 0;
        if (!IsCityScoped(def)) return sim.State.PlayerById(playerId).Resources.GetValueOrDefault(resourceId);
        if (city != null) return city.Resources.GetValueOrDefault(resourceId);
        return sim.State.Cities.Where(c => c.Owner == playerId).Sum(c => c.Resources.GetValueOrDefault(resourceId));
    }

    /// <summary>Null if the player (paying from <paramref name="city"/> for city-scoped resources) can pay, else why not.</summary>
    public static string? CheckAfford(Simulation sim, int playerId, City? city, IReadOnlyDictionary<string, long> cost)
    {
        foreach (var (id, amount) in cost.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (!sim.Content.Resources.TryGet(id, out var def)) return $"unknown resource {id}";
            long have = Available(sim, playerId, city, id);
            if (have < amount) return $"needs {amount} {def.Name} (have {have})";
        }
        return null;
    }

    /// <summary>
    /// Pay a cost already checked with <see cref="CheckAfford"/>. A city-scoped cost with no city is drawn from the player's
    /// cities in id order.
    /// </summary>
    public static void Spend(Simulation sim, int playerId, City? city, IReadOnlyDictionary<string, long> cost)
    {
        foreach (var (id, amount) in cost.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (amount <= 0) continue;
            var def = sim.Content.Resources.Get(id);
            if (!IsCityScoped(def)) { Take(sim.State.PlayerById(playerId).Resources, id, amount); continue; }
            if (city != null) { Take(city.Resources, id, amount); continue; }
            long left = amount;
            foreach (var c in sim.State.Cities.Where(c => c.Owner == playerId).OrderBy(c => c.Id))
            {
                long take = Math.Min(left, c.Resources.GetValueOrDefault(id));
                if (take > 0) { Take(c.Resources, id, take); left -= take; }
                if (left == 0) break;
            }
        }
    }

    /// <summary>Add (or with a negative amount remove) to the pool the resource lives in. Pools never go below 0 or above the cap.</summary>
    public static void Add(Simulation sim, int playerId, City? city, string resourceId, long amount)
    {
        var def = sim.Content.Resources.Get(resourceId);
        if (!def.Stockpile || amount == 0) return;
        var pool = IsCityScoped(def)
            ? (city ?? throw new ArgumentException($"{resourceId} is city-scoped; a city is required")).Resources
            : sim.State.PlayerById(playerId).Resources;
        Put(pool, def, pool.GetValueOrDefault(resourceId) + amount);
    }

    /// <summary>"20 Faith, 5 Renown", for UIs and messages.</summary>
    public static string CostText(ContentDb content, IReadOnlyDictionary<string, long> cost) =>
        string.Join(", ", cost.Where(kv => kv.Value > 0).OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Value} {(content.Resources.TryGet(kv.Key, out var d) ? d.Name : kv.Key)}"));

    private static void Take(SortedDictionary<string, long> pool, string id, long amount) => pool[id] = Math.Max(0, pool.GetValueOrDefault(id) - amount);

    private static void Put(SortedDictionary<string, long> pool, ResourceDef def, long value)
    {
        value = Math.Max(0, value);
        if (def.Cap > 0) value = Math.Min(def.Cap, value);
        pool[def.Id] = value;
    }

    internal static void Collect(Simulation sim)
    {
        var playerTotals = new SortedDictionary<int, SortedDictionary<string, long>>();
        foreach (var city in sim.State.Cities.OrderBy(c => c.Id))
        {
            foreach (var (id, amount) in CityRates(sim, city))
            {
                if (!sim.Content.Resources.TryGet(id, out var def) || !def.Stockpile) continue;
                if (IsCityScoped(def)) { Put(city.Resources, def, city.Resources.GetValueOrDefault(id) + amount); continue; }
                if (!playerTotals.TryGetValue(city.Owner, out var totals)) playerTotals[city.Owner] = totals = new(StringComparer.Ordinal);
                totals[id] = totals.GetValueOrDefault(id) + amount;
            }
        }
        // Player pools take the net of all their cities at once, so the order cities are visited never matters.
        foreach (var (playerId, totals) in playerTotals)
        {
            var pool = sim.State.PlayerById(playerId).Resources;
            foreach (var (id, amount) in totals) Put(pool, sim.Content.Resources.Get(id), pool.GetValueOrDefault(id) + amount);
        }
    }
}

/// <summary>Built into the core: adds every city's resource rates to the right pools once per turn, after the base rules.</summary>
public sealed class ResourceSystem : ITurnSystem
{
    public string Id => "core:resources";
    public int Order => 400;
    public void Run(Simulation sim) => Resources.Collect(sim);
}
