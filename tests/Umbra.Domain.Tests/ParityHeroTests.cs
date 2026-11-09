using System.Text;
using System.Text.Json;
using Umbra.Domain.Combat;
using Umbra.Domain.Combat.V16;
using Umbra.Domain.Data;
using Umbra.Domain.Heroes;
using Umbra.Domain.Items;
using Umbra.Domain.Skills;
using Umbra.Domain.Util;

namespace Umbra.Domain.Tests;

/// <summary>
/// Phase 5.8a parity: the hero core replays tools/parity/hero_parity.py -> tests/parity/hero_cases.json (real composed
/// heroes of all seven classes). Every step compares the hero's stats, level, resource, timers, states and bag counters.
/// </summary>
[Collection("SkillEnv")]
public class ParityHeroTests
{
    private sealed class Map : IMap
    {
        public required int[,] Cells;
        public int Width => Cells.GetLength(1);
        public int Height => Cells.GetLength(0);
        public int Cell(int x, int y) => Cells[y, x];
        public bool Sanctuary(int x, int y) => false;
    }

    private static readonly JsonElement Root;

    static ParityHeroTests()
    {
        HeroTables.Load(UmbraData.FindRoot());
        Root = JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(UmbraData.FindRoot())!, "..", "..", "tests", "parity", "hero_cases.json"),
                                                    Encoding.UTF8)).RootElement;
    }

    private static Map BuildMap()
    {
        var rows = Root.GetProperty("map").EnumerateArray().Select(r => r.GetString()!).ToList();
        var cells = new int[rows.Count, rows[0].Length];
        for (int y = 0; y < rows.Count; y++) for (int x = 0; x < rows[0].Length; x++) cells[y, x] = rows[y][x] == '#' ? 1 : 0;
        return new Map { Cells = cells };
    }

    private static List<object?> L(params object?[] items) => items.ToList();

    private static List<object?> Rows(ICombatant e) =>
        (e.Statuses?.Kinds ?? new List<string>()).Select(k => (object?)L(k, e.Statuses!.Get(k)!.Select(r =>
            (object?)L((double)r.Ticks, r.Magnitude, r.Timer is int t ? (double)t : null, r.Carry)).ToList())).ToList();

    private static PyDict Snap(Hero p) => new()
    {
        ["x"] = (double)p.X, ["y"] = (double)p.Y, ["previous"] = L((double)p.PreviousPosition.X, (double)p.PreviousPosition.Y), ["alive"] = p.Alive,
        ["level"] = (double)p.Level, ["xp"] = (double)p.Xp, ["xp_to_next"] = (double)p.XpToNext, ["base_attack"] = (double)p.BaseAttack,
        ["base_defense"] = (double)p.BaseDefense, ["attack"] = (double)p.Attack, ["defense"] = (double)p.Defense, ["health"] = (double)p.Health,
        ["max_health"] = (double)p.MaxHealth, ["mana"] = p.Mana, ["max_mana"] = (double)p.MaxMana, ["resource"] = (double)p.Resource,
        ["max_resource"] = (double)p.MaxResource, ["regen_acc"] = p.RegenAccumulator, ["regeneration_timer"] = (double)p.RegenerationTimer,
        ["skill_cooldown"] = (double)p.SkillCooldown, ["dash_cooldown"] = (double)p.DashCooldown, ["invulnerability"] = (double)p.Invulnerability,
        ["power"] = L(p.TemporaryPower, (double)p.TemporaryPowerTimer), ["shield"] = (double)p.ShieldPoints, ["attack_cooldown"] = (double)p.AttackCooldown,
        ["combo"] = L((double)p.ComboIndex, (double)p.ComboWindow), ["statuses"] = Rows(p), ["fortify"] = L(p.Fortify, (double)p.FortifyTimer, p.FortifyCarry),
        ["potion"] = (double)p.Inventory.Potion, ["capacity"] = (double)p.Inventory.Capacity, ["points"] = (double)p.Tree.Points, ["gold"] = (double)p.Gold,
        ["relics"] = p.Relics.Cast<object?>().ToList(), ["ira_carry"] = p.IraCarry, ["name"] = p.Name,
        ["cooldowns"] = p.Skills.Cooldowns.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => (object?)L(kv.Key, (double)kv.Value)).ToList(),
        ["items"] = (double)p.Inventory.Items.Count,
    };

    private static object? Run(JsonElement op, Hero p, Map map, List<string> families)
    {
        int I(string k) => op.GetProperty(k).GetInt32();
        double D(string k) => op.GetProperty(k).GetDouble();
        string? S(string k) => op.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        switch (op.GetProperty("op").GetString())
        {
            case "xp": p.GainXp(I("n")); return null;
            case "hurt": return p.TakeDamage(D("n"));
            case "gain": return (double)p.GainResource(D("n"), S("source") ?? "combat");
            case "spend": return p.SpendResource((int)D("n"));
            case "tick": for (int i = 0; i < I("n"); i++) { p.Update(); Status.TickOnce(p); Resources.Tick(p); } return null;
            case "potion": return p.UsePotion();
            case "potions": return (double)Pages.AddPotions(p, I("n"));
            case "move": return p.Move(I("dx"), I("dy"), map);
            case "dash": return p.Dash(I("dx"), I("dy"), map);
            case "state": return (double)Status.Apply(p, S("kind")!, D("ticks"), D("mag"));
            case "stack": return (double)Resources.AddStack(p, S("kind")!, I("n"));
            case "fortify": return Resources.Fortify(p, D("n"));
            case "shield": p.ShieldPoints = I("n"); return null;
            case "power": p.TemporaryPower = D("value"); p.TemporaryPowerTimer = I("ticks"); return null;
            case "points": p.Tree.AddPoint(I("n")); return null;
            case "learn": return TreeRules.Learn(p, S("key")!);
            case "equip": { var (ok, msg) = Gear.Equip(p, (PyDict)Py.FromJson(op.GetProperty("record"))!); return L(ok, msg); }
            case "unequip": return Gear.Unequip(p, S("slot")!);
            case "damage":
            {
                bool skill = op.GetProperty("skill").GetBoolean();
                if (op.GetProperty("health").ValueKind == JsonValueKind.Null) return (double)p.AttackDamage(null, null, null, skill);
                return (double)p.AttackDamage(I("health"), 100, (p.X + I("dx"), p.Y + I("dy")), skill);
            }
            case "collect":
            {
                string kind = S("kind")!;
                var data = Loot.Drop(kind, I("floor"), p.Level, p.PlayerClass, SkillEnv.Rng, families);
                return L(p.Collect(kind, data), data);
            }
            case "recalc": p.RecalculateStats(); return null;
        }
        throw new InvalidOperationException(op.ToString());
    }

    public static IEnumerable<object[]> Cases() =>
        Root.GetProperty("cases").EnumerateArray().Select((c, i) => new object[] { i, c.GetProperty("name").GetString()! });

    [Theory]
    [MemberData(nameof(Cases))]
    public void SameHeroAsPython(int index, string name)
    {
        var @case = Root.GetProperty("cases")[index];
        string cls = @case.GetProperty("class").GetString()!;
        SkillEnv.Hooks = new HeroSkillHooks();
        SkillEnv.Rng = new PyRandom(@case.GetProperty("seed").GetInt64());
        var map = BuildMap(); var hero = new Hero((3, 3), cls) { ActiveMap = map };
        var families = Root.GetProperty("families").GetProperty(cls).EnumerateArray().Select(f => f.GetString()!).ToList();
        Compare.SameValue(@case.GetProperty("start"), Snap(hero), name + ", início");
        int step = 0;
        foreach (var s in @case.GetProperty("steps").EnumerateArray())
        {
            string where = $"{name}, passo {step} ({s.GetProperty("op").ToString()[..Math.Min(120, s.GetProperty("op").ToString().Length)]})";
            var result = Run(s.GetProperty("op"), hero, map, families);
            Compare.SameValue(s.GetProperty("result"), result, where + " retorno");
            Compare.SameValue(s.GetProperty("state"), Snap(hero), where);
            step++;
        }
    }
}
