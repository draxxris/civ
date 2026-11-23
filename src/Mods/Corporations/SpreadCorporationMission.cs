using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Corporations;

/// <summary>An executive tries to open a branch in the city it stands in, own or foreign. It pays the spread cost and is
/// spent either way; the branch opens with the source's spread chance (<see cref="CorporationService.SpreadChance"/>).</summary>
internal sealed class SpreadCorporationMission(CorporationService service) : IMission
{
    public string Id => CorporationMissions.Spread;
    public IEnumerable<string> Subjects(Simulation sim, Unit unit, City? city) =>
        sim.Content.Get<CorporationDef>().All.Where(def => CorporationService.Spreads(sim, unit, def)).Select(def => def.Id);
    public string Label(Simulation sim, string subject) => "Spread " + sim.Content.Get<CorporationDef>(subject).Name;
    public string? Validate(Simulation sim, MissionOrder order) =>
        service.ValidateSpread(sim, order.Player, order.City.Id, order.Subject, order.Unit);
    public MissionTerms Terms(Simulation sim, MissionOrder order)
    {
        var def = sim.Content.Get<CorporationDef>(order.Subject);
        return new(Chance: service.SpreadChance(sim, order.Unit, order.City, def), Note: $"{service.SpreadCost(sim, order.Player, order.City, def)} gold");
    }
    public void Resolve(Simulation sim, MissionOrder order, bool success) =>
        service.SpendOnBranch(sim, order.Player, order.City, order.Subject, success);
}
