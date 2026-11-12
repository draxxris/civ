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
    private static IRelationsService Relations(Simulation sim) => sim.Rules().Relations;
    private static bool HasMet(Simulation sim, int a, int b) => Relations(sim).HasMet(sim, a, b);
    private static TeamAgreement? Agreement(Simulation sim, int a, int b) => Read(sim).GetAgreement(a, b);
    private static bool AtWar(Simulation sim, int a, int b) => sim.State.TeamById(a).Wars.Contains(b);
    private static bool Barbarian(Simulation sim, int team) => sim.State.TeamById(team).Members.Any(p => PlayerRules.IsBarbarian(sim, p));
    private static string? Participants(Simulation sim, int player, int target)
    {
        if (!sim.State.Players.Any(p => p.Id == player) || !sim.State.Players.Any(p => p.Id == target)) return "unknown player";
        int a = sim.TeamOf(player).Id, b = sim.TeamOf(target).Id;
        if (a == b) return "same team";
        return Barbarian(sim, a) || Barbarian(sim, b) ? "barbarians cannot negotiate" : null;
    }
    public bool HasOpenBorders(Simulation sim, int teamA, int teamB) => teamA == teamB ||
        (!AtWar(sim, teamA, teamB) && Agreement(sim, teamA, teamB)?.OpenBordersStarted is not null);
    public bool HasDefensivePact(Simulation sim, int teamA, int teamB) => teamA != teamB &&
        !AtWar(sim, teamA, teamB) && Agreement(sim, teamA, teamB)?.DefensivePactStarted is not null;
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
    public void SignAgreement(Simulation sim, int player, int target, DiplomaticAgreement agreement) =>
        Edit(sim).GetOrCreateAgreement(sim.TeamOf(player).Id, sim.TeamOf(target).Id).Sign(agreement, sim.State.Turn);
    public string? ValidateCancelAgreement(Simulation sim, int player, int target, DiplomaticAgreement agreement)
    {
        var error = Participants(sim, player, target);
        if (error is not null) return error;
        int a = sim.TeamOf(player).Id, b = sim.TeamOf(target).Id;
        if (!Enum.IsDefined(agreement)) return "unknown agreement";
        if (!HasMet(sim, a, b)) return "teams have not met";
        if (AtWar(sim, a, b) || Agreement(sim, a, b)?.Started(agreement) is not { } started) return "agreement not active";
        return sim.State.Turn - started < BindingTurns ? "agreement is binding for ten turns" : null;
    }
    public void CancelAgreement(Simulation sim, int player, int target, DiplomaticAgreement agreement) =>
        Agreement(sim, sim.TeamOf(player).Id, sim.TeamOf(target).Id)?.Cancel(agreement);

    // The base game's questions about agreements and dispositions.
    public bool OpenBorders(Simulation sim, int team, int other) => HasOpenBorders(sim, team, other);
    public bool Allied(Simulation sim, int teamA, int teamB) => HasDefensivePact(sim, teamA, teamB);
    public Disposition Toward(Simulation sim, int fromPlayer, int towardPlayer) => GetAttitude(sim, fromPlayer, towardPlayer).Tier;

    /// <summary>An offensive declaration dissolves every defensive pact of the aggressor, and then calls each partner of
    /// the defender's pacts into the war, consuming those pacts whether or not the partner can join.</summary>
    public IReadOnlyList<int> CallToArms(Simulation sim, int aggressor, int defender)
    {
        var state = Read(sim);
        foreach (var (key, agreement) in state.Agreements)
            if (Involves(key, aggressor)) agreement.Cancel(DiplomaticAgreement.DefensivePact);
        var partners = state.Agreements.Where(p => Involves(p.Key, defender) && HasDefensivePact(sim, Other(p.Key, defender), defender))
            .Select(p => Other(p.Key, defender)).Where(t => t != aggressor).Order().ToArray();
        foreach (int partner in partners) Agreement(sim, defender, partner)!.Cancel(DiplomaticAgreement.DefensivePact);
        return partners;
    }
    private static bool Involves(string pairKey, int team) => Teams(pairKey) is var (a, b) && (a == team || b == team);
    private static int Other(string pairKey, int team) => Teams(pairKey) is var (a, b) && a == team ? b : a;
    private static (int, int) Teams(string pairKey)
    {
        int slash = pairKey.IndexOf('/');
        return (int.Parse(pairKey.AsSpan(0, slash)), int.Parse(pairKey.AsSpan(slash + 1)));
    }

    /// <summary>A war ends the two teams' agreements and every deal between them, proposed or running.</summary>
    internal static void EndAgreements(Simulation sim, WarDeclared war)
    {
        var state = Read(sim);
        if (state.GetAgreement(war.AggressorTeam, war.DefenderTeam) is { } agreement)
        {
            agreement.Cancel(DiplomaticAgreement.OpenBorders);
            agreement.Cancel(DiplomaticAgreement.DefensivePact);
        }
        var pair = Pair(war.AggressorTeam, war.DefenderTeam);
        foreach (var deal in state.Deals.Where(d => d.Status is DiplomaticDealStatus.Active or DiplomaticDealStatus.Proposed))
            if (Pair(sim.TeamOf(deal.Proposer).Id, sim.TeamOf(deal.Recipient).Id) == pair) deal.Status = DiplomaticDealStatus.Cancelled;
    }
}
