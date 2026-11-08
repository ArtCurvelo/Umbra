using System.Text;
using System.Text.Json;
using Umbra.Domain.Combat;
using Umbra.Domain.Combat.V16;
using Umbra.Domain.Data;
using Umbra.Domain.Skills;
using Umbra.Domain.Util;

namespace Umbra.Domain.Tests;

/// <summary>
/// Phase 5.4 parity: the C# skill engine (Combat/V16 + Skills/Tuning) replays tools/parity/skills_parity.py ->
/// tests/parity/skills_cases.json. After every step it checks the return value, the resolved skill (options and rank),
/// the full hero (life, resource, position, states, cooldowns, charges, stacks, imbue, ramp, zones, events, effects,
/// auras, channel) and every enemy. The fixture (hero, enemies, map) is the same as the Python one, rule for rule.
/// </summary>
public class ParitySkillsTests
{
    private sealed class Map : IMap
    {
        public int Width { get; init; }
        public int Height { get; init; }
        public HashSet<(int, int)> Walls { get; init; } = new();
        public (int A, int B, int C, int D) City { get; init; }
        public int Cell(int x, int y) => Walls.Contains((x, y)) ? 1 : 0;
        public bool Sanctuary(int x, int y) => City.A <= x && x < City.C && City.B <= y && y < City.D;
    }

    private class Body : ITarget
    {
        public string Name { get; init; } = "";
        public int Health { get; set; }
        public int MaxHealth { get; init; }
        public bool Alive => Health > 0;
        public bool Boss { get; init; }
        public void TakeDamage(int amount) => Health = Math.Max(0, Health - amount);
        public void Heal(int amount) => Health += amount;
        public StatusMap? Statuses { get; set; }
        public int CcPressure { get; set; }
        public int CcPressureTimer { get; set; }
        public double AbsorbedTotal { get; set; }
        public int? AttackWindup { get; set; }
        public IReadOnlyDictionary<string, double>? Resistances => null;
        public string? EnemyType { get; init; }
        public IReadOnlyList<GearRecord> Gear => Array.Empty<GearRecord>();
        public int NecroVulnerableTicks => 0;
        public virtual string ThornsElement => "physical";
        public int X { get; set; }
        public int Y { get; set; }
        public (int X, int Y) AttackDirection { get; set; }
        public bool V16Dead { get; set; }
    }

    private sealed class Hero : Body, ISkillUser
    {
        public double Fortify { get; set; }
        public int FortifyTimer { get; set; }
        public double FortifyCarry { get; set; }
        public int Resource { get; set; } = 60;
        public int MaxResource { get; init; } = 100;
        public double Attack => 40;
        public (int X, int Y)? AimTarget { get; set; }
        public string? ThornsChoice { get; set; }
        public override string ThornsElement => ThornsChoice ?? "physical";
        public int DashCooldown { get; set; }
        public int WardBlocked => 0;
        public bool HasShield { get; init; }
        public int ActiveMainSlot => 0;
        public int Recalcs;
        public SkillState Skills { get; } = new();

        public bool SpendResource(int amount)
        {
            amount = Math.Max(0, amount);
            if (Resource < amount) return false;
            Resource -= amount; return true;
        }

        public int GainResource(double amount, string source)
        {
            int gained = (int)PyMath.Round(amount); Resource = Math.Min(MaxResource, Resource + gained); return gained;
        }

        public double GetAttackDamage(ITarget? enemy, bool skill) => Attack * (skill ? 1.1 : 1.0);
        public void RecalculateStats() => Recalcs++;
        public string PlayerClass { get; set; } = "barbaro";
        public SkillTree? Tree { get; set; }
    }

    private static readonly JsonElement Root = JsonDocument.Parse(File.ReadAllText(
        Path.Combine(Path.GetDirectoryName(UmbraData.FindRoot())!, "..", "..", "tests", "parity", "skills_cases.json"), Encoding.UTF8)).RootElement;
    private static readonly UmbraData Catalog = UmbraData.Load(UmbraData.FindRoot());
    private static long _ticks;

    static ParitySkillsTests()
    {
        TreeCatalog.Load(UmbraData.FindRoot()); Passives.Use(Catalog);
        SkillEnv.Clock = () => _ticks * 1000 / 60;                     // pygame.time.get_ticks in the recording
    }

