namespace Ariel.Content;

/// <summary>The commerce types the foundation registers (<c>data/commerce.json</c>) and the reads other modules make of
/// them by role: gold pays the treasury, research feeds the research module, culture the city's culture, espionage the
/// espionage feature. Any other loaded type is read through <see cref="ContentDb.Commerce"/>.</summary>
public static class FoundationCommerce
{
    /// <summary>Vector keys of the foundation's commerce types.</summary>
    public const string GoldKey = "gold", ResearchKey = "research", CultureKey = "culture", EspionageKey = "espionage";

    extension(CommerceVector vector)
    {
        /// <summary>The gold amount.</summary>
        public long Gold => vector[GoldKey];
        /// <summary>The research amount.</summary>
        public long Research => vector[ResearchKey];
        /// <summary>The culture amount.</summary>
        public long Culture => vector[CultureKey];
        /// <summary>The espionage amount.</summary>
        public long Espionage => vector[EspionageKey];
    }
}
