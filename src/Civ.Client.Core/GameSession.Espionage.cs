using Civ.Game;
using Schema = Civ.Content.Schema;

namespace Civ.Client;

public sealed record IntelligencePermission(PassiveEspionageKind Kind, long Threshold, bool Unlocked);
public sealed record RivalTreasury(int PlayerId, string Name, long Gold);
public sealed record RivalDemographics(int Cities, long Population, int Units, IReadOnlyList<RivalTreasury> Treasuries);
public sealed record RivalTechnology(string Id, string Name);
public sealed record RivalResearch(RivalTechnology? Current, long? Progress, long? Cost, IReadOnlyList<RivalTechnology> Queue);
public sealed record RivalCityDetails(int Population, long FoodStored, long Food, long Production, long Commerce,
    int Happy, int Unhappy, int Healthy, int Unhealthy, IReadOnlyList<QueueRow> Queue);
public sealed record RivalCityIntelligence(int Id, int Owner, string Name, Hex Position, RivalCityDetails? Details);

/// <summary>Null sections are locked or unavailable, never zero-filled. All data is a detached, read-only snapshot.</summary>
public sealed record RivalIntelligence(int TeamId, string Name, long Points, int Weight, long Spending,
    IReadOnlyList<IntelligencePermission> Permissions, CounterEspionageEffect? ProtectionAgainstRival,
    CounterEspionageEffect? RivalProtection, RivalDemographics? Demographics, RivalResearch? Research,
    IReadOnlyList<RivalCityIntelligence>? Cities);

public sealed partial class GameSession
{
    /// <summary>Only known, living rivals qualify. Check permissions before reading each private section.</summary>
    public RivalIntelligence? DescribeRivalIntelligence(int targetTeam)
    {
        var service = OptionalService<IEspionageService>();
        if (service is null || service.ValidateWeight(Sim, Human, targetTeam, 0) is not null) return null;
        int me = MyTeam.Id;
        var permissions = Enum.GetValues<PassiveEspionageKind>()
            .Select(kind => new IntelligencePermission(kind, service.PassiveThreshold(Sim, me, targetTeam, kind),
                service.HasPassiveEffect(Sim, me, targetTeam, kind))).ToArray();
        bool Allowed(PassiveEspionageKind kind) => permissions.Single(p => p.Kind == kind).Unlocked;
        var rival = Sim.State.TeamById(targetTeam);

        return new(targetTeam, TeamName(rival), service.Points(Sim, me, targetTeam), service.Weight(Sim, Human, targetTeam),
            service.Spending(Sim, Human, CurrentEspionageBudget()).GetValueOrDefault(targetTeam), permissions,
            service.CounterEspionage(Sim, me, targetTeam), service.CounterEspionage(Sim, targetTeam, me),
            Allowed(PassiveEspionageKind.Demographics) ? RivalDemographicsOf(rival) : null,
            Allowed(PassiveEspionageKind.InspectResearch) ? RivalResearchOf(rival) : null,
            Allowed(PassiveEspionageKind.CityVisibility) ? RivalCitiesOf(rival, Allowed(PassiveEspionageKind.InvestigateCity)) : null);
    }

    private long CurrentEspionageBudget() => OptionalService<IPlayerEspionageSource>()?.EspionageHundredths(Sim, Human)
        ?? OptionalService<IPlayerEconomyService>()?.Calculate(Sim, Human).CommerceHundredths.Espionage ?? 0;

    private RivalDemographics RivalDemographicsOf(Team rival)
    {
        var cities = Sim.State.Cities.Where(c => rival.Members.Contains(c.Owner)).ToArray();
        return new(cities.Length, cities.Sum(c => (long)c.Population), Sim.State.Units.Count(u => rival.Members.Contains(u.Owner)),
            rival.Members.Select(id => new RivalTreasury(id, PlayerName(id), Sim.State.PlayerById(id).Treasury)).ToArray());
    }

    private RivalTechnology RivalTechnologyOf(string id) => new(id, Sim.Content.Get<Schema.TechDef>(id).Name);

    private RivalResearch? RivalResearchOf(Team rival)
    {
        var research = OptionalService<IResearchService>();
        if (research is null) return null;
        var queue = rival.Extensions.Get<ResearchTeamState>(Sim.Content)?.Queue ?? [];
        if (rival.Researching is not { } id) return new(null, null, null, queue.Select(RivalTechnologyOf).ToArray());
        return new(RivalTechnologyOf(id), rival.ResearchProgress, research.CalculateCost(Sim, rival.Id, id).Cost,
            queue.Select(RivalTechnologyOf).ToArray());
    }

    private IReadOnlyList<RivalCityIntelligence> RivalCitiesOf(Team rival, bool investigate) =>
        Sim.State.Cities.Where(c => rival.Members.Contains(c.Owner)).OrderBy(c => c.Id)
            .Select(c => new RivalCityIntelligence(c.Id, c.Owner, c.Name, c.Position, investigate ? RivalCityDetailsOf(c) : null)).ToArray();

    // Do not use DescribeCity: its DTO contains own-city choices and controls, not rival intelligence.
    private RivalCityDetails? RivalCityDetailsOf(City city)
    {
        var yields = OptionalService<ICityYieldService>();
        var wellbeing = OptionalService<ICityWellbeingService>();
        if (yields is null || wellbeing is null) return null;
        var output = yields.Calculate(Sim, city.Id).Yields;
        var health = wellbeing.Calculate(Sim, city.Id);
        return new(city.Population, city.FoodStored, output.Food, output.Production, output.Commerce,
            health.Happy, health.Unhappy, health.Healthy, health.Unhealthy, QueueRows(city));
    }
}
