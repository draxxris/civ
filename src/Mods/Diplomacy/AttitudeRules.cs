using Civ.Content;
using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.Diplomacy;

internal sealed partial class DiplomacyService
{
    private static S.LeaderDef? Leader(Simulation sim, int player) => sim.Content.TryGet<S.LeaderDef>(sim.State.PlayerById(player).Leader, out var leader) ? leader : null;
    private static string? Religion(Simulation sim, int player) => sim.Content.Services.Get<IReligionService>().SingleOrDefault()?.StateReligion(sim, player);
    private static int Growth(int turns, int divisor, int limit) => divisor <= 0 ? 0 : Math.Sign(limit) * Math.Min(Math.Abs(limit), Math.Max(0, turns) / divisor);
    private static bool FavoriteCivic(Simulation sim, int from, int toward, S.LeaderDef leader) => leader.FavoriteCivic is { } civic &&
        sim.State.PlayerById(from).Civics.Values.Contains(civic) && sim.State.PlayerById(toward).Civics.Values.Contains(civic);
    private static string Direction(int from, int toward) => from + "/" + toward;
    private static bool CloseBorders(Simulation sim, int teamA, int teamB) => sim.State.Grid.Tiles.Any(tile =>
        sim.State.Plots[tile.Value].Owner is { } owner && sim.TeamOf(owner).Id == teamA && sim.State.Grid.Neighbors(tile).Any(n =>
            sim.State.Plots[n.Value].Owner is { } other && sim.TeamOf(other).Id == teamB));
    public AttitudeResult GetAttitude(Simulation sim, int fromPlayer, int towardPlayer)
    {
        var sink = new ModifierSink();
        var leader = Leader(sim, fromPlayer);
        if (leader is null) return new(0, AttitudeTier.Cautious, sink.Ledger);
        int a = sim.TeamOf(fromPlayer).Id, b = sim.TeamOf(towardPlayer).Id;
        var relation = Relation(sim, a, b);
        var history = Read(sim).Histories.GetValueOrDefault(Direction(fromPlayer, towardPlayer)) ?? new();
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
        foreach (var memory in history.Memories)
        {
            string key = memory.Key.StartsWith("memory_", StringComparison.Ordinal) ? memory.Key[7..] : memory.Key;
            Add(memory.Key, (long)memory.Value * leader.MemoryAttitudePercents.GetValueOrDefault(key) / 100);
        }
        int score = checked((int)sink.Ledger.Total("diplomacy:attitude"));
        return new(score, Tier(score), sink.Ledger);
    }
    public void RecordMemory(Simulation sim, int fromPlayer, int towardPlayer, string memory)
    {
        var state = Edit(sim);
        string direction = Direction(fromPlayer, towardPlayer);
        if (!state.Histories.TryGetValue(direction, out var history)) state.Histories[direction] = history = new();
        history.Memories[memory] = history.Memories.GetValueOrDefault(memory) + 1;
    }
    private static AttitudeTier Tier(int score) => score switch
    {
        <= -10 => AttitudeTier.Furious, <= -3 => AttitudeTier.Annoyed,
        >= 10 => AttitudeTier.Friendly, >= 3 => AttitudeTier.Pleased, _ => AttitudeTier.Cautious,
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
        int offensiveWars = b == relation.First ? relation.FirstOffensiveWars : relation.SecondOffensiveWars;
        add("war_history", -3L * offensiveWars);
        if (HasOpenBorders(sim, a, b)) add("open_borders", Growth(sim.State.Turn - relation.OpenBordersStarted!.Value, leader.OpenBordersAttitudeDivisor, leader.OpenBordersAttitudeChangeLimit));
        if (HasDefensivePact(sim, a, b)) add("defensive_pact", Growth(sim.State.Turn - relation.DefensivePactStarted!.Value, leader.DefensivePactAttitudeDivisor, leader.DefensivePactAttitudeChangeLimit));
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
            var civic = sim.Content.Get<S.CivicDef>(id);
            add("civic:" + id, civic.GlobalRelationsChange);
            foreach (var effect in civic.CivicAttitudeChanges.OrderBy(p => p.Key, StringComparer.Ordinal))
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
                string key = Direction(from.Id, toward.Id);
                if (!state.Histories.TryGetValue(key, out var history)) state.Histories[key] = history = new();
                string? mine = Religion(sim, from.Id), theirs = Religion(sim, toward.Id);
                history.SameReligionTurns = mine is not null && mine == theirs ? history.SameReligionTurns + 1 : 0;
                history.DifferentReligionTurns = mine is not null && theirs is not null && mine != theirs ? history.DifferentReligionTurns + 1 : 0;
                var leader = Leader(sim, from.Id);
                history.FavoriteCivicTurns = leader is not null && FavoriteCivic(sim, from.Id, toward.Id, leader) ? history.FavoriteCivicTurns + 1 : 0;
            }
        state.LastHistoryTurn = sim.State.Turn;
    }
}
