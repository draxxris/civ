using System.Collections.Immutable;

namespace Civ.Content.Schema;

/// <summary>Feature content. All authoritative numbers are integers.</summary>
public sealed class FeatureDef : GameContentDef
{
    public YieldVector RiverYieldChange { get; init; } = default;
    [ContentReference(typeof(TerrainDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> Terrains { get; init; } = ImmutableDictionary<string, bool>.Empty;
    public YieldVector YieldChanges { get; init; } = default;
    public bool AddsFreshWater { get; init; } = false;
    public bool CanGrowAnywhere { get; init; } = false;
    public bool IgnoreTerrainCulture { get; init; } = false;
    public bool Impassable { get; init; } = false;
    public bool NoAdjacent { get; init; } = false;
    public bool NoCity { get; init; } = false;
    public bool NoCoast { get; init; } = false;
    public bool NoImprovement { get; init; } = false;
    public bool NoRiver { get; init; } = false;
    public bool NukeImmune { get; init; } = false;
    public bool RequiresFlatlands { get; init; } = false;
    public bool RequiresRiver { get; init; } = false;
    public bool VisibleAlways { get; init; } = false;
    public int AdvancedStartRemoveCost { get; init; } = 0;
    public int Appearance { get; init; } = 0;
    public int CultureDistance { get; init; } = 0;
    public int DefensePercent { get; init; } = 0;
    public int Disappearance { get; init; } = 0;
    public int EffectProbability { get; init; } = 0;
    public int Growth { get; init; } = 0;
    public int HealthPercent { get; init; } = 0;
    public int MovementCost { get; init; } = 0;
    public int SeeThrough { get; init; } = 0;
    public int Spread { get; init; } = 0;
    public int TurnDamage { get; init; } = 0;
    public int WarmingDefense { get; init; } = 0;
}
