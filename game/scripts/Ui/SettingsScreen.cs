using Godot;

namespace Umbra.Game.Ui;

/// <summary>
/// Configurações (approved mockups): five tabs, values on the right, a panel explaining the focused option with its
/// measured cost. Every change applies at once and is saved. Controls: remapping per column (keyboard/mouse, gamepad),
/// with a confirmation when the new binding is already used by another action.
/// </summary>
public partial class SettingsScreen : Screen
{
    private sealed record Opt(string Label, string Info, string Kind, Func<string> Value, Action<int>? Change = null, Action? Accept = null,
                              Func<int>? Percent = null, string? Cost = null, int CostLevel = 0);

    private static readonly string[] TabNames = { "Vídeo", "Áudio", "Controles", "Acessibilidade", "Jogo" };
    private readonly Main _main;
    private int _tab, _focus, _column;          // column: 0 = keyboard/mouse, 1 = gamepad (Controls tab)
    private string? _capture;                    // action waiting for a new key/button
    private (string Action, InputEvent Event, string Other)? _conflict;
    private string _message = "";

    public SettingsScreen(Main main, int tab = 0) { _main = main; _tab = tab; }
    private static Settings S => Settings.Current;
    public override string FocusText => _tab == 2 && _focus < Bindings.Remappable.Length ? Bindings.Remappable[_focus].Label : Options()[_focus].Label;
    public override bool CapturesRaw => _capture != null;

    private static string OnOff(bool v) => v ? "LIGADO" : "DESLIGADO";
    private static int Cycle(int index, int count, int dir) => ((index + dir) % count + count) % count;

    private void Changed(bool preset = true)
    {
        _main.ApplySettings(preset);
        S.Save();
    }

