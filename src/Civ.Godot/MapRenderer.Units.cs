using Civ.Game;
using Godot;

namespace Civ.Client;

/// <summary>3D unit models standing on the map, and the Civ 4 style attack and death animations played over them.</summary>
internal sealed partial class MapRenderer
{
    private const float StrikeTime = 0.3f, FinaleTime = 0.45f, HoldTime = 0.15f, MaxLunge = 0.35f;
    private const float FacingStep = Mathf.Pi / 3, FacingOffset = Mathf.Pi / 6;

    private sealed class Battle(BattlePlayback playback, UnitActor attacker, UnitActor defender, Vector3 from, Vector3 to, bool[] strikes, bool slides)
    {
        public BattlePlayback Playback { get; } = playback;
        public UnitActor Attacker { get; } = attacker;
        public UnitActor Defender { get; } = defender;
        public Vector3 From { get; } = from;
        public Vector3 To { get; } = to;
        public bool[] Strikes { get; } = strikes;
        public bool Slides { get; } = slides;
        public float Time { get; set; }
        public int Strike { get; set; } = -1;
        public bool Finale { get; set; }
    }

    private readonly Dictionary<int, UnitActor> _actors = new();
    private readonly Dictionary<int, Hex> _lastHex = new();
    private readonly Dictionary<int, float> _facing = new();
    private readonly List<(Unit Unit, Vector3 At)> _standing = new();
    private readonly HashSet<int> _battleIds = new();
    private Battle? _battle;

    /// <summary>True while a battle animation is on screen.</summary>
    public bool Fighting => _battle != null;

    private static Color OwnerColor(int owner) => Ui.ToColor(MapStyle.PlayerColor(owner));

    private void SyncActors()
    {
        var wanted = new HashSet<int>();
        foreach (var (unit, at) in _standing)
        {
            wanted.Add(unit.Id);
            if (!_actors.TryGetValue(unit.Id, out var actor))
            {
                actor = new UnitActor(_models.Instantiate(unit.Type), OwnerColor(unit.Owner)) { Ground = GroundAt };
                _actors[unit.Id] = actor;
                AddChild(actor);
            }
            actor.Anchor = at;
            actor.Position = actor.Anchor;
            actor.Rotation = new Vector3(0, FacingOf(unit), 0);
            actor.Play(_session.ActivityOf(unit) is UnitActivity.Fortify or UnitActivity.Sentry ? "fortify" : "idle");
        }
        foreach (var id in _actors.Keys.Where(id => !wanted.Contains(id)).ToList())
        {
            _actors[id].QueueFree();
            _actors.Remove(id);
            _lastHex.Remove(id);
            _facing.Remove(id);
        }
    }

    /// <summary>One of the six hex facings: the direction of the unit's last step, or the previous facing.</summary>
    private float FacingOf(Unit unit)
    {
        float yaw = _facing.GetValueOrDefault(unit.Id, 0f);
        if (_lastHex.TryGetValue(unit.Id, out var previous) && previous != unit.Position)
        {
            var d = WorldOf(unit.Position) - WorldOf(previous);
            if (Period > 0 && Mathf.Abs(d.X) > Period / 2) d.X -= Mathf.Sign(d.X) * Period;
            yaw = Mathf.Round((Mathf.Atan2(d.X, d.Z) - FacingOffset) / FacingStep) * FacingStep + FacingOffset;
        }
        _lastHex[unit.Id] = unit.Position;
        return _facing[unit.Id] = yaw;
    }

    /// <summary>Keep every actor on the world copy nearest the camera, like the labels.</summary>
    private void PlaceActors(float focusX)
    {
        foreach (var actor in _actors.Values) Place(actor, focusX);
        if (_battle == null) return;
        Place(_battle.Attacker, focusX);
        Place(_battle.Defender, focusX);
    }

    private void Place(UnitActor actor, float focusX)
    {
        var p = actor.Anchor;
        if (Period > 0) p.X += Mathf.Round((focusX - p.X) / Period) * Period;
        actor.Position = p;
    }

    // --- battles --------------------------------------------------------------------------------

    private bool Seen(Hex hex) => _session.SightOf(_session.Sim.State.PlotAt(hex)) == Sight.Visible;

    private void StartNextBattle()
    {
        while (_session.DequeueBattle() is { } playback)
        {
            if (!Seen(playback.AttackerHex) && !Seen(playback.DefenderHex)) continue;
            _battle = Begin(playback);
            return;
        }
    }

