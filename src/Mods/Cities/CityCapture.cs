using Civ.Content;
using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.Cities;

/// <summary>Minimal capture policy. Historical culture and buildings survive; queues and work orders do not.</summary>
internal sealed class CityCapture : ICityCaptureService
{
    public void Capture(Simulation sim, int cityId, int player)
    {
        var city = sim.State.CityById(cityId);
        if (!sim.TeamOf(player).Wars.Contains(sim.TeamOf(city.Owner).Id)) throw new InvalidOperationException("Capture requires war");
        var state = CityData.Edit(sim, city);
        state.CultureHundredths[city.Owner] = city.Culture * 100 + state.CultureRemainder;
        long culture = state.CultureHundredths.GetValueOrDefault(player);
        state.CultureRemainder = culture % 100; state.LastCultureRateHundredths = 0;
        state.Overflow = 0; state.FeatureProduction = 0; state.PendingCompletion = null;
        state.PendingCost = 0; state.PendingProduction = 0; state.PendingModifierPercent = 0;
        state.PausedProduction.Clear(); state.ManualWork = false; state.Disorder = false;
        var transferred = new City { Id = city.Id, Owner = player, Name = city.Name, Position = city.Position,
            Population = Math.Max(1, city.Population - 1), Buildings = city.Buildings.Where(id => !sim.Content.Get<S.BuildingDef>(id).Capital).ToList(),
            FoodStored = 0, Culture = culture / 100, Extensions = city.Extensions };
        sim.State.RemoveCity(cityId); sim.State.AddCity(transferred);
        sim.State.PlotAt(city.Position).Owner = player;
        sim.Content.Services.Get<ICityWorkService>().Single().Assign(sim, cityId);
        sim.Events.Publish(new CityCaptured(cityId, city.Owner, player));
        sim.Content.Services.Get<IVisibilityService>().Single().Recompute(sim);
    }
    public string? ValidateRaze(Simulation sim, int city, int player) =>
        sim.State.FindCity(city) is { } owned && owned.Owner == player ? null : "Not your city";
    public void Raze(Simulation sim, int cityId)
    {
        var city = sim.State.CityById(cityId);
        var tile = CityData.Tile(sim, city);
        int radius = Math.Max(1, CityCulture.CultureRadius(sim, city));
        sim.State.RemoveCity(cityId);
        foreach (var plot in sim.State.Grid.Range(tile, radius)) CityCulture.Resolve(sim, plot);
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
