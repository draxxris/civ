using Ariel.Content.Schema;
using ModifierSink = Ariel.Content.ModifierSink;
using ModifierScope = Ariel.Content.ModifierScope;
using Ariel.Game;

namespace Ariel.Mods.Espionage;

internal sealed partial class EspionageService
{
    public long MissionCost(Simulation sim, int player, int city, EspionageMissionKind mission, string? tech = null)
    {
        long baseline = mission switch
        {
            EspionageMissionKind.StealTreasury => 50,
            EspionageMissionKind.SabotageProduction => 80,
            EspionageMissionKind.InciteRevolt => 100,
            EspionageMissionKind.FomentUnhappiness => 60,
            EspionageMissionKind.CounterEspionage => ResearchSpeedPercent(sim),
            EspionageMissionKind.StealTech => tech is not null ? sim.Content.Services.Get<IResearchService>().Single().CalculateCost(sim, sim.TeamOf(player).Id, tech).Cost * 2 : 0,
            _ => throw new ArgumentOutOfRangeException(nameof(mission))
        };
        return Math.Max(1, checked(baseline * CostModifier(sim, sim.TeamOf(player).Id, sim.TeamOf(sim.State.CityById(city).Owner).Id)) / 100);
    }
    public string? ValidateMission(Simulation sim, int player, int unitId, int cityId, EspionageMissionKind mission, string? tech = null)
    {
        if (!Enum.IsDefined(mission)) return "Unknown espionage mission";
        if (ValidateSpyLocation(sim, player, unitId, cityId) is { } locationError) return locationError;
        var city = sim.State.CityById(cityId);
        if (MissionAvailability(sim, player, city, mission, tech) is { } error) return error;
        if (Points(sim, sim.TeamOf(player).Id, sim.TeamOf(city.Owner).Id) < MissionCost(sim, player, cityId, mission, tech)) return "Insufficient espionage points";
        if (!sim.State.Cities.Any(c => c.Owner == player)) return "Requires a return city";
        return null;
    }
    private static string? ValidateSpyLocation(Simulation sim, int player, int unitId, int cityId)
    {
        var unit = sim.State.FindUnit(unitId);
        if (unit is null || unit.Owner != player || !sim.Content.Get<Ariel.Content.Schema.UnitDef>(unit.Type).Spy) return "Requires your spy unit";
        if (unit.MovesLeft <= 0) return "No moves left";
        var city = sim.State.FindCity(cityId);
        if (city is null || city.Position != unit.Position) return "Spy must be in the target city";
        if (sim.TeamOf(city.Owner).Id == sim.TeamOf(player).Id) return "Requires a rival city";
        var diplomacy = sim.Content.Services.Get<IDiplomacyService>().SingleOrDefault();
        return diplomacy is not null && !diplomacy.HasMet(sim, sim.TeamOf(player).Id, sim.TeamOf(city.Owner).Id) ? "Requires a known rival team" : null;
    }
    private string? MissionAvailability(Simulation sim, int player, City city, EspionageMissionKind mission, string? tech) => mission switch
    {
        EspionageMissionKind.CounterEspionage => CounterEspionage(sim, sim.TeamOf(player).Id, sim.TeamOf(city.Owner).Id) is not null
            ? "Counter-espionage is already active against this team" : null,
        EspionageMissionKind.StealTreasury => sim.State.PlayerById(city.Owner).Treasury <= 0 ? "Target treasury is empty" : null,
        EspionageMissionKind.SabotageProduction => (city.ProductionQueue.FirstOrDefault()?.Progress ?? Math.Max(city.BuildProgress, city.TrainProgress)) <= 0
            ? "No production to sabotage" : null,
        EspionageMissionKind.StealTech => ValidateStolenTech(sim, player, city, tech),
        _ => null
    };
    private static string? ValidateStolenTech(Simulation sim, int player, City city, string? tech)
    {
        if (tech is null || !sim.Content.TryGet<TechDef>(tech, out _)) return "Choose a technology";
        if (!sim.TeamOf(city.Owner).Techs.Contains(tech) || sim.TeamOf(player).Techs.Contains(tech)) return "Technology must be known only by the target team";
        return sim.Content.Services.Get<IResearchService>().Single().Validate(sim, player, tech).Success ? null : "Technology prerequisites are not met";
    }
    public void ExecuteMission(Simulation sim, int player, int unitId, int cityId, EspionageMissionKind mission, string? tech = null)
    {
        if (ValidateMission(sim, player, unitId, cityId, mission, tech) is { } error) throw new InvalidOperationException(error);
        var city = sim.State.CityById(cityId);
        long cost = MissionCost(sim, player, cityId, mission, tech);
        int chance = SuccessChance(sim, player, cityId);
        var team = Edit(sim, sim.TeamOf(player).Id);
        int target = sim.TeamOf(city.Owner).Id;
        team.Points[target] -= cost;
        team.Spent[target] = team.Spent.GetValueOrDefault(target) + cost;
        bool success = sim.Rng.Next(100) < chance;
        if (success)
        {
            ApplyEffect(sim, player, sim.State.UnitById(unitId), city, mission, tech);
            var capital = sim.State.Cities.Where(c => c.Owner == player).OrderByDescending(c => c.Buildings.Any(b => sim.Content.Get<BuildingDef>(b).Capital)).ThenBy(c => c.Id).First();
            sim.State.MoveUnit(unitId, capital.Position);
            sim.State.UnitById(unitId).MovesLeft = 0;
        }
        else sim.State.RemoveUnit(unitId);
        sim.Events.Publish(new EspionageMissionResult(unitId, cityId, mission, success, cost));
        sim.Content.Services.Get<IVisibilityService>().SingleOrDefault()?.Recompute(sim);
    }
    private static void ApplyEffect(Simulation sim, int player, Unit spy, City city, EspionageMissionKind mission, string? tech)
    {
        switch (mission)
        {
            case EspionageMissionKind.StealTreasury:
                var victim = sim.State.PlayerById(city.Owner);
                long gold = Math.Min(victim.Treasury, Math.Max(1, victim.Treasury / 4));
                victim.Treasury -= gold; sim.State.PlayerById(player).Treasury += gold;
                break;
            case EspionageMissionKind.SabotageProduction:
                SabotageProduction(city);
                break;
            case EspionageMissionKind.CounterEspionage:
                int duration = checked((int)(10L * ResearchSpeedPercent(sim) / 100));
                Edit(sim, sim.TeamOf(player).Id).CounterEspionage[sim.TeamOf(city.Owner).Id] =
                    new(checked(sim.State.Turn + duration), checked(200 + 5 * SpyInterception(sim, spy)));
                break;
            case EspionageMissionKind.StealTech:
                var research = sim.Content.Services.Get<IResearchService>().Single();
                research.AddProgress(sim, player, tech!, research.CalculateCost(sim, sim.TeamOf(player).Id, tech!).Cost);
                break;
            case EspionageMissionKind.InciteRevolt:
                var revolt = CityState(sim, city);
                revolt.RevoltUntilTurn = sim.State.Turn + 1;
                var rules = city.Extensions.Get<CityRuleState>(sim.Content) ?? new(); city.Extensions.Set(sim.Content, rules);
                if (!rules.Disorder) { revolt.OwnsDisorder = true; rules.Disorder = true; }
                break;
            case EspionageMissionKind.FomentUnhappiness:
                CityState(sim, city).UnhappyUntilTurn = sim.State.Turn + 3;
                break;
        }
    }
    private static int ResearchSpeedPercent(Simulation sim) => sim.Content.Get<GameSpeedDef>(sim.Settings.GameSpeed).ResearchPercent;

