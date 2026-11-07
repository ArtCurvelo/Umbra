using Godot;
using Umbra.Domain.Gameplay;

namespace Umbra.Game.Ui;

/// <summary>
/// Bárbaro skill tree (approved mockup): trunk by category, catalog names. The three skills of the slice's combat can be
/// raised (grades, PROVISÓRIO +10% each); the others and the C/E/T options arrive with Phase 5 and show as locked.
/// The virtual cursor exists only here (Game Director decision): the left stick moves it and it snaps to nodes.
/// Arrows/D-pad jump between nodes; Q/E or LB/RB and the mouse wheel zoom; the right mouse button drags.
/// </summary>
public partial class TreeScreen : Screen
{
    private sealed record SkillNode(string Id, string Name, string Category, string Shape, string Text, float Tier, float Column, string[] Options);

    private static readonly SkillNode[] Nodes =
    {
        new("barb2_furia_primordial", "Fúria Primordial", "Suprema", "Bônus", "+25% de ataque e +15% de velocidade de ataque e de movimento por 10 s; recarga de 70 s.", -272, 0, Array.Empty<string>()),
        new("barb2_pele_ferro", "Pele de Ferro", "Defensiva", "Barreira", "Barreira de 22% da vida máxima por 5 s; recarga de 22 s.", -176, 0, Array.Empty<string>()),
        new("barb2_investida", "Investida", "Mobilidade", "Investida", "Avança 4 tiles e causa 90%; recarga de 9 s.", -80, -.5f,
            new[] { "C Quebra-guarda", "C Arrastar", "C Impulso", "E Pernas Fortes", "E Longo Alcance", "T Dupla Carga", "T Carga do Touro" }),
        new("barb2_grito", "Grito de Guerra", "Suporte", "Bônus", "+15% de dano por 6 s; recarga de 18 s.", -80, .5f, Array.Empty<string>()),
        new("barb2_redemoinho", "Redemoinho", "Principal", "Canalizada", "Canalizado: 65% por pulso (a cada 0,5 s) em raio 2; 12 Fúria/s; anda a 70% da velocidade.", 40, -1, Array.Empty<string>()),
        new("barb2_ruptura", "Ruptura", "Principal", "Cone", "145% em cone de 2 tiles; 28 Fúria.", 40, 0,
            new[] { "C Hemorragia", "C Fenda Profunda", "C Golpe Fendido", "E Lâmina Pesada", "E Economia", "T Rasgo Reto", "T Sacrifício" }),
        new("barb2_martelo", "Martelo Ancestral", "Principal", "Área", "185% em área ao redor do alvo; 35 Fúria.", 40, 1, Array.Empty<string>()),
        new("barb2_golpe_brutal", "Golpe Brutal", "Básica", "Corpo a corpo", "110% físico; alcance 1; gera 9 Fúria.", 180, -.5f,
            new[] { "C Impacto", "C Fenda", "C Ímpeto", "E Força Bruta", "E Sede de Fúria", "T Golpe de Sangue", "T Arremesso" }),
        new("barb2_frenesi", "Frenesi", "Básica", "Corpo a corpo", "70% por golpe; alcance 1; gera 6 Fúria; cada acerto dá +5% de velocidade de ataque por 3 s.", 180, .5f, Array.Empty<string>()),
    };

    private static readonly Dictionary<string, SkillDef> Live = new()
    {
        [SliceData.GolpeBrutal.Id] = SliceData.GolpeBrutal, [SliceData.Ruptura.Id] = SliceData.Ruptura, [SliceData.Investida.Id] = SliceData.Investida,
    };

    private readonly Main _main;
    private int _focus = 5;                       // Ruptura
    private float _zoom = 1f;
    private Vector2 _pan, _cursor;
    private bool _ready, _cursorActive;
    private string _message = "";
    private readonly List<Vector2> _centers = new();

