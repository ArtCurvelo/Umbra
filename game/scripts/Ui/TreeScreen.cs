using Godot;
using Umbra.Domain.Heroes;
using Umbra.Domain.Skills;

namespace Umbra.Game.Ui;

/// <summary>
/// Bárbaro skill tree (approved mockup) on UMBRA's real tree (step 5.8c): the nine catalog skills along the trunk, ranks
/// 1–15, level locks, and the seven options of the focused skill in a fan (3 Comportamento, exclusive; Especialização 0/1
/// and Transformação 2/3, one of each pair). Buying and refunding go through TreeRules (player.unlock_skill / respec.refund);
/// Y/R equips the focused skill (a Básica to the attack button, the others to the first free slot).
/// The virtual cursor exists only here (Game Director decision): the left stick moves it and it snaps to nodes.
/// Arrows/D-pad jump between nodes; Q/E or LB/RB and the mouse wheel zoom; the right mouse button drags.
/// Class passives (class:*) are not shown yet: they wait for a mockup.
/// </summary>
public partial class TreeScreen : Screen
{
    private sealed record SkillNode(string Id, string Name, string Category, string Shape, string Text, float Tier, float Column);

    private static readonly SkillNode[] Nodes =
    {
        new("barb2_furia_primordial", "Fúria Primordial", "Suprema", "Bônus", "+25% de ataque e +15% de velocidade de ataque e de movimento por 10 s; recarga de 70 s.", -272, 0),
        new("barb2_pele_ferro", "Pele de Ferro", "Defensiva", "Barreira", "Barreira de 22% da vida máxima por 5 s; recarga de 22 s.", -176, 0),
        new("barb2_investida", "Investida", "Mobilidade", "Investida", "Avança 4 tiles e causa 90%; recarga de 9 s.", -80, -.5f),
        new("barb2_grito", "Grito de Guerra", "Suporte", "Bônus", "+15% de dano por 6 s; recarga de 18 s.", -80, .5f),
        new("barb2_redemoinho", "Redemoinho", "Principal", "Canalizada", "Canalizado: 65% por pulso (a cada 0,5 s) em raio 2; 12 Fúria/s; anda a 70% da velocidade.", 40, -1),
        new("barb2_ruptura", "Ruptura", "Principal", "Cone", "145% em cone de 2 tiles; 28 Fúria.", 40, 0),
        new("barb2_martelo", "Martelo Ancestral", "Principal", "Área", "185% em área ao redor do alvo; 35 Fúria.", 40, 1),
        new("barb2_golpe_brutal", "Golpe Brutal", "Básica", "Corpo a corpo", "110% físico; alcance 1; gera 9 Fúria.", 180, -.5f),
        new("barb2_frenesi", "Frenesi", "Básica", "Corpo a corpo", "70% por golpe; alcance 1; gera 6 Fúria; cada acerto dá +5% de velocidade de ataque por 3 s.", 180, .5f),
    };
    private const int Options = 7;

    private readonly Main _main;
    private int _focus = 5;                       // 0..8 skills, 9..15 options of the anchored skill
    private int _anchor = 5;                      // whose options are fanned out (Ruptura)
    private float _zoom = 1f;
    private Vector2 _pan, _cursor;
    private bool _ready, _cursorActive;
    private string _message = "";
    private readonly List<Vector2> _centers = new();

    public TreeScreen(Main main) { _main = main; }
    private Hero Hero => _main.Session.Hero;
    private SkillTree Tree => Hero.Tree;
    public float Zoom => _zoom;
    public Vector2 Cursor => _cursor;
    public string FocusedId => _focus < Nodes.Length ? Nodes[_focus].Id : OptionKey(_focus - Nodes.Length);
    /// <summary>Tree key of what has focus: learn:&lt;skill&gt; or one of its option keys.</summary>
    public string FocusedKey => _focus < Nodes.Length ? Root(Nodes[_focus]) : OptionKey(_focus - Nodes.Length);
    public int Points => Tree.Points;

    private static string Root(SkillNode n) => "learn:" + n.Id;
    private string OptionKey(int k) => Tuning.OptionKeys(Nodes[_anchor].Id)[k];
    private PyDict? NodeData(string key) => Tree.Nodes.TryGetValue(key, out var d) ? d : null;
    private string NameOf(string key) => NodeData(key)?.Str("name", key) ?? key;
    private int Required(string key) => (int)(NodeData(key)?.Num("required_level", 1) ?? 1);

