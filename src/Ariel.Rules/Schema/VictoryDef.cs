namespace Ariel.Content.Schema;

/// <summary>A way to win this game. <see cref="Rule"/> names the registered victory condition that decides it
/// (<c>Ariel.Game.IVictoryCondition</c>): the Victory module's <c>victory:conquest</c>, <c>victory:domination</c>,
/// <c>victory:cultural</c> and <c>victory:time</c>, or a mod's. A definition ships with the module that registers its
/// condition. Lower Priority values are checked first. The fields below are the base conditions' parameters; a mod's
/// condition reads its own content.</summary>
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
    /// <summary>Time: score points for each citizen, land plot, technology and wonder. The first time victory by id
    /// weighs every player's score, whether or not it is enabled.</summary>
    public int ScorePerPopulation { get; init; }
    public int ScorePerLand { get; init; }
    public int ScorePerTech { get; init; }
    public int ScorePerWonder { get; init; }
}
