using System.Security.Cryptography;
using System.Text;
using Umbra.Domain.Combat;
using Umbra.Domain.Skills;
using Umbra.Domain.Util;

namespace Umbra.Domain.Items;

/// <summary>
/// A drop, exactly as UMBRA's composed engine makes it: Item(position, kind, floor, player_level, player_class) runs
/// entities/items.Item.create_data (rebalanced by core.balance.rebalance_item_data), sets the item levels, and for
/// equipment game/equipment/loot.roll re-rolls the record (keeping only the rarity), adds the Necromancer affixes, the
/// legendary aspect, the appearance (equipment/appearance.enrich) and the Reforge fields (itemdata.normalize).
/// Every random draw happens in the same order as in Python, so a seeded PyRandom gives the same items.
/// Difficulty tables of Hard/Extreme (world.difficulty.rarity) come with the world (5.9): this is the Normal roll.
/// </summary>
public static class Loot
{
    private static PyDict Items => ItemTables.Section("items");
    private static PyDict LootTable => ItemTables.Section("loot");

    public static PyDict RarityRow(string name) => Items.Sub("RARITIES")!.Sub(Rarity.Canonical(name))!;

    public static long RarityScaledValue(long baseValue, string rarity) =>
        Math.Max(1, (long)Math.Ceiling(baseValue * RarityRow(rarity).Num("multiplier")));

    private static string FloorRarity(long floor, IRandom rng)
    {
        double[] weights = { 58, 25, 12, 4, 1 };
        if (floor >= 10) weights = new double[] { 42, 30, 18, 8, 2 };
        if (floor >= 20) weights = new double[] { 30, 30, 22, 13, 5 };
        var merged = Rarity.MergeWeights(new[] { "Comum", "Incomum", "Raro", "Épico", "Lendário" }.Zip(weights));
        return rng.Weighted(merged.Select(m => m.Name).ToList(), merged.Select(m => m.Weight).ToList());
    }

    private static object? Color(string rarity) => Py.DeepCopy(RarityRow(rarity).Get("color"));

    /// <summary>entities/items.Item.create_data, followed by core.balance.rebalance_item_data.</summary>
    public static PyDict CreateData(string kind, long floor, IRandom rng)
    {
        long gold = rng.RandInt(8, 20), essence = rng.RandInt(12, 20);    // the dict literal is built on every call
        var basic = new Dictionary<string, PyDict>
        {
            ["potion"] = new() { ["name"] = "Poção de Cura", ["color"] = Rgb(220, 45, 65), ["value"] = 40L },
            ["power"] = new() { ["name"] = "Relíquia de Poder", ["color"] = Rgb(220, 160, 40), ["value"] = 5L },
            ["protection"] = new() { ["name"] = "Relíquia de Proteção", ["color"] = Rgb(70, 150, 220), ["value"] = 2L },
            ["gold"] = new() { ["name"] = "Ouro", ["color"] = Rgb(255, 210, 40), ["value"] = gold },
            ["chest"] = new() { ["name"] = "Baú Antigo", ["color"] = Rgb(205, 150, 65), ["value"] = 0L, ["rarity"] = "Especial" },
            ["resource_orb"] = new() { ["name"] = "Orbe de Essência", ["color"] = Rgb(225, 105, 205), ["value"] = essence, ["rarity"] = "Incomum" },
        };
        if (kind == "relic")
        {
            var profile = ((PyDict)rng.Choice(Items.List("RELICS"))!).Copy();
            profile["color"] = Rgb(245, 190, 85); profile["value"] = 0L; profile["rarity"] = "Lendário";
            profile["stats"] = new PyDict(profile.Sub("stats") ?? new PyDict());
            return profile;
        }
        if (basic.TryGetValue(kind, out var simple)) return simple;
        string rarity = Rarity.Canonical(FloorRarity(floor, rng));
        long bonus = Math.Max(0, floor - 1); PyDict data;
        long itemLevel = ItemLevels.ItemLevel(floor, Rarity.Tier(rarity)), required = ItemLevels.RequiredLevel(floor);
        if (kind == "weapon")
        {
            string name = rng.Choice(ItemTables.Strings(Items.Get("WEAPONS")));
            long @base = (long)Items.Sub("WEAPON_BASE")!.Num(name) + bonus, value = RarityScaledValue(@base, rarity);
            data = new() { ["name"] = name, ["color"] = Color(rarity), ["value"] = value, ["rarity"] = rarity, ["slot"] = "weapon",
                           ["stats"] = new PyDict { ["attack"] = value }, ["base_value"] = @base, ["item_level"] = itemLevel, ["required_level"] = required };
        }
        else if (kind == "armor")
        {
            string name = rng.Choice(ItemTables.Strings(Items.Get("ARMORS")));
            long @base = (long)Items.Sub("ARMOR_BASE")!.Num(name) + bonus / 2, value = RarityScaledValue(@base, rarity);
            data = new() { ["name"] = name, ["color"] = Color(rarity), ["value"] = value, ["rarity"] = rarity, ["slot"] = "armor",
                           ["stats"] = new PyDict { ["defense"] = value }, ["base_value"] = @base, ["item_level"] = itemLevel, ["required_level"] = required };
        }
        else if (kind == "accessory")
        {
            var pair = (List<object?>)rng.Choice(Items.List("ACCESSORIES"))!;
            string name = (string)pair[0]!; var baseStats = (PyDict)pair[1]!;
            var stats = new PyDict(baseStats.Select(kv => new KeyValuePair<string, object?>(kv.Key, RarityScaledValue((long)Py.Num(kv.Value) + bonus / 2, rarity))));
            long value = stats.Count > 0 ? stats.Values.Max(v => (long)Py.Num(v)) : 1;
            data = new() { ["name"] = name, ["color"] = Color(rarity), ["value"] = value, ["rarity"] = rarity, ["slot"] = "accessory",
                           ["stats"] = stats, ["base_stats"] = baseStats.Copy(), ["item_level"] = itemLevel, ["required_level"] = required };
        }
        else throw new ArgumentException("Tipo de item desconhecido: " + kind);
        return AttachAspect(Rebalance(data, kind), rng);
    }

