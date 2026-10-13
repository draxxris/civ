using Civ.Content;
using Civ.Content.Schema;
using Civ.Game;

namespace Civ.Mods.Economy;

internal sealed class CityCommerce : ICityCultureSource, ICityProcessService
{
    internal static readonly string[] Types = ["gold", "research", "culture", "espionage"];
    internal static long[] Parts(CommerceVector value) => [value.Gold, value.Research, value.Culture, value.Espionage];
    internal static CommerceVector Vector(IReadOnlyList<long> parts) => new(parts[0], parts[1], parts[2], parts[3]);
    internal CityCommerceResult Calculate(Simulation sim, int cityId, CommerceVector rates)
    {
        var city = sim.State.CityById(cityId);
        var yields = sim.Content.Services.Get<ICityYieldService>().Single().Calculate(sim, cityId);
        var lines = yields.Ledger.Lines.ToList();
        var slider = Parts(rates); var direct = Parts(yields.DirectCommerceHundredths);
        var modifier = Parts(yields.CommercePercent); var result = new long[4];
        long raw = yields.Yields.Commerce * 100;
        var split = slider.Select(p => raw * p / 100).ToArray();
        split[0] += raw - split.Sum(); // Gold receives every unallocated hundredth.
        var process = Parts(ProcessCommerce(sim, city, yields.Yields.Production));
        bool disorder = GovernmentStatus.IsDisorder(sim, city);
        for (int i = 0; i < 4; i++)
        {
            long baseRate = split[i] + direct[i];
            long percent = Math.Max(1, 100 + modifier[i]);
            long modified = baseRate > 0 ? baseRate * percent / 100 : baseRate * 100 / percent;
            long final = modified + process[i];
            if (i == 1 || i == 2) final = Math.Max(0, final);
            result[i] = disorder ? 0 : final;
            string key = "economy:" + Types[i];
            lines.Add(new("economy:slider", key + "_split_hundredths", split[i]));
            lines.Add(new("economy:direct", key + "_direct_hundredths", direct[i]));
            lines.Add(new("economy:modifier", key + "_factor_percent", percent));
            lines.Add(new("economy:process", key + "_process_hundredths", process[i]));
            lines.Add(new("economy:total", key + "_hundredths", result[i]));
        }
        return new(Vector(result), new(lines));
    }
    private CommerceVector ProcessCommerce(Simulation sim, City city, long production)
    {
        if (city.ProductionQueue.FirstOrDefault() is not { Kind: "economy:process" } order) return default;
        return Convert(sim, city.Id, order.ContentId, production);
    }
    public RequirementResult Validate(Simulation sim, int city, string process)
    {
        if (!sim.Content.TryGet<ProcessDef>(process, out var def)) return new(false, [new(null, "Unknown process")]);
        var subject = sim.State.CityById(city);
        return sim.Content.Requirements.Evaluate(def.Requirements, new(sim, subject.Owner, subject, sim.State.Grid.TileAt(subject.Position)));
    }
    public CommerceVector Convert(Simulation sim, int city, string process, long production) => Validate(sim, city, process).Success
        ? sim.Content.Get<ProcessDef>(process).ProductionToCommerceModifiers * production : default;
    public long CultureHundredths(Simulation sim, int city) => sim.Content.Services.Get<IPlayerEconomyService>().Single().CalculateCity(sim, city).Hundredths.Culture;
}
