using Ariel.Content;

namespace Ariel.Ai;

/// <summary>One step of the standard computer player's turn (<c>ai:standard</c>). The AI's own planners and the
/// planners mods contribute are all registered the same way, as a service under this interface
/// (<c>context.RegisterService&lt;IAiPlanner&gt;(name, planner)</c>), and run one after another, ascending by
/// <see cref="Order"/>, then by ordinal <see cref="IHasId.Id"/>. A planner issues its own commands through
/// <see cref="AiContext.Issue"/>, so every later planner sees their effect.
/// <para>One registered instance serves every player of every game, and AI turns of different players can run at
/// the same time, so an implementation keeps no mutable fields: per-turn working state belongs in a local object
/// created inside <see cref="Plan"/>. Planners share nothing but the game state and the context's ledger of units
/// that already acted (<see cref="AiContext.HasActed"/>, <see cref="AiContext.MarkActed"/>). Decisions must depend
/// only on the simulation and its RNG and break ties by ordinal id.</para>
/// <para>The id carries the owning mod's namespace (<c>mymod:planner</c>).</para></summary>
public interface IAiPlanner : IHasId
{
    /// <summary>Position in the turn. <see cref="AiPlannerOrder"/> lists where the built-in planners run.</summary>
    int Order { get; }

    /// <summary>Make this planner's decisions for the player <paramref name="ctx"/> plays.</summary>
    void Plan(AiContext ctx);
}

/// <summary>Where the standard AI's own planners run, as <see cref="IAiPlanner.Order"/> values. They are spaced a
/// hundred apart so a contributed planner can take any slot between two of them; a planner that should act before
/// or after another contributed planner says so in its own value.</summary>
public static class AiPlannerOrder
{
    /// <summary>Choose research.</summary>
    public const int Research = 100;
    /// <summary>Declare war and make peace.</summary>
    public const int War = 200;
    /// <summary>Change civics.</summary>
    public const int Civics = 300;
    /// <summary>Answer and propose deals.</summary>
    public const int Diplomacy = 400;
    /// <summary>Send settlers to city sites and found cities.</summary>
    public const int Settlers = 500;
    /// <summary>Assign citizens. Every cached view is renewed first.</summary>
    public const int Citizens = 600;
    /// <summary>Spread the faith and purge minorities.</summary>
    public const int Religion = 700;
    /// <summary>Choose what every city builds. Every cached view is renewed first. The slots between
    /// <see cref="Religion"/> and this one suit planners that use units, great people above all, before cities
    /// plan production around them.</summary>
    public const int Cities = 1000;
    /// <summary>Improve plots.</summary>
    public const int Workers = 1100;
    /// <summary>Rebase, scout with and fly air units, before naval support and ground assaults.</summary>
    public const int Air = 1200;
    /// <summary>Garrisons, campaigns, assaults, fleets and explorers.</summary>
    public const int Military = 1300;
    /// <summary>Weigh espionage spending and move spies.</summary>
    public const int Espionage = 1400;
    /// <summary>Keep cities stable.</summary>
    public const int Stability = 1500;
    /// <summary>Set the commerce sliders.</summary>
    public const int Economy = 1600;
    /// <summary>Spend spare gold to hurry production.</summary>
    public const int Hurry = 1700;
}
