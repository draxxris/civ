using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ariel.Content;

namespace Ariel.Game;

/// <summary>An open write window (<see cref="Simulation.OpenWriteWindow"/>); disposing it closes the window.</summary>
public readonly struct WriteWindow : IDisposable
{
    private readonly Simulation? _sim;
    private readonly int _depth;
    internal WriteWindow(Simulation sim, int depth) { _sim = sim; _depth = depth; }
    /// <summary>Close the window: what it declared counts as written from now on.</summary>
    public void Dispose() => _sim?.CloseWriteWindow(_depth);
}

/// <summary>A span of kernel work (<see cref="Simulation.EnterKernel"/>); disposing it leaves it.</summary>
public readonly struct KernelEntry : IDisposable
{
    private readonly Simulation? _sim;
    internal KernelEntry(Simulation sim) => _sim = sim;
    /// <summary>Leave the kernel span; leaving the outermost one forgets every derived answer kept across writes.</summary>
    public void Dispose() => _sim?.LeaveKernel();
}

/// <summary>Write windows and write stamps: the kernel's record of what may have changed, for answers kept across
/// writes (the per-source modifier cache, <see cref="ModifierSink.Collect"/>).
/// <para>Every write the rules make happens inside a window the kernel opens: a command's application
/// (<see cref="TryApply"/>), one turn system's run, and the kernel's own bookkeeping (the turn rollover, the turn
/// order, ended turns, wholesale state replacement). A window declares the state domains it writes
/// (<see cref="StateDomain"/>): a command through <see cref="GameCommand.WriteDomains"/>, a turn system through
/// <see cref="ITurnSystem.WriteDomains"/>. A window that declares nothing may write anything, so it counts as writing
/// every domain and the state's remainder. Closing a window stamps what it declared as written. While it is open,
/// readers that depend on what it declares neither use nor keep remembered answers, because its writes are under
/// way. A window widens to everything when an event that some subscriber handles is published inside it, since a
/// subscriber may write anything, and a nested window adds what it declared to the window around it.</para>
/// <para>Answers are kept across writes only inside kernel work (<see cref="EnterKernel"/>: commands, the turn
/// rollover, AI turns): a host may edit the state directly between its calls, so entering and leaving the outermost
/// kernel span counts as writing everything.</para></summary>
public sealed partial class Simulation
{
    // A window's own writes (Mask, Rest) may happen at any moment while it is open; what its nested windows wrote
    // (Nested, NestedRest) is finished and stamped when they close, and is kept only to verify the window.
    internal sealed class WindowFrame
    {
        public ulong Mask, Nested;
        public bool Rest, NestedRest, Unchecked;
        public string Writer = "";
        public JsonNode? Before;
    }

    private readonly List<WindowFrame> _windows = new();
    private readonly Stack<WindowFrame> _spareFrames = new();
    private ulong _openMask;
    private bool _openRest;
    private long _writeStamp = 1, _restStamp = 1;
    private readonly long[] _domainStamps = new long[StateDomainRegistry.Capacity];
    private int _kernelDepth;

    /// <summary>When set, every window that declared its writes is checked, by hashing the state without its declared domains before and
    /// after, to have written nothing else. A difference throws. Very slow: for tests and verification runs. Not
    /// saved.</summary>
    public bool VerifyWrites { get; set; }

    /// <summary>The modifier contributions remembered across writes for this simulation (<see cref="ModifierSink"/>).</summary>
    internal ModifierCache ModifierCache { get; } = new();

    /// <summary>Whether answers may be kept across writes now: inside kernel work, where every write is in a window.</summary>
    internal bool KeepsAnswers => _kernelDepth > 0;

    /// <summary>The current write stamp. A reader that remembers an answer records it, and the answer stays good while
    /// <see cref="WrittenSince"/> says nothing it depends on was written after it.</summary>
    internal long WriteStamp => _writeStamp;

    /// <summary>Whether anything outside the domains in <paramref name="unread"/> was written after
    /// <paramref name="stamp"/>.</summary>
    internal bool WrittenSince(ulong unread, long stamp)
    {
        if (_restStamp > stamp) return true;
        ulong read = Content.StateDomains.AllMask & ~unread;
        while (read != 0)
        {
            int bit = BitOperations.TrailingZeroCount(read);
            if (_domainStamps[bit] > stamp) return true;
            read &= read - 1;
        }
        return false;
    }

    /// <summary>Whether an open window may be writing something outside the domains in <paramref name="unread"/>.</summary>
    internal bool WritingNow(ulong unread) => _windows.Count > 0 && (_openRest || (_openMask & ~unread) != 0);

