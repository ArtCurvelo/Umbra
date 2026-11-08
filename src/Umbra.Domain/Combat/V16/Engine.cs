using Umbra.Domain.Combat;
using Umbra.Domain.Skills;

namespace Umbra.Domain.Combat.V16;

/// <summary>What one action has done so far (Python's ctx.state dict). "center" keeps Python's semantics: it can be
/// present with no value (a copy of a context that never had one), and then readers get null.</summary>
public sealed class CtxState
{
    public List<ITarget> Targets = new();
    public bool HasCenter;
    public (int X, int Y)? Center;
    public (int X, int Y)? Origin;
    public bool Last, Primed, Crit, CritHit, DodgeUsed, Oath;
    public Dictionary<ITarget, int>? Concentrate;
    public Dictionary<PyDict, int>? Limits;
    public int Healed;
    public int? ResourceBefore, BlockedBefore;

    public void SetCenter((int X, int Y)? center) { HasCenter = true; Center = center; }
    public (int X, int Y)? CenterOr((int X, int Y) fallback) => HasCenter ? Center : fallback;
}

/// <summary>One action of a 0.16 skill — port of engine.Ctx (targets, damage, the after-hit chain).</summary>
public sealed class Ctx
{
    public readonly ISkillUser Player;
    public readonly PyDict Spec;
    public readonly IMap? Map;
    public readonly List<ITarget> AllEnemies, Enemies;
    public readonly CtxState State = new();

    public Ctx(ISkillUser player, PyDict spec, IEnumerable<ITarget>? enemies, IMap? map)
    {
        Player = player; Spec = spec; Map = map;
        AllEnemies = enemies?.ToList() ?? new List<ITarget>();
        Enemies = AllEnemies.Where(e => e.Alive).ToList();
    }

    public bool Safe((int X, int Y) position) => Map != null && Map.Sanctuary(position.X, position.Y);

    public List<ITarget> Near((int X, int Y) position, double radius, ITarget? exclude = null) =>
        Enemies.Where(e => !ReferenceEquals(e, exclude) && e.Alive && Geo.Steps(e.Position, position) <= radius
                           && !Safe(e.Position) && Geo.Clear(Map, position, e.Position)).ToList();

    public bool Occupied((int X, int Y) position, ITarget? ignore = null) =>
        Enemies.Any(e => !ReferenceEquals(e, ignore) && e.Alive && e.Position == position);

    public ITarget? Primary(double reach)
    {
        var p = Player; var rows = Near(p.Position, reach); var aim = p.AimTarget;
        if (rows.Count == 0) return null;
        return aim is { } a
            ? Py.MinBy(rows, e => (double)((e.X - a.X) * (e.X - a.X) + (e.Y - a.Y) * (e.Y - a.Y)))
            : Py.MinBy(rows, e => (Geo.Steps(e.Position, p.Position), e.Health, e.Y, e.X));
    }

    // -------------------------------------------------------------- damage
    public int Amount(ITarget enemy, double factor)
    {
        var p = Player; var spec = Spec; var h = SkillEnv.Hooks;
        double @base = spec.Str("category") == "Básica" ? p.GetAttackDamage(enemy, false) : p.GetAttackDamage(enemy, true) * spec.Num("multiplier", 1);
        double value = @base * factor * Conditions.Factor(this, enemy) * h.PassiveDamage(this, enemy) * h.TagDamage(p, spec);
        value *= h.WarlockDamage(p, spec, enemy);
        value *= h.NecroDamage(this, enemy);
        if (spec.T("concentrate"))
        {
            var seen = State.Concentrate ??= new Dictionary<ITarget, int>(ReferenceEqualityComparer.Instance as IEqualityComparer<ITarget>);
            int n = seen.GetValueOrDefault(enemy); seen[enemy] = n + 1;
            value *= 1 + spec.Num("concentrate") * n;
        }
        if (spec.T("ramp"))
        {
            var (last, n) = p.Skills.Ramp; n = ReferenceEquals(last, enemy) ? n + 1 : 0;
            p.Skills.Ramp = (enemy, n); p.Skills.HasRamp = true;
            value *= 1 + Math.Min(spec.Num("ramp_max", .25), spec.Num("ramp") * n);
        }
        if (!State.Primed)                                              // one-shot hero bonuses: the first hit of an action
        {
            State.Primed = true;
            value *= 1 + Take(p, "next_hit");
            if (Conditions.Check(this, enemy, "isolated")) value *= 1 + Take(p, "next_isolated");
            if (HasTake(p, "next_crit")) { value *= 1.5; State.Crit = true; }
        }
        if (!State.Crit)
        {
            double chance = h.PassiveValue(p, "crit") + h.Grimoire(p, "crit") + h.Affix(p, "crit")
                            + spec.Rows("crits").Where(r => Conditions.Check(this, enemy, r.Str("when"))).Sum(r => r.Num("chance"));
            if (chance != 0 && SkillEnv.Rng.Random() < chance) { value *= 1.5 + h.Grimoire(p, "crit_damage"); State.CritHit = true; }
        }
        return (int)Math.Max(1, PyMath.Round(value));
    }

