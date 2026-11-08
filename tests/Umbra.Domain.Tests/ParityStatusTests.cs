using System.Text;
using System.Text.Json;
using Umbra.Domain.Combat;

namespace Umbra.Domain.Tests;

/// <summary>
/// Phase 5 parity: the C# port of status.py/elements.py replays the operation sequences recorded from UMBRA's real Python
/// code (tools/parity/status_parity.py -> tests/parity/status_cases.json) and must reach the same return values and the
/// same full state of every entity after every step.
/// </summary>
public class ParityStatusTests
{
    private sealed class Fake : ICombatant
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
        public IReadOnlyDictionary<string, double>? Resistances { get; init; }
        public string? EnemyType { get; init; }
        public IReadOnlyList<GearRecord> Gear { get; init; } = Array.Empty<GearRecord>();
        public int NecroVulnerableTicks => 0;
        public string ThornsElement { get; init; } = "physical";
    }

    private static readonly JsonElement Root = JsonDocument.Parse(File.ReadAllText(
        Path.Combine(Path.GetDirectoryName(UmbraDataRoot())!, "..", "..", "tests", "parity", "status_cases.json"), Encoding.UTF8)).RootElement;

    private static string UmbraDataRoot() => Umbra.Domain.Data.UmbraData.FindRoot();

    private static Dictionary<string, double> Dict(JsonElement e) => e.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetDouble());

    private static Dictionary<string, Fake> World()
    {
        var world = new Dictionary<string, Fake>();
        foreach (var p in Root.GetProperty("entities").EnumerateObject())
        {
            var s = p.Value;
            int health = s.GetProperty("health").GetInt32();
            var gear = s.TryGetProperty("gear", out var g)
                ? g.EnumerateArray().Select(x => new GearRecord(
                    x.TryGetProperty("resistances", out var r) ? Dict(r) : new Dictionary<string, double>(),
                    x.TryGetProperty("element_bonus", out var b) ? Dict(b) : new Dictionary<string, double>())).ToList()
                : new List<GearRecord>();
            world[p.Name] = new Fake
            {
                Name = p.Name, Health = health, MaxHealth = health,
                Boss = s.TryGetProperty("boss", out var boss) && boss.GetBoolean(),
                EnemyType = s.TryGetProperty("enemy_type", out var et) ? et.GetString() : null,
                Resistances = s.TryGetProperty("resistances", out var res) ? Dict(res) : null,
                Gear = gear,
                AttackWindup = s.TryGetProperty("windup", out var w) && w.GetBoolean() ? 25 : null,
                ThornsElement = s.TryGetProperty("thorns_element", out var te) ? te.GetString()! : "physical",
            };
        }
        return world;
    }

    private static string? Str(JsonElement op, string key) => op.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static object? Run(JsonElement op, Dictionary<string, Fake> world)
    {
        var t = world[op.GetProperty("t").GetString()!];
        var srcName = Str(op, "src");
        ICombatant? src = srcName != null ? world[srcName] : null;
        string? kind = Str(op, "kind");
        switch (op.GetProperty("op").GetString())
        {
            case "apply": return Status.Apply(t, kind!, op.GetProperty("ticks").GetDouble(), op.GetProperty("mag").GetDouble(), src);
            case "frost": return Status.AddFrost(t, op.GetProperty("amount").GetDouble());
            case "tick": { int dealt = 0; for (int i = 0; i < op.GetProperty("n").GetInt32(); i++) dealt += Status.TickOnce(t); return dealt; }
            case "absorb": return Status.Absorb(t, op.GetProperty("amount").GetDouble());
            case "deal": return Elements.Deal(t, op.GetProperty("amount").GetDouble(), kind!, src, Str(op, "id") ?? "");
            case "remove": Status.Remove(t, kind); return null;
            case "cleanse": Status.Cleanse(t); return null;
            case "snapshot":
                var data = Status.Snapshot(t);
                Status.Restore(t, data.Select(d => (d.Kind, (double)d.Ticks, d.Magnitude)));
                return data;
            case "query":
                return new object[]
                {
                    Status.Value(t, kind!), Status.Has(t, kind!), Status.MoveFactor(t), Status.IsHarmful(t),
                    Elements.Resistance(t, Elements.Names.ContainsKey(kind!) ? kind! : "physical"), Elements.Outgoing(t, "holy"), Elements.Vulnerability(t),
                };
        }
        throw new InvalidOperationException();
    }

    private static void Same(double expected, double actual, string where)
    {
        if (Math.Abs(expected - actual) > 1e-9 * Math.Max(1, Math.Abs(expected))) throw new Xunit.Sdk.XunitException($"{where}: esperado {expected}, obtido {actual}");
    }

    private static void SameResult(JsonElement expected, object? actual, string where)
    {
        switch (actual)
        {
            case null: Assert.True(expected.ValueKind == JsonValueKind.Null, where + ": esperado " + expected); break;
            case int i: Same(expected.GetDouble(), i, where); break;
            case double d: Same(expected.GetDouble(), d, where); break;
            case object[] q:
                var e = expected.EnumerateArray().ToList();
                for (int k = 0; k < q.Length; k++)
                {
                    if (q[k] is bool b) Assert.True(e[k].GetBoolean() == b, $"{where}[{k}]: esperado {e[k]}, obtido {b}");
                    else Same(e[k].GetDouble(), Convert.ToDouble(q[k]), $"{where}[{k}]");
                }
                break;
            case List<(string Kind, int Ticks, double Magnitude)> snap:
                var rows = expected.EnumerateArray().ToList();
                Assert.True(rows.Count == snap.Count, $"{where}: {rows.Count} estados salvos esperados, {snap.Count} obtidos");
                for (int k = 0; k < rows.Count; k++)
                {
                    var r = rows[k].EnumerateArray().ToList();
                    Assert.True(r[0].GetString() == snap[k].Kind, $"{where}: estado {k}");
                    Same(r[1].GetDouble(), snap[k].Ticks, where); Same(r[2].GetDouble(), snap[k].Magnitude, where);
                }
                break;
            default: throw new InvalidOperationException(actual.GetType().Name);
        }
    }

    private static void SameState(JsonElement expected, Fake e, string where)
    {
        Same(expected.GetProperty("health").GetInt32(), e.Health, where + " vida");
        Same(expected.GetProperty("pressure").GetInt32(), e.CcPressure, where + " pressão");
        Same(expected.GetProperty("pressure_timer").GetInt32(), e.CcPressureTimer, where + " temporizador da pressão");
        Same(expected.GetProperty("absorbed").GetDouble(), e.AbsorbedTotal, where + " absorvido");
        var windup = expected.GetProperty("windup");
        Assert.True(windup.ValueKind == JsonValueKind.Null ? e.AttackWindup == null : e.AttackWindup == windup.GetInt32(), where + " preparação");
        var kinds = expected.GetProperty("statuses").EnumerateArray().ToList();
        var map = e.Statuses;
        var actualKinds = map?.Kinds.ToList() ?? new List<string>();
        Assert.True(kinds.Select(k => k[0].GetString()).SequenceEqual(actualKinds),
                    $"{where} estados: esperado [{string.Join(",", kinds.Select(k => k[0].GetString()))}], obtido [{string.Join(",", actualKinds)}]");
        foreach (var k in kinds)
        {
            string kind = k[0].GetString()!;
            var rows = k[1].EnumerateArray().ToList(); var actual = map!.Get(kind)!;
            Assert.True(rows.Count == actual.Count, $"{where} {kind}: {rows.Count} linhas esperadas, {actual.Count} obtidas");
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i].EnumerateArray().ToList(); var a = actual[i];
                Same(r[0].GetDouble(), a.Ticks, $"{where} {kind}[{i}] ticks");
                Same(r[1].GetDouble(), a.Magnitude, $"{where} {kind}[{i}] magnitude");
                Assert.True(r[2].ValueKind == JsonValueKind.Null ? a.Timer == null : a.Timer == r[2].GetInt32(), $"{where} {kind}[{i}] timer");
                Same(r[3].GetDouble(), a.Carry, $"{where} {kind}[{i}] resto");
                Assert.True((r[4].ValueKind == JsonValueKind.Null ? null : r[4].GetString()) == a.Source?.Name, $"{where} {kind}[{i}] origem");
            }
        }
    }

    public static IEnumerable<object[]> Cases() =>
        Root.GetProperty("cases").EnumerateArray().Select((c, i) => new object[] { i, c.GetProperty("name").GetString()! });

    [Theory]
    [MemberData(nameof(Cases))]
    public void SameNumbersAsPython(int index, string name)
    {
        var @case = Root.GetProperty("cases")[index];
        var world = World();
        int step = 0;
        foreach (var s in @case.GetProperty("steps").EnumerateArray())
        {
            string where = $"{name}, passo {step} ({s.GetProperty("op")})";
            var result = Run(s.GetProperty("op"), world);
            SameResult(s.GetProperty("result"), result, where);
            foreach (var p in s.GetProperty("state").EnumerateObject()) SameState(p.Value, world[p.Name], where + " / " + p.Name);
            step++;
        }
    }

    [Fact]
    public void ConstantsMatchTheExportedRules()
    {
        var data = Umbra.Domain.Data.UmbraData.Load(UmbraDataRoot()).Rules.GetProperty("status");
        Assert.Equal(data.GetProperty("KINDS").EnumerateArray().Select(x => x.GetString()), Status.Kinds);
        Assert.Equal(data.GetProperty("SAVED").EnumerateArray().Select(x => x.GetString()), Status.Saved);
        Assert.Equal(data.GetProperty("HARMFUL").EnumerateArray().Select(x => x.GetString()), Status.Harmful);
        Assert.Equal(Dict(data.GetProperty("CAPS")).OrderBy(kv => kv.Key), Status.Caps.OrderBy(kv => kv.Key));
        Assert.Equal(data.GetProperty("TICK").GetInt32(), Status.Tick);
        Assert.Equal(data.GetProperty("MAX_STACKS").GetInt32(), Status.MaxStacks);
        Assert.Equal(data.GetProperty("MAX_TICKS").GetInt32(), Status.MaxTicks);
        Assert.Equal(data.GetProperty("PRESSURE_DECAY").GetInt32(), Status.PressureDecay);
        Assert.Equal(data.GetProperty("IMMUNE_AT").GetInt32(), Status.ImmuneAt);
        Assert.Equal(data.GetProperty("FREEZE_TICKS").GetInt32(), Status.FreezeTicks);
        var elements = Umbra.Domain.Data.UmbraData.Load(UmbraDataRoot()).Rules.GetProperty("elements");
        Assert.Equal(elements.GetProperty("NAMES").EnumerateObject().Select(p => (p.Name, p.Value.GetString())), Elements.Names.Select(kv => (kv.Key, (string?)kv.Value)));
        foreach (var p in elements.GetProperty("PROFILES").EnumerateObject()) Assert.Equal(Dict(p.Value).OrderBy(kv => kv.Key), Elements.Profiles[p.Name].OrderBy(kv => kv.Key));
    }
}
