using Godot;
using Umbra.Domain.Play;
using Umbra.Domain.Skills;
using NVec = System.Numerics.Vector2;

namespace Umbra.Game;

/// <summary>
/// Hero, enemies and drops on screen. Positions are interpolated between the last two ticks (the hero glides inside its
/// tile, enemies step tile to tile as in UMBRA). The UMBRA models have no skeleton yet (assets/models/v080/README.txt),
/// so motion is procedural: walk bob, lunge on attack, wind-up lean, hit flash and a fall on death.
/// </summary>
public partial class ActorLayer : Node3D
{
    /// <summary>The v080 models look down +Z (glTF convention).</summary>
    private const float ModelYaw = 0f;
    private const float HeroRadius = .35f, EnemyRadius = .45f;

    private sealed class View
    {
        public int Id;
        public bool Hero;
        public Node3D Root = null!, Body = null!;
        public ShaderMaterial Skin = null!;
        public MeshInstance3D? Ring, RingFill;
        public NVec Previous, Current;
        public float WalkPhase, FlashTime, DeathTime = -1, Yaw, StepTime;
        public int LungeTick = -1000;
        public bool WasWinding;
        public string Look = "";
    }

    private readonly Dictionary<int, View> _views = new();
    private readonly Dictionary<int, Node3D> _drops = new();
    private GameSession _session = null!;
    private CpuParticles3D _dashTrail = null!, _dust = null!;
    public static bool Dust;

    public static Vector3 World(NVec p, float y = 0) => new(p.X, y, p.Y);

    private static IEnumerable<int> Ids(GameSession s) => new[] { s.HeroId }.Concat(s.Enemies.Select(s.IdOf));

    /// <summary>Same crypt, new fight (benchmark loop, Recomeçar): reuse the models already on screen when the cast is the
    /// same, instead of instancing them again.</summary>
    public bool Rebind(GameSession session)
    {
        var ids = Ids(session).ToList();
        if (ids.Count != _views.Count) return false;
        var views = _views.Values.ToList();
        _views.Clear();
        foreach (var node in _drops.Values) node.QueueFree();
        _drops.Clear();
        _session = session;
        for (int i = 0; i < ids.Count; i++)
        {
            var v = views[i]; bool hero = ids[i] == session.HeroId;
            if (v.Hero != hero || !hero && v.Look != LookOf(session.EnemyById(ids[i])!).Key) return false;
            v.Id = ids[i]; v.Previous = v.Current = session.PositionOf(v.Id); v.DeathTime = -1; v.FlashTime = 0; v.LungeTick = -1000; v.WasWinding = false;
            v.Body.Rotation = Vector3.Zero; v.Body.Position = Vector3.Zero;
            v.Skin.SetShaderParameter("dead", 0f); v.Skin.SetShaderParameter("flash", 0f);
            _views[v.Id] = v;
        }
        return true;
    }

    public void Build(GameSession session)
    {
        _session = session;
        foreach (var child in GetChildren()) child.QueueFree();
        _views.Clear(); _drops.Clear(); _danger.Clear();
        _views[session.HeroId] = MakeView(session.HeroId, true, LaunchOptions.Skip.Contains("hero") ? "none" : "res://assets/models/barbaro.gltf", 1f);
        if (!LaunchOptions.Skip.Contains("enemies"))
            foreach (var e in session.Enemies)
            {
                var look = LookOf(e);
                var view = MakeView(session.IdOf(e), false, "res://assets/models/bruto.gltf", look.Scale);
                view.Look = look.Key; view.Skin.SetShaderParameter("tint", look.Tint);
                if (look.Rim is { } rim) view.Skin.SetShaderParameter("rim_color", rim);
                _views[view.Id] = view;
            }
        foreach (var d in session.Items) AddDrop(d);
        _dashTrail = new CpuParticles3D
        {
            Emitting = false, Amount = 24, Lifetime = .35f, Mesh = new QuadMesh { Size = new Vector2(.12f, .12f), Material = Materials.Particle() },
            Direction = Vector3.Up, Spread = 30, InitialVelocityMin = .2f, InitialVelocityMax = .6f, Gravity = Vector3.Zero,
            ColorRamp = WorldView.Fade(Materials.Rgb(255, 150, 80, .8f), Materials.Rgb(120, 40, 20, 0)),
            Position = new Vector3(0, .8f, 0),
        };
        _views[session.HeroId].Root.AddChild(_dashTrail);
        // Alto: dust motes drifting in the hero's light (world space, so they stay put while the hero walks through).
        _dust = new CpuParticles3D
        {
            Amount = 48, Lifetime = 6f, Preprocess = 6f, LocalCoords = false, Position = new Vector3(0, 1.2f, 0),
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(5, 1.2f, 5),
            Mesh = new QuadMesh { Size = new Vector2(.03f, .03f), Material = Materials.Particle() },
            Direction = Vector3.Up, Spread = 180, InitialVelocityMin = .02f, InitialVelocityMax = .08f, Gravity = new Vector3(0, -.01f, 0),
            ColorRamp = Twinkle(),
        };
        _views[session.HeroId].Root.AddChild(_dust);
        foreach (var v in _views.Values) if (!session.AliveOf(v.Id)) v.DeathTime = 10f;      // fast-forwarded: already lying down
    }

