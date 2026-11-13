using Ariel.Game;

namespace Ariel.Mods.Barbarians;

/// <summary>Whether something bars a barbarian unit from a plot it could otherwise enter, such as a wonder that keeps
/// barbarians out of its owner's lands. A mod registers one as a service
/// (<c>context.RegisterService&lt;IBarbarianBarrier&gt;(name, barrier)</c>); the Barbarians mod asks every registered
/// barrier, in registration order, before a barbarian moves without attacking, and an attack is never barred. Without
/// the Barbarians mod no barrier is asked. Queries never change state or use the RNG.</summary>
public interface IBarbarianBarrier
{
    /// <summary>Whether this barbarian unit may not enter the target plot.</summary>
    bool Blocks(Simulation sim, int barbarianUnit, Hex target);
}
