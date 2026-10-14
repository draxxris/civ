using Civ.Game;
using Schema = Civ.Content.Schema;

namespace Civ.Client;

/// <summary>One corporation in the corporations screen.</summary>
public sealed record CorporationRow(string Id, string Name, string Icon, string Help, bool Founded, int? Headquarters, string? HeadquartersName, int Branches);

/// <summary>A corporation present in a city; <see cref="Headquarters"/> marks the city that founded it.</summary>
public sealed record CityCorporation(string Id, string Name, bool Headquarters, bool Active, string Icon);

public sealed record CorporationCityRow(int CityId, string CityName, IReadOnlyList<CityCorporation> Corporations);

public sealed record CorporationScreen(bool Available, IReadOnlyList<CorporationRow> Corporations, IReadOnlyList<CorporationCityRow> Cities);

/// <summary>One rival team in the espionage screen. Passive flags show what the accumulated points already unlocked.</summary>
public sealed record EspionageRow(
    int TeamId, string Name, long Points, int Weight, bool Demographics, bool CityVisibility, bool InspectResearch, bool InvestigateCity);

public sealed record EspionageScreen(bool Available, IReadOnlyList<EspionageRow> Rivals);

public sealed record EventOptionRow(string Id, string Name, string Help, bool IsDefault, string? Blocked);

public sealed record PendingEventRow(int EventId, string Trigger, string Name, string Help, int? City, string? CityName, int Turn, IReadOnlyList<EventOptionRow> Options);

public sealed partial class GameSession
{
    private bool Registered<T>() where T : Content.ContentDef
    {
        try { _ = Sim.Content.Get<T>(); return true; }
        catch (KeyNotFoundException) { return false; }
    }

    /// <summary>All corporations with their founding state, and what each of the player's cities hosts.</summary>
    public CorporationScreen DescribeCorporations()
    {
        var service = OptionalService<ICorporationService>();
        if (service == null || !Registered<Schema.CorporationDef>()) return new(false, [], []);
        var rows = Sim.Content.Get<Schema.CorporationDef>().All.OrderBy(c => c.Name, StringComparer.Ordinal).ThenBy(c => c.Id, StringComparer.Ordinal)
            .Select(def =>
            {
                int? hq = service.Headquarters(Sim, def.Id);
                int branches = Sim.State.Cities.Count(c => service.Present(Sim, c.Id).Contains(def.Id));
                return new CorporationRow(def.Id, def.Name, def.Icon, def.Help, hq != null, hq, hq is int id ? Sim.State.FindCity(id)?.Name : null, branches);
            }).ToList();
        var cities = Sim.State.Cities.Where(c => c.Owner == Human).OrderBy(c => c.Id)
            .Select(c => new CorporationCityRow(c.Id, c.Name, CorporationsIn(c))).Where(r => r.Corporations.Count > 0).ToList();
        return new(true, rows, cities);
    }

    /// <summary>The corporations present in a city, headquarters marked.</summary>
    public IReadOnlyList<CityCorporation> CorporationsIn(City city)
    {
        var service = OptionalService<ICorporationService>();
        if (service == null) return [];
        return service.Present(Sim, city.Id).Select(id => Sim.Content.Get<Schema.CorporationDef>(id))
            .Select(d => new CityCorporation(d.Id, d.Name, service.Headquarters(Sim, d.Id) == city.Id, service.IsActive(Sim, city.Id, d.Id), d.Icon)).ToList();
    }

    /// <summary>Rival teams the player has met, with espionage points, weight and unlocked passive effects.</summary>
    public EspionageScreen DescribeEspionage()
    {
        var service = OptionalService<IEspionageService>();
        if (service == null) return new(false, []);
        var diplomacy = OptionalService<IDiplomacyService>();
        int me = MyTeam.Id;
        var rivals = Sim.State.Teams.Where(t => t.Id != me && (diplomacy == null || diplomacy.HasMet(Sim, me, t.Id))).OrderBy(t => t.Id)
            .Select(t => new EspionageRow(t.Id, TeamName(t), service.Points(Sim, me, t.Id), service.Weight(Sim, Human, t.Id),
                service.HasPassiveEffect(Sim, me, t.Id, PassiveEspionageKind.Demographics),
                service.HasPassiveEffect(Sim, me, t.Id, PassiveEspionageKind.CityVisibility),
                service.HasPassiveEffect(Sim, me, t.Id, PassiveEspionageKind.InspectResearch),
                service.HasPassiveEffect(Sim, me, t.Id, PassiveEspionageKind.InvestigateCity))).ToList();
        return new(true, rivals);
    }

