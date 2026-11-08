using Umbra.Domain.Combat;
using Umbra.Domain.Skills;
using Umbra.Domain.Util;

namespace Umbra.Domain.Items;

/// <summary>Crafting materials — game/equipment/materials.py (player.materials = {id: count}).</summary>
public static class Materials
{
    private static PyDict Catalog => ItemTables.Section("materials").Sub("CATALOG")!;
    public const string TemperDust = "material:poeira_tempera", SocketIron = "material:ferragem_engaste",
                        Primordial = "material:essencia_primordial", KeyShard = "material:fragmento_chave";

    public static long Count(IGearOwner p, string material) => (long)Math.Truncate(p.Materials.Num(material, 0));

    public static void Add(IGearOwner p, string material, long amount = 1)
    {
        if (!Catalog.ContainsKey(material) || amount <= 0) return;
        p.Materials[material] = (long)p.Materials.Num(material, 0) + amount;
    }

    public static bool Has(IGearOwner p, PyDict? cost) => (cost ?? new PyDict()).All(kv => Count(p, kv.Key) >= Py.Num(kv.Value));

    public static bool Spend(IGearOwner p, PyDict? cost)
    {
        if (!Has(p, cost)) return false;
        foreach (var (k, v) in cost ?? new PyDict())
        {
            p.Materials[k] = (long)p.Materials.Num(k) - (long)Py.Num(v);
            if (p.Materials.Num(k) <= 0) p.Materials.Remove(k);
        }
        return true;
    }

    public static string Name(string material) => Catalog.Get(material) is List<object?> row ? (string)row[0]! : material;

    public static string DescribeCost(PyDict? cost, long gold = 0)
    {
        var parts = (cost ?? new PyDict()).Select(kv => $"{Name(kv.Key)} x{kv.Value}").ToList();
        if (gold != 0) parts.Add($"{gold} ouro");
        return parts.Count > 0 ? string.Join(" + ", parts) : "grátis";
    }

    public static PyDict Yield(string rarity) => ItemTables.Section("materials").Sub("RECYCLE_YIELD")!.Sub(rarity)!;
}

/// <summary>Gems and sockets — game/equipment/gems.py. Pending values (None) give no bonus.</summary>
public static class Gems
{
    private static PyDict G => ItemTables.Section("gems");
    public static List<string> Grades => ItemTables.Strings(G.Get("GRADES"));
    private static PyDict Families => G.Sub("FAMILIES")!;

    public static string GemId(string family, long grade)
    {
        if (!Families.ContainsKey(family) || !(1 <= grade && grade <= Grades.Count)) throw new ArgumentException($"gema inválida: {family}:{grade}");
        return $"{family}:{grade}";
    }

    public static (string Family, int Grade)? Parse(object? gid)
    {
        string text = gid as string ?? Convert.ToString(gid) ?? "";
        int colon = text.IndexOf(':'); string family = colon < 0 ? text : text[..colon], grade = colon < 0 ? "" : text[(colon + 1)..];
        if (!Families.ContainsKey(family) || grade.Length == 0 || !grade.All(char.IsAsciiDigit) || !(1 <= int.Parse(grade) && int.Parse(grade) <= Grades.Count)) return null;
        return (family, int.Parse(grade));
    }

    public static string Name(string gid)
    {
        var (family, grade) = Parse(gid)!.Value;
        return $"{((List<object?>)Families[family]!)[0]} {Grades[grade - 1]}";
    }

    public static PyDict Make(string family, long grade, long count = 1) => new()
    {
        ["kind"] = "gem", ["id"] = GemId(family, grade), ["family"] = family, ["grade"] = grade, ["count"] = Math.Max(1, count), ["name"] = Name(GemId(family, grade)),
    };

    public static (string Stat, object? Value) Effect(string gid, string category)
    {
        var (family, grade) = Parse(gid)!.Value;
        var row = (List<object?>)((PyDict)((List<object?>)Families[family]!)[1]!)[category]!;
        return ((string)row[0]!, ((List<object?>)row[1]!)[grade - 1]);
    }

