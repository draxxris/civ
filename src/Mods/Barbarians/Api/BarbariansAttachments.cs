using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.Mods.Barbarians;

// What base definitions mean to barbarian spawning, kept in this mod's own rows (see "Content attachments" in
// docs/DESIGN.md). A definition without a row means nothing to barbarians: read it as None, whose values are all
// zero or false.

/// <summary>What a handicap means to barbarians: when they may first appear, and how much unclaimed land or coastal
/// water supports one barbarian unit.</summary>
public sealed class HandicapBarbariansDef : ContentAttachment<HandicapDef>
{
    /// <summary>The row of a handicap nothing attached to.</summary>
    public static readonly HandicapBarbariansDef None = new();
    /// <summary>Turns before barbarians may appear, before the game speed's <see cref="GameSpeedBarbariansDef.BarbPercent"/>.</summary>
    public int BarbarianCreationTurnsElapsed { get; init; }
    /// <summary>Unowned land tiles per barbarian land unit. The data file holds the imported value; the loaded row
    /// holds it multiplied by the mod's hex <c>UnownedTilesMultiplier</c>. Zero or less spawns no land units.</summary>
    public int UnownedTilesPerBarbarianUnit { get; init; }
    /// <summary>Unowned coastal water tiles per barbarian ship, before the factor of three; zero or less means 40.</summary>
    public int UnownedWaterTilesPerBarbarianUnit { get; init; }
}

/// <summary>What a game speed means to barbarians.</summary>
public sealed class GameSpeedBarbariansDef : ContentAttachment<GameSpeedDef>
{
    /// <summary>The row of a game speed nothing attached to.</summary>
    public static readonly GameSpeedBarbariansDef None = new();
    /// <summary>Percent of the handicap's barbarian creation turns on this speed.</summary>
    public int BarbPercent { get; init; }
}

/// <summary>What an era means to barbarians.</summary>
public sealed class EraBarbariansDef : ContentAttachment<EraDef>
{
    /// <summary>The row of an era nothing attached to.</summary>
    public static readonly EraBarbariansDef None = new();
    /// <summary>No barbarians appear while every civilization is in this era.</summary>
    public bool NoBarbUnits { get; init; }
}
