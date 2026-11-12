using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Rules.Religion;

internal sealed partial class ReligionService
{
    /// <summary>Per-turn memo: sorted sources, per-source influence and the connected-source set of each target.</summary>
    internal sealed class SpreadTurn(Simulation sim, ReligionService rules)
    {
        public IReadOnlyList<City> Cities { get; } = sim.State.Cities.OrderBy(c => c.Id).ToArray();
        private readonly Dictionary<(int City, string Religion), int> _influence = [];
        private readonly Dictionary<int, HashSet<int>> _connected = [];
        private readonly Dictionary<int, IReadOnlyList<BuildingDef>> _active = [];

        public int Influence(City city, string religion)
        {
            if (_influence.TryGetValue((city.Id, religion), out int value)) return value;
            value = rules.Influence(sim, city, religion, this);
            _influence[(city.Id, religion)] = value;
            return value;
        }

        /// <summary>The city's active buildings, resolved once per pass: the influence sum asks for them per faith.</summary>
        public IReadOnlyList<BuildingDef> Active(City city)
        {
            if (_active.TryGetValue(city.Id, out var buildings)) return buildings;
            buildings = ReligionData.Active(sim, city);
            _active[city.Id] = buildings;
            return buildings;
        }

        /// <summary>Decay removes buildings, so its city's cached activity and influence are stale afterwards.</summary>
        public void Forget(City city)
        {
            _active.Remove(city.Id);
            foreach (var key in _influence.Keys.Where(k => k.City == city.Id).ToArray()) _influence.Remove(key);
        }

        public bool TradeConnected(City city, City source)
        {
            if (!_connected.TryGetValue(city.Id, out var linked))
            {
                linked = [];
                foreach (var candidate in Cities) if (ReligionService.TradeConnected(sim, city, candidate)) linked.Add(candidate.Id);
                _connected[city.Id] = linked;
            }
            return linked.Contains(source.Id);
        }
    }

    internal SpreadTurn BeginSpreadTurn(Simulation sim) => new(sim, this);

    public ReligionSpreadOdds NaturalSpreadOdds(Simulation sim, int cityId, string religion) =>
        NaturalSpreadOdds(sim, new SpreadTurn(sim, this), cityId, religion);

    private ReligionSpreadOdds NaturalSpreadOdds(Simulation sim, SpreadTurn turn, int cityId, string religion)
    {
        var city = sim.State.CityById(cityId);
        int range = Math.Max(1, ReligionData.Raw(sim, "RELIGION_SPREAD_RAND", 1000) * sim.Content.Get<GameSpeedDef>(sim.Settings.GameSpeed).GrowthPercent / 100);
        if (ReligionData.Has(sim, city, religion) || ReligionData.BlocksSpread(sim, city, religion)) return new(0, range);
        int count = ReligionData.Present(sim, city).Count;
        if (count > 0 && !sim.IsOptionEnabled("foundation:gameoption_multiple_religion_spread")) return new(0, range);
        var def = sim.Content.Get<ReligionDef>(religion);
        int threshold = 0;
        foreach (var source in turn.Cities)
        {
            int spread = turn.Influence(source, religion) * def.SpreadFactor;
            if (spread <= 0) continue;
            if (!turn.TradeConnected(city, source)) continue;
            int distance = sim.State.Grid.Distance(city.Position, source.Position);
            int divisor = Math.Max(1, ReligionData.Raw(sim, "RELIGION_SPREAD_DISTANCE_DIVISOR", 100) * distance
                / Math.Max(1, sim.State.Grid.MaximumDistance) - ReligionData.Raw(sim, "RELIGION_SPREAD_DISTANCE_FLOOR", 5));
            spread = spread / divisor / (count + ReligionData.Raw(sim, "RELIGION_SPREAD_COUNT_OFFSET", 1));
            threshold = Math.Max(threshold, spread + ReligionData.Raw(sim, "RELIGION_SPREAD_THRESHOLD_BUMP", 1));
        }
        int modifier = 100 + ReligionData.Civics(sim, city.Owner).Sum(c => c.ReligionSpreadRate);
        threshold = threshold * Math.Max(0, modifier) / 100;
        threshold = AgeAdjustment(sim, city, religion, threshold);
        return new(threshold / Math.Max(1, ReligionData.Raw(sim, "RELIGION_SPREAD_THRESHOLD_UNIT", 4)), range);
    }

