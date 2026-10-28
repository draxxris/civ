using System.Text.Json;
using Ariel.ContentImport;
using Xunit;

namespace Ariel.ContentImport.Tests;

public sealed class TemporaryDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "civ-import-" + Guid.NewGuid().ToString("N"));
    public TemporaryDirectory() => Directory.CreateDirectory(Path);
    public string Write(string relative, string content)
    {
        var path = System.IO.Path.Combine(Path, relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }
    public void Dispose() => Directory.Delete(Path, true);
}

public sealed class TreeAndProfileTests
{
    [Fact]
    public void Reference_preserves_its_provenance_and_lossless_node_content()
    {
        using var directory = new TemporaryDirectory();
        directory.Write("tree.yaml", """
            format: and-xml-tree-v1
            source: {"layer":"base","path":"XML/first.xml","sha256":"abc"}
            tree: ["Root",[["Repeated","001"],["Repeated",""],["Mixed",[["#text"," before "],["{urn:other}Bold","text"],["#text"," after "]]],["{}Reset","NONE"]],[["xmlns","urn:root"],["{urn:attr}key","true"]]]
            """);
        var reference = directory.Write("alias.yaml", """
            format: and-xml-reference-v1
            source: {"layer":"bts","path":"XML/second.xml","sha256":"abc"}
            treeYaml: "tree.yaml"
            """);
        var tree = new TreeReader(directory.Path).Read(reference);
        Assert.Equal("bts", tree.Layer);
        Assert.Equal("XML/second.xml", tree.SourcePath);
        Assert.Equal(new[] { "001", "" }, tree.Root.Children.Take(2).Select(x => x.Text));
        Assert.Equal(" before text after ", tree.Root.Child("Mixed")!.InnerText());
        Assert.Equal("{urn:other}Bold", tree.Root.Child("Mixed")!.Children[1].Name);
        Assert.Equal("{}Reset", tree.Root.Children[3].Name);
        Assert.Equal(new NodeAttribute("{urn:attr}key", "true"), tree.Root.Attributes[1]);
        using var roundTrip = JsonDocument.Parse(JsonSerializer.Serialize(tree.Root.ToTree()));
        Assert.Equal("001", roundTrip.RootElement[1][0][1].GetString());
        Assert.Equal(3, roundTrip.RootElement.GetArrayLength());
    }

    [Theory]
    [InlineData("loop.yaml", "cycle")]
    [InlineData("../outside.yaml", "leaves")]
    public void Invalid_references_fail_without_unbounded_recursion_or_external_reads(string target, string diagnostic)
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Write("loop.yaml", "format: and-xml-reference-v1\n"
            + "source: {\"layer\":\"base\",\"path\":\"XML/test.xml\",\"sha256\":\"abc\"}\n"
            + "treeYaml: " + JsonSerializer.Serialize(target));
        var exception = Assert.Throws<InvalidDataException>(() => new TreeReader(directory.Path).Read(path));
        Assert.Contains(diagnostic, exception.Message);
    }

    [Fact]
    public void Profile_resolves_file_fallback_and_nested_controls_in_declared_order()
    {
        using var directory = new TemporaryDirectory();
        directory.Write("module-controls.yaml", """
            # JSON flow
            {"controls":[
              {"layer":"and","path":"Modules/MLF_CIV4ModularLoadingControls.xml","defaultConfiguration":"chosen","configurations":[
                {"type":"other","modules":[{"directory":"Disabled","load":"1"}]},
                {"type":"chosen","modules":[{"directory":"Zulu","load":"1"},{"directory":"Disabled","load":"0"},{"directory":"Alpha","load":"1"}]}]},
              {"layer":"and","path":"Modules/Zulu/MLF_CIV4ModularLoadingControls.xml","defaultConfiguration":"nested","configurations":[
                {"type":"nested","modules":[{"directory":"On","load":"1"},{"directory":"Off","load":"0"}]}]},
              {"layer":"and","path":"Modules/Disabled/MLF_CIV4ModularLoadingControls.xml","defaultConfiguration":"nested","configurations":[
                {"type":"nested","modules":[{"directory":"EnabledChild","load":"1"}]}]}]}
            """);
        directory.Write("layers/base/XML/Test.xml.yaml", "base");
        directory.Write("layers/bts/XML/Test.xml.yaml", "expansion");
        var winner = directory.Write("layers/and/XML/Test.xml.yaml", "source");
        var fallback = directory.Write("layers/warlords/XML/Fallback.xml.yaml", "fallback");
        var recovered = directory.Write("recovered/and/XML/GameInfo/CIV4CivicInfos.xml.yaml", "recovery");
        directory.Write("layers/bts/XML/GameInfo/CIV4CivicInfos.xml.yaml", "old civics");
        var z = directory.Write("layers/and/Modules/Zulu/On/z_CIV4TechInfos.xml.yaml", "z");
        var a = directory.Write("layers/and/Modules/Zulu/On/a_CIV4TechInfos.xml.yaml", "a");
        var last = directory.Write("layers/and/Modules/Alpha/end_CIV4TechInfos.xml.yaml", "last");
        directory.Write("layers/and/Modules/Zulu/Off/ignored.xml.yaml", "off");
        directory.Write("layers/and/Modules/Zulu/parent.xml.yaml", "controlled parent is not a leaf directory");
        directory.Write("layers/and/Modules/Disabled/EnabledChild/ignored.xml.yaml", "disabled parent");
        directory.Write("layers/and/XML/README_CIV4TechInfos.xml.yaml", "not a data file");
        var profile = new ModuleProfile(directory.Path);
        Assert.Equal(new[] { "Modules/Zulu/On", "Modules/Alpha" }, profile.ModuleDirectories);
        Assert.Equal(new[] { fallback, recovered, winner, a, z, last }, profile.Files.Select(x => x.ExportPath));
        Assert.Equal(new[] { false, false, false, true, true, true }, profile.Files.Select(x => x.IsModule));
    }
}
