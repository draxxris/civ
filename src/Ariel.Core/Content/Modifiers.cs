using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ariel.Game;

namespace Ariel.Content;

/// <summary>Integer food, production and commerce. Percent math truncates toward zero after summing sources.</summary>
public readonly record struct YieldVector(long Food, long Production, long Commerce)
{
    /// <summary>Component-wise sum.</summary>
    public static YieldVector operator +(YieldVector a, YieldVector b) => new(a.Food + b.Food, a.Production + b.Production, a.Commerce + b.Commerce);
    /// <summary>Integer scaling.</summary>
    public static YieldVector operator *(YieldVector a, long n) => new(a.Food * n, a.Production * n, a.Commerce * n);
    /// <summary>Apply percentage bonuses component-wise; 25 means +25%. Multipliers clamp at zero, so reductions below -100% yield nothing.</summary>
    public YieldVector WithPercent(YieldVector percent) => new(Food * Math.Max(0, 100 + percent.Food) / 100, Production * Math.Max(0, 100 + percent.Production) / 100, Commerce * Math.Max(0, 100 + percent.Commerce) / 100);
}
/// <summary>Integer gold, research, culture and espionage.</summary>
public readonly record struct CommerceVector(long Gold, long Research, long Culture, long Espionage)
{
    /// <summary>Component-wise sum.</summary>
    public static CommerceVector operator +(CommerceVector a, CommerceVector b) => new(a.Gold + b.Gold, a.Research + b.Research, a.Culture + b.Culture, a.Espionage + b.Espionage);
    /// <summary>Integer scaling.</summary>
    public static CommerceVector operator *(CommerceVector a, long n) => new(a.Gold * n, a.Research * n, a.Culture * n, a.Espionage * n);
    /// <summary>Apply percentage bonuses component-wise. Multipliers clamp at zero, so reductions below -100% yield nothing.</summary>
    public CommerceVector WithPercent(CommerceVector p) => new(Gold * Math.Max(0, 100 + p.Gold) / 100, Research * Math.Max(0, 100 + p.Research) / 100, Culture * Math.Max(0, 100 + p.Culture) / 100, Espionage * Math.Max(0, 100 + p.Espionage) / 100);
}
/// <summary>Target scope of a modifier calculation. Content names it in lower camel case (<c>"city"</c>).</summary>
[JsonConverter(typeof(CamelCaseEnumConverter<ModifierScope>))]
public enum ModifierScope
{
    /// <summary>One map plot.</summary>
    Plot,
    /// <summary>One city.</summary>
    City,
    /// <summary>One player.</summary>
    Player,
    /// <summary>One team.</summary>
    Team,
    /// <summary>One unit.</summary>
    Unit,
}
/// <summary>Calculation subject. The calculating mod chooses the scope and supplies applicable subject ids.</summary>
public sealed record ModifierContext(Simulation Sim, ModifierScope Scope, int Player, City? City = null, TileId? Plot = null, Unit? Unit = null, Team? Team = null, Plot? ProposedPlot = null);
/// <summary>Cross-mod contribution source. Contribute must not change simulation state.</summary>
public interface IModifierSource
{
    /// <summary>Append typed effects to a calculation's sink; ignore unsupported scopes.</summary>
    void Contribute(ModifierContext context, ModifierSink sink);
}
/// <summary>One explainable contribution to a calculation.</summary>
public sealed record LedgerLine(string Source, string Key, long Amount);
/// <summary>Contribution lines and ordinal totals by key. Different units (flat/percent) have different keys.</summary>
public sealed class Ledger
{
    /// <summary>Lines in service and contribution order.</summary>
    public IReadOnlyList<LedgerLine> Lines { get; }
    /// <summary>Summed amounts by typed or namespaced key.</summary>
    public IReadOnlyDictionary<string, long> Totals { get; }
    /// <summary>Build an immutable ledger snapshot.</summary>
    public Ledger(IEnumerable<LedgerLine> lines)
    {
        Lines = Array.AsReadOnly(lines.ToArray());
        var totals = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (var line in Lines) totals[line.Key] = totals.GetValueOrDefault(line.Key) + line.Amount;
        Totals = new System.Collections.ObjectModel.ReadOnlyDictionary<string, long>(totals);
    }
    /// <summary>Total for a key, zero when absent.</summary>
    public long Total(string key) => Totals.GetValueOrDefault(key);
}
/// <summary>Typed effect accumulator with an extensible namespaced bag and an explanation ledger.</summary>
public sealed class ModifierSink
{
    private readonly List<LedgerLine> _lines = new();
    /// <summary>Summed flat yield effects.</summary>
    public YieldVector FlatYield { get; private set; }
    /// <summary>Summed percent yield effects.</summary>
    public YieldVector PercentYield { get; private set; }
    /// <summary>Summed flat commerce effects.</summary>
    public CommerceVector FlatCommerce { get; private set; }
    /// <summary>Summed percent commerce effects.</summary>
    public CommerceVector PercentCommerce { get; private set; }
    /// <summary>Ledger snapshot, including all typed effects and keyed values.</summary>
    public Ledger Ledger => new(_lines);
    /// <summary>Add flat or percentage yields (keys yield.flat.food, yield.percent.production, etc.).</summary>
    public void AddYield(string source, YieldVector value, bool percent = false)
    {
        if (percent) PercentYield += value; else FlatYield += value;
        string prefix = percent ? "yield.percent." : "yield.flat.";
        Line(source, prefix + "food", value.Food); Line(source, prefix + "production", value.Production); Line(source, prefix + "commerce", value.Commerce);
    }
    /// <summary>Add flat or percentage commerce (keys commerce.flat.gold, commerce.percent.research, etc.).</summary>
    public void AddCommerce(string source, CommerceVector value, bool percent = false)
    {
        if (percent) PercentCommerce += value; else FlatCommerce += value;
        string prefix = percent ? "commerce.percent." : "commerce.flat.";
        Line(source, prefix + "gold", value.Gold); Line(source, prefix + "research", value.Research); Line(source, prefix + "culture", value.Culture); Line(source, prefix + "espionage", value.Espionage);
    }
    /// <summary>Add a mod-defined integer effect. Keys must be namespaced to avoid collisions.</summary>
    public void Add(string source, string key, long amount)
    {
        if (!key.Contains(':') || key.StartsWith(':') || key.EndsWith(':')) throw new ArgumentException("Modifier keys must be namespaced", nameof(key));
        Line(source, key, amount);
    }
    /// <summary>Add one data-driven effect. A typed key (<see cref="IsTypedKey"/>) feeds the matching yield or commerce
    /// vector exactly as <see cref="AddYield"/> or <see cref="AddCommerce"/> would; any other key is added as
    /// <see cref="ModifierEffect.LineKey"/>. The caller has already matched the effect's scope.</summary>
    public void Apply(string source, ModifierEffect effect)
    {
        long a = effect.Amount;
        switch (effect.Key)
        {
            case "yield.flat.food": AddYield(source, new(a, 0, 0)); break;
            case "yield.flat.production": AddYield(source, new(0, a, 0)); break;
            case "yield.flat.commerce": AddYield(source, new(0, 0, a)); break;
            case "yield.percent.food": AddYield(source, new(a, 0, 0), true); break;
            case "yield.percent.production": AddYield(source, new(0, a, 0), true); break;
            case "yield.percent.commerce": AddYield(source, new(0, 0, a), true); break;
            case "commerce.flat.gold": AddCommerce(source, new(a, 0, 0, 0)); break;
            case "commerce.flat.research": AddCommerce(source, new(0, a, 0, 0)); break;
            case "commerce.flat.culture": AddCommerce(source, new(0, 0, a, 0)); break;
            case "commerce.flat.espionage": AddCommerce(source, new(0, 0, 0, a)); break;
            case "commerce.percent.gold": AddCommerce(source, new(a, 0, 0, 0), true); break;
            case "commerce.percent.research": AddCommerce(source, new(0, a, 0, 0), true); break;
            case "commerce.percent.culture": AddCommerce(source, new(0, 0, a, 0), true); break;
            case "commerce.percent.espionage": AddCommerce(source, new(0, 0, 0, a), true); break;
            default: Add(source, effect.LineKey(), a); break;
        }
    }
    /// <summary>Whether a key names one component of the sink's own yield or commerce vectors, flat or percent
    /// (<c>yield.percent.food</c>, <c>commerce.flat.gold</c>, ...). These are the ledger keys
    /// <see cref="AddYield"/> and <see cref="AddCommerce"/> write, and need no <see cref="ModifierKeyDef"/>.</summary>
    public static bool IsTypedKey(string key) => key is "yield.flat.food" or "yield.flat.production" or "yield.flat.commerce"
        or "yield.percent.food" or "yield.percent.production" or "yield.percent.commerce"
        or "commerce.flat.gold" or "commerce.flat.research" or "commerce.flat.culture" or "commerce.flat.espionage"
        or "commerce.percent.gold" or "commerce.percent.research" or "commerce.percent.culture" or "commerce.percent.espionage";
    private void Line(string source, string key, long amount) { if (amount != 0) _lines.Add(new(source, key, amount)); }
    /// <summary>Sum all registered sources in their deterministic registration order.</summary>
    public static ModifierSink Collect(ModifierContext context)
    {
        var sink = new ModifierSink();
        foreach (var source in context.Sim.Content.Services.Get<IModifierSource>()) source.Contribute(context, sink);
        return sink;
    }
}

