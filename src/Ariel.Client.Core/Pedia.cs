using Ariel.Content;
using S = Ariel.Content.Schema;

namespace Ariel.Client;

/// <summary>One article in the pedia, with text already resolved from the loaded content.</summary>
public sealed record PediaEntry(
    string Category, string Id, string Title, string Help, string Strategy, string Pedia, string Icon,
    int? Cost, IReadOnlyList<string> Prerequisites)
{
    internal bool Matches(string text) =>
        Title.Contains(text, StringComparison.OrdinalIgnoreCase) || Help.Contains(text, StringComparison.OrdinalIgnoreCase)
        || Strategy.Contains(text, StringComparison.OrdinalIgnoreCase) || Pedia.Contains(text, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Index of every encyclopedia-worthy definition in the loaded content, grouped by category.</summary>
public sealed class PediaCatalog
{
    private readonly List<PediaEntry> _entries = [];

    public PediaCatalog(ContentDb content)
    {
        Add<S.TechDef>(content, "Technologies", d => d.Cost, _ => null);
        Add<S.BuildingDef>(content, "Buildings", d => d.Cost, _ => null);
        Add<S.UnitDef>(content, "Units", d => d.Cost, _ => null);
        Add<S.PromotionDef>(content, "Promotions", _ => 0, d => [Tech(content, d.ObsoleteTech, "Obsolete with")]);
        Add<S.CivicDef>(content, "Civics", _ => 0, _ => null);
        Add<S.ReligionDef>(content, "Religions", _ => 0, d => [Tech(content, d.FoundingTech, "Founded by")]);
        Add<S.BonusDef>(content, "Resources", _ => 0, d => [Tech(content, d.RevealTech, "Revealed by"), Tech(content, d.TradeTech, "Traded with")]);
        Add<S.ImprovementDef>(content, "Improvements", _ => 0, d => [Tech(content, d.PrereqTech, "Requires")]);
        Add<S.TerrainDef>(content, "Terrain", _ => 0, _ => null);
        Add<S.FeatureDef>(content, "Features", _ => 0, _ => null);
        Add<S.SpecialistDef>(content, "Specialists", _ => 0, _ => null);
        Add<S.CivilizationDef>(content, "Civilizations", _ => 0, _ => null);
        Add<S.LeaderDef>(content, "Leaders", _ => 0, _ => null);
        Add<S.TraitDef>(content, "Traits", _ => 0, _ => null);
        AddIfRegistered<S.CorporationDef>(content, "Corporations", _ => 0, d => [Tech(content, d.FoundingTech, "Founded with")]);
        AddIfRegistered<S.ProjectDef>(content, "Projects", _ => 0, _ => null);
        AddIfRegistered<S.VictoryDef>(content, "Victories", _ => 0, _ => null);
        AddIfRegistered<S.EventTriggerDef>(content, "Events", _ => 0, d => [Tech(content, d.RequiredTech, "Requires")]);
        Categories = _entries.Select(e => e.Category).Distinct().ToList();
    }

    public IReadOnlyList<string> Categories { get; }
    public IReadOnlyList<PediaEntry> All => _entries;

    public PediaEntry? Find(string id) => _entries.FirstOrDefault(e => e.Id == id);

    /// <summary>Entries of one category (or all) whose title or text contains the query, in title order.</summary>
    public IReadOnlyList<PediaEntry> Search(string? category = null, string? query = null)
    {
        var q = query?.Trim() ?? "";
        return _entries.Where(e => category == null || e.Category == category)
            .Where(e => q.Length == 0 || e.Matches(q))
            .OrderBy(e => e.Title, StringComparer.Ordinal).ThenBy(e => e.Id, StringComparer.Ordinal).ToList();
    }

    private void AddIfRegistered<T>(ContentDb content, string category, Func<T, int> cost, Func<T, string?[]?> extra) where T : S.GameContentDef
    {
        try { _ = content.Get<T>(); }
        catch (KeyNotFoundException) { return; }
        Add(content, category, cost, extra);
    }

    private void Add<T>(ContentDb content, string category, Func<T, int> cost, Func<T, string?[]?> extra) where T : S.GameContentDef
    {
        foreach (var def in content.Get<T>().All.OrderBy(d => d.Name, StringComparer.Ordinal).ThenBy(d => d.Id, StringComparer.Ordinal))
        {
            var prereqs = new List<string>();
            Describe(content, def.Requirements, prereqs);
            prereqs.AddRange((extra(def) ?? []).Where(p => p != null)!);
            int price = cost(def);
            _entries.Add(new(category, def.Id, def.Name, def.Help, def.Strategy, def.Pedia, def.Icon, price > 0 ? price : null, prereqs));
        }
    }

    private static string? Tech(ContentDb content, string? id, string verb) =>
        id == null ? null : $"{verb} {(content.TryGet<S.TechDef>(id, out var t) ? t.Name : Labels.Humanize(id))}";

    private static void Describe(ContentDb content, Requirement? requirement, List<string> into)
    {
        switch (requirement)
        {
            case Requirement.Leaf leaf:
                into.Add(leaf.Kind == "tech" && content.TryGet<S.TechDef>(leaf.Id, out var tech) ? tech.Name
                    : $"{Labels.Humanize(leaf.Kind)} {Labels.Humanize(leaf.Id)}");
                break;
            case Requirement.All all: foreach (var item in all.Items) Describe(content, item, into); break;
            case Requirement.Any any:
                var options = new List<string>();
                foreach (var item in any.Items) Describe(content, item, options);
                if (options.Count > 0) into.Add(string.Join(" or ", options));
                break;
            case Requirement.Not not:
                var inner = new List<string>();
                Describe(content, not.Item, inner);
                into.AddRange(inner.Select(i => "Not " + i));
                break;
        }
    }
}

public sealed partial class GameSession
{
    private PediaCatalog? _pedia;

    /// <summary>The pedia index, built once per loaded ruleset.</summary>
    public PediaCatalog Pedia => _pedia ??= new(Sim.Content);
}
