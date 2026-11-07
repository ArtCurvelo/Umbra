using Godot;

namespace Umbra.Game;

/// <summary>
/// Pause menu in "Ferro e Cinza". Focus moves with arrows, D-pad or stick (Godot's ui_* actions), Enter/A confirms,
/// Esc/B or Start closes; the mouse hovers and clicks. The world is paused while it is open.
/// </summary>
public partial class PauseMenu : CanvasLayer
{
    public event Action? Resumed, Restart, CameraToggled, QualityCycled, Quit;

    private readonly List<Button> _buttons = new();
    private Control _root = null!;
    private Func<string> _cameraLabel = () => "", _qualityLabel = () => "";

    public bool IsOpen => _root.Visible;
    public IReadOnlyList<Button> Buttons => _buttons;

    public void Setup(Func<string> cameraLabel, Func<string> qualityLabel)
    {
        _cameraLabel = cameraLabel; _qualityLabel = qualityLabel;
    }

    private partial class Panel : Control
    {
        public override void _Draw()
        {
            DrawRect(new Rect2(Vector2.Zero, Size), new Color(0, 0, 0, .55f));
            var r = new Rect2(Size / 2 - new Vector2(210, 190), new Vector2(420, 380));
            UiKit.Frame(this, r);
            UiKit.TextIn(this, "PAUSA", new Rect2(r.Position + new Vector2(0, 16), new Vector2(r.Size.X, 44)), 33, Materials.Gold);
            UiKit.TextIn(this, "Setas ou direcional: navegar   Enter ou A: confirmar   Esc ou B: voltar", new Rect2(r.Position.X, r.End.Y - 34, r.Size.X, 24), 11, Materials.Ash);
        }
    }

    public override void _Ready()
    {
        Layer = 20;
        ProcessMode = ProcessModeEnum.Always;
        _root = new Panel();
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);
        var box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        box.AddThemeConstantOverride("separation", 10);
        box.SetAnchorsPreset(Control.LayoutPreset.Center);
        box.CustomMinimumSize = new Vector2(320, 260);
        box.Position = new Vector2(-160, -120);
        _root.AddChild(box);
        foreach (var (id, action) in new (string, Action)[]
                 {
                     ("continuar", () => Close()), ("camera", () => { CameraToggled?.Invoke(); Refresh(); }),
                     ("qualidade", () => { QualityCycled?.Invoke(); Refresh(); }), ("recomecar", () => { Close(); Restart?.Invoke(); }),
                     ("sair", () => Quit?.Invoke()),
                 })
        {
            var b = Styled(new Button { Name = id, CustomMinimumSize = new Vector2(320, 42), FocusMode = Control.FocusModeEnum.All });
            b.Pressed += action;
            b.MouseEntered += () => b.GrabFocus();
            box.AddChild(b); _buttons.Add(b);
        }
        _root.Visible = false;
        Refresh();
    }

    private static Button Styled(Button b)
    {
        StyleBoxFlat Box(Color bg, Color border) => new()
        {
            BgColor = bg, BorderColor = border, BorderWidthBottom = 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
            ContentMarginLeft = 16,
        };
        b.AddThemeStyleboxOverride("normal", Box(Materials.Rgb(24, 22, 28), Materials.Iron));
        b.AddThemeStyleboxOverride("hover", Box(Materials.Rgb(44, 30, 28), Materials.Gold));
        b.AddThemeStyleboxOverride("pressed", Box(Materials.Rgb(60, 36, 30), Materials.Gold));
        b.AddThemeStyleboxOverride("focus", Box(new Color(0, 0, 0, 0), Materials.Gold));
        b.AddThemeColorOverride("font_color", Materials.Ash);
        b.AddThemeColorOverride("font_hover_color", Materials.Bone);
        b.AddThemeColorOverride("font_focus_color", Materials.Bone);
        b.AddThemeFontSizeOverride("font_size", 22);
        b.Alignment = HorizontalAlignment.Left;
        return b;
    }

    public void Refresh()
    {
        if (_buttons.Count == 0) return;
        _buttons[0].Text = "Continuar";
        _buttons[1].Text = "Câmera: " + _cameraLabel();
        _buttons[2].Text = "Qualidade: " + _qualityLabel();
        _buttons[3].Text = "Recomeçar a cripta";
        _buttons[4].Text = "Sair do jogo";
    }

    public void Open()
    {
        Refresh();
        _root.Visible = true;
        GetTree().Paused = true;
        _buttons[0].GrabFocus();
    }

    public void Close()
    {
        _root.Visible = false;
        GetTree().Paused = false;
        Resumed?.Invoke();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!IsOpen) return;
        if (e.IsActionPressed("pause") || e.IsActionPressed("ui_cancel"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }
}
