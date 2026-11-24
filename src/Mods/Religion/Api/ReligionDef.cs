using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.Mods.Religion;

/// <summary>A religion: founding, spread and commerce. Commerce uses whole points. Content names a religion by its id in
/// <c>religion</c> requirement leaves (no parameter: present in the city; 1: the player's state religion; 2: the city is
/// its holy city), in the wonder that founds it (<see cref="BuildingDef.Founds"/>) and in this mod's rows about
/// buildings and units. No base row names one.</summary>
[ContentLeafKind("religion")]
public sealed class ReligionDef : GameContentDef
{
    /// <summary>The technology the religion's founding wonder requires. The wonder's own requirements enforce it.</summary>
    [ContentReference(typeof(TechDef))]
    public string? FoundingTech { get; init; }
    [ContentReference(typeof(UnitDef))]
    public string? Missionary { get; init; }
    public int FreeUnits { get; init; }
    public int SpreadFactor { get; init; }
    public CommerceVector StateReligionCommerce { get; init; }
    public CommerceVector HolyCityCommerce { get; init; }
    public CommerceVector GlobalCityCommerce { get; init; }
}

/// <summary>A requirement about religion that names no particular faith, in <c>religion-condition</c> leaves:
/// <see cref="StateReligion"/> (the player has a state religion and it is present in the city) and
/// <see cref="AnyReligion"/> (some religion is present in the city).</summary>
[ContentLeafKind("religion-condition")]
public sealed class ReligionConditionDef : GameContentDef
{
    public const string StateReligion = "religion:state_religion";
    public const string AnyReligion = "religion:any_religion";
}
