namespace Ariel.Content.Schema;

/// <summary>An executable victory condition. Rule is one of conquest, domination, cultural, time, project.
/// Lower Priority values are checked first.</summary>
public sealed class VictoryDef : GameContentDef
{
    [ContentReference(typeof(VictoryIdentityDef))]
    public string? Identity { get; init; }
    public string Rule { get; init; } = "";
    public bool Enabled { get; init; } = true;
    public int Priority { get; init; }
    /// <summary>Domination: share of all city population, in percent.</summary>
    public int PopulationPercent { get; init; }
    /// <summary>Domination: share of all land plots, in percent.</summary>
    public int LandPercent { get; init; }
    /// <summary>Cultural: number of cities at the required culture level.</summary>
    public int CultureCities { get; init; }
    [ContentReference(typeof(CultureLevelDef))]
    public string? CultureLevel { get; init; }
    /// <summary>Time: base turn limit, scaled by the game speed victory delay percent.</summary>
    public int MaxTurns { get; init; }
    /// <summary>Time: score points for each citizen, land plot, technology and wonder.</summary>
    public int ScorePerPopulation { get; init; }
    public int ScorePerLand { get; init; }
    public int ScorePerTech { get; init; }
    public int ScorePerWonder { get; init; }
}
