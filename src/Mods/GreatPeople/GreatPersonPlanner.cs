using System.Globalization;
using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Ai;
using Ariel.Game;
using Ariel.Mods.Religion;
using SpecialistDef = Ariel.Content.Schema.SpecialistDef;

namespace Ariel.Mods.GreatPeople;

/// <summary>The computer player's use of great people, run inside the standard AI's turn after religion and after
/// any planner that spends a great person on something of its own (<see cref="AiPlannerOrder.Missions"/> + 100), and
/// before cities choose production.</summary>
internal sealed class GreatPersonAiPlanner : IAiPlanner
{
    public string Id => "great_people:planner";
    public int Order => AiPlannerOrder.Missions + 200;
    public void Plan(AiContext ctx) => new GreatPersonPlanner(ctx).Plan();
}

/// <summary>Scores every action open to each great person (shrines, buildings, hurrying, trade missions, joining a
/// city, culture, leading a unit, a golden age or a technology) and takes the best one it can reach.</summary>
internal sealed class GreatPersonPlanner(AiContext ctx)
{
    private sealed record Choice(Hex Destination, GameCommand Command, long Score, string Key);
    // A trade mission scores TradeScore plus at most 500 for its gold and 100 for a capital. Every choice above
    // TradeCeiling comes before every trade mission, so trade missions, which weigh a route to every foreign city, are
    // weighed only once none of those choices could be taken.
    private const long TradeScore = 6000, TradeCeiling = TradeScore + 500 + 100;
    private readonly Dictionary<int, YieldVector> _yields = new();
    private readonly List<GreatPersonAction> _actions = new();

    public void Plan()
    {
        var service = ctx.Sim.Service<IGreatPeopleService>();
        if (service is null) return;
        foreach (var unit in ctx.Units.OrderBy(u => u.Id))
        {
            if (!ctx.Alive(unit) || unit.Health <= 0 || ctx.HasActed(unit) || !IsGreatPerson(ctx.Def(unit), GreatPeopleData.Unit(ctx.Sim.Content, unit.Type))) continue;
            PlanUnit(unit, service);
        }
    }

    private static bool IsGreatPerson(UnitDef def, UnitGreatPeopleDef gp) => gp.GreatPeoples.Any(p => p.Value) || gp.GreatGeneral
        || gp.BaseDiscover > 0 || gp.DiscoverMultiplier > 0 || gp.BaseHurry > 0 || gp.HurryMultiplier > 0
        || def.BaseTrade > 0 || def.TradeMultiplier > 0 || gp.GreatWorkCulture > 0 || gp.LeaderPromotion is not null || gp.LeaderExperience > 0;

    private void PlanUnit(Unit unit, IGreatPeopleService service)
    {
        long goldenAgeValue = GoldenAgeValue() / Math.Max(1, service.GoldenAgeUnitsRequired(ctx.Sim, ctx.PlayerId));
        var choices = new List<Choice>();
        CityChoices(unit, goldenAgeValue, choices);
        LeadChoices(unit, choices);
        var actions = service.AvailableActions(ctx.Sim, ctx.PlayerId, unit.Id);
        var discover = actions.FirstOrDefault(a => a.Mission == GreatPersonMissions.Discover);
        long amount = discover?.Amount ?? 0;
        if (goldenAgeValue * 100 / Math.Max(1, amount) > 60 && service.GoldenAgeTurnsRemaining(ctx.Sim, ctx.PlayerId) == 0
            && ctx.Self.Extensions.Get<CityPlayerState>(ctx.Sim.Content)?.GoldenAge != true
            && actions.FirstOrDefault(a => a.Mission == GreatPersonMissions.GoldenAge) is { } goldenAge)
            choices.Add(new(unit.Position, Command(unit, goldenAge), 5000, "golden-age"));
        if (discover is not null && (ctx.Turn > 80 || amount >= goldenAgeValue))
            choices.Add(new(unit.Position, Command(unit, discover), 4000, discover.Mission));
        // The choices above every trade mission are tried first, in the order the whole list sorts them; one that could
        // not be taken changed nothing, so the trade missions weighed after them are the same and sort after them.
        var ordered = Ordered(unit, choices);
        int next = 0;
        for (; next < ordered.Count && ordered[next].Score > TradeCeiling; next++)
            if (Take(unit, ordered[next])) return;
        if (TradeChoices(unit, goldenAgeValue, choices)) ordered = Ordered(unit, choices);
        for (; next < ordered.Count; next++)
            if (Take(unit, ordered[next])) return;
    }