    public TreeScreen(Main main) { _main = main; }
    private SkillRanks Ranks => _main.Sim.Hero.Ranks;
    public float Zoom => _zoom;
    public Vector2 Cursor => _cursor;
    public string FocusedId => Nodes[_focus].Id;
    public override string FocusText => $"{Nodes[_focus].Name}, {(Ranks.Learnable(Nodes[_focus].Id) ? $"grau {Ranks.Rank(Nodes[_focus].Id)} de {SkillRanks.MaxRank}" : "bloqueada")}";

    private static readonly Vector2 Center = new(450, 380);
    private Vector2 ToScreen(Vector2 local) => Center + _pan + (local) * _zoom;
    private Vector2 NodePos(SkillNode n) => ToScreen(new Vector2(n.Column * 220, n.Tier));

    public override void OnOpen() { _cursor = NodePos(Nodes[_focus]) + new Vector2(18, 14); }

    public override void Tick(double delta)
    {
        if (_ready && Input.IsActionJustPressed("skill_tree")) { Stack.Pop(); return; }
        _ready = true;
        // virtual cursor: left stick only (keyboard and mouse have their own ways)
        var stick = new Vector2(Input.GetJoyAxis(0, JoyAxis.LeftX), Input.GetJoyAxis(0, JoyAxis.LeftY));
        if (stick.Length() > .25f)
        {
            _cursorActive = true;
            _cursor += stick * 620f * (float)delta;
            _cursor = _cursor.Clamp(Vector2.Zero, Size);
            for (int i = 0; i < Nodes.Length; i++)
                if (NodePos(Nodes[i]).DistanceTo(_cursor) < 30 * Mathf.Max(.7f, _zoom)) { _focus = i; break; }
            if (_cursor.X < 40 || _cursor.X > Size.X - 400 || _cursor.Y < 60 || _cursor.Y > Size.Y - 60)
                _pan -= (_cursor - Size / 2).Normalized() * 260f * (float)delta;     // pushing the edge pans
        }
    }

    public void MoveCursor(Vector2 to) { _cursorActive = true; _cursor = to; for (int i = 0; i < Nodes.Length; i++) if (NodePos(Nodes[i]).DistanceTo(to) < 30) _focus = i; }

    public override void OnNav(Nav nav)
    {
        bool stick = new Vector2(Input.GetJoyAxis(0, JoyAxis.LeftX), Input.GetJoyAxis(0, JoyAxis.LeftY)).Length() > .5f;
        switch (nav)
        {
            case Nav.Up or Nav.Down or Nav.Left or Nav.Right when !stick:
                _focus = Spatial.Next(_centers, _focus, nav); _cursor = NodePos(Nodes[_focus]) + new Vector2(18, 14); _message = ""; break;
            case Nav.Accept: Raise(); break;
            case Nav.Delete: Lower(); break;
            case Nav.TabLeft: SetZoom(_zoom - .15f); break;
            case Nav.TabRight: SetZoom(_zoom + .15f); break;
            case Nav.Back: Stack.Pop(); break;
        }
    }

