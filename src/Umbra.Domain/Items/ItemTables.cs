using System.Text;
using System.Text.Json;
using Umbra.Domain.Combat;
using Umbra.Domain.Skills;

namespace Umbra.Domain.Items;

/// <summary>
/// The item tables of UMBRA (game/data/umbra/equipment.json, exported after the engine is composed, so the rarity
/// multipliers and the forge's aspect names are the live ones) plus the palette and the element pools of rules.json.
/// Records stay open dictionaries (PyDict), as UMBRA saves them; integers are long, decimals double (Python int/float).
/// </summary>
public static class ItemTables
{
    public static PyDict Root { get; private set; } = new();
    public static PyDict Palette { get; private set; } = new();
    public static PyDict ClassPool { get; private set; } = new();

    public static void Load(string dataRoot)
    {
        Root = (PyDict)Py.FromJson(JsonDocument.Parse(File.ReadAllText(Path.Combine(dataRoot, "equipment.json"), Encoding.UTF8)).RootElement)!;
        var rules = (PyDict)Py.FromJson(JsonDocument.Parse(File.ReadAllText(Path.Combine(dataRoot, "rules.json"), Encoding.UTF8)).RootElement)!;
        Palette = rules.Sub("palette")!.Sub("DEFAULT")!;
        ClassPool = rules.Sub("elements")!.Sub("CLASS_POOL")!;
    }

    public static PyDict Section(string name) => Root.Sub(name) ?? throw new KeyNotFoundException(name);
    public static List<string> Strings(object? list) => ((List<object?>)list!).Cast<string>().ToList();
}

/// <summary>game/equipment/rarity.py.</summary>
public static class Rarity
{
    public static readonly string[] Order = { "Comum", "Mágico", "Raro", "Lendário", "Único", "Único Mítico" };
    public static readonly IReadOnlyDictionary<string, string> Legacy = new Dictionary<string, string> { ["Incomum"] = "Mágico", ["Épico"] = "Raro" };
    public static readonly string[] Droppable = { "Comum", "Mágico", "Raro", "Lendário" };
    public static readonly IReadOnlyDictionary<string, int> Tiers = new Dictionary<string, int>
    {
        ["Comum"] = 0, ["Mágico"] = 1, ["Raro"] = 2, ["Lendário"] = 4, ["Único"] = 4, ["Único Mítico"] = 4,
    };

    public static string Canonical(object? name)
    {
        var text = name as string ?? "";
        if (Legacy.TryGetValue(text, out var mapped)) text = mapped;
        return Order.Contains(text) ? text : "Comum";
    }

    public static int Tier(object? name) => Tiers[Canonical(name)];

    /// <summary>Old rarity names -> new ones, in place. Idempotent.</summary>
    public static bool Migrate(PyDict? record)
    {
        if (record?.Get("rarity") is not string old || !Legacy.ContainsKey(old)) return false;
        record["legacy_rarity"] = old; record["rarity"] = Legacy[old];
        record["color"] = Py.DeepCopy(ItemTables.Palette.Get("rarity." + Legacy[old]));
        return true;
    }

    /// <summary>(name, weight) pairs for the old five rarities -> the droppable ones (Épico's weight goes to Raro).</summary>
    public static List<(string Name, double Weight)> MergeWeights(IEnumerable<(string Name, double Weight)> table)
    {
        var merged = new Dictionary<string, double>();
        foreach (var (name, weight) in table) merged[Canonical(name)] = merged.GetValueOrDefault(Canonical(name)) + weight;
        return Droppable.Where(merged.ContainsKey).Select(n => (n, merged[n])).ToList();
    }
}

/// <summary>Item levels — game/skills/leveling.py (equipment part).</summary>
public static class ItemLevels
{
    public const int MaxLevel = 70;

    public static long RequiredLevel(long floor, long? playerLevel = null)
    {
        long required = Math.Max(1, (Math.Max(1, floor) + 2) / 3);
        if (playerLevel is long level) required = Math.Min(Math.Max(required, level - 2), Math.Max(1, level) + 1);
        return Math.Min(MaxLevel, required);
    }

    public static long ItemLevel(long floor, long rarityIndex = 0, long? playerLevel = null)
    {
        floor = Math.Max(1, floor); rarityIndex = Math.Max(0, Math.Min(4, rarityIndex));
        long level = Math.Max(1, floor * 2 + rarityIndex * 2);
        if (playerLevel is long p) level = Math.Min(Math.Max(level, p * 2 - 4 + rarityIndex), Math.Max(1, p * 2 + 4));
        return Math.Min(140, level);
    }
}

