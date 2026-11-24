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
/// only on the context and the state: not on the sink it adds to, nor on anything outside the simulation.
/// <para>A source may declare the state domains it never reads (<see cref="IStateReader.UnreadDomains"/>). Inside
/// kernel work a contribution is kept and used again until something it may read is written
/// (<see cref="ModifierSink.Collect"/>), so a write confined to those domains leaves it valid. A source that declares
/// nothing may read everything, and any write makes it compute again.</para></summary>
public interface IModifierSource : IStateReader
{
    /// <summary>Append typed effects to a calculation's sink; ignore unsupported scopes.</summary>
    void Contribute(ModifierContext context, ModifierSink sink);
    /// <summary>Whether the source may add anything to a calculation of this scope. True, the default, for every
    /// scope. A source that adds nothing to some scope says so, and <see cref="ModifierSink.Collect"/> does not ask it
    /// there: a reader that collects a nested calculation of a scope then depends only on the sources that contribute
    /// to it (<see cref="ModifierSources.UnreadThroughCalculation"/>). Answered once per content database.
    /// <see cref="Game.Simulation.VerifyWrites"/> asks a skipped source anyway and throws if it adds a line.</summary>
    bool ContributesTo(ModifierScope scope) => true;
}

/// <summary>Declarations for readers of whole calculations.</summary>
public static class ModifierSources
{
    /// <summary>What a reader that collects a nested calculation of <paramref name="scope"/> may declare unread
    /// (<see cref="IStateReader.UnreadDomains"/>), given its own list: the part of <paramref name="own"/> that every other
    /// source contributing to that scope also declares. <paramref name="self"/>, when the reader is itself a source, is
    /// left out, since its own declaration already covers what it adds there. Two sources that each collect the
    /// other's scope this way would recurse; neither may.</summary>
    public static IReadOnlyList<string> UnreadThroughCalculation(IReadOnlyList<string> own, ContentDb content, ModifierScope scope,
        IModifierSource? self = null) =>
        StateDomainRegistry.UnreadByAll(own, content,
            content.Services.Get<IModifierSource>().Where(s => !ReferenceEquals(s, self) && s.ContributesTo(scope)));
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
    /// <summary>An empty sink. Its commerce vectors carry the keys of whatever is added.</summary>
    public ModifierSink() { }
    /// <summary>An empty sink whose commerce vectors start with every loaded commerce type, so they and the ledger lines
    /// <see cref="AddCommerce"/> writes follow the database's commerce order.</summary>
    public ModifierSink(CommerceTypes commerce) { FlatCommerce = PercentCommerce = commerce.Zero; }
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
    /// <summary>Add flat or percentage commerce (keys commerce.flat.gold, commerce.percent.research, etc.): one line per
    /// non-zero amount, in the order of the sink's commerce vector.</summary>
    public void AddCommerce(string source, CommerceVector value, bool percent = false)
    {
        var total = (percent ? PercentCommerce : FlatCommerce) + value;
        if (percent) PercentCommerce = total; else FlatCommerce = total;
        if (total.Layout is not { } layout) return;
        var keys = percent ? layout.PercentKeys : layout.FlatKeys;
        bool same = ReferenceEquals(layout, value.Layout);
        for (int i = 0; i < layout.Count; i++) Line(source, keys[i], same ? value.At(i) : value[layout[i]]);
    }
    /// <summary>Add a mod-defined integer effect. Keys must be namespaced to avoid collisions.</summary>
    public void Add(string source, string key, long amount)
    {
        if (!key.Contains(':') || key.StartsWith(':') || key.EndsWith(':')) throw new ArgumentException("Modifier keys must be namespaced", nameof(key));
        Line(source, key, amount);
    }
    /// <summary>Add one data-driven effect. A typed key (<see cref="CommerceTypes.IsTypedKey"/>) feeds the matching yield or commerce
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
            default:
                if (CommerceKeyOf(effect.Key, out bool percent) is { IsEmpty: false } commerce) AddCommerce(source, effect.Key, commerce, a, percent);
                else Add(source, effect.LineKey(), a);
                break;
        }
    }
    /// <summary>Whether a key names one component of the sink's own yield vectors, flat or percent
    /// (<c>yield.percent.food</c>, ...). Typed commerce keys depend on the loaded commerce types
    /// (<see cref="CommerceTypes.IsTypedKey"/>). These are the ledger keys <see cref="AddYield"/> and
    /// <see cref="AddCommerce"/> write, and need no <see cref="ModifierKeyDef"/>.</summary>
    public static bool IsYieldKey(string key) => key is "yield.flat.food" or "yield.flat.production" or "yield.flat.commerce"
        or "yield.percent.food" or "yield.percent.production" or "yield.percent.commerce";
    /// <summary>The commerce type key of a typed commerce key (<c>commerce.flat.gold</c> gives <c>gold</c>), or an empty
    /// span when the key has neither prefix.</summary>
    public static ReadOnlySpan<char> CommerceKeyOf(string key, out bool percent)
    {
        const string flat = "commerce.flat.", percentPrefix = "commerce.percent.";
        percent = key.StartsWith(percentPrefix, StringComparison.Ordinal);
        if (percent) return key.AsSpan(percentPrefix.Length);
        return key.StartsWith(flat, StringComparison.Ordinal) ? key.AsSpan(flat.Length) : default;
    }
    // One typed commerce line: the amount joins the vector under its type and the line keeps the typed key.
    private void AddCommerce(string source, string lineKey, ReadOnlySpan<char> type, long amount, bool percent)
    {
        if (percent) PercentCommerce = PercentCommerce.Plus(type, amount); else FlatCommerce = FlatCommerce.Plus(type, amount);
        Line(source, lineKey, amount);
    }
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
                default:
                    if (CommerceKeyOf(line.Key, out bool percent) is { IsEmpty: false } type)
                    {
                        if (percent) PercentCommerce = PercentCommerce.Plus(type, a); else FlatCommerce = FlatCommerce.Plus(type, a);
                    }
                    break;
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
    /// written since: a source declares what it never reads (<see cref="IStateReader.UnreadDomains"/>), and no source reads a private
    /// domain (<see cref="StateDomain.Private"/>), and a
    /// writer declares what it writes (<see cref="GameCommand.WriteDomains"/>, <see cref="ITurnSystem.WriteDomains"/>).
    /// An undeclared source is used again only while nothing at all was written. Only city, player and team
    /// calculations are kept (<see cref="Kept"/>).</para></summary>
    public static ModifierSink Collect(ModifierContext context)
    {
        var sim = context.Sim;
        if (Memo.TryGet(sim, context, out var known)) return known.Copy();
        ModifierSink sink;
        using (sim.Profiler.Measure(ProfileKind.Rule, "modifiers:collect"))
        {
            sink = new ModifierSink(sim.Content.Commerce);
            var sources = sim.Content.Services.Get<IModifierSource>();
            var table = Table(sim.Content, sources);
            var asks = table.Asks[(int)context.Scope];
            if (!sim.KeepsAnswers || !Kept(context))
            {
                for (int i = 0; i < sources.Count; i++)
                    if (asks[i]) sources[i].Contribute(context, sink);
                    else if (sim.VerifyWrites) VerifySkipped(sources[i], context);
            }
            else Contribute(sim, sources, table.Masks, asks, context, sink);
        }
        Memo.Set(sim, context, sink.Copy());
        return sink;
    }
    /// <summary>Whether a calculation's contributions are kept across writes: city, player and team calculations, which
    /// every turn system and planner asks again. Unit and plot calculations are not: combat and placement previews ask
    /// them once each, often of a probe unit or a proposed plot that is never asked again, so keeping them only filled
    /// and emptied the table.</summary>
    private static bool Kept(ModifierContext context) => context.ProposedPlot is null
        && context.Scope is ModifierScope.City or ModifierScope.Player or ModifierScope.Team;
    private static void Contribute(Simulation sim, IReadOnlyList<IModifierSource> sources, ulong[] unread, bool[] asks,
        ModifierContext context, ModifierSink sink)
    {
        var cache = sim.ModifierCache;
        for (int i = 0; i < sources.Count; i++)
        {
            if (!asks[i]) { if (sim.VerifyWrites) VerifySkipped(sources[i], context); continue; }
            ulong mask = unread[i];
            if (sim.WritingNow(mask)) { sources[i].Contribute(context, sink); continue; }
            if (cache.TryGet(i, context, out var entry) && !sim.WrittenSince(mask, entry.Stamp))
            {
                if (sim.VerifyWrites) Verify(sources[i], context, entry.Lines);
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
    private static void Verify(IModifierSource source, ModifierContext context, LedgerLine[] kept)
    {
        var fresh = new ModifierSink(context.Sim.Content.Commerce);
        source.Contribute(context, fresh);
        if (fresh._lines.SequenceEqual(kept)) return;
        int at = 0;
        while (at < kept.Length && at < fresh._lines.Count && kept[at] == fresh._lines[at]) at++;
        throw new InvalidOperationException($"Modifier source {source.GetType().Name} kept a stale contribution for a "
            + $"{context.Scope} calculation (player {context.Player}, city {context.City?.Id}, unit {context.Unit?.Id}) on turn "
            + $"{context.Sim.State.Turn}: line {at} kept {(at < kept.Length ? kept[at] : null)}, now "
            + $"{(at < fresh._lines.Count ? fresh._lines[at] : null)}. A writer wrote what the source reads without declaring "
            + "it, or the source reads a domain it declares unread.");
    }
    private static void VerifySkipped(IModifierSource source, ModifierContext context)
    {
        var fresh = new ModifierSink(context.Sim.Content.Commerce);
        source.Contribute(context, fresh);
        if (fresh._lines.Count > 0)
            throw new InvalidOperationException($"Modifier source {source.GetType().Name} says it adds nothing to a "
                + $"{context.Scope} calculation but added {fresh._lines[0]}.");
    }
    // Each source's unread domains as a mask, and whether it is asked for each scope, for each content database;
    // sources and domains are frozen with it.
    private static SourceTable Table(ContentDb content, IReadOnlyList<IModifierSource> sources) =>
        SourceTables.GetValue(content, db => new(sources.Select(s => db.StateDomains.UnreadMask(s, db)).ToArray(),
            Enum.GetValues<ModifierScope>().Select(scope => sources.Select(s => s.ContributesTo(scope)).ToArray()).ToArray()));
    private sealed class SourceTable(ulong[] masks, bool[][] asks) { public readonly ulong[] Masks = masks; public readonly bool[][] Asks = asks; }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ContentDb, SourceTable> SourceTables = new();
    private static readonly ReadEpochMemo<ModifierContext, ModifierSink> Memo = new();
}

/// <summary>One data-driven modifier: a definition that carries it adds <see cref="Amount"/> under <see cref="Key"/> to
/// every calculation of <see cref="Scope"/> its holder takes part in, with the holder's id as the ledger source.
/// <see cref="Key"/> is either a <see cref="ModifierKeyDef"/> id, declared by the module that reads the key, or one of
/// the sink's typed vector keys (<see cref="CommerceTypes.IsTypedKey"/>). A targeted key names the definition it is about
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
