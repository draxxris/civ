using System.Collections.Immutable;

namespace Ariel.Content.Schema;

/// <summary>Religion identity, founding and spread effects. Commerce uses whole points.</summary>
public sealed class ReligionDef : GameContentDef
{
    [ContentReference(typeof(TechDef))]
    public string? FoundingTech { get; init; }
    [ContentReference(typeof(UnitDef))]
    public string? Missionary { get; init; }
    public int FreeUnits { get; init; }
    public int SpreadFactor { get; init; }
    public string Adjective { get; init; } = "";
    public CommerceVector StateReligionCommerce { get; init; }
    public CommerceVector HolyCityCommerce { get; init; }
    public CommerceVector GlobalCityCommerce { get; init; }
    [ContentReference(typeof(BuildingDef), ReferencePart.Values)]
    public ImmutableArray<string> Shrines { get; init; } = [];
    [ContentReference(typeof(BuildingDef), ReferencePart.Values)]
    public ImmutableArray<string> Temples { get; init; } = [];
    [ContentReference(typeof(BuildingDef), ReferencePart.Values)]
    public ImmutableArray<string> Monasteries { get; init; } = [];
    [ContentReference(typeof(BuildingDef), ReferencePart.Values)]
    public ImmutableArray<string> Cathedrals { get; init; } = [];
}
