using System.Text;
using System.Text.Json;
using Umbra.Domain.Combat;
using Umbra.Domain.Combat.V16;
using Umbra.Domain.Data;
using Umbra.Domain.Enemies;
using Umbra.Domain.Skills;
using Umbra.Domain.Util;

namespace Umbra.Domain.Tests;

/// <summary>
/// Phase 5.7 parity: enemies, AI, elites and bosses replay tools/parity/enemies_parity.py -> tests/parity/enemies_cases.json
/// (real composed enemies of UMBRA against a fixture hero). Every step compares every enemy and the hero.
/// </summary>
public class ParityEnemiesTests
{
    private sealed class Map : IMap
    {
        public required int[,] Cells;
        public int Width => Cells.GetLength(1);
        public int Height => Cells.GetLength(0);
        public int Cell(int x, int y) => Cells[y, x];
        public bool Sanctuary(int x, int y) => false;
    }

    private sealed class Hero : IFoeTarget
    {
        public string Name => "heroi";
        public int Health { get; set; } = 2000;
        public int MaxHealth => 2000;
        public bool Alive => Health > 0;
        public bool Boss => false;
        public void TakeDamage(int amount) => Health = Math.Max(0, Health - amount);
        public void Heal(int amount) => Health += amount;
        public StatusMap? Statuses { get; set; }
        public int CcPressure { get; set; }
        public int CcPressureTimer { get; set; }
        public double AbsorbedTotal { get; set; }
        public int? AttackWindup { get; set; }
        public IReadOnlyDictionary<string, double>? Resistances => null;
        public string? EnemyType => null;
        public IReadOnlyList<GearRecord> Gear => Array.Empty<GearRecord>();
        public int NecroVulnerableTicks => 0;
        public string ThornsElement => "physical";
        public int X { get; set; }
        public int Y { get; set; }
        public int WardBlocked { get; set; }
        public double WardGain => 0;
        public int Resource;
        public int GainResource(double amount, string source) { int n = (int)PyMath.Round(amount); Resource += n; return n; }
    }

    private static readonly JsonElement Root;

