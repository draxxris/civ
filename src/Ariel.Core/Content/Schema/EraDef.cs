using System.Collections.Immutable;

namespace Ariel.Content.Schema;

/// <summary>Era content. All authoritative numbers are integers.</summary>
public sealed class EraDef : GameContentDef
{
    public bool NoAnimals { get; init; } = false;
    public bool NoBarbCities { get; init; } = false;
    public bool NoBarbUnits { get; init; } = false;
    public bool NoGoodies { get; init; } = false;
    public int AdvancedStartPoints { get; init; } = 0;
    public int AnarchyPercent { get; init; } = 0;
    public int BuildPercent { get; init; } = 0;
    public int ConstructCostModifier { get; init; } = 0;
    public int ConstructPercent { get; init; } = 0;
    public int CreateCostModifier { get; init; } = 0;
    public int CreatePercent { get; init; } = 0;
    public int CulturePercent { get; init; } = 0;
    public int EventChancePerTurn { get; init; } = 0;
    public int FreePopulation { get; init; } = 0;
    public int GreatPeoplePercent { get; init; } = 0;
    public int GrowthPercent { get; init; } = 0;
    public int ImprovementPercent { get; init; } = 0;
    public int InitialCityMaintenancePercent { get; init; } = 0;
    public int ResearchPercent { get; init; } = 0;
    public int StartPercent { get; init; } = 0;
    public int StartingDefenseUnits { get; init; } = 0;
    public int StartingExploreUnits { get; init; } = 0;
    public int StartingGold { get; init; } = 0;
    public int StartingUnitMultiplier { get; init; } = 0;
    public int StartingWorkerUnits { get; init; } = 0;
    public int TechCostModifier { get; init; } = 0;
    public int TrainCostModifier { get; init; } = 0;
    public int TrainPercent { get; init; } = 0;
}
