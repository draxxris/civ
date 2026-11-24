using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;

namespace Ariel.Rules.Economy;

internal sealed class CityCommerce : ICityCultureSource, ICityProcessService
{
    /// <summary>What a city's commerce is made of besides the slider rates: its commerce yield, direct commerce,
    /// percent modifiers, process output and disorder, each by commerce type in roster order. The rates enter only
    /// <see cref="Split"/>.</summary>
    internal sealed record Inputs(CommerceRoster Commerce, CityYieldResult Yields, long Raw, long[] Direct, long[] Modifier, long[] Process,
        bool Disorder);
    internal Inputs Read(Simulation sim, int cityId)
    {
        var city = sim.State.CityById(cityId);
        var commerce = sim.Rules().Commerce;
        var yields = sim.Rules().CityYields.Calculate(sim, cityId);
        return new(commerce, yields, yields.Yields.Commerce * 100, commerce.Parts(yields.DirectCommerceHundredths),
            commerce.Parts(yields.CommercePercent), commerce.Parts(ProcessCommerce(sim, city, yields.Yields.Production)),
            GovernmentStatus.IsDisorder(sim, city));
    }
    /// <summary>The city's commerce in hundredths under <paramref name="rates"/>, and the slider split and factor of each
    /// type, as <see cref="Calculate"/> reports them, by commerce type in roster order.</summary>
    internal static (long[] Result, long[] Split, long[] Percent) Split(Inputs inputs, CommerceVector rates)
    {
        var commerce = inputs.Commerce;
        var slider = commerce.Parts(rates); long raw = inputs.Raw;
        int count = slider.Length;
        var split = new long[count];
        long allocated = 0;
        for (int i = 0; i < count; i++) allocated += split[i] = raw * slider[i] / 100;
        split[commerce.Remainder.Index] += raw - allocated; // The remainder type (gold) receives every unallocated hundredth.
        var result = new long[count]; var percents = new long[count];
        for (int i = 0; i < count; i++)
        {
            long baseRate = split[i] + inputs.Direct[i];
            long percent = percents[i] = Math.Max(1, 100 + inputs.Modifier[i]);
            long modified = baseRate > 0 ? baseRate * percent / 100 : baseRate * 100 / percent;
            long final = modified + inputs.Process[i];
            if (commerce.Def(commerce.Types[i]).FloorAtZero) final = Math.Max(0, final);
            result[i] = inputs.Disorder ? 0 : final;
        }
        return (result, split, percents);
    }
    internal CityCommerceResult Calculate(Simulation sim, int cityId, CommerceVector rates)
    {
        var inputs = Read(sim, cityId);
        var commerce = inputs.Commerce;
        var lines = inputs.Yields.Ledger.Lines.ToList();
        var (result, split, percents) = Split(inputs, rates);
        var splitKeys = commerce.Names("economy:", "_split_hundredths");
        var directKeys = commerce.Names("economy:", "_direct_hundredths");
        var factorKeys = commerce.Names("economy:", "_factor_percent");
        var processKeys = commerce.Names("economy:", "_process_hundredths");
        var totalKeys = commerce.Names("economy:", "_hundredths");
        for (int i = 0; i < result.Length; i++)
        {
            lines.Add(new("economy:slider", splitKeys[i], split[i]));
            lines.Add(new("economy:direct", directKeys[i], inputs.Direct[i]));
            lines.Add(new("economy:modifier", factorKeys[i], percents[i]));
            lines.Add(new("economy:process", processKeys[i], inputs.Process[i]));
            lines.Add(new("economy:total", totalKeys[i], result[i]));
        }
        return new(commerce.Vector(result), new(lines));
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
