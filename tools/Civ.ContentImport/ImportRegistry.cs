namespace Civ.ContentImport;

public sealed record Contribution(string Class, string Type, DataNode Node, TreeDocument Source, int FileOrder, int Ordinal);
public sealed record EffectiveRecord(string Class, string Type, DataNode Node, IReadOnlyList<string> Sources, string? Era = null);
public sealed record Collision(string Class, string Type, IReadOnlyList<string> Sources, int Definitions);
public sealed record MergeLog(string Class, string Type, string Source, string Rule, IReadOnlyList<string> ChangedFields);
public sealed record DefineValue(string Value, string ValueKind, string Source);
public sealed record DefineOverride(string Key, string OldValue, string NewValue, string Source);

public sealed class ImportRegistry
{
    public ModuleProfile Profile { get; }
    public IReadOnlyList<EffectiveRecord> Records { get; }
    public IReadOnlyList<Collision> Collisions { get; }
    public List<MergeLog> Merges { get; } = [];
    public List<string> IgnoredContributions { get; } = [];
    public SortedDictionary<string, DefineValue> Defines { get; } = new(StringComparer.Ordinal);
    public List<DefineOverride> DefineOverrides { get; } = [];
    public List<string> DefineFiles { get; } = [];
    public List<string> Warnings { get; } = [];
    public RecoveryEvidence Recovery { get; }
    public EnglishText Text { get; }
    public IReadOnlyList<IdEntry> Ids { get; }
    public IReadOnlyList<UnresolvedText> UnresolvedText { get; }
    public IReadOnlyList<BrandingHit> BrandingHits { get; }

    public ImportRegistry(string sourceDirectory)
    {
        var data = Path.Combine(sourceDirectory, "data");
        Profile = new(data);
        var reader = new TreeReader(data);
        var documents = Profile.Files.Select(x => (File: x, Tree: reader.Read(x.ExportPath))).ToArray();
        Recovery = CivicRecovery.Check(data, documents.Single(x => x.Tree.Format == "and-recovered-xml-tree-v1").Tree);
        LoadDefines(data, reader, documents);
        var contributions = documents.SelectMany(x => ReadRecords(x.File, x.Tree)).ToArray();
        var groups = contributions.GroupBy(x => (x.Class, x.Type)).OrderBy(x => x.Key.Class, StringComparer.Ordinal)
            .ThenBy(x => x.Key.Type, StringComparer.Ordinal).ToArray();
        // Measure the raw active definitions before applying force flags or any merges.
        Collisions = groups.Where(x => x.Count() > 1).Select(x => new Collision(x.Key.Class, x.Key.Type,
            x.Select(y => SourceLabel(y.Source)).ToArray(), x.Count())).ToArray();
        var merger = new RecordMerger(ClassTable(contributions, "UnitClassInfo"), ClassTable(contributions, "BuildingClassInfo"));
        var effective = groups.Select(x => Accumulate(x, merger)).Where(x => x is not null).Cast<EffectiveRecord>().ToArray();
        Records = EraTagging.Tag(effective, contributions);
        Ids = IdMapping.Create(Records);
        Text = new(data, documents.Where(x => !x.File.IsModule && x.Tree.Layer != ModuleProfile.SourceLayer).Select(x => x.Tree),
            documents.Where(x => x.File.IsModule).Select(x => x.Tree));
        UnresolvedText = FindUnresolved();
        BrandingHits = Text.BrandingHits.Concat(Records.SelectMany(x => x.Node.DescendantsAndSelf()
                .Where(n => n.Text is not null).SelectMany(n => Branding.Scan(x.Class + "/" + x.Type + "/" + n.Name, n.Text!))))
            .OrderBy(x => x.Context, StringComparer.Ordinal).ThenBy(x => x.Rule, StringComparer.Ordinal).ToArray();
        Warnings.Add("Module files use ordinal path order within each MLF directory. Native filesystem order is not captured.");
        Warnings.Add("Generic records retain source strings and unresolved references in memory. Dumps are neutral development data, not typed game content.");
        Warnings.Add("Diplomacy response matching uses mask intersections without the native loader's loop-state bugs.");
    }

    private static IEnumerable<Contribution> ReadRecords(ProfileFile file, TreeDocument tree)
    {
        var ordinal = 0;
        foreach (var node in tree.Root.DescendantsAndSelf())
            if (node.Name.EndsWith("Info", StringComparison.Ordinal) && node.Value("Type") is { Length: > 0 } type)
                yield return new(node.Name, type, node, tree, file.Order, ordinal++);
    }

    private static IReadOnlyDictionary<string, DataNode> ClassTable(IEnumerable<Contribution> records, string family) =>
        records.Where(x => x.Class == family).GroupBy(x => x.Type, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Last().Node, StringComparer.Ordinal);

    private EffectiveRecord? Accumulate(IEnumerable<Contribution> group, RecordMerger merger)
    {
        DataNode? node = null;
        var sources = new List<string>();
        foreach (var contribution in group)
        {
            var label = SourceLabel(contribution.Source);
            if (RecordMerger.IsTrue(contribution.Node.Value("bForceDelete"))
                || Defines.TryGetValue(contribution.Type, out var value) && int.TryParse(value.Value, out var flag) && flag != 0)
            {
                IgnoredContributions.Add(label + "/" + contribution.Type + ": discard incoming definition (not the old record)");
                continue;
            }
            RejectConditionalRecord(contribution);
            if (node is not null)
            {
                var merge = merger.Merge(node, contribution.Node);
                Merges.Add(new(contribution.Class, contribution.Type, label, merge.Rule, merge.ChangedFields));
                node = merge.Node;
            }
            else node = contribution.Node;
            sources.Add(label);
        }
        return node is null ? null : new(node.Name, node.Value("Type"), node, sources);
    }

