using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.Mods.AirCombat;

// What base definitions mean to air missions, kept in this mod's own rows (see "Content attachments" in
// docs/DESIGN.md). A definition without a row means nothing to air missions: read it as None, whose values are all
// zero.

/// <summary>What a unit means to air missions: its mission range, how hard it bombs, how much damage it may deal in
/// one air strike, and how likely it evades interception.</summary>
public sealed class UnitAirCombatDef : ContentAttachment<UnitDef>
{
    /// <summary>The row of a unit nothing attached to.</summary>
    public static readonly UnitAirCombatDef None = new();
    /// <summary>Hit points below full that the unit's air strikes may wound a defender to, at most; a defender
    /// already that hurt is no target.</summary>
    public int AirCombatLimit { get; init; }
    /// <summary>Tiles the unit reaches on a mission, before effects; twice this when rebasing.</summary>
    public int AirRange { get; init; }
    /// <summary>How hard the unit bombs a tile's improvement; zero means it cannot bomb.</summary>
    public int BombRate { get; init; }
    /// <summary>Percent chance the unit evades an interceptor, before effects.</summary>
    public int EvasionPercent { get; init; }
}

/// <summary>What an improvement means to air missions: how well it resists bombing.</summary>
public sealed class ImprovementAirCombatDef : ContentAttachment<ImprovementDef>
{
    /// <summary>The row of an improvement nothing attached to.</summary>
    public static readonly ImprovementAirCombatDef None = new();
    public int AirBombDefense { get; init; }
}

/// <summary>The air combat rule constants, attached to the base game's <see cref="RuleConstantsDef"/>.</summary>
public sealed class RuleConstantsAirCombatDef : ContentAttachment<RuleConstantsDef>
{
    /// <summary>The row when nothing attached to the rule constants.</summary>
    public static readonly RuleConstantsAirCombatDef None = new();
    /// <summary>Damage of an even air strike, before the strength ratio.</summary>
    public int AirCombatDamage { get; init; }
}
