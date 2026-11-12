using System.Collections.Immutable;

namespace Ariel.Content.Schema;

/// <summary>BuildingCategory content. All authoritative numbers are integers.</summary>
public sealed class BuildingCategoryDef : GameContentDef
{
    [ContentReference(typeof(TechDef), ReferencePart.Values)]
    public string? ObsoleteTech { get; init; } = null;
    [ContentReference(typeof(TraitDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> TraitProductionModifiers { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(TechDef), ReferencePart.Values)]
    public string? TechPrereqAnyone { get; init; } = null;
    public bool Valid { get; init; } = false;
}
