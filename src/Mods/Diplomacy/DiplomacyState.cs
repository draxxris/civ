using Civ.Game;

namespace Civ.Mods.Diplomacy;

/// <summary>Authoritative contracts and counters. No derived caches or RNG are stored here.</summary>
public sealed class DiplomacyState
{
    public SortedDictionary<string, TeamRelation> Relations { get; set; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, AttitudeHistory> Histories { get; set; } = new(StringComparer.Ordinal);
    public List<DiplomaticDeal> Deals { get; set; } = [];
    public int NextDealId { get; set; } = 1;
    public int LastHistoryTurn { get; set; }
}
public sealed class TeamRelation
{
    public int First { get; set; }
    public int Second { get; set; }
    public int ContactTurn { get; set; }
    public int PeaceStarted { get; set; }
    public int? TreatyStarted { get; set; }
    public int? WarStarted { get; set; }
    public int FirstOffensiveWars { get; set; }
    public int SecondOffensiveWars { get; set; }
    public int? OpenBordersStarted { get; set; }
    public int? DefensivePactStarted { get; set; }
}
public sealed class AttitudeHistory
{
    public int SameReligionTurns { get; set; }
    public int DifferentReligionTurns { get; set; }
    public int FavoriteCivicTurns { get; set; }
    public SortedDictionary<string, int> Memories { get; set; } = new(StringComparer.Ordinal);
}
