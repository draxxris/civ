using System.Text.RegularExpressions;

namespace Civ.ContentImport;

public sealed record BrandingHit(string Context, string Rule, int Occurrences, string NeutralExcerpt);

public static class Branding
{
    // Input recognition only. These names never become product identifiers or output text.
    private static readonly (string Label, Regex Pattern, string Replacement)[] Rules =
    [
        ("source-title", new("A[ _]+New[ _]+Dawn", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "the source mod"),
        ("source-parent-title", new("Rise[ _]+of[ _]+Mankind", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "the source mod"),
        ("source-short-name", new("\u0052oM", RegexOptions.CultureInvariant), "Source"),
        ("source-acronym", new(@"(?<![A-Za-z0-9])\u0041ND(?=[^A-Za-z0-9]|$|Concept)", RegexOptions.CultureInvariant), "Source"),
        ("source-path", new(@"(?<![A-Za-z0-9])and(?=[_:/\\.\-])", RegexOptions.CultureInvariant), "source")
    ];

    public static string Scrub(string text)
    {
        foreach (var rule in Rules) text = rule.Pattern.Replace(text, rule.Replacement);
        return text;
    }

    public static string Output(string text) => Regex.Replace(Scrub(text), @"(^|/)and(?=/|$)", "$1source", RegexOptions.CultureInvariant);

    public static string Identifier(string text) => Output(text).Replace("the source mod", "source", StringComparison.Ordinal);

    public static IEnumerable<BrandingHit> Scan(string context, string text)
    {
        foreach (var rule in Rules)
        {
            var matches = rule.Pattern.Matches(text);
            if (matches.Count == 0) continue;
            var start = Math.Max(0, matches[0].Index - 40);
            var excerpt = text.Substring(start, Math.Min(160, text.Length - start));
            yield return new(Identifier(context), rule.Label, matches.Count, Scrub(excerpt));
        }
    }
}