    /// <summary>PROVISÓRIO until the art phase: only the Bruto has a model, so the crypt family wears it with a tint and a
    /// size of its own; the boss is larger and takes its profile colour (boss_catalog.color); elites get a golden rim.</summary>
    private static (string Key, float Scale, Color Tint, Color? Rim) LookOf(Umbra.Domain.Enemies.Enemy e)
    {
        if (e.Boss)
        {
            var c = e.Controller?.Profile.List("color");
            var tint = c is { Count: >= 3 } ? Materials.Rgb((int)Py.Num(c[0]), (int)Py.Num(c[1]), (int)Py.Num(c[2])).Lightened(.25f) : Colors.White;
            return ("boss", 1.6f, tint, Materials.Rgb(255, 140, 60));
        }
        Color? elite = e.Rank == "elite" ? Materials.Rgb(255, 205, 80) : null;
        return e.EnemyType switch
        {
            "sombra" => ("sombra" + e.Rank, .95f, Materials.Rgb(150, 125, 200), elite),
            "espectro" => ("espectro" + e.Rank, 1.05f, Materials.Rgb(165, 205, 255), elite),
            "cultista" => ("cultista" + e.Rank, 1f, Materials.Rgb(255, 150, 120), elite),
            _ => (e.EnemyType + e.Rank, 1.12f, Colors.White, elite),
        };
    }

    // ---------------------------------------------------------------- cells about to be hit
    private readonly List<MeshInstance3D> _danger = new();

