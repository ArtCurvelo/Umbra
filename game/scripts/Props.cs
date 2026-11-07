using Godot;
using Umbra.Domain.Core;
using Umbra.Domain.World;

namespace Umbra.Game;

/// <summary>
/// Set dressing of the crypt, visual only (no collision, so the rules do not change): pillars set into the walls,
/// rubble at the foot of the walls, scattered bones and skulls, and stains on the floor. Placement is seeded by the
/// map, so the same crypt always looks the same. Everything is instanced (one draw per kind and chunk of the map).
/// </summary>
public static class Props
{
    public static Node3D Build(DungeonMap map, ulong seed, Material wall, HashSet<(int, int)> torches)
    {
        var root = new Node3D { Name = "Cenario" };
        var rng = new Rng(seed * 2654435761UL + 17);
        var pillars = new List<Transform3D>(); var caps = new List<Transform3D>();
        var rubble = new List<Transform3D>(); var bones = new List<Transform3D>(); var skulls = new List<Transform3D>();
        var stains = new List<Transform3D>();
        var start = ((int)map.Start.X, (int)map.Start.Y);

        foreach (var (x, y) in map.VisibleWalls())
        {
            var open = new[] { (0, 1), (0, -1), (1, 0), (-1, 0) }.Where(d => !map.IsWall(x + d.Item1, y + d.Item2)).ToList();
            if (open.Count != 1 || torches.Contains((x, y))) continue;                 // straight wall pieces only
            if ((x * 3 + y * 5) % 4 != 0) continue;                                   // a rhythm, not every block
            var (dx, dy) = open[0];
            var at = new Vector3(x + .5f + dx * .12f, 0, y + .5f + dy * .12f);
            pillars.Add(new Transform3D(Basis.Identity.Scaled(new Vector3(.62f, 1f, .62f)), at + new Vector3(0, 1.25f, 0)));
            caps.Add(new Transform3D(Basis.Identity.Scaled(new Vector3(.82f, 1f, .82f)), at + new Vector3(0, .12f, 0)));
            caps.Add(new Transform3D(Basis.Identity.Scaled(new Vector3(.82f, 1f, .82f)), at + new Vector3(0, 2.38f, 0)));
        }

        foreach (var (x, y) in map.Floors())
        {
            if ((x, y) == start) continue;
            bool byWall = DungeonMap.Neighbours8(x, y).Any(n => map.IsWall(n.X, n.Y));
            if (byWall && rng.Chance(.22))
            {
                int pieces = rng.Range(3, 6);
                var wallDir = DungeonMap.Neighbours8(x, y).First(n => map.IsWall(n.X, n.Y));
                var centre = new Vector3(x + .5f + (wallDir.X - x) * .3f, 0, y + .5f + (wallDir.Y - y) * .3f);
                for (int i = 0; i < pieces; i++)
                {
                    float s = .08f + (float)rng.NextDouble() * .16f;
                    var basis = new Basis(new Vector3((float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble()).Normalized(),
                                          (float)rng.NextDouble() * 3f).Scaled(new Vector3(s * 1.3f, s * .8f, s));
                    rubble.Add(new Transform3D(basis, centre + new Vector3((float)rng.NextDouble() - .5f, s * .35f, (float)rng.NextDouble() - .5f) * new Vector3(.7f, 1, .7f)));
                }
            }
            if (rng.Chance(.06))
            {
                var centre = new Vector3(x + .5f, .03f, y + .5f);
                for (int i = 0; i < rng.Range(2, 4); i++)
                    bones.Add(new Transform3D(new Basis(Vector3.Up, (float)rng.NextDouble() * Mathf.Tau) * new Basis(Vector3.Right, Mathf.Pi / 2),
                                              centre + new Vector3((float)rng.NextDouble() - .5f, 0, (float)rng.NextDouble() - .5f) * .6f));
                if (rng.Chance(.5)) skulls.Add(new Transform3D(new Basis(Vector3.Up, (float)rng.NextDouble() * Mathf.Tau), centre + new Vector3(.15f, .07f, -.1f)));
            }
            if (rng.Chance(.1))
            {
                float s = .7f + (float)rng.NextDouble() * .9f;
                stains.Add(new Transform3D(new Basis(Vector3.Up, (float)rng.NextDouble() * Mathf.Tau).Scaled(new Vector3(s, 1, s)), new Vector3(x + .5f, .006f, y + .5f)));
            }
        }

        root.AddChild(Instanced(new BoxMesh { Size = new Vector3(1, 2.5f, 1), Material = wall }, pillars, "Pilares"));
        root.AddChild(Instanced(new BoxMesh { Size = new Vector3(1, .24f, 1), Material = wall }, caps, "Capiteis"));
        root.AddChild(Instanced(new BoxMesh { Size = Vector3.One, Material = Stone() }, rubble, "Entulho"));
        root.AddChild(Instanced(new CapsuleMesh { Radius = .035f, Height = .42f, RadialSegments = 6, Rings = 1, Material = Bone() }, bones, "Ossos"));
        root.AddChild(Instanced(new SphereMesh { Radius = .09f, Height = .16f, RadialSegments = 8, Rings = 4, Material = Bone() }, skulls, "Cranios"));
        root.AddChild(Instanced(new PlaneMesh { Size = Vector2.One, Material = Stain() }, stains, "Manchas"));
        return root;
    }

    private static MultiMeshInstance3D Instanced(Mesh mesh, List<Transform3D> transforms, string name)
    {
        var multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = transforms.Count };
        for (int i = 0; i < transforms.Count; i++) multimesh.SetInstanceTransform(i, transforms[i]);
        return new MultiMeshInstance3D { Multimesh = multimesh, Name = name };
    }

    private static StandardMaterial3D Stone() => new() { AlbedoColor = Materials.Rgb(58, 54, 52), Roughness = 1f };
    private static StandardMaterial3D Bone() => new() { AlbedoColor = Materials.Rgb(150, 142, 120), Roughness = .85f };

    /// <summary>Dark irregular blotch (old blood, damp, soot): radial gradient broken by noise, alpha-blended.</summary>
    private static StandardMaterial3D Stain()
    {
        var noise = new FastNoiseLite { Seed = 3, NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Frequency = .03f, FractalOctaves = 3 };
        var texture = new NoiseTexture2D { Noise = noise, Width = 128, Height = 128 };
        var ramp = new Gradient();
        ramp.Offsets = new[] { 0f, .45f, .7f };
        ramp.Colors = new[] { new Color(.05f, .02f, .02f, 0f), new Color(.05f, .02f, .02f, 0f), new Color(.06f, .03f, .025f, .55f) };
        texture.ColorRamp = ramp;
        return new StandardMaterial3D
        {
            AlbedoTexture = texture, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = .6f,
            AlbedoColor = Colors.White, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
    }

    /// <summary>Torch flame: a camera-facing teardrop of additive light that flickers in WorldView.Tick.</summary>
    public static MeshInstance3D Flame()
    {
        var g = new Gradient();
        g.Offsets = new[] { 0f, .35f, 1f };
        g.Colors = new[] { new Color(1f, .92f, .7f, 1f), new Color(1f, .55f, .18f, .8f), new Color(.8f, .2f, .05f, 0f) };
        var texture = new GradientTexture2D
        {
            Gradient = g, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(.5f, .72f), FillTo = new Vector2(.5f, .05f),
            Width = 64, Height = 128,
        };
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoTexture = texture, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add, BillboardMode = BaseMaterial3D.BillboardModeEnum.FixedY,
            EmissionEnabled = false, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        return new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(.26f, .5f), Material = material }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
    }
}