    public int Damage(ITarget enemy, double raw, string? element = null, bool riders = false)
    {
        element = element ?? Spec.StrOrNull("element") ?? "physical"; int whole = (int)Math.Truncate(raw);
        string id = Spec.Str("id"); int dealt;
        if (Spec.T("split_element") && whole > 1)
        {
            dealt = Elements.Deal(enemy, whole / 2, Spec.Str("split_element"), Player, id);
            dealt += enemy.Alive ? Elements.Deal(enemy, whole - whole / 2, element, Player, id) : 0;
        }
        else dealt = Elements.Deal(enemy, whole, element, Player, id);
        if (riders) After(enemy, dealt);
        return dealt;
    }

    public int Hit(ITarget enemy, double factor = 1, string? element = null, bool riders = true)
    {
        if (!enemy.Alive) return 0;
        if (!State.Targets.Any(t => ReferenceEquals(t, enemy))) State.Targets.Add(enemy);
        if (Spec.Num("multiplier", 1) <= 0 && Spec.Str("category") != "Básica")
        {
            if (riders) After(enemy, 0);
            return 0;
        }
        return Damage(enemy, Amount(enemy, factor), element, riders);
    }

    public void After(ITarget enemy, int dealt)
    {
        var p = Player; var spec = Spec; var h = SkillEnv.Hooks;
        Riders.Run(this, "hit", enemy, dealt);
        if (dealt > 0)
        {
            if (spec.T("hit_heal"))
            {
                int over = Riders.Heal(p, p.MaxHealth * spec.Num("hit_heal"));
                if (over != 0 && spec.T("overheal_barrier"))
                {
                    double cap = p.MaxHealth * spec.Num("overheal_barrier");
                    Status.Apply(p, "barrier", 300, PyMath.Round(Math.Min(cap, Status.Value(p, "barrier") + over)));
                }
            }
            if (Status.Has(p, "bloodlust")) Riders.Heal(p, p.MaxHealth * Status.Value(p, "bloodlust"));
            SkillEngine.Imbue(this, enemy);
            h.PassiveOnHit(this, enemy, dealt);
            h.WarlockOnHit(this, enemy, dealt);
            h.NecroOnHit(this, enemy, dealt);
        }
        if (!enemy.Alive) Riders.Run(this, "kill", enemy, dealt);
    }

    private static double Take(ISkillUser p, string kind)
    {
        var rows = p.Statuses?.Get(kind);
        if (rows == null) return 0;
        p.Statuses!.Remove(kind);
        return rows.Count > 0 ? rows.Max(r => r.Magnitude) : 0;
    }

    private static bool HasTake(ISkillUser p, string kind)
    {
        var rows = p.Statuses?.Get(kind);
        if (rows == null) return false;
        p.Statuses!.Remove(kind);
        return rows.Count > 0;
    }
}

/// <summary>Casting a 0.16 skill — port of engine.py (costs, cooldowns, charges, cast, strike). Shapes: Shapes.cs.</summary>
public static class SkillEngine
{
    /// <summary>Imbuição Sombria: the next attacks carry the imbuement's states and riders.</summary>
    public static void Imbue(Ctx ctx, ITarget enemy)
    {
        var p = ctx.Player; var row = p.Skills.Imbue;
        if (row == null || ctx.Spec.Str("id") == row.Value.Spec.Str("id")) return;
        var (spec, charges) = row.Value;
        var other = new Ctx(p, spec, ctx.Enemies, ctx.Map); other.State.Targets = new List<ITarget> { enemy };
        foreach (var rider in spec.Rows("states"))
            if (Riders.Allowed(other, rider, enemy)) Riders.Act(other, rider, enemy, 0);
        Riders.Run(other, "imbue", enemy, 0);
        p.Skills.Imbue = charges > 1 ? (spec, charges - 1) : null;
    }

