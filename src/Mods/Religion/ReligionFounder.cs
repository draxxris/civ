using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Religion;

/// <summary>A religion is founded by completing its founding wonder (<see cref="BuildingDef.FoundsReligion"/>): the
/// completing city becomes the holy city and its owner receives the religion's free missionaries. Cities offers the
/// wonder once its technology is known and refuses it to barbarians; this founder refuses it once the religion is
/// founded.</summary>
internal sealed class ReligionFounder : IBuildingFounder
{
    public bool Claims(ContentDb content, BuildingDef building) => ReligionIdentities.Religion(content, building.FoundsReligion) is not null;

    public string? Refuse(Simulation sim, City city, BuildingDef building) =>
        ReligionData.Game(sim).Founded.ContainsKey(ReligionIdentities.Religion(sim.Content, building.FoundsReligion)!) ? "Religion is already founded" : null;

    public void Found(Simulation sim, City city, BuildingDef building)
    {
        var id = ReligionIdentities.Religion(sim.Content, building.FoundsReligion)!;
        var state = ReligionData.EditGame(sim);
        // The world limit and building validation keep a second copy from completing; a scenario may still pre-found.
        if (state.Founded.ContainsKey(id)) return;
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
        for (int i = 0; i < religion.FreeUnits; i++) sim.Rules().Units.Create(sim, player.Id, unitType, city.Position);
    }
}
