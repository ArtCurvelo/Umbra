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

    private static NoiseTexture2D Gray(int seed, FastNoiseLite.NoiseTypeEnum type, float frequency, FastNoiseLite.CellularReturnTypeEnum cellular,
                                       bool fractal, bool normal = false, float bump = 8f)
    {
        var noise = new FastNoiseLite
        {
            Seed = seed, NoiseType = type, Frequency = frequency, CellularReturnType = cellular,
            CellularDistanceFunction = FastNoiseLite.CellularDistanceFunctionEnum.Euclidean, CellularJitter = .9f,
            FractalType = fractal ? FastNoiseLite.FractalTypeEnum.Fbm : FastNoiseLite.FractalTypeEnum.None, FractalOctaves = 4,
        };
        var texture = new NoiseTexture2D { Noise = noise, Width = 512, Height = 512, Seamless = true, GenerateMipmaps = true };
        if (normal) { texture.AsNormalMap = true; texture.BumpStrength = bump; }
        return texture;
    }

    private static NoiseTexture2D? _grime;
    private static NoiseTexture2D Grime() => _grime ??= Gray(5, FastNoiseLite.NoiseTypeEnum.SimplexSmooth, .012f, default, true);

    /// <summary>Flagstone floor (shaders/floor.gdshader) with contact darkening from the map's wall proximity.</summary>
    public static ShaderMaterial Floor(Umbra.Domain.World.DungeonMap map)
    {
        const float frequency = .0075f;
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/floor.gdshader") };
        material.SetShaderParameter("stone_cells", Gray(11, FastNoiseLite.NoiseTypeEnum.Cellular, frequency, FastNoiseLite.CellularReturnTypeEnum.CellValue, false));
        material.SetShaderParameter("stone_edges", Gray(11, FastNoiseLite.NoiseTypeEnum.Cellular, frequency, FastNoiseLite.CellularReturnTypeEnum.Distance2Sub, false));
        material.SetShaderParameter("stone_normal", Gray(11, FastNoiseLite.NoiseTypeEnum.Cellular, frequency, FastNoiseLite.CellularReturnTypeEnum.Distance2Sub, false, true, 9f));
        material.SetShaderParameter("grime", Grime());
        material.SetShaderParameter("proximity", Proximity(map));
        material.SetShaderParameter("map_size", new Vector2(map.Width, map.Height));
        return material;
    }

    /// <summary>Ashlar walls (shaders/wall_cutaway.gdshader) with the see-through window around the hero.</summary>
    public static ShaderMaterial Wall()
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/wall_cutaway.gdshader") };
        material.SetShaderParameter("grime", Grime());
        material.SetShaderParameter("normal_tex", Gray(23, FastNoiseLite.NoiseTypeEnum.Cellular, .02f, FastNoiseLite.CellularReturnTypeEnum.Distance2Sub, true, true, 5f));
        return material;
    }

    /// <summary>4 texels per tile: distance from each texel to the nearest wall tile, 0 at the wall, 1 at 1.2 tiles or more.</summary>
    public static ImageTexture Proximity(Umbra.Domain.World.DungeonMap map)
    {
        const int k = 4; const float reach = 1.2f;
        var image = Image.CreateEmpty(map.Width * k, map.Height * k, false, Image.Format.R8);
        for (int py = 0; py < map.Height * k; py++)
            for (int px = 0; px < map.Width * k; px++)
            {
                float x = (px + .5f) / k, y = (py + .5f) / k, best = reach;
                for (int ty = (int)y - 2; ty <= (int)y + 2; ty++)
                    for (int tx = (int)x - 2; tx <= (int)x + 2; tx++)
                    {
                        if (!map.IsWall(tx, ty)) continue;
                        float dx = Math.Max(Math.Max(tx - x, 0), x - (tx + 1)), dy = Math.Max(Math.Max(ty - y, 0), y - (ty + 1));
                        best = Math.Min(best, MathF.Sqrt(dx * dx + dy * dy));
                    }
                image.SetPixel(px, py, new Color(best / reach, 0, 0));
            }
        return ImageTexture.CreateFromImage(image);
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
