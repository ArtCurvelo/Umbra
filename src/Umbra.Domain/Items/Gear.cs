using Umbra.Domain.Skills;

namespace Umbra.Domain.Items;

/// <summary>entities/items.InventoryBag: the Equipment page (items) plus the gems, consumables and keys pages and the
/// potion counter. `add` stores a shallow copy, as dict(item_data) does.</summary>
public sealed class Bag
{
    public long Potion;
    public long Capacity = 24;
    public List<PyDict> Items { get; } = new();
    public List<PyDict> Gems { get; } = new();
    public List<PyDict> Consumables { get; } = new();
    public List<PyDict> Keys { get; } = new();

    public bool Add(PyDict record)
    {
        if (Items.Count >= Capacity) return false;
        Items.Add(new PyDict(record)); return true;
    }

    public PyDict? Remove(int index)
    {
        if (index < 0 || index >= Items.Count) return null;
        var row = Items[index]; Items.RemoveAt(index); return row;
    }
}

/// <summary>The hero as the equipment rules see it.</summary>
public interface IGearOwner
{
    string PlayerClass { get; }
    long Level { get; }
    long Gold { get; set; }
    Bag Inventory { get; }
    /// <summary>slot -> record (null = empty), in slot order; null before the first migration.</summary>
    PyDict? Loadout { get; set; }
    /// <summary>The pre-arsenal three slots (weapon/armor/accessory), kept as aliases of the loadout.</summary>
    PyDict Equipment { get; }
    bool CanonicalReady { get; set; }
    string? BasicSkillId { get; }
    string? ActiveWeaponSkill { get; }
    /// <summary>player.materials, created on first use.</summary>
    PyDict Materials { get; }
    List<PyDict> LostItems { get; }
    long PotionCapacity { get; }
    /// <summary>respec.refresh_stats / recalculate_stats: the hero's attributes (step 5.8).</summary>
    void RefreshStats();
}

/// <summary>game/equipment/slots.py, migration.py, selection.py and transactions.py.</summary>
public static class Gear
{
    private static PyDict S => ItemTables.Section("slots");
    public static List<string> Armor => ItemTables.Strings(S.Get("ARMOR"));
    public static List<string> Jewelry => ItemTables.Strings(S.Get("JEWELRY"));
    public static PyDict ClassWeapons(string cls) => S.Sub("CLASS_WEAPONS")!.Sub(cls)!;
    public static bool Support(string slot) => ItemTables.Strings(S.Get("SUPPORT_SLOTS")).Contains(slot);
    public static string Label(string slot) => S.Sub("LABELS")!.Str(slot);

    public static List<string> Slots(IGearOwner p) => Armor.Concat(Jewelry).Concat(ClassWeapons(p.PlayerClass).Keys).ToList();
    public static PyDict Empty(IGearOwner p) => new(Slots(p).Select(s => new KeyValuePair<string, object?>(s, null)));

    public static string DefaultWeaponSlot(IGearOwner p) => p.PlayerClass switch
    {
        "barbaro" => "slashing", "guerreiro" => "blunt", "paladino" => "sword", "ladino" => "dual1", "mago" => "main_hand", "necromante" => "main_hand",
        _ => ClassWeapons(p.PlayerClass).Keys.First(),
    };

    private static bool Caster(IGearOwner p) => p.PlayerClass is "mago" or "necromante";
    private static PyDict? At(IGearOwner p, string slot) => p.Loadout?.Get(slot) as PyDict;

    public static string OccupiedBy(IGearOwner p, string slot) =>
        Caster(p) && slot == "off_hand" && ItemData.IsTwoHanded(At(p, "main_hand")) ? "main_hand" : slot;

    public static string? TargetSlot(IGearOwner p, PyDict record, string? preferred = null)
    {
        string? slot = record.TryGetValue("slot", out var s) ? s as string : record.Get("kind") as string;
        slot = slot switch { "armor" => "chest", "accessory" => "amulet", _ => slot };
        if (slot != null && (Armor.Contains(slot) || slot == "amulet")) return preferred == null || preferred == slot ? slot : null;
        if (slot is "ring" or "ring1" or "ring2")
        {
            if (preferred != null) return preferred is "ring1" or "ring2" ? preferred : null;
            return new[] { "ring1", "ring2" }.FirstOrDefault(r => !Py.Truthy(At(p, r))) ?? "ring1";
        }
        var family = record.Get("weapon_type") as string; var weapons = ClassWeapons(p.PlayerClass);
        if (family == null && slot == "weapon")
        {
            string @default = DefaultWeaponSlot(p);
            if (Caster(p)) return preferred == null || preferred == @default ? @default : null;
            return preferred != null && weapons.ContainsKey(preferred) ? preferred : preferred == null ? @default : null;
        }
        var candidates = weapons.Where(kv => ItemTables.Strings(kv.Value).Contains(family!)).Select(kv => kv.Key).ToList();
        if (preferred != null) return candidates.Contains(preferred) ? preferred : null;
        return candidates.FirstOrDefault(k => !Py.Truthy(At(p, k))) ?? (candidates.Count > 0 ? candidates[0] : null);
    }

