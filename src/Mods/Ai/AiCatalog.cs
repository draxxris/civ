using System.Runtime.CompilerServices;
using Civ.Content;
using Civ.Content.Schema;

namespace Civ.Mods.Ai;

/// <summary>What the AI uses a unit definition for.</summary>
internal enum UnitRole { Other, Settler, Worker, Explorer, Melee, Siege }

/// <summary>Immutable per-ruleset lookups derived from content. Safe to share between games.</summary>
internal sealed class AiCatalog
{
    private static readonly ConditionalWeakTable<ContentDb, AiCatalog> Cache = new();
    internal const string Explore = "military:unitai_explore", CityDefense = "military:unitai_city_defense", Attack = "military:unitai_attack";
    public IReadOnlyList<BuildingDef> Buildings { get; }
    public IReadOnlyList<UnitDef> Units { get; }
    public IReadOnlyList<TechDef> Techs { get; }
    public IReadOnlyDictionary<string, UnitRole> Roles { get; }
    public IReadOnlyDictionary<string, string[]> TechLeaves { get; }
    public IReadOnlyDictionary<string, List<string>> TechUnlocks { get; }
    public IReadOnlyList<BuildDef> WorkerBuilds { get; }

    public static AiCatalog For(ContentDb content) => Cache.GetValue(content, c => new AiCatalog(c));

    private AiCatalog(ContentDb content)
    {
        Buildings = content.Get<BuildingDef>().All.Where(b => b.Cost > 0).OrderBy(b => b.Id, StringComparer.Ordinal).ToArray();
        Units = content.Get<UnitDef>().All.Where(u => u.Cost > 0 || u.CanFoundCity).OrderBy(u => u.Id, StringComparer.Ordinal).ToArray();
        Techs = content.Get<TechDef>().All.Where(t => !t.Disable).OrderBy(t => t.Id, StringComparer.Ordinal).ToArray();
        Roles = content.Get<UnitDef>().All.ToDictionary(u => u.Id, Classify, StringComparer.Ordinal);
        WorkerBuilds = content.Get<BuildDef>().All.Where(b => b.ImprovementType is not null || b.RouteType is not null).OrderBy(b => b.Id, StringComparer.Ordinal).ToArray();
        var leaves = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var unlocks = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var def in Buildings.Cast<ContentDef>().Concat(Units)) Index(def, leaves, unlocks);
        TechLeaves = leaves; TechUnlocks = unlocks;
    }

    private static void Index(ContentDef def, Dictionary<string, string[]> leaves, Dictionary<string, List<string>> unlocks)
    {
        var techs = new List<string>();
        Collect(def.Requirements, techs);
        leaves[def.Id] = techs.ToArray();
        foreach (var tech in techs.Distinct(StringComparer.Ordinal))
        {
            if (!unlocks.TryGetValue(tech, out var list)) unlocks[tech] = list = new();
            list.Add(def.Id);
        }
    }

    private static void Collect(Requirement? r, List<string> techs)
    {
        switch (r)
        {
            case Requirement.Leaf { Kind: "tech" } leaf: techs.Add(leaf.Id); break;
            case Requirement.All all: foreach (var i in all.Items) Collect(i, techs); break;
            case Requirement.Any any: foreach (var i in any.Items) Collect(i, techs); break;
        }
    }

    private static UnitRole Classify(UnitDef u)
    {
        if (u.Domain != "military:domain_land") return UnitRole.Other;
        if (u.CanFoundCity) return UnitRole.Settler;
        if (u.WorkRate > 0 && u.Builds.Count > 0) return UnitRole.Worker;
        if (u.Strength <= 0) return UnitRole.Other;
        if (u.UnitAiTypes.ContainsKey(Explore) && u.Strength <= 2) return UnitRole.Explorer;
        if (IsSiege(u)) return UnitRole.Siege;
        return UnitRole.Melee;
    }

    private static bool IsSiege(UnitDef u) => u.CollateralDamage > 0
        || u.CityAttack >= 40 && u.Strength >= 4 && !u.UnitAiTypes.ContainsKey(CityDefense);
}
