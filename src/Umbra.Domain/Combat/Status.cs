namespace Umbra.Domain.Combat;

/// <summary>
/// Timed combat states shared by every entity — C# port of UMBRA's game/combat_system/status.py (0.15.0 + Reforge +
/// NECROMANCER REBORN), rule for rule. Durations are 60 Hz ticks. Parity with the Python module is checked by
/// tests/Umbra.Domain.Tests/ParityStatusTests.cs against cases recorded from the real Python code.
/// Not ported yet (needs the class systems, Phase 5 later steps): the Warlock's "Sacrificial" protection
/// (warlock.protected), which only exists on heroes of that class.
/// </summary>
public static class Status
{
    public static readonly IReadOnlyDictionary<string, string> Dot = new Dictionary<string, string>
    {
        ["burn"] = "fire", ["bleed"] = "physical", ["poison"] = "poison", ["decay"] = "shadow", ["corruption"] = "shadow",
    };
    public static readonly string[] Control = { "stun", "root", "slow", "taunt", "blind", "freeze", "fear", "numb" };
    public static readonly string[] Buffs =
    {
        "power", "haste", "swift", "guard", "regen", "unstoppable", "ward",
        "bloodlust", "steadfast", "thorns", "furyhide", "carnage", "torrent", "avatar", "laststand", "chill", "focus",
        "next_hit", "next_isolated", "next_crit", "resist", "holy_power", "thrift", "trail", "seed", "martyr",
        "demon_form", "shadow_form", "stealth", "legion", "brutal",
        "ferocity", "determination", "unhindered",
    };
    public static readonly string[] Tags = { "prey", "reaper", "contagion", "deathburst", "eviscerated", "sentence" };
    public static readonly string[] Kinds = Control.Concat(new[] { "vulnerable", "intangible", "barrier" }).Concat(Dot.Keys)
        .Concat(new[] { "weaken", "curse", "mark" }).Concat(Buffs).Concat(Tags).Concat(new[] { "frost" }).ToArray();
    public static readonly string[] BossImmune = { "freeze", "fear" };
    public const double FrostSlow = .005, FrostBossSlow = .30, CorruptionHeal = .20;
    public const int FreezeTicks = 90;
    public static readonly IReadOnlyDictionary<string, double> Caps = new Dictionary<string, double>
    {
        ["slow"] = .70, ["vulnerable"] = 1.0, ["weaken"] = .60, ["guard"] = .60, ["haste"] = 1.0, ["swift"] = .60, ["power"] = 2.0,
        ["resist"] = .5, ["holy_power"] = 2.0, ["thrift"] = 1.0, ["chill"] = .7,
    };
    public static readonly string[] Harmful = Control.Concat(new[] { "vulnerable" }).Concat(Dot.Keys).Concat(new[] { "weaken", "curse", "mark" }).ToArray();
    public const int Tick = 30, MaxStacks = 5, MaxTicks = 6000;
    public const double BossFactor = .5, PressureStep = .25;
    public const int ImmuneAt = 4, PressureDecay = 180;
    public static readonly string[] Saved =
        new[] { "stun", "root", "slow", "vulnerable", "barrier" }.Concat(Dot.Keys)
        .Concat(new[] { "blind", "weaken", "curse", "mark", "prey", "reaper", "deathburst", "frost", "freeze", "numb" }).ToArray();

    private static readonly HashSet<string> KindSet = new(Kinds), ControlSet = new(Control);

    private static StatusMap Rows(ICombatant e) => e.Statuses ??= new StatusMap();

    private static double BossFactorFor(ICombatant target)
    {
        int pressure = target.CcPressure;
        target.CcPressure = pressure + 1; target.CcPressureTimer = PressureDecay;
        return pressure >= ImmuneAt ? 0 : BossFactor * Math.Max(0, 1 - PressureStep * pressure);
    }

