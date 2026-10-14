using Civ.Game;
using Schema = Civ.Content.Schema;

namespace Civ.Client;

/// <summary>Something that happened that the local player should hear about. <see cref="Turn"/> is the turn it happened in.</summary>
public sealed record TurnNotification(int Turn, string Kind, string Text, int? City = null, Hex? Where = null);

public sealed partial class GameSession
{
    private const int MaxNotifications = 200;

    private readonly List<TurnNotification> _notifications = [];
    private IReadOnlyList<DomainEvent> _seenPrevious = [];
    private int _seenCount;
    private HashSet<int> _myUnits = [];

    /// <summary>Every notification since the game was started or loaded, oldest first (the newest 200 are kept).</summary>
    public IReadOnlyList<TurnNotification> Notifications => _notifications;

    /// <summary>Notifications from the current turn and the <paramref name="turns"/> - 1 turns before it.</summary>
    public IReadOnlyList<TurnNotification> RecentNotifications(int turns = 2) =>
        _notifications.Where(n => n.Turn > Sim.State.Turn - turns).ToList();

    private void ResetNotifications()
    {
        _notifications.Clear();
        _seenPrevious = Sim.Events.PreviousTurn;
        _seenCount = Sim.Events.Published.Count;
        _myUnits = MyUnitIds();
        SnapshotUnits();
        _battles.Clear();
    }

    private HashSet<int> MyUnitIds() => Sim.State.Units.Where(u => u.Owner == Human).Select(u => u.Id).ToHashSet();

    /// <summary>Read the events published since the last call. The simulation's bus keeps the current and previous turn, so polling after each command is enough.</summary>
    private void CollectNotifications()
    {
        var events = Sim.Events;
        int turn = Sim.State.Turn;
        if (!ReferenceEquals(events.PreviousTurn, _seenPrevious))
        {
            foreach (var e in events.PreviousTurn.Skip(_seenCount)) Notify(e, turn - 1);
            _seenPrevious = events.PreviousTurn;
            _seenCount = 0;
        }
        var published = events.Published;
        for (; _seenCount < published.Count; _seenCount++) Notify(published[_seenCount], turn);
        _myUnits = MyUnitIds();
        SnapshotUnits();
        if (_notifications.Count > MaxNotifications) _notifications.RemoveRange(0, _notifications.Count - MaxNotifications);
    }

    private void Notify(DomainEvent e, int turn)
    {
        if (e is CombatResolved combat) RecordBattle(combat);
        var note = Describe(e, turn);
        if (note != null) _notifications.Add(note);
    }

    private string CityName(int id) => Sim.State.FindCity(id)?.Name ?? "a city";
    private bool Mine(int city) => Sim.State.FindCity(city)?.Owner == Human;
    private string ReligionName(string id) => Sim.Content.TryGet<Schema.ReligionDef>(id, out var r) ? r.Name : Labels.Humanize(id);
    private string CorporationName(string id) => Sim.Content.TryGet<Schema.CorporationDef>(id, out var c) ? c.Name : Labels.Humanize(id);
    private static TurnNotification Note(int turn, string kind, string text, int? city = null, Hex? at = null) => new(turn, kind, text, city, at);

