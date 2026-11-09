using Godot;
using Umbra.Domain.Gameplay;
using Umbra.Domain.Play;
using Umbra.Domain.Skills;

namespace Umbra.Game.Ui;

/// <summary>Drawing parts shared by the screens (approved in the Phase 4 mockups): rows, tabs, prompts, tooltips, slots.</summary>
public static class Parts
{
    public static readonly Color Dim = new(0.02f, 0.02f, 0.03f, .86f);
    public static Color Faint => Materials.Rgb(110, 102, 96);
    public static readonly Color Good = Materials.Rgb(140, 210, 120), Bad = Materials.Rgb(230, 110, 100);

    public static void Shade(Control c, float alpha) => c.DrawRect(new Rect2(Vector2.Zero, c.Size), new Color(Dim, alpha));

    public static void Row(Control c, Rect2 r, string label, bool focus, string? hint = null, bool enabled = true)
    {
        c.DrawRect(r, focus ? Materials.Rgb(44, 30, 28) : Materials.Rgb(22, 20, 26, .9f));
        c.DrawRect(r, focus ? Materials.Gold : Materials.Iron, false, 2);
        if (focus)
        {
            float y = r.Position.Y + r.Size.Y / 2;
            c.DrawColoredPolygon(new[] { new Vector2(r.Position.X - 18, y - 8), new Vector2(r.Position.X - 6, y), new Vector2(r.Position.X - 18, y + 8) }, Materials.Gold);
        }
        var color = !enabled ? Faint : focus ? Materials.Bone : Materials.Ash;
        if (hint == null) UiKit.TextIn(c, label, r, 22, color, HorizontalAlignment.Left, 18);
        else
        {
            UiKit.TextIn(c, label, new Rect2(r.Position, new Vector2(r.Size.X, r.Size.Y * .6f)), 22, color, HorizontalAlignment.Left, 18);
            UiKit.TextIn(c, hint, new Rect2(r.Position + new Vector2(0, r.Size.Y * .52f), new Vector2(r.Size.X, r.Size.Y * .42f)), 11, Faint, HorizontalAlignment.Left, 18);
        }
    }

    /// <summary>Footer prompts for the device in use. Each entry: keyboard key, Xbox button (A/B/X/Y or a word), label.</summary>
    public static void Prompts(Control c, float y, params (string Key, string Pad, string Label)[] items)
    {
        float x = 40; var device = DeviceTracker.Current;
        foreach (var (key, pad, label) in items)
        {
            float w;
            bool face = pad is "A" or "B" or "X" or "Y";
            if (device == Device.Keyboard || !face)
            {
                string text = device == Device.Keyboard ? key : pad;
                w = Mathf.Max(22, UiKit.Measure(text, 11) + 10);
                UiKit.Prompt(c, new Vector2(x + w / 2, y), Device.Keyboard, text, "", 11);
            }
            else { w = 24; UiKit.Prompt(c, new Vector2(x + 12, y), device, "", pad, 11); }
            UiKit.Text(c, label, new Vector2(x + w + 8, y + 5), 11, Materials.Ash);
            x += w + UiKit.Measure(label, 11) + 30;
        }
    }

    public static float Wrap(Control c, string text, Rect2 box, int size, Color color, float gap = 6)
    {
        int px = UiKit.Px(size); float y = box.Position.Y + px; string line = "";
        foreach (var word in text.Split(' '))
        {
            string trial = line.Length == 0 ? word : line + " " + word;
            if (UiKit.Measure(trial, size) > box.Size.X && line.Length > 0)
            {
                UiKit.Text(c, line, new Vector2(box.Position.X, y), size, color); y += px + gap; line = word;
            }
            else line = trial;
        }
        if (line.Length > 0) UiKit.Text(c, line, new Vector2(box.Position.X, y), size, color);
        return y;
    }

    /// <summary>Tab strip with the device's previous/next prompts at both ends. Returns each tab's rectangle.</summary>
    public static List<Rect2> Tabs(Control c, Rect2 area, string[] tabs, int selected)
    {
        var rects = new List<Rect2>();
        var device = DeviceTracker.Current;
        string left = device == Device.Keyboard ? "Q" : "LB", right = device == Device.Keyboard ? "E" : "RB";
        UiKit.Prompt(c, new Vector2(area.Position.X + 14, area.Position.Y + area.Size.Y / 2), Device.Keyboard, left, "", 11);
        float x = area.Position.X + 44;
        for (int i = 0; i < tabs.Length; i++)
        {
            float w = UiKit.Measure(tabs[i], 22) + 32;
            var r = new Rect2(x, area.Position.Y, w, area.Size.Y);
            if (i == selected) { c.DrawRect(r, Materials.Rgb(44, 30, 28)); c.DrawRect(new Rect2(r.Position.X, r.End.Y - 3, r.Size.X, 3), Materials.Gold); }
            UiKit.TextIn(c, tabs[i], r, 22, i == selected ? Materials.Bone : Materials.Ash);
            rects.Add(r);
            x += w + 8;
        }
        UiKit.Prompt(c, new Vector2(x + 16, area.Position.Y + area.Size.Y / 2), Device.Keyboard, right, "", 11);
        return rects;
    }

