using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Diplomacy;

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
    private static TeamPair Pair(int a, int b) => TeamPair.Of(a, b);
    private static TeamRelation? Relation(Simulation sim, int a, int b) => Read(sim).GetRelation(a, b);
    private static bool Barbarian(Simulation sim, int team) => sim.State.TeamById(team).Members.Any(p => PlayerRules.IsBarbarian(sim, p));
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
        var pair = Pair(teamA, teamB);
        Edit(sim).SetRelation(pair, new() { First = pair.First, Second = pair.Second, ContactTurn = sim.State.Turn, PeaceStarted = sim.State.Turn });
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
    public bool HasOpenBorders(Simulation sim, int teamA, int teamB) => teamA == teamB ||
        (!sim.State.TeamById(teamA).Wars.Contains(teamB) && Relation(sim, teamA, teamB)?.HasOpenBorders == true);
    public bool HasDefensivePact(Simulation sim, int teamA, int teamB) => teamA != teamB &&
        !sim.State.TeamById(teamA).Wars.Contains(teamB) && Relation(sim, teamA, teamB)?.HasDefensivePact == true;
    public string? ValidateAgreement(Simulation sim, int player, int target, DiplomaticAgreement agreement)
    {
        var error = Participants(sim, player, target);
        if (error is not null) return error;
        int a = sim.TeamOf(player).Id, b = sim.TeamOf(target).Id;
        if (!Enum.IsDefined(agreement)) return "unknown agreement";
        if (!HasMet(sim, a, b)) return "teams have not met";
        if (sim.TeamOf(player).Wars.Contains(b)) return "teams are at war";
        if (agreement == DiplomaticAgreement.DefensivePact && (sim.TeamOf(player).Wars.Count > 0 || sim.TeamOf(target).Wars.Count > 0)) return "defensive pacts require peace with all teams";
        var tech = agreement == DiplomaticAgreement.OpenBorders ? TradeTech(sim, a, b, t => t.OpenBordersTrading, "open borders") : TradeTech(sim, a, b, t => t.DefensivePactTrading, "defensive pacts");
        if (tech is not null) return tech;
        bool exists = agreement == DiplomaticAgreement.OpenBorders ? HasOpenBorders(sim, a, b) : HasDefensivePact(sim, a, b);
        return exists ? "agreement already active" : null;
    }
    /// <summary>Either participant can supply the unlock. Content without an enabling flag has no technology gate.</summary>
    internal static string? TradeTech(Simulation sim, int teamA, int teamB, Func<S.TechDef, bool> flag, string what)
    {
        var techs = sim.Content.Get<S.TechDef>().All;
        if (!techs.Any(flag)) return null;
        bool enabled = new[] { teamA, teamB }.Any(t => sim.State.TeamById(t).Techs.Any(id => sim.Content.TryGet<S.TechDef>(id, out var def) && flag(def)));
        return enabled ? null : $"no team has researched the technology enabling {what}";
    }
    public void SignAgreement(Simulation sim, int player, int target, DiplomaticAgreement agreement)
    {
        var relation = Relation(sim, sim.TeamOf(player).Id, sim.TeamOf(target).Id)!;
        relation.SignPact(agreement, sim.State.Turn);
    }
    public string? ValidateCancelAgreement(Simulation sim, int player, int target, DiplomaticAgreement agreement)
    {
        var error = Participants(sim, player, target);
        if (error is not null) return error;
        int a = sim.TeamOf(player).Id, b = sim.TeamOf(target).Id;
        if (!Enum.IsDefined(agreement)) return "unknown agreement";
        if (!HasMet(sim, a, b)) return "teams have not met";
        var relation = Relation(sim, a, b);
        if (relation is null) return "no agreement exists";
        int? started = agreement == DiplomaticAgreement.OpenBorders ? relation.OpenBordersStarted : relation.DefensivePactStarted;
        if (started is null) return "agreement not active";
        return sim.State.Turn - started < BindingTurns ? "agreement is binding for ten turns" : null;
    }
    public void CancelAgreement(Simulation sim, int player, int target, DiplomaticAgreement agreement)
    {
        var relation = Relation(sim, sim.TeamOf(player).Id, sim.TeamOf(target).Id);
        relation?.CancelPact(agreement);
    }
    private static bool BindingPeace(Simulation sim, int a, int b) =>
        Relation(sim, a, b)?.HasBindingPeace(sim.State.Turn, BindingTurns) == true;
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
        int[] friends = FriendsOf(sim, a, b);
        var state = Edit(sim);
        // Offensive declarations dissolve ALL the aggressor's pacts before defending partners are collected.
        foreach (var r in state.Relations.Values.Where(r => r.First == a || r.Second == a)) r.CancelPact(DiplomaticAgreement.DefensivePact);
        var defenders = state.Relations.Values.Where(r => r.HasDefensivePact && (r.First == b || r.Second == b))
            .Select(r => r.First == b ? r.Second : r.First).Where(t => t != a).Order().ToArray();
        StartWar(sim, a, b, true);
        RememberWar(sim, a, b, friends);
        foreach (int defender in defenders)
        {
            // The war consumes the victim's pact with each partner, whether or not that partner can join it.
            Relation(sim, b, defender)!.CancelPact(DiplomaticAgreement.DefensivePact);
            if (!sim.State.TeamById(defender).Wars.Contains(a) && !BindingPeace(sim, defender, a)) StartWar(sim, a, defender, false);
        }
    }
    private void StartWar(Simulation sim, int aggressor, int defender, bool offensive)
    {
        EstablishContact(sim, aggressor, defender);
        var r = Relation(sim, aggressor, defender)!;
        sim.State.SetWar(aggressor, defender, true);
        r.StartWar(sim.State.Turn, aggressor, offensive);
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
        r.MakePeace(sim.State.Turn);
        sim.Events.Publish(new DiplomaticPeaceSigned(a, b));
    }
}