    public static string[] Conflicts(IGearOwner p, string slot, PyDict? record = null)
    {
        if (Caster(p))
        {
            if (slot == "main_hand" && ItemData.IsTwoHanded(record)) return new[] { "off_hand" };
            if (slot == "off_hand" && ItemData.IsTwoHanded(At(p, "main_hand"))) return new[] { "main_hand" };
            return Array.Empty<string>();
        }
        if (p.PlayerClass == "bruxo") return slot == "staff" ? new[] { "sword", "focus" } : slot is "sword" or "focus" ? new[] { "staff" } : Array.Empty<string>();
        if (p.PlayerClass == "paladino") return slot == "greatsword" ? new[] { "sword", "shield" } : slot is "sword" or "shield" ? new[] { "greatsword" } : Array.Empty<string>();
        return Array.Empty<string>();
    }

    // ------------------------------------------------------------------ migration.py
    public static PyDict Migrate(IGearOwner p, PyDict? saved = null)
    {
        var result = Empty(p);
        PyDict source = saved != null && saved.Values.Any(Py.Truthy) ? saved : new PyDict
        {
            ["chest"] = p.Equipment.Get("armor"), ["amulet"] = p.Equipment.Get("accessory"), [DefaultWeaponSlot(p)] = p.Equipment.Get("weapon"),
        };
        var records = new PyDict(source.Where(kv => kv.Value is PyDict).Select(kv => new KeyValuePair<string, object?>(kv.Key, Py.DeepCopy(kv.Value))));
        var overflow = new List<PyDict>();
        if (Caster(p))
        {
            var active = p.Equipment.Get("weapon");
            var options = new[] { "main_hand", "staff", "wand" }.Where(records.ContainsKey).Select(k => { var v = (PyDict)records[k]!; records.Remove(k); return (Key: k, Value: v); }).ToList();
            (string Key, PyDict Value)? chosen = options.Cast<(string, PyDict)?>().FirstOrDefault(o => PyEquals(o!.Value.Item2, active)) ?? (options.Count > 0 ? options[0] : null);
            if (chosen is { } c)
            {
                result["main_hand"] = c.Value;
                overflow.AddRange(options.Where(o => o.Key != c.Key).Select(o => o.Value));
            }
            var offOptions = new[] { "off_hand", "focus" }.Where(records.ContainsKey).Select(k => { var v = (PyDict)records[k]!; records.Remove(k); return v; }).ToList();
            var off = offOptions.Count > 0 ? offOptions[0] : null;
            overflow.AddRange(offOptions.Skip(1));
            if (off != null) { if (ItemData.IsTwoHanded(result.Get("main_hand") as PyDict)) overflow.Add(off); else result["off_hand"] = off; }
        }
        foreach (var (key, value) in records)
        {
            if (result.ContainsKey(key)) result[key] = value; else overflow.Add((PyDict)value!);
        }
        p.Inventory.Capacity = Math.Max(p.Inventory.Capacity, p.Inventory.Items.Count + overflow.Count);
        foreach (var record in overflow) p.Inventory.Add(record);
        p.Loadout = result; p.CanonicalReady = true;
        return result;
    }

    /// <summary>Python == on records (deep value equality).</summary>
    public static bool PyEquals(object? a, object? b) => (a, b) switch
    {
        (null, null) => true,
        (PyDict x, PyDict y) => x.Count == y.Count && x.All(kv => y.TryGetValue(kv.Key, out var v) && PyEquals(kv.Value, v)),
        (List<object?> x, List<object?> y) => x.Count == y.Count && x.Zip(y).All(t => PyEquals(t.First, t.Second)),
        (bool x, bool y) => x == y,
        (string x, string y) => x == y,
        (double or long, double or long) => Py.Num(a) == Py.Num(b),
        _ => false,
    };

