using Ariel.Content;
using Ariel.Game;

namespace Ariel.Ai;

/// <summary>Something a city of the computer player should want to produce, and how much: <paramref name="Kind"/> is a
/// production kind id (<c>cities:unit</c>, <c>cities:building</c>, or a mod's own), <paramref name="ContentId"/> what
/// to produce, and <paramref name="Score"/> its rank among the city planner's scored options.
/// <para>The city planner ranks every scored option on one scale, highest first, ties by ordinal id. It is not the
/// <see cref="PlannerPoints"/> scale itself: a building scores its value in those points times 1000 over its cost
/// plus 60 (and more for a slow build). For reference, an attacker for an army that is short of soldiers scores 80 to
/// about 300, naval, siege and air units in a war 170 to 390, a spy 150 plus 1000 over its cost, and an inquisitor 500
/// plus 1000 over its cost. Emergency defenders, settlers, workers and explorers come before every scored option, and
/// processes after them.</para></summary>
public readonly record struct ProductionWish(string Kind, string ContentId, long Score);

/// <summary>How a mod tells the standard computer player what its cities should produce for the mod's feature,
/// beside what the city planner values itself: spies for espionage, say. Registered as a service
/// (<c>context.RegisterService&lt;IProductionWishes&gt;(name, wishes)</c>); the city planner asks every one of them,
/// by ordinal <see cref="IHasId.Id"/>, each time it scores a city's options, so a wish sees what earlier choices this
/// turn already queued. It keeps a wish only when the player could plausibly build it now (technology, civilization
/// and obsolescence, as for its own options) and the city can produce it, then ranks it with its own options.
/// <para>Like a planner, one instance serves every player of every game, possibly at the same time: keep no mutable
/// fields, decide from the simulation alone, never issue a command, and never draw from the RNG. The id carries the
/// owning mod's namespace (<c>mymod:wishes</c>).</para></summary>
public interface IProductionWishes : IHasId
{
    /// <summary>What <paramref name="city"/>, one of the player's own, should want to produce now. Return nothing when
    /// the feature needs nothing; the order of the wishes does not matter.</summary>
    IEnumerable<ProductionWish> Wishes(AiContext ctx, City city);
}