    private List<Opt> Options() => _tab switch
    {
        0 => new List<Opt>
        {
            new("Qualidade", "Ajusta de uma vez sombras, luzes, névoa, partículas e pós-processamento. Mudar qualquer um deles torna a qualidade Personalizada.",
                "choice", () => S.QualityName, d => { var ids = Quality.All.Select(p => p.Id).ToList(); S.UsePreset(ids[Cycle(Math.Max(0, ids.IndexOf(S.Preset)), ids.Count, d)]); Changed(); },
                Cost: "Medido no ThinkPad T440: Baixo 47 FPS, Médio 40, Alto 22."),
            new("Modo de tela", "Janela, tela cheia sem bordas ou tela cheia exclusiva.", "choice",
                () => S.WindowMode switch { "tela_cheia" => "Tela cheia", "exclusiva" => "Exclusiva", _ => "Janela" },
                d => { var m = new[] { "janela", "tela_cheia", "exclusiva" }; S.WindowMode = m[Cycle(Array.IndexOf(m, S.WindowMode), 3, d)]; Changed(false); }),
            new("Resolução", "Tamanho da janela (no modo janela).", "choice", () => S.Resolution.Replace("x", " x "),
                d => { S.Resolution = Settings.Resolutions[Cycle(Array.IndexOf(Settings.Resolutions, S.Resolution), Settings.Resolutions.Length, d)]; Changed(false); }),
            new("VSync", "Sincroniza os quadros com o monitor: evita cortes na imagem.", "toggle", () => OnOff(S.VSync), _ => { S.VSync = !S.VSync; Changed(false); }),
            new("Limite de quadros", "Teto de quadros por segundo. O combate roda sempre a 60 passos por segundo.", "choice",
                () => S.FpsLimit == 0 ? "Sem limite" : S.FpsLimit.ToString(),
                d => { S.FpsLimit = Settings.FpsChoices[Cycle(Array.IndexOf(Settings.FpsChoices, S.FpsLimit), Settings.FpsChoices.Length, d)]; Changed(false); }),
            new("Escala da interface", "Tamanho do HUD e dos menus. O texto continua nítido em qualquer escala.", "choice", () => S.UiScale + "%",
                d => { S.UiScale = Settings.UiScales[Cycle(Array.IndexOf(Settings.UiScales, S.UiScale), Settings.UiScales.Length, d)]; Changed(false); }),
            new("Sombras dos personagens", "Os personagens projetam sombra da luz da lua. Dá profundidade à cena, mas é o recurso visual mais caro.",
                "toggle", () => OnOff(S.CharacterShadows), _ => { S.CharacterShadows = !S.CharacterShadows; Changed(); },
                Cost: "Medido no T440 (Médio): cerca de 7 FPS a menos.", CostLevel: 4),
            new("Luzes simultâneas", "Quantas tochas próximas iluminam a cena ao mesmo tempo.", "choice", () => S.Lights.ToString(),
                d => { S.Lights = Settings.LightChoices[Cycle(Array.IndexOf(Settings.LightChoices, S.Lights), Settings.LightChoices.Length, d)]; Changed(); }, CostLevel: 2),
            new("Névoa", "Névoa de profundidade na cripta.", "toggle", () => OnOff(S.Fog), _ => { S.Fog = !S.Fog; Changed(); }, CostLevel: 1),
            new("Partículas", "Densidade de brasas, poeira e faíscas. Não muda dano, alvos nem alcance.", "slider", () => S.Particles + "%",
                d => { S.Particles = Math.Clamp(S.Particles + d * 10, 0, 100); Changed(); }, Percent: () => S.Particles, CostLevel: 1),
            new("Pós-processamento", "Gradação de cor, vinheta e grão. O HUD não é afetado.", "toggle", () => OnOff(S.Post), _ => { S.Post = !S.Post; Changed(); },
                Cost: "Medido no T440 (Médio): cerca de 4 FPS a menos.", CostLevel: 3),
            new("Brilho", "Exposição da cena. 50% é o valor aprovado na Fase 3.", "slider", () => S.Brightness + "%",
                d => { S.Brightness = Math.Clamp(S.Brightness + d * 5, 0, 100); Changed(false); }, Percent: () => S.Brightness),
        },
        1 => new List<Opt>
        {
            new("Volume geral", "Volume de tudo. (A slice ainda não tem som; o valor fica salvo para quando tiver.)", "slider", () => S.Master + "%",
                d => { S.Master = Math.Clamp(S.Master + d * 5, 0, 100); Changed(false); }, Percent: () => S.Master),
            new("Música", "Volume da música.", "slider", () => S.Music + "%", d => { S.Music = Math.Clamp(S.Music + d * 5, 0, 100); Changed(false); }, Percent: () => S.Music),
            new("Efeitos", "Volume dos efeitos sonoros.", "slider", () => S.Effects + "%", d => { S.Effects = Math.Clamp(S.Effects + d * 5, 0, 100); Changed(false); }, Percent: () => S.Effects),
        },
        3 => new List<Opt>
        {
            new("Tremor de câmera", "Intensidade do tremor nos impactos fortes. 0% desliga.", "slider", () => S.Shake + "%",
                d => { S.Shake = Math.Clamp(S.Shake + d * 25, 0, 100); Changed(false); }, Percent: () => S.Shake),
            new("Reduzir clarões", "Diminui os clarões de luz dos impactos e o piscar dos personagens atingidos.", "toggle", () => OnOff(S.ReduceFlashes),
                _ => { S.ReduceFlashes = !S.ReduceFlashes; Changed(); }),
            new("Números de dano", "Mostra o dano causado e recebido sobre os personagens.", "toggle", () => OnOff(S.DamageNumbers),
                _ => { S.DamageNumbers = !S.DamageNumbers; Changed(false); }),
        },
        _ => new List<Opt>
        {
            new("Câmera inicial", "Visão ao entrar na cripta. Durante o jogo, Tab / LS alterna.", "choice", () => S.StartCamera == "top" ? "Top-down" : "Isométrica",
                _ => { S.StartCamera = S.StartCamera == "top" ? "iso" : "top"; Changed(false); }),
            new("Zoom inicial", "Distância da câmera ao entrar na cripta (30% = valor da slice).", "slider", () => S.Zoom + "%",
                d => { S.Zoom = Math.Clamp(S.Zoom + d * 5, 0, 100); Changed(false); }, Percent: () => S.Zoom),
            new("Mostrar FPS", "Quadros por segundo no canto da tela.", "toggle", () => OnOff(S.ShowFps), _ => { S.ShowFps = !S.ShowFps; Changed(false); }),
        },
    };

