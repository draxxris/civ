using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Ai;

internal sealed class ReligionPlanner(AiContext ctx)
{
    public void Plan()
    {
        var religion = ctx.Sim.Content.Services.Get<IReligionService>().SingleOrDefault();
        if (religion is null) return;
        Adopt(religion);
        foreach (var unit in ctx.Units.Where(ctx.Alive).Where(u => u.Health > 0 && !ctx.Done.Contains(u.Id)).OrderBy(u => u.Id))
        {
            if (ctx.MilitaryState(unit).Activity != UnitActivity.Awake) continue;
            if (ctx.Def(unit).Inquisitor) Purge(unit, religion);
            else Spread(unit, religion);
        }
    }

    private void Adopt(IReligionService religion)
    {
        if (GovernmentStatus.IsAnarchy(ctx.Sim, ctx.PlayerId)) return;
        if (!ctx.Self.Civics.Values.Any(c => ctx.Sim.Content.Get<CivicDef>(c).RequiresStateReligion)) return;
        // Validation rejects a switch during the cooldown, so asking would only burn a plan pass.
        if (ctx.Self.Extensions.Get<ReligionPlayerState>(ctx.Sim.Content)?.ConversionTimer > 0) return;
        int Count(string id) => StateReligionValue(religion, id);
        var current = religion.StateReligion(ctx.Sim, ctx.PlayerId);
        var best = religion.Choices(ctx.Sim, ctx.PlayerId).OrderByDescending(r => Count(r.Id))
            .ThenBy(r => r.Id, StringComparer.Ordinal).FirstOrDefault();
        if (best is null) return;
        int gain = Count(best.Id) - (current is null ? 0 : Count(current));
        if (gain <= 0) return;
        // The first adoption unlocks the state-religion civic, so it pays for its anarchy. A switch between faiths
        // buys presence only: a point is about one city, a turn of anarchy costs the whole empire, so require the
        // gain to exceed the dead turns it costs.
        if (current is not null && gain <= religion.PreviewConversion(ctx.Sim, ctx.PlayerId).AnarchyTurns) return;
        ctx.Issue(new SetStateReligionCommand { Player = ctx.PlayerId, Religion = best.Id });
    }

    internal const int HolyCityValue = 3, ShrineValue = 3;

    /// <summary>Own cities that follow the faith, plus a bonus for holding its holy city and more for its shrine there,
    /// since the shrine's income grows with the faith.</summary>
    public int StateReligionValue(IReligionService religion, string id)
    {
        int value = ctx.Cities.Count(c => religion.Present(ctx.Sim, c.Id).Contains(id));
        if (religion.HolyCity(ctx.Sim, id) is not { } holy || ctx.Sim.State.FindCity(holy) is not { } city || city.Owner != ctx.PlayerId)
            return value;
        value += HolyCityValue;
        if (city.Buildings.Any(b => ctx.Sim.Content.Get<BuildingDef>(b).GlobalReligionCommerce == id)) value += ShrineValue;
        return value;
    }

    private void Purge(Unit unit, IReligionService religion)
    {
        if (!religion.IsInquisitionEnabled(ctx.Sim, ctx.PlayerId)) return;
        var targets = ctx.Cities.Where(c => religion.HasInquisitionTargets(ctx.Sim, c.Id))
            .OrderByDescending(c => c.Extensions.TryGet<RevolutionCityState>(ctx.Sim.Content)?.InstabilityIndex ?? 0)
            .ThenBy(c => ctx.Grid.Distance(unit.Position, c.Position)).ThenBy(c => c.Id);
        foreach (var city in targets)
            if (Visit(unit, city, new PerformInquisitionCommand { Player = ctx.PlayerId, Unit = unit.Id })) return;
    }

    private void Spread(Unit unit, IReligionService religion)
    {
        foreach (var spread in ctx.Def(unit).ReligionSpreads.Where(p => p.Value > 0).OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var targets = ctx.Cities.Where(c => !religion.Present(ctx.Sim, c.Id).Contains(spread.Key))
                .OrderBy(c => ctx.Grid.Distance(unit.Position, c.Position)).ThenByDescending(c => c.Population).ThenBy(c => c.Id)
                .Concat(ForeignTargets(religion, unit, spread.Key));
            foreach (var city in targets)
                if (Visit(unit, city, new SpreadReligionCommand { Player = ctx.PlayerId, Unit = unit.Id, Religion = spread.Key })) return;
        }
    }

    internal const int ForeignSpreadRange = 10;

    /// <summary>Once every own city has the faith, nearby cities of civilizations at peace come next: sharing a
    /// religion improves relations, and a holy city's shrine earns from every follower.</summary>
    private IEnumerable<City> ForeignTargets(IReligionService religion, Unit unit, string faith)
    {
        if (ctx.Cities.Any(c => !religion.Present(ctx.Sim, c.Id).Contains(faith))) return [];
        return ctx.Sim.State.Cities.Where(c => c.Owner != ctx.PlayerId && !ctx.IsEnemy(c.Owner)
                && !WarRules.IsBarbarian(ctx.Sim, c.Owner)
                && ctx.Sim.State.PlotAt(c.Position).Revealed.Contains(ctx.Team.Id)
                && ctx.Grid.Distance(unit.Position, c.Position) <= ForeignSpreadRange
                && !religion.Present(ctx.Sim, c.Id).Contains(faith))
            .OrderBy(c => ctx.Grid.Distance(unit.Position, c.Position)).ThenByDescending(c => c.Population).ThenBy(c => c.Id).ToList();
    }

    private bool Visit(Unit unit, City city, GameCommand command)
    {
        bool moved = ctx.MoveToward(unit, city.Position);
        bool used = unit.Position == city.Position && ctx.Issue(command);
        if (!moved && !used) return false;
        // A unit that moved has spent its turn even when it did not arrive, so it is not planned again this pass.
        ctx.Done.Add(unit.Id);
        return true;
    }
}
