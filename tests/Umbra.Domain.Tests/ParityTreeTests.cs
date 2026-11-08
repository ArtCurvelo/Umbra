using System.Text;
using System.Text.Json;
using Umbra.Domain.Combat;
using Umbra.Domain.Combat.V16;
using Umbra.Domain.Data;
using Umbra.Domain.Skills;

namespace Umbra.Domain.Tests;

/// <summary>
/// Phase 5.5 parity: tree, ranks, loadout and class-passive values replay tools/parity/tree_parity.py ->
/// tests/parity/tree_cases.json, recorded on real heroes of UMBRA's composed engine. Life and max life come from the
/// hero's own stat rules (step 5.8), so the recorded values are fed in before the factors are compared.
/// </summary>
public class ParityTreeTests
{
    internal sealed class TreeHero : ITreeOwner, ISkillUser
    {
        public TreeHero(string cls) { PlayerClass = cls; Tree = new SkillTree(cls) { }; Tree.Owner = this; }
        public string PlayerClass { get; }
        public SkillTree Tree { get; }
        SkillTree? ISkillUser.Tree => Tree;
        public int Level { get; set; } = 1;
        public string? BasicSkillId { get; set; }
        public List<string?> EquippedMainSkills { get; } = new() { null, null, null, null };
        public HashSet<string> UnlockedMainSkills { get; } = new();
        public int ActiveMainSlot { get; set; }
        public void RefreshStats() { }
        public void CancelAttack(string reason) { }
        public string Name => "heroi";
        public int Health { get; set; } = 100;
        public int MaxHealth { get; set; } = 100;
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
        public string ThornsElement => ThornsChoice ?? "physical";
        public double Fortify { get; set; }
        public int FortifyTimer { get; set; }
        public double FortifyCarry { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public (int X, int Y) AttackDirection { get; set; } = (1, 0);
        public bool V16Dead { get; set; }
        public int Resource { get; set; }
        public double Attack => 10;
        public (int X, int Y)? AimTarget => null;
        public string? ThornsChoice { get; set; }
        public int DashCooldown => 0;
        public int WardBlocked => 0;
        public bool HasShield => false;
        public void RecalculateStats() { }
        public SkillState Skills { get; } = new();
        public bool SpendResource(int amount) => true;
        public int GainResource(double amount, string source) => 0;
        public double GetAttackDamage(ITarget? enemy, bool skill) => Attack;
    }

    private static readonly JsonElement Root;
    private static readonly UmbraData Data;

