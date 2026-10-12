using Civ.Content;

namespace Civ.Modding;

/// <summary>Entry point of a mod DLL. The loader finds the single public implementation in the mod's assembly.</summary>
public interface IMod
{
    void Register(IModContext ctx);
}

/// <summary>Read access to the mod's sidecar data files (paths are relative to the mod folder).</summary>
public interface IModData
{
    bool Exists(string path);
    string ReadText(string path);
    T ReadJson<T>(string path);
}

public interface IModContext
{
    string ModId { get; }
    IModData Data { get; }
    ModRegistry<TerrainDef> Terrains { get; }
    ModRegistry<CivilizationDef> Civilizations { get; }
    ModRegistry<IPlayerAI> PlayerAIs { get; }
    ModRegistry<UnitDef> Units { get; }
    ModRegistry<PromotionDef> Promotions { get; }
    ModRegistry<ExperienceLevelDef> ExperienceLevels { get; }
    ModRegistry<SpecialistDef> Specialists { get; }
    ModRegistry<CivicDef> Civics { get; }
    ModRegistry<TechDef> Techs { get; }
    ModRegistry<BuildingDef> Buildings { get; }
    ModRegistry<CultureLevelDef> CultureLevels { get; }
    ModRegistry<IMapGenerator> MapGenerators { get; }
    ModRegistry<ITurnSystem> TurnSystems { get; }
    ModRegistry<CommandTypeDef> CommandTypes { get; }
    /// <summary>Mod resources (faith, renown, ...), their data-driven rates, and the pools they accumulate in.</summary>
    ModRegistry<ResourceDef> Resources { get; }
    /// <summary>Code-driven per-city resource rates.</summary>
    ModRegistry<IResourceSource> ResourceSources { get; }
    /// <summary>Code-driven extra food/production/commerce/culture for a city.</summary>
    ModRegistry<ICityYieldSource> YieldSources { get; }
    /// <summary>Religions, guilds, corporations: founded in one city, spread by units or on their own.</summary>
    ModRegistry<InstitutionDef> Institutions { get; }
    /// <summary>Register a command class under "modid:name".</summary>
    void RegisterCommand<TCommand>(string name) where TCommand : Game.GameCommand;
}
