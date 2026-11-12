namespace Ariel.Game;

/// <summary>One unit carrying out one mission: who orders it, the unit, the city it stands in, and what the mission
/// is about (a religion, a corporation, a building, a technology), empty for a mission with nothing to choose.</summary>
public readonly record struct MissionOrder(int Player, Unit Unit, City City, string Subject);

/// <summary>What becomes of the unit once its mission is resolved.</summary>
public enum MissionUnitFate
{
    /// <summary>The unit is used up whether the mission works or not.</summary>
    Spent,
    /// <summary>The unit is lost only when the mission fails.</summary>
    SpentOnFailure,
    /// <summary>The unit survives.</summary>
    Kept,
}

/// <summary>What a mission asks and risks, as it would be carried out now.</summary>
/// <param name="Chance">Percent chance of success, rolled once when the mission is performed; null is a mission
/// that cannot fail and rolls nothing.</param>
/// <param name="Note">Short text for the order's tooltip, such as a price. The mission collects its own price.</param>
/// <param name="Fate">What becomes of the unit.</param>
public sealed record MissionTerms(int? Chance = null, string Note = "", MissionUnitFate Fate = MissionUnitFate.Spent)
{
    /// <summary>The note and the chance as one line of tooltip text.</summary>
    public string Summary => Chance is { } chance ? (Note.Length == 0 ? $"{chance}% chance" : $"{Note}, {chance}% chance") : Note;
}

/// <summary>A kind of mission: something a unit does to the city it stands in. A feature registers each kind it
/// adds as a service; the base game checks the unit, rolls the chance, removes a spent unit, publishes
/// <see cref="MissionResolved"/> and refreshes sight, and offers every legal mission in the unit's panel.</summary>
public interface IMission
{
    /// <summary>Stable namespaced id, saved in commands, such as "religion:spread".</summary>
    string Id { get; }
    /// <summary>The subjects this unit could try here, in a stable order, before validation. A mission with nothing
    /// to choose yields one empty subject for a unit of the right kind, and nothing otherwise.</summary>
    IEnumerable<string> Subjects(Simulation sim, Unit unit, City city);
    /// <summary>The text of the order's button for a subject.</summary>
    string Label(Simulation sim, string subject);
    /// <summary>Why the mission cannot be carried out, or null. The unit is already known to belong to the player, to
    /// be alive with moves left, and to stand in the city.</summary>
    string? Validate(Simulation sim, MissionOrder order);
    /// <summary>The terms of a valid order. Must not change state or consume random numbers.</summary>
    MissionTerms Terms(Simulation sim, MissionOrder order);
    /// <summary>Collect the price and apply the result of a valid order whose chance has been rolled. The unit is
    /// still on the map; the base game removes it afterwards when its fate says so.</summary>
    void Resolve(Simulation sim, MissionOrder order, bool success);
}

/// <summary>The base game's handling of every registered <see cref="IMission"/>.</summary>
public interface IMissionService
{
    /// <summary>Why a unit cannot carry out a mission, or null.</summary>
    string? Validate(Simulation sim, int player, int unit, string mission, string subject);
    /// <summary>The terms of a mission that validates, or null for one that does not.</summary>
    MissionTerms? Terms(Simulation sim, int player, int unit, string mission, string subject);
    /// <summary>Carry out a valid mission.</summary>
    void Perform(Simulation sim, int player, int unit, string mission, string subject);
}

/// <summary>Order a unit to carry out a mission in the city it stands in.</summary>
public sealed class PerformMissionCommand : GameCommand
{
    public int Unit { get; init; }
    public string Mission { get; init; } = "";
    public string Subject { get; init; } = "";
    public override string? Validate(Simulation sim) => sim.Rules().Missions.Validate(sim, Player, Unit, Mission, Subject);
    public override void Apply(Simulation sim) => sim.Rules().Missions.Perform(sim, Player, Unit, Mission, Subject);
}

/// <summary>A mission was carried out. Features publish their own events for what the mission changed.</summary>
public sealed record MissionResolved(string Mission, string Subject, int Unit, int City, int Player, bool Success) : DomainEvent;
