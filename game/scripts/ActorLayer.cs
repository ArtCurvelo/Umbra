using Godot;
using Umbra.Domain.Gameplay;
using NVec = System.Numerics.Vector2;

namespace Umbra.Game;

/// <summary>
/// Hero, enemies and drops on screen. Positions are interpolated between the last two simulation ticks.
/// The UMBRA models have no skeleton yet (assets/models/v080/README.txt), so motion is procedural: walk bob,
/// lunge on attack, wind-up lean, hit flash and a fall on death.
/// </summary>
public partial class ActorLayer : Node3D
{
    /// <summary>The v080 models look down +Z (glTF convention).</summary>
    private const float ModelYaw = 0f;

    private sealed class View
    {
        public Actor Actor = null!;
        public Node3D Root = null!, Body = null!;
        public StandardMaterial3D Flash = null!;
        public MeshInstance3D? Ring, RingFill;
        public NVec Previous, Current;
        public float WalkPhase, FlashTime, DeathTime = -1, Yaw;
        public int LungeTick = -1000;
    }

    private readonly Dictionary<int, View> _views = new();
    private readonly Dictionary<int, Node3D> _drops = new();
    private Simulation _sim = null!;
    private CpuParticles3D _dashTrail = null!;

    public static Vector3 World(NVec p, float y = 0) => new(p.X, y, p.Y);

    public void Build(Simulation sim)
    {
        _sim = sim;
        foreach (var child in GetChildren()) child.QueueFree();
        _views.Clear(); _drops.Clear();
        _views[sim.Hero.Id] = MakeView(sim.Hero, LaunchOptions.Skip.Contains("hero") ? "none" : "res://assets/models/barbaro.gltf", 1f);
        if (!LaunchOptions.Skip.Contains("enemies")) foreach (var e in sim.Enemies) _views[e.Id] = MakeView(e, "res://assets/models/bruto.gltf", 1.12f);
        foreach (var d in sim.Drops) AddDrop(d);
        _dashTrail = new CpuParticles3D
        {
            Emitting = false, Amount = 24, Lifetime = .35f, Mesh = new QuadMesh { Size = new Vector2(.12f, .12f), Material = Materials.Particle() },
            Direction = Vector3.Up, Spread = 30, InitialVelocityMin = .2f, InitialVelocityMax = .6f, Gravity = Vector3.Zero,
            ColorRamp = WorldView.Fade(Materials.Rgb(255, 150, 80, .8f), Materials.Rgb(120, 40, 20, 0)),
            Position = new Vector3(0, .8f, 0),
        };
        _views[sim.Hero.Id].Root.AddChild(_dashTrail);
        foreach (var v in _views.Values) if (!v.Actor.Alive) v.DeathTime = 10f;      // fast-forwarded: already lying down
    }

