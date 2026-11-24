using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Religion;

/// <summary>Natural spread: a faith reaches a city from a trade-connected city that has it. Each turn, each eligible
/// (city, faith) pair spreads with the same chance, whose mean wait is <see cref="RuleConstantsReligionDef.NaturalSpreadMeanTurns"/>
/// scaled by the game speed's growth percent and divided by the city owner's civic spread rates. Distance, the number of
/// sources, their buildings and the faith's age play no part.</summary>
internal sealed partial class ReligionService
{
    /// <summary>The setup switch that turns natural spread off; missionaries and founding still place faiths.</summary>
    internal const string NoNaturalSpreadOption = "religion:gameoption_no_natural_spread";
    internal const string MultipleSpreadOption = "religion:gameoption_multiple_religion_spread";

    /// <summary>One spread pass: the cities in id order, where each faith was when the pass began (so a faith that arrives
    /// during the pass spreads on from its new city only the next turn), the connected cities of each target, and each
    /// city's active buildings for decay.</summary>
    internal sealed class SpreadTurn
    {
        private readonly Simulation _sim;
        private readonly Dictionary<string, List<City>> _sources = new(StringComparer.Ordinal);
        private readonly Dictionary<int, HashSet<int>> _connected = [];
        private readonly Dictionary<int, IReadOnlyList<BuildingDef>> _active = [];
        private int[]? _ids;

        public SpreadTurn(Simulation sim)
        {
            _sim = sim;
            Cities = sim.State.Cities.OrderBy(c => c.Id).ToArray();
            foreach (var city in Cities)
            {
                if (city.Extensions.Get<ReligionCityState>(sim.Content) is not { } state) continue;
                foreach (string religion in state.Religions)
                {
                    if (!_sources.TryGetValue(religion, out var holders)) _sources[religion] = holders = [];
                    holders.Add(city);
                }
            }
        }

        public IReadOnlyList<City> Cities { get; }

        /// <summary>Whether a city that had the faith when the pass began is trade-connected to the city.</summary>
        public bool Reaches(City city, string religion) =>
            _sources.TryGetValue(religion, out var holders) && holders.Exists(source => source.Id != city.Id && TradeConnected(city, source));

        /// <summary>The city's active buildings, resolved once per pass: decay asks for them per faith.</summary>
        public IReadOnlyList<BuildingDef> Active(City city)
        {
            if (_active.TryGetValue(city.Id, out var buildings)) return buildings;
            buildings = ReligionData.Active(_sim, city);
            _active[city.Id] = buildings;
            return buildings;
        }

        /// <summary>Decay removes buildings, so its city's cached activity is stale afterwards.</summary>
        public void Forget(City city) => _active.Remove(city.Id);

        public bool TradeConnected(City city, City source)
        {
            if (!_connected.TryGetValue(city.Id, out var linked))
            {
                linked = [];
                // One flood from the city answers every candidate's network connection.
                _ids ??= Cities.Select(c => c.Id).ToArray();
                var network = _sim.Rules().CityConnections?.ConnectedFrom(_sim, city.Id, _ids);
                for (int i = 0; i < Cities.Count; i++)
                    if (ReligionService.TradeConnected(_sim, city, Cities[i], network?[i])) linked.Add(Cities[i].Id);
                _connected[city.Id] = linked;
            }
            return linked.Contains(source.Id);
        }
    }

    internal static SpreadTurn BeginSpreadTurn(Simulation sim) => new(sim);

    public ReligionSpreadOdds NaturalSpreadOdds(Simulation sim, int cityId, string religion) =>
        NaturalSpreadOdds(sim, new SpreadTurn(sim), cityId, religion);