    /// <summary>Open a write window that declares the domains it writes (null: anything). The kernel opens these
    /// around commands and turn systems; a rule opens one only for kernel work of its own.</summary>
    internal WriteWindow OpenWriteWindow(IReadOnlyList<string>? domains, string writer)
    {
        var frame = _spareFrames.Count > 0 ? _spareFrames.Pop() : new WindowFrame();
        frame.Writer = writer;
        frame.Unchecked = frame.NestedRest = false;
        frame.Nested = 0;
        frame.Before = null;
        if (domains is null) { frame.Mask = 0; frame.Rest = true; }
        else { frame.Mask = Content.StateDomains.Mask(domains, out bool unknown); frame.Rest = unknown; }
        _windows.Add(frame);
        _openMask |= frame.Mask;
        _openRest |= frame.Rest;
        if (VerifyWrites && !frame.Rest) frame.Before = JsonSerializer.SerializeToNode(State, CanonicalJson.Options);
        return new WriteWindow(this, _windows.Count);
    }

    internal void CloseWriteWindow(int depth)
    {
        if (_windows.Count != depth) throw new InvalidOperationException("Write windows must close in order");
        var frame = _windows[^1];
        _windows.RemoveAt(_windows.Count - 1);
        var before = frame.Before;
        frame.Before = null;
        Stamp(frame.Mask, frame.Rest);
        if (_windows.Count > 0)
        {
            var outer = _windows[^1];
            outer.Nested |= frame.Mask | frame.Nested;
            outer.NestedRest |= frame.Rest | frame.NestedRest;
            outer.Unchecked |= frame.Unchecked;
        }
        _openMask = 0; _openRest = false;
        foreach (var open in _windows) { _openMask |= open.Mask; _openRest |= open.Rest; }
        bool check = before is not null && !frame.Rest && !frame.NestedRest && !frame.Unchecked;
        ulong written = frame.Mask | frame.Nested;
        string writer = frame.Writer;
        _spareFrames.Push(frame);
        if (!check) return;
        // Verification: the state without the domains the window wrote must be what it was when the window opened.
        var after = JsonSerializer.SerializeToNode(State, CanonicalJson.Options)!;
        Content.StateDomains.Strip(before!, written);
        Content.StateDomains.Strip(after, written);
        if (CanonicalJson.Fnv1a64(before) != CanonicalJson.Fnv1a64(after))
            throw new InvalidOperationException($"{writer} wrote outside the state domains it declares");
    }

    /// <summary>The innermost open window may write anything from here on: an event a subscriber handles was
    /// published inside it.</summary>
    internal void WidenWriteWindow()
    {
        if (_windows.Count == 0) return;
        _windows[^1].Rest = true;
        _openRest = true;
    }

    /// <summary>The kernel wrote outside any window (ended turns, the turn order): everything counts as written.</summary>
    internal void NoteKernelWrite() => Stamp(0, rest: true);

    /// <summary>The state was replaced wholesale: forget every remembered answer, whose keys name the old entities.</summary>
    internal void NoteStateReplaced()
    {
        NoteKernelWrite();
        ModifierCache.Clear();
    }

    private void Stamp(ulong mask, bool rest)
    {
        long stamp = ++_writeStamp;
        if (rest) { _restStamp = stamp; return; }
        while (mask != 0)
        {
            int bit = BitOperations.TrailingZeroCount(mask);
            _domainStamps[bit] = stamp;
            mask &= mask - 1;
        }
    }

    /// <summary>Kernel work that runs whole turns (the rollover, AI turns) inside a command's window: the windows
    /// around it are set aside while it runs, since its own commands and systems open theirs, and everything counts as
    /// written on both sides of it.</summary>
    internal SuspendedWindows SuspendWriteWindows()
    {
        if (_windows.Count == 0) return default;
        var saved = _windows.ToArray();
        foreach (var frame in saved) frame.Unchecked = true;
        _windows.Clear();
        _openMask = 0; _openRest = false;
        NoteKernelWrite();
        return new SuspendedWindows(this, saved);
    }

    internal readonly struct SuspendedWindows : IDisposable
    {
        private readonly Simulation? _sim;
        private readonly WindowFrame[]? _saved;
        internal SuspendedWindows(Simulation sim, WindowFrame[] saved) { _sim = sim; _saved = saved; }
        public void Dispose()
        {
            if (_sim is null) return;
            _sim.NoteKernelWrite();
            _sim._windows.AddRange(_saved!);
            foreach (var open in _saved!) { _sim._openMask |= open.Mask; _sim._openRest |= open.Rest; }
        }
    }

    /// <summary>Enter kernel work. The outermost entry counts everything as written, since the host may have edited
    /// the state since the last, and so does leaving it, which also forgets what was remembered.</summary>
    internal KernelEntry EnterKernel()
    {
        if (_kernelDepth++ == 0) NoteKernelWrite();
        return new KernelEntry(this);
    }

    internal void LeaveKernel()
    {
        if (--_kernelDepth > 0) return;
        NoteKernelWrite();
        ModifierCache.Clear();
    }
}
