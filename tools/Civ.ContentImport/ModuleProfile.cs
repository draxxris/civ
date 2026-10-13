using System.Text.Json;

namespace Civ.ContentImport;

public sealed record ProfileFile(string ExportPath, bool IsModule, int Order);

public sealed class ModuleProfile
{
    public const string SourceLayer = "and";
    public IReadOnlyList<ProfileFile> Files { get; }
    public IReadOnlyList<string> ModuleDirectories { get; }

    public ModuleProfile(string dataDirectory)
    {
        using var document = JsonDocument.Parse(string.Join("\n", File.ReadLines(
            Path.Combine(dataDirectory, "module-controls.yaml")).Where(x => !x.StartsWith('#'))));
        var controls = document.RootElement.GetProperty("controls").EnumerateArray()
            .Where(x => x.GetProperty("layer").GetString() == SourceLayer)
            .ToDictionary(x => Parent(x.GetProperty("path").GetString()!), x => x.Clone(), StringComparer.Ordinal);
        var directories = new List<string>();
        Visit("Modules", controls, directories);
        ModuleDirectories = directories.AsReadOnly();

        // Whole-file fallback applies only to the core XML tree, not modules.
        var core = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var layer in new[] { "base", "warlords", "bts", SourceLayer })
        {
            var folder = Path.Combine(dataDirectory, "layers", layer, "XML");
            if (!Directory.Exists(folder)) continue;
            foreach (var file in Directory.GetFiles(folder, "*.xml.yaml", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
                if (IsDataFile(file)) core[Path.GetRelativePath(folder, file).Replace('\\', '/')] = file;
        }
        var civicRecovery = Path.Combine(dataDirectory, "recovered", SourceLayer, "XML/GameInfo/CIV4CivicInfos.xml.yaml");
        if (!File.Exists(civicRecovery)) throw new InvalidDataException("The core civic recovery is missing.");
        core["GameInfo/CIV4CivicInfos.xml.yaml"] = civicRecovery;
        var files = core.Values.Select(x => new ProfileFile(x, false, 0)).ToList();
        foreach (var directory in directories)
        {
            var folder = Path.Combine(dataDirectory, "layers", SourceLayer, directory);
            if (!Directory.Exists(folder)) continue;
            files.AddRange(Directory.GetFiles(folder, "*.xml.yaml", SearchOption.AllDirectories)
                .Where(IsDataFile).Order(StringComparer.Ordinal).Select(x => new ProfileFile(x, true, 0)));
        }
        Files = files.Select((x, i) => x with { Order = i }).ToArray();
    }

    private static void Visit(string directory, Dictionary<string, JsonElement> controls, List<string> result)
    {
        if (!controls.TryGetValue(directory, out var control)) { result.Add(directory); return; }
        var selected = control.GetProperty("defaultConfiguration").GetString();
        var configuration = control.GetProperty("configurations").EnumerateArray()
            .FirstOrDefault(x => x.GetProperty("type").GetString() == selected);
        if (selected == "NONE" || configuration.ValueKind == JsonValueKind.Undefined)
            throw new InvalidDataException("The profile needs a usable default module configuration.");
        foreach (var module in configuration.GetProperty("modules").EnumerateArray())
            if (module.GetProperty("load").GetString() is "1" or "true")
                Visit(directory + "/" + module.GetProperty("directory").GetString(), controls, result);
    }

    private static string Parent(string path) => path[..path.LastIndexOf('/')];
    private static bool IsDataFile(string path)
    {
        var file = Path.GetFileName(path);
        return !file.StartsWith("README", StringComparison.OrdinalIgnoreCase)
            && !file.Contains("Schema", StringComparison.OrdinalIgnoreCase)
            && !file.Contains("ModularLoadingControls", StringComparison.Ordinal);
    }
}
