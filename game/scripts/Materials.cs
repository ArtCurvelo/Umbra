using Godot;

namespace Umbra.Game;

/// <summary>
/// Placeholder dark-fantasy materials, all procedural (Godot noise textures): flagstone floor, rough wall stone,
/// emissive flame, unshaded markers. No external texture or third-party asset (Game Director decision D2).
/// </summary>
public static class Materials
{
    public static readonly Color Ink = new(0.02f, 0.02f, 0.03f);
    public static readonly Color Bone = Rgb(236, 222, 196), Ash = Rgb(150, 142, 132), Gold = Rgb(220, 176, 98);
    public static readonly Color Ember = Rgb(190, 82, 44), Blood = Rgb(168, 36, 40);
    public static readonly Color IronDark = Rgb(20, 18, 22), Iron = Rgb(46, 42, 48), IronLight = Rgb(98, 90, 86);

    /// <summary>Rarity colours of UMBRA's default palette (game/accessibility/palette.py).</summary>
    public static Color Rarity(Umbra.Domain.Gameplay.Rarity r) => r switch
    {
        Umbra.Domain.Gameplay.Rarity.Comum => Rgb(196, 196, 200),
        Umbra.Domain.Gameplay.Rarity.Magico => Rgb(70, 140, 255),
        Umbra.Domain.Gameplay.Rarity.Raro => Rgb(250, 222, 70),
        _ => Rgb(255, 128, 32),
    };

    public static Color Rgb(int r, int g, int b, float a = 1f) => new(r / 255f, g / 255f, b / 255f, a);

    private static NoiseTexture2D Noise(int seed, float frequency, FastNoiseLite.CellularReturnTypeEnum cellular, Color dark, Color light,
                                        bool normal, float bump = 6f, float darkStop = .08f)
    {
        var noise = new FastNoiseLite
        {
            Seed = seed, NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular, Frequency = frequency,
            CellularReturnType = cellular, FractalType = FastNoiseLite.FractalTypeEnum.Fbm, FractalOctaves = 2, FractalGain = .35f,
        };
        var texture = new NoiseTexture2D { Noise = noise, Width = 512, Height = 512, Seamless = true, GenerateMipmaps = true };
        if (normal) { texture.AsNormalMap = true; texture.BumpStrength = bump; }
        else
        {
            var ramp = new Gradient();
            ramp.Offsets = new[] { 0f, darkStop, 1f };
            ramp.Colors = new[] { dark * .45f, dark, light };
            texture.ColorRamp = ramp;
        }
        return texture;
    }

    public static Material Floor()
    {
        var albedo = Noise(11, .0075f, FastNoiseLite.CellularReturnTypeEnum.Distance2Sub, Rgb(38, 36, 38), Rgb(92, 86, 80), false, darkStop: .06f);
        var normal = Noise(11, .0075f, FastNoiseLite.CellularReturnTypeEnum.Distance2Sub, default, default, true, 9f);
        return new StandardMaterial3D
        {
            AlbedoTexture = albedo, NormalEnabled = true, NormalTexture = normal, NormalScale = .9f,
            Roughness = .95f, Uv1Triplanar = true, Uv1WorldTriplanar = true, Uv1Scale = new Vector3(.5f, .5f, .5f),
        };
    }

    public static ShaderMaterial Wall()
    {
        var albedo = Noise(23, .012f, FastNoiseLite.CellularReturnTypeEnum.Distance2Sub, Rgb(44, 40, 40), Rgb(104, 96, 88), false, darkStop: .1f);
        var normal = Noise(23, .012f, FastNoiseLite.CellularReturnTypeEnum.Distance2Sub, default, default, true, 7f);
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/wall_cutaway.gdshader") };
        material.SetShaderParameter("albedo_tex", albedo);
        material.SetShaderParameter("normal_tex", normal);
        material.SetShaderParameter("tint", new Color(1f, .96f, .92f));
        return material;
    }

    public static StandardMaterial3D Glow(Color color, float energy = 3f) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = color,
        EmissionEnabled = true, Emission = color, EmissionEnergyMultiplier = energy,
    };

    public static StandardMaterial3D Flat(Color color, bool transparent = false, bool noDepth = false) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = color,
        Transparency = transparent ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
        NoDepthTest = noDepth, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };

    public static StandardMaterial3D Metal(Color color) => new() { AlbedoColor = color, Metallic = .7f, Roughness = .45f };

    /// <summary>Particle quad: camera-facing, coloured by the particle system.</summary>
    public static StandardMaterial3D Particle(bool additive = true) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true,
        BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = additive ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
        AlbedoTexture = SoftDot(),
    };

    private static GradientTexture2D? _softDot;
    public static GradientTexture2D SoftDot()
    {
        if (_softDot != null) return _softDot;
        var g = new Gradient();
        g.Offsets = new[] { 0f, 1f };
        g.Colors = new[] { new Color(1, 1, 1, 1), new Color(1, 1, 1, 0) };
        return _softDot = new GradientTexture2D
        {
            Gradient = g, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(.5f, .5f), FillTo = new Vector2(1f, .5f),
            Width = 64, Height = 64,
        };
    }

    /// <summary>Soft dark disc under every actor: the only shadow on the low preset, a contact shadow on the others.</summary>
    public static StandardMaterial3D Blob()
    {
        var g = new Gradient();
        g.Offsets = new[] { 0f, 1f };
        g.Colors = new[] { new Color(0, 0, 0, .62f), new Color(0, 0, 0, 0) };
        var texture = new GradientTexture2D
        {
            Gradient = g, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(.5f, .5f), FillTo = new Vector2(1f, .5f),
            Width = 64, Height = 64,
        };
        return new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoTexture = texture,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = Colors.White,
        };
    }
}
