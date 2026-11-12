using System.Collections.Immutable;
using Ariel.Content;

namespace Ariel.Mods.Diplomacy;

/// <summary>Diplomacy balance numbers that leaders do not carry themselves.</summary>
public sealed class DiplomacyRulesDef : ContentDef
{
    /// <summary>How fast a remembered event fades, for memory kinds the remembering leader's own
    /// <c>MemoryDecays</c> leaves out. Keyed like the leader tables, without the <c>memory_</c> prefix. Each turn a
    /// remembered kind loses one count with a chance of one in this many turns, scaled by the game speed's pace.</summary>
    public ImmutableDictionary<string, int> MemoryDecays { get; init; } = ImmutableDictionary<string, int>.Empty;
    /// <summary>The decay for a memory kind neither the leader nor <see cref="MemoryDecays"/> names.</summary>
    public int DefaultMemoryDecay { get; init; } = 60;
    /// <summary>Most gifts a player remembers from one giver at once; further gifts add nothing until one fades.</summary>
    public int GiveHelpMemoryLimit { get; init; } = 3;
}