    static ParityTreeTests()
    {
        Data = UmbraData.Load(UmbraData.FindRoot());
        TreeCatalog.Load(UmbraData.FindRoot()); Passives.Use(Data);
        Root = JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(UmbraData.FindRoot())!, "..", "..", "tests", "parity", "tree_cases.json"),
                                                    Encoding.UTF8)).RootElement;
    }

    private static object? Run(JsonElement op, TreeHero p)
    {
        string? S(string k) => op.TryGetProperty(k, out var v) ? v.GetString() : null;
        switch (op.GetProperty("op").GetString())
        {
            case "learn": return TreeRules.Learn(p, S("key")!);
            case "refund": { var (ok, msg) = TreeRules.Refund(p, S("key")!); return new List<object?> { ok, msg }; }
            case "choose": return p.Tree.ChoosePassive(S("key")!);
            case "equip": { var (ok, msg) = TreeRules.Equip(p, S("skill")!, op.GetProperty("index").GetInt32()); return new List<object?> { ok, msg }; }
            case "unequip": TreeRules.Unequip(p, op.GetProperty("index").GetInt32()); return null;
            case "points": p.Tree.AddPoint(op.GetProperty("n").GetInt32()); return null;
            case "level": p.Level = op.GetProperty("n").GetInt32(); return null;
            case "health": return null;                                   // the life itself is fed from the recording
            case "state": Status.Apply(p, S("kind")!, 600, op.GetProperty("mag").GetDouble()); return null;
        }
        throw new InvalidOperationException();
    }

    private static List<object?> L(IEnumerable<object?> items) => items.ToList();
    private static List<object?> Pairs(IEnumerable<KeyValuePair<string, int>> rows) =>
        L(rows.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => (object?)L(new object?[] { kv.Key, (double)kv.Value })));

    private static PyDict Snap(TreeHero p, IReadOnlyList<string> effects, IReadOnlyList<string> passiveEffects)
    {
        var t = p.Tree;
        var learned = t.Catalog.Order.Where(k => t.Has(k) && t.Nodes[k].Str("kind") == "root").Select(k => t.Nodes[k].Str("skill_id")).ToList();
        var chain = Passives.ChainBonus(p);
        return new PyDict
        {
            ["points"] = (double)t.Points, ["unlocked"] = L(t.Unlocked.OrderBy(k => k, StringComparer.Ordinal)),
            ["investments"] = Pairs(t.Investments), ["ranks"] = Pairs(t.Ranks),
            ["slots"] = L(t.PassiveSlots.Select(kv => (object?)L(new object?[] { kv.Key, kv.Value }))),
            ["basic"] = p.BasicSkillId, ["main"] = L(p.EquippedMainSkills), ["unlocked_main"] = L(p.UnlockedMainSkills.OrderBy(k => k, StringComparer.Ordinal)),
            ["active_slot"] = (double)p.ActiveMainSlot, ["active_skills"] = L(t.ActiveSkills()),
            ["effects"] = L(effects.Select(e => (object?)t.GetEffect(e))),
            ["chosen"] = L(learned.Select(sid => (object?)L(new object?[]
                { sid, L(TreeRules.Chosen(t, sid).Select(i => (object?)(double)i)), t.Equipped("learn:" + sid) }))),
            ["passives"] = L(passiveEffects.Select(e => (object?)Passives.Value(p, e)).Concat(passiveEffects.Select(e => (object?)Passives.Owned(p, e)))),
            ["factors"] = L(new[] { "slow", "root", "poison", "stun" }.Select(k => (object?)Passives.DurationFactor(p, k))
                .Concat(new[] { "burn", "decay", "bleed" }.Select(k => (object?)Passives.DotFactor(p, k)))
                .Concat(new object?[] { Passives.HealFactor(p), Passives.BarrierFactor(p), Passives.HealthCostFactor(p), Passives.ResourceGainFactor(p),
                                        Passives.RegenFactor(p), Passives.IncomingFactor(p, "physical"), Passives.IncomingFactor(p, "fire"),
                                        (double)chain.Jumps, chain.PerJump })),
        };
    }

    public static IEnumerable<object[]> Cases() =>
        Root.GetProperty("cases").EnumerateArray().Select((c, i) => new object[] { i, c.GetProperty("name").GetString()! });

    [Theory]
    [MemberData(nameof(Cases))]
    public void SameNumbersAsPython(int index, string name)
    {
        var @case = Root.GetProperty("cases")[index];
        var effects = Root.GetProperty("effects").EnumerateArray().Select(e => e.GetString()!).ToList();
        var passiveEffects = Root.GetProperty("passive_effects").EnumerateArray().Select(e => e.GetString()!).ToList();
        var hero = new TreeHero(@case.GetProperty("class").GetString()!);
        int step = 0;
        foreach (var s in @case.GetProperty("steps").EnumerateArray())
        {
            string where = $"{name}, passo {step} ({s.GetProperty("op")})";
            var result = Run(s.GetProperty("op"), hero);
            var life = s.GetProperty("health"); hero.Health = life[0].GetInt32(); hero.MaxHealth = life[1].GetInt32();
            Compare.SameValue(s.GetProperty("result"), result, where + " retorno");
            Compare.SameValue(s.GetProperty("state"), Snap(hero, effects, passiveEffects), where);
            step++;
        }
    }

    [Fact]
    public void TreesMatchTheGame()
    {
        foreach (var cls in new[] { "barbaro", "mago", "ladino", "paladino", "necromante", "bruxo", "druida" })
        {
            var tree = new SkillTree(cls);
            Assert.Equal(1, tree.Points);
            Assert.Equal(6, tree.Nodes.Values.Count(n => n.Str("kind") == "class_passive"));
            Assert.Equal(Data.Skills[cls].Skills.Count + Data.Skills[cls].Extra.Count, tree.Nodes.Values.Count(n => n.Str("kind") == "root"));
        }
    }
}

/// <summary>Deep comparison of a recorded JSON value with a PyDict/list/number/bool/string built in C#.</summary>
internal static class Compare
{
    public static void Same(double expected, double actual, string where)
    {
        if (Math.Abs(expected - actual) > 1e-9 * Math.Max(1, Math.Abs(expected))) throw new Xunit.Sdk.XunitException($"{where}: esperado {expected}, obtido {actual}");
    }

    public static void True(bool ok, string message) { if (!ok) throw new Xunit.Sdk.XunitException(message); }

    public static void SameValue(JsonElement expected, object? actual, string where)
    {
        switch (expected.ValueKind)
        {
            case JsonValueKind.Null: True(actual == null, $"{where}: esperado null, obtido {actual}"); break;
            case JsonValueKind.True or JsonValueKind.False:
                True(actual is bool b && b == expected.GetBoolean(), $"{where}: esperado {expected}, obtido {actual}"); break;
            case JsonValueKind.Number: True(actual is double or int or long, $"{where}: esperado número {expected}, obtido {actual ?? "null"}"); Same(expected.GetDouble(), Py.Num(actual), where); break;
            case JsonValueKind.String: True(actual as string == expected.GetString(), $"{where}: esperado {expected}, obtido {actual}"); break;
            case JsonValueKind.Array:
            {
                var list = actual as List<object?>; var rows = expected.EnumerateArray().ToList();
                True(list != null && list.Count == rows.Count, $"{where}: lista {expected} vs [{(list == null ? "null" : string.Join(",", list))}]");
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
}
