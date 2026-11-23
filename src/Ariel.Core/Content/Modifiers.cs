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
/// <summary>Cross-mod contribution source. Contribute must not change simulation state, and what it adds must depend
/// only on the context and the state: not on the sink it adds to, nor on anything outside the simulation.</summary>
public interface IModifierSource
{
    /// <summary>Append typed effects to a calculation's sink; ignore unsupported scopes.</summary>
    void Contribute(ModifierContext context, ModifierSink sink);
    /// <summary>The state domains (<see cref="StateDomain"/>) this source never reads, directly or through any rule it
    /// asks, for any scope. Inside kernel work a contribution is kept and used again until something it may read is
    /// written (<see cref="ModifierSink.Collect"/>), so a write confined to these domains leaves it valid. Null, the
    /// default, declares nothing: the source may read everything, and any write makes it compute again. A wrong
    /// declaration makes the game read stale values; <see cref="Simulation.VerifyWrites"/> catches it where a run
    /// exercises it.</summary>
    IReadOnlyList<string>? UnreadDomains => null;
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
    // Both are derived from the lines and dropped whenever a line is added, so repeated reads of an unchanged sink
    // share one snapshot. Each is built in full before it is published, so concurrent readers of a sink nobody is
    // adding to see either nothing or a complete value.
    private Ledger? _ledger;
    private Dictionary<string, long>? _totals;
    /// <summary>Ledger snapshot, including all typed effects and keyed values. Unchanged until the next line is
    /// added; a snapshot taken earlier never changes.</summary>
    public Ledger Ledger => _ledger ??= new(_lines);
    /// <summary>Summed amount for a key, zero when absent; equal to <c>Ledger.Total(key)</c> without building the
    /// snapshot.</summary>
    public long Total(string key)
    {
        var totals = _totals;
        if (totals is null)
        {
            totals = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var line in _lines) totals[line.Key] = totals.GetValueOrDefault(line.Key) + line.Amount;
            _totals = totals;
        }
        return totals.GetValueOrDefault(key);
    }
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
    private void Line(string source, string key, long amount)
    {
        if (amount == 0) return;
        _lines.Add(new(source, key, amount));
        _ledger = null; _totals = null;
    }
    /// <summary>A sink with the same lines and totals, which later additions to either leave apart.</summary>
    private ModifierSink Copy()
    {
        var copy = new ModifierSink { FlatYield = FlatYield, PercentYield = PercentYield, FlatCommerce = FlatCommerce,
            PercentCommerce = PercentCommerce, _ledger = _ledger, _totals = _totals };
        copy._lines.AddRange(_lines);
        return copy;
    }
    /// <summary>Add lines another sink made, updating the typed vectors exactly as the calls that made them did: a
    /// typed line carries one component of the vector its call added, and calls never add a zero line.</summary>
    private void Append(LedgerLine[] lines)
    {
        if (lines.Length == 0) return;
        foreach (var line in lines)
        {
            _lines.Add(line);
            long a = line.Amount;
            switch (line.Key)
            {
                case "yield.flat.food": FlatYield += new YieldVector(a, 0, 0); break;
                case "yield.flat.production": FlatYield += new YieldVector(0, a, 0); break;
                case "yield.flat.commerce": FlatYield += new YieldVector(0, 0, a); break;
                case "yield.percent.food": PercentYield += new YieldVector(a, 0, 0); break;
                case "yield.percent.production": PercentYield += new YieldVector(0, a, 0); break;
                case "yield.percent.commerce": PercentYield += new YieldVector(0, 0, a); break;
                case "commerce.flat.gold": FlatCommerce += new CommerceVector(a, 0, 0, 0); break;
                case "commerce.flat.research": FlatCommerce += new CommerceVector(0, a, 0, 0); break;
                case "commerce.flat.culture": FlatCommerce += new CommerceVector(0, 0, a, 0); break;
                case "commerce.flat.espionage": FlatCommerce += new CommerceVector(0, 0, 0, a); break;
                case "commerce.percent.gold": PercentCommerce += new CommerceVector(a, 0, 0, 0); break;
                case "commerce.percent.research": PercentCommerce += new CommerceVector(0, a, 0, 0); break;
                case "commerce.percent.culture": PercentCommerce += new CommerceVector(0, 0, a, 0); break;
                case "commerce.percent.espionage": PercentCommerce += new CommerceVector(0, 0, 0, a); break;
            }
        }
        _ledger = null; _totals = null;
    }
    /// <summary>Sum all registered sources in their deterministic registration order. Sources only read the state
    /// (<see cref="IModifierSource"/>), so while the simulation holds a read epoch (<see cref="Simulation.ReadEpoch"/>)
    /// the same context sums to the same sink: each thread keeps the sums of the open epoch and hands out copies, which
    /// callers may add to.
    /// <para>Across writes, inside kernel work, each source's lines for each context are kept in the simulation's
    /// modifier cache with the write stamp they were made at, and used again while nothing the source may read was
    /// written since: a source declares what it never reads (<see cref="IModifierSource.UnreadDomains"/>), and a
    /// writer declares what it writes (<see cref="GameCommand.WriteDomains"/>, <see cref="ITurnSystem.WriteDomains"/>).
    /// An undeclared source is used again only while nothing at all was written. A context with a proposed plot names
    /// a hypothetical plot by reference, so it is never kept.</para></summary>
    public static ModifierSink Collect(ModifierContext context)
    {
        var sim = context.Sim;
        if (Memo.TryGet(sim, context, out var known)) return known.Copy();
        ModifierSink sink;
        using (sim.Profiler.Measure(ProfileKind.Rule, "modifiers:collect"))
        {
            sink = new ModifierSink();
            var sources = sim.Content.Services.Get<IModifierSource>();
            if (!sim.KeepsAnswers || context.ProposedPlot is not null)
                foreach (var source in sources) source.Contribute(context, sink);
            else Contribute(sim, sources, context, sink);
        }
        Memo.Set(sim, context, sink.Copy());
        return sink;
    }
    private static void Contribute(Simulation sim, IReadOnlyList<IModifierSource> sources, ModifierContext context, ModifierSink sink)
    {
        var unread = UnreadMasks(sim.Content, sources);
        var cache = sim.ModifierCache;
        for (int i = 0; i < sources.Count; i++)
        {
            ulong mask = unread[i];
            if (sim.WritingNow(mask)) { sources[i].Contribute(context, sink); continue; }
            if (cache.TryGet(i, context, out var entry) && !sim.WrittenSince(mask, entry.Stamp))
            {
                sink.Append(entry.Lines);
                sim.Profiler?.Add(ProfileKind.Rule, "modifiers:reused", 0);
                continue;
            }
            long stamp = sim.WriteStamp;
            int start = sink._lines.Count;
            sources[i].Contribute(context, sink);
            cache.Set(i, context, new(sink._lines.GetRange(start, sink._lines.Count - start).ToArray(), stamp));
        }
    }
    // Each source's unread domains as a mask, for each content database; sources and domains are frozen with it.
    private static ulong[] UnreadMasks(ContentDb content, IReadOnlyList<IModifierSource> sources) =>
        UnreadMaskTable.GetValue(content, db => new(sources.Select(s => s.UnreadDomains is { } ids ? db.StateDomains.Mask(ids, out _) : 0UL).ToArray())).Masks;
    private sealed class UnreadMaskBox(ulong[] masks) { public readonly ulong[] Masks = masks; }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ContentDb, UnreadMaskBox> UnreadMaskTable = new();
    private static readonly ReadEpochMemo<ModifierContext, ModifierSink> Memo = new();
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