    /// <summary>core.compatibility wraps create_data (outside the balance layer) with forge.attach_aspect: a Lendário
    /// weapon draws a forge aspect. The draw happens even though loot.roll replaces the record afterwards.</summary>
    public static PyDict AttachAspect(PyDict record, IRandom rng)
    {
        if (Equals(record.Get("rarity"), "Lendário") && Equals(record.Get("slot"), "weapon"))
        {
            string aspect = rng.Choice(ItemTables.Section("forge").Sub("ASPECTS")!.Keys.ToList());
            if (!record.ContainsKey("aspect")) record["aspect"] = aspect;
        }
        return record;
    }

    private static List<object?> Rgb(long r, long g, long b) => new() { r, g, b };

    /// <summary>core.balance.rebalance_item_data: caps the generated bonus by item level, keeping the rarity.</summary>
    public static PyDict Rebalance(PyDict data, string kind)
    {
        var result = new PyDict(data); var stats = new PyDict(result.Sub("stats") ?? new PyDict());
        long itemLevel = Math.Max(1, (long)result.Num("item_level", 1)), value = Math.Max(0, (long)result.Num("value", 0));
        if (kind == "weapon") { value = Math.Min(value, 4 + (long)(itemLevel * 0.34)); if (stats.ContainsKey("attack")) stats["attack"] = value; }
        else if (kind == "armor") { value = Math.Min(value, 2 + (long)(itemLevel * 0.22)); if (stats.ContainsKey("defense")) stats["defense"] = value; }
        else
        {
            var limits = new (string, long)[] { ("attack", 3), ("defense", 2), ("health", Math.Min(18, 8 + (long)(itemLevel * 0.25))), ("mana", Math.Min(18, 8 + (long)(itemLevel * 0.25))) };
            foreach (var (key, amount) in limits) if (stats.ContainsKey(key)) stats[key] = Math.Min(amount, Math.Max(0, (long)Py.Num(stats[key])));
            value = stats.Count > 0 ? stats.Values.Max(v => (long)Py.Num(v)) : 1;
        }
        result["value"] = value; result["stats"] = stats;
        return result;
    }

