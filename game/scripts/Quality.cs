using Godot;

namespace Umbra.Game;

/// <summary>
/// The slice's three visual levels, all inside the Compatibility renderer (the only one this machine runs; plan D1).
/// They change only what is drawn: resolution scale, anti-aliasing, shadows, light budget, fog, glow and particle
/// density. Combat rules live in Umbra.Domain and never see these values.
/// </summary>
public static class Quality
{
    public sealed record Preset(string Id, string Name, float Scale3D, Viewport.Msaa Msaa, bool MoonShadow, int TorchShadows,
                                int MaxLights, bool Fog, bool Glow, float Particles);

    // Measured on the ThinkPad T440 (docs/RELATORIO_SLICE.md): in Compatibility, a 3D scale below 1 adds an intermediate
    // buffer that cost ~22 ms per frame on the HD 4400, and MSAA 2x cost ~9 ms; both stay off here. Scale3D is kept for
    // the Forward+ presets of a stronger machine.
    public static readonly Preset Baixo = new("baixo", "Baixo", 1f, Viewport.Msaa.Disabled, false, 0, 4, false, false, .3f);
    public static readonly Preset Medio = new("medio", "Médio", 1f, Viewport.Msaa.Disabled, true, 0, 6, true, false, .7f);
    public static readonly Preset Alto = new("alto", "Alto", 1f, Viewport.Msaa.Disabled, true, 1, 8, true, true, 1f);
    public static readonly Preset[] All = { Baixo, Medio, Alto };

    public static Preset ById(string id) => All.FirstOrDefault(p => p.Id == id) ?? Medio;
    public static Preset Next(Preset p) => All[(Array.IndexOf(All, p) + 1) % All.Length];

    public static void Apply(Viewport viewport, WorldView world, Preset preset)
    {
        if (LaunchOptions.Skip.Contains("scaling")) preset = preset with { Scale3D = 1f };
        if (LaunchOptions.Skip.Contains("msaa")) preset = preset with { Msaa = Viewport.Msaa.Disabled };
        if (LaunchOptions.Skip.Contains("torchshadows")) preset = preset with { TorchShadows = 0 };
        if (LaunchOptions.Skip.Contains("glow")) preset = preset with { Glow = false };
        if (LaunchOptions.Skip.Contains("moonshadow")) preset = preset with { MoonShadow = false };
        viewport.Scaling3DMode = Viewport.Scaling3DModeEnum.Bilinear;
        viewport.Scaling3DScale = preset.Scale3D;
        viewport.Msaa3D = preset.Msaa;
        world.ApplyPreset(preset);
        Effects.ParticleScale = preset.Particles;
    }
}
