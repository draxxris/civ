using System.Text.Json;
using Ariel.Content;
using Ariel.Game;
using Ariel.Mods.Tests;

namespace Ariel.Benchmarks.AiEvaluation;

internal sealed class EvaluationGame(RunFiles files, EvaluationConfig config, ulong seed, CancellationToken cancellation)
{
    private int observedTurn;
    private int observedEvents;
    private WorkerActivityHistory[] workerActivity = [];
    public int SnapshotCount { get; private set; }
    public EconomicSnapshot[] FinalSnapshots { get; private set; } = [];

    public void Run(ContentDb content, Action<SimulationSettings> recordSettings)
    {
        cancellation.ThrowIfCancellationRequested();
        files.Progress("sample_started", seed, 1);
        try
        {
            var sim = GameFactory.CreateAi(content, seed, config.Players, config.World);
            recordSettings(sim.Settings);
            var totals = new CommerceVector[config.Players];
            workerActivity = Enumerable.Range(0, config.Players).Select(_ => new WorkerActivityHistory()).ToArray();
            DrainEvents(sim);
            Capture(sim, totals);
            for (int advance = 0; advance < config.Turns; advance++) Advance(sim, totals);
            cancellation.ThrowIfCancellationRequested();
            files.Progress("sample_completed", seed, sim.State.Turn);
        }
        catch (Exception error)
        {
            files.Progress("sample_failed", seed, Math.Max(1, observedTurn), error.Message);
            throw;
        }
    }

    private void Advance(Simulation sim, CommerceVector[] totals)
    {
        cancellation.ThrowIfCancellationRequested();
        int settlingTurn = sim.State.Turn;
        files.Progress("checkpoint_started", seed, settlingTurn + 1);
        try { sim.AdvanceTurn(); }
        catch
        {
            DrainEvents(sim);
            throw;
        }
        DrainEvents(sim);
        for (int player = 0; player < config.Players; player++)
        {
            var state = sim.State.PlayerById(player).Extensions.TryGet<EconomyPlayerState>(sim.Content);
            if (state?.SettledTurn != settlingTurn)
                throw new InvalidDataException($"Missing economy settlement for seed {seed}, player {player}, turn {settlingTurn}.");
            totals[player] += state.SettledCommerceHundredths;
        }
        Capture(sim, totals);
    }

    private void Capture(Simulation sim, CommerceVector[] totals)
    {
        var snapshots = new List<EconomicSnapshot>();
        for (int player = 0; player < config.Players; player++)
        {
            var snapshot = EvaluationMetrics.Capture(sim, seed, sim.State.PlayerById(player), totals[player], workerActivity[player]);
            files.Append("economy.jsonl", snapshot);
            snapshots.Add(snapshot);
            SnapshotCount++;
        }
        FinalSnapshots = snapshots.ToArray();
        files.Progress("checkpoint_completed", seed, sim.State.Turn);
    }

    private void DrainEvents(Simulation sim)
    {
        if (observedTurn != 0 && observedTurn != sim.State.Turn)
            WriteEvents(sim.Events.PreviousTurn, observedTurn, observedEvents, sim.State.Turn);
        int skip = observedTurn == sim.State.Turn ? observedEvents : 0;
        WriteEvents(sim.Events.Published, sim.State.Turn, skip, sim.State.Turn);
        observedTurn = sim.State.Turn;
        observedEvents = sim.Events.Published.Count;
    }

    private void WriteEvents(IReadOnlyList<DomainEvent> events, int sourceTurn, int skip, int checkpoint)
    {
        for (int ordinal = skip; ordinal < events.Count; ordinal++)
        {
            var message = events[ordinal];
            files.Append("events.jsonl", new
            {
                seed, scenario = EvaluationConfig.Scenario, checkpoint, sourceTurn, ordinal,
                eventType = message.GetType().Name,
                payload = JsonSerializer.SerializeToElement(message, message.GetType(), RunFiles.Json),
            });
        }
    }
}
