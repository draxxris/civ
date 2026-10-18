using Civ.Content.Schema;
using Civ.Game;

namespace Civ.Mods.Religion;

internal sealed partial class ReligionService
{
    public int DecayChancePermille(Simulation sim, int cityId, string religion) =>
        (int)Math.Clamp((long)DecayThreshold(sim, sim.State.CityById(cityId), religion) * 1000 / DecayRange(sim), 0, 1000);

    private int DecayThreshold(Simulation sim, City city, string religion)
    {
        int count = Present(sim, city.Id).Count;
        if (!CanDecay(sim, city, religion, count)) return 0;
        int decay = sim.Content.Get<ReligionDef>(religion).SpreadFactor;
        if (count < 3) decay = decay * count / 3;
        else if (count > 3) decay += 10 * (count - 3);
        decay = DecayAge(sim, religion, decay);
        decay = DomesticDecay(sim, city, religion, decay);
        decay /= Math.Max(1, 1 + Influence(sim, city, religion));
        return Math.Max(0, HolyCityDecay(sim, city, religion, decay) * 100);
    }
    private bool CanDecay(Simulation sim, City city, string religion, int count)
    {
        string? state = StateReligion(sim, city.Owner);
        if (!ReligionData.Has(sim, city, religion) || religion == state || HolyCity(sim, religion) == city.Id) return false;
        if (!Option(sim, city, "foundation:gameoption_religion_decay") && count <= 1) return false;
        return DecayConditions(sim, city, count, state);
    }
    private int DomesticDecay(Simulation sim, City city, string religion, int decay)
    {
        foreach (var other in sim.State.Cities.Where(c => c.Owner == city.Owner).OrderBy(c => c.Id))
            if (ReligionData.Has(sim, other, religion) && TradeConnected(sim, city, other))
                decay = decay * 9 / (10 + Math.Max(0, Influence(sim, other, religion)));
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
        if (age < ReligionData.Raw(sim, "NEW_RELIGION_SPREAD_TURN_THRESHOLD", 50) * growth / 100) decay /= 10;
        if (age > ReligionData.Raw(sim, "OLD_RELIGION_SPREAD_TURN_THRESHOLD", 250) * growth / 100) decay = decay * 4 / 3;
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
    private void Decay(Simulation sim, City city)
    {
        foreach (string religion in Present(sim, city.Id))
        {
            int threshold = DecayThreshold(sim, city, religion);
            if (threshold <= 0 || sim.Rng.Next(DecayRange(sim)) >= threshold) continue;
            foreach (var building in ReligionData.ReligiousBuildings(sim, city, [religion])) city.Buildings.Remove(building.Id);
            ReligionData.Remove(sim, city, religion, "decay");
            break;
        }
    }
}
