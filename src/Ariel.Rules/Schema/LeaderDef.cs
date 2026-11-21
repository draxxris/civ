using System.Collections.Immutable;

namespace Ariel.Content.Schema;

/// <summary>Leader content. All authoritative numbers are integers.</summary>
public sealed class LeaderDef : GameContentDef
{
    public string DefensivePactRefuseAttitudeThreshold { get; init; } = "";
    public string DemandTributeAttitudeThreshold { get; init; } = "";
    public ImmutableDictionary<string, int> Flavors { get; init; } = ImmutableDictionary<string, int>.Empty;
    [ContentReference(typeof(ImprovementDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> ImprovementWeightModifiers { get; init; } = ImmutableDictionary<string, int>.Empty;
    public ImmutableDictionary<string, int> MemoryAttitudePercents { get; init; } = ImmutableDictionary<string, int>.Empty;
    public ImmutableDictionary<string, int> MemoryDecays { get; init; } = ImmutableDictionary<string, int>.Empty;
    public string NoGiveHelpAttitudeThreshold { get; init; } = "";
    public string OpenBordersRefuseAttitudeThreshold { get; init; } = "";
    public string StopTradingRefuseAttitudeThreshold { get; init; } = "";
    public string StopTradingThemRefuseAttitudeThreshold { get; init; } = "";
    public string TechRefuseAttitudeThreshold { get; init; } = "";
    [ContentReference(typeof(TraitDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> Traits { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(UnitAiDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> UnitAiWeightModifiers { get; init; } = ImmutableDictionary<string, int>.Empty;
    public int AtPeaceAttitudeChangeLimit { get; init; } = 0;
    public int AtPeaceAttitudeDivisor { get; init; } = 0;
    public int AtWarAttitudeChangeLimit { get; init; } = 0;
    public int AtWarAttitudeDivisor { get; init; } = 0;
    public int BaseAttackOddsChange { get; init; } = 0;
    public int BaseAttitude { get; init; } = 0;
    public int BasePeaceWeight { get; init; } = 0;
    public int BonusTradeAttitudeChangeLimit { get; init; } = 0;
    public int BonusTradeAttitudeDivisor { get; init; } = 0;
    public int BuildUnitProbability { get; init; } = 0;
    public int CloseBordersAttitudeChange { get; init; } = 0;
    public int DefensivePactAttitudeChangeLimit { get; init; } = 0;
    public int DefensivePactAttitudeDivisor { get; init; } = 0;
    public int DemandRebukedWarProbability { get; init; } = 0;
    public int DifferentReligionAttitudeChange { get; init; } = 0;
    public int DifferentReligionAttitudeChangeLimit { get; init; } = 0;
    public int DifferentReligionAttitudeDivisor { get; init; } = 0;
    public int FavoriteCivicAttitudeChange { get; init; } = 0;
    public int FavoriteCivicAttitudeChangeLimit { get; init; } = 0;
    public int FavoriteCivicAttitudeDivisor { get; init; } = 0;
    public int MaxWarRand { get; init; } = 0;
    public int OpenBordersAttitudeChangeLimit { get; init; } = 0;
    public int OpenBordersAttitudeDivisor { get; init; } = 0;
    public int RazeCityProbability { get; init; } = 0;
    public int SameReligionAttitudeChange { get; init; } = 0;
    public int SameReligionAttitudeChangeLimit { get; init; } = 0;
    public int SameReligionAttitudeDivisor { get; init; } = 0;
    public int WarmongerRespect { get; init; } = 0;
    public int WonderConstructRand { get; init; } = 0;
    [ContentReference(typeof(CivicDef))]
    public string? FavoriteCivic { get; init; }
}
