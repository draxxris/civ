using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Mods.Religion;

internal sealed partial class ReligionService
{
    /// <summary>Decay chance per mille; zero for a stale city id, like the other queries.</summary>
    public int DecayChancePermille(Simulation sim, int cityId, string religion) => sim.State.FindCity(cityId) is { } city
        ? (int)Math.Clamp((long)DecayThreshold(sim, city, religion) * 1000 / DecayRange(sim), 0, 1000)
        : 0;

    private int DecayThreshold(Simulation sim, City city, string religion, SpreadTurn? turn = null)
    {
        int count = Present(sim, city.Id).Count;
        if (!CanDecay(sim, city, religion, count)) return 0;
        int decay = sim.Content.Get<ReligionDef>(religion).SpreadFactor;
        if (count < 3) decay = decay * count / 3;
        else if (count > 3) decay += 10 * (count - 3);
        decay = DecayAge(sim, religion, decay);
        decay = DomesticDecay(sim, city, religion, decay, turn);
        decay /= Math.Max(1, 1 + Influence(sim, city, religion, turn));
        return Math.Max(0, HolyCityDecay(sim, city, religion, decay) * 100);
    }
    private bool CanDecay(Simulation sim, City city, string religion, int count)
    {
        // AND gates all decay on the game option (CvCity.cpp:20651): with it off no religion decays at all.
        if (!sim.IsOptionEnabled("foundation:gameoption_religion_decay")) return false;
        string? state = StateReligion(sim, city.Owner);
        if (!ReligionData.Has(sim, city, religion) || religion == state || HolyCity(sim, religion) == city.Id) return false;
        return DecayConditions(sim, city, count, state);
    }
    private int DomesticDecay(Simulation sim, City city, string religion, int decay, SpreadTurn? turn = null)
    {
        int numerator = ReligionData.Raw(sim, "RELIGION_DECAY_DOMESTIC_NUMERATOR", 9);
        int offset = ReligionData.Raw(sim, "RELIGION_DECAY_DOMESTIC_OFFSET", 10);
        foreach (var other in sim.State.Cities.Where(c => c.Owner == city.Owner).OrderBy(c => c.Id))
            if (ReligionData.Has(sim, other, religion) && TradeConnected(sim, city, other))
                decay = decay * numerator / (offset + Math.Max(0, Influence(sim, other, religion, turn)));
        return decay;
    }
    private static bool DecayConditions(Simulation sim, City city, int count, string? state)
    {
        if (count > 2) return true;
        if (state is not null) return ReligionData.Has(sim, city, state);
        string era = sim.State.PlayerById(city.Owner).Extensions.Get<CityPlayerState>(sim.Content)?.CurrentEra ?? sim.Settings.StartingEra;
        var eras = sim.Content.Get<EraDef>().All.OrderBy(e => e.StartPercent).ThenBy(e => e.Id, StringComparer.Ordinal).ToArray();
        return Array.FindIndex(eras, e => e.Id == era) > 0;
    }
    private static int DecayAge(Simulation sim, string religion, int decay)
    {
        var founding = ReligionData.Game(sim).Founded.GetValueOrDefault(religion);
        if (founding is null) return decay;
        int age = Math.Max(0, sim.State.Turn - founding.Turn);
        int growth = sim.Content.Get<GameSpeedDef>(sim.Settings.GameSpeed).GrowthPercent;
        if (age < ReligionData.Raw(sim, "NEW_RELIGION_SPREAD_TURN_THRESHOLD", 50) * growth / 100)
            decay /= Math.Max(1, ReligionData.Raw(sim, "RELIGION_DECAY_YOUNG_DIVISOR", 10));
        if (age > ReligionData.Raw(sim, "OLD_RELIGION_SPREAD_TURN_THRESHOLD", 250) * growth / 100)
            decay = decay * ReligionData.Raw(sim, "RELIGION_DECAY_OLD_NUMERATOR", 4)
                / Math.Max(1, ReligionData.Raw(sim, "RELIGION_DECAY_OLD_DENOMINATOR", 3));
        return decay;
    }
    private int HolyCityDecay(Simulation sim, City city, string religion, int decay)
    {
        if (HolyCity(sim, religion) is not { } holyId || sim.State.FindCity(holyId) is not { } holy) return decay;
        if (holy.Owner == city.Owner) return decay / 2;
        return sim.TeamOf(city.Owner).Wars.Contains(sim.TeamOf(holy.Owner).Id) ? decay * 4 / 3 : decay;
    }
    private static int DecayRange(Simulation sim) => Math.Max(1, ReligionData.Raw(sim, "RELIGION_SPREAD_RAND", 1000)
        * sim.Content.Get<GameSpeedDef>(sim.Settings.GameSpeed).GrowthPercent / 50);
    private void Decay(Simulation sim, City city, SpreadTurn? turn = null)
    {
        foreach (string religion in Present(sim, city.Id))
        {
            int threshold = DecayThreshold(sim, city, religion, turn);
            if (threshold <= 0 || sim.Rng.Next(DecayRange(sim)) >= threshold) continue;
            foreach (var building in ReligionData.InvalidatedBuildings(sim, city, [religion])) city.Buildings.Remove(building.Id);
            ReligionData.Remove(sim, city, religion, "decay");
            turn?.Forget(city);
            break;
        }
    }
}
