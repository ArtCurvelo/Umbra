using Godot;

namespace Umbra.Game;

/// <summary>
/// Short-lived visual effects. Only presentation: every effect is spawned from a simulation event and never feeds back
/// into rules. Particle counts and impact lights follow the quality preset; hits, targets and ranges never do.
/// Language (docs/GUIA_VISUAL.md): bone-white for the hero's steel, embers for Fúria skills, dark red for blood,
/// grey dust for stone; every flash is short (≤ 0,2 s) so the screen stays readable.
/// </summary>
public partial class Effects : Node3D
{
    public static float ParticleScale = 1f;
    public static bool ImpactLights = true;
    /// <summary>Screenshots only (--fx): every effect is held at this fraction of its life.</summary>
    public static float? Hold;

    private sealed class Fade
    {
        public Node3D Node = null!;
        public StandardMaterial3D? Material;
        public OmniLight3D? Light;
        public Color Color;
        public float Age, Life, Energy;
        public Vector3 GrowFrom = Vector3.One, GrowTo = Vector3.One;
        public string? Pool;
    }

    // Pools: effect meshes are built once per shape, effect nodes (with their own material) and lights are reused,
    // so a swing costs no allocation, no mesh upload and no new material (measured spikes before: 5-30 ms).
    private static readonly Dictionary<string, ArrayMesh> Meshes = new();
    private readonly Dictionary<string, Stack<MeshInstance3D>> _idle = new();
    private readonly Stack<OmniLight3D> _idleLights = new();

    private readonly List<Fade> _fades = new();
    private readonly List<CpuParticles3D> _bursts = new();
    private int _next;
    private static Texture2D? _crack;

    public override void _Ready()
    {
        for (int i = 0; i < 16; i++)
        {
            var p = new CpuParticles3D
            {
                Emitting = false, OneShot = true, Explosiveness = .95f, Lifetime = .55f, Amount = 16,
                Mesh = new QuadMesh { Size = new Vector2(.07f, .07f), Material = Materials.Particle() },
                Direction = Vector3.Up, Spread = 70f, InitialVelocityMin = 1.5f, InitialVelocityMax = 3.4f,
                Gravity = new Vector3(0, -7f, 0), ScaleAmountMin = .6f, ScaleAmountMax = 1.3f,
            };
            AddChild(p); _bursts.Add(p);
        }
    }

    public void Burst(Vector3 at, Color color, int amount = 16, float speed = 1f, float gravity = -7f, float size = 1f)
    {
        if (ParticleScale <= 0) return;
        var p = _bursts[_next++ % _bursts.Count];
        p.Amount = Math.Max(3, (int)(amount * ParticleScale));
        p.InitialVelocityMin = 1.5f * speed; p.InitialVelocityMax = 3.4f * speed;
        p.Gravity = new Vector3(0, gravity, 0);
        p.ScaleAmountMin = .6f * size; p.ScaleAmountMax = 1.3f * size;
        p.ColorRamp = WorldView.Fade(color, new Color(color, 0f));
        p.GlobalPosition = at;
        p.Restart();
        p.Emitting = true;
    }

    /// <summary>Loading: every effect once, invisible (alpha and light at 0), so shaders and code are compiled before the
    /// fight instead of in the frame of the first hit.</summary>
    public void Prewarm(Vector3 at)
    {
        bool lights = ImpactLights; ImpactLights = true;
        int first = _fades.Count;
        Swing(at, Vector3.Forward, 1f, 90f); Cone(at, Vector3.Forward, 2f, 90f); Impact(at, true); Death(at); Dash(at);
        ImpactLights = lights;
        for (int i = first; i < _fades.Count; i++)
        {
            var f = _fades[i];
            f.Color = new Color(f.Color, 0f); f.Energy = 0; f.Life = .1f;
            if (f.Material != null) f.Material.AlbedoColor = f.Color;
            if (f.Light != null) f.Light.LightEnergy = 0;
        }
        foreach (var p in _bursts) p.Emitting = false;
    }

