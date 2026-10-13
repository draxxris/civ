using System.Collections.Immutable;

namespace Civ.Content.Schema;

/// <summary>CultureLevel content. All authoritative numbers are integers.</summary>
public sealed class CultureLevelDef : GameContentDef
{
    [ContentReference(typeof(GameSpeedDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> SpeedThresholds { get; init; } = ImmutableDictionary<string, int>.Empty;
    public int CityDefenseModifier { get; init; } = 0;
    public int CityRadius { get; init; } = 0;
    public int MaxNationalWonders { get; init; } = 0;
    public int MaxNationalWondersOCC { get; init; } = 0;
    public int MaxTeamWonders { get; init; } = 0;
    public int MaxWorldWonders { get; init; } = 0;
}
