using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Religion;

internal sealed partial class ReligionService
{
    public bool IsInquisitionEnabled(Simulation sim, int player)
    {
        if (StateReligion(sim, player) is null) return false;
        var civics = ReligionData.Civics(sim, player).ToArray();
        return civics.Any(c => c.Rules.AllowInquisitions) && !civics.Any(c => c.Rules.DisallowInquisitions);
    }
    public bool HasInquisitionTargets(Simulation sim, int cityId)
    {
        var city = sim.State.FindCity(cityId);
        if (city is null || StateReligion(sim, city.Owner) is not { } state) return false;
        return ReligionData.Has(sim, city, state) && Present(sim, cityId).Any(r => r != state);
    }
    internal string? ValidateInquisition(Simulation sim, MissionOrder order)
    {
        if (!sim.Content.Get<Ariel.Content.Schema.UnitDef>(order.Unit.Type).Inquisitor) return "Unit cannot perform an inquisition";
        if (!IsInquisitionEnabled(sim, order.Player)) return "Civics and state religion do not allow inquisitions";
        var city = order.City;
        if (sim.TeamOf(city.Owner).Id != sim.TeamOf(order.Player).Id) return "Inquisitor must be in a friendly city";
        if (StateReligion(sim, city.Owner) != StateReligion(sim, order.Player)) return "City owner must share your state religion";
        return HasInquisitionTargets(sim, city.Id) ? null : "No non-state religions to remove";
    }
    internal int InquisitionChance(Simulation sim, MissionOrder order)
    {
        var city = order.City;
        var targets = Present(sim, city.Id).Where(r => r != StateReligion(sim, order.Unit.Owner)).ToArray();
        int holy = targets.Any(r => HolyCity(sim, r) == city.Id) ? 50 : 0;
        return Math.Max(25, 95 - holy - 5 * targets.Length);
    }
    internal void ResolveInquisition(Simulation sim, MissionOrder order, bool success)
    {
        var (unit, city) = (order.Unit, order.City);
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
        sim.Events.Publish(new InquisitionAttempt(unit.Id, city.Id, unit.Owner, success, removed, gold));
    }
    private long PurgeReligions(Simulation sim, City city, string[] religions)
    {
        long gold = 0;
        int speed = sim.Content.Get<GameSpeedDef>(sim.Settings.GameSpeed).ConstructPercent;
        foreach (var building in ReligionData.InvalidatedBuildings(sim, city, religions))
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
        // Where a feature tracks unrest, the compensation calms the purged city while it can; otherwise it is paid.
        if (sim.Rules().CityUnrest?.Relieve(sim, city.Id, gold) == true) return;
        sim.State.PlayerById(player).Treasury += gold;
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
        var relations = sim.Rules().Relations;
        var plot = sim.State.PlotAt(city.Position);
        foreach (var other in sim.State.Players.Where(p => p.Id != player).OrderBy(p => p.Id))
        {
            if (!plot.Revealed.Contains(other.TeamId) && !plot.Visible.Contains(other.TeamId)) continue;
            if (!relations.HasMet(sim, other.TeamId, sim.TeamOf(player).Id)) continue;
            if (removed.Any(r => StateReligion(sim, other.Id) == r || OwnsHolyCity(sim, other.Id, r)))
                relations.Remember(sim, other.Id, player, ReligionMemories.Inquisition);
        }
    }
    private bool OwnsHolyCity(Simulation sim, int player, string religion) =>
        HolyCity(sim, religion) is { } id && sim.State.FindCity(id)?.Owner == player;
}

/// <summary>An inquisitor tries to remove every religion but the state religion from a friendly city. The attempt
/// angers the city and spends the inquisitor either way.</summary>
internal sealed class InquisitionMission(ReligionService service) : IMission
{
    public string Id => ReligionMissions.Inquisition;
    public IEnumerable<string> Subjects(Simulation sim, Unit unit, City city) =>
        sim.Content.Get<Ariel.Content.Schema.UnitDef>(unit.Type).Inquisitor ? [""] : [];
    public string Label(Simulation sim, string subject) => "Perform inquisition";
    public string? Validate(Simulation sim, MissionOrder order) => service.ValidateInquisition(sim, order);
    public MissionTerms Terms(Simulation sim, MissionOrder order) => new(Chance: service.InquisitionChance(sim, order));
    public void Resolve(Simulation sim, MissionOrder order, bool success) => service.ResolveInquisition(sim, order, success);
}
