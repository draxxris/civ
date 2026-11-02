using Ariel.Content;
namespace Ariel.Mods.Espionage;

/// <summary>Every balance number the espionage mod uses. Point costs are whole espionage points before the
/// bilateral ratio multiplier; durations are nominal turns scaled by the game speed's pace.</summary>
public sealed class EspionageRulesDef : ContentDef
{
    public int DemographicsThreshold { get; init; } = 50;
    public int CityVisibilityThreshold { get; init; } = 100;
    public int InspectResearchThreshold { get; init; } = 150;
    public int InvestigateCityThreshold { get; init; } = 200;

    public int StealTreasuryCost { get; init; } = 50;
    public int SabotageProductionCost { get; init; } = 80;
    public int InciteRevoltCost { get; init; } = 100;
    public int FomentUnhappinessCost { get; init; } = 60;
    /// <summary>Flat price. Unlike the research budget, counter-espionage does not get cheaper on fast speeds.</summary>
    public int CounterEspionageCost { get; init; } = 100;
    /// <summary>Technology theft price as a percentage of the technology's research cost.</summary>
    public int StealTechCostPercent { get; init; } = 200;

    /// <summary>Share of the target treasury stolen, in percent, never less than one gold.</summary>
    public int StealTreasuryPercent { get; init; } = 25;
    /// <summary>Share of the active production order's progress destroyed, in percent.</summary>
    public int SabotagePercent { get; init; } = 50;
    public int FomentUnhappinessTurns { get; init; } = 3;
    public int FomentUnhappiness { get; init; } = 3;
    /// <summary>Fixed disorder duration for a spy-incited revolt, like any other revolt.</summary>
    public int InciteRevoltTurns { get; init; } = 3;

    public int CounterEspionageTurns { get; init; } = 10;
    /// <summary>Cost multiplier against a protected team, in percent.</summary>
    public int CounterEspionagePercent { get; init; } = 200;
    /// <summary>Additional cost multiplier per interception point of the spy that set the protection.</summary>
    public int CounterEspionagePerInterception { get; init; } = 5;

    public int BaseSuccessPercent { get; init; } = 85;
    public int DefenderSuccessPenalty { get; init; } = 15;
    /// <summary>Counter-spies past this count add no further penalty; the formula's cliff is explicit here.</summary>
    public int MaxDefenders { get; init; } = 6;
    /// <summary>Success penalty per 100% of the defender's counter-espionage cost multiplier above 100%.</summary>
    public int ProtectionSuccessPenalty { get; init; } = 20;
    public int RatioSuccessDivisor { get; init; } = 5;
    public int MinSuccessPercent { get; init; } = 10;
    public int MaxSuccessPercent { get; init; } = 95;

    public int MaxWeight { get; init; } = 10000;
}
