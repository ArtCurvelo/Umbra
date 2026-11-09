using System.Text;
using System.Text.Json;
using Umbra.Domain.Combat;

namespace Umbra.Domain.Tests;

/// <summary>
/// Phase 5 parity for resources.py: replays tools/parity/resources_parity.py -> tests/parity/resources_cases.json on the
/// C# port and checks every return value, every entity (life, resource, Fortificação, states) and the orbs on the floor
/// after every step. The hook values (item affixes, Grimório) were computed by UMBRA's real code for each entity.
/// </summary>
public class ParityResourcesTests
{
    private sealed class Fake : IHero
    {
        public string Name { get; init; } = "";
        public int Health { get; set; }
        public int MaxHealth { get; init; }
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
        public double Fortify { get; set; }
        public int FortifyTimer { get; set; }
        public double FortifyCarry { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Resource { get; set; }
        public int MaxResource { get; init; }
        public Dictionary<string, double> Hooks { get; set; } = new();

        public int GainResource(double amount, string source)
        {
            int gained = (int)PyMath.Round(amount); Resource = Math.Min(MaxResource, Resource + gained);
            return gained;
        }
    }

    static ParityResourcesTests()
    {
        var affix = Resources.AffixShare; var grimoire = Resources.GrimoireBonus;     // chained: other test classes keep theirs
        Resources.AffixShare = (e, key) => e is Fake f ? (f.Hooks.TryGetValue("affix_" + key, out var v) ? v : 0) : affix?.Invoke(e, key) ?? 0;
        Resources.GrimoireBonus = (e, key) => e is Fake f ? (f.Hooks.TryGetValue("grimoire_" + key, out var v) ? v : 0) : grimoire?.Invoke(e, key) ?? 0;
    }

    private static readonly JsonElement Root = JsonDocument.Parse(File.ReadAllText(
        Path.Combine(Path.GetDirectoryName(Umbra.Domain.Data.UmbraData.FindRoot())!, "..", "..", "tests", "parity", "resources_cases.json"),
        Encoding.UTF8)).RootElement;

    private static int Int(JsonElement e, string key, int fallback) => e.TryGetProperty(key, out var v) ? v.GetInt32() : fallback;

    private static Dictionary<string, Fake> World(JsonElement hooks)
    {
        var world = new Dictionary<string, Fake>();
        foreach (var p in Root.GetProperty("entities").EnumerateObject())
        {
            var s = p.Value; int health = s.GetProperty("health").GetInt32();
            world[p.Name] = new Fake
            {
                Name = p.Name, Health = health, MaxHealth = health, X = Int(s, "x", 0), Y = Int(s, "y", 0),
                Resource = Int(s, "resource", 0), MaxResource = Int(s, "max_resource", 100),
                Hooks = hooks.GetProperty(p.Name).EnumerateObject().ToDictionary(h => h.Name, h => h.Value.GetDouble()),
            };
        }
        return world;
    }

    private static object? Run(JsonElement op, Dictionary<string, Fake> world, List<BloodOrb> orbs)
    {
        Fake? t = op.TryGetProperty("t", out var tn) ? world[tn.GetString()!] : null;
        double D(string key) => op.GetProperty(key).GetDouble();
        int N() => op.GetProperty("n").GetInt32();
        switch (op.GetProperty("op").GetString())
        {
            case "barrier": return Resources.AddBarrier(t!, D("seconds"), D("points"));
            case "apply": return Status.Apply(t!, op.GetProperty("kind").GetString()!, D("ticks"), D("mag"));
            case "absorb": return Status.Absorb(t!, D("amount"));
            case "stick": { int dealt = 0; for (int i = 0; i < N(); i++) dealt += Status.TickOnce(t!); return dealt; }
            case "fortify": return Resources.Fortify(t!, D("points"));
            case "rtick": for (int i = 0; i < N(); i++) Resources.Tick(t!); return null;
            case "stack":
                var limit = op.TryGetProperty("limit", out var l) && l.ValueKind == JsonValueKind.Number ? l.GetInt32() : (int?)null;
                return Resources.AddStack(t!, op.GetProperty("kind").GetString()!, op.GetProperty("amount").GetInt32(), limit);
            case "hurt": t!.TakeDamage(op.GetProperty("amount").GetInt32()); return null;
            case "move": t!.X = op.GetProperty("x").GetInt32(); t.Y = op.GetProperty("y").GetInt32(); return null;
            case "drop":
                var pos = op.GetProperty("pos").EnumerateArray().Select(v => v.GetDouble()).ToArray();
                Resources.DropOrb(orbs, pos[0], pos[1], D("heal"), D("resource"), D("seconds"), D("fortify")); return null;
            case "orbs":
                var heroes = new[] { world["heroi"], world["aliado"] };
                for (int i = 0; i < N(); i++) Resources.TickOrbs(orbs, heroes);
                return null;
            case "query":
                return new double[]
                {
                    Resources.BarrierTotal(t!), Resources.Fortification(t!), Resources.StackCount(t!, "ferocity"),
                    Resources.StackCount(t!, "determination"), Resources.StackCount(t!, "brutal"), Resources.AttackSpeed(t!), Resources.Armour(t!),
                };
        }
        throw new InvalidOperationException();
    }

    private static void Same(double expected, double actual, string where)
    {
        if (Math.Abs(expected - actual) > 1e-9 * Math.Max(1, Math.Abs(expected))) throw new Xunit.Sdk.XunitException($"{where}: esperado {expected}, obtido {actual}");
    }

