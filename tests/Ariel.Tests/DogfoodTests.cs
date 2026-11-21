using System.Text.RegularExpressions;
using Xunit;

namespace Ariel.Tests;

public sealed class DogfoodTests
{
    [Fact]
    public void CoreAndClientsDoNotKnowShippedModIdentities()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Ariel.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var pattern = new Regex(@"(foundation|world|cities|economy|military|research|government|peoples|religion|ai|base):");
        var violations = new List<string>();
        foreach (var project in new[] { "Ariel.Core", "Ariel.Client.Core", "Ariel.Godot" })
        {
            var directory = Path.Combine(root.FullName, "src", project);
            var files = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(f => !Path.GetRelativePath(directory, f).Split(Path.DirectorySeparatorChar)
                    .Any(part => part is "bin" or "obj" or ".godot")).ToArray();
            Assert.NotEmpty(files);
            foreach (var file in files)
                foreach (var (line, index) in File.ReadLines(file).Select((line, index) => (line, index)))
                    if (pattern.IsMatch(line)) violations.Add($"{Path.GetRelativePath(root.FullName, file)}:{index + 1}: {line.Trim()}");
        }
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    /// <summary>Rules, the AI and mods create units only through <c>IUnitService.Create</c>, which allocates the id,
    /// initialises the unit, refreshes sight and contacts, and publishes <c>UnitCreated</c>. Allocating an id, or adding
    /// a unit (which allocates one for an unset id), is the mark of a hand-built unit; a preview probe never enters the
    /// state. An upgrade replaces a unit under its own id. New-game setup lays out the board before play, as a scene or
    /// a save does, and Military initialises those units when the first turn starts.</summary>
    [Fact]
    public void Only_the_unit_service_allocates_unit_ids()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Ariel.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        string[] allowed = [Path.Combine("src", "Ariel.Rules", "Military", "UnitService.cs"), Path.Combine("src", "Ariel.Rules", "Military", "UnitUpgrades.cs"),
            Path.Combine("src", "Ariel.Rules", "Military", "UnitHandover.cs"),
            Path.Combine("src", "Ariel.Rules", "Foundation", "NewGameSetup.cs")];
        var violations = new List<string>();
        foreach (var project in new[] { "Ariel.Rules", "Ariel.Ai", "Mods" })
        {
            var directory = Path.Combine(root.FullName, "src", project);
            var files = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(f => !Path.GetRelativePath(directory, f).Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
                .Where(f => !allowed.Contains(Path.GetRelativePath(root.FullName, f))).ToArray();
            Assert.NotEmpty(files);
            foreach (var file in files)
                foreach (var (line, index) in File.ReadLines(file).Select((line, index) => (line, index)))
                    if (line.Contains("NextUnitId", StringComparison.Ordinal) || line.Contains(".AddUnit(", StringComparison.Ordinal))
                        violations.Add($"{Path.GetRelativePath(root.FullName, file)}:{index + 1}: {line.Trim()}");
        }
        Assert.True(violations.Count == 0, "Create units with IUnitService.Create:\n" + string.Join("\n", violations));
    }
}