    // ------------------------------------------------------------------ controls tab helpers

    private static InputEvent? Binding(string action, int column) =>
        InputMap.ActionGetEvents(action).FirstOrDefault(e => Bindings.IsKeyboard(e) == (column == 0));

    private int ControlRows => Bindings.Remappable.Length + 2;     // + icons + restore

    private void Assign(string action, InputEvent e)
    {
        var old = Binding(action, _column);
        if (old != null) InputMap.ActionEraseEvent(action, old);
        InputMap.ActionAddEvent(action, e);
        S.Bindings[action] = Bindings.Serialize(action);
        S.Save();
    }

    private void Captured(InputEvent e)
    {
        var action = _capture!;
        _capture = null;
        string text = Bindings.Text(e)!;
        var other = Bindings.Remappable.Select(r => r.Action).FirstOrDefault(a => a != action && InputMap.ActionGetEvents(a).Any(x => Bindings.Text(x) == text));
        if (other != null) { _conflict = (action, e, other); return; }
        Assign(action, e);
        _message = $"{Label(action)}: {Bindings.Name(e)}";
    }

    private static string Label(string action) => Bindings.Remappable.First(r => r.Action == action).Label;

    public override void _Input(InputEvent e)
    {
        if (_capture == null || !e.IsPressed() || e.IsEcho()) return;
        if (e is InputEventKey { PhysicalKeycode: Godot.Key.Escape } || e is InputEventJoypadButton { ButtonIndex: JoyButton.Start })
        { _capture = null; _message = "Cancelado."; GetViewport().SetInputAsHandled(); return; }
        bool keyboard = e is InputEventKey or InputEventMouseButton { ButtonIndex: not (MouseButton.WheelUp or MouseButton.WheelDown) };
        bool pad = e is InputEventJoypadButton || e is InputEventJoypadMotion m && Mathf.Abs(m.AxisValue) > .6f;
        if ((_column == 0 && keyboard) || (_column == 1 && pad))
        {
            var clean = e is InputEventKey k ? Bindings.Key(k.PhysicalKeycode) : e is InputEventMouseButton mb ? Bindings.Mouse(mb.ButtonIndex)
                      : e is InputEventJoypadButton jb ? Bindings.Button(jb.ButtonIndex) : Bindings.Axis(((InputEventJoypadMotion)e).Axis, Math.Sign(((InputEventJoypadMotion)e).AxisValue));
            Captured(clean);
            GetViewport().SetInputAsHandled();
        }
    }

    // ------------------------------------------------------------------ navigation

    public override void OnNav(Nav nav)
    {
        if (_capture != null) return;
        if (_conflict is { } c)
        {
            if (nav == Nav.Accept)
            {
                var mine = Binding(c.Action, _column);
                var theirs = InputMap.ActionGetEvents(c.Other).First(x => Bindings.Text(x) == Bindings.Text(c.Event));
                InputMap.ActionEraseEvent(c.Other, theirs);
                if (mine != null) InputMap.ActionAddEvent(c.Other, mine);
                S.Bindings[c.Other] = Bindings.Serialize(c.Other);
                Assign(c.Action, c.Event);
                _message = $"Trocado: {Label(c.Action)} e {Label(c.Other)}.";
                _conflict = null;
            }
            else if (nav == Nav.Back) { _conflict = null; _message = "Cancelado."; }
            return;
        }
        int rows = _tab == 2 ? ControlRows : Options().Count;
        switch (nav)
        {
            case Nav.Up: _focus = Cycle(_focus, rows, -1); _message = ""; break;
            case Nav.Down: _focus = Cycle(_focus, rows, 1); _message = ""; break;
            case Nav.TabLeft: _tab = Cycle(_tab, TabNames.Length, -1); _focus = 0; _message = ""; break;
            case Nav.TabRight: _tab = Cycle(_tab, TabNames.Length, 1); _focus = 0; _message = ""; break;
            case Nav.Back: S.Save(); Stack.Pop(); break;
            case Nav.Special: RestoreTab(); break;
            case Nav.Left or Nav.Right or Nav.Accept when _tab == 2: ControlsNav(nav); break;
            case Nav.Left: Options()[_focus].Change?.Invoke(-1); break;
            case Nav.Right: Options()[_focus].Change?.Invoke(1); break;
            case Nav.Accept: var o = Options()[_focus]; if (o.Accept != null) o.Accept(); else o.Change?.Invoke(1); break;
        }
    }

