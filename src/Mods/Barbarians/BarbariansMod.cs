using Ariel.Game;
using Ariel.Modding;

namespace Ariel.Mods.Barbarians;

/// <summary>The barbarians: the hostile player, created when the first barbarian appears; land units spawned in
/// unclaimed land and ships off unclaimed coasts; and how every barbarian unit moves and attacks. The base game keeps
/// the hostile-faction flag (<see cref="MilitaryGameState.BarbarianPlayer"/>) and every rule that asks it, and reaches
/// this mod through <see cref="IHostileFaction"/> for hostile villagers, rebels and barriers.</summary>
public sealed class BarbariansMod : IMod
{
    public void Register(IModContext context)
    {
        context.RegisterService<IHostileFaction>("faction", new BarbarianFaction());
        context.TurnSystems.Register(new BarbarianSpawns());
        context.TurnSystems.Register(new BarbarianBehavior());
    }
}
