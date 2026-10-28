using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ariel.ContentImport;

public sealed record TextEntry(string Key, string Text, DataNode English, string Source);
public sealed record UnresolvedText(string Key, IReadOnlyList<string> Uses);

public sealed class EnglishText
{
    private readonly SortedDictionary<string, TextEntry> entries = new(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, TextEntry> Entries => entries;
    public List<BrandingHit> BrandingHits { get; } = [];
    public int DuplicateKeys { get; private set; }

    public EnglishText(string dataDirectory, IEnumerable<TreeDocument> fallbackText, IEnumerable<TreeDocument> modules)
    {
        foreach (var tree in fallbackText) LoadTree(tree);
        foreach (var path in Directory.GetFiles(Path.Combine(dataDirectory, "localization/english"), "*.yaml", SearchOption.AllDirectories)
                     .Order(StringComparer.Ordinal))
        {
            var lines = File.ReadAllLines(path);
            using var source = JsonDocument.Parse(TreeReader.Header(lines, "source"));
            var provenance = source.RootElement.GetProperty("path").GetString()!;
            foreach (var line in lines.Where(x => x.StartsWith("  - ", StringComparison.Ordinal)))
            {
                using var record = JsonDocument.Parse(line[4..]);
                var value = record.RootElement;
                var english = value.GetProperty("english").EnumerateArray().Select(TreeReader.ParseNode).First();
                Add(value.GetProperty("id").GetString()!, english, provenance);
            }
        }
        foreach (var tree in modules) LoadTree(tree);
    }

    private void LoadTree(TreeDocument tree)
    {
        foreach (var node in tree.Root.DescendantsAndSelf().Where(x => x.Name == "TEXT"))
            if (node.Child("English") is { } english && node.Value("Tag") is { Length: > 0 } key)
                Add(key, english, tree.SourcePath);
    }

    private void Add(string key, DataNode english, string source)
    {
        if (entries.ContainsKey(key)) DuplicateKeys++;
        // Grammar metadata is retained separately. Text is the visible English form.
        var visible = english.Text ?? english.Child("Text")?.InnerText() ?? english.InnerText();
        BrandingHits.AddRange(Branding.Scan("text/" + key, visible));
        entries[key] = new(key, ConvertMarkup(Branding.Scrub(visible)), english, source);
    }

    public string? Lookup(string key) => entries.GetValueOrDefault(key)?.Text;
    public static IEnumerable<string> KeysIn(string text) => TextKeys.Matches(text).Select(x => x.Value).Distinct(StringComparer.Ordinal);
    private static readonly Regex TextKeys = new(@"\bTXT_KEY_[A-Za-z0-9_]+", RegexOptions.CultureInvariant);
    private static readonly Regex Tokens = new(@"\[(ICON_[^\]]+|COLOR_[^\]]+|LINK=[^\]]+|\\LINK|/LINK|NEWLINE|PARAGRAPH|SPACE|BULLET|BULLETS|TAB)\]", RegexOptions.CultureInvariant);
    private static readonly Regex Placeholders = new(@"%([sd])([0-9]+)(?:_([A-Za-z0-9_]+))?", RegexOptions.CultureInvariant);

    public static string ConvertMarkup(string text)
    {
        text = Tokens.Replace(text, x => ConvertToken(x.Groups[1].Value));
        text = Placeholders.Replace(text, x => "{arg:" + x.Groups[2].Value + ":"
            + (x.Groups[1].Value == "s" ? "text" : "number")
            + (x.Groups[3].Success ? ":" + x.Groups[3].Value : "") + "}");
        return text.Replace("%%", "%", StringComparison.Ordinal).Replace('•', '●');
    }

    private static string ConvertToken(string token)
    {
        if (token.StartsWith("ICON_", StringComparison.Ordinal)) return "{icon:" + token[5..].ToLowerInvariant() + "}";
        if (token == "COLOR_REVERT") return "{/color}";
        if (token.StartsWith("COLOR_", StringComparison.Ordinal)) return "{color:" + token[6..].ToLowerInvariant() + "}";
        if (token.StartsWith("LINK=", StringComparison.Ordinal)) return "{link:" + token[5..].ToLowerInvariant() + "}";
        return token switch
        {
            "\\LINK" or "/LINK" => "{/link}", "NEWLINE" => "\n", "PARAGRAPH" => "\n\n",
            "SPACE" => " ", "TAB" => "\t", "BULLET" or "BULLETS" => "● ", _ => "[" + token + "]"
        };
    }
}
