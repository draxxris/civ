using Civ.Game;

namespace Civ.Mods.Ai;

/// <summary>How many soldiers the empire wants, shared by production and unit orders.</summary>
internal sealed class ArmyPlan(AiContext ctx)
{
    private readonly Lazy<int> _soldiers = new(() => ctx.Units.Count(u => ctx.Role(u) is UnitRole.Melee or UnitRole.Siege));

    public int Soldiers => _soldiers.Value;
    public int DesiredGarrison(CityView v)
    {
        if (ctx.Turn < 8 && v.Threat == 0) return 0;
        int want = 1 + (ctx.Turn >= 20 ? 1 : 0) + (v.Threat > 0 || ctx.Turn >= 40 && v.City.Id == ctx.Cities.Min(c => c.Id) ? 1 : 0);
        return Math.Min(want, 3);
    }

    /// <summary>Field army beyond garrisons: grows with empire size, leader militarism and wartime.</summary>
    public int FieldTarget(IEnumerable<CityView> views)
    {
        if (ctx.Turn < 20) return 0;
        int cities = ctx.Cities.Count;
        int target = 1 + (cities * ctx.Weight("military") + 19) / 20;
        if (ctx.AtWarWithPlayers) target += 3 + cities / 2;
        return target;
    }

    public int Want(IReadOnlyList<CityView> views) => views.Sum(DesiredGarrison) + FieldTarget(views);
}
