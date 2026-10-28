using System.Text.Json;
using Ariel.Client;
using Ariel.Modding;
using Xunit;

namespace Ariel.Mods.Tests;

public sealed class ModManagerTests
{
    [Fact]
    public void Discovery_reads_metadata_without_loading_code_and_keeps_good_manifests_when_another_is_invalid()
    {
        using var directory = new ModDirectory();
        string source = Path.Combine(AppContext.BaseDirectory, "mods", "Foundation");
        string valid = directory.Add(new() { Id = "sample", Name = "Sample", Version = "2.3", Assembly = "sample.dll", Dependencies = ["provider"] });
        File.Copy(Path.Combine(source, "Ariel.Mods.Foundation.dll"), Path.Combine(valid, "sample.dll"));
        directory.Add(new() { Id = "no_binary", Name = "No Binary", Assembly = "absent.dll" });
        string broken = Path.Combine(directory.Root, "broken");
        Directory.CreateDirectory(broken);
        File.WriteAllText(Path.Combine(broken, "mod.json"), "{");

        var discovery = ModDiscovery.Discover(directory.Root);
        Assert.Equal(2, discovery.Mods.Count);
        var manifest = discovery.Mods.Single(m => m.Manifest.Id == "sample").Manifest;
        Assert.Equal("Sample", manifest.Name);
        Assert.Equal("2.3", manifest.Version);
        Assert.Equal(new[] { "provider" }, manifest.Dependencies);
        Assert.Contains("broken", Assert.Single(discovery.Errors));
        Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), a => !a.IsDynamic && a.Location.StartsWith(directory.Root, StringComparison.Ordinal));
    }

    [Fact]
    public void Selection_closes_transitive_dependencies_and_content_references_and_explains_each_provider()
    {
        var installed = new[]
        {
            Mod("feature", dependencies: ["rules"]), Mod("rules", references: ["data"]),
            Mod("data", references: ["rules"]), Mod("unrelated", dependencies: ["absent"]),
        };
        var selection = ModSelection.Resolve(installed, ["feature"]);
        Assert.True(selection.IsValid, string.Join("\n", selection.Errors));
        Assert.Equal(new[] { "data", "feature", "rules" }, selection.Mods.Select(m => m.Manifest.Id));
        Assert.Equal(new[] { "data", "feature" }, selection.RequiredBy["rules"]);
        Assert.Equal(new[] { "rules" }, selection.RequiredBy["data"]);
        Assert.Empty(ModSelection.Resolve(installed, []).Mods);
    }

    [Theory]
    [InlineData("missing_dependency", "absent")]
    [InlineData("missing_reference", "absent")]
    [InlineData("cycle", "cycle")]
    [InlineData("duplicate", "Duplicate")]
    [InlineData("stale", "removed")]
    public void Invalid_selection_is_rejected_before_any_assembly_load_and_can_be_corrected(string scenario, string expected)
    {
        InstalledMod[] installed = scenario switch
        {
            "missing_dependency" => [Mod("feature", dependencies: ["absent"])],
            "missing_reference" => [Mod("feature", references: ["absent"])],
            "cycle" => [Mod("feature", dependencies: ["other"]), Mod("other", dependencies: ["feature"])],
            "duplicate" => [Mod("feature"), Mod("feature")],
            _ => [],
        };
        string[] enabled = [scenario == "stale" ? "removed" : "feature"];
        // All paths point to absent DLLs. A load before validation would fail for a different reason.
        var error = Assert.Throws<InvalidOperationException>(() => ModDiscovery.LoadMods(installed, enabled));
        Assert.Contains(expected, error.Message);
        Assert.Empty(ModDiscovery.LoadMods(installed, []));
    }

    [Fact]
    public void Profile_persists_explicit_stable_ids_across_folder_moves_and_reports_stale_ids_without_enabling_new_installs()
    {
        using var directory = new ModDirectory();
        string feature = directory.Add(new() { Id = "feature", Name = "Feature", Assembly = "none.dll", Dependencies = ["provider"] });
        directory.Add(new() { Id = "provider", Name = "Provider", Assembly = "none.dll" });
        string path = Path.Combine(directory.Root, "profile.json");
        ModProfile.Save(path, ["removed", "feature", "feature"]);
        Directory.Move(feature, Path.Combine(directory.Root, "renamed-folder"));
        directory.Add(new() { Id = "new_install", Name = "New Install", Assembly = "none.dll" });

        var installed = ModDiscovery.Discover(directory.Root).Mods;
        var ids = ModProfile.InitialSelection(installed, ModProfile.Load(path));
        Assert.Equal(new[] { "feature", "removed" }, ids);
        var selection = ModSelection.Resolve(installed, ids);
        Assert.Contains("removed", Assert.Single(selection.Errors));
        Assert.DoesNotContain(selection.Mods, m => m.Manifest.Id == "new_install");
        Assert.Contains(selection.Mods, m => m.Manifest.Id == "provider");
        Assert.True(ModSelection.Resolve(installed, ids.Where(id => id != "removed")).IsValid);

        ModProfile.Save(path, []);
        Assert.Empty(ModProfile.InitialSelection(installed, ModProfile.Load(path)));
    }

    [Theory]
    [InlineData("null", "data")]
    [InlineData("{", "json")]
    [InlineData("{}", "json")]
    [InlineData("{\"Version\":2,\"EnabledModIds\":[]}", "data")]
    [InlineData("{\"Version\":1,\"EnabledModIds\":null}", "data")]
    [InlineData("{\"Version\":1,\"EnabledModIds\":[\"\"]}", "data")]
    [InlineData("directory", "read")]
    public void Broken_profile_never_falls_back_to_the_all_mod_default(string json, string failure)
    {
        using var directory = new ModDirectory();
        string path = Path.Combine(directory.Root, "profile.json");
        if (json == "directory") Directory.CreateDirectory(path);
        else File.WriteAllText(path, json);
        // A directory at the profile path is a read failure, not a content failure, so it must not be pinned to
        // the operating system's own exception type: a directory is UnauthorizedAccessException on Unix, not IOException.
        switch (failure)
        {
            case "json": Assert.Throws<JsonException>(() => ModProfile.Load(path)); break;
            case "read":
                var thrown = Record.Exception(() => ModProfile.Load(path));
                Assert.True(thrown is IOException or UnauthorizedAccessException or System.Security.SecurityException,
                    $"expected a read failure, got {thrown?.GetType().Name ?? "no exception"}");
                break;
            default: Assert.Throws<InvalidDataException>(() => ModProfile.Load(path)); break;
        }
    }
    [Fact]
    public void Missing_profile_keeps_all_mod_default_and_subset_catalog_uses_real_loader_order_without_enabling_game_options()
    {
        using var directory = new ModDirectory();
        var main = ModDiscovery.Discover(Path.Combine(AppContext.BaseDirectory, "mods"));
        var optional = ModDiscovery.Discover(Path.Combine(AppContext.BaseDirectory, "optional-mods"));
        Assert.Empty(main.Errors);
        Assert.Empty(optional.Errors);
        var installed = main.Mods.Concat(optional.Mods).ToArray();
        var defaults = ModProfile.InitialSelection(installed, ModProfile.Load(Path.Combine(directory.Root, "missing.json")));
        Assert.Equal(installed.Select(m => m.Manifest.Id).Order(), defaults.Order());
        Assert.Equal(main.Mods.Count, ModDiscovery.LoadMods(Path.Combine(AppContext.BaseDirectory, "mods")).Count);

        var catalog = GameCatalog.Load(installed, ["tech_tree", "card_research"]);
        Assert.Contains(catalog.Mods, m => m.Manifest.Id == "research");
        Assert.DoesNotContain(catalog.Mods, m => m.Manifest.Id == "ai");
        Assert.DoesNotContain(catalog.Mods, m => m.Manifest.Id == "events");
        var order = catalog.Content.LoadedMods.ToList();
        Assert.True(order.IndexOf("research") < order.IndexOf("tech_tree"));
        Assert.True(order.IndexOf("research") < order.IndexOf("card_research"));
        Assert.False(catalog.GameOptions.Single(o => o.Id == "tech_tree:enabled").Default);
        Assert.False(catalog.GameOptions.Single(o => o.Id == "card_research:enabled").Default);
    }

    private static InstalledMod Mod(string id, string[]? dependencies = null, string[]? references = null) =>
        new(new() { Id = id, Name = id, Assembly = "absent.dll", Dependencies = dependencies ?? [], References = references ?? [] }, "/absent/" + id);

    private sealed class ModDirectory : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "civ-mod-manager-" + Guid.NewGuid().ToString("N"));
        public ModDirectory() => Directory.CreateDirectory(Root);
        public string Add(ModManifest manifest)
        {
            string path = Path.Combine(Root, manifest.Id);
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "mod.json"), JsonSerializer.Serialize(manifest));
            return path;
        }
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
