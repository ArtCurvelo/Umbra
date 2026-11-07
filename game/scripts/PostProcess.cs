using Godot;

namespace Umbra.Game;

/// <summary>Full-screen grade (shaders/post.gdshader) between the 3D world and the HUD; on for Médio and Alto.</summary>
public partial class PostProcess : CanvasLayer
{
    private static PostProcess? _instance;
    private readonly ColorRect _rect = new() { MouseFilter = Control.MouseFilterEnum.Ignore };
    private ShaderMaterial _material = null!;

    public override void _Ready()
    {
        _instance = this;
        Layer = 0;                                             // the HUD is on layer 1, the pause menu on 20
        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/post.gdshader") };
        _rect.Material = _material;
        _rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_rect);
    }

    public static void Apply(Quality.Preset preset)
    {
        if (_instance == null) return;
        _instance.Visible = preset.Post;
        _instance._material.SetShaderParameter("grain", preset.Grain ? .035f : 0f);
    }
}
