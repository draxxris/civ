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
    /// <summary>Mod resources paid when research starts, by resource id (see <see cref="ResourceDef"/>).</summary>
    public Dictionary<string, long> ResourceCost { get; init; } = new();
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
    /// <summary>Specialist slots this building provides, by specialist id. -1 = unlimited.</summary>
    public Dictionary<string, int> SpecialistSlots { get; init; } = new();
    /// <summary>Mod resources paid when construction starts, by resource id.</summary>
    public Dictionary<string, long> ResourceCost { get; init; } = new();
    /// <summary>The city must host this institution (e.g. a particular guild).</summary>
    public string? RequiresInstitution { get; init; }
    /// <summary>The city must host some institution of this category (e.g. any religion).</summary>
    public string? RequiresInstitutionCategory { get; init; }
}

/// <summary>
/// What a citizen yields when it works no tile. Slots come from buildings and civics; an <see cref="Unlimited"/>
/// specialist (the plain citizen) needs none.
/// </summary>
public sealed class SpecialistDef : ContentDef
{
    public bool Unlimited { get; init; }
    public int Food { get; init; }
    public int Production { get; init; }
    public int Commerce { get; init; }
    public int Culture { get; init; }
}

/// <summary>A government/economy/labor/... option. A player holds one civic per <see cref="Category"/>.</summary>
public sealed class CivicDef : ContentDef
{
    public string Category { get; init; } = "";
    /// <summary>Held from the start of the game when its category has nothing else.</summary>
    public bool IsDefault { get; init; }
    public string? RequiresTech { get; init; }
    public int ResearchBonus { get; init; }
    public int ProductionBonus { get; init; }
    public int CultureBonus { get; init; }
    public Dictionary<string, int> SpecialistSlots { get; init; } = new();
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
    /// <summary>Mod resources paid when training starts, by resource id.</summary>
    public Dictionary<string, long> ResourceCost { get; init; } = new();
    /// <summary>The training city must host this institution.</summary>
    public string? RequiresInstitution { get; init; }
    /// <summary>The training city must host some institution of this category.</summary>
    public string? RequiresInstitutionCategory { get; init; }
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