    // Native land spies use maximum interception; only air units scale it by health by default.
    // Commander bonuses and the optional ground-health interception setting have no contracts here.
    private static int SpyInterception(Simulation sim, Unit spy)
    {
        var def = sim.Content.Get<UnitDef>(spy.Type);
        var effects = ModifierSink.Collect(new(sim, ModifierScope.Unit, spy.Owner, sim.State.CityAt(spy.Position),
            sim.State.Grid.TileAt(spy.Position), spy, sim.TeamOf(spy.Owner)));
        var constants = sim.Content.Get<RuleConstantsDef>().All.Single();
        int maximum = constants.Raw.TryGetValue("MAX_INTERCEPTION_PROBABILITY", out var raw)
            && int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int value) ? value : 100;
        int probability = checked((int)Math.Clamp(def.InterceptionPercent + effects.Ledger.Total("military:intercept"), 0, maximum));
        return def.Domain == "military:domain_air" ? checked(probability * spy.Health) / constants.Values.MaxHitPoints : probability;
    }
    private static void SabotageProduction(City city)
    {
        if (city.ProductionQueue.Count > 0) city.ProductionQueue[0] = city.ProductionQueue[0] with { Progress = city.ProductionQueue[0].Progress / 2 };
        else { city.BuildProgress /= 2; city.TrainProgress /= 2; }
    }
    private static EspionageCityState CityState(Simulation sim, City city)
    {
        var state = city.Extensions.Get<EspionageCityState>(sim.Content) ?? new(); city.Extensions.Set(sim.Content, state); return state;
    }
}
