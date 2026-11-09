using Godot;
using Umbra.Domain.Heroes;
using Umbra.Domain.Items;
using Umbra.Domain.Play;
using Umbra.Game.Ui;
using NVec = System.Numerics.Vector2;

namespace Umbra.Game;

/// <summary>
/// Vertical slice entry point. The game session (Umbra.Domain.Play: UMBRA's real rules since step 5.8c) runs at a fixed
/// 60 ticks per second; this node turns devices into InputFrames, steps it, hands its events to the views and draws an
/// interpolated frame.
/// </summary>
public partial class Main : Node3D
{
    public LaunchOptions Options { get; private set; } = new();
    public GameSession Session { get; private set; } = null!;
    public const float TickSeconds = 1f / 60f;
    public WorldView World { get; private set; } = null!;
    public ActorLayer Actors { get; } = new();
    public CameraRig Rig { get; } = new();
    public Hud Hud { get; } = new();
    public ScreenStack Screens { get; } = new();
    /// <summary>A crypt run the player started (the main menu's Continuar).</summary>
    public bool RunInProgress { get; set; }
    /// <summary>Screenshots, benchmark, self-test: never read or write the player's settings.</summary>
    public bool ToolRun => Options.Shot != null || Options.Bench != null || Options.SelfTest;
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
        ThemeDB.FallbackFont = UiKit.Font;          // every Control (buttons, labels) uses UMBRA Pixel
        ThemeDB.FallbackFontSize = 22;
        Settings.Load(persistent: !ToolRun);
        if (ToolRun) Settings.Current.UsePreset(Options.Preset);
        else Bindings.ApplySaved(Settings.Current.Bindings);
        Preset = Settings.Current.Effective();
        if (Options.Scale is { } scale) Preset = Preset with { Scale3D = scale };
        if (Options.Lights is { } lights) Preset = Preset with { MaxLights = lights, TorchShadows = Math.Min(Preset.TorchShadows, lights) };
        AddChild(Rig);
        AddChild(Fx);
        if (LaunchOptions.Skip.Contains("fx")) Fx.ProcessMode = ProcessModeEnum.Disabled;
        AddChild(Actors);
        AddChild(new PostProcess());
        AddChild(Hud);
        Hud.Visible = !Options.NoHud;
        AddChild(Screens);
        Screens.Changed += () => Hud.Visible = !Options.NoHud && Screens.Count == 0;

        Rig.SetMode(ToolRun ? Options.Camera == "top" : Settings.Current.StartCamera == "top");
        Rig.SetYawStep(Options.Yaw);
        Rig.SetZoom(Options.Zoom ?? (ToolRun ? 30 : Settings.Current.Zoom));
        LoadTables();
        StartRun(Options.Seed);
        Settings.Current.ApplySystem(this);
        if (Options.Screen != null) OpenForShot(Options.Screen);
        else if (!ToolRun && !Options.Direct) Screens.Push(new MainMenuScreen(this));
        else RunInProgress = true;

