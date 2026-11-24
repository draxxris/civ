using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Religion;

/// <summary>How long one player has shared its state religion with another, or held a different one, by
/// <see cref="PlayerDirection.Key"/>. A pair whose counters are both zero has no entry.</summary>
public sealed class ReligionRegardState
{
    public SortedDictionary<string, FaithRegard> Pairs { get; set; } = new(StringComparer.Ordinal);
    /// <summary>The turn the counters last grew, so a turn grows them once.</summary>
    public int LastTurn { get; set; }
}
/// <summary>Consecutive turns two players' state religions have been the same, or different.</summary>
public sealed class FaithRegard
{
    public int SameTurns { get; set; }
    public int DifferentTurns { get; set; }
}

/// <summary>A leader regards a player of its own state religion better and one of another worse, more so the longer it
/// lasts (each leader's same and different religion attitude, with its growth divisor and limit). Players without a
/// state religion have no such reason.</summary>
internal sealed class FaithAttitude : IAttitudeSource
{
    internal const string Same = "religion:same_religion", Different = "religion:different_religion";

    public void Add(Simulation sim, int fromPlayer, int towardPlayer, LeaderDef leader, Action<string, long> add)
    {
        string? mine = ReligionData.StateReligion(sim, fromPlayer), theirs = ReligionData.StateReligion(sim, towardPlayer);
        if (mine is null || theirs is null) return;
        var regard = sim.State.Extensions.Get<ReligionRegardState>(sim.Content)?.Pairs.GetValueOrDefault(PlayerDirection.Of(fromPlayer, towardPlayer).Key);
        if (mine == theirs)
            add(Same, leader.SameReligionAttitudeChange
                + AttitudeGrowth.Of(regard?.SameTurns ?? 0, leader.SameReligionAttitudeDivisor, leader.SameReligionAttitudeChangeLimit));
        else
            add(Different, leader.DifferentReligionAttitudeChange
                + AttitudeGrowth.Of(regard?.DifferentTurns ?? 0, leader.DifferentReligionAttitudeDivisor, leader.DifferentReligionAttitudeChangeLimit));
    }

    /// <summary>Grows every met pair's counters once a turn, after the turn's contacts are made
    /// (<c>relations:turn</c>, before <see cref="TurnPhase.GameEnd"/>).</summary>
    internal static void Grow(Simulation sim)
    {
        var current = sim.State.Extensions.Get<ReligionRegardState>(sim.Content);
        if (current?.LastTurn == sim.State.Turn) return;
        var state = current ?? new();
        var relations = sim.Rules().Relations;
        foreach (var from in sim.State.Players.OrderBy(p => p.Id))
        {
            string? mine = ReligionData.StateReligion(sim, from.Id);
            foreach (var toward in sim.State.Players.Where(p => p.TeamId != from.TeamId).OrderBy(p => p.Id))
            {
                if (!relations.HasMet(sim, from.TeamId, toward.TeamId)) continue;
                string key = PlayerDirection.Of(from.Id, toward.Id).Key;
                string? theirs = ReligionData.StateReligion(sim, toward.Id);
                var regard = state.Pairs.GetValueOrDefault(key);
                int same = mine is not null && mine == theirs ? (regard?.SameTurns ?? 0) + 1 : 0;
                int different = mine is not null && theirs is not null && mine != theirs ? (regard?.DifferentTurns ?? 0) + 1 : 0;
                if (same == 0 && different == 0) state.Pairs.Remove(key);
                else state.Pairs[key] = new() { SameTurns = same, DifferentTurns = different };
            }
        }
        state.LastTurn = sim.State.Turn;
        if (current is null) sim.State.Extensions.Set(sim.Content, state);
    }
}

internal sealed class FaithAttitudeTurns : ITurnSystem
{
    public string Id => "religion:attitudes";
    public int Order => TurnPhase.GameEnd;
    public void Run(Simulation sim) => FaithAttitude.Grow(sim);
}

/// <summary>Names the religion reasons in an attitude.</summary>
internal sealed class FaithAttitudeLabels : ILedgerSourceLabel
{
    public string? Label(Simulation sim, string source) => source switch
    {
        FaithAttitude.Same => "Shared religion",
        FaithAttitude.Different => "Different religion",
        _ => null,
    };
}