    private void ControlsNav(Nav nav)
    {
        int n = Bindings.Remappable.Length;
        if (_focus < n)
        {
            if (nav == Nav.Left) _column = 0;
            else if (nav == Nav.Right) _column = 1;
            else { _capture = Bindings.Remappable[_focus].Action; _message = ""; }
        }
        else if (_focus == n)
        {
            var icons = new[] { "auto", "xbox", "playstation" };
            S.Icons = icons[Cycle(Array.IndexOf(icons, S.Icons), 3, nav == Nav.Left ? -1 : 1)];
            Changed(false);
        }
        else if (nav == Nav.Accept) RestoreTab();
    }

    private void RestoreTab()
    {
        var d = new Settings();
        switch (_tab)
        {
            case 0:
                S.UsePreset(d.Preset); S.WindowMode = d.WindowMode; S.Resolution = d.Resolution; S.VSync = d.VSync; S.FpsLimit = d.FpsLimit;
                S.UiScale = d.UiScale; S.Brightness = d.Brightness; break;
            case 1: S.Master = d.Master; S.Music = d.Music; S.Effects = d.Effects; break;
            case 2: S.Bindings.Clear(); S.Icons = d.Icons; Bindings.Install(); break;
            case 3: S.Shake = d.Shake; S.ReduceFlashes = d.ReduceFlashes; S.DamageNumbers = d.DamageNumbers; break;
            default: S.StartCamera = d.StartCamera; S.Zoom = d.Zoom; S.ShowFps = d.ShowFps; break;
        }
        _message = "Padrão restaurado nesta aba.";
        Changed();
    }

    public override void OnWheel(float steps) => OnNav(steps > 0 ? Nav.Up : Nav.Down);

    // ------------------------------------------------------------------ drawing

    protected override void Paint()
    {
        Parts.Shade(this, .9f);
        var panel = new Rect2(40, 24, Size.X - 80, Size.Y - 72);
        Parts.Panel(this, panel, "CONFIGURAÇÕES");
        var tabs = Parts.Tabs(this, new Rect2(panel.Position.X + 20, panel.Position.Y + 58, 900, 36), TabNames, _tab);
        for (int i = 0; i < tabs.Count; i++) { int t = i; Hit(tabs[i], null, () => { _tab = t; _focus = 0; }); }
        if (_tab == 2) PaintControls(panel); else PaintOptions(panel);
        if (_message != "") UiKit.Text(this, _message, new Vector2(panel.Position.X + 40, panel.End.Y - 16), 11, Materials.Gold);
        if (_tab == 2)
            Parts.Prompts(this, Size.Y - 22, ("Enter", "A", "Mudar"), ("< >", "Dir.", "Coluna"), ("Q E", "LB", "Abas"), ("R", "Y", "Restaurar padrão"), ("Esc", "B", "Voltar"));
        else
            Parts.Prompts(this, Size.Y - 22, ("Setas", "Dir.", "Navegar"), ("< >", "Dir.", "Alterar"), ("Q E", "LB", "Abas"), ("R", "Y", "Restaurar padrão"), ("Esc", "B", "Voltar"));
    }

