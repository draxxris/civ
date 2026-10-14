using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.Religion;

internal sealed partial class ReligionService
{
    public string? ValidateMissionary(Simulation sim, int player, int unitId, string religion)
    {
        var unit = sim.State.FindUnit(unitId);
        if (unit is null || unit.Owner != player) return "Not your missionary";
        if (unit.MovesLeft <= 0) return "No moves left";
        if (!sim.Content.TryGet<S.ReligionDef>(religion, out _)) return "Unknown religion";
        var def = sim.Content.Get<S.UnitDef>(unit.Type);
        if (def.DefaultUnitAi != "military:unitai_missionary" || def.ReligionSpreads.GetValueOrDefault(religion) <= 0) return "Unit cannot spread this religion";
        var city = sim.State.CityAt(unit.Position);
        if (city is null) return "Missionary must be in a city";
        if (ReligionData.Has(sim, city, religion)) return "Religion is already present";
        if (ForeignSpreadBlocked(sim, player, city, religion)) return "Foreign civics prohibit this religion";
        return null;
    }
    private static bool ForeignSpreadBlocked(Simulation sim, int player, City city, string religion) =>
        sim.TeamOf(city.Owner).Id != sim.TeamOf(player).Id && ReligionData.BlocksSpread(sim, city, religion);
    public int MissionaryChance(Simulation sim, int unitId, string religion)
    {
        var unit = sim.State.UnitById(unitId);
        var city = sim.State.CityAt(unit.Position);
        if (city is null) return 0;
        int probability = sim.Content.Get<S.UnitDef>(unit.Type).ReligionSpreads.GetValueOrDefault(religion);
        if (sim.TeamOf(city.Owner).Id != sim.TeamOf(unit.Owner).Id) probability /= 2;
        int religions = sim.Content.Get<S.ReligionDef>().Count;
        probability += (religions - ReligionData.Present(sim, city).Count) * (100 - probability) / Math.Max(1, religions);
        return Math.Clamp(probability, 0, 100);
    }
    public void SpreadMissionary(Simulation sim, int unitId, string religion)
    {
        var unit = sim.State.UnitById(unitId);
        var city = sim.State.CityAt(unit.Position)!;
        bool success = sim.Rng.Next(100) < MissionaryChance(sim, unitId, religion);
        if (success) ReligionData.Add(sim, city, religion);
        sim.Events.Publish(new MissionaryAttempt(unit.Id, city.Id, religion, success));
        sim.State.RemoveUnit(unitId);
        sim.Content.Services.Get<IVisibilityService>().SingleOrDefault()?.Recompute(sim);
    }
}
