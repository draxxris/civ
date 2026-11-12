using Ariel.Game;

namespace Ariel.Mods.Espionage;

internal sealed class EspionageService : IEspionageService
{
    /// <summary>Missions are their own class with this service as a dependency, not a second half of it.</summary>
    private readonly SpyMissionRules missions;
    public EspionageService() => missions = new(this);

    public long MissionCost(Simulation sim, int player, int city, EspionageMissionKind mission, string? tech = null) =>
        missions.MissionCost(sim, player, city, mission, tech);
    public string? ValidateMission(Simulation sim, int player, int unit, int city, EspionageMissionKind mission, string? tech = null) =>
        missions.ValidateMission(sim, player, unit, city, mission, tech);
    public void ExecuteMission(Simulation sim, int player, int unit, int city, EspionageMissionKind mission, string? tech = null) =>
        missions.ExecuteMission(sim, player, unit, city, mission, tech);
    public IReadOnlyList<string> StealableTechnologies(Simulation sim, int player, int city) =>
        missions.StealableTechnologies(sim, player, city);

    internal static EspionageRulesDef Rules(Simulation sim) => sim.Content.Get<EspionageRulesDef>().All.Single();

    internal static EspionageTeamState Read(Simulation sim, int team) => sim.State.TeamById(team).Extensions.Get<EspionageTeamState>(sim.Content) ?? new();
    internal static EspionageTeamState Edit(Simulation sim, int team)
    {
        var state = Read(sim, team); sim.State.TeamById(team).Extensions.Set(sim.Content, state); return state;
    }
    /// <summary>Detached copy for reading only. Mutate through <see cref="EditPlayer"/>.</summary>
    internal static EspionagePlayerState PlayerState(Simulation sim, int player) => sim.State.PlayerById(player).Extensions.Get<EspionagePlayerState>(sim.Content) ?? new();
    internal static EspionagePlayerState EditPlayer(Simulation sim, int player)
    {
        var state = PlayerState(sim, player); sim.State.PlayerById(player).Extensions.Set(sim.Content, state); return state;
    }

    public long Points(Simulation sim, int team, int targetTeam) => Read(sim, team).Points.GetValueOrDefault(targetTeam);
    /// <summary>Unset weights mean "no interest": a rival receives nothing until the player asks for it.</summary>
    public int Weight(Simulation sim, int player, int targetTeam) => PlayerState(sim, player).Weights.GetValueOrDefault(targetTeam);
    public long Held(Simulation sim, int player) => PlayerState(sim, player).RemainderHundredths / 100;

    public IReadOnlyList<int> KnownRivals(Simulation sim, int player)
    {
        var diplomacy = sim.Rules().Diplomacy;
        int own = sim.TeamOf(player).Id;
        var living = LivingTeams(sim);
        return sim.State.Teams.Where(t => t.Id != own && living.Contains(t.Id) && (diplomacy is null || diplomacy.HasMet(sim, own, t.Id)))
            .Select(t => t.Id).Order().ToArray();
    }

    /// <summary>Teams with a non-barbarian civilization that still owns a city or unit. Eliminated teams receive no espionage.</summary>
    private static HashSet<int> LivingTeams(Simulation sim)
    {
        var owners = sim.State.Cities.Select(c => c.Owner).Concat(sim.State.Units.Select(u => u.Owner)).ToHashSet();
        return sim.State.Players.Where(p => owners.Contains(p.Id) && !PlayerRules.IsBarbarian(sim, p.Id)).Select(p => p.TeamId).ToHashSet();
    }

    public string? ValidateWeight(Simulation sim, int player, int targetTeam, int weight)
    {
        if (!sim.State.Players.Any(p => p.Id == player)) return "Unknown player";
        if (!KnownRivals(sim, player).Contains(targetTeam)) return "Requires a known rival team";
        int max = Rules(sim).MaxWeight;
        return weight < 0 || weight > max ? $"Espionage weight must be 0..{max}" : null;
    }

    public void SetWeight(Simulation sim, int player, int targetTeam, int weight)
    {
        if (ValidateWeight(sim, player, targetTeam, weight) is { } error) throw new InvalidOperationException(error);
        EditPlayer(sim, player).Weights[targetTeam] = weight;
    }

    public IReadOnlyDictionary<int, long> Spending(Simulation sim, int player, long espionageHundredths)
    {
        long points = (PlayerState(sim, player).RemainderHundredths + Math.Max(0, espionageHundredths)) / 100;
        var rivals = KnownRivals(sim, player);
        var spending = new SortedDictionary<int, long>();
        foreach (int rival in rivals) spending[rival] = 0;
        if (rivals.Count == 0) return spending;
        var weights = new int[rivals.Count];
        long total = 0;
        for (int i = 0; i < rivals.Count; i++)
        {
            weights[i] = Weight(sim, player, rivals[i]);
            total = checked(total + weights[i]);
        }
        if (total == 0) return spending; // Hold points until a target has positive weight.
        var shares = new (int Team, long Share, long Remainder)[rivals.Count];
        long assigned = 0;
        for (int i = 0; i < rivals.Count; i++)
        {
            long product = checked(points * weights[i]);
            shares[i] = (rivals[i], product / total, product % total);
            assigned = checked(assigned + shares[i].Share);
        }
        for (int i = 0; i < shares.Length; i++) spending[shares[i].Team] = shares[i].Share;
        foreach (var share in shares.OrderByDescending(s => s.Remainder).ThenBy(s => s.Team).Take(checked((int)(points - assigned))))
            spending[share.Team] = checked(spending[share.Team] + 1);
        return spending;
    }