/// <summary>One data-driven modifier: a definition that carries it adds <see cref="Amount"/> under <see cref="Key"/> to
/// every calculation of <see cref="Scope"/> its holder takes part in, with the holder's id as the ledger source.
/// <see cref="Key"/> is either a <see cref="ModifierKeyDef"/> id, declared by the module that reads the key, or one of
/// the sink's typed vector keys (<see cref="ModifierSink.IsTypedKey"/>). A targeted key names the definition it is about
/// in <see cref="Target"/>, and the ledger line is written under <c>Key:Target</c>. Loading checks the key, its
/// provider and its target, so a misspelt key fails instead of doing nothing. Immutable once loaded.</summary>
public sealed class ModifierEffect
{
    private string? _lineKey;
    /// <summary>Calculation scope the effect joins.</summary>
    public ModifierScope Scope { get; init; }
    /// <summary>Declared modifier key, or a typed yield/commerce key.</summary>
    public string Key { get; init; } = "";
    /// <summary>Id of the definition a targeted key is about; null for an untargeted key.</summary>
    [ContentReference(typeof(ContentDef))]
    public string? Target { get; init; }
    /// <summary>Amount in the key's own unit (flat points, percent, hundredths, or 1 for a flag).</summary>
    public long Amount { get; init; }
    /// <summary>The ledger key the effect writes: <see cref="Key"/>, followed by <c>:Target</c> when targeted.</summary>
    public string LineKey() => _lineKey ??= Target is null ? Key : Key + ":" + Target;
}

