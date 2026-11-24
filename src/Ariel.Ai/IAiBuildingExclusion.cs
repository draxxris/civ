using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.Ai;

/// <summary>Buildings the standard computer player leaves out of what its cities consider building, because they need
/// rules it does not know: a faith's temples, which only matter where a state religion and a city's faiths line up.
/// A mod that owns such buildings registers one (<c>context.RegisterService&lt;IAiBuildingExclusion&gt;(name, rule)</c>).
/// The city planner then never chooses them, as a general building or from a production wish; a mod's own planner
/// may still order one. The answer is asked once per content, so it depends on the definition alone.</summary>
public interface IAiBuildingExclusion
{
    /// <summary>Whether the city planner leaves <paramref name="building"/> out.</summary>
    bool Excludes(ContentDb content, BuildingDef building);
}