    public static void Panel(Control c, Rect2 r, string title, int titleSize = 33)
    {
        UiKit.Frame(c, r);
        UiKit.TextIn(c, title, new Rect2(r.Position + new Vector2(28, 10), new Vector2(r.Size.X - 56, 44)), titleSize, Materials.Gold, HorizontalAlignment.Left);
    }

    public static void Box(Control c, Rect2 r) { c.DrawRect(r, Materials.Rgb(12, 11, 14, .92f)); c.DrawRect(r, Materials.Iron, false, 2); }

    public static string Tag(Rarity r) => Loot.Tags[(int)r];

    public static void ItemSlot(Control c, Rect2 r, Item? item, string? label, bool focus)
    {
        c.DrawRect(r, Materials.Rgb(14, 12, 16));
        c.DrawRect(r, item != null ? Materials.Rarity(item.Rarity) : Materials.Iron, false, item != null ? 2 : 1);
        if (item != null) UiKit.TextIn(c, ItemText.Short(item.Slot), r, 22, Materials.Rarity(item.Rarity));
        else if (label != null) UiKit.TextIn(c, label, r, 11, Faint);
        if (focus) c.DrawRect(r.Grow(3), Materials.Gold, false, 2);
    }

    /// <summary>Item tooltip: name in its rarity colour with the tag, type, main stat, affixes and, when given, the
    /// comparison with the equipped item in colour AND words ("melhor"/"pior").</summary>
    public static void Tooltip(Control c, Rect2 r, Item item, string type, IReadOnlyList<(string Text, bool Better)>? compare)
    {
        var color = Materials.Rarity(item.Rarity);
        c.DrawRect(r, Materials.Rgb(10, 9, 12, .96f));
        c.DrawRect(r, color, false, 2);
        c.DrawRect(new Rect2(r.Position, new Vector2(r.Size.X, 52)), new Color(color, .14f));
        UiKit.TextIn(c, $"[{item.Tag}] {item.Name}", new Rect2(r.Position + new Vector2(0, 2), new Vector2(r.Size.X, 30)), 22, color, HorizontalAlignment.Left, 12);
        UiKit.TextIn(c, item.RarityName + "  —  " + type, new Rect2(r.Position + new Vector2(0, 30), new Vector2(r.Size.X, 18)), 11, Materials.Ash, HorizontalAlignment.Left, 12);
        float y = r.Position.Y + 62;
        UiKit.TextIn(c, ItemText.Main(item), new Rect2(r.Position.X, y, r.Size.X, 30), 22, Materials.Bone, HorizontalAlignment.Left, 12); y += 34;
        foreach (var a in item.Affixes) { UiKit.TextIn(c, ItemText.Affix(a), new Rect2(r.Position.X, y, r.Size.X, 28), 22, Materials.Rgb(140, 170, 230), HorizontalAlignment.Left, 12); y += 28; }
        if (compare == null || compare.Count == 0) return;
        y += 8;
        c.DrawRect(new Rect2(r.Position.X + 12, y, r.Size.X - 24, 1), Materials.Iron);
        UiKit.Text(c, "COMPARADO AO EQUIPADO", new Vector2(r.Position.X + 12, y + 18), 11, Materials.Ash); y += 26;
        foreach (var (text, better) in compare)
        {
            var row = new Rect2(r.Position.X, y, r.Size.X, 26);
            UiKit.TextIn(c, text, row, 22, better ? Good : Bad, HorizontalAlignment.Left, 12);
            UiKit.TextIn(c, better ? "melhor" : "pior", row, 11, Materials.Ash, HorizontalAlignment.Right, 12);
            y += 26;
        }
    }

    public static float TooltipHeight(Item item, int compareLines) => 62 + 34 + item.Affixes.Count * 28 + (compareLines > 0 ? 34 + compareLines * 26 : 0) + 14;

