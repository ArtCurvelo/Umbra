using Godot;

namespace Umbra.Game;

/// <summary>
/// One camera, two views of the same world: isometric (default) and top-down. Yaw turns in 45° steps; zoom is a
/// percentage of the distance range. Calibration for the Game Director's "30%": ZoomPercent 30 = 12.2 units away
/// with a 40° field of view (PROVISÓRIO until judged on screen).
/// </summary>
public partial class CameraRig : Node3D
{
    public const float MinDistance = 8f, MaxDistance = 22f, Fov = 40f;
    public const float IsoPitch = 52f, TopPitch = 84f;

    public Camera3D Camera { get; } = new() { Fov = Fov, Near = .3f, Far = 120f };
    public bool TopDown { get; private set; }
    public int YawStep { get; private set; }
    public float ZoomPercent { get; private set; } = 30f;

    private Vector3 _focus;
    private float _shake, _shakeTime;
    /// <summary>Configurações > Acessibilidade: 0 = no tremor, 1 = full.</summary>
    public static float ShakeScale = 1f;
    private float _yaw, _pitch = IsoPitch, _distance;

    public override void _Ready()
    {
        AddChild(Camera);
        Camera.MakeCurrent();
        _yaw = TargetYaw; _distance = TargetDistance;
    }

    private float TargetYaw => 45f + YawStep * 45f;
    private float TargetPitch => TopDown ? TopPitch : IsoPitch;
    private float TargetDistance => MinDistance + (MaxDistance - MinDistance) * ZoomPercent / 100f;

    public void SetMode(bool topDown) => TopDown = topDown;
    public void ToggleMode() => TopDown = !TopDown;
    public void Rotate(int steps) => YawStep += steps;
    public void SetYawStep(int step) => YawStep = step;
    public void Zoom(float deltaPercent) => ZoomPercent = Mathf.Clamp(ZoomPercent + deltaPercent, 0f, 100f);
    public void SetZoom(float percent) => ZoomPercent = Mathf.Clamp(percent, 0f, 100f);

    /// <summary>Jump straight to the target pose (first frame, screenshots, benchmark).</summary>
    public void Snap(Vector3 focus)
    {
        _focus = focus; _yaw = TargetYaw; _pitch = TargetPitch; _distance = TargetDistance;
        Apply();
    }

    /// <summary>Short camera tremor on heavy impacts (presentation only; can be switched off for comfort).</summary>
    public void Shake(float amount) { if (ShakeScale > 0) _shake = Mathf.Max(_shake, amount * Mathf.Sqrt(ShakeScale)); }

    public void Follow(Vector3 focus, double delta)
    {
        _shakeTime += (float)delta;
        _shake = Mathf.Max(0, _shake - (float)delta * 1.6f);
        float k = 1f - Mathf.Exp(-(float)delta * 9f);
        _focus = _focus.Lerp(focus, k);
        _yaw = Mathf.Lerp(_yaw, TargetYaw, k);
        _pitch = Mathf.Lerp(_pitch, TargetPitch, k);
        _distance = Mathf.Lerp(_distance, TargetDistance, k);
        Apply();
    }

    private void Apply()
    {
        float yaw = Mathf.DegToRad(_yaw), pitch = Mathf.DegToRad(_pitch);
        var offset = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch)) * _distance;
        var target = _focus + new Vector3(0, .9f, 0);
        if (_shake > 0) target += new Vector3(Mathf.Sin(_shakeTime * 71f), Mathf.Sin(_shakeTime * 53f) * .5f, Mathf.Cos(_shakeTime * 67f)) * _shake * _shake * 2.2f;
        Camera.GlobalPosition = target + offset;
        Camera.LookAt(target, Vector3.Up);
    }

    /// <summary>Ground directions of the screen's right and up, for camera-relative movement.</summary>
    public (Vector3 Right, Vector3 Up) GroundAxes()
    {
        var basis = Camera.GlobalTransform.Basis;
        var right = new Vector3(basis.X.X, 0, basis.X.Z).Normalized();
        var up = new Vector3(basis.Y.X, 0, basis.Y.Z);              // the screen's up, laid on the floor (works in top-down too)
        if (up.LengthSquared() < 1e-4f) up = new Vector3(-basis.Z.X, 0, -basis.Z.Z);
        return (right, up.Normalized());
    }

    /// <summary>Point on the floor (y = 0) under a screen position.</summary>
    public Vector3? GroundUnder(Vector2 screen)
    {
        var origin = Camera.ProjectRayOrigin(screen);
        var normal = Camera.ProjectRayNormal(screen);
        if (Mathf.Abs(normal.Y) < 1e-4f) return null;
        float t = -origin.Y / normal.Y;
        return t > 0 ? origin + normal * t : null;
    }
}