    public override void OnWheel(float steps) => SetZoom(_zoom + steps * .1f);
    private void SetZoom(float z) => _zoom = Mathf.Clamp(z, .6f, 1.6f);

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseMotion { ButtonMask: MouseButtonMask.Right } drag) { _pan += drag.Relative; AcceptEvent(); return; }
        base._GuiInput(e);
    }

    private void Raise()
    {
        var node = Nodes[_focus];
        if (!Ranks.Learnable(node.Id)) { _message = "Esta habilidade chega com a Fase 5."; return; }
        _message = Ranks.Raise(node.Id) ? $"{node.Name}: grau {Ranks.Rank(node.Id)}." : Ranks.Points <= 0 ? "Sem pontos." : "Grau máximo.";
    }

    private void Lower()
    {
        var node = Nodes[_focus];
        if (Ranks.Lower(node.Id)) _message = $"{node.Name}: ponto devolvido.";
    }

    private enum State { Learned, Available, Locked }

    private State StateOf(SkillNode n) => Ranks.Learnable(n.Id) ? State.Learned : State.Locked;

    protected override void Paint()
    {
        _centers.Clear();
        Parts.Shade(this, .94f);
        UiKit.TextIn(this, "ÁRVORE DE HABILIDADES  —  BÁRBARO", new Rect2(40, 16, 700, 44), 22, Materials.Gold, HorizontalAlignment.Left);
        UiKit.TextIn(this, $"Pontos: {Ranks.Points}", new Rect2(40, 16, Size.X - 420, 44), 22, Materials.Gold, HorizontalAlignment.Right);
        var trunkTop = ToScreen(new Vector2(0, -272)); var trunkBottom = ToScreen(new Vector2(0, 230));
        DrawLine(trunkBottom, trunkTop, Materials.Iron, 4 * _zoom);
        DrawLine(trunkBottom, ToScreen(new Vector2(0, -80)), Materials.Gold, 4 * _zoom);
        string last = "";
        foreach (var n in Nodes)
        {
            var p = NodePos(n);
            if (n.Category != last && !(n.Category == "Suporte")) UiKit.Text(this, n.Category.ToUpperInvariant(), new Vector2(40, p.Y + 4), 11, Parts.Faint);
            last = n.Category;
            var s = StateOf(n);
            DrawLine(new Vector2(ToScreen(Vector2.Zero).X, p.Y), p, s == State.Locked ? Materials.Iron : Materials.Gold, 3 * _zoom);
        }
        for (int i = 0; i < Nodes.Length; i++)
        {
            var n = Nodes[i]; var p = NodePos(n); var s = StateOf(n);
            float r = 22 * _zoom;
            var fill = s == State.Learned ? Materials.Rgb(120, 86, 40) : Materials.Rgb(24, 22, 28);
            var edge = s == State.Learned ? Materials.Gold : s == State.Available ? Materials.Bone : Materials.Iron;
            var pts = new[] { p + new Vector2(0, -r), p + new Vector2(r, 0), p + new Vector2(0, r), p + new Vector2(-r, 0) };
            DrawColoredPolygon(pts, fill);
            DrawPolyline(new[] { pts[0], pts[1], pts[2], pts[3], pts[0] }, edge, 2);
            if (i == _focus) DrawArc(p, r + 7, 0, Mathf.Tau, 32, Materials.Gold, 2);
            string rank = Ranks.Learnable(n.Id) ? $"{Ranks.Rank(n.Id)}/{SkillRanks.MaxRank}" : "Fase 5";
            UiKit.TextIn(this, rank, new Rect2(p.X - 40, p.Y + r + 2, 80, 18), 11, s == State.Locked ? Parts.Faint : Materials.Bone);
            UiKit.TextIn(this, n.Name, new Rect2(p.X - 110, p.Y - r - 24, 220, 18), 11, s == State.Locked ? Parts.Faint : Materials.Bone);
            _centers.Add(p);
            int index = i;
            Hit(new Rect2(p - new Vector2(r + 6, r + 6), new Vector2(2 * r + 12, 2 * r + 12)), () => _focus = index, () => { _focus = index; Raise(); });
        }
        // options of the focused skill: shown in a fan, locked until Phase 5
        var f = Nodes[_focus]; var fp = NodePos(f);
        for (int k = 0; k < f.Options.Length && k < 5; k++)
        {
            float ang = Mathf.DegToRad(-55 + k * 29); float dist = (k % 2 == 0 ? 70 : 84) * _zoom;
            var q = fp + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist;
            DrawLine(fp, q, Materials.Iron, 2);
            DrawCircle(q, 9 * _zoom, Materials.Rgb(24, 22, 28)); DrawArc(q, 9 * _zoom, 0, Mathf.Tau, 20, Materials.Iron, 2);
        }
        // virtual cursor
        if (_cursorActive)
        {
            DrawArc(_cursor, 9, 0, Mathf.Tau, 20, Materials.Bone, 2);
            DrawLine(_cursor + new Vector2(-14, 0), _cursor + new Vector2(-6, 0), Materials.Bone, 2);
            DrawLine(_cursor + new Vector2(6, 0), _cursor + new Vector2(14, 0), Materials.Bone, 2);
        }
        PaintDetail(f);
        UiKit.Text(this, $"Zoom {Mathf.RoundToInt(_zoom * 100)}%", new Vector2(40, Size.Y - 44), 11, Parts.Faint);
        if (_message != "") UiKit.Text(this, _message, new Vector2(300, Size.Y - 44), 11, Materials.Gold);
        Parts.Prompts(this, Size.Y - 22, ("Enter", "A", "Aprender"), ("Del", "X", "Devolver ponto"), ("Q E", "LB", "Zoom"), ("Botão dir.", "LS", "Mover"), ("K", "B", "Fechar"));
    }

    private void PaintDetail(SkillNode n)
    {
        var info = new Rect2(Size.X - 360, 70, 330, Size.Y - 140);
        UiKit.Frame(this, info);
        UiKit.TextIn(this, n.Name, new Rect2(info.Position + new Vector2(0, 12), new Vector2(info.Size.X, 34)), 22, Materials.Gold, HorizontalAlignment.Left, 18);
        UiKit.TextIn(this, $"{n.Category}  —  Físico  —  {n.Shape}", new Rect2(info.Position + new Vector2(0, 44), new Vector2(info.Size.X, 18)), 11, Materials.Ash, HorizontalAlignment.Left, 18);
        float y;
        if (Live.TryGetValue(n.Id, out var def))
        {
            int rank = Ranks.Rank(n.Id);
            UiKit.TextIn(this, $"Grau {rank} / {SkillRanks.MaxRank}", new Rect2(info.Position + new Vector2(0, 66), new Vector2(info.Size.X, 30)), 22, Materials.Bone, HorizontalAlignment.Left, 18);
            y = Parts.Wrap(this, n.Text, new Rect2(info.Position + new Vector2(18, 100), new Vector2(info.Size.X - 36, 90)), 22, Materials.Bone) + 16;
            int now = Mathf.RoundToInt(def.Multiplier * Ranks.Factor(n.Id) * 100);
            UiKit.Text(this, $"Dano agora: {now}%", new Vector2(info.Position.X + 18, y + 8), 22, Materials.Bone);
            if (rank < SkillRanks.MaxRank)
                UiKit.Text(this, $"Próximo grau: {Mathf.RoundToInt(def.Multiplier * (Ranks.Factor(n.Id) + (float)SkillRanks.PerRank) * 100)}%", new Vector2(info.Position.X + 18, y + 36), 22, Parts.Good);
            UiKit.Text(this, "+10% por grau (PROVISÓRIO)", new Vector2(info.Position.X + 18, y + 56), 11, Parts.Faint);
            y += 76;
        }
        else
        {
            UiKit.TextIn(this, "Chega na Fase 5", new Rect2(info.Position + new Vector2(0, 66), new Vector2(info.Size.X, 30)), 22, Parts.Faint, HorizontalAlignment.Left, 18);
            y = Parts.Wrap(this, n.Text, new Rect2(info.Position + new Vector2(18, 100), new Vector2(info.Size.X - 36, 90)), 22, Materials.Ash) + 22;
        }
        if (n.Options.Length == 0) return;
        UiKit.Text(this, "OPÇÕES (FASE 5)", new Vector2(info.Position.X + 18, y), 11, Materials.Ash); y += 8;
        foreach (var o in n.Options)
        {
            if (y > info.End.Y - 24) break;
            UiKit.TextIn(this, o, new Rect2(info.Position.X, y, info.Size.X, 24), 22, Parts.Faint, HorizontalAlignment.Left, 18);
            y += 26;
        }
    }
}
