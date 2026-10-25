using System.Text.Json;
using System.Text.Json.Nodes;
using Civ.Client;
using Civ.Modding;
using Xunit;

namespace Civ.Mods.Tests;

public sealed class MergedModDiscoveryTests
{
    [Theory]
    [InlineData(true, true, "CIV_MODS")]
    [InlineData(false, true, "User-installed")]
    [InlineData(false, false, "Bundled 1")]
    public void Merge_keeps_unrelated_ids_and_resolves_requirements_from_only_the_winning_manifest(bool environment, bool user, string source)
    {
        using var tree = new ModTree();
        tree.Add("bundle", "shared", dependencies: ["losing_dependency"], references: ["losing_reference"]);
        tree.Add("bundle", "bundled_only");
        string winner = tree.Add("bundle", "provider");
        if (user)
        {
            winner = tree.Add("user", "shared", references: ["provider"]);
            tree.Add("user", "user_only");
        }
        if (environment)
        {
            winner = tree.Add("environment", "shared", dependencies: ["provider"]);
            tree.Add("environment", "environment_only");
        }
        // The bundled winner needs a valid manifest when neither higher-priority root exists.
        if (!user && !environment) winner = tree.Add("bundle", "shared", dependencies: ["provider"]);

        var discovery = ModDiscovery.DiscoverMerged([tree.Root("bundle")], tree.Root("user"), tree.Root("environment"));
        Assert.Empty(discovery.Errors);
        var shared = discovery.Mods.Single(m => m.Manifest.Id == "shared");
        Assert.Equal(winner, shared.Root);
        Assert.Equal(source, shared.Source);
        Assert.Equal((user ? 1 : 0) + (environment ? 1 : 0), shared.Overridden.Count);
        string[] expected = new[] { "bundled_only", "provider", "shared" }
            .Concat(user ? ["user_only"] : Array.Empty<string>())
            .Concat(environment ? ["environment_only"] : Array.Empty<string>()).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(expected, discovery.Mods.Select(m => m.Manifest.Id));
        var selection = ModSelection.Resolve(discovery.Mods, ["shared"]);
        Assert.True(selection.IsValid, string.Join("\n", selection.Errors));
        Assert.Equal(new[] { "provider", "shared" }, selection.Mods.Select(m => m.Manifest.Id));
        Assert.Empty(ModDiscovery.LoadMods(discovery.Mods, []));
        Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), a => !a.IsDynamic && a.Location.StartsWith(tree.Path, StringComparison.Ordinal));
    }

    [Fact]
    public void Bundled_ties_use_candidate_order_then_ordinal_folder_order_not_creation_order()
    {
        using var tree = new ModTree();
        string last = tree.Add("first", "shared", folder: "z-last");
        string first = tree.Add("first", "shared", folder: "a-first");
        string second = tree.Add("second", "shared");
        var result = ModDiscovery.DiscoverMerged([tree.Root("first"), tree.Root("second")], null, null);
        var winner = Assert.Single(result.Mods);
        Assert.Equal(first, winner.Root);
        Assert.Equal(new[] { last, second }, winner.Overridden.Select(m => m.Root));
        Assert.Equal(second, Assert.Single(ModDiscovery.DiscoverMerged([tree.Root("second"), tree.Root("first")], null, null).Mods).Root);
    }

    /// <summary>One installation reached through several root spellings is discovered once, takes the label of the
    /// highest-priority root that names it, and reports no overrides or errors — for path spellings and for links.</summary>
    [Theory]
    [InlineData("relative and trailing slash", "Bundled 1")]
    [InlineData("dot segment in the environment root", "CIV_MODS")]
    [InlineData("linked environment root", "CIV_MODS")]
    [InlineData("linked user root", "User-installed")]
    [InlineData("linked user root through a relative target", "User-installed")]
    public void Equivalent_roots_do_not_repeat_an_installation(string alias, string label)
    {
        using var tree = new ModTree();
        string mod = tree.Add("bundle", "shared");
        string root = tree.Root("bundle");
        var links = new List<string>();
        try
        {
            string[] bundles = [root];
            string? user = null, environment = null, expected = mod;
            switch (alias)
            {
                case "relative and trailing slash":
                    bundles = [System.IO.Path.GetRelativePath(Environment.CurrentDirectory, root), root + "/"];
                    break;
                case "dot segment in the environment root":
                    environment = System.IO.Path.Combine(root, ".");
                    break;
                case "linked environment root":
                    environment = System.IO.Path.Combine(Link(tree.Path, "linked-parent", tree.Path, links), "bundle");
                    expected = System.IO.Path.Combine(environment, "shared");
                    break;
                case "linked user root":
                    string parent = Link(tree.Path, "linked-parent", tree.Path, links);
                    user = Link(tree.Path, "linked-root", System.IO.Path.Combine(parent, "bundle"), links);
                    expected = System.IO.Path.Combine(user, "shared");
                    break;
                default:
                    // A relative link target must resolve against the link's own directory.
                    Link(tree.Path, "linked-parent", tree.Path, links);
                    user = Link(tree.Path, "linked-root", "linked-parent/bundle", links);
                    expected = System.IO.Path.Combine(user, "shared");
                    break;
            }

            var discovery = ModDiscovery.DiscoverMerged(bundles, user, environment);
            var winner = Assert.Single(discovery.Mods);

            Assert.Equal("shared", winner.Manifest.Id);
            Assert.Equal(label, winner.Source);
            Assert.Equal(expected, winner.Root);
            Assert.Empty(winner.Overridden);
            Assert.Empty(discovery.Errors);
        }
        finally
        {
            for (int i = links.Count - 1; i >= 0; i--) Directory.Delete(links[i]);
        }
    }

    private static string Link(string dir, string name, string target, List<string> created)
    {
        string link = System.IO.Path.Combine(dir, name);
        Directory.CreateSymbolicLink(link, target);
        created.Add(link);
        return link;
    }

    [Fact]
    public void Missing_optional_roots_and_unidentified_bad_manifests_do_not_hide_unrelated_mods()
    {
        using var tree = new ModTree();
        tree.Add("bundle", "good");
        tree.Raw("environment", "broken", "{");
        var discovery = ModDiscovery.DiscoverMerged([tree.Root("absent"), tree.Root("bundle")], tree.Root("absent-user"), tree.Root("environment"));
        Assert.Equal("good", Assert.Single(discovery.Mods).Manifest.Id);
        Assert.Contains(System.IO.Path.Combine(tree.Root("environment"), "broken", "mod.json"), Assert.Single(discovery.Errors));
        var empty = ModDiscovery.DiscoverMerged([tree.Root("missing")], "", null);
        Assert.Empty(empty.Mods);
        Assert.Empty(empty.Errors);
    }

    [Theory]
    [InlineData("{\"id\":\"shared\",\"name\":\"Invalid\",\"assembly\":\"none.dll\",\"unexpected\":true}")]
    [InlineData("{\"Id\":\"shared\",\"Assembly\":\"none.dll\"}")]
    [InlineData("{\"ID\":\"shared\",\"name\":\"Invalid\",\"assembly\":\"none.dll\",\"dependencies\":null}")]
    [InlineData("{\"id\":\"shared\",\"name\":\"Invalid\",\"assembly\":\"none.dll\",\"references\":[42]}")]
    [InlineData("{/*comment*/\"id\":\"shared\",\"dependencies\":")]
    public void Invalid_identifiable_winner_blocks_fallback_before_assembly_loading_but_can_be_disabled(string json)
    {
        using var tree = new ModTree();
        tree.Add("bundle", "shared");
        tree.Add("bundle", "feature", dependencies: ["shared"]);
        tree.Add("user", "shared");
        string invalid = tree.Raw("environment", "renamed-folder", json);
        var discovery = ModDiscovery.DiscoverMerged([tree.Root("bundle")], tree.Root("user"), tree.Root("environment"));
        var winner = discovery.Mods.Single(m => m.Manifest.Id == "shared");
        Assert.Equal(invalid, winner.Root);
        Assert.NotNull(winner.Error);
        Assert.Equal(2, winner.Overridden.Count);
        Assert.Contains(invalid, Assert.Single(discovery.Errors));
        var error = Assert.Throws<InvalidOperationException>(() => ModDiscovery.LoadMods(discovery.Mods, ["feature"]));
        Assert.Contains("shared", error.Message);
        Assert.Contains("blocked", error.Message);
        Assert.Contains(invalid, error.Message);
        Assert.Empty(ModDiscovery.LoadMods(discovery.Mods, []));
        Assert.Contains("shared", ModProfile.InitialSelection(discovery.Mods, null));
    }

    [Theory]
    [InlineData("user")]
    [InlineData("bundle")]
    public void Invalid_lower_priority_winner_blocks_fallback_but_does_not_poison_a_valid_higher_copy(string invalidRoot)
    {
        using var tree = new ModTree();
        tree.Add("fallback", "shared");
        tree.Raw(invalidRoot, "bad", "{\"id\":\"shared\",\"assembly\":\"none.dll\"}");
        string? user = invalidRoot == "user" ? tree.Root("user") : null;
        string[] bundled = invalidRoot == "bundle" ? [tree.Root("bundle"), tree.Root("fallback")] : [tree.Root("fallback")];
        var blocked = ModDiscovery.DiscoverMerged(bundled, user, null);
        Assert.False(ModSelection.Resolve(blocked.Mods, ["shared"]).IsValid);

        string higher = tree.Add("environment", "shared");
        var discovery = ModDiscovery.DiscoverMerged(bundled, user, tree.Root("environment"));
        Assert.Single(discovery.Errors);
        Assert.Equal(higher, Assert.Single(discovery.Mods).Root);
        Assert.True(ModSelection.Resolve(discovery.Mods, ["shared"]).IsValid);
    }

    [Fact]
    public void Conflicting_identity_fields_reserve_both_ids_instead_of_executing_either_fallback()
    {
        using var tree = new ModTree();
        tree.Add("bundle", "first");
        tree.Add("bundle", "second");
        tree.Raw("environment", "bad", "{\"id\":\"first\",\"ID\":\"second\",\"name\":\"Bad\",\"assembly\":\"none.dll\"}");
        var discovery = ModDiscovery.DiscoverMerged([tree.Root("bundle")], null, tree.Root("environment"));
        Assert.Single(discovery.Errors);
        Assert.False(ModSelection.Resolve(discovery.Mods, ["first"]).IsValid);
        Assert.False(ModSelection.Resolve(discovery.Mods, ["second"]).IsValid);
    }

    [Fact]
    public void Catalog_uses_only_winner_assembly_data_and_art_and_profiles_keep_the_stable_id()
    {
        using var tree = new ModTree();
        string bundled = System.IO.Path.Combine(AppContext.BaseDirectory, "mods");
        string lower = tree.Add("lower", "foundation", dependencies: ["losing_dependency"]);
        tree.File(lower, "art/icons/lower-only.png", "lower art");
        string winner = tree.Root("environment") + "/renamed-foundation";
        string original = System.IO.Path.Combine(bundled, "Foundation");
        Directory.CreateDirectory(winner);
        foreach (var file in Directory.GetFiles(original, "*", SearchOption.TopDirectoryOnly))
            File.Copy(file, System.IO.Path.Combine(winner, System.IO.Path.GetFileName(file)));
        foreach (var file in Directory.GetFiles(System.IO.Path.Combine(original, "data"), "*", SearchOption.AllDirectories))
        {
            string target = System.IO.Path.Combine(winner, System.IO.Path.GetRelativePath(original, file));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
        // A different assembly filename proves that apply uses the winning manifest, not a losing filename.
        File.Move(System.IO.Path.Combine(winner, "Civ.Mods.Foundation.dll"), System.IO.Path.Combine(winner, "winner.dll"));
        var manifest = JsonNode.Parse(File.ReadAllText(System.IO.Path.Combine(winner, "mod.json")))!;
        manifest["assembly"] = "winner.dll";
        tree.File(winner, "mod.json", manifest.ToJsonString());
        string sizesFile = System.IO.Path.Combine(winner, "data/world-size.json");
        var sizes = JsonNode.Parse(File.ReadAllText(sizesFile))!;
        sizes[0]!["name"] = "Winning world";
        File.WriteAllText(sizesFile, sizes.ToJsonString());
        tree.File(winner, "art/icons/winner-only.png", "winner art");
        string profile = System.IO.Path.Combine(tree.Path, "profile.json");
        ModProfile.Save(profile, ["foundation"]);

        var discovery = ModDiscovery.DiscoverMerged([tree.Root("lower"), bundled], null, tree.Root("environment"));
        var enabled = ModProfile.InitialSelection(discovery.Mods, ModProfile.Load(profile));
        var catalog = GameCatalog.Load(discovery.Mods, enabled);
        var loaded = catalog.Mods.Single(m => m.Manifest.Id == "foundation");
        Assert.Equal(winner, loaded.Root);
        Assert.Equal("winner.dll", loaded.Manifest.Assembly);
        Assert.Equal(System.IO.Path.Combine(winner, "winner.dll"), loaded.Instance.GetType().Assembly.Location);
        Assert.Contains(catalog.WorldSizes, w => w.Name == "Winning world");
        Assert.Equal(System.IO.Path.Combine(winner, "art/icons/winner-only.png"), catalog.IconPath("foundation:winner-only"));
        Assert.Null(catalog.IconPath("foundation:lower-only"));
        Assert.DoesNotContain(catalog.Mods, m => m.Manifest.Id == "ai");
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("dependency")]
    [InlineData("reference")]
    [InlineData("assembly")]
    [InlineData("version")]
    [InlineData("name")]
    [InlineData("invalid")]
    [InlineData("deleted")]
    [InlineData("conflicting_ids")]
    public void Changed_selected_manifest_rejects_the_entire_batch_before_loading_code_without_fallback(string change)
    {
        using var tree = new ModTree();
        string first = tree.Add("environment", "a_first");
        string winner = tree.Add("environment", "sample", dependencies: ["a_first"]);
        string lower = tree.Add("bundle", "sample");
        string assembly = System.IO.Path.Combine(AppContext.BaseDirectory, "mods", "Foundation", "Civ.Mods.Foundation.dll");
        foreach (string directory in new[] { first, winner, lower })
            File.Copy(assembly, System.IO.Path.Combine(directory, "absent.dll"));
        var discovery = ModDiscovery.DiscoverMerged([tree.Root("bundle")], null, tree.Root("environment"));
        Assert.Empty(discovery.Errors);
        Assert.Equal(winner, discovery.Mods.Single(m => m.Manifest.Id == "sample").Root);
        Assert.True(ModSelection.Resolve(discovery.Mods, ["sample"]).IsValid);

        string path = System.IO.Path.Combine(winner, "mod.json");
        var manifest = JsonNode.Parse(File.ReadAllText(path))!;
        switch (change)
        {
            case "identity":
                manifest["Id"] = "not_enabled";
                manifest["Dependencies"] = new JsonArray("missing_provider");
                break;
            case "dependency": manifest["Dependencies"] = new JsonArray("missing_provider"); break;
            case "reference": manifest["References"] = new JsonArray("missing_provider"); break;
            case "assembly": manifest["Assembly"] = "changed.dll"; break;
            case "version": manifest["Version"] = "2.0.0"; break;
            case "name": manifest["Name"] = "Updated sample"; break;
        }
        if (change == "deleted") File.Delete(path);
        else tree.File(winner, "mod.json", change switch
        {
            "invalid" => "{",
            "conflicting_ids" => manifest.ToJsonString().Insert(1, "\"id\":\"not_enabled\","),
            _ => manifest.ToJsonString()
        });

        var error = Assert.Throws<InvalidOperationException>(() => ModDiscovery.LoadMods(discovery.Mods, ["sample"]));
        Assert.Contains(path, error.Message);
        Assert.Contains("Restart", error.Message);
        Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), a => !a.IsDynamic && a.Location.StartsWith(tree.Path, StringComparison.Ordinal));
        Assert.Empty(ModDiscovery.LoadMods(discovery.Mods, []));
    }

    private sealed class ModTree : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "civ-merged-mods-" + Guid.NewGuid().ToString("N"));
        public string Root(string name) => System.IO.Path.Combine(Path, name);
        public string Add(string root, string id, string[]? dependencies = null, string[]? references = null, string? folder = null) =>
            Raw(root, folder ?? id, JsonSerializer.Serialize(new ModManifest
            {
                Id = id, Name = id, Assembly = "absent.dll", Dependencies = dependencies ?? [], References = references ?? []
            }));
        public string Raw(string root, string folder, string json)
        {
            string directory = System.IO.Path.Combine(Root(root), folder);
            File(directory, "mod.json", json);
            return directory;
        }
        public void File(string directory, string relative, string text)
        {
            string path = System.IO.Path.Combine(directory, relative);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            System.IO.File.WriteAllText(path, text);
        }
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
