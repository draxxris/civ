using Civ.Content;

namespace Civ.ContentImport;

/// <summary>Preserves prerequisite grouping and scopes. Empty sentinel-only lists add no constraint.</summary>
internal sealed class PrerequisiteMapper(ContentMapper mapper)
{
    public Requirement? Map(EffectiveRecord record, IEnumerable<RequirementMapping> mappings, HashSet<string> mapped)
    {
        var clauses = new List<Requirement>();
        var promotionAlternatives = new List<Requirement>();
        foreach (var mapping in mappings)
        {
            var node = record.Node.Child(mapping.Source);
            if (node is null) continue;
            var items = Entries(record, node, mapping).ToArray();
            ContentMapper.Mark(node, mapping.Source, mapped);
            if (items.Length == 0) continue;
            if (mapping.Mode == "promotionAny") { promotionAlternatives.AddRange(items); continue; }
            clauses.Add(mapping.Mode switch
            {
                "any" => new Requirement.Any(items),
                "not" => new Requirement.All(items.Select(r => (Requirement)new Requirement.Not(r)).ToArray()),
                _ => new Requirement.All(items)
            });
        }
        if (promotionAlternatives.Count > 0) clauses.Add(new Requirement.Any(promotionAlternatives));
        AddConditions(record, clauses, mapped);
        return clauses.Count == 0 ? null : new Requirement.All(clauses);
    }

    private IEnumerable<Requirement> Entries(EffectiveRecord record, DataNode node, RequirementMapping mapping)
    {
        if (node.Text is { } scalar)
        {
            if (ContentMapper.Present(scalar)) yield return Leaf(record, scalar, mapping, null);
            yield break;
        }
        foreach (var entry in node.Children)
        {
            var requirement = Entry(record, entry, mapping);
            if (requirement is not null) yield return requirement;
        }
    }

    private Requirement? Entry(EffectiveRecord record, DataNode entry, RequirementMapping mapping)
    {
        var symbol = entry.Text ?? entry.Children.FirstOrDefault()?.Text;
        if (!ContentMapper.Present(symbol)) return null;
        var amount = entry.Children.Length > 1 ? entry.Children[1].Text : null;
        if (amount is not null && ContentMapper.Integer(amount) == 0) return null;
        var leaf = Leaf(record, symbol!, mapping, amount is null ? null : ContentMapper.Integer(amount));
        return mapping.Mode == "overrides" ? WithOverrides(record, entry, leaf) : leaf;
    }

    private Requirement WithOverrides(EffectiveRecord record, DataNode entry, Requirement leaf)
    {
        var options = new List<Requirement> { leaf };
        var tech = entry.Value("TechOverride");
        if (ContentMapper.Present(tech)) options.Add(new Requirement.Leaf("tech", mapper.RequiredReference("TechInfo", tech, record, "TechOverride")));
        var era = entry.Value("EraOverride");
        if (ContentMapper.Present(era)) options.Add(new Requirement.Leaf("era", mapper.RequiredReference("EraInfo", era, record, "EraOverride")));
        return options.Count == 1 ? leaf : new Requirement.Any(options);
    }

    private Requirement.Leaf Leaf(EffectiveRecord record, string symbol, RequirementMapping mapping, int? count) =>
        new(mapping.Kind, mapper.RequiredReference(mapping.Target, symbol, record, mapping.Source), count, mapping.Scope);

    private void AddConditions(EffectiveRecord record, List<Requirement> clauses, HashSet<string> mapped)
    {
        if (record.Class is not ("BuildingInfo" or "UnitInfo")) return;
        foreach (var (field, condition) in Flags)
        {
            if (!ContentMapper.Boolean(record.Node.Value(field))) continue;
            clauses.Add(Condition(condition));
            ContentMapper.Mark(record.Node.Child(field), field, mapped);
        }
        foreach (var (field, condition) in Minimums)
        {
            var count = ContentMapper.Integer(record.Node.Value(field));
            if (count <= 0) continue;
            clauses.Add(Condition(condition, count));
            ContentMapper.Mark(record.Node.Child(field), field, mapped);
        }
        if (record.Class == "BuildingInfo") AddBuildingLocation(record, clauses, mapped);
    }

    private void AddBuildingLocation(EffectiveRecord record, List<Requirement> clauses, HashSet<string> mapped)
    {
        var water = ContentMapper.Boolean(record.Node.Value("bWater"));
        var river = ContentMapper.Boolean(record.Node.Value("bRiver"));
        var area = ContentMapper.Integer(record.Node.Value("iMinAreaSize"));
        var location = new List<Requirement>();
        if (water) location.Add(Condition("coastal", area > 0 ? area : null));
        if (river) location.Add(Condition("river"));
        // The native location predicate permits either coast or river when both flags are set.
        if (location.Count > 0) clauses.Add(new Requirement.Any(location));
        if (!water && area > 0) clauses.Add(Condition("minimum_area", area));
        if (ContentMapper.Boolean(record.Node.Value("bFreshWater"))) clauses.Add(Condition("fresh_water"));
        foreach (var field in new[] { "bWater", "bRiver", "bFreshWater", "iMinAreaSize" }) ContentMapper.Mark(record.Node.Child(field), field, mapped);
    }

    private Requirement Condition(string name, int? count = null)
    {
        mapper.Conditions.Add(name);
        return new Requirement.Leaf("condition", "foundation:" + name, count);
    }

    private static readonly (string Field, string Condition)[] Flags =
    [
        ("bPrereqPower", "power"), ("bStateReligion", "state_religion"),
        ("bPrereqReligion", "any_religion"), ("bPrereqBonuses", "resource_area")
    ];
    private static readonly (string Field, string Condition)[] Minimums =
    [
        ("iPrereqPopulation", "population"), ("iCitiesPrereq", "city_count"),
        ("iTeamsPrereq", "team_count"), ("iLevelPrereq", "unit_level")
    ];
}
