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
        // Foreign troops massed nearby in peacetime count at half weight: they could attack the turn war is declared.
        int danger = v.Threat + Pressure(v) / 2;
        int threat = (danger + tuning.AiThreatStrengthPerDefender - 1) / tuning.AiThreatStrengthPerDefender;
        return Math.Clamp(baseline + threat, baseline, tuning.AiMaximumGarrison);
    }

    /// <summary>Foreign land strength within two tiles of the city, counted only beyond a small escort's worth.</summary>
    public int Pressure(CityView v) => Math.Max(0, ctx.PressureAround(v.City.Position, 2) - PressureAllowance);
    internal const int PressureAllowance = 6;

    /// <summary>Field army beyond garrisons: grows with empire size, leader militarism and wartime.</summary>
    public int FieldTarget(IEnumerable<CityView> views)
    {
        if (ctx.Turn < 20) return 0;
        int cities = ctx.Cities.Count;
        int target = 1 + (cities * ctx.Weight("military") + 19) / 20;
        if (ctx.AtWarWithPlayers) target += 3 + cities / 2;
        else if (AiIntent.Read(ctx).War is not null) target += 3 + cities / 3;
        return target;
    }

    public int Want(IReadOnlyList<CityView> views) => views.Sum(DesiredGarrison) + FieldTarget(views);
}
