using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Rules.Relations;

internal sealed class RelationsService : IRelationsService
{
    private static readonly IReadOnlyDictionary<string, int> Nothing = new SortedDictionary<string, int>(StringComparer.Ordinal);

    internal static RelationsState Read(Simulation sim) => sim.State.Extensions.Get<RelationsState>(sim.Content) ?? new();
    private static RelationsState Edit(Simulation sim)
    {
        var state = sim.State.Extensions.Get<RelationsState>(sim.Content);
        if (state is not null) return state;
        state = new(); sim.State.Extensions.Set(sim.Content, state); return state;
    }
    private static bool Barbarian(Simulation sim, int team) => sim.State.TeamById(team).Members.Any(p => PlayerRules.IsBarbarian(sim, p));
    private static IEnumerable<int> Members(Simulation sim, int team) =>
        sim.State.Players.Where(p => p.TeamId == team).OrderBy(p => p.Id).Select(p => p.Id);

    public TeamRelation? Relation(Simulation sim, int teamA, int teamB) => Read(sim).GetRelation(teamA, teamB);
    public bool HasMet(Simulation sim, int teamA, int teamB) => teamA == teamB || Relation(sim, teamA, teamB) is not null;

    public void EstablishContact(Simulation sim, int teamA, int teamB)
    {
        if (teamA == teamB || Barbarian(sim, teamA) || Barbarian(sim, teamB) || HasMet(sim, teamA, teamB)) return;
        var pair = TeamPair.Of(teamA, teamB);
        Edit(sim).Relations[pair.Key] = new() { First = pair.First, Second = pair.Second, ContactTurn = sim.State.Turn, PeaceStarted = sim.State.Turn };
        sim.Events.Publish(new TeamsMet(pair.First, pair.Second));
    }

    public void DiscoverContacts(Simulation sim)
    {
        // Current sight, not revealed history. Occupants and owned borders both count as contact targets.
        foreach (var tile in sim.State.Grid.Tiles)
        {
            var plot = sim.State.Plots[tile.Value];
            var occupants = new SortedSet<int>(sim.State.UnitsAt(sim.State.Grid.HexOf(tile)).Select(u => sim.TeamOf(u.Owner).Id));
            if (plot.Owner is { } owner) occupants.Add(sim.TeamOf(owner).Id);
            var observers = new SortedSet<int>(occupants);
            foreach (var team in sim.State.Teams.Where(t => plot.Visible.Contains(t.Id))) observers.Add(team.Id);
            foreach (var neighbor in sim.State.Grid.Neighbors(tile))
            {
                var adjacent = sim.State.Plots[neighbor.Value];
                if (adjacent.Owner is { } p) observers.Add(sim.TeamOf(p).Id);
                foreach (var unit in sim.State.UnitsAt(sim.State.Grid.HexOf(neighbor))) observers.Add(sim.TeamOf(unit.Owner).Id);
            }
            foreach (int target in occupants)
                foreach (int observer in observers) EstablishContact(sim, observer, target);
        }
    }

    private static string? Participants(Simulation sim, int player, int target)
    {
        if (!sim.State.Players.Any(p => p.Id == player) || !sim.State.Players.Any(p => p.Id == target)) return "unknown player";
        int a = sim.TeamOf(player).Id, b = sim.TeamOf(target).Id;
        if (a == b) return "same team";
        return Barbarian(sim, a) || Barbarian(sim, b) ? "barbarians are always at war" : null;
    }
    private bool BindingPeace(Simulation sim, int a, int b) => Relation(sim, a, b)?.HasBindingPeace(sim.State.Turn) == true;

    public string? ValidateWar(Simulation sim, int player, int target)
    {
        var error = Participants(sim, player, target);
        if (error is not null) return error;
        int a = sim.TeamOf(player).Id, b = sim.TeamOf(target).Id;
        if (!HasMet(sim, a, b)) return "teams have not met";
        if (sim.TeamOf(player).Wars.Contains(b)) return "already at war";
        return BindingPeace(sim, a, b) ? "peace treaty is too recent" : null;
    }

    public void DeclareWar(Simulation sim, int player, int target)
    {
        int a = sim.TeamOf(player).Id, b = sim.TeamOf(target).Id;
        // Read before anything changes: the war itself, and the agreements it breaks, change how players regard each other.
        int[] friends = FriendsOf(sim, a, b);
        var allies = sim.Rules().TeamAgreements?.CallToArms(sim, a, b) ?? [];
        StartWar(sim, a, b, true);
        RememberWar(sim, a, b, friends);
        foreach (int ally in allies)
            if (ally != a && !sim.State.TeamById(ally).Wars.Contains(a) && !BindingPeace(sim, ally, a)) StartWar(sim, a, ally, false);
    }

    private void StartWar(Simulation sim, int aggressor, int defender, bool offensive)
    {
        EstablishContact(sim, aggressor, defender);
        var relation = Relation(sim, aggressor, defender)!;
        sim.State.SetWar(aggressor, defender, true);
        relation.StartWar(sim.State.Turn, aggressor, offensive);
        sim.Events.Publish(new WarDeclared(aggressor, defender, offensive));
    }

    public string? ValidatePeace(Simulation sim, int player, int target)
    {
        var error = Participants(sim, player, target);
        if (error is not null) return error;
        int a = sim.TeamOf(player).Id, b = sim.TeamOf(target).Id;
        if (!sim.TeamOf(player).Wars.Contains(b)) return "not at war";
        int started = Relation(sim, a, b)?.WarStarted ?? 0;
        return sim.State.Turn - started < RelationRules.MinimumWarTurns ? "war is too recent" : null;
    }