    private static Map BuildMap()
    {
        var m = Root.GetProperty("map"); var city = m.GetProperty("city").EnumerateArray().Select(v => v.GetInt32()).ToArray();
        return new Map
        {
            Width = m.GetProperty("width").GetInt32(), Height = m.GetProperty("height").GetInt32(),
            Walls = m.GetProperty("walls").EnumerateArray().Select(w => (w[0].GetInt32(), w[1].GetInt32())).ToHashSet(),
            City = (city[0], city[1], city[2], city[3]),
        };
    }

    private sealed class Scene
    {
        public required Hero Hero;
        public required List<Body> Enemies;
        public required Map Map;
        public Body Find(string name) => Enemies.First(e => e.Name == name);
    }

    private static Scene Build(bool shield)
    {
        var enemies = Root.GetProperty("enemies").EnumerateArray().Select(r => new Body
        {
            Name = r[0].GetString()!, X = r[1].GetInt32(), Y = r[2].GetInt32(), Health = r[3].GetInt32(), MaxHealth = r[3].GetInt32(),
            Boss = r[4].GetBoolean(), AttackDirection = (r[5][0].GetInt32(), r[5][1].GetInt32()),
            EnemyType = r[6].ValueKind == JsonValueKind.String ? r[6].GetString() : null,
        }).ToList();
        return new Scene { Hero = new Hero { Name = "heroi", Health = 500, MaxHealth = 500, X = 8, Y = 8, AttackDirection = (1, 0), HasShield = shield },
                           Enemies = enemies, Map = BuildMap() };
    }

    private static PyDict Resolve(JsonElement op) =>
        Tuning.Resolve((PyDict)Py.FromJson(Catalog.Skill(op.GetProperty("skill").GetString()!).Raw)!, op.GetProperty("rank").GetInt32(),
                       op.GetProperty("picks").EnumerateArray().Select(v => v.GetInt32()));

    private static (object? Result, PyDict? Spec) Run(JsonElement op, Scene s)
    {
        var p = s.Hero; var enemies = s.Enemies.Cast<ITarget>().ToList();
        switch (op.GetProperty("op").GetString())
        {
            case "cast": { var spec = Resolve(op); var frozen = spec.Copy(); return (SkillEngine.Cast(p, spec, enemies, s.Map), frozen); }
            case "strike":
            {
                var spec = Resolve(op); var frozen = spec.Copy();
                return (SkillEngine.Strike(p, spec, s.Find(op.GetProperty("target").GetString()!), enemies, s.Map, op.GetProperty("factor").GetDouble()), frozen);
            }
            case "tick":
                for (int i = 0; i < op.GetProperty("n").GetInt32(); i++)
                {
                    _ticks++;
                    SkillWorldTick.Tick(new SkillScene(p, enemies, s.Map));
                    Status.TickOnce(p); foreach (var e in s.Enemies) Status.TickOnce(e);
                    foreach (var k in p.Skills.Cooldowns.Keys.ToList()) p.Skills.Cooldowns[k] = Math.Max(0, p.Skills.Cooldowns[k] - 1);
                }
                return (null, null);
            case "hold": p.Skills.Hold = op.GetProperty("on").GetBoolean(); return (null, null);
            case "move":
            {
                string who = op.GetProperty("who").GetString()!; Body body = who == "heroi" ? p : s.Find(who);
                body.X = op.GetProperty("x").GetInt32(); body.Y = op.GetProperty("y").GetInt32(); return (null, null);
            }
            case "aim":
            {
                var at = op.GetProperty("at");
                p.AimTarget = at.ValueKind == JsonValueKind.Null ? null : (at[0].GetInt32(), at[1].GetInt32()); return (null, null);
            }
            case "face": { var d = op.GetProperty("d"); p.AttackDirection = (d[0].GetInt32(), d[1].GetInt32()); return (null, null); }
            case "hurt": p.Health = Math.Max(1, p.Health - op.GetProperty("amount").GetInt32()); return (null, null);
            case "resource": p.Resource = op.GetProperty("value").GetInt32(); return (null, null);
            case "enemy_state":
                return (Status.Apply(s.Find(op.GetProperty("target").GetString()!), op.GetProperty("kind").GetString()!,
                                     op.GetProperty("ticks").GetDouble(), op.GetProperty("mag").GetDouble(), p), null);
            case "dodge": p.DashCooldown = op.GetProperty("value").GetInt32(); return (null, null);
        }
        throw new InvalidOperationException();
    }