    /// <summary>The weapon families a class can roll, in the order Python iterates them. UMBRA builds them as a set, whose
    /// order depends on the process's string hashing; the parity test passes the order it recorded.</summary>
    public static List<string> Families(string? playerClass)
    {
        var classWeapons = ItemTables.Section("slots").Sub("CLASS_WEAPONS")!;
        if (playerClass != null && classWeapons.Sub(playerClass) is { } table)
            return table.Values.SelectMany(v => ItemTables.Strings(v)).Distinct().ToList();
        return LootTable.Sub("WEAPONS")!.Keys.ToList();
    }

    /// <summary>Item((x, y), kind, floor, player_level, player_class).data</summary>
    public static PyDict Drop(string kind, long floor, long? playerLevel, string? playerClass, IRandom rng, IReadOnlyList<string>? families = null)
    {
        var data = CreateData(kind, floor, rng);
        bool equipment = kind is "weapon" or "armor" or "accessory";
        if (equipment && playerLevel != null)
        {
            data["required_level"] = ItemLevels.RequiredLevel(floor, playerLevel);
            data["item_level"] = ItemLevels.ItemLevel(floor, Rarity.Tier(data.Get("rarity")), playerLevel);
        }
        return equipment ? Roll(kind, floor, playerLevel, data.Get("rarity") as string, playerClass, rng, families) : data;
    }

    /// <summary>game/equipment/loot.roll.</summary>
    public static PyDict Roll(string kind, long floor, long? playerLevel, string? rarity, string? playerClass, IRandom rng, IReadOnlyList<string>? families = null)
    {
        if (string.IsNullOrEmpty(rarity))
        {
            var merged = Rarity.MergeWeights(new[] { "Comum", "Incomum", "Raro", "Épico", "Lendário" }.Zip(new double[] { 40, 28, 19, 10, 3 }));
            rarity = rng.Weighted(merged.Select(m => m.Name).ToList(), merged.Select(m => m.Weight).ToList());
        }
        rarity = Rarity.Canonical(rarity);
        int index = Rarity.Tier(rarity); long required = ItemLevels.RequiredLevel(floor, playerLevel);
        long ilevel = ItemLevels.ItemLevel(floor, index, playerLevel);
        double factor = RarityRow(rarity).Num("multiplier"); var affixes = new PyDict();
        string slot, name; string? family; long value; PyDict stats;
        if (kind == "weapon")
        {
            var pool = families ?? Families(playerClass);
            family = rng.Choice(pool); slot = "weapon"; name = rng.Choice(ItemTables.Strings(LootTable.Sub("WEAPONS")!.Get(family)));
            value = Math.Max(2, PyMath.Round((3 + ilevel * .26) * factor));
            stats = new PyDict { ["attack"] = value };
            if (family == "shield") { stats = new PyDict { ["defense"] = value }; value = Math.Max(1, value); }
            else if (family == "focus") { stats = new PyDict { ["attack"] = Math.Max(1, value / 4), ["mana"] = 4 + ilevel / 4 }; value = 1; }
        }
        else if (kind == "armor")
        {
            var armors = LootTable.Sub("ARMORS")!;
            slot = rng.Choice(armors.Keys.ToList()); family = null; name = rng.Choice(ItemTables.Strings(armors.Get(slot)));
            value = Math.Max(1, PyMath.Round((1 + ilevel * .12) * factor)); stats = new PyDict { ["defense"] = value };
        }
        else
        {
            slot = rng.Choice(new[] { "amulet", "ring" }); family = null; name = slot == "amulet" ? "Amuleto" : "Anel";
            value = Math.Max(1, PyMath.Round((1 + ilevel * .045) * factor));
            string key = rng.Choice(new[] { "attack", "defense" });
            stats = new PyDict { [key] = value, ["health"] = 4 + ilevel / 2 };
        }
        foreach (var key in rng.Sample(new[] { "attack", "defense", "health", "mana" }, Math.Min(3, index)))
            affixes[key] = Math.Max(1, PyMath.Round((ilevel * .035 + 1) * (key is "health" or "mana" ? 3 : 1)));
        var record = new PyDict
        {
            ["kind"] = kind, ["slot"] = slot, ["name"] = name + " " + rng.Choice(ItemTables.Strings(LootTable.Get("SUFFIXES"))), ["weapon_type"] = family,
            ["value"] = value, ["base_value"] = value, ["stats"] = stats, ["affixes"] = affixes, ["rarity"] = rarity, ["color"] = Color(rarity),
            ["required_level"] = required, ["item_level"] = ilevel, ["gear_schema"] = 60L, ["forge_rank"] = 0L,
        };
        if (playerClass == "necromante") NecroAffixes.Roll(record, index, ilevel, rng);
        if (rarity == "Lendário")
        {
            var catalog = ItemTables.Section("legendary").Sub("CATALOG")!;
            record["aspect"] = rng.Choice(catalog.Where(kv => playerClass == null || ((List<object?>)kv.Value!)[1] is null || Equals(((List<object?>)kv.Value!)[1], playerClass))
                                                 .Select(kv => kv.Key).ToList());
        }
        return ItemData.Normalize(Appearance.Enrich(record, playerClass));
    }
}