    internal void Allocate(Simulation sim, int player)
    {
        long produced = sim.Rules().EspionageSource?.EspionageHundredths(sim, player)
            ?? sim.Rules().Economy?.Calculate(sim, player).CommerceHundredths.Espionage ?? 0;
        var state = PlayerState(sim, player);
        if (produced <= 0 && state.RemainderHundredths == 0) return;
        var spending = Spending(sim, player, produced);
        state = EditPlayer(sim, player);
        state.RemainderHundredths = checked(state.RemainderHundredths + Math.Max(0, produced) - spending.Values.Sum() * 100);
        if (spending.Values.All(p => p == 0)) return;
        var team = Edit(sim, sim.TeamOf(player).Id);
        foreach (var share in spending)
            team.Points[share.Key] = checked(team.Points.GetValueOrDefault(share.Key) + share.Value);
    }

    /// <summary>
    /// Bilateral ratio from each side's lifetime allocation plus live points against the other side. A mission
    /// spends points and is not credited back to <c>Spent</c>, so running missions lowers the attacker's own
    /// investment and raises the price of the next mission.
    /// </summary>
    private static long RatioPercent(Simulation sim, int team, int target)
    {
        long own = Invested(sim, team, target);
        long rival = Invested(sim, target, team);
        return Math.Clamp(checked(200 * (rival + 50)) / checked(own + rival + 100), 50, 200);
    }

    private static long Invested(Simulation sim, int team, int target)
    {
        var state = Read(sim, team);
        return checked(state.Spent.GetValueOrDefault(target) + state.Points.GetValueOrDefault(target));
    }

    /// <summary>Percent of a value with truncation toward zero. The single rounding step for espionage costs
    /// and thresholds, so multiply and divide order cannot silently rebalance missions.</summary>
    internal static long Percent(long value, long percent) => checked(value * percent) / 100;

    public long PassiveThreshold(Simulation sim, int team, int targetTeam, PassiveEspionageKind kind)
    {
        var rules = Rules(sim);
        long baseline = kind switch
        {
            PassiveEspionageKind.Demographics => rules.DemographicsThreshold,
            PassiveEspionageKind.CityVisibility => rules.CityVisibilityThreshold,
            PassiveEspionageKind.InspectResearch => rules.InspectResearchThreshold,
            PassiveEspionageKind.InvestigateCity => rules.InvestigateCityThreshold,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        return Percent(baseline, CostModifier(sim, team, targetTeam));
    }

    public IReadOnlyList<PassiveEspionageStatus> PassiveStatus(Simulation sim, int team, int targetTeam)
    {
        long modifier = CostModifier(sim, team, targetTeam);
        long points = Points(sim, team, targetTeam);
        bool living = team != targetTeam && LivingTeams(sim).Contains(targetTeam);
        var rules = Rules(sim);
        var kinds = new (PassiveEspionageKind Kind, int Baseline)[]
        {
            (PassiveEspionageKind.Demographics, rules.DemographicsThreshold),
            (PassiveEspionageKind.CityVisibility, rules.CityVisibilityThreshold),
            (PassiveEspionageKind.InspectResearch, rules.InspectResearchThreshold),
            (PassiveEspionageKind.InvestigateCity, rules.InvestigateCityThreshold)
        };
        var result = new PassiveEspionageStatus[kinds.Length];
        for (int i = 0; i < kinds.Length; i++)
        {
            long threshold = Percent(kinds[i].Baseline, modifier);
            result[i] = new(kinds[i].Kind, threshold, living && points >= threshold);
        }
        return result;
    }

    public CounterEspionageEffect? CounterEspionage(Simulation sim, int defendingTeam, int rivalTeam)
    {
        var effect = Read(sim, defendingTeam).CounterEspionage.GetValueOrDefault(rivalTeam);
        return effect is not null && sim.State.Turn < effect.ExpiresTurn ? effect : null;
    }

    internal static long CostModifier(Simulation sim, int attacker, int defender)
    {
        var protection = sim.Content.Services.Get<IEspionageService>().Single().CounterEspionage(sim, defender, attacker);
        return Percent(RatioPercent(sim, attacker, defender), protection?.CostPercent ?? 100);
    }

    public bool HasPassiveEffect(Simulation sim, int team, int targetTeam, PassiveEspionageKind kind) =>
        PassiveStatus(sim, team, targetTeam).Single(p => p.Kind == kind).Unlocked;

    public int SuccessChance(Simulation sim, int player, int city)
    {
        var rules = Rules(sim);
        var target = sim.State.CityById(city);
        int defenderTeam = sim.TeamOf(target.Owner).Id;
        int defenders = Math.Min(rules.MaxDefenders, sim.State.UnitsAt(target.Position)
            .Count(u => u.Health > 0 && sim.TeamOf(u.Owner).Id == defenderTeam && sim.Content.Get<Ariel.Content.Schema.UnitDef>(u.Type).CounterSpy));
        int ratio = (int)RatioPercent(sim, sim.TeamOf(player).Id, defenderTeam);
        long protection = CounterEspionage(sim, defenderTeam, sim.TeamOf(player).Id) is { } effect
            ? Percent(Math.Max(0, effect.CostPercent - 100), rules.ProtectionSuccessPenalty) : 0;
        long chance = rules.BaseSuccessPercent - defenders * (long)rules.DefenderSuccessPenalty
            + (100 - ratio) / rules.RatioSuccessDivisor - protection;
        return (int)Math.Clamp(chance, rules.MinSuccessPercent, rules.MaxSuccessPercent);
    }
}
