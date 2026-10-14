using Civ.Content;
using Civ.Content.Schema;
using Civ.Game;

namespace Civ.Mods.Ai;

/// <summary>Production choice for every city: emergencies, expansion, then flavor-weighted value.</summary>
internal sealed class CityPlanner(AiContext ctx, SitePlanner sites, ArmyPlan army)
{
    private readonly BuildingValuer _valuer = new(ctx);
    private readonly Lazy<AiOptions> _options = new(() => new AiOptions(ctx));
    private int _settlersQueued, _workersQueued, _wondersQueued, _soldiersQueued;
    public IReadOnlyList<CityView> Views { get; private set; } = [];
    public AiOptions Options => _options.Value;

    public void Plan()
    {
        Views = ctx.Cities.Select(c => CityView.Build(ctx, c)).ToArray();
        CountQueued();
        foreach (var view in Views.OrderByDescending(v => v.Population).ThenBy(v => v.City.Id))
        {
            if (NeedsChoice(view)) Choose(view);
        }
    }

    private void CountQueued()
    {
        foreach (var city in ctx.Cities)
        {
            if (city.ProductionQueue.FirstOrDefault() is not { Kind: "cities:unit" } head) { CountWonder(city); continue; }
            var role = ctx.Catalog.Roles.GetValueOrDefault(head.ContentId);
            if (role == UnitRole.Settler) _settlersQueued++;
            else if (role == UnitRole.Worker) _workersQueued++;
            else if (role is UnitRole.Melee or UnitRole.Siege) _soldiersQueued++;
        }
    }

    private void CountWonder(City city)
    {
        if (city.ProductionQueue.FirstOrDefault() is { Kind: "cities:building" } head
            && BuildingValuer.IsWonder(ctx.Sim.Content.Get<BuildingDef>(head.ContentId))) _wondersQueued++;
    }

    private bool NeedsChoice(CityView v)
    {
        if (v.City.ProductionQueue.Count == 0) return true;
        var head = v.City.ProductionQueue[0];
        if (head.Kind == "economy:process") return true;
        if (!ctx.Service<ICityProductionService>().Validate(ctx.Sim, v.City.Id, head.Kind, head.ContentId).Success) return true;
        if (head.Kind != "cities:unit") return v.Garrison < army.DesiredGarrison(v) || NeedsEscort() || ReadyToColonize(v, head);
        var role = ctx.Catalog.Roles.GetValueOrDefault(head.ContentId);
        if (v.Garrison < army.DesiredGarrison(v) && role is not (UnitRole.Melee or UnitRole.Siege)) return true;
        return role == UnitRole.Settler && !SettlerStillWanted(v);
    }

    private bool ReadyToColonize(CityView view, ProductionOrder head) => ctx.Cities.Count < 3 && CanExpand(view)
        && !BuildingValuer.IsWonder(ctx.Sim.Content.Get<BuildingDef>(head.ContentId));

    private bool SettlerStillWanted(CityView v) => sites.WantsMoreCities(ctx.Units.Count(u => ctx.Role(u) == UnitRole.Settler), 0) && v.Population >= 2;

    private void Choose(CityView v)
    {
        foreach (var (kind, id) in Candidates(v))
            if (ctx.Issue(new SetCityProductionCommand { Player = ctx.PlayerId, CityId = v.City.Id, Kind = kind, ContentId = id })) { Note(kind, id); return; }
    }

    private void Note(string kind, string id)
    {
        if (kind == "cities:building") { if (BuildingValuer.IsWonder(ctx.Sim.Content.Get<BuildingDef>(id))) _wondersQueued++; return; }
        if (kind != "cities:unit") return;
        var role = ctx.Catalog.Roles.GetValueOrDefault(id);
        if (role == UnitRole.Settler) _settlersQueued++;
        else if (role == UnitRole.Worker) _workersQueued++;
        else if (role is UnitRole.Melee or UnitRole.Siege) _soldiersQueued++;
    }

    private IEnumerable<(string Kind, string Id)> Candidates(CityView v)
    {
        foreach (var unit in Emergency(v)) yield return unit;
        foreach (var unit in Expansion(v)) yield return unit;
        foreach (var scored in Scored(v)) yield return scored;
        foreach (var process in Processes()) yield return ("economy:process", process);
    }

