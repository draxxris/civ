using Ariel.Content;

namespace Ariel.ContentImport;

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
            if (mapping.Mode == "not") items = Forbidden(record, mapping, items);
            if (items.Length == 0) continue;
            if (mapping.Mode == "promotionAny") { promotionAlternatives.AddRange(items); continue; }
            clauses.Add(mapping.Mode switch
            {
                "any" => Alternatives(record, mapping, items),
                "not" => new Requirement.All(items.Select(r => (Requirement)new Requirement.Not(r)).ToArray()),
                _ => new Requirement.All(items)
            });
        }
        if (promotionAlternatives.Count > 0) clauses.Add(new Requirement.Any(promotionAlternatives));
        // A civic a mod owns forbids the base civics that forbid it, which their rows may not say.
        foreach (var mapping in mappings.Where(m => m.Mode == "not" && m.Kind == "civic" && record.Node.Child(m.Source) is null))
            if (mapper.ForbiddenBy(record, mapping).ToArray() is { Length: > 0 } mirrored)
                clauses.Add(new Requirement.All(mirrored.Select(r => (Requirement)new Requirement.Not(r)).ToArray()));
        AddConditions(record, clauses, mapped);
        return clauses.Count == 0 ? null : new Requirement.All(clauses);
    }

    /// <summary>An alternative that names a record a feature mod owns, which this row may not name (a base building that
    /// accepts a religious wonder in place of a palace), is left out; the other alternatives still apply. The owner's
    /// amending row carries it beside the first kept alternative (<see cref="OwnedAlternatives"/>) when its mod maps the
    /// field with kind <c>alternatives</c>; otherwise it is lost, and reported.</summary>
    private Requirement Alternatives(EffectiveRecord record, RequirementMapping mapping, Requirement[] items)
    {
        var kept = items.Where(i => i is not Requirement.Leaf leaf || mapper.MayName(leaf.Id)).ToArray();
        foreach (var dropped in items.Except(kept).Cast<Requirement.Leaf>())
            if (kept.FirstOrDefault() is not Requirement.Leaf || !mapper.CarriesAlternatives(record, mapping.Source, dropped.Id))
                mapper.NoteUnnamedAlternative(record, mapping.Source, dropped.Id);
        return new Requirement.Any(kept);
    }

    /// <summary>A forbidden record this row may not name is left out. Civics forbid each other both ways, as the government
    /// validates every civic of a cabinet, so the owner's civic forbids this one instead (<see cref="ContentMapper.ForbiddenBy"/>);
    /// anything else is reported. The civic's own forbidden list gains this one unless it names it already.</summary>
    private Requirement[] Forbidden(EffectiveRecord record, RequirementMapping mapping, Requirement[] items)
    {
        var kept = items.Where(i => i is not Requirement.Leaf leaf || mapper.MayName(leaf.Id)).ToList();
        foreach (var dropped in items.Except(kept).Cast<Requirement.Leaf>())
            if (mapping.Kind != "civic") mapper.NoteUnnamedAlternative(record, mapping.Source, dropped.Id);
        if (mapping.Kind == "civic")
            foreach (var mirrored in mapper.ForbiddenBy(record, mapping))
                if (!kept.OfType<Requirement.Leaf>().Any(l => l.Id == mirrored.Id)) kept.Add(mirrored);
        return kept.ToArray();
    }

    /// <summary>For an amending row: the alternatives of the record's <paramref name="source"/> group that name the
    /// emitting mod's own records, keyed by the first alternative the base row keeps, or null when there are none or the
    /// base row keeps no leaf to address the group by.</summary>
    public Dictionary<string, Requirement[]>? OwnedAlternatives(EffectiveRecord record, string source)
    {
        var mapping = SchemaCatalog.Definitions.Where(d => d.SourceClass == record.Class && !d.IsAttachment)
            .SelectMany(d => d.Requirements).FirstOrDefault(r => r.Source == source && r.Mode == "any");
        if (mapping is null || record.Node.Child(source) is not { } node) return null;
        var items = Entries(record, node, mapping).ToArray();
        var owned = items.Where(i => i is Requirement.Leaf leaf && mapper.Owns(leaf.Id)).ToArray();
        if (owned.Length == 0 || items.FirstOrDefault(i => i is Requirement.Leaf leaf && mapper.Claimant(leaf.Id) is null) is not Requirement.Leaf beside)
            return null;
        return new() { [beside.Id] = owned };
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

    private Requirement.Leaf Leaf(EffectiveRecord record, string symbol, RequirementMapping mapping, int? count)
    {
        // Needing a corporation's headquarters building means being that corporation's headquarters (an executive is
        // trained there). Name the corporation and let its rules say where it is based: the city that completed the
        // headquarters founded it, and a city that captured that city has taken its place.
        if (mapping.Kind == "building" && mapper.FoundedCorporation(symbol) is { } corporation)
            return new("corporation", corporation, count, HeadquartersParam);
        return new(mapping.Kind, mapper.RequiredReference(mapping.Target, symbol, record, mapping.Source), count, mapping.Scope);
    }

    /// <summary>The <c>corporation</c> leaf parameter for "the city is its headquarters", as 2 is a religion's holy city.</summary>
    internal const int HeadquartersParam = 2;

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
        if (ReligionConditions.Contains(name))
        {
            mapper.ReligionConditions.Add(name);
            return new Requirement.Leaf("religion-condition", "religion:" + name, count);
        }
        mapper.Conditions.Add(name);
        return new Requirement.Leaf("condition", "foundation:" + name, count);
    }
    /// <summary>The conditions about religion are the Religion mod's own leaf kind (<c>religion-condition</c>), so a base
    /// row never names one: only the buildings and units Religion owns carry the flags that need them.</summary>
    private static readonly HashSet<string> ReligionConditions = new(["state_religion", "any_religion"], StringComparer.Ordinal);

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
