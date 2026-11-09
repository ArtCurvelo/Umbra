using Godot;
using Umbra.Domain.Heroes;
using Umbra.Domain.Items;
using Umbra.Domain.Play;
using Umbra.Domain.Skills;

namespace Umbra.Game.Ui;

/// <summary>
/// Character and inventory (approved mockup): paper doll with the Bárbaro's twelve UMBRA slots (step 5.8c), stats, a 10 x 3 bag,
/// the focused item's tooltip with the comparison against the equipped one (colour and words). Enter/A equips or
/// unequips, Del/X drops the item at the hero's feet (nothing is destroyed). Only the Equipamento tab exists in the slice.
/// </summary>
public partial class CharacterScreen : Screen
{
    private static readonly string[] TabNames = { "Equipamento", "Materiais", "Gemas", "Baú" };
    // UMBRA's Bárbaro loadout (equipment.json: ARMOR + JEWELRY + CLASS_WEAPONS.barbaro), around the same figure
    private static readonly (string Slot, Vector2 Offset)[] Doll =
    {
        ("helmet", new(-26, -178)), ("amulet", new(64, -158)), ("gloves", new(-130, -60)), ("chest", new(-26, -50)),
        ("ring1", new(78, -60)), ("ring2", new(78, 2)), ("pants", new(-26, 34)), ("boots", new(-26, 104)),
        ("blunt", new(-188, 10)), ("slashing", new(-130, 10)), ("dual1", new(-188, 72)), ("dual2", new(-130, 72)),
    };

    private readonly Main _main;
    private int _focus = Doll.Length;              // 0..8 equipment, 9.. bag cells
    private int _tab;
    private string _message = "";
    private bool _ready;
    private readonly List<Vector2> _centers = new();

    public CharacterScreen(Main main) { _main = main; }
    private GameSession Session => _main.Session;
    private Hero Hero => Session.Hero;
    private List<PyDict> Bag => Hero.Inventory.Items;
    private PyDict Loadout => Gear.Ensure(Hero);
    private static string Label(string slot) => Gear.Label(slot);

    public override string FocusText
    {
        get
        {
            var item = FocusedItem();
            string where = _focus < Doll.Length ? "equipado, " + Label(Doll[_focus].Slot) : $"mochila, espaço {_focus - Doll.Length + 1}";
            return item == null ? $"{where}: vazio" : $"{where}: {ItemView.Name(item)}, {ItemView.Rarity(item)}";
        }
    }

    private PyDict? FocusedItem() => _focus < Doll.Length ? Loadout.Get(Doll[_focus].Slot) as PyDict
                                   : _focus - Doll.Length < Bag.Count ? Bag[_focus - Doll.Length] : null;

    public override void Tick(double delta)
    {
        if (_ready && Input.IsActionJustPressed("inventory")) Stack.Pop();
        _ready = true;
    }

    public override void OnNav(Nav nav)
    {
        switch (nav)
        {
            case Nav.Up or Nav.Down or Nav.Left or Nav.Right: _focus = Spatial.Next(_centers, _focus, nav); _message = ""; break;
            case Nav.Accept: Use(); break;
            case Nav.Delete: DropFocused(); break;
            case Nav.TabLeft: _tab = (_tab + TabNames.Length - 1) % TabNames.Length; break;
            case Nav.TabRight: _tab = (_tab + 1) % TabNames.Length; break;
            case Nav.Back: Stack.Pop(); break;
        }
    }

    private void Use()
    {
        if (_focus < Doll.Length)
        {
            var slot = Doll[_focus].Slot;
            if (!Py.Truthy(Loadout.Get(slot))) return;
            _message = Gear.Unequip(Hero, slot) ? "Guardado na mochila." : "A mochila está cheia.";
            return;
        }
        int index = _focus - Doll.Length;
        if (index >= Bag.Count) return;
        if (!ItemView.Equipment(Bag[index])) { _message = "Este item não se equipa."; return; }
        _message = Gear.FromInventory(Hero, index).Message;
    }

    private void DropFocused()
    {
        int index = _focus - Doll.Length;
        if (_focus < Doll.Length || index >= Bag.Count) return;
        string name = ItemView.Name(Bag[index]);
        if (Session.Discard(index) is { } dropped)
        {
            _main.Actors.AddDrop(dropped);
            _message = $"{name} foi largado no chão.";
        }
    }