    /// <summary>The faith spreads when a draw in [0, mean turns x growth percent) falls below 100 plus the owner's civic
    /// spread rates: a mean wait of the scaled mean turns at no rate, shorter with a positive rate, never with -100% or
    /// less.</summary>
    private ReligionSpreadOdds NaturalSpreadOdds(Simulation sim, SpreadTurn turn, int cityId, string religion)
    {
        var city = sim.State.CityById(cityId);
        int range = NaturalSpreadRange(sim);
        if (!Eligible(sim, turn, city, religion)) return new(0, range);
        int rate = 100 + ReligionData.Civics(sim, city.Owner).Sum(c => c.Rules.ReligionSpreadRate);
        return new(Math.Clamp(rate, 0, range), range);
    }

    private static int NaturalSpreadRange(Simulation sim) => (int)Math.Clamp(
        (long)ReligionData.Rules(sim).NaturalSpreadMeanTurns * sim.Content.Get<GameSpeedDef>(sim.Settings.GameSpeed).GrowthPercent, 1, int.MaxValue);

    /// <summary>Natural spread is on, the city lacks the faith, its owner's civics let the faith in, it holds no faith
    /// unless multiple religion spread is on, and a city that has the faith is trade-connected to it.</summary>
    private static bool Eligible(Simulation sim, SpreadTurn turn, City city, string religion)
    {
        if (sim.IsOptionEnabled(NoNaturalSpreadOption)) return false;
        if (ReligionData.Has(sim, city, religion) || ReligionData.BlocksSpread(sim, city, religion)) return false;
        if (ReligionData.Count(sim, city) > 0 && !sim.IsOptionEnabled(MultipleSpreadOption)) return false;
        return turn.Reaches(city, religion);
    }

    private static bool TradeConnected(Simulation sim, City city, City source) => TradeConnected(sim, city, source, null);
    /// <param name="network">The network connection from <paramref name="city"/> to <paramref name="source"/>, when the
    /// caller already knows it; asked otherwise.</param>
    private static bool TradeConnected(Simulation sim, City city, City source, bool? network)
    {
        int team = sim.TeamOf(city.Owner).Id, other = sim.TeamOf(source.Owner).Id;
        if (team != other)
        {
            if (sim.TeamOf(city.Owner).Wars.Contains(other)) return false;
            if (!sim.Rules().Relations.HasMet(sim, team, other)) return false;
        }
        if ((network ?? sim.Rules().CityConnections?.Connected(sim, city.Id, source.Id)) == true) return true;
        return sim.Rules().TradeLinks?.Linked(sim, city.Id, source.Id) == true;
    }

    /// <summary>A city's hold on a faith for decay: its holy city influence and the faith's buildings there.</summary>
    private int Influence(Simulation sim, City city, string religion, SpreadTurn? turn = null)
    {
        int holy = HolyCity(sim, religion) == city.Id ? ReligionData.Rules(sim).HolyCityInfluence : 0;
        var buildings = turn?.Active(city) ?? ReligionData.Active(sim, city);
        return Math.Max(0, holy + buildings.Sum(b => ReligionData.Building(sim.Content, b.Id).ReligionChanges.GetValueOrDefault(religion)));
    }

    /// <summary>Decay first, then each founded faith in id order; a faith that arrives is checked against the next, so
    /// without multiple religion spread at most one arrives.</summary>
    internal void NaturalSpread(Simulation sim, City city, SpreadTurn turn)
    {
        Decay(sim, city, turn);
        var founded = ReligionData.Game(sim).Founded;
        foreach (var religion in sim.Content.Get<ReligionDef>().All.Where(r => founded.ContainsKey(r.Id)).OrderBy(r => r.Id, StringComparer.Ordinal))
        {
            var odds = NaturalSpreadOdds(sim, turn, city.Id, religion.Id);
            if (odds.Threshold <= 0) continue;
            if (sim.Rng.Next(odds.RandomRange) >= odds.Threshold) continue;
            // A faith that arrives is a write beyond a narrow declaration (ReligionTurns).
            sim.WidenWriteWindow();
            ReligionData.Add(sim, city, religion.Id);
        }
    }
}