    // ------------------------------------------------------------------ costs, cooldowns, charges
    public static bool Ready(ISkillUser p, PyDict spec)
    {
        if (spec.T("return_window") && p.Skills.Return != null) return true;
        string id = spec.Str("id");
        if (spec.Num("charges") > 1) return (p.Skills.Charges.TryGetValue(id, out var held) ? held : spec.Num("charges")) > 0;
        if (!spec.T("cooldown")) return true;
        return p.Skills.Cooldowns.GetValueOrDefault(id) <= 0;
    }

    public static int CostOf(ISkillUser p, PyDict spec)
    {
        if (Status.Has(p, "torrent")) return 0;
        return (int)PyMath.Round(Py.Int(spec.Num("cost")) * (1 - Status.Value(p, "thrift")) * (1 - SkillEnv.Hooks.Grimoire(p, "cost")));
    }

    public static bool Affordable(ISkillUser p, PyDict spec)
    {
        if (spec.T("health_cost")) return p.Health > p.MaxHealth * spec.Num("health_cost") + 1;
        return p.Resource >= CostOf(p, spec);
    }

    public static bool Pay(ISkillUser p, PyDict spec)
    {
        if (spec.T("health_cost"))
        {
            double ratio = spec.Num("health_cost") * SkillEnv.Hooks.HealthCostFactor(p);
            p.Health = Math.Max(1, p.Health - (int)Math.Max(1, PyMath.Round(p.MaxHealth * ratio)));
            if (!spec.T("cost")) return true;
        }
        int cost = CostOf(p, spec);
        if (!p.SpendResource(cost)) return false;
        if (cost != 0 && Status.Has(p, "furyhide"))                 // Couro de Fúria: spent Fúria thickens the barrier
        {
            var rows = p.Statuses!.Get("barrier");
            if (rows is { Count: > 0 }) rows[0].Magnitude += PyMath.Round(p.MaxHealth * Status.Value(p, "furyhide") * cost / 10);
        }
        return true;
    }

    public static void StartCooldown(ISkillUser p, PyDict spec)
    {
        string id = spec.Str("id"); var s = p.Skills;
        int duration = (int)PyMath.Round(Py.Int(spec.Num("cooldown")) * (1 - SkillEnv.Hooks.Affix(p, "cooldown")));
        if (spec.Num("charges") > 1)
        {
            s.Charges[id] = (s.Charges.TryGetValue(id, out var held) ? held : Py.Int(spec.Num("charges"))) - 1;
            if (duration != 0 && s.Cooldowns.GetValueOrDefault(id) <= 0) s.Cooldowns[id] = duration;
            SkillWorld.RegisterCharges(p, spec);
        }
        else if (duration != 0) s.Cooldowns[id] = duration;
        else s.Cooldowns.Remove(id);
        s.Cooldown = duration;
    }

    // ------------------------------------------------------------------ cast and strike
    public static bool Cast(ISkillUser player, PyDict spec, IEnumerable<ITarget> enemies, IMap? map)
    {
        var h = SkillEnv.Hooks;
        if (!player.Alive || !Ready(player, spec) || !Affordable(player, spec)) return false;
        if (!h.WarlockAllowed(player, spec)) return false;               // greater demons need their shard active
        if (map != null && map.Sanctuary(player.X, player.Y)) return false;
        var ctx = new Ctx(player, spec, enemies, map); bool paid = false;
        if (!h.NecroBeforeCast(ctx)) return false;

        bool Commit()
        {
            if (paid) return true;
            ctx.State.ResourceBefore = player.Resource;
            if (!Pay(player, spec)) return false;
            paid = true; Riders.Run(ctx, "cast"); return true;
        }

        var outcome = Shapes.Run(ctx, Commit);
        if (outcome == null) return false;
        if (outcome == Outcome.Off) return true;                          // aura switched off: free, no cooldown
        if (outcome == Outcome.Free) { player.Skills.LastSkillId = spec.Str("id"); return true; }
        if (outcome == Outcome.Fail || !paid) return false;
        Riders.Run(ctx, "after");
        StartCooldown(player, spec);
        if (spec.T("resource_gain") && spec.Str("category") != "Básica") player.GainResource(Py.Int(spec.Num("resource_gain")), "v16_cast");
        h.PassiveOnCast(ctx);
        h.WarlockOnCast(ctx);
        h.NecroAfterCast(ctx);
        var s = player.Skills;
        s.LastSkillId = spec.Str("id"); s.LastSkillAnim = spec.Str("anim", spec.Str("id"));
        var targets = ctx.State.Targets;
        s.TargetPositions.Clear();
        if (targets.Count > 0) s.TargetPositions.AddRange(targets.Select(e => ((int, int)?)e.Position));
        else s.TargetPositions.Add(ctx.State.CenterOr(player.Position));
        if (targets.Count > 0) s.Direction = Geo.Direction(player.Position, targets[0].Position, player.AttackDirection);
        return true;
    }

