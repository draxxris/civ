namespace Civ.Content;

/// <summary>Everything mods registered. Built by <see cref="Modding.ModLoader"/>, immutable once frozen.</summary>
public sealed class ContentDb
{
    public Registry<TerrainDef> Terrains { get; } = new("terrain");
    public Registry<UnitDef> Units { get; } = new("unit");
    public Registry<PromotionDef> Promotions { get; } = new("promotion");
    public Registry<ExperienceLevelDef> ExperienceLevels { get; } = new("experience level");
    public Registry<SpecialistDef> Specialists { get; } = new("specialist");
    public Registry<CivicDef> Civics { get; } = new("civic");
    public Registry<TechDef> Techs { get; } = new("tech");
    public Registry<BuildingDef> Buildings { get; } = new("building");
    public Registry<CultureLevelDef> CultureLevels { get; } = new("culture level");
    public Registry<CivilizationDef> Civilizations { get; } = new("civilization");
    public Registry<IPlayerAI> PlayerAIs { get; } = new("player AI");
    public Registry<IMapGenerator> MapGenerators { get; } = new("map generator");
    public Registry<ITurnSystem> TurnSystems { get; } = new("turn system");
    public Registry<CommandTypeDef> CommandTypes { get; } = new("command type");

    public IReadOnlyList<string> LoadedMods { get; internal set; } = [];

    internal void Freeze()
    {
        Specialists.Freeze(); Civics.Freeze(); Units.Freeze(); Promotions.Freeze(); ExperienceLevels.Freeze(); Terrains.Freeze(); Civilizations.Freeze(); PlayerAIs.Freeze(); Techs.Freeze(); Buildings.Freeze(); CultureLevels.Freeze();
        MapGenerators.Freeze(); TurnSystems.Freeze(); CommandTypes.Freeze();
    }

    /// <summary>Highest culture level reached for a culture total, or null if below the first threshold.</summary>
    public CultureLevelDef? CultureLevelFor(long culture)
    {
        CultureLevelDef? best = null;
        foreach (var lvl in CultureLevels.All)
            if (culture >= lvl.Threshold && (best == null || lvl.Threshold >= best.Threshold)) best = lvl;
        return best;
    }
}

public interface IMapGenerator : IHasId
{
    /// <summary>Returns the terrain id for every tile of a hex map with the given radius. Must be deterministic in (radius, rng).</summary>
    IReadOnlyDictionary<Hex, string> Generate(int radius, DeterministicRng rng, ContentDb content);
}

/// <summary>
/// Controls a player. Called by the simulation itself at the start of each turn, on every peer, so it must be deterministic
/// and may use only the simulation state and <c>sim.Rng</c>. It returns ordinary commands, which are validated like a human's.
/// </summary>
public interface IPlayerAI : IHasId
{
    IEnumerable<Game.GameCommand> Decide(Game.Simulation sim, int playerId);
}

/// <summary>A step run once per game turn, in ascending <see cref="Order"/> (then id). This is how rules like research or production are plugged in.</summary>
public interface ITurnSystem : IHasId
{
    int Order { get; }
    void Run(Game.Simulation sim);
}
