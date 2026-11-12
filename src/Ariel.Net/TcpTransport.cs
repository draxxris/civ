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
/// heartbeat, so sends never block the game and a long turn on the game thread never looks like a lost connection.
/// Frames are a 4-byte big-endian length followed by UTF-8 JSON.
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

    private readonly ConcurrentQueue<Action> _inbox = new();
    private readonly ConcurrentDictionary<int, Link> _links = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly TimeSpan _timeout;
    private readonly TcpListener? _listener;
    private int _nextPeer = 1;

    public int LocalPeer { get; private set; }
    /// <summary>The port the host listens on (useful when it was asked for port 0).</summary>
    public int Port { get; }
    public bool IsHost => _listener != null;
    public event Action<NetMessage>? Received;
    public event Action<int>? PeerJoined;
    public event Action<int>? PeerLeft;

    private TcpTransport(TcpListener? listener, int port, TimeSpan timeout)
    {
        _listener = listener;
        Port = port;
        _timeout = timeout;
    }

    /// <summary>Listen for clients as peer 0.</summary>
    public static TcpTransport Host(int port, TimeSpan? timeout = null)
    {
        var listener = new TcpListener(IPAddress.IPv6Any, port);
        listener.Server.DualMode = true;
        listener.Start();
        var transport = new TcpTransport(listener, ((IPEndPoint)listener.LocalEndpoint).Port, timeout ?? TimeSpan.FromSeconds(20));
        _ = transport.AcceptLoop();
        _ = transport.Watchdog();
        return transport;
    }

    /// <summary>Connect to a host and wait until it assigns this peer an id.</summary>
    public static async Task<TcpTransport> JoinAsync(string host, int port, TimeSpan? timeout = null, CancellationToken cancel = default)
    {
        var limit = timeout ?? TimeSpan.FromSeconds(20);
        var client = new TcpClient { NoDelay = true };
        using var connecting = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        connecting.CancelAfter(limit);
        try
        {
            await client.ConnectAsync(host, port, connecting.Token);
            var stream = client.GetStream();
            var welcome = await ReadFrame(stream, connecting.Token) ?? throw new IOException("The host closed the connection");
            if (welcome.Type != WireType.Welcome) throw new InvalidDataException($"Expected a welcome, got {welcome.Type}");
            var transport = new TcpTransport(null, port, limit) { LocalPeer = welcome.Peer };
            transport.Open(0, client);
            _ = transport.Watchdog();
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

    public void Dispose()
    {
        if (_stop.IsCancellationRequested) return;
        _stop.Cancel();
        _listener?.Stop();
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
            catch (Exception) when (_stop.IsCancellationRequested) { return; }
            catch (SocketException) { continue; }
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
        if (_stop.IsCancellationRequested) return;
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
                    if (now - link.LastReceived > _timeout.TotalMilliseconds) { link.Close(); continue; }
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
        private int _closing;
        private long _queued;

        public int Peer => peer;
        public long LastReceived = Environment.TickCount64;
        public long LastSent = Environment.TickCount64;

        public void Start()
        {
            _ = ReadLoop();
            _ = WriteLoop();
        }

        public void Send(Wire wire)
        {
            if (_closing != 0) return;
            var frame = Encode(wire);
            // One frame always fits, however large: a whole saved game travels as one.
            long queued = Interlocked.Add(ref _queued, frame.Length);
            if (queued > MaxQueuedBytes && queued != frame.Length) { Close(); return; }
            _outbox.Writer.TryWrite(frame);
        }

        public void Close()
        {
            if (Interlocked.Exchange(ref _closing, 1) == 1) return;
            _outbox.Writer.TryComplete();
            _closed.Cancel();
            client.Dispose();
            owner.OnClosed(this);
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
            }
            catch (Exception e) when (e is IOException or SocketException or OperationCanceledException or ObjectDisposedException) { }
            finally { Close(); }
        }
    }
}
