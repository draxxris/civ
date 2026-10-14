using Civ.Content;
using Civ.Content.Schema;
using Civ.Game;

namespace Civ.Mods.Events;

internal sealed class EventModifiers : IModifierSource
{
    public void Contribute(ModifierContext context, ModifierSink sink)
    {
        var state = context.Sim.State.Extensions.Get<EventState>(context.Sim.Content);
        if (state is null) return;
        foreach (var effect in state.Effects)
        {
            if (context.Scope == ModifierScope.City && context.City is { } city && AppliesToCity(effect, city))
            {
                sink.Add(effect.Option, "cities:happiness", effect.Happiness);
                sink.Add(effect.Option, "cities:health", effect.Health);
            }
            else if (context.Scope == ModifierScope.Plot && context.Plot is { } tile && effect.Plot == tile.Value)
                sink.AddYield(effect.Option, new(effect.PlotFood, effect.PlotProduction, effect.PlotCommerce));
        }
    }

    /// <summary>A city event stays with its city. An empire-wide event (no city) covers every city its player owns.</summary>
    private static bool AppliesToCity(ActiveEventEffect effect, City city) =>
        effect.City == city.Id || (effect.City == -1 && effect.Player == city.Owner);
}

internal sealed class EventService : IEventService
{
    private static EventState Read(Simulation sim) => sim.State.Extensions.Get<EventState>(sim.Content) ?? new();
    private static EventState Write(Simulation sim)
    {
        var state = sim.State.Extensions.Get<EventState>(sim.Content);
        if (state is not null) return state;
        state = new(); sim.State.Extensions.Set(sim.Content, state); return state;
    }

    public IReadOnlyList<PendingEvent> Pending(Simulation sim, int player) =>
        Read(sim).Pending.Where(p => p.Player == player).OrderBy(p => p.Id).ToArray();

    public IReadOnlyList<string> Options(Simulation sim, int eventId) =>
        Read(sim).Pending.FirstOrDefault(p => p.Id == eventId) is { } pending ? LegalOptions(sim, pending).Select(o => o.Id).ToArray() : [];

    private static IEnumerable<EventOptionDef> LegalOptions(Simulation sim, PendingEvent pending) =>
        sim.Content.Get<EventOptionDef>().All.Where(o => o.Trigger == pending.Trigger)
            .Where(o => o.GoldDelta >= 0 || sim.State.PlayerById(pending.Player).Treasury >= -o.GoldDelta)
            .OrderBy(o => o.Id, StringComparer.Ordinal);

    public string? ValidateChoice(Simulation sim, int player, int eventId, string option)
    {
        var pending = Read(sim).Pending.FirstOrDefault(p => p.Id == eventId);
        if (pending is null) return "Unknown event";
        if (pending.Player != player) return "Event belongs to another player";
        return LegalOptions(sim, pending).Any(o => o.Id == option) ? null : "Option is not available";
    }

    public void Choose(Simulation sim, int player, int eventId, string option, bool automatic = false)
    {
        var state = Write(sim);
        var pending = state.Pending.First(p => p.Id == eventId);
        state.Pending.Remove(pending);
        var def = sim.Content.Get<EventOptionDef>(option);
        Apply(sim, state, pending, def);
        sim.Events.Publish(new EventResolved(player, eventId, pending.Trigger, option, automatic));
    }

    private static void Apply(Simulation sim, EventState state, PendingEvent pending, EventOptionDef option)
    {
        var player = sim.State.PlayerById(pending.Player);
        player.Treasury = Math.Max(0, player.Treasury + option.GoldDelta);
        if (option.ResearchBonus > 0 && sim.Content.Services.Get<IResearchService>().SingleOrDefault() is { } research)
            research.Contribute(sim, player.Id, option.ResearchBonus);
        if (sim.State.FindCity(pending.City) is { } city && city.Owner == player.Id)
        {
            city.FoodStored = Math.Max(0, city.FoodStored + option.CityFoodDelta);
            city.Culture = Math.Max(0, city.Culture + option.CityCultureHundredthsDelta / 100);
            if (option.CityProductionDelta != 0 && city.ProductionQueue.Count > 0)
                city.ProductionQueue[0] = city.ProductionQueue[0] with { Progress = Math.Max(0, city.ProductionQueue[0].Progress + option.CityProductionDelta) };
        }
        bool kept = option.HappinessDelta != 0 || option.HealthDelta != 0 || option.PlotFoodDelta != 0 || option.PlotProductionDelta != 0 || option.PlotCommerceDelta != 0;
        if (!kept) return;
        state.Effects.Add(new ActiveEventEffect
        {
            Player = player.Id, City = pending.City, Plot = pending.Plot, Option = option.Id,
            // Timed effects count from the trigger turn, so a late answer does not outlast an automatic one.
            ExpiresTurn = option.DurationTurns > 0 ? Math.Max(sim.State.Turn, pending.Turn + option.DurationTurns) : 0,
            Happiness = option.HappinessDelta, Health = option.HealthDelta,
            PlotFood = option.PlotFoodDelta, PlotProduction = option.PlotProductionDelta, PlotCommerce = option.PlotCommerceDelta,
        });
    }