    public static PyDict Bonuses(PyDict record)
    {
        var totals = new PyDict();
        if (!ItemData.IsEquipment(record)) return totals;
        string category = ItemData.GemCategory(record);
        foreach (var gid in record.List("sockets"))
        {
            if (!Py.Truthy(gid) || Parse(gid) == null) continue;
            var (stat, value) = Effect((string)gid!, category);
            if (value != null) totals[stat] = Add(totals.Get(stat) ?? 0L, value);
        }
        return totals;
    }

    private static object Add(object a, object b) => a is long x && b is long y ? (object)(x + y) : Py.Num(a) + Py.Num(b);

    public static (bool Ok, string Message) AddSocket(PyDict record)
    {
        ItemData.Normalize(record); var sockets = record.List("sockets");
        if (sockets.Count >= ItemData.SocketLimit(record)) return (false, "Este item já tem o máximo de engastes.");
        sockets.Add(null); return (true, "Engaste adicionado.");
    }

    public static (bool Ok, string Message) Insert(PyDict record, PyDict? stack, int? index = null)
    {
        ItemData.Normalize(record);
        if (stack == null || !Equals(stack.Get("kind"), "gem") || Parse(stack.Get("id")) == null || stack.Num("count", 0) < 1) return (false, "Escolha uma gema.");
        var sockets = record.List("sockets");
        var empty = Enumerable.Range(0, sockets.Count).Where(i => sockets[i] == null).ToList();
        index ??= empty.Count > 0 ? empty[0] : null;
        if (index == null || !empty.Contains(index.Value)) return (false, "Não há engaste livre.");
        sockets[index.Value] = stack["id"]; stack["count"] = (long)stack.Num("count") - 1;
        return (true, $"{Name((string)stack["id"]!)} engastada.");
    }

    public static (bool Ok, object Result) Remove(PyDict record, int index)
    {
        ItemData.Normalize(record); var sockets = record.List("sockets");
        if (index < 0 || index >= sockets.Count || sockets[index] == null) return (false, "Este engaste está vazio.");
        var gid = (string)sockets[index]!; sockets[index] = null;
        var (family, grade) = Parse(gid)!.Value;
        return (true, Make(family, grade));
    }
}

/// <summary>Tempering — game/equipment/temper.py.</summary>
public static class Temper
{
    private static PyDict T => ItemTables.Section("temper");
    private static PyDict Recipes => T.Sub("RECIPES")!;
    public static long Gold => (long)T.Num("GOLD");

    public static List<string> Known(PyDict? unlocks)
    {
        var starting = ItemTables.Strings(T.Get("STARTING")); var found = unlocks?.Get("temper") as List<object?> ?? new List<object?>();
        return Recipes.Keys.Where(r => starting.Contains(r) || found.Contains(r)).ToList();
    }

    public static bool Unlock(PyDict unlocks, string recipe)
    {
        if (unlocks.Get("temper") is not List<object?> rows) { rows = new List<object?>(); unlocks["temper"] = rows; }
        if (Recipes.ContainsKey(recipe) && !rows.Contains(recipe) && !ItemTables.Strings(T.Get("STARTING")).Contains(recipe)) { rows.Add(recipe); return true; }
        return false;
    }

    private static List<object?> Recipe(string id) => (List<object?>)Recipes[id]!;
    public static bool Fits(PyDict record, string recipe) => ItemData.IsEquipment(record) && Equals(Recipe(recipe)[1], ItemData.GemCategory(record));
    public static long ResetsFor(PyDict record) => (long)T.Sub("RESETS")!.Num(ItemData.Category(record), T.Num("DEFAULT_RESETS"));

