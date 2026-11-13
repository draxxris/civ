using Ariel.Ai;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Religion;

/// <summary>The computer player's use of religion, in the slot the standard AI keeps for it
/// (<see cref="AiPlannerOrder.Religion"/>): adopt the best state religion, spread the faith with missionaries and purge
/// minorities with inquisitors.</summary>
internal sealed class ReligionAiPlanner : IAiPlanner
{
    public string Id => "religion:planner";
    public int Order => AiPlannerOrder.Religion;
    public void Plan(AiContext ctx) => new ReligionPlanner(ctx).Plan();
}

internal sealed class ReligionPlanner(AiContext ctx)
{
    public void Plan()
    {
        if (ctx.Sim.Service<IReligionService>() is not { } religion) return;
        Adopt(religion);
        foreach (var unit in ctx.Units.Where(ctx.Alive).Where(u => u.Health > 0 && !ctx.HasActed(u)).OrderBy(u => u.Id))
        {
            if ((unit.Extensions.Get<MilitaryUnitState>(ctx.Sim.Content)?.Activity ?? UnitActivity.Awake) != UnitActivity.Awake) continue;
            if (ctx.Def(unit).Inquisitor) Purge(unit, religion);
            else Spread(unit, religion);
        }
    }

    private void Adopt(IReligionService religion)
    {
        if (GovernmentStatus.IsAnarchy(ctx.Sim, ctx.PlayerId)) return;
        if (!ReligionData.Civics(ctx.Sim, ctx.PlayerId).Any(c => c.Rules.RequiresStateReligion)) return;
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
        string identity = ReligionIdentities.Identity(ctx.Sim.Content, id);
        if (city.Buildings.Any(b => ctx.Sim.Content.Get<BuildingDef>(b).GlobalReligionCommerce == identity)) value += ShrineValue;
        return value;
    }

    private void Purge(Unit unit, IReligionService religion)
    {
        if (!religion.IsInquisitionEnabled(ctx.Sim, ctx.PlayerId)) return;
        var unrest = ctx.Sim.Rules().CityUnrest;
        var targets = ctx.Cities.Where(c => religion.HasInquisitionTargets(ctx.Sim, c.Id))
            .OrderByDescending(c => unrest?.Unrest(ctx.Sim, c.Id) ?? 0)
            .ThenBy(c => ctx.Grid.Distance(unit.Position, c.Position)).ThenBy(c => c.Id);
        foreach (var city in targets)
            if (Visit(unit, city, new PerformMissionCommand { Player = ctx.PlayerId, Unit = unit.Id, Mission = ReligionMissions.Inquisition })) return;
    }

    private void Spread(Unit unit, IReligionService religion)
    {
        // Units name the faiths they spread by identity; missions and presence use the religion's own id.
        var spreads = ctx.Def(unit).ReligionSpreads.Where(p => p.Value > 0)
            .Select(p => ReligionIdentities.Religion(ctx.Sim.Content, p.Key)).OfType<string>().Order(StringComparer.Ordinal);
        foreach (var faith in spreads)
        {
            var targets = ctx.Cities.Where(c => !religion.Present(ctx.Sim, c.Id).Contains(faith))
                .OrderBy(c => ctx.Grid.Distance(unit.Position, c.Position)).ThenByDescending(c => c.Population).ThenBy(c => c.Id)
                .Concat(ForeignTargets(religion, unit, faith));
            foreach (var city in targets)
                if (Visit(unit, city, new PerformMissionCommand { Player = ctx.PlayerId, Unit = unit.Id, Mission = ReligionMissions.Spread, Subject = faith })) return;
        }
    }

    internal const int ForeignSpreadRange = 10;

