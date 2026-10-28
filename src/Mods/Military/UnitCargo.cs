using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Military;

internal sealed partial class UnitService
{
    public int CargoCapacity(Simulation sim, int transport)
    {
        var unit = sim.State.UnitById(transport);
        var def = MilitaryData.Definition(sim, unit);
        return Math.Max(def.Cargo, def.TotalCargo) + MilitaryData.Effect(MilitaryData.Effects(sim, unit), "cargo");
    }
    public IReadOnlyList<int> CargoUnits(Simulation sim, int transport) => sim.State.Units
        .Where(u => u.Health > 0 && MilitaryData.Read(sim, u).TransportId == transport).OrderBy(u => u.Id).Select(u => u.Id).ToArray();

    public string? ValidateLoad(Simulation sim, int player, int unit, int transport)
    {
        var cargo = sim.State.FindUnit(unit);
        if (cargo is null || cargo.Owner != player) return "Not your unit";
        return LoadError(sim, cargo, sim.State.FindUnit(transport), cargo.Position);
    }
    // The destination is explicit so movement queries can check boarding without moving the unit.
    internal static string? LoadError(Simulation sim, Unit unit, Unit? transport, Hex destination)
    {
        if (transport is null || transport.Health <= 0) return "Transport does not exist";
        if (unit.Health <= 0 || unit.MovesLeft <= 0) return "Unit cannot load";
        if (unit.Id == transport.Id) return "Unit cannot carry itself";
        if (!MilitaryData.Friendly(sim, unit.Owner, transport.Owner)) return "Transport is not on your team";
        if (transport.Position != destination) return "Transport is on another plot";
        return CargoCompatibility(sim, unit, transport);
    }
    private static string? CargoCompatibility(Simulation sim, Unit unit, Unit transport)
    {
        if (MilitaryData.Read(sim, unit).TransportId == transport.Id) return "Unit is already aboard";
        var units = sim.Content.Services.Get<IUnitService>().Single();
        if (units.CargoUnits(sim, unit.Id).Count > 0) return "Unit is carrying cargo";
        if (MilitaryData.Read(sim, transport).TransportId is not null) return "Transport is itself cargo";
        if (units.CargoCapacity(sim, transport.Id) <= units.CargoUnits(sim, transport.Id).Count) return "Transport is full";
        var def = MilitaryData.Definition(sim, transport); var cargo = MilitaryData.Definition(sim, unit);
        if (def.CargoDomain is { } domain && domain != cargo.Domain) return "Transport cannot carry this domain";
        if (!def.CargoCategories.IsEmpty && !cargo.Categories.Intersect(def.CargoCategories, StringComparer.Ordinal).Any()) return "Transport cannot carry this category";
        return null;
    }
    public void Load(Simulation sim, int unit, int transport)
    {
        var state = MilitaryData.Edit(sim, sim.State.UnitById(unit));
        state.TransportId = transport; state.Activity = UnitActivity.Sleep; state.FortifyTurns = 0;
        MilitaryData.Sight(sim);
    }
    public string? ValidateUnload(Simulation sim, int player, int unitId, Hex? destination = null)
    {
        var unit = sim.State.FindUnit(unitId);
        if (unit is null || unit.Owner != player) return "Not your unit";
        if (MilitaryData.Read(sim, unit).TransportId is null) return "Unit is not cargo";
        if (unit.MovesLeft <= 0) return "No moves left";
        if (!sim.State.Grid.TryTile(destination ?? unit.Position, out var to)) return "Off map";
        var target = sim.State.Grid.HexOf(to);
        if (sim.State.Grid.Distance(unit.Position, target) > 1) return "Destination is too far away";
        if (target == unit.Position) return UnloadLocation(sim, unit);
        var error = MovementService.TerrainEntry(sim, unit, MilitaryData.Definition(sim, unit), sim.State.Plots[to.Value], MilitaryData.Effects(sim, unit));
        if (error is not null) return error;
        return sim.Content.Services.Get<IMovementService>().Single().CanEnter(sim, unit.Id, sim.State.Grid.TileAt(unit.Position), to);
    }
    private static string? UnloadLocation(Simulation sim, Unit unit)
    {
        var def = MilitaryData.Definition(sim, unit);
        if (def.Domain != "military:domain_air")
            return MovementService.TerrainEntry(sim, unit, def, sim.State.PlotAt(unit.Position), MilitaryData.Effects(sim, unit));
        return AirBaseError(sim, unit, unit.Position);
    }
    internal static string? AirBaseError(Simulation sim, Unit unit, Hex target)
    {
        if (AirBaseCapacity(sim, unit, target) is not { } capacity) return "Aircraft need a friendly base";
        if (MilitaryData.Definition(sim, unit).AirUnitCap <= 0) return null;
        int used = sim.State.UnitsAt(target).Where(u => u.Id != unit.Id && MilitaryData.Friendly(sim, unit.Owner, u.Owner)
            && MilitaryData.Read(sim, u).TransportId is null && MilitaryData.Definition(sim, u).Domain == "military:domain_air")
            .Sum(u => MilitaryData.Definition(sim, u).AirUnitCap);
        return used >= capacity ? "Air base is full" : null;
    }
    private static int? AirBaseCapacity(Simulation sim, Unit unit, Hex target)
    {
        int capacity = MilitaryData.Raw(sim, "CITY_AIR_UNIT_CAPACITY", 4);
        if (sim.State.CityAt(target) is { } city)
            return MilitaryData.Friendly(sim, unit.Owner, city.Owner) ? capacity + MilitaryData.Buildings(sim, city).Sum(b => b.AirUnitCapacity) : null;
        var plot = sim.State.PlotAt(target);
        return MilitaryData.Friendly(sim, unit.Owner, plot.Owner)
            && plot.Improvement is { } id && sim.Content.Get<S.ImprovementDef>(id).ActsAsCity ? capacity : null;
    }
    public void Unload(Simulation sim, int unitId, Hex? destination = null)
    {
        var unit = sim.State.UnitById(unitId);
        var target = sim.State.Grid.Normalize(destination ?? unit.Position);
        var state = MilitaryData.Edit(sim, unit);
        state.TransportId = null; state.Activity = UnitActivity.Awake; state.FortifyTurns = 0;
        if (target != unit.Position)
        {
            sim.State.MoveUnit(unit.Id, target); unit.MovesLeft = 0; state.Moved = true;
        }
        MilitaryData.Sight(sim);
    }
}
