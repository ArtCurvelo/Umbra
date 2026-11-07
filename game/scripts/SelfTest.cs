using Godot;
using Umbra.Domain.Gameplay;

namespace Umbra.Game;

/// <summary>
/// Integration test of the Godot layer (run headless with --selftest; exit code 0 = passed). One simulation tick per
/// frame through the real Main/views/HUD: the autopilot clears the crypt; then simulated gamepad presses open the pause
/// menu, move the focus with the D-pad, cycle the quality and the camera with A, and close it with Start; then every
/// preset and both cameras are applied while frames keep running.
/// </summary>
public sealed class SelfTest
{
    private readonly Main _main;
    private readonly List<string> _failures = new(), _log = new();
    private int _phase, _wait, _frame, _settle;
    private readonly Queue<Action> _script = new();

    public SelfTest(Main main) { _main = main; }

    private void Check(bool ok, string what)
    {
        _log.Add((ok ? "ok   " : "FALHA ") + what);
        if (!ok) _failures.Add(what);
    }

    private static void Press(JoyButton button)
    {
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = true, Device = 0 });
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = false, Device = 0 });
    }

    public void Frame()
    {
        _frame++;
        var sim = _main.Sim;
        switch (_phase)
        {
            case 0:                                         // fight: autopilot, one tick per frame, real views
                if (!_main.Pause.IsOpen) _main.StepOnce(Autopilot.Next(sim));
                _main.Present(1f, 1 / 60.0);
                bool over = (sim.Cleared && sim.Drops.Count == 0) || !sim.Hero.Alive || sim.Tick > 120 * 60;
                if (over && _settle < 3) { _settle++; break; }           // let the HUD process the last tick first
                if (over)
                {
                    Check(sim.Hero.Alive, $"herói vivo ao fim (vida {sim.Hero.Health}/{sim.Hero.Stats.MaxHealth})");
                    Check(sim.Cleared, $"cripta limpa ({sim.EnemiesRemaining} restantes, {sim.Tick} ticks = {sim.Tick / 60f:0.0} s)");
                    Check(sim.Inventory.Count > 0, $"itens recolhidos: {string.Join(", ", sim.Inventory.Select(i => $"[{i.Tag}] {i.Name}"))}");
                    Check(_main.Hud.ObjectiveText == "Cripta limpa.", $"HUD mostra o objetivo cumprido (\"{_main.Hud.ObjectiveText}\")");
                    Check(_main.Hud.Banner == "clear", "faixa CRIPTA LIMPA");
                    _phase = 1;
                    Enqueue();
                }
                break;
            case 1:                                         // pause menu with a simulated gamepad, a few frames per step
                if (--_wait > 0) break;
                if (_script.Count == 0) { _phase = 2; break; }
                _script.Dequeue()();
                _wait = 4;
                break;
            case 2:                                         // every preset and both cameras keep drawing
                foreach (var preset in Quality.All)
                {
                    _main.SetPreset(preset);
                    for (int i = 0; i < 3; i++) _main.Present(1f, 1 / 60.0);
                    Check(_main.GetViewport().Scaling3DScale == preset.Scale3D && _main.World.ActiveLights <= preset.MaxLights,
                          $"preset {preset.Name}: escala {preset.Scale3D}, luzes ativas {_main.World.ActiveLights} (máx. {preset.MaxLights})");
                }
                _main.Rig.SetMode(true);
                for (int i = 0; i < 60; i++) _main.Present(1f, 1 / 60.0);
                Check(_main.Rig.Camera.GlobalRotationDegrees.X < -80f, $"top-down olha para baixo ({_main.Rig.Camera.GlobalRotationDegrees.X:0}°)");
                _main.Rig.SetMode(false);
                for (int i = 0; i < 60; i++) _main.Present(1f, 1 / 60.0);
                Check(_main.Rig.Camera.GlobalRotationDegrees.X is < -45f and > -60f, $"isométrica a ~52° ({_main.Rig.Camera.GlobalRotationDegrees.X:0}°)");
                Finish();
                _phase = 3;
                break;
        }
    }

    private void Enqueue()
    {
        var menu = _main.Pause;
        string startQuality = "";
        bool startTop = false;
        _script.Enqueue(() => Press(JoyButton.Start));
        _script.Enqueue(() => { Check(menu.IsOpen && _main.GetTree().Paused, "Start abre a pausa e pausa o mundo"); Check(menu.Buttons[0].HasFocus(), "foco no primeiro botão"); });
        _script.Enqueue(() => Press(JoyButton.DpadDown));
        _script.Enqueue(() => Press(JoyButton.DpadDown));
        _script.Enqueue(() => { Check(menu.Buttons[2].HasFocus(), "dois toques no direcional = dois passos (Qualidade)"); startQuality = _main.Preset.Id; Press(JoyButton.A); });
        _script.Enqueue(() => Check(_main.Preset.Id != startQuality && menu.Buttons[2].Text.Contains(_main.Preset.Name), $"A troca a qualidade ({startQuality} -> {_main.Preset.Id})"));
        _script.Enqueue(() => { Press(JoyButton.DpadUp); });
        _script.Enqueue(() => { Check(menu.Buttons[1].HasFocus(), "direcional para cima volta para Câmera"); startTop = _main.Rig.TopDown; Press(JoyButton.A); });
        _script.Enqueue(() => Check(_main.Rig.TopDown != startTop, "A alterna a câmera (isométrica <-> top-down)"));
        _script.Enqueue(() => { Check(!_main.CanProcess(), "mundo parado durante a pausa (Main não processa)"); Press(JoyButton.Start); });
        _script.Enqueue(() => Check(!menu.IsOpen && !_main.GetTree().Paused, "Start fecha a pausa"));
        _script.Enqueue(() => { Press(JoyButton.Start); });
        _script.Enqueue(() => { Check(menu.IsOpen, "abre de novo"); Press(JoyButton.B); });
        _script.Enqueue(() => Check(!menu.IsOpen, "B fecha a pausa"));
        _script.Enqueue(() => _main.Rig.SetMode(false));
    }

    private void Finish()
    {
        foreach (var line in _log) GD.Print(line);
        GD.Print(_failures.Count == 0 ? $"SELFTEST OK ({_log.Count} verificações, {_frame} quadros)" : $"SELFTEST FALHOU: {_failures.Count} de {_log.Count}");
        _main.GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }
}

/// <summary>Runs the self-test script even while the tree is paused (the pause menu steps need it).</summary>
public partial class SelfTestDriver : Node
{
    public SelfTest? Test;
    public override void _Ready() => ProcessMode = ProcessModeEnum.Always;
    public override void _Process(double delta) => Test?.Frame();
}
