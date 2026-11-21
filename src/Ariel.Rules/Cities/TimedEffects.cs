using Ariel.Content;
using Ariel.Game;

namespace Ariel.Rules.Cities;

/// <summary>The store of timed modifier effects (<see cref="ITimedEffects"/>), its modifier source and its upkeep.</summary>
internal sealed class TimedEffects : ITimedEffects, IModifierSource
{
    private static TimedEffectState? Read(Simulation sim) => sim.State.Extensions.Get<TimedEffectState>(sim.Content);
    private static TimedEffectState Edit(Simulation sim)
    {
        var state = Read(sim);
        if (state is null) sim.State.Extensions.Set(sim.Content, state = new TimedEffectState());
        return state;
    }
    private static bool SameHolder(TimedEffect a, TimedEffect b) =>
        a.Source == b.Source && a.Player == b.Player && a.City == b.City && a.Plot == b.Plot;

    public TimedEffect Add(Simulation sim, TimedEffect effect)
    {
        ArgumentException.ThrowIfNullOrEmpty(effect.Source);
        var state = Edit(sim);
        var stored = effect with { Id = state.NextId++ };
        state.Effects.Add(stored);
        return stored;
    }

    public TimedEffect Set(Simulation sim, TimedEffect effect)
    {
        if (Read(sim) is { } state) state.Effects.RemoveAll(e => SameHolder(e, effect));
        return Add(sim, effect);
    }

    public bool Remove(Simulation sim, int id) => Read(sim) is { } state && state.Effects.RemoveAll(e => e.Id == id) > 0;

    public TimedEffect? Find(Simulation sim, int id)
    {
        if (Read(sim) is not { } state) return null;
        foreach (var effect in state.Effects) if (effect.Id == id) return effect;
        return null;
    }

    public TimedEffect? Find(Simulation sim, string source, int player = -1, int city = -1, int plot = -1)
    {
        if (Read(sim) is not { } state) return null;
        foreach (var effect in state.Effects)
            if (effect.Source == source && effect.Player == player && effect.City == city && effect.Plot == plot
                && effect.IsActive(sim.State.Turn)) return effect;
        return null;
    }

    public IReadOnlyList<TimedEffect> Active(Simulation sim)
    {
        if (Read(sim) is not { } state) return [];
        int turn = sim.State.Turn;
        return state.Effects.Where(e => e.IsActive(turn)).OrderBy(e => e.Id).ToArray();
    }

    public void Contribute(ModifierContext context, ModifierSink sink)
    {
        if (context.Scope is not (ModifierScope.Plot or ModifierScope.City or ModifierScope.Player)) return;
        if (Read(context.Sim) is not { Effects.Count: > 0 } state) return;
        int turn = context.Sim.State.Turn;
        foreach (var held in state.Effects)
        {
            if (!held.IsActive(turn) || !Holds(held, context)) continue;
            foreach (var effect in held.Effects)
                if (effect.Scope == context.Scope) sink.Apply(held.Source, effect);
        }
    }

    /// <summary>Whether the calculation's subject is the holder for this scope.</summary>
    private static bool Holds(TimedEffect held, ModifierContext context) => context.Scope switch
    {
        ModifierScope.Plot => context.Plot is { } tile && held.Plot == tile.Value,
        ModifierScope.City => context.City is { } city
            && (held.City >= 0 ? held.City == city.Id : held.Player >= 0 && held.Player == city.Owner),
        ModifierScope.Player => held.Player >= 0 && held.Player == context.Player,
        _ => false,
    };

    /// <summary>Drop expired effects and those of cities that are gone.</summary>
    internal static void Expire(Simulation sim)
    {
        if (Read(sim) is not { Effects.Count: > 0 } state) return;
        int turn = sim.State.Turn;
        state.Effects.RemoveAll(e => !e.IsActive(turn) || (e.City >= 0 && sim.State.FindCity(e.City) is null));
    }

    /// <summary>A city's effects end when it changes hands or is destroyed.</summary>
    internal static void EndCity(Simulation sim, int city)
    {
        if (Read(sim) is { } state) state.Effects.RemoveAll(e => e.City == city);
    }
}

/// <summary>Drops the timed effects that no longer apply, at the start of a turn's processing.</summary>
internal sealed class TimedEffectExpiry : ITurnSystem
{
    public string Id => "cities:timed_effects";
    public int Order => TurnPhase.GameBegin;
    public void Run(Simulation sim) => TimedEffects.Expire(sim);
}