    public override string FocusText
    {
        get
        {
            string key = FocusedKey; int rank = Tree.Rank(key);
            string state = Tree.LevelBlocked(key) ? $"bloqueada até o nível {Required(key)}"
                         : _focus < Nodes.Length ? (rank > 0 ? $"rank {rank} de {TreeCatalog.MaxRank}" : "não aprendida")
                         : Tree.Has(key) ? (Tree.Active(key) ? "comprada" : "comprada, inativa") : "disponível";
            return $"{NameOf(key)}, {state}";
        }
    }

    private static readonly Vector2 Center = new(450, 380);
    private Vector2 ToScreen(Vector2 local) => Center + _pan + (local) * _zoom;
    private Vector2 NodePos(SkillNode n) => ToScreen(new Vector2(n.Column * 220, n.Tier));
    private Vector2 OptionPos(int k)
    {
        float ang = Mathf.DegToRad(-80 + k * 26); float dist = (k % 2 == 0 ? 72 : 88) * _zoom;
        return NodePos(Nodes[_anchor]) + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist;
    }
    private Vector2 FocusPos(int i) => i < Nodes.Length ? NodePos(Nodes[i]) : OptionPos(i - Nodes.Length);

    public override void OnOpen() { _cursor = FocusPos(_focus) + new Vector2(18, 14); }