    public static PyDict Ensure(IGearOwner p)
    {
        var gear = p.Loadout;
        bool legacyMage = Caster(p) && gear != null && new[] { "staff", "wand", "focus" }.Any(gear.ContainsKey);
        bool oldAliases = !p.CanonicalReady && gear != null && !gear.Values.Any(Py.Truthy) && p.Equipment.Values.Any(Py.Truthy);
        if (gear == null || legacyMage || oldAliases) return Migrate(p, legacyMage ? gear : null);
        return gear;
    }

    // ------------------------------------------------------------------ selection.py
    public static string ActiveKey(IGearOwner p, string? skill = null)
    {
        var gear = Ensure(p); string cls = p.PlayerClass;
        string id = !string.IsNullOrEmpty(skill) ? skill : p.BasicSkillId ?? "";          // skill or basic_skill_id or 
        string key;
        if (cls is "barbaro" or "guerreiro")
            key = new[] { "crush", "seismic", "slam" }.Any(id.Contains) ? "blunt" : new[] { "frenzy", "double", "whirlwind" }.Any(id.Contains) ? "dual1" : "slashing";
        else if (cls == "ladino") key = new[] { "knife", "mark", "shot" }.Any(id.Contains) ? "ranged" : "dual1";
        else if (cls == "mago") key = "main_hand";
        else if (cls == "bruxo") key = Py.Truthy(gear.Get("staff")) ? "staff" : "sword";
        else if (cls == "paladino") key = Py.Truthy(gear.Get("greatsword")) ? "greatsword" : "sword";
        else key = "staff";
        if (Py.Truthy(gear.Get(key))) return key;
        return ClassWeapons(cls).Keys.FirstOrDefault(k => Py.Truthy(gear.Get(k)) && !Support(k)) ?? key;
    }

    public static void SyncAliases(IGearOwner p, string? skill = null)
    {
        var gear = Ensure(p);
        p.Equipment["weapon"] = gear.Get(ActiveKey(p, skill)); p.Equipment["armor"] = gear.Get("chest"); p.Equipment["accessory"] = gear.Get("amulet");
    }

    public static List<PyDict> Records(IGearOwner p)
    {
        var gear = Ensure(p);
        return (gear.Values.Any(Py.Truthy) ? gear : p.Equipment).Values.OfType<PyDict>().Where(Py.Truthy).ToList();
    }

    // ------------------------------------------------------------------ transactions.py
    public static (bool Ok, string Message) Equip(IGearOwner p, PyDict record, string? preferred = null)
    {
        Ensure(p);
        if (p.Level < (long)Math.Truncate(record.Num("required_level", 1))) return (false, "Nível insuficiente.");
        var slot = TargetSlot(p, record, preferred);
        if (slot == null) return (false, "Tipo de arma ou espaço incompatível com a classe.");
        var loadout = p.Loadout!;
        var displaced = new[] { slot }.Concat(Conflicts(p, slot, record)).Where(k => Py.Truthy(loadout.Get(k))).Select(k => (Key: k, Item: (PyDict)loadout[k]!)).ToList();
        if (p.Inventory.Items.Count + displaced.Count > p.Inventory.Capacity) return (false, "Libere espaço na mochila para os itens substituídos.");
        foreach (var (key, item) in displaced) { p.Inventory.Add(item); loadout[key] = null; }
        loadout[slot] = record.Copy();
        p.CanonicalReady = true;
        SyncAliases(p);
        p.RefreshStats();
        return (true, "Equipado em " + Label(slot) + ".");
    }

    public static (bool Ok, string Message) FromInventory(IGearOwner p, int index, string? preferred = null)
    {
        if (index < 0 || index >= p.Inventory.Items.Count) return (false, "Selecione um item.");
        var record = p.Inventory.Items[index]; p.Inventory.Items.RemoveAt(index);
        var result = Equip(p, record, preferred);
        if (!result.Ok) p.Inventory.Items.Insert(index, record);
        return result;
    }

    public static bool Unequip(IGearOwner p, string slot)
    {
        var gear = Ensure(p); slot = OccupiedBy(p, slot);
        var item = gear.Get(slot) as PyDict;
        if (item == null || !p.Inventory.Add(item)) return false;
        gear[slot] = null;
        SyncAliases(p); p.RefreshStats();
        return true;
    }

