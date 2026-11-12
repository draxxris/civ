using System.Collections.Immutable;
using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.Mods.Corporations;

/// <summary>Corporation rules. Output uses whole points; maintenance uses hundredths of gold per resource copy.
/// <para><see cref="Id"/> is the rules id used by services, effects and saves. <see cref="Identity"/> is the
/// imported corporation id, and it is the id every other content record uses: buildings reference it through
/// <c>corporation</c> requirement leaves, headquarters buildings through <c>FoundsCorporation</c>, executives through
/// <c>CorporationSpreads</c>. Anything outside the corporation rules therefore keys on the identity, never on
/// <see cref="Id"/>; the rules mod is the only place that reads both.</para></summary>
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
