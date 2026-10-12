using Civ.Content;

namespace Civ.Game;

/// <summary>
/// The shared founding and spreading rules for religions, guilds, corporations and anything else a mod registers as an
/// <see cref="InstitutionDef"/>. What an institution actually does (yields, resources) is up to the mod that defines it.
/// </summary>
public static class Institutions
{
    public static bool IsFounded(Simulation sim, string id) => sim.State.Institutions.ContainsKey(id);

    /// <summary>Cities hosting it, in id order.</summary>
    public static IEnumerable<City> CitiesWith(Simulation sim, string id) =>
        sim.State.Cities.Where(c => c.Institutions.Contains(id)).OrderBy(c => c.Id);

    /// <summary>Its first city (holy city, headquarters), or null if not founded or that city is gone.</summary>
    public static City? HomeCity(Simulation sim, string id) =>
        sim.State.Institutions.TryGetValue(id, out var f) ? sim.State.Cities.FirstOrDefault(c => c.Id == f.HomeCity) : null;

    /// <summary>True when one of the player's cities hosts it.</summary>
    public static bool PlayerHas(Simulation sim, int playerId, string id) =>
        sim.State.Cities.Any(c => c.Owner == playerId && c.Institutions.Contains(id));

    public static bool HasCategory(Simulation sim, City city, string category) =>
        city.Institutions.Any(i => sim.Content.Institutions.TryGet(i, out var d) && d.Category == category);

    /// <summary>Null if the city can take it in: not already there and no conflicting institution either way round.</summary>
    public static string? CanHost(Simulation sim, City city, InstitutionDef def)
    {
        if (city.Institutions.Contains(def.Id)) return $"{city.Name} already has {def.Name}";
        foreach (var other in city.Institutions)
        {
            var o = sim.Content.Institutions.Get(other);
            if (def.Conflicts.Contains(other) || o.Conflicts.Contains(def.Id)) return $"{def.Name} cannot share a city with {o.Name}";
        }
        return null;
    }

    /// <summary>Requirement check for buildings and units that need an institution (or any of a category) in the city.</summary>
    public static string? CheckRequirement(Simulation sim, City city, string? institution, string? category)
    {
        if (institution != null && !city.Institutions.Contains(institution))
            return $"needs {(sim.Content.Institutions.TryGet(institution, out var d) ? d.Name : institution)} in the city";
        if (category != null && !HasCategory(sim, city, category))
            return $"needs a {category} in the city";
        return null;
    }

    public static string? CanFound(Simulation sim, int playerId, City city, string id)
    {
        if (city.Owner != playerId) return "not your city";
        if (!sim.Content.Institutions.TryGet(id, out var def)) return "unknown institution";
        if (IsFounded(sim, id)) return $"{def.Name} has already been founded";
        if (def.RequiresTech != null && !sim.State.PlayerById(playerId).Techs.Contains(def.RequiresTech)) return "tech not known";
        if (def.RequiresBuilding != null && !city.Buildings.Contains(def.RequiresBuilding))
            return $"needs a {(sim.Content.Buildings.TryGet(def.RequiresBuilding, out var b) ? b.Name : def.RequiresBuilding)} in the city";
        return CanHost(sim, city, def) ?? Resources.CheckAfford(sim, playerId, city, def.FoundCost);
    }

    /// <summary>Pays the founding cost and makes <paramref name="city"/> its home. Call <see cref="CanFound"/> first.</summary>
    public static void Found(Simulation sim, int playerId, City city, string id)
    {
        var def = sim.Content.Institutions.Get(id);
        Resources.Spend(sim, playerId, city, def.FoundCost);
        sim.State.Institutions[id] = new FoundedInstitution { Founder = playerId, HomeCity = city.Id, FoundedTurn = sim.State.Turn };
        city.Institutions.Add(id);
    }

    public static string? CanSpreadWith(Simulation sim, int playerId, Unit unit, string id)
    {
        if (unit.Owner != playerId) return "not your unit";
        if (!sim.Content.Institutions.TryGet(id, out var def)) return "unknown institution";
        if (!IsFounded(sim, id)) return $"{def.Name} has not been founded";
        if (!def.Spreaders.Contains(unit.Type)) return $"this unit cannot spread {def.Name}";
        if (!PlayerHas(sim, playerId, id)) return $"none of your cities has {def.Name}";
        var city = sim.State.CityAt(unit.Position);
        if (city == null) return "the unit must be in a city";
        return CanHost(sim, city, def) ?? Resources.CheckAfford(sim, playerId, null, def.SpreadCost);
    }

    /// <summary>Uses up the unit, pays the spread cost and adds the institution to the city it stands in.</summary>
    public static void SpreadWith(Simulation sim, int playerId, Unit unit, string id)
    {
        var def = sim.Content.Institutions.Get(id);
        Resources.Spend(sim, playerId, null, def.SpreadCost);
        var city = sim.State.CityAt(unit.Position)!;
        sim.State.Units.RemoveAll(u => u.Id == unit.Id);
        city.Institutions.Add(id);
    }

    /// <summary>Each founded institution with a passive chance rolls once for every city in range of a city that had it at the start of the step.</summary>
    internal static void SpreadPassively(Simulation sim)
    {
        foreach (var id in sim.State.Institutions.Keys.ToList())
        {
            var def = sim.Content.Institutions.Get(id);
            if (def.PassiveSpreadPercent <= 0) continue;
            var sources = CitiesWith(sim, id).Select(c => c.Position).ToList();
            if (sources.Count == 0) continue;
            foreach (var city in sim.State.Cities.OrderBy(c => c.Id))
            {
                if (CanHost(sim, city, def) != null) continue;
                if (!sources.Any(p => p.DistanceTo(city.Position) <= def.SpreadRange)) continue;
                if (sim.Rng.Next(100) < def.PassiveSpreadPercent) city.Institutions.Add(id);
            }
        }
    }
}

/// <summary>Found an institution in one of your cities, paying its <see cref="InstitutionDef.FoundCost"/>.</summary>
public sealed class FoundInstitutionCommand : GameCommand
{
    public int CityId { get; init; }
    public string Institution { get; init; } = "";

    public override string? Validate(Simulation sim)
    {
        var city = sim.State.Cities.FirstOrDefault(c => c.Id == CityId);
        if (city == null) return "unknown city";
        return Institutions.CanFound(sim, Player, city, Institution);
    }

    public override void Apply(Simulation sim) =>
        Institutions.Found(sim, Player, sim.State.Cities.First(c => c.Id == CityId), Institution);
}

/// <summary>Use up a spreader unit (missionary, executive, ...) to bring an institution to the city it stands in.</summary>
public sealed class SpreadInstitutionCommand : GameCommand
{
    public int UnitId { get; init; }
    public string Institution { get; init; } = "";

    public override string? Validate(Simulation sim)
    {
        var unit = sim.State.Units.FirstOrDefault(u => u.Id == UnitId);
        if (unit == null) return "unknown unit";
        return Institutions.CanSpreadWith(sim, Player, unit, Institution);
    }

    public override void Apply(Simulation sim) =>
        Institutions.SpreadWith(sim, Player, sim.State.UnitById(UnitId), Institution);
}

/// <summary>Built into the core: passive spread of institutions to nearby cities, after resources are collected.</summary>
public sealed class InstitutionSpreadSystem : ITurnSystem
{
    public string Id => "core:institutions";
    public int Order => 450;
    public void Run(Simulation sim) => Institutions.SpreadPassively(sim);
}
