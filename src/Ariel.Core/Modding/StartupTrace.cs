using System.Diagnostics;

namespace Ariel.Modding;

/// <summary>Opt-in startup phase timings on stderr. Set ARIEL_TRACE_STARTUP=1 before launching the host.</summary>
public readonly struct StartupTrace : IDisposable
{
    private static readonly bool Enabled = Environment.GetEnvironmentVariable("ARIEL_TRACE_STARTUP") == "1";
    private readonly string? _phase;
    private readonly long _start;

    private StartupTrace(string phase)
    {
        _phase = phase;
        _start = Stopwatch.GetTimestamp();
    }

    /// <summary>Measure a phase, or return a no-op scope when tracing is disabled.</summary>
    public static StartupTrace Phase(string name) => Enabled ? new(name) : default;

    /// <summary>Write the completed phase without changing simulation state.</summary>
    public void Dispose()
    {
        if (_phase is not null)
            Console.Error.WriteLine($"[startup] {_phase}: {Stopwatch.GetElapsedTime(_start).TotalMilliseconds:F2} ms");
    }
}