    // ------------------------------------------------------------------ comparison
    private static void Same(double expected, double actual, string where)
    {
        if (Math.Abs(expected - actual) > 1e-9 * Math.Max(1, Math.Abs(expected))) throw new Xunit.Sdk.XunitException($"{where}: esperado {expected}, obtido {actual}");
    }

    private static void True(bool ok, string message) { if (!ok) throw new Xunit.Sdk.XunitException(message); }

    private static void SameValue(JsonElement expected, object? actual, string where)
    {
        switch (expected.ValueKind)
        {
            case JsonValueKind.Null: True(actual == null, $"{where}: esperado null, obtido {actual}"); break;
            case JsonValueKind.True or JsonValueKind.False:
                True(actual is bool b && b == expected.GetBoolean(), $"{where}: esperado {expected}, obtido {actual}"); break;
            case JsonValueKind.Number: True(actual is double or int or long or bool, $"{where}: esperado número, obtido {actual}"); Same(expected.GetDouble(), Py.Num(actual), where); break;
            case JsonValueKind.String: True(actual as string == expected.GetString(), $"{where}: esperado {expected}, obtido {actual}"); break;
            case JsonValueKind.Array:
            {
                var list = actual as List<object?>; var rows = expected.EnumerateArray().ToList();
                True(list != null && list.Count == rows.Count, $"{where}: lista {expected} vs {actual}");
                for (int i = 0; i < rows.Count; i++) SameValue(rows[i], list![i], $"{where}[{i}]");
                break;
            }
            case JsonValueKind.Object:
            {
                var dict = actual as PyDict; var props = expected.EnumerateObject().ToList();
                True(dict != null, $"{where}: esperado objeto, obtido {actual}");
                var missing = props.Select(p => p.Name).Except(dict!.Keys).Concat(dict.Keys.Except(props.Select(p => p.Name))).ToList();
                True(missing.Count == 0, $"{where}: chaves diferentes [{string.Join(",", missing)}]");
                foreach (var prop in props) SameValue(prop.Value, dict[prop.Name], $"{where}.{prop.Name}");
                break;
            }
        }
    }

    private static object? Pos((int X, int Y)? p) => p is { } v ? new List<object?> { (double)v.X, (double)v.Y } : null;
    private static List<object?> L(params object?[] items) => items.ToList();

    private static List<object?> Rows(ICombatant e) =>
        (e.Statuses?.Kinds ?? new List<string>()).Select(k => (object?)L(k, e.Statuses!.Get(k)!.Select(r =>
            (object?)L((double)r.Ticks, r.Magnitude, r.Timer is int t ? (double)t : null, r.Carry, r.Source?.Name)).ToList())).ToList();

