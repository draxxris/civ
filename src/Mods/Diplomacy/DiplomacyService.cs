using Civ.Game;

namespace Civ.Mods.Diplomacy;

internal sealed partial class DiplomacyService : IDiplomacyService
{
    internal const int BindingTurns = 10;
    private static DiplomacyState Read(Simulation sim) => sim.State.Extensions.Get<DiplomacyState>(sim.Content) ?? new();
    private static DiplomacyState Edit(Simulation sim)
    {
        var state = sim.State.Extensions.Get<DiplomacyState>(sim.Content);
        if (state is not null) return state;
        state = new(); sim.State.Extensions.Set(sim.Content, state); return state;
    }
    private static string Pair(int a, int b) => Math.Min(a, b) + "/" + Math.Max(a, b);
    private static TeamRelation? Relation(Simulation sim, int a, int b) => Read(sim).Relations.GetValueOrDefault(Pair(a, b));
    private static bool Barbarian(Simulation sim, int team) => sim.State.TeamById(team).Members.Any(p =>
        sim.State.PlayerById(p).Extensions.TryGet<MilitaryPlayerState>(sim.Content)?.Barbarian == true);
    private static string? Participants(Simulation sim, int player, int target)
    {
        if (!sim.State.Players.Any(p => p.Id == player) || !sim.State.Players.Any(p => p.Id == target)) return "unknown player";
        int a = sim.TeamOf(player).Id, b = sim.TeamOf(target).Id;
        if (a == b) return "same team";
        return Barbarian(sim, a) || Barbarian(sim, b) ? "barbarians cannot negotiate" : null;
    }
    public bool HasMet(Simulation sim, int teamA, int teamB) => teamA == teamB || Relation(sim, teamA, teamB) is not null;
    public void EstablishContact(Simulation sim, int teamA, int teamB)
    {
        if (teamA == teamB || Barbarian(sim, teamA) || Barbarian(sim, teamB) || HasMet(sim, teamA, teamB)) return;
        int first = Math.Min(teamA, teamB), second = Math.Max(teamA, teamB);
        Edit(sim).Relations[Pair(first, second)] = new() { First = first, Second = second, ContactTurn = sim.State.Turn, PeaceStarted = sim.State.Turn };
        sim.Events.Publish(new TeamsMet(first, second));
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
    public bool HasOpenBorders(Simulation sim, int teamA, int teamB) => teamA == teamB ||
        (!sim.State.TeamById(teamA).Wars.Contains(teamB) && Relation(sim, teamA, teamB)?.OpenBordersStarted is not null);
    public bool HasDefensivePact(Simulation sim, int teamA, int teamB) => teamA != teamB &&
        !sim.State.TeamById(teamA).Wars.Contains(teamB) && Relation(sim, teamA, teamB)?.DefensivePactStarted is not null;
    public string? ValidateAgreement(Simulation sim, int player, int target, DiplomaticAgreement agreement)
    {
        var error = Participants(sim, player, target);
        if (error is not null) return error;
        int a = sim.TeamOf(player).Id, b = sim.TeamOf(target).Id;
        if (!Enum.IsDefined(agreement)) return "unknown agreement";
        if (!HasMet(sim, a, b)) return "teams have not met";
        if (sim.TeamOf(player).Wars.Contains(b)) return "teams are at war";
        if (agreement == DiplomaticAgreement.DefensivePact && (sim.TeamOf(player).Wars.Count > 0 || sim.TeamOf(target).Wars.Count > 0)) return "defensive pacts require peace with all teams";
        bool exists = agreement == DiplomaticAgreement.OpenBorders ? HasOpenBorders(sim, a, b) : HasDefensivePact(sim, a, b);
        return exists ? "agreement already active" : null;
    }
    public void SignAgreement(Simulation sim, int player, int target, DiplomaticAgreement agreement)
    {
        var relation = Relation(sim, sim.TeamOf(player).Id, sim.TeamOf(target).Id)!;
        if (agreement == DiplomaticAgreement.OpenBorders) relation.OpenBordersStarted = sim.State.Turn;
        else relation.DefensivePactStarted = sim.State.Turn;
    }
    private static bool BindingPeace(Simulation sim, int a, int b) => Relation(sim, a, b)?.TreatyStarted is { } signed && sim.State.Turn - signed < BindingTurns;
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
        var state = Edit(sim);
        // Offensive declarations dissolve ALL the aggressor's pacts before defending partners are collected.
        foreach (var r in state.Relations.Values.Where(r => r.First == a || r.Second == a)) r.DefensivePactStarted = null;
        var defenders = state.Relations.Values.Where(r => r.DefensivePactStarted is not null && (r.First == b || r.Second == b))
            .Select(r => r.First == b ? r.Second : r.First).Where(t => t != a).Order().ToArray();
        StartWar(sim, a, b, true);
        foreach (int defender in defenders)
        {
            // The war consumes the victim's pact with each partner, whether or not that partner can join it.
            Relation(sim, b, defender)!.DefensivePactStarted = null;
            if (!sim.State.TeamById(defender).Wars.Contains(a) && !BindingPeace(sim, defender, a)) StartWar(sim, a, defender, false);
        }
    }
    private void StartWar(Simulation sim, int aggressor, int defender, bool offensive)
    {
        EstablishContact(sim, aggressor, defender);
        var r = Relation(sim, aggressor, defender)!;
        sim.State.SetWar(aggressor, defender, true);
        r.WarStarted = sim.State.Turn; r.OpenBordersStarted = null; r.DefensivePactStarted = null;
        if (offensive)
        {
            if (aggressor == r.First) r.FirstOffensiveWars++;
            else r.SecondOffensiveWars++;
        }
        foreach (var deal in Edit(sim).Deals.Where(d => d.Status is DiplomaticDealStatus.Active or DiplomaticDealStatus.Proposed))
            if (Pair(sim.TeamOf(deal.Proposer).Id, sim.TeamOf(deal.Recipient).Id) == Pair(aggressor, defender)) deal.Status = DiplomaticDealStatus.Cancelled;
        sim.Events.Publish(new DiplomaticWarDeclared(aggressor, defender));
    }
    public string? ValidatePeace(Simulation sim, int player, int target)
    {
        var error = Participants(sim, player, target);
        if (error is not null) return error;
        int b = sim.TeamOf(target).Id;
        if (!sim.TeamOf(player).Wars.Contains(b)) return "not at war";
        int started = Relation(sim, sim.TeamOf(player).Id, b)?.WarStarted ?? 0;
        return sim.State.Turn - started < 12 ? "war is too recent" : null;
    }
    public void MakePeace(Simulation sim, int player, int target)
    {
        int a = sim.TeamOf(player).Id, b = sim.TeamOf(target).Id;
        EstablishContact(sim, a, b);
        var r = Relation(sim, a, b)!;
        sim.State.SetWar(a, b, false);
        r.WarStarted = null; r.PeaceStarted = sim.State.Turn; r.TreatyStarted = sim.State.Turn;
        sim.Events.Publish(new DiplomaticPeaceSigned(a, b));
    }
}