    public static (bool Ok, string Message) Apply(IGearOwner p, PyDict record, string recipe, PyDict? unlocks, IRandom rng)
    {
        if (!Recipes.ContainsKey(recipe)) return (false, "Receita desconhecida.");
        if (!Known(unlocks).Contains(recipe)) return (false, "Encontre o manual desta receita.");
        if (!Fits(record, recipe)) return (false, "Esta receita não serve para esta peça.");
        var current = ItemData.Normalize(record).Get("temper") as PyDict;
        if (current is { Count: > 0 } && current.Num("resets_left", 0) <= 0) return (false, "Sem redefinições restantes para este item.");
        var cost = T.Sub("COST")!;
        if (!Materials.Has(p, cost)) return (false, $"Falta {Materials.DescribeCost(cost)}.");
        if (p.Gold < Gold) return (false, $"São necessários {Gold} de ouro.");
        Materials.Spend(p, cost); p.Gold -= Gold;
        var row = Recipe(recipe); string name = (string)row[0]!, stat = (string)row[2]!;
        long left = current is { Count: > 0 } ? (long)current.Num("resets_left") - 1 : ResetsFor(record);
        record["temper"] = new PyDict { ["recipe"] = recipe, ["stat"] = stat, ["value"] = (long)rng.RandInt((int)Py.Num(row[3]), (int)Py.Num(row[4])), ["resets_left"] = left };
        p.RefreshStats();
        return (true, $"Têmpera {name}: +{((PyDict)record["temper"]!)["value"]} {T.Sub("STAT_NAMES")!.Str(stat)}.");
    }

    public static PyDict Bonus(PyDict? record)
    {
        var row = record?.Get("temper") as PyDict;
        return row != null && row.Get("stat") is string stat && T.Sub("STAT_NAMES")!.ContainsKey(stat)
            ? new PyDict { [stat] = (long)Math.Truncate(Py.Num(row["value"])) } : new PyDict();
    }
}

/// <summary>Occultist enchanting — game/equipment/enchant.py.</summary>
public static class Enchant
{
    private static PyDict E => ItemTables.Section("enchant");
    private static List<string> Keys => ItemTables.Strings(E.Get("KEYS"));

    public static long Cost(PyDict record) => (long)(E.Num("BASE_GOLD") * Math.Pow(E.Num("GROWTH"), (long)record.Num("enchant_attempts", 0)));

    public static (long Low, long High) ValueRange(PyDict record, string key)
    {
        long centre = Math.Max(1, PyMath.Round((Math.Truncate(record.Num("item_level", 1)) * .035 + 1) * (key is "health" or "mana" ? 3 : 1)));
        return (Math.Max(1, (long)(centre * .8)), Math.Max(1, PyMath.Round(centre * 1.2)));
    }

    public static List<(string Key, (long Low, long High) Range)> Options(PyDict record, string key)
    {
        var current = (record.Sub("affixes") ?? new PyDict()).Keys.Where(k => k != key).ToHashSet();
        return Keys.Where(k => !current.Contains(k)).Select(k => (k, ValueRange(record, k))).ToList();
    }

    public static (bool Ok, string Message, string? NewKey) Apply(IGearOwner p, PyDict record, string key, IRandom rng)
    {
        if (!ItemData.IsEquipment(record) || !(record.Sub("affixes") ?? new PyDict()).ContainsKey(key)) return (false, "Escolha um afixo do item.", null);
        long price = Cost(record);
        if (p.Gold < price) return (false, $"São necessários {price} de ouro.", null);
        var (newKey, (low, high)) = rng.Choice(Options(record, key)); long value = rng.RandInt((int)low, (int)high);
        p.Gold -= price; record["enchant_attempts"] = (long)record.Num("enchant_attempts", 0) + 1;
        var affixes = new PyDict(record.Sub("affixes")!.Where(kv => kv.Key != key)) { [newKey] = value };
        record["affixes"] = affixes;
        if (record.Sub("quality_base") is { Count: > 0 } @base)
        {
            var baseAffixes = new PyDict(@base.Sub("affixes")!.Where(kv => kv.Key != key)) { [newKey] = value };
            @base["affixes"] = baseAffixes;
            if (Equals(record.Get("maximized"), key)) record["maximized"] = newKey;
            ItemData.ApplyQuality(record);
        }
        p.RefreshStats();
        var names = E.Sub("NAMES")!;
        return (true, $"Encantado: +{((PyDict)record["affixes"]!)[newKey]} {names.Str(newKey)}.", newKey);
    }
}

