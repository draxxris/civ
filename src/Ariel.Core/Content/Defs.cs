namespace Ariel.Content;

/// <summary>Stable namespaced identity shared by content and legacy rule registries.</summary>
public interface IHasId
{
    /// <summary>Ordinal id in the form modid:name.</summary>
    string Id { get; }
}

/// <summary>Base for sidecar definitions. Definitions and nested collections must not be mutated after loading.</summary>
public abstract class ContentDef : IHasId
{
    /// <summary>Stable namespaced identity.</summary>
    public string Id { get; init; } = "";
    /// <summary>Localized display name.</summary>
    public string Name { get; init; } = "";
    /// <summary>Optional eligibility expression, interpreted by registered leaf evaluators.</summary>
    public Requirement? Requirements { get; init; }
}

/// <summary>Basic terrain data used by map generators, rules and clients.</summary>
public sealed class TerrainDef : ContentDef
{
    /// <summary>Integer movement cost.</summary>
    public int MoveCost { get; init; } = 1;
    /// <summary>Whether the plot is water.</summary>
    public bool IsWater { get; init; }
    /// <summary>Whether ordinary land units may enter.</summary>
    public bool Passable { get; init; } = true;
    /// <summary>Flat food yield.</summary>
    public int Food { get; init; }
    /// <summary>Flat production yield.</summary>
    public int Production { get; init; }
    /// <summary>Flat commerce yield.</summary>
    public int Commerce { get; init; }
    /// <summary>Client color hint, #rrggbb.</summary>
    public string Color { get; init; } = "#888888";
    /// <summary>Percent strength bonus for a defender.</summary>
    public int DefenseBonus { get; init; }
}

/// <summary>Minimal technology schema; richer feature-mod fields follow separately.</summary>
public sealed class TechDef : ContentDef
{
    /// <summary>Required research points.</summary>
    public int Cost { get; init; } = 10;
    /// <summary>Legacy all-required technology ids. New rulesets use Requirements.</summary>
    public string[] Prerequisites { get; init; } = [];
}

/// <summary>Minimal building schema for the test ruleset.</summary>
public sealed class BuildingDef : ContentDef
{
    /// <summary>Production cost.</summary>
    public int Cost { get; init; } = 20;
    /// <summary>Legacy required technology, or null.</summary>
    public string? RequiresTech { get; init; }
    /// <summary>Flat food yield.</summary>
    public int Food { get; init; }
    /// <summary>Flat production yield.</summary>
    public int Production { get; init; }
    /// <summary>Flat commerce yield.</summary>
    public int Commerce { get; init; }
    /// <summary>Flat culture yield.</summary>
    public int Culture { get; init; }
    /// <summary>Starting experience for units trained in this city.</summary>
    public int UnitExperience { get; init; }
    /// <summary>Slots by specialist id; -1 means unlimited.</summary>
    public Dictionary<string, int> SpecialistSlots { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>A non-working citizen role; slots come from buildings and civics.</summary>
public sealed class SpecialistDef : ContentDef
{
    /// <summary>Whether the role needs no slots.</summary>
    public bool Unlimited { get; init; }
    /// <summary>Food per specialist.</summary>
    public int Food { get; init; }
    /// <summary>Production per specialist.</summary>
    public int Production { get; init; }
    /// <summary>Commerce per specialist.</summary>
    public int Commerce { get; init; }
    /// <summary>Culture per specialist.</summary>
    public int Culture { get; init; }
}

/// <summary>A government option; a player holds one civic per category.</summary>
public sealed class CivicDef : ContentDef
{
    /// <summary>Category identity.</summary>
    public string Category { get; init; } = "";
    /// <summary>Initial civic when the category has no choice yet.</summary>
    public bool IsDefault { get; init; }
    /// <summary>Legacy required technology, or null.</summary>
    public string? RequiresTech { get; init; }
    /// <summary>Percentage research bonus; 25 means +25%.</summary>
    public int ResearchBonus { get; init; }
    /// <summary>Percentage production bonus.</summary>
    public int ProductionBonus { get; init; }
    /// <summary>Percentage culture bonus.</summary>
    public int CultureBonus { get; init; }
    /// <summary>Slots by specialist id; -1 means unlimited.</summary>
    public Dictionary<string, int> SpecialistSlots { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>Minimal unit schema for the test ruleset.</summary>
public sealed class UnitDef : ContentDef
{
    /// <summary>Production cost.</summary>
    public int Cost { get; init; } = 20;
    /// <summary>Legacy required technology, or null.</summary>
    public string? RequiresTech { get; init; }
    /// <summary>Base combat strength; zero cannot fight.</summary>
    public int Strength { get; init; }
    /// <summary>Integer movement points per turn.</summary>
    public int Moves { get; init; } = 1;
    /// <summary>Whether this unit can found cities.</summary>
    public bool CanFoundCity { get; init; }
    /// <summary>Class tags used to restrict promotions.</summary>
    public string[] Classes { get; init; } = [];
}

/// <summary>Minimal promotion schema for the test ruleset.</summary>
public sealed class PromotionDef : ContentDef
{
    /// <summary>Client help text.</summary>
    public string Description { get; init; } = "";
    /// <summary>Allowed class tags; empty means any combat unit.</summary>
    public string[] Classes { get; init; } = [];
    /// <summary>Required previous promotions; at least one, or none if empty.</summary>
    public string[] RequiresAny { get; init; } = [];
    /// <summary>Percentage strength bonus.</summary>
    public int StrengthBonus { get; init; }
    /// <summary>Percentage attacking strength bonus.</summary>
    public int AttackBonus { get; init; }
    /// <summary>Percentage defending strength bonus.</summary>
    public int DefenseBonus { get; init; }
    /// <summary>Extra movement points.</summary>
    public int MovesBonus { get; init; }
}

/// <summary>Experience threshold earning one promotion.</summary>
public sealed class ExperienceLevelDef : ContentDef
{
    /// <summary>Required accumulated experience.</summary>
    public int Threshold { get; init; }
}

/// <summary>A level reached through accumulated culture.</summary>
public sealed class CultureLevelDef : ContentDef
{
    /// <summary>Required accumulated culture.</summary>
    public long Threshold { get; init; }
}

/// <summary>Playable civilization with starting techs and test-ruleset modifiers.</summary>
public sealed class CivilizationDef : ContentDef
{
    /// <summary>Leader display name.</summary>
    public string Leader { get; init; } = "";
    /// <summary>Client color hint, #rrggbb.</summary>
    public string Color { get; init; } = "#cccccc";
    /// <summary>Initially known technology ids.</summary>
    public string[] StartingTechs { get; init; } = [];
    /// <summary>Percentage research bonus.</summary>
    public int ResearchBonus { get; init; }
    /// <summary>Percentage production bonus.</summary>
    public int ProductionBonus { get; init; }
    /// <summary>Percentage culture bonus.</summary>
    public int CultureBonus { get; init; }
}

/// <summary>Command serialization identity and its CLR type.</summary>
public sealed record CommandTypeDef(string Id, Type Type) : IHasId;
