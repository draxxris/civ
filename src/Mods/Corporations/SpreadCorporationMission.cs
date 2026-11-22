using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Corporations;

/// <summary>An executive opens a branch in the city it stands in, own or foreign, for the spread cost.</summary>
internal sealed class SpreadCorporationMission(CorporationService service) : IMission
{
    public string Id => CorporationMissions.Spread;
    public IEnumerable<string> Subjects(Simulation sim, Unit unit, City? city) =>
        sim.Content.Get<CorporationDef>().All.Where(def => CorporationService.Spreads(sim, unit, def)).Select(def => def.Id);
    public string Label(Simulation sim, string subject) => "Spread " + sim.Content.Get<CorporationDef>(subject).Name;
    public string? Validate(Simulation sim, MissionOrder order) =>
        service.ValidateSpread(sim, order.Player, order.City.Id, order.Subject, order.Unit);
    public MissionTerms Terms(Simulation sim, MissionOrder order) =>
        new(Note: $"{sim.Content.Get<CorporationDef>(order.Subject).SpreadCost} gold");
    public void Resolve(Simulation sim, MissionOrder order, bool success) =>
        service.OpenBranch(sim, order.Player, order.City, order.Subject);
}