/// <summary>Blacksmith recycling and repair — game/equipment/recycle.py; prices of game/equipment/forge.py.</summary>
public static class Smith
{
    private static PyDict R => ItemTables.Section("recycle");
    private static List<string> Valuable => ItemTables.Strings(R.Get("VALUABLE"));
    public static string Question => R.Str("QUESTION");

    public static long SellPrice(PyDict record) => Math.Max(3, (long)Math.Truncate(record.Num("item_level", 1)) * 3 + (long)Math.Truncate(record.Num("value", 0)) * 2);
    public static long UpgradeCost(PyDict record) => 35 + (long)Math.Truncate(record.Num("item_level", 1)) * 4 + (long)Math.Truncate(record.Num("quality", 0)) * 18;
    public static long GoldFor(PyDict record) => Math.Max(1, SellPrice(record) / 2);
    public static bool Eligible(PyDict record) => ItemData.IsEquipment(record) && !record.T("locked");

    public static bool ToggleJunk(PyDict record)
    {
        if (!ItemData.IsEquipment(record)) return false;
        record["junk"] = !record.T("junk"); return (bool)record["junk"]!;
    }

    public sealed record Plan(string Mode, List<int> Indices, PyDict Tally, long Gold, PyDict Materials, bool Confirm);

    public static Plan MakePlan(IGearOwner p, string mode)
    {
        var items = p.Inventory.Items; var picked = new List<int>();
        for (int i = 0; i < items.Count; i++)
        {
            if (!Eligible(items[i])) continue;
            string r = Rarity.Canonical(items[i].Get("rarity"));
            if ((mode == "junk" && items[i].T("junk")) || mode == r || mode == "all") picked.Add(i);
        }
        var tally = new PyDict(); long gold = 0; var mats = new PyDict();
        foreach (int i in picked)
        {
            string r = Rarity.Canonical(items[i].Get("rarity")); tally[r] = (long)tally.Num(r, 0) + 1; gold += GoldFor(items[i]);
            foreach (var (k, v) in Materials.Yield(r)) mats[k] = (long)mats.Num(k, 0) + (long)Py.Num(v);
        }
        return new Plan(mode, picked, tally, gold, mats, tally.Keys.Any(Valuable.Contains));
    }

    public static (bool Ok, string Message) Execute(IGearOwner p, Plan chosen, bool confirmed = false, PyDict? unlocks = null)
    {
        if (chosen.Indices.Count == 0) return (false, "Nenhum item para reciclar.");
        if (chosen.Confirm && !confirmed) return (false, Question);
        var items = p.Inventory.Items;
        foreach (int i in chosen.Indices.OrderByDescending(i => i))
            if (i < 0 || i >= items.Count || !Eligible(items[i])) return (false, "A mochila mudou; refaça a seleção.");
        long learned = 0;
        foreach (int i in chosen.Indices.OrderByDescending(i => i))
        {
            var record = items[i]; items.RemoveAt(i);
            if (unlocks != null) learned += Codex.LearnFrom(unlocks, record);
        }
        p.Gold += chosen.Gold;
        foreach (var (k, v) in chosen.Materials) Materials.Add(p, k, (long)Py.Num(v));
        string extra = learned != 0 ? $" {learned} desbloqueio(s) novo(s)." : "";
        return (true, $"{chosen.Indices.Count} item(ns) reciclado(s): +{chosen.Gold} de ouro.{extra}");
    }

    public static long RepairCost(PyDict record)
    {
        ItemData.Normalize(record); var d = record.Sub("durability")!;
        long missing = (long)d.Num("max") - (long)d.Num("current");
        return missing != 0 ? (long)(missing * 3 * (1 + .05 * (long)Math.Truncate(record.Num("item_level", 1)))) : 0;
    }

