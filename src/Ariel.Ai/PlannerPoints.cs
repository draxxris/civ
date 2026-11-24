using Ariel.Content;
using Ariel.Content.Schema;

namespace Ariel.Ai;

/// <summary>Point values for one unit of each yield and commerce kind, shared so the city planner and any contributed
/// planner ranking content agree on scale instead of keeping private copies. A commerce type's points are its row's
/// <see cref="CommerceDef.AiPoints"/>, so a mod's commerce type brings its own.</summary>
public static class PlannerPoints
{
    public const int Food = 8, Production = 8, CommerceYield = 5;

    public static long Yields(YieldVector yields) =>
        yields.Food * Food + yields.Production * Production + yields.Commerce * CommerceYield;

    /// <summary>Points for one unit of a loaded commerce type.</summary>
    public static int Of(CommerceType type) => ((CommerceDef)type.Definition).AiPoints;

    /// <summary>Points for one unit of gold.</summary>
    public static int Gold(ContentDb content) => Of(content.Commerce[FoundationCommerce.GoldKey]);

    public static long Commerce(ContentDb content, CommerceVector commerce)
    {
        long points = 0;
        foreach (var type in content.Commerce) points += commerce[type] * Of(type);
        return points;
    }
}
