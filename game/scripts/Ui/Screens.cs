using Godot;

namespace Umbra.Game.Ui;

/// <summary>Menu navigation codes. Every screen receives these and nothing else (no raw device events).</summary>
public enum Nav { Up, Down, Left, Right, Accept, Back, TabLeft, TabRight, Special, Delete }

/// <summary>
/// One press = one step (UMBRA's controller rule). Holding a direction repeats once after 380 ms, then every 110 ms,
/// whatever the device: keyboard, D-pad and stick all go through Godot's ui_* actions.
/// </summary>
public static class MenuInput
{
    private static readonly (string Action, Nav Nav)[] Directions =
        { ("ui_up", Nav.Up), ("ui_down", Nav.Down), ("ui_left", Nav.Left), ("ui_right", Nav.Right) };
    private static readonly (string Action, Nav Nav)[] Buttons =
    {
        ("ui_accept", Nav.Accept), ("ui_cancel", Nav.Back), ("pause", Nav.Back), ("tab_left", Nav.TabLeft), ("tab_right", Nav.TabRight),
        ("ui_special", Nav.Special), ("ui_delete_item", Nav.Delete),
    };
    private static string? _held;
    private static double _heldFor, _nextRepeat;
    public const double FirstRepeat = .38, Repeat = .11;

    public static List<Nav> Poll(double delta)
    {
        var result = new List<Nav>();
        foreach (var (action, nav) in Buttons) if (Input.IsActionJustPressed(action)) result.Add(nav);
        string? held = null; Nav heldNav = Nav.Up;
        foreach (var (action, nav) in Directions)
            if (Input.IsActionPressed(action)) { held = action; heldNav = nav; break; }
        if (held == null) { _held = null; return result; }
        if (held != _held) { _held = held; _heldFor = 0; _nextRepeat = FirstRepeat; result.Add(heldNav); return result; }
        _heldFor += delta;
        if (_heldFor >= _nextRepeat) { _nextRepeat += Repeat; result.Add(heldNav); }
        return result;
    }

    /// <summary>Clears the hold state (a new screen must not inherit a held direction).</summary>
    public static void Reset() { _held = null; }
}

/// <summary>
/// A modal screen drawn with the "Ferro e Cinza" kit. Screens register clickable areas while painting (Hit), so the
/// mouse hovers to focus and clicks to confirm on exactly what is drawn.
/// </summary>
public abstract partial class Screen : Control
{
    public ScreenStack Stack = null!;
    private readonly List<(Rect2 Rect, Action? Hover, Action? Click)> _hits = new();
    public virtual bool PausesWorld => true;
    /// <summary>True while the screen reads raw device input (remapping): menu navigation is suspended.</summary>
    public virtual bool CapturesRaw => false;
    /// <summary>Text for the screen reader / logs: what has the focus now.</summary>
    public virtual string FocusText => "";

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);      // anchors alone left the size at 0 under a CanvasLayer
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.None;
    }

    public virtual void OnOpen() { }
    public virtual void OnNav(Nav nav) { }
    public virtual void OnWheel(float steps) { }
    public virtual void Tick(double delta) { }

    protected abstract void Paint();

    public override void _Draw()
    {
        _hits.Clear();
        Paint();
    }

    protected void Hit(Rect2 rect, Action? hover, Action? click) => _hits.Add((rect, hover, click));

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseMotion motion)
        {
            foreach (var (rect, hover, _) in _hits) if (rect.HasPoint(motion.Position)) { hover?.Invoke(); break; }
        }
        else if (e is InputEventMouseButton { Pressed: true } button)
        {
            if (button.ButtonIndex == MouseButton.WheelUp) OnWheel(1);
            else if (button.ButtonIndex == MouseButton.WheelDown) OnWheel(-1);
            else if (button.ButtonIndex == MouseButton.Left)
                foreach (var (rect, hover, click) in _hits) if (rect.HasPoint(button.Position)) { hover?.Invoke(); click?.Invoke(); break; }
            else if (button.ButtonIndex == MouseButton.Right) OnNav(Nav.Back);
        }
        AcceptEvent();
    }
}

/// <summary>Screens on top of the game, one active at a time (the top). Pauses the world while any pausing screen is open.</summary>
public partial class ScreenStack : CanvasLayer
{
    private readonly List<Screen> _screens = new();
    private ulong _changedFrame = ulong.MaxValue;
    public Screen? Top => _screens.Count > 0 ? _screens[^1] : null;
    public int Count => _screens.Count;
    public event Action? Emptied;
    /// <summary>After every push or pop (Main hides the HUD while a screen is open).</summary>
    public event Action? Changed;

    public override void _Ready()
    {
        Layer = 15;
        ProcessMode = ProcessModeEnum.Always;
    }

    public void Push(Screen screen)
    {
        if (Top != null) Top.Visible = false;
        screen.Stack = this;
        _screens.Add(screen);
        AddChild(screen);
        MenuInput.Reset();
        _changedFrame = Engine.GetProcessFrames();
        screen.OnOpen();
        UpdatePause();
        Changed?.Invoke();
    }

    public void Pop()
    {
        if (Top == null) return;
        var top = Top; _screens.RemoveAt(_screens.Count - 1); top.QueueFree();
        if (Top != null) { Top.Visible = true; Top.OnOpen(); }
        MenuInput.Reset();
        _changedFrame = Engine.GetProcessFrames();
        UpdatePause();
        Changed?.Invoke();
        if (Top == null) Emptied?.Invoke();
    }

    public void Clear() { while (Top != null) Pop(); }

    private void UpdatePause() => GetTree().Paused = _screens.Any(s => s.PausesWorld);

    public override void _Process(double delta)
    {
        var top = Top;
        if (top == null) return;
        if (top.CapturesRaw) { MenuInput.Reset(); top.QueueRedraw(); return; }
        // the press that opened (or closed) a screen must not also act inside the new top screen
        if (Engine.GetProcessFrames() == _changedFrame) { MenuInput.Poll(delta); top.QueueRedraw(); return; }
        foreach (var nav in MenuInput.Poll(delta))
        {
            if (Top != top) break;                       // the screen changed during this frame
            top.OnNav(nav);
        }
        Top?.Tick(delta);
        Top?.QueueRedraw();
    }
}
