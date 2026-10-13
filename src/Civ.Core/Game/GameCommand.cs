using System.Text.Json;
using Civ.Content;
using Civ.Modding;

namespace Civ.Game;

/// <summary>
/// A player intent. Commands are the only thing sent over the network. They must be plain
/// JSON-serializable data; Validate/Apply must depend only on the simulation state.
/// </summary>
public abstract class GameCommand
{
    /// <summary>Issuing player id.</summary>
    public int Player { get; init; }

    /// <summary>Return null if legal, otherwise the reason it was rejected.</summary>
    public abstract string? Validate(Simulation sim);
    /// <summary>Apply a validated command deterministically.</summary>
    public abstract void Apply(Simulation sim);
}

/// <summary>Built-in because it is the lockstep turn primitive. When every player has ended, the turn advances.</summary>
public sealed class EndTurnCommand : GameCommand
{
    /// <inheritdoc/>
    public override string? Validate(Simulation sim) =>
        sim.State.Players.Any(p => p.Id == Player) ? null : "unknown player";

    /// <inheritdoc/>
    public override void Apply(Simulation sim)
    {
        sim.State.PlayerById(Player).EndedTurn = true;
        if (sim.State.Players.All(p => p.EndedTurn)) sim.AdvanceTurn();
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
