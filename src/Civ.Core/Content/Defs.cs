namespace Civ.Content;

public interface IHasId { string Id { get; } }

/// <summary>Base for data-driven definitions. Ids are namespaced: "modid:name". Plain init props so sidecar JSON binds directly.</summary>
public abstract class ContentDef : IHasId
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
}

public sealed class TerrainDef : ContentDef
{
    public int MoveCost { get; init; } = 1;
    public bool IsWater { get; init; }
    public bool Passable { get; init; } = true;
    public int Food { get; init; }
    public int Production { get; init; }
    public int Commerce { get; init; }
    /// <summary>Hint for clients, "#rrggbb".</summary>
    public string Color { get; init; } = "#888888";
    /// <summary>Percent bonus to the strength of a defender standing here.</summary>
    public int DefenseBonus { get; init; }
}

public sealed class TechDef : ContentDef
{
    public int Cost { get; init; } = 10;
    public string[] Prerequisites { get; init; } = [];
}

public sealed class BuildingDef : ContentDef
{
    public int Cost { get; init; } = 20;
    public string? RequiresTech { get; init; }
    public int Food { get; init; }
    public int Production { get; init; }
    public int Commerce { get; init; }
    public int Culture { get; init; }
    /// <summary>Experience given to every unit trained in a city with this building (Civ4 barracks).</summary>
    public int UnitExperience { get; init; }
}

public sealed class UnitDef : ContentDef
{
    public int Cost { get; init; } = 20;
    public string? RequiresTech { get; init; }
    /// <summary>0 = cannot fight.</summary>
    public int Strength { get; init; }
    public int Moves { get; init; } = 1;
    public bool CanFoundCity { get; init; }
    /// <summary>Tags such as "melee", "ranged", "mounted". Promotions are restricted by class.</summary>
    public string[] Classes { get; init; } = [];
}

/// <summary>
/// A specialization a unit can pick when it levels up. Empty <see cref="Classes"/> = any combat unit;
/// empty <see cref="RequiresAny"/> = no prerequisite promotion.
/// </summary>
public sealed class PromotionDef : ContentDef
{
    public string Description { get; init; } = "";
    public string[] Classes { get; init; } = [];
    public string[] RequiresAny { get; init; } = [];
    /// <summary>Percent bonuses to strength.</summary>
    public int StrengthBonus { get; init; }
    public int AttackBonus { get; init; }
    public int DefenseBonus { get; init; }
    public int MovesBonus { get; init; }
    /// <summary>Extra health restored per turn spent resting.</summary>
    public int HealBonus { get; init; }
}

/// <summary>Reaching <see cref="Threshold"/> experience earns one promotion.</summary>
public sealed class ExperienceLevelDef : ContentDef
{
    public int Threshold { get; init; }
}

/// <summary>A cultural level (Civ4: Poor, Fledgling, Developing...). Reached when accumulated culture >= Threshold.</summary>
public sealed class CultureLevelDef : ContentDef
{
    public long Threshold { get; init; }
}

/// <summary>A playable civilization: starting techs plus percentage modifiers the rule systems apply. Mods add or override these.</summary>
public sealed class CivilizationDef : ContentDef
{
    public string Leader { get; init; } = "";
    public string Color { get; init; } = "#cccccc";
    public string[] StartingTechs { get; init; } = [];
    /// <summary>Percent bonuses; 25 means +25%.</summary>
    public int ResearchBonus { get; init; }
    public int ProductionBonus { get; init; }
    public int CultureBonus { get; init; }
}

/// <summary>Registers a command class under an id so it can be serialized for the network.</summary>
public sealed record CommandTypeDef(string Id, Type Type) : IHasId;