    public void MakePeace(Simulation sim, int player, int target)
    {
        int a = sim.TeamOf(player).Id, b = sim.TeamOf(target).Id;
        EstablishContact(sim, a, b);
        var relation = Relation(sim, a, b)!;
        sim.State.SetWar(a, b, false);
        relation.MakePeace(sim.State.Turn);
        sim.Events.Publish(new PeaceSigned(a, b));
    }

    public bool MayEnter(Simulation sim, int team, int ownerTeam) => team == ownerTeam
        || (!sim.State.TeamById(team).Wars.Contains(ownerTeam) && sim.Rules().TeamAgreements?.OpenBorders(sim, team, ownerTeam) == true);

    public bool Allied(Simulation sim, int teamA, int teamB) => sim.Rules().TeamAgreements?.Allied(sim, teamA, teamB) == true;

    public Disposition DispositionToward(Simulation sim, int fromPlayer, int towardPlayer) =>
        sim.Rules().Dispositions?.Toward(sim, fromPlayer, towardPlayer) ?? Disposition.Cautious;

    /// <summary>Players outside both teams who have met the aggressor and are at least Pleased with someone on the
    /// defending team. Nobody is anybody's friend without a mod that judges dispositions.</summary>
    private int[] FriendsOf(Simulation sim, int aggressor, int defender)
    {
        if (sim.Rules().Dispositions is not { } dispositions) return [];
        return sim.State.Players
            .Where(p => p.TeamId != aggressor && p.TeamId != defender && HasMet(sim, p.TeamId, aggressor)
                && Members(sim, defender).Any(d => dispositions.Toward(sim, p.Id, d) >= Disposition.Pleased))
            .OrderBy(p => p.Id).Select(p => p.Id).ToArray();
    }

    private void RememberWar(Simulation sim, int aggressor, int defender, int[] friends)
    {
        foreach (int attacker in Members(sim, aggressor))
        {
            foreach (int victim in Members(sim, defender)) Remember(sim, victim, attacker, BaseMemories.DeclaredWar);
            foreach (int friend in friends) Remember(sim, friend, attacker, BaseMemories.DeclaredWarOnFriend);
        }
    }

    /// <summary>The player it was captured from remembers a razed city.</summary>
    internal static void RememberRaze(Simulation sim, CityRazed razed)
    {
        if (razed.CapturedFrom is { } victim && sim.State.Players.Any(p => p.Id == victim))
            sim.Rules().Relations.Remember(sim, victim, razed.Player, BaseMemories.RazedCity);
    }

    public void Remember(Simulation sim, int fromPlayer, int towardPlayer, string kind)
    {
        if (!sim.Content.TryGet<MemoryKindDef>(kind, out var def))
            throw new InvalidOperationException($"'{kind}' is not a registered memory kind");
        if (sim.TeamOf(fromPlayer).Id == sim.TeamOf(towardPlayer).Id
            || PlayerRules.IsBarbarian(sim, fromPlayer) || PlayerRules.IsBarbarian(sim, towardPlayer)) return;
        var memories = Edit(sim).Memories;
        string key = PlayerDirection.Of(fromPlayer, towardPlayer).Key;
        if (!memories.TryGetValue(key, out var ledger)) memories[key] = ledger = new(StringComparer.Ordinal);
        int count = ledger.GetValueOrDefault(kind);
        if (def.Limit > 0 && count >= def.Limit) return;
        ledger[kind] = count + 1;
    }

    public IReadOnlyDictionary<string, int> Memories(Simulation sim, int fromPlayer, int towardPlayer) =>
        Read(sim).GetMemories(fromPlayer, towardPlayer) ?? Nothing;

    /// <summary>Each remembered kind loses one count with a chance of one in its decay, rolled in player order and then
    /// in ordinal order of the kind's leader-table name; a kind at zero is forgotten. Players who remember nothing draw
    /// nothing, and memories fade once a turn, never on turn 0.</summary>
    internal static void FadeMemories(Simulation sim)
    {
        var state = Edit(sim);
        if (state.LastFadeTurn == sim.State.Turn) return;
        foreach (var from in sim.State.Players.OrderBy(p => p.Id))
        {
            var leader = sim.Content.TryGet<LeaderDef>(from.Leader, out var own) ? own : null;
            foreach (var toward in sim.State.Players.OrderBy(p => p.Id))
            {
                if (state.GetMemories(from.Id, toward.Id) is not { Count: > 0 } ledger) continue;
                var kinds = ledger.Keys.Select(sim.Content.Get<MemoryKindDef>)
                    .OrderBy(k => k.LeaderKey, StringComparer.Ordinal).ThenBy(k => k.Id, StringComparer.Ordinal).ToArray();
                foreach (var kind in kinds)
                {
                    int decay = leader?.MemoryDecays.GetValueOrDefault(kind.LeaderKey) is int days and > 0 ? days : kind.Decay;
                    if (sim.Rng.Next(GamePace.Scale(sim, decay)) != 0) continue;
                    if (ledger[kind.Id] <= 1) ledger.Remove(kind.Id);
                    else ledger[kind.Id]--;
                }
            }
        }
        state.LastFadeTurn = sim.State.Turn;
    }
}
