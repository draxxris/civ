using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Ariel.ContentImport;

public sealed record RecoveryEvidence(int Records, int RepairedComments, string Sha256, bool TreeMatches, string Note);

public static class CivicRecovery
{
    public static RecoveryEvidence Check(string dataDirectory, TreeDocument tree)
    {
        var path = Path.Combine(dataDirectory, "quarantine", ModuleProfile.SourceLayer, tree.SourcePath);
        var bytes = File.ReadAllBytes(path);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (hash != tree.Sha256) throw new InvalidDataException("Civic quarantine hash differs from recovery provenance.");
        var text = new UTF8Encoding(false, true).GetString(bytes);
        try
        {
            ParseXml(text);
            throw new InvalidDataException("The quarantined civic XML unexpectedly passed strict parsing.");
        }
        catch (XmlException exception) when (exception.Message.Contains("comment", StringComparison.OrdinalIgnoreCase)) { }
        var repairs = 0;
        var fixedText = Regex.Replace(text, @"<!--(.*?)-->", match =>
        {
            var comment = match.Groups[1].Value;
            if (comment.Contains("--", StringComparison.Ordinal)) repairs++;
            return "<!--" + comment.Replace("--", "- -", StringComparison.Ordinal) + "-->";
        }, RegexOptions.Singleline | RegexOptions.CultureInvariant);
        var element = ParseXml(fixedText);
        var rebuilt = Encode(element, element.Name.Namespace);
        if (JsonSerializer.Serialize(rebuilt.ToTree()) != JsonSerializer.Serialize(tree.Root.ToTree()))
            throw new InvalidDataException("Civic recovery differs from the comment-only repair of the original.");
        var records = tree.Root.DescendantsAndSelf().Where(x => x.Name == "CivicInfo").ToArray();
        if (tree.Root.Name != "Civ4CivicInfos" || records.Length != 81
            || records.Select(x => x.Value("Type")).Distinct(StringComparer.Ordinal).Count() != 81
            || records.Any(x => !x.Value("Type").StartsWith("CIVIC_", StringComparison.Ordinal)
                || !x.Value("CivicOptionType").StartsWith("CIVICOPTION_", StringComparison.Ordinal)))
            throw new InvalidDataException("Civic recovery has an unexpected shape or record count.");
        return new(81, repairs, hash, true, "Comment-only recovery matches the full extracted tree. Original parser behavior remains unverified.");
    }

    private static XElement ParseXml(string text)
    {
        using var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings
        { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreWhitespace = false });
        return XElement.Load(reader, LoadOptions.PreserveWhitespace);
    }

    private static DataNode Encode(XElement element, XNamespace rootNamespace)
    {
        var attributes = element.Attributes().Select(x => new NodeAttribute(AttributeName(x), x.Value)).ToArray();
        var nodes = DataNodes(element).ToArray();
        var name = ElementName(element.Name, rootNamespace);
        if (nodes.All(x => x is XText))
            return new(name, string.Concat(nodes.Cast<XText>().Select(x => x.Value)), [], [.. attributes]);
        return new(name, null, [.. nodes.Select(x => x is XElement child ? Encode(child, rootNamespace)
            : DataNode.Leaf("#text", ((XText)x).Value))], [.. attributes]);
    }

    private static IEnumerable<XNode> DataNodes(XElement element)
    {
        var preserve = element.AncestorsAndSelf().Select(x => (string?)x.Attribute(XNamespace.Xml + "space"))
            .FirstOrDefault(x => x is not null) == "preserve";
        var hasChildren = element.Elements().Any();
        var mixed = hasChildren && element.Nodes().OfType<XText>().Any(x => !string.IsNullOrWhiteSpace(x.Value));
        return element.Nodes().Where(x => x is XElement || x is XText t
            && (preserve || mixed || !hasChildren || !string.IsNullOrWhiteSpace(t.Value)));
    }

    private static string ElementName(XName name, XNamespace rootNamespace) => name.Namespace == rootNamespace ? name.LocalName
        : name.Namespace == XNamespace.None ? "{}" + name.LocalName : name.ToString();

    private static string AttributeName(XAttribute attribute) => attribute.IsNamespaceDeclaration
        ? attribute.Name.LocalName == "xmlns" ? "xmlns" : "xmlns:" + attribute.Name.LocalName
        : attribute.Name.Namespace == XNamespace.None ? attribute.Name.LocalName : attribute.Name.ToString();
}
