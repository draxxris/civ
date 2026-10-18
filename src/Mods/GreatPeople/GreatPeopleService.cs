using System.Collections.ObjectModel;
using Civ.Content;
using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.GreatPeople;

internal sealed class GreatPeopleService : IGreatPeopleService, ICityGreatPeopleService
{
    public GreatPeopleCityResult Calculate(Simulation sim, int cityId)
    {
        var city = sim.State.CityById(cityId);
        var sink = GreatPeopleData.Effects(sim, ModifierScope.City, city.Owner, city);
        var rates = SourceRates(sim, city, sink);
        if (rates.Count > 0)
        {
            var playerEffects = GreatPeopleData.Effects(sim, ModifierScope.Player, city.Owner).Ledger;
            foreach (var line in playerEffects.Lines.Where(l => IsRateModifier(l.Key))) sink.Add(line.Source, line.Key, line.Amount);
        }
        long modifier = 100 + sink.Ledger.Total("cities:great_people_percent") + sink.Ledger.Total("cities:great_people_rate_percent");
        long rate = Math.Max(0, rates.Values.Sum() * Math.Max(0, modifier) / 100);
        if (GovernmentStatus.IsDisorder(sim, city)) rate = 0;
        sink.Add("great_people:total", "great_people:rate", rate);
        var state = GreatPeopleData.Read(sim, city);
        return new(state.Progress, Threshold(sim, city.Owner), rate,
            new ReadOnlyDictionary<string, long>(new SortedDictionary<string, long>(state.ClassPoints, StringComparer.Ordinal)), sink.Ledger);
    }
    private static bool IsRateModifier(string key) => key is "cities:great_people_percent" or "cities:great_people_rate_percent";
    CityGreatPeopleResult ICityGreatPeopleService.Calculate(Simulation sim, int city)
    {
        var result = Calculate(sim, city);
        return new(result.Threshold, result.Rate, result.Ledger);
    }
    public long Threshold(Simulation sim, int player)
    {
        long threshold = GreatPeopleData.Raw(sim, "GREAT_PEOPLE_THRESHOLD", 100)
            * Math.Max(0L, 100L + GreatPeopleData.Read(sim, player).ThresholdModifier) / 100;
        threshold = threshold * GreatPeopleData.Speed(sim).GreatPeoplePercent / 100;
        return Math.Max(1, threshold * sim.Content.Get<S.EraDef>(sim.Settings.StartingEra).GreatPeoplePercent / 100);
    }
    public long GeneralThreshold(Simulation sim, int player)
    {
        int modifier = sim.State.PlayerById(player).Extensions.Get<MilitaryPlayerState>(sim.Content)?.GeneralThresholdModifier ?? 0;
        long threshold = GreatPeopleData.Raw(sim, "GREAT_GENERALS_THRESHOLD", 30) * Math.Max(0L, 100L + modifier) / 100;
        var speed = GreatPeopleData.Speed(sim);
        threshold = threshold * speed.GreatPeoplePercent / Math.Max(1, speed.TrainPercent);
        return Math.Max(1, threshold * sim.Content.Get<S.EraDef>(sim.Settings.StartingEra).GreatPeoplePercent / 100);
    }
    public long GeneralProgress(Simulation sim, int player) =>
        sim.State.PlayerById(player).Extensions.Get<MilitaryPlayerState>(sim.Content)?.GeneralPoints ?? 0;
    public IReadOnlyDictionary<string, int> SettledSpecialists(Simulation sim, int city) =>
        new ReadOnlyDictionary<string, int>(new SortedDictionary<string, int>(GreatPeopleData.Read(sim, sim.State.CityById(city)).SettledSpecialists, StringComparer.Ordinal));
    public int GoldenAgeTurnsRemaining(Simulation sim, int player) => GreatPeopleData.Read(sim, player).GoldenAgeTurns;
    public int GoldenAgeUnitsRequired(Simulation sim, int player) => Math.Max(1, GreatPeopleData.Raw(sim, "BASE_GOLDEN_AGE_UNITS", 1)
        + GreatPeopleData.Read(sim, player).UnitGoldenAges * GreatPeopleData.Raw(sim, "GOLDEN_AGE_UNITS_MULTIPLIER", 1));
    public IReadOnlyList<GreatPersonAction> AvailableActions(Simulation sim, int player, int unit, Hex? destination = null) => GreatPersonActions.Available(sim, player, unit, destination);

