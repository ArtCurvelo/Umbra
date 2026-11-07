using Godot;

namespace Umbra.Game;

/// <summary>
/// Command line after "--" (Godot passes user args through OS.GetCmdlineUserArgs):
///   --seed N            crypt seed (default 7)
///   --preset baixo|medio|alto
///   --camera iso|top    --yaw N (45° steps)   --zoom P (0..100, % of the zoom range)
///   --autopilot         the scripted player drives (also on for --bench and --selftest)
///   --ticks N           fast-forward the simulation N ticks with the autopilot before the first frame
///   --shot FILE --frames N   save a screenshot after N frames and quit
///   --bench FILE --seconds S --warmup W   benchmark: frame statistics as JSON, then quit
///   --freeze            no simulation steps after the fast-forward (same instant for preset comparisons)
///   --scale X --lights N --nohud   diagnostic overrides for the benchmark
///   --fx golpe|ruptura|investida|impacto   play that effect (visual only) in frame 2, for screenshots
///   --selftest          headless integration test; exit code 0 = passed
/// </summary>
public sealed class LaunchOptions
{
    public ulong Seed = 7;
    public string Preset = "medio";
    public string Camera = "iso";
    public int Yaw;
    public float? Zoom;
    public bool Autopilot;
    public int FastForward;
    public string? Shot;
    public int ShotFrames = 120;
    public string? Bench;
    public double BenchSeconds = 20, BenchWarmup = 3;
    public bool SelfTest;
    public bool Freeze;
    public string? Fx;
    public static HashSet<string> Skip = new();
    public float? Scale; public int? Lights; public bool NoHud, NoNormals;

    public static LaunchOptions Parse(string[] args)
    {
        var o = new LaunchOptions();
        for (int i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : "";
            switch (args[i])
            {
                case "--seed": o.Seed = ulong.Parse(Next()); break;
                case "--preset": o.Preset = Next(); break;
                case "--camera": o.Camera = Next(); break;
                case "--yaw": o.Yaw = int.Parse(Next()); break;
                case "--zoom": o.Zoom = float.Parse(Next(), System.Globalization.CultureInfo.InvariantCulture); break;
                case "--autopilot": o.Autopilot = true; break;
                case "--ticks": o.FastForward = int.Parse(Next()); break;
                case "--shot": o.Shot = Next(); break;
                case "--frames": o.ShotFrames = int.Parse(Next()); break;
                case "--bench": o.Bench = Next(); o.Autopilot = true; break;
                case "--seconds": o.BenchSeconds = double.Parse(Next(), System.Globalization.CultureInfo.InvariantCulture); break;
                case "--warmup": o.BenchWarmup = double.Parse(Next(), System.Globalization.CultureInfo.InvariantCulture); break;
                case "--scale": o.Scale = float.Parse(Next(), System.Globalization.CultureInfo.InvariantCulture); break;
                case "--lights": o.Lights = int.Parse(Next()); break;
                case "--nohud": o.NoHud = true; break;
                case "--skip": foreach (var s in Next().Split(',')) Skip.Add(s); break;
                case "--fx": o.Fx = Next(); break;
                case "--freeze": o.Freeze = true; break;
                case "--selftest": o.SelfTest = true; o.Autopilot = true; break;
                default: GD.PushWarning($"Argumento desconhecido: {args[i]}"); break;
            }
        }
        return o;
    }
}
