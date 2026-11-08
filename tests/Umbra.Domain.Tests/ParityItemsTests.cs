using System.Text;
using System.Text.Json;
using Umbra.Domain.Data;
using Umbra.Domain.Items;
using Umbra.Domain.Skills;
using Umbra.Domain.Util;

namespace Umbra.Domain.Tests;

/// <summary>
/// Phase 5.6 parity: drops, records, equipment slots, bag pages and crafting replay tools/parity/items_parity.py ->
/// tests/parity/items_cases.json (recorded on real heroes of UMBRA's composed engine). The comparison is strict about
/// Python's int/float: a whole number must be a long here and a decimal a double, because quality depends on it.
/// </summary>
public class ParityItemsTests
{
    private sealed class GearHero : IGearOwner
    {
        public required string PlayerClass { get; init; }
        public long Level { get; set; }
        public long Gold { get; set; }
        public Bag Inventory { get; } = new();
        public PyDict? Loadout { get; set; }
        public PyDict Equipment { get; } = new();
        public bool CanonicalReady { get; set; }
        public string? BasicSkillId { get; init; }
        public string? ActiveWeaponSkill { get; init; }
        public PyDict Materials { get; } = new();
        public List<PyDict> LostItems { get; } = new();
        public long PotionCapacity { get; init; }

        /// <summary>The part of the hero's recalculate_stats that touches items (equipment/stats.recalculate).</summary>
        public void RefreshStats()
        {
            if (Loadout == null || !Loadout.Values.Any(Py.Truthy)) return;
            Gear.Ensure(this); Gear.SyncAliases(this, ActiveWeaponSkill);
        }
    }

    private static readonly JsonElement Root;