    private View MakeView(Actor actor, string model, float scale)
    {
        var root = new Node3D { Name = actor is Hero ? "Heroi" : "Inimigo" + actor.Id };
        var body = new Node3D();
        root.AddChild(body);
        var flash = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(1, 1, 1, 0),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        };
        var scene = ResourceLoader.Exists(model) ? ResourceLoader.Load<PackedScene>(model) : null;
        Node3D mesh = scene?.Instantiate<Node3D>() ?? new MeshInstance3D { Mesh = new CapsuleMesh { Radius = .3f, Height = 1.7f }, Position = new Vector3(0, .85f, 0) };
        mesh.Scale = Vector3.One * scale;
        mesh.RotationDegrees = new Vector3(0, ModelYaw, 0);
        body.AddChild(mesh);
        foreach (var m in Meshes(mesh))
        {
            var material = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = .78f, Metallic = .08f };
            for (int s = 0; s < m.Mesh.GetSurfaceCount(); s++) m.SetSurfaceOverrideMaterial(s, material);
            m.MaterialOverlay = flash;
        }
        var blob = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = Vector2.One * actor.Radius * 3.4f, Material = Materials.Blob() },
            Position = new Vector3(0, .015f, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        root.AddChild(blob);
        var view = new View { Actor = actor, Root = root, Body = body, Flash = flash, Previous = actor.Position, Current = actor.Position };
        if (actor is Enemy)
        {
            view.Ring = Disc(Materials.Rgb(220, 50, 40, .9f), true);
            view.RingFill = Disc(Materials.Rgb(200, 40, 30, .45f), false);
            root.AddChild(view.Ring); root.AddChild(view.RingFill);
        }
        view.Yaw = Mathf.Atan2(actor.Facing.X, actor.Facing.Y);
        AddChild(root);
        root.Position = World(actor.Position);
        return view;
    }

    private static IEnumerable<MeshInstance3D> Meshes(Node node)
    {
        if (node is MeshInstance3D m && m.Mesh != null) yield return m;
        foreach (var child in node.GetChildren()) foreach (var inner in Meshes(child)) yield return inner;
    }

    /// <summary>Ground marker of a wind-up: the outline shows the reach, the fill grows until the blow lands.</summary>
    private static MeshInstance3D Disc(Color color, bool outline)
    {
        var g = new Gradient();
        if (outline) { g.Offsets = new[] { 0f, .78f, .9f, 1f }; g.Colors = new[] { new Color(color, 0), new Color(color, 0), color, new Color(color, 0) }; }
        else { g.Offsets = new[] { 0f, .9f, .97f }; g.Colors = new[] { new Color(color, color.A * .5f), color, new Color(color, 0) }; }
        var texture = new GradientTexture2D { Gradient = g, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(.5f, .5f), FillTo = new Vector2(1f, .5f), Width = 128, Height = 128 };
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoTexture = texture, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        float size = (SliceData.Brute.Radius + SliceData.Barbarian.Radius + .55f + .35f) * 2;
        return new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(size, size), Material = material }, Position = new Vector3(0, .03f, 0), Visible = false,
                                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
    }

    public void AddDrop(Drop drop)
    {
        var color = Materials.Rarity(drop.Item.Rarity);
        var root = new Node3D { Position = World(drop.Position) };
        root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(.32f, .14f, .22f), Material = Materials.Metal(color.Darkened(.35f)) },
                                           Position = new Vector3(0, .08f, 0), RotationDegrees = new Vector3(0, 30, 0) });
        var beam = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = .01f, BottomRadius = .09f, Height = 1.8f, Material = Beam(color) },
            Position = new Vector3(0, .8f, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        root.AddChild(beam);
        AddChild(root);
        _drops[drop.Id] = root;
    }

    private static StandardMaterial3D Beam(Color color)
    {
        var m = Materials.Flat(new Color(color, .22f), true);
        m.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
        return m;
    }

    public void RemoveDrop(Drop drop)
    {
        if (_drops.Remove(drop.Id, out var node)) node.QueueFree();
    }

    /// <summary>Called right before each simulation tick.</summary>
    public void BeforeStep()
    {
        foreach (var v in _views.Values) v.Previous = v.Actor.Position;
    }

    public void AfterStep()
    {
        foreach (var v in _views.Values) v.Current = v.Actor.Position;
    }

    public void OnHit(int actorId) { if (_views.TryGetValue(actorId, out var v)) v.FlashTime = .12f; }
    public void OnDied(int actorId) { if (_views.TryGetValue(actorId, out var v)) v.DeathTime = 0; }
    public void OnStrike(int actorId) { if (_views.TryGetValue(actorId, out var v)) v.LungeTick = _sim.Tick; }

    public Vector3 Interpolated(Actor actor, float alpha) =>
        _views.TryGetValue(actor.Id, out var v) ? World(NVec.Lerp(v.Previous, v.Current, alpha)) : World(actor.Position);

    public void Sync(float alpha, double delta)
    {
        float dt = (float)delta;
        foreach (var v in _views.Values)
        {
            var pos = World(NVec.Lerp(v.Previous, v.Current, alpha));
            float speed = (v.Current - v.Previous).Length() * 60f;
            v.Root.Position = pos;
            float targetYaw = Mathf.Atan2(v.Actor.Facing.X, v.Actor.Facing.Y);
            v.Yaw = Mathf.LerpAngle(v.Yaw, targetYaw, 1 - Mathf.Exp(-dt * 16));
            v.Root.Rotation = new Vector3(0, v.Yaw, 0);
            v.FlashTime = Mathf.Max(0, v.FlashTime - dt);
            v.Flash.AlbedoColor = new Color(1f, .35f, .25f, v.FlashTime > 0 ? .7f : 0f);

            if (v.DeathTime >= 0)
            {
                v.DeathTime += dt;
                float fall = Mathf.Clamp(v.DeathTime / .45f, 0, 1);
                v.Body.Rotation = new Vector3(Mathf.Pi / 2 * fall * fall, 0, 0);
                v.Body.Position = new Vector3(0, -.15f * fall, -.1f * fall);
                if (v.Ring != null) { v.Ring.Visible = false; v.RingFill!.Visible = false; }
                continue;
            }

            float bob = 0, pitch = 0, forward = 0;
            if (speed > .3f)
            {
                v.WalkPhase += dt * speed * 3.2f;
                bob = Mathf.Abs(Mathf.Sin(v.WalkPhase)) * .07f;
                pitch = .08f;
            }
            if (v.Actor is Hero hero)
            {
                float since = (_sim.Tick - hero.LastSkillTick) / 60f;
                if (hero.LastSkill == SliceData.GolpeBrutal.Id && since < .25f) { float k = Mathf.Sin(since / .25f * Mathf.Pi); forward = .28f * k; pitch = .3f * k; }
                if (hero.LastSkill == SliceData.Ruptura.Id && since < .4f) { float k = Mathf.Sin(since / .4f * Mathf.Pi); pitch = .45f * k; bob = -.12f * k; }
                bool dashing = hero.DashTicks > 0;
                if (dashing) { pitch = .35f; forward = .1f; }
                _dashTrail.Emitting = dashing && Effects.ParticleScale > 0;
            }
            else if (v.Actor is Enemy enemy)
            {
                bool windup = enemy.State == AiState.Windup;
                v.Ring!.Visible = windup; v.RingFill!.Visible = windup;
                if (windup)
                {
                    float progress = 1f - enemy.StateTicks / (float)SliceData.BruteWindupTicks;
                    v.RingFill.Scale = Vector3.One * Mathf.Max(.05f, progress);
                    pitch = -.32f * progress;                         // leans back, readable before the blow
                    bob = .06f * progress;
                }
                float since = (_sim.Tick - v.LungeTick) / 60f;
                if (since < .3f) { float k = Mathf.Sin(since / .3f * Mathf.Pi); pitch = .5f * k; forward = .35f * k; }
            }
            v.Body.Position = new Vector3(0, bob, forward);
            v.Body.Rotation = new Vector3(pitch, 0, 0);
        }
    }
}
