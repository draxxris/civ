using System.Security.Cryptography;

namespace Ariel.Client;

/// <summary>
/// One-time copy of saves, installed mods and the mod profile from the user-data directory the pre-rename build
/// used into the current one. Path-based and engine-free so ordinary tests can exercise it without Godot.
/// </summary>
/// <remarks>
/// The copy is restart-safe. Each file is written to a temporary name, verified against its source, then moved into
/// place, so an interrupted run never leaves a partial destination file. A rerun skips identical files and resumes
/// the rest. When both locations hold a different file, neither is touched: the run keeps using the new location and
/// reports the conflict. The completion marker is written only once every copied file verifies, and its presence
/// stops later runs from reporting the same conflicts again.
/// </remarks>
public static class UserDataMigration
{
    /// <summary>Godot names the user-data directory after <c>application/config/name</c>; this is the old name.</summary>
    public const string LegacyApplicationName = "Civ";

    /// <summary>Records that the copy finished, and which files conflicted when it did.</summary>
    public const string MarkerFileName = "user-data-migration.json";

    /// <summary>Saves first, so a player's games are the first thing that moves, then mods, then the profile.</summary>
    private static readonly string[] Entries = ["saves", "mods", "mod-profile.json"];

    private const int MarkerVersion = 1;

    /// <summary>What a run did: whether this run copied anything, and the destination-relative files it left alone.</summary>
    public sealed record Report(bool MigratedNow, IReadOnlyList<string> Conflicts);

    /// <summary>The sibling user-data directory the old application name produced.</summary>
    public static string LegacyRootFor(string userDataRoot)
    {
        var current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(userDataRoot));
        var parent = Path.GetDirectoryName(current);
        return parent == null ? current : Path.Combine(parent, LegacyApplicationName);
    }

    public static Report Migrate(string legacyRoot, string currentRoot)
    {
        var legacy = Path.TrimEndingDirectorySeparator(Path.GetFullPath(legacyRoot));
        var current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(currentRoot));
        // A build without the new application name still writes here; copying onto itself would read as a conflict.
        if (string.Equals(legacy, current, PathComparison)) return new(false, []);
        if (!Directory.Exists(legacy)) return new(false, []);
        var marker = Path.Combine(current, MarkerFileName);
        if (HasMarker(marker)) return new(false, []);
        Directory.CreateDirectory(current);
        var conflicts = new List<string>();
        foreach (var entry in Entries) CopyEntry(entry, legacy, current, conflicts);
        WriteMarker(marker, conflicts);
        return new(true, conflicts);
    }

    private static bool HasMarker(string marker)
    {
        if (!File.Exists(marker)) return false;
        // An unreadable or older marker means the previous run did not finish recording its outcome.
        try
        {
            var record = System.Text.Json.JsonSerializer.Deserialize<Marker>(File.ReadAllText(marker));
            return record is { Version: MarkerVersion };
        }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private static void WriteMarker(string marker, IReadOnlyList<string> conflicts)
    {
        var temporary = marker + ".tmp";
        try
        {
            File.WriteAllText(temporary, System.Text.Json.JsonSerializer.Serialize(
                new Marker(MarkerVersion, DateTimeOffset.UtcNow, conflicts.ToArray()),
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, marker, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void CopyEntry(string relative, string legacy, string current, List<string> conflicts)
    {
        var directory = Path.Combine(legacy, relative);
        if (!Directory.Exists(directory))
        {
            CopyFile(relative, legacy, current, conflicts);
            return;
        }
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            CopyFile(Path.GetRelativePath(legacy, file), legacy, current, conflicts);
    }

    private static void CopyFile(string relative, string legacy, string current, List<string> conflicts)
    {
        var source = Path.Combine(legacy, relative);
        if (!File.Exists(source)) return;
        var destination = Path.Combine(current, relative);
        var temporary = destination + ".migrating";
        if (File.Exists(destination))
        {
            // Whatever this run decides, the temporary file of an earlier interrupted run is debris, not data.
            if (File.Exists(temporary)) File.Delete(temporary);
            if (!SameContent(source, destination)) conflicts.Add(relative);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        try
        {
            File.Copy(source, temporary, overwrite: true);
            if (!SameContent(source, temporary))
                throw new IOException($"The copied file '{relative}' does not match its source in the old data folder.");
            File.Move(temporary, destination, overwrite: false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static bool SameContent(string left, string right)
    {
        if (new FileInfo(left).Length != new FileInfo(right).Length) return false;
        using var leftStream = File.OpenRead(left);
        using var rightStream = File.OpenRead(right);
        return SHA256.HashData(leftStream).AsSpan().SequenceEqual(SHA256.HashData(rightStream));
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private sealed record Marker(int Version, DateTimeOffset CompletedUtc, string[] Conflicts);
}
