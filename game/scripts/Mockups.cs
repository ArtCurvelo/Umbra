using Godot;

namespace Umbra.Game;

/// <summary>
/// Phase 4 mockups (document sec. 8: "wireframes or real captures before implementing large screens"). Drawn with the
/// real UI kit and font over the real game scene, with sample data, so the Game Director judges layout and reading
/// before any screen is built. Not functional: navigation and data come in the implementation step after approval.
///   --mockup menu | config | controles | inventario | arvore
/// </summary>
public partial class Mockups : CanvasLayer
{
    private readonly string _name;
    public Mockups(string name) { _name = name; Layer = 5; }

    private partial class Canvas : Control
    {
        public string Name2 = "";
        public override void _Draw()
        {
            switch (Name2)
            {
                case "menu": Menu(this); break;
                case "config": Config(this); break;
                case "controles": Controls(this); break;
                case "inventario": Inventory(this); break;
                default: Tree(this); break;
            }
        }
    }

    public override void _Ready()
    {
        var canvas = new Canvas { Name2 = _name, MouseFilter = Control.MouseFilterEnum.Ignore };
        canvas.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(canvas);
    }

    // ------------------------------------------------------------------ shared pieces

    private static readonly Color Dim = new(0.02f, 0.02f, 0.03f, .86f);
    private static Color Faint => Materials.Rgb(110, 102, 96);

    private static void Shade(Control c, float alpha) => c.DrawRect(new Rect2(Vector2.Zero, c.Size), new Color(Dim, alpha));

    /// <summary>Menu row in the "Ferro e Cinza" language: gold frame and a gold pointer when focused.</summary>
    private static void Row(Control c, Rect2 r, string label, bool focus, string? hint = null)
    {
        c.DrawRect(r, focus ? Materials.Rgb(44, 30, 28) : Materials.Rgb(22, 20, 26, .9f));
        c.DrawRect(r, focus ? Materials.Gold : Materials.Iron, false, 2);
        if (focus)
        {
            float y = r.Position.Y + r.Size.Y / 2;
            c.DrawColoredPolygon(new[] { new Vector2(r.Position.X - 18, y - 8), new Vector2(r.Position.X - 6, y), new Vector2(r.Position.X - 18, y + 8) }, Materials.Gold);
        }
        if (hint == null) UiKit.TextIn(c, label, r, 22, focus ? Materials.Bone : Materials.Ash, HorizontalAlignment.Left, 18);
        else
        {
            UiKit.TextIn(c, label, new Rect2(r.Position, new Vector2(r.Size.X, r.Size.Y * .6f)), 22, focus ? Materials.Bone : Materials.Ash, HorizontalAlignment.Left, 18);
            UiKit.TextIn(c, hint, new Rect2(r.Position + new Vector2(0, r.Size.Y * .52f), new Vector2(r.Size.X, r.Size.Y * .42f)), 11, Faint, HorizontalAlignment.Left, 18);
        }
    }

    private static void Prompts(Control c, float y, params (string Key, string Pad, string Label)[] items)
    {
        float x = 40;
        foreach (var (key, pad, label) in items)
        {
            UiKit.Prompt(c, new Vector2(x + 12, y), Device.Keyboard, key, pad, 11);
            float kw = Mathf.Max(22, UiKit.Measure(key, 11) + 10);
            UiKit.Text(c, label, new Vector2(x + kw + 10, y + 7), 11, Materials.Ash);
            x += kw + UiKit.Measure(label, 11) + 34;
        }
    }

    private static void Wrap(Control c, string text, Rect2 box, int size, Color color, float lineGap = 6)
    {
        int px = UiKit.Px(size); float y = box.Position.Y + px; string line = "";
        foreach (var word in text.Split(' '))
        {
            string trial = line.Length == 0 ? word : line + " " + word;
            if (UiKit.Measure(trial, size) > box.Size.X && line.Length > 0)
            {
                UiKit.Text(c, line, new Vector2(box.Position.X, y), size, color); y += px + lineGap; line = word;
            }
            else line = trial;
        }
        if (line.Length > 0) UiKit.Text(c, line, new Vector2(box.Position.X, y), size, color);
    }

    private static void Title(Control c, string text, Rect2 r, int size = 33) => UiKit.TextIn(c, text, r, size, Materials.Gold, HorizontalAlignment.Left, 0);

