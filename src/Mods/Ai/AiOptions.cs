using Civ.Content;
using Civ.Content.Schema;

namespace Civ.Mods.Ai;

/// <summary>What the player can plausibly build right now (technology, civilization and class-limit prefilters).
/// Final legality always comes from the production validator.</summary>
internal sealed class AiOptions
{
    private readonly AiContext _ctx;
    public IReadOnlyList<BuildingDef> Buildings { get; }
    public IReadOnlyList<UnitDef> Units { get; }

    public AiOptions(AiContext ctx)
    {
        _ctx = ctx;
        var civ = ctx.Sim.Content.Get<CivilizationDef>(ctx.Self.Civilization);
        var rc = new RequirementContext(ctx.Sim, ctx.PlayerId);
        var limited = LimitedClasses();
        Buildings = ctx.Catalog.Buildings.Where(b => PlausibleBuilding(b, civ, rc, limited)).ToArray();
        Units = ctx.Catalog.Units.Where(u => TechOk(u, rc) && civ.UnitOverrides.GetValueOrDefault(u.DefaultUnit, u.DefaultUnit) == u.Id
            && (u.ForceObsoleteTech is not { } o || !ctx.Sim.KnowsTech(ctx.PlayerId, o))).ToArray();
    }

    private bool TechOk(ContentDef def, RequirementContext rc) => _ctx.Sim.Content.Requirements.EvaluateKind(def.Requirements, rc, "tech").Success;

    private bool PlausibleBuilding(BuildingDef b, CivilizationDef civ, RequirementContext rc, HashSet<string> limited)
    {
        if (UnavailableMechanic(b)) return false;
        if (civ.BuildingOverrides.GetValueOrDefault(b.DefaultBuilding, b.DefaultBuilding) != b.Id) return false;
        if (b.ObsoleteTech is { } o && _ctx.Sim.KnowsTech(_ctx.PlayerId, o)) return false;
        if ((BuildingValuer.IsWonder(b) || b.Limits.Player >= 0) && limited.Contains(b.DefaultBuilding)) return false;
        return TechOk(b, rc);
    }

    private static bool UnavailableMechanic(BuildingDef b) => b.Capital || b.RequiresReligion
        || b.RequiresStateReligion || b.ReligionType is not null || b.RequiresPower;

    /// <summary>Limited classes that already exist elsewhere (world/team) or in this empire.</summary>
    private HashSet<string> LimitedClasses()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var city in _ctx.Sim.State.Cities)
            foreach (var id in city.Buildings)
            {
                var b = _ctx.Sim.Content.Get<BuildingDef>(id);
                bool global = b.Limits.World >= 0 || b.Limits.Team >= 0;
                if (global || b.Limits.Player >= 0 && city.Owner == _ctx.PlayerId) set.Add(b.DefaultBuilding);
            }
        return set;
    }

    public UnitDef? Best(int city, UnitRole role, Func<UnitDef, int> score, Func<UnitDef, bool>? filter = null)
    {
        var production = _ctx.Service<Civ.Game.ICityProductionService>();
        return Units.Where(u => _ctx.Catalog.Roles[u.Id] == role && (filter?.Invoke(u) ?? true))
            .OrderByDescending(u => score(u) + PersonalityValue(u)).ThenBy(u => u.Id, StringComparer.Ordinal)
            .FirstOrDefault(u => production.Validate(_ctx.Sim, city, "cities:unit", u.Id).Success);
    }

    private int PersonalityValue(UnitDef unit)
    {
        int score = _ctx.FlavorScore(unit.Flavors);
        foreach (var role in unit.UnitAiTypes.Where(r => r.Value))
            score += (_ctx.Leader?.UnitAiWeightModifiers.GetValueOrDefault(role.Key) ?? 0) / 10;
        return score;
    }

    public static int Quality(UnitDef u) => u.Strength * u.Strength * 100 / Math.Max(10, u.Cost);
    public static int DefenderQuality(UnitDef u) => u.Strength * u.Strength
        * Math.Max(10, 100 + u.CityDefense + u.HillsDefense / 2 + u.FirstStrikes * 15) / Math.Max(10, u.Cost + 40);
    public static bool Defends(UnitDef u) => u.UnitAiTypes.ContainsKey(AiCatalog.CityDefense);
    public static bool Attacks(UnitDef u) => u.UnitAiTypes.ContainsKey(AiCatalog.Attack) || u.UnitAiTypes.ContainsKey("military:unitai_attack_city");
}
