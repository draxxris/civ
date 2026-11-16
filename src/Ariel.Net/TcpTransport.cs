using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Channels;

namespace Ariel.Net;

/// <summary>
/// LAN and direct-IP transport over TCP, as a star: clients connect to the host, which relays what they send to the
/// other peers. Only the host needs a reachable port. Each connection has its own reader and writer tasks plus a
/// heartbeat, all on the thread pool whatever thread opened the transport, so sends never block the game and a long
/// frame or turn on the game thread never looks like a lost connection. Disposing sends what is still queued before
/// it closes. Frames are a 4-byte big-endian length followed by UTF-8 JSON.
/// </summary>
public sealed class TcpTransport : ITransport
{
    /// <summary>Largest frame accepted; a whole saved game travels as one message.</summary>
    public const int MaxFrameBytes = 512 * 1024 * 1024;
    /// <summary>Most bytes waiting to go out on one link. A peer that stops reading is dropped once its queue passes
    /// this, instead of the host buffering everything it relays to it.</summary>
    public const long MaxQueuedBytes = 64L * 1024 * 1024;
    /// <summary>A frame body is read in pieces of this size and grows as its bytes arrive, so a header that announces a
    /// huge frame costs nothing until the frame's bytes are actually sent.</summary>
    private const int ReadChunkBytes = 64 * 1024;
    /// <summary>How often an idle link sends a heartbeat.</summary>
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(1);
    /// <summary>Longest <see cref="Dispose"/> waits for the links to send what is queued and for the peers to close.</summary>
    public static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(1);

    private readonly ConcurrentQueue<Action> _inbox = new();
    private readonly ConcurrentDictionary<int, Link> _links = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly TimeSpan _silenceTimeout;
    private readonly TcpListener? _listener;
    private int _nextPeer = 1;
    private int _disposed;

    public int LocalPeer { get; private set; }
    /// <summary>The port the host listens on (useful when it was asked for port 0).</summary>
    public int Port { get; }
    public bool IsHost => _listener != null;
    public event Action<NetMessage>? Received;
    public event Action<int>? PeerJoined;
    public event Action<int>? PeerLeft;

    private TcpTransport(TcpListener? listener, int port, TimeSpan silenceTimeout)
    {
        _listener = listener;
        Port = port;
        _silenceTimeout = silenceTimeout;
    }

    /// <summary>How long a link may stay silent before it counts as lost, unless the caller says otherwise. The links
    /// run on the thread pool, so only a peer that is gone, hung or starved of every thread goes this quiet.</summary>
    public static readonly TimeSpan DefaultSilenceTimeout = TimeSpan.FromSeconds(20);
    /// <summary>How long joining waits for the host, unless the caller says otherwise.</summary>
    public static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(20);

    /// <summary>Listen for clients as peer 0.</summary>
    /// <param name="silenceTimeout">How long a client may send nothing before it counts as lost.</param>
    public static TcpTransport Host(int port, TimeSpan? silenceTimeout = null)
    {
        var listener = new TcpListener(IPAddress.IPv6Any, port);
        listener.Server.DualMode = true;
        listener.Start();
        var transport = new TcpTransport(listener, ((IPEndPoint)listener.LocalEndpoint).Port, silenceTimeout ?? DefaultSilenceTimeout);
        // Off the caller's context: a game thread's resumes continuations only between frames.
        _ = Task.Run(transport.AcceptLoop);
        _ = Task.Run(transport.Watchdog);
        return transport;
    }

