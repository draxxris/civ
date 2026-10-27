using System.Globalization;
using Civ.Content;
using Civ.Content.Schema;
using Civ.Game;
using SpecialistDef = Civ.Content.Schema.SpecialistDef;
namespace Civ.Mods.Ai;

internal sealed class GreatPersonPlanner(AiContext ctx)
{
    private sealed record Choice(Hex Destination, GameCommand Command, long Score, string Key);
    private readonly Dictionary<int, YieldVector> _yields = new();

    public void Plan()
    {
        var service = ctx.Sim.Content.Services.Get<IGreatPeopleService>().SingleOrDefault();
        if (service is null) return;
        foreach (var unit in ctx.Units.OrderBy(u => u.Id))
        {
            if (!ctx.Alive(unit) || unit.Health <= 0 || ctx.Done.Contains(unit.Id) || !IsGreatPerson(ctx.Def(unit))) continue;
            PlanUnit(unit, service);
        }
    }

    private static bool IsGreatPerson(UnitDef def) => def.GreatPeoples.Any(p => p.Value) || def.GreatGeneral
        || def.BaseDiscover > 0 || def.DiscoverMultiplier > 0 || def.BaseHurry > 0 || def.HurryMultiplier > 0
        || def.BaseTrade > 0 || def.TradeMultiplier > 0 || def.GreatWorkCulture > 0 || def.LeaderPromotion is not null || def.LeaderExperience > 0;

    private void PlanUnit(Unit unit, IGreatPeopleService service)
    {
        long goldenAgeValue = GoldenAgeValue() / Math.Max(1, service.GoldenAgeUnitsRequired(ctx.Sim, ctx.PlayerId));
        var choices = CityChoices(unit, service, goldenAgeValue).Concat(LeadChoices(unit, service)).ToList();
        var actions = service.AvailableActions(ctx.Sim, ctx.PlayerId, unit.Id);
        var discover = actions.FirstOrDefault(a => a.Command == "great_people:discover_tech");
        long amount = discover?.Amount ?? 0;
        if (goldenAgeValue * 100 / Math.Max(1, amount) > 60 && service.GoldenAgeTurnsRemaining(ctx.Sim, ctx.PlayerId) == 0
            && ctx.Self.Extensions.Get<CityPlayerState>(ctx.Sim.Content)?.GoldenAge != true
            && actions.Any(a => a.Command == "great_people:golden_age"))
            choices.Add(new(unit.Position, new GoldenAgeGreatPersonCommand { Player = ctx.PlayerId, UnitId = unit.Id }, 5000, "golden-age"));
        if (discover is not null && (ctx.Turn > 80 || amount >= goldenAgeValue))
            choices.Add(new(unit.Position, Command(unit, discover)!, 4000, discover.Command));
        foreach (var choice in choices.OrderByDescending(c => c.Score)
            .ThenBy(c => ctx.Grid.Distance(unit.Position, c.Destination)).ThenBy(c => c.Key, StringComparer.Ordinal))
        {
            bool moved = ctx.MoveToward(unit, choice.Destination);
            bool used = unit.Position == choice.Destination && ctx.Issue(choice.Command);
            if (!moved && !used) continue;
            ctx.Done.Add(unit.Id);
            return;
        }
    }

    private IEnumerable<Choice> CityChoices(Unit unit, IGreatPeopleService service, long goldenAgeValue)
    {
        foreach (var city in ctx.Sim.State.Cities.OrderBy(c => c.Id))
        {
            if (city.Owner == ctx.PlayerId)
                foreach (var shrine in Shrines(unit, city)) yield return shrine;
            foreach (var action in service.AvailableActions(ctx.Sim, ctx.PlayerId, unit.Id, city.Position))
            {
                long score = CityActionScore(unit, city, action, goldenAgeValue);
                if (score > 0 && Command(unit, action) is { } command)
                    yield return new(city.Position, command, score, city.Id.ToString("D10", CultureInfo.InvariantCulture) + action.Command + action.Target);
            }
        }
    }

