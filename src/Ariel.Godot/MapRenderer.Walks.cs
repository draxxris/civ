using Godot;

namespace Ariel.Client;

/// <summary>
/// Units walking from plot to plot, replayed in the order the session reports steps and battles. Every unit walks its
/// own steps one after another, all units at once; a battle waits for the steps before it, and steps after a battle
/// wait for it. A walking unit leaves its stack and comes back when it has no steps left. Steps the local player
/// cannot see, and units without a 3D model, skip straight to the end.
/// </summary>
internal sealed partial class MapRenderer
{
    private const float StepTime = 0.22f, WalkLift = 0.015f;
    /// <summary>Longest run of steps a walker plays at full length; longer runs speed up to fit.</summary>
    private const int FullSpeedSteps = 6;

    private readonly record struct Step(Vector3 From, Vector3 To, int After);

    private sealed class Walker(UnitActor actor)
    {
        public UnitActor Actor { get; } = actor;
        public Queue<Step> Steps { get; } = new();
        public float Time { get; set; } = -1; // Negative until the first step may start.
    }

    private readonly Dictionary<int, Walker> _walkers = new();
    private readonly List<int> _walked = new();
    private readonly Queue<(BattlePlayback Battle, int StepsBefore)> _pendingBattles = new();
    private int _battlesQueued, _battlesDone, _stepsQueued, _stepsDone;

    /// <summary>Whether units are still walking or waiting to fight on screen.</summary>
    private bool Replaying => _walkers.Count > 0 || _battle != null;

    /// <summary>Take everything the session has to replay. Steps join their unit's walk; battles wait their turn.</summary>
    private void DrainPlayback()
    {
        while (_session.DequeuePlayback() is { } item)
        {
            if (item is BattlePlayback battle)
            {
                _pendingBattles.Enqueue((battle, _stepsQueued));
                _battlesQueued++;
                continue;
            }
            var step = (StepPlayback)item;
            _stepsQueued++;
            if (Clean || !_models.Has(step.Type) || (!Seen(step.From) && !Seen(step.To))) { _stepsDone++; continue; }
            if (!_walkers.TryGetValue(step.UnitId, out var walker))
            {
                var actor = new UnitActor(_models.Instantiate(step.Type), OwnerColor(step.Owner)) { Ground = GroundAt };
                actor.Anchor = WorldOf(step.From, WalkLift);
                actor.Position = actor.Anchor;
                AddChild(actor);
                actor.Play("idle");
                walker = new Walker(actor);
                _walkers[step.UnitId] = walker;
                _revision = -1; // Redraw the stacks without the walker.
            }
            var from = WorldOf(step.From, WalkLift);
            var to = WorldOf(step.To, WalkLift);
            if (Period > 0) to.X += Mathf.Round((from.X - to.X) / Period) * Period;
            walker.Steps.Enqueue(new Step(from, to, _battlesQueued));
        }
    }

    /// <summary>Start the next battle once every step before it has finished; ones nobody can see are skipped.</summary>
    private void StartNextBattle()
    {
        while (_battle == null && _pendingBattles.TryPeek(out var next) && _stepsDone >= next.StepsBefore)
        {
            _pendingBattles.Dequeue();
            var p = next.Battle;
            if (!Seen(p.AttackerHex) && !Seen(p.DefenderHex)) { _battlesDone++; continue; }
            SetWalkerVisible(p.AttackerId, false);
            SetWalkerVisible(p.DefenderId, false);
            _battle = Begin(p);
        }
    }

    private void SetWalkerVisible(int unit, bool visible)
    {
        if (_walkers.TryGetValue(unit, out var walker)) walker.Actor.Visible = visible;
    }

    private void AdvanceWalkers(float delta)
    {
        foreach (var (id, walker) in _walkers)
        {
            if (!walker.Steps.TryPeek(out var step)) { _walked.Add(id); continue; }
            var actor = walker.Actor;
            if (walker.Time < 0)
            {
                if (step.After > _battlesDone) continue; // A battle before this step is still to play.
                walker.Time = 0;
                actor.Anchor = step.From;
                actor.FaceToward(step.To - step.From);
                actor.Play("run");
            }
            float duration = StepTime * Mathf.Min(1f, (float)FullSpeedSteps / walker.Steps.Count);
            walker.Time += delta;
            float f = Mathf.Clamp(walker.Time / duration, 0f, 1f);
            var at = step.From.Lerp(step.To, f);
            at.Y = GroundAt(at.X, at.Z) + WalkLift;
            actor.Anchor = at;
            if (f < 1f) continue;
            walker.Steps.Dequeue();
            walker.Time = -1;
            _stepsDone++;
            if (walker.Steps.Count == 0) _walked.Add(id);
            else actor.Play("idle");
        }
        if (_walked.Count == 0) return;
        foreach (int id in _walked)
        {
            _walkers.Remove(id, out var done);
            done!.Actor.QueueFree();
        }
        _walked.Clear();
        _revision = -1; // Put the walkers back into their stacks.
    }
}