    private static List<object?> Sorted<T>(IEnumerable<KeyValuePair<string, T>> rows, Func<string, T, List<object?>> row) =>
        rows.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => (object?)row(kv.Key, kv.Value)).ToList();

    private static PyDict HeroState(Hero p, Scene s)
    {
        var k = p.Skills;
        var state = new PyDict
        {
            ["health"] = (double)p.Health, ["resource"] = (double)p.Resource, ["x"] = (double)p.X, ["y"] = (double)p.Y, ["statuses"] = Rows(p),
            ["cooldowns"] = Sorted(k.Cooldowns, (id, v) => L(id, (double)v)), ["cooldown"] = (double)k.Cooldown,
            ["charges"] = Sorted(k.Charges, (id, v) => L(id, (double)v)),
            ["stacks"] = Sorted(k.Stacks, (id, v) => L(id, (double)v.Count, (double)v.Ticks)),
            ["return"] = k.Return is { } r ? L(Pos(r.Origin), (double)r.Ticks) : null,
            ["imbue"] = k.Imbue is { } im ? L(im.Spec.Str("id"), (double)im.Charges) : null,
            ["ramp"] = k.HasRamp ? L((k.Ramp.Target as Body)?.Name, (double)k.Ramp.Count) : null,
            ["thorns"] = p.ThornsChoice, ["ward_gain"] = k.WardGain, ["absorbed"] = p.AbsorbedTotal,
            ["last"] = k.LastSkillId, ["anim"] = k.LastSkillAnim, ["targets"] = k.TargetPositions.Select(Pos).ToList(),
            ["direction"] = Pos(k.Direction), ["hold"] = k.Hold, ["recalcs"] = (double)p.Recalcs, ["last_dash"] = (double)k.LastDash,
            ["pbuffs"] = k.PassiveBuffs.Select(kv => (object?)L(kv.Key, (double)kv.Value.Ticks, kv.Value.Value)).ToList(),
            ["streak"] = L((double)k.Streak, (double)k.StreakTicks), ["family"] = k.HasLastFamily ? k.LastFamily : null,
            ["elements"] = k.Elements.Select(e => (object?)L(e.Element, (double)e.Time)).ToList(), ["bastion"] = (double)k.BastionTicks,
        };
        if (k.World is { } w)
            state["world"] = new PyDict
            {
                ["zones"] = w.Zones.Select(z => (object?)L(z.SkillId, Pos(z.Center), z.Radius, (double)z.Ticks, (double)z.Timer, (double)z.ShotTimer,
                                                            (double)z.Interval, (double)z.Touched.Count, z.Dps, z.Element)).ToList(),
                ["events"] = w.Events.Select(e => (object?)L(e.Kind, (double)e.Ticks, e.Kind == "blast" ? Pos(e.Center) : e.Kind == "recall" ? Pos(e.Origin) : null)).ToList(),
                ["effects"] = w.Effects.Select(e => (object?)L(e.Ctx.Spec.Str("id"), (double)e.Ticks, e.Absorbed)).ToList(),
                ["auras"] = w.Auras.Select(a => (object?)L(a.Id, (double)a.Row.Timer, (double)a.Row.UpkeepTimer)).ToList(),
                ["channel"] = w.Channel is { } c ? L(c.Ctx.Spec.Str("id"), (double)c.Timer, (double)c.UpkeepTimer, c.Action) : null,
                ["charges"] = Sorted(w.Charges, (id, v) => L(id, (double)v.Max, (double)v.Cooldown)),
                ["trail_timer"] = (double)w.TrailTimer,
            };
        return state;
    }

    private static PyDict EnemyState(Body e) => new()
    {
        ["health"] = (double)e.Health, ["x"] = (double)e.X, ["y"] = (double)e.Y, ["statuses"] = Rows(e), ["dead"] = e.V16Dead,
        ["pressure"] = (double)e.CcPressure, ["absorbed"] = e.AbsorbedTotal,
    };

    public static IEnumerable<object[]> Cases() =>
        Root.GetProperty("cases").EnumerateArray().Select((c, i) => new object[] { i, c.GetProperty("name").GetString()! });

    [Theory]
    [MemberData(nameof(Cases))]
    public void SameNumbersAsPython(int index, string name)
    {
        var @case = Root.GetProperty("cases")[index];
        var ranks = @case.GetProperty("ranks");
        SkillEnv.Hooks = ranks.ValueKind == JsonValueKind.Null ? new NeutralHooks() : new PassiveHooks();
        _ticks = 0;
        SkillEnv.Rng = new PyRandom(@case.GetProperty("seed").GetInt64());
        var scene = Build(@case.GetProperty("shield").GetBoolean());
        if (ranks.ValueKind != JsonValueKind.Null)
        {
            string cls = @case.GetProperty("class").GetString()!; var tree = new SkillTree(cls);
            foreach (var r in ranks.EnumerateObject())
                if (r.Value.GetInt32() != 0) { tree.Unlocked.Add("class:" + r.Name); tree.Ranks["class:" + r.Name] = r.Value.GetInt32(); }
            scene.Hero.PlayerClass = cls; scene.Hero.Tree = tree;
        }
        var specs = Root.GetProperty("specs");
        int step = 0;
        foreach (var s in @case.GetProperty("steps").EnumerateArray())
        {
            var op = s.GetProperty("op"); string where = $"{name}, passo {step} ({op})";
            if (s.TryGetProperty("error", out var error))
            {
                True(Record.Exception(() => Run(op, scene)) != null, $"{where}: o Python falhou ({error}) e o C# não");
                break;
            }
            var (result, spec) = Run(op, scene);
            if (s.TryGetProperty("spec", out var key)) SameValue(specs.GetProperty(key.GetString()!), spec, where + " habilidade resolvida");
            SameValue(s.GetProperty("result"), result, where + " retorno");
            SameValue(s.GetProperty("hero"), HeroState(scene.Hero, scene), where + " herói");
            foreach (var p in s.GetProperty("enemies").EnumerateObject()) SameValue(p.Value, EnemyState(scene.Find(p.Name)), $"{where} {p.Name}");
            step++;
        }
    }
}