    private IEnumerable<Choice> Shrines(Unit unit, City city)
    {
        var religion = ctx.Sim.Content.Services.Get<IReligionService>().SingleOrDefault();
        if (religion is null || GovernmentStatus.IsDisorder(ctx.Sim, city)) yield break;
        foreach (var building in ctx.Def(unit).ConstructibleBuildings.Where(p => p.Value).OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var def = ctx.Sim.Content.Get<BuildingDef>(building.Key);
            if (def.GlobalReligionCommerce is not { } faith || city.Buildings.Contains(def.Id) || religion.HolyCity(ctx.Sim, faith) != city.Id) continue;
            var eligibility = ctx.Service<ICityProductionService>().Validate(ctx.Sim, city.Id, "cities:building", def.Id);
            if (eligibility.Failures.Any(f => f.Explanation != "Building cannot be constructed")) continue;
            yield return new(city.Position, new BuildShrineCommand { Player = ctx.PlayerId, Unit = unit.Id, Building = def.Id },
                10000 + ctx.Sim.State.Cities.Count(c => religion.Present(ctx.Sim, c.Id).Contains(faith)), def.Id);
        }
    }

    private long CityActionScore(Unit unit, City city, GreatPersonAction action, long goldenAgeValue) => action.Command switch
    {
        "great_people:construct_building" => 8000 + ConstructionValue(city, action.Target!),
        "great_people:hurry_production" => HurryScore(city, action),
        "great_people:trade_mission" => action.Amount >= goldenAgeValue ? 6000 + Math.Min(500, action.Amount / 10) + (IsCapital(city) ? 100 : 0) : 0,
        "great_people:join_city" => 1000 + SettlementValue(unit, city, action.Target!),
        "great_people:culture_bomb" => Frontier(city) && city.Culture < action.Amount / 5 ? 3000 : 0,
        _ => 0,
    };

    private long HurryScore(City city, GreatPersonAction action)
    {
        var order = city.ProductionQueue.FirstOrDefault();
        if (order is null || order.Kind != "cities:building") return 0;
        var building = ctx.Sim.Content.Get<BuildingDef>(order.ContentId);
        long cost = ctx.Service<ICityProductionService>().Calculate(ctx.Sim, city.Id, order.Kind, order.ContentId).Cost;
        if (action.Amount < cost / 2 || !CityBuildingRules.IsWonder(building) && cost < 200) return 0;
        return 7000 + Math.Min(500, action.Amount / 10);
    }

    private long ConstructionValue(City city, string building)
    {
        var def = ctx.Sim.Content.Get<BuildingDef>(building);
        var yields = Yields(city);
        return (long)def.CommerceModifiers.Research * yields.Commerce / 100
            + ((long)def.YieldModifiers.Production + def.MilitaryProductionModifier) * yields.Production / 100;
    }

    private long SettlementValue(Unit unit, City city, string specialist)
    {
        var yields = Yields(city);
        if (ctx.Def(unit).GreatGeneral) return yields.Production * 10;
        var def = ctx.Sim.Content.Get<SpecialistDef>(specialist);
        var modifiers = ModifierSink.Collect(new(ctx.Sim, ModifierScope.City, ctx.PlayerId, city)).Ledger;
        return def.Yields.Production * (100 + modifiers.Total("cities:production_percent")) / 100
            + def.Commerces.Research * (100 + modifiers.Total("economy:research_percent")) / 100
            + def.Commerces.Gold * (100 + modifiers.Total("economy:gold_percent")) / 100
            + city.Population + yields.Commerce;
    }