    /// <summary>Connect to a host and wait until it assigns this peer an id.</summary>
    /// <param name="connectTimeout">How long to wait for the connection and the host's welcome.</param>
    /// <param name="silenceTimeout">Once connected, how long the host may send nothing before it counts as lost. It is
    /// separate from <paramref name="connectTimeout"/>: a short wait to connect must not make a running game fragile.</param>
    public static async Task<TcpTransport> JoinAsync(string host, int port, TimeSpan? connectTimeout = null, TimeSpan? silenceTimeout = null,
        CancellationToken cancel = default)
    {
        var client = new TcpClient { NoDelay = true };
        using var connecting = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        connecting.CancelAfter(connectTimeout ?? DefaultConnectTimeout);
        try
        {
            await client.ConnectAsync(host, port, connecting.Token).ConfigureAwait(false);
            var stream = client.GetStream();
            var welcome = await ReadFrame(stream, connecting.Token).ConfigureAwait(false) ?? throw new IOException("The host closed the connection");
            if (welcome.Type != WireType.Welcome) throw new InvalidDataException($"Expected a welcome, got {welcome.Type}");
            var transport = new TcpTransport(null, port, silenceTimeout ?? DefaultSilenceTimeout) { LocalPeer = welcome.Peer };
            transport.Open(0, client);
            _ = Task.Run(transport.Watchdog);
            return transport;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public void Broadcast(NetMessage message) => Route(message with { Peer = LocalPeer, To = NetMessage.Everyone }, from: LocalPeer);

    public void SendTo(int peer, NetMessage message)
    {
        if (peer == LocalPeer) return;
        Route(message with { Peer = LocalPeer, To = peer }, from: LocalPeer);
    }

    public void Poll()
    {
        while (_inbox.TryDequeue(out var action)) action();
    }

    /// <summary>Close every link once it has sent what is queued and its peer has read it, waiting at most
    /// <see cref="CloseTimeout"/>; a link still busy then is cut. Peers see this one leave; it raises no events.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        _listener?.Stop();
        var finishing = _links.Values.Select(link => link.Finish()).ToArray();
        Task.WaitAll(finishing, CloseTimeout);
        _stop.Cancel();
        foreach (var link in _links.Values) link.Close();
        _links.Clear();
    }

    // --- routing ------------------------------------------------------------------------------

    /// <summary>Deliver a message from <paramref name="from"/> where it is going. A client sends everything to the
    /// host; the host keeps what is for it and relays the rest.</summary>
    private void Route(NetMessage message, int from)
    {
        if (!IsHost)
        {
            if (from == LocalPeer) Send(0, new Wire(WireType.Message, LocalPeer, message));
            else Deliver(message);
            return;
        }
        if (from != LocalPeer && (message.To == NetMessage.Everyone || message.To == LocalPeer)) Deliver(message);
        foreach (var (peer, link) in _links)
            if (peer != from && (message.To == NetMessage.Everyone || message.To == peer))
                link.Send(new Wire(WireType.Message, from, message));
    }

    private void Deliver(NetMessage message) => _inbox.Enqueue(() => Received?.Invoke(message));

    private void Send(int peer, Wire wire)
    {
        if (_links.TryGetValue(peer, out var link)) link.Send(wire);
    }

    // --- connections --------------------------------------------------------------------------

    private async Task AcceptLoop()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await _listener!.AcceptTcpClientAsync(_stop.Token); }
            catch (Exception) when (_stop.IsCancellationRequested || _disposed != 0) { return; }
            catch (SocketException) { continue; }
            if (_disposed != 0) { client.Dispose(); return; }
            client.NoDelay = true;
            int peer = Interlocked.Increment(ref _nextPeer) - 1;
            var others = _links.Keys.ToArray();
            var link = Open(peer, client);
            link.Send(new Wire(WireType.Welcome, peer, null));
            foreach (int other in others)
            {
                link.Send(new Wire(WireType.Joined, other, null));
                Send(other, new Wire(WireType.Joined, peer, null));
            }
            _inbox.Enqueue(() => PeerJoined?.Invoke(peer));
        }
    }

    private Link Open(int peer, TcpClient client)
    {
        var link = new Link(this, peer, client);
        _links[peer] = link;
        link.Start();
        return link;
    }

    private void OnFrame(Link link, Wire wire)
    {
        switch (wire.Type)
        {
            case WireType.Message when wire.Message is { } message:
                // The host stamps the true sender, so a client cannot speak for another peer.
                Route(IsHost ? message with { Peer = link.Peer } : message, IsHost ? link.Peer : wire.Peer);
                break;
            case WireType.Joined when !IsHost:
                _inbox.Enqueue(() => PeerJoined?.Invoke(wire.Peer));
                break;
            case WireType.Left when !IsHost:
                _inbox.Enqueue(() => PeerLeft?.Invoke(wire.Peer));
                break;
        }
    }

    private void OnClosed(Link link)
    {
        if (!_links.TryRemove(new KeyValuePair<int, Link>(link.Peer, link))) return;
        if (_disposed != 0) return;
        _inbox.Enqueue(() => PeerLeft?.Invoke(link.Peer));
        if (IsHost) foreach (var other in _links.Values) other.Send(new Wire(WireType.Left, link.Peer, null));
    }

    /// <summary>Send heartbeats on idle links and drop links that have gone silent.</summary>
    private async Task Watchdog()
    {
        using var timer = new PeriodicTimer(HeartbeatInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(_stop.Token))
            {
                long now = Environment.TickCount64;
                foreach (var link in _links.Values)
                {
                    if (now - link.LastReceived > _silenceTimeout.TotalMilliseconds) { link.Close(); continue; }
                    if (now - link.LastSent >= HeartbeatInterval.TotalMilliseconds) link.Send(new Wire(WireType.Ping, LocalPeer, null));
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    // --- wire format --------------------------------------------------------------------------

    private enum WireType { Welcome, Joined, Left, Ping, Message }

    private sealed record Wire(WireType Type, int Peer, NetMessage? Message);

    private static readonly JsonSerializerOptions Json = new() { IncludeFields = false };

    private static byte[] Encode(Wire wire)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(wire, Json);
        var frame = new byte[4 + body.Length];
        BinaryPrimitives.WriteInt32BigEndian(frame, body.Length);
        body.CopyTo(frame, 4);
        return frame;
    }

    /// <summary>Read one frame, or null when the stream ended cleanly between frames.</summary>
    private static async Task<Wire?> ReadFrame(Stream stream, CancellationToken cancel)
    {
        var header = new byte[4];
        if (!await Fill(stream, header, cancel)) return null;
        int length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length < 0 || length > MaxFrameBytes) throw new InvalidDataException($"Frame of {length} bytes");
        var body = new byte[Math.Min(length, ReadChunkBytes)];
        int read = 0;
        while (read < length)
        {
            if (read == body.Length) Array.Resize(ref body, (int)Math.Min(length, 2L * body.Length));
            int n = await stream.ReadAsync(body.AsMemory(read), cancel);
            if (n == 0) throw new EndOfStreamException("Connection closed inside a frame");
            read += n;
        }
        return JsonSerializer.Deserialize<Wire>(body, Json) ?? throw new InvalidDataException("Empty frame");
    }

    private static async Task<bool> Fill(Stream stream, byte[] buffer, CancellationToken cancel)
    {
        int read = 0;
        while (read < buffer.Length)
        {
            int n = await stream.ReadAsync(buffer.AsMemory(read), cancel);
            if (n == 0)
            {
                if (read == 0) return false;
                throw new EndOfStreamException("Connection closed inside a frame");
            }
            read += n;
        }
        return true;
    }

    /// <summary>One TCP connection: a reader task, a writer task fed by a queue bounded by <see cref="MaxQueuedBytes"/>,
    /// and liveness stamps.</summary>
    private sealed class Link(TcpTransport owner, int peer, TcpClient client)
    {
        private readonly Channel<byte[]> _outbox = Channel.CreateUnbounded<byte[]>(new() { SingleReader = true });
        private readonly CancellationTokenSource _closed = CancellationTokenSource.CreateLinkedTokenSource(owner._stop.Token);
        private readonly TaskCompletionSource _ended = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _closing, _finishing;
        private long _queued;

        public int Peer => peer;
        public long LastReceived = Environment.TickCount64;
        public long LastSent = Environment.TickCount64;

        public void Start()
        {
            _ = Task.Run(ReadLoop);
            _ = Task.Run(WriteLoop);
        }

        public void Send(Wire wire)
        {
            if (_closing != 0 || _finishing != 0) return;
            var frame = Encode(wire);
            // One frame always fits, however large: a whole saved game travels as one.
            long queued = Interlocked.Add(ref _queued, frame.Length);
            if (queued > MaxQueuedBytes && queued != frame.Length) { Close(); return; }
            _outbox.Writer.TryWrite(frame);
        }

        /// <summary>Take no more frames, send those queued, then end this side of the stream. The peer reads every frame
        /// before it sees the end and closes its side, which ends the read loop and the link. Completes once the link
        /// is closed.</summary>
        public Task Finish()
        {
            if (Interlocked.Exchange(ref _finishing, 1) == 0) _outbox.Writer.TryComplete();
            return _ended.Task;
        }

        /// <summary>Cut the connection at once, dropping anything still queued.</summary>
        public void Close()
        {
            if (Interlocked.Exchange(ref _closing, 1) == 1) return;
            _outbox.Writer.TryComplete();
            _closed.Cancel();
            client.Dispose();
            owner.OnClosed(this);
            _ended.TrySetResult();
        }

        private async Task ReadLoop()
        {
            try
            {
                var stream = client.GetStream();
                while (await ReadFrame(stream, _closed.Token) is { } wire)
                {
                    Interlocked.Exchange(ref LastReceived, Environment.TickCount64);
                    owner.OnFrame(this, wire);
                }
            }
            catch (Exception e) when (e is IOException or SocketException or OperationCanceledException or ObjectDisposedException
                or InvalidDataException or JsonException) { }
            finally { Close(); }
        }

        private async Task WriteLoop()
        {
            try
            {
                var stream = client.GetStream();
                await foreach (var frame in _outbox.Reader.ReadAllAsync(_closed.Token))
                {
                    await stream.WriteAsync(frame, _closed.Token);
                    Interlocked.Add(ref _queued, -frame.Length);
                    Interlocked.Exchange(ref LastSent, Environment.TickCount64);
                }
                // The queue ran dry because the link is finishing: end our side, and leave the read loop running to
                // take what the peer still sends until it closes. Closing now, with its frames unread, could reset
                // the connection and lose ours.
                if (_finishing != 0 && _closing == 0)
                {
                    client.Client.Shutdown(SocketShutdown.Send);
                    return;
                }
            }
            catch (Exception e) when (e is IOException or SocketException or OperationCanceledException or ObjectDisposedException) { }
            Close();
        }
    }
}
