using System.Text.Json;
using Ariel.Content;
using Ariel.Modding;

namespace Ariel.Game;

/// <summary>
/// A player intent. Commands are the only thing sent over the network. They must be plain
/// JSON-serializable data; Validate/Apply must depend only on the simulation state.
/// </summary>
public abstract class GameCommand
{
    /// <summary>Issuing player id.</summary>
    public int Player { get; init; }

    /// <summary>Whether a player may issue this command outside its turn segment. Session control, not play.</summary>
    public virtual bool OutOfTurn => false;

    /// <summary>Return null if legal, otherwise the reason it was rejected.</summary>
    public abstract string? Validate(Simulation sim);
    /// <summary>Apply a validated command deterministically.</summary>
    public abstract void Apply(Simulation sim);
}

/// <summary>Built-in because it is the lockstep turn primitive. When every player of the active segment has ended, the
/// next segment starts; after the last, the turn advances.</summary>
public sealed class EndTurnCommand : GameCommand
{
    /// <inheritdoc/>
    public override string? Validate(Simulation sim)
    {
        if (!sim.State.Players.Any(p => p.Id == Player)) return "unknown player";
        return sim.State.PlayerById(Player).EndedTurn ? "Player has already ended turn." : null;
    }

    /// <inheritdoc/>
    public override void Apply(Simulation sim)
    {
        sim.State.PlayerById(Player).EndedTurn = true;
        sim.ContinueTurn(mayAdvance: true);
    }
}

/// <summary>
/// Hands a player to an AI or back to a human, as a lockstep command so every peer switches at the same step: a
/// multiplayer host issues it when a player disconnects or rejoins. A player handed to the AI mid-turn plays that turn
/// at once if its segment is moving, so the game does not wait for someone who left. <see cref="GameCommand.Player"/>
/// is the issuer; <see cref="Target"/> is the player whose control changes.
/// </summary>
public sealed class SetPlayerControlCommand : GameCommand
{
    /// <summary>The player whose control changes.</summary>
    public int Target { get; init; }
    /// <summary>The AI service to take over, or null to hand the player back to a human.</summary>
    public string? Ai { get; init; }

    /// <inheritdoc/>
    public override bool OutOfTurn => true;

    /// <inheritdoc/>
    public override string? Validate(Simulation sim)
    {
        if (!sim.State.Players.Any(p => p.Id == Target)) return "unknown player";
        if (Ai != null && !sim.Content.PlayerAIs.All.Any(a => a.Id == Ai)) return $"unknown AI '{Ai}'";
        return sim.State.PlayerById(Target).Ai == Ai ? "Nothing to change." : null;
    }

    /// <inheritdoc/>
    public override void Apply(Simulation sim)
    {
        sim.State.PlayerById(Target).Ai = Ai;
        if (Ai != null) sim.ContinueTurn(mayAdvance: true);
    }
}

/// <summary>Mod command JSON envelopes with registered type identities.</summary>
public sealed class CommandCodec
{
    private readonly ContentDb _content;
    private readonly Dictionary<Type, string> _ids = new();

    /// <summary>Create a codec for frozen command registrations.</summary>
    public CommandCodec(ContentDb content)
    {
        _content = content;
        foreach (var t in content.CommandTypes.All) _ids[t.Type] = t.Id;
    }

    /// <summary>Encode a registered command as a type/data envelope.</summary>
    public string Encode(GameCommand cmd)
    {
        var type = cmd.GetType();
        if (!_ids.TryGetValue(type, out var id)) throw new InvalidOperationException($"{type.Name} is not registered as a command");
        return JsonSerializer.Serialize(new Envelope(id, JsonSerializer.SerializeToElement(cmd, type, ModLoader.Json)), ModLoader.Json);
    }

    /// <summary>Decode using this ruleset's registered command type.</summary>
    public GameCommand Decode(string text)
    {
        var env = JsonSerializer.Deserialize<Envelope>(text, ModLoader.Json) ?? throw new InvalidDataException("empty command");
        var def = _content.CommandTypes.Get(env.Type);
        return (GameCommand)(env.Data.Deserialize(def.Type, ModLoader.Json) ?? throw new InvalidDataException("bad command body"));
    }

    private sealed record Envelope(string Type, JsonElement Data);
}
