using Civ.Game;
using Schema = Civ.Content.Schema;

namespace Civ.Client;

/// <summary>One religion in a city: <see cref="Holy"/> when this city is its holy city.</summary>
public sealed record CityReligion(string Id, string Name, bool Holy, bool State, string Icon);

/// <summary>One loaded religion as the religion screen lists it.</summary>
public sealed record ReligionRow(
    string Id, string Name, string Adjective, string Icon, string Help, bool Founded, string? FoundingTech,
    int? HolyCityId, string? HolyCityName, Hex? HolyCityHex, bool IsState, bool CanAdopt, string? Blocked);

/// <summary>The religion screen: every loaded religion, the state religion, and what changing it would cost.</summary>
public sealed record ReligionScreen(
    string? StateReligion, string? StateReligionName, bool CanConvert, int ConversionAnarchyTurns, int CooldownTurns,
    int CooldownRemaining, string? NoReligionBlocked, IReadOnlyList<ReligionRow> Religions);

public sealed partial class GameSession
{
    private IReligionService? Religions => OptionalService<IReligionService>();

    /// <summary>The religion screen for the local player; empty when no ruleset registers religion.</summary>
    public ReligionScreen DescribeReligions()
    {
        var service = Religions;
        if (service == null) return new(null, null, false, 0, 0, 0, null, []);
        var state = service.StateReligion(Sim, Human);
        var founded = Sim.Content.Get<Schema.ReligionDef>().All.OrderBy(r => r.Name, StringComparer.Ordinal).Select(r => RowOf(service, r, state)).ToList();
        var preview = service.PreviewConversion(Sim, Human);
        int remaining = Me.Extensions.Get<ReligionPlayerState>(Sim.Content)?.ConversionTimer ?? 0;
        string? none = service.ValidateConversion(Sim, Human, null);
        bool can = founded.Any(r => r.CanAdopt) || (state != null && none == null);
        string? name = state == null ? null : Sim.Content.Get<Schema.ReligionDef>(state).Name;
        return new(state, name, can, preview.AnarchyTurns, preview.CooldownTurns, remaining, none, founded);
    }

    private ReligionRow RowOf(IReligionService service, Schema.ReligionDef def, string? state)
    {
        int? holy = service.HolyCity(Sim, def.Id);
        var city = holy is int id ? Sim.State.FindCity(id) : null;
        string? blocked = def.Id == state ? "Already the state religion" : service.ValidateConversion(Sim, Human, def.Id);
        string? tech = def.FoundingTech is { } t && Sim.Content.TryGet<Schema.TechDef>(t, out var techDef) ? techDef.Name : null;
        return new(def.Id, def.Name, def.Adjective, def.Icon, def.Help, holy != null || Founded(def.Id), tech, holy, city?.Name,
            city?.Position, def.Id == state, blocked == null, blocked);
    }

    private bool Founded(string religion) =>
        Sim.State.Extensions.Get<ReligionGameState>(Sim.Content)?.Founded.ContainsKey(religion) == true;

    /// <summary>Adopt a state religion, or pass null to drop to none. The rules decide anarchy and cooldown.</summary>
    public bool AdoptStateReligion(string? religion) => Submit(new SetStateReligionCommand { Player = Human, Religion = religion });

    /// <summary>The religions present in a city, holy-city markers included.</summary>
    public IReadOnlyList<CityReligion> ReligionsIn(City city)
    {
        var service = Religions;
        if (service == null) return [];
        var state = service.StateReligion(Sim, city.Owner);
        return service.Present(Sim, city.Id).Select(id => Sim.Content.Get<Schema.ReligionDef>(id))
            .Select(r => new CityReligion(r.Id, r.Name, service.HolyCity(Sim, r.Id) == city.Id, r.Id == state, r.Icon)).ToList();
    }

    /// <summary>Short banner text such as "Buddhism*, Taoism" where a star marks a holy city.</summary>
    public string ReligionBadge(City city) => string.Join(", ", ReligionsIn(city).Select(r => r.Name + (r.Holy ? "*" : "")));

    private IEnumerable<UnitActionInfo> ReligionActions(Unit unit)
    {
        var service = Religions;
        if (service == null || unit.MovesLeft <= 0) yield break;
        foreach (var religion in Sim.Content.Get<Schema.ReligionDef>().All.OrderBy(r => r.Id, StringComparer.Ordinal))
        {
            var spread = new SpreadReligionCommand { Player = Human, Unit = unit.Id, Religion = religion.Id };
            if (spread.Validate(Sim) == null)
                yield return new("spread:" + religion.Id, "Spread " + religion.Name, spread, null,
                    $"{service.MissionaryChance(Sim, unit.Id, religion.Id)}% chance");
            foreach (var building in religion.Shrines.Distinct().OrderBy(b => b, StringComparer.Ordinal))
            {
                var shrine = new BuildShrineCommand { Player = Human, Unit = unit.Id, Building = building };
                if (shrine.Validate(Sim) == null)
                    yield return new("shrine:" + building, "Build " + Sim.Content.Get<Schema.BuildingDef>(building).Name, shrine);
            }
        }
    }
}