    /// <summary>Applies a state; returns the duration actually granted (0 when refused or immune).</summary>
    public static int Apply(ICombatant target, string kind, double ticksIn, double magnitude = 0, ICombatant? source = null)
    {
        if (!KindSet.Contains(kind) || !target.Alive) return 0;
        int ticks = (int)Math.Truncate(ticksIn);
        if (Caps.TryGetValue(kind, out var cap)) magnitude = Math.Min(cap, magnitude);
        bool control = ControlSet.Contains(kind);
        if (control && Has(target, "unstoppable")) return 0;
        if ((kind is "slow" or "root") && (Has(target, "steadfast") || Has(target, "unhindered"))) return 0;
        if (BossImmune.Contains(kind) && target.Boss) return 0;
        if (control && target.Boss) ticks = (int)PyMath.Round(ticks * BossFactorFor(target));
        ticks = Math.Min(MaxTicks, ticks);
        if (ticks <= 0 || !double.IsFinite(magnitude) || magnitude < 0) return 0;
        var rows = Rows(target).GetOrAdd(kind);
        if (Dot.ContainsKey(kind))
        {
            rows.Add(new StatusRow { Ticks = ticks, Magnitude = magnitude, Source = source, Timer = Tick, Carry = 0 });
            if (rows.Count > MaxStacks)
            {
                var weakest = rows[0];
                foreach (var r in rows) if (r.Magnitude * r.Ticks < weakest.Magnitude * weakest.Ticks) weakest = r;
                rows.Remove(weakest);
            }
        }
        else if (rows.Count > 0)
        {
            var row = rows[0];
            row.Ticks = Math.Max(row.Ticks, ticks); row.Magnitude = Math.Max(row.Magnitude, magnitude);
            if (source != null) row.Source = source;
        }
        else rows.Add(new StatusRow { Ticks = ticks, Magnitude = magnitude, Source = source });
        if (kind == "stun" && target.AttackWindup != null) target.AttackWindup = 0;      // interrupts the swing
        return ticks;
    }

    /// <summary>Gelidez builds up; at 100 it freezes (not bosses) and starts again.</summary>
    public static double AddFrost(ICombatant target, double amount)
    {
        if (!target.Alive || amount <= 0) return 0;
        var rows = Rows(target).GetOrAdd("frost");
        if (rows.Count == 0) rows.Add(new StatusRow { Ticks = 240, Magnitude = 0 });
        var row = rows[0];
        row.Magnitude = Math.Min(100, row.Magnitude + amount); row.Ticks = 240;
        if (row.Magnitude >= 100 && !target.Boss)
        {
            rows.Clear(); Rows(target).Remove("frost");
            Apply(target, "freeze", FreezeTicks);
            return 100;
        }
        return row.Magnitude;
    }

    public static double FrostSlowOf(ICombatant target)
    {
        double share = Value(target, "frost") * FrostSlow;
        return target.Boss ? Math.Min(FrostBossSlow, share) : Math.Min(Caps["slow"], share);
    }

    public static bool Has(ICombatant e, string kind) => e.Statuses?.Get(kind) is { Count: > 0 };

    public static double Value(ICombatant e, string kind)
    {
        var rows = e.Statuses?.Get(kind);
        return rows is { Count: > 0 } ? rows.Max(r => r.Magnitude) : 0;
    }

    public static void Remove(ICombatant e, string? kind = null)
    {
        var rows = e.Statuses;
        if (rows == null || rows.Count == 0) return;
        if (kind == null) rows.Clear(); else rows.Remove(kind);
    }

    /// <summary>Removes slow, root and damage over time (Purificação, Tenacidade).</summary>
    public static void Cleanse(ICombatant e)
    {
        foreach (var kind in new[] { "slow", "root" }.Concat(Dot.Keys))
            if (e.Statuses is { Count: > 0 } rows) rows.Remove(kind);
    }

    public static bool IsHarmful(ICombatant e) => e.Statuses != null && Harmful.Any(k => e.Statuses.Get(k) is { Count: > 0 });

    /// <summary>Multiplier on the step cooldown (1.0 = normal speed).</summary>
    public static double MoveFactor(ICombatant e)
    {
        double slow = 1 - (1 - Value(e, "slow")) * (1 - FrostSlowOf(e));
        return slow > 0 ? 1 / (1 - Math.Min(Caps["slow"], slow)) : 1;
    }

