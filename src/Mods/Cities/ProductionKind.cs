using Civ.Content;
using Civ.Game;

namespace Civ.Mods.Cities;

internal sealed class ProductionKind<T>(string id, string label, Func<T, long> cost, ICityProductionService production) : IProductionKind
    where T : ContentDef
{
    private static readonly string[] HiddenBlockers =
        ["Civilization class replacement required", "Building cannot be constructed", "Unit cannot be trained", "Unknown building", "Unknown unit"];

    public string Id => id;
    public string Label => label;
    public bool IsContinuous => false;
    public IEnumerable<ContentDef> Candidates(ContentDb content) => content.Get<T>().All.Where(d => cost(d) >= 0);
    public ContentDef Definition(ContentDb content, string contentId) => content.Get<T>(contentId);
    public long BaseCost(ContentDef definition) => cost((T)definition);
    public bool IsVisible(RequirementResult eligibility) => !eligibility.Failures.Any(f => HiddenBlockers.Contains(f.Explanation));
    public RequirementResult Validate(Simulation sim, int city, string contentId) => production.Validate(sim, city, Id, contentId);
    public CityProductionResult Calculate(Simulation sim, int city, string contentId) => production.Calculate(sim, city, Id, contentId);
}

internal sealed class PlotSourceLabel : ILedgerSourceLabel
{
    public string? Label(Simulation sim, string source)
    {
        const string prefix = "cities:plot:";
        if (!source.StartsWith(prefix, StringComparison.Ordinal) || !int.TryParse(source[prefix.Length..], out var tile)
            || tile < 0 || tile >= sim.State.Plots.Length) return null;
        return sim.Content.Get<Civ.Content.Schema.TerrainDef>(sim.State.Plots[tile].Terrain).Name;
    }
}