    private void SetFocus(int i) { _focus = i; if (i < Nodes.Length) _anchor = i; }

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
            Snap(_cursor, 30 * Mathf.Max(.7f, _zoom), 18 * Mathf.Max(.7f, _zoom));
            if (_cursor.X < 40 || _cursor.X > Size.X - 400 || _cursor.Y < 60 || _cursor.Y > Size.Y - 60)
                _pan -= (_cursor - Size / 2).Normalized() * 260f * (float)delta;     // pushing the edge pans
        }
    }

    private void Snap(Vector2 at, float nodeRadius, float optionRadius)
    {
        for (int k = 0; k < Options; k++) if (OptionPos(k).DistanceTo(at) < optionRadius) { _focus = Nodes.Length + k; return; }
        for (int i = 0; i < Nodes.Length; i++) if (NodePos(Nodes[i]).DistanceTo(at) < nodeRadius) { SetFocus(i); return; }
    }

    public void MoveCursor(Vector2 to) { _cursorActive = true; _cursor = to; Snap(to, 30, 18); }

    public override void OnNav(Nav nav)
    {
        bool stick = new Vector2(Input.GetJoyAxis(0, JoyAxis.LeftX), Input.GetJoyAxis(0, JoyAxis.LeftY)).Length() > .5f;
        switch (nav)
        {
            case Nav.Up or Nav.Down or Nav.Left or Nav.Right when !stick:
                SetFocus(Spatial.Next(_centers, _focus, nav)); _cursor = FocusPos(_focus) + new Vector2(18, 14); _message = ""; break;
            case Nav.Accept: Raise(); break;
            case Nav.Delete: Lower(); break;
            case Nav.Special: EquipFocused(); break;
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
        string key = FocusedKey;
        if (TreeRules.Learn(Hero, key))
        {
            _message = _focus < Nodes.Length ? $"{NameOf(key)}: rank {Tree.Rank(key)}." : $"{NameOf(key)}: comprada.";
            return;
        }
        _message = Tree.LevelBlocked(key) ? $"Requer o nível {Required(key)}."
                 : Tree.Points <= 0 ? "Sem pontos."
                 : _focus < Nodes.Length && Tree.Rank(key) >= TreeCatalog.MaxRank ? "Rank máximo."
                 : _focus >= Nodes.Length && !Tree.Has(Root(Nodes[_anchor])) ? "Aprenda a habilidade antes."
                 : Tree.Has(key) ? "Já comprada." : "Outra opção do mesmo grupo já foi escolhida.";
    }

    private void Lower()
    {
        string key = FocusedKey;
        if (!Tree.Has(key)) return;
        _message = TreeRules.Refund(Hero, key).Message;
    }

    private void EquipFocused()
    {
        if (_focus >= Nodes.Length) return;
        string id = Nodes[_focus].Id;
        if (!TreeRules.Learned(Hero, id)) { _message = "Aprenda a habilidade antes de equipar."; return; }
        int index = TreeRules.IsBasic(Hero, id) ? 0 : FreeSlot(id);
        _message = TreeRules.Equip(Hero, id, index).Message;
    }

    /// <summary>Where Y puts a non-basic skill: where it already is, else the first free of the slice's two skill buttons,
    /// else the first.</summary>
    private int FreeSlot(string id)
    {
        if (TreeRules.IndexOf(Hero, id) is { } at and > 0) return at;
        for (int i = 1; i <= 2; i++) if (Hero.EquippedMainSkills[i - 1] == null) return i;
        return 1;
    }

    private enum State { Learned, Available, Locked }

    private State StateOf(string key) => Tree.Has(key) ? State.Learned : Tree.LevelBlocked(key) ? State.Locked : State.Available;

    protected override void Paint()
    {
        _centers.Clear();
        Parts.Shade(this, .94f);
        UiKit.TextIn(this, "ÁRVORE DE HABILIDADES  —  BÁRBARO", new Rect2(40, 16, 700, 44), 22, Materials.Gold, HorizontalAlignment.Left);
        UiKit.TextIn(this, $"Nível {Hero.Level}   Pontos: {Tree.Points}", new Rect2(40, 16, Size.X - 420, 44), 22, Materials.Gold, HorizontalAlignment.Right);
        var trunkTop = ToScreen(new Vector2(0, -272)); var trunkBottom = ToScreen(new Vector2(0, 230));
        DrawLine(trunkBottom, trunkTop, Materials.Iron, 4 * _zoom);
        string last = "";
        foreach (var n in Nodes)
        {
            var p = NodePos(n);
            if (n.Category != last && !(n.Category == "Suporte")) UiKit.Text(this, n.Category.ToUpperInvariant(), new Vector2(40, p.Y + 4), 11, Parts.Faint);
            last = n.Category;
            DrawLine(new Vector2(ToScreen(Vector2.Zero).X, p.Y), p, StateOf(Root(n)) == State.Learned ? Materials.Gold : Materials.Iron, 3 * _zoom);
        }
        // options of the anchored skill, in a fan: filled when bought (dim if inactive), gold ring on focus
        var fp = NodePos(Nodes[_anchor]); var keys = Tuning.OptionKeys(Nodes[_anchor].Id);
        for (int k = 0; k < Options; k++)
        {
            var q = OptionPos(k); string key = keys[k]; var s = StateOf(key); float r = 9 * _zoom;
            bool owned = Tree.Has(key), active = owned && Tree.Active(key);
            DrawLine(fp, q, owned ? Materials.Gold : Materials.Iron, 2);
            DrawCircle(q, r, active ? Materials.Rgb(120, 86, 40) : owned ? Materials.Rgb(60, 46, 30) : Materials.Rgb(24, 22, 28));
            DrawArc(q, r, 0, Mathf.Tau, 20, owned ? Materials.Gold : s == State.Available ? Materials.Bone : Materials.Iron, 2);
            if (_focus == Nodes.Length + k) DrawArc(q, r + 5, 0, Mathf.Tau, 24, Materials.Gold, 2);
        }
        var slots = TreeRules.Slots(Hero);
        for (int i = 0; i < Nodes.Length; i++)
        {
            var n = Nodes[i]; var p = NodePos(n); string key = Root(n); var s = StateOf(key);
            float r = 22 * _zoom;
            var fill = s == State.Learned ? Materials.Rgb(120, 86, 40) : Materials.Rgb(24, 22, 28);
            var edge = s == State.Learned ? Materials.Gold : s == State.Available ? Materials.Bone : Materials.Iron;
            var pts = new[] { p + new Vector2(0, -r), p + new Vector2(r, 0), p + new Vector2(0, r), p + new Vector2(-r, 0) };
            DrawColoredPolygon(pts, fill);
            DrawPolyline(new[] { pts[0], pts[1], pts[2], pts[3], pts[0] }, edge, 2);
            if (i == _focus) DrawArc(p, r + 7, 0, Mathf.Tau, 32, Materials.Gold, 2);
            int slot = slots.IndexOf(n.Id);
            if (slot >= 0) UiKit.TextIn(this, (slot + 1).ToString(), new Rect2(p - new Vector2(r, r), new Vector2(2 * r, 2 * r)), 11, Materials.Bone);
            string rank = s == State.Locked ? $"Nível {Required(key)}" : $"{Tree.Rank(key)}/{TreeCatalog.MaxRank}";
            UiKit.TextIn(this, rank, new Rect2(p.X - 40, p.Y + r + 2, 80, 18), 11, s == State.Locked ? Parts.Faint : Materials.Bone);
            UiKit.TextIn(this, n.Name, new Rect2(p.X - 110, p.Y - r - 24, 220, 18), 11, s == State.Locked ? Parts.Faint : Materials.Bone);
            _centers.Add(p);
            int index = i;
            Hit(new Rect2(p - new Vector2(r + 6, r + 6), new Vector2(2 * r + 12, 2 * r + 12)), () => SetFocus(index), () => { SetFocus(index); Raise(); });
        }
        for (int k = 0; k < Options; k++)
        {
            var q = OptionPos(k); float r = 9 * _zoom; int index = Nodes.Length + k;
            _centers.Add(q);
            Hit(new Rect2(q - new Vector2(r + 5, r + 5), new Vector2(2 * r + 10, 2 * r + 10)), () => _focus = index, () => { _focus = index; Raise(); });
        }
        // virtual cursor
        if (_cursorActive)
        {
            DrawArc(_cursor, 9, 0, Mathf.Tau, 20, Materials.Bone, 2);
            DrawLine(_cursor + new Vector2(-14, 0), _cursor + new Vector2(-6, 0), Materials.Bone, 2);
            DrawLine(_cursor + new Vector2(6, 0), _cursor + new Vector2(14, 0), Materials.Bone, 2);
        }
        PaintDetail(Nodes[_anchor]);
        UiKit.Text(this, $"Zoom {Mathf.RoundToInt(_zoom * 100)}%", new Vector2(40, Size.Y - 44), 11, Parts.Faint);
        if (_message != "") UiKit.Text(this, _message, new Vector2(300, Size.Y - 44), 11, Materials.Gold);
        Parts.Prompts(this, Size.Y - 22, ("Enter", "A", "Aprender"), ("Del", "X", "Devolver ponto"), ("R", "Y", "Equipar"),
                      ("Q E", "LB", "Zoom"), ("Botão dir.", "LS", "Mover"), ("K", "B", "Fechar"));
    }

    private static readonly string[] Groups = { "C", "C", "C", "E", "E", "T", "T" };

    private void PaintDetail(SkillNode n)
    {
        var info = new Rect2(Size.X - 360, 70, 330, Size.Y - 140);
        UiKit.Frame(this, info);
        UiKit.TextIn(this, n.Name, new Rect2(info.Position + new Vector2(0, 12), new Vector2(info.Size.X, 34)), 22, Materials.Gold, HorizontalAlignment.Left, 18);
        UiKit.TextIn(this, $"{n.Category}  —  Físico  —  {n.Shape}", new Rect2(info.Position + new Vector2(0, 44), new Vector2(info.Size.X, 18)), 11, Materials.Ash, HorizontalAlignment.Left, 18);
        string key = Root(n); int rank = Tree.Rank(key);
        string head = Tree.LevelBlocked(key) ? $"Requer o nível {Required(key)}" : rank == 0 ? "Não aprendida" : $"Rank {rank} / {TreeCatalog.MaxRank}";
        UiKit.TextIn(this, head, new Rect2(info.Position + new Vector2(0, 66), new Vector2(info.Size.X, 30)), 22, rank > 0 ? Materials.Bone : Parts.Faint, HorizontalAlignment.Left, 18);
        float y = Parts.Wrap(this, n.Text, new Rect2(info.Position + new Vector2(18, 100), new Vector2(info.Size.X - 36, 90)), 22, rank > 0 ? Materials.Bone : Materials.Ash) + 16;
        var row = n.Category == "Básica" ? HeroTables.BasicRow(n.Id) : HeroTables.SkillRow(n.Id);
        if (row != null && row.Num("multiplier", 0) > 0)
        {
            var picks = TreeRules.Chosen(Tree, n.Id);
            double now = Tuning.Resolve(row, Math.Max(1, rank), picks).Num("multiplier");
            UiKit.Text(this, $"Dano {(rank > 0 ? "agora" : "no rank 1")}: {Math.Round(now * 100)}%", new Vector2(info.Position.X + 18, y + 8), 22, Materials.Bone);
            if (rank > 0 && rank < TreeCatalog.MaxRank)
                UiKit.Text(this, $"Próximo rank: {Math.Round(Tuning.Resolve(row, rank + 1, picks).Num("multiplier") * 100)}%", new Vector2(info.Position.X + 18, y + 36), 22, Parts.Good);
            y += 60;
        }
        UiKit.Text(this, "OPÇÕES", new Vector2(info.Position.X + 18, y), 11, Materials.Ash); y += 8;
        var keys = Tuning.OptionKeys(n.Id);
        for (int k = 0; k < keys.Count; k++)
        {
            if (y > info.End.Y - 24) break;
            string o = keys[k]; var s = StateOf(o);
            bool focus = _focus == Nodes.Length + k;
            var color = Tree.Has(o) ? (Tree.Active(o) ? Materials.Gold : Materials.Ash) : s == State.Locked ? Parts.Faint : Materials.Bone;
            string suffix = s == State.Locked ? $"  (nível {Required(o)})" : Tree.Has(o) && !Tree.Active(o) ? "  (inativa)" : "";
            var line = new Rect2(info.Position.X, y, info.Size.X, 24);
            if (focus) DrawRect(new Rect2(line.Position + new Vector2(10, 0), line.Size - new Vector2(20, 0)), Materials.Rgb(44, 30, 28));
            UiKit.TextIn(this, $"{Groups[k]} {NameOf(o)}{suffix}", line, 22, color, HorizontalAlignment.Left, 18);
            y += 26;
        }
    }
}
