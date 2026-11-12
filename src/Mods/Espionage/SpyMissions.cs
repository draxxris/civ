using Ariel.Content.Schema;
using ModifierSink = Ariel.Content.ModifierSink;
using ModifierScope = Ariel.Content.ModifierScope;
using Ariel.Game;

namespace Ariel.Mods.Espionage;

/// <summary>Mission pricing, validation and execution. Owns no state: it reads and writes the service's saved
/// components, so a mission can only be run through the same paths a query uses.</summary>
internal sealed class SpyMissionRules(EspionageService service)
{
    public long MissionCost(Simulation sim, int player, int city, EspionageMissionKind mission, string? tech = null)
    {
        var rules = EspionageService.Rules(sim);
        long baseline = mission switch
        {
            EspionageMissionKind.StealTreasury => rules.StealTreasuryCost,
            EspionageMissionKind.SabotageProduction => rules.SabotageProductionCost,
            EspionageMissionKind.InciteRevolt => rules.InciteRevoltCost,
            EspionageMissionKind.FomentUnhappiness => rules.FomentUnhappinessCost,
            EspionageMissionKind.CounterEspionage => rules.CounterEspionageCost,
            EspionageMissionKind.StealTech => EspionageService.Percent(ResearchCost(sim, player, tech), rules.StealTechCostPercent),
            _ => throw new ArgumentOutOfRangeException(nameof(mission))
        };
        int target = sim.TeamOf(sim.State.CityById(city).Owner).Id;
        return Math.Max(1, EspionageService.Percent(baseline, EspionageService.CostModifier(sim, sim.TeamOf(player).Id, target)));
    }

    /// <summary>A technology steal without a technology cannot be priced. Callers that offer the mission must
    /// supply one; <see cref="ValidateMission"/> rejects the mission before it is costed.</summary>
    private static long ResearchCost(Simulation sim, int player, string? tech) => tech is not null
        ? sim.Content.Services.Get<IResearchService>().Single().CalculateCost(sim, sim.TeamOf(player).Id, tech).Cost
        : throw new ArgumentException("Technology theft requires a technology id", nameof(tech));

    public string? ValidateMission(Simulation sim, int player, int unitId, int cityId, EspionageMissionKind mission, string? tech = null) =>
        ValidateMissionCore(sim, player, unitId, cityId, mission, tech, out _);

    /// <summary>Validate once and hand the priced mission to the caller, so a command pays for one cost calculation.</summary>
    private string? ValidateMissionCore(Simulation sim, int player, int unitId, int cityId, EspionageMissionKind mission,
        string? tech, out long cost)
    {
        cost = 0;
        if (!Enum.IsDefined(mission)) return "Unknown espionage mission";
        if (ValidateSpyLocation(sim, player, unitId, cityId) is { } locationError) return locationError;
        var city = sim.State.CityById(cityId);
        if (MissionAvailability(sim, player, city, mission, tech) is { } error) return error;
        cost = MissionCost(sim, player, cityId, mission, tech);
        if (service.Points(sim, sim.TeamOf(player).Id, sim.TeamOf(city.Owner).Id) < cost) return "Insufficient espionage points";
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
        EspionageMissionKind.CounterEspionage => service.CounterEspionage(sim, sim.TeamOf(player).Id, sim.TeamOf(city.Owner).Id) is not null
            ? "Counter-espionage is already active against this team" : null,
        EspionageMissionKind.StealTreasury => sim.State.PlayerById(city.Owner).Treasury <= 0 ? "Target treasury is empty" : null,
        EspionageMissionKind.SabotageProduction => (city.ProductionQueue.FirstOrDefault()?.Progress ?? Math.Max(city.BuildProgress, city.TrainProgress)) <= 0
            ? "No production to sabotage" : null,
        // A revolt is a state, not an event: inciting one in a city that is already in disorder cannot stack.
        EspionageMissionKind.InciteRevolt => InRevolt(sim, city) ? "The city is already in revolt" : null,
        EspionageMissionKind.StealTech => ValidateStolenTech(sim, player, city, tech),
        _ => null
    };

    private static bool InRevolt(Simulation sim, City city) =>
        GovernmentStatus.IsDisorder(sim, city)
        || (city.Extensions.Get<EspionageCityState>(sim.Content) is { RevoltUntilTurn: > 0 } state && sim.State.Turn < state.RevoltUntilTurn);

    private static string? ValidateStolenTech(Simulation sim, int player, City city, string? tech)
    {
        if (tech is null || !sim.Content.TryGet<TechDef>(tech, out _)) return "Choose a technology";
        if (!sim.TeamOf(city.Owner).Techs.Contains(tech) || sim.TeamOf(player).Techs.Contains(tech)) return "Technology must be known only by the target team";
        return sim.Content.Services.Get<IResearchService>().Single().Validate(sim, player, tech).Success ? null : "Technology prerequisites are not met";
    }

    public IReadOnlyList<string> StealableTechnologies(Simulation sim, int player, int city)
    {
        var target = sim.State.FindCity(city);
        if (target is null) return [];
        return sim.TeamOf(target.Owner).Techs.Order(StringComparer.Ordinal)
            .Where(tech => ValidateStolenTech(sim, player, target, tech) is null).ToArray();
    }

