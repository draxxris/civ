namespace Ariel.Content;

/// <summary>Commerce vectors of the four commerce types the tests' content registers, in their order.</summary>
internal static class TestCommerce
{
    public static CommerceVector Vector(long gold, long research, long culture, long espionage) =>
        CommerceVector.Of(("gold", gold), ("research", research), ("culture", culture), ("espionage", espionage));
}
