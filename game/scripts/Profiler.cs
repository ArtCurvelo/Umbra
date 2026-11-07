using System.Diagnostics;

namespace Umbra.Game;

/// <summary>Wall-clock time of our own code per frame (simulation, presentation, HUD drawing), for the benchmark.</summary>
public static class Profiler
{
    public static readonly Dictionary<string, double> Frame = new();
    /// <summary>What happened in the frame (event kinds), to explain slow frames in the benchmark.</summary>
    public static readonly HashSet<string> Tags = new();
    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    public static double Now => Clock.Elapsed.TotalMilliseconds;
    public static void Add(string section, double startMs) => Frame[section] = Frame.GetValueOrDefault(section) + (Now - startMs);
}
