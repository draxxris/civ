using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.ContentImport;

public sealed record DataIssue(string Record, string Field, string SourceValue, string Resolution);
public sealed record MappedDefinition(DefinitionMapping Mapping, ContentDef Definition);

/// <summary>Compiles merged trees into immutable Core definitions. No source table is read at game startup.</summary>
public sealed partial class ContentMapper
{
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
        ids = idEntries.ToDictionary(r => (r.Class, r.SourceType), r => r.Id);
        foreach (var role in NativeUnitAiRoles)
        {
            if (records.ContainsKey(("UnitAIInfo", role))) continue;
            records[("UnitAIInfo", role)] = new("UnitAIInfo", role, DataNode.Branch("UnitAIInfo", [DataNode.Leaf("Type", role), DataNode.Leaf("Description", Humanize(role[7..]))]), ["native-enum"]);
            ids[("UnitAIInfo", role)] = IdMapping.Id("UnitAIInfo", role);
        }
        symbols = idEntries.GroupBy(r => r.SourceType, StringComparer.Ordinal).Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.Single(), StringComparer.OrdinalIgnoreCase);
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
                    if (MapAttachment(record, mapping, attached) is { } row) result.Add(new(mapping, row));
                foreach (var mapping in family.Where(m => !m.IsAttachment))
                    result.Add(new(mapping, MapRecord(record, mapping, attached)));
            }
        }
        return result;
    }

    /// <summary>The attachment row a feature mod keeps about one record, or null when every field it maps is at its
    /// default, so a mod only lists the records its rules care about.</summary>
    private ContentAttachment? MapAttachment(EffectiveRecord record, DefinitionMapping mapping, HashSet<string> mapped)
    {
        var target = ids[(record.Class, record.Type)];
        var identity = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = mapping.Mod + ":" + target[(target.IndexOf(':') + 1)..], ["target"] = target,
        };
        var values = new Dictionary<string, object?>(identity, StringComparer.Ordinal);
        foreach (var field in mapping.Fields)
        {
            var node = record.Node.Child(field.Source);
            if (node is null) continue;
            values[JsonNamingPolicy.CamelCase.ConvertName(field.Property)] = MapValue(record, node, field);
            Mark(node, field.Source, mapped);
        }
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
        if (id[..colon] == mapping.Mod) return id;
        if (mapping.DefinitionType.GetProperty("Identity") is null)
            throw new InvalidDataException($"{mapping.Definition} is emitted by {mapping.Mod} for {id} but has no Identity to name it by");
        return mapping.Mod + id[colon..];
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
            if (field.Kind.StartsWith("effect", StringComparison.Ordinal)) effects.AddRange((IEnumerable<ModifierEffect>)value!);
            else values[JsonNamingPolicy.CamelCase.ConvertName(field.Property)] = value;
            Mark(node, field.Source, mapped);
        }
        // One canonical order, whichever source field gave an effect.
        if (mapping.Fields.Any(f => f.Kind.StartsWith("effect", StringComparison.Ordinal)))
            values["effects"] = effects.Where(e => e.Amount != 0).OrderBy(e => e.Key, StringComparer.Ordinal)
                .ThenBy(e => e.Target ?? "", StringComparer.Ordinal).ToArray();
        var requirements = new PrerequisiteMapper(this).Map(record, mapping.Requirements, mapped);
        if (requirements is not null) values["requirements"] = JsonSerializer.SerializeToElement(requirements, typeof(Requirement), Json);
        NoteAmbiguousReligion(record, requirements);
        // Ariel founds corporations through great people, so an imported headquarters that founds one (AND's guild
        // halls) must not be purchasable: it would be a building nothing in the rules reacts to.
        if (record.Class == "BuildingInfo" && values.GetValueOrDefault("foundsCorporation") is string founded && founded.Length > 0
            && values.GetValueOrDefault("cost") is int cost && cost >= 0)
        {
            values["cost"] = -1;
            Issues.Add(new(ids[(record.Class, record.Type)], "iCost", cost.ToString(CultureInfo.InvariantCulture),
                "Set to -1: Ariel founds corporations through great people, not by constructing the headquarters."));
        }
        if ((string)values["id"]! != ids[(record.Class, record.Type)]) values["identity"] = ids[(record.Class, record.Type)];
        FoldClass(record, values, mapped);
        // The buildings that serve a faith are Religion's rules; the base game's identity of the faith names none.
        if (mapping.DefinitionType == typeof(Ariel.Mods.Religion.ReligionDef)) ReligionBuildings(record, values);
        if (record.Class == "CivicOptionInfo") values["defaultCivic"] = CivicDefault(record);
        values["unsupported"] = FieldCensus.Flatten(record.Node).Where(f => !mapped.Contains(f.Path) && RecordMerger.IsNonDefault(f.Node))
            .Select(f => f.Path).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (!MappedPaths.TryGetValue(record.Class, out var paths)) MappedPaths[record.Class] = paths = new(StringComparer.Ordinal);
        paths.UnionWith(mapped);
        var json = JsonSerializer.Serialize(values, Json);
        return (GameContentDef)JsonSerializer.Deserialize(json, mapping.DefinitionType, Json)!;
    }

    /// <summary>AND stores at most one presence religion per building, and the state-religion rule reads the first
    /// such leaf. Two different ones cannot be represented, so record the conversion error instead of shipping a
    /// requirement whose second half never runs.</summary>
    private void NoteAmbiguousReligion(EffectiveRecord record, Requirement? requirements)
    {
        if (requirements is null) return;
        var religions = ContentReferences.Leaves(requirements)
            .Where(l => l.Kind == "religion" && l.Param is null or 0).Select(l => l.Id).Distinct(StringComparer.Ordinal).ToArray();
        if (religions.Length <= 1) return;
        Issues.Add(new(ids[(record.Class, record.Type)], "PrereqReligion", string.Join(", ", religions),
            "More than one presence religion. AND has a single PrereqReligion field and the game keeps the first, so split this requirement before shipping."));
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
        "ref" => Reference(field.Target!, node.Text, record, field.Source),
        "csvRefs" => (node.Text ?? "").Split(',').Where(Present).Select(v => Reference(field.Target!, v, record, field.Source)).Where(v => v is not null).ToArray(),
        "csvInts" => (node.Text ?? "").Split(',').Where(v => v.Trim().Length > 0).Select(v => Integer(v)).ToArray(),
        "refs" => node.Children.Select(n => Reference(field.Target!, n.Text, record, field.Source)).Where(v => v is not null).ToArray(),
        "texts" => node.Children.Select(n => Text(n.Text ?? "")).ToArray(),
        "intList" => node.Children.Select(n => Integer(n.Text)).ToArray(),
        "yield" => Yield(node), "commerce" => Commerce(node),
        "turns" => node.Children.Select(n => new TurnIncrement(Integer(n.Value("iMonthIncrement")), Integer(n.Value("iTurnsPerIncrement")))).ToArray(),
        "civilization" => CivilizationField(record, node),
        "outcomes" => OutcomeList(record, node),
        "actions" => PairMap(record, node, "MissionInfo", n => OutcomeList(record, n.Children[1])),
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
        "buildingCommerce" => BuildingCommerce(record, node),
        "civicAttitudes" => Indexed(record, node, field, n => new Ariel.Mods.Diplomacy.CivicAttitudeEffect(Integer(n.Value("iAttitudeChange")), Text(n.Value("Description")))),
        "effect" => Effects(field, (null, Integer(node.Text, DefaultInt(field)))),
        "effectFlag" => Effects(field, (null, GetterBoolean(node.Text, DefaultBool(field)) ? 1 : 0)),
        "effectYield" => VectorEffects(field, null, Yield(node)),
        "effectCommerce" => VectorEffects(field, null, Commerce(node)),
        "effectMapInt" => Effects(field, Indexed(record, node, field, n => Integer(n.Children[1].Text)).Select(p => ((string?)p.Key, (long)p.Value)).ToArray()),
        "effectMapBool" => Effects(field, Indexed(record, node, field, n => Boolean(n.Children[1].Text)).Select(p => ((string?)p.Key, p.Value ? 1L : 0L)).ToArray()),
        "effectMapYield" => Indexed(record, node, field, n => Yield(n.Children[1])).SelectMany(p => VectorEffects(field, p.Key, p.Value)).ToArray(),
        "effectMapCommerce" => Indexed(record, node, field, n => Commerce(n.Children[1])).SelectMany(p => VectorEffects(field, p.Key, p.Value)).ToArray(),
        "mapInt" => Indexed(record, node, field, n => Integer(n.Children[1].Text)),
        "mapBool" => Indexed(record, node, field, n => Boolean(n.Children[1].Text)),
        "mapText" => Indexed(record, node, field, n => Text(n.Children[1].Text ?? "")),
        "mapRef" => Indexed(record, node, field, n => Reference(field.ValueTarget!, n.Children[1].Text, record, field.Source)),
        "mapYield" => Indexed(record, node, field, n => Yield(n.Children[1])),
        "mapCommerce" => Indexed(record, node, field, n => Commerce(n.Children[1])),
        _ => throw new InvalidDataException("Unknown mapping kind: " + field.Kind)
    };

    private static ModifierEffect[] Effects(FieldMapping field, params (string? Target, long Amount)[] amounts) => amounts
        .Select(a => new ModifierEffect { Scope = Scope(field), Key = field.Modifier!, Target = a.Target, Amount = a.Amount }).ToArray();
    /// <summary>One effect per vector component. <see cref="FieldMapping.Modifier"/> is a key prefix (<c>yield.percent</c>
    /// gives <c>yield.percent.food</c>) or a format whose <c>{}</c> the component fills.</summary>
    private static ModifierEffect[] VectorEffects(FieldMapping field, string? target, YieldVector value) =>
        VectorEffects(field, target, [("food", value.Food), ("production", value.Production), ("commerce", value.Commerce)]);
    private static ModifierEffect[] VectorEffects(FieldMapping field, string? target, CommerceVector value) =>
        VectorEffects(field, target, [("gold", value.Gold), ("research", value.Research), ("culture", value.Culture), ("espionage", value.Espionage)]);
    private static ModifierEffect[] VectorEffects(FieldMapping field, string? target, (string Part, long Amount)[] parts) => parts
        .Select(p => new ModifierEffect
        {
            Scope = Scope(field), Target = target, Amount = p.Amount,
            Key = field.Modifier!.Contains("{}", StringComparison.Ordinal) ? field.Modifier.Replace("{}", p.Part, StringComparison.Ordinal) : field.Modifier + "." + p.Part,
        }).ToArray();
    private static ModifierScope Scope(FieldMapping field) =>
        Enum.Parse<ModifierScope>(field.Scope ?? throw new InvalidDataException(field.Source + ": an effect needs a scope"), ignoreCase: true);

    private OutcomeEffect[] OutcomeList(EffectiveRecord record, DataNode node) => node.Children.Select(entry => Outcome(record, entry)).ToArray();

    private OutcomeEffect Outcome(EffectiveRecord record, DataNode node)
    {
        var delivery = node.Value("bUnitToCity").Trim();
        var policy = delivery.Length > 0 && delivery is not ("0" or "1" or "false" or "true") ? delivery : null;
        if (policy is not null && !defines.ContainsKey(policy))
            Issues.Add(new(ids[(record.Class, record.Type)], "bUnitToCity", policy, "Absent boolean define defaults to false; policy key is retained."));
        return new OutcomeEffect
        {
            Outcome = RequiredReference("OutcomeInfo", node.Value("OutcomeType"), record, "OutcomeType"),
            Chance = Integer(node.Value("iChance")), Unit = Reference("UnitInfo", node.Value("UnitType"), record, "UnitType"),
            Bonus = Reference("BonusInfo", node.Value("BonusType"), record, "BonusType"), Yields = Yield(node.Child("Yields")),
            DeliverUnitToCity = GetterBoolean(delivery), DeliveryPolicyKey = policy
        };
    }

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
        PairMap(record, node, field.Target, value);

    private SortedDictionary<string, T> PairMap<T>(EffectiveRecord record, DataNode? node, string? target, Func<DataNode, T> value)
    {
        var result = new SortedDictionary<string, T>(StringComparer.Ordinal);
        foreach (var entry in node?.Children ?? [])
        {
            if (entry.Children.Length < 2) throw new InvalidDataException($"Malformed indexed field: {record.Type}/{node!.Name}");
            var key = entry.Children[0].Text;
            if (!Present(key)) continue;
            var id = target is null ? NativeKey(key!) : Reference(target, key, record, node!.Name);
            if (id is not null) result[id] = value(entry); // Native positional pairs use the last slot assignment.
        }
        return result;
    }

    private object CivilizationField(EffectiveRecord record, DataNode node)
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
            if (overrides) { result[key] = Reference(family, entry.Children[1].Text, record, node.Name); continue; }
            var granted = StartingGrant(record, symbol, key, unit);
            if (granted is null) continue;
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

    private SortedDictionary<string, CommerceVector> BuildingCommerce(EffectiveRecord record, DataNode node)
    {
        var result = new SortedDictionary<string, CommerceVector>(StringComparer.Ordinal);
        foreach (var entry in node.Children)
        {
            var id = RequiredReference("BuildingClassInfo", entry.Children[0].Text!, record, node.Name);
            var amount = Integer(entry.Children[2].Text);
            var change = NativeKey(entry.Children[1].Text!) switch
            {
                "gold" => new CommerceVector(amount, 0, 0, 0), "research" => new(0, amount, 0, 0),
                "culture" => new(0, 0, amount, 0), "espionage" => new(0, 0, 0, amount),
                _ => throw new InvalidDataException("Unknown commerce type")
            };
            result[id] = result.GetValueOrDefault(id) + change;
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
            NoLimit = Boolean(classRecord.Node.Value("bNoLimit")), Monument = Boolean(classRecord.Node.Value("bMonument"))
        };
        Mark(record.Node.Child(sourceField), sourceField, mapped);
        if (!MappedPaths.TryGetValue(classFamily, out var paths)) MappedPaths[classFamily] = paths = new(StringComparer.Ordinal);
        foreach (var field in new[] { "Type", "DefaultBuilding", "DefaultUnit", "iMaxGlobalInstances", "iMaxTeamInstances", "iMaxPlayerInstances", "iExtraPlayerInstances", "bNoLimit", "bMonument" }) Mark(classRecord.Node.Child(field), field, paths);
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

    public string RequiredReference(string family, string value, EffectiveRecord record, string field) =>
        Reference(family, value, record, field) ?? throw new InvalidDataException($"Required source reference is unresolved: {record.Type}/{field} -> {family}/{value}");

    private string? Missing(EffectiveRecord record, string field, string value)
    {
        Issues.Add(new(ids[(record.Class, record.Type)], field, value, "Native missing-info sentinel; optional reference becomes null. No guessed alias."));
        return null;
    }

    public string Text(string value)
    {
        if (value.StartsWith("TXT_KEY_", StringComparison.Ordinal))
        {
            var text = lookupText(value);
            if (text is null) return Humanize(value[8..]);
            value = text;
        }
        return Branding.Output(Regex.Replace(value, @"\{link:([^}]+)\}(.*?)\{/link\}", match =>
        {
            var body = match.Groups[2].Value;
            if (symbols.TryGetValue(match.Groups[1].Value, out var entry) && SchemaCatalog.Definitions.Any(d => d.SourceClass == entry.Class))
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
    internal static CommerceVector Commerce(DataNode? node)
    {
        var n = Vector(node, 4); return new(n[0], n[1], n[2], n[3]);
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
}
