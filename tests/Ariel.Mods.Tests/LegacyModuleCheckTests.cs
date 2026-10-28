using System.Text;
using System.Text.Json;
using Ariel.Client;
using Ariel.Modding;
using Xunit;

namespace Ariel.Mods.Tests;

public sealed class LegacyModuleCheckTests
{
    [Theory]
    [InlineData("Civ", true)]
    [InlineData("Civ.Core", true)]
    [InlineData("Civ.Client.Core", true)]
    [InlineData("Civ.Mods.World", true)]
    [InlineData("Ariel.Core", false)]
    [InlineData("Ariel.Mods.World", false)]
    [InlineData("CivicsTools", false)]
    [InlineData("Civ4", false)]
    [InlineData("Civ-Core", false)]
    [InlineData("System.Runtime", false)]
    public void OnlyPreRenameAssemblyNamesAreRejected(string name, bool legacy) =>
        Assert.Equal(legacy, LegacyModuleCheck.IsPreRenameName(name));

    [Fact]
    public void MissingOrUnreadableAssembliesAreNotClaimedAsLegacy()
    {
        using var directory = new Tree();
        Assert.False(LegacyModuleCheck.RequiresPreRenameCore(Path.Combine(directory.Path, "absent.dll")));
        Directory.CreateDirectory(directory.Path);
        string text = Path.Combine(directory.Path, "not-a-binary.dll");
        File.WriteAllText(text, "not a portable executable");
        Assert.False(LegacyModuleCheck.RequiresPreRenameCore(text));
    }

    [Fact]
    public void EveryShippedModuleBindsToTheCurrentCore()
    {
        // A regression here means a shipped module was rebuilt against, or still manifests, the pre-rename API.
        var mods = Path.Combine(AppContext.BaseDirectory, "mods");
        var assemblies = Directory.GetDirectories(mods)
            .Select(dir => JsonSerializer.Deserialize<ModManifest>(File.ReadAllText(Path.Combine(dir, "mod.json")), ModLoader.Json)!)
            .Select(manifest => Path.Combine(mods, manifest.Id.Split(':')[0], manifest.Assembly))
            .ToArray();
        Assert.NotEmpty(assemblies);
        var flagged = assemblies.Where(LegacyModuleCheck.RequiresPreRenameCore).ToArray();
        Assert.True(flagged.Length == 0, string.Join("\n", flagged));
    }

    [Fact]
    public void DiscoveryDropsALegacyBinaryAndLetsTheBundledCopyOfItsIdLoad()
    {
        using var tree = new Tree();
        // The stale user copy declares the same ID as the bundled module; dropping it must not reserve that ID.
        string user = tree.Mod("user", "shared", "Civ.Mods.Shared.dll");
        WriteLegacyBinary(Path.Combine(user, "Civ.Mods.Shared.dll"));
        tree.Mod("bundle", "shared", "Absent.dll");

        var discovery = ModDiscovery.DiscoverMerged([tree.Root("bundle")], tree.Root("user"), null);

        var winner = Assert.Single(discovery.Mods);
        Assert.Equal("Bundled 1", winner.Source);
        string error = Assert.Single(discovery.Errors);
        Assert.Contains("'shared'", error);
        Assert.Contains("Civ.Core", error);
        Assert.Contains("Update it", error);
        Assert.Contains("disable or remove", error);
    }

    [Fact]
    public void SingleRootDiscoveryAlsoDropsALegacyBinary()
    {
        using var tree = new Tree();
        WriteLegacyBinary(Path.Combine(tree.Mod("only", "stale", "Old.dll"), "Old.dll"));

        var discovery = ModDiscovery.Discover(tree.Root("only"));

        Assert.Empty(discovery.Mods);
        Assert.Single(discovery.Errors);
    }

    /// <summary>Rewrite a real mod assembly's core reference to the pre-rename name. The replacement keeps the
    /// string-heap length identical, so every metadata offset stays valid and only the reference name changes.</summary>
    private static void WriteLegacyBinary(string path)
    {
        var bytes = File.ReadAllBytes(typeof(ModDiscovery).Assembly.Location);
        byte[] needle = Encoding.ASCII.GetBytes("Ariel.Core");
        byte[] replacement = Encoding.ASCII.GetBytes("Civ.Core\0\0");
        Assert.Equal(needle.Length, replacement.Length);
        int patched = 0;
        for (int index = 0; index + needle.Length <= bytes.Length; index++)
        {
            if (!bytes.AsSpan(index, needle.Length).SequenceEqual(needle)) continue;
            replacement.CopyTo(bytes, index);
            patched++;
        }
        Assert.True(patched > 0, "The client assembly no longer references its core assembly.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    private sealed class Tree : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ariel-legacy-module-" + Guid.NewGuid().ToString("N"));
        public string Root(string name) => System.IO.Path.Combine(Path, name);
        public string Mod(string root, string id, string assembly)
        {
            string directory = System.IO.Path.Combine(Root(root), id);
            Directory.CreateDirectory(directory);
            File.WriteAllText(System.IO.Path.Combine(directory, "mod.json"), JsonSerializer.Serialize(
                new ModManifest { Id = id, Name = id, Version = "1", Assembly = assembly }, ModLoader.Json));
            return directory;
        }
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true); }
    }
}