    private static void Tabs(Control c, Rect2 area, string[] tabs, int selected)
    {
        float x = area.Position.X + 44;
        UiKit.Prompt(c, new Vector2(area.Position.X + 14, area.Position.Y + area.Size.Y / 2), Device.Keyboard, "Q", "LB", 11);
        for (int i = 0; i < tabs.Length; i++)
        {
            float w = UiKit.Measure(tabs[i], 22) + 32;
            var r = new Rect2(x, area.Position.Y, w, area.Size.Y);
            if (i == selected) { c.DrawRect(r, Materials.Rgb(44, 30, 28)); c.DrawRect(new Rect2(r.Position.X, r.End.Y - 3, r.Size.X, 3), Materials.Gold); }
            UiKit.TextIn(c, tabs[i], r, 22, i == selected ? Materials.Bone : Materials.Ash);
            x += w + 8;
        }
        UiKit.Prompt(c, new Vector2(x + 16, area.Position.Y + area.Size.Y / 2), Device.Keyboard, "E", "RB", 11);
    }

    // ------------------------------------------------------------------ main menu

    private static void Menu(Control c)
    {
        // darker on the left, where the menu lives; the crypt stays visible on the right
        for (int i = 0; i < 32; i++)
            c.DrawRect(new Rect2(i * c.Size.X / 32, 0, c.Size.X / 32 + 1, c.Size.Y), new Color(Dim, Mathf.Lerp(.92f, .25f, i / 31f)));
        UiKit.Text(c, "UMBRA", new Vector2(96 + 6, 186 + 6), 66, new Color(0, 0, 0, .8f));
        UiKit.Text(c, "UMBRA", new Vector2(96, 186), 66, Materials.Gold);
        UiKit.Text(c, "ASHES BELOW", new Vector2(100, 214), 22, Materials.Ash);
        c.DrawRect(new Rect2(98, 230, 260, 2), Materials.Iron);
        var rows = new (string Label, string? Hint)[]
        {
            ("Continuar", "Kael, Bárbaro nível 12 — Cripta Esquecida"), ("Novo jogo", null), ("Configurações", null), ("Créditos", null), ("Sair", null),
        };
        float y = 270;
        for (int i = 0; i < rows.Length; i++)
        {
            float h = rows[i].Hint != null ? 64 : 46;
            Row(c, new Rect2(110, y, 380, h), rows[i].Label, i == 0, rows[i].Hint);
            y += h + 10;
        }
        UiKit.Text(c, "vertical slice 0.1  —  Godot 4.7.2", new Vector2(40, c.Size.Y - 44), 11, Faint);
        Prompts(c, c.Size.Y - 22, ("Enter", "A", "Confirmar"), ("Esc", "B", "Voltar"));
    }

    // ------------------------------------------------------------------ settings: video

