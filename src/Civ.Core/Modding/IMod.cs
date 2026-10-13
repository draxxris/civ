using Civ.Content;

namespace Civ.Modding;

/// <summary>Mod DLL entry point; the loader finds exactly one public implementation.</summary>
public interface IMod
{
    /// <summary>Register content and rules during mod loading.</summary>
    void Register(IModContext ctx);
}

/// <summary>Read access to sidecar data relative to the mod folder.</summary>
public interface IModData
{
    /// <summary>Whether a sidecar path exists.</summary>
    bool Exists(string path);
    /// <summary>Read a UTF-8 text sidecar.</summary>
    string ReadText(string path);
    /// <summary>Read typed JSON using shared content converters.</summary>
    T ReadJson<T>(string path);
}

/// <summary>Mod registration contract, usable only during deterministic loading.</summary>
public interface IModContext
{
    /// <summary>Register any data-driven content subtype in this mod's namespace.</summary>
    ModRegistry<T> Content<T>() where T : ContentDef;
    /// <summary>Register under a shared service interface in load/registration order.</summary>
    void RegisterService<T>(string name, T service) where T : class;
    /// <summary>Register a stable identity for a JSON-serializable state type.</summary>
    void RegisterStateComponent<T>(string name) where T : class;
    /// <summary>Subscribe to an event type in deterministic registration order.</summary>
    void Subscribe<TEvent>(string name, Action<Game.Simulation, TEvent> handler) where TEvent : Game.DomainEvent;
    /// <summary>This mod's unqualified id.</summary>
    string ModId { get; }
    /// <summary>This mod's sidecar reader.</summary>
    IModData Data { get; }
    /// <summary>Terrain convenience registry.</summary>
    ModRegistry<TerrainDef> Terrains { get; }
    /// <summary>Civilization convenience registry.</summary>
    ModRegistry<CivilizationDef> Civilizations { get; }
    /// <summary>Deterministic player AI registry.</summary>
    ModRegistry<IPlayerAI> PlayerAIs { get; }
    /// <summary>Unit convenience registry.</summary>
    ModRegistry<UnitDef> Units { get; }
    /// <summary>Promotion convenience registry.</summary>
    ModRegistry<PromotionDef> Promotions { get; }
    /// <summary>Experience threshold convenience registry.</summary>
    ModRegistry<ExperienceLevelDef> ExperienceLevels { get; }
    /// <summary>Specialist convenience registry.</summary>
    ModRegistry<SpecialistDef> Specialists { get; }
    /// <summary>Civic convenience registry.</summary>
    ModRegistry<CivicDef> Civics { get; }
    /// <summary>Technology convenience registry.</summary>
    ModRegistry<TechDef> Techs { get; }
    /// <summary>Building convenience registry.</summary>
    ModRegistry<BuildingDef> Buildings { get; }
    /// <summary>Culture level convenience registry.</summary>
    ModRegistry<CultureLevelDef> CultureLevels { get; }
    /// <summary>Map generator registry.</summary>
    ModRegistry<IMapGenerator> MapGenerators { get; }
    /// <summary>Ordered turn step registry.</summary>
    ModRegistry<ITurnSystem> TurnSystems { get; }
    /// <summary>Command serialization registry.</summary>
    ModRegistry<CommandTypeDef> CommandTypes { get; }
    /// <summary>Register a command under modid:name.</summary>
    void RegisterCommand<TCommand>(string name) where TCommand : Game.GameCommand;
}
