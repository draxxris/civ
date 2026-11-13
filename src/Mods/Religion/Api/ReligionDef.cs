using Ariel.Content;
using Ariel.Content.Schema;
using System.Collections.Immutable;

namespace Ariel.Mods.Religion;

/// <summary>A religion's rules: founding, spread and commerce. Commerce uses whole points.
/// <para><see cref="Id"/> is the rules id used by services, effects and saves. <see cref="Identity"/> is the id every
/// other content record uses (requirement leaves, building, unit and leader religion fields); the religion rules are the
/// only place that reads both.</para></summary>
public sealed class ReligionDef : GameContentDef
{
    [ContentReference(typeof(ReligionIdentityDef))]
    public string Identity { get; init; } = "";
    /// <summary>The technology the religion's founding wonder requires. The wonder's own requirements enforce it.</summary>
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