    protected override void Paint()
    {
        _centers.Clear();
        Parts.Shade(this, .55f);
        var panel = new Rect2(Size.X - 700, 20, 680, Size.Y - 64);
        UiKit.Frame(this, panel);
        UiKit.TextIn(this, "PERSONAGEM", new Rect2(panel.Position + new Vector2(24, 8), new Vector2(300, 44)), 22, Materials.Gold, HorizontalAlignment.Left);
        var tabs = Parts.Tabs(this, new Rect2(panel.Position.X + 10, panel.Position.Y + 48, 640, 32), TabNames, _tab);
        for (int i = 0; i < tabs.Count; i++) { int t = i; Hit(tabs[i], null, () => _tab = t); }
        if (_tab != 0)
        {
            Parts.Wrap(this, $"A aba {TabNames[_tab]} chega com a migração dos sistemas do UMBRA (Fase 5).",
                       new Rect2(panel.Position + new Vector2(40, 120), new Vector2(panel.Size.X - 80, 200)), 22, Materials.Ash);
            Footer();
            return;
        }
        var doll = new Vector2(panel.Position.X + 200, panel.Position.Y + 282);
        DrawCircle(doll + new Vector2(0, -92), 18, Materials.Rgb(30, 28, 34));
        DrawRect(new Rect2(doll + new Vector2(-30, -70), new Vector2(60, 90)), Materials.Rgb(30, 28, 34));
        DrawRect(new Rect2(doll + new Vector2(-28, 20), new Vector2(24, 70)), Materials.Rgb(30, 28, 34));
        DrawRect(new Rect2(doll + new Vector2(4, 20), new Vector2(24, 70)), Materials.Rgb(30, 28, 34));
        for (int i = 0; i < Doll.Length; i++)
        {
            var r = new Rect2(doll + Doll[i].Offset, new Vector2(52, 52));
            Parts.ItemSlot(this, r, Loadout.Get(Doll[i].Slot) as PyDict, Label(Doll[i].Slot), i == _focus);
            Focusable(r, i);
        }
        // stats with the equipment bonus
        var stats = new Rect2(panel.Position.X + 380, panel.Position.Y + 112, 276, 226);
        Parts.Box(this, stats);
        var lines = new (string, int, int)[]
        {
            ("Vida", Hero.MaxHealth, 0), (Hero.ResourceLabel, Hero.MaxResource, 0),
            ("Ataque", Hero.Attack, Hero.Attack - Hero.BaseAttack), ("Armadura", Hero.Defense, Hero.Defense - Hero.BaseDefense),
        };
        for (int i = 0; i < lines.Length; i++)
        {
            var r = new Rect2(stats.Position.X + 12, stats.Position.Y + 10 + i * 34, stats.Size.X - 24, 32);
            UiKit.TextIn(this, lines[i].Item1, r, 22, Materials.Ash, HorizontalAlignment.Left);
            string bonus = lines[i].Item3 > 0 ? $"  (+{lines[i].Item3})" : "";
            UiKit.TextIn(this, lines[i].Item2 + bonus, r, 22, lines[i].Item3 > 0 ? Parts.Good : Materials.Bone, HorizontalAlignment.Right);
        }
        UiKit.TextIn(this, $"Nível {Hero.Level}", new Rect2(stats.Position.X + 12, stats.Position.Y + 180, stats.Size.X - 24, 32), 22, Materials.Ash, HorizontalAlignment.Left);
        UiKit.TextIn(this, $"{Hero.Gold} ouro", new Rect2(stats.Position.X + 12, stats.Position.Y + 180, stats.Size.X - 24, 32), 22, Materials.Gold, HorizontalAlignment.Right);
        UiKit.TextIn(this, "Velocidade", new Rect2(stats.Position.X + 12, stats.Position.Y + 146, stats.Size.X - 24, 32), 22, Materials.Ash, HorizontalAlignment.Left);
        UiKit.TextIn(this, Locomotion.Speed.ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")), new Rect2(stats.Position.X + 12, stats.Position.Y + 146, stats.Size.X - 24, 32), 22, Materials.Bone, HorizontalAlignment.Right);
        UiKit.Text(this, "Entre parênteses: itens e árvore", new Vector2(stats.Position.X + 12, stats.End.Y + 16), 11, Parts.Faint);
        // bag
        var bag = new Vector2(panel.Position.X + 24, panel.Position.Y + 474);
        int capacity = (int)Hero.Inventory.Capacity;
        UiKit.Text(this, $"MOCHILA  {Bag.Count} / {capacity}", new Vector2(bag.X, bag.Y - 10), 11, Materials.Ash);
        for (int i = 0; i < capacity; i++)
        {
            var r = new Rect2(bag + new Vector2(i % 10 * 63, i / 10 * 63), new Vector2(56, 56));
            Parts.ItemSlot(this, r, i < Bag.Count ? Bag[i] : null, null, Doll.Length + i == _focus);
            Focusable(r, Doll.Length + i);
        }
        // tooltips: the focused item, and the equipped one it would replace
        var item = FocusedItem();
        if (item != null)
        {
            PyDict? equipped = null;
            bool inBag = _focus >= Doll.Length && ItemView.Equipment(item);
            if (inBag && Gear.TargetSlot(Hero, item) is { } target) equipped = Loadout.Get(target) as PyDict;
            var compare = inBag ? ItemView.Compare(item, equipped) : null;
            float h = Parts.TooltipHeight(item, compare?.Count ?? 0);
            var tip = new Rect2(panel.Position.X - 330, Mathf.Min(panel.Position.Y + 300, panel.End.Y - h), 316, h);
            Parts.Tooltip(this, tip, item, ItemView.Kind(item) + (_focus < Doll.Length ? "  —  EQUIPADO" : ""), compare);
            if (equipped != null)
            {
                float he = Parts.TooltipHeight(equipped, 0);
                Parts.Tooltip(this, new Rect2(tip.Position.X, Mathf.Max(20, tip.Position.Y - he - 14), 316, he), equipped, ItemView.Kind(equipped) + "  —  EQUIPADO", null);
            }
        }
        if (_message != "") UiKit.Text(this, _message, new Vector2(panel.Position.X + 24, panel.End.Y - 12), 11, Materials.Gold);
        Footer();
    }

    private void Footer()
    {
        bool equippedFocus = _focus < Doll.Length;
        Parts.Prompts(this, Size.Y - 22, ("Enter", "A", equippedFocus ? "Guardar" : "Equipar"), ("Del", "X", "Largar no chão"), ("Q E", "LB", "Abas"), ("I", "B", "Fechar"));
    }

    private void Focusable(Rect2 r, int id)
    {
        while (_centers.Count <= id) _centers.Add(Vector2.Zero);
        _centers[id] = r.GetCenter();
        Hit(r, () => _focus = id, () => { _focus = id; Use(); });
    }
}
