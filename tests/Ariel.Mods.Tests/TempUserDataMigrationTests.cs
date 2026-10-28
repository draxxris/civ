using Ariel.Client;
using Xunit;

namespace Ariel.Mods.Tests;

// Temporary: covers the one-time user-data copy during the rename. Removed once the migration has shipped.
public sealed class TempUserDataMigrationTests
{
    [Fact]
    public void MissingLegacyDirectoryIsNotACopyAndLeavesNoMarker()
    {
        using var tree = new Tree();
        var report = UserDataMigration.Migrate(tree.Legacy, tree.Current);
        Assert.False(report.MigratedNow);
        Assert.Empty(report.Conflicts);
        Assert.False(File.Exists(Path.Combine(tree.Current, UserDataMigration.MarkerFileName)));
        Assert.False(Directory.Exists(tree.Current));
    }

    [Fact]
    public void FirstRunCopiesSavesModsAndProfileByteForByte()
    {
        using var tree = new Tree();
        tree.Write("Civ", "saves/game.json", "save");
        tree.Write("Civ", "mods/my-mod/mod.json", "{\"id\":\"my-mod\"}");
        tree.Write("Civ", "mod-profile.json", "{\"Version\":1,\"EnabledModIds\":[]}");

        var report = UserDataMigration.Migrate(tree.Legacy, tree.Current);

        Assert.True(report.MigratedNow);
        Assert.Empty(report.Conflicts);
        Assert.Equal("save", tree.Read("Ariel", "saves/game.json"));
        Assert.Equal("{\"id\":\"my-mod\"}", tree.Read("Ariel", "mods/my-mod/mod.json"));
        Assert.Equal("{\"Version\":1,\"EnabledModIds\":[]}", tree.Read("Ariel", "mod-profile.json"));
        Assert.True(File.Exists(Path.Combine(tree.Current, UserDataMigration.MarkerFileName)));
    }

    [Fact]
    public void CompletedMigrationNeverCopiesOrReportsAgain()
    {
        using var tree = new Tree();
        tree.Write("Civ", "saves/first.json", "one");
        tree.Write("Civ", "saves/second.json", "two");
        UserDataMigration.Migrate(tree.Legacy, tree.Current);
        tree.Write("Civ", "saves/added-later.json", "three");

        var rerun = UserDataMigration.Migrate(tree.Legacy, tree.Current);

        Assert.False(rerun.MigratedNow);
        Assert.Empty(rerun.Conflicts);
        Assert.False(File.Exists(Path.Combine(tree.Current, "saves", "added-later.json")));
    }

    [Fact]
    public void InterruptedCopyResumesWithoutConflictsOrPartialFiles()
    {
        using var tree = new Tree();
        tree.Write("Civ", "saves/done.json", "done");
        tree.Write("Civ", "saves/pending.json", "pending");
        // A previous run copied one file, died mid-way, and left its temporary file behind.
        tree.Write("Ariel", "saves/done.json", "done");
        tree.Write("Ariel", "saves/pending.json.migrating", "pen");

        var report = UserDataMigration.Migrate(tree.Legacy, tree.Current);

        Assert.True(report.MigratedNow);
        Assert.Empty(report.Conflicts);
        Assert.Equal("done", tree.Read("Ariel", "saves/done.json"));
        Assert.Equal("pending", tree.Read("Ariel", "saves/pending.json"));
        Assert.False(File.Exists(Path.Combine(tree.Current, "saves", "pending.json.migrating")));
    }

    [Fact]
    public void ConflictingFilesAreBothKeptReportedOnceAndTheNewCopyWins()
    {
        using var tree = new Tree();
        tree.Write("Civ", "saves/game.json", "old");
        tree.Write("Civ", "mod-profile.json", "old profile");
        tree.Write("Ariel", "saves/game.json", "new");
        tree.Write("Ariel", "saves/game.json.migrating", "new");
        tree.Write("Ariel", "mod-profile.json", "new profile");

        var report = UserDataMigration.Migrate(tree.Legacy, tree.Current);

        Assert.True(report.MigratedNow);
        Assert.Equal(["saves/game.json", "mod-profile.json"], report.Conflicts);
        Assert.Equal("new", tree.Read("Ariel", "saves/game.json"));
        Assert.Equal("old", tree.Read("Civ", "saves/game.json"));
        Assert.Equal("new profile", tree.Read("Ariel", "mod-profile.json"));
        Assert.False(File.Exists(Path.Combine(tree.Current, "saves", "game.json.migrating")));
        Assert.Equal("old profile", tree.Read("Civ", "mod-profile.json"));

        var rerun = UserDataMigration.Migrate(tree.Legacy, tree.Current);
        Assert.False(rerun.MigratedNow);
        Assert.Empty(rerun.Conflicts);
    }

    [Fact]
    public void LegacyRootIsTheSiblingOldApplicationNameDirectory()
    {
        string current = Path.Combine(Path.GetTempPath(), "godot", "app_userdata", "Ariel");
        Assert.Equal(Path.Combine(Path.GetTempPath(), "godot", "app_userdata", "Civ"), UserDataMigration.LegacyRootFor(current));
        // A build that still uses the old name migrates nothing onto itself.
        var report = UserDataMigration.Migrate(current, current);
        Assert.False(report.MigratedNow);
        Assert.Empty(report.Conflicts);
    }

    private sealed class Tree : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "ariel-user-data-" + Guid.NewGuid().ToString("N"));
        public string Legacy => Path.Combine(Root, "Civ");
        public string Current => Path.Combine(Root, "Ariel");
        public void Write(string application, string relative, string text)
        {
            string path = Path.Combine(Root, application, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
        }
        public string Read(string application, string relative) =>
            File.ReadAllText(Path.Combine(Root, application, relative));
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true); }
    }
}
