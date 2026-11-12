using System.Collections.Immutable;

namespace Ariel.Content.Schema;

/// <summary>UnitCategory content. All authoritative numbers are integers.</summary>
public sealed class UnitCategoryDef : GameContentDef
{
    [ContentReference(typeof(UnitAiDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> CarrierUnitAis { get; init; } = ImmutableDictionary<string, bool>.Empty;
    public bool CityLoad { get; init; } = false;
    public bool Valid { get; init; } = false;
}