        if (Options.Bench != null) _bench = new Bench(this);
        if (Options.Shot != null) AddChild(new ShotTaker { Main = this });
        if (Options.SelfTest) AddChild(new SelfTestDriver { Test = new SelfTest(this) });
        if (Options.Shot != null || Options.Bench != null)
        {
            DisplayServer.WindowSetSize(Options.Size);
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
            Engine.MaxFps = 0;
        }
    }

    private static bool _tablesLoaded;

    /// <summary>UMBRA's exported tables (game/data/umbra: heroes, trees, equipment, foes). Read from disk through the
    /// globalized path: fine in the editor and in tool runs; an exported build must ship the folder next to the
    /// executable (open point for the Phase 6 packaging).</summary>
    private static void LoadTables()
    {
        if (_tablesLoaded) return;
        string root = ProjectSettings.GlobalizePath("res://data/umbra");
        HeroTables.Load(root);
        Umbra.Domain.Enemies.FoeTables.Load(root);
        _tablesLoaded = true;
    }

    public void StartRun(ulong seed)
    {
        World?.QueueFree();
        Session = new GameSession(seed);
        for (int i = 0; i < Options.FastForward; i++) Session.Step(SessionPilot.Next(Session));
        World = new WorldView { Name = "Cripta" };
        AddChild(World);
        World.Build(Session.Map, Preset, Options.Seed);
        Actors.Build(Session);
        Hud.Bind(Session, Rig, Actors);
        Quality.Apply(GetViewport(), World, Preset);
        Rig.Snap(ActorLayer.World(Session.HeroWorld));
        Actors.Prewarm();
        Fx.Prewarm(ActorLayer.World(Session.HeroWorld));
        _accumulator = 0;
    }

    /// <summary>Benchmark: the same fight again on the same map (same seed), without rebuilding the geometry.</summary>
    private void RestartFight()
    {
        Session = new GameSession(Options.Seed);
        if (!Actors.Rebind(Session)) Actors.Build(Session);
        Hud.Bind(Session, Rig, Actors);
        _bench!.Restarts++;
        Profiler.Tags.Add("recomeco");
    }

    /// <summary>Settings screen changes: the 3D preset (when <paramref name="preset"/>) and everything else.</summary>
    public void ApplySettings(bool preset)
    {
        if (preset) SetPreset(Settings.Current.Effective());
        Settings.Current.ApplySystem(this);
    }

    /// <summary>--tela: opens a screen over the frozen scene for screenshots and documentation.</summary>
    private void OpenForShot(string screen)
    {
        var parts = screen.Split(':');
        if (parts[0] == "inventario") for (int i = 0; i < 2 && Session.Hero.Inventory.Items.Count > 1; i++) Gear.FromInventory(Session.Hero, Session.Hero.Inventory.Items.Count - 1);
        Screen s = parts[0] switch
        {
            "menu" => new MainMenuScreen(this), "pausa" => new PauseScreen(this), "inventario" => new CharacterScreen(this), "arvore" => new TreeScreen(this),
            _ => new SettingsScreen(this, parts.Length > 1 ? int.Parse(parts[1]) : 0),
        };
        Screens.Push(s);
    }

    public void SetPreset(Quality.Preset preset)
    {
        Preset = preset;
        Quality.Apply(GetViewport(), World, preset);
    }

    public override void _Input(InputEvent e) => DeviceTracker.Observe(e);

    public override void _UnhandledInput(InputEvent e)
    {
        if (Screens.Count > 0) return;
        if (e.IsActionPressed("pause")) { Screens.Push(new PauseScreen(this)); GetViewport().SetInputAsHandled(); return; }
        if (e.IsActionPressed("inventory")) { Screens.Push(new CharacterScreen(this)); GetViewport().SetInputAsHandled(); return; }
        if (e.IsActionPressed("skill_tree")) { Screens.Push(new TreeScreen(this)); GetViewport().SetInputAsHandled(); return; }
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
        _accumulator = Math.Min(_accumulator + delta, TickSeconds * 5);
        while (_accumulator >= TickSeconds && !Options.Freeze)
        {
            StepOnce(Options.Autopilot ? SessionPilot.Next(Session) : ReadInput());
            _accumulator -= TickSeconds;
        }
        Profiler.Add("simulacao", t0);
        double t1 = Profiler.Now;
        Present((float)(_accumulator / TickSeconds), delta);
        Profiler.Add("apresentacao", t1);
        if (_bench != null && (Session.Cleared && Session.Items.Count == 0 || !Session.Hero.Alive)) RestartFight();

        _bench?.Frame(delta);
        if (Options.Fx != null && _frames == 2) { Effects.Hold = .35f; Showcase(Options.Fx); }
    }

    /// <summary>--shot: counts frames even while a screen pauses the world, then saves the image and quits.</summary>
    private sealed partial class ShotTaker : Node
    {
        public Main Main = null!;
        private int _frames;
        public override void _Ready() => ProcessMode = ProcessModeEnum.Always;
        public override void _Process(double delta)
        {
            if (++_frames != Main.Options.ShotFrames) return;
            var image = GetViewport().GetTexture().GetImage();
            image.SavePng(Main.Options.Shot);
            GD.Print($"captura: {Main.Options.Shot} ({image.GetWidth()}x{image.GetHeight()}, {Main.Preset.Name}, {(Main.Rig.TopDown ? "top-down" : "isométrica")})");
            GetTree().Quit();
        }
    }

    public void StepOnce(in InputFrame input)
    {
        Actors.BeforeStep();
        Session.Step(input);
        Actors.AfterStep();
        foreach (var e in Session.Events) Dispatch(e);
    }

    public void Present(float alpha, double delta)
    {
        var hero = Actors.Interpolated(Session.HeroId, alpha);
        Actors.Sync(alpha, delta);
        Rig.Follow(hero, delta);
        World.Tick(delta, hero, Rig.Camera.GlobalPosition);
    }

    /// <summary>Screenshots of effects: plays one skill's visuals toward the nearest enemy, without touching the rules.</summary>
    private void Showcase(string fx)
    {
        var here = Session.HeroWorld;
        var target = Session.Enemies.Where(e => e.Alive).OrderBy(e => NVec.Distance(GameSession.ToWorld(e.Position), here)).FirstOrDefault();
        var dir = target == null ? Session.Facing : NVec.Normalize(GameSession.ToWorld(target.Position) - here);
        string id = fx switch { "golpe" => "barb2_golpe_brutal", "ruptura" => "barb2_ruptura", "investida" => "barb2_investida", _ => "" };
        if (id != "") Dispatch(new CastEvent(id, id == "barb2_golpe_brutal" ? "basic" : id, here, dir));
        if (target != null) Dispatch(new DamageEvent(Session.IdOf(target), 29, GameSession.ToWorld(target.Position), false));
    }

    /// <summary>Visual reach of a swing or cone: the skill's range in tiles (catalog) plus the body.</summary>
    private float Reach(string skillId) => (float)(Session.RangeOf(skillId) + .5);

    private void Dispatch(PlayEvent e)
    {
        Profiler.Tags.Add(e is CastEvent c ? c.SkillId.Replace("barb2_", "") : e.GetType().Name.Replace("Event", ""));
        Hud.OnEvent(e);
        switch (e)
        {
            case DamageEvent hit:
                Actors.OnHit(hit.TargetId);
                Fx.Impact(ActorLayer.World(hit.Position, 1f), hit.OnHero);
                if (hit.OnHero) Rig.Shake(.16f);
                break;
            case DeathEvent died:
                Actors.OnDied(died.EnemyId);
                Fx.Death(ActorLayer.World(died.Position));
                break;
            case CastEvent cast:
                var at = ActorLayer.World(cast.Position); var dir = new Vector3(cast.Direction.X, 0, cast.Direction.Y);
                if (dir.LengthSquared() < 1e-4f) dir = new Vector3(Session.Facing.X, 0, Session.Facing.Y);
                if (cast.SkillId == "barb2_ruptura") { Fx.Cone(at, dir, Reach(cast.SkillId), 90f); Rig.Shake(.22f); }
                else if (cast.SkillId == "barb2_investida") { Fx.Dash(at); Rig.Shake(.08f); }
                else Fx.Swing(at, dir, Reach(cast.SkillId), 110f);
                break;
            case ItemDroppedEvent dropped: Actors.AddDrop(dropped.Item); break;
            case ItemTakenEvent taken:
                Actors.RemoveDrop(taken.Item);
                Fx.Burst(ActorLayer.World(GameSession.ToWorld(taken.Item.Cell), .4f), Materials.RarityOf(taken.Item.Data), 20, .7f);
                break;
        }
    }

    /// <summary>Keyboard + mouse and gamepad to one InputFrame, in world units, relative to the camera.</summary>
    private InputFrame ReadInput()
    {
        var here = Session.HeroWorld; var facing = Session.Facing;
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
            aim = source == Vector2.Zero ? here + facing : here + new NVec(w.X, w.Z) * 2;
        }
        else
        {
            var ground = Rig.GroundUnder(GetViewport().GetMousePosition());
            aim = ground is { } g ? new NVec(g.X, g.Z) : here + facing;
        }

        bool attack = Input.IsActionPressed("attack");
        NVec? destination = null;
        if (!pad && _clickHeld && Screens.Count == 0)
        {
            var target = Session.Enemies.FirstOrDefault(e => e.Alive && NVec.Distance(GameSession.ToWorld(e.Position), aim) < .75f);
            if (target != null)
            {
                aim = GameSession.ToWorld(target.Position);        // click on an enemy: walk up to it, then swing
                if (Session.Adjacent(target)) attack = true;
                else destination = aim;
            }
            else if (Input.IsKeyPressed(Key.Shift)) attack = true;   // Shift + click: swing in place
            else destination = aim;
        }
        return new InputFrame(move, destination, aim, attack, Input.IsActionPressed("skill_1"), Input.IsActionPressed("skill_2"), Input.IsActionPressed("potion"));
    }
}