/// <summary>
/// Record fields of the MASSIVE REFORGE — game/equipment/itemdata.py: durability, quality 0..25 (scaled from a
/// snapshotted base, +25 maximizes one affix with the special material), sockets per category, temper, transmog.
/// Normalize is idempotent and never changes stats, affixes or value.
/// </summary>
public static class ItemData
{
    private static PyDict T => ItemTables.Section("itemdata");
    public static long DurabilityMax => (long)T.Num("DURABILITY_MAX");
    public static long DeathLoss => (long)T.Num("DEATH_LOSS");
    public static long QualityMax => (long)T.Num("QUALITY_MAX");
    public static double QualityStep => T.Num("QUALITY_STEP");
    public static long QualitySpecial => (long)T.Num("QUALITY_SPECIAL");
    public static double MaximizeBonus => T.Num("MAXIMIZE_BONUS");
    public static string SpecialMaterial => T.Str("SPECIAL_MATERIAL");

    public static string? KindOf(PyDict r) => r.ContainsKey("kind") ? r.Get("kind") as string : r.Get("slot") as string;

    public static bool IsEquipment(object? record) =>
        record is PyDict r && (r.ContainsKey("kind") ? r.Get("kind") : r.Get("slot")) is string k && k is "weapon" or "armor" or "accessory";

    public static bool IsTwoHanded(PyDict? record) =>
        record != null && record.Get("weapon_type") is string w && w is "staff" or "scythe" or "mace2h" or "sword2h" or "axe2h";

    public static string Category(PyDict record)
    {
        var kind = KindOf(record);
        var limits = T.Sub("SOCKET_LIMITS")!;
        if (kind == "armor") return record.Get("slot") is string s && limits.ContainsKey(s) ? s : "chest";
        if (kind == "accessory") return Equals(record.Get("slot"), "amulet") ? "amulet" : "ring";
        var family = record.Get("weapon_type") as string;
        if (family == "focus") return "focus";
        if (family == "shield") return "shield";
        return IsTwoHanded(record) ? "two_hand" : "one_hand";
    }

    public static string GemCategory(PyDict record)
    {
        var kind = KindOf(record);
        return kind == "accessory" ? "jewelry" : kind == "armor" || Equals(record.Get("weapon_type"), "shield") ? "armor" : "weapon";
    }

    public static long SocketLimit(PyDict record) => (long)T.Sub("SOCKET_LIMITS")!.Num(Category(record), 0);

    private static long IntOf(object? v, long fallback) => v == null ? fallback : (long)Math.Truncate(Py.Num(v));

    public static PyDict Normalize(PyDict record)
    {
        if (!IsEquipment(record)) return record;
        Rarity.Migrate(record);
        if (record.Get("durability") is not PyDict durability)
            record["durability"] = new PyDict { ["current"] = DurabilityMax, ["max"] = DurabilityMax };
        else
        {
            long top = Math.Max(1, IntOf(durability.TryGetValue("max", out var m) ? m : DurabilityMax, DurabilityMax));
            long current = IntOf(durability.TryGetValue("current", out var c) ? c : top, top);
            record["durability"] = new PyDict { ["current"] = Math.Max(0, Math.Min(top, current)), ["max"] = top };
        }
        var q = record.Get("quality");
        record["quality"] = Math.Max(0, Math.Min(QualityMax, Py.Truthy(q) ? IntOf(q, 0) : 0));
        record["sockets"] = record.Get("sockets") is List<object?> sockets ? sockets.Take((int)SocketLimit(record)).ToList() : new List<object?>();
        if (!record.ContainsKey("temper")) record["temper"] = null;
        if (!record.ContainsKey("transmog")) record["transmog"] = null;
        if (record.ContainsKey("appearance") && record["appearance"] == null) record.Remove("appearance");
        return record;
    }

    // ------------------------------------------------------------------ durability
    public static long Wear(PyDict? record, long? amount = null)
    {
        if (!IsEquipment(record)) return 0;
        Normalize(record!); var d = record!.Sub("durability")!; long before = (long)d.Num("current");
        d["current"] = Math.Max(0, before - (amount ?? DeathLoss));
        return before - (long)d.Num("current");
    }

    /// <summary>Death: every equipped item loses durability; a shared record (a staff in both hands) counts once.</summary>
    public static long WearEquipped(IEnumerable<PyDict?> loadout, long? amount = null)
    {
        var seen = new HashSet<PyDict>(ReferenceEqualityComparer.Instance); long lost = 0;
        foreach (var record in loadout) if (record != null && seen.Add(record)) lost += Wear(record, amount);
        return lost;
    }