    private static bool TradeConnected(Simulation sim, City city, City source)
    {
        int team = sim.TeamOf(city.Owner).Id, other = sim.TeamOf(source.Owner).Id;
        if (team != other)
        {
            if (sim.TeamOf(city.Owner).Wars.Contains(other)) return false;
            if (sim.Content.Services.Get<IDiplomacyService>().SingleOrDefault()?.HasMet(sim, team, other) != true) return false;
        }
        if (sim.Content.Services.Get<ICityConnectionService>().SingleOrDefault()?.Connected(sim, city.Id, source.Id) == true) return true;
        var trade = sim.Content.Services.Get<ITradeRouteService>().SingleOrDefault();
        if (trade is null) return false;
        return trade.HasBilateralRoute(sim, city.Id, source.Id);
    }
    private int Influence(Simulation sim, City city, string religion, SpreadTurn? turn = null)
    {
        int holy = HolyCity(sim, religion) == city.Id ? ReligionData.Raw(sim, "HOLY_CITY_INFLUENCE", 1) : 0;
        var buildings = turn?.Active(city) ?? ReligionData.Active(sim, city);
        return Math.Max(0, holy + buildings.Sum(b => b.ReligionChanges.GetValueOrDefault(religion)));
    }
    private static int AgeAdjustment(Simulation sim, City city, string religion, int threshold)
    {
        var founded = ReligionData.Game(sim).Founded.GetValueOrDefault(religion);
        if (founded is null) return threshold;
        // Elapsed age, not a negative age: the original subtraction inverted the documented age policy.
        int age = Math.Max(0, sim.State.Turn - founded.Turn);
        int growth = sim.Content.Get<GameSpeedDef>(sim.Settings.GameSpeed).GrowthPercent;
        int young = ReligionData.Raw(sim, "NEW_RELIGION_SPREAD_TURN_THRESHOLD", 50) * growth / 100;
        if (age < young)
        {
            threshold = threshold * ReligionData.Raw(sim, "BOOST_NEW_RELIGION_SPREAD_PERCENT", 250) / 100;
            var era = sim.State.PlayerById(city.Owner).Extensions.Get<CityPlayerState>(sim.Content)?.CurrentEra ?? sim.Settings.StartingEra;
            int rank = sim.Content.Get<EraDef>().All.OrderBy(e => e.StartPercent).ThenBy(e => e.Id, StringComparer.Ordinal).Select(e => e.Id).ToList().IndexOf(era);
            if (rank < 2) threshold /= 20;
            else if (rank == 2) threshold /= 10;
        }
        int old = ReligionData.Raw(sim, "OLD_RELIGION_SPREAD_TURN_THRESHOLD", 250) * growth / 100;
        if (age > old) threshold = (int)((long)threshold * old / age);
        return threshold;
    }
    internal void NaturalSpread(Simulation sim, City city, SpreadTurn turn)
    {
        Decay(sim, city, turn);
        var founded = ReligionData.Game(sim).Founded;
        foreach (var religion in sim.Content.Get<ReligionDef>().All.Where(r => founded.ContainsKey(r.Id)).OrderBy(r => r.Id, StringComparer.Ordinal))
        {
            var odds = NaturalSpreadOdds(sim, turn, city.Id, religion.Id);
            if (odds.Threshold <= 0) continue;
            if (sim.Rng.Next(odds.RandomRange) >= odds.Threshold) continue;
            ReligionData.Add(sim, city, religion.Id);
            break;
        }
    }
}
