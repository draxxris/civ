using Civ.Content;
using Civ.Content.Schema;
using Civ.Game;

namespace Civ.Mods.Ai;

/// <summary>Production choice for every city: emergencies, expansion, then flavor-weighted value.</summary>
internal sealed class CityPlanner(AiContext ctx, SitePlanner sites, ArmyPlan army)
{
    private readonly BuildingValuer _valuer = new(ctx);
    private const int ParallelCities = 3, ParallelBuildings = 8;
    private readonly Lazy<AiOptions> _options = new(() => new AiOptions(ctx), LazyThreadSafetyMode.ExecutionAndPublication);
    private int _settlersQueued, _workersQueued, _wondersQueued, _soldiersQueued;
    public IReadOnlyList<CityView> Views { get; private set; } = [];
    public AiOptions Options => _options.Value;

    public void Plan()
    {
        AnnexMaturePuppets();
        Views = ParallelScoring.Select(ctx.Sim, ctx.Cities, ParallelCities, ctx.ViewOf);
        CountQueued();
        foreach (var view in Views.OrderByDescending(v => v.Population).ThenBy(v => v.City.Id))
        {
            if (!IsPuppet(view.City) && NeedsChoice(view)) Choose(view);
        }
    }

    private void AnnexMaturePuppets()
    {
        var candidates = ctx.Cities.Where(IsMaturePuppet).OrderBy(city => city.Id).ToArray();
        if (candidates.Length == 0 || !EconomyCanAnnex()) return;
        foreach (var city in candidates)
            ctx.Issue(new ResolveCityConquestCommand { Player = ctx.PlayerId, CityId = city.Id, Option = CityConquestOption.Annex });
    }

    private bool IsMaturePuppet(City city) =>
        city.Extensions.Get<CityRuleState>(ctx.Sim.Content) is { IsPuppet: true, OccupationTurns: 0 };

    private bool EconomyCanAnnex()
    {
        if (ctx.Self.Treasury < EconomyPlanner.Reserve(ctx.Cities.Count)) return false;
        return ctx.Service<IPlayerEconomyService>().Calculate(ctx.Sim, ctx.PlayerId).NetGold >= 0;
    }

    // These counters reserve this pass's production choices, including replaced heads, as before.
    // Exact all-queue role counts live in the context segment and reset after each accepted command.
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

    private bool IsPuppet(City city) => city.Extensions.Get<CityRuleState>(ctx.Sim.Content)?.IsPuppet == true;

    private bool NeedsChoice(CityView v)
    {
        if (v.City.ProductionQueue.Count == 0) return true;
        var head = v.City.ProductionQueue[0];
        if (head.Kind == "economy:process") return true;
        if (!ctx.CanProduce(v.City.Id, head.Kind, head.ContentId)) return true;
        if (head.Kind != "cities:unit") return v.Garrison < army.DesiredGarrison(v) || NeedsEscort() || ReadyToColonize(v, head);
        var role = ctx.Catalog.Roles.GetValueOrDefault(head.ContentId);
        if (v.Garrison < army.DesiredGarrison(v) && role != UnitRole.Melee) return true;
        return role == UnitRole.Settler && !SettlerStillWanted(v);
    }

    private bool ReadyToColonize(CityView view, ProductionOrder head) => ctx.Cities.Count < 3 && CanExpand(view)
        && !BuildingValuer.IsWonder(ctx.Sim.Content.Get<BuildingDef>(head.ContentId));

