using Umbra.Domain.Items;
using Umbra.Domain.Skills;

namespace Umbra.Domain.Play;

/// <summary>How an item record reads on screen (names, rarity tag, the slot it goes to, stat and affix lines, comparison).
/// Formatting only: the numbers are the record's.</summary>
public static class ItemView
{
    private static readonly Dictionary<string, string> Tags = new()
    {
        ["Comum"] = "C", ["Mágico"] = "M", ["Raro"] = "R", ["Lendário"] = "L", ["Único"] = "U", ["Único Mítico"] = "UM",
    };
    private static readonly Dictionary<string, string> StatNames = new()
    {
        ["attack"] = "ataque", ["defense"] = "armadura", ["health"] = "vida", ["mana"] = "recurso",
    };
    public static readonly string[] Keys = { "attack", "defense", "health", "mana" };

    public static string Name(PyDict r) => r.Str("name", "Item");
    public static string Rarity(PyDict r) => Items.Rarity.Canonical(r.Get("rarity"));
    public static string Tag(PyDict r) => Tags.GetValueOrDefault(Rarity(r), "C");
    public static bool Equipment(PyDict r) => ItemData.IsEquipment(r);

    /// <summary>Short kind for a slot cell: ARMA, ELMO, ANEL...</summary>
    public static string Kind(PyDict r)
    {
        if (r.Get("weapon_type") is string w) return w switch
        {
            "shield" => "ESCUDO", "focus" => "FOCO", "staff" => "CAJADO", "wand" => "VARINHA", "bow" or "crossbow" => "ARCO", "dagger" => "ADAGA",
            "mace2h" => "MALHO", "axe1h" or "axe2h" => "MACHADO", "scythe" => "FOICE", _ => "ESPADA",
        };
        return r.Str("slot") switch
        {
            "helmet" => "ELMO", "chest" or "armor" => "PEITO", "gloves" => "LUVAS", "pants" => "CALÇA", "boots" => "BOTAS",
            "amulet" => "AMULETO", "ring" => "ANEL", _ => r.Str("kind").ToUpperInvariant(),
        };
    }

    public static List<string> StatLines(PyDict r) =>
        (r.Sub("stats") ?? new PyDict()).Select(kv => $"+{kv.Value} {StatNames.GetValueOrDefault(kv.Key, kv.Key)}").ToList();

    public static List<string> AffixLines(PyDict r)
    {
        var lines = (r.Sub("affixes") ?? new PyDict()).Select(kv => NecroAffixes.Text(kv.Key, kv.Value!)).ToList();
        foreach (var (k, v) in Temper.Bonus(r)) lines.Add($"+{v} {StatNames.GetValueOrDefault(k, k)} (têmpera)");
        return lines;
    }

    public static string Footer(PyDict r)
    {
        var d = r.Sub("durability");
        string durability = d != null ? $"  |  Durabilidade {d["current"]}/{d["max"]}" : "";
        return $"Nível {r.Get("required_level") ?? 1}  |  iLvl {r.Get("item_level") ?? 1}{durability}";
    }

    public static long Total(PyDict? r, string key) =>
        r == null ? 0 : (long)Py.Num((r.Sub("stats") ?? new PyDict()).Get(key) ?? 0L) + (long)Py.Num((r.Sub("affixes") ?? new PyDict()).Get(key) ?? 0L)
                        + (long)Py.Num(Temper.Bonus(r).Get(key) ?? 0L);

    /// <summary>Differences against the item it would replace (stats + affixes + temper), better first.</summary>
    public static List<(string Text, bool Better)> Compare(PyDict item, PyDict? equipped)
    {
        var lines = new List<(string, bool)>();
        foreach (var key in Keys)
        {
            long delta = Total(item, key) - Total(equipped, key);
            if (delta != 0) lines.Add(($"{(delta > 0 ? "+" : "-")}{Math.Abs(delta)} {StatNames[key]}", delta > 0));
        }
        return lines;
    }
}
