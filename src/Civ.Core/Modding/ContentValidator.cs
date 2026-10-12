using Civ.Content;

namespace Civ.Modding;

/// <summary>Cross-mod reference checks run once every mod has registered, so a typo in sidecar data fails at load, not mid-game.</summary>
internal static class ContentValidator
{
    public static void Validate(ContentDb db)
    {
        foreach (var r in db.Resources.All)
        {
            if (r.Scope != ResourceScopes.Player && r.Scope != ResourceScopes.City)
                throw Bad($"resource '{r.Id}' has scope '{r.Scope}' (expected '{ResourceScopes.Player}' or '{ResourceScopes.City}')");
            if (!r.Stockpile && r.Scope != ResourceScopes.Player)
                throw Bad($"resource '{r.Id}' is a flow (stockpile false) so it must be player-scoped");
            foreach (var rate in r.Rates)
            {
                if (!ResourceRate.Kinds.Contains(rate.From))
                    throw Bad($"resource '{r.Id}' has a rate from '{rate.From}' (expected one of {string.Join(", ", ResourceRate.Kinds)})");
                bool known = rate.From switch
                {
                    "building" => db.Buildings.Contains(rate.Id ?? ""),
                    "specialist" => db.Specialists.Contains(rate.Id ?? ""),
                    "civic" => db.Civics.Contains(rate.Id ?? ""),
                    "terrain" => db.Terrains.Contains(rate.Id ?? ""),
                    "institution" => db.Institutions.Contains(rate.Id ?? ""),
                    _ => true,
                };
                if (!known) throw Bad($"resource '{r.Id}' has a rate from unknown {rate.From} '{rate.Id}'");
            }
        }

        foreach (var t in db.Techs.All) Costs(db, t.ResourceCost, $"tech '{t.Id}'");
        foreach (var b in db.Buildings.All)
        {
            Costs(db, b.ResourceCost, $"building '{b.Id}'");
            Institution(db, b.RequiresInstitution, $"building '{b.Id}'");
        }
        foreach (var u in db.Units.All)
        {
            Costs(db, u.ResourceCost, $"unit '{u.Id}'");
            Institution(db, u.RequiresInstitution, $"unit '{u.Id}'");
        }
        foreach (var i in db.Institutions.All)
        {
            var what = $"institution '{i.Id}'";
            Costs(db, i.FoundCost, what);
            Costs(db, i.SpreadCost, what);
            if (i.RequiresTech != null && !db.Techs.Contains(i.RequiresTech)) throw Bad($"{what} requires unknown tech '{i.RequiresTech}'");
            if (i.RequiresBuilding != null && !db.Buildings.Contains(i.RequiresBuilding)) throw Bad($"{what} requires unknown building '{i.RequiresBuilding}'");
            foreach (var u in i.Spreaders) if (!db.Units.Contains(u)) throw Bad($"{what} is spread by unknown unit '{u}'");
            foreach (var c in i.Conflicts) if (!db.Institutions.Contains(c)) throw Bad($"{what} conflicts with unknown institution '{c}'");
        }
    }

    private static void Costs(ContentDb db, Dictionary<string, long> costs, string what)
    {
        foreach (var (id, amount) in costs)
        {
            if (!db.Resources.TryGet(id, out var r)) throw Bad($"{what} costs unknown resource '{id}'");
            if (!r.Stockpile) throw Bad($"{what} costs '{id}', which is a flow and cannot be spent");
            if (amount < 0) throw Bad($"{what} has a negative cost of '{id}'");
        }
    }

    private static void Institution(ContentDb db, string? id, string what)
    {
        if (id != null && !db.Institutions.Contains(id)) throw Bad($"{what} requires unknown institution '{id}'");
    }

    private static InvalidOperationException Bad(string message) => new(message);
}
