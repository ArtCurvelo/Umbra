using Godot;

namespace Umbra.Game;

/// <summary>
/// Short-lived visual effects. Only presentation: every effect is spawned from a simulation event and never feeds back
/// into rules. Particle counts follow the quality preset; nothing about hits, targets or ranges does.
/// </summary>
public partial class Effects : Node3D
{
    public static float ParticleScale = 1f;

    private sealed class Fade
    {
        public Node3D Node = null!;
        public StandardMaterial3D? Material;
        public Color Color;
        public float Age, Life;
        public Vector3 GrowFrom = Vector3.One, GrowTo = Vector3.One;
    }

    private readonly List<Fade> _fades = new();
    private readonly List<CpuParticles3D> _bursts = new();
    private int _next;

    public override void _Ready()
    {
        for (int i = 0; i < 12; i++)
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

    public void Burst(Vector3 at, Color color, int amount = 16, float speed = 1f)
    {
        if (ParticleScale <= 0) return;
        var p = _bursts[_next++ % _bursts.Count];
        p.Amount = Math.Max(3, (int)(amount * ParticleScale));
        p.InitialVelocityMin = 1.5f * speed; p.InitialVelocityMax = 3.4f * speed;
        p.ColorRamp = WorldView.Fade(color, new Color(color, 0f));
        p.GlobalPosition = at;
        p.Restart();
        p.Emitting = true;
    }

    /// <summary>Golpe Brutal: a pale crescent in front of the hero.</summary>
    public void Swing(Vector3 at, Vector3 dir, float reach, float arcDegrees)
    {
        var mesh = Fan(reach * .55f, reach + .4f, arcDegrees, 12);
        Add(mesh, at + new Vector3(0, .95f, 0), dir, Materials.Rgb(236, 222, 196, .55f), .16f);
    }

    /// <summary>Ruptura: the ground cracks open in a cone of embers.</summary>
    public void Cone(Vector3 at, Vector3 dir, float reach, float arcDegrees)
    {
        var mesh = Fan(.3f, reach + .5f, arcDegrees, 16);
        Add(mesh, at + new Vector3(0, .04f, 0), dir, Materials.Rgb(255, 120, 40, .75f), .42f, new Vector3(.4f, 1, .4f));
        for (int i = 1; i <= 3; i++) Burst(at + dir * (reach * i / 3f) + new Vector3(0, .1f, 0), Materials.Rgb(170, 140, 110), 10, .6f);
    }

    private void Add(Mesh mesh, Vector3 at, Vector3 dir, Color color, float life, Vector3? growFrom = null)
    {
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = color, VertexColorUseAsAlbedo = false,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        var node = new MeshInstance3D { Mesh = mesh, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(node);
        node.GlobalPosition = at;
        node.Rotation = new Vector3(0, Mathf.Atan2(dir.X, dir.Z), 0);
        _fades.Add(new Fade { Node = node, Material = material, Color = color, Life = life, GrowFrom = growFrom ?? Vector3.One });
    }

    /// <summary>Flat ring sector on the XZ plane, centred on +Z.</summary>
    private static ArrayMesh Fan(float inner, float outer, float arcDegrees, int segments)
    {
        var verts = new List<Vector3>();
        float half = Mathf.DegToRad(arcDegrees) / 2;
        for (int i = 0; i < segments; i++)
        {
            float a0 = -half + 2 * half * i / segments, a1 = -half + 2 * half * (i + 1) / segments;
            Vector3 P(float r, float a) => new(Mathf.Sin(a) * r, 0, Mathf.Cos(a) * r);
            verts.AddRange(new[] { P(inner, a0), P(outer, a0), P(outer, a1), P(inner, a0), P(outer, a1), P(inner, a1) });
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    public override void _Process(double delta)
    {
        for (int i = _fades.Count - 1; i >= 0; i--)
        {
            var f = _fades[i];
            f.Age += (float)delta;
            float t = Mathf.Clamp(f.Age / f.Life, 0, 1);
            if (f.Material != null) f.Material.AlbedoColor = new Color(f.Color, f.Color.A * (1 - t));
            f.Node.Scale = f.GrowFrom.Lerp(f.GrowTo, Mathf.Min(1, t * 3));
            if (t >= 1) { f.Node.QueueFree(); _fades.RemoveAt(i); }
        }
    }
}
