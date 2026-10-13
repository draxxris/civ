using Civ.Content.Schema;
using Civ.Modding;

namespace Civ.Mods.World;

/// <summary>World definitions and requirement identities. Gameplay systems follow separately.</summary>
public sealed class WorldMod : IMod
{
    public void Register(IModContext context)
    {
        ContentRegistration.Load<BonusClassDef>(context, "bonus-class.json");
        ContentRegistration.Load<BonusDef>(context, "bonus.json");
        ContentRegistration.Load<BuildDef>(context, "build.json");
        ContentRegistration.Load<FeatureDef>(context, "feature.json");
        ContentRegistration.Load<GoodyDef>(context, "goody.json");
        ContentRegistration.Load<ImprovementDef>(context, "improvement.json");
        ContentRegistration.Load<RouteDef>(context, "route.json");
        ContentRegistration.Load<TerrainDef>(context, "terrain.json");
        ContentRegistration.Evaluator<BonusDef>(context, "bonus");
        ContentRegistration.Evaluator<FeatureDef>(context, "feature");
        ContentRegistration.Evaluator<TerrainDef>(context, "terrain");
        ContentRegistration.Evaluator<ImprovementDef>(context, "improvement");
        ContentRegistration.Evaluator<RouteDef>(context, "route");
        ContentRegistration.Evaluator<BuildDef>(context, "build");
    }
}
