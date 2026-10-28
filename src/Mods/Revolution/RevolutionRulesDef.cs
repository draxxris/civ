using System.Collections.Immutable;
using Ariel.Content;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Revolution;

public sealed class RevolutionRulesDef : ContentDef
{
    public int WarningThreshold { get; init; } = 400;
    public int MutinyThreshold { get; init; } = 700;
    public int RebellionThreshold { get; init; } = 1000;
    public int WellbeingFactor { get; init; } = 12;
    public int DistanceFactor { get; init; } = 3;
    public int NonStateReligionFactor { get; init; } = 10;
    public int StateReligionFactor { get; init; } = -10;
    public int IntolerancePercent { get; init; } = 100;
    public int AnarchyFactor { get; init; } = 40;
    public int GarrisonFactor { get; init; } = -12;
    public int UndefendedFactor { get; init; } = 15;
    public int GoldenAgeFactor { get; init; } = -30;
    public int NaturalRecovery { get; init; } = -5;
    public int RebellionRelease { get; init; } = 600;
    public int CooldownTurns { get; init; } = 10;
    public int UnrestTurns { get; init; } = 3;
    public int UnrestUnhappiness { get; init; } = 3;
    public int RebelUnits { get; init; } = 2;
    public int SuppressionReduction { get; init; } = 400;
    public int SuppressionGold { get; init; } = 100;
    [ContentReference(typeof(S.UnitDef))]
    public string RebelUnit { get; init; } = "";
    [ContentReference(typeof(S.CivicDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> CivicFactors { get; init; } = ImmutableDictionary<string, int>.Empty;
}
