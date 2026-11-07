using Godot;
using Umbra.Domain.Gameplay;

namespace Umbra.Game.Ui;

/// <summary>
/// Character and inventory (approved mockup): paper doll with nine slots, stats with the equipment bonus, a 10 x 3 bag,
/// the focused item's tooltip with the comparison against the equipped one (colour and words). Enter/A equips or
/// unequips, Del/X drops the item at the hero's feet (nothing is destroyed). Only the Equipamento tab exists in the slice.
/// </summary>
public partial class CharacterScreen : Screen
{
    private static readonly string[] TabNames = { "Equipamento", "Materiais", "Gemas", "Baú" };
    private static readonly (EquipSlot Slot, Vector2 Offset)[] Doll =
    {
        (EquipSlot.Elmo, new(-26, -178)), (EquipSlot.Amuleto, new(64, -158)), (EquipSlot.Luvas, new(-130, -60)), (EquipSlot.Peitoral, new(-26, -50)),
        (EquipSlot.Anel1, new(78, -60)), (EquipSlot.Arma, new(-130, 10)), (EquipSlot.MaoSecundaria, new(78, 10)), (EquipSlot.Anel2, new(78, 80)),
        (EquipSlot.Botas, new(-26, 104)),
    };

    private readonly Main _main;
    private int _focus = Doll.Length;              // 0..8 equipment, 9.. bag cells
    private int _tab;
    private string _message = "";
    private bool _ready;
    private readonly List<Vector2> _centers = new();

    public CharacterScreen(Main main) { _main = main; }
    private Simulation Sim => _main.Sim;
    private Hero Hero => Sim.Hero;

    public override string FocusText
    {
        get
        {
            var item = FocusedItem();
            string where = _focus < Doll.Length ? "equipado, " + Equipment.Label(Doll[_focus].Slot) : $"mochila, espaço {_focus - Doll.Length + 1}";
            return item == null ? $"{where}: vazio" : $"{where}: {item.Name}, {item.RarityName}";
        }
    }

    private Item? FocusedItem() => _focus < Doll.Length ? Hero.Equipment[Doll[_focus].Slot]
                                 : _focus - Doll.Length < Sim.Inventory.Count ? Sim.Inventory[_focus - Doll.Length] : null;

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
            if (Hero.Equipment[slot] == null) return;
            _message = Sim.Unequip(slot) ? "Guardado na mochila." : "A mochila está cheia.";
            return;
        }
        int index = _focus - Doll.Length;
        if (index >= Sim.Inventory.Count) return;
        var item = Sim.Inventory[index];
        if (Sim.Equip(index)) _message = $"Equipado: {item.Name}.";
    }

    private void DropFocused()
    {
        int index = _focus - Doll.Length;
        if (_focus < Doll.Length || index >= Sim.Inventory.Count) return;
        string name = Sim.Inventory[index].Name;
        if (Sim.Discard(index))
        {
            foreach (var e in Sim.Events) if (e is DroppedEvent d) _main.Actors.AddDrop(d.Drop);
            Sim.Events.Clear();
            _message = $"{name} foi largado no chão.";
        }
    }

    private List<(string Text, bool Better)> Compare(Item item, Item? equipped)
    {
        var lines = new List<(string, bool)>();
        foreach (Stat s in Enum.GetValues<Stat>())
        {
            int delta = item.Total(s) - (equipped?.Total(s) ?? 0);
            if (delta != 0) lines.Add(($"{(delta > 0 ? "+" : "-")}{Math.Abs(delta)} {ItemText.StatName(s)}", delta > 0));
        }
        return lines;
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
            Parts.ItemSlot(this, r, Hero.Equipment[Doll[i].Slot], Equipment.Label(Doll[i].Slot), i == _focus);
            Focusable(r, i);
        }
        // stats with the equipment bonus
        var stats = new Rect2(panel.Position.X + 380, panel.Position.Y + 112, 276, 226);
        Parts.Box(this, stats);
        var lines = new (string, int, int)[]
        {
            ("Vida", Hero.MaxHealth, Hero.MaxHealth - Hero.Stats.MaxHealth), ("Fúria", SliceData.FuryMax, 0),
            ("Ataque", Hero.Attack, Hero.Attack - Hero.Stats.Attack), ("Defesa", Hero.Defense, Hero.Defense - Hero.Stats.Defense),
        };
        for (int i = 0; i < lines.Length; i++)
        {
            var r = new Rect2(stats.Position.X + 12, stats.Position.Y + 10 + i * 34, stats.Size.X - 24, 32);
            UiKit.TextIn(this, lines[i].Item1, r, 22, Materials.Ash, HorizontalAlignment.Left);
            string bonus = lines[i].Item3 > 0 ? $"  (+{lines[i].Item3})" : "";
            UiKit.TextIn(this, lines[i].Item2 + bonus, r, 22, lines[i].Item3 > 0 ? Parts.Good : Materials.Bone, HorizontalAlignment.Right);
        }
        UiKit.TextIn(this, "Velocidade", new Rect2(stats.Position.X + 12, stats.Position.Y + 146, stats.Size.X - 24, 32), 22, Materials.Ash, HorizontalAlignment.Left);
        UiKit.TextIn(this, "4,8", new Rect2(stats.Position.X + 12, stats.Position.Y + 146, stats.Size.X - 24, 32), 22, Materials.Bone, HorizontalAlignment.Right);
        UiKit.Text(this, "Bônus de itens: valores PROVISÓRIOS", new Vector2(stats.Position.X + 12, stats.End.Y - 12), 11, Parts.Faint);
        // bag
        var bag = new Vector2(panel.Position.X + 24, panel.Position.Y + 474);
        UiKit.Text(this, $"MOCHILA  {Sim.Inventory.Count} / {Simulation.BagSize}", new Vector2(bag.X, bag.Y - 10), 11, Materials.Ash);
        for (int i = 0; i < Simulation.BagSize; i++)
        {
            var r = new Rect2(bag + new Vector2(i % 10 * 63, i / 10 * 63), new Vector2(56, 56));
            Parts.ItemSlot(this, r, i < Sim.Inventory.Count ? Sim.Inventory[i] : null, null, Doll.Length + i == _focus);
            Focusable(r, Doll.Length + i);
        }
        // tooltips: the focused item, and the equipped one it would replace
        var item = FocusedItem();
        if (item != null)
        {
            Item? equipped = null;
            if (_focus >= Doll.Length && Hero.Equipment.TargetFor(item) is { } target) equipped = Hero.Equipment[target];
            var compare = _focus >= Doll.Length ? Compare(item, equipped) : null;
            float h = Parts.TooltipHeight(item, compare?.Count ?? 0);
            var tip = new Rect2(panel.Position.X - 330, Mathf.Min(panel.Position.Y + 300, panel.End.Y - h), 316, h);
            Parts.Tooltip(this, tip, item, item.Slot + (_focus < Doll.Length ? "  —  EQUIPADO" : ""), compare);
            if (equipped != null)
            {
                float he = Parts.TooltipHeight(equipped, 0);
                Parts.Tooltip(this, new Rect2(tip.Position.X, Mathf.Max(20, tip.Position.Y - he - 14), 316, he), equipped, equipped.Slot + "  —  EQUIPADO", null);
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