    private Battle Begin(BattlePlayback p)
    {
        foreach (var id in new[] { p.AttackerId, p.DefenderId })
        {
            _battleIds.Add(id);
            if (_actors.Remove(id, out var old)) old.QueueFree();
        }
        _revision = -1; // Redraw the flat layer without the two fighters.
        var from = WorldOf(p.AttackerHex, 0.015f);
        var to = WorldOf(p.DefenderHex, 0.015f);
        if (Period > 0) to.X += Mathf.Round((from.X - to.X) / Period) * Period;
        var attacker = new UnitActor(_models.Instantiate(p.AttackerType), OwnerColor(p.AttackerOwner)) { Anchor = from, Ground = GroundAt };
        var defender = new UnitActor(_models.Instantiate(p.DefenderType), OwnerColor(p.DefenderOwner)) { Anchor = to, Ground = GroundAt };
        AddChild(attacker);
        AddChild(defender);
        attacker.FaceToward(to - from);
        defender.FaceToward(from - to);
        attacker.Play("idle");
        defender.Play("idle");
        bool slides = p.WinnerId == p.AttackerId && _session.Sim.State.FindUnit(p.AttackerId)?.Position == p.DefenderHex;
        return new Battle(p, attacker, defender, from, to, Strikes(p), slides);
    }

    /// <summary>Who lands each blow, true for the attacker; at most four, ending with the winner's.</summary>
    private static bool[] Strikes(BattlePlayback p)
    {
        bool attackerWon = p.WinnerId == p.AttackerId;
        var hits = new List<bool>();
        int attacker = 100, defender = 100;
        foreach (var round in p.Rounds)
        {
            if (round.DefenderHealth < defender) hits.Add(true);
            else if (round.AttackerHealth < attacker) hits.Add(false);
            attacker = round.AttackerHealth; defender = round.DefenderHealth;
        }
        if (hits.Count == 0) hits.AddRange([true, false, true]);
        var sampled = hits.Count <= 4 ? hits : Enumerable.Range(0, 4).Select(i => hits[i * (hits.Count - 1) / 3]).ToList();
        sampled[^1] = attackerWon;
        if (sampled.Count < 2) sampled.Insert(0, !attackerWon);
        return sampled.ToArray();
    }

    public override void _Process(double delta)
    {
        if (_battle != null) Advance(_battle, (float)delta);
    }

    private void Advance(Battle b, float delta)
    {
        b.Time += delta;
        float strikesEnd = b.Strikes.Length * StrikeTime;
        var a = b.Attacker; var d = b.Defender;
        if (b.Time < strikesEnd) { Exchange(b); return; }
        if (!b.Finale)
        {
            b.Finale = true;
            a.Lunge = d.Lunge = a.Shake = d.Shake = 0;
            var (winner, loser) = b.Playback.WinnerId == b.Playback.AttackerId ? (a, d) : (d, a);
            winner.Play("idle");
            if (b.Playback.LoserDied) loser.Play("die", FinaleTime); else loser.Play("idle");
        }
        float f = Mathf.Clamp((b.Time - strikesEnd) / FinaleTime, 0f, 1f);
        if (b.Playback.LoserDied)
        {
            var loser = b.Playback.LoserId == b.Playback.AttackerId ? a : d;
            if (!loser.HasClip("die")) loser.Fall = f;
        }
        if (b.Slides)
        {
            var at = b.From.Lerp(b.To, f * f * (3 - 2 * f));
            at.Y = GroundAt(at.X, at.Z) + 0.015f;
            a.Anchor = at;
        }
        if (b.Time >= strikesEnd + FinaleTime + HoldTime) EndBattle(b);
    }

    private static void Exchange(Battle b)
    {
        int i = Mathf.Min((int)(b.Time / StrikeTime), b.Strikes.Length - 1);
        float f = Mathf.Clamp((b.Time - i * StrikeTime) / StrikeTime, 0f, 1f);
        bool attackerHits = b.Strikes[i];
        var hitter = attackerHits ? b.Attacker : b.Defender;
        var victim = attackerHits ? b.Defender : b.Attacker;
        if (i != b.Strike)
        {
            b.Strike = i;
            hitter.Play("attack", StrikeTime);
            victim.Play("hurt", StrikeTime);
        }
        hitter.Lunge = hitter.HasClip("attack") ? 0 : Mathf.Sin(f * Mathf.Pi) * MaxLunge;
        hitter.Shake = 0;
        victim.Lunge = 0;
        victim.Shake = !victim.HasClip("hurt") && f > 0.4f ? Mathf.Sin(f * 50f) * 0.05f * (1 - f) : 0;
    }

    private void EndBattle(Battle b)
    {
        b.Attacker.QueueFree();
        b.Defender.QueueFree();
        _battleIds.Remove(b.Playback.AttackerId);
        _battleIds.Remove(b.Playback.DefenderId);
        _battle = null;
        _revision = -1;
    }
}