    /// <summary>Hit on an enemy: steel sparks and a spray of dark blood.</summary>
    public void Impact(Vector3 at, bool onHero)
    {
        Burst(at, onHero ? Materials.Rgb(230, 70, 60) : Materials.Rgb(255, 200, 140), 14, 1f);
        Burst(at - new Vector3(0, .2f, 0), Materials.Rgb(110, 14, 16), 12, .7f, -12f, 1.4f);
        if (onHero) Light(at, new Color(1f, .3f, .2f), 1.2f, 3f, .12f);
    }

    /// <summary>Golpe Brutal: a crescent of pale steel, bright at the edge, fading inward and at both tips.</summary>
    public void Swing(Vector3 at, Vector3 dir, float reach, float arcDegrees)
    {
        var mesh = Fan(reach * .78f, reach + .3f, arcDegrees, 16, true);
        Add(mesh, at + new Vector3(0, 1.0f, 0), dir, Materials.Rgb(255, 236, 205, .75f), .17f, vertexColors: true);
    }

    /// <summary>Ruptura: the stone splits in a cone of glowing cracks, dust is thrown up, a short flash of ember light.</summary>
    public void Cone(Vector3 at, Vector3 dir, float reach, float arcDegrees)
    {
        var shock = Fan(.3f, reach + .5f, arcDegrees, 16, true);
        Add(shock, at + new Vector3(0, .05f, 0), dir, Materials.Rgb(255, 130, 50, .8f), .3f, new Vector3(.35f, 1, .35f), vertexColors: true);
        var cracks = Fan(.25f, reach + .55f, arcDegrees, 16, false);
        Add(cracks, at + new Vector3(0, .03f, 0), dir, Materials.Rgb(255, 120, 40, 1f), 1.1f, texture: Crack());
        for (int i = 1; i <= 3; i++) Burst(at + dir * (reach * i / 3f) + new Vector3(0, .1f, 0), Materials.Rgb(150, 132, 112), 12, .55f, -4f, 1.8f);
        Light(at + dir * reach * .6f + new Vector3(0, .6f, 0), new Color(1f, .5f, .2f), 2.4f, 5f, .2f);
    }

    /// <summary>Investida: a puff of dust where the charge starts.</summary>
    public void Dash(Vector3 at) => Burst(at + new Vector3(0, .15f, 0), Materials.Rgb(140, 126, 110), 18, .5f, -2f, 2f);

    public void Death(Vector3 at) => Burst(at + new Vector3(0, .3f, 0), Materials.Rgb(120, 110, 100), 26, .6f, -5f, 1.6f);

    private void Light(Vector3 at, Color color, float energy, float range, float life)
    {
        if (!ImpactLights) return;
        var light = _idleLights.Count > 0 ? _idleLights.Pop() : NewLight();
        light.LightColor = color; light.LightEnergy = energy; light.OmniRange = range; light.Visible = true;
        light.GlobalPosition = at;
        _fades.Add(new Fade { Node = light, Light = light, Energy = energy, Life = life });
    }

    private OmniLight3D NewLight()
    {
        var light = new OmniLight3D { ShadowEnabled = false };
        AddChild(light);
        return light;
    }

    private void Add(Mesh mesh, Vector3 at, Vector3 dir, Color color, float life, Vector3? growFrom = null, bool vertexColors = false, Texture2D? texture = null)
    {
        string pool = $"{mesh.GetInstanceId()}|{vertexColors}|{texture?.GetInstanceId()}";
        if (!_idle.TryGetValue(pool, out var stack)) _idle[pool] = stack = new();
        MeshInstance3D node;
        if (stack.Count > 0) node = stack.Pop();
        else
        {
            var fresh = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = vertexColors,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled, AlbedoTexture = texture,
            };
            node = new MeshInstance3D { Mesh = mesh, MaterialOverride = fresh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(node);
        }
        var material = (StandardMaterial3D)node.MaterialOverride;
        material.AlbedoColor = color;
        node.Visible = true;
        node.Scale = growFrom ?? Vector3.One;
        node.GlobalPosition = at;
        node.Rotation = new Vector3(0, Mathf.Atan2(dir.X, dir.Z), 0);
        _fades.Add(new Fade { Node = node, Material = material, Color = color, Life = life, GrowFrom = growFrom ?? Vector3.One, Pool = pool });
    }

