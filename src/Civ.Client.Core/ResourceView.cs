using Civ.Game;

namespace Civ.Client;

/// <summary>A mod resource as a UI shows it: the pool it is stored in (null for a flow) and its per-turn change.</summary>
public sealed record ResourceRow(string Id, string Name, string Color, bool CityScoped, long? Stored, long PerTurn);

/// <summary>An institution present in a city. <see cref="IsHome"/>: the city it was founded in (holy city, headquarters).</summary>
public sealed record InstitutionRow(string Id, string Name, string Category, string Color, bool IsHome);

/// <summary>Something the player could do with an institution (found it here, spread it with this unit). <see cref="Blocked"/> says why not, or is null.</summary>
public sealed record InstitutionAction(string Id, string Name, string Category, string Cost, string? Blocked);

/// <summary>Engine-free views of mod resources and institutions, shared by every renderer and the tests.</summary>
public static class ResourceView
{
    /// <summary>The player's own pools and flows (player-scoped resources) that are non-zero or changing.</summary>
    public static IReadOnlyList<ResourceRow> Player(Simulation sim, int playerId)
    {
        var rates = Resources.PlayerRates(sim, playerId);
        var pools = sim.State.PlayerById(playerId).Resources;
        return sim.Content.Resources.All
            .Where(r => !Resources.IsCityScoped(r))
            .Select(r => new ResourceRow(r.Id, r.Name, r.Color, false, r.Stockpile ? pools.GetValueOrDefault(r.Id) : null, rates.GetValueOrDefault(r.Id)))
            .Where(r => (r.Stored ?? 0) != 0 || r.PerTurn != 0)
            .ToList();
    }

    /// <summary>Everything this city adds or keeps: its own pools (city-scoped) and its contribution to the owner's resources.</summary>
    public static IReadOnlyList<ResourceRow> City(Simulation sim, City city)
    {
        var rates = Resources.CityRates(sim, city);
        return sim.Content.Resources.All
            .Select(r =>
            {
                bool local = Resources.IsCityScoped(r);
                return new ResourceRow(r.Id, r.Name, r.Color, local, local ? city.Resources.GetValueOrDefault(r.Id) : null, rates.GetValueOrDefault(r.Id));
            })
            .Where(r => (r.Stored ?? 0) != 0 || r.PerTurn != 0)
            .ToList();
    }

    public static IReadOnlyList<InstitutionRow> Institutions(Simulation sim, City city) =>
        city.Institutions.Select(id =>
        {
            var d = sim.Content.Institutions.Get(id);
            bool home = sim.State.Institutions.TryGetValue(id, out var f) && f.HomeCity == city.Id;
            return new InstitutionRow(id, d.Name, d.Category, d.Color, home);
        }).ToList();

    /// <summary>Institutions not founded yet whose tech the player knows, with why each can't be founded in this city right now.</summary>
    public static IReadOnlyList<InstitutionAction> Foundable(Simulation sim, int playerId, City city)
    {
        var techs = sim.State.PlayerById(playerId).Techs;
        return sim.Content.Institutions.All
            .Where(d => !Game.Institutions.IsFounded(sim, d.Id) && (d.RequiresTech == null || techs.Contains(d.RequiresTech)))
            .Select(d => new InstitutionAction(d.Id, d.Name, d.Category, Resources.CostText(sim.Content, d.FoundCost),
                Game.Institutions.CanFound(sim, playerId, city, d.Id)))
            .ToList();
    }

    /// <summary>Institutions this unit type can carry that the player has somewhere, with why it can't spread each one here.</summary>
    public static IReadOnlyList<InstitutionAction> Spreadable(Simulation sim, int playerId, Unit unit) =>
        sim.Content.Institutions.All
            .Where(d => d.Spreaders.Contains(unit.Type) && Game.Institutions.PlayerHas(sim, playerId, d.Id))
            .Select(d => new InstitutionAction(d.Id, d.Name, d.Category, Resources.CostText(sim.Content, d.SpreadCost),
                Game.Institutions.CanSpreadWith(sim, playerId, unit, d.Id)))
            .ToList();

    /// <summary>"12 (+3/turn)", or "+3/turn" for a flow.</summary>
    public static string Amount(ResourceRow r) =>
        r.Stored is long s ? $"{s} ({r.PerTurn:+#;-#;0}/turn)" : $"{r.PerTurn:+#;-#;0}/turn";
}
