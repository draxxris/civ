using System.Collections.Immutable;

namespace Civ.Content.Schema;

/// <summary>Upkeep content. All authoritative numbers are integers.</summary>
public sealed class UpkeepDef : GameContentDef
{
    public int CityPercent { get; init; } = 0;
    public int PopulationPercent { get; init; } = 0;
}