    public void ExecuteMission(Simulation sim, int player, int unitId, int cityId, EspionageMissionKind mission, string? tech = null)
    {
        if (ValidateMissionCore(sim, player, unitId, cityId, mission, tech, out long cost) is { } error) throw new InvalidOperationException(error);
        var city = sim.State.CityById(cityId);
        int chance = service.SuccessChance(sim, player, cityId);
        var team = EspionageService.Edit(sim, sim.TeamOf(player).Id);
        int target = sim.TeamOf(city.Owner).Id;
        team.Points[target] = checked(team.Points[target] - cost);
        bool success = sim.Rng.Next(100) < chance;
        if (success)
        {
            // A technology theft awards the technology's own research cost, priced once here.
            long effect = mission == EspionageMissionKind.StealTech ? ResearchCost(sim, player, tech) : cost;
            ApplyEffect(sim, player, sim.State.UnitById(unitId), city, mission, tech, effect);
            // Nearest owned city: success is still a retreat, but not a free intercontinental extraction.
            var home = NearestOwnedCity(sim, player, city.Position);
            sim.State.MoveUnit(unitId, home.Position);
            sim.State.UnitById(unitId).MovesLeft = 0;
        }
        else sim.State.RemoveUnit(unitId);
        sim.Events.Publish(new EspionageMissionResult(unitId, cityId, mission, success, cost, player));
        sim.Content.Services.Get<IVisibilityService>().SingleOrDefault()?.Recompute(sim);
    }

    /// <summary>Nearest owned city by hex distance; ties go to the lower city id. Validation guarantees one exists.</summary>
    private static City NearestOwnedCity(Simulation sim, int player, Hex from) =>
        sim.State.Cities.Where(c => c.Owner == player)
            .OrderBy(c => sim.State.Grid.Distance(from, c.Position)).ThenBy(c => c.Id).First();

    private static void ApplyEffect(Simulation sim, int player, Unit spy, City city, EspionageMissionKind mission, string? tech, long amount)
    {
        var rules = EspionageService.Rules(sim);
        switch (mission)
        {
            case EspionageMissionKind.StealTreasury:
                var victim = sim.State.PlayerById(city.Owner);
                long gold = Math.Min(victim.Treasury, Math.Max(1, EspionageService.Percent(victim.Treasury, rules.StealTreasuryPercent)));
                victim.Treasury -= gold; sim.State.PlayerById(player).Treasury = checked(sim.State.PlayerById(player).Treasury + gold);
                break;
            case EspionageMissionKind.SabotageProduction:
                SabotageProduction(city, rules.SabotagePercent);
                break;
            case EspionageMissionKind.CounterEspionage:
                EspionageService.Edit(sim, sim.TeamOf(player).Id).CounterEspionage[sim.TeamOf(city.Owner).Id] =
                    new(checked(sim.State.Turn + GamePace.Scale(sim, rules.CounterEspionageTurns)),
                        checked(rules.CounterEspionagePercent + rules.CounterEspionagePerInterception * SpyInterception(sim, spy)));
                break;
            case EspionageMissionKind.StealTech:
                sim.Content.Services.Get<IResearchService>().Single().AddProgress(sim, player, tech!, amount);
                break;
            case EspionageMissionKind.InciteRevolt:
                // Tell the Revolution mod first: it refuses cities that are already in disorder, and this mission
                // is about to make this one disordered.
                sim.Content.Services.Get<IRevolutionService>().SingleOrDefault()?.Incite(sim, city.Id);
                var state = CityState(sim, city);
                state.RevoltUntilTurn = checked(sim.State.Turn + GamePace.Scale(sim, rules.InciteRevoltTurns));
                var cityRules = city.Extensions.Get<CityRuleState>(sim.Content) ?? new();
                city.Extensions.Set(sim.Content, cityRules);
                cityRules.Disorder = true;
                break;
            case EspionageMissionKind.FomentUnhappiness:
                CityState(sim, city).UnhappyUntilTurn = checked(sim.State.Turn + GamePace.Scale(sim, rules.FomentUnhappinessTurns));
                break;
        }
    }

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
        // Spies have one hit point: a wounded spy is a dead spy, so health never discounts interception.
        return probability;
    }

    private static void SabotageProduction(City city, int percent)
    {
        if (city.ProductionQueue.Count > 0) city.ProductionQueue[0] = city.ProductionQueue[0] with { Progress = city.ProductionQueue[0].Progress - EspionageService.Percent(city.ProductionQueue[0].Progress, percent) };
        else
        {
            city.BuildProgress -= EspionageService.Percent(city.BuildProgress, percent);
            city.TrainProgress -= EspionageService.Percent(city.TrainProgress, percent);
        }
    }

    private static EspionageCityState CityState(Simulation sim, City city)
    {
        var state = city.Extensions.Get<EspionageCityState>(sim.Content) ?? new(); city.Extensions.Set(sim.Content, state); return state;
    }
}
