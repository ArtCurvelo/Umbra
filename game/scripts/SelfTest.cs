using Godot;
using Umbra.Domain.Gameplay;
using Umbra.Game.Ui;

namespace Umbra.Game;

/// <summary>
/// Integration test of the Godot layer (run headless with --selftest; exit code 0 = passed). One simulation tick per
/// frame through the real Main/views/HUD: the autopilot clears the crypt. Then a simulated gamepad (and a few keys)
/// drives every screen through the real input path: pause, character (equip), skill tree (raise and give back a grade),
/// settings (quality, remapping with a conflict, restore defaults) and the main menu (new game). Finally every preset and
/// both cameras keep drawing. Tool runs never touch the player's settings file.
/// </summary>
public sealed class SelfTest
{
    private readonly Main _main;
    private readonly List<string> _failures = new(), _log = new();
    private int _phase, _wait, _frame, _settle;
    private readonly Queue<Action> _script = new();
    private readonly List<InputEvent> _release = new();

    public SelfTest(Main main) { _main = main; }

    private void Check(bool ok, string what)
    {
        _log.Add((ok ? "ok   " : "FALHA ") + what);
        if (!ok) _failures.Add(what);
    }

    /// <summary>Press now, release at the next script step (Godot's "just pressed" needs the button still down).</summary>
    private void Press(JoyButton button)
    {
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = true, Device = 0 });
        _release.Add(new InputEventJoypadButton { ButtonIndex = button, Pressed = false, Device = 0 });
    }

    private void Key(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = true });
        _release.Add(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = false });
    }

    private Screen? Top => _main.Screens.Top;

    public void Frame()
    {
        _frame++;
        var sim = _main.Sim;
        switch (_phase)
        {
            case 0:                                         // fight: autopilot, one tick per frame, real views
                if (_main.Screens.Count == 0) _main.StepOnce(Autopilot.Next(sim));
                _main.Present(1f, 1 / 60.0);
                bool over = (sim.Cleared && sim.Drops.Count == 0) || !sim.Hero.Alive || sim.Tick > 120 * 60;
                if (over && _settle < 3) { _settle++; break; }
                if (over)
                {
                    Check(sim.Hero.Alive, $"herói vivo ao fim (vida {sim.Hero.Health}/{sim.Hero.MaxHealth})");
                    Check(sim.Cleared, $"cripta limpa ({sim.EnemiesRemaining} restantes, {sim.Tick} ticks = {sim.Tick / 60f:0.0} s)");
                    Check(sim.Inventory.Count > 0, $"itens recolhidos: {string.Join(", ", sim.Inventory.Select(i => $"[{i.Tag}] {i.Name}"))}");
                    Check(_main.Hud.ObjectiveText == "Cripta limpa.", $"HUD mostra o objetivo cumprido (\"{_main.Hud.ObjectiveText}\")");
                    Check(_main.Hud.Banner == "clear", "faixa CRIPTA LIMPA");
                    Check(!Settings.Persistent, "execução de ferramenta não lê nem grava as configurações do jogador");
                    _phase = 1;
                    Enqueue();
                }
                break;
            case 1:                                         // screens, a few frames per step
                if (--_wait > 0) break;
                if (_release.Count > 0)                    // let go, and wait a little before the next press (as a hand does)
                {
                    foreach (var e in _release) Input.ParseInputEvent(e);
                    _release.Clear();
                    _wait = 3;
                    break;
                }
                if (_script.Count == 0) { _phase = 2; break; }
                _script.Dequeue()();
                _wait = 5;
                break;
            case 2:                                         // every preset and both cameras keep drawing
                foreach (var preset in Quality.All)
                {
                    _main.SetPreset(preset);
                    for (int i = 0; i < 3; i++) _main.Present(1f, 1 / 60.0);
                    Check(_main.World.ActiveLights <= preset.MaxLights, $"preset {preset.Name}: luzes ativas {_main.World.ActiveLights} (máx. {preset.MaxLights})");
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
        var hero = _main.Sim.Hero;
        int attackBefore = 0, defenseBefore = 0, healthBefore = 0, bagBefore = 0;
        string presetBefore = "";
        // pause
        _script.Enqueue(() => Press(JoyButton.Start));
        _script.Enqueue(() => { Check(Top is PauseScreen && _main.GetTree().Paused, "Start abre a pausa e pausa o mundo"); Press(JoyButton.DpadDown); });
        _script.Enqueue(() => { Check(Top?.FocusText == "Personagem", $"um toque no direcional = um passo (\"{Top?.FocusText}\")"); Press(JoyButton.A); });
        // character: equip the first bag item
        _script.Enqueue(() =>
        {
            Check(Top is CharacterScreen, "A abre a tela de personagem");
            attackBefore = hero.Attack; defenseBefore = hero.Defense; healthBefore = hero.MaxHealth; bagBefore = _main.Sim.Inventory.Count;
            Press(JoyButton.A);
        });
        _script.Enqueue(() =>
        {
            bool changed = hero.Attack != attackBefore || hero.Defense != defenseBefore || hero.MaxHealth != healthBefore;
            Check(hero.Equipment.All.Any() && changed, $"A equipa o item e muda os atributos (ataque {attackBefore}->{hero.Attack}, defesa {defenseBefore}->{hero.Defense}, vida {healthBefore}->{hero.MaxHealth})");
            Check(_main.Sim.Inventory.Count == bagBefore - 1, "o item sai da mochila");
            Press(JoyButton.B);
        });
        _script.Enqueue(() => { Check(_main.Screens.Count == 0 && !_main.GetTree().Paused, "B fecha a tela e o mundo volta"); Press(JoyButton.RightStick); });
        // skill tree
        _script.Enqueue(() =>
        {
            Check(Top is TreeScreen t && t.FocusedId == SliceData.Ruptura.Id, "R3 abre a árvore com foco na Ruptura");
            Press(JoyButton.A);
        });
        _script.Enqueue(() => { Check(hero.Ranks.Rank(SliceData.Ruptura.Id) == 2 && hero.Ranks.Points == SkillRanks.StartingPoints - 1, "A aprende um grau (ponto gasto)"); Press(JoyButton.X); });
        _script.Enqueue(() => { Check(hero.Ranks.Rank(SliceData.Ruptura.Id) == 1 && hero.Ranks.Points == SkillRanks.StartingPoints, "X devolve o ponto"); Press(JoyButton.RightShoulder); });
        _script.Enqueue(() => { Check(Top is TreeScreen t && t.Zoom > 1f, "RB aproxima a árvore"); Press(JoyButton.DpadUp); });
        _script.Enqueue(() => { Check(Top is TreeScreen t && t.FocusedId != SliceData.Ruptura.Id, $"direcional muda o nó em foco ({(Top as TreeScreen)?.FocusedId})"); Press(JoyButton.B); });
        // settings: quality, remap with a conflict, restore
        _script.Enqueue(() => Press(JoyButton.Start));
        _script.Enqueue(() => Press(JoyButton.DpadDown));
        _script.Enqueue(() => Press(JoyButton.DpadDown));
        _script.Enqueue(() => Press(JoyButton.DpadDown));
        _script.Enqueue(() => { Check(Top?.FocusText == "Configurações", $"foco em Configurações (\"{Top?.FocusText}\")"); Press(JoyButton.A); });
        _script.Enqueue(() => { Check(Top is SettingsScreen && Top.FocusText == "Qualidade", "configurações abrem na aba Vídeo, em Qualidade"); presetBefore = _main.Preset.Id; Press(JoyButton.DpadRight); });
        _script.Enqueue(() => { Check(_main.Preset.Id != presetBefore && Settings.Current.Preset == _main.Preset.Id, $"direita troca a qualidade e aplica ({presetBefore} -> {_main.Preset.Id})"); Press(JoyButton.RightShoulder); });
        _script.Enqueue(() => Press(JoyButton.RightShoulder));
        _script.Enqueue(() => { Check(Top?.FocusText == "Andar para cima", $"RB duas vezes = aba Controles (\"{Top?.FocusText}\")"); Press(JoyButton.DpadLeft); });
        _script.Enqueue(() => Press(JoyButton.A));
        _script.Enqueue(() => { Check(Top is SettingsScreen s && s.CapturesRaw, "A espera uma tecla nova"); Key(Godot.Key.F); });
        _script.Enqueue(() =>
        {
            Check(InputMap.ActionGetEvents("move_up").Any(e => e is InputEventKey { PhysicalKeycode: Godot.Key.F }) &&
                  !InputMap.ActionGetEvents("move_up").Any(e => e is InputEventKey { PhysicalKeycode: Godot.Key.W }), "F substitui W em Andar para cima");
            Press(JoyButton.DpadDown);
        });
        for (int i = 0; i < 3; i++) _script.Enqueue(() => Press(JoyButton.DpadDown));
        _script.Enqueue(() => { Check(Top?.FocusText == "Golpe Brutal", $"foco em Golpe Brutal (\"{Top?.FocusText}\")"); Press(JoyButton.A); });
        _script.Enqueue(() => Key(Godot.Key.Q));
        _script.Enqueue(() => Press(JoyButton.A));                          // confirm the swap
        _script.Enqueue(() =>
        {
            Check(InputMap.ActionGetEvents("attack").Any(e => e is InputEventKey { PhysicalKeycode: Godot.Key.Q }) &&
                  InputMap.ActionGetEvents("skill_1").Any(e => e is InputEventKey { PhysicalKeycode: Godot.Key.Space }), "conflito: Q vai para Golpe Brutal e Espaço para Ruptura");
            Press(JoyButton.Y);
        });
        _script.Enqueue(() =>
        {
            Check(InputMap.ActionGetEvents("move_up").Any(e => e is InputEventKey { PhysicalKeycode: Godot.Key.W }) &&
                  InputMap.ActionGetEvents("attack").Any(e => e is InputEventKey { PhysicalKeycode: Godot.Key.Space }), "Y restaura os controles padrão");
            Press(JoyButton.B);
        });
        _script.Enqueue(() => { Check(Top is PauseScreen, "B volta para a pausa"); Press(JoyButton.B); });
        // main menu
        _script.Enqueue(() => { Check(_main.Screens.Count == 0, "B fecha a pausa"); _main.Screens.Push(new MainMenuScreen(_main)); });
        _script.Enqueue(() => { Check(Top?.FocusText == "Continuar", "menu principal com foco em Continuar (partida em andamento)"); Press(JoyButton.DpadDown); });
        _script.Enqueue(() => Press(JoyButton.A));
        _script.Enqueue(() =>
        {
            var s = _main.Sim;
            Check(_main.Screens.Count == 0 && s.EnemiesRemaining == s.Enemies.Count && s.Inventory.Count == 0, "Novo jogo começa uma cripta nova");
        });
    }

    private void Finish()
    {
        foreach (var line in _log) GD.Print(line);
        GD.Print(_failures.Count == 0 ? $"SELFTEST OK ({_log.Count} verificações, {_frame} quadros)" : $"SELFTEST FALHOU: {_failures.Count} de {_log.Count}");
        _main.GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }
}

/// <summary>Runs the self-test script even while the tree is paused (the screens need it).</summary>
public partial class SelfTestDriver : Node
{
    public SelfTest? Test;
    public override void _Ready() => ProcessMode = ProcessModeEnum.Always;
    public override void _Process(double delta) => Test?.Frame();
}
