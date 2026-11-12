using Ariel.Content;

namespace Ariel.Ai;

/// <summary>Point values for one unit of each yield and commerce kind, shared so the city planner and any contributed
/// planner ranking content agree on scale instead of keeping private copies.</summary>
public static class PlannerPoints
{
    public const int Food = 8, Production = 8, CommerceYield = 5, Gold = 5, Research = 6, Culture = 3, Espionage = 1;

    public static long Yields(YieldVector yields) =>
        yields.Food * Food + yields.Production * Production + yields.Commerce * CommerceYield;

    public static long Commerce(CommerceVector commerce) =>
        commerce.Gold * Gold + commerce.Research * Research + commerce.Culture * Culture + commerce.Espionage * Espionage;
}