    public static ICombatant? TauntTarget(ICombatant e)
    {
        var rows = e.Statuses?.Get("taunt");
        var target = rows is { Count: > 0 } ? rows[0].Source : null;
        return target != null && target.Alive ? target : null;
    }

    /// <summary>Barrier soaks damage first (the layer expiring first goes first); returns what is left.</summary>
    public static double Absorb(ICombatant e, double amount)
    {
        var rows = e.Statuses?.Get("barrier");
        if (rows is not { Count: > 0 } || amount <= 0) return amount;
        double left = amount;
        foreach (var row in rows.OrderBy(r => r.Ticks).ToList())
        {
            double soaked = Math.Min(row.Magnitude, left);
            row.Magnitude -= soaked; left -= soaked;
            e.AbsorbedTotal += soaked;
            if (row.Magnitude <= 0) rows.Remove(row);
            if (left <= 0) break;
        }
        return left;
    }

    /// <summary>One 60 Hz step: damage over time, durations, boss pressure. Returns the damage dealt.</summary>
    public static int TickOnce(ICombatant e)
    {
        if (e.CcPressureTimer > 0)
        {
            e.CcPressureTimer--;
            if (e.CcPressureTimer == 0 && e.CcPressure > 0)
            {
                e.CcPressure--; e.CcPressureTimer = e.CcPressure > 0 ? PressureDecay : 0;
            }
        }
        var map = e.Statuses;
        if (map == null || map.Count == 0) return 0;
        int dealt = 0;
        foreach (var kind in map.Kinds.ToList())
        {
            var list = map.Get(kind);
            if (list == null) continue;
            foreach (var row in list.ToList())
            {
                if (Dot.TryGetValue(kind, out var element) && e.Alive)
                {
                    row.Timer = (row.Timer ?? Tick) - 1;
                    if (row.Timer <= 0)
                    {
                        row.Timer = Tick; row.Carry += row.Magnitude * Tick / 60.0;
                        int amount = (int)Math.Truncate(row.Carry); row.Carry -= amount;
                        if (amount > 0) dealt += Elements.Deal(e, amount, element, row.Source, "status_" + kind);
                    }
                }
                else if (kind == "regen")
                {
                    row.Timer = (row.Timer ?? Tick) - 1;
                    if (row.Timer <= 0 && e.MaxHealth != 0)
                    {
                        row.Timer = Tick;
                        int healed = Math.Min(e.MaxHealth, e.Health + (int)Math.Max(1, PyMath.Round(e.MaxHealth * row.Magnitude * Tick / 60.0)));
                        e.Heal(healed - e.Health);
                    }
                }
                row.Ticks--;
                var current = map.Get(kind);
                if (row.Ticks <= 0 && current != null && current.Contains(row)) current.Remove(row);
            }
            if (map.Get(kind) is not { Count: > 0 }) map.Remove(kind);
        }
        return dealt;
    }

    /// <summary>Saved form: [kind, ticks, magnitude rounded to 4 places] for the kinds that survive a save.</summary>
    public static List<(string Kind, int Ticks, double Magnitude)> Snapshot(ICombatant e)
    {
        var result = new List<(string, int, double)>();
        if (e.Statuses == null) return result;
        foreach (var kind in Saved)
            foreach (var r in e.Statuses.Get(kind) ?? new List<StatusRow>())
                result.Add((kind, r.Ticks, PyMath.Round(r.Magnitude, 4)));
        return result;
    }

    public static void Restore(ICombatant e, IEnumerable<(string Kind, double Ticks, double Magnitude)> data)
    {
        Remove(e);
        foreach (var (kind, ticksIn, magnitudeIn) in data)
        {
            if (!Saved.Contains(kind) || !double.IsFinite(ticksIn) || !double.IsFinite(magnitudeIn)) continue;
            int ticks = Math.Max(0, Math.Min(MaxTicks, (int)Math.Truncate(ticksIn)));
            double magnitude = Math.Max(0, magnitudeIn);
            if (ticks == 0) continue;
            double cap = Caps.TryGetValue(kind, out var c) ? c : magnitude;
            Rows(e).GetOrAdd(kind).Add(new StatusRow { Ticks = ticks, Magnitude = Math.Min(cap, magnitude), Timer = Tick, Carry = 0 });
        }
    }
}
