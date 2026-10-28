using Ariel.Content;
using Ariel.Game;

namespace Ariel.Mods.Cities;

internal sealed class ProductionKind<T>(string id, string label, Func<T, long> cost, ICityProductionService production,
    Func<T, QueueScope> scope, IReadOnlyList<ProductionCategory>? categories = null) : IProductionKind
    where T : ContentDef
{
    private static readonly string[] HiddenBlockers =
        ["Civilization class replacement required", "Building cannot be constructed", "Unit cannot be trained", "Unknown building", "Unknown unit"];

    public string Id => id;
    public string Label => label;
    public bool IsContinuous => false;
    public IReadOnlyList<ProductionCategory> Categories { get; } = categories ?? [];
    public QueueScope QueuedScope(Simulation sim, int city, ContentDef definition) => scope((T)definition);
    public IEnumerable<ContentDef> Candidates(ContentDb content) => content.Get<T>().All.Where(d => cost(d) >= 0);
    public ContentDef Definition(ContentDb content, string contentId) => content.Get<T>(contentId);
    public long BaseCost(ContentDef definition) => cost((T)definition);
    public bool IsVisible(RequirementResult eligibility) => !eligibility.Failures.Any(f => HiddenBlockers.Contains(f.Explanation));
    public RequirementResult Validate(Simulation sim, int city, string contentId) => production.Validate(sim, city, Id, contentId);
    /// <summary>One read-only memo scope for the whole batch: every candidate would otherwise repeat the whole-empire
    /// building, power and resource state that <see cref="CityReadScope"/> memoises.</summary>
    public IReadOnlyList<RequirementResult> ValidateBatch(Simulation sim, int city, IReadOnlyList<string> ids) =>
        CityReadScope.Run(sim, _ => ids.Select(id => Validate(sim, city, id)).ToArray());
    public CityProductionResult Calculate(Simulation sim, int city, string contentId) => production.Calculate(sim, city, Id, contentId);
}

/// <summary>The extra tab the building kind feeds. The kinds of content a city can build are few, so a category is what
/// keeps the wonders of a 600-building catalog from being buried among the ordinary buildings.</summary>
internal static class BuildingProductionCategories
{
    internal static readonly ProductionCategory Wonders = new("cities:wonder", "Wonders",
        definition => CityBuildingRules.IsWonder((Ariel.Content.Schema.BuildingDef)definition));

    internal static readonly IReadOnlyList<ProductionCategory> All = [Wonders];
}

/// <summary>How far a queued copy of a building reaches, read from the building limits the eligibility checks enforce.</summary>
internal static class BuildingScope
{
    /// <summary>A building is unique within its city. One whose tightest limit allows a single copy is also one per
    /// player, so a second city of the same player cannot queue it; different players still race for the same one.
    /// A limit that allows several copies (corporation buildings, for example) stays per city.
    /// <para><see cref="Ariel.Content.Schema.ClassLimits.NoLimit"/> is deliberately not consulted here: it exempts a
    /// building from the wonder counters (<see cref="CityBuildingRules.IsWonder"/>), not from the copy caps that
    /// <c>ProductionEligibility.Limits</c> enforces, so the queue scope must follow the caps. The palace declares
    /// <c>Player: 1, ExtraPlayer: 1</c> and therefore stays per city, which is what lets a civilization build its
    /// replacement palaces in parallel.</para></summary>
    public static QueueScope Of(Ariel.Content.Schema.BuildingDef b) =>
        SmallestAllowance(b.Limits) <= 1 ? QueueScope.Player : QueueScope.City;

    /// <summary>The tightest finite number of copies the limits allow, or <see cref="int.MaxValue"/> when unlimited.</summary>
    private static int SmallestAllowance(Ariel.Content.Schema.ClassLimits limits)
    {
        int allowance = int.MaxValue;
        if (limits.World >= 0) allowance = Math.Min(allowance, limits.World);
        if (limits.Team >= 0) allowance = Math.Min(allowance, limits.Team);
        if (limits.Player >= 0) allowance = Math.Min(allowance, limits.Player + limits.ExtraPlayer);
        return allowance;
    }
}

internal sealed class PlotSourceLabel : ILedgerSourceLabel
{
    public string? Label(Simulation sim, string source)
    {
        const string prefix = "cities:plot:";
        if (!source.StartsWith(prefix, StringComparison.Ordinal) || !int.TryParse(source[prefix.Length..], out var tile)
            || tile < 0 || tile >= sim.State.Plots.Length) return null;
        return sim.Content.Get<Ariel.Content.Schema.TerrainDef>(sim.State.Plots[tile].Terrain).Name;
    }
}
