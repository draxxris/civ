using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Mods.Religion;

/// <summary>A missionary tries to bring its religion to the city it stands in, and is spent either way.</summary>
internal sealed class SpreadReligionMission : IMission
{
    public string Id => ReligionMissions.Spread;

    public IEnumerable<string> Subjects(Simulation sim, Unit unit, City city)
    {
        var def = sim.Content.Get<S.UnitDef>(unit.Type);
        return def.DefaultUnitAi != ReligionData.MissionaryUnitAi ? []
            : def.ReligionSpreads.Where(p => p.Value > 0).Select(p => ReligionIdentities.Religion(sim.Content, p.Key))
                .OfType<string>().Order(StringComparer.Ordinal);
    }

    public string Label(Simulation sim, string subject) => "Spread " + sim.Content.Get<ReligionDef>(subject).Name;

    public string? Validate(Simulation sim, MissionOrder order)
    {
        if (!sim.Content.TryGet<ReligionDef>(order.Subject, out _)) return "Unknown religion";
        var def = sim.Content.Get<S.UnitDef>(order.Unit.Type);
        if (def.DefaultUnitAi != ReligionData.MissionaryUnitAi
            || def.ReligionSpreads.GetValueOrDefault(ReligionIdentities.Identity(sim.Content, order.Subject)) <= 0)
            return "Unit cannot spread this religion";
        if (ReligionData.Has(sim, order.City, order.Subject)) return "Religion is already present";
        bool foreign = sim.TeamOf(order.City.Owner).Id != sim.TeamOf(order.Player).Id;
        return foreign && ReligionData.BlocksSpread(sim, order.City, order.Subject) ? "Foreign civics prohibit this religion" : null;
    }

    public MissionTerms Terms(Simulation sim, MissionOrder order)
    {
        int probability = sim.Content.Get<S.UnitDef>(order.Unit.Type).ReligionSpreads
            .GetValueOrDefault(ReligionIdentities.Identity(sim.Content, order.Subject));
        if (sim.TeamOf(order.City.Owner).Id != sim.TeamOf(order.Unit.Owner).Id) probability /= 2;
        int religions = sim.Content.Get<ReligionDef>().Count;
        probability += (religions - ReligionData.Present(sim, order.City).Count) * (100 - probability) / Math.Max(1, religions);
        return new(Chance: Math.Clamp(probability, 0, 100));
    }

    public void Resolve(Simulation sim, MissionOrder order, bool success)
    {
        if (success) ReligionData.Add(sim, order.City, order.Subject);
    }
}