/// <summary>Necromancer item affixes — game/equipment/necro_affixes.py (roll, total, share, text).</summary>
public static class NecroAffixes
{
    private static PyDict T => ItemTables.Section("necro_affixes");

    public static PyDict Roll(PyDict record, int rarityIndex, long itemLevel, IRandom rng)
    {
        int count = Math.Min(2, Math.Max(0, rarityIndex));
        if (count == 0) return record;
        if (record.Sub("affixes") is not { } affixes) { affixes = new PyDict(); record["affixes"] = affixes; }
        var table = T.Sub("AFFIXES")!; var percent = ItemTables.Strings(T.Get("PERCENT"));
        foreach (var key in rng.Sample(table.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList(), count))
        {
            var row = (List<object?>)table[key]!; double @base = Py.Num(row[1]), perTen = Py.Num(row[2]);
            affixes[key] = percent.Contains(key) ? Math.Max(1, PyMath.Round(@base + perTen * itemLevel / 10))
                                                 : (long)@base + (itemLevel >= 60 && key == "corpse_reach" ? 1 : 0);
        }
        return record;
    }

    public static long Total(IEnumerable<PyDict?> gear, string key)
    {
        long value = gear.Where(i => i != null).Sum(i => (long)Math.Truncate(Py.Num(i!.Sub("affixes")?.Get(key) ?? 0L)));
        return Math.Min(T.Sub("CAPS")!.TryGetValue(key, out var cap) ? (long)Py.Num(cap) : 1_000_000, value);
    }

    public static double Share(IEnumerable<PyDict?> gear, string key) => Total(gear, key) / 100.0;

    public static string Text(string key, object value)
    {
        var table = T.Sub("AFFIXES")!;
        if (table.TryGetValue(key, out var row))
        {
            string label = (string)((List<object?>)row!)[0]!;
            return ItemTables.Strings(T.Get("PERCENT")).Contains(key) ? $"+{value}% {label}" : $"+{value} {label}";
        }
        var names = new Dictionary<string, string> { ["attack"] = "ATAQUE", ["defense"] = "ARMADURA", ["health"] = "VIDA", ["mana"] = "RECURSO" };
        return $"+{value} " + (names.TryGetValue(key, out var n) ? n : key.ToUpperInvariant());
    }
}

/// <summary>The item's own look — game/equipment/appearance.describe/enrich (identity hashed with SHA-256, as in Python).</summary>
public static class Appearance
{
    private static readonly string[] Materials = { "cloth", "leather", "iron", "bone" };
    private static readonly int[][] Palettes =
        { new[] { 99, 137, 155 }, new[] { 151, 103, 77 }, new[] { 116, 137, 91 }, new[] { 143, 113, 159 },
          new[] { 171, 152, 97 }, new[] { 123, 147, 146 }, new[] { 159, 96, 101 }, new[] { 137, 132, 170 } };

    private static string Identity(PyDict record, string family) =>
        PyJson.Dumps(new List<object?> { record.Get("id"), record.TryGetValue("name", out var n) ? n : "", family, record.Get("slot"),
                                         record.TryGetValue("rarity", out var r) ? r : "Comum" });

    private static string FamilyOf(PyDict record) =>
        new[] { record.Get("weapon_type"), record.Get("slot"), record.TryGetValue("kind", out var k) ? k : "chest" }.First(Py.Truthy) as string ?? "chest";

