namespace Ariel.Content;

/// <summary>Commerce vectors of the base game's three commerce types, in their order, and of the Espionage mod's type
/// when a test loads that mod and names an amount of it.</summary>
internal static class TestCommerce
{
    public static CommerceVector Vector(long gold, long research, long culture) =>
        CommerceVector.Of(("gold", gold), ("research", research), ("culture", culture));

    /// <summary>A vector with an espionage amount, for content that loads the Espionage mod. A zero amount leaves the key
    /// out, so the vector also suits content without the mod.</summary>
    public static CommerceVector Vector(long gold, long research, long culture, long espionage) => espionage == 0
        ? Vector(gold, research, culture)
        : CommerceVector.Of(("gold", gold), ("research", research), ("culture", culture), ("espionage", espionage));
}