    /// <summary>legendary.active: the aspects of the equipped items that work for this class.</summary>
    public static HashSet<string> ActiveAspects(IGearOwner p)
    {
        var catalog = ItemTables.Section("legendary").Sub("CATALOG")!;
        return Records(p).Select(r => r.Get("aspect") as string).Where(a => a != null && catalog.ContainsKey(a)
            && ((List<object?>)catalog[a]!)[1] is var owner && (owner == null || Equals(owner, p.PlayerClass))).Select(a => a!).ToHashSet();
    }
}

/// <summary>Inventory 2.0 — game/inventory/pages.py and lost.py.</summary>
public static class Pages
{
    private static PyDict P => ItemTables.Section("pages");
    public static long Capacity => (long)P.Num("CAPACITY");
    public static long PotionCap => (long)P.Num("POTION_CAP");
    public static long StackMax => (long)P.Num("STACK_MAX");
    public static string Name(string page) => P.Sub("NAMES")!.Str(page);

    public static string? PageOf(PyDict? record)
    {
        var kind = record == null ? null : record.ContainsKey("kind") ? record.Get("kind") : record.Get("slot");
        return kind switch { "weapon" or "armor" or "accessory" => "equipment", "gem" => "gems", "dungeon_key" => "keys", "consumable" => "consumables", _ => null };
    }

    public static List<PyDict> Rows(Bag bag, string page) => page switch
    {
        "equipment" => bag.Items, "gems" => bag.Gems, "consumables" => bag.Consumables, "keys" => bag.Keys, _ => throw new KeyNotFoundException(page),
    };

    public static long Used(Bag bag, string page) => Rows(bag, page).Count + (page == "consumables" && bag.Potion != 0 ? 1 : 0);
    public static long Free(Bag bag, string page) => Capacity - Used(bag, page);

    public static (bool Ok, string Message) Add(Bag bag, PyDict record)
    {
        var page = PageOf(record);
        if (page == null) return (false, "Este item não pertence a nenhuma página da mochila.");
        if (page == "equipment") return bag.Add(record) ? (true, "") : (false, "Mochila de equipamentos cheia.");
        var target = Rows(bag, page);
        if (page is "gems" or "consumables" && Py.Truthy(record.Get("id")))
        {
            var stack = target.FirstOrDefault(row => PyEqualsId(row, record) && row.Num("count", 1) < StackMax);
            if (stack != null)
            {
                long room = StackMax - (long)stack.Num("count", 1), count = (long)Math.Truncate(record.Num("count", 1));
                long moved = Math.Min(room, count), rest = count - moved;
                if (rest > 0 && Free(bag, page) <= 0) return (false, $"Página \"{Name(page)}\" cheia.");
                stack["count"] = (long)stack.Num("count", 1) + moved;
                if (rest <= 0) return (true, "");
                record = new PyDict(record) { ["count"] = rest };
            }
        }
        if (Free(bag, page) <= 0) return (false, $"Página \"{Name(page)}\" cheia.");
        target.Add(new PyDict(record)); return (true, "");
    }

    private static bool PyEqualsId(PyDict row, PyDict record) => Gear.PyEquals(row.Get("id"), record.Get("id"));

    public static long PotionCapOf(IGearOwner p) => p.PotionCapacity;

    /// <summary>Adds up to the cap; returns how many went in. The page needs a slot for the first potion.</summary>
    public static long AddPotions(IGearOwner p, long amount = 1)
    {
        var bag = p.Inventory; long have = bag.Potion;
        if (have == 0 && Free(bag, "consumables") <= 0) return 0;
        long room = Math.Max(0, PotionCapOf(p) - have), added = Math.Min(room, amount);
        bag.Potion = have + added;
        return added;
    }

    // ------------------------------------------------------------------ lost.py
    public static long LostMax => (long)ItemTables.Section("lost").Num("MAX");

    public static bool Keep(IGearOwner p, PyDict record)
    {
        if (p.LostItems.Count >= LostMax) return false;
        p.LostItems.Add(new PyDict(record)); return true;
    }

    public static (bool Ok, string Message) Recover(IGearOwner p, int index)
    {
        var rows = p.LostItems;
        if (index < 0 || index >= rows.Count) return (false, "Nada para recuperar.");
        var (ok, message) = Add(p.Inventory, rows[index]);
        if (!ok) return (false, message);
        rows.RemoveAt(index); return (true, "Item recuperado.");
    }
}
