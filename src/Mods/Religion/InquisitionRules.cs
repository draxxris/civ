using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Religion;

internal sealed partial class ReligionService
{
    public bool IsInquisitionEnabled(Simulation sim, int player)
    {
        if (StateReligion(sim, player) is null) return false;
        var civics = ReligionData.Civics(sim, player).ToArray();
        return civics.Any(c => c.AllowInquisitions) && !civics.Any(c => c.DisallowInquisitions);
    }
    public bool HasInquisitionTargets(Simulation sim, int cityId)
    {
        var city = sim.State.FindCity(cityId);
        if (city is null || StateReligion(sim, city.Owner) is not { } state) return false;
        return ReligionData.Has(sim, city, state) && Present(sim, cityId).Any(r => r != state);
    }
    public string? ValidateInquisition(Simulation sim, int player, int unitId)
    {
        var unit = sim.State.FindUnit(unitId);
        if (unit is null || unit.Owner != player || unit.Health <= 0) return "Not your inquisitor";
        if (!sim.Content.Get<Ariel.Content.Schema.UnitDef>(unit.Type).Inquisitor) return "Unit cannot perform an inquisition";
        if (unit.MovesLeft <= 0) return "No moves left";
        if (!IsInquisitionEnabled(sim, player)) return "Civics and state religion do not allow inquisitions";
        var city = sim.State.CityAt(unit.Position);
        return ValidateInquisitionCity(sim, player, city);
    }
    private string? ValidateInquisitionCity(Simulation sim, int player, City? city)
    {
        if (city is null || sim.TeamOf(city.Owner).Id != sim.TeamOf(player).Id) return "Inquisitor must be in a friendly city";
        if (StateReligion(sim, city.Owner) != StateReligion(sim, player)) return "City owner must share your state religion";
        return HasInquisitionTargets(sim, city.Id) ? null : "No non-state religions to remove";
    }
    public int InquisitionChance(Simulation sim, int unitId)
    {
        var unit = sim.State.FindUnit(unitId);
        if (unit is null || ValidateInquisition(sim, unit.Owner, unitId) is not null) return 0;
        var city = sim.State.CityAt(unit.Position)!;
        var targets = Present(sim, city.Id).Where(r => r != StateReligion(sim, unit.Owner)).ToArray();
        int holy = targets.Any(r => HolyCity(sim, r) == city.Id) ? 50 : 0;
        return Math.Max(25, 95 - holy - 5 * targets.Length);
    }
    public void PerformInquisition(Simulation sim, int unitId)
    {
        var unit = sim.State.UnitById(unitId);
        var city = sim.State.CityAt(unit.Position)!;
        bool success = sim.Rng.Next(100) < InquisitionChance(sim, unitId);
        string[] removed = [];
        long gold = 0;
        if (success)
        {
            removed = Present(sim, city.Id).Where(r => r != StateReligion(sim, unit.Owner)).ToArray();
            RecordInquisitionMemory(sim, unit.Owner, city, removed);
            gold = PurgeReligions(sim, city, removed);
            CompensateInquisition(sim, unit.Owner, city, gold);
        }
        AddInquisitionAnger(sim, city);
        sim.Events.Publish(new InquisitionAttempt(unitId, city.Id, unit.Owner, success, removed, gold));
        sim.State.RemoveUnit(unitId);
        sim.Content.Services.Get<IVisibilityService>().SingleOrDefault()?.Recompute(sim);
    }
    private long PurgeReligions(Simulation sim, City city, string[] religions)
    {
        long gold = 0;
        int speed = sim.Content.Get<GameSpeedDef>(sim.Settings.GameSpeed).ConstructPercent;
        foreach (var building in ReligionData.ReligiousBuildings(sim, city, religions))
        {
            city.Buildings.Remove(building.Id);
            gold += (long)Math.Max(0, building.Cost) * speed / Math.Max(1, ReligionData.Raw(sim, "INQUISITION_BUILDING_GOLD_DIVISOR", 4));
        }
        foreach (string religion in religions)
        {
            bool holy = HolyCity(sim, religion) == city.Id;
            ReligionData.Remove(sim, city, religion, "inquisition");
            gold += holy ? ReligionData.Raw(sim, "HOLYCITY_REMOVAL_GOLD", 500) : ReligionData.Raw(sim, "RELIGION_REMOVAL_GOLD", 50);
            if (holy) RelocateHolyCity(sim, religion);
        }
        return gold;
    }
    private static void RelocateHolyCity(Simulation sim, string religion)
    {
        var candidates = sim.State.Cities.Where(c => ReligionData.Has(sim, c, religion))
            .GroupBy(c => c.Owner).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).FirstOrDefault();
        int destination = candidates?.OrderBy(c => c.Id).First().Id ?? -1;
        var game = ReligionData.EditGame(sim);
        game.Founded[religion] = game.Founded[religion] with { HolyCity = destination };
    }
    private static void CompensateInquisition(Simulation sim, int player, City city, long gold)
    {
        if (!sim.Content.Services.Get<IRevolutionService>().Any())
        {
            sim.State.PlayerById(player).Treasury += gold;
            return;
        }
        var state = city.Extensions.Get<RevolutionCityState>(sim.Content);
        if (state is not null) state.InstabilityIndex = (int)Math.Max(0, state.InstabilityIndex - gold);
    }
    private static void AddInquisitionAnger(Simulation sim, City city)
    {
        var state = city.Extensions.Get<EconomyCityState>(sim.Content) ?? new();
        long anger = sim.Content.Get<RuleConstantsDef>().All.Single().Values.HurryAngerDivisor;
        anger = anger * sim.Content.Get<GameSpeedDef>(sim.Settings.GameSpeed).HurryConscriptAngerPercent / 100;
        anger = anger * Math.Max(0, 100 + ReligionData.Active(sim, city).Sum(b => b.HurryAngerModifier)) / 100;
        state.HurryAngerTurns += (int)Math.Max(1, anger);
        city.Extensions.Set(sim.Content, state);
    }
    private void RecordInquisitionMemory(Simulation sim, int player, City city, string[] removed)
    {
        var diplomacy = sim.Content.Services.Get<IDiplomacyService>().SingleOrDefault();
        if (diplomacy is null) return;
        var plot = sim.State.PlotAt(city.Position);
        foreach (var other in sim.State.Players.Where(p => p.Id != player).OrderBy(p => p.Id))
        {
            if (!plot.Revealed.Contains(other.TeamId) && !plot.Visible.Contains(other.TeamId)) continue;
            if (!diplomacy.HasMet(sim, other.TeamId, sim.TeamOf(player).Id)) continue;
            if (removed.Any(r => StateReligion(sim, other.Id) == r || OwnsHolyCity(sim, other.Id, r)))
                diplomacy.RecordMemory(sim, other.Id, player, "memory_inquisition");
        }
    }
    private bool OwnsHolyCity(Simulation sim, int player, string religion) =>
        HolyCity(sim, religion) is { } id && sim.State.FindCity(id)?.Owner == player;
}