/// <summary>Sums over data-driven modifier lists, for readers that weigh a definition's effects without a calculation
/// (planners, presentation).</summary>
public static class ModifierEffects
{
    /// <summary>Sum of the amounts a list gives a key in one scope, every target included.</summary>
    public static long Total(this ImmutableArray<ModifierEffect> effects, ModifierScope scope, string key)
    {
        long total = 0;
        foreach (var effect in effects)
            if (effect.Scope == scope && effect.Key == key) total += effect.Amount;
        return total;
    }
}

/// <summary>How a modifier key's amount reads.</summary>
[JsonConverter(typeof(CamelCaseEnumConverter<ModifierUnit>))]
public enum ModifierUnit
{
    /// <summary>Whole points: "+2 happiness".</summary>
    Flat,
    /// <summary>Percent: "+25% population growth".</summary>
    Percent,
    /// <summary>A switch that is on when the total is positive: "No unhealthiness from population".</summary>
    Flag,
    /// <summary>Hundredths of a point: "+0.5 gold".</summary>
    Hundredths,
}

/// <summary>A modifier key that content may name in a <see cref="ModifierEffect"/>. The module that reads the key
/// declares it in its own namespace, so the key's meaning, unit and label have one owner, and an effect that names a
/// key nobody reads fails loading. <see cref="ContentDef.Name"/> is the label; a targeted key's label may contain
/// <c>{target}</c>, replaced by the target's name.</summary>
public sealed class ModifierKeyDef : ContentDef
{
    /// <summary>How an amount reads.</summary>
    public ModifierUnit Unit { get; init; }
    /// <summary>Whether effects must name a <see cref="ModifierEffect.Target"/> (true) or must not (false).</summary>
    public bool Targeted { get; init; }
}

/// <summary>Enum names in lower camel case, as content writes them. Reading ignores case.</summary>
public sealed class CamelCaseEnumConverter<T>() : JsonStringEnumConverter<T>(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
    where T : struct, Enum;