    static ParityEnemiesTests()
    {
        FoeTables.Load(UmbraData.FindRoot());
        Root = JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(UmbraData.FindRoot())!, "..", "..", "tests", "parity", "enemies_cases.json"),
                                                    Encoding.UTF8)).RootElement;
    }

    private static Map BuildMap(string name)
    {
        var rows = Root.GetProperty("maps").GetProperty(name).EnumerateArray().Select(r => r.GetString()!).ToList();
        var cells = new int[rows.Count, rows[0].Length];
        for (int y = 0; y < rows.Count; y++) for (int x = 0; x < rows[0].Length; x++) cells[y, x] = rows[y][x] == '#' ? 1 : 0;
        return new Map { Cells = cells };
    }

    private static List<object?> L(params object?[] items) => items.ToList();
    private static object? P((int X, int Y)? p) => p is { } v ? L((double)v.X, (double)v.Y) : null;

    private static List<object?> Rows(ICombatant e) =>
        (e.Statuses?.Kinds ?? new List<string>()).Select(k => (object?)L(k, e.Statuses!.Get(k)!.Select(r =>
            (object?)L((double)r.Ticks, r.Magnitude, r.Timer is int t ? (double)t : null, r.Carry, r.Source?.Name)).ToList())).ToList();

    private static PyDict EnemyState(Enemy e)
    {
        var row = new PyDict
        {
            ["name"] = e.Name, ["type"] = e.EnemyType, ["x"] = (double)e.X, ["y"] = (double)e.Y, ["health"] = (double)e.Health, ["max_health"] = (double)e.MaxHealth,
            ["attack"] = (double)e.Attack, ["defense"] = (double)e.Defense, ["alive"] = e.Alive, ["ai"] = e.AiState, ["move_cd"] = (double)e.MoveCooldown,
            ["attack_cd"] = (double)e.AttackCooldown, ["windup"] = (double)e.Windup, ["current"] = e.CurrentAttack, ["target"] = P(e.AttackTarget),
            ["direction"] = P(e.AttackDirection), ["origin"] = P(e.AttackOrigin), ["known"] = P(e.LastKnownPlayer), ["memory"] = (double)e.MemoryFrames,
            ["stagger"] = (double)e.StaggerFrames, ["stuck"] = (double)e.StuckFrames, ["previous"] = P(e.PreviousPosition), ["base_move"] = (double)e.BaseMoveCooldown,
            ["statuses"] = Rows(e), ["pressure"] = L((double)e.CcPressure, (double)e.CcPressureTimer),
            ["necro"] = L((double)e.NecroSlowTicks, (double)e.NecroFrozenTicks, (double)e.NecroVulnerableTicks, (double)e.NecroPrisonTicks),
            ["rank"] = Elites.Rank(e), ["last_kind"] = e.LastAttackKind, ["summon"] = e.SummonRequested, ["absorbed"] = e.AbsorbedTotal,
        };
        if (e.Controller is { } b)
            row["boss"] = new PyDict
            {
                ["phase"] = (double)b.Phase, ["triggered"] = b.PhaseTwoTriggered, ["cooldowns"] = b.AbilityCooldowns.Select(kv => (object?)L(kv.Key, (double)kv.Value)).ToList(),
                ["action"] = (double)b.ActionTimer, ["message"] = b.Message, ["message_timer"] = (double)b.MessageTimer, ["pending"] = b.PendingAbility,
                ["pending_timer"] = (double)b.PendingTimer, ["pending_total"] = (double)b.PendingTotal, ["pending_target"] = P(b.PendingTarget),
                ["pending_direction"] = P(b.PendingDirection), ["last"] = b.LastAbility, ["basic"] = L((double)b.BasicTimer, (double)b.BasicTotal, P(b.BasicTarget)),
                ["last_basic_target"] = P(b.LastBasicTarget), ["bone"] = (double)b.BoneArmor,
                ["zones"] = b.Zones.Select(z => (object?)L(P(z.Position), (double)z.Timer)).ToList(), ["summon"] = b.SummonRequested, ["teleport"] = b.TeleportRequested,
            };
        return row;
    }

    private static PyDict HeroState(Hero h) => new()
    {
        ["x"] = (double)h.X, ["y"] = (double)h.Y, ["health"] = (double)h.Health, ["statuses"] = Rows(h), ["ward_blocked"] = (double)h.WardBlocked, ["resource"] = (double)h.Resource,
    };

    private static object? Run(JsonElement op, Hero hero, List<Enemy> enemies, Map map, PyRandom rng)
    {
        int I(string k) => op.GetProperty(k).GetInt32();
        bool B(string k) => op.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.True;
        Enemy At() => enemies[I("i") % enemies.Count];
        switch (op.GetProperty("op").GetString())
        {
            case "spawn":
            {
                var at = op.GetProperty("at");
                var e = new Enemy((at[0].GetInt32(), at[1].GetInt32()), I("floor"), B("boss"), op.TryGetProperty("type", out var t) ? t.GetString() : null, B("summoned"));
                if (B("elite")) Elites.Promote(e);
                enemies.Add(e); return (double)enemies.Count;
            }
            case "tick":
                for (int n = 0; n < I("n"); n++)
                {
                    EnemyAI.Active = enemies;
                    foreach (var e in enemies.ToList()) if (e.Alive && !Enemy.Dormant(e, hero.X, hero.Y)) e.Update(hero, map, rng);
                }
                return null;
            case "move": { var at = op.GetProperty("at"); hero.X = at[0].GetInt32(); hero.Y = at[1].GetInt32(); return null; }
            case "hit": return (double)At().Damage(I("amount"));
            case "state": return (double)Status.Apply(At(), op.GetProperty("kind").GetString()!, op.GetProperty("ticks").GetDouble(), op.GetProperty("mag").GetDouble(), hero);
            case "hero_state": return (double)Status.Apply(hero, op.GetProperty("kind").GetString()!, op.GetProperty("ticks").GetDouble(), op.GetProperty("mag").GetDouble());
            case "necro":
            {
                var e = At(); int ticks = I("ticks");
                switch (op.GetProperty("field").GetString())
                {
                    case "necro_slow_ticks": e.NecroSlowTicks = ticks; break;
                    case "necro_frozen_ticks": e.NecroFrozenTicks = ticks; break;
                    case "necro_vulnerable_ticks": e.NecroVulnerableTicks = ticks; break;
                    default: e.NecroPrisonTicks = ticks; break;
                }
                return null;
            }
            case "heal_hero": hero.Health = I("value"); return null;
            case "elites": return (double)Elites.Roll(enemies, new PyRandom(I("seed")));
            case "rewards": { var e = At(); return L((double)Elites.Xp(e, I("xp")), (double)Elites.Gold(e, I("gold"))); }
        }
        throw new InvalidOperationException(op.ToString());
    }

    public static IEnumerable<object[]> Cases() =>
        Root.GetProperty("cases").EnumerateArray().Select((c, i) => new object[] { i, c.GetProperty("name").GetString()! });

    [Theory]
    [MemberData(nameof(Cases))]
    public void SameEnemiesAsPython(int index, string name)
    {
        var @case = Root.GetProperty("cases")[index];
        var map = BuildMap(@case.GetProperty("map").GetString()!);
        var heroAt = @case.GetProperty("hero");
        var hero = new Hero { X = heroAt[0].GetInt32(), Y = heroAt[1].GetInt32() };
        var enemies = new List<Enemy>(); var rng = new PyRandom(@case.GetProperty("seed").GetInt64());
        int step = 0;
        foreach (var s in @case.GetProperty("steps").EnumerateArray())
        {
            string where = $"{name}, passo {step} ({s.GetProperty("op")})";
            var result = Run(s.GetProperty("op"), hero, enemies, map, rng);
            Compare.SameValue(s.GetProperty("result"), result, where + " retorno");
            Compare.SameValue(s.GetProperty("hero"), HeroState(hero), where + " herói");
            var rows = s.GetProperty("enemies").EnumerateArray().ToList();
            Compare.True(rows.Count == enemies.Count, $"{where}: {rows.Count} inimigos esperados, {enemies.Count} obtidos");
            for (int i = 0; i < rows.Count; i++) Compare.SameValue(rows[i], EnemyState(enemies[i]), $"{where} inimigo {i}");
            step++;
        }
    }
}