    public static (string Family, string Material, long Variant, List<object?> Color) Describe(PyDict record, string playerClass = "barbaro")
    {
        string family = FamilyOf(record);
        var look = record.Get("transmog") as PyDict ?? record.Get("appearance") as PyDict ?? new PyDict();
        string material = look.Get("material") as string ?? "";
        if (!Materials.Contains(material))
            material = playerClass is "mago" or "bruxo" or "necromante" ? "cloth" : playerClass is "ladino" or "druida" ? "leather" : "iron";
        object identity = new[] { look.Get("skin_id"), record.Get("skin_id") }.FirstOrDefault(Py.Truthy) ?? Identity(record, family);
        byte[] seed = SHA256.HashData(Encoding.UTF8.GetBytes(identity as string ?? Convert.ToString(identity, System.Globalization.CultureInfo.InvariantCulture)!));
        long variant = (look.ContainsKey("variant") ? (long)Math.Truncate(Py.Num(look["variant"])) : seed[0]) % 8;
        var @base = Palettes[seed[1] % Palettes.Length];
        List<object?> color = look.Get("tint") is List<object?> tint
            ? tint.Take(3).ToList()
            : @base.Select((c, i) => (object?)(long)Math.Max(40, Math.Min(220, c + seed[i + 2] % 31 - 15))).ToList();
        return (family, material, variant, color);
    }

    public static PyDict Enrich(PyDict record, string? playerClass = null)
    {
        if (record.Get("kind") is not ("weapon" or "armor" or "accessory")) return record;
        var (family, material, variant, color) = Describe(record, playerClass ?? "barbaro");
        var old = record.Get("appearance") as PyDict ?? new PyDict();
        object identity = new[] { old.Get("skin_id"), record.Get("skin_id") }.FirstOrDefault(Py.Truthy)
                          ?? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Identity(record, family)))).ToLowerInvariant()[..16];
        var look = new PyDict(old) { ["skin_id"] = identity, ["family"] = family, ["material"] = material, ["variant"] = variant, ["tint"] = color };
        record["appearance"] = look;
        string rarity = record.TryGetValue("rarity", out var r) && r is string s ? s : "Comum";
        double strength = rarity switch
        {
            "Mágico" or "Incomum" => .03, "Raro" or "Épico" => .05, "Lendário" or "Único" or "Único Mítico" => .10, _ => 0,
        };
        if (strength != 0)
        {
            if (Equals(record["kind"], "weapon"))
            {
                var pool = playerClass != null && ItemTables.ClassPool.Get(playerClass) is List<object?> p ? ItemTables.Strings(p) : new List<string> { "physical" };
                record["element_bonus"] = new PyDict { [pool[(int)(variant % pool.Count)]] = strength };
            }
            else
            {
                string[] pool = { "fire", "cold", "lightning", "poison", "shadow", "holy" };
                long sum = (record.TryGetValue("slot", out var slotValue) ? Convert.ToString(slotValue) ?? "None" : "").Sum(ch => (long)ch);
                record["resistances"] = new PyDict { [pool[(variant + sum) % pool.Length]] = strength };
            }
        }
        return record;
    }
}

/// <summary>Python's json.dumps(value, ensure_ascii=False, separators=(',', ':')) for the values item identities use.</summary>
public static class PyJson
{
    public static string Dumps(object? value)
    {
        var sb = new StringBuilder(); Write(sb, value); return sb.ToString();
    }

    private static void Write(StringBuilder sb, object? value)
    {
        switch (value)
        {
            case null: sb.Append("null"); break;
            case bool b: sb.Append(b ? "true" : "false"); break;
            case long n: sb.Append(n); break;
            case string s:
                sb.Append('"');
                foreach (char c in s)
                    sb.Append(c switch
                    {
                        '"' => "\\\"", '\\' => "\\\\", '\n' => "\\n", '\r' => "\\r", '\t' => "\\t", '\b' => "\\b", '\f' => "\\f",
                        < ' ' => $"\\u{(int)c:x4}", _ => c.ToString(),
                    });
                sb.Append('"'); break;
            case List<object?> list:
                sb.Append('['); for (int i = 0; i < list.Count; i++) { if (i > 0) sb.Append(','); Write(sb, list[i]); } sb.Append(']'); break;
            default: throw new NotSupportedException(value.GetType().Name);
        }
    }
}
