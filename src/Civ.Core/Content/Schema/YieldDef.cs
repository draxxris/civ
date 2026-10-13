using System.Collections.Immutable;

namespace Civ.Content.Schema;

/// <summary>Yield content. All authoritative numbers are integers.</summary>
public sealed class YieldDef : GameContentDef
{
    public int AiWeightPercent { get; init; } = 0;
    public int CityChange { get; init; } = 0;
    public int GoldenAgeYield { get; init; } = 0;
    public int GoldenAgeYieldThreshold { get; init; } = 0;
    public int HillsChange { get; init; } = 0;
    public int LakeChange { get; init; } = 0;
    public int MinCity { get; init; } = 0;
    public int PeakChange { get; init; } = 0;
    public int PopulationChangeDivisor { get; init; } = 0;
    public int PopulationChangeOffset { get; init; } = 0;
    public int TradeModifier { get; init; } = 0;
}
