using Ariel.Content;
using Ariel.Game;
using Ariel.Rules.Foundation;

namespace Ariel.Rules.Cities;

/// <summary>Adds the modifiers of every active timer (<see cref="GameTimer.Effects"/>) to the calculations of the scope
/// each applies to that its holder takes part in. Kept in Cities, where the city calculations that read timers'
/// modifiers are collected; the timers themselves are the Foundation's.</summary>
internal sealed class TimerModifiers : IModifierSource
{
    public void Contribute(ModifierContext context, ModifierSink sink)
    {
        if (Timers.Read(context.Sim) is not { Timers.Count: > 0 } state) return;
        int turn = context.Sim.State.Turn;
        foreach (var held in state.Timers)
        {
            if (held.Effects.IsDefaultOrEmpty || !held.IsActive(turn) || !Holds(held.Holder, context)) continue;
            foreach (var effect in held.Effects)
                if (effect.Scope == context.Scope) sink.Apply(held.Source, effect);
        }
    }

    /// <summary>Whether the calculation's subject is the holder for this scope.</summary>
    private static bool Holds(TimerHolder holder, ModifierContext context) => context.Scope switch
    {
        ModifierScope.Plot => context.Plot is { } tile && holder.Plot == tile.Value,
        ModifierScope.City => context.City is { } city
            && (holder.City is { } held ? held == city.Id : holder.Player is { } player && player == city.Owner),
        ModifierScope.Player => holder.Player is { } player && player == context.Player,
        ModifierScope.Unit => context.Unit is { } unit && holder.Unit == unit.Id,
        _ => false,
    };
}
