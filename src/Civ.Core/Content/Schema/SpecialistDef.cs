using System.Collections.Immutable;

namespace Civ.Content.Schema;

/// <summary>Specialist content. All authoritative numbers are integers.</summary>
public sealed class SpecialistDef : GameContentDef
{
    public CommerceVector Commerces { get; init; } = default;
    public ImmutableDictionary<string, int> Flavors { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(UnitDef), ReferencePart.Values)]
    public string? GreatPersonUnit { get; init; } = null;
    public YieldVector Yields { get; init; } = default;
    public bool Visible { get; init; } = false;
    public int Experience { get; init; } = 0;
    public int GreatPeopleRateChange { get; init; } = 0;
    public int HappinessPercent { get; init; } = 0;
    public int HealthPercent { get; init; } = 0;
}