    private static SortedDictionary<string, long> SourceRates(Simulation sim, City city, ModifierSink sink)
    {
        var rates = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (var specialist in sim.Content.Get<S.SpecialistDef>().All.OrderBy(s => s.Id, StringComparer.Ordinal))
        {
            long count = Math.Max(0, city.Specialists.GetValueOrDefault(specialist.Id))
                + Math.Max(0, sink.Ledger.Total("cities:free:" + specialist.Id));
            AddSource(sim, specialist.Id, specialist.GreatPersonUnit, specialist.GreatPeopleRateChange * count, rates, sink);
        }
        foreach (var building in GreatPeopleData.Active(sim, city))
            AddSource(sim, building.Id, building.GreatPersonUnit, building.GreatPeopleRateChange, rates, sink);
        return rates;
    }
    private static void AddSource(Simulation sim, string source, string? unit, long points, SortedDictionary<string, long> rates, ModifierSink sink)
    {
        if (unit is null || points <= 0 || !sim.Content.TryGet<S.UnitDef>(unit, out var def)) return;
        string unitClass = def.DefaultUnit;
        rates[unitClass] = rates.GetValueOrDefault(unitClass) + points;
        sink.Add(source, "great_people:base_rate", points);
        sink.Add(source, "great_people:class:" + unitClass, points);
    }
    internal void Run(Simulation sim, City city)
    {
        if (GovernmentStatus.IsDisorder(sim, city)) return;
        var result = Calculate(sim, city.Id);
        var state = GreatPeopleData.Edit(sim, city);
        state.Progress += result.Rate;
        foreach (var line in result.Ledger.Lines.Where(l => l.Key.StartsWith("great_people:class:", StringComparison.Ordinal)))
        {
            string unitClass = line.Key["great_people:class:".Length..];
            state.ClassPoints[unitClass] = state.ClassPoints.GetValueOrDefault(unitClass) + line.Amount;
        }
        if (state.Progress < result.Threshold) return;
        var weights = state.ClassPoints.Where(p => p.Value > 0 && GreatPeopleData.ResolveUnit(sim, city.Owner, p.Key) is not null).ToArray();
        long total = weights.Sum(p => p.Value);
        if (total <= 0) return; // Disabled civilization classes never produce invalid units or consume RNG.
        long draw = (long)(sim.Rng.NextUInt64() % (ulong)total);
        foreach (var pair in weights)
        {
            if (draw >= pair.Value) { draw -= pair.Value; continue; }
            Birth(sim, city, pair.Key);
            state.Progress -= result.Threshold;
            state.ClassPoints.Clear();
            return;
        }
    }
    private static void Birth(Simulation sim, City city, string unitClass)
    {
        string type = GreatPeopleData.ResolveUnit(sim, city.Owner, unitClass)!;
        var def = sim.Content.Get<S.UnitDef>(type);
        var unit = new Unit { Id = sim.State.NextUnitId++, Owner = city.Owner, Position = city.Position, Type = type,
            MovesLeft = def.Moves * GreatPeopleData.Constants(sim).Values.MoveDenominator };
        sim.State.AddUnit(unit);
        var player = GreatPeopleData.Edit(sim, city.Owner);
        player.Births++;
        int scale = player.Births / 10 + 1;
        player.ThresholdModifier += GreatPeopleData.Raw(sim, "GREAT_PEOPLE_THRESHOLD_INCREASE", 50) * scale;
        // The team increase includes the creator's personal increase and scales with every member's births.
        var team = sim.TeamOf(city.Owner).Members;
        int teamScale = team.Sum(m => GreatPeopleData.Read(sim, m).Births) / 10 + 1;
        foreach (int member in team.Order())
            GreatPeopleData.Edit(sim, member).ThresholdModifier += GreatPeopleData.Raw(sim, "GREAT_PEOPLE_THRESHOLD_INCREASE_TEAM", 50) * teamScale;
        sim.Events.Publish(new UnitCreated(unit.Id, unit.Owner, unit.Type, unit.Position));
        sim.Events.Publish(new GreatPersonBorn(city.Owner, city.Id, unit.Id, unitClass));
    }
}
