using Umbra.Domain.Combat;

namespace Umbra.Domain.Skills;

/// <summary>
/// Resolved skill = catalog row + rank + the options bought in each branch — port of game/skills/v16/tuning.py and the
/// rank scaling of game/skills/ranks.py. Options apply in branch order (Comportamento 0–2, Especialização 3–4,
/// Transformação 5–6), so a Transformação can override what came before. Which options are bought and active is the
/// tree's business (step 5.5); here they arrive as indices.
/// </summary>
public static class Tuning
{
    public static readonly string[] ModifierKeys = { "power", "reach", "flow" };
    public static readonly IReadOnlyDictionary<string, double> Defaults = new Dictionary<string, double>
    {
        ["summon_health"] = 1, ["summon_damage"] = 1, ["recovery"] = 1, ["duration"] = 0, ["count"] = 1, ["targets"] = 1,
    };
    public static readonly string[] Integer = { "cost", "cooldown", "range", "radius", "resource_gain", "targets", "count", "pierce",
                                                "ricochet", "charges", "duration", "distance", "length", "width" };
    public const int FullSteps = 4;
    public const double LateStep = .5;

    public static List<string> OptionKeys(string skillId)
    {
        string root = "learn:" + skillId;
        return ModifierKeys.Select(k => $"{root}:mod:{k}").Concat(Enumerable.Range(0, 4).Select(i => $"{root}:passive:{i}")).ToList();
    }

    /// <summary>ranks.steps: effective rank steps above rank 1 (ranks 6..15 count half a step, PROVISÓRIO in UMBRA).</summary>
    public static double Steps(int level)
    {
        int extra = Math.Max(0, level - 1);
        return Math.Min(extra, FullSteps) + LateStep * Math.Max(0, extra - FullSteps);
    }

    public static PyDict Apply(PyDict row, PyDict ops)
    {
        foreach (var (key, factor) in ops.Sub("mul") ?? new PyDict())
            row[key] = (row.TryGetValue(key, out var v) ? Py.Num(v) : Defaults.GetValueOrDefault(key, 0)) * Py.Num(factor);
        foreach (var (key, value) in ops.Sub("add") ?? new PyDict())
            row[key] = (row.TryGetValue(key, out var v) ? Py.Num(v) : Defaults.GetValueOrDefault(key, 0)) + Py.Num(value);
        foreach (var (key, value) in ops.Sub("set") ?? new PyDict()) row[key] = Py.DeepCopy(value);
        row["riders"] = row.List("riders").Concat((List<object?>)Py.DeepCopy(ops.List("riders"))!).ToList();
        row["bonus"] = row.List("bonus").Concat((List<object?>)Py.DeepCopy(ops.List("bonus"))!).ToList();
        return row;
    }

    /// <summary>tuning.resolved. <paramref name="picks"/>: indices of the bought, active options (any order).</summary>
    public static PyDict Resolve(PyDict row, int rank, IEnumerable<int> picks)
    {
        var result = new PyDict(row.Where(kv => kv.Key != "options").Select(kv => new KeyValuePair<string, object?>(kv.Key, Py.DeepCopy(kv.Value))));
        result["rank"] = (double)rank; double steps = Steps(rank);
        result["multiplier"] = result.Num("multiplier") * (1 + .12 * steps);
        if (result.T("cooldown")) result["cooldown"] = result.Num("cooldown") * Math.Pow(.96, steps);
        if (result.Str("shape") == "summon")
        {
            result["summon_health"] = result.Num("summon_health", 1) * (1 + .05 * steps);
            result["summon_damage"] = result.Num("summon_damage", 1) * (1 + .08 * steps);
        }
        var chosen = new List<object?>(); result["chosen"] = chosen;
        var options = row.List("options");
        foreach (int index in picks.Distinct().OrderBy(i => i))
        {
            var picked = (PyDict)options[index]!;
            Apply(result, picked.Sub("ops")!); chosen.Add(picked.Str("name"));
        }
        foreach (var key in Integer)
            if (result.TryGetValue(key, out var v) && v is double or long or bool) result[key] = (double)Math.Max(0, PyMath.Round(Py.Num(v)));
        if (result.Str("category") == "Básica") result["cost"] = 0.0;
        return result;
    }
}
