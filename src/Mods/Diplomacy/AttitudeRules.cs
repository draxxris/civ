using Ariel.Content;
using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Diplomacy;

internal sealed partial class DiplomacyService
{
    private static S.LeaderDef? Leader(Simulation sim, int player) => sim.Content.TryGet<S.LeaderDef>(sim.State.PlayerById(player).Leader, out var leader) ? leader : null;
    private static string? Religion(Simulation sim, int player) => sim.Rules().Religion?.StateReligion(sim, player);
    private static int Growth(int turns, int divisor, int limit) => divisor <= 0 ? 0 : Math.Sign(limit) * Math.Min(Math.Abs(limit), Math.Max(0, turns) / divisor);
    private static bool FavoriteCivic(Simulation sim, int from, int toward, S.LeaderDef leader) => leader.FavoriteCivic is { } civic &&
        sim.State.PlayerById(from).Civics.Values.Contains(civic) && sim.State.PlayerById(toward).Civics.Values.Contains(civic);
    private static PlayerDirection Direction(int from, int toward) => PlayerDirection.Of(from, toward);
    private static bool CloseBorders(Simulation sim, int teamA, int teamB) => sim.State.Grid.Tiles.Any(tile =>
        sim.State.Plots[tile.Value].Owner is { } owner && sim.TeamOf(owner).Id == teamA && sim.State.Grid.Neighbors(tile).Any(n =>
            sim.State.Plots[n.Value].Owner is { } other && sim.TeamOf(other).Id == teamB));
    public AttitudeResult GetAttitude(Simulation sim, int fromPlayer, int towardPlayer)
    {
        var sink = new ModifierSink();
        var leader = Leader(sim, fromPlayer);
        if (leader is null) return new(0, Disposition.Cautious, sink.Ledger);
        int a = sim.TeamOf(fromPlayer).Id, b = sim.TeamOf(towardPlayer).Id;
        var relation = Relations(sim).Relation(sim, a, b);
        var history = Read(sim).GetHistory(fromPlayer, towardPlayer) ?? new();
        void Add(string reason, long amount) => sink.Add("diplomacy:" + reason, "diplomacy:attitude", amount);
        Add("base", leader.BaseAttitude);
        AddPersonality(leader, Leader(sim, towardPlayer), Add);
        AddReligion(sim, fromPlayer, towardPlayer, leader, history, Add);
        if (FavoriteCivic(sim, fromPlayer, towardPlayer, leader))
            Add("favorite_civic", leader.FavoriteCivicAttitudeChange + Growth(history.FavoriteCivicTurns, leader.FavoriteCivicAttitudeDivisor, leader.FavoriteCivicAttitudeChangeLimit));
        if (a != b && CloseBorders(sim, a, b)) Add("close_borders", leader.CloseBordersAttitudeChange);
        AddRelations(sim, a, b, leader, relation, Add);
        int tradeTurns = Read(sim).Deals.Where(d => Live(sim, d) && (d.Proposer == fromPlayer && d.Recipient == towardPlayer || d.Proposer == towardPlayer && d.Recipient == fromPlayer))
            .Select(d => sim.State.Turn - d.StartedTurn).DefaultIfEmpty().Max();
        Add("active_deals", Growth(tradeTurns, leader.BonusTradeAttitudeDivisor, leader.BonusTradeAttitudeChangeLimit));
        AddCivics(sim, fromPlayer, towardPlayer, Add);
        foreach (var memory in Relations(sim).Memories(sim, fromPlayer, towardPlayer))
            Add("memory:" + memory.Key, (long)memory.Value * MemoryPercent(sim, leader, memory.Key) / 100);
        int score = checked((int)sink.Ledger.Total("diplomacy:attitude"));
        return new(score, Tier(score), sink.Ledger);
    }
    /// <summary>Attitude per remembered count: the leader's own weight for the kind, else the kind's default.</summary>
    private static int MemoryPercent(Simulation sim, S.LeaderDef leader, string kind)
    {
        var def = sim.Content.Get<S.MemoryKindDef>(kind);
        return leader.MemoryAttitudePercents.TryGetValue(def.LeaderKey, out int percent) ? percent : def.AttitudePercent;
    }
    private static Disposition Tier(int score) => score switch
    {
        <= -10 => Disposition.Furious, <= -3 => Disposition.Annoyed,
        >= 10 => Disposition.Friendly, >= 3 => Disposition.Pleased, _ => Disposition.Cautious,
    };
    private static void AddPersonality(S.LeaderDef leader, S.LeaderDef? other, Action<string, long> add)
    {
        if (other is null) return;
        add("peace_weight", 4 - Math.Abs(leader.BasePeaceWeight - other.BasePeaceWeight));
        add("warmonger_respect", Math.Min(leader.WarmongerRespect, other.WarmongerRespect));
    }
    private void AddRelations(Simulation sim, int a, int b, S.LeaderDef leader, TeamRelation? relation, Action<string, long> add)
    {
        if (relation is null) return;
        bool war = sim.State.TeamById(a).Wars.Contains(b);
        if (war) add("war", -3 + Growth(sim.State.Turn - (relation.WarStarted ?? sim.State.Turn), leader.AtWarAttitudeDivisor, leader.AtWarAttitudeChangeLimit));
        else add("years_of_peace", Growth(sim.State.Turn - relation.PeaceStarted, leader.AtPeaceAttitudeDivisor, leader.AtPeaceAttitudeChangeLimit));
        int offensiveWars = relation.OffensiveWarsBy(b);
        add("war_history", -3L * offensiveWars);
        var agreement = Agreement(sim, a, b);
        if (HasOpenBorders(sim, a, b)) add("open_borders", Growth(sim.State.Turn - agreement!.OpenBordersStarted!.Value, leader.OpenBordersAttitudeDivisor, leader.OpenBordersAttitudeChangeLimit));
        if (HasDefensivePact(sim, a, b)) add("defensive_pact", Growth(sim.State.Turn - agreement!.DefensivePactStarted!.Value, leader.DefensivePactAttitudeDivisor, leader.DefensivePactAttitudeChangeLimit));
    }
    private static void AddReligion(Simulation sim, int from, int toward, S.LeaderDef leader, AttitudeHistory history, Action<string, long> add)
    {
        string? mine = Religion(sim, from), theirs = Religion(sim, toward);
        if (mine is null || theirs is null) return;
        if (mine == theirs) add("same_religion", leader.SameReligionAttitudeChange + Growth(history.SameReligionTurns, leader.SameReligionAttitudeDivisor, leader.SameReligionAttitudeChangeLimit));
        else add("different_religion", leader.DifferentReligionAttitudeChange + Growth(history.DifferentReligionTurns, leader.DifferentReligionAttitudeDivisor, leader.DifferentReligionAttitudeChangeLimit));
    }
    private static void AddCivics(Simulation sim, int from, int toward, Action<string, long> add)
    {
        var theirs = sim.State.PlayerById(toward).Civics.Values.ToHashSet(StringComparer.Ordinal);
        foreach (var id in sim.State.PlayerById(from).Civics.Values.Order(StringComparer.Ordinal))
        {
            if (sim.Content.Attachment<CivicDiplomacyDef>(id) is not { } civic) continue;
            add("civic:" + id, civic.GlobalRelationsChange);
            foreach (var effect in civic.AttitudeChanges.OrderBy(p => p.Key, StringComparer.Ordinal))
                if (theirs.Contains(effect.Key)) add("civic_pair:" + id + ":" + effect.Key, effect.Value.Change);
        }
    }
    internal void UpdateHistory(Simulation sim)
    {
        var state = Edit(sim);
        if (state.LastHistoryTurn == sim.State.Turn) return;
        foreach (var from in sim.State.Players.OrderBy(p => p.Id))
            foreach (var toward in sim.State.Players.Where(p => p.TeamId != from.TeamId).OrderBy(p => p.Id))
            {
                if (!HasMet(sim, from.TeamId, toward.TeamId)) continue;
                var history = state.GetOrCreateHistory(from.Id, toward.Id);
                string? mine = Religion(sim, from.Id), theirs = Religion(sim, toward.Id);
                history.SameReligionTurns = mine is not null && mine == theirs ? history.SameReligionTurns + 1 : 0;
                history.DifferentReligionTurns = mine is not null && theirs is not null && mine != theirs ? history.DifferentReligionTurns + 1 : 0;
                var leader = Leader(sim, from.Id);
                history.FavoriteCivicTurns = leader is not null && FavoriteCivic(sim, from.Id, toward.Id, leader) ? history.FavoriteCivicTurns + 1 : 0;
            }
        state.LastHistoryTurn = sim.State.Turn;
    }
}