    public static List<(string Where, object Key, PyDict Record)> Damaged(IGearOwner p)
    {
        var rows = new List<(string, object, PyDict)>(); var seen = new HashSet<PyDict>(ReferenceEqualityComparer.Instance);
        foreach (var (key, value) in p.Loadout ?? new PyDict())
            if (value is PyDict record && Py.Truthy(record) && !seen.Contains(record) && RepairCost(record) != 0) { seen.Add(record); rows.Add(("equipped", key, record)); }
        for (int i = 0; i < p.Inventory.Items.Count; i++)
        {
            var record = p.Inventory.Items[i];
            if (ItemData.IsEquipment(record) && RepairCost(record) != 0) rows.Add(("bag", i, record));
        }
        return rows;
    }

    public static (bool Ok, string Message) Repair(IGearOwner p, PyDict record)
    {
        long cost = RepairCost(record);
        if (cost == 0) return (false, "Este item não precisa de reparo.");
        if (p.Gold < cost) return (false, $"São necessários {cost} de ouro.");
        p.Gold -= cost; ItemData.Repair(record); return (true, $"Reparado por {cost} de ouro.");
    }

    public static (bool Ok, string Message) RepairAll(IGearOwner p)
    {
        var rows = Damaged(p); long total = rows.Sum(r => RepairCost(r.Record));
        if (rows.Count == 0) return (false, "Nada precisa de reparo.");
        if (p.Gold < total) return (false, $"São necessários {total} de ouro.");
        p.Gold -= total;
        foreach (var (_, _, record) in rows) ItemData.Repair(record);
        return (true, $"{rows.Count} item(ns) reparado(s) por {total} de ouro.");
    }

    /// <summary>forge.transact: buy a potion, sell, or raise quality (the old +3 upgrade became quality 0..25).</summary>
    public static (bool Ok, string Message) Transact(IGearOwner p, string action, int index = 0)
    {
        var bag = p.Inventory;
        if (action == "potion")
        {
            if (p.Gold < 25) return (false, "São necessários 25 de ouro.");
            if (Pages.AddPotions(p, 1) == 0) return (false, $"Limite de {Pages.PotionCapOf(p)} poções.");
            p.Gold -= 25; return (true, "Poção comprada por 25 de ouro.");
        }
        if (index < 0 || index >= bag.Items.Count) return (false, "Selecione um item da mochila.");
        var record = bag.Items[index];
        if (action == "sell")
        {
            long price = SellPrice(record); bag.Remove(index); p.Gold += price;
            return (true, $"Item vendido por {price} de ouro.");
        }
        if (action == "upgrade")
        {
            if (!ItemData.IsEquipment(record)) return (false, "Este item não pode ser aprimorado.");
            long price = UpgradeCost(ItemData.Normalize(record));
            if ((long)record.Num("quality") < ItemData.QualityMax && p.Gold < price) return (false, $"São necessários {price} de ouro.");
            var (ok, message) = ItemData.RaiseQuality(record, p.Materials);
            if (!ok) return (false, message);
            p.Gold -= price;
            return (true, $"{message} Custo: {price} de ouro.");
        }
        return (false, "Serviço desconhecido.");
    }
}

/// <summary>Aspect codex and wardrobe — game/equipment/codex.py (the registry is shared per world).</summary>
public static class Codex
{
    private static PyDict C => ItemTables.Section("codex");

    public static PyDict Registry(PyDict unlocks)
    {
        if (!unlocks.ContainsKey("codex")) unlocks["codex"] = new List<object?>();
        if (!unlocks.ContainsKey("wardrobe")) unlocks["wardrobe"] = new PyDict();
        if (!unlocks.ContainsKey("temper")) unlocks["temper"] = new List<object?>();
        return unlocks;
    }

