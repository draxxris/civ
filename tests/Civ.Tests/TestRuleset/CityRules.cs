using Civ.Content;
using Civ.Game;

namespace Civ.BaseGame;

/// <summary>Population, worked tiles and specialists, Civ4 style. Pure functions of the simulation state.</summary>
public static class CityRules
{
    public const int Radius = 2;
    public const int FoodPerCitizen = 2;

    public static int FoodToGrow(int population) => 10 + 4 * population;

    public static int TileScore(Simulation sim, Hex hex)
    {
        var y = Yields.OfTile(sim, hex);
        return y.Food * 3 + y.Production * 2 + y.Commerce * 2;
    }

    /// <summary>Tiles in the city radius (centre excluded) that no other city is working or sits on.</summary>
    public static IEnumerable<Hex> WorkableTiles(Simulation sim, City city) =>
        sim.State.Grid.Range(city.Position, Radius)
            .Where(h => h != city.Position)
            .Where(h => sim.State.CityAt(h) is not { } occupying || occupying.Id == city.Id)
            .Where(h => !sim.State.Cities.Any(o => o.Id != city.Id && o.Worked.Contains(h)));

    public static SpecialistDef? DefaultSpecialist(Simulation sim) => sim.Content.Specialists.All.FirstOrDefault(s => s.Unlimited);

    /// <summary>How many of a specialist the city may have: building slots plus civic slots; -1 anywhere means unlimited.</summary>
    public static int Slots(Simulation sim, City city, string specialistId)
    {
        if (sim.Content.Specialists.Get(specialistId).Unlimited) return int.MaxValue;
        int total = 0;
        foreach (var id in city.Buildings)
            if (sim.Content.Buildings.Get(id).SpecialistSlots.TryGetValue(specialistId, out var n)) { if (n < 0) return int.MaxValue; total += n; }
        foreach (var id in sim.State.PlayerById(city.Owner).Civics.Values)
            if (sim.Content.Civics.Get(id).SpecialistSlots.TryGetValue(specialistId, out var n)) { if (n < 0) return int.MaxValue; total += n; }
        return total;
    }


    public static string? ValidateAssignment(Simulation sim, City city, IReadOnlyCollection<Hex> tiles, IReadOnlyDictionary<string, int> specialists)
    {
        if (tiles.Distinct().Count() != tiles.Count) return "duplicate tile";
        var workable = WorkableTiles(sim, city).ToHashSet();
        if (tiles.Any(t => !workable.Contains(t))) return "tile not workable by this city";
        foreach (var (id, n) in specialists)
        {
            if (!sim.Content.Specialists.Contains(id)) return $"unknown specialist {id}";
            if (n < 0) return "negative specialist count";
            if (n > Slots(sim, city, id)) return $"not enough {sim.Content.Specialists.Get(id).Name} slots";
        }
        if (tiles.Count + specialists.Values.Sum() != city.Population) return "every citizen must work a tile or be a specialist";
        return null;
    }

    public static void Assign(City city, IEnumerable<Hex> tiles, IReadOnlyDictionary<string, int> specialists)
    {
        city.Worked.Clear();
        city.Worked.AddRange(tiles.OrderBy(h => h.Q).ThenBy(h => h.R));
        city.Specialists.Clear();
        foreach (var (id, n) in specialists) if (n > 0) city.Specialists[id] = n;
    }

    /// <summary>Put one new citizen on the best free tile, or make it a default specialist if there is none.</summary>
    public static void AddCitizen(Simulation sim, City city)
    {
        var best = WorkableTiles(sim, city).Where(h => !city.Worked.Contains(h))
            .OrderByDescending(h => TileScore(sim, h)).ThenBy(h => h.Q).ThenBy(h => h.R)
            .Select(h => (Hex?)h).FirstOrDefault();
        if (best != null)
        {
            city.Worked.Add(best.Value);
            city.Worked.Sort((a, b) => a.Q != b.Q ? a.Q.CompareTo(b.Q) : a.R.CompareTo(b.R));
            return;
        }
        var spec = DefaultSpecialist(sim);
        if (spec != null) city.Specialists[spec.Id] = city.Specialists.GetValueOrDefault(spec.Id) + 1;
    }

    /// <summary>Take one citizen away: default specialists first, then other specialists, then the worst tile.</summary>
    public static void RemoveCitizen(Simulation sim, City city)
    {
        var id = city.Specialists.Keys.OrderByDescending(k => sim.Content.Specialists.Get(k).Unlimited).ThenBy(k => k, StringComparer.Ordinal).FirstOrDefault();
        if (id != null)
        {
            if (--city.Specialists[id] <= 0) city.Specialists.Remove(id);
            return;
        }
        var worst = city.Worked.OrderBy(h => TileScore(sim, h)).ThenByDescending(h => h.Q).ThenByDescending(h => h.R).FirstOrDefault();
        city.Worked.Remove(worst);
    }

    /// <summary>After slots shrink (a building lost, a civic changed) turn surplus specialists back into default citizens.</summary>
    public static void EnforceSlots(Simulation sim, City city)
    {
        var fallback = DefaultSpecialist(sim);
        foreach (var id in city.Specialists.Keys.ToList())
        {
            int over = city.Specialists[id] - Slots(sim, city, id);
            if (over <= 0) continue;
            city.Specialists[id] -= over;
            if (city.Specialists[id] <= 0) city.Specialists.Remove(id);
            if (fallback != null) city.Specialists[fallback.Id] = city.Specialists.GetValueOrDefault(fallback.Id) + over;
        }
    }
}