    private string TeamName(Team team) => string.Join(", ", team.Members.OrderBy(m => m).Select(PlayerName));

    /// <summary>Change how much of the espionage budget goes against a team.</summary>
    public bool SetEspionageWeight(int targetTeam, int weight) => Submit(new SetEspionageWeightCommand { Player = Human, TargetTeam = targetTeam, Weight = weight });

    /// <summary>Events waiting for the local player's choice.</summary>
    public IReadOnlyList<PendingEventRow> DescribePendingEvents()
    {
        var service = OptionalService<IEventService>();
        if (service == null) return [];
        return service.Pending(Sim, Human).Select(p =>
        {
            Sim.Content.TryGet<Schema.EventTriggerDef>(p.Trigger, out var def);
            var options = service.Options(Sim, p.Id).Select(o =>
            {
                Sim.Content.TryGet<Schema.EventOptionDef>(o, out var opt);
                return new EventOptionRow(o, opt?.Name ?? Labels.Humanize(o), opt?.Help ?? "", opt?.IsDefault ?? false, service.ValidateChoice(Sim, Human, p.Id, o));
            }).ToList();
            return new PendingEventRow(p.Id, p.Trigger, def?.Name ?? Labels.Humanize(p.Trigger), def?.Help ?? "",
                p.City >= 0 ? p.City : null, p.City >= 0 ? Sim.State.FindCity(p.City)?.Name : null, p.Turn, options);
        }).ToList();
    }

    /// <summary>Pick an option for the pending event of the given trigger.</summary>
    public bool ChooseEventOption(string triggerId, string optionId)
    {
        var service = OptionalService<IEventService>();
        var pending = service?.Pending(Sim, Human).FirstOrDefault(p => p.Trigger == triggerId);
        if (pending == null) { SetMessage("No such event is waiting"); return false; }
        return Submit(new ChooseEventOptionCommand { Player = Human, EventId = pending.Id, Option = optionId });
    }

    private IEnumerable<UnitActionInfo> CorporationActions(Unit unit)
    {
        var service = OptionalService<ICorporationService>();
        if (service == null || unit.MovesLeft <= 0 || !Registered<Schema.CorporationDef>()) yield break;
        var city = Sim.State.CityAt(unit.Position);
        foreach (var def in Sim.Content.Get<Schema.CorporationDef>().All.OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            var found = new FoundCorporationCommand { Player = Human, Unit = unit.Id, Corporation = def.Id };
            if (found.Validate(Sim) == null) yield return new("found_corp:" + def.Id, "Found " + def.Name, found, null, def.Help);
            if (city == null) continue;
            var spread = new SpreadCorporationCommand { Player = Human, City = city.Id, Unit = unit.Id, Corporation = def.Id };
            if (spread.Validate(Sim) == null) yield return new("spread_corp:" + def.Id, "Spread " + def.Name, spread, null, $"{def.SpreadCost} gold");
        }
    }

    private IEnumerable<UnitActionInfo> SpyActions(Unit unit)
    {
        var service = OptionalService<IEspionageService>();
        var city = Sim.State.CityAt(unit.Position);
        if (service == null || city == null || city.Owner == Human || unit.MovesLeft <= 0) yield break;
        foreach (var kind in Enum.GetValues<EspionageMissionKind>().Where(k => k != EspionageMissionKind.StealTech))
        {
            var mission = new ExecuteEspionageMissionCommand { Player = Human, Unit = unit.Id, City = city.Id, Mission = kind };
            if (mission.Validate(Sim) != null) continue;
            yield return new($"spy:{kind}:{city.Id}", Labels.Humanize(kind.ToString()), mission, null,
                $"{service.MissionCost(Sim, Human, city.Id, kind)} points, {service.SuccessChance(Sim, Human, city.Id)}% chance");
        }
    }
}
