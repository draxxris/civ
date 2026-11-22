using Ariel.ContentImport;

try
{
    if (args.Length > 0 && args[0] == "diff") return ImportDiff.Run(args[1..]);
    var options = ParseArguments(args);
    var staging = StagingLayout.Locate();
    var source = Path.GetFullPath(options["--source"]);
    var reports = Path.GetFullPath(options.GetValueOrDefault("--reports", staging.Reports));
    var dump = options.TryGetValue("--dump", out var destination) ? Path.GetFullPath(destination) : null;
    PreventSourceWrites(source, reports);
    if (dump is not null) PreventSourceWrites(source, dump);
    var registry = new ImportRegistry(source);
    var roots = new ContentRoots(Path.GetFullPath(options.GetValueOrDefault("--out-rules", staging.Rules)),
        Path.GetFullPath(options.GetValueOrDefault("--out-mods", staging.Mods)));
    foreach (var target in new[] { reports, roots.Rules, roots.Mods }) staging.RejectTracked(target);
    // A default run replaces its previous staged output, so a family the importer no longer emits cannot linger.
    if (Directory.Exists(staging.Root) && !options.ContainsKey("--reports") && !options.ContainsKey("--out-rules") && !options.ContainsKey("--out-mods"))
        Directory.Delete(staging.Root, true);
    PreventSourceWrites(source, roots.Rules);
    PreventSourceWrites(source, roots.Mods);
    var emission = ContentEmission.Compile(registry, options.GetValueOrDefault("--max-era"));
    ContentEmission.Write(emission, roots, reports);
    Reports.Write(registry, source, reports, dump, emission.MappedPaths);
    Console.WriteLine($"Records: {registry.Records.Count}; classes: {registry.Records.Select(x => x.Class).Distinct().Count()}; "
        + $"collision keys: {registry.Collisions.Count}; merges: {registry.Merges.Count}; "
        + $"unresolved text keys: {registry.UnresolvedText.Count}; branding hits: {registry.BrandingHits.Count}.");
    Console.WriteLine($"Wrote {Path.GetRelativePath(staging.Repository, roots.Rules)}, {Path.GetRelativePath(staging.Repository, roots.Mods)} and {Path.GetRelativePath(staging.Repository, reports)}; committed data is untouched. Compare with `mise run import:diff`.");
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
        Console.WriteLine("Usage: [--source <extraction-directory>] [--reports <report-directory>] [--dump <directory>] [--out-rules <directory>] [--out-mods <directory>] [--max-era <era-id>]");
        Console.WriteLine("Output goes to .import-staging/ unless redirected, and never into src/. 'diff [Module ...] [--values]' compares .import-staging/ with the committed data.");
        Environment.Exit(0);
    }
    for (var index = 0; index < arguments.Length; index += 2)
    {
        var key = arguments[index];
        if (key is not ("--source" or "--reports" or "--dump" or "--out-rules" or "--out-mods" or "--max-era") || index + 1 == arguments.Length || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException("Use --source <directory> [--reports <directory>] [--dump <directory>] [--out-rules <directory>] [--out-mods <directory>] [--max-era <era-id>].");
        if (!options.TryAdd(key, arguments[index + 1])) throw new ArgumentException("Duplicate option: " + key);
    }
    if (!options.ContainsKey("--source"))
    {
        var supplied = Environment.GetEnvironmentVariable("ARIEL_CONTENT_SOURCE");
        if (string.IsNullOrWhiteSpace(supplied)) throw new ArgumentException("--source is required (or set ARIEL_CONTENT_SOURCE).");
        options["--source"] = supplied;
    }
    return options;
}

static void PreventSourceWrites(string source, string target)
{
    if (target == source || target.StartsWith(source.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        throw new ArgumentException("Output must remain outside the source directory.");
}
