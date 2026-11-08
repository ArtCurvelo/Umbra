using Umbra.Domain.Combat;
using Umbra.Domain.Data;
using Umbra.Domain.Skills;

namespace Umbra.Domain.Combat.V16;

/// <summary>
/// Class passives of the 0.16 catalogs (6 per class, 3 ranks, bought as `class:&lt;id&gt;`) — port of
/// game/skills/v16/passives.py. Temporary bonuses the passives grant live in SkillState.PassiveBuffs and run down in Tick.
/// Minion passives (minion_health, minion_damage, ally_lost) come with the summons (5.8).
/// </summary>
public static class Passives
{
    public const string Prefix = "class:";
    private static IReadOnlyDictionary<string, IReadOnlyList<PassiveData>> _catalog = new Dictionary<string, IReadOnlyList<PassiveData>>();

    /// <summary>Reads the catalogs and installs the defender hook of typed damage (Pele Endurecida, Guardião).</summary>
    public static void Use(UmbraData data)
    {
        _catalog = data.Skills.ToDictionary(kv => kv.Key, kv => kv.Value.Passives);
        Elements.IncomingFactor = (target, kind) => target is ISkillUser { Tree: not null } p ? IncomingFactor(p, kind) : 1;
    }

    private static IReadOnlyList<PassiveData> Rows(ISkillUser p) => _catalog.TryGetValue(p.PlayerClass, out var rows) ? rows : Array.Empty<PassiveData>();

    public static int Level(ISkillUser p, string passiveId)
    {
        var tree = p.Tree; string key = Prefix + passiveId;
        if (tree == null || !tree.Unlocked.Contains(key)) return 0;
        return Math.Max(0, Math.Min(3, tree.Ranks.TryGetValue(key, out var r) ? r : 1));
    }

    public static double Value(ISkillUser p, string effect)
    {
        double total = 0;
        foreach (var row in Rows(p))
            if (row.Effect == effect) { int n = Level(p, row.Id); if (n != 0) total += row.Values[n - 1]; }
        return total;
    }

    public static bool Owned(ISkillUser p, string effect) => Rows(p).Any(row => row.Effect == effect && Level(p, row.Id) != 0);

    public static string Affinity(ISkillUser p) => p.Skills.Affinity is { Length: > 0 } a ? a : "arcane";

    // ------------------------------------------------------------------ temporary passive bonuses
    public static void Grant(ISkillUser p, string name, double seconds, double amount)
    {
        if (amount == 0) return;
        var rows = p.Skills.PassiveBuffs; var row = (Ticks: (int)PyMath.Round(seconds * 60), Value: amount);
        int i = rows.FindIndex(kv => kv.Key == name);
        if (i >= 0) rows[i] = new(name, row); else rows.Add(new(name, row));
    }

    public static double Active(ISkillUser p, string name)
    {
        var row = p.Skills.PassiveBuffs.FirstOrDefault(kv => kv.Key == name);
        return row.Key != null && row.Value.Ticks > 0 ? row.Value.Value : 0;
    }

    public static double Consume(ISkillUser p, string name)
    {
        var rows = p.Skills.PassiveBuffs; int i = rows.FindIndex(kv => kv.Key == name);
        if (i < 0) return 0;
        var row = rows[i].Value; rows.RemoveAt(i);
        return row.Ticks > 0 ? row.Value : 0;
    }

    // ------------------------------------------------------------------ factors used by the engine
    public static double DamageFactor(Ctx ctx, ITarget? enemy)
    {
        var p = ctx.Player; var spec = ctx.Spec; string element = spec.Str("element", "physical"); double bonus = 1;
        foreach (var (effect, when) in new[] { ("execute", "low:0.25"), ("vs_controlled", "controlled"), ("vs_marked", "status:mark"),
                                               ("vs_cursed", "status:curse"), ("solitary", "alone:4") })
        {
            double amount = Value(p, effect);
            if (amount != 0 && enemy != null && Conditions.Check(ctx, enemy, when)) bonus *= 1 + amount;
        }
        if (element == Affinity(p)) bonus *= 1 + Value(p, "affinity");
        if (element == "shadow") bonus *= 1 + Value(p, "shadow_damage");
        if (spec.List("tags").Contains("bone")) bonus *= 1 + Value(p, "bone_damage");
        foreach (var name in new[] { "streak", "alternate", "convergence" }) bonus *= 1 + Active(p, name);
        if (element == "physical") bonus *= 1 + Active(p, "instinct");
        if (!ctx.State.DodgeUsed) { ctx.State.DodgeUsed = true; bonus *= 1 + Consume(p, "after_dodge"); }
        return bonus;
    }

    public static double DurationFactor(ISkillUser p, string kind) =>
        kind is "slow" or "root" ? 1 + Value(p, "control_duration") : kind == "poison" ? 1 + Value(p, "poison_duration") : 1;

    public static double DotFactor(ISkillUser p, string kind) =>
        kind == "burn" ? 1 + Value(p, "burn_damage") : kind == "decay" ? 1 + Value(p, "decay_damage") : 1;

    public static double HealFactor(ISkillUser p)
    {
        double factor = 1 + Value(p, "healing");
        if (p.Health < p.MaxHealth * .5) factor *= 1 + Value(p, "thirst");
        return factor;
    }

    public static double BarrierFactor(ISkillUser p) => 1 + Value(p, "barrier_strength");

    public static double HealthCostFactor(ISkillUser p) => p.Health > p.MaxHealth * .8 ? 1 - Value(p, "price_of_power") : 1;

    public static double ResourceGainFactor(ISkillUser p) => 1 + Value(p, "resource_gain");

    public static double RegenFactor(ISkillUser p) =>
        Status.Has(p, "martyr") ? 0 : (1 + Value(p, "resource_regen")) * (1 + Status.Value(p, "focus"));