    private IEnumerable<(string, string)> Emergency(CityView v)
    {
        int garrisonWanted = army.DesiredGarrison(v);
        if (NeedsEscort() && v.City.Id == ctx.Cities.Min(c => c.Id)) garrisonWanted++;
        if (v.Garrison >= garrisonWanted) yield break;
        // Even a warrior is better than leaving a city empty. City-defense types receive a preference, not a gate.
        if (Options.Best(v.City.Id, UnitRole.Melee, u => AiOptions.DefenderQuality(u) + (AiOptions.Defends(u) ? 30 : 0)) is { } defender)
            yield return ("cities:unit", defender.Id);
    }

    private bool NeedsEscort() => ctx.UnitsWithRole(UnitRole.Settler).Any()
        && army.Soldiers + _soldiersQueued <= Views.Sum(army.DesiredGarrison);

    private IEnumerable<(string, string)> Expansion(CityView v)
    {
        if (CanExpand(v) && Options.Best(v.City.Id, UnitRole.Settler, u => -u.Cost) is { } settler)
            yield return ("cities:unit", settler.Id);
        int workers = ctx.Units.Count(u => ctx.Role(u) == UnitRole.Worker) + _workersQueued;
        if (v.Population >= 2 && workers < (ctx.Cities.Count * 3 + 3) / 4
            && Options.Best(v.City.Id, UnitRole.Worker, u => -u.Cost) is { } worker) yield return ("cities:unit", worker.Id);
        if (NeedsExplorer(v) && Options.Best(v.City.Id, UnitRole.Explorer, u => -u.Cost) is { } scout)
            yield return ("cities:unit", scout.Id);
    }

    private bool CanExpand(CityView v) => v.Threat == 0 && v.Population >= 2
        && sites.WantsMoreCities(ctx.Units.Count(u => ctx.Role(u) == UnitRole.Settler), _settlersQueued);

    private bool NeedsExplorer(CityView v) => ctx.Turn < 40 && !ctx.UnitsWithRole(UnitRole.Explorer).Any()
        && v.City.Id == ctx.Cities[0].Id;

    private IEnumerable<(string, string)> Scored(CityView v)
    {
        var options = new List<(string Kind, string Id, long Score)>();
        foreach (var b in Options.Buildings)
            if (!v.City.Buildings.Contains(b.Id) && Rate(b, v) is { } score) options.Add(("cities:building", b.Id, score));
        if (army.Soldiers + _soldiersQueued < army.Want(Views) && v.Production >= 2) options.AddRange(ArmyOptions(v));
        return options.OrderByDescending(o => o.Score).ThenBy(o => o.Id, StringComparer.Ordinal).Select(o => (o.Kind, o.Id));
    }

    private IEnumerable<(string, string, long)> ArmyOptions(CityView v)
    {
        int shortage = army.Want(Views) - army.Soldiers - _soldiersQueued;
        long drive = 80 + Math.Min(4, shortage) * 40 + (ctx.Leader?.BuildUnitProbability ?? 25) + (ctx.AtWarWithPlayers ? 50 : 0);
        if (Options.Best(v.City.Id, UnitRole.Melee, AiOptions.Quality, AiOptions.Attacks) is { } attacker) yield return ("cities:unit", attacker.Id, drive);
        if (Options.Best(v.City.Id, UnitRole.Siege, AiOptions.Quality) is { } siege && ctx.Units.Count(u => ctx.Role(u) == UnitRole.Siege) < 1 + ctx.Cities.Count / 3)
            yield return ("cities:unit", siege.Id, drive + (ctx.Team.Wars.Count > 0 && army.Soldiers >= 5 ? 20 : -5));
    }

    private long? Rate(BuildingDef b, CityView v)
    {
        int value = _valuer.Value(b, v);
        bool wonder = BuildingValuer.IsWonder(b);
        if (wonder && !WonderAffordable(b, v)) return null;
        if (wonder) value += 30 + (ctx.Leader?.WonderConstructRand ?? 0) / 2;
        if (value < 14) return null;
        return value * 1000L / (b.Cost + 60);
    }

    private bool WonderAffordable(BuildingDef b, CityView v) =>
        _wondersQueued < (ctx.Cities.Count + 1) / 2 && v.Production * 40 >= b.Cost && v.Population >= 4;

    private IEnumerable<string> Processes()
    {
        bool poor = ctx.Self.Treasury < EconomyPlanner.Reserve(ctx.Cities.Count);
        yield return poor ? "economy:process_wealth" : "economy:process_research";
        yield return "economy:process_wealth";
        yield return "economy:process_culture";
        yield return "economy:process_idle";
    }
}