    public static (string Key, PyDict Look) LookOf(PyDict record)
    {
        var look = new PyDict(record.Sub("appearance") ?? new PyDict());
        var family = new[] { look.Get("family"), record.Get("weapon_type"), record.Get("slot"), record.Get("kind") }.FirstOrDefault(Py.Truthy) ?? record.Get("kind");
        var skin = look.TryGetValue("skin_id", out var s) ? s : record.TryGetValue("name", out var n) ? n : "";
        return ($"{PyStr(family)}:{PyStr(skin)}", new PyDict(look) { ["family"] = family });
    }

    private static string PyStr(object? v) => v switch { null => "None", bool b => b ? "True" : "False", _ => Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)! };

    public static long LearnFrom(PyDict unlocks, PyDict record)
    {
        Registry(unlocks); long fresh = 0;
        if (ItemData.IsEquipment(record))
        {
            var (key, look) = LookOf(record); var wardrobe = unlocks.Sub("wardrobe")!;
            if (!wardrobe.ContainsKey(key)) { wardrobe[key] = look; fresh++; }
            var aspect = record.Get("aspect") as string; var codex = (List<object?>)unlocks["codex"]!;
            if (aspect != null && C.Sub("ASPECT_CATEGORY")!.ContainsKey(aspect) && Rarity.Canonical(record.Get("rarity")) is "Lendário" or "Único" or "Único Mítico"
                && !codex.Contains(aspect)) { codex.Add(aspect); fresh++; }
        }
        return fresh;
    }

    public static bool Accepts(PyDict record, string aspect)
    {
        var category = C.Sub("ASPECT_CATEGORY")!.Get(aspect);
        return C.Sub("SLOT_CATEGORIES")!.Get(ItemData.Category(record)) is List<object?> allowed && category != null && allowed.Contains(category);
    }

    public static (bool Ok, string Message) Engrave(IGearOwner p, PyDict record, string aspect, PyDict unlocks)
    {
        var catalog = ItemTables.Section("legendary").Sub("CATALOG")!;
        Registry(unlocks);
        if (!((List<object?>)unlocks["codex"]!).Contains(aspect)) return (false, "Aspecto ainda não registrado no Códice.");
        if (!ItemData.IsEquipment(record) || !ItemTables.Strings(C.Get("ENGRAVE_RARITIES")).Contains(Rarity.Canonical(record.Get("rarity"))))
            return (false, "Só itens Raros ou Lendários recebem aspectos.");
        var owner = ((List<object?>)catalog[aspect]!)[1];
        if (owner != null && !Equals(owner, p.PlayerClass)) return (false, "Este aspecto é de outra classe.");
        if (!Accepts(record, aspect)) return (false, $"Esta peça não aceita aspectos {C.Sub("ASPECT_CATEGORY")!.Str(aspect)}s.");
        long gold = (long)C.Num("ENGRAVE_GOLD");
        if (p.Gold < gold) return (false, $"São necessários {gold} de ouro.");
        p.Gold -= gold; record["aspect"] = aspect;
        return (true, $"{((List<object?>)catalog[aspect]!)[0]} gravado.");
    }

    public static List<(string Key, PyDict Look)> LooksFor(PyDict unlocks, PyDict record)
    {
        var family = LookOf(record).Look.Get("family");
        return Registry(unlocks).Sub("wardrobe")!.Where(kv => Gear.PyEquals(((PyDict)kv.Value!).Get("family"), family)).Select(kv => (kv.Key, (PyDict)kv.Value!)).ToList();
    }

    public static (bool Ok, string Message) ApplyLook(PyDict record, PyDict unlocks, string? key)
    {
        if (key == null) { record["transmog"] = null; return (true, "Aparência original restaurada."); }
        var look = Registry(unlocks).Sub("wardrobe")!.Get(key) as PyDict;
        if (look == null || look.Count == 0) return (false, "Aparência bloqueada.");
        if (!Gear.PyEquals(look.Get("family"), LookOf(record).Look.Get("family"))) return (false, "Aparência de outro tipo de peça.");
        record["transmog"] = new PyDict(look); return (true, "Aparência aplicada.");
    }
}

