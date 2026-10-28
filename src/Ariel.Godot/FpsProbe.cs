using System.Diagnostics;
using Godot;

namespace Ariel.Client;

/// <summary>Wall-clock frame sampling, excluding loading and 120 warmup frames at each zoom.</summary>
internal sealed class FpsProbe(Action zoomOut)
{
    private const int WarmupFrames = 120, SampleFrames = 300;
    private readonly double[] _seconds = new double[SampleFrames];
    private long _previous = Stopwatch.GetTimestamp();
    private int _frame = -WarmupFrames;
    private bool _wide;

    public bool Tick()
    {
        long now = Stopwatch.GetTimestamp();
        double elapsed = (now - _previous) / (double)Stopwatch.Frequency;
        _previous = now;
        if (_frame < 0) { _frame++; return false; }
        _seconds[_frame++] = elapsed;
        if (_frame < SampleFrames) return false;
        double average = SampleFrames / _seconds.Sum();
        // 1%-low FPS is the reciprocal mean of the slowest 1% of frame times (three frames).
        double low = 1 / _seconds.OrderDescending().Take(SampleFrames / 100).Average();
        GD.Print($"FPS probe {(_wide ? "maximum zoom-out (44)" : "default zoom (18)")}: average={average:F2}, 1%-low={low:F2}; {SampleFrames} frames, {WarmupFrames} warmup frames.");
        if (_wide) return true;
        _wide = true;
        _frame = -WarmupFrames;
        zoomOut();
        return false;
    }
}
