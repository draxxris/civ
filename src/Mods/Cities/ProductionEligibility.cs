using Civ.Content;
using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.Cities;

internal static class ProductionEligibility
{
    internal static RequirementResult Validate(Simulation sim, City city, string kind, string id)
    {
        var failures = new List<RequirementFailure>();
        if (kind == "cities:building") Building(sim, city, id, failures);
        else if (kind == "cities:unit") Unit(sim, city, id, failures);
        else if (kind == "economy:process")
        {
            var processes = sim.Content.Services.Get<ICityProcessService>().SingleOrDefault();
            if (processes is null) Fail(failures, "Process service is unavailable");
            else failures.AddRange(processes.Validate(sim, city.Id, id).Failures);
        }
        else if (sim.Content.Services.Get<IProductionKind>().FirstOrDefault(k => k.Id == kind) is { } owner)
            failures.AddRange(owner.Validate(sim, city.Id, id).Failures);
        else Fail(failures, "Unsupported production kind: " + kind);
        return new(failures.Count == 0, failures);
    }
    private static void Building(Simulation sim, City city, string id, List<RequirementFailure> failures)
    {
        if (!sim.Content.TryGet<S.BuildingDef>(id, out var b)) { Fail(failures, "Unknown building"); return; }
        Requirements(sim, city, b, failures);
        if (b.Cost < 0) Fail(failures, "Building cannot be constructed");
        if (city.Buildings.Contains(id) || CityData.EffectiveBuildings(sim, city).Any(x => x.Id == id)) Fail(failures, "Building already exists");
        if (b.ObsoleteTech is { } obsolete && sim.KnowsTech(city.Owner, obsolete)) Fail(failures, "Building is obsolete");
        Substitution(sim, city, b.DefaultBuilding, id, false, failures);
        Limits(sim, city, b.DefaultBuilding, b.Limits, false, failures);
        Location(sim, city, b, failures);
        WonderCapacity(sim, city, b, failures);
        Religion(sim, city, b, failures);
        Upgrade(sim, city, b, failures);
        if (b.BonusType is { } bonus && Available(sim, city, bonus) <= 0)
            Fail(failures, "Requires connected resource: " + bonus);
        Power(sim, city, b, failures);
    }
    private static void Power(Simulation sim, City city, S.BuildingDef b, List<RequirementFailure> failures)
    {
        if (b.RequiresPower && !CityData.IsPowered(sim, city)) Fail(failures, "Requires power");
        if (b.NoPower && CityData.IsAreaCleanPower(sim, city)) Fail(failures, "Area already has clean power");
    }
    private static void Upgrade(Simulation sim, City city, S.BuildingDef b, List<RequirementFailure> failures)
    {
        var active = CityData.Active(sim, city);
        if (active.Any(existing => CityBuildingRules.Replaces(existing, b)))
            Fail(failures, "Replaced by an upgraded building");
        if (b.ExtendsBuildingClass is { Length: > 0 } category && !active.Any(x => x.DefaultBuilding == category || x.Id == category))
            Fail(failures, "Requires active building class: " + category);
    }
    /// <summary>Connected copies after diplomatic allocations. Imports count here; exports reduce the count.</summary>
    private static long Available(Simulation sim, City city, string bonus) =>
        sim.Content.Services.Get<IDiplomacyService>().SingleOrDefault()?.AvailableResourceCount(sim, city.Owner, city.Id, bonus)
        ?? sim.Content.Services.Get<IResourceNetwork>().Single().ConnectedCount(sim, city.Owner, city.Id, bonus);
    private static void Religion(Simulation sim, City city, S.BuildingDef b, List<RequirementFailure> failures)
    {
        var service = sim.Content.Services.Get<IReligionService>().SingleOrDefault();
        if (service is not null)
        {
            if (service.ValidateBuilding(sim, city, b) is { } error) Fail(failures, error);
            return;
        }
        if (b.RequiresReligion || b.RequiresStateReligion || b.ReligionType is not null) Fail(failures, "Religion state is unavailable");
    }
    private static void Unit(Simulation sim, City city, string id, List<RequirementFailure> failures)
    {
        if (!sim.Content.TryGet<S.UnitDef>(id, out var unit)) { Fail(failures, "Unknown unit"); return; }
        Requirements(sim, city, unit, failures);
        if (unit.Inquisitor && sim.Content.Services.Get<IReligionService>().SingleOrDefault() is { } religion && !religion.IsInquisitionEnabled(sim, city.Owner))
            Fail(failures, "Civics and state religion do not allow inquisitions");
        if (unit.Cost < 0) Fail(failures, "Unit cannot be trained");
        if (unit.ForceObsoleteTech is { } obsolete && sim.KnowsTech(city.Owner, obsolete)) Fail(failures, "Unit is obsolete");
        if (unit.Domain == "military:domain_sea" && !CityData.Coastal(sim, city)) Fail(failures, "Requires coast");
        Substitution(sim, city, unit.DefaultUnit, id, true, failures);
        Limits(sim, city, unit.DefaultUnit, unit.Limits, true, failures);
    }
    private static void Requirements(Simulation sim, City city, ContentDef def, List<RequirementFailure> failures) =>
        failures.AddRange(sim.Content.Requirements.Evaluate(def.Requirements, new(sim, city.Owner, city, CityData.Tile(sim, city))).Failures);
    private static void Substitution(Simulation sim, City city, string category, string id, bool unit, List<RequirementFailure> failures)
    {
        string? selected = CityData.ResolveBuilding(sim, city, category);
        if (unit)
        {
            string civilization = sim.State.PlayerById(city.Owner).Civilization;
            if (sim.IsOptionEnabled("foundation:gameoption_assimilation"))
                civilization = CityData.Read(sim, city).OriginalCivilization is { Length: > 0 } original ? original : civilization;
            selected = sim.Content.Get<S.CivilizationDef>(civilization).UnitOverrides.GetValueOrDefault(category, category);
        }
        if (selected != id) Fail(failures, "Civilization class replacement required");
    }
    private static void Limits(Simulation sim, City city, string category, S.ClassLimits limits, bool units, List<RequirementFailure> failures)
    {
        // An unlimited class costs nothing to check, and most of the catalog declares no limit at all. Without this an
        // eligibility pass walked every unit and every city's buildings for each of them.
        if (limits.World < 0 && limits.Team < 0 && limits.Player < 0) return;
        var owners = units ? sim.State.Units.Where(u => sim.Content.Get<S.UnitDef>(u.Type).DefaultUnit == category).Select(u => u.Owner)
            : sim.State.Cities.SelectMany(c => c.Buildings.Where(b => sim.Content.Get<S.BuildingDef>(b).DefaultBuilding == category).Select(_ => c.Owner));
        var all = owners.ToArray();
        CheckLimit(failures, limits.World, all.Length, "WorldLimitReached");
        CheckLimit(failures, limits.Team, all.Count(p => sim.State.PlayerById(p).TeamId == sim.State.PlayerById(city.Owner).TeamId), "TeamLimitReached");
        int playerLimit = limits.Player < 0 ? -1 : limits.Player + limits.ExtraPlayer;
        CheckLimit(failures, playerLimit, all.Count(p => p == city.Owner), "PlayerLimitReached");
    }
    private static void CheckLimit(List<RequirementFailure> failures, int maximum, int count, string reason)
    {
        if (maximum >= 0 && count >= maximum) Fail(failures, reason);
    }
    private static void Location(Simulation sim, City city, S.BuildingDef b, List<RequirementFailure> failures)
    {
        // A coastal check scans the neighbouring plots, and its area size the whole map, so it is only worth making when
        // the building asks for a coast at all.
        bool coast = b.CoastalLocation && CityData.Coastal(sim, city, b.MinAreaSize);
        bool river = sim.State.PlotAt(city.Position).RiverEdges != 0;
        bool location = (b.CoastalLocation && coast) || (b.RiverLocation && river);
        if ((b.CoastalLocation || b.RiverLocation) && !location) Fail(failures, "Requires coast or river location");
        if (city.Population < b.MinimumPopulation) Fail(failures, "Requires population: " + b.MinimumPopulation);
        int latitude = Math.Abs(2 * (CityData.Tile(sim, city).Value / sim.State.Map.Width) - (sim.State.Map.Height - 1)) * 90 / Math.Max(1, sim.State.Map.Height - 1);
        if (latitude < b.MinLatitude || latitude > b.MaxLatitude) Fail(failures, "Invalid latitude");
    }
    private static void WonderCapacity(Simulation sim, City city, S.BuildingDef b, List<RequirementFailure> failures)
    {
        if (b.Limits.NoLimit) return;
        var level = CityData.Level(sim, city);
        var buildings = city.Buildings.Select(sim.Content.Get<S.BuildingDef>).ToArray();
        int extra = buildings.Sum(x => x.WonderCapacityIncrement);
        if (b.Limits.World >= 0) CheckLimit(failures, level.MaxWorldWonders + extra, buildings.Count(x => x.Limits.World >= 0 && !x.Limits.NoLimit), "CityWorldWonderCapacityReached");
        if (b.Limits.Team >= 0) CheckLimit(failures, level.MaxTeamWonders + extra, buildings.Count(x => x.Limits.Team >= 0 && !x.Limits.NoLimit), "CityTeamWonderCapacityReached");
        if (b.Limits.Player >= 0) CheckLimit(failures, level.MaxNationalWonders + extra, buildings.Count(x => x.Limits.Player >= 0 && !x.Limits.NoLimit), "CityNationalWonderCapacityReached");
    }
    private static void Fail(List<RequirementFailure> failures, string reason) => failures.Add(new(null, reason));
}
