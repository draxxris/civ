using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Rules.Economy;

internal sealed class CityCommerce : ICityCultureSource, ICityProcessService
{
    internal static readonly string[] Types = ["gold", "research", "culture", "espionage"];
    internal static long[] Parts(CommerceVector value) => [value.Gold, value.Research, value.Culture, value.Espionage];
    internal static CommerceVector Vector(IReadOnlyList<long> parts) => new(parts[0], parts[1], parts[2], parts[3]);
    /// <summary>What a city's commerce is made of besides the slider rates: its commerce yield, direct commerce,
    /// percent modifiers, process output and disorder. The rates enter only <see cref="Split"/>.</summary>
    internal sealed record Inputs(CityYieldResult Yields, long Raw, long[] Direct, long[] Modifier, long[] Process, bool Disorder);
    internal Inputs Read(Simulation sim, int cityId)
    {
        var city = sim.State.CityById(cityId);
        var yields = sim.Rules().CityYields.Calculate(sim, cityId);
        return new(yields, yields.Yields.Commerce * 100, Parts(yields.DirectCommerceHundredths), Parts(yields.CommercePercent),
            Parts(ProcessCommerce(sim, city, yields.Yields.Production)), GovernmentStatus.IsDisorder(sim, city));
    }
    /// <summary>The city's commerce in hundredths under <paramref name="rates"/>, and the slider split and factor of each
    /// type, as <see cref="Calculate"/> reports them.</summary>
    internal static (long[] Result, long[] Split, long[] Percent) Split(Inputs inputs, CommerceVector rates)
    {
        var slider = Parts(rates); long raw = inputs.Raw;
        var split = new long[4];
        long allocated = 0;
        for (int i = 0; i < 4; i++) allocated += split[i] = raw * slider[i] / 100;
        split[0] += raw - allocated; // Gold receives every unallocated hundredth.
        var result = new long[4]; var percents = new long[4];
        for (int i = 0; i < 4; i++)
        {
            long baseRate = split[i] + inputs.Direct[i];
            long percent = percents[i] = Math.Max(1, 100 + inputs.Modifier[i]);
            long modified = baseRate > 0 ? baseRate * percent / 100 : baseRate * 100 / percent;
            long final = modified + inputs.Process[i];
            if (i == 1 || i == 2) final = Math.Max(0, final);
            result[i] = inputs.Disorder ? 0 : final;
        }
        return (result, split, percents);
    }
    internal CityCommerceResult Calculate(Simulation sim, int cityId, CommerceVector rates)
    {
        var inputs = Read(sim, cityId);
        var lines = inputs.Yields.Ledger.Lines.ToList();
        var (result, split, percents) = Split(inputs, rates);
        for (int i = 0; i < 4; i++)
        {
            string key = "economy:" + Types[i];
            lines.Add(new("economy:slider", key + "_split_hundredths", split[i]));
            lines.Add(new("economy:direct", key + "_direct_hundredths", inputs.Direct[i]));
            lines.Add(new("economy:modifier", key + "_factor_percent", percents[i]));
            lines.Add(new("economy:process", key + "_process_hundredths", inputs.Process[i]));
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
    public long CultureHundredths(Simulation sim, int city) => sim.Rules().Economy.CalculateCity(sim, city).Hundredths.Culture;
}