    private static void Config(Control c)
    {
        Shade(c, .9f);
        var panel = new Rect2(40, 24, c.Size.X - 80, c.Size.Y - 72);
        UiKit.Frame(c, panel);
        Title(c, "CONFIGURAÇÕES", new Rect2(panel.Position + new Vector2(28, 10), new Vector2(400, 44)));
        Tabs(c, new Rect2(panel.Position.X + 20, panel.Position.Y + 58, 900, 36), new[] { "Vídeo", "Áudio", "Controles", "Acessibilidade", "Jogo" }, 0);
        var options = new (string Label, string Value, string Kind)[]
        {
            ("Qualidade", "Médio", "choice"), ("Resolução", "1280 x 720", "choice"), ("Modo de tela", "Janela", "choice"),
            ("VSync", "LIGADO", "toggle"), ("Limite de quadros", "60", "choice"), ("Escala da interface", "1x", "choice"),
            ("Sombras dos personagens", "DESLIGADO", "toggle"), ("Luzes simultâneas", "6", "choice"), ("Névoa", "LIGADO", "toggle"),
            ("Partículas", "70", "slider"), ("Pós-processamento", "LIGADO", "toggle"), ("Brilho", "50", "slider"), ("Tremor de câmera", "100", "slider"),
        };
        float y = panel.Position.Y + 108; int focus = 6;
        for (int i = 0; i < options.Length; i++)
        {
            var r = new Rect2(panel.Position.X + 40, y, 640, 34);
            bool f = i == focus;
            if (f) { c.DrawRect(r, Materials.Rgb(44, 30, 28)); c.DrawRect(r, Materials.Gold, false, 2); }
            UiKit.TextIn(c, options[i].Label, r, 22, f ? Materials.Bone : Materials.Ash, HorizontalAlignment.Left, 14);
            var v = new Rect2(r.Position.X + 380, r.Position.Y, 246, r.Size.Y);
            switch (options[i].Kind)
            {
                case "slider":
                    var bar = new Rect2(v.Position.X + 4, v.Position.Y + 14, 170, 6);
                    c.DrawRect(bar, Materials.IronDark);
                    c.DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * int.Parse(options[i].Value) / 100f, bar.Size.Y)), f ? Materials.Gold : Materials.Ash);
                    UiKit.TextIn(c, options[i].Value + "%", new Rect2(v.Position.X + 180, v.Position.Y, 66, v.Size.Y), 22, Materials.Bone, HorizontalAlignment.Right);
                    break;
                case "toggle":
                    bool on = options[i].Value == "LIGADO";
                    UiKit.TextIn(c, options[i].Value, v, 22, on ? Materials.Rgb(150, 200, 120) : Materials.Rgb(200, 120, 100), HorizontalAlignment.Right, 6);
                    break;
                default:
                    UiKit.TextIn(c, "<   " + options[i].Value + "   >", v, 22, Materials.Bone, HorizontalAlignment.Right, 6);
                    break;
            }
            y += 38;
        }
        // description of the focused option, with the measured cost
        var info = new Rect2(panel.Position.X + 720, panel.Position.Y + 108, panel.Size.X - 760, 330);
        c.DrawRect(info, Materials.Rgb(12, 11, 14, .9f)); c.DrawRect(info, Materials.Iron, false, 2);
        UiKit.TextIn(c, "Sombras dos personagens", new Rect2(info.Position + new Vector2(0, 8), new Vector2(info.Size.X, 32)), 22, Materials.Gold, HorizontalAlignment.Left, 16);
        Wrap(c, "Os personagens projetam sombra da luz da lua. Dá profundidade à cena, mas é o recurso visual mais caro.",
             new Rect2(info.Position + new Vector2(16, 48), new Vector2(info.Size.X - 32, 120)), 22, Materials.Bone);
        UiKit.Text(c, "Impacto no desempenho", new Vector2(info.Position.X + 16, info.Position.Y + 214), 11, Materials.Ash);
        for (int i = 0; i < 5; i++)
            c.DrawRect(new Rect2(info.Position.X + 16 + i * 30, info.Position.Y + 224, 24, 10), i < 4 ? Materials.Ember : Materials.IronDark);
        UiKit.Text(c, "ALTO", new Vector2(info.Position.X + 172, info.Position.Y + 234), 11, Materials.Ember);
        Wrap(c, "Medido no ThinkPad T440 (preset Médio): cerca de 7 FPS a menos.", new Rect2(info.Position + new Vector2(16, 250), new Vector2(info.Size.X - 32, 60)), 11, Faint, 4);
        Prompts(c, c.Size.Y - 22, ("Setas", "Dir.", "Navegar"), ("< >", "Esq./Dir.", "Alterar"), ("Q E", "LB RB", "Abas"), ("R", "Y", "Restaurar padrão"), ("Esc", "B", "Voltar"));
    }

    // ------------------------------------------------------------------ settings: controls (remapping)

    private static void Controls(Control c)
    {
        Shade(c, .9f);
        var panel = new Rect2(40, 24, c.Size.X - 80, c.Size.Y - 72);
        UiKit.Frame(c, panel);
        Title(c, "CONFIGURAÇÕES", new Rect2(panel.Position + new Vector2(28, 10), new Vector2(400, 44)));
        Tabs(c, new Rect2(panel.Position.X + 20, panel.Position.Y + 58, 900, 36), new[] { "Vídeo", "Áudio", "Controles", "Acessibilidade", "Jogo" }, 2);
        float x0 = panel.Position.X + 40, y = panel.Position.Y + 112;
        UiKit.Text(c, "AÇÃO", new Vector2(x0 + 14, y), 11, Materials.Ash);
        UiKit.Text(c, "TECLADO E MOUSE", new Vector2(x0 + 330, y), 11, Materials.Ash);
        UiKit.Text(c, "CONTROLE", new Vector2(x0 + 560, y), 11, Materials.Ash);
        y += 12;
        var rows = new (string Action, string Key, string Pad)[]
        {
            ("Andar", "W A S D", "LS"), ("Golpe Brutal", "Espaço", "A"), ("Ruptura", "Q", "X"), ("Investida", "E", "Y"),
            ("Pausa", "Esc", "Start"), ("Câmera iso / top-down", "Tab", "Back"), ("Girar câmera", "Z  C", "LB RB"), ("Zoom", "Roda", "Dir."),
        };
        for (int i = 0; i < rows.Length; i++)
        {
            var r = new Rect2(x0, y, 680, 40);
            bool f = i == 2;
            c.DrawRect(r, f ? Materials.Rgb(44, 30, 28) : (i % 2 == 0 ? Materials.Rgb(20, 18, 24, .7f) : new Color(0, 0, 0, 0)));
            UiKit.TextIn(c, rows[i].Action, new Rect2(r.Position, new Vector2(320, r.Size.Y)), 22, f ? Materials.Bone : Materials.Ash, HorizontalAlignment.Left, 14);
            var key = new Rect2(r.Position.X + 320, r.Position.Y + 4, 210, 32);
            if (f)
            {
                c.DrawRect(key, Materials.Rgb(60, 36, 30)); c.DrawRect(key, Materials.Gold, false, 2);
                UiKit.TextIn(c, "Pressione uma tecla", key, 11, Materials.Gold);
            }
            else UiKit.Prompt(c, new Vector2(key.Position.X + 40, key.Position.Y + 16), Device.Keyboard, rows[i].Key, "", 11);
            var pad = rows[i].Pad;
            if (pad is "A" or "B" or "X" or "Y") UiKit.Prompt(c, new Vector2(r.Position.X + 570, r.Position.Y + 20), Device.Xbox, "", pad, 12);
            else UiKit.TextIn(c, pad, new Rect2(r.Position.X + 548, r.Position.Y, 120, r.Size.Y), 22, Materials.Bone, HorizontalAlignment.Left);
            y += 42;
        }
        var info = new Rect2(panel.Position.X + 760, panel.Position.Y + 112, panel.Size.X - 800, 360);
        c.DrawRect(info, Materials.Rgb(12, 11, 14, .9f)); c.DrawRect(info, Materials.Iron, false, 2);
        UiKit.TextIn(c, "Dispositivo", new Rect2(info.Position + new Vector2(0, 8), new Vector2(info.Size.X, 32)), 22, Materials.Gold, HorizontalAlignment.Left, 16);
        Wrap(c, "Controle detectado: Xbox (XInput). Os ícones seguem o último dispositivo usado.",
             new Rect2(info.Position + new Vector2(16, 48), new Vector2(info.Size.X - 32, 80)), 22, Materials.Bone);
        UiKit.Text(c, "Ícones", new Vector2(info.Position.X + 16, info.Position.Y + 186), 11, Materials.Ash);
        UiKit.TextIn(c, "<   Automático   >", new Rect2(info.Position.X + 16, info.Position.Y + 194, info.Size.X - 32, 32), 22, Materials.Bone, HorizontalAlignment.Left);
        float px = info.Position.X + 30;
        foreach (var b in new[] { "A", "B", "X", "Y" }) { UiKit.Prompt(c, new Vector2(px, info.Position.Y + 256), Device.Xbox, "", b, 12); px += 34; }
        px += 16;
        foreach (var b in new[] { "A", "B", "X", "Y" }) { UiKit.Prompt(c, new Vector2(px, info.Position.Y + 256), Device.PlayStation, "", b, 12); px += 34; }
        Wrap(c, "Conflito: uma tecla já usada mostra qual ação a ocupa e pede confirmação para trocar.",
             new Rect2(info.Position + new Vector2(16, 286), new Vector2(info.Size.X - 32, 60)), 11, Faint, 4);
        Prompts(c, c.Size.Y - 22, ("Enter", "A", "Mudar tecla"), ("Del", "X", "Limpar"), ("Q E", "LB RB", "Abas"), ("R", "Y", "Restaurar padrão"), ("Esc", "B", "Voltar"));
    }

    // ------------------------------------------------------------------ character and inventory

    private static void ItemSlot(Control c, Rect2 r, string? label, Umbra.Domain.Gameplay.Rarity? rarity, bool focus = false, string? glyph = null)
    {
        c.DrawRect(r, Materials.Rgb(14, 12, 16));
        var border = rarity is { } rr ? Materials.Rarity(rr) : Materials.Iron;
        c.DrawRect(r, border, false, rarity == null ? 1 : 2);
        if (glyph != null) UiKit.TextIn(c, glyph, r, 22, rarity is { } q ? Materials.Rarity(q) : Materials.Ash);
        else if (label != null) UiKit.TextIn(c, label, r, 11, Faint);
        if (focus) c.DrawRect(r.Grow(3), Materials.Gold, false, 2);
    }

    private static void Inventory(Control c)
    {
        Shade(c, .55f);
        var panel = new Rect2(c.Size.X - 700, 20, 680, c.Size.Y - 64);
        UiKit.Frame(c, panel);
        Title(c, "PERSONAGEM", new Rect2(panel.Position + new Vector2(24, 8), new Vector2(300, 44)), 22);
        Tabs(c, new Rect2(panel.Position.X + 10, panel.Position.Y + 48, 640, 32), new[] { "Equipamento", "Materiais", "Gemas", "Baú" }, 0);
        // paper doll
        var doll = new Vector2(panel.Position.X + 200, panel.Position.Y + 282);
        c.DrawCircle(doll + new Vector2(0, -92), 18, Materials.Rgb(30, 28, 34));
        c.DrawRect(new Rect2(doll + new Vector2(-30, -70), new Vector2(60, 90)), Materials.Rgb(30, 28, 34));
        c.DrawRect(new Rect2(doll + new Vector2(-28, 20), new Vector2(24, 70)), Materials.Rgb(30, 28, 34));
        c.DrawRect(new Rect2(doll + new Vector2(4, 20), new Vector2(24, 70)), Materials.Rgb(30, 28, 34));
        var R = Umbra.Domain.Gameplay.Rarity.Raro; var M = Umbra.Domain.Gameplay.Rarity.Magico; var C = Umbra.Domain.Gameplay.Rarity.Comum; var L = Umbra.Domain.Gameplay.Rarity.Lendario;
        Vector2 S(float x, float y) => doll + new Vector2(x, y);
        var size = new Vector2(52, 52);
        ItemSlot(c, new Rect2(S(-26, -178), size), "Elmo", M, glyph: "E");
        ItemSlot(c, new Rect2(S(64, -158), size), "Amuleto", null);
        ItemSlot(c, new Rect2(S(-26, -50), size), "Peito", C, true, "P");
        ItemSlot(c, new Rect2(S(-130, -60), size), "Luvas", R, glyph: "L");
        ItemSlot(c, new Rect2(S(-130, 10), size), "Arma", L, glyph: "M");
        ItemSlot(c, new Rect2(S(78, 10), size), "Mão sec.", null);
        ItemSlot(c, new Rect2(S(78, -60), size), "Anel", M, glyph: "A");
        ItemSlot(c, new Rect2(S(78, 80), size), "Anel", null);
        ItemSlot(c, new Rect2(S(-26, 104), size), "Botas", C, glyph: "B");
        // stats
        var stats = new Rect2(panel.Position.X + 380, panel.Position.Y + 112, 276, 226);
        c.DrawRect(stats, Materials.Rgb(12, 11, 14, .9f)); c.DrawRect(stats, Materials.Iron, false, 1);
        var lines = new (string, string)[] { ("Vida", "190"), ("Fúria", "100"), ("Ataque", "22"), ("Defesa", "8"), ("Crítico", "5%"), ("Velocidade", "4,8") };
        for (int i = 0; i < lines.Length; i++)
        {
            var r = new Rect2(stats.Position.X + 12, stats.Position.Y + 10 + i * 34, stats.Size.X - 24, 32);
            UiKit.TextIn(c, lines[i].Item1, r, 22, Materials.Ash, HorizontalAlignment.Left);
            UiKit.TextIn(c, lines[i].Item2, r, 22, Materials.Bone, HorizontalAlignment.Right);
        }
        // bag: 10 x 3
        var bagOrigin = new Vector2(panel.Position.X + 24, panel.Position.Y + 474);
        UiKit.Text(c, "MOCHILA  7 / 30", new Vector2(bagOrigin.X, bagOrigin.Y - 10), 11, Materials.Ash);
        var bag = new Dictionary<int, (Umbra.Domain.Gameplay.Rarity, string)> { [0] = (R, "P"), [1] = (C, "B"), [2] = (M, "A"), [5] = (C, "E"), [11] = (L, "M"), [12] = (M, "L"), [20] = (C, "A") };
        for (int i = 0; i < 30; i++)
        {
            var r = new Rect2(bagOrigin + new Vector2(i % 10 * 63, i / 10 * 63), new Vector2(56, 56));
            if (bag.TryGetValue(i, out var it)) ItemSlot(c, r, null, it.Item1, i == 0, it.Item2); else ItemSlot(c, r, null, null);
        }
        // tooltip of the focused bag item, with the comparison against the equipped one
        var tip = new Rect2(panel.Position.X - 330, panel.Position.Y + 300, 316, 330);
        Tooltip(c, tip, "Cota Remendada", R, "Peitoral  —  nível 12", "Armadura 38", new[] { "+12 de vida", "+4% de redução de dano", "+2 de Força" },
                new[] { ("+14 armadura", true), ("+12 vida", true), ("-3% velocidade", false) });
        var eq = new Rect2(panel.Position.X - 330, panel.Position.Y + 96, 316, 190);
        Tooltip(c, eq, "Gibão de Couro", C, "Peitoral  —  EQUIPADO", "Armadura 24", new[] { "+3% de velocidade" }, null);
        Prompts(c, c.Size.Y - 22, ("Enter", "A", "Equipar"), ("Del", "X", "Descartar"), ("Shift", "LT", "Comparar"), ("Q E", "LB RB", "Abas"), ("I", "B", "Fechar"));
    }

    private static void Tooltip(Control c, Rect2 r, string name, Umbra.Domain.Gameplay.Rarity rarity, string type, string main, string[] affixes, (string Text, bool Better)[]? compare)
    {
        var color = Materials.Rarity(rarity);
        c.DrawRect(r, Materials.Rgb(10, 9, 12, .96f));
        c.DrawRect(r, color, false, 2);
        var head = new Rect2(r.Position, new Vector2(r.Size.X, 52));
        c.DrawRect(head, new Color(color, .14f));
        string tag = rarity switch { Umbra.Domain.Gameplay.Rarity.Comum => "C", Umbra.Domain.Gameplay.Rarity.Magico => "M", Umbra.Domain.Gameplay.Rarity.Raro => "R", _ => "L" };
        string rarityName = rarity switch { Umbra.Domain.Gameplay.Rarity.Comum => "Comum", Umbra.Domain.Gameplay.Rarity.Magico => "Mágico", Umbra.Domain.Gameplay.Rarity.Raro => "Raro", _ => "Lendário" };
        UiKit.TextIn(c, $"[{tag}] {name}", new Rect2(r.Position + new Vector2(0, 2), new Vector2(r.Size.X, 30)), 22, color, HorizontalAlignment.Left, 12);
        UiKit.TextIn(c, rarityName + "  —  " + type, new Rect2(r.Position + new Vector2(0, 30), new Vector2(r.Size.X, 18)), 11, Materials.Ash, HorizontalAlignment.Left, 12);
        float y = r.Position.Y + 62;
        UiKit.TextIn(c, main, new Rect2(r.Position.X, y, r.Size.X, 30), 22, Materials.Bone, HorizontalAlignment.Left, 12); y += 34;
        foreach (var a in affixes) { UiKit.TextIn(c, a, new Rect2(r.Position.X, y, r.Size.X, 28), 22, Materials.Rgb(140, 170, 230), HorizontalAlignment.Left, 12); y += 28; }
        if (compare == null) return;
        y += 8;
        c.DrawRect(new Rect2(r.Position.X + 12, y, r.Size.X - 24, 1), Materials.Iron);
        UiKit.Text(c, "COMPARADO AO EQUIPADO", new Vector2(r.Position.X + 12, y + 18), 11, Materials.Ash); y += 26;
        foreach (var (text, better) in compare)
        {
            var row = new Rect2(r.Position.X, y, r.Size.X, 26);
            UiKit.TextIn(c, text, row, 22, better ? Materials.Rgb(140, 210, 120) : Materials.Rgb(230, 110, 100), HorizontalAlignment.Left, 12);
            UiKit.TextIn(c, better ? "melhor" : "pior", row, 11, Materials.Ash, HorizontalAlignment.Right, 12);
            y += 26;
        }
    }

    // ------------------------------------------------------------------ skill tree (Bárbaro)

    private enum NodeState { Learned, Available, Locked }

    private static void Node(Control c, Vector2 p, float r, NodeState s, bool diamond, string? rank = null, bool focus = false)
    {
        var fill = s == NodeState.Learned ? Materials.Rgb(120, 86, 40) : Materials.Rgb(24, 22, 28);
        var edge = s == NodeState.Learned ? Materials.Gold : s == NodeState.Available ? Materials.Bone : Materials.Iron;
        if (diamond)
        {
            var pts = new[] { p + new Vector2(0, -r), p + new Vector2(r, 0), p + new Vector2(0, r), p + new Vector2(-r, 0) };
            c.DrawColoredPolygon(pts, fill);
            c.DrawPolyline(new[] { pts[0], pts[1], pts[2], pts[3], pts[0] }, edge, 2);
        }
        else { c.DrawCircle(p, r, fill); c.DrawArc(p, r, 0, Mathf.Tau, 24, edge, 2); }
        if (focus) c.DrawArc(p, r + 7, 0, Mathf.Tau, 32, Materials.Gold, 2);
        if (rank != null) UiKit.TextIn(c, rank, new Rect2(p.X - 30, p.Y + r + 2, 60, 18), 11, s == NodeState.Locked ? Faint : Materials.Bone);
    }

    private static void Tree(Control c)
    {
        Shade(c, .94f);
        Title(c, "ÁRVORE DE HABILIDADES  —  BÁRBARO", new Rect2(40, 16, 700, 44), 22);
        UiKit.TextIn(c, "Pontos: 3", new Rect2(40, 16, 860, 44), 22, Materials.Gold, HorizontalAlignment.Right);
        var center = new Vector2(450, 380);
        var tiers = new (string Category, float Y, (string Name, NodeState State, string Rank)[] Skills)[]
        {
            ("Suprema", -272, new[] { ("Fúria Primordial", NodeState.Locked, "0/1") }),
            ("Defensiva", -176, new[] { ("Pele de Ferro", NodeState.Available, "0/5") }),
            ("Mobilidade e Suporte", -80, new[] { ("Investida", NodeState.Learned, "1/5"), ("Grito de Guerra", NodeState.Available, "0/5") }),
            ("Principal", 40, new[] { ("Redemoinho", NodeState.Available, "0/5"), ("Ruptura", NodeState.Learned, "2/5"), ("Martelo Ancestral", NodeState.Locked, "0/5") }),
            ("Básica", 180, new[] { ("Golpe Brutal", NodeState.Learned, "3/5"), ("Frenesi", NodeState.Available, "0/5") }),
        };
        // trunk
        c.DrawLine(center + new Vector2(0, 230), center + new Vector2(0, -250), Materials.Iron, 4);
        c.DrawLine(center + new Vector2(0, 230), center + new Vector2(0, -80), Materials.Gold, 4);
        Vector2 ruptura = default;
        foreach (var (category, ty, skills) in tiers)
        {
            float y = center.Y + ty;
            UiKit.Text(c, category.ToUpperInvariant(), new Vector2(40, y + 4), 11, Faint);
            for (int i = 0; i < skills.Length; i++)
            {
                float x = center.X + (i - (skills.Length - 1) / 2f) * 220;
                var p = new Vector2(x, y);
                c.DrawLine(new Vector2(center.X, y), p, skills[i].State == NodeState.Learned ? Materials.Gold : Materials.Iron, 3);
                bool focus = skills[i].Name == "Ruptura";
                Node(c, p, 22, skills[i].State, true, skills[i].Rank, focus);
                UiKit.TextIn(c, skills[i].Name, new Rect2(p.X - 110, p.Y - 46, 220, 18), 11, skills[i].State == NodeState.Locked ? Faint : Materials.Bone);
                if (focus) ruptura = p;
            }
        }
        // Ruptura's options: behaviour (C), specialisation (E), transformation (T) — names from the 0.16 catalog
        var opts = new (string Name, NodeState S, float Ang, float Dist)[]
        {
            ("Hemorragia", NodeState.Learned, -55, 70), ("Fenda Profunda", NodeState.Available, -25, 84), ("Golpe Fendido", NodeState.Available, 5, 70),
            ("Lâmina Pesada", NodeState.Learned, 35, 84), ("Economia", NodeState.Available, 62, 70),
        };
        foreach (var (name, s, ang, dist) in opts)
        {
            var q = ruptura + new Vector2(Mathf.Cos(Mathf.DegToRad(ang)), Mathf.Sin(Mathf.DegToRad(ang))) * dist;
            c.DrawLine(ruptura, q, s == NodeState.Learned ? Materials.Gold : Materials.Iron, 2);
            Node(c, q, 9, s, false);
        }
        // virtual cursor (the only screen where it exists, by decision of the Game Director)
        var cursor = ruptura + new Vector2(64, 46);
        c.DrawArc(cursor, 9, 0, Mathf.Tau, 20, Materials.Bone, 2);
        c.DrawLine(cursor + new Vector2(-14, 0), cursor + new Vector2(-6, 0), Materials.Bone, 2);
        c.DrawLine(cursor + new Vector2(6, 0), cursor + new Vector2(14, 0), Materials.Bone, 2);
        // detail panel
        var info = new Rect2(c.Size.X - 360, 70, 330, c.Size.Y - 140);
        UiKit.Frame(c, info);
        UiKit.TextIn(c, "Ruptura", new Rect2(info.Position + new Vector2(0, 12), new Vector2(info.Size.X, 34)), 22, Materials.Gold, HorizontalAlignment.Left, 18);
        UiKit.TextIn(c, "Principal  —  Físico  —  Cone", new Rect2(info.Position + new Vector2(0, 44), new Vector2(info.Size.X, 18)), 11, Materials.Ash, HorizontalAlignment.Left, 18);
        UiKit.TextIn(c, "Grau 2 / 5", new Rect2(info.Position + new Vector2(0, 66), new Vector2(info.Size.X, 30)), 22, Materials.Bone, HorizontalAlignment.Left, 18);
        Wrap(c, "145% em cone de 2 tiles; 28 Fúria.", new Rect2(info.Position + new Vector2(18, 100), new Vector2(info.Size.X - 36, 60)), 22, Materials.Bone);
        float y2 = info.Position.Y + 176;
        UiKit.Text(c, "OPÇÕES", new Vector2(info.Position.X + 18, y2), 11, Materials.Ash); y2 += 10;
        var opt = new (string Tag, string Name, string Text, bool On)[]
        {
            ("C", "Hemorragia", "Sangramento de 45% em 4 s.", true), ("C", "Fenda Profunda", "Cone de 3 tiles; lentidão de 30% por 2 s.", false),
            ("C", "Golpe Fendido", "+60% contra alvos que sangram.", false), ("E", "Lâmina Pesada", "+25% de dano.", true), ("E", "Economia", "Custa 22 Fúria.", false),
        };
        foreach (var (tag, name, text, on) in opt)
        {
            UiKit.TextIn(c, $"{tag}  {name}", new Rect2(info.Position.X, y2, info.Size.X, 26), 22, on ? Materials.Gold : Materials.Ash, HorizontalAlignment.Left, 18);
            UiKit.TextIn(c, text, new Rect2(info.Position.X, y2 + 22, info.Size.X, 18), 11, Faint, HorizontalAlignment.Left, 46);
            y2 += 46;
        }
        UiKit.Text(c, "Zoom 100%", new Vector2(40, c.Size.Y - 44), 11, Faint);
        Prompts(c, c.Size.Y - 22, ("Enter", "A", "Aprender"), ("Del", "X", "Devolver ponto"), ("Roda", "RS", "Zoom"), ("Arrastar", "LS", "Mover"), ("K", "B", "Fechar"));
    }
}
