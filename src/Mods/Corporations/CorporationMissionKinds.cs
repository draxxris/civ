using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Corporations;

/// <summary>A great person founds a corporation in the city it stands in, which becomes the headquarters.</summary>
internal sealed class FoundCorporationMission(CorporationService service) : IMission
{
    public string Id => CorporationMissions.Found;
    public IEnumerable<string> Subjects(Simulation sim, Unit unit, City city) =>
        sim.Content.Get<CorporationDef>().All.Where(def => CorporationService.Founds(sim, unit, def)).Select(def => def.Id);
    public string Label(Simulation sim, string subject) => "Found " + sim.Content.Get<CorporationDef>(subject).Name;
    public string? Validate(Simulation sim, MissionOrder order) => service.ValidateFound(sim, order);
    public MissionTerms Terms(Simulation sim, MissionOrder order) => new(Note: sim.Content.Get<CorporationDef>(order.Subject).Help);
    public void Resolve(Simulation sim, MissionOrder order, bool success) => service.Found(sim, order);
}

/// <summary>An executive opens a branch in the city it stands in, own or foreign, for the spread cost.</summary>
internal sealed class SpreadCorporationMission(CorporationService service) : IMission
{
    public string Id => CorporationMissions.Spread;
    public IEnumerable<string> Subjects(Simulation sim, Unit unit, City city) =>
        sim.Content.Get<CorporationDef>().All.Where(def => CorporationService.Spreads(sim, unit, def)).Select(def => def.Id);
    public string Label(Simulation sim, string subject) => "Spread " + sim.Content.Get<CorporationDef>(subject).Name;
    public string? Validate(Simulation sim, MissionOrder order) =>
        service.ValidateSpread(sim, order.Player, order.City.Id, order.Subject, order.Unit);
    public MissionTerms Terms(Simulation sim, MissionOrder order) =>
        new(Note: $"{sim.Content.Get<CorporationDef>(order.Subject).SpreadCost} gold");
    public void Resolve(Simulation sim, MissionOrder order, bool success) =>
        service.OpenBranch(sim, order.Player, order.City, order.Subject);
}
