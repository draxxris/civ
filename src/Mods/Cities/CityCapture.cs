using Ariel.Content;
using Ariel.Game;
using S = Ariel.Content.Schema;
namespace Ariel.Mods.Cities;

/// <summary>Capture preserves historical culture. Conquest rules replace queues and citizen orders.</summary>
internal sealed class CityCapture : ICityCaptureService
{
    public void Capture(Simulation sim, int cityId, int player)
    {
        var city = sim.State.CityById(cityId);
        if (!sim.TeamOf(player).Wars.Contains(sim.TeamOf(city.Owner).Id)) throw new InvalidOperationException("Capture requires war");
        bool wasCapital = city.Buildings.Any(id => sim.Content.Get<S.BuildingDef>(id).Capital);
        bool hadWorldWonder = city.Buildings.Any(id => IsWorldWonder(sim, id));
        var state = CityData.Edit(sim, city);
        int gold = CalculateCaptureGold(sim, city, state);
        sim.State.PlayerById(player).Treasury += gold;
        bool autoRaze = Math.Max(city.Population, state.HighestPopulation) <= 1 && ConquestCultureLevel(sim, city) <= 0;
        ResetState(sim, city, player, state);
        long culture = state.CultureHundredths.GetValueOrDefault(player);
        var transferred = new City { Id = city.Id, Owner = player, Name = city.Name, Position = city.Position,
            Population = Math.Max(1, city.Population - 1), FoodStored = 0, Culture = culture / 100, Extensions = city.Extensions };
        sim.State.RemoveCity(cityId); sim.State.AddCity(transferred);
        sim.State.PlotAt(city.Position).Owner = player;
        state.TurnAcquired = sim.State.Turn;
        state.HighestPopulation = Math.Max(state.HighestPopulation, city.Population);
        if (!autoRaze) PrepareConquest(sim, city, transferred, state);
        CityCapital.RelocateIfNeeded(sim, city.Owner);
        sim.Events.Publish(new CityCaptured(cityId, city.Owner, player, gold, autoRaze));
        if (autoRaze) Raze(sim, cityId);
        else if (sim.State.PlayerById(player).Ai is not null)
            ResolveAiConquestChoice(sim, transferred, wasCapital, hadWorldWonder);
        sim.Content.Services.Get<IVisibilityService>().Single().Recompute(sim);
    }
    private static int ConquestCultureLevel(Simulation sim, City city)
    {
        var levels = CityData.Levels(sim, city.Owner, city);
        // Imported zero-threshold levels describe the same unexpanded city tier.
        int initial = CityData.LevelIndex(sim, new City { Owner = city.Owner }, levels);
        return CityData.LevelIndex(sim, city, levels) - initial;
    }
    private static int CalculateCaptureGold(Simulation sim, City city, CityRuleState state)
    {
        int gold = CityData.Raw(sim, "BASE_CAPTURE_GOLD", 20) + city.Population * CityData.Raw(sim, "CAPTURE_GOLD_PER_POPULATION", 10)
            + Next(sim, CityData.Raw(sim, "CAPTURE_GOLD_RAND1", 50)) + Next(sim, CityData.Raw(sim, "CAPTURE_GOLD_RAND2", 50));
        int maxTurns = CityData.Raw(sim, "CAPTURE_GOLD_MAX_TURNS", 50);
        return maxTurns > 0 ? gold * Math.Clamp(sim.State.Turn - state.TurnAcquired, 0, maxTurns) / maxTurns : gold;
    }
    private static int Next(Simulation sim, int maximum) => maximum > 0 ? sim.Rng.Next(maximum) : 0;
    private static void ResetState(Simulation sim, City city, int player, CityRuleState state)
    {
        if (CityCulture.IsBarbarian(sim, city.Owner)) state.OriginalCivilization = null;
        else state.OriginalCivilization ??= sim.State.PlayerById(city.Owner).Civilization;
        state.CultureHundredths[city.Owner] = city.Culture * 100 + state.CultureRemainder;
        state.CultureRemainder = state.CultureHundredths.GetValueOrDefault(player) % 100;
        state.LastCultureRateHundredths = 0;
        state.Overflow = 0; state.FeatureProduction = 0; state.PendingCompletion = null;
        state.PendingCost = 0; state.PendingProduction = 0; state.PendingModifierPercent = 0;
        state.PausedProduction.Clear(); state.ManualWork = false; state.Disorder = false;
        state.OccupationTurns = 0; state.IsPuppet = false; state.PendingConquestChoice = false;
    }
    private static void PrepareConquest(Simulation sim, City source, City target, CityRuleState state)
    {
        CityConquestBuildings.Transfer(sim, source, target);
        state.OccupationTurns = CalculateOccupationTurns(sim, target);
        state.PendingConquestChoice = true;
        state.IsPuppet = true;
        sim.Content.Services.Get<ICityWorkService>().Single().Assign(sim, target.Id);
    }
    private static int CalculateOccupationTurns(Simulation sim, City city)
    {
        int culture = TeamCulturePercent(sim, city);
        if (culture >= CityData.Raw(sim, "OCCUPATION_CULTURE_PERCENT_THRESHOLD", 75)) return 0;
        int maximum = CityData.Raw(sim, "BASE_OCCUPATION_TURNS", 3)
            + city.Population * CityData.Raw(sim, "OCCUPATION_TURNS_POPULATION_PERCENT", 50) / 100;
        int turns = maximum > 0 ? 1 + sim.Rng.Next(maximum) * Math.Max(0, 100 - culture) / 100 : 0;
        long modifier = CityData.Active(sim, city).Sum(b => (long)b.OccupationTimeModifier);
        modifier += CityData.Effect(CityData.Effects(sim, city), "occupation_time_percent");
        modifier += ModifierSink.Collect(new(sim, ModifierScope.Player, city.Owner)).Ledger.Total("cities:occupation_time_percent");
        return (int)Math.Clamp(turns * Math.Max(0, 100 + modifier) / 100, 0, int.MaxValue);
    }
    private static int TeamCulturePercent(Simulation sim, City city)
    {
        var culture = sim.State.PlotAt(city.Position).Culture;
        long total = culture.Values.Sum();
        if (total <= 0) return 100;
        long team = culture.Where(p => sim.TeamOf(p.Key).Id == sim.TeamOf(city.Owner).Id).Sum(p => p.Value);
        return (int)(team * 100 / total);
    }
    private static void ResolveAiConquestChoice(Simulation sim, City city, bool wasCapital, bool hadWorldWonder)
    {
        int cityCount = CityData.Owned(sim, city.Owner).Count();
        var option = AiConquestOption(sim, city, cityCount, wasCapital, hadWorldWonder);
        sim.Content.Services.Get<ICityCommandService>().Single().ResolveConquestChoice(sim, city.Owner, city.Id, option);
    }