    /// <summary>Flat ring sector on the XZ plane centred on +Z. Vertex alpha: bright at the outer edge, 0 inside and at the
    /// tips (when <paramref name="soft"/>). UVs follow world-ish units, for the crack texture.</summary>
    private static ArrayMesh Fan(float inner, float outer, float arcDegrees, int segments, bool soft)
    {
        string key = $"{inner:0.###}|{outer:0.###}|{arcDegrees}|{segments}|{soft}";
        if (Meshes.TryGetValue(key, out var cached)) return cached;
        var verts = new List<Vector3>(); var colors = new List<Color>(); var uvs = new List<Vector2>();
        float half = Mathf.DegToRad(arcDegrees) / 2;
        void V(float r, float a, float alpha)
        {
            var p = new Vector3(Mathf.Sin(a) * r, 0, Mathf.Cos(a) * r);
            verts.Add(p); uvs.Add(new Vector2(p.X, p.Z) * .5f + new Vector2(.5f, 0));
            colors.Add(new Color(1, 1, 1, alpha));
        }
        for (int i = 0; i < segments; i++)
        {
            float t0 = i / (float)segments, t1 = (i + 1) / (float)segments;
            float a0 = -half + 2 * half * t0, a1 = -half + 2 * half * t1;
            float e0 = soft ? Mathf.Sin(t0 * Mathf.Pi) : 1, e1 = soft ? Mathf.Sin(t1 * Mathf.Pi) : 1;
            float innerA = soft ? 0 : 1;
            V(inner, a0, innerA * e0); V(outer, a0, e0); V(outer, a1, e1);
            V(inner, a0, innerA * e0); V(outer, a1, e1); V(inner, a1, innerA * e1);
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return Meshes[key] = mesh;
    }

    /// <summary>Glowing crack lines: thin ridges of a cellular noise (Voronoi edges), transparent elsewhere.</summary>
    private static Texture2D Crack()
    {
        if (_crack != null) return _crack;
        var noise = new FastNoiseLite { Seed = 9, NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular, Frequency = .05f,
                                        CellularReturnType = FastNoiseLite.CellularReturnTypeEnum.Distance2Sub };
        var texture = new NoiseTexture2D { Noise = noise, Width = 256, Height = 256, Seamless = true };
        var ramp = new Gradient();
        ramp.Offsets = new[] { 0f, .05f, .12f };
        ramp.Colors = new[] { new Color(1, .85f, .6f, 1), new Color(1, .5f, .2f, .7f), new Color(1, .4f, .1f, 0) };
        texture.ColorRamp = ramp;
        return _crack = texture;
    }

    public override void _Process(double delta)
    {
        for (int i = _fades.Count - 1; i >= 0; i--)
        {
            var f = _fades[i];
            f.Age = Hold is { } hold ? f.Life * hold : f.Age + (float)delta;
            float t = Mathf.Clamp(f.Age / f.Life, 0, 1);
            if (f.Material != null) f.Material.AlbedoColor = new Color(f.Color, f.Color.A * (1 - t * t));
            if (f.Light != null) f.Light.LightEnergy = f.Energy * (1 - t);
            else f.Node.Scale = f.GrowFrom.Lerp(f.GrowTo, Mathf.Min(1, t * 3));
            if (t >= 1)
            {
                f.Node.Visible = false;
                if (f.Light != null) _idleLights.Push(f.Light);
                else if (f.Pool != null) _idle[f.Pool].Push((MeshInstance3D)f.Node);
                _fades.RemoveAt(i);
            }
        }
    }
}