    private List<Choice> Ordered(Unit unit, List<Choice> choices) => choices.OrderByDescending(c => c.Score)
        .ThenBy(c => ctx.Grid.Distance(unit.Position, c.Destination)).ThenBy(c => c.Key, StringComparer.Ordinal).ToList();

    private bool Take(Unit unit, Choice choice)
    {
        bool moved = ctx.MoveToward(unit, choice.Destination);
        bool used = unit.Position == choice.Destination && ctx.Issue(choice.Command);
        if (!moved && !used) return false;
        ctx.MarkActed(unit);
        return true;
    }

    private void CityChoices(Unit unit, long goldenAgeValue, List<Choice> choices)
    {
        var def = ctx.Def(unit);
        foreach (var city in ctx.Sim.State.Cities.OrderBy(c => c.Id))
        {
            if (city.Owner != ctx.PlayerId) continue;
            foreach (var shrine in Shrines(unit, city)) choices.Add(shrine);
            _actions.Clear();
            GreatPersonActions.OwnCityActions(ctx.Sim, unit, def, city, _actions);
            foreach (var action in _actions) Add(unit, city, action, goldenAgeValue, choices);
        }
    }

    /// <summary>Trade missions to every city that offers one; whether any was added.</summary>
    private bool TradeChoices(Unit unit, long goldenAgeValue, List<Choice> choices)
    {
        var def = ctx.Def(unit);
        if (def.BaseTrade <= 0 && def.TradeMultiplier <= 0) return false;
        int count = choices.Count;
        var terms = new GreatPersonActions.TradeTerms(ctx.Sim, ctx.PlayerId);
        foreach (var city in ctx.Sim.State.Cities.OrderBy(c => c.Id))
            if (GreatPersonActions.Trade(ctx.Sim, unit, def, city, terms) is { } trade) Add(unit, city, trade, goldenAgeValue, choices);
        return choices.Count > count;
    }

    private void Add(Unit unit, City city, GreatPersonAction action, long goldenAgeValue, List<Choice> choices)
    {
        long score = CityActionScore(unit, city, action, goldenAgeValue);
        if (score > 0)
            choices.Add(new(city.Position, Command(unit, action), score, city.Id.ToString("D10", CultureInfo.InvariantCulture) + action.Mission + action.Subject));
    }
    private IEnumerable<Choice> Shrines(Unit unit, City city)
    {
        var religion = ctx.Sim.Service<IReligionService>();
        if (religion is null || GovernmentStatus.IsDisorder(ctx.Sim, city)) yield break;
        foreach (var building in ctx.Def(unit).ConstructibleBuildings.Where(p => p.Value).OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var def = ctx.Sim.Content.Get<BuildingDef>(building.Key);
            if (ctx.Sim.Content.Attachment<BuildingReligionDef>(def.Id)?.GlobalReligionCommerce is not { } faith || city.Buildings.Contains(def.Id)
                || religion.HolyCity(ctx.Sim, faith) != city.Id) continue;
            var eligibility = ctx.Service<ICityProductionService>().Validate(ctx.Sim, city.Id, "cities:building", def.Id);
            if (eligibility.Failures.Any(f => !ProductionFailures.IsCostOnly(f))) continue;
            yield return new(city.Position, new PerformMissionCommand { Player = ctx.PlayerId, Unit = unit.Id, Mission = ReligionMissions.Shrine, Subject = def.Id },
                10000 + ctx.Sim.State.Cities.Count(c => religion.Present(ctx.Sim, c.Id).Contains(faith)), def.Id);
        }
    }