    private static void RejectConditionalRecord(Contribution record)
    {
        var controls = new[] { "bTypeDependency", "AndDependencyTypes", "OrDependencyTypes", "ReplacementCondition" };
        if (controls.Any(x => record.Node.Child(x) is { } node && RecordMerger.IsNonDefault(node)))
            throw new InvalidDataException($"Conditional loading requires an explicit policy for {record.Class}/{record.Type}.");
    }

    public static string SourceLabel(TreeDocument tree) => Branding.Identifier(tree.Layer + "/" + tree.SourcePath);

    private void LoadDefines(string data, TreeReader reader, (ProfileFile File, TreeDocument Tree)[] documents)
    {
        var order = new[] { "GlobalDefines.xml", "GlobalDefinesAlt.xml", "ParallelMaps_GlobalDefines.xml",
            "source_GlobalDefines.xml", "BBAI_Game_Options_GlobalDefines.xml", "BBAI_AI_Variables_GlobalDefines.xml",
            "TechDiffusion_GlobalDefines.xml", "Inthegrave_GlobalDefines.xml", "LeadFromBehind_GlobalDefines.xml" };
        var core = documents.Where(x => !x.File.IsModule && IsDefines(x.Tree)).Select(x => x.Tree).ToList();
        foreach (var file in order)
        {
            var index = core.FindIndex(x => Branding.Identifier(Path.GetFileName(x.SourcePath)) == file);
            if (index >= 0) { ApplyDefines(core[index]); core.RemoveAt(index); }
            else Warnings.Add("A reference-loader define file is absent: " + Branding.Identifier(file));
        }
        var font = Path.Combine(data, "layers", ModuleProfile.SourceLayer, "res/Fonts/GameFont_GlobalDefines.xml.yaml");
        if (File.Exists(font)) ApplyDefines(reader.Read(font));
        foreach (var tree in core.OrderBy(x => x.SourcePath, StringComparer.Ordinal))
        {
            Warnings.Add("Extra define file loads after reference-loader core files: " + SourceLabel(tree));
            ApplyDefines(tree);
        }
        foreach (var tree in documents.Where(x => x.File.IsModule && IsDefines(x.Tree)).Select(x => x.Tree)) ApplyDefines(tree);
    }

    private static bool IsDefines(TreeDocument tree) => tree.Root.Name == "Civ4Defines";
    private void ApplyDefines(TreeDocument tree)
    {
        DefineFiles.Add(SourceLabel(tree));
        foreach (var node in tree.Root.Children.Where(x => x.Name == "Define"))
        {
            var key = node.Value("DefineName");
            var value = node.Children.FirstOrDefault(x => x.Name != "DefineName");
            if (key.Length == 0 || value?.Text is null) throw new InvalidDataException("Malformed global define.");
            if (Defines.TryGetValue(key, out var old)) DefineOverrides.Add(new(key, old.Value, value.Text, SourceLabel(tree)));
            Defines[key] = new(value.Text, value.Name, SourceLabel(tree));
        }
    }

    private IReadOnlyList<UnresolvedText> FindUnresolved()
    {
        var uses = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var record in Records)
            foreach (var field in record.Node.DescendantsAndSelf().Where(x => x.Text is not null))
                foreach (var key in EnglishText.KeysIn(field.Text!))
                {
                    if (Text.Lookup(key) is not null) continue;
                    if (!uses.TryGetValue(key, out var contexts)) uses[key] = contexts = new(StringComparer.Ordinal);
                    contexts.Add(IdMapping.CanonicalClass(record.Class) + "/" + record.Type + "/" + field.Name);
                }
        return uses.Select(x => new UnresolvedText(x.Key, x.Value.ToArray())).ToArray();
    }
}

public static class EraTagging
{
    public static IReadOnlyList<EffectiveRecord> Tag(IReadOnlyList<EffectiveRecord> records, IEnumerable<Contribution> source)
    {
        var eraOrder = source.Where(x => x.Class == "EraInfo").Select(x => x.Type).Distinct(StringComparer.Ordinal)
            .Select((x, i) => (x, i)).ToDictionary(x => x.x, x => x.i, StringComparer.Ordinal);
        var techEras = records.Where(x => x.Class == "TechInfo").ToDictionary(x => x.Type, x => x.Node.Value("Era"), StringComparer.Ordinal);
        return records.Select(x => x with { Era = Derive(x, techEras, eraOrder) }).ToArray();
    }

    private static string? Derive(EffectiveRecord record, Dictionary<string, string> techEras, Dictionary<string, int> eraOrder)
    {
        if (record.Class == "EraInfo") return record.Type;
        if (record.Class == "TechInfo") return eraOrder.ContainsKey(record.Node.Value("Era")) ? record.Node.Value("Era") : null;
        // No geometry or availability filter is applied here. These are lower-bound tech-era annotations.
        var prerequisites = new List<string>();
        foreach (var name in new[] { "PrereqTech", "TechPrereq" })
            if (record.Node.Value(name).StartsWith("TECH_", StringComparison.Ordinal)) prerequisites.Add(record.Node.Value(name));
        if (record.Node.Child("TechTypes") is { } list)
            prerequisites.AddRange(list.DescendantsAndSelf().Where(x => x.Text?.StartsWith("TECH_", StringComparison.Ordinal) == true).Select(x => x.Text!));
        if (prerequisites.Count == 0 || prerequisites.Any(x => !techEras.ContainsKey(x))) return null;
        return prerequisites.Select(x => techEras[x]).Where(eraOrder.ContainsKey)
            .OrderByDescending(x => eraOrder[x]).FirstOrDefault();
    }
}
