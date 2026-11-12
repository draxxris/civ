using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Rules.Religion;

/// <summary>A religion is founded by completing its founding wonder: the completing city becomes the holy city and its
/// owner receives the religion's free missionaries.</summary>
internal static class ReligionFoundingRules
{
    internal static void Completed(Simulation sim, BuildingCompleted message)
    {
        if (sim.Content.Get<BuildingDef>(message.Building).FoundsReligion is not { } id) return;
        var state = ReligionData.EditGame(sim);
        // The world limit and building validation keep a second copy from completing; a scenario may still pre-found.
        if (state.Founded.ContainsKey(id)) return;
        var city = sim.State.CityById(message.City);
        var religion = sim.Content.Get<ReligionDef>(id);
        state.Founded[id] = new(city.Id, sim.State.Turn);
        ReligionData.Add(sim, city, id);
        sim.Events.Publish(new ReligionFounded(id, city.Id, city.Owner));
        AwardUnits(sim, sim.State.PlayerById(city.Owner), city, religion);
    }
    private static void AwardUnits(Simulation sim, Player player, City city, ReligionDef religion)
    {
        if (religion.Missionary is not { } defaultUnit) return;
        var civilization = sim.Content.Get<CivilizationDef>(player.Civilization);
        var unitType = civilization.UnitOverrides.GetValueOrDefault(defaultUnit, defaultUnit);
        if (unitType is null) return;
        // Full health is the Unit default; moves must be set here or the award is immobile until the next refill.
        var def = sim.Content.Get<UnitDef>(unitType);
        int moves = def.Moves * sim.Content.Get<RuleConstantsDef>().All.Single().Values.MoveDenominator;
        for (int i = 0; i < religion.FreeUnits; i++)
        {
            var unit = new Unit { Id = sim.State.NextUnitId++, Owner = player.Id, Type = unitType, Position = city.Position, MovesLeft = moves };
            sim.State.AddUnit(unit);
            sim.Events.Publish(new UnitCreated(unit.Id, player.Id, unit.Type, city.Position));
        }
    }
}
