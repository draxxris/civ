using Civ.Content;
using S = Civ.Content.Schema;

namespace Civ.Client;

/// <summary>One thing a technology makes available: its category (for example "Buildings"), content id and name.</summary>
public sealed record TechUnlock(string Category, string Id, string Name);

public sealed partial class GameSession
{
    private Dictionary<string, List<TechUnlock>>? _techUnlocks;

    /// <summary>What researching the technology makes available, in category then name order. Built once per ruleset.</summary>
    public IReadOnlyList<TechUnlock> UnlocksOf(string techId)
    {
        _techUnlocks ??= BuildTechUnlocks();
        return _techUnlocks.TryGetValue(techId, out var list) ? list : [];
    }

    private Dictionary<string, List<TechUnlock>> BuildTechUnlocks()
    {
        var map = new Dictionary<string, List<TechUnlock>>(StringComparer.Ordinal);
        void Add(string category, ContentDef def, string? explicitTech)
        {
            var techs = new HashSet<string>(StringComparer.Ordinal);
            if (explicitTech != null) techs.Add(explicitTech);
            CollectTechs(def.Requirements, techs);
            foreach (var tech in techs)
            {
                if (!map.TryGetValue(tech, out var list)) map[tech] = list = [];
                list.Add(new(category, def.Id, def.Name));
            }
        }
        void Scan<T>(string category, Func<T, string?> tech) where T : ContentDef
        {
            try { foreach (var def in Sim.Content.Get<T>().All) Add(category, def, tech(def)); }
            catch (KeyNotFoundException) { }
        }
        Scan<S.BuildingDef>("Buildings", _ => null);
        Scan<S.UnitDef>("Units", _ => null);
        Scan<S.ImprovementDef>("Improvements", d => d.PrereqTech);
        Scan<S.ProjectDef>("Projects", d => d.TechPrereq);
        Scan<S.CivicDef>("Civics", _ => null);
        Scan<S.ReligionDef>("Religions", d => d.FoundingTech);
        Scan<S.CorporationDef>("Corporations", d => d.FoundingTech);
        Scan<S.BonusDef>("Resources", d => d.RevealTech);
        Scan<S.BonusDef>("Resources", d => d.TradeTech);
        Scan<S.TechDef>("Technologies", _ => null);
        Scan<S.PromotionDef>("Promotions", _ => null);
        Scan<S.BuildDef>("Worker actions", _ => null);
        Scan<S.ProcessDef>("Processes", _ => null);
        foreach (var list in map.Values)
            list.Sort((a, b) => { int c = string.CompareOrdinal(a.Category, b.Category); return c != 0 ? c : string.CompareOrdinal(a.Name, b.Name); });
        return map;
    }

    /// <summary>Tech leaves that must hold; leaves under a Not forbid the thing rather than unlock it, so they are skipped.</summary>
    private static void CollectTechs(Requirement? requirement, HashSet<string> into)
    {
        switch (requirement)
        {
            case Requirement.Leaf { Kind: "tech" } leaf: into.Add(leaf.Id); break;
            case Requirement.All all: foreach (var item in all.Items) CollectTechs(item, into); break;
            case Requirement.Any any: foreach (var item in any.Items) CollectTechs(item, into); break;
        }
    }
}
