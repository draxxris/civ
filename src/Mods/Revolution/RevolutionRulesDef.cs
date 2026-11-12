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
    /// <summary>Garrisons past this count add no further calm; the cliff is content, not a hidden constant.</summary>
    public int MaxGarrison { get; init; } = 4;
    public int RebellionRelease { get; init; } = 600;
    public int CooldownTurns { get; init; } = 10;
    public int UnrestTurns { get; init; } = 3;
    public int UnrestUnhappiness { get; init; } = 3;
    /// <summary>Happiness penalty once a city reaches Mutiny. Warning stays a display-only early alert.</summary>
    public int MutinyUnhappiness { get; init; } = 2;
    /// <summary>Pressure a spy-incited revolt adds before the normal rebellion rules apply.</summary>
    public int InciteInstability { get; init; } = 300;
    public int RebelUnits { get; init; } = 2;
    public int SuppressionReduction { get; init; } = 400;
    /// <summary>Price of a full <see cref="SuppressionReduction"/>; smaller reductions cost proportionally less.</summary>
    public int SuppressionGold { get; init; } = 100;
    [ContentReference(typeof(S.UnitDef))]
    public string RebelUnit { get; init; } = "";
    [ContentReference(typeof(S.CivicDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> CivicFactors { get; init; } = ImmutableDictionary<string, int>.Empty;
}
