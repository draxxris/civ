using Ariel.Content;
using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Rules.Religion;

/// <summary>A prophet builds a religion's shrine in its holy city, skipping the production cost.</summary>
internal sealed class ShrineMission(ReligionService service) : IMission
{
    public string Id => ReligionMissions.Shrine;

    public IEnumerable<string> Subjects(Simulation sim, Unit unit, City city) =>
        sim.Content.Get<S.UnitDef>(unit.Type).ConstructibleBuildings.Where(p => p.Value).Select(p => p.Key)
            .Where(id => sim.Content.TryGet<S.BuildingDef>(id, out var building) && building.GlobalReligionCommerce is not null)
            .Order(StringComparer.Ordinal);

    public string Label(Simulation sim, string subject) => "Build " + sim.Content.Get<S.BuildingDef>(subject).Name;

    public string? Validate(Simulation sim, MissionOrder order)
    {
        if (order.City.Owner != order.Player) return "Unit must be in your city";
        if (!sim.Content.TryGet<S.BuildingDef>(order.Subject, out var def) || def.GlobalReligionCommerce is null) return "Not a shrine";
        if (!sim.Content.Get<S.UnitDef>(order.Unit.Type).ConstructibleBuildings.GetValueOrDefault(order.Subject))
            return "Unit cannot construct this shrine";
        if (service.HolyCity(sim, def.GlobalReligionCommerce) != order.City.Id) return "Requires the holy city";
        // Prophets bypass the negative production cost, not requirements, replacements or class limits.
        var result = sim.Rules().CityProduction.Validate(sim, order.City.Id, "cities:building", def.Id);
        return result.Failures.FirstOrDefault(f => !ProductionFailures.IsCostOnly(f))?.Explanation;
    }

    public MissionTerms Terms(Simulation sim, MissionOrder order) => new();

    public void Resolve(Simulation sim, MissionOrder order, bool success)
    {
        order.City.Buildings.Add(order.Subject);
        sim.Events.Publish(new BuildingCompleted(order.City.Id, order.Subject));
    }
}
