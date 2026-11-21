using Ariel.Modding;

namespace Ariel.Mods.GlobalWarming;

/// <summary>Global warming: city pollution against the world's forest defence, rolled every turn, melting, drying and
/// flooding plots one step at a time. The base game keeps what the rolls read (<c>FeatureDef.WarmingDefense</c>, the
/// <c>GLOBAL_WARMING_*</c> rule constants, the cities' environmental unhealth) and how a changed plot is settled
/// (<see cref="Ariel.Game.ITerrainChanges"/>, whose <see cref="Ariel.Game.TerrainChanged"/> event moves units off a
/// flooded plot and refreshes sight). Without this mod the world never warms.</summary>
public sealed class GlobalWarmingMod : IMod
{
    public void Register(IModContext context) => context.TurnSystems.Register(new GlobalWarmingSystem());
}
