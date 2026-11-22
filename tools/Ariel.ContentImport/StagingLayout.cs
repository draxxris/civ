namespace Ariel.ContentImport;

/// <summary>Where an import run writes by default: an untracked, git-ignored directory at the repository root that mirrors
/// the committed layout (<c>rules/&lt;Module&gt;/data</c>, <c>mods/&lt;Module&gt;/data</c>, <c>reports</c>). The importer
/// never writes into the tracked <c>src</c> tree or the committed reports, so a run cannot overwrite hand-maintained data.</summary>
public sealed record StagingLayout(string Repository)
{
    public const string DirectoryName = ".import-staging";

    public string Root => Path.Combine(Repository, DirectoryName);
    public string Rules => Path.Combine(Root, "rules");
    public string Mods => Path.Combine(Root, "mods");
    public string Reports => Path.Combine(Root, "reports");

    /// <summary>The tracked module roots that staging mirrors, for the diff.</summary>
    public ContentRoots Committed => new(Path.Combine(Repository, "src", "Ariel.Rules"), Path.Combine(Repository, "src", "Mods"));
    public string CommittedReports => Path.Combine(Repository, "tools", "Ariel.ContentImport", "reports");

    /// <summary>The repository holding the working directory (the nearest ancestor with <c>Ariel.slnx</c>).</summary>
    public static StagingLayout Locate()
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Ariel.slnx"))) return new(directory.FullName);
        throw new ArgumentException("Run the importer from inside the repository (Ariel.slnx not found above the working directory).");
    }

    /// <summary>Refuses an output directory that would overwrite committed content.</summary>
    public void RejectTracked(string target)
    {
        foreach (var protectedPath in new[] { Path.Combine(Repository, "src"), CommittedReports })
            if (target == protectedPath || target.StartsWith(protectedPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new ArgumentException($"Refusing to write into tracked content ({Path.GetRelativePath(Repository, protectedPath)}). "
                    + $"The importer writes to {DirectoryName}/ by default; bring changes in with `mise run import:diff`.");
    }
}