    private IEnumerable<Choice> LeadChoices(Unit unit, IGreatPeopleService service)
    {
        if (!ctx.Def(unit).GreatGeneral || ctx.Team.Wars.Count == 0) yield break;
        foreach (var position in ctx.Units.Where(ctx.Alive).Where(u => ctx.Def(u).Strength > 0).Select(u => u.Position).Distinct())
            foreach (var action in service.AvailableActions(ctx.Sim, ctx.PlayerId, unit.Id, position).Where(a => a.Command == "great_people:lead_unit"))
            {
                var target = ctx.Sim.State.UnitById(int.Parse(action.Target!, CultureInfo.InvariantCulture));
                yield return new(position, Command(unit, action)!, 9000 + Math.Min(500, ctx.Def(target).Strength * target.Health / 100), action.Target!);
            }
    }

    private GameCommand? Command(Unit unit, GreatPersonAction action)
    {
        GreatPersonCommand? command = action.Command switch
        {
            "great_people:construct_building" => new ConstructGreatPersonCommand { Player = ctx.PlayerId, UnitId = unit.Id, Target = action.Target },
            "great_people:hurry_production" => new HurryGreatPersonCommand { Player = ctx.PlayerId, UnitId = unit.Id, Target = action.Target },
            "great_people:trade_mission" => new TradeGreatPersonCommand { Player = ctx.PlayerId, UnitId = unit.Id, Target = action.Target },
            "great_people:join_city" => new JoinGreatPersonCommand { Player = ctx.PlayerId, UnitId = unit.Id, Target = action.Target },
            "great_people:discover_tech" => new DiscoverGreatPersonCommand { Player = ctx.PlayerId, UnitId = unit.Id, Target = action.Target },
            "great_people:culture_bomb" => new CultureGreatPersonCommand { Player = ctx.PlayerId, UnitId = unit.Id },
            "great_people:lead_unit" => new LeadGreatPersonCommand { Player = ctx.PlayerId, UnitId = unit.Id, Target = action.Target },
            _ => null,
        };
        return command;
    }

    private YieldVector Yields(City city)
    {
        if (!_yields.TryGetValue(city.Id, out var yields)) _yields[city.Id] = yields = ctx.Service<ICityYieldService>().Calculate(ctx.Sim, city.Id).Yields;
        return yields;
    }

    private bool IsCapital(City city) => city.Buildings.Any(b => ctx.Sim.Content.Get<BuildingDef>(b).Capital);
    private bool Frontier(City city) => ctx.Grid.Range(ctx.Tile(city.Position), 2)
        .Any(t => ctx.Sim.State.Plots[t.Value].Owner is { } owner && ctx.Sim.TeamOf(owner).Id != ctx.Team.Id);

    // CvPlayerAI::AI_calculateGoldenAgeValue: yield gain, empire population, duration, then instability.
    private long GoldenAgeValue()
    {
        var constants = ctx.Sim.Content.Get<RuleConstantsDef>().All.Single();
        int length = constants.Raw.TryGetValue("GOLDEN_AGE_LENGTH", out var raw)
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var turns) ? turns : constants.Values.GoldenAgeTurns;
        long duration = Math.Max(1, length * ctx.Sim.Content.Get<GameSpeedDef>(ctx.Sim.Settings.GameSpeed).GoldenAgePercent / 100);
        long gain = ctx.Sim.Content.Get<YieldDef>().All.Sum(y => (long)y.GoldenAgeYield * Math.Max(100, y.AiWeightPercent) / Math.Max(1, 1 + y.GoldenAgeYieldThreshold));
        long value = gain * ctx.Cities.Sum(c => (long)c.Population) * duration / 100;
        if (ctx.Sim.Content.Services.Get<IRevolutionService>().Any())
        {
            long average = ctx.Cities.Sum(c => (long)(c.Extensions.Get<RevolutionCityState>(ctx.Sim.Content)?.InstabilityIndex ?? 0)) / Math.Max(1, ctx.Cities.Count);
            value *= Math.Max(1, average / 500);
        }
        return value;
    }
}