    private static void SameState(JsonElement x, Fake e, string where)
    {
        Same(x.GetProperty("health").GetInt32(), e.Health, where + " vida");
        Same(x.GetProperty("resource").GetDouble(), e.Resource, where + " recurso");
        Same(x.GetProperty("x").GetInt32(), e.X, where + " x"); Same(x.GetProperty("y").GetInt32(), e.Y, where + " y");
        Same(x.GetProperty("fortify").GetDouble(), e.Fortify, where + " fortificação");
        Same(x.GetProperty("fortify_timer").GetInt32(), e.FortifyTimer, where + " temporizador da fortificação");
        Same(x.GetProperty("fortify_carry").GetDouble(), e.FortifyCarry, where + " resto da fortificação");
        Same(x.GetProperty("absorbed").GetDouble(), e.AbsorbedTotal, where + " absorvido");
        var kinds = x.GetProperty("statuses").EnumerateArray().ToList();
        var actualKinds = e.Statuses?.Kinds.ToList() ?? new List<string>();
        Assert.True(kinds.Select(k => k[0].GetString()).SequenceEqual(actualKinds),
                    $"{where} estados: esperado [{string.Join(",", kinds.Select(k => k[0].GetString()))}], obtido [{string.Join(",", actualKinds)}]");
        foreach (var k in kinds)
        {
            string kind = k[0].GetString()!;
            var rows = k[1].EnumerateArray().ToList(); var actual = e.Statuses!.Get(kind)!;
            Assert.True(rows.Count == actual.Count, $"{where} {kind}: {rows.Count} linhas esperadas, {actual.Count} obtidas");
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i].EnumerateArray().ToList(); var a = actual[i];
                Same(r[0].GetDouble(), a.Ticks, $"{where} {kind}[{i}] ticks");
                Same(r[1].GetDouble(), a.Magnitude, $"{where} {kind}[{i}] magnitude");
                Assert.True(r[2].ValueKind == JsonValueKind.Null ? a.Timer == null : a.Timer == r[2].GetInt32(), $"{where} {kind}[{i}] timer");
                Same(r[3].GetDouble(), a.Carry, $"{where} {kind}[{i}] resto");
            }
        }
    }

    private static void SameOrbs(JsonElement x, List<BloodOrb> orbs, string where)
    {
        var rows = x.EnumerateArray().ToList();
        Assert.True(rows.Count == orbs.Count, $"{where}: {rows.Count} orbes esperados, {orbs.Count} obtidos");
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i].EnumerateArray().ToList(); var o = orbs[i]; var pos = r[0].EnumerateArray().ToList();
            Same(pos[0].GetInt32(), o.X, $"{where} orbe {i} x"); Same(pos[1].GetInt32(), o.Y, $"{where} orbe {i} y");
            Same(r[1].GetInt32(), o.Ticks, $"{where} orbe {i} ticks"); Same(r[2].GetDouble(), o.Heal, $"{where} orbe {i} cura");
            Same(r[3].GetDouble(), o.Resource, $"{where} orbe {i} recurso"); Same(r[4].GetDouble(), o.Fortify, $"{where} orbe {i} fortificação");
        }
    }

    public static IEnumerable<object[]> Cases() =>
        Root.GetProperty("cases").EnumerateArray().Select((c, i) => new object[] { i, c.GetProperty("name").GetString()! });

    [Theory]
    [MemberData(nameof(Cases))]
    public void SameNumbersAsPython(int index, string name)
    {
        var @case = Root.GetProperty("cases")[index];
        var world = World(@case.GetProperty("hooks"));
        var orbs = new List<BloodOrb>();
        int step = 0;
        foreach (var s in @case.GetProperty("steps").EnumerateArray())
        {
            string where = $"{name}, passo {step} ({s.GetProperty("op")})";
            var result = Run(s.GetProperty("op"), world, orbs);
            var expected = s.GetProperty("result");
            switch (result)
            {
                case null: Assert.True(expected.ValueKind == JsonValueKind.Null, where + ": esperado " + expected); break;
                case double[] q:
                    var e = expected.EnumerateArray().ToList();
                    for (int k = 0; k < q.Length; k++) Same(e[k].GetDouble(), q[k], $"{where}[{k}]");
                    break;
                default: Same(expected.GetDouble(), Convert.ToDouble(result), where); break;
            }
            foreach (var p in s.GetProperty("state").EnumerateObject()) SameState(p.Value, world[p.Name], where + " / " + p.Name);
            SameOrbs(s.GetProperty("orbs"), orbs, where);
            step++;
        }
    }

    [Fact]
    public void ConstantsMatchTheExportedRules()
    {
        var data = Umbra.Domain.Data.UmbraData.Load(Umbra.Domain.Data.UmbraData.FindRoot()).Rules.GetProperty("resources");
        Assert.Equal(data.GetProperty("FORTIFY_HEAL").GetDouble(), Resources.FortifyHeal);
        Assert.Equal(data.GetProperty("FORTIFY_SECONDS").GetInt32(), Resources.FortifySeconds);
        Assert.Equal(data.GetProperty("ORB_SECONDS").GetDouble(), Resources.OrbSeconds);
        Assert.Equal(data.GetProperty("ORB_HEAL").GetDouble(), Resources.OrbHeal);
        Assert.Equal(data.GetProperty("ORB_RESOURCE").GetDouble(), Resources.OrbResource);
        Assert.Equal(data.GetProperty("ORB_LIMIT").GetInt32(), Resources.OrbLimit);
        var stacks = data.GetProperty("STACKS").EnumerateObject().ToList();
        Assert.Equal(stacks.Select(p => p.Name), Resources.Stacks.Keys);
        foreach (var p in stacks)
        {
            var v = p.Value.EnumerateArray().ToList();
            Assert.Equal((v[0].GetInt32(), v[1].GetInt32(), v[2].GetDouble()), Resources.Stacks[p.Name]);
        }
    }
}
