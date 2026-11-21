using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Events;

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

    /// <summary>The timer kind of every kept event option effect; the timer's key is the option.</summary>
    internal const string OptionEffectKind = "events:option_effect";

    public IReadOnlyList<GameTimer> ActiveEffects(Simulation sim, int player) => sim.Rules().Timers.Active(sim, OptionEffectKind)
        .Where(e => e.Holder.Player == player).ToArray();

    public IReadOnlyList<string> Options(Simulation sim, int eventId) =>
        Read(sim).FindPending(eventId) is { } pending ? LegalOptions(sim, pending).Select(o => o.Id).ToArray() : [];

    private static IEnumerable<EventOptionDef> LegalOptions(Simulation sim, PendingEvent pending) =>
        sim.Content.Get<EventOptionDef>().All.Where(o => o.Trigger == pending.Trigger)
            .Where(o => o.GoldDelta >= 0 || sim.State.PlayerById(pending.Player).Treasury >= -o.GoldDelta)
            .OrderBy(o => o.Id, StringComparer.Ordinal);

    public string? ValidateChoice(Simulation sim, int player, int eventId, string option)
    {
        var pending = Read(sim).FindPending(eventId);
        if (pending is null) return "Unknown event";
        if (pending.Player != player) return "Event belongs to another player";
        return LegalOptions(sim, pending).Any(o => o.Id == option) ? null : "Option is not available";
    }

    public void Choose(Simulation sim, int player, int eventId, string option, bool automatic = false)
    {
        var state = Write(sim);
        var pending = state.FindPending(eventId) ?? throw new InvalidOperationException($"Pending event {eventId} not found");
        state.RemovePending(eventId);
        var def = sim.Content.Get<EventOptionDef>(option);
        Apply(sim, pending, def);
        sim.Events.Publish(new EventResolved(player, eventId, pending.Trigger, option, automatic));
    }

    private static void Apply(Simulation sim, PendingEvent pending, EventOptionDef option)
    {
        var player = sim.State.PlayerById(pending.Player);
        player.Treasury = Math.Max(0, player.Treasury + option.GoldDelta);
        if (option.ResearchBonus > 0 && sim.Rules().Research is { } research)
            research.Contribute(sim, player.Id, option.ResearchBonus);
        if (sim.State.FindCity(pending.City) is { } city && city.Owner == player.Id)
        {
            city.FoodStored = Math.Max(0, city.FoodStored + option.CityFoodDelta);
            city.Culture = Math.Max(0, city.Culture + option.CityCultureHundredthsDelta / 100);
            if (option.CityProductionDelta != 0 && city.ProductionQueue.Count > 0)
                city.ProductionQueue[0] = city.ProductionQueue[0] with { Progress = Math.Max(0, city.ProductionQueue[0].Progress + option.CityProductionDelta) };
        }
        var kept = KeptEffects(option);
        if (kept.Length == 0) return;
        sim.Rules().Timers.Start(sim, new GameTimer
        {
            Kind = OptionEffectKind, Key = option.Id,
            // A city event ends with the city's capture; an empire-wide one (no city) covers every city the player owns.
            Holder = new() { Player = player.Id, City = Held(pending.City), Plot = Held(pending.Plot) },
            // Timed effects count from the resolution turn, so a late answer still gets its full duration, and last
            // through the expiry turn itself, as they always have.
            ExpiresTurn = option.DurationTurns > 0 ? sim.State.Turn + option.DurationTurns + 1 : 0,
            Effects = kept,
        });
    }

    /// <summary>An event's city or plot, where a negative id means it has none.</summary>
    private static int? Held(int id) => id >= 0 ? id : null;

    /// <summary>The option's lasting happiness, health and plot yield changes as modifiers.</summary>
    private static System.Collections.Immutable.ImmutableArray<ModifierEffect> KeptEffects(EventOptionDef option)
    {
        var effects = System.Collections.Immutable.ImmutableArray.CreateBuilder<ModifierEffect>();
        void Keep(ModifierScope scope, string key, long amount)
        {
            if (amount != 0) effects.Add(new() { Scope = scope, Key = key, Amount = amount });
        }
        Keep(ModifierScope.City, "cities:happiness", option.HappinessDelta);
        Keep(ModifierScope.City, "cities:health", option.HealthDelta);
        Keep(ModifierScope.Plot, "yield.flat.food", option.PlotFoodDelta);
        Keep(ModifierScope.Plot, "yield.flat.production", option.PlotProductionDelta);
        Keep(ModifierScope.Plot, "yield.flat.commerce", option.PlotCommerceDelta);
        return effects.ToImmutable();
    }

    /// <summary>Settle open events, then roll new events for every player.</summary>
    internal void RunTurn(Simulation sim)
    {
        var state = Write(sim);
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
        if (pick is null)
        {
            var state = Write(sim);
            state.RemovePending(pending.Id);
            sim.Events.Publish(new EventResolved(pending.Player, pending.Id, pending.Trigger, EventResolved.Expired, Automatic: true));
            return;
        }
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
            state.AddPending(pending);
            state.RecordFired(EventCooldownKey.Of(player.Id, trigger.Id), sim.State.Turn);
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
        if (state.GetLastFired(EventCooldownKey.Of(player.Id, t.Id)) is { } last && (!t.Repeatable || sim.State.Turn - last < t.CooldownTurns)) return null;
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
