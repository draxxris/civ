using Ariel.Content;

namespace Ariel.Mods.Barbarians;

/// <summary>Hand-authored hex adaptation of barbarian spawning, never emitted by the importer.</summary>
public sealed class BarbarianRulesDef : ContentDef
{
    /// <summary>A hex map has far more tiles than the source grid it is scaled from, so each handicap's imported
    /// unowned land tiles per barbarian unit is multiplied by this when the mod loads it. At least 1.</summary>
    public int UnownedTilesMultiplier { get; init; } = 1;
}
