using System.Globalization;

namespace Umbra.Domain.Combat;

/// <summary>A worn item as typed damage sees it (UMBRA records: "resistances" and "element_bonus" per element).</summary>
public sealed record GearRecord(IReadOnlyDictionary<string, double> Resistances, IReadOnlyDictionary<string, double> ElementBonus);

/// <summary>Anything that can carry combat states and take typed damage (hero, enemy, minion). Port of the attributes
/// UMBRA's status.py/elements.py read with getattr; defaults match the Python ones.</summary>
public interface ICombatant
{
    string Name { get; }
    bool Alive { get; }
    bool Boss { get; }
    int Health { get; }
    int MaxHealth { get; }
    void TakeDamage(int amount);
    StatusMap? Statuses { get; set; }
    int CcPressure { get; set; }
    int CcPressureTimer { get; set; }
    double AbsorbedTotal { get; set; }
    /// <summary>Null when the entity has no wind-up (stun only interrupts swings that exist).</summary>
    int? AttackWindup { get; set; }
    IReadOnlyDictionary<string, double>? Resistances { get; }
    string? EnemyType { get; }
    IReadOnlyList<GearRecord> Gear { get; }
    int NecroVulnerableTicks { get; }
    string ThornsElement { get; }
    void Heal(int amount);
}

public sealed class StatusRow
{
    public int Ticks;
    public double Magnitude;
    public ICombatant? Source;
    /// <summary>Damage-over-time and restored rows have a timer; plain rows only get one when they regenerate.</summary>
    public int? Timer;
    public double Carry;
}

/// <summary>
/// The states of one entity, in insertion order (Python dict semantics: removing a kind and adding it again puts it at
/// the end). The order matters: damage over time is dealt kind by kind in this order.
/// </summary>
public sealed class StatusMap
{
    private readonly List<string> _order = new();
    private readonly Dictionary<string, List<StatusRow>> _rows = new();

    public int Count => _order.Count;
    public IReadOnlyList<string> Kinds => _order;
    public List<StatusRow>? Get(string kind) => _rows.TryGetValue(kind, out var r) ? r : null;

    public List<StatusRow> GetOrAdd(string kind)
    {
        if (_rows.TryGetValue(kind, out var rows)) return rows;
        rows = new List<StatusRow>();
        _rows[kind] = rows; _order.Add(kind);
        return rows;
    }

    public void Remove(string kind) { if (_rows.Remove(kind)) _order.Remove(kind); }
    public void Clear() { _rows.Clear(); _order.Clear(); }
}

public static class PyMath
{
    /// <summary>Python's round(x) for floats: to the nearest integer, ties to even.</summary>
    public static long Round(double x) => (long)Math.Round(x, MidpointRounding.ToEven);

    /// <summary>Python's round(x, n): decided on the exact binary value of x, ties to even.</summary>
    public static double Round(double x, int digits)
    {
        if (!double.IsFinite(x)) return x;
        var exact = decimal.Parse(x.ToString("E28", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture);
        return (double)Math.Round(exact, digits, MidpointRounding.ToEven);
    }
}