    /// <summary>The cells an attack in preparation will hit (GameSession.Danger), as red tiles that fill in as the wind-up
    /// runs out: the marked cell of an enemy, the boss's blow and ability area, its corruption zones.</summary>
    private void SyncDanger()
    {
        var cells = new Dictionary<(int X, int Y), float>();
        foreach (var (cell, progress) in _session.Danger()) cells[cell] = Math.Max(cells.GetValueOrDefault(cell), progress);
        int i = 0;
        foreach (var (cell, progress) in cells)
        {
            if (i == _danger.Count)
            {
                var quad = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(.94f, .94f) }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                AddChild(quad); _danger.Add(quad);
            }
            var q = _danger[i++];
            q.Position = World(GameSession.ToWorld(cell), .025f);
            q.Visible = true;
            q.MaterialOverride = DangerShade(progress);
        }
        for (; i < _danger.Count; i++) _danger[i].Visible = false;
    }

    private readonly Dictionary<int, StandardMaterial3D> _dangerShades = new();

    /// <summary>Ten steps of red, from faint to strong, shared by every tile (no material per frame).</summary>
    private StandardMaterial3D DangerShade(float progress)
    {
        int step = Math.Clamp((int)(progress * 10), 0, 10);
        if (_dangerShades.TryGetValue(step, out var m)) return m;
        m = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = Materials.Rgb(230, 40, 30, .18f + .42f * step / 10f), CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        return _dangerShades[step] = m;
    }

    private static Gradient Twinkle()
    {
        var g = new Gradient();
        g.Offsets = new[] { 0f, .3f, .7f, 1f };
        g.Colors = new[] { new Color(1, .85f, .6f, 0), new Color(1, .85f, .6f, .35f), new Color(1, .85f, .6f, .35f), new Color(1, .85f, .6f, 0) };
        return g;
    }

    private View MakeView(int id, bool hero, string model, float scale)
    {
        var root = new Node3D { Name = hero ? "Heroi" : "Inimigo" + id };
        var body = new Node3D();
        root.AddChild(body);
        // One shader for the whole model: vertex colours, a rim of light (warm for the hero, cold red for enemies),
        // hit flash and darkening after death; no second overlay pass.
        var skin = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/actor.gdshader") };
        skin.SetShaderParameter("rim_color", hero ? new Color(1f, .78f, .55f) : new Color(.85f, .3f, .28f));
        skin.SetShaderParameter("rim_strength", hero ? .6f : .45f);
        var scene = ResourceLoader.Exists(model) ? ResourceLoader.Load<PackedScene>(model) : null;
        Node3D mesh = scene?.Instantiate<Node3D>() ?? new MeshInstance3D { Mesh = new CapsuleMesh { Radius = .3f, Height = 1.7f }, Position = new Vector3(0, .85f, 0) };
        mesh.Scale = Vector3.One * scale;
        mesh.RotationDegrees = new Vector3(0, ModelYaw, 0);
        body.AddChild(mesh);
        foreach (var m in Meshes(mesh))
            for (int s = 0; s < m.Mesh.GetSurfaceCount(); s++) m.SetSurfaceOverrideMaterial(s, skin);
        float radius = hero ? HeroRadius : EnemyRadius;
        var blob = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = Vector2.One * radius * 3.4f, Material = Materials.Blob() },
            Position = new Vector3(0, .015f, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        root.AddChild(blob);
        var position = _session.PositionOf(id);
        var view = new View { Id = id, Hero = hero, Root = root, Body = body, Skin = skin, Previous = position, Current = position };
        if (!hero)
        {
            view.Ring = Disc(Materials.Rgb(220, 50, 40, .9f), true);
            view.RingFill = Disc(Materials.Rgb(200, 40, 30, .45f), false);
            root.AddChild(view.Ring); root.AddChild(view.RingFill);
        }
        if (hero && !LaunchOptions.Skip.Contains("herolight"))
        {
            // The hero carries a warm light radius: the crypt is lit around them, as in the dark ARPGs of the 2000s.
            root.AddChild(new OmniLight3D
            {
                Name = "LuzDoHeroi", LightColor = new Color(1f, .82f, .62f), LightEnergy = .85f, OmniRange = 6.5f, OmniAttenuation = 1.6f,
                Position = new Vector3(0, 2.4f, 0), ShadowEnabled = false,
            });
        }
        var facing = _session.FacingOf(id);
        view.Yaw = Mathf.Atan2(facing.X, facing.Y);
        AddChild(root);
        root.Position = World(position);
        return view;
    }

    private static IEnumerable<MeshInstance3D> Meshes(Node node)
    {
        if (node is MeshInstance3D m && m.Mesh != null) yield return m;
        foreach (var child in node.GetChildren()) foreach (var inner in Meshes(child)) yield return inner;
    }

    /// <summary>Ground marker of a wind-up: the outline covers the tiles the blow reaches, the fill grows until it lands.</summary>
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
        const float size = 3f;                                  // the eight neighbouring tiles
        return new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(size, size), Material = material }, Position = new Vector3(0, .03f, 0), Visible = false,
                                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
    }

    private static readonly Dictionary<Color, (Mesh Box, Mesh Beam)> DropMeshes = new();

    /// <summary>Box and light beam of each colour, created once and shared by every drop of that colour.</summary>
    private static (Mesh Box, Mesh Beam) DropMesh(Color color)
    {
        if (DropMeshes.TryGetValue(color, out var meshes)) return meshes;
        meshes = (new BoxMesh { Size = new Vector3(.32f, .14f, .22f), Material = Materials.Metal(color.Darkened(.35f)) },
                  new CylinderMesh { TopRadius = .01f, BottomRadius = .09f, Height = 1.8f, Material = Beam(color) });
        return DropMeshes[color] = meshes;
    }

    public void AddDrop(GroundItem item)
    {
        var (box, beamMesh) = DropMesh(Materials.RarityOf(item.Data));
        var root = new Node3D { Position = World(GameSession.ToWorld(item.Cell)) };
        root.AddChild(new MeshInstance3D { Mesh = box, Position = new Vector3(0, .08f, 0), RotationDegrees = new Vector3(0, 30, 0) });
        if (item.Kind is "weapon" or "armor" or "accessory")     // equipment carries a beam of its rarity colour
            root.AddChild(new MeshInstance3D { Mesh = beamMesh, Position = new Vector3(0, .8f, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        AddChild(root);
        _drops[item.Id] = root;
    }

    private static StandardMaterial3D Beam(Color color)
    {
        var m = Materials.Flat(new Color(color, .22f), true);
        m.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
        return m;
    }

    private readonly List<(Node3D Node, int Frames)> _warm = new();

    /// <summary>Loading: one drop of each rarity drawn (under the hero, a few frames) so the GPU compiles their shaders and
    /// .NET compiles the code before the fight; the first real drop then costs nothing extra.</summary>
    public void Prewarm()
    {
        int id = -1;
        foreach (var rarity in new[] { "Comum", "Mágico", "Raro", "Lendário" })
        {
            AddDrop(new GroundItem { Id = id, Kind = "weapon", Data = new PyDict { ["rarity"] = rarity }, Cell = _session.Hero.Position });
            _warm.Add((_drops[id], 3)); _drops.Remove(id--);
        }
    }

    public void RemoveDrop(GroundItem item)
    {
        if (_drops.Remove(item.Id, out var node)) node.QueueFree();
    }

    /// <summary>Called right before each tick.</summary>
    public void BeforeStep()
    {
        foreach (var v in _views.Values) if (v.Hero) v.Previous = v.Current;
    }

    public void AfterStep()
    {
        foreach (var v in _views.Values)
        {
            var now = _session.PositionOf(v.Id);
            if (!v.Hero && now != v.Current) { v.Previous = v.Current; v.StepTime = 0; }
            v.Current = now;
            bool winding = !v.Hero && _session.WindupOf(v.Id) != null;
            if (v.WasWinding && !winding && _session.AliveOf(v.Id)) v.LungeTick = _session.Tick;   // the blow lands
            v.WasWinding = winding;
        }
    }

    public void OnHit(int actorId) { if (_views.TryGetValue(actorId, out var v)) v.FlashTime = .12f; }
    public void OnDied(int actorId) { if (_views.TryGetValue(actorId, out var v)) v.DeathTime = 0; }

    public Vector3 Interpolated(int id, float alpha) =>
        _views.TryGetValue(id, out var v) ? World(NVec.Lerp(v.Previous, v.Current, alpha)) : World(_session.PositionOf(id));

    /// <summary>Enemies change tile in one tick; the model slides there over a short step, as UMBRA draws it.</summary>
    private const float EnemyStep = .16f;

    public void Sync(float alpha, double delta)
    {
        float dt = (float)delta;
        SyncDanger();
        if (_dust != null) _dust.Emitting = Dust && Effects.ParticleScale > 0;
        for (int i = _warm.Count - 1; i >= 0; i--)
        {
            if (_warm[i].Frames <= 0) { _warm[i].Node.QueueFree(); _warm.RemoveAt(i); }
            else _warm[i] = (_warm[i].Node, _warm[i].Frames - 1);
        }
        foreach (var v in _views.Values)
        {
            NVec at; float speed;
            if (v.Hero) { at = NVec.Lerp(v.Previous, v.Current, alpha); speed = (v.Current - v.Previous).Length() * 60f; }
            else
            {
                bool stepping = v.StepTime < EnemyStep && v.Previous != v.Current;
                v.StepTime += dt;
                at = NVec.Lerp(v.Previous, v.Current, Mathf.Clamp(v.StepTime / EnemyStep, 0, 1));
                speed = stepping ? 4f : 0f;
            }
            v.Root.Position = World(at);
            var facing = _session.FacingOf(v.Id);
            float targetYaw = Mathf.Atan2(facing.X, facing.Y);
            v.Yaw = Mathf.LerpAngle(v.Yaw, targetYaw, 1 - Mathf.Exp(-dt * 16));
            v.Root.Rotation = new Vector3(0, v.Yaw, 0);
            v.FlashTime = Mathf.Max(0, v.FlashTime - dt);
            v.Skin.SetShaderParameter("flash", v.FlashTime > 0 ? 1.6f * Effects.FlashScale * v.FlashTime / .12f : 0f);

            if (v.DeathTime >= 0)
            {
                v.DeathTime += dt;
                float fall = Mathf.Clamp(v.DeathTime / .45f, 0, 1);
                v.Body.Rotation = new Vector3(Mathf.Pi / 2 * fall * fall, 0, 0);
                v.Body.Position = new Vector3(0, -.15f * fall, -.1f * fall);
                v.Skin.SetShaderParameter("dead", Mathf.Clamp((v.DeathTime - .3f) / 1.5f, 0, .85f));
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
            if (v.Hero)
            {
                float since = (_session.Tick - _session.LastCastTick) / 60f;
                bool strike = _session.LastCast is not ("barb2_ruptura" or "barb2_investida");
                if (strike && since < .25f) { float k = Mathf.Sin(since / .25f * Mathf.Pi); forward = .28f * k; pitch = .3f * k; }
                if (_session.LastCast == "barb2_ruptura" && since < .4f) { float k = Mathf.Sin(since / .4f * Mathf.Pi); pitch = .45f * k; bob = -.12f * k; }
                bool dashing = _session.Dashing;
                if (dashing) { pitch = .35f; forward = .1f; }
                _dashTrail.Emitting = dashing && Effects.ParticleScale > 0;
            }
            else
            {
                var progress = _session.WindupOf(v.Id);
                v.Ring!.Visible = progress != null; v.RingFill!.Visible = progress != null;
                if (progress is { } p)
                {
                    v.RingFill.Scale = Vector3.One * Mathf.Max(.05f, p);
                    pitch = -.32f * p;                         // leans back, readable before the blow
                    bob = .06f * p;
                }
                float since = (_session.Tick - v.LungeTick) / 60f;
                if (since < .3f) { float k = Mathf.Sin(since / .3f * Mathf.Pi); pitch = .5f * k; forward = .35f * k; }
            }
            if (v.FlashTime > 0) { float k = v.FlashTime / .12f; pitch -= .22f * k; forward -= .08f * k; }   // flinch on hit
            v.Body.Position = new Vector3(0, bob, forward);
            v.Body.Rotation = new Vector3(pitch, 0, 0);
        }
    }
}
