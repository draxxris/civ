using Civ.Content;
using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.Cities;

internal sealed class CityGreatPeople : ICityGreatPeopleService
{
    public CityGreatPeopleResult Calculate(Simulation sim, int cityId)
    {
        var city = sim.State.CityById(cityId);
        var sink = CityData.Effects(sim, city);
        var rates = Rates(sim, city, sink);
        long rate = rates.Values.Sum() * Math.Max(0, 100 + CityData.Effect(sink, "great_people_percent")) / 100;
        if (CityData.PlayerState(sim, city.Owner).GoldenAge) rate = rate * (100 + CityData.Constants(sim).GoldenAgeGreatPeoplePercent) / 100;
        if (CityData.Read(sim, city).Disorder) rate = 0;
        long threshold = CityData.Raw(sim, "GREAT_PEOPLE_THRESHOLD", 100) * Math.Max(0, 100 + CityData.PlayerState(sim, city.Owner).GreatPeopleThresholdModifier) / 100;
        threshold = threshold * CityData.Speed(sim).GreatPeoplePercent / 100;
        threshold = Math.Max(1, threshold * sim.Content.Get<S.EraDef>(sim.Settings.StartingEra).GreatPeoplePercent / 100);
        sink.Add("cities:total", "cities:great_people_rate", rate); sink.Add("cities:total", "cities:great_people_threshold", threshold);
        return new(threshold, rate, sink.Ledger);
    }
    private static SortedDictionary<string, long> Rates(Simulation sim, City city, ModifierSink sink)
    {
        var rates = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (var pair in Specialists.All(sim, city, sink))
        {
            var def = sim.Content.Get<S.SpecialistDef>(pair.Key);
            if (def.GreatPersonUnit is { } unit) rates[unit] = rates.GetValueOrDefault(unit) + def.GreatPeopleRateChange * pair.Value;
        }
        foreach (var b in CityData.Active(sim, city))
            if (b.GreatPersonUnit is { } unit) rates[unit] = rates.GetValueOrDefault(unit) + b.GreatPeopleRateChange;
        return rates;
    }
    internal void Run(Simulation sim, City city)
    {
        if (CityData.Read(sim, city).Disorder) return;
        var state = CityData.Edit(sim, city);
        var result = Calculate(sim, city.Id);
        state.GreatPeopleProgress += result.Rate;
        foreach (var pair in Rates(sim, city, CityData.Effects(sim, city)))
            state.GreatPeopleWeights[pair.Key] = state.GreatPeopleWeights.GetValueOrDefault(pair.Key) + pair.Value;
        long weight = state.GreatPeopleWeights.Values.Sum();
        if (state.GreatPeopleProgress < result.Threshold || weight <= 0) return;
        long draw = (long)(sim.Rng.NextUInt64() % (ulong)weight);
        foreach (var pair in state.GreatPeopleWeights)
        {
            if (draw >= pair.Value) { draw -= pair.Value; continue; }
            CityProduction.Spawn(sim, city, pair.Key);
            state.GreatPeopleProgress -= result.Threshold;
            state.GreatPeopleWeights.Clear();
            IncreaseThreshold(sim, city.Owner);
            return;
        }
    }
    private static void IncreaseThreshold(Simulation sim, int owner)
    {
        var player = CityData.PlayerState(sim, owner);
        player.GreatPeopleBorn++;
        player.GreatPeopleThresholdModifier += CityData.Raw(sim, "GREAT_PEOPLE_THRESHOLD_INCREASE", 50) * (player.GreatPeopleBorn / 10 + 1);
        sim.State.PlayerById(owner).Extensions.Set(sim.Content, player);
        foreach (int member in sim.TeamOf(owner).Members.Where(m => m != owner))
        {
            var other = CityData.PlayerState(sim, member);
            other.GreatPeopleThresholdModifier += CityData.Raw(sim, "GREAT_PEOPLE_THRESHOLD_INCREASE_TEAM", 50) * (player.GreatPeopleBorn / 10 + 1);
            sim.State.PlayerById(member).Extensions.Set(sim.Content, other);
        }
    }
}
