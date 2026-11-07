using System.Text.Json;
using Godot;

namespace Umbra.Game;

/// <summary>
/// Reproducible benchmark: fixed seed, autopilot fighting through the crypt, 1280x720 window, VSync off. After a
/// warm-up it records every frame for a fixed time and writes JSON: hardware, renderer, preset, mean FPS, 1% low,
/// frame-time percentiles, CPU process time, GPU time (when the driver reports it), draw calls and objects.
/// Only measured numbers; nothing is estimated.
/// </summary>
public sealed class Bench
{
    private readonly Main _main;
    private readonly List<double> _frameMs = new();
    private readonly List<double> _processMs = new(), _gpuMs = new(), _cpuRenderMs = new();
    private readonly List<double> _drawCalls = new(), _objects = new(), _primitives = new();
    private double _elapsed;
    private bool _done;
    public int Restarts;
    public readonly bool Measure;
    private readonly Dictionary<string, List<double>> _sections = new();

    public Bench(Main main)
    {
        _main = main;
        Measure = !LaunchOptions.Skip.Contains("measure");
        RenderingServer.ViewportSetMeasureRenderTime(main.GetViewport().GetViewportRid(), Measure);
    }

    public void Frame(double delta)
    {
        if (_done) return;
        _elapsed += delta;
        var o = _main.Options;
        if (_elapsed < o.BenchWarmup) { Profiler.Frame.Clear(); return; }
        foreach (var (k, v) in Profiler.Frame) { if (!_sections.ContainsKey(k)) _sections[k] = new(); _sections[k].Add(v); }
        Profiler.Frame.Clear();
        var rid = _main.GetViewport().GetViewportRid();
        _frameMs.Add(delta * 1000);
        _processMs.Add(Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000);
        _gpuMs.Add(RenderingServer.ViewportGetMeasuredRenderTimeGpu(rid));
        _cpuRenderMs.Add(RenderingServer.ViewportGetMeasuredRenderTimeCpu(rid));
        _drawCalls.Add(Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame));
        _objects.Add(Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame));
        _primitives.Add(Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame));
        if (_elapsed < o.BenchWarmup + o.BenchSeconds) return;
        _done = true;
        Write(o.Bench!);
        _main.GetTree().Quit();
    }

    private void Write(string path)
    {
        var sorted = _frameMs.OrderBy(x => x).ToList();
        int worst = Math.Max(1, sorted.Count / 100);
        double lowMs = sorted.Skip(sorted.Count - worst).Average();
        double Pct(double p) => sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * p))];
        double? gpu = _gpuMs.Average() > 0 ? Math.Round(_gpuMs.Average(), 3) : null;
        var size = DisplayServer.WindowGetSize();
        var report = new Dictionary<string, object?>
        {
            ["cena"] = $"cripta seed {_main.Options.Seed}, piloto automático, câmera {(_main.Rig.TopDown ? "top-down" : "isométrica")}",
            ["cpu"] = OS.GetProcessorName(),
            ["gpu"] = RenderingServer.GetVideoAdapterName(),
            ["gpu_fabricante"] = RenderingServer.GetVideoAdapterVendor(),
            ["api"] = RenderingServer.GetVideoAdapterApiVersion(),
            ["renderizador"] = RenderingServer.GetCurrentRenderingMethod(),
            ["driver_render"] = RenderingServer.GetCurrentRenderingDriverName(),
            ["resolucao_janela"] = $"{size.X}x{size.Y}",
            ["escala_3d"] = _main.Preset.Scale3D,
            ["preset"] = _main.Preset.Name,
            ["luzes_ativas"] = _main.World.ActiveLights,
            ["vsync"] = DisplayServer.WindowGetVsyncMode().ToString(),
            ["aquecimento_s"] = _main.Options.BenchWarmup,
            ["duracao_s"] = _main.Options.BenchSeconds,
            ["quadros"] = _frameMs.Count,
            ["fps_medio"] = Math.Round(1000.0 / _frameMs.Average(), 1),
            ["fps_1pct_baixo"] = Math.Round(1000.0 / lowMs, 1),
            ["quadro_ms_medio"] = Math.Round(_frameMs.Average(), 2),
            ["quadro_ms_p50"] = Math.Round(Pct(.5), 2),
            ["quadro_ms_p99"] = Math.Round(Pct(.99), 2),
            ["cpu_process_ms_medio"] = Math.Round(_processMs.Average(), 2),
            ["cpu_render_ms_medio"] = Math.Round(_cpuRenderMs.Average(), 2),
            ["gpu_ms_medio"] = gpu,
            ["draw_calls_medio"] = Math.Round(_drawCalls.Average(), 0),
            ["objetos_medio"] = Math.Round(_objects.Average(), 0),
            ["primitivas_medio"] = Math.Round(_primitives.Average(), 0),
            ["memoria_estatica_mb"] = Math.Round(Performance.GetMonitor(Performance.Monitor.MemoryStatic) / 1048576.0, 1),
            ["nosso_codigo_ms_medio"] = _sections.ToDictionary(kv => kv.Key, kv => Math.Round(kv.Value.Sum() / _frameMs.Count, 2)),
            ["lutas_recomecadas"] = Restarts,
            ["inimigos_restantes_no_fim"] = _main.Sim.EnemiesRemaining,
        };
        System.IO.File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        GD.Print($"benchmark: {path} — {report["preset"]}: {report["fps_medio"]} FPS médio, 1% baixo {report["fps_1pct_baixo"]}");
    }
}
