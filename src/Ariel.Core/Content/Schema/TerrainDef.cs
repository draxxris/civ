using System.Collections.Immutable;

namespace Ariel.Content.Schema;

/// <summary>Terrain content. All authoritative numbers are integers.</summary>
public sealed class TerrainDef : GameContentDef
{
    public YieldVector RiverYieldChange { get; init; } = default;
    public YieldVector Yields { get; init; } = default;
    public bool CanFoundCity { get; init; } = false;
    public bool FoundCoast { get; init; } = false;
    public bool FoundFreshWater { get; init; } = false;
    public bool GraphicalOnly { get; init; } = false;
    public bool Impassable { get; init; } = false;
    public bool IsWater { get; init; } = false;
    public int BuildModifier { get; init; } = 0;
    public int CultureDistance { get; init; } = 0;
    public int DefensePercent { get; init; } = 0;
    public int HealthPercent { get; init; } = 0;
    public int MovementCost { get; init; } = 0;
    public int SeeFrom { get; init; } = 0;
    public int SeeThrough { get; init; } = 0;
}
