namespace Ariel.Content;

/// <summary>The commerce types the foundation registers (<c>data/commerce.json</c>) and the reads other modules make of
/// them by role: gold pays the treasury, research feeds the research module, culture the city's culture. A type a mod
/// registers is read by its key through <see cref="ContentDb.Commerce"/> and the mod's own API.</summary>
public static class FoundationCommerce
{
    /// <summary>Vector keys of the foundation's commerce types.</summary>
    public const string GoldKey = "gold", ResearchKey = "research", CultureKey = "culture";

    extension(CommerceVector vector)
    {
        /// <summary>The gold amount.</summary>
        public long Gold => vector[GoldKey];
        /// <summary>The research amount.</summary>
        public long Research => vector[ResearchKey];
        /// <summary>The culture amount.</summary>
        public long Culture => vector[CultureKey];
    }
}