    /// <summary>Basic attack on the enemy standing on the marked tile (after the wind-up); returns the damage dealt to it.</summary>
    public static int Strike(ISkillUser player, PyDict spec, ITarget enemy, IEnumerable<ITarget> enemies, IMap? map, double factor = 1)
    {
        var ctx = new Ctx(player, spec, enemies, map);
        SkillEnv.Hooks.WarlockBroke(player);                              // attacking ends Furtividade
        int dealt = ctx.Hit(enemy, factor, riders: false);
        if (dealt <= 0) return 0;
        ctx.After(enemy, dealt);
        var facing = Geo.Direction(player.Position, enemy.Position);
        if (spec.T("sides"))
        {
            var side = (X: -facing.Y, Y: facing.X);
            foreach (var cell in new[] { (enemy.X + side.X, enemy.Y + side.Y), (enemy.X - side.X, enemy.Y - side.Y) })
            {
                var other = ctx.Enemies.FirstOrDefault(e => e.Alive && e.Position == cell);
                if (other != null) ctx.Hit(other, spec.Num("sides"), riders: false);
            }
        }
        if (spec.T("pierce"))
        {
            var line = Geo.LineCells(enemy.Position, facing, spec.Num("range"));
            var behind = ctx.Enemies.Where(e => e.Alive && !ReferenceEquals(e, enemy) && line.Contains(e.Position)).ToList();
            foreach (var other in behind.OrderBy(e => Geo.Steps(e.Position, enemy.Position)).Take(Py.Int(spec.Num("pierce"))))
                ctx.Hit(other, spec.Num("pierce_factor", .7), riders: false);
        }
        if (spec.T("ricochet"))
        {
            var last = enemy; var done = new List<ITarget> { enemy };
            for (int i = 0; i < Py.Int(spec.Num("ricochet")); i++)
            {
                var from = last;
                var pool = ctx.Near(from.Position, 3).Where(e => !done.Contains(e)).ToList();
                if (pool.Count == 0) break;
                last = Py.MinBy(pool, e => Geo.Steps(e.Position, from.Position)); done.Add(last);
                ctx.Hit(last, spec.Num("ricochet_factor", .6), riders: false);
            }
        }
        if (spec.T("fan"))
        {
            var others = ctx.Near(player.Position, spec.Num("range"), enemy).OrderBy(e => Geo.Steps(e.Position, player.Position)).ToList();
            foreach (var other in others.Where(e => Geo.InCone(player.Position, facing, spec.Num("range"), e.Position, .3)).Take(Py.Int(spec.Num("fan"))))
                ctx.Hit(other, 1, riders: false);
        }
        if (spec.T("cone"))
            foreach (var other in ctx.Near(player.Position, spec.Num("cone"), enemy).Where(e => Geo.InCone(player.Position, facing, spec.Num("cone"), e.Position)).ToList())
                ctx.Hit(other, 1, riders: false);
        double area = spec.T("aoe") ? spec.Num("aoe") : Status.Has(player, "avatar") ? 1 : 0;
        if (area != 0)
        {
            ctx.State.SetCenter(enemy.Position);
            foreach (var other in ctx.Near(enemy.Position, area, enemy)) ctx.Hit(other, spec.Num("aoe_factor", 1), riders: false);
        }
        return dealt;
    }
}
