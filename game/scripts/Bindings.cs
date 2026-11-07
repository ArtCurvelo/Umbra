using Godot;

namespace Umbra.Game;

/// <summary>
/// Input actions, declared in code so the bindings are readable and diffable. Keyboard + mouse and gamepad share every
/// action; the menus use Godot's ui_* actions (arrows/D-pad/stick, Enter/A, Esc/B). The remappable actions can be changed
/// in Configurações > Controles; changes are saved as text ("key:W", "joy:0", "axis:1:-1", "mouse:2") in the settings.
/// </summary>
public static class Bindings
{
    /// <summary>Actions the player can remap, in the order of the Controls tab.</summary>
    public static readonly (string Action, string Label)[] Remappable =
    {
        ("move_up", "Andar para cima"), ("move_down", "Andar para baixo"), ("move_left", "Andar para a esquerda"), ("move_right", "Andar para a direita"),
        ("attack", "Golpe Brutal"), ("skill_1", "Ruptura"), ("skill_2", "Investida"), ("inventory", "Personagem"), ("skill_tree", "Habilidades"),
        ("camera_mode", "Câmera iso / top-down"), ("camera_left", "Girar câmera (esq.)"), ("camera_right", "Girar câmera (dir.)"),
    };

    public static void Install()
    {
        Add("move_up", Key(Godot.Key.W), Axis(JoyAxis.LeftY, -1));
        Add("move_down", Key(Godot.Key.S), Axis(JoyAxis.LeftY, 1));
        Add("move_left", Key(Godot.Key.A), Axis(JoyAxis.LeftX, -1));
        Add("move_right", Key(Godot.Key.D), Axis(JoyAxis.LeftX, 1));
        Add("aim_up", Axis(JoyAxis.RightY, -1));
        Add("aim_down", Axis(JoyAxis.RightY, 1));
        Add("aim_left", Axis(JoyAxis.RightX, -1));
        Add("aim_right", Axis(JoyAxis.RightX, 1));
        Add("click", Mouse(MouseButton.Left));                                   // move to the point, or attack the enemy under it
        Add("attack", Key(Godot.Key.Space), Button(JoyButton.A));                // attack in place, toward the cursor/stick
        Add("skill_1", Key(Godot.Key.Q), Mouse(MouseButton.Right), Button(JoyButton.X));   // Ruptura
        Add("skill_2", Key(Godot.Key.E), Button(JoyButton.Y));                   // Investida
        Add("pause", Key(Godot.Key.Escape), Button(JoyButton.Start));
        Add("inventory", Key(Godot.Key.I), Button(JoyButton.Back));              // character and inventory
        Add("skill_tree", Key(Godot.Key.K), Button(JoyButton.RightStick));       // skill tree
        Add("camera_mode", Key(Godot.Key.Tab), Button(JoyButton.LeftStick));     // isometric <-> top-down
        Add("camera_left", Key(Godot.Key.Z), Button(JoyButton.LeftShoulder));
        Add("camera_right", Key(Godot.Key.C), Button(JoyButton.RightShoulder));
        Add("zoom_in", Mouse(MouseButton.WheelUp), Button(JoyButton.DpadUp));
        Add("zoom_out", Mouse(MouseButton.WheelDown), Button(JoyButton.DpadDown));
        Add("restart", Key(Godot.Key.Enter), Button(JoyButton.A));
        // Menus
        Add("tab_left", Key(Godot.Key.Q), Button(JoyButton.LeftShoulder));
        Add("tab_right", Key(Godot.Key.E), Button(JoyButton.RightShoulder));
        Add("ui_special", Key(Godot.Key.R), Button(JoyButton.Y));
        Add("ui_delete_item", Key(Godot.Key.Delete), Button(JoyButton.X));
        Ensure("ui_accept", Button(JoyButton.A));
        Ensure("ui_cancel", Button(JoyButton.B));
        Ensure("ui_up", Button(JoyButton.DpadUp)); Ensure("ui_up", Axis(JoyAxis.LeftY, -1));
        Ensure("ui_down", Button(JoyButton.DpadDown)); Ensure("ui_down", Axis(JoyAxis.LeftY, 1));
        Ensure("ui_left", Button(JoyButton.DpadLeft)); Ensure("ui_left", Axis(JoyAxis.LeftX, -1));
        Ensure("ui_right", Button(JoyButton.DpadRight)); Ensure("ui_right", Axis(JoyAxis.LeftX, 1));
        foreach (var a in new[] { "ui_up", "ui_down", "ui_left", "ui_right" }) InputMap.ActionSetDeadzone(a, .5f);
    }

    /// <summary>Restores the player's saved remaps (after Install).</summary>
    public static void ApplySaved(Dictionary<string, List<string>> saved)
    {
        foreach (var (action, events) in saved)
        {
            if (!InputMap.HasAction(action) || !Remappable.Any(r => r.Action == action)) continue;
            var parsed = events.Select(Parse).Where(e => e != null).ToList();
            if (parsed.Count == 0) continue;
            InputMap.ActionEraseEvents(action);
            foreach (var e in parsed) InputMap.ActionAddEvent(action, e!);
        }
    }

    public static List<string> Serialize(string action) => InputMap.ActionGetEvents(action).Select(Text).Where(t => t != null).Select(t => t!).ToList();

    public static string? Text(InputEvent e) => e switch
    {
        InputEventKey k => $"key:{(k.PhysicalKeycode != Godot.Key.None ? k.PhysicalKeycode : k.Keycode)}",
        InputEventMouseButton m => $"mouse:{(int)m.ButtonIndex}",
        InputEventJoypadButton j => $"joy:{(int)j.ButtonIndex}",
        InputEventJoypadMotion a => $"axis:{(int)a.Axis}:{Math.Sign(a.AxisValue)}",
        _ => null,
    };

