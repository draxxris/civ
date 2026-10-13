namespace Civ.ContentImport;

public sealed record MergeResult(DataNode Node, string Rule, IReadOnlyList<string> ChangedFields);

public sealed class RecordMerger(IReadOnlyDictionary<string, DataNode> unitClasses,
    IReadOnlyDictionary<string, DataNode> buildingClasses)
{
    public const string Version = "profile-v1-merge-v1";
    private static readonly HashSet<string> ArtClasses = new(StringComparer.Ordinal)
    { "CivilizationArtInfo", "LeaderheadArtInfo", "MiscArtInfo", "UnitArtInfo" };
    private static readonly Dictionary<string, string> CivilizationKeys = new(StringComparer.Ordinal)
    {
        ["Buildings"] = "BuildingClassType", ["Units"] = "UnitClassType",
        ["FreeUnitClasses"] = "UnitClassType", ["FreeBuildingClasses"] = "BuildingClassType",
        ["FreeTechs"] = "TechType", ["DisableTechs"] = "TechType", ["Leaders"] = "LeaderName"
    };

    public MergeResult Merge(DataNode previous, DataNode incoming)
    {
        if (previous.Name != incoming.Name || previous.Value("Type") != incoming.Value("Type"))
            throw new InvalidDataException("Cannot merge different record keys.");
        var force = IsTrue(incoming.Value("bForceOverwrite"));
        DataNode result;
        string rule;
        if (force || ArtClasses.Contains(incoming.Name))
        {
            result = incoming;
            rule = force ? "force-overwrite" : "art-full-replacement";
        }
        else
        {
            (result, rule) = incoming.Name switch
            {
                "EraInfo" => (MergeFields(previous, incoming, MergeEraField), "era-defaults-and-soundtracks"),
                "CivilizationInfo" => (MergeFields(previous, incoming, MergeCivilizationField), "civilization-defaults-and-keyed-arrays"),
                "DiplomacyInfo" => (MergeDiplomacy(previous, incoming), "diplomacy-response-masks"),
                _ => throw new InvalidDataException($"No measured merge policy for {incoming.Name}.")
            };
        }
        var old = previous.Children.GroupBy(x => x.Name).ToDictionary(x => x.Key, x => Canonical(x), StringComparer.Ordinal);
        var updated = result.Children.GroupBy(x => x.Name).ToDictionary(x => x.Key, x => Canonical(x), StringComparer.Ordinal);
        var changed = old.Keys.Union(updated.Keys).Where(x => old.GetValueOrDefault(x) != updated.GetValueOrDefault(x))
            .Order(StringComparer.Ordinal).ToArray();
        return new(result, rule, changed);
    }

    private static string Canonical(IEnumerable<DataNode> nodes) => System.Text.Json.JsonSerializer.Serialize(nodes.Select(x => x.ToTree()));
    public static bool IsTrue(string value) => value is "1" or "true";
    public static bool IsNonDefault(DataNode node) => node.Text is { } value
        ? !string.IsNullOrWhiteSpace(value) && value.Trim() is not ("0" or "NONE" or "-1")
            && !value.Trim().Equals("false", StringComparison.OrdinalIgnoreCase)
        : node.Children.Any(IsNonDefault);

    private static DataNode MergeFields(DataNode old, DataNode next, Func<DataNode, DataNode, DataNode> merge)
    {
        // Match occurrences, not just field names. Repeated fields remain independent.
        var remaining = old.Children.ToList();
        var fields = new List<DataNode>();
        foreach (var field in next.Children)
        {
            var index = remaining.FindIndex(x => x.Name == field.Name);
            if (index < 0) fields.Add(field);
            else { fields.Add(merge(remaining[index], field)); remaining.RemoveAt(index); }
        }
        fields.AddRange(remaining);
        return next with { Children = [.. fields] };
    }

    private static DataNode InheritScalar(DataNode old, DataNode next)
    {
        if (next.Text is null) return next.Children.IsEmpty ? old : next;
        var value = next.Text.Trim();
        return IsEmptyScalar(value) || IsNumericZero(next.Name, value) ? old : next;
    }

    private static bool IsEmptyScalar(string value) => value.Length == 0 || value is "NONE" or "-1"
        || value.Equals("false", StringComparison.OrdinalIgnoreCase);

    private static bool IsNumericZero(string field, string value) => field.Length != 0 && field[0] is 'i' or 'b' or 'f'
        && decimal.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var number) && number == 0;

    private static DataNode MergeEraField(DataNode old, DataNode next) => next.Name switch
    {
        "EraInfoSoundtracks" => Append(old, next, false), // Source appends old tracks, including duplicates.
        "CitySoundscapes" => MergePositions(old, next),
        _ => InheritScalar(old, next)
    };

    private DataNode MergeCivilizationField(DataNode old, DataNode next)
    {
        if (next.Name == "Cities") return Append(old, next, true);
        if (next.Name == "InitialCivics") return MergePositions(old, next);
        if (CivilizationKeys.TryGetValue(next.Name, out var key)) return MergeKeyed(old, next, key);
        return InheritScalar(old, next);
    }

    private DataNode MergeKeyed(DataNode old, DataNode next, string key)
    {
        var remaining = old.Children.ToList();
        var items = new List<DataNode>();
        foreach (var item in next.Children)
        {
            var index = remaining.FindIndex(x => x.Value(key) == item.Value(key));
            if (index < 0) { items.Add(item); continue; }
            var prior = remaining[index];
            remaining.RemoveAt(index);
            if (next.Name is "Buildings" or "Units")
            {
                var classes = next.Name == "Buildings" ? buildingClasses : unitClasses;
                var valueField = next.Name == "Buildings" ? "BuildingType" : "UnitType";
                var defaultField = next.Name == "Buildings" ? "DefaultBuilding" : "DefaultUnit";
                var defaultValue = classes.GetValueOrDefault(item.Value(key))?.Value(defaultField);
                // NONE explicitly disables a class. Only the class default inherits a previous override.
                items.Add(item.Value(valueField) == defaultValue ? prior : item);
            }
            else items.Add(MergeFields(prior, item, InheritScalar));
        }
        items.AddRange(remaining);
        return next with { Text = null, Children = [.. items] };
    }

    private static DataNode MergePositions(DataNode old, DataNode next)
    {
        var items = next.Children.Select((x, i) => i < old.Children.Length ? InheritScalar(old.Children[i], x) : x).ToList();
        items.AddRange(old.Children.Skip(items.Count));
        return next with { Text = null, Children = [.. items] };
    }

    private static DataNode Append(DataNode old, DataNode next, bool unique)
    {
        var items = next.Children.ToList();
        foreach (var item in old.Children)
            if (!unique || !items.Any(x => Canonical([x]) == Canonical([item]))) items.Add(item);
        return next with { Text = null, Children = [.. items] };
    }

    private DataNode MergeDiplomacy(DataNode old, DataNode next)
    {
        var responses = old.Child("Responses")?.Children.ToList() ?? [];
        foreach (var response in next.Child("Responses")?.Children ?? [])
        {
            var index = responses.Count == 1 ? 0 : responses.FindIndex(x => MatchesResponse(x, response));
            var merged = response;
            if (index >= 0)
            {
                merged = MergeFields(responses[index], response, (a, b) => b.Name == "DiplomacyText"
                    ? Append(a, b, true) : MergeMask(a, b));
                responses.RemoveAt(index);
            }
            responses.Add(merged);
        }
        return MergeFields(old, next, (a, b) => b.Name == "Responses"
            ? DataNode.Branch("Responses", responses) : InheritScalar(a, b));
    }

    private static readonly string[] ResponseAxes = ["Leaders", "Attitudes", "DiplomacyPowers"];
    private static HashSet<string> Mask(DataNode? axis) => axis?.Children
        .Where(x => x.Children.Any(c => c.Name.StartsWith('b') && IsTrue(c.Text ?? "")))
        .Select(x => x.Children.First(c => !c.Name.StartsWith('b')).Text ?? "")
        .ToHashSet(StringComparer.Ordinal) ?? new(StringComparer.Ordinal);

    private static bool MatchesResponse(DataNode old, DataNode next) => ResponseAxes.All(axis =>
    {
        var a = Mask(old.Child(axis));
        var b = Mask(next.Child(axis));
        return a.Count == 0 && b.Count == 0 || a.Overlaps(b);
    });

    private static DataNode MergeMask(DataNode old, DataNode next)
    {
        var items = next.Children.ToList();
        foreach (var item in old.Children)
        {
            var key = item.Children.FirstOrDefault(x => !x.Name.StartsWith('b'));
            var index = items.FindIndex(x => x.Value(key?.Name ?? "") == key?.Text);
            if (index < 0) items.Add(item);
            else items[index] = MergeFields(item, items[index], InheritScalar);
        }
        return next with { Text = null, Children = [.. items] };
    }
}