    /// <summary>Once every own city has the faith, nearby cities of civilizations at peace come next: sharing a
    /// religion improves relations, and a holy city's shrine earns from every follower.</summary>
    private IEnumerable<City> ForeignTargets(IReligionService religion, Unit unit, string faith)
    {
        if (ctx.Cities.Any(c => !religion.Present(ctx.Sim, c.Id).Contains(faith))) return [];
        return ctx.Sim.State.Cities.Where(c => c.Owner != ctx.PlayerId && !AtWarWith(c.Owner)
                && !PlayerRules.IsBarbarian(ctx.Sim, c.Owner)
                && ctx.Sim.State.PlotAt(c.Position).Revealed.Contains(ctx.Team.Id)
                && ctx.Grid.Distance(unit.Position, c.Position) <= ForeignSpreadRange
                && !religion.Present(ctx.Sim, c.Id).Contains(faith))
            .OrderBy(c => ctx.Grid.Distance(unit.Position, c.Position)).ThenByDescending(c => c.Population).ThenBy(c => c.Id).ToList();
    }

    private bool AtWarWith(int player) => ctx.Team.Wars.Contains(ctx.Sim.State.PlayerById(player).TeamId);

    private bool Visit(Unit unit, City city, GameCommand command)
    {
        bool moved = ctx.MoveToward(unit, city.Position);
        bool used = unit.Position == city.Position && ctx.Issue(command);
        if (!moved && !used) return false;
        // A unit that moved has spent its turn even when it did not arrive, so it is not planned again this pass.
        ctx.MarkActed(unit);
        return true;
    }
}

/// <summary>Asks cities for one inquisitor when inquisitions are allowed, a city has a minority faith to purge, and no
/// inquisitor is alive or queued.</summary>
internal sealed class InquisitorWishes : IProductionWishes
{
    internal const long InquisitorScore = 500;
    public string Id => "religion:inquisitors";

    public IEnumerable<ProductionWish> Wishes(AiContext ctx, City city)
    {
        if (!Wanted(ctx)) yield break;
        foreach (var unit in ctx.Sim.Content.Get<UnitDef>().All.Where(u => u.Inquisitor))
            yield return new("cities:unit", unit.Id, InquisitorScore + 1000L / Math.Max(1, unit.Cost));
    }

    private static bool Wanted(AiContext ctx)
    {
        if (ctx.Sim.Service<IReligionService>() is not { } religion || !religion.IsInquisitionEnabled(ctx.Sim, ctx.PlayerId)) return false;
        if (ctx.Units.Any(u => ctx.Alive(u) && u.Health > 0 && ctx.Def(u).Inquisitor)) return false;
        if (ctx.Cities.SelectMany(c => c.ProductionQueue).Any(o => o.Kind == "cities:unit" && ctx.Sim.Content.Get<UnitDef>(o.ContentId).Inquisitor))
            return false;
        return ctx.Cities.Any(c => religion.HasInquisitionTargets(ctx.Sim, c.Id));
    }
}

/// <summary>A founding wonder has no effects of its own; this value stands for the holy city, its religion's spread and
/// the free missionaries.</summary>
internal sealed class FoundingWonderValuer : IAiValuer<BuildingSubject>
{
    internal const int FoundingValue = 80;
    public string Id => "religion:founding_valuer";
    public int Adjust(AiContext ctx, in BuildingSubject subject) => subject.Building.FoundsReligion is null ? 0 : FoundingValue;
}

/// <summary>A civic that needs a state religion is only worth adopting when one of the empire's religions is present,
/// so for an empire without one it is ruled out. The civic the player already holds is left alone: its score is what
/// a change is measured against.</summary>
internal sealed class StateReligionCivicValuer : IAiValuer<CivicSubject>
{
    public string Id => "religion:civic_valuer";

    public int Adjust(AiContext ctx, in CivicSubject subject)
    {
        if (ctx.Sim.Content.Attachment<CivicReligionDef>(subject.Civic.Id)?.RequiresStateReligion != true) return 0;
        if (ctx.Self.Civics.ContainsValue(subject.Civic.Id)) return 0;
        var religion = ctx.Sim.Service<IReligionService>()!;
        bool fits = religion.StateReligion(ctx.Sim, ctx.PlayerId) is not null
            || ctx.Cities.Any(city => religion.Present(ctx.Sim, city.Id).Count > 0);
        return fits ? 0 : -AiValuation.Limit;
    }
}
