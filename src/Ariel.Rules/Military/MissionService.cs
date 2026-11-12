using Ariel.Game;

namespace Ariel.Rules.Military;

/// <summary>Carries out missions for every feature that registers one: the checks every mission shares, the roll,
/// the unit's fate, the common event and the sight refresh.</summary>
internal sealed class MissionService : IMissionService, IUnitActionSource
{
    public string? Validate(Simulation sim, int player, int unit, string mission, string subject) =>
        Prepare(sim, player, unit, mission, subject, out _, out _);

    public MissionTerms? Terms(Simulation sim, int player, int unit, string mission, string subject) =>
        Prepare(sim, player, unit, mission, subject, out var kind, out var order) is null ? kind!.Terms(sim, order) : null;

    public void Perform(Simulation sim, int player, int unit, string mission, string subject)
    {
        if (Prepare(sim, player, unit, mission, subject, out var kind, out var order) is { } error)
            throw new InvalidOperationException(error);
        var terms = kind!.Terms(sim, order);
        bool success = terms.Chance is not { } chance || sim.Rng.Next(100) < chance;
        int city = order.City.Id;
        kind.Resolve(sim, order, success);
        bool spent = terms.Fate == MissionUnitFate.Spent || (terms.Fate == MissionUnitFate.SpentOnFailure && !success);
        if (spent && sim.State.FindUnit(unit) is not null) sim.State.RemoveUnit(unit);
        sim.Events.Publish(new MissionResolved(mission, subject, unit, city, player, success));
        sim.Rules().Visibility.Recompute(sim);
    }

    public IEnumerable<UnitActionOffer> Offers(Simulation sim, int player, Unit unit)
    {
        if (unit.Owner != player || unit.Health <= 0 || unit.MovesLeft <= 0 || sim.State.CityAt(unit.Position) is not { } city) yield break;
        foreach (var kind in sim.Rules().MissionKinds)
            foreach (string subject in kind.Subjects(sim, unit, city))
            {
                var order = new MissionOrder(player, unit, city, subject);
                // Only legal missions are offered, with their terms; an illegal one has no terms to show.
                if (kind.Validate(sim, order) is not null) continue;
                yield return new(subject.Length == 0 ? kind.Id : kind.Id + ":" + subject, kind.Label(sim, subject),
                    new PerformMissionCommand { Player = player, Unit = unit.Id, Mission = kind.Id, Subject = subject },
                    kind.Terms(sim, order).Summary);
            }
    }

    private static string? Prepare(Simulation sim, int player, int unitId, string mission, string subject,
        out IMission? kind, out MissionOrder order)
    {
        kind = null; order = default;
        var unit = sim.State.FindUnit(unitId);
        if (unit is null || unit.Owner != player || unit.Health <= 0) return "Not your unit";
        if (unit.MovesLeft <= 0) return "No moves left";
        if (sim.State.CityAt(unit.Position) is not { } city) return "Unit must be in a city";
        foreach (var candidate in sim.Rules().MissionKinds) if (candidate.Id == mission) kind = candidate;
        if (kind is null) return "Unknown mission";
        order = new(player, unit, city, subject ?? "");
        return kind.Validate(sim, order);
    }
}
