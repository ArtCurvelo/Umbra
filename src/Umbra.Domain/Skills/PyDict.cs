using System.Globalization;
using System.Text.Json;

namespace Umbra.Domain.Skills;

/// <summary>
/// A skill, an option or a rider as plain data — the UMBRA catalogs are Python dicts with open keys, and options
/// change any key (mul/add/set), so the port keeps them as an ordered dictionary. Values: double, bool, string, null,
/// List&lt;object?&gt; and PyDict. The accessors follow Python's dict.get and truthiness.
/// </summary>
public sealed class PyDict : Dictionary<string, object?>
{
    public PyDict() { }
    public PyDict(IEnumerable<KeyValuePair<string, object?>> rows) { foreach (var (k, v) in rows) this[k] = v; }

    public object? Get(string key) => TryGetValue(key, out var v) ? v : null;

    /// <summary>Python truthiness of spec.get(key).</summary>
    public bool T(string key) => Py.Truthy(Get(key));

    public double Num(string key, double fallback = 0)
    {
        if (!TryGetValue(key, out var v)) return fallback;
        return Py.Num(v ?? throw new InvalidOperationException($"{key} is None"));
    }

    public string Str(string key, string fallback = "") => TryGetValue(key, out var v) && v is string s ? s : fallback;
    public string? StrOrNull(string key) => Get(key) as string;
    public List<object?> List(string key) => Get(key) as List<object?> ?? new List<object?>();
    public IEnumerable<PyDict> Rows(string key) => List(key).Cast<PyDict>();
    public PyDict? Sub(string key) => Get(key) as PyDict;

    public PyDict Copy() => (PyDict)Py.DeepCopy(this)!;
}

public static class Py
{
    public static bool Truthy(object? v) => v switch
    {
        null => false, bool b => b, double d => d != 0, int i => i != 0, string s => s.Length > 0,
        List<object?> l => l.Count > 0, PyDict d => d.Count > 0, _ => true,
    };

    public static double Num(object? v) => v switch
    {
        double d => d, int i => i, long l => l, bool b => b ? 1 : 0,
        _ => throw new InvalidOperationException("not a number: " + v),
    };

    /// <summary>Python int(x) for a number: truncates towards zero.</summary>
    public static int Int(object? v) => (int)Math.Truncate(Num(v));

    public static double Float(string text) => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);

    /// <summary>Python min(items, key=...): the first item with the smallest key.</summary>
    public static T MinBy<T, TKey>(IEnumerable<T> items, Func<T, TKey> key) where TKey : IComparable<TKey>
    {
        bool any = false; T best = default!; TKey bestKey = default!;
        foreach (var item in items)
        {
            var k = key(item);
            if (!any || k.CompareTo(bestKey) < 0) { best = item; bestKey = k; any = true; }
        }
        if (!any) throw new InvalidOperationException("min() arg is an empty sequence");
        return best;
    }

    /// <summary>Python list[:stop] (a negative stop counts from the end).</summary>
    public static List<T> Head<T>(IReadOnlyList<T> items, int stop)
    {
        if (stop < 0) stop = Math.Max(0, items.Count + stop);
        return items.Take(stop).ToList();
    }

    public static object? DeepCopy(object? v) => v switch
    {
        PyDict d => new PyDict(d.Select(kv => new KeyValuePair<string, object?>(kv.Key, DeepCopy(kv.Value)))),
        List<object?> l => l.Select(DeepCopy).ToList(),
        _ => v,
    };

    public static object? FromJson(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Object => new PyDict(e.EnumerateObject().Select(p => new KeyValuePair<string, object?>(p.Name, FromJson(p.Value)))),
        JsonValueKind.Array => e.EnumerateArray().Select(FromJson).ToList(),
        JsonValueKind.Number => e.GetDouble(),
        JsonValueKind.String => e.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };
}
