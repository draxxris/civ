using Civ.Game;

namespace Civ.Mods.Espionage;

internal sealed partial class EspionageService : IEspionageService
{
    private static EspionageTeamState Read(Simulation sim, int team) => sim.State.TeamById(team).Extensions.Get<EspionageTeamState>(sim.Content) ?? new();
    private static EspionageTeamState Edit(Simulation sim, int team)
    {
        var state = Read(sim, team); sim.State.TeamById(team).Extensions.Set(sim.Content, state); return state;
    }
    private static EspionagePlayerState Player(Simulation sim, int player) => sim.State.PlayerById(player).Extensions.Get<EspionagePlayerState>(sim.Content) ?? new();
    public long Points(Simulation sim, int team, int targetTeam) => Read(sim, team).Points.GetValueOrDefault(targetTeam);
    public int Weight(Simulation sim, int player, int targetTeam) => Player(sim, player).Weights.GetValueOrDefault(targetTeam, 1);
    private static IEnumerable<int> Rivals(Simulation sim, int player)
    {
        var diplomacy = sim.Content.Services.Get<IDiplomacyService>().SingleOrDefault();
        int own = sim.TeamOf(player).Id;
        return sim.State.Teams.Where(t => t.Id != own && (diplomacy is null || diplomacy.HasMet(sim, own, t.Id))).Select(t => t.Id).Order();
    }
    public string? ValidateWeight(Simulation sim, int player, int targetTeam, int weight)
    {
        if (!sim.State.Players.Any(p => p.Id == player)) return "Unknown player";
        if (!Rivals(sim, player).Contains(targetTeam)) return "Requires a known rival team";
        return weight is < 0 or > 10000 ? "Espionage weight must be 0..10000" : null;
    }
    public void SetWeight(Simulation sim, int player, int targetTeam, int weight)
    {
        if (ValidateWeight(sim, player, targetTeam, weight) is { } error) throw new InvalidOperationException(error);
        var state = Player(sim, player); sim.State.PlayerById(player).Extensions.Set(sim.Content, state);
        state.Weights[targetTeam] = weight;
    }
    internal void Allocate(Simulation sim, int player)
    {
        var economy = sim.State.PlayerById(player).Extensions.TryGet<EconomyPlayerState>(sim.Content);
        long produced = economy?.SettledTurn == sim.State.Turn ? economy.SettledCommerceHundredths.Espionage
            : sim.Content.Services.Get<IPlayerEconomyService>().SingleOrDefault()?.Calculate(sim, player).CommerceHundredths.Espionage ?? 0;
        var state = Player(sim, player);
        if (produced <= 0 && state.RemainderHundredths == 0) return;
        sim.State.PlayerById(player).Extensions.Set(sim.Content, state);
        state.RemainderHundredths += Math.Max(0, produced);
        var rivals = Rivals(sim, player).Where(t => Weight(sim, player, t) > 0).ToArray();
        long weight = rivals.Sum(t => (long)Weight(sim, player, t));
        if (weight == 0) return; // Hold unallocated points, including fractions, until a target is available.
        long points = state.RemainderHundredths / 100; state.RemainderHundredths %= 100;
        if (points == 0) return;
        var team = Edit(sim, sim.TeamOf(player).Id);
        long assigned = 0;
        var shares = rivals.Select(t => (Team: t, Share: points * Weight(sim, player, t) / weight,
            Remainder: points * Weight(sim, player, t) % weight)).ToArray();
        foreach (var share in shares)
        {
            team.Points[share.Team] = team.Points.GetValueOrDefault(share.Team) + share.Share;
            assigned += share.Share;
        }
        foreach (var share in shares.OrderByDescending(s => s.Remainder).ThenBy(s => s.Team).Take((int)(points - assigned))) team.Points[share.Team]++;
    }
    private static long RatioPercent(Simulation sim, int team, int target)
    {
        long own = Read(sim, team).Spent.Values.Sum();
        long rival = Read(sim, target).Spent.Values.Sum();
        return Math.Clamp(200 * (rival + 50) / (own + rival + 100), 50, 200);
    }
    public long PassiveThreshold(Simulation sim, int team, int targetTeam, PassiveEspionageKind kind)
    {
        long baseline = kind switch
        {
            PassiveEspionageKind.Demographics => 50,
            PassiveEspionageKind.CityVisibility => 100,
            PassiveEspionageKind.InspectResearch => 150,
            PassiveEspionageKind.InvestigateCity => 200,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        return baseline * RatioPercent(sim, team, targetTeam) / 100;
    }
    public bool HasPassiveEffect(Simulation sim, int team, int targetTeam, PassiveEspionageKind kind) => team != targetTeam
        && Points(sim, team, targetTeam) >= PassiveThreshold(sim, team, targetTeam, kind);
    public int SuccessChance(Simulation sim, int player, int city)
    {
        var target = sim.State.CityById(city);
        int defenders = sim.State.UnitsAt(target.Position).Count(u => sim.TeamOf(u.Owner).Id == sim.TeamOf(target.Owner).Id && sim.Content.Get<Civ.Content.Schema.UnitDef>(u.Type).CounterSpy);
        return (int)Math.Clamp(85 - defenders * 15 + (100 - RatioPercent(sim, sim.TeamOf(player).Id, sim.TeamOf(target.Owner).Id)) / 5, 10, 95);
    }
}
