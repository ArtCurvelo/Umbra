using Godot;
using Umbra.Domain.Gameplay;
using NVec = System.Numerics.Vector2;

namespace Umbra.Game;

/// <summary>
/// Vertical slice entry point. The simulation (Umbra.Domain) runs at a fixed 60 ticks per second; this node turns
/// devices into InputFrames, steps the simulation, hands its events to the views and draws an interpolated frame.
/// </summary>
public partial class Main : Node3D
{
    public LaunchOptions Options { get; private set; } = new();
    public Simulation Sim { get; private set; } = null!;
    public WorldView World { get; private set; } = null!;
    public ActorLayer Actors { get; } = new();
    public CameraRig Rig { get; } = new();
    public Hud Hud { get; } = new();
    public PauseMenu Pause { get; } = new();
    public Effects Fx { get; } = new();
    public Quality.Preset Preset { get; private set; } = Quality.Medio;

    private double _accumulator;
    private Bench? _bench;
    private int _frames;
    private bool _clickHeld;

    public override void _Ready()
    {
        Options = LaunchOptions.Parse(OS.GetCmdlineUserArgs());
        Bindings.Install();
        Preset = Quality.ById(Options.Preset);
        if (Options.Scale is { } scale) Preset = Preset with { Scale3D = scale };
        if (Options.Lights is { } lights) Preset = Preset with { MaxLights = lights, TorchShadows = Math.Min(Preset.TorchShadows, lights) };
        AddChild(Rig);
        if (!LaunchOptions.Skip.Contains("fx")) AddChild(Fx);
        AddChild(Actors);
        AddChild(Hud);
        Hud.Visible = !Options.NoHud;
        AddChild(Pause);
        Pause.Setup(() => Rig.TopDown ? "Top-down" : "Isométrica", () => Preset.Name);
        Pause.CameraToggled += () => Rig.ToggleMode();
        Pause.QualityCycled += () => SetPreset(Quality.Next(Preset));
        Pause.Restart += () => StartRun(Options.Seed);
        Pause.Quit += () => GetTree().Quit();

        Rig.SetMode(Options.Camera == "top");
        Rig.SetYawStep(Options.Yaw);
        if (Options.Zoom is { } zoom) Rig.SetZoom(zoom);
        StartRun(Options.Seed);

        if (Options.Bench != null) _bench = new Bench(this);
        if (Options.SelfTest) AddChild(new SelfTestDriver { Test = new SelfTest(this) });
        if (Options.Shot != null || Options.Bench != null)
        {
            DisplayServer.WindowSetSize(new Vector2I(1280, 720));
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
            Engine.MaxFps = 0;
        }
    }

    public void StartRun(ulong seed)
    {
        World?.QueueFree();
        Sim = new Simulation(seed);
        for (int i = 0; i < Options.FastForward; i++) Sim.Step(Autopilot.Next(Sim));
        World = new WorldView { Name = "Cripta" };
        AddChild(World);
        World.Build(Sim.Map, Preset);
        Actors.Build(Sim);
        Hud.Bind(Sim, Rig, Actors);
        Quality.Apply(GetViewport(), World, Preset);
        Rig.Snap(ActorLayer.World(Sim.Hero.Position));
        _accumulator = 0;
    }

    /// <summary>Benchmark: the same fight again on the same map (same seed), without rebuilding the geometry.</summary>
    private void RestartFight()
    {
        Sim = new Simulation(Options.Seed);
        Actors.Build(Sim);
        Hud.Bind(Sim, Rig, Actors);
        _bench!.Restarts++;
    }

    public void SetPreset(Quality.Preset preset)
    {
        Preset = preset;
        Quality.Apply(GetViewport(), World, preset);
    }

