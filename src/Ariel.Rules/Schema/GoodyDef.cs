using System.Collections.Immutable;

namespace Ariel.Content.Schema;

/// <summary>Goody content. All authoritative numbers are integers.</summary>
public sealed class GoodyDef : GameContentDef
{
    [ContentReference(typeof(UnitDef), ReferencePart.Values)]
    public string? BarbarianUnit { get; init; } = null;
    [ContentReference(typeof(UnitDef), ReferencePart.Values)]
    public string? GrantedUnit { get; init; } = null;
    public bool Bad { get; init; } = false;
    public bool Tech { get; init; } = false;
    public int BarbarianUnitProbability { get; init; } = 0;
    public int DamagePrereq { get; init; } = 0;
    public int Experience { get; init; } = 0;
    public int Gold { get; init; } = 0;
    public int GoldRand1 { get; init; } = 0;
    public int GoldRand2 { get; init; } = 0;
    public int Healing { get; init; } = 0;
    public int MapOffset { get; init; } = 0;
    public int MapProbability { get; init; } = 0;
    public int MapRange { get; init; } = 0;
    public int MinBarbarians { get; init; } = 0;
}
