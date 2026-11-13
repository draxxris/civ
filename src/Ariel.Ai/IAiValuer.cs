using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Ai;

/// <summary>How a mod adjusts what the standard computer player thinks one kind of thing is worth: a resource, a
/// building, a unit, a technology or a civic, each named by its subject type (<see cref="ResourceSubject"/>,
/// <see cref="BuildingSubject"/>, <see cref="UnitSubject"/>, <see cref="TechSubject"/>, <see cref="CivicSubject"/>).
/// Registered as a service under the closed interface, for example
/// <c>context.RegisterService&lt;IAiValuer&lt;TechSubject&gt;&gt;("tech_valuer", valuer)</c>. The planner that owns the
/// choice asks every registered valuer of that subject, by ordinal <see cref="IHasId.Id"/>, and adds the sum of their
/// answers to its own score for the candidate (see <see cref="AiContext.Adjustment(in TechSubject)"/>), before it ranks,
/// thresholds or divides by cost. Each subject documents the scale its points are on.
/// <para>Valuers change how much the AI wants something it already considers; they do not add candidates. To make
/// cities produce something the city planner never considers, wish for it (<see cref="IProductionWishes"/>); to make
/// the AI do something new, contribute a planner (<see cref="IAiPlanner"/>).</para>
/// <para>Like a planner, one instance serves every player of every game, possibly from several threads at once (the
/// AI scores candidates in parallel): keep no mutable fields, decide from the simulation and the subject alone, never
/// issue a command, and never draw from the RNG. Return an integer; the AI clamps the sum to
/// ±<see cref="AiValuation.Limit"/>. The id carries the owning mod's namespace (<c>mymod:tech_valuer</c>), and two
/// valuers of one subject with one id fail the content load.</para></summary>
public interface IAiValuer<TSubject> : IHasId where TSubject : struct
{
    /// <summary>Points to add to the AI's own score for <paramref name="subject"/>, for the player <paramref name="ctx"/>
    /// plays; zero when the mod has nothing to say about it.</summary>
    int Adjust(AiContext ctx, in TSubject subject);
}

/// <summary>Bounds shared by every <see cref="IAiValuer{TSubject}"/>.</summary>
public static class AiValuation
{
    /// <summary>The largest adjustment, either way, that the sum of a subject's valuers may make. Planners add it to
    /// integer scores; the bound keeps any sum of them far from overflow.</summary>
    public const int Limit = 1_000_000;
}

/// <summary>One more source of <paramref name="Resource"/> for the player, which <paramref name="Held"/> copies of it
/// are usable at its capital without it (imports included, as trades count them).
/// <para>Scale: <see cref="PlannerPoints"/>, as for a source that gives so much every turn. The AI's own worth of a
/// resource is about 30 points plus 8 for each happiness and 6 for each health it brings (the worker's connection
/// value); +40 makes a resource about as wanted as a new happy resource, −40 cancels it. Asked by city site scoring
/// and the worker's choice of plot, which weigh plots on half this scale and add half the adjustment, by the
/// worker's choice of build (in full), and by the Diplomacy mod for resources in deals (in full, as gold).</para>
/// <para><paramref name="Plot"/> is the plot the source would come from: a plot in a prospective city site, or one a
/// worker would connect. It is null for a resource received in a deal. A resource the player gives away is valued
/// as one more source for a player holding what it would have left.</para></summary>
public readonly record struct ResourceSubject(BonusDef Resource, long Held, TileId? Plot);

/// <summary>A building for one of the player's cities.
/// <para>Scale: <see cref="PlannerPoints"/>, the city planner's value of the building in that city before its
/// specialisation percent and its cost (a building's value is its yields and commerce in those points, its
/// happiness worth 2 to 16 a point and health 1 to 12 depending on the city's margin, and its flavors). +20 is about
/// what three more food a turn would add. Asked in parallel, for every building the city could build, each time the
/// city planner chooses its production. A wonder is still held back while the empire builds enough of them, and a
/// building whose adjusted value stays under the planner's threshold is not considered.</para></summary>
public readonly record struct BuildingSubject(BuildingDef Building, City City);

/// <summary>A unit one of the player's cities could train to fill a role it needs: a defender, an attacker, siege, a
/// ship, an aircraft, a settler, a worker or an explorer.
/// <para>Scale: the city planner's rating of units of one role, which picks one unit per role and never decides
/// whether the role is wanted. Soldiers rate their quality, strength squared times 100 over cost (about 5 to 150),
/// plus the leader's flavors; settlers, workers and explorers rate minus their cost. An adjustment larger than the
/// spread among one role's candidates, +200 say, makes a unit the role's choice wherever the city can train it. To
/// make cities want a unit they would not otherwise build, wish for it instead (<see cref="IProductionWishes"/>).</para></summary>
public readonly record struct UnitSubject(UnitDef Unit, City City);

/// <summary>A technology the player's team could research, now or as a step toward a goal.
/// <para>Scale: the research planner's value points before it divides by the remaining cost: 40, plus three times the
/// flavors, plus what the technology unlocks (a building or unit counts its flavors and yields, up to 400 in all),
/// plus resources and abilities. +100 roughly doubles the worth of an ordinary technology; a long-range goal sums the
/// values of every step on its route, so an adjustment also pulls research toward the technologies leading to it.
/// Asked in parallel for every selectable technology whenever the team chooses research. Free technologies are still
/// chosen by cost.</para></summary>
public readonly record struct TechSubject(TechDef Tech);

/// <summary>A civic the player could run, the one it runs in that option included.
/// <para>Scale: the civic planner's score. It sums the civic's flavors and AI weight with its open effects, about 8
/// points per happiness or 6 per health and 3 to 5 per percent of commerce or yield, each times half the number of
/// cities. The planner changes a civic only when the new one scores 15 more than the held one, and starts a revolution
/// only when the changes together gain 45, plus 60 per turn of anarchy. A civic's score enters a review the AI keeps
/// until its next review turn or until its civics, techs, wars, cities or era change, so a valuer that reads other
/// state is heard at the next review.</para></summary>
public readonly record struct CivicSubject(CivicDef Civic);
