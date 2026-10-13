using System.Collections.Immutable;

namespace Civ.Content.Schema;

/// <summary>Civilization content. All authoritative numbers are integers.</summary>
public sealed class CivilizationDef : GameContentDef
{
    [ContentReference(typeof(BuildingDef), ReferencePart.Keys)]
    [ContentReference(typeof(BuildingDef), ReferencePart.Values)]
    public ImmutableDictionary<string, string?> BuildingOverrides { get; init; } = ImmutableDictionary<string, string?>.Empty;
    public ImmutableArray<string> CityNames { get; init; } = [];
    [ContentReference(typeof(BuildingDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> StartingBuildings { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(TechDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> StartingTechs { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(UnitDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> StartingUnits { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(CivicDef), ReferencePart.Values)]
    public ImmutableArray<string> StartingCivics { get; init; } = [];
    [ContentReference(typeof(LeaderDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> Leaders { get; init; } = ImmutableDictionary<string, bool>.Empty;
    public string ShortDescription { get; init; } = "";
    [ContentReference(typeof(UnitDef), ReferencePart.Keys)]
    [ContentReference(typeof(UnitDef), ReferencePart.Values)]
    public ImmutableDictionary<string, string?> UnitOverrides { get; init; } = ImmutableDictionary<string, string?>.Empty;
    public bool AiPlayable { get; init; } = false;
    public bool Playable { get; init; } = false;
    public string Adjective { get; init; } = "";
    [ContentReference(typeof(CivilizationDef))]
    public string? DerivativeCivilization { get; init; }
}