    private TurnNotification? Describe(DomainEvent e, int turn)
    {
        switch (e)
        {
            case TechDiscovered t when t.Team == MyTeam.Id:
                return Note(turn, "tech", $"Discovered {(Sim.Content.TryGet<Schema.TechDef>(t.Tech, out var tech) ? tech.Name : t.Tech)}");
            case CityFounded c when c.Player == Human:
                return Note(turn, "city", $"Founded {CityName(c.City)}", c.City, c.Position);
            case CityGrew g when Mine(g.City):
                return Note(turn, "growth", $"{CityName(g.City)} grew to size {g.Population}", g.City);
            case BuildingCompleted b when Mine(b.City):
                return Note(turn, "building", $"{CityName(b.City)} completed {(Sim.Content.TryGet<Schema.BuildingDef>(b.Building, out var def) ? def.Name : b.Building)}", b.City);
            case UnitCreated u when u.Player == Human:
                _myUnits.Add(u.Unit); // Later events in this batch, such as a fight on the same roll, belong to this unit.
                return Note(turn, "unit", $"New {(Sim.Content.TryGet<Schema.UnitDef>(u.Type, out var unit) ? unit.Name : u.Type)}", null, u.Position);
            case CombatResolved c: return Combat(c, turn);
            case ReligionFounded r:
                return Note(turn, "religion", $"{ReligionName(r.Religion)} was founded in {CityName(r.City)}", r.City);
            case ReligionSpread s when Mine(s.City):
                return Note(turn, "religion", $"{ReligionName(s.Religion)} spread to {CityName(s.City)}", s.City);
            case StateReligionChanged s:
                return Note(turn, "religion", s.Religion == null ? $"{PlayerName(s.Player)} no longer has a state religion"
                    : $"{PlayerName(s.Player)} adopted {ReligionName(s.Religion)}");
            case MissionaryAttempt m when _myUnits.Contains(m.Unit) || Mine(m.City):
                return Note(turn, "religion", $"Missionary {(m.Success ? "spread" : "failed to spread")} {ReligionName(m.Religion)} in {CityName(m.City)}", m.City);
            case CityCaptured c when c.Player == Human || c.PreviousOwner == Human:
                return Note(turn, "city", c.Player == Human ? $"Captured {CityName(c.City)}" : $"{CityName(c.City)} was captured", c.City);
            case CityRazed c when c.Player == Human:
                return Note(turn, "city", "A city was razed", null, c.Position);
            case GoodyReceived g when _myUnits.Contains(g.Unit):
                return Note(turn, "goody", $"Found {Labels.Humanize(g.Goody)}");
            case CorporationFounded f when Mine(f.City) || f.Player == Human:
                return Note(turn, "corporation", $"{CorporationName(f.Corporation)} was founded in {CityName(f.City)}", f.City);
            case CorporationSpread s when Mine(s.City):
                return Note(turn, "corporation", $"{CorporationName(s.Corporation)} spread to {CityName(s.City)}", s.City);
            case EspionageMissionResult m when m.Unit >= 0 && (_myUnits.Contains(m.Unit) || Mine(m.City)):
                return Note(turn, "espionage", $"Spy mission {Labels.Humanize(m.Mission.ToString())} in {CityName(m.City)} {(m.Success ? "succeeded" : "failed")}", m.City);
            case EventTriggered t when t.Player == Human:
                return Note(turn, "event", $"Event: {(Sim.Content.TryGet<Schema.EventTriggerDef>(t.Trigger, out var trigger) ? trigger.Name : Labels.Humanize(t.Trigger))}",
                    t.City >= 0 ? t.City : null);
            case EventResolved r when r.Player == Human:
                return Note(turn, "event", $"{(Sim.Content.TryGet<Schema.EventTriggerDef>(r.Trigger, out var resolved) ? resolved.Name : Labels.Humanize(r.Trigger))} resolved"
                    + (r.Automatic ? " automatically" : ""));
            case ProjectCompleted p when p.Player == Human || p.Team == MyTeam.Id:
                return Note(turn, "project", $"{CityName(p.City)} completed {(Sim.Content.TryGet<Schema.ProjectDef>(p.Project, out var project) ? project.Name : Labels.Humanize(p.Project))}", p.City);
            case VictoryAchieved v:
                return Note(turn, "victory", $"{PlayerName(v.Player)} won a {(Sim.Content.TryGet<Schema.VictoryDef>(v.Victory, out var victory) ? victory.Name : Labels.Humanize(v.Victory))}");
            case CityRebellionTriggered r when r.Player == Human || Mine(r.City):
                return Note(turn, "rebellion", $"{CityName(r.City)} rebelled: {r.Units.Count} rebel units appeared", r.City);
            case CityRebellionSuppressed r when r.Player == Human:
                return Note(turn, "rebellion", $"Rebellion in {CityName(r.City)} was suppressed", r.City);
            case ScriptedWonderEffectApplied w when Mine(w.City):
                return Note(turn, "wonder", $"{(Sim.Content.TryGet<Schema.BuildingDef>(w.Building, out var wonder) ? wonder.Name : Labels.Humanize(w.Building))} in {CityName(w.City)}: {Labels.Humanize(w.Effect)}", w.City);
            default: return null;
        }
    }

    private TurnNotification? Combat(CombatResolved c, int turn)
    {
        int loser = c.Winner == c.Attacker ? c.Defender : c.Attacker;
        Hex? at = c.Report?.DefenderHex ?? Sim.State.FindUnit(c.Winner)?.Position; // The fight happened on the defender's hex, even if the winner has moved since.
        if (_myUnits.Contains(c.Winner)) return Note(turn, "combat", "Your unit won a battle", null, at);
        return _myUnits.Contains(loser) ? Note(turn, "combat", "Your unit was lost in battle", null, at) : null;
    }

    private string PlayerName(int player) => player == Human ? "You" : Sim.State.Players.FirstOrDefault(p => p.Id == player)?.Name ?? $"Player {player}";
}
