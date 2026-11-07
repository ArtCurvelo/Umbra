using Godot;

namespace Umbra.Game;

/// <summary>
/// "Ferro e Cinza" in Godot (UMBRA's game/ui/umbra_kit.py): dark iron frames with a one-pixel bevel, gold studs only
/// on corners, bone text, gold for focus. Shared by the HUD and the pause menu.
/// </summary>
public static class UiKit
{
    public static readonly Color Fill = Materials.Rgb(16, 14, 18, 236 / 255f);
    public static Font Font => ThemeDB.FallbackFont;

    public static void Frame(CanvasItem c, Rect2 r, Color? corner = null)
    {
        c.DrawRect(r, Fill);
        c.DrawRect(r, Materials.IronDark, false, 4);
        c.DrawRect(r.Grow(-3), Materials.IronLight, false, 2);
        c.DrawLine(new Vector2(r.Position.X + 4, r.End.Y - 6), new Vector2(r.End.X - 6, r.End.Y - 6), Materials.Iron, 2);
        var gold = corner ?? Materials.Gold;
        foreach (var p in new[] { r.Position, new Vector2(r.End.X - 8, r.Position.Y), new Vector2(r.Position.X, r.End.Y - 8), r.End - new Vector2(8, 8) })
        {
            c.DrawRect(new Rect2(p, new Vector2(8, 8)), Materials.IronDark);
            c.DrawRect(new Rect2(p + new Vector2(2, 2), new Vector2(4, 4)), gold);
        }
    }

    public static void Text(CanvasItem c, string text, Vector2 at, int size, Color color, HorizontalAlignment align = HorizontalAlignment.Left, float width = -1)
    {
        var shadow = new Color(0, 0, 0, .8f * color.A);
        c.DrawString(Font, at + new Vector2(1, 1), text, align, width, size, shadow);
        c.DrawString(Font, at, text, align, width, size, color);
    }

    /// <summary>Bar with an iron rim; the share drawn is value/max. Text centred on it.</summary>
    public static void Bar(CanvasItem c, Rect2 r, float value, float max, Color color, string label)
    {
        c.DrawRect(r.Grow(3), Materials.IronDark);
        c.DrawRect(r.Grow(1), Materials.IronLight, false, 1);
        c.DrawRect(r, Materials.Rgb(10, 9, 12));
        float share = max <= 0 ? 0 : Mathf.Clamp(value / max, 0, 1);
        var fill = new Rect2(r.Position, new Vector2(r.Size.X * share, r.Size.Y));
        c.DrawRect(fill, color.Darkened(.25f));
        c.DrawRect(new Rect2(fill.Position, new Vector2(fill.Size.X, r.Size.Y * .45f)), color);
        Text(c, label, new Vector2(r.Position.X, r.Position.Y + r.Size.Y * .5f + 6), 15, Materials.Bone, HorizontalAlignment.Center, r.Size.X);
    }

    /// <summary>Button prompt for the device in use: keycap, mouse button, Xbox letter or PlayStation shape.</summary>
    public static void Prompt(CanvasItem c, Vector2 center, Device device, string key, string xbox, float radius = 11)
    {
        if (device == Device.Keyboard)
        {
            if (key is "M1" or "M2")
            {
                var body = new Rect2(center - new Vector2(8, 11), new Vector2(16, 22));
                c.DrawRect(body, Materials.Rgb(30, 28, 32)); c.DrawRect(body, Materials.Ash, false, 1.5f);
                var half = key == "M1" ? new Rect2(body.Position, new Vector2(8, 9)) : new Rect2(body.Position + new Vector2(8, 0), new Vector2(8, 9));
                c.DrawRect(half, Materials.Gold);
                c.DrawLine(body.Position + new Vector2(0, 9), body.Position + new Vector2(16, 9), Materials.Ash, 1);
                return;
            }
            float w = Mathf.Max(22, key.Length * 9 + 10);
            var cap = new Rect2(center - new Vector2(w / 2, 11), new Vector2(w, 22));
            c.DrawRect(cap, Materials.Rgb(30, 28, 32)); c.DrawRect(cap, Materials.Ash, false, 1.5f);
            Text(c, key, new Vector2(cap.Position.X, center.Y + 5), 13, Materials.Bone, HorizontalAlignment.Center, w);
            return;
        }
        if (device == Device.Xbox)
        {
            var color = xbox switch { "A" => Materials.Rgb(96, 176, 72), "B" => Materials.Rgb(200, 64, 56), "X" => Materials.Rgb(64, 120, 210), "Y" => Materials.Rgb(220, 180, 60), _ => Materials.Ash };
            c.DrawCircle(center, radius, Materials.Rgb(24, 22, 26));
            c.DrawArc(center, radius, 0, Mathf.Tau, 24, color, 2);
            Text(c, xbox, new Vector2(center.X - radius, center.Y + 5), 14, color, HorizontalAlignment.Center, radius * 2);
            return;
        }
        // PlayStation: A=cross, B=circle, X=square, Y=triangle, drawn as shapes (no font glyph needed)
        c.DrawCircle(center, radius, Materials.Rgb(24, 22, 26));
        c.DrawArc(center, radius, 0, Mathf.Tau, 24, Materials.Ash, 1.5f);
        float s = radius * .5f;
        switch (xbox)
        {
            case "A":
                var blue = Materials.Rgb(120, 160, 230);
                c.DrawLine(center + new Vector2(-s, -s), center + new Vector2(s, s), blue, 2); c.DrawLine(center + new Vector2(-s, s), center + new Vector2(s, -s), blue, 2); break;
            case "B": c.DrawArc(center, s, 0, Mathf.Tau, 20, Materials.Rgb(230, 100, 110), 2); break;
            case "X": c.DrawRect(new Rect2(center - new Vector2(s, s), new Vector2(2 * s, 2 * s)), Materials.Rgb(220, 140, 200), false, 2); break;
            default:
                var green = Materials.Rgb(90, 200, 170);
                var a = center + new Vector2(0, -s); var b = center + new Vector2(s, s * .8f); var d = center + new Vector2(-s, s * .8f);
                c.DrawPolyline(new[] { a, b, d, a }, green, 2); break;
        }
    }
}
