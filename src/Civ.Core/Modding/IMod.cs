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
    ModRegistry<TechDef> Techs { get; }
    ModRegistry<BuildingDef> Buildings { get; }
    ModRegistry<CultureLevelDef> CultureLevels { get; }
    ModRegistry<IMapGenerator> MapGenerators { get; }
    ModRegistry<ITurnSystem> TurnSystems { get; }
    ModRegistry<CommandTypeDef> CommandTypes { get; }
    /// <summary>Register a command class under "modid:name".</summary>
    void RegisterCommand<TCommand>(string name) where TCommand : Game.GameCommand;
}
