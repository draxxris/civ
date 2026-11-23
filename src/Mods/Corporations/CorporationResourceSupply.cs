using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Corporations;

/// <summary>The resources corporation branches produce for their city (<see cref="CorporationDef.BonusProduced"/>), as
/// the source counts them: an active branch produces one copy for each copy of its consumed resources the city has, or
/// one when it consumes none, once the city's team knows the produced resource's trade technology and while that
/// resource is not obsolete for it. A produced copy may feed another branch that consumes it (the Masons' marble feeds
/// Creative Constructions), so the copies are summed to a fixed point, at most one pass per producing branch, as the
/// source iterates. The copies stay in the city: <see cref="ResourceAccess"/> never offers them for trade.</summary>
internal sealed class CorporationResourceSupply(CorporationService service) : ICityResourceSupply
{
    public IEnumerable<KeyValuePair<string, long>> Supply(Simulation sim, City city,
        Func<IReadOnlyCollection<string>, IReadOnlyDictionary<string, long>> networkCounts)
    {
        var producers = new List<CorporationDef>();
        foreach (var id in service.Present(sim, city.Id))
        {
            var def = sim.Content.Get<CorporationDef>(id);
            // Not IsActive: that counts the city's resources, which is what is being answered here. A branch without its
            // consumed resource produces no copy anyway (copies below is zero).
            if (def.BonusProduced is { } bonus && Producible(sim, city.Owner, bonus) && service.IsOperating(sim, city.Id, id)) producers.Add(def);
        }
        if (producers.Count == 0) return [];
        var consumed = producers.SelectMany(p => p.PrereqBonuses).Distinct(StringComparer.Ordinal).ToArray();
        var network = networkCounts(consumed);
        var produced = new SortedDictionary<string, long>(StringComparer.Ordinal);
        for (int pass = 0; pass < producers.Count; pass++)
        {
            var next = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (var def in producers)
            {
                long copies = def.PrereqBonuses.Length == 0 ? 1
                    : def.PrereqBonuses.Distinct(StringComparer.Ordinal).Sum(b => Math.Max(0, network.GetValueOrDefault(b)) + produced.GetValueOrDefault(b));
                if (copies > 0) next[def.BonusProduced!] = next.GetValueOrDefault(def.BonusProduced!) + copies;
            }
            bool settled = next.Count == produced.Count && next.All(p => produced.GetValueOrDefault(p.Key) == p.Value);
            produced = next;
            if (settled) break;
        }
        return produced;
    }

    /// <summary>Whether the player's team may use the resource: it knows the resource's trade technology and not the one
    /// that makes it obsolete.</summary>
    private static bool Producible(Simulation sim, int player, string bonus)
    {
        var def = sim.Content.Get<BonusDef>(bonus);
        return (def.TradeTech is not { } trade || sim.KnowsTech(player, trade)) && (def.ObsoleteTech is not { } obsolete || !sim.KnowsTech(player, obsolete));
    }
}
