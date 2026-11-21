using Ariel.Modding;

namespace Ariel.Mods.GlobalWarming;

/// <summary>Global warming: city pollution against the world's forest defence, rolled every turn, melting, drying and
/// flooding plots one step at a time. The mod keeps each feature's warming defence in its own
/// <see cref="FeatureGlobalWarmingDef"/> rows; the base game keeps the rest of what the rolls read (the
/// <c>GLOBAL_WARMING_*</c> rule constants, the cities' environmental unhealth) and how a changed plot is settled
/// (<see cref="Ariel.Game.ITerrainChanges"/>, whose <see cref="Ariel.Game.TerrainChanged"/> event moves units off a
/// flooded plot and refreshes sight). Without this mod the world never warms.</summary>
public sealed class GlobalWarmingMod : IPreparableMod
{
    /// <summary>What features mean to warming, attached to the base features.</summary>
    private static readonly ModJsonFile<FeatureGlobalWarmingDef[]> Features = new("data/feature-global-warming.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [Features];

    public void Register(IModContext context)
    {
        Ariel.Rules.ContentRegistration.Load(context, Features);
        context.TurnSystems.Register(new GlobalWarmingSystem());
    }
}