    private static CityConquestOption AiConquestOption(Simulation sim, City city, int cityCount, bool wasCapital, bool hadWorldWonder)
    {
        if (ShouldRaze(sim, city, cityCount, wasCapital, hadWorldWonder)) return CityConquestOption.Raze;
        int targetCities = Math.Max(1, sim.Content.Get<S.WorldSizeDef>(sim.Settings.WorldSize!).TargetNumCities);
        return cityCount > targetCities ? CityConquestOption.Puppet : CityConquestOption.Annex;
    }

    private static bool ShouldRaze(Simulation sim, City city, int cityCount, bool wasCapital, bool hadWorldWonder)
    {
        if (cityCount <= 4 || city.Population > 2) return false;
        if (HasRazeProtection(sim, city, wasCapital, hadWorldWonder)) return false;
        if (CityData.Raw(sim, "PLAYER_ALWAYS_RAZES_CITIES", 0) != 0) return true;
        return RazeRollSucceeds(sim, city.Owner);
    }

    private static bool HasRazeProtection(Simulation sim, City city, bool wasCapital, bool hadWorldWonder) =>
        wasCapital || hadWorldWonder || IsHolyCity(sim, city) || IsCorporationHeadquarters(sim, city) || HasStrategicBonus(sim, city);

    private static bool IsHolyCity(Simulation sim, City city)
    {
        var service = sim.Content.Services.Get<IReligionService>().SingleOrDefault();
        return service is not null && sim.Content.Get<S.ReligionDef>().All.Any(religion => service.HolyCity(sim, religion.Id) == city.Id);
    }

    private static bool IsCorporationHeadquarters(Simulation sim, City city)
    {
        var service = sim.Content.Services.Get<ICorporationService>().SingleOrDefault();
        return service is not null && sim.Content.Get<S.CorporationDef>().All.Any(corporation => service.Headquarters(sim, corporation.Id) == city.Id);
    }

    private static bool HasStrategicBonus(Simulation sim, City city)
    {
        int radius = Math.Max(1, CityCulture.CultureRadius(sim, city));
        foreach (var tile in sim.State.Grid.Range(CityData.Tile(sim, city), radius))
        {
            var bonus = sim.State.Plots[tile.Value].Bonus;
            if (bonus is not null && IsStrategicBonus(sim, bonus)) return true;
        }
        return false;
    }

    private static bool IsStrategicBonus(Simulation sim, string id) => sim.Content.Get<S.BonusDef>(id).BonusClass is
        "world:bonusclass_fuel" or "world:bonusclass_manufactured" or "world:bonusclass_modern" or "world:bonusclass_future" or "world:bonusclass_rush";

    private static bool IsWorldWonder(Simulation sim, string id)
    {
        var building = sim.Content.Get<S.BuildingDef>(id);
        return building.Limits.World >= 0 && !building.Limits.NoLimit;
    }

    private static bool RazeRollSucceeds(Simulation sim, int player)
    {
        int chance = sim.Content.Services.Get<IPeoplesService>().SingleOrDefault()?.Leader(sim, player)?.RazeCityProbability ?? 0;
        return chance > 0 && sim.Rng.Next(100) < chance;
    }
    public string? ValidateRaze(Simulation sim, int cityId, int player)
    {
        var city = sim.State.FindCity(cityId);
        if (city is null || city.Owner != player) return "Not your city";
        return CityCapital.IsCapital(sim, city) ? "Cannot raze a capital" : null;
    }
    public void Raze(Simulation sim, int cityId)
    {
        var city = sim.State.CityById(cityId);
        var tile = CityData.Tile(sim, city);
        int radius = Math.Max(1, CityCulture.CultureRadius(sim, city));
        sim.State.RemoveCity(cityId);
        foreach (var plot in sim.State.Grid.Range(tile, radius)) CityCulture.Resolve(sim, plot);
        CityCapital.RelocateIfNeeded(sim, city.Owner);
        sim.Events.Publish(new CityRazed(cityId, city.Owner, city.Position));
        sim.Content.Services.Get<IVisibilityService>().Single().Recompute(sim);
    }
}

/// <summary>Building grants use Military's modifier keys without an assembly dependency.</summary>
internal sealed class BuildingMilitaryEffects : IModifierSource
{
    public void Contribute(ModifierContext context, ModifierSink sink)
    {
        if (context.Scope != ModifierScope.Unit || context.Unit is not { } unit || context.City is not { } city) return;
        if (unit.Owner != city.Owner) return;
        foreach (var building in CityData.Active(context.Sim, city))
            if (building.FreePromotion is { } promotion) sink.Add(building.Id, "military:free_promotion:" + promotion, 1);
    }
}
