using System.Globalization;
using Ariel.Game;

namespace Ariel.Mods.GreatPeople;

/// <summary>A great person action as a mission. What is possible, and what it yields, comes from the one calculation
/// <see cref="GreatPersonActions.Available"/> that also answers the planner's previews, so an offer, its validation and
/// its result always agree. Every great person mission uses up the person.</summary>
internal abstract class GreatPersonMission(string id, string label, bool needsCity) : IMission
{
    internal static readonly IReadOnlyList<GreatPersonMission> All =
        [new Join(), new Construct(), new Hurry(), new Culture(), new Trade(), new Discover(), new Lead(), new GoldenAge()];

    public string Id => id;
    public bool NeedsCity => needsCity;
    public string Label(Simulation sim, string subject) => label;

    public IEnumerable<string> Subjects(Simulation sim, Unit unit, City? city) =>
        GreatPersonActions.Available(sim, unit.Owner, unit.Id).Where(a => a.Mission == Id).Select(a => a.Subject);

    public virtual string? Validate(Simulation sim, MissionOrder order) =>
        Action(sim, order) is null ? "Great Person action is not available" : null;

    public MissionTerms Terms(Simulation sim, MissionOrder order)
    {
        long amount = Action(sim, order)?.Amount ?? 0;
        return new(Note: amount > 0 ? amount.ToString(CultureInfo.InvariantCulture) : "", Fate: MissionUnitFate.Spent);
    }

    public void Resolve(Simulation sim, MissionOrder order, bool success)
    {
        var action = Action(sim, order)!;
        Apply(sim, order, action);
        sim.Events.Publish(new GreatPersonUsed(order.Player, order.Unit.Id, Id, action.Subject, action.Amount));
    }

    /// <summary>The action this order names, as it stands now, or null.</summary>
    protected virtual GreatPersonAction? Action(Simulation sim, MissionOrder order) =>
        GreatPersonActions.Available(sim, order.Player, order.Unit.Id).FirstOrDefault(a => a.Mission == Id && a.Subject == order.Subject);

    protected abstract void Apply(Simulation sim, MissionOrder order, GreatPersonAction action);

    private sealed class Join() : GreatPersonMission(GreatPersonMissions.Join, "Join city", true)
    {
        protected override void Apply(Simulation sim, MissionOrder order, GreatPersonAction action)
        {
            var state = GreatPeopleData.Edit(sim, order.City);
            state.SettledSpecialists[action.Subject] = state.SettledSpecialists.GetValueOrDefault(action.Subject) + 1;
        }
    }

    private sealed class Construct() : GreatPersonMission(GreatPersonMissions.Construct, "Construct building", true)
    {
        protected override void Apply(Simulation sim, MissionOrder order, GreatPersonAction action)
        {
            order.City.Buildings.Add(action.Subject);
            sim.Events.Publish(new BuildingCompleted(order.City.Id, action.Subject));
        }
    }

    private sealed class Hurry() : GreatPersonMission(GreatPersonMissions.Hurry, "Hurry production", true)
    {
        protected override void Apply(Simulation sim, MissionOrder order, GreatPersonAction action)
        {
            var queue = order.City.ProductionQueue;
            queue[0] = queue[0] with { Progress = queue[0].Progress + action.Amount };
        }
    }

    private sealed class Culture() : GreatPersonMission(GreatPersonMissions.Culture, "Great work", true)
    {
        protected override void Apply(Simulation sim, MissionOrder order, GreatPersonAction action)
        {
            int turns = GreatPeopleData.Raw(sim, "GREAT_WORKS_CULTURE_TURNS", 20) * GreatPeopleData.SpeedRules(sim).UnitGreatWorkPercent / 100;
            sim.Rules().CityCulture.AddCulture(sim, order.City.Id, action.Amount, turns);
        }
    }

    private sealed class Trade() : GreatPersonMission(GreatPersonMissions.Trade, "Trade mission", true)
    {
        protected override void Apply(Simulation sim, MissionOrder order, GreatPersonAction action) =>
            sim.State.PlayerById(order.Player).Treasury += action.Amount;
    }

    private sealed class Discover() : GreatPersonMission(GreatPersonMissions.Discover, "Discover technology", false)
    {
        protected override void Apply(Simulation sim, MissionOrder order, GreatPersonAction action) =>
            sim.Rules().Research.AddProgress(sim, order.Player, action.Subject, action.Amount);
    }

    private sealed class Lead() : GreatPersonMission(GreatPersonMissions.Lead, "Attach leader", false)
    {
        protected override void Apply(Simulation sim, MissionOrder order, GreatPersonAction action) =>
            GreatPersonActions.Lead(sim, order.Unit, action);
    }

    /// <summary>The subject names the participants, or is empty for the nearest ones; the panel offers only the
    /// empty subject.</summary>
    private sealed class GoldenAge() : GreatPersonMission(GreatPersonMissions.GoldenAge, "Start golden age", false)
    {
        public override string? Validate(Simulation sim, MissionOrder order)
        {
            if (Action(sim, order) is null) return "Great Person action is not available";
            return Participants(sim, order) is { Length: > 0 } ? null : "Select the required number of distinct Great Person classes";
        }

        protected override GreatPersonAction? Action(Simulation sim, MissionOrder order) =>
            GreatPersonActions.Available(sim, order.Player, order.Unit.Id).FirstOrDefault(a => a.Mission == Id);

        protected override void Apply(Simulation sim, MissionOrder order, GreatPersonAction action)
        {
            // The base game spends the performing unit; the other participants go here.
            foreach (int id in Participants(sim, order)!) if (id != order.Unit.Id) sim.State.RemoveUnit(id);
            GreatPeopleData.Edit(sim, order.Player).UnitGoldenAges++;
            GoldenAges.Start(sim, order.Player, false);
        }

        private static int[]? Participants(Simulation sim, MissionOrder order)
        {
            var requested = new List<int>();
            if (order.Subject.Length > 0)
                foreach (string part in order.Subject.Split(','))
                {
                    if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out int id)) return null;
                    requested.Add(id);
                }
            var units = GreatPersonActions.GoldenAgeUnits(sim, order.Player, order.Unit, requested);
            return units.Length == GreatPersonActions.Required(sim, order.Player) ? units : null;
        }
    }
}
