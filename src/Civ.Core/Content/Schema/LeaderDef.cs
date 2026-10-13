using System.Collections.Immutable;

namespace Civ.Content.Schema;

/// <summary>Leader content. All authoritative numbers are integers.</summary>
public sealed class LeaderDef : GameContentDef
{
    public string AdoptCivicRefuseAttitudeThreshold { get; init; } = "";
    public ImmutableDictionary<string, int> ContactDelays { get; init; } = ImmutableDictionary<string, int>.Empty;
    public ImmutableDictionary<string, int> ContactRands { get; init; } = ImmutableDictionary<string, int>.Empty;
    public string ConvertReligionRefuseAttitudeThreshold { get; init; } = "";
    public string DeclareWarRefuseAttitudeThreshold { get; init; } = "";
    public string DeclareWarThemRefuseAttitudeThreshold { get; init; } = "";
    public string DefensivePactRefuseAttitudeThreshold { get; init; } = "";
    public string DemandTributeAttitudeThreshold { get; init; } = "";
    [ContentReference(typeof(ReligionIdentityDef), ReferencePart.Values)]
    public string? FavoriteReligion { get; init; } = null;
    public ImmutableDictionary<string, int> Flavors { get; init; } = ImmutableDictionary<string, int>.Empty;
    public string HappinessBonusRefuseAttitudeThreshold { get; init; } = "";
    public string HealthBonusRefuseAttitudeThreshold { get; init; } = "";
    [ContentReference(typeof(ImprovementDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> ImprovementWeightModifiers { get; init; } = ImmutableDictionary<string, int>.Empty;
    public string MapRefuseAttitudeThreshold { get; init; } = "";
    public ImmutableDictionary<string, int> MemoryAttitudePercents { get; init; } = ImmutableDictionary<string, int>.Empty;
    public ImmutableDictionary<string, int> MemoryDecays { get; init; } = ImmutableDictionary<string, int>.Empty;
    public string NoGiveHelpAttitudeThreshold { get; init; } = "";
    public ImmutableDictionary<string, int> NoWarAttitudeProbabilities { get; init; } = ImmutableDictionary<string, int>.Empty;
    public string OpenBordersRefuseAttitudeThreshold { get; init; } = "";
    public string PermanentAllianceRefuseAttitudeThreshold { get; init; } = "";
    public string StopTradingRefuseAttitudeThreshold { get; init; } = "";
    public string StopTradingThemRefuseAttitudeThreshold { get; init; } = "";
    public string StrategicBonusRefuseAttitudeThreshold { get; init; } = "";
    public string TechRefuseAttitudeThreshold { get; init; } = "";
    [ContentReference(typeof(TraitDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, bool> Traits { get; init; } = ImmutableDictionary<string, bool>.Empty;
    [ContentReference(typeof(UnitAiDef), ReferencePart.Keys)]
    public ImmutableDictionary<string, int> UnitAiWeightModifiers { get; init; } = ImmutableDictionary<string, int>.Empty;
    public string VassalRefuseAttitudeThreshold { get; init; } = "";
    public int AtPeaceAttitudeChangeLimit { get; init; } = 0;
    public int AtPeaceAttitudeDivisor { get; init; } = 0;
    public int AtWarAttitudeChangeLimit { get; init; } = 0;
    public int AtWarAttitudeDivisor { get; init; } = 0;
    public int AttackOddsChangeRand { get; init; } = 0;
    public int BaseAttackOddsChange { get; init; } = 0;
    public int BaseAttitude { get; init; } = 0;
    public int BasePeaceWeight { get; init; } = 0;
    public int BetterRankDifferenceAttitudeChange { get; init; } = 0;
    public int BonusTradeAttitudeChangeLimit { get; init; } = 0;
    public int BonusTradeAttitudeDivisor { get; init; } = 0;
    public int BuildUnitProbability { get; init; } = 0;
    public int CloseBordersAttitudeChange { get; init; } = 0;
    public int ConquestVictoryWeight { get; init; } = 0;
    public int CultureVictoryWeight { get; init; } = 0;
    public int DeclareWarTradeRand { get; init; } = 0;
    public int DefensivePactAttitudeChangeLimit { get; init; } = 0;
    public int DefensivePactAttitudeDivisor { get; init; } = 0;
    public int DemandRebukedSneakProbability { get; init; } = 0;
    public int DemandRebukedWarProbability { get; init; } = 0;
    public int DifferentReligionAttitudeChange { get; init; } = 0;
    public int DifferentReligionAttitudeChangeLimit { get; init; } = 0;
    public int DifferentReligionAttitudeDivisor { get; init; } = 0;
    public int DiplomacyVictoryWeight { get; init; } = 0;
    public int DogpileWarRand { get; init; } = 0;
    public int DominationVictoryWeight { get; init; } = 0;
    public int EspionageWeight { get; init; } = 0;
    public int FavoriteCivicAttitudeChange { get; init; } = 0;
    public int FavoriteCivicAttitudeChangeLimit { get; init; } = 0;
    public int FavoriteCivicAttitudeDivisor { get; init; } = 0;
    public int FreedomAppreciation { get; init; } = 0;
    public int LimitedWarPowerRatio { get; init; } = 0;
    public int LimitedWarRand { get; init; } = 0;
    public int LostWarAttitudeChange { get; init; } = 0;
    public int MakePeaceRand { get; init; } = 0;
    public int MaxGoldPerTurnTradePercent { get; init; } = 0;
    public int MaxGoldTradePercent { get; init; } = 0;
    public int MaxWarDistantPowerRatio { get; init; } = 0;
    public int MaxWarMinAdjacentLandPercent { get; init; } = 0;
    public int MaxWarNearbyPowerRatio { get; init; } = 0;
    public int MaxWarRand { get; init; } = 0;
    public int NoTechTradeThreshold { get; init; } = 0;
    public int OpenBordersAttitudeChangeLimit { get; init; } = 0;
    public int OpenBordersAttitudeDivisor { get; init; } = 0;
    public int PeaceWeightRand { get; init; } = 0;
    public int RazeCityProbability { get; init; } = 0;
    public int RefuseToTalkWarThreshold { get; init; } = 0;
    public int SameReligionAttitudeChange { get; init; } = 0;
    public int SameReligionAttitudeChangeLimit { get; init; } = 0;
    public int SameReligionAttitudeDivisor { get; init; } = 0;
    public int ShareWarAttitudeChange { get; init; } = 0;
    public int ShareWarAttitudeChangeLimit { get; init; } = 0;
    public int ShareWarAttitudeDivisor { get; init; } = 0;
    public int SpaceVictoryWeight { get; init; } = 0;
    public int TechTradeKnownPercent { get; init; } = 0;
    public int VassalPowerModifier { get; init; } = 0;
    public int WarmongerRespect { get; init; } = 0;
    public int WonderConstructRand { get; init; } = 0;
    public int WorseRankDifferenceAttitudeChange { get; init; } = 0;
    [ContentReference(typeof(CivicDef))]
    public string? FavoriteCivic { get; init; }
}
