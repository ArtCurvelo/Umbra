using System.Text;
using System.Text.Json;
using Umbra.Domain.Combat;
using Umbra.Domain.Combat.V16;
using Umbra.Domain.Data;
using Umbra.Domain.Enemies;
using Umbra.Domain.Heroes;
using Umbra.Domain.Skills;
using Umbra.Domain.Util;

namespace Umbra.Domain.Tests;

/// <summary>
/// Phase 5.8b parity: the basic attack (resolver), slot skills and a full combat loop replay tools/parity/combat_parity.py ->
/// tests/parity/combat_cases.json (real composed heroes and enemies). Same SkillEnv collection as the engine tests.
/// </summary>
[Collection("SkillEnv")]
public class ParityCombatTests
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
    private static long _ticks;

    static ParityCombatTests()
    {
        HeroTables.Load(UmbraData.FindRoot()); FoeTables.Load(UmbraData.FindRoot());
        Root = JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(UmbraData.FindRoot())!, "..", "..", "tests", "parity", "combat_cases.json"),
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
    private static object? P((int X, int Y)? p) => p is { } v ? L((double)v.X, (double)v.Y) : null;
    private static List<object?> Rows(ICombatant e) =>
        (e.Statuses?.Kinds ?? new List<string>()).Select(k => (object?)L(k, e.Statuses!.Get(k)!.Select(r => (object?)L((double)r.Ticks, r.Magnitude)).ToList())).ToList();

    private static PyDict Snap(Hero p, List<Enemy> enemies)
    {
        var s = p.Skills; var w = s.World; var pending = p.Pending; var last = p.LastResult;
        var hero = new PyDict
        {
            ["x"] = (double)p.X, ["y"] = (double)p.Y, ["health"] = (double)p.Health, ["resource"] = (double)p.Resource, ["alive"] = p.Alive, ["shield"] = (double)p.ShieldPoints,
            ["pending"] = pending == null ? null : L(P(pending.Origin), P(pending.Target), P(pending.Direction), (double)pending.Reach, (double)pending.Timer, (double)pending.ComboIndex),
            ["attack_cooldown"] = (double)p.AttackCooldown, ["buffered"] = p.AttackBuffered, ["buffer_direction"] = P(p.BufferDirection),
            ["direction"] = P(p.AttackDirection), ["combo"] = L((double)p.ComboIndex, (double)p.ComboWindow),
            ["last"] = last == null ? null : L(last.Hit, (double)last.Damage, P(last.Target), last.Reason),
            ["basic"] = p.BasicSkillId, ["slots"] = p.EquippedMainSkills.Cast<object?>().ToList(), ["active_slot"] = (double)p.ActiveMainSlot, ["weapon_skill"] = p.ActiveWeaponSkill,
            ["cooldowns"] = s.Cooldowns.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => (object?)L(kv.Key, (double)kv.Value)).ToList(),
            ["statuses"] = Rows(p), ["last_skill"] = s.LastSkillId, ["zones"] = (double)(w?.Zones.Count ?? 0), ["events"] = (double)(w?.Events.Count ?? 0),
            ["channel"] = w?.Channel != null, ["hold"] = s.Hold,
            ["stacks"] = s.Stacks.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => (object?)L(kv.Key, (double)kv.Value.Count, (double)kv.Value.Ticks)).ToList(),
        };
        return new PyDict
        {
            ["hero"] = hero,
            ["enemies"] = enemies.Select(e => (object?)L((double)e.X, (double)e.Y, (double)e.Health, e.Alive, e.AiState, (double)e.Windup, Rows(e))).ToList(),
        };
    }

    private static void Tick(Hero p, List<Enemy> enemies, Map map, PyRandom rng)
    {
        _ticks++;
        p.ActiveEnemies = enemies; p.CombatEnemies = enemies; p.ActiveMap = map; EnemyAI.Active = enemies;
        p.Update();
        foreach (var e in enemies.ToList()) if (e.Alive && !Enemy.Dormant(e, p.X, p.Y)) e.Update(p, map, rng);
        Status.TickOnce(p); Resources.Tick(p);
        SkillWorldTick.Tick(new SkillScene(p, enemies, map));
    }

    private static object? Run(JsonElement op, Hero p, List<Enemy> enemies, Map map, PyRandom rng)
    {
        int I(string k) => op.GetProperty(k).GetInt32();
        string? S(string k) => op.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        switch (op.GetProperty("op").GetString())
        {
            case "learn": return TreeRules.Learn(p, S("key")!);
            case "equip": { var (ok, msg) = TreeRules.Equip(p, S("skill")!, I("index")); return L(ok, msg); }
            case "attack": p.ActiveMap = map; return HeroCombat.AttackEnemy(p, enemies);
            case "skill": p.ActiveMap = map; return HeroCombat.UseSkill(p, enemies, I("slot"));
            case "tick": for (int i = 0; i < I("n"); i++) Tick(p, enemies, map, rng); return null;
            case "face": { var d = op.GetProperty("d"); p.AttackDirection = (d[0].GetInt32(), d[1].GetInt32()); return null; }
            case "aim": { var a = op.GetProperty("at"); p.AimTarget = a.ValueKind == JsonValueKind.Null ? null : (a[0].GetInt32(), a[1].GetInt32()); return null; }
            case "move": return p.Move(I("dx"), I("dy"), map);
            case "resource": p.Resource = I("n"); return null;
            case "hold": p.Skills.Hold = op.GetProperty("on").GetBoolean(); return null;
            case "gear": { var (ok, msg) = Umbra.Domain.Items.Gear.Equip(p, (PyDict)Py.FromJson(op.GetProperty("record"))!); return L(ok, msg); }
            case "spawn": { var at = op.GetProperty("at"); enemies.Add(new Enemy((at[0].GetInt32(), at[1].GetInt32()), I("floor"), false, S("type"))); return (double)enemies.Count; }
        }
        throw new InvalidOperationException(op.ToString());
    }

    public static IEnumerable<object[]> Cases() =>
        Root.GetProperty("cases").EnumerateArray().Select((c, i) => new object[] { i, c.GetProperty("name").GetString()! });

    [Theory]
    [MemberData(nameof(Cases))]
    public void SameFightAsPython(int index, string name)
    {
        var @case = Root.GetProperty("cases")[index];
        SkillEnv.Hooks = new HeroSkillHooks(); var rng = new PyRandom(@case.GetProperty("seed").GetInt64()); SkillEnv.Rng = rng;
        _ticks = 0; SkillEnv.Clock = () => _ticks * 1000 / 60;
        var map = BuildMap(); var at = @case.GetProperty("hero");
        var hero = new Hero((at[0].GetInt32(), at[1].GetInt32()), @case.GetProperty("class").GetString()!) { ActiveMap = map };
        hero.Tree.AddPoint(200); hero.Level = 40; hero.RecalculateStats(); hero.Health = hero.MaxHealth;
        var enemies = @case.GetProperty("enemies").EnumerateArray()
            .Select(e => new Enemy((e[0][0].GetInt32(), e[0][1].GetInt32()), e[1].GetInt32(), false, e[2].GetString())).ToList();
        int step = 0;
        foreach (var s in @case.GetProperty("steps").EnumerateArray())
        {
            string op = s.GetProperty("op").ToString(); string where = $"{name}, passo {step} ({op[..Math.Min(100, op.Length)]})";
            var result = Run(s.GetProperty("op"), hero, enemies, map, rng);
            Compare.SameValue(s.GetProperty("result"), result, where + " retorno");
            Compare.SameValue(s.GetProperty("state"), Snap(hero, enemies), where);
            step++;
        }
    }
}
