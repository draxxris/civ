using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.Mods.RangedCombat;

/// <summary>What a unit means to ranged strikes, kept in this mod's own rows (see "Content attachments" in
/// docs/DESIGN.md). A unit without a row reads as <see cref="None"/>.</summary>
public sealed class UnitRangedCombatDef : ContentAttachment<UnitDef>
{
    /// <summary>The row of a unit nothing attached to.</summary>
    public static readonly UnitRangedCombatDef None = new();
    /// <summary>Percent chance a ranged strike hits; zero means the default of 50. Clamped to 10..100.</summary>
    public int BombAccuracyPercent { get; init; }
}
