namespace Ariel.Content;

/// <summary>The failures a production rule emits when the definition's own cost forbids it. Callers that pay a
/// different price (a great person, a prophet, a relocated palace) filter exactly these out, so the text lives here
/// instead of being compared as a literal in four other files.</summary>
public static class ProductionFailures
{
    public const string BuildingCost = "Building cannot be constructed";
    public const string UnitCost = "Unit cannot be trained";

    public static bool IsCostOnly(RequirementFailure failure) => failure.Explanation is BuildingCost or UnitCost;
}