    public static long Repair(PyDict record)
    {
        Normalize(record); var d = record.Sub("durability")!; long missing = (long)d.Num("max") - (long)d.Num("current");
        d["current"] = d["max"];
        return missing;
    }

    // ------------------------------------------------------------------ quality
    /// <summary>Base value raised by quality; never below the base. Integers stay integers (rounded half up).</summary>
    public static object Scaled(object value, long quality)
    {
        double raised = Py.Num(value) * (1 + QualityStep * quality);
        if (value is long n) return Math.Max(n, (long)Math.Floor(raised + .5));
        return Math.Max(Py.Num(value), PyMath.Round(raised, 4));
    }

    public static PyDict ApplyQuality(PyDict record)
    {
        var @base = record.Sub("quality_base");
        if (@base == null || @base.Count == 0) return record;
        long q = (long)record.Num("quality", 0);
        record["stats"] = new PyDict(@base.Sub("stats")!.Select(kv => new KeyValuePair<string, object?>(kv.Key, Scaled(kv.Value!, q))));
        if (@base.ContainsKey("value")) record["value"] = Scaled(@base["value"]!, q);
        var affixes = new PyDict(@base.Sub("affixes")!.Select(kv => new KeyValuePair<string, object?>(kv.Key, Scaled(kv.Value!, q))));
        record["affixes"] = affixes;
        if (record.Get("maximized") is string max && max.Length > 0 && affixes.TryGetValue(max, out var v))
            affixes[max] = v is long n ? (object)(long)Math.Floor(n * (1 + MaximizeBonus) + .5) : PyMath.Round(Py.Num(v) * (1 + MaximizeBonus), 4);
        return record;
    }

    /// <summary>One quality step. +25 needs the special material and maximizes one affix.</summary>
    public static (bool Ok, string Message) RaiseQuality(PyDict record, PyDict? materials = null)
    {
        if (!IsEquipment(record)) return (false, "Este item não tem qualidade.");
        Normalize(record); long q = (long)record.Num("quality");
        if (q >= QualityMax) return (false, $"Qualidade máxima (+{QualityMax}).");
        if (q + 1 >= QualitySpecial)
        {
            if (materials == null || materials.Num(SpecialMaterial, 0) <= 0)
                return (false, $"A qualidade +{QualitySpecial} exige um material especial (conteúdo de nível 60 ou mais).");
            materials[SpecialMaterial] = (long)materials.Num(SpecialMaterial) - 1;
        }
        if (!record.ContainsKey("quality_base"))
            record["quality_base"] = new PyDict
            {
                ["stats"] = new PyDict(record.Sub("stats") ?? new PyDict()), ["affixes"] = new PyDict(record.Sub("affixes") ?? new PyDict()),
                ["value"] = record.TryGetValue("value", out var value) ? value : 0L,
            };
        record["quality"] = q + 1;
        var baseAffixes = record.Sub("quality_base")!.Sub("affixes")!;
        if ((long)record.Num("quality") >= QualitySpecial && record.T("affixes") && !record.T("maximized"))
            record["maximized"] = Py.MaxBy(baseAffixes.Keys, k => Py.Num(baseAffixes[k]));
        ApplyQuality(record);
        return (true, $"Qualidade +{record["quality"]}.");
    }

    public static string Badge(object? record)
    {
        if (record is not PyDict r) return "";
        long q = Py.Truthy(r.Get("quality")) ? IntOf(r.Get("quality"), 0) : 0, old = Py.Truthy(r.Get("forge_rank")) ? IntOf(r.Get("forge_rank"), 0) : 0;
        return q != 0 ? $"+{q}" : old != 0 ? $"+{old}" : "";
    }

    public static string Describe(PyDict record)
    {
        long q = Py.Truthy(record.Get("quality")) ? IntOf(record.Get("quality"), 0) : 0, old = Py.Truthy(record.Get("forge_rank")) ? IntOf(record.Get("forge_rank"), 0) : 0;
        string text = $"{(record.TryGetValue("rarity", out var r) ? r : "Comum")}  |  Qualidade +{q}/{QualityMax}";
        return text + (old != 0 ? $"  |  Aprimoramento antigo +{old}" : "");
    }

    public static string AffixTone(PyDict record, string key) =>
        Equals(record.Get("maximized"), key) ? "maximized" : record.Num("quality", 0) > 0 ? "improved" : "base";
}
