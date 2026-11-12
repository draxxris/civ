using Ariel.Ai;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Modding;

namespace Ariel.TestMods.Wishes;

/// <summary>Adds a unit the computer player has no use for, a courier that cannot fight, settle, work or explore, and
/// a production wish for it. The wish starts at <see cref="FirstTurn"/>, so earlier turns show the AI ignoring it.</summary>
public sealed class ProductionWishProbeMod : IMod
{
    public const string Courier = "production_wish_probe:courier";
    public const int FirstTurn = 45;

    public void Register(IModContext context)
    {
        context.Content<UnitDef>().Register(new()
        {
            Id = Courier, Name = "Courier", Cost = 10, Moves = 1, Domain = "military:domain_land", DefaultUnit = Courier,
        });
        context.RegisterService<IProductionWishes>("wishes", new CourierWish());
    }

    /// <summary>One courier per player, wished in every city until one is queued or alive, above every scored option.</summary>
    private sealed class CourierWish : IProductionWishes
    {
        public string Id => "production_wish_probe:wishes";

        public IEnumerable<ProductionWish> Wishes(AiContext ctx, City city)
        {
            if (ctx.Turn < FirstTurn || ctx.Units.Any(u => u.Type == Courier)
                || ctx.Cities.Any(c => c.ProductionQueue.Any(o => o.ContentId == Courier))) yield break;
            yield return new("cities:unit", Courier, 100_000);
        }
    }
}
