using System.Collections.Immutable;
using System.Text.Json;

namespace Civ.ContentImport;

/// <summary>Lossless data node. Text and children are mutually exclusive; order and repeats are retained.</summary>
public sealed record DataNode(string Name, string? Text, ImmutableArray<DataNode> Children,
    ImmutableArray<NodeAttribute> Attributes)
{
    public static DataNode Leaf(string name, string value) => new(name, value, [], []);
    public static DataNode Branch(string name, IEnumerable<DataNode> children) => new(name, null, [.. children], []);
    public DataNode? Child(string name) => Children.FirstOrDefault(x => x.Name == name);
    public string Value(string name) => Child(name)?.Text ?? "";
    public IEnumerable<DataNode> DescendantsAndSelf()
    {
        yield return this;
        foreach (var child in Children)
            foreach (var node in child.DescendantsAndSelf()) yield return node;
    }
    public string InnerText() => Text ?? string.Concat(Children.Select(x => x.InnerText()));
    public object ToTree() => Attributes.IsEmpty
        ? new object[] { Name, Text is not null ? Text : Children.Select(x => x.ToTree()).ToArray() }
        : new object[] { Name, Text is not null ? Text : Children.Select(x => x.ToTree()).ToArray(),
            Attributes.Select(x => new[] { x.Name, x.Value }).ToArray() };
}

public sealed record NodeAttribute(string Name, string Value);
public sealed record TreeDocument(DataNode Root, string Layer, string SourcePath, string Sha256, string Format);

public sealed class TreeReader(string dataDirectory)
{
    private readonly string root = Path.GetFullPath(dataDirectory);
    private readonly Dictionary<string, TreeDocument> cache = new(StringComparer.Ordinal);

    public TreeDocument Read(string path) => Read(Path.GetFullPath(path), new HashSet<string>(StringComparer.Ordinal));

    private TreeDocument Read(string path, HashSet<string> active)
    {
        if (cache.TryGetValue(path, out var cached)) return cached;
        if (!active.Add(path)) throw new InvalidDataException("Tree reference cycle.");
        var lines = File.ReadAllLines(path);
        var format = Header(lines, "format").Trim('"');
        using var source = JsonDocument.Parse(Header(lines, "source"));
        var metadata = source.RootElement;
        DataNode node;
        if (format == "and-xml-reference-v1")
        {
            var target = JsonSerializer.Deserialize<string>(Header(lines, "treeYaml"))!;
            var full = Path.GetFullPath(Path.Combine(root, target));
            if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidDataException("Tree reference leaves the data directory.");
            node = Read(full, active).Root;
        }
        else if (format is "and-xml-tree-v1" or "and-recovered-xml-tree-v1")
        {
            using var tree = JsonDocument.Parse(Header(lines, "tree"));
            node = ParseNode(tree.RootElement);
        }
        else throw new InvalidDataException($"Unsupported tree format in {Path.GetFileName(path)}.");
        active.Remove(path);
        var result = new TreeDocument(node, metadata.GetProperty("layer").GetString()!,
            metadata.GetProperty("path").GetString()!, metadata.GetProperty("sha256").GetString()!, format);
        cache[path] = result;
        return result;
    }

    public static string Header(IEnumerable<string> lines, string key) =>
        lines.FirstOrDefault(x => x.StartsWith(key + ": ", StringComparison.Ordinal))?[(key.Length + 2)..]
        ?? throw new InvalidDataException($"Missing {key} header.");

    public static DataNode ParseNode(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() is < 2 or > 3)
            throw new InvalidDataException("A tree node must have two or three entries.");
        var name = value[0].GetString() ?? throw new InvalidDataException("Missing node name.");
        var content = value[1];
        var attributes = value.GetArrayLength() == 3
            ? value[2].EnumerateArray().Select(x => new NodeAttribute(x[0].GetString()!, x[1].GetString()!)).ToImmutableArray()
            : ImmutableArray<NodeAttribute>.Empty;
        return content.ValueKind switch
        {
            JsonValueKind.String => new(name, content.GetString()!, [], attributes),
            JsonValueKind.Array => new(name, null, content.EnumerateArray().Select(ParseNode).ToImmutableArray(), attributes),
            _ => throw new InvalidDataException("XML content must be a string or a child list.")
        };
    }
}
