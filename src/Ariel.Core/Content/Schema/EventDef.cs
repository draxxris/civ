using System.Collections.Immutable;

namespace Ariel.Content.Schema;

/// <summary>A random event that can happen to a player. Eligibility is integer-only; the chance is rolled per turn.</summary>
public sealed class EventTriggerDef : GameContentDef
{
    /// <summary>Team must know this technology.</summary>
    [ContentReference(typeof(TechDef))]
    public string? RequiredTech { get; init; }
    /// <summary>Required completed building in the affected city.</summary>
    [ContentReference(typeof(BuildingDef))]
    public string? RequiredBuilding { get; init; }
    /// <summary>Required improvement on a plot near the affected city.</summary>
    [ContentReference(typeof(ImprovementDef))]
    public string? RequiredImprovement { get; init; }
    /// <summary>Required feature on a plot near the affected city.</summary>
    [ContentReference(typeof(FeatureDef))]
    public string? RequiredFeature { get; init; }
    /// <summary>Whether the event happens to one city. City conditions imply this.</summary>
    public bool CityScope { get; init; }
    /// <summary>Smallest population of the affected city.</summary>
    public int MinPopulation { get; init; }
    /// <summary>Smallest player treasury.</summary>
    public int MinTreasury { get; init; }
    /// <summary>First turn on which the event can happen.</summary>
    public int MinTurn { get; init; }
    /// <summary>Percent chance each turn, 0 to 100.</summary>
    public int ChancePercent { get; init; }
    /// <summary>False means once per player. True means the event waits CooldownTurns between occurrences.</summary>
    public bool Repeatable { get; init; }
    /// <summary>Turns between occurrences for a repeatable event.</summary>
    public int CooldownTurns { get; init; }
}

/// <summary>One choice for an event. Numbers are integers; negative gold is a cost.</summary>
public sealed class EventOptionDef : GameContentDef
{
    /// <summary>The event this option belongs to.</summary>
    [ContentReference(typeof(EventTriggerDef))]
    public string Trigger { get; init; } = "";
    /// <summary>Chosen automatically when the player did not choose, and by AI players.</summary>
    public bool IsDefault { get; init; }
    /// <summary>Gold gained, or paid when negative.</summary>
    public int GoldDelta { get; init; }
    /// <summary>Beakers added to current research.</summary>
    public int ResearchBonus { get; init; }
    /// <summary>Stored food added to the city.</summary>
    public int CityFoodDelta { get; init; }
    /// <summary>Production added to the first queued item of the city.</summary>
    public int CityProductionDelta { get; init; }
    /// <summary>City culture in hundredths of a point.</summary>
    public int CityCultureHundredthsDelta { get; init; }
    /// <summary>City happiness change.</summary>
    public int HappinessDelta { get; init; }
    /// <summary>City health change.</summary>
    public int HealthDelta { get; init; }
    /// <summary>Yield change on the event plot.</summary>
    public int PlotFoodDelta { get; init; }
    /// <summary>Yield change on the event plot.</summary>
    public int PlotProductionDelta { get; init; }
    /// <summary>Yield change on the event plot.</summary>
    public int PlotCommerceDelta { get; init; }
    /// <summary>Turns that happiness, health and plot changes last. Zero means permanent.</summary>
    public int DurationTurns { get; init; }
    /// <summary>Shown to the player after the choice.</summary>
    public string Outcome { get; init; } = "";
}
