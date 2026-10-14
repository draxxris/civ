using Civ.Game;

namespace Civ.Content;

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
/// <summary>Target scope of a modifier calculation.</summary>
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
    private void Line(string source, string key, long amount) { if (amount != 0) _lines.Add(new(source, key, amount)); }
    /// <summary>Sum all registered sources in their deterministic registration order.</summary>
    public static ModifierSink Collect(ModifierContext context)
    {
        var sink = new ModifierSink();
        foreach (var source in context.Sim.Content.Services.Get<IModifierSource>()) source.Contribute(context, sink);
        return sink;
    }
}