/// <summary>Jeweler — game/equipment/jeweler.py (prices PROVISÓRIOS).</summary>
public static class Jeweler
{
    private static PyDict J => ItemTables.Section("jeweler");
    public static long CreateCount => (long)J.Num("CREATE_COUNT");
    public static long CreateGold(long grade) => 100 * grade;
    public static long RecycleGold(long grade) => 25 * grade;

    public static PyDict? Stack(IGearOwner p, string gid) => p.Inventory.Gems.FirstOrDefault(g => Equals(g.Get("id"), gid));

    private static bool Take(IGearOwner p, string gid, long n)
    {
        var row = Stack(p, gid);
        if (row == null || row.Num("count", 0) < n) return false;
        row["count"] = (long)row.Num("count") - n;
        if (row.Num("count") <= 0) p.Inventory.Gems.Remove(row);
        return true;
    }

    public static long CanCreate(IGearOwner p, string family, long grade)
    {
        if (!(2 <= grade && grade <= Gems.Grades.Count)) return 0;
        var row = Stack(p, Gems.GemId(family, grade - 1)); long have = row != null ? (long)row.Num("count") / CreateCount : 0;
        return Math.Min(have, FloorDiv(p.Gold, CreateGold(grade)));
    }

    private static long FloorDiv(long a, long b) => (long)Math.Floor((double)a / b);

    public static (bool Ok, string Message) Create(IGearOwner p, string family, long grade, long times = 1)
    {
        long made = 0;
        for (long i = 0; i < Math.Max(1, times); i++)
        {
            if (CanCreate(p, family, grade) < 1) break;
            var gem = Gems.Make(family, grade); var (ok, message) = Pages.Add(p.Inventory, gem);
            if (!ok) return (made > 0, message);
            Take(p, Gems.GemId(family, grade - 1), CreateCount); p.Gold -= CreateGold(grade); made++;
        }
        if (made == 0) return (false, "Gemas ou ouro insuficientes.");
        return (true, $"{made} x {Gems.Name(Gems.GemId(family, grade))} criada(s).");
    }

    public static (bool Ok, string Message) Recycle(IGearOwner p, string gid)
    {
        var parsed = Gems.Parse(gid);
        if (parsed == null || !Take(p, gid, 1)) return (false, "Escolha uma gema.");
        p.Gold += RecycleGold(parsed.Value.Grade); return (true, $"{Gems.Name(gid)} reciclada: +{RecycleGold(parsed.Value.Grade)} de ouro.");
    }

    public static (bool Ok, string Message) Unsocket(IGearOwner p, PyDict record, int index)
    {
        var sockets = record.List("sockets");
        if (index < 0 || index >= sockets.Count || !Py.Truthy(sockets[index])) return (false, "Este engaste está vazio.");
        var gid = (string)sockets[index]!; var (family, grade) = Gems.Parse(gid)!.Value;
        var (ok, message) = Pages.Add(p.Inventory, Gems.Make(family, grade));
        if (!ok) return (false, message);
        sockets[index] = null; p.RefreshStats();
        return (true, $"{Gems.Name(gid)} removida.");
    }

    public static (bool Ok, string Message) SocketGem(IGearOwner p, PyDict record, string gid, int? index = null)
    {
        var row = Stack(p, gid);
        var (ok, message) = row != null ? Gems.Insert(record, row, index) : (false, "Escolha uma gema.");
        if (ok && row!.Num("count") <= 0) p.Inventory.Gems.Remove(row);
        if (ok) p.RefreshStats();
        return (ok, message);
    }

    public static (bool Ok, string Message) AddSocket(IGearOwner p, PyDict record)
    {
        ItemData.Normalize(record);
        if (record.List("sockets").Count >= ItemData.SocketLimit(record)) return (false, "Este item já tem o máximo de engastes.");
        var cost = J.Sub("SOCKET_COST")!;
        if (!Materials.Spend(p, cost)) return (false, $"Falta {Materials.DescribeCost(cost)}.");
        return Gems.AddSocket(record);
    }
}
