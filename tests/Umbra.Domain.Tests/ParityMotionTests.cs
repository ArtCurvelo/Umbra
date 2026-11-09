using System.Text;
using System.Text.Json;
using Umbra.Domain.Combat;
using Umbra.Domain.Combat.V16;
using Umbra.Domain.Data;
using Umbra.Domain.Heroes;
using Umbra.Domain.Skills;

namespace Umbra.Domain.Tests;

/// <summary>Phase 5.8c parity: the hero's continuous movement replays tools/parity/motion_parity.py -> tests/parity/motion_cases.json.</summary>
[Collection("SkillEnv")]
public class ParityMotionTests
{
    private sealed class Map : IMap
    {
        public required int[,] Cells;
        public int Width => Cells.GetLength(1);
        public int Height => Cells.GetLength(0);
        public int Cell(int x, int y) => Cells[y, x];
        public bool Sanctuary(int x, int y) => false;
    }

    private sealed class Blocker : ITarget
    {
        public string Name => "bloqueio";
        public int X { get; set; }
        public int Y { get; set; }
        public bool Alive => true;
        public bool Boss => false;
        public int Health => 1;
        public int MaxHealth => 1;
        public void TakeDamage(int amount) { }
        public void Heal(int amount) { }
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
        public (int X, int Y) AttackDirection => (0, 0);
        public bool V16Dead { get; set; }
    }

    private static readonly JsonElement Root;

    static ParityMotionTests()
    {
        HeroTables.Load(UmbraData.FindRoot());
        Root = JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(UmbraData.FindRoot())!, "..", "..", "tests", "parity", "motion_cases.json"),
                                                    Encoding.UTF8)).RootElement;
    }

    private static List<object?> L(params object?[] items) => items.ToList();

    public static IEnumerable<object[]> Cases() =>
        Root.GetProperty("cases").EnumerateArray().Select((c, i) => new object[] { i, c.GetProperty("name").GetString()! });

    [Theory]
    [MemberData(nameof(Cases))]
    public void SameMovementAsPython(int index, string name)
    {
        var @case = Root.GetProperty("cases")[index];
        var rows = Root.GetProperty("map").EnumerateArray().Select(r => r.GetString()!).ToList();
        var cells = new int[rows.Count, rows[0].Length];
        for (int y = 0; y < rows.Count; y++) for (int x = 0; x < rows[0].Length; x++) cells[y, x] = rows[y][x] == '#' ? 1 : 0;
        var map = new Map { Cells = cells };
        SkillEnv.Hooks = new HeroSkillHooks();
        var hero = new Hero((2, 2), "barbaro") { ActiveMap = map };
        hero.ActiveEnemies = @case.GetProperty("blockers").EnumerateArray().Select(b => (ITarget)new Blocker { X = b[0].GetInt32(), Y = b[1].GetInt32() }).ToList();
        Locomotion.Sync(hero, map);
        int step = 0;
        foreach (var s in @case.GetProperty("steps").EnumerateArray())
        {
            var op = s.GetProperty("op");
            switch (op.GetProperty("op").GetString())
            {
                case "move":
                    var v = op.GetProperty("v");
                    for (int i = 0; i < op.GetProperty("n").GetInt32(); i++) Locomotion.Advance(hero, map, (v[0].GetDouble(), v[1].GetDouble()));
                    break;
                case "state": Status.Apply(hero, op.GetProperty("kind").GetString()!, op.GetProperty("ticks").GetDouble(), op.GetProperty("mag").GetDouble()); break;
                case "tick": for (int i = 0; i < op.GetProperty("n").GetInt32(); i++) Status.TickOnce(hero); break;
            }
            var m = hero.Motion!;
            var state = new PyDict
            {
                ["pos"] = L(m.Position.X, m.Position.Y), ["vel"] = L(m.Velocity.X, m.Velocity.Y), ["dist"] = m.Distance,
                ["cell"] = L((double)hero.X, (double)hero.Y), ["previous"] = L((double)hero.PreviousPosition.X, (double)hero.PreviousPosition.Y),
                ["facing"] = L((double)hero.AttackDirection.X, (double)hero.AttackDirection.Y), ["moving"] = hero.IsMoving,
            };
            Compare.SameValue(s.GetProperty("state"), state, $"{name}, passo {step} ({op})");
            step++;
        }
    }
}
