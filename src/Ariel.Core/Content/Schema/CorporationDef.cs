using System.Collections.Immutable;

namespace Ariel.Content.Schema;

/// <summary>Corporation rules. Output uses whole points; maintenance uses hundredths of gold per resource copy.</summary>
public sealed class CorporationDef : GameContentDef
{
    [ContentReference(typeof(CorporationIdentityDef))]
    public string Identity { get; init; } = "";
    [ContentReference(typeof(TechDef))]
    public string? FoundingTech { get; init; }
    [ContentReference(typeof(UnitDef))]
    public string RequiredGreatPersonClass { get; init; } = "";
    [ContentReference(typeof(BonusDef), ReferencePart.Values)]
    public ImmutableArray<string> PrereqBonuses { get; init; } = [];
    public YieldVector YieldProduced { get; init; }
    public CommerceVector CommerceProduced { get; init; }
    public CommerceVector HeadquartersCommerce { get; init; }
    public int SpreadCost { get; init; }
    public int MaintenanceHundredths { get; init; }
}
