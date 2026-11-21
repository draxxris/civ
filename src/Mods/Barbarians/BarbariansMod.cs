using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Rules;
using Ariel.Modding;

namespace Ariel.Mods.Barbarians;

/// <summary>The barbarians: a faction row on the barbarian civilization (<see cref="BarbarianFaction"/>), which makes
/// its player a faction the base game restricts (never met, always at war, dearer research, the difficulty level's
/// combat rules) and the one that fields hostile villagers and rebels; land units spawned in unclaimed land and ships
/// off unclaimed coasts; and how every barbarian unit moves and attacks. Movement asks the base game's
/// <see cref="IMovementBarrier"/>s, not this mod, what bars a barbarian.</summary>
public sealed class BarbariansMod : IPreparableMod
{
    private static readonly ModJsonFile<FactionDef[]> Factions = new("data/faction.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [Factions];

    public void Register(IModContext context)
    {
        ContentRegistration.Load(context, Factions);
        context.TurnSystems.Register(new BarbarianSpawns());
        context.TurnSystems.Register(new BarbarianBehavior());
    }
}
