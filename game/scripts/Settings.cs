using System.Text.Json;
using Godot;

namespace Umbra.Game;

/// <summary>
/// Player settings (Settings screen), saved to user://configuracoes.json and applied live. Tool runs (screenshots,
/// benchmark, self-test) never read or write this file, so they stay reproducible and never touch the player's choices.
/// Quality = a named preset whose individual parts can be changed; any change makes it "Personalizado".
/// </summary>
public sealed class Settings
{
    // Vídeo
    public string Preset { get; set; } = "medio";
    public string WindowMode { get; set; } = "janela";            // janela | tela_cheia | exclusiva
    public string Resolution { get; set; } = "1280x720";
    public bool VSync { get; set; } = true;
    public int FpsLimit { get; set; } = 60;                         // 0 = sem limite
    public int UiScale { get; set; } = 100;
    public bool CharacterShadows { get; set; }
    public int Lights { get; set; } = 6;
    public bool Fog { get; set; } = true;
    public int Particles { get; set; } = 70;
    public bool Post { get; set; } = true;
    public int Brightness { get; set; } = 50;
    // Áudio (no sound in the slice yet: the buses exist and keep the values)
    public int Master { get; set; } = 80;
    public int Music { get; set; } = 70;
    public int Effects { get; set; } = 80;
    // Acessibilidade
    public int Shake { get; set; } = 100;
    public bool ReduceFlashes { get; set; }
    public bool DamageNumbers { get; set; } = true;
    // Jogo
    public string StartCamera { get; set; } = "iso";
    public int Zoom { get; set; } = 30;
    public bool ShowFps { get; set; }
    public string Icons { get; set; } = "auto";                     // auto | xbox | playstation
    public Dictionary<string, List<string>> Bindings { get; set; } = new();

    public static Settings Current { get; private set; } = new();
    public static bool Persistent { get; private set; }
    private const string Path = "user://configuracoes.json";

    public static readonly int[] FpsChoices = { 30, 60, 120, 0 };
    public static readonly string[] Resolutions = { "1280x720", "1600x900", "1920x1080" };
    public static readonly int[] UiScales = { 100, 125, 150 };
    public static readonly int[] LightChoices = { 4, 6, 8 };

    public static void Load(bool persistent)
    {
        Persistent = persistent;
        Current = new Settings();
        if (!persistent || !Godot.FileAccess.FileExists(Path)) return;
        try { Current = JsonSerializer.Deserialize<Settings>(Godot.FileAccess.GetFileAsString(Path)) ?? new Settings(); }
        catch (JsonException) { GD.PushWarning("configuracoes.json ilegível; usando o padrão (o arquivo não foi apagado)."); }
    }

    public void Save()
    {
        if (!Persistent) return;
        using var file = Godot.FileAccess.Open(Path, Godot.FileAccess.ModeFlags.Write);
        file?.StoreString(JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Copies a named preset's parts into the individual options.</summary>
    public void UsePreset(string id)
    {
        var p = Quality.ById(id);
        Preset = p.Id; CharacterShadows = p.MoonShadow; Lights = p.MaxLights; Fog = p.Fog; Particles = (int)Math.Round(p.Particles * 100); Post = p.Post;
    }

    /// <summary>Name shown for Qualidade: the preset if every part matches it, else "Personalizado".</summary>
    public string QualityName
    {
        get
        {
            var p = Quality.ById(Preset);
            bool same = p.MoonShadow == CharacterShadows && p.MaxLights == Lights && p.Fog == Fog && (int)Math.Round(p.Particles * 100) == Particles && p.Post == Post;
            return same ? p.Name : "Personalizado";
        }
    }

    public Quality.Preset Effective()
    {
        var p = Quality.ById(Preset);
        return p with
        {
            MoonShadow = CharacterShadows, MaxLights = Lights, TorchShadows = Math.Min(p.TorchShadows, Lights), Fog = Fog, Particles = Particles / 100f,
            Post = Post, Grain = Post && p.Grain, ImpactLights = !ReduceFlashes && p.ImpactLights,
        };
    }

    /// <summary>Applies everything that is not part of the 3D preset (window, sound, accessibility, camera, icons).</summary>
    public void ApplySystem(Main main)
    {
        var window = main.GetWindow();
        if (main.Options.Shot == null && main.Options.Bench == null && !main.Options.SelfTest)
        {
            DisplayServer.WindowSetMode(WindowMode switch
            {
                "tela_cheia" => DisplayServer.WindowMode.Fullscreen, "exclusiva" => DisplayServer.WindowMode.ExclusiveFullscreen,
                _ => DisplayServer.WindowMode.Windowed,
            });
            if (WindowMode == "janela")
            {
                var wh = Resolution.Split('x');
                DisplayServer.WindowSetSize(new Vector2I(int.Parse(wh[0]), int.Parse(wh[1])));
            }
            DisplayServer.WindowSetVsyncMode(VSync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
            Engine.MaxFps = FpsLimit;
        }
        window.ContentScaleFactor = UiScale / 100f;
        SetBus("Master", Master); SetBus("Musica", Music); SetBus("Efeitos", Effects);
        CameraRig.ShakeScale = Shake / 100f;
        Umbra.Game.Effects.FlashScale = ReduceFlashes ? .35f : 1f;
        Hud.DamageNumbers = DamageNumbers;
        Hud.ShowFps = ShowFps;
        DeviceTracker.Override = Icons switch { "xbox" => Device.Xbox, "playstation" => Device.PlayStation, _ => null };
        main.World?.SetBrightness(Brightness);
    }

    private static void SetBus(string name, int percent)
    {
        int index = AudioServer.GetBusIndex(name);
        if (index < 0) { AudioServer.AddBus(); index = AudioServer.BusCount - 1; AudioServer.SetBusName(index, name); AudioServer.SetBusSend(index, "Master"); }
        AudioServer.SetBusVolumeDb(index, percent <= 0 ? -80f : Mathf.LinearToDb(percent / 100f));
    }
}