    private bool SettlerStillWanted(CityView v) => sites.WantsMoreCities(ctx.CountUnits(UnitRole.Settler), 0) && v.Population >= 2;

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
        if (Options.Best(v.City.Id, UnitRole.Melee, u => AiOptions.DefenderQuality(u) + (v.Threat > 0 ? u.Strength * 30 : 0) + (AiOptions.Defends(u) ? 30 : 0)) is { } defender)
            yield return ("cities:unit", defender.Id);
    }

    private bool NeedsEscort() => ctx.CountUnits(UnitRole.Settler) > 0
        && army.Soldiers + _soldiersQueued <= Views.Sum(army.DesiredGarrison);

    private IEnumerable<(string, string)> Expansion(CityView v)
    {
        if (CanExpand(v) && Options.Best(v.City.Id, UnitRole.Settler, u => -u.Cost) is { } settler)
            yield return ("cities:unit", settler.Id);
        int workers = ctx.CountUnits(UnitRole.Worker) + _workersQueued;
        if (v.Population >= 2 && workers < (ctx.Cities.Count * 3 + 3) / 4
            && Options.Best(v.City.Id, UnitRole.Worker, u => -u.Cost) is { } worker) yield return ("cities:unit", worker.Id);
        if (NeedsExplorer(v) && Options.Best(v.City.Id, UnitRole.Explorer, u => -u.Cost) is { } scout)
            yield return ("cities:unit", scout.Id);
    }



    private bool CanExpand(CityView v) => v.Threat == 0 && v.Population >= 2
        && sites.WantsMoreCities(ctx.CountUnits(UnitRole.Settler), _settlersQueued);

    private bool NeedsExplorer(CityView v) => ctx.Turn < 40 && ctx.CountUnits(UnitRole.Explorer) == 0
        && v.City.Id == ctx.Cities[0].Id;

    private IEnumerable<(string, string)> Scored(CityView v)
    {
        var options = new List<(string Kind, string Id, long Score)>();
        var buildings = Options.Buildings;
        var rated = ParallelScoring.Select(ctx.Sim, buildings, ParallelBuildings, b => Rate(b, v));
        for (int i = 0; i < buildings.Count; i++)
            if (rated[i] is { } score) options.Add(("cities:building", buildings[i].Id, score));
        if (army.Soldiers + _soldiersQueued < army.Want(Views) && v.Production >= 2) options.AddRange(ArmyOptions(v));
        options.AddRange(StrategicOptions(v));
        if (InquisitorWanted())
            foreach (var unit in Options.Units.Where(u => u.Inquisitor))
                if (ctx.CanProduce(v.City.Id, "cities:unit", unit.Id))
                    options.Add(("cities:unit", unit.Id, 500 + 1000L / Math.Max(1, unit.Cost)));
        return options.OrderByDescending(o => o.Score).ThenBy(o => o.Id, StringComparer.Ordinal).Select(o => (o.Kind, o.Id));
    }

    private int CountRole(UnitRole role) => ctx.CountUnits(role) + ctx.CountQueued(role);

    private IEnumerable<(string, string, long)> StrategicOptions(CityView view)
    {
        var targets = ctx.Targets;
        bool war = ctx.Team.Wars.Count > 0;
        if (targets.Coastal(view.City))
            foreach (var option in NavalOptions(view, targets, war)) yield return option;
        if (!war) yield break;
        int land = ctx.CountUnits(UnitRole.Melee) + ctx.CountUnits(UnitRole.Siege);
        if (CountRole(UnitRole.Siege) < Math.Max(1, land / 4)
            && Options.Best(view.City.Id, UnitRole.Siege, u => AiOptions.Quality(u) + u.BombardRate + u.BombRange * 10) is { } siege)
            yield return ("cities:unit", siege.Id, 340);
        if (CountRole(UnitRole.Air) < Math.Max(1, land / 6)
            && Options.Best(view.City.Id, UnitRole.Air, u => u.AirCombat * 10 + u.BombRate + u.InterceptionPercent) is { } air)
            yield return ("cities:unit", air.Id, 360);
    }

    private IEnumerable<(string, string, long)> NavalOptions(CityView view, CampaignTargets targets, bool war)
    {
        int land = ctx.CountUnits(UnitRole.Melee) + ctx.CountUnits(UnitRole.Siege);
        bool needTransport = war && CountRole(UnitRole.NavalTransport) < Math.Max(1, land / 4)
            && (targets.OverseasTarget(targets.Area(view.City.Position)) is not null
                || ctx.Cities.Select(c => targets.Area(c.Position)).Distinct().Count() > 1);
        if (needTransport && Options.Best(view.City.Id, UnitRole.NavalTransport, u => Math.Max(u.Cargo, u.TotalCargo) * 100 - u.Cost,
                u => u.CargoDomain is null or "military:domain_land") is { } transport)
            yield return ("cities:unit", transport.Id, 390);
        int escorts = CountRole(UnitRole.NavalCombat) + CountRole(UnitRole.NavalExplorer);
        if (war && escorts < Math.Max(1, CountRole(UnitRole.NavalTransport))
            && Options.Best(view.City.Id, UnitRole.NavalCombat, AiOptions.Quality) is { } ship)
            yield return ("cities:unit", ship.Id, needTransport ? 380 : 300);
        foreach (var option in NavalExploration(view, targets, escorts)) yield return option;
    }

    private IEnumerable<(string, string, long)> NavalExploration(CityView view, CampaignTargets targets, int escorts)
    {
        if (escorts != 0) yield break;
        if (!ctx.Grid.Tiles.Any(t => targets.Water(t) && !ctx.Sim.State.Plots[t.Value].Revealed.Contains(ctx.Team.Id))) yield break;
        if (Options.Best(view.City.Id, UnitRole.NavalExplorer, u => u.Moves * 20 + u.Strength) is { } explorer)
            yield return ("cities:unit", explorer.Id, 170);
    }

    private bool InquisitorWanted()
    {
        var religion = ctx.Sim.Content.Services.Get<IReligionService>().SingleOrDefault();
        if (religion is null || !religion.IsInquisitionEnabled(ctx.Sim, ctx.PlayerId)) return false;
        if (ctx.Units.Any(u => ctx.Alive(u) && u.Health > 0 && ctx.Def(u).Inquisitor)) return false;
        if (ctx.Cities.SelectMany(c => c.ProductionQueue).Any(o => o.Kind == "cities:unit" && ctx.Sim.Content.Get<UnitDef>(o.ContentId).Inquisitor)) return false;
        return ctx.Cities.Any(c => religion.HasInquisitionTargets(ctx.Sim, c.Id));
    }

    private IEnumerable<(string, string, long)> ArmyOptions(CityView v)
    {
        int shortage = army.Want(Views) - army.Soldiers - _soldiersQueued;
        long drive = 80 + Math.Min(4, shortage) * 40 + (ctx.Leader?.BuildUnitProbability ?? 25) + (ctx.AtWarWithPlayers ? 50 : 0);
        if (Options.Best(v.City.Id, UnitRole.Melee, AiOptions.Quality, AiOptions.Attacks) is { } attacker) yield return ("cities:unit", attacker.Id, drive);
        if (ctx.CountUnits(UnitRole.Siege) < 1 + ctx.Cities.Count / 3 && Options.Best(v.City.Id, UnitRole.Siege, AiOptions.Quality) is { } siege)
            yield return ("cities:unit", siege.Id, drive + (ctx.Team.Wars.Count > 0 && army.Soldiers >= 5 ? 20 : -5));
    }

    private long? Rate(BuildingDef b, CityView v)
    {
        bool wonder = BuildingValuer.IsWonder(b);
        if (wonder && !WonderAffordable(b, v)) return null;
        if (!ctx.CanProduce(v.City.Id, "cities:building", b.Id)) return null;
        int value = _valuer.Value(b, v);
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