    /// <summary>Expire effects, settle open events, then roll new events for every player.</summary>
    internal void RunTurn(Simulation sim)
    {
        var state = Write(sim);
        state.Effects.RemoveAll(e => e.ExpiresTurn > 0 && sim.State.Turn >= e.ExpiresTurn);
        foreach (var pending in state.Pending.OrderBy(p => p.Id).ToArray()) AutoResolve(sim, pending);
        foreach (var player in sim.State.Players.OrderBy(p => p.Id)) Roll(sim, state, player);
    }

    private void AutoResolve(Simulation sim, PendingEvent pending)
    {
        var legal = LegalOptions(sim, pending).ToArray();
        var player = sim.State.PlayerById(pending.Player);
        var pick = player.Ai is not null
            ? legal.OrderByDescending(Value).ThenByDescending(o => o.IsDefault).FirstOrDefault()
            : legal.FirstOrDefault(o => o.IsDefault) ?? legal.FirstOrDefault();
        if (pick is null) { Write(sim).Pending.Remove(pending); return; }
        Choose(sim, pending.Player, pending.Id, pick.Id, true);
    }

    private static long Value(EventOptionDef o) =>
        o.GoldDelta + o.ResearchBonus + o.CityFoodDelta + o.CityProductionDelta + o.CityCultureHundredthsDelta / 100
        + 3L * (o.HappinessDelta + o.HealthDelta + o.PlotFoodDelta + o.PlotProductionDelta + o.PlotCommerceDelta);

    private void Roll(Simulation sim, EventState state, Player player)
    {
        foreach (var trigger in sim.Content.Get<EventTriggerDef>().All.OrderBy(t => t.Id, StringComparer.Ordinal))
        {
            if (trigger.ChancePercent <= 0 || Eligible(sim, state, player, trigger) is not { } subject) continue;
            if (trigger.ChancePercent < 100 && sim.Rng.Next(100) >= trigger.ChancePercent) continue;
            var pending = new PendingEvent(state.NextEventId++, player.Id, trigger.Id, subject.City, subject.Plot, sim.State.Turn);
            state.Pending.Add(pending);
            state.LastFired[player.Id + ":" + trigger.Id] = sim.State.Turn;
            sim.Events.Publish(new EventTriggered(player.Id, pending.Id, trigger.Id, pending.City));
            if (player.Ai is not null) AutoResolve(sim, pending);
            return;
        }
    }

    private static bool IsCityScope(EventTriggerDef t) =>
        t.CityScope || t.MinPopulation > 0 || t.RequiredBuilding is not null || t.RequiredImprovement is not null || t.RequiredFeature is not null;

    /// <summary>The city and plot an event would happen to, or null when the player is not eligible.</summary>
    internal static (int City, int Plot)? Eligible(Simulation sim, EventState state, Player player, EventTriggerDef t)
    {
        if (sim.State.Turn < t.MinTurn || player.Treasury < t.MinTreasury) return null;
        if (t.RequiredTech is not null && !sim.TeamOf(player.Id).Techs.Contains(t.RequiredTech)) return null;
        if (state.LastFired.TryGetValue(player.Id + ":" + t.Id, out int last) && (!t.Repeatable || sim.State.Turn - last < t.CooldownTurns)) return null;
        if (state.Pending.Any(p => p.Player == player.Id)) return null;
        if (!IsCityScope(t)) return (-1, -1);
        foreach (var city in sim.State.Cities.Where(c => c.Owner == player.Id).OrderBy(c => c.Id))
        {
            if (city.Population < t.MinPopulation) continue;
            if (t.RequiredBuilding is not null && !city.Buildings.Contains(t.RequiredBuilding)) continue;
            if (FindPlot(sim, city, player.Id, t) is { } plot) return (city.Id, plot);
        }
        return null;
    }

    private static int? FindPlot(Simulation sim, City city, int owner, EventTriggerDef t)
    {
        var grid = sim.State.Grid;
        if (t.RequiredFeature is null && t.RequiredImprovement is null) return grid.TileAt(city.Position).Value;
        foreach (var tile in grid.Range(grid.TileAt(city.Position), 2).OrderBy(x => x.Value))
        {
            var plot = sim.State.Plots[tile.Value];
            if (plot.Owner != owner) continue;
            if (t.RequiredFeature is not null && plot.Feature != t.RequiredFeature) continue;
            if (t.RequiredImprovement is not null && plot.Improvement != t.RequiredImprovement) continue;
            return tile.Value;
        }
        return null;
    }
}
