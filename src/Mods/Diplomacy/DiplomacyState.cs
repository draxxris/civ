using Ariel.Game;

namespace Ariel.Mods.Diplomacy;

/// <summary>Agreements, deals and the counters attitudes grow with. Contact, war and peace, and memories are the base
/// game's (<see cref="RelationsState"/>). No derived caches or RNG are stored here.</summary>
public sealed class DiplomacyState
{
    /// <summary>Agreements by <see cref="TeamPair.Key"/>. A war between the teams ends them.</summary>
    public SortedDictionary<string, TeamAgreement> Agreements { get; set; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, AttitudeHistory> Histories { get; set; } = new(StringComparer.Ordinal);
    public List<DiplomaticDeal> Deals { get; set; } = [];
    public int NextDealId { get; set; } = 1;
    public int LastHistoryTurn { get; set; }

    public TeamAgreement? GetAgreement(int teamA, int teamB) => Agreements.GetValueOrDefault(TeamPair.Of(teamA, teamB).Key);
    public TeamAgreement GetOrCreateAgreement(int teamA, int teamB)
    {
        string key = TeamPair.Of(teamA, teamB).Key;
        if (!Agreements.TryGetValue(key, out var agreement)) Agreements[key] = agreement = new();
        return agreement;
    }

    public AttitudeHistory? GetHistory(int fromPlayer, int towardPlayer) => Histories.GetValueOrDefault(PlayerDirection.Of(fromPlayer, towardPlayer).Key);
    public AttitudeHistory GetOrCreateHistory(int fromPlayer, int towardPlayer)
    {
        string key = PlayerDirection.Of(fromPlayer, towardPlayer).Key;
        if (!Histories.TryGetValue(key, out var history)) Histories[key] = history = new();
        return history;
    }
}

/// <summary>The agreements two teams have signed, each with the turn it began, or null when not in force.</summary>
public sealed class TeamAgreement
{
    public int? OpenBordersStarted { get; set; }
    public int? DefensivePactStarted { get; set; }

    public int? Started(DiplomaticAgreement agreement) =>
        agreement == DiplomaticAgreement.OpenBorders ? OpenBordersStarted : DefensivePactStarted;

    public void Sign(DiplomaticAgreement agreement, int turn)
    {
        if (agreement == DiplomaticAgreement.OpenBorders) OpenBordersStarted = turn;
        else if (agreement == DiplomaticAgreement.DefensivePact) DefensivePactStarted = turn;
    }

    public void Cancel(DiplomaticAgreement agreement)
    {
        if (agreement == DiplomaticAgreement.OpenBorders) OpenBordersStarted = null;
        else if (agreement == DiplomaticAgreement.DefensivePact) DefensivePactStarted = null;
    }
}

/// <summary>How long one player has shared a religion or a favourite civic with another.</summary>
public sealed class AttitudeHistory
{
    public int SameReligionTurns { get; set; }
    public int DifferentReligionTurns { get; set; }
    public int FavoriteCivicTurns { get; set; }
}