    public override void _Input(InputEvent e) => DeviceTracker.Observe(e);

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("pause") && !Pause.IsOpen) { Pause.Open(); GetViewport().SetInputAsHandled(); return; }
        if (e.IsActionPressed("camera_mode")) Rig.ToggleMode();
        if (e.IsActionPressed("camera_left")) Rig.Rotate(-1);
        if (e.IsActionPressed("camera_right")) Rig.Rotate(1);
        if (e.IsActionPressed("zoom_in")) Rig.Zoom(-8);
        if (e.IsActionPressed("zoom_out")) Rig.Zoom(8);
        if (e.IsActionPressed("restart") && Hud.Banner == "dead") StartRun(Options.Seed);
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } mb) _clickHeld = mb.Pressed;
    }

    public override void _Process(double delta)
    {
        _frames++;
        if (Options.SelfTest) return;

        double t0 = Profiler.Now;
        _accumulator = Math.Min(_accumulator + delta, Simulation.TickSeconds * 5);
        while (_accumulator >= Simulation.TickSeconds && !Options.Freeze)
        {
            StepOnce(Options.Autopilot ? Autopilot.Next(Sim) : ReadInput());
            _accumulator -= Simulation.TickSeconds;
        }
        Profiler.Add("simulacao", t0);
        double t1 = Profiler.Now;
        Present((float)(_accumulator / Simulation.TickSeconds), delta);
        Profiler.Add("apresentacao", t1);
        if (_bench != null && Sim.Cleared && Sim.Drops.Count == 0) RestartFight();

        _bench?.Frame(delta);
        if (Options.Shot != null && _frames == Options.ShotFrames)
        {
            var image = GetViewport().GetTexture().GetImage();
            image.SavePng(Options.Shot);
            GD.Print($"captura: {Options.Shot} ({image.GetWidth()}x{image.GetHeight()}, {Preset.Name}, {(Rig.TopDown ? "top-down" : "isométrica")})");
            GetTree().Quit();
        }
    }

    public void StepOnce(in InputFrame input)
    {
        Actors.BeforeStep();
        Sim.Step(input);
        Actors.AfterStep();
        foreach (var e in Sim.Events) Dispatch(e);
    }

    public void Present(float alpha, double delta)
    {
        var hero = Actors.Interpolated(Sim.Hero, alpha);
        Actors.Sync(alpha, delta);
        Rig.Follow(hero, delta);
        World.Tick(delta, hero, Rig.Camera.GlobalPosition);
    }

    private void Dispatch(GameEvent e)
    {
        Hud.OnEvent(e);
        switch (e)
        {
            case HitEvent hit:
                Actors.OnHit(hit.TargetId);
                Fx.Burst(ActorLayer.World(hit.Position, 1f), hit.OnHero ? Materials.Rgb(220, 60, 50) : Materials.Rgb(255, 190, 120));
                break;
            case DiedEvent died:
                Actors.OnDied(died.ActorId);
                Fx.Burst(ActorLayer.World(died.Position, .3f), Materials.Rgb(120, 110, 100), 26, .6f);
                break;
            case StrikeEvent strike: Actors.OnStrike(strike.EnemyId); break;
            case SkillEvent skill:
                var at = ActorLayer.World(skill.Position); var dir = new Vector3(skill.Direction.X, 0, skill.Direction.Y);
                if (skill.SkillId == SliceData.GolpeBrutal.Id) Fx.Swing(at, dir, SliceData.GolpeBrutal.Range + .35f, SliceData.GolpeBrutal.ArcDegrees);
                else if (skill.SkillId == SliceData.Ruptura.Id) Fx.Cone(at, dir, SliceData.Ruptura.Range + .35f, SliceData.Ruptura.ArcDegrees);
                break;
            case DroppedEvent dropped: Actors.AddDrop(dropped.Drop); break;
            case PickedUpEvent picked:
                Actors.RemoveDrop(picked.Drop);
                Fx.Burst(ActorLayer.World(picked.Drop.Position, .4f), Materials.Rarity(picked.Drop.Item.Rarity), 20, .7f);
                break;
        }
    }

    /// <summary>Keyboard + mouse and gamepad to one InputFrame, in world units, relative to the camera.</summary>
    private InputFrame ReadInput()
    {
        var hero = Sim.Hero;
        var (right, up) = Rig.GroundAxes();
        var stick = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        var moveWorld = right * stick.X - up * stick.Y;
        var move = new NVec(moveWorld.X, moveWorld.Z);

        var aimStick = Input.GetVector("aim_left", "aim_right", "aim_up", "aim_down");
        NVec aim;
        bool pad = DeviceTracker.Current != Device.Keyboard;
        if (pad)
        {
            var source = aimStick.Length() > .3f ? aimStick : stick.Length() > .3f ? stick : Vector2.Zero;
            var w = right * source.X - up * source.Y;
            aim = source == Vector2.Zero ? hero.Position + hero.Facing : hero.Position + new NVec(w.X, w.Z) * 2;
        }
        else
        {
            var ground = Rig.GroundUnder(GetViewport().GetMousePosition());
            aim = ground is { } g ? new NVec(g.X, g.Z) : hero.Position + hero.Facing;
        }

        bool attack = Input.IsActionPressed("attack");
        NVec? destination = null;
        if (!pad && _clickHeld && !Pause.IsOpen)
        {
            var target = Sim.Enemies.FirstOrDefault(e => e.Alive && NVec.Distance(e.Position, aim) < .9f);
            if (target != null)
            {
                aim = target.Position;                              // click on an enemy: walk up to it, then swing
                var toward = NVec.Normalize(aim - hero.Position);
                if (Sim.InArc(target, toward, SliceData.GolpeBrutal.Range, SliceData.GolpeBrutal.ArcDegrees)) attack = true;
                else destination = target.Position;
            }
            else if (Input.IsKeyPressed(Key.Shift)) attack = true;   // Shift + click: swing in place
            else destination = aim;
        }
        return new InputFrame(move, destination, aim, attack, Input.IsActionPressed("skill_1"), Input.IsActionPressed("skill_2"));
    }
}
