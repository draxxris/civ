using Ariel.Game;
using Ariel.Content.Schema;

namespace Ariel.Mods.Ai;

/// <summary>How many soldiers the empire wants, shared by production and unit orders.</summary>
internal sealed class ArmyPlan(AiContext ctx)
{
    public int Soldiers => ctx.CountUnits(UnitRole.Melee) + ctx.CountUnits(UnitRole.Siege);
    public int DesiredGarrison(CityView v)
    {
        if (ctx.Turn < 8 && v.Threat == 0) return 0;
        var tuning = ctx.Sim.Content.Get<HexOverridesDef>("foundation:hex_overrides");
        int baseline = ctx.Turn < 20 ? 1 : tuning.AiMinimumGarrison;
        int threat = (v.Threat + tuning.AiThreatStrengthPerDefender - 1) / tuning.AiThreatStrengthPerDefender;
        return Math.Clamp(baseline + threat, baseline, tuning.AiMaximumGarrison);
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
