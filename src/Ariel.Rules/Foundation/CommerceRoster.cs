using System.Collections.Concurrent;
using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.Game;

/// <summary>The loaded commerce types as the economy runs them: every <see cref="CommerceDef"/> of every mod, in commerce
/// order, with the behaviour each row declares. Built and checked when content freezes
/// (<see cref="GameRules.Commerce"/>), so a set the economy cannot run fails loading.</summary>
public sealed class CommerceRoster
{
    private readonly CommerceDef[] _defs;
    private readonly ConcurrentDictionary<(string Prefix, string Suffix), string[]> _names = new();

    internal CommerceRoster(ContentDb content)
    {
        Types = content.Commerce;
        _defs = Types.Select(t => t.Definition as CommerceDef
            ?? throw new InvalidDataException($"Commerce type '{t.Id}' is a {t.Definition.GetType().Name}; the economy runs CommerceDef rows")).ToArray();
        var remainders = Types.Where(t => Def(t).TakesRemainder).ToArray();
        if (remainders.Length != 1)
            throw new InvalidDataException($"Exactly one commerce type takes the slider remainder; {remainders.Length} do");
        Remainder = remainders[0];
        if (Def(Remainder).DeficitCut is not null)
            throw new InvalidDataException($"Commerce type '{Remainder.Id}' takes the remainder and cannot be cut for a deficit");
        DeficitCuts = Types.Where(t => Def(t).DeficitCut is not null).OrderBy(t => Def(t).DeficitCut).ThenBy(t => t.Index).ToArray();
        if (_defs.Any(d => d.InitialPercent is < 0 or > 100) || _defs.Sum(d => d.InitialPercent) != 100)
            throw new InvalidDataException("Commerce types' initial percents must each be 0..100 and sum to 100");
        InitialRates = Vector(_defs.Select(d => (long)d.InitialPercent).ToArray());
    }

    /// <summary>The loaded types, in order.</summary>
    public CommerceTypes Types { get; }
    /// <summary>The row of a loaded type.</summary>
    public CommerceDef Def(CommerceType type) => _defs[type.Index];
    /// <summary>The type that receives unallocated commerce and deficit cuts (<see cref="CommerceDef.TakesRemainder"/>).</summary>
    public CommerceType Remainder { get; }
    /// <summary>The types settlement cuts for a deficit, in cut order (<see cref="CommerceDef.DeficitCut"/>).</summary>
    public IReadOnlyList<CommerceType> DeficitCuts { get; }
    /// <summary>A new player's slider rates (<see cref="CommerceDef.InitialPercent"/>).</summary>
    public CommerceVector InitialRates { get; }
    /// <summary>A zero vector carrying every loaded type.</summary>
    public CommerceVector Zero => Types.Zero;
    /// <summary>A loaded type by key.</summary>
    public CommerceType this[string key] => Types[key];

    /// <summary>A vector of every loaded type, amounts in type order.</summary>
    public CommerceVector Vector(ReadOnlySpan<long> amounts) => Types.Layout is { } layout ? CommerceVector.Of(layout, amounts) : default;
    /// <summary>A vector's amounts in type order.</summary>
    public long[] Parts(CommerceVector vector)
    {
        var parts = new long[Types.Count];
        for (int i = 0; i < parts.Length; i++) parts[i] = vector[Types[i]];
        return parts;
    }
    /// <summary>Every type's key between a prefix and a suffix, in type order (<c>("economy:", "_hundredths")</c> gives
    /// <c>economy:gold_hundredths</c>, ...). Built once per pair.</summary>
    public string[] Names(string prefix, string suffix) =>
        _names.GetOrAdd((prefix, suffix), key => Types.Select(t => key.Prefix + t.Key + key.Suffix).ToArray());
}
