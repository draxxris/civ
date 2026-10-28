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
}