    private long CityActionScore(Unit unit, City city, GreatPersonAction action, long goldenAgeValue) => action.Mission switch
    {
        GreatPersonMissions.Construct => 8000 + ConstructionValue(city, action.Subject),
        GreatPersonMissions.Hurry => HurryScore(city, action),
        GreatPersonMissions.Trade => action.Amount >= goldenAgeValue ? TradeScore + Math.Min(500, action.Amount / 10) + (IsCapital(city) ? 100 : 0) : 0,
        GreatPersonMissions.Join => 1000 + SettlementValue(unit, city, action.Subject),
        GreatPersonMissions.Culture => Frontier(city) && city.Culture < action.Amount / 5 ? 3000 : 0,
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
        if (GreatPeopleData.Unit(ctx.Sim.Content, unit.Type).GreatGeneral) return yields.Production * 10;
        var def = ctx.Sim.Content.Get<SpecialistDef>(specialist);
        var modifiers = ModifierSink.Collect(new(ctx.Sim, ModifierScope.City, ctx.PlayerId, city)).Ledger;
        return def.Yields.Production * (100 + modifiers.Total("cities:production_percent")) / 100
            + def.Commerces.Research * (100 + modifiers.Total("economy:research_percent")) / 100
            + def.Commerces.Gold * (100 + modifiers.Total("economy:gold_percent")) / 100
            + city.Population + yields.Commerce;
    }

    private void LeadChoices(Unit unit, List<Choice> choices)
    {
        if (!GreatPeopleData.Unit(ctx.Sim.Content, unit.Type).GreatGeneral || ctx.Team.Wars.Count == 0) return;
        var def = ctx.Def(unit);
        foreach (var position in ctx.Units.Where(ctx.Alive).Where(u => ctx.Def(u).Strength > 0).Select(u => u.Position).Distinct())
        {
            _actions.Clear();
            GreatPersonActions.AddLeadActions(ctx.Sim, unit, def, position, _actions);
            foreach (var action in _actions)
            {
                var target = ctx.Sim.State.UnitById(int.Parse(action.Subject, CultureInfo.InvariantCulture));
                choices.Add(new(position, Command(unit, action), 9000 + Math.Min(500, ctx.Def(target).Strength * target.Health / 100), action.Subject));
            }
        }
    }

    private PerformMissionCommand Command(Unit unit, GreatPersonAction action) =>
        new() { Player = ctx.PlayerId, Unit = unit.Id, Mission = action.Mission, Subject = action.Subject };

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
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var turns) ? turns : GreatPeopleData.Rules(ctx.Sim).GoldenAgeTurns;
        long duration = Math.Max(1, length * ctx.Sim.Content.Get<GameSpeedDef>(ctx.Sim.Settings.GameSpeed).GoldenAgePercent / 100);
        long gain = ctx.Sim.Content.Get<YieldDef>().All.Select(y => GreatPeopleData.Yield(ctx.Sim.Content, y.Id))
            .Sum(y => (long)y.GoldenAgeYield * Math.Max(100, y.AiWeightPercent) / Math.Max(1, 1 + y.GoldenAgeYieldThreshold));
        long value = gain * ctx.Cities.Sum(c => (long)c.Population) * duration / 100;
        if (ctx.Sim.Rules().CityUnrest is { } unrest)
        {
            long average = ctx.Cities.Sum(c => (long)unrest.Unrest(ctx.Sim, c.Id)) / Math.Max(1, ctx.Cities.Count);
            value *= Math.Max(1, average / 500);
        }
        return value;
    }
}