    static ParityItemsTests()
    {
        ItemTables.Load(UmbraData.FindRoot());
        Root = JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(UmbraData.FindRoot())!, "..", "..", "tests", "parity", "items_cases.json"),
                                                    Encoding.UTF8)).RootElement;
    }

    private static GearHero Build(JsonElement @case)
    {
        var start = @case.GetProperty("start"); var extra = @case.GetProperty("start_extra");
        string? S(JsonElement e, string k) => e.GetProperty(k).ValueKind == JsonValueKind.String ? e.GetProperty(k).GetString() : null;
        var hero = new GearHero
        {
            PlayerClass = @case.GetProperty("class").GetString()!, Level = start.GetProperty("level").GetInt64(), Gold = start.GetProperty("gold").GetInt64(),
            BasicSkillId = S(extra, "basic_skill_id"), ActiveWeaponSkill = S(extra, "active_weapon_skill"), PotionCapacity = extra.GetProperty("potion_capacity").GetInt64(),
        };
        hero.Inventory.Potion = start.GetProperty("potion").GetInt64(); hero.Inventory.Capacity = start.GetProperty("capacity").GetInt64();
        hero.Loadout = new PyDict(start.GetProperty("loadout").EnumerateArray().Select(r => new KeyValuePair<string, object?>(r[0].GetString()!, null)));
        foreach (var alias in start.GetProperty("aliases").EnumerateObject()) hero.Equipment[alias.Name] = null;
        return hero;
    }

    private static PyDict? BagRecord(GearHero p, int index) => p.Inventory.Items.Count == 0 ? null : p.Inventory.Items[index % p.Inventory.Items.Count];
    private static List<object?> L(params object?[] items) => items.ToList();
    private static List<object?> Pair((bool Ok, string Message) r) => L(r.Ok, r.Message);

    private static object? Run(JsonElement op, GearHero p, PyDict unlocks, PyRandom rng)
    {
        string? S(string k) => op.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        long N(string k) => op.GetProperty(k).GetInt64();
        int I(string k) => op.GetProperty(k).GetInt32();
        int? At() => op.TryGetProperty("at", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;
        bool B(string k) => op.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.True;
        switch (op.GetProperty("op").GetString())
        {
            case "drop":
            {
                string? cls = S("cls"); long? level = op.GetProperty("level").ValueKind == JsonValueKind.Number ? N("level") : null;
                var families = cls != null ? Root.GetProperty("families").GetProperty(cls).EnumerateArray().Select(f => f.GetString()!).ToList() : null;
                var data = Loot.Drop(S("kind")!, N("floor"), level, cls, rng, families);
                if (B("keep")) return Pair(Pages.Add(p.Inventory, data));
                if (B("lose")) return Pages.Keep(p, data);
                return data;
            }
            case "equip": return Pair(Gear.FromInventory(p, I("index"), S("slot")));
            case "equip_last": return Pair(Gear.FromInventory(p, p.Inventory.Items.Count - 1 - I("back")));
            case "unequip": return Gear.Unequip(p, S("slot")!);
            case "forge": return Pair(Smith.Transact(p, S("action")!, I("index")));
            case "wear": return ItemData.WearEquipped(p.Loadout!.Values.Cast<PyDict?>(), N("amount"));
            case "repair_all": return Pair(Smith.RepairAll(p));
            case "repair": { var r = BagRecord(p, I("index")); return r == null ? null : Pair(Smith.Repair(p, r)); }
            case "add_socket": { var r = BagRecord(p, I("index")); return r == null ? null : Pair(Jeweler.AddSocket(p, r)); }
            case "give_gem": return Pair(Pages.Add(p.Inventory, Gems.Make(S("family")!, N("grade"), N("count"))));
            case "socket": { var r = BagRecord(p, I("index")); return r == null ? null : Pair(Jeweler.SocketGem(p, r, S("gid")!, At())); }
            case "unsocket": { var r = BagRecord(p, I("index")); return r == null ? null : Pair(Jeweler.Unsocket(p, r, At()!.Value)); }
            case "gem_create": return Pair(Jeweler.Create(p, S("family")!, N("grade"), N("times")));
            case "gem_recycle": return Pair(Jeweler.Recycle(p, S("gid")!));
            case "temper": { var r = BagRecord(p, I("index")); return r == null ? null : Pair(Temper.Apply(p, r, S("recipe")!, unlocks, rng)); }
            case "unlock_temper": return Temper.Unlock(unlocks, S("recipe")!);
            case "enchant":
            {
                var r = BagRecord(p, I("index"));
                if (r == null || !r.T("affixes")) return null;
                var keys = r.Sub("affixes")!.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
                var (ok, message, key) = Enchant.Apply(p, r, keys[I("pick") % keys.Count], rng);
                return L(ok, message, key);
            }
            case "junk": { var r = BagRecord(p, I("index")); return r == null ? null : Smith.ToggleJunk(r); }
            case "lock": { var r = BagRecord(p, I("index")); if (r != null) r["locked"] = !r.T("locked"); return null; }
            case "recycle":
            {
                var plan = Smith.MakePlan(p, S("mode")!);
                var planned = new PyDict
                {
                    ["mode"] = plan.Mode, ["indices"] = plan.Indices.Select(i => (object?)(long)i).ToList(), ["tally"] = plan.Tally, ["gold"] = plan.Gold,
                    ["materials"] = plan.Materials, ["confirm"] = plan.Confirm,
                }.Copy();
                return L(planned, Pair(Smith.Execute(p, plan, B("confirm"), unlocks)));
            }
            case "engrave": { var r = BagRecord(p, I("index")); return r == null ? null : Pair(Codex.Engrave(p, r, S("aspect")!, unlocks)); }
            case "look":
            {
                var r = BagRecord(p, I("index"));
                if (r == null) return null;
                var looks = Codex.LooksFor(unlocks, r); int pick = I("pick");
                string? key = pick < 0 || looks.Count == 0 ? null : looks[pick % looks.Count].Key;
                return Pair(Codex.ApplyLook(r, unlocks, key));
            }
            case "put": return Pair(Pages.Add(p.Inventory, (PyDict)Py.FromJson(op.GetProperty("record"))!));
            case "material": Materials.Add(p, S("id")!, N("n")); return null;
            case "gold": p.Gold = N("n"); return null;
            case "level": p.Level = N("n"); return null;
            case "recover": return Pair(Pages.Recover(p, I("index")));
            case "potions": return Pages.AddPotions(p, N("n"));
            case "legacy": { var r = (PyDict)Py.FromJson(op.GetProperty("record"))!; ItemData.Normalize(r); ItemData.Normalize(r); return r; }
        }
        throw new InvalidOperationException(op.ToString());
    }

    private static PyDict Snap(GearHero p, PyDict unlocks, IReadOnlyList<string> necroKeys)
    {
        var gear = p.Loadout ?? new PyDict();
        var aliases = new PyDict();
        foreach (var (key, value) in p.Equipment)
        {
            var same = gear.FirstOrDefault(kv => kv.Value != null && ReferenceEquals(kv.Value, value)).Key;
            aliases[key] = same != null ? L("slot", same) : L("value", value);
        }
        var bag = p.Inventory;
        return new PyDict
        {
            ["gold"] = p.Gold, ["level"] = p.Level, ["materials"] = p.Materials,
            ["items"] = bag.Items.Cast<object?>().ToList(), ["gems"] = bag.Gems.Cast<object?>().ToList(), ["consumables"] = bag.Consumables.Cast<object?>().ToList(),
            ["potion"] = bag.Potion, ["capacity"] = bag.Capacity, ["loadout"] = gear.Select(kv => (object?)L(kv.Key, kv.Value)).ToList(), ["aliases"] = aliases,
            ["lost"] = p.LostItems.Cast<object?>().ToList(), ["unlocks"] = unlocks,
            ["aspects"] = Gear.ActiveAspects(p).OrderBy(a => a, StringComparer.Ordinal).Cast<object?>().ToList(), ["active_key"] = Gear.ActiveKey(p),
            ["gem_bonuses"] = gear.Values.Select(r => r is PyDict d && Py.Truthy(d) ? (object?)Gems.Bonuses(d) : null).ToList(),
            ["necro"] = necroKeys.Select(k => (object?)NecroAffixes.Total(gear.Values.Cast<PyDict?>(), k)).ToList(),
            ["slots_free"] = new[] { "equipment", "gems", "consumables", "keys" }.Select(page => (object?)Pages.Free(bag, page)).ToList(),
        };
    }

    // ------------------------------------------------------------------ strict comparison (int vs float)
    private static void Fail(string message) => throw new Xunit.Sdk.XunitException(message);

    private static void Strict(JsonElement expected, object? actual, string where)
    {
        switch (expected.ValueKind)
        {
            case JsonValueKind.Null: if (actual != null) Fail($"{where}: esperado null, obtido {actual}"); break;
            case JsonValueKind.True or JsonValueKind.False: if (actual is not bool b || b != expected.GetBoolean()) Fail($"{where}: esperado {expected}, obtido {actual}"); break;
            case JsonValueKind.Number:
                if (Py.IsInteger(expected))
                {
                    if (actual is not long n) Fail($"{where}: esperado inteiro {expected}, obtido {actual?.GetType().Name} {actual}");
                    else if (n != expected.GetInt64()) Fail($"{where}: esperado {expected}, obtido {n}");
                }
                else
                {
                    if (actual is not double d) Fail($"{where}: esperado decimal {expected}, obtido {actual?.GetType().Name} {actual}");
                    else if (Math.Abs(d - expected.GetDouble()) > 1e-12 * Math.Max(1, Math.Abs(d))) Fail($"{where}: esperado {expected}, obtido {d}");
                }
                break;
            case JsonValueKind.String: if (actual as string != expected.GetString()) Fail($"{where}: esperado {expected}, obtido {actual}"); break;
            case JsonValueKind.Array:
            {
                var rows = expected.EnumerateArray().ToList();
                if (actual is not List<object?> list || list.Count != rows.Count) { Fail($"{where}: lista {expected} vs {Describe(actual)}"); return; }
                for (int i = 0; i < rows.Count; i++) Strict(rows[i], list[i], $"{where}[{i}]");
                break;
            }
            case JsonValueKind.Object:
            {
                if (actual is not PyDict dict) { Fail($"{where}: esperado objeto {expected}, obtido {Describe(actual)}"); return; }
                var names = expected.EnumerateObject().Select(p => p.Name).ToList();
                var diff = names.Except(dict.Keys).Select(k => "-" + k).Concat(dict.Keys.Except(names).Select(k => "+" + k)).ToList();
                if (diff.Count > 0) Fail($"{where}: chaves diferentes [{string.Join(",", diff)}]");
                foreach (var prop in expected.EnumerateObject()) Strict(prop.Value, dict[prop.Name], $"{where}.{prop.Name}");
                break;
            }
        }
    }

    private static string Describe(object? v) => v switch
    {
        null => "null", List<object?> l => "[" + string.Join(",", l.Select(Describe)) + "]",
        PyDict d => "{" + string.Join(",", d.Select(kv => kv.Key + ":" + Describe(kv.Value))) + "}", _ => v.ToString()!,
    };

    public static IEnumerable<object[]> Cases() =>
        Root.GetProperty("cases").EnumerateArray().Select((c, i) => new object[] { i, c.GetProperty("name").GetString()! });

    [Theory]
    [MemberData(nameof(Cases))]
    public void SameItemsAsPython(int index, string name)
    {
        var @case = Root.GetProperty("cases")[index];
        var hero = Build(@case); var unlocks = new PyDict { ["codex"] = new List<object?>(), ["wardrobe"] = new PyDict(), ["temper"] = new List<object?>() };
        var rng = new PyRandom(@case.GetProperty("seed").GetInt64());
        var necroKeys = Root.GetProperty("necro_keys").EnumerateArray().Select(k => k.GetString()!).ToList();
        Strict(@case.GetProperty("start"), Snap(hero, unlocks, necroKeys), name + ", início");
        int step = 0;
        foreach (var s in @case.GetProperty("steps").EnumerateArray())
        {
            string where = $"{name}, passo {step} ({s.GetProperty("op")})";
            var result = Run(s.GetProperty("op"), hero, unlocks, rng);
            Strict(s.GetProperty("result"), result, where + " retorno");
            Strict(s.GetProperty("state"), Snap(hero, unlocks, necroKeys), where);
            step++;
        }
    }
}
