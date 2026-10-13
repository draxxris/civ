using System.Collections.Immutable;
using Civ.Content;
using Civ.Modding;
using Xunit;
using Schema = Civ.Content.Schema;

namespace Civ.Mods.Tests;

public sealed class ReferenceValidationTests
{
    [Theory]
    [InlineData("missing-id", "FirstFreeUnit")]
    [InlineData("wrong-type", "UnitDef")]
    [InlineData("undeclared-provider", "must declare content provider 'beta'")]
    [InlineData("missing-provider", "missing mod 'beta'")]
    public void Load_rejects_invalid_content_references_with_actionable_diagnostics(string error, string diagnostic)
    {
        var alpha = Source("alpha", error == "undeclared-provider" ? [] : ["beta"], context =>
            context.Content<Schema.TechDef>().Register(new() { Id = "alpha:example", FirstFreeUnit = "beta:target" }));
        var beta = Source("beta", [], context =>
        {
            if (error == "wrong-type") context.Content<Schema.TerrainDef>().Register(new() { Id = "beta:target" });
            else if (error != "missing-id") context.Content<Schema.UnitDef>().Register(new() { Id = "beta:target", DefaultUnit = "beta:target" });
        });
        var exception = Assert.ThrowsAny<Exception>(() => ModLoader.Build(error == "missing-provider" ? [alpha] : [alpha, beta]));
        Assert.Contains(diagnostic, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_checks_reference_keys_in_nested_immutable_maps()
    {
        var source = Source("example", [], context => context.Content<Schema.BuildingDef>().Register(new()
        {
            Id = "example:building", DefaultBuilding = "example:building",
            TechSpecialistChanges = ImmutableDictionary<string, Schema.SpecialistSlotsChange>.Empty.Add("example:tech", new()
            { Slots = ImmutableDictionary<string, int>.Empty.Add("example:missing_specialist", -1) })
        }));
        var exception = Assert.Throws<InvalidDataException>(() => ModLoader.Build([source]));
        Assert.Contains("missing_specialist", exception.Message);
        Assert.Contains("SpecialistDef", exception.Message);
    }

    [Fact]
    public void Load_rejects_an_unresolved_typed_requirement_leaf()
    {
        var source = Source("example", [], context =>
        {
            context.Content<Schema.TechDef>().Register(new() { Id = "example:tech", Requirements = new Requirement.All([new Requirement.Leaf("tech", "example:missing")]) });
            context.RegisterService<IRequirementEvaluator>("tech", new TechIdentity());
        });
        var exception = Assert.Throws<InvalidDataException>(() => ModLoader.Build([source]));
        Assert.Contains("example:missing", exception.Message);
        Assert.Contains("TechDef", exception.Message);
    }

    private static ModSource Source(string id, string[] references, Action<IModContext> register) => new()
    {
        Manifest = new() { Id = id, References = references }, Root = AppContext.BaseDirectory, Instance = new TestMod(register)
    };
    private sealed class TestMod(Action<IModContext> register) : IMod { public void Register(IModContext context) => register(context); }
    private sealed class TechIdentity : IContentRequirementEvaluator
    {
        public string Kind => "tech";
        public Type DefinitionType => typeof(Schema.TechDef);
        public string? Evaluate(Requirement.Leaf leaf, RequirementContext context) => "Unavailable";
    }
}
