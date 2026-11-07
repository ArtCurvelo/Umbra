using Godot;
using Umbra.Domain.World;

namespace Umbra.Game;

/// <summary>
/// The crypt as 3D geometry: floor and walls as instanced boxes in 6x6 chunks (each chunk is lit by its own nearby
/// torches; the Compatibility renderer caps lights per object), torches with flickering omni lights and embers,
/// a faint moonlight for shape, and the environment (ambient, fog, glow, tone mapping).
/// </summary>
public partial class WorldView : Node3D
{
    public const float WallHeight = 2.2f;
    private const int Chunk = 6;

    private readonly List<Torch> _torches = new();
    private ShaderMaterial _wall = null!;
    private float _time;

    public DirectionalLight3D Moon { get; } = new();
    public WorldEnvironment Environment { get; } = new();
    public int ActiveLights { get; private set; }

    private sealed class Torch
    {
        public Node3D Root = null!;
        public OmniLight3D Light = null!;
        public CpuParticles3D Embers = null!;
        public MeshInstance3D Flame = null!;
        public float Phase;
    }

    public void Build(DungeonMap map, Quality.Preset preset, ulong seed)
    {
        var floor = Materials.Floor(map);
        _wall = Materials.Wall();
        _wall.SetShaderParameter("wall_height", WallHeight);
        var floorMesh = new BoxMesh { Size = new Vector3(1, .2f, 1), Material = floor };
        var wallMesh = new BoxMesh { Size = new Vector3(1, WallHeight, 1), Material = _wall };

        if (!LaunchOptions.Skip.Contains("floor")) foreach (var group in map.Floors().GroupBy(c => (c.X / Chunk, c.Y / Chunk)))
            AddChild(Instanced(floorMesh, group.Select(c => new Vector3(c.X + .5f, -.1f, c.Y + .5f)).ToList(), "Chao"));
        if (!LaunchOptions.Skip.Contains("walls")) foreach (var group in map.VisibleWalls().GroupBy(c => (c.X / Chunk, c.Y / Chunk)))
            AddChild(Instanced(wallMesh, group.Select(c => new Vector3(c.X + .5f, WallHeight / 2, c.Y + .5f)).ToList(), "Parede"));

        if (!LaunchOptions.Skip.Contains("torches")) foreach (var (x, y) in map.Torches) AddTorch(map, x, y, preset);
        if (!LaunchOptions.Skip.Contains("props")) AddChild(Props.Build(map, seed, _wall, map.Torches.ToHashSet()));

        Moon.LightColor = new Color(.55f, .62f, .85f);
        Moon.LightEnergy = .22f;
        Moon.RotationDegrees = new Vector3(-62, 25, 0);
        Moon.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal;
        Moon.DirectionalShadowMaxDistance = 30f;
        if (!LaunchOptions.Skip.Contains("moon")) AddChild(Moon);

        Environment.Environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = Materials.Ink,
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(.30f, .32f, .40f),
            AmbientLightEnergy = .22f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic, TonemapExposure = 1.05f,
            FogLightColor = new Color(.06f, .06f, .08f), FogDensity = .035f, FogSkyAffect = 0f,
            GlowIntensity = .55f, GlowBloom = .05f, GlowHdrThreshold = 1.1f,
        };
        if (!LaunchOptions.Skip.Contains("env")) AddChild(Environment);
    }

    private static MultiMeshInstance3D Instanced(Mesh mesh, List<Vector3> positions, string name)
    {
        var multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = positions.Count };
        for (int i = 0; i < positions.Count; i++) multimesh.SetInstanceTransform(i, new Transform3D(Basis.Identity, positions[i]));
        return new MultiMeshInstance3D { Multimesh = multimesh, Name = name };
    }

    private void AddTorch(DungeonMap map, int x, int y, Quality.Preset preset)
    {
        var (dx, dy) = new[] { (0, 1), (0, -1), (1, 0), (-1, 0) }.FirstOrDefault(d => !map.IsWall(x + d.Item1, y + d.Item2));
        var face = new Vector3(dx, 0, dy);
        var root = new Node3D { Position = new Vector3(x + .5f, 0, y + .5f) + face * .56f, Name = "Tocha" };
        root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(.12f, .5f, .12f), Material = Materials.Metal(Materials.Rgb(60, 52, 46)) },
                                           Position = new Vector3(0, 1.45f, 0) - face * .05f, RotationDegrees = new Vector3(dy * 18, 0, -dx * 18) });
        var flame = Props.Flame();
        flame.Position = new Vector3(0, 1.86f, 0) + face * .07f;
        root.AddChild(flame);
        var light = new OmniLight3D
        {
            LightColor = new Color(1f, .58f, .28f), LightEnergy = 1.9f, OmniRange = 6.5f, OmniAttenuation = 1.3f,
            Position = new Vector3(0, 1.9f, 0) + face * .35f, ShadowBias = .08f,
        };
        root.AddChild(light);
        var embers = new CpuParticles3D
        {
            Amount = Math.Max(2, (int)(14 * preset.Particles)), Lifetime = 1.1f, Position = flame.Position,
            Mesh = new QuadMesh { Size = new Vector2(.05f, .05f), Material = Materials.Particle() },
            Direction = Vector3.Up, Spread = 20f, InitialVelocityMin = .3f, InitialVelocityMax = .8f, Gravity = new Vector3(0, .4f, 0),
            ColorRamp = Fade(new Color(1f, .7f, .3f), new Color(.8f, .2f, .05f, 0f)),
        };
        root.AddChild(embers);
        AddChild(root);
        _torches.Add(new Torch { Root = root, Light = light, Embers = embers, Flame = flame, Phase = (x * 7 + y * 13) % 10 });
    }

    public static Gradient Fade(Color from, Color to)
    {
        var g = new Gradient();
        g.Offsets = new[] { 0f, 1f };
        g.Colors = new[] { from, to };
        return g;
    }

    /// <summary>Configurações > Vídeo > Brilho: 50% = the approved exposure (1.05).</summary>
    public void SetBrightness(int percent) => Environment.Environment.TonemapExposure = .65f + percent / 100f * .8f;

    public void ApplyPreset(Quality.Preset preset)
    {
        Moon.ShadowEnabled = preset.MoonShadow;
        var env = Environment.Environment;
        env.FogEnabled = preset.Fog;
        env.GlowEnabled = preset.Glow;
        foreach (var torch in _torches)
        {
            torch.Embers.Visible = preset.Particles > 0;
            torch.Embers.Amount = Math.Max(2, (int)(14 * preset.Particles));
        }
        _preset = preset;
    }

    private Quality.Preset _preset = Quality.Medio;

    /// <summary>Per frame: flicker, keep only the nearest torches lit (preset budget), feed the wall cut-away.</summary>
    public void Tick(double delta, Vector3 hero, Vector3 camera)
    {
        _time += (float)delta;
        var order = _torches.OrderBy(t => t.Root.GlobalPosition.DistanceSquaredTo(hero)).ToList();
        ActiveLights = 0;
        for (int i = 0; i < order.Count; i++)
        {
            var torch = order[i];
            bool on = i < _preset.MaxLights;
            torch.Light.Visible = on;
            torch.Light.ShadowEnabled = on && i < _preset.TorchShadows;
            if (on) ActiveLights++;
            float t = _time * 7f + torch.Phase;
            float flicker = .86f + .08f * Mathf.Sin(t) + .06f * Mathf.Sin(t * 2.3f + 1.7f);
            torch.Light.LightEnergy = 1.9f * flicker;
            torch.Flame.Scale = new Vector3(.9f + .15f * Mathf.Sin(t * 1.7f), .8f + .35f * flicker, 1);
        }
        _wall.SetShaderParameter("hero_position", hero);
        _wall.SetShaderParameter("camera_position", camera);
    }
}
