using Civ.Content.Schema;
using Civ.Game;

namespace Civ.Mods.Ai;

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
        int Count(string id) => ctx.Cities.Count(c => religion.Present(ctx.Sim, c.Id).Contains(id));
        var current = religion.StateReligion(ctx.Sim, ctx.PlayerId);
        var best = religion.Choices(ctx.Sim, ctx.PlayerId).OrderByDescending(r => Count(r.Id))
            .ThenBy(r => r.Id, StringComparer.Ordinal).FirstOrDefault();
        if (best is null || current is not null && Count(best.Id) <= Count(current)) return;
        ctx.Issue(new SetStateReligionCommand { Player = ctx.PlayerId, Religion = best.Id });
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
                .OrderBy(c => ctx.Grid.Distance(unit.Position, c.Position)).ThenByDescending(c => c.Population).ThenBy(c => c.Id);
            foreach (var city in targets)
                if (Visit(unit, city, new SpreadReligionCommand { Player = ctx.PlayerId, Unit = unit.Id, Religion = spread.Key })) return;
        }
    }

    private bool Visit(Unit unit, City city, GameCommand command)
    {
        bool moved = ctx.MoveToward(unit, city.Position);
        bool used = unit.Position == city.Position && ctx.Issue(command);
        if (!moved && !used) return false;
        ctx.Done.Add(unit.Id);
        return true;
    }
}
