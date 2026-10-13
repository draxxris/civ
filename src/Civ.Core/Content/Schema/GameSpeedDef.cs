using System.Collections.Immutable;

namespace Civ.Content.Schema;

/// <summary>GameSpeed content. All authoritative numbers are integers.</summary>
public sealed class GameSpeedDef : GameContentDef
{
    public ImmutableArray<TurnIncrement> TurnIncrements { get; init; } = [];
    public ImmutableDictionary<string, int> ScalingPercents { get; init; } = ImmutableDictionary<string, int>.Empty;
    public int AnarchyPercent { get; init; } = 0;
    public int BarbPercent { get; init; } = 0;
    public int BuildPercent { get; init; } = 0;
    public int ConstructPercent { get; init; } = 0;
    public int CreatePercent { get; init; } = 0;
    public int CulturePercent { get; init; } = 0;
    public int FeatureProductionPercent { get; init; } = 0;
    public int GoldModifier { get; init; } = 0;
    public int GoldenAgePercent { get; init; } = 0;
    public int GreatPeoplePercent { get; init; } = 0;
    public int GrowthPercent { get; init; } = 0;
    public int HurryConscriptAngerPercent { get; init; } = 0;
    public int HurryPercent { get; init; } = 0;
    public int ImprovementPercent { get; init; } = 0;
    public int InflationOffset { get; init; } = 0;
    public int InflationPercent { get; init; } = 0;
    public int ResearchPercent { get; init; } = 0;
    public int TrainPercent { get; init; } = 0;
    public int UnitDiscoverPercent { get; init; } = 0;
    public int UnitGreatWorkPercent { get; init; } = 0;
    public int UnitHurryPercent { get; init; } = 0;
    public int UnitTradePercent { get; init; } = 0;
    public int VictoryDelayPercent { get; init; } = 0;
}