    // ------------------------------------------------------------------ UMBRA item records (step 5.8c)
    public static void ItemSlot(Control c, Rect2 r, PyDict? record, string? label, bool focus)
    {
        c.DrawRect(r, Materials.Rgb(14, 12, 16));
        var color = record != null ? Materials.RarityOf(record) : Materials.Iron;
        c.DrawRect(r, color, false, record != null ? 2 : 1);
        if (record != null) UiKit.TextIn(c, ItemView.Kind(record), r, 11, color);
        else if (label != null) UiKit.TextIn(c, label, r, 11, Faint);
        if (focus) c.DrawRect(r.Grow(3), Materials.Gold, false, 2);
    }

    /// <summary>Tooltip of a record: name in its rarity colour with the tag, rarity and type, stats, affixes, level and
    /// durability, and the comparison with the equipped item in colour AND words ("melhor"/"pior").</summary>
    public static void Tooltip(Control c, Rect2 r, PyDict record, string type, IReadOnlyList<(string Text, bool Better)>? compare)
    {
        var color = Materials.RarityOf(record);
        c.DrawRect(r, Materials.Rgb(10, 9, 12, .96f));
        c.DrawRect(r, color, false, 2);
        c.DrawRect(new Rect2(r.Position, new Vector2(r.Size.X, 52)), new Color(color, .14f));
        UiKit.TextIn(c, $"[{ItemView.Tag(record)}] {ItemView.Name(record)}", new Rect2(r.Position + new Vector2(0, 2), new Vector2(r.Size.X, 30)), 22, color, HorizontalAlignment.Left, 12);
        UiKit.TextIn(c, ItemView.Rarity(record) + "  —  " + type, new Rect2(r.Position + new Vector2(0, 30), new Vector2(r.Size.X, 18)), 11, Materials.Ash, HorizontalAlignment.Left, 12);
        float y = r.Position.Y + 62;
        foreach (var line in ItemView.StatLines(record)) { UiKit.TextIn(c, line, new Rect2(r.Position.X, y, r.Size.X, 30), 22, Materials.Bone, HorizontalAlignment.Left, 12); y += 30; }
        foreach (var line in ItemView.AffixLines(record)) { UiKit.TextIn(c, line, new Rect2(r.Position.X, y, r.Size.X, 28), 22, Materials.Rgb(140, 170, 230), HorizontalAlignment.Left, 12); y += 28; }
        UiKit.TextIn(c, ItemView.Footer(record), new Rect2(r.Position.X, y + 2, r.Size.X, 18), 11, Faint, HorizontalAlignment.Left, 12); y += 22;
        if (compare == null || compare.Count == 0) return;
        y += 8;
        c.DrawRect(new Rect2(r.Position.X + 12, y, r.Size.X - 24, 1), Materials.Iron);
        UiKit.Text(c, "COMPARADO AO EQUIPADO", new Vector2(r.Position.X + 12, y + 18), 11, Materials.Ash); y += 26;
        foreach (var (text, better) in compare)
        {
            var row = new Rect2(r.Position.X, y, r.Size.X, 26);
            UiKit.TextIn(c, text, row, 22, better ? Good : Bad, HorizontalAlignment.Left, 12);
            UiKit.TextIn(c, better ? "melhor" : "pior", row, 11, Materials.Ash, HorizontalAlignment.Right, 12);
            y += 26;
        }
    }

    public static float TooltipHeight(PyDict record, int compareLines) =>
        62 + ItemView.StatLines(record).Count * 30 + ItemView.AffixLines(record).Count * 28 + 22
        + (compareLines > 0 ? 34 + compareLines * 26 : 0) + 14;
}

/// <summary>Display text of item stats (PROVISIONAL numbers live in Umbra.Domain.Gameplay.ItemRules).</summary>
public static class ItemText
{
    /// <summary>Two-letter mark of the item type in its slot (placeholder for the icons of the art phase).</summary>
    public static string Short(string slot) => slot switch
    {
        "Arma" => "AR", "Anel" => "AN", "Amuleto" => "AM", "Elmo" => "EL", "Peitoral" => "PE", "Luvas" => "LU", "Botas" => "BO", "Mão secundária" => "MS", _ => "?",
    };

    public static string StatName(Stat s) => s switch { Stat.Attack => "ataque", Stat.Defense => "defesa", _ => "vida" };
    public static string Main(Item item) => item.MainStat switch
    {
        Stat.Attack => $"Dano +{item.Power}",
        Stat.Defense => $"Armadura +{item.Power}",
        _ => $"Vida +{item.Power}",
    };
    public static string Affix(Affix a) => $"+{a.Value} de {StatName(a.Stat)}";
}
