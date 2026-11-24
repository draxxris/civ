using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.ContentImport;

public sealed record DataIssue(string Record, string Field, string SourceValue, string Resolution);
/// <summary>One emitted definition. <see cref="Mod"/> is the mod whose folder it is written to: the mapping's, unless the
/// record belongs to a mod that owns it outright (<see cref="IdMapping.RecordOwners"/>), which then emits every row about it.
/// <see cref="Subfolder"/> is the folder under the mod's <c>data</c> a row goes to when the mod registers it only with
/// another mod (<see cref="IdMapping.OptionalProviders"/>).</summary>
public sealed record MappedDefinition(DefinitionMapping Mapping, ContentDef Definition, string? Emitter = null, string? Subfolder = null)
{
    public string Mod => Emitter ?? Mapping.Mod;
    /// <summary>The folder under <c>data</c>, empty for the folder itself.</summary>
    public string Folder => Subfolder ?? "";
}

/// <summary>Compiles merged trees into immutable Core definitions. No source table is read at game startup.</summary>
public sealed partial class ContentMapper
{
    /// <summary>The source's native commerce enum, in its index order: a native commerce array reads in this order.</summary>
    private static readonly string[] NativeCommerce = ["gold", "research", "culture", "espionage"];
    /// <summary>The commerce types the base game has. A native array's other slots belong to the mod that owns that commerce
    /// type (<see cref="IdMapping.RecordOwners"/>): only its own rows and its amending rows carry them.</summary>
    private static readonly string[] BaseCommerce = ["gold", "research", "culture"];
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true,
        WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private readonly Func<string, string?> lookupText;
    private readonly IReadOnlyDictionary<string, DefineValue> defines;
    private readonly Dictionary<(string Class, string Type), EffectiveRecord> records;
    private readonly Dictionary<(string Class, string Type), string> ids;
    private readonly Dictionary<string, IdEntry> symbols;
    /// <summary>The records a feature mod owns outright (<see cref="IdMapping.RecordOwners"/>), by id, with that mod.</summary>
    private readonly Dictionary<string, string> claimed;
    /// <summary>The native commerce types a mod owns (<c>espionage</c>), by key, with that mod.</summary>
    private readonly Dictionary<string, string> commerceOwners;
    /// <summary>The (class, source field, commerce key) triples an amending row carries (<see cref="FieldMapping.Part"/>): a
    /// base row's amount of a commerce type it cannot name is lost unless one of these keeps it.</summary>
    private readonly HashSet<(string Class, string Source, string Part)> amended = SchemaCatalog.Definitions
        .SelectMany(d => d.Fields.Where(f => f.Part is not null).Select(f => (d.SourceClass, f.Source, f.Part!))).ToHashSet();
    /// <summary>The mod the row being mapped is written for. Only it may name its own claimed records: a row of any other
    /// mod leaves out a map entry that names one, since that mod cannot depend on the owner.</summary>
    private string emitter = "";
    // Native roles absent from the documentary XML table. Verified in the source enum and native registration.
    public static readonly string[] NativeUnitAiRoles = ["UNITAI_PILLAGE_COUNTER", "UNITAI_SUBDUED_ANIMAL", "UNITAI_HUNTER", "UNITAI_DIPLOMAT"];
    public List<DataIssue> Issues { get; } = [];
    public Dictionary<string, HashSet<string>> MappedPaths { get; } = new(StringComparer.Ordinal);
    public SortedSet<string> Conditions { get; } = new(StringComparer.Ordinal);

public ContentMapper(IReadOnlyList<EffectiveRecord> effectiveRecords, Func<string, string?> lookupText,
        IReadOnlyDictionary<string, DefineValue>? defines = null)
    {
        this.lookupText = lookupText;
        this.defines = defines ?? new Dictionary<string, DefineValue>(StringComparer.Ordinal);
        records = effectiveRecords.ToDictionary(r => (r.Class, r.Type));
        var idEntries = IdMapping.Create(effectiveRecords);
        var all = new IdMapping.SourceRecords(effectiveRecords);
        ids = idEntries.ToDictionary(r => (r.Class, r.SourceType), r => r.Id);
        foreach (var role in NativeUnitAiRoles)
        {
            if (records.ContainsKey(("UnitAIInfo", role))) continue;
            records[("UnitAIInfo", role)] = new("UnitAIInfo", role, DataNode.Branch("UnitAIInfo", [DataNode.Leaf("Type", role), DataNode.Leaf("Description", Humanize(role[7..]))]), ["native-enum"]);
            ids[("UnitAIInfo", role)] = IdMapping.Id("UnitAIInfo", role);
        }
        symbols = idEntries.GroupBy(r => r.SourceType, StringComparer.Ordinal).Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.Single(), StringComparer.OrdinalIgnoreCase);
        claimed = effectiveRecords.Select(r => (Record: r, Owner: IdMapping.Claimant(r, all))).Where(r => r.Owner is not null)
            .ToDictionary(r => ids[(r.Record.Class, r.Record.Type)], r => r.Owner!, StringComparer.Ordinal);
        commerceOwners = effectiveRecords.Where(r => IdMapping.CanonicalClass(r.Class) == "CommerceInfo" && claimed.ContainsKey(ids[(r.Class, r.Type)]))
            .ToDictionary(r => NativeKey(r.Type), r => claimed[ids[(r.Class, r.Type)]], StringComparer.Ordinal);
    }

    /// <summary>The mod that owns this id outright, or null for a record of its class's module.</summary>
    public string? Claimant(string id) => claimed.GetValueOrDefault(id);
    /// <summary>Whether the row being mapped may name the record: one a mod owns outright only from that mod's own rows,
    /// and with <paramref name="owned"/> only those (a mod's attachment that lists just its own records).</summary>
    private bool Names(string id, bool owned, bool amending = false) =>
        claimed.TryGetValue(id, out var owner) ? owner == emitter && !amending : !owned;
    /// <summary>The mod that writes the rows about the record: the one owning it outright (<see cref="IdMapping.Emitter"/>),
    /// else the mapping's.</summary>
    private string Emitter(EffectiveRecord record, DefinitionMapping mapping) =>
        IdMapping.Emitter(claimed.GetValueOrDefault(ids[(record.Class, record.Type)]), mapping.Mod);
    /// <summary>The folder under the emitter's <c>data</c> the row about the record goes to: the optional provider whose
    /// content or row type it is (<see cref="IdMapping.OptionalProviders"/>), else none.</summary>
    private string? Subfolder(EffectiveRecord record, DefinitionMapping mapping)
    {
        var owner = claimed.GetValueOrDefault(ids[(record.Class, record.Type)]);
        return IdMapping.Subfolder(emitter, mapping.TargetMod ?? (owner is not null && owner != emitter ? owner : mapping.Mod));
    }

    public IReadOnlyList<MappedDefinition> Map()
    {
        var result = new List<MappedDefinition>();
        foreach (var family in SchemaCatalog.Definitions.GroupBy(m => m.SourceClass).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var attachments = family.Where(m => m.IsAttachment).ToArray();
            foreach (var record in records.Values.Where(r => r.Class == family.Key).OrderBy(r => ids[(r.Class, r.Type)], StringComparer.Ordinal))
            {
                // A field a feature mod's attachment maps is mapped for the record, not unsupported on its primary definition.
                var attached = new HashSet<string>(StringComparer.Ordinal);
                foreach (var mapping in attachments)
                {
                    emitter = Emitter(record, mapping);
                    if (MapAttachment(record, mapping, attached) is { } row) result.Add(new(mapping, row, emitter, Subfolder(record, mapping)));
                }
                foreach (var mapping in family.Where(m => !m.IsAttachment))
                {
                    emitter = Emitter(record, mapping);
                    result.Add(new(mapping, MapRecord(record, mapping, attached), emitter, Subfolder(record, mapping)));
                }
            }
        }
        return result;
    }

    /// <summary>The attachment row a feature mod keeps about one record, or null when every field it maps is at its
    /// default, so a mod only lists the records its rules care about.</summary>
    private ContentAttachment? MapAttachment(EffectiveRecord record, DefinitionMapping mapping, HashSet<string> mapped)
    {
        var recordId = ids[(record.Class, record.Type)];
        var name = recordId[(recordId.IndexOf(':') + 1)..];
        var identity = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = emitter + ":" + name, ["target"] = (mapping.TargetMod ?? recordId[..recordId.IndexOf(':')]) + ":" + name,
        };
        var values = new Dictionary<string, object?>(identity, StringComparer.Ordinal);
        var effects = new List<ModifierEffect>();
        foreach (var field in mapping.Fields)
        {
            var node = record.Node.Child(field.Source);
            if (node is null) continue;
            // The mod's own record already carries its commerce type in its own row: an amendment would count it twice.
            if (field.Part is { } part && claimed.TryGetValue(recordId, out var owner) && commerceOwners.GetValueOrDefault(part) == owner) continue;
            // Nor does an amending field that lists the mod's own records, for a record the mod owns: its row names them.
            if (field.Owned && claimed.GetValueOrDefault(recordId) == emitter
                && mapping.DefinitionType.GetProperty(field.Property)?.IsDefined(typeof(AmendsTargetAttribute), true) == true) continue;
            var value = MapValue(record, node, field);
            Mark(node, field.Source, mapped);
            // Several source fields can fill one property: effects add up, and maps of different keys join.
            if (field.Kind.StartsWith("effect", StringComparison.Ordinal)) effects.AddRange((IEnumerable<ModifierEffect>)value!);
            else Assign(values, JsonNamingPolicy.CamelCase.ConvertName(field.Property), value);
        }
        if (mapping.Fields.Any(f => f.Kind.StartsWith("effect", StringComparison.Ordinal)))
            values["effects"] = CanonicalEffects(effects);
        if (!MappedPaths.TryGetValue(record.Class, out var paths)) MappedPaths[record.Class] = paths = new(StringComparer.Ordinal);
        paths.UnionWith(mapped);
        var row = (ContentAttachment)JsonSerializer.Deserialize(JsonSerializer.Serialize(values, Json), mapping.DefinitionType, Json)!;
        var empty = JsonSerializer.Deserialize(JsonSerializer.Serialize(identity, Json), mapping.DefinitionType, Json)!;
        return JsonSerializer.Serialize(row, mapping.DefinitionType, Json) == JsonSerializer.Serialize(empty, mapping.DefinitionType, Json) ? null : row;
    }

    /// <summary>A record's id in its family. A family a mod other than the record's owner emits (Religion's rules about
    /// the base game's religion identities) gets the id <c>mod:name</c> and names the identity it describes in
    /// <c>identity</c>, so the mod's rules and the base game's references stay in their own namespaces.</summary>
    private string RecordId(EffectiveRecord record, DefinitionMapping mapping)
    {
        string id = ids[(record.Class, record.Type)];
        int colon = id.IndexOf(':');
        if (id[..colon] == mapping.Mod || claimed.ContainsKey(id)) return id;
        if (mapping.DefinitionType.GetProperty("Identity") is null)
            throw new InvalidDataException($"{mapping.Definition} is emitted by {mapping.Mod} for {id} but has no Identity to name it by");
        return mapping.Mod + id[colon..];
    }

    /// <summary>The effects of a row: those that change something, in one order whichever source field gave them.</summary>
    private static ModifierEffect[] CanonicalEffects(IEnumerable<ModifierEffect> effects) => effects.Where(e => e.Amount != 0)
        .OrderBy(e => e.Key, StringComparer.Ordinal).ThenBy(e => e.Target ?? "", StringComparer.Ordinal).ToArray();

    /// <summary>Sets a row's property; a second field for the same property adds its entries (the maps the fields give are
    /// disjoint: an amending row's entries for records its mod owns, and its share of the rest).</summary>
    private static void Assign(Dictionary<string, object?> values, string property, object? value)
    {
        if (values.TryGetValue(property, out var existing) && existing is System.Collections.IDictionary map && value is System.Collections.IDictionary more)
            foreach (System.Collections.DictionaryEntry entry in more) map.Add(entry.Key, entry.Value);
        else values[property] = value;
    }

    private static bool IsCommerceKind(string kind) =>
        kind is "commerce" or "mapCommerce" or "buildingCommerce" or "effectCommerce" or "effectMapCommerce";

    /// <summary>Whether a vector of the row being mapped carries this commerce type: the base game's, or one the emitting mod owns.</summary>
    private bool Carries(string key) => BaseCommerce.Contains(key) || commerceOwners.GetValueOrDefault(key) == emitter;

    /// <summary>Whether a mapped value holds any non-zero amount.</summary>
    private static bool HasAmount(object? value) => value switch
    {
        null => false,
        CommerceVector vector => !vector.IsZero,
        ModifierEffect[] effects => effects.Any(e => e.Amount != 0),
        System.Collections.IDictionary map => map.Values.Cast<object?>().Any(HasAmount),
        _ => true,
    };

    /// <summary>A base row cannot carry a commerce type another mod owns, so its amount there is for an amending row. When no
    /// field of the schema catalog carries it, the amount is lost: say so instead of dropping it silently.</summary>
    /// <summary>Whether the row being mapped may name the record: not one a feature mod owns outright that the row's mod
    /// may not name.</summary>
    internal bool MayName(string id) => !claimed.TryGetValue(id, out var owner) || IdMapping.MayName(emitter, owner);
    /// <summary>A requirement alternative the row may not name is left out; say which, since the rest still apply.</summary>
    internal void NoteUnnamedAlternative(EffectiveRecord record, string field, string id) =>
        Issues.Add(new(ids[(record.Class, record.Type)], field, id,
            $"The alternative {id} is left out: {claimed[id]} owns it, and this row may not name it."));

    private void NoteDroppedCommerce(EffectiveRecord record, DataNode node, FieldMapping field)
    {
        if (!IsCommerceKind(field.Kind) || field.Part is not null) return;
        foreach (var key in NativeCommerce.Where(k => !Carries(k) && !amended.Contains((record.Class, field.Source, k))))
            if (HasAmount(MapValue(record, node, field with { Part = key })))
                Issues.Add(new(ids[(record.Class, record.Type)], field.Source, key,
                    $"The {key} amount is dropped: no amending row type carries this field for the mod that owns {key}."));
    }

    /// <summary>The (class, source field) pairs an amending row maps with <see cref="FieldMapping.Owned"/>: a single reference
    /// the row being mapped may not name is that row's to carry.</summary>
    private readonly HashSet<(string Class, string Source)> ownedReferences = SchemaCatalog.Definitions
        .SelectMany(d => d.Fields.Where(f => f.Owned).Select(f => (d.SourceClass, f.Source))).ToHashSet();
    /// <summary>A single reference, left out when it names a record a feature mod owns that the row may not name (the
    /// Shaolin Temple's faith promotion). The owner's amending row maps the same field with owned and carries it; without
    /// one the reference is lost, which the run reports.</summary>
    private string? NamedReference(EffectiveRecord record, DataNode node, FieldMapping field)
    {
        if (Reference(field.Target!, node.Text, record, field.Source) is not { } id) return null;
        if (Names(id, field.Owned)) return id;
        if (!field.Owned && !ownedReferences.Contains((record.Class, field.Source)))
            Issues.Add(new(ids[(record.Class, record.Type)], field.Source, id, $"The reference to {id} is dropped: {claimed[id]} owns it, and no amending row carries this field."));
        return null;
    }

    private object? MapValue(EffectiveRecord record, DataNode node, FieldMapping field)
    {
        try { return MapField(record, node, field); }
        catch (Exception e) when (e is FormatException or OverflowException or JsonException)
        { throw new InvalidDataException($"{record.Class}/{record.Type}/{field.Source}: {e.Message}", e); }
    }

    private GameContentDef MapRecord(EffectiveRecord record, DefinitionMapping mapping, IEnumerable<string> attached)
    {
        var mapped = new HashSet<string>(attached, StringComparer.Ordinal);
        foreach (var field in new[] { "Type", "Description", "Civilopedia", "Help", "Strategy", "Button", "Era" }) Mark(record.Node.Child(field), field, mapped);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = RecordId(record, mapping), ["name"] = Text(record.Node.Value("Description")),
            ["pedia"] = Text(record.Node.Value("Civilopedia")), ["help"] = Text(record.Node.Value("Help")),
            ["strategy"] = Text(record.Node.Value("Strategy")), ["icon"] = Icon(record),
            ["era"] = Reference("EraInfo", record.Era, record, "Era")
        };
        var effects = new List<ModifierEffect>();
        foreach (var field in mapping.Fields)
        {
            var node = record.Node.Child(field.Source);
            if (node is null) continue;
            var value = MapValue(record, node, field);
            if (field.Kind == "leafRef" && value is null) { Mark(node, field.Source, mapped); continue; }
            if (field.Kind.StartsWith("effect", StringComparison.Ordinal)) effects.AddRange((IEnumerable<ModifierEffect>)value!);
            else values[JsonNamingPolicy.CamelCase.ConvertName(field.Property)] = value;
            Mark(node, field.Source, mapped);
            NoteDroppedCommerce(record, node, field);
        }
        // One canonical order, whichever source field gave an effect.
        if (mapping.Fields.Any(f => f.Kind.StartsWith("effect", StringComparison.Ordinal))) values["effects"] = CanonicalEffects(effects);
        var requirements = new PrerequisiteMapper(this).Map(record, mapping.Requirements, mapped);
        if (requirements is not null) values["requirements"] = JsonSerializer.SerializeToElement(requirements, typeof(Requirement), Json);
        NoteAmbiguousReligion(record, requirements);
        if (record.Class == "BuildingInfo" && values.GetValueOrDefault("founds") is LeafReference { Kind: "corporation" }
            && values.GetValueOrDefault("cost") is int cost)
            values["cost"] = FoundingCost(record, values.GetValueOrDefault("era") as string, cost);
        if (mapping.DefinitionType == typeof(Ariel.Mods.Corporations.CorporationDef)) values["seat"] = CorporationSeat(record);
        if ((string)values["id"]! != ids[(record.Class, record.Type)]) values["identity"] = ids[(record.Class, record.Type)];
        FoldClass(record, values, mapped);
        if (record.Class == "BuildingInfo") values["replacedBy"] = ReplacedBy((string)values["id"]!, (string)values["defaultBuilding"]!);
        if (record.Class == "CivicOptionInfo") values["defaultCivic"] = CivicDefault(record);
        values["unsupported"] = FieldCensus.Flatten(record.Node).Where(f => !mapped.Contains(f.Path) && RecordMerger.IsNonDefault(f.Node))
            .Select(f => f.Path).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (!MappedPaths.TryGetValue(record.Class, out var paths)) MappedPaths[record.Class] = paths = new(StringComparer.Ordinal);
        paths.UnionWith(mapped);
        var json = JsonSerializer.Serialize(values, Json);
        return (GameContentDef)JsonSerializer.Deserialize(json, mapping.DefinitionType, Json)!;
    }

    /// <summary>The buildings that replace this one but cannot say so in their own <c>ReplaceBuildings</c>, because this
    /// building belongs outright to a mod theirs cannot depend on (a corporation's store, replaced by the base theatre):
    /// the row names them in <c>ReplacedBy</c> instead, which the city rules read as the same replacement. A replacement
    /// a base row may name stays in that row.</summary>
    private string[] ReplacedBy(string id, string defaultBuilding)
    {
        replacedBy ??= BuildReplacedBy();
        return replacedBy.Where(p => p.Key == id || p.Key == defaultBuilding).SelectMany(p => p.Value)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }
    private Dictionary<string, List<string>>? replacedBy;
    /// <summary>Each claimed building or class a source replacement names, with the buildings whose rows leave it out.</summary>
    private Dictionary<string, List<string>> BuildReplacedBy()
    {
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var replacer in records.Values.Where(r => r.Class == "BuildingInfo"))
        {
            string replacerId = ids[(replacer.Class, replacer.Type)];
            string writer = claimed.GetValueOrDefault(replacerId) ?? IdMapping.ClassMods["BuildingInfo"];
            foreach (var entry in replacer.Node.Child("ReplaceBuildings")?.Children ?? [])
            {
                if (entry.Children.Length < 2 || !Boolean(entry.Children[1].Text) || !Present(entry.Children[0].Text)) continue;
                if (Reference("BuildingClassInfo", entry.Children[0].Text, replacer, "ReplaceBuildings") is not { } replaced) continue;
                if (!claimed.TryGetValue(replaced, out var owner) || owner == writer) continue;
                if (!result.TryGetValue(replaced, out var list)) result[replaced] = list = [];
                list.Add(replacerId);
            }
        }
        return result;
    }

    /// <summary>The production cost of a building that founds a corporation or guild. Ariel founds every one of them by
    /// building its headquarters or Master Guild in a city, and great people do not found them. The source leaves the
    /// 21 corporate headquarters to a great person (cost -1), so each costs 70% of the median national wonder of its
    /// era (700 industrial, 1300 modern), measured over the imported buildings with a player limit of one. The
    /// source's Master Guilds cost 300, the medieval median, and take the same 70%. The cost is set by era rather than
    /// scaled from the source value, so a re-import cannot drift. Only a headquarters, whose source cost is missing,
    /// is an <c>iCost</c> data issue; the guild discount is a balance rule.</summary>
    private int FoundingCost(EffectiveRecord record, string? era, int sourceCost)
    {
        int cost = era switch
        {
            "foundation:era_medieval" => 210,
            "foundation:era_industrial" => 490,
            "foundation:era_modern" => 910,
            _ => throw new InvalidDataException($"{record.Type}: a founding building of era '{era}' has no cost; add one to FoundingCost"),
        };
        if (sourceCost < 0)
            Issues.Add(new(ids[(record.Class, record.Type)], "iCost", sourceCost.ToString(CultureInfo.InvariantCulture),
                $"Set to {cost}, 70% of the median national wonder cost of its era: Ariel founds corporations by building the headquarters, not with a great person."));
        return cost;
    }

    /// <summary>What the corporation calls its founding building: a guild's is its Master Guild (not the common Guild
    /// Hall), a corporation's its headquarters, by the special building type of the building that founds it.</summary>
    private string CorporationSeat(EffectiveRecord corporation)
    {
        var headquarters = records.Values.Where(r => r.Class == "BuildingInfo" && r.Node.Value("FoundsCorporation") == corporation.Type)
            .Select(r => r.Node.Value("SpecialBuildingType")).Distinct(StringComparer.Ordinal).ToArray();
        return headquarters switch
        {
            ["SPECIALBUILDING_GUILD_HQ"] => "Master Guild",
            ["SPECIALBUILDING_CORPORATION_HQ"] => "headquarters",
            _ => throw new InvalidDataException($"{corporation.Type}: founding headquarters special types [{string.Join(", ", headquarters)}] name neither a guild nor a corporation"),
        };
    }

    /// <summary>The source stores at most one presence religion per building, and the state-religion rule reads the first
    /// such leaf. Two different ones cannot be represented, so record the conversion error instead of shipping a
    /// requirement whose second half never runs.</summary>
    private void NoteAmbiguousReligion(EffectiveRecord record, Requirement? requirements)
    {
        if (requirements is null) return;
        var religions = ContentReferences.Leaves(requirements)
            .Where(l => l.Kind == "religion" && l.Param is null or 0).Select(l => l.Id).Distinct(StringComparer.Ordinal).ToArray();
        if (religions.Length <= 1) return;
        Issues.Add(new(ids[(record.Class, record.Type)], "PrereqReligion", string.Join(", ", religions),
            "More than one presence religion. The source has a single PrereqReligion field and the game keeps the first, so split this requirement before shipping."));
    }

    private string? CivicDefault(EffectiveRecord option)
    {
        // This profile has one ungated civic per option. Do not invent defaults for ambiguous mod data.
        var candidates = records.Values.Where(r => r.Class == "CivicInfo" && r.Node.Value("CivicOptionType") == option.Type)
            .Where(r => !Present(r.Node.Value("TechPrereq"))).ToArray();
        return candidates.Length == 1 ? ids[("CivicInfo", candidates[0].Type)] : null;
    }

    internal object? MapField(EffectiveRecord record, DataNode node, FieldMapping field) => field.Kind switch
    {
        "int" => Integer(node.Text, DefaultInt(field)), "bool" => GetterBoolean(node.Text, DefaultBool(field)),
        "text" => Text(node.Text ?? ""), "enum" => NativeKey(node.Text ?? ""),
        "ref" => NamedReference(record, node, field),
        "leafRef" => Reference(field.Target!, node.Text, record, field.Source) is { } named
            ? new LeafReference(field.LeafKind ?? throw new InvalidDataException(field.Source + ": a leafRef needs a leafKind"), named) : null,
        "csvRefs" => (node.Text ?? "").Split(',').Where(Present).Select(v => Reference(field.Target!, v, record, field.Source)).Where(v => v is not null).ToArray(),
        "refs" => node.Children.Select(n => Reference(field.Target!, n.Text, record, field.Source)).Where(v => v is not null).ToArray(),
        "texts" => node.Children.Select(n => Text(n.Text ?? "")).ToArray(),
        "intList" => node.Children.Select(n => Integer(n.Text)).ToArray(),
        "yield" => Yield(node), "commerce" => Commerce(node, field),
        "turns" => node.Children.Select(n => new TurnIncrement(Integer(n.Value("iMonthIncrement")), Integer(n.Value("iTurnsPerIncrement")))).ToArray(),
        "civilization" => CivilizationField(record, node, field),
        "improvementBonuses" => Indexed(record, node, field, n => new ImprovementBonus
        {
            MakesValid = Boolean(n.Value("bBonusMakesValid")), Connects = Boolean(n.Value("bBonusTrade")),
            DiscoveryRandomRange = Integer(n.Value("iDiscoverRand")), DepletionRandomRange = Integer(n.Value("iDepletionRand")),
            Yields = Yield(n.Child("YieldChanges"))
        }),
        "buildFeatures" => Indexed(record, node, field, n => new BuildFeature
        {
            Requirements = Present(n.Value("PrereqTech")) ? new Requirement.Leaf("tech", RequiredReference("TechInfo", n.Value("PrereqTech"), record, field.Source)) : null,
            Time = Integer(n.Value("iTime")), Production = Integer(n.Value("iProduction")), Remove = Boolean(n.Value("bRemove")),
            AllowRemovalWithoutProduction = Boolean(n.Value("bCanRemoveWithNoProductionGain"))
        }),
        "combatPromotions" => Indexed(record, node, field, n => n.Child("UnitCombatTypes")!.Children
            .Select(t => RequiredReference("UnitCombatInfo", t.Text!, record, field.Source)).ToArray()),
        "techSpecialists" => Indexed(record, node, field, n => new SpecialistSlotsChange
        { Slots = PairMap(record, n.Child("SpecialistCounts"), "SpecialistInfo", n => Integer(n.Children[1].Text)).ToImmutable() }),
        "buildingCommerce" => BuildingCommerce(record, node, field),
        "civicAttitudes" => Indexed(record, node, field, n => new Ariel.Content.Schema.CivicAttitudeEffect(Integer(n.Value("iAttitudeChange")), Text(n.Value("Description")))),
        "effect" => Effects(field, (null, Integer(node.Text, DefaultInt(field)))),
        "effectFlag" => Effects(field, (null, GetterBoolean(node.Text, DefaultBool(field)) ? 1 : 0)),
        "effectYield" => VectorEffects(field, null, Yield(node)),
        "effectCommerce" => VectorEffects(field, null, Commerce(node, field)),
        "effectMapInt" => Effects(field, Indexed(record, node, field, n => Integer(n.Children[1].Text)).Select(p => ((string?)p.Key, (long)p.Value)).ToArray()),
        "effectMapBool" => Effects(field, Indexed(record, node, field, n => Boolean(n.Children[1].Text)).Select(p => ((string?)p.Key, p.Value ? 1L : 0L)).ToArray()),
        "effectMapYield" => Indexed(record, node, field, n => Yield(n.Children[1])).SelectMany(p => VectorEffects(field, p.Key, p.Value)).ToArray(),
        "effectMapCommerce" => Indexed(record, node, field, n => Commerce(n.Children[1], field)).SelectMany(p => VectorEffects(field, p.Key, p.Value)).ToArray(),
        "mapInt" => Indexed(record, node, field, n => Integer(n.Children[1].Text)),
        "mapBool" => Indexed(record, node, field, n => Boolean(n.Children[1].Text)),
        // The keys of a (reference, flag) pair list whose flag is set: a false pair says nothing.
        "flaggedRefs" => Indexed(record, node, field, n => Boolean(n.Children[1].Text)).Where(p => p.Value).Select(p => p.Key).ToArray(),
        "mapRef" => Indexed(record, node, field, n => Reference(field.ValueTarget!, n.Children[1].Text, record, field.Source)),
        "mapYield" => Indexed(record, node, field, n => Yield(n.Children[1])),
        // An amending row lists only the entries that add something.
        "mapCommerce" => Indexed(record, node, field, n => Commerce(n.Children[1], field))
            .Where(p => field.Part is null && !field.Owned || !p.Value.IsZero).ToSortedDictionary(),
        _ => throw new InvalidDataException("Unknown mapping kind: " + field.Kind)
    };

    private static ModifierEffect[] Effects(FieldMapping field, params (string? Target, long Amount)[] amounts) => amounts
        .Select(a => new ModifierEffect { Scope = Scope(field), Key = field.Modifier!, Target = a.Target, Amount = a.Amount }).ToArray();
    /// <summary>One effect per vector component. <see cref="FieldMapping.Modifier"/> is a key prefix (<c>yield.percent</c>
    /// gives <c>yield.percent.food</c>) or a format whose <c>{}</c> the component fills.</summary>
    private static ModifierEffect[] VectorEffects(FieldMapping field, string? target, YieldVector value) =>
        VectorEffects(field, target, [("food", value.Food), ("production", value.Production), ("commerce", value.Commerce)]);
    private static ModifierEffect[] VectorEffects(FieldMapping field, string? target, CommerceVector value) =>
        VectorEffects(field, target, Parts(value));
    private static (string Part, long Amount)[] Parts(CommerceVector value)
    {
        var parts = new List<(string, long)>();
        foreach (var part in value) parts.Add(part);
        return parts.ToArray();
    }
    private static ModifierEffect[] VectorEffects(FieldMapping field, string? target, (string Part, long Amount)[] parts) => parts
        .Select(p => new ModifierEffect
        {
            Scope = Scope(field), Target = target, Amount = p.Amount,
            Key = field.Modifier!.Contains("{}", StringComparison.Ordinal) ? field.Modifier.Replace("{}", p.Part, StringComparison.Ordinal) : field.Modifier + "." + p.Part,
        }).ToArray();
    private static ModifierScope Scope(FieldMapping field) =>
        Enum.Parse<ModifierScope>(field.Scope ?? throw new InvalidDataException(field.Source + ": an effect needs a scope"), ignoreCase: true);

    private bool GetterBoolean(string? value, bool fallback = false)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        value = value.Trim();
        if (value is "0" or "false") return false;
        if (value is "1" or "true") return true;
        // The native XML utility treats a non-literal boolean as a define name, with zero for a missing define.
        return Integer(defines.GetValueOrDefault(value)?.Value) != 0;
    }

    private SortedDictionary<string, T> Indexed<T>(EffectiveRecord record, DataNode node, FieldMapping field, Func<DataNode, T> value) =>
        PairMap(record, node, field.Target, value, field.Owned, field);

    /// <summary>A (reference, value) pair list keyed by id. An entry whose key the row may not name (<see cref="Names"/>)
    /// is left out, and so is one the field's <see cref="FieldMapping.OnlyKeys"/> or <see cref="FieldMapping.ExceptKeys"/>
    /// give to another row. A field that carries only a commerce <see cref="FieldMapping.Part"/> leaves the entries about
    /// the emitting mod's own records to the field that carries them whole.</summary>
    private SortedDictionary<string, T> PairMap<T>(EffectiveRecord record, DataNode? node, string? target, Func<DataNode, T> value, bool owned = false,
        FieldMapping? field = null)
    {
        var result = new SortedDictionary<string, T>(StringComparer.Ordinal);
        foreach (var entry in node?.Children ?? [])
        {
            if (entry.Children.Length < 2) throw new InvalidDataException($"Malformed indexed field: {record.Type}/{node!.Name}");
            var key = entry.Children[0].Text;
            if (!Present(key)) continue;
            var id = target is null ? NativeKey(key!) : Reference(target, key, record, node!.Name);
            if (id is null || (target is null || Names(id, owned, field?.Part is not null)) is false) continue;
            if (field?.OnlyKeys is { } only && !only.Contains(id, StringComparer.Ordinal)) continue;
            if (field?.ExceptKeys is { } except && except.Contains(id, StringComparer.Ordinal)) continue;
            result[id] = value(entry); // Native positional pairs use the last slot assignment.
        }
        return result;
    }

    /// <summary>A civilization's initial civics, class overrides or starting grants. An override or grant of a unit or
    /// building the row may not name (<see cref="Names"/>) is left out.</summary>
    private object CivilizationField(EffectiveRecord record, DataNode node, FieldMapping field)
    {
        if (node.Name == "InitialCivics") return node.Children.Where(n => Present(n.Text))
            .Select(n => RequiredReference("CivicInfo", n.Text!, record, node.Name)).ToArray();
        bool unit = node.Name.Contains("Unit", StringComparison.Ordinal);
        var classFamily = unit ? "UnitClassInfo" : "BuildingClassInfo";
        var family = unit ? "UnitInfo" : "BuildingInfo";
        bool overrides = node.Name is "Units" or "Buildings";
        var result = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var entry in node.Children)
        {
            var symbol = entry.Children[0].Text!;
            var key = RequiredReference(classFamily, symbol, record, node.Name);
            if (overrides)
            {
                if (Names(key, field.Owned)) result[key] = Reference(family, entry.Children[1].Text, record, node.Name);
                continue;
            }
            var granted = StartingGrant(record, symbol, key, unit);
            if (granted is null || !Names(granted, field.Owned)) continue;
            result[granted] = unit ? (object)Integer(entry.Children[1].Text) : Boolean(entry.Children[1].Text);
        }
        return result;
    }

    private string? StartingGrant(EffectiveRecord record, string classSymbol, string defaultId, bool unit)
    {
        var list = unit ? "Units" : "Buildings";
        var replacement = record.Node.Child(list)?.Children.FirstOrDefault(n => n.Children[0].Text == classSymbol)?.Children[1].Text;
        return replacement is null ? defaultId : Reference(unit ? "UnitInfo" : "BuildingInfo", replacement, record, list);
    }

    private SortedDictionary<string, CommerceVector> BuildingCommerce(EffectiveRecord record, DataNode node, FieldMapping field)
    {
        var result = new SortedDictionary<string, CommerceVector>(StringComparer.Ordinal);
        foreach (var entry in node.Children)
        {
            var id = RequiredReference("BuildingClassInfo", entry.Children[0].Text!, record, node.Name);
            var amount = Integer(entry.Children[2].Text);
            var type = NativeKey(entry.Children[1].Text!);
            if (Array.IndexOf(NativeCommerce, type) < 0) throw new InvalidDataException("Unknown commerce type");
            if (!Wanted(type, field)) continue;
            // An amending row lists only the entries that add something.
            if (field.Part is not null && amount == 0) continue;
            var change = field.Part is null ? CommerceVector.Zero(Layout(field)).With(type, amount) : CommerceVector.Of((type, amount));
            result[id] = result.GetValueOrDefault(id) + change;
        }
        // A base row keeps every entry the source lists, even for a type it cannot carry: the entry is then all zeros.
        if (field.Part is null)
            foreach (var entry in node.Children)
            {
                var id = RequiredReference("BuildingClassInfo", entry.Children[0].Text!, record, node.Name);
                if (!result.ContainsKey(id)) result[id] = CommerceVector.Zero(Layout(field));
            }
        return result;
    }

    private void FoldClass(EffectiveRecord record, Dictionary<string, object?> values, HashSet<string> mapped)
    {
        bool building = record.Class == "BuildingInfo";
        if (!building && record.Class != "UnitInfo") return;
        var sourceField = building ? "BuildingClass" : "Class";
        var classFamily = building ? "BuildingClassInfo" : "UnitClassInfo";
        var classRecord = records[(classFamily, record.Node.Value(sourceField))];
        values[building ? "defaultBuilding" : "defaultUnit"] = RequiredReference(classFamily, classRecord.Type, record, sourceField);
        values["limits"] = new ClassLimits
        {
            World = Integer(classRecord.Node.Value("iMaxGlobalInstances"), -1), Team = Integer(classRecord.Node.Value("iMaxTeamInstances"), -1),
            Player = Integer(classRecord.Node.Value("iMaxPlayerInstances"), -1), ExtraPlayer = Integer(classRecord.Node.Value("iExtraPlayerInstances")),
            NoLimit = Boolean(classRecord.Node.Value("bNoLimit"))
        };
        Mark(record.Node.Child(sourceField), sourceField, mapped);
        if (!MappedPaths.TryGetValue(classFamily, out var paths)) MappedPaths[classFamily] = paths = new(StringComparer.Ordinal);
        foreach (var field in new[] { "Type", "DefaultBuilding", "DefaultUnit", "iMaxGlobalInstances", "iMaxTeamInstances", "iMaxPlayerInstances", "iExtraPlayerInstances", "bNoLimit" }) Mark(classRecord.Node.Child(field), field, paths);
    }

    public string? Reference(string family, string? value, EffectiveRecord record, string field)
    {
        if (!Present(value)) return null;
        value = value!.Trim();
        if (family is "UnitClassInfo" or "BuildingClassInfo")
        {
            if (!records.TryGetValue((family, value), out var classRecord)) return Missing(record, field, value);
            return Reference(family == "UnitClassInfo" ? "UnitInfo" : "BuildingInfo", classRecord.Node.Value(family == "UnitClassInfo" ? "DefaultUnit" : "DefaultBuilding"), record, field);
        }
        if (ids.TryGetValue((family, value), out var id)) return id;
        return Missing(record, field, value);
    }

    /// <summary>The corporation a source building founds, as an Ariel id, or null when it is no headquarters (or no
    /// building of that name exists, which <see cref="RequiredReference"/> then reports).</summary>
    public string? FoundedCorporation(string building) =>
        records.TryGetValue(("BuildingInfo", building.Trim()), out var headquarters) && headquarters.Node.Value("FoundsCorporation") is var founded
            && Present(founded) ? RequiredReference("CorporationInfo", founded!, headquarters, "FoundsCorporation") : null;

    public string RequiredReference(string family, string value, EffectiveRecord record, string field) =>
        Reference(family, value, record, field) ?? throw new InvalidDataException($"Required source reference is unresolved: {record.Type}/{field} -> {family}/{value}");

    private string? Missing(EffectiveRecord record, string field, string value)
    {
        Issues.Add(new(ids[(record.Class, record.Type)], field, value, "Native missing-info sentinel; optional reference becomes null. No guessed alias."));
        return null;
    }

    private static readonly Regex PrefixedTextKey = new(@"^[A-Z0-9]+_TXT_KEY_[A-Z0-9_]+$", RegexOptions.CultureInvariant);

    public string Text(string value)
    {
        if (value.StartsWith("TXT_KEY_", StringComparison.Ordinal))
        {
            var text = lookupText(value);
            if (text is null) return Humanize(value[8..]);
            value = text;
        }
        // A key the source renamed in place, such as CHANGED_TXT_KEY_CORPORATION_2, is still a key of the text table.
        else if (PrefixedTextKey.IsMatch(value) && lookupText(value) is { } renamed) value = renamed;
        return Branding.Output(Regex.Replace(value, @"\{link:([^}]+)\}(.*?)\{/link\}", match =>
        {
            var body = match.Groups[2].Value;
            // A link is a reference: text of a row that may not name the record a feature mod owns keeps only the words.
            if (symbols.TryGetValue(match.Groups[1].Value, out var entry) && claimed.TryGetValue(entry.Id, out var owner)
                && !IdMapping.MayName(emitter, owner)) return body;
            if (entry is not null && SchemaCatalog.Definitions.Any(d => d.SourceClass == entry.Class))
                return "{link:" + entry.Id + "}" + body + "{/link}";
            return "{topic:" + Branding.Identifier(match.Groups[1].Value) + "}" + body + "{/topic}";
        }, RegexOptions.CultureInvariant | RegexOptions.Singleline));
    }

    private string Icon(EffectiveRecord record)
    {
        var icon = record.Node.Value("Button");
        if (icon.Length == 0)
        {
            var art = record.Node.Value("ArtDefineTag");
            if (record.Class == "UnitInfo") art = record.Node.Child("UnitMeshGroups")?.DescendantsAndSelf().FirstOrDefault(n => n.Name == "EarlyArtDefineTag")?.Text ?? "";
            var artRecord = records.Values.FirstOrDefault(r => r.Type == art && r.Class.EndsWith("ArtInfo", StringComparison.Ordinal));
            icon = artRecord?.Node.Value("Button") ?? "";
        }
        return Branding.Identifier(icon);
    }

    internal static bool Present(string? value) => value?.Trim() is { Length: > 0 } text && text is not ("NONE" or "-1" or "0");
    internal static int Integer(string? value, int fallback = 0) => string.IsNullOrWhiteSpace(value) ? fallback : int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    internal static bool Boolean(string? value, bool fallback = false) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim() is "1" or "true";
    private static int DefaultInt(FieldMapping field) => field.Default.ValueKind == JsonValueKind.Number ? field.Default.GetInt32() : 0;
    private static bool DefaultBool(FieldMapping field) => field.Default.ValueKind == JsonValueKind.True || field.Default.ValueKind == JsonValueKind.Number && field.Default.GetInt32() != 0;
    internal static string NativeKey(string value)
    {
        value = value.Trim();
        int prefix = value.IndexOf('_');
        return Branding.Identifier(prefix < 0 ? value : value[(prefix + 1)..]).ToLowerInvariant();
    }
    private static string Humanize(string value) => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(Branding.Identifier(value).ToLowerInvariant().Replace('_', ' '));
    internal static YieldVector Yield(DataNode? node)
    {
        var n = Vector(node, 3); return new(n[0], n[1], n[2]);
    }
    /// <summary>Whether a field carries this commerce type: only its <see cref="FieldMapping.Part"/> when it has one, else
    /// every type the row being mapped may name (<see cref="Carries"/>).</summary>
    private bool Wanted(string key, FieldMapping field) => field.Part is { } part ? key == part : Carries(key);
    /// <summary>The layout of a field's vectors: the commerce types the row may name, in source order.</summary>
    private CommerceLayout Layout(FieldMapping field) => CommerceLayout.Of(NativeCommerce.Where(k => Wanted(k, field)).ToArray());
    /// <summary>A native commerce array as a vector of the types the field carries. A field with a <see cref="FieldMapping.Part"/>
    /// that has no amount there gives the default vector, which an amending row leaves out.</summary>
    internal CommerceVector Commerce(DataNode? node, FieldMapping field)
    {
        var n = Vector(node, NativeCommerce.Length);
        var parts = NativeCommerce.Select((key, i) => (Key: key, Amount: (long)n[i])).Where(p => Wanted(p.Key, field)).ToArray();
        return parts.Length == 0 || field.Part is not null && parts.All(p => p.Amount == 0) ? default : CommerceVector.Of(parts);
    }
    private static int[] Vector(DataNode? node, int length)
    {
        var result = new int[length];
        if (node is null) return result;
        if (node.Children.Length > length) throw new InvalidDataException("Indexed vector exceeds native enum length: " + node.Name);
        for (int i = 0; i < node.Children.Length; i++) result[i] = Integer(node.Children[i].Text);
        return result;
    }
    internal static void Mark(DataNode? node, string path, HashSet<string> mapped)
    {
        if (node is null) return;
        mapped.Add(path);
        foreach (var child in FieldCensus.Flatten(node, path)) mapped.Add(child.Path);
    }
}

internal static class ImmutableMapExtensions
{
    public static System.Collections.Immutable.ImmutableDictionary<string, T> ToImmutable<T>(this SortedDictionary<string, T> map) =>
        System.Collections.Immutable.ImmutableDictionary.CreateRange(StringComparer.Ordinal, map);
    public static SortedDictionary<string, T> ToSortedDictionary<T>(this IEnumerable<KeyValuePair<string, T>> pairs) =>
        new(pairs.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal), StringComparer.Ordinal);
}
