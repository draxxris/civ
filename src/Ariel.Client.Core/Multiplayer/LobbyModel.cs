using Ariel.Game;

namespace Ariel.Client;

/// <summary>Who plays a seat.</summary>
public enum SeatKind { Human, Ai }

/// <summary>One player slot. A human seat is open until a peer takes it. An empty <see cref="Leader"/> means a random
/// leader; a negative <see cref="Team"/> means the seat's own team.</summary>
public sealed record LobbySeat(SeatKind Kind, int? Peer = null, string Leader = "", string Civilization = "", int Team = -1);

/// <summary>A connected peer. One whose content differs from the host's cannot take a seat.</summary>
public sealed record LobbyPeer(int Peer, string Name, bool ContentMatches);

/// <summary>The host's game setup, apart from the seats.</summary>
public sealed record LobbySetup(
    string WorldSize, string GameSpeed, string Handicap, string MapGenerator, string StartingEra, ulong Seed,
    TurnMode TurnMode = TurnMode.Dynamic, IReadOnlyDictionary<string, bool>? GameOptions = null, int TurnTimerSeconds = 0)
{
    /// <summary>The single-player defaults, with dynamic turns.</summary>
    public static LobbySetup Default(GameCatalog catalog)
    {
        var d = catalog.DefaultOptions();
        return new(d.WorldSize, d.GameSpeed, d.Handicap, d.MapGenerator, d.StartingEra, (ulong)Environment.TickCount64);
    }
}

/// <summary>Everything every peer sees in the lobby. The host owns it and sends the whole state after each change.
/// <see cref="SavedTurn"/> is set when the host loaded a saved game: then the seats are that game's players.</summary>
public sealed record LobbyState(int Revision, LobbySetup Setup, IReadOnlyList<LobbySeat> Seats, IReadOnlyList<LobbyPeer> Peers, int? SavedTurn = null)
{
    /// <summary>Peers holding human seats, ascending: the lockstep peers of the game.</summary>
    public int[] SeatedPeers() => Seats.Where(s => s.Kind == SeatKind.Human && s.Peer != null).Select(s => s.Peer!.Value).Distinct().Order().ToArray();

    /// <summary>The seat a peer holds, or null.</summary>
    public int? SeatOf(int peer)
    {
        for (int i = 0; i < Seats.Count; i++)
            if (Seats[i].Kind == SeatKind.Human && Seats[i].Peer == peer) return i;
        return null;
    }

    public string NameOf(int peer) => Peers.FirstOrDefault(p => p.Peer == peer)?.Name ?? $"Player {peer}";

    /// <summary>Why the game cannot start yet, or null.</summary>
    public string? StartProblem()
    {
        if (Seats.Count < 2) return "A game needs at least two seats.";
        if (Seats.Any(s => s.Kind == SeatKind.Human && s.Peer == null)) return "Every human seat needs a player, or make it an AI.";
        if (SeatOf(0) == null) return "The host must take a seat.";
        if (Peers.Any(p => !p.ContentMatches && SeatOf(p.Peer) != null)) return "A seated player has different mods.";
        return null;
    }

    /// <summary>The game settings every peer will build the same world from.</summary>
    public SimulationSettings ToSettings(GameCatalog catalog)
    {
        var leaders = catalog.AssignLeaders(Seats.Select(s => (s.Leader, s.Civilization)).ToList(), Setup.Seed);
        var gameOptions = new SortedDictionary<string, bool>(StringComparer.Ordinal);
        foreach (var option in catalog.GameOptions) gameOptions[option.Id] = option.Default;
        if (Setup.GameOptions is not null)
            foreach (var option in Setup.GameOptions) gameOptions[option.Key] = option.Value;
        return new SimulationSettings
        {
            Seed = Setup.Seed, WorldSize = Setup.WorldSize, MapGenerator = Setup.MapGenerator, GameSpeed = Setup.GameSpeed,
            Handicap = Setup.Handicap, StartingEra = Setup.StartingEra, PlayerCount = Seats.Count,
            Leaders = leaders.Select(l => l.Id).ToArray(), Civilizations = leaders.Select(l => l.CivilizationId).ToArray(),
            PlayerAis = Seats.Select(s => s.Kind == SeatKind.Ai ? catalog.OpponentAi : null).ToArray(),
            PlayerTeams = Seats.Select((s, i) => s.Team >= 0 ? s.Team : i).ToArray(),
            GameOptions = gameOptions, TurnMode = Setup.TurnMode,
        }.Resolve(catalog.Content);
    }
}

/// <summary>What a peer needs to build the agreed game: new-game settings or a save, the lockstep peers, every peer's
/// seat and name, and its own seat. <see cref="ResumeTick"/> is set for a peer joining a game in progress, with the
/// batches the host held for that tick and later. <see cref="Early"/> holds messages that reached the lobby meant for the
/// game: lockstep input from peers already playing, and everything after the start; the lobby keeps adding to it until
/// it releases the connection.</summary>
public sealed record GameStart(SimulationSettings? Settings, int[] Peers, int LocalSeat, IReadOnlyDictionary<int, string> Names)
{
    public string? Save { get; init; }
    public int? ResumeTick { get; init; }
    public IReadOnlyDictionary<int, int> Seats { get; init; } = new Dictionary<int, int>();
    public IReadOnlyList<Ariel.Net.NetMessage> Early { get; init; } = [];
    internal IReadOnlyList<CarriedBatch> Batches { get; init; } = [];
    public int TurnTimerSeconds { get; init; }
}

/// <summary>A chat line; <see cref="Peer"/> is -1 for a system notice.</summary>
public sealed record ChatLine(int Peer, string Name, string Text);

/// <summary>Chat shared by the lobby and the game that follows it.</summary>
public sealed class ChatLog
{
    private const int Keep = 200;
    private readonly List<ChatLine> _lines = [];
    public IReadOnlyList<ChatLine> Lines => _lines;
    /// <summary>Bumps on every new line, for front ends to redraw only on change.</summary>
    public int Revision { get; private set; }

    public void Add(ChatLine line)
    {
        _lines.Add(line);
        if (_lines.Count > Keep) _lines.RemoveRange(0, _lines.Count - Keep);
        Revision++;
    }

    public void Notice(string text) => Add(new ChatLine(-1, "", text));
}
