using Civ.ContentImport;

try
{
    var options = ParseArguments(args);
    var source = Path.GetFullPath(options["--source"]);
    var reports = Path.GetFullPath(options["--reports"]);
    var dump = options.TryGetValue("--dump", out var destination) ? Path.GetFullPath(destination) : null;
    PreventSourceWrites(source, reports);
    if (dump is not null) PreventSourceWrites(source, dump);
    var registry = new ImportRegistry(source);
    var mods = Path.GetFullPath(options.GetValueOrDefault("--out-mods", "src/Mods"));
    PreventSourceWrites(source, mods);
    var emission = ContentEmission.Compile(registry, options.GetValueOrDefault("--max-era"));
    ContentEmission.Write(emission, mods, reports);
    Reports.Write(registry, source, reports, dump, emission.MappedPaths);
    Console.WriteLine($"Records: {registry.Records.Count}; classes: {registry.Records.Select(x => x.Class).Distinct().Count()}; "
        + $"collision keys: {registry.Collisions.Count}; merges: {registry.Merges.Count}; "
        + $"unresolved text keys: {registry.UnresolvedText.Count}; branding hits: {registry.BrandingHits.Count}.");
    Console.WriteLine($"Typed definitions: {emission.Definitions.Count}; dropped: {emission.Dropped.Count}.");
    return 0;
}
catch (Exception exception) when (exception is ArgumentException or IOException or System.Text.Json.JsonException
    or System.Xml.XmlException or UnauthorizedAccessException)
{
    Console.Error.WriteLine(Branding.Identifier(exception.Message));
    return 1;
}

static Dictionary<string, string> ParseArguments(string[] arguments)
{
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    if (arguments.Length == 1 && arguments[0] is "--help" or "-h")
    {
        Console.WriteLine("Usage: --source <extraction-directory> --reports <report-directory> [--dump <directory>] [--out-mods <directory>] [--max-era <era-id>]");
        Environment.Exit(0);
    }
    for (var index = 0; index < arguments.Length; index += 2)
    {
        var key = arguments[index];
        if (key is not ("--source" or "--reports" or "--dump" or "--out-mods" or "--max-era") || index + 1 == arguments.Length || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException("Use --source <directory> --reports <directory> [--dump <directory>] [--out-mods <directory>] [--max-era <era-id>].");
        if (!options.TryAdd(key, arguments[index + 1])) throw new ArgumentException("Duplicate option: " + key);
    }
    if (!options.ContainsKey("--source") || !options.ContainsKey("--reports")) throw new ArgumentException("Both --source and --reports are required.");
    return options;
}

static void PreventSourceWrites(string source, string target)
{
    if (target == source || target.StartsWith(source.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        throw new ArgumentException("Output must remain outside the source directory.");
}
