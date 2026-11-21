using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Ariel.Content;

namespace Ariel.Game;

/// <summary>A bundle of modifiers a feature grants for a while: an event's happiness, a spy's unrest, a rebellion's
/// aftermath. <see cref="Source"/> is the feature's id for it and the ledger source of every line it adds. The holder
/// says where each effect applies, by its <see cref="ModifierEffect.Scope"/>: a plot effect to <see cref="Plot"/>; a
/// city effect to <see cref="City"/>, or with no city to every city <see cref="Player"/> owns; a player effect to
/// <see cref="Player"/>. Unused holder fields are -1. An effect held by a city ends when the city is captured or
/// destroyed. Kept by the base game (<see cref="GameRules.TimedEffects"/>); immutable once added.</summary>
public sealed record TimedEffect
{
    /// <summary>Assigned by the store when the effect is added; unique for the game.</summary>
    public int Id { get; init; }
    public string Source { get; init; } = "";
    public int Player { get; init; } = -1;
    public int City { get; init; } = -1;
    public int Plot { get; init; } = -1;
    /// <summary>The first turn the effect no longer applies; 0 means it never expires.</summary>
    public int ExpiresTurn { get; init; }
    public ImmutableArray<ModifierEffect> Effects { get; init; } = [];

    [JsonIgnore] public bool IsPermanent => ExpiresTurn == 0;
    /// <summary>Whether the effect applies on this turn.</summary>
    public bool IsActive(int turn) => ExpiresTurn == 0 || turn < ExpiresTurn;
    /// <summary>Turns the effect still applies, counting this one; null for a permanent effect.</summary>
    public int? TurnsRemaining(int turn) => ExpiresTurn == 0 ? null : Math.Max(0, ExpiresTurn - turn);
}

/// <summary>Saved timed effects, in the order they were added.</summary>
public sealed class TimedEffectState
{
    public int NextId { get; set; } = 1;
    public List<TimedEffect> Effects { get; set; } = [];
}

/// <summary>The one store of timed modifier effects. It contributes every active effect to the calculations its
/// holder takes part in, drops expired ones at the start of a turn's processing and city-held ones when the city
/// changes hands or is destroyed. Queries never change state or use the RNG.</summary>
public interface ITimedEffects
{
    /// <summary>Add an effect, giving it the next id. Returns the stored effect.</summary>
    TimedEffect Add(Simulation sim, TimedEffect effect);
    /// <summary>Replace every effect with the same source and holder by this one (or add it when there is none).</summary>
    TimedEffect Set(Simulation sim, TimedEffect effect);
    /// <summary>Remove one effect by id. Returns false when there is none.</summary>
    bool Remove(Simulation sim, int id);
    /// <summary>The effect with this id, or null.</summary>
    TimedEffect? Find(Simulation sim, int id);
    /// <summary>The active effect with this source and holder, or null.</summary>
    TimedEffect? Find(Simulation sim, string source, int player = -1, int city = -1, int plot = -1);
    /// <summary>Every active effect, in id order.</summary>
    IReadOnlyList<TimedEffect> Active(Simulation sim);
}