    public static InputEvent? Parse(string text)
    {
        var p = text.Split(':');
        try
        {
            return p[0] switch
            {
                "key" => Key(Enum.Parse<Godot.Key>(p[1])),
                "mouse" => Mouse((MouseButton)int.Parse(p[1])),
                "joy" => Button((JoyButton)int.Parse(p[1])),
                "axis" => Axis((JoyAxis)int.Parse(p[1]), int.Parse(p[2])),
                _ => null,
            };
        }
        catch (Exception) { return null; }
    }

    /// <summary>Short name for a binding, for the Controls tab (keyboard/mouse or gamepad).</summary>
    public static string Name(InputEvent e) => e switch
    {
        InputEventKey k => KeyName(k.PhysicalKeycode != Godot.Key.None ? k.PhysicalKeycode : k.Keycode),
        InputEventMouseButton m => m.ButtonIndex switch { MouseButton.Left => "M1", MouseButton.Right => "M2", MouseButton.Middle => "M3",
                                                          MouseButton.WheelUp => "Roda +", MouseButton.WheelDown => "Roda -", _ => "Mouse " + (int)m.ButtonIndex },
        InputEventJoypadButton j => j.ButtonIndex switch
        {
            JoyButton.A => "A", JoyButton.B => "B", JoyButton.X => "X", JoyButton.Y => "Y", JoyButton.LeftShoulder => "LB", JoyButton.RightShoulder => "RB",
            JoyButton.Back => "Back", JoyButton.Start => "Start", JoyButton.LeftStick => "LS", JoyButton.RightStick => "RS",
            JoyButton.DpadUp => "Dir. cima", JoyButton.DpadDown => "Dir. baixo", JoyButton.DpadLeft => "Dir. esq.", JoyButton.DpadRight => "Dir. dir.",
            _ => "Botão " + (int)j.ButtonIndex,
        },
        InputEventJoypadMotion a => a.Axis switch
        {
            JoyAxis.LeftX or JoyAxis.LeftY => "LS " + Arrow(a), JoyAxis.RightX or JoyAxis.RightY => "RS " + Arrow(a),
            JoyAxis.TriggerLeft => "LT", JoyAxis.TriggerRight => "RT", _ => "Eixo " + (int)a.Axis,
        },
        _ => "?",
    };

    private static string Arrow(InputEventJoypadMotion a) => (a.Axis is JoyAxis.LeftX or JoyAxis.RightX) ? (a.AxisValue < 0 ? "esq." : "dir.") : (a.AxisValue < 0 ? "cima" : "baixo");

    private static string KeyName(Key key) => key switch
    {
        Godot.Key.Space => "Espaço", Godot.Key.Escape => "Esc", Godot.Key.Enter => "Enter", Godot.Key.Tab => "Tab", Godot.Key.Shift => "Shift",
        Godot.Key.Up => "Cima", Godot.Key.Down => "Baixo", Godot.Key.Left => "Esquerda", Godot.Key.Right => "Direita", _ => OS.GetKeycodeString(key),
    };

    public static bool IsKeyboard(InputEvent e) => e is InputEventKey or InputEventMouseButton;

    private static void Ensure(string action, InputEvent e)
    {
        if (!InputMap.HasAction(action)) InputMap.AddAction(action);
        if (!InputMap.ActionHasEvent(action, e)) InputMap.ActionAddEvent(action, e);
    }

    private static void Add(string action, params InputEvent[] events)
    {
        if (InputMap.HasAction(action)) InputMap.EraseAction(action);
        InputMap.AddAction(action, .25f);
        foreach (var e in events) InputMap.ActionAddEvent(action, e);
    }

    public static InputEvent Key(Key key) => new InputEventKey { PhysicalKeycode = key };
    public static InputEvent Mouse(MouseButton button) => new InputEventMouseButton { ButtonIndex = button };
    public static InputEvent Button(JoyButton button) => new InputEventJoypadButton { ButtonIndex = button, Device = -1 };
    public static InputEvent Axis(JoyAxis axis, float value) => new InputEventJoypadMotion { Axis = axis, AxisValue = value, Device = -1 };
}

/// <summary>Which device the player used last, for the button prompts (keyboard, Xbox or PlayStation).
/// The Controls tab can force the icon set (Override).</summary>
public enum Device { Keyboard, Xbox, PlayStation }

public static class DeviceTracker
{
    private static Device _detected = Device.Keyboard;
    public static Device? Override;
    public static Device Current => _detected == Device.Keyboard || Override == null ? _detected : Override.Value;
    public static string LastPadName { get; private set; } = "";

    public static void Observe(InputEvent e)
    {
        if (e is InputEventKey or InputEventMouseButton) _detected = Device.Keyboard;
        else if (e is InputEventMouseMotion m && m.Relative.LengthSquared() > 4) _detected = Device.Keyboard;
        else if (e is InputEventJoypadButton || (e is InputEventJoypadMotion j && Mathf.Abs(j.AxisValue) > .4f))
        {
            _detected = Detect(e.Device);
            LastPadName = Input.GetJoyName(e.Device);
        }
    }

    public static Device Detect(int device)
    {
        string name = Input.GetJoyName(device).ToLowerInvariant();
        return name.Contains("ps") || name.Contains("sony") || name.Contains("dualsense") || name.Contains("dualshock")
            ? Device.PlayStation : Device.Xbox;
    }
}
