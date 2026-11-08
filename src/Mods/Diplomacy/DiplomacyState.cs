using System.Text.Json.Serialization;
using Ariel.Game;

namespace Ariel.Mods.Diplomacy;

/// <summary>Authoritative contracts and counters. No derived caches or RNG are stored here.</summary>
public sealed class DiplomacyState
{
    public SortedDictionary<string, TeamRelation> Relations { get; set; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, AttitudeHistory> Histories { get; set; } = new(StringComparer.Ordinal);
    public List<DiplomaticDeal> Deals { get; set; } = [];
    public int NextDealId { get; set; } = 1;
    public int LastHistoryTurn { get; set; }

    public TeamRelation? GetRelation(TeamPair pair) => Relations.GetValueOrDefault(pair.Key);
    public TeamRelation? GetRelation(int teamA, int teamB) => GetRelation(TeamPair.Of(teamA, teamB));
    public void SetRelation(TeamPair pair, TeamRelation relation) => Relations[pair.Key] = relation;
    public void SetRelation(int teamA, int teamB, TeamRelation relation) => SetRelation(TeamPair.Of(teamA, teamB), relation);
    public bool HasRelation(TeamPair pair) => Relations.ContainsKey(pair.Key);
    public bool HasRelation(int teamA, int teamB) => HasRelation(TeamPair.Of(teamA, teamB));

    public AttitudeHistory? GetHistory(PlayerDirection direction) => Histories.GetValueOrDefault(direction.Key);
    public AttitudeHistory? GetHistory(int fromPlayer, int towardPlayer) => GetHistory(PlayerDirection.Of(fromPlayer, towardPlayer));
    public AttitudeHistory GetOrCreateHistory(PlayerDirection direction)
    {
        if (!Histories.TryGetValue(direction.Key, out var history))
        {
            history = new AttitudeHistory();
            Histories[direction.Key] = history;
        }
        return history;
    }
    public AttitudeHistory GetOrCreateHistory(int fromPlayer, int towardPlayer) => GetOrCreateHistory(PlayerDirection.Of(fromPlayer, towardPlayer));
    public bool HasHistory(PlayerDirection direction) => Histories.ContainsKey(direction.Key);
    public bool HasHistory(int fromPlayer, int towardPlayer) => HasHistory(PlayerDirection.Of(fromPlayer, towardPlayer));
}
public sealed class TeamRelation
{
    public int First { get; set; }
    public int Second { get; set; }
    public int ContactTurn { get; set; }
    public int PeaceStarted { get; set; }
    public int? TreatyStarted { get; set; }
    private int? _warStarted;
    public int? WarStarted
    {
        get => _warStarted;
        set
        {
            _warStarted = value;
            if (value is not null)
            {
                _openBordersStarted = null;
                _defensivePactStarted = null;
            }
        }
    }
    public int FirstOffensiveWars { get; set; }
    public int SecondOffensiveWars { get; set; }
    private int? _openBordersStarted;
    public int? OpenBordersStarted
    {
        get => _warStarted is null ? _openBordersStarted : null;
        set => _openBordersStarted = _warStarted is null ? value : null;
    }
    private int? _defensivePactStarted;
    public int? DefensivePactStarted
    {
        get => _warStarted is null ? _defensivePactStarted : null;
        set => _defensivePactStarted = _warStarted is null ? value : null;
    }

    [JsonIgnore]
    public bool AtWar => _warStarted is not null;

    [JsonIgnore]
    public bool HasOpenBorders => _warStarted is null && _openBordersStarted is not null;

    [JsonIgnore]
    public bool HasDefensivePact => _warStarted is null && _defensivePactStarted is not null;

    public bool HasBindingPeace(int currentTurn, int bindingTurns = 10) =>
        _warStarted is null && TreatyStarted is { } signed && currentTurn - signed < bindingTurns;

    public int OffensiveWarsBy(int team) =>
        team == First ? FirstOffensiveWars : team == Second ? SecondOffensiveWars : 0;

    public void RecordOffensiveWar(int aggressor)
    {
        if (aggressor == First) FirstOffensiveWars++;
        else if (aggressor == Second) SecondOffensiveWars++;
    }

    public void StartWar(int turn, int aggressor, bool offensive)
    {
        WarStarted = turn;
        if (offensive) RecordOffensiveWar(aggressor);
    }

    public void MakePeace(int turn)
    {
        _warStarted = null;
        PeaceStarted = turn;
        TreatyStarted = turn;
    }

    public void SignPact(DiplomaticAgreement agreement, int turn)
    {
        if (_warStarted is not null) return;
        if (agreement == DiplomaticAgreement.OpenBorders) _openBordersStarted = turn;
        else if (agreement == DiplomaticAgreement.DefensivePact) _defensivePactStarted = turn;
    }

    public void CancelPact(DiplomaticAgreement agreement)
    {
        if (agreement == DiplomaticAgreement.OpenBorders) _openBordersStarted = null;
        else if (agreement == DiplomaticAgreement.DefensivePact) _defensivePactStarted = null;
    }

    public void CancelAllPacts()
    {
        _openBordersStarted = null;
        _defensivePactStarted = null;
    }
}
public sealed class AttitudeHistory
{
    public int SameReligionTurns { get; set; }
    public int DifferentReligionTurns { get; set; }
    public int FavoriteCivicTurns { get; set; }
    public SortedDictionary<string, int> Memories { get; set; } = new(StringComparer.Ordinal);

    public int GetMemory(string memory) => Memories.GetValueOrDefault(memory);
    public void RecordMemory(string memory, int count = 1) =>
        Memories[memory] = Memories.GetValueOrDefault(memory) + count;
    public bool HasMemory(string memory) => Memories.ContainsKey(memory);
}