    public static double IncomingFactor(ISkillUser p, string kind)
    {
        double factor = 1;
        if (kind == "physical") factor *= 1 - Value(p, "physical_taken");
        factor *= 1 - Value(p, "guardian") / 2;            // approved: in solo, half of Guardião protects the paladin
        return factor;
    }

    /// <summary>Reação em Cadeia / Tempestade Crescente: (+jumps, +damage per jump).</summary>
    public static (int Jumps, double PerJump) ChainBonus(ISkillUser p) => (Owned(p, "chain_jumps") ? 1 : 0, Value(p, "chain_jumps"));

    // ------------------------------------------------------------------ events
    public static void OnHit(Ctx ctx, ITarget enemy, int dealt)
    {
        var p = ctx.Player; var s = p.Skills;
        if (dealt <= 0) return;
        if (Owned(p, "streak"))
        {
            s.Streak++; s.StreakTicks = 120;
            if (s.Streak >= 5) Grant(p, "streak", 3, Value(p, "streak"));
        }
        if (Owned(p, "alternate"))
        {
            string family = ctx.Spec.Str("category") == "Básica" ? "basic" : "skill";
            if ((s.HasLastFamily ? s.LastFamily : family) != family) Grant(p, "alternate", 4, Value(p, "alternate"));
            s.LastFamily = family; s.HasLastFamily = true;
        }
        if (Owned(p, "solar_oath") && ctx.Spec.StrOrNull("element") == "holy" && ctx.Spec.Str("category") != "Básica")
        {
            if (SkillEnv.Rng.Random() < Value(p, "solar_oath") && !ctx.State.Oath)
            {
                ctx.State.Oath = true;
                foreach (var other in ctx.Near(enemy.Position, 2)) ctx.Damage(other, Math.Max(1, PyMath.Round(Riders.BaseDamage(p) * .6)), "holy");
            }
        }
    }

    public static void OnCast(Ctx ctx)
    {
        var p = ctx.Player;
        if (ctx.Spec.Str("category") == "Mobilidade" && Owned(p, "instinct")) Grant(p, "instinct", 4, Value(p, "instinct"));
        if (Owned(p, "convergence"))
        {
            long now = SkillEnv.Clock();
            var seen = p.Skills.Elements.Where(e => now - e.Time <= 5000).ToList();
            seen.Add((ctx.Spec.StrOrNull("element"), now)); p.Skills.Elements = seen.Skip(Math.Max(0, seen.Count - 8)).ToList();
            if (seen.Select(e => e.Element).Distinct().Count() >= 3) Grant(p, "convergence", 4, Value(p, "convergence"));
        }
    }

    public static void OnDodge(ISkillUser p)
    {
        if (Owned(p, "after_dodge")) Grant(p, "after_dodge", 3, Value(p, "after_dodge"));
    }

    public static void OnKills(ISkillUser p, int count)
    {
        if (count <= 0) return;
        double amount = Value(p, "kill_resource");
        if (amount != 0) p.GainResource((int)Math.Truncate(amount) * count, "passive_kill");
        double ratio = Value(p, "cycle_of_life");
        if (ratio != 0) p.Health = Math.Min(p.MaxHealth, p.Health + (int)Math.Max(1, PyMath.Round(p.MaxHealth * ratio)) * count);
    }

    public static void Tick(ISkillUser p)
    {
        var s = p.Skills; var rows = s.PassiveBuffs;
        for (int i = 0; i < rows.Count; i++)
        {
            var (name, row) = (rows[i].Key, rows[i].Value);
            rows[i] = new(name, (row.Ticks - 1, row.Value));
        }
        rows.RemoveAll(kv => kv.Value.Ticks <= 0);
        if (s.StreakTicks > 0) { s.StreakTicks--; if (s.StreakTicks == 0) s.Streak = 0; }
        if (s.PactTicks > 0) s.PactTicks--;
        if (s.BastionTicks > 0) s.BastionTicks--;
        else if (p.Alive && p.Health < p.MaxHealth * .3)
        {
            double ratio = Value(p, "last_bastion");
            if (ratio != 0)
            {
                Status.Apply(p, "barrier", 300, PyMath.Round(p.MaxHealth * ratio * BarrierFactor(p))); s.BastionTicks = 45 * 60;
            }
        }
    }
}

/// <summary>The class-passive part of ISkillHooks; everything else stays neutral until its step.</summary>
public class PassiveHooks : ISkillHooks
{
    public double PassiveDamage(Ctx ctx, ITarget? enemy) => Passives.DamageFactor(ctx, enemy);
    public double PassiveValue(ISkillUser p, string effect) => Passives.Value(p, effect);
    public double DurationFactor(ISkillUser p, string kind) => Passives.DurationFactor(p, kind);
    public double DotFactor(ISkillUser p, string kind) => Passives.DotFactor(p, kind);
    public double HealFactor(ISkillUser p) => Passives.HealFactor(p);
    public double BarrierFactor(ISkillUser p) => Passives.BarrierFactor(p);
    public double HealthCostFactor(ISkillUser p) => Passives.HealthCostFactor(p);
    public (int Jumps, double PerJump) ChainBonus(ISkillUser p) => Passives.ChainBonus(p);
    public void PassiveOnHit(Ctx ctx, ITarget enemy, int dealt) => Passives.OnHit(ctx, enemy, dealt);
    public void PassiveOnCast(Ctx ctx) => Passives.OnCast(ctx);
    public void PassiveOnKills(ISkillUser p, int count) => Passives.OnKills(p, count);
    public void PassiveOnDodge(ISkillUser p) => Passives.OnDodge(p);
    public void PassiveTick(ISkillUser p) => Passives.Tick(p);
}
