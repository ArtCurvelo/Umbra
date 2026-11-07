using Godot;

namespace Umbra.Game;

/// <summary>
/// Input actions, declared in code so the bindings are readable and diffable. Keyboard + mouse and gamepad share every
/// action; the menus use Godot's ui_* actions (arrows/D-pad/stick, Enter/A, Esc/B), which already cover all three.
/// </summary>
public static class Bindings
{
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
        Add("camera_mode", Key(Godot.Key.Tab), Button(JoyButton.Back));          // isometric <-> top-down
        Add("camera_left", Key(Godot.Key.Z), Button(JoyButton.LeftShoulder));
        Add("camera_right", Key(Godot.Key.C), Button(JoyButton.RightShoulder));
        Add("zoom_in", Mouse(MouseButton.WheelUp), Button(JoyButton.DpadUp));
        Add("zoom_out", Mouse(MouseButton.WheelDown), Button(JoyButton.DpadDown));
        Add("restart", Key(Godot.Key.Enter), Button(JoyButton.A));
        // Menus: make sure Godot's ui_* actions also answer the gamepad (A confirms, B goes back), whatever the defaults.
        Ensure("ui_accept", Button(JoyButton.A));
        Ensure("ui_cancel", Button(JoyButton.B));
        Ensure("ui_up", Button(JoyButton.DpadUp));
        Ensure("ui_down", Button(JoyButton.DpadDown));
    }

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

    private static InputEvent Key(Key key) => new InputEventKey { PhysicalKeycode = key };
    private static InputEvent Mouse(MouseButton button) => new InputEventMouseButton { ButtonIndex = button };
    private static InputEvent Button(JoyButton button) => new InputEventJoypadButton { ButtonIndex = button, Device = -1 };
    private static InputEvent Axis(JoyAxis axis, float value) => new InputEventJoypadMotion { Axis = axis, AxisValue = value, Device = -1 };
}

/// <summary>Which device the player used last, for the button prompts (keyboard, Xbox or PlayStation).</summary>
public enum Device { Keyboard, Xbox, PlayStation }

public static class DeviceTracker
{
    public static Device Current { get; private set; } = Device.Keyboard;

    public static void Observe(InputEvent e)
    {
        if (e is InputEventKey or InputEventMouseButton) Current = Device.Keyboard;
        else if (e is InputEventMouseMotion m && m.Relative.LengthSquared() > 4) Current = Device.Keyboard;
        else if (e is InputEventJoypadButton || (e is InputEventJoypadMotion j && Mathf.Abs(j.AxisValue) > .4f))
            Current = Detect(e.Device);
    }

    public static Device Detect(int device)
    {
        string name = Input.GetJoyName(device).ToLowerInvariant();
        return name.Contains("ps") || name.Contains("sony") || name.Contains("dualsense") || name.Contains("dualshock")
            ? Device.PlayStation : Device.Xbox;
    }
}
