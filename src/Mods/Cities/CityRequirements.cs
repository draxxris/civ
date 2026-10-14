using Civ.Content;
using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.Cities;

internal sealed class CityRequirements(ICityWorkService work) : ICityRequirementService
{
    public string? Evaluate(Requirement.Leaf leaf, RequirementContext context)
    {
        bool? success = leaf.Kind switch
        {
            "condition" => Condition(leaf, context),
            "culture-level" => context.City is { } city && city.Culture >= context.Sim.Content.Get<S.CultureLevelDef>(leaf.Id).SpeedThresholds[context.Sim.Settings.GameSpeed],
            "era" => CityData.Era(context.Sim, context.Player).Id == leaf.Id,
            "bonus" => Vicinity(leaf, context),
            _ => null
        };
        return success switch { true => null, false => $"Required {leaf.Kind}: {leaf.Id}", null => $"Requirement {leaf.Kind} {leaf.Id} has unavailable runtime state" };
    }
    private static bool? Condition(Requirement.Leaf leaf, RequirementContext context)
    {
        var sim = context.Sim;
        if (leaf.Id is "foundation:coastal" or "foundation:river" or "foundation:fresh_water") return Location(leaf, context);
        if (leaf.Id is "foundation:any_religion" or "foundation:state_religion")
        {
            var religion = sim.Content.Services.Get<IReligionService>().SingleOrDefault();
            return religion is null ? null : religion.EvaluateCondition(leaf, context) is null;
        }
        return leaf.Id switch
        {
            "foundation:population" => context.City?.Population >= (leaf.Count ?? 1),
            "foundation:city_count" => CityData.Owned(sim, context.Player).Count() >= (leaf.Count ?? 1),
            "foundation:team_count" => sim.State.Teams.Count >= (leaf.Count ?? 1),

            _ => null
        };
    }
    private static bool Location(Requirement.Leaf leaf, RequirementContext context)
    {
        var sim = context.Sim;
        var tile = context.Plot ?? (context.City is { } c ? CityData.Tile(sim, c) : (TileId?)null);
        if (tile is not { } t) return false;
        return leaf.Id switch
        {
            "foundation:coastal" => CityData.Coastal(sim, t, leaf.Count ?? 0),
            "foundation:river" => sim.State.Plots[t.Value].RiverEdges != 0,
            _ => sim.Content.Services.Get<IWaterService>().Single().IsFreshWater(sim, t)
        };
    }
    private bool Vicinity(Requirement.Leaf leaf, RequirementContext context)
    {
        if (context.City is not { } city) return false;
        var def = context.Sim.Content.Get<S.BonusDef>(leaf.Id);
        if (def.RevealTech is { } reveal && !context.Sim.KnowsTech(context.Player, reveal)) return false;
        if (def.ObsoleteTech is { } obsolete && context.Sim.KnowsTech(context.Player, obsolete)) return false;
        int count = work.WorkArea(context.Sim, city.Id).Count(t => context.Sim.State.Plots[t.Value].Bonus == leaf.Id && context.Sim.State.Plots[t.Value].Owner == context.Player);
        return count >= (leaf.Count ?? 1);
    }
}
internal sealed class BuildingRequirement(string kind) : IContentRequirementEvaluator
{
    public string Kind => kind;
    public Type DefinitionType => typeof(S.BuildingDef);
    public string? Evaluate(Requirement.Leaf leaf, RequirementContext context)
    {
        int needed = leaf.Count ?? 1;
        if (kind == "building") return context.City?.Buildings.Count(b => b == leaf.Id) >= needed ? null : "Requires local building: " + leaf.Id;
        var sim = context.Sim;
        string category = sim.Content.Get<S.BuildingDef>(leaf.Id).DefaultBuilding;
        if (leaf.Param == 1)
        {
            needed = Math.Max(1, needed * (100 + CityData.World(sim).BuildingClassPrereqModifier) / 100);
            int count = CityData.Owned(sim, context.Player).Sum(c => Count(sim, c, category));
            return count >= needed ? null : $"Requires {needed} empire buildings of class: {category}";
        }
        return context.City is { } city && Count(sim, city, category) >= needed ? null : "Requires local building class: " + category;
    }
    private static int Count(Simulation sim, City city, string category) => city.Buildings.Count(b => sim.Content.Get<S.BuildingDef>(b).DefaultBuilding == category);
}