    private void PaintOptions(Rect2 panel)
    {
        var options = Options();
        _focus = Math.Clamp(_focus, 0, options.Count - 1);
        float y = panel.Position.Y + 108;
        for (int i = 0; i < options.Count; i++)
        {
            var o = options[i]; var r = new Rect2(panel.Position.X + 40, y, 640, 34);
            bool f = i == _focus;
            if (f) { DrawRect(r, Materials.Rgb(44, 30, 28)); DrawRect(r, Materials.Gold, false, 2); }
            UiKit.TextIn(this, o.Label, r, 22, f ? Materials.Bone : Materials.Ash, HorizontalAlignment.Left, 14);
            var v = new Rect2(r.Position.X + 380, r.Position.Y, 246, r.Size.Y);
            if (o.Kind == "slider")
            {
                var bar = new Rect2(v.Position.X + 4, v.Position.Y + 14, 160, 6);
                DrawRect(bar, Materials.IronDark);
                DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * o.Percent!() / 100f, bar.Size.Y)), f ? Materials.Gold : Materials.Ash);
                UiKit.TextIn(this, o.Value(), new Rect2(v.Position.X + 170, v.Position.Y, 76, v.Size.Y), 22, Materials.Bone, HorizontalAlignment.Right);
            }
            else if (o.Kind == "toggle")
                UiKit.TextIn(this, o.Value(), v, 22, o.Value() == "LIGADO" ? Parts.Good : Parts.Bad, HorizontalAlignment.Right, 6);
            else UiKit.TextIn(this, "<   " + o.Value() + "   >", v, 22, Materials.Bone, HorizontalAlignment.Right, 6);
            int index = i;
            Hit(r, () => _focus = index, () => { _focus = index; var opt = Options()[index]; if (opt.Accept != null) opt.Accept(); else opt.Change?.Invoke(1); });
            y += 38;
        }
        var cur = options[_focus];
        var info = new Rect2(panel.Position.X + 720, panel.Position.Y + 108, panel.Size.X - 760, 330);
        Parts.Box(this, info);
        UiKit.TextIn(this, cur.Label, new Rect2(info.Position + new Vector2(0, 8), new Vector2(info.Size.X, 32)), 22, Materials.Gold, HorizontalAlignment.Left, 16);
        Parts.Wrap(this, cur.Info, new Rect2(info.Position + new Vector2(16, 48), new Vector2(info.Size.X - 32, 150)), 22, Materials.Bone);
        if (cur.CostLevel > 0)
        {
            UiKit.Text(this, "Impacto no desempenho", new Vector2(info.Position.X + 16, info.Position.Y + 214), 11, Materials.Ash);
            for (int i = 0; i < 5; i++)
                DrawRect(new Rect2(info.Position.X + 16 + i * 30, info.Position.Y + 224, 24, 10), i < cur.CostLevel ? Materials.Ember : Materials.IronDark);
        }
        if (cur.Cost != null) Parts.Wrap(this, cur.Cost, new Rect2(info.Position + new Vector2(16, 250), new Vector2(info.Size.X - 32, 60)), 11, Parts.Faint, 4);
    }

    private void PaintControls(Rect2 panel)
    {
        _focus = Math.Clamp(_focus, 0, ControlRows - 1);
        float x0 = panel.Position.X + 40, y = panel.Position.Y + 106;
        UiKit.Text(this, "AÇÃO", new Vector2(x0 + 14, y + 4), 11, Materials.Ash);
        UiKit.Text(this, "TECLADO E MOUSE", new Vector2(x0 + 330, y + 4), 11, _column == 0 ? Materials.Gold : Materials.Ash);
        UiKit.Text(this, "CONTROLE", new Vector2(x0 + 548, y + 4), 11, _column == 1 ? Materials.Gold : Materials.Ash);
        y += 12;
        for (int i = 0; i < Bindings.Remappable.Length; i++)
        {
            var (action, label) = Bindings.Remappable[i];
            var r = new Rect2(x0, y, 680, 32);
            bool f = i == _focus;
            DrawRect(r, f ? Materials.Rgb(44, 30, 28) : (i % 2 == 0 ? Materials.Rgb(20, 18, 24, .7f) : new Color(0, 0, 0, 0)));
            UiKit.TextIn(this, label, new Rect2(r.Position, new Vector2(320, r.Size.Y)), 22, f ? Materials.Bone : Materials.Ash, HorizontalAlignment.Left, 14);
            for (int col = 0; col < 2; col++)
            {
                var cell = new Rect2(r.Position.X + (col == 0 ? 320 : 540), r.Position.Y + 2, 200, 28);
                bool active = f && col == _column;
                if (active) DrawRect(cell, Materials.Gold, false, 2);
                string text = _capture == action && active ? (col == 0 ? "Pressione uma tecla" : "Pressione um botão")
                            : Binding(action, col) is { } e ? Bindings.Name(e) : "—";
                UiKit.TextIn(this, text, cell, _capture == action && active ? 11 : 22, _capture == action && active ? Materials.Gold : Materials.Bone, HorizontalAlignment.Left, 10);
                int index = i, column = col;
                Hit(cell, () => { _focus = index; _column = column; }, () => { _focus = index; _column = column; _capture = Bindings.Remappable[index].Action; });
            }
            y += 34;
        }
        y += 6;
        var iconsRow = new Rect2(x0, y, 680, 32);
        bool fi = _focus == Bindings.Remappable.Length;
        if (fi) { DrawRect(iconsRow, Materials.Rgb(44, 30, 28)); DrawRect(iconsRow, Materials.Gold, false, 2); }
        UiKit.TextIn(this, "Ícones dos botões", iconsRow, 22, fi ? Materials.Bone : Materials.Ash, HorizontalAlignment.Left, 14);
        UiKit.TextIn(this, "<   " + (S.Icons switch { "xbox" => "Xbox", "playstation" => "PlayStation", _ => "Automático" }) + "   >", iconsRow, 22, Materials.Bone, HorizontalAlignment.Right, 10);
        Hit(iconsRow, () => _focus = Bindings.Remappable.Length, () => ControlsNav(Nav.Right));
        var restore = new Rect2(x0, y + 36, 680, 32);
        bool fr = _focus == Bindings.Remappable.Length + 1;
        if (fr) { DrawRect(restore, Materials.Rgb(44, 30, 28)); DrawRect(restore, Materials.Gold, false, 2); }
        UiKit.TextIn(this, "Restaurar controles padrão", restore, 22, fr ? Materials.Bone : Materials.Ash, HorizontalAlignment.Left, 14);
        Hit(restore, () => _focus = Bindings.Remappable.Length + 1, RestoreTab);

        var info = new Rect2(panel.Position.X + 760, panel.Position.Y + 112, panel.Size.X - 800, 360);
        Parts.Box(this, info);
        if (_conflict is { } c)
        {
            UiKit.TextIn(this, "Conflito", new Rect2(info.Position + new Vector2(0, 8), new Vector2(info.Size.X, 32)), 22, Materials.Gold, HorizontalAlignment.Left, 16);
            Parts.Wrap(this, $"{Bindings.Name(c.Event)} já está em {Label(c.Other)}. Enter / A troca as duas; Esc / B cancela.",
                       new Rect2(info.Position + new Vector2(16, 48), new Vector2(info.Size.X - 32, 200)), 22, Materials.Bone);
            return;
        }
        UiKit.TextIn(this, "Dispositivo", new Rect2(info.Position + new Vector2(0, 8), new Vector2(info.Size.X, 32)), 22, Materials.Gold, HorizontalAlignment.Left, 16);
        string detected = DeviceTracker.LastPadName != "" ? $"Último controle: {DeviceTracker.LastPadName}." : "Nenhum controle usado ainda.";
        Parts.Wrap(this, detected + " Os ícones seguem o último dispositivo usado, ou o conjunto escolhido abaixo.",
                   new Rect2(info.Position + new Vector2(16, 48), new Vector2(info.Size.X - 32, 150)), 22, Materials.Bone);
        float px = info.Position.X + 30;
        foreach (var b in new[] { "A", "B", "X", "Y" }) { UiKit.Prompt(this, new Vector2(px, info.Position.Y + 256), Device.Xbox, "", b, 12); px += 34; }
        px += 16;
        foreach (var b in new[] { "A", "B", "X", "Y" }) { UiKit.Prompt(this, new Vector2(px, info.Position.Y + 256), Device.PlayStation, "", b, 12); px += 34; }
        Parts.Wrap(this, "Esc e Start ficam reservados para a pausa e cancelam a troca.", new Rect2(info.Position + new Vector2(16, 286), new Vector2(info.Size.X - 32, 60)), 11, Parts.Faint, 4);
    }
}
